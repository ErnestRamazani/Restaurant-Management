using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Reporting;
using Xunit;

namespace EliteRestaurant.Core.Tests;

public sealed class AttendanceReportEntriesTests
{
    [Fact]
    public void Build_emits_clock_in_and_clock_out_for_shift()
    {
        var day = new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Unspecified);
        var row = new EmployeeAttendance
        {
            EmployeeId = 1,
            WorkDate = day,
            ClockInTime = day.AddHours(9).AddMinutes(5),
            ClockOutTime = day.AddHours(17),
            ClockInStatus = "On Time"
        };

        var events = AttendanceReportEntries.Build(row, "Emma Russo").ToList();

        Assert.Equal(2, events.Count);
        Assert.Equal(AttendanceReportEntries.ClockInEventType, events[0].EventType);
        Assert.Contains("signed in at 09:05", events[0].Summary, StringComparison.Ordinal);
        Assert.Equal(AttendanceReportEntries.ClockOutEventType, events[1].EventType);
        Assert.Contains("clocked out at 17:00", events[1].Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_emits_absence_without_clock_in()
    {
        var day = new DateTime(2026, 5, 21, 0, 0, 0, DateTimeKind.Unspecified);
        var row = new EmployeeAttendance
        {
            EmployeeId = 2,
            WorkDate = day,
            IsAbsence = true,
            AbsenceJustification = "Sick",
            ClockInStatus = "Absent"
        };

        var events = AttendanceReportEntries.Build(row, "Liam Foster").ToList();

        Assert.Single(events);
        Assert.Equal("Absence", events[0].EventType);
        Assert.Contains("marked absent", events[0].Summary, StringComparison.Ordinal);
    }
}
