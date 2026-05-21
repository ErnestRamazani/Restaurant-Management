using System.Globalization;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Utils;

/// <summary>Shift boundaries (local time-of-day) used for attendance, payroll, and auto-absence.</summary>
public readonly record struct AttendanceShiftSchedule(
    TimeSpan MorningStart,
    TimeSpan MorningEnd,
    TimeSpan NightStart,
    TimeSpan NightEnd)
{
    public static AttendanceShiftSchedule Defaults { get; } = new(
        new TimeSpan(12, 0, 0),
        new TimeSpan(18, 0, 0),
        new TimeSpan(18, 0, 0),
        new TimeSpan(23, 0, 0));

    public static AttendanceShiftSchedule FromSettings(AttendanceSettings? settings)
    {
        if (settings is null)
            return Defaults;

        return new AttendanceShiftSchedule(
            ClampToDay(settings.MorningShiftStart),
            ClampToDay(settings.MorningShiftEnd),
            ClampToDay(settings.NightShiftStart),
            ClampToDay(settings.NightShiftEnd));
    }

    private static TimeSpan ClampToDay(TimeSpan t)
    {
        if (t < TimeSpan.Zero)
            return TimeSpan.Zero;
        if (t >= TimeSpan.FromDays(1))
            return TimeSpan.FromDays(1).Subtract(TimeSpan.FromTicks(1));
        return t;
    }

    public string FormatMorningRange()
        => $"{FormatTime12h(MorningStart)}-{FormatTime12h(MorningEnd)}";

    public string FormatNightRange()
        => $"{FormatTime12h(NightStart)}-{FormatTime12h(NightEnd)}";

    /// <summary>Full Day spans morning start through night end (configured in appearance settings).</summary>
    public string FormatFullDayRange()
        => $"{FormatTime12h(MorningStart)}-{FormatTime12h(NightEnd)}";

    public TimeSpan FullDayStart => MorningStart;

    public TimeSpan FullDayEnd => NightEnd;

    private static string FormatTime12h(TimeSpan timeOfDay) =>
        DateTime.Today.Add(timeOfDay).ToString("h:mm tt", CultureInfo.CurrentCulture);
}

/// <summary>Shift resolution and auto-absence generation for scheduled workdays.</summary>
public static class AttendanceScheduleHelper
{
    public readonly record struct ShiftWindow(string Name, TimeSpan Start, TimeSpan End, string WindowText, bool IsOff);

    public static ShiftWindow ResolveShiftWindow(Employee employee, DateTime day, AttendanceShiftSchedule schedule)
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
            return new ShiftWindow("Off", schedule.MorningStart, schedule.MorningEnd, "Off day", IsOff: true);

        if (IsFullDayShift(normalized))
        {
            var w = schedule.FormatFullDayRange().Replace("-", " - ", StringComparison.Ordinal);
            return new ShiftWindow("Full Day", schedule.FullDayStart, schedule.FullDayEnd, w, IsOff: false);
        }

        if (normalized.Contains("Night", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("Evening", StringComparison.OrdinalIgnoreCase))
        {
            var w = $"{schedule.FormatNightRange().Replace("-", " - ")}";
            return new ShiftWindow("Night Shift", schedule.NightStart, schedule.NightEnd, w, IsOff: false);
        }

        if (normalized.Contains("Morning", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("Afternoon", StringComparison.OrdinalIgnoreCase))
        {
            var w = $"{schedule.FormatMorningRange().Replace("-", " - ")}";
            return new ShiftWindow("Morning Shift", schedule.MorningStart, schedule.MorningEnd, w, IsOff: false);
        }

        {
            var w = $"{schedule.FormatMorningRange().Replace("-", " - ")}";
            return new ShiftWindow("Morning Shift", schedule.MorningStart, schedule.MorningEnd, w, IsOff: false);
        }
    }

    public static bool IsFullDayShift(string? configuredShift)
    {
        var n = (configuredShift ?? string.Empty).Trim();
        if (n.Length == 0)
            return false;

        return n.Contains("Full", StringComparison.OrdinalIgnoreCase)
               && n.Contains("Day", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// For each past scheduled workday up to yesterday, ensure an absence row exists when there was no clock-in.
    /// </summary>
    public static void EnsureAutoAbsences(AppDbContext db, DateTime fromDate, DateTime throughDate)
        => EnsureAutoAbsences(db, fromDate, throughDate, null, null);

    /// <param name="skipDates">Calendar dates already validated by admin — left unchanged.</param>
    public static void EnsureAutoAbsences(
        AppDbContext db,
        DateTime fromDate,
        DateTime throughDate,
        IReadOnlySet<DateTime>? skipDates,
        AttendanceShiftSchedule? schedule = null)
    {
        var sched = schedule ?? AttendanceShiftSchedule.FromSettings(SettingsManager.Load().Attendance);
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
                var shift = ResolveShiftWindow(emp, d, sched);
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
