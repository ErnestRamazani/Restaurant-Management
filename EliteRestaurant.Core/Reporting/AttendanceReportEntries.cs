using System.Globalization;
using EliteRestaurant.Core.Models;

namespace EliteRestaurant.Core.Reporting;

/// <summary>Builds per-sign-in report timeline rows from <see cref="EmployeeAttendance"/>.</summary>
public static class AttendanceReportEntries
{
    public const string ClockInEventType = "Clock in";
    public const string ClockOutEventType = "Clock out";
    public const string ScheduledShiftEventType = "Scheduled shift";

    public readonly record struct Row(
        DateTime EventTime,
        string EventType,
        string Summary,
        string RelatedInfo,
        string EntityContext);

    public static IEnumerable<Row> Build(EmployeeAttendance row, string employeeName)
    {
        var name = string.IsNullOrWhiteSpace(employeeName) ? "Unknown" : employeeName.Trim();
        var status = string.IsNullOrWhiteSpace(row.ClockInStatus) ? "Pending" : row.ClockInStatus.Trim();
        var justification = string.IsNullOrWhiteSpace(row.Justification) ? "-" : row.Justification.Trim();
        var absenceNote = string.IsNullOrWhiteSpace(row.AbsenceJustification) ? "-" : row.AbsenceJustification.Trim();

        if (row.IsAbsence)
        {
            yield return new Row(
                row.WorkDate.Date.AddHours(9),
                "Absence",
                $"{name} marked absent",
                $"Justification: {absenceNote}",
                $"Employee: {name}");
            yield break;
        }

        if (row.ClockInTime is DateTime clockIn)
        {
            yield return new Row(
                clockIn,
                ClockInEventType,
                $"{name} signed in at {clockIn.ToString("HH:mm", CultureInfo.InvariantCulture)} ({status})",
                $"Work day {FormatWorkDate(row.WorkDate)} | Justification: {justification}",
                $"Employee: {name}");
        }
        else
        {
            yield return new Row(
                row.WorkDate.Date.AddHours(9),
                ScheduledShiftEventType,
                $"{name} scheduled (not clocked in yet)",
                $"Work day {FormatWorkDate(row.WorkDate)} | Status: {status}",
                $"Employee: {name}");
        }

        if (row.ClockOutTime is DateTime clockOut)
        {
            yield return new Row(
                clockOut,
                ClockOutEventType,
                $"{name} clocked out at {clockOut.ToString("HH:mm", CultureInfo.InvariantCulture)}",
                row.ClockInTime is DateTime ci
                    ? $"Shift {ci.ToString("HH:mm", CultureInfo.InvariantCulture)} → {clockOut.ToString("HH:mm", CultureInfo.InvariantCulture)}"
                    : $"Work day {FormatWorkDate(row.WorkDate)}",
                $"Employee: {name}");
        }
    }

    /// <summary>Employee detail tab: combined line plus separate clock-out when present.</summary>
    public static IEnumerable<Row> BuildForEmployeeDetail(EmployeeAttendance row, string employeeName)
    {
        var name = string.IsNullOrWhiteSpace(employeeName) ? "Unknown" : employeeName.Trim();
        var status = string.IsNullOrWhiteSpace(row.ClockInStatus) ? "Pending" : row.ClockInStatus.Trim();
        var justification = string.IsNullOrWhiteSpace(row.Justification) ? "-" : row.Justification.Trim();
        var absenceNote = string.IsNullOrWhiteSpace(row.AbsenceJustification) ? "-" : row.AbsenceJustification.Trim();

        if (row.IsAbsence)
        {
            yield return new Row(
                row.WorkDate.Date.AddHours(9),
                "Absence",
                $"Absent ({status})",
                $"Justification: {absenceNote}",
                name);
            yield break;
        }

        var clockIn = row.ClockInTime?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "Not clocked in";
        var clockOut = row.ClockOutTime?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "Not clocked out";
        var anchor = row.ClockInTime ?? row.WorkDate.Date.AddHours(9);

        yield return new Row(
            anchor,
            row.ClockInTime.HasValue ? ClockInEventType : ScheduledShiftEventType,
            row.ClockInTime.HasValue
                ? $"Sign in: {clockIn} ({status}) | Clock out: {clockOut}"
                : $"Scheduled | Clock out: {clockOut}",
            $"Justification: {justification}",
            name);

        if (row.ClockOutTime is DateTime co && row.ClockInTime is DateTime _)
        {
            yield return new Row(
                co,
                ClockOutEventType,
                $"{name} clocked out at {co.ToString("HH:mm", CultureInfo.InvariantCulture)}",
                $"Sign in was {clockIn}",
                name);
        }
    }

    private static string FormatWorkDate(DateTime workDate) =>
        workDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
