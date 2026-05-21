using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Xunit;

namespace EliteRestaurant.Core.Tests;

public class AttendanceScheduleHelperTests
{
    private static readonly AttendanceShiftSchedule Schedule = new(
        new TimeSpan(12, 0, 0),
        new TimeSpan(18, 0, 0),
        new TimeSpan(18, 0, 0),
        new TimeSpan(23, 0, 0));

    [Theory]
    [InlineData("Full Day")]
    [InlineData("full day")]
    [InlineData("Full Day Shift")]
    public void ResolveShiftWindow_FullDay_UsesMorningStartThroughNightEnd(string configured)
    {
        var emp = new Employee { TuesdayShift = configured };
        var window = AttendanceScheduleHelper.ResolveShiftWindow(emp, new DateTime(2026, 5, 19), Schedule);

        Assert.False(window.IsOff);
        Assert.Equal("Full Day", window.Name);
        Assert.Equal(Schedule.MorningStart, window.Start);
        Assert.Equal(Schedule.NightEnd, window.End);
    }

    [Fact]
    public void IsFullDayShift_DoesNotMatchMorningOrOff()
    {
        Assert.False(AttendanceScheduleHelper.IsFullDayShift("Morning"));
        Assert.False(AttendanceScheduleHelper.IsFullDayShift("Morning Shift"));
        Assert.False(AttendanceScheduleHelper.IsFullDayShift("Off"));
        Assert.True(AttendanceScheduleHelper.IsFullDayShift("Full Day"));
    }

    [Fact]
    public void GetHourlyGrossForPayrollMonth_FullDayUsesCombinedWindow()
    {
        var emp = new Employee
        {
            JoinDate = new DateTime(2020, 1, 1),
            HourlyRate = 10m,
            MondayShift = "Full Day",
            TuesdayShift = "Off",
            WednesdayShift = "Off",
            ThursdayShift = "Off",
            FridayShift = "Off",
            SaturdayShift = "Off",
            SundayShift = "Off"
        };
        var (hours, workdays, gross) = PayrollCalculator.GetHourlyGrossForPayrollMonth(emp, 2026, 4);
        Assert.True(workdays >= 4);
        Assert.True(hours >= 44m);
        Assert.Equal(Math.Round(10m * hours, 2), gross);
    }
}
