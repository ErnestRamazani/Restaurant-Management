using System.Globalization;
using System.Linq;
using EliteRestaurant.Core.Models;

namespace EliteRestaurant.Core.Utils;

/// <summary>Normalized payroll parameters from <see cref="SalarySettings"/> or <see cref="PublicMenuSetting"/>.</summary>
public readonly record struct SalaryPayrollRules(
    int LateDaysPerAttendanceUnit,
    bool AbsenceCountsAsAttendanceUnit,
    decimal SalesBonusPercent,
    decimal MaxSalaryAdvancePercentOfGross)
{
    public decimal SalesBonusFraction => SalesBonusPercent / 100m;

    public decimal MaxAdvanceFraction => MaxSalaryAdvancePercentOfGross / 100m;

    public static SalaryPayrollRules FromSalarySettings(SalarySettings? s)
    {
        s ??= new SalarySettings();
        var late = s.LateDaysPerAttendanceUnit < 1 ? 4 : s.LateDaysPerAttendanceUnit;
        var bonus = Math.Clamp(s.SalesBonusPercent, 0m, 100m);
        var advance = Math.Clamp(s.MaxSalaryAdvancePercentOfGross, 0m, 100m);
        return new SalaryPayrollRules(late, s.AbsenceCountsAsAttendanceUnit, bonus, advance);
    }

    public static SalaryPayrollRules FromPublicMenuRow(PublicMenuSetting? row) =>
        row is null ? FromSalarySettings(null) : FromSalarySettings(new SalarySettings
        {
            LateDaysPerAttendanceUnit = row.PayrollLateDaysPerAttendanceUnit,
            AbsenceCountsAsAttendanceUnit = row.PayrollAbsenceCountsAsAttendanceUnit,
            SalesBonusPercent = row.PayrollSalesBonusPercent,
            MaxSalaryAdvancePercentOfGross = row.PayrollMaxSalaryAdvancePercentOfGross
        });
}

public static class PayrollCalculator
{
    /// <summary>Legacy default bonus rate (5% of merchandise). Prefer <see cref="SalaryPayrollRules.SalesBonusFraction"/>.</summary>
    public const decimal LegacyBonusPercentOfSales = 0.05m;

    /// <summary>Resolved rules from this machine’s <c>app-settings.json</c> (desktop sync client).</summary>
    public static SalaryPayrollRules ResolveSalaryPayrollRulesForLocalFile() =>
        SalaryPayrollRules.FromSalarySettings(SettingsManager.Load().Salary);

    /// <summary>Salary advances for a payroll month cannot exceed this fraction of the payroll gross base (monthly prorated or hourly × scheduled hours).</summary>
    public static decimal MaxAdvanceFractionOfScheduledGross(SalaryPayrollRules rules) => rules.MaxAdvanceFraction;

    /// <summary>Resolved base gross and schedule context for one employee in a calendar month.</summary>
    public readonly record struct PayrollMonthBase(
        decimal ScheduledHours,
        int ScheduledWorkdays,
        decimal GrossPayUsd,
        bool UsesMonthlySalary,
        decimal ContractMonthlySalaryUsd,
        /// <summary>Divisor for attendance deductions (<see cref="ComputeBaseAfterAttendanceUsd"/>).</summary>
        int AttendanceDenominatorWorkdays);

    /// <summary>
    /// Calendar proration: full <paramref name="employee"/>.<see cref="Employee.MonthlySalaryUSD"/> when employed the whole month;
    /// otherwise scale by days from join through month end.
    /// </summary>
    public static decimal GetProratedMonthlyGrossUsd(Employee employee, int year, int month)
    {
        if (employee.MonthlySalaryUSD <= 0m)
            return 0m;

        var daysInMonth = DateTime.DaysInMonth(year, month);
        var monthStart = new DateTime(year, month, 1).Date;
        var monthEnd = new DateTime(year, month, daysInMonth).Date;
        var join = employee.JoinDate.Date;
        if (join > monthEnd)
            return 0m;

        var firstEmployed = join > monthStart ? join : monthStart;
        var employedDays = (monthEnd - firstEmployed).Days + 1;
        return Math.Round(employee.MonthlySalaryUSD * employedDays / daysInMonth, 2);
    }

    /// <summary>
    /// When <see cref="Employee.MonthlySalaryUSD"/> is set, that path is primary. Otherwise hourly × scheduled hours.
    /// Attendance deductions use scheduled workdays when any exist; if the employee is monthly-only with no shifts,
    /// employed calendar days in the month are used so net pay math stays stable.
    /// </summary>
    public static PayrollMonthBase ResolvePayrollMonthBase(Employee employee, int year, int month)
    {
        var hourly = GetHourlyGrossForPayrollMonth(employee, year, month);
        if (employee.MonthlySalaryUSD <= 0m)
        {
            var denomHourly = hourly.ScheduledWorkdays > 0 ? hourly.ScheduledWorkdays : 1;
            return new PayrollMonthBase(
                hourly.ScheduledHours,
                hourly.ScheduledWorkdays,
                hourly.GrossPayUsd,
                false,
                0m,
                denomHourly);
        }

        var gross = GetProratedMonthlyGrossUsd(employee, year, month);
        var denom = hourly.ScheduledWorkdays > 0
            ? hourly.ScheduledWorkdays
            : Math.Max(1, CountEmployedCalendarDaysInMonth(employee, year, month));

        return new PayrollMonthBase(
            hourly.ScheduledHours,
            hourly.ScheduledWorkdays,
            gross,
            true,
            employee.MonthlySalaryUSD,
            denom);
    }

