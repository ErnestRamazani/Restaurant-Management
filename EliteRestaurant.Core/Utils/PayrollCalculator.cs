using System.Globalization;
using System.Linq;
using EliteRestaurant.Core.Models;

namespace EliteRestaurant.Core.Utils;

public static class PayrollCalculator
{
    public const decimal BonusPercentOfSales = 0.05m;

    /// <summary>Salary advances for a payroll month cannot exceed this fraction of scheduled gross (hourly × scheduled shift hours).</summary>
    public const decimal MaxAdvanceFractionOfScheduledGross = 0.30m;

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
    /// Total units = absence days + floor(late days ÷ 4).
    /// </summary>
    public static (int AbsenceDays, int LateDays, int LatePenaltyAbsences, int TotalDeductionUnits)
        CountAttendanceUnitsForPayroll(Employee employee, int year, int month, IEnumerable<EmployeeAttendance> monthRows)
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

        var latePenalty = lateDays / 4;
        var total = absenceDays + latePenalty;
        return (absenceDays, lateDays, latePenalty, total);
    }

    public static decimal ComputeBonusUsd(decimal moneyGeneratedUsd)
        => Math.Round(Math.Max(0m, moneyGeneratedUsd) * BonusPercentOfSales, 2);

    /// <summary>
    /// After attendance deductions, add sales bonus, subtract advances. Result is never negative.
    /// Each deduction unit costs one average scheduled workday: gross ÷ scheduled workdays.
    /// </summary>
    public static decimal ComputeFinalNetPayUsd(
        decimal grossPayUsd,
        int scheduledWorkdays,
        int totalDeductionUnits,
        decimal moneyGeneratedUsd,
        decimal advancesToDeductUsd)
    {
        var afterAttendance = ComputeBaseAfterAttendanceUsd(grossPayUsd, scheduledWorkdays, totalDeductionUnits);
        var bonus = ComputeBonusUsd(moneyGeneratedUsd);
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
