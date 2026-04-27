namespace EliteRestaurant.Core.Utils;

/// <summary>
/// WorkDate is stored as <c>timestamp with time zone</c> using a UTC calendar-day anchor (local date at 00:00 with <see cref="DateTimeKind.Utc"/>).
/// Queries must use a [start, end) range — not <see cref="DateTime.Date"/> equality — or lookups miss rows and violate the unique index on (EmployeeId, WorkDate).
/// </summary>
public static class AttendanceCalendar
{
    public static DateTime DayAnchorUtc(DateTime localCalendarDay) =>
        DateTime.SpecifyKind(localCalendarDay.Date, DateTimeKind.Utc);

    public static (DateTime StartUtc, DateTime EndUtc) DayRangeUtc(DateTime localCalendarDay)
    {
        var start = DayAnchorUtc(localCalendarDay);
        return (start, start.AddDays(1));
    }
}