    private static int CountEmployedCalendarDaysInMonth(Employee employee, int year, int month)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var monthStart = new DateTime(year, month, 1).Date;
        var monthEnd = new DateTime(year, month, daysInMonth).Date;
        var join = employee.JoinDate.Date;
        if (join > monthEnd)
            return 0;

        var firstEmployed = join > monthStart ? join : monthStart;
        return (monthEnd - firstEmployed).Days + 1;
    }

    /// <summary>
    /// Scheduled shift hours and gross pay for the calendar month (after join date): sum of shift lengths on non-Off days × hourly rate.
    /// </summary>
    public static (decimal ScheduledHours, int ScheduledWorkdays, decimal GrossPayUsd) GetHourlyGrossForPayrollMonth(
        Employee employee,
        int year,
        int month)
    {
        var start = new DateTime(year, month, 1).Date;
        var end = new DateTime(year, month, DateTime.DaysInMonth(year, month)).Date;
        var join = employee.JoinDate.Date;

        decimal hours = 0m;
        var workdays = 0;
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (d < join)
                continue;

            var shift = AttendanceScheduleHelper.ResolveShiftWindow(
                employee,
                d,
                AttendanceShiftSchedule.FromSettings(SettingsManager.Load().Attendance));
            if (shift.IsOff)
                continue;

            var span = shift.End - shift.Start;
            if (span <= TimeSpan.Zero)
                continue;

            hours += (decimal)span.TotalHours;
            workdays++;
        }

        var rate = employee.HourlyRate;
        var gross = Math.Round(Math.Max(0m, rate) * hours, 2);
        return (hours, workdays, gross);
    }

    /// <summary>
    /// Per scheduled workday in the month: absence if no row, marked absent, or no clock-in; late if clocked in late.
    /// Total units = (optional) absence days + floor(late days ÷ <see cref="SalaryPayrollRules.LateDaysPerAttendanceUnit"/>).
    /// </summary>
    public static (int AbsenceDays, int LateDays, int LatePenaltyAbsences, int TotalDeductionUnits)
        CountAttendanceUnitsForPayroll(
            Employee employee,
            int year,
            int month,
            IEnumerable<EmployeeAttendance> monthRows,
            SalaryPayrollRules rules)
    {
        var start = new DateTime(year, month, 1).Date;
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var end = new DateTime(year, month, daysInMonth).Date;
        var join = employee.JoinDate.Date;

        var byDate = monthRows
            .GroupBy(a => a.WorkDate.Date)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.Id).First());

        var absenceDays = 0;
        var lateDays = 0;
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (d < join)
                continue;

            var shift = AttendanceScheduleHelper.ResolveShiftWindow(
                employee,
                d,
                AttendanceShiftSchedule.FromSettings(SettingsManager.Load().Attendance));
            if (shift.IsOff)
                continue;

            if (!byDate.TryGetValue(d, out var att))
            {
                absenceDays++;
                continue;
            }

            if (att.IsAbsence || att.ClockInTime is null)
            {
                absenceDays++;
                continue;
            }

            if (string.Equals(att.ClockInStatus, "Late", StringComparison.OrdinalIgnoreCase))
                lateDays++;
        }

        var divisor = Math.Max(1, rules.LateDaysPerAttendanceUnit);
        var latePenalty = lateDays / divisor;
        var absenceUnits = rules.AbsenceCountsAsAttendanceUnit ? absenceDays : 0;
        var total = absenceUnits + latePenalty;
        return (absenceDays, lateDays, latePenalty, total);
    }

    public static decimal ComputeBonusUsd(decimal moneyGeneratedUsd, SalaryPayrollRules rules)
        => Math.Round(Math.Max(0m, moneyGeneratedUsd) * rules.SalesBonusFraction, 2);

    /// <summary>
    /// After attendance deductions, add sales bonus, subtract advances. Result is never negative.
    /// Each deduction unit costs one average scheduled workday: gross ÷ scheduled workdays.
    /// </summary>
    public static decimal ComputeFinalNetPayUsd(
        decimal grossPayUsd,
        int scheduledWorkdays,
        int totalDeductionUnits,
        decimal moneyGeneratedUsd,
        decimal advancesToDeductUsd,
        SalaryPayrollRules rules)
    {
        var afterAttendance = ComputeBaseAfterAttendanceUsd(grossPayUsd, scheduledWorkdays, totalDeductionUnits);
        var bonus = ComputeBonusUsd(moneyGeneratedUsd, rules);
        var net = afterAttendance + bonus - Math.Max(0m, advancesToDeductUsd);
        return Math.Round(Math.Max(0m, net), 2);
    }

    public static decimal ComputeBaseAfterAttendanceUsd(
        decimal grossPayUsd,
        int scheduledWorkdays,
        int totalDeductionUnits)
    {
        if (grossPayUsd <= 0m || scheduledWorkdays <= 0)
            return 0m;

        var perUnit = grossPayUsd / scheduledWorkdays;
        return Math.Round(Math.Max(0m, grossPayUsd - totalDeductionUnits * perUnit), 2);
    }

    public static string FormatPayrollMonthLabel(int year, int month)
        => new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
}
