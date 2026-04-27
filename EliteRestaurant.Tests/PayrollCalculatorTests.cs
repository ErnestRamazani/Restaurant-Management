using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Xunit;

namespace EliteRestaurant.Tests;

public class PayrollCalculatorTests
{
    [Fact]
    public void ComputeBonusUsd_IsFivePercentOfSales()
    {
        Assert.Equal(5m, PayrollCalculator.ComputeBonusUsd(100m));
        Assert.Equal(0m, PayrollCalculator.ComputeBonusUsd(-10m));
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
        var net = PayrollCalculator.ComputeFinalNetPayUsd(100m, 5, 10, 0m, 500m);
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
}
