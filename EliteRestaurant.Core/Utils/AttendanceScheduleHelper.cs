using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Utils;

/// <summary>Shift resolution and auto-absence generation for scheduled workdays.</summary>
public static class AttendanceScheduleHelper
{
    private static readonly TimeSpan MorningShiftStart = new(12, 0, 0);
    private static readonly TimeSpan MorningShiftEnd = new(18, 0, 0);
    private static readonly TimeSpan NightShiftStart = new(18, 0, 0);
    private static readonly TimeSpan NightShiftEnd = new(23, 0, 0);

    public readonly record struct ShiftWindow(string Name, TimeSpan Start, TimeSpan End, string WindowText, bool IsOff);

    public static ShiftWindow ResolveShiftWindow(Employee employee, DateTime day)
    {
        var configuredShift = day.DayOfWeek switch
        {
            DayOfWeek.Monday => employee.MondayShift,
            DayOfWeek.Tuesday => employee.TuesdayShift,
            DayOfWeek.Wednesday => employee.WednesdayShift,
            DayOfWeek.Thursday => employee.ThursdayShift,
            DayOfWeek.Friday => employee.FridayShift,
            DayOfWeek.Saturday => employee.SaturdayShift,
            DayOfWeek.Sunday => employee.SundayShift,
            _ => "Off"
        };

        var normalized = (configuredShift ?? string.Empty).Trim();

        if (normalized.Equals("Off", StringComparison.OrdinalIgnoreCase))
            return new ShiftWindow("Off", MorningShiftStart, MorningShiftEnd, "Off day", IsOff: true);

        if (normalized.Contains("Night", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("Evening", StringComparison.OrdinalIgnoreCase))
        {
            return new ShiftWindow("Night Shift", NightShiftStart, NightShiftEnd, "06:00 PM - 11:00 PM", IsOff: false);
        }

        if (normalized.Contains("Morning", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("Afternoon", StringComparison.OrdinalIgnoreCase))
        {
            return new ShiftWindow("Morning Shift", MorningShiftStart, MorningShiftEnd, "12:00 PM - 06:00 PM", IsOff: false);
        }

        return new ShiftWindow("Morning Shift", MorningShiftStart, MorningShiftEnd, "12:00 PM - 06:00 PM", IsOff: false);
    }

    /// <summary>
    /// For each past scheduled workday up to yesterday, ensure an absence row exists when there was no clock-in.
    /// </summary>
    public static void EnsureAutoAbsences(AppDbContext db, DateTime fromDate, DateTime throughDate)
        => EnsureAutoAbsences(db, fromDate, throughDate, null);

    /// <param name="skipDates">Calendar dates already validated by admin — left unchanged.</param>
    public static void EnsureAutoAbsences(AppDbContext db, DateTime fromDate, DateTime throughDate, IReadOnlySet<DateTime>? skipDates)
    {
        var end = throughDate.Date < DateTime.Today
            ? throughDate.Date
            : DateTime.Today.AddDays(-1);
        if (end < fromDate.Date)
            return;

        var employees = db.Employees.Where(e => e.EmploymentStatus == "Active").ToList();
        for (var d = fromDate.Date; d <= end; d = d.AddDays(1))
        {
            if (skipDates is not null && skipDates.Contains(d.Date))
                continue;

            foreach (var emp in employees)
            {
                var shift = ResolveShiftWindow(emp, d);
                if (shift.IsOff)
                    continue;

                var (dayStartUtc, dayEndUtc) = AttendanceCalendar.DayRangeUtc(d);
                var att = db.EmployeeAttendances
                    .FirstOrDefault(a => a.EmployeeId == emp.Id && a.WorkDate >= dayStartUtc && a.WorkDate < dayEndUtc);
                if (att is null)
                {
                    db.EmployeeAttendances.Add(new EmployeeAttendance
                    {
                        EmployeeId = emp.Id,
                        WorkDate = dayStartUtc,
                        IsAbsence = true,
                        ClockInStatus = "Absent"
                    });
                }
                else if (att.ClockInTime is null && !att.IsAbsence)
                {
                    att.IsAbsence = true;
                    if (string.IsNullOrWhiteSpace(att.ClockInStatus) ||
                        att.ClockInStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                        att.ClockInStatus = "Absent";
                }
            }
        }

        db.SaveChanges();
    }
}
