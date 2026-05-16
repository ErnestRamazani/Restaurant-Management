using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Xunit;

namespace EliteRestaurant.Tests;

public class PayrollCalculatorTests
{
    private static readonly SalaryPayrollRules DefaultRules = SalaryPayrollRules.FromSalarySettings(null);

    [Fact]
    public void ComputeBonusUsd_IsFivePercentOfSalesWithDefaultRules()
    {
        Assert.Equal(5m, PayrollCalculator.ComputeBonusUsd(100m, DefaultRules));
        Assert.Equal(0m, PayrollCalculator.ComputeBonusUsd(-10m, DefaultRules));
    }

    [Fact]
    public void ComputeBonusUsd_UsesCustomSalesPercent()
    {
        var rules = SalaryPayrollRules.FromSalarySettings(new SalarySettings { SalesBonusPercent = 10m });
        Assert.Equal(10m, PayrollCalculator.ComputeBonusUsd(100m, rules));
    }

    [Fact]
    public void ComputeBaseAfterAttendanceUsd_DeductsUnitsFromGross()
    {
        var after = PayrollCalculator.ComputeBaseAfterAttendanceUsd(1000m, 10, 2);
        Assert.Equal(800m, after);
    }

    [Fact]
    public void ComputeFinalNetPayUsd_NeverNegative()
    {
        var net = PayrollCalculator.ComputeFinalNetPayUsd(100m, 5, 10, 0m, 500m, DefaultRules);
        Assert.Equal(0m, net);
    }

    [Fact]
    public void GetHourlyGrossForPayrollMonth_CountsScheduledShifts()
    {
        var emp = new Employee
        {
            JoinDate = new DateTime(2020, 1, 1),
            HourlyRate = 10m,
            MondayShift = "Morning",
            TuesdayShift = "Off",
            WednesdayShift = "Off",
            ThursdayShift = "Off",
            FridayShift = "Off",
            SaturdayShift = "Off",
            SundayShift = "Off"
        };
        var (hours, workdays, gross) = PayrollCalculator.GetHourlyGrossForPayrollMonth(emp, 2026, 4);
        Assert.True(workdays >= 4);
        Assert.True(hours > 0);
        Assert.Equal(Math.Round(10m * hours, 2), gross);
    }

    [Fact]
    public void GetProratedMonthlyGrossUsd_FullMonthWhenJoinedBeforeMonth()
    {
        var emp = new Employee
        {
            JoinDate = new DateTime(2020, 1, 1),
            MonthlySalaryUSD = 3000m
        };
        Assert.Equal(3000m, PayrollCalculator.GetProratedMonthlyGrossUsd(emp, 2026, 4));
    }

    [Fact]
    public void GetProratedMonthlyGrossUsd_ProratesWhenJoiningMidMonth()
    {
        var emp = new Employee
        {
            JoinDate = new DateTime(2026, 4, 16),
            MonthlySalaryUSD = 3100m
        };
        // April 2026: 30 days; join 16th → 15 days employed → 3100 * 15/30 = 1550
        Assert.Equal(1550m, PayrollCalculator.GetProratedMonthlyGrossUsd(emp, 2026, 4));
    }

    [Fact]
    public void ResolvePayrollMonthBase_PrefersMonthlyWhenSet()
    {
        var emp = new Employee
        {
            JoinDate = new DateTime(2020, 1, 1),
            HourlyRate = 50m,
            MonthlySalaryUSD = 4000m,
            MondayShift = "Morning",
            TuesdayShift = "Morning",
            WednesdayShift = "Off",
            ThursdayShift = "Off",
            FridayShift = "Off",
            SaturdayShift = "Off",
            SundayShift = "Off"
        };
        var b = PayrollCalculator.ResolvePayrollMonthBase(emp, 2026, 5);
        Assert.True(b.UsesMonthlySalary);
        Assert.Equal(4000m, b.GrossPayUsd);
        Assert.True(b.ScheduledWorkdays > 0);
        Assert.Equal(b.ScheduledWorkdays, b.AttendanceDenominatorWorkdays);
    }

    [Fact]
    public void ResolvePayrollMonthBase_HourlyWhenMonthlyZero()
    {
        var emp = new Employee
        {
            JoinDate = new DateTime(2020, 1, 1),
            HourlyRate = 20m,
            MonthlySalaryUSD = 0m,
            MondayShift = "Morning",
            TuesdayShift = "Off",
            WednesdayShift = "Off",
            ThursdayShift = "Off",
            FridayShift = "Off",
            SaturdayShift = "Off",
            SundayShift = "Off"
        };
        var b = PayrollCalculator.ResolvePayrollMonthBase(emp, 2026, 5);
        Assert.False(b.UsesMonthlySalary);
        Assert.True(b.GrossPayUsd > 0m);
        Assert.Equal(b.ScheduledWorkdays, b.AttendanceDenominatorWorkdays);
    }

    [Fact]
    public void CountAttendanceUnitsForPayroll_LatePenaltyUsesConfiguredDivisor()
    {
        var rules = SalaryPayrollRules.FromSalarySettings(new SalarySettings { LateDaysPerAttendanceUnit = 2 });
        var emp = new Employee
        {
            JoinDate = new DateTime(2026, 6, 1),
            WednesdayShift = "Morning",
            MondayShift = "Off",
            TuesdayShift = "Off",
            ThursdayShift = "Off",
            FridayShift = "Off",
            SaturdayShift = "Off",
            SundayShift = "Off"
        };
        var clock = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var rows = new[]
        {
            new EmployeeAttendance { Id = 1, WorkDate = new DateTime(2026, 6, 3), ClockInTime = clock, IsAbsence = false, ClockInStatus = "Late" },
            new EmployeeAttendance { Id = 2, WorkDate = new DateTime(2026, 6, 10), ClockInTime = clock, IsAbsence = false, ClockInStatus = "Late" },
            new EmployeeAttendance { Id = 3, WorkDate = new DateTime(2026, 6, 17), ClockInTime = clock, IsAbsence = false, ClockInStatus = "Late" },
            new EmployeeAttendance { Id = 4, WorkDate = new DateTime(2026, 6, 24), ClockInTime = clock, IsAbsence = false, ClockInStatus = "OnTime" }
        };
        var (absenceDays, lateDays, latePenalty, total) =
            PayrollCalculator.CountAttendanceUnitsForPayroll(emp, 2026, 6, rows, rules);
        Assert.Equal(0, absenceDays);
        Assert.Equal(3, lateDays);
        Assert.Equal(1, latePenalty);
        Assert.Equal(1, total);
    }

    [Fact]
    public void CountAttendanceUnitsForPayroll_CanDisableAbsenceUnits()
    {
        var rules = SalaryPayrollRules.FromSalarySettings(new SalarySettings
        {
            AbsenceCountsAsAttendanceUnit = false,
            LateDaysPerAttendanceUnit = 4
        });
        var emp = new Employee
        {
            JoinDate = new DateTime(2026, 6, 1),
            MondayShift = "Morning",
            TuesdayShift = "Off",
            WednesdayShift = "Off",
            ThursdayShift = "Off",
            FridayShift = "Off",
            SaturdayShift = "Off",
            SundayShift = "Off"
        };
        var (absenceDays, lateDays, latePenalty, total) =
            PayrollCalculator.CountAttendanceUnitsForPayroll(emp, 2026, 6, Array.Empty<EmployeeAttendance>(), rules);
        Assert.True(absenceDays > 0);
        Assert.Equal(0, lateDays);
        Assert.Equal(0, latePenalty);
        Assert.Equal(0, total);
    }
}
