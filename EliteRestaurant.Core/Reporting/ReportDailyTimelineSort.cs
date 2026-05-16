namespace EliteRestaurant.Core.Reporting;

/// <summary>
/// Shared ordering for day-grouped daily reports: payroll/salary activity is listed first within each calendar day,
/// then rows are newest-first by event clock time (late catch-up payments surface at the top of that day).
/// </summary>
public static class ReportDailyTimelineSort
{
    public static bool IsPinnedPayrollSalaryEventType(string eventType)
    {
        if (string.IsNullOrEmpty(eventType))
            return false;

        return string.Equals(eventType, "Payroll month (record)", StringComparison.Ordinal)
            || string.Equals(eventType, "Salary payment (Money)", StringComparison.Ordinal)
            || string.Equals(eventType, "Salary advance (Money)", StringComparison.Ordinal)
            || string.Equals(eventType, "Salary advance (record)", StringComparison.Ordinal)
            || string.Equals(eventType, "Salary / Money", StringComparison.Ordinal);
    }

    /// <summary>Within a single calendar day: payroll/salary rows first, then newest-first by clock time.</summary>
    public static int CompareWithinDayPayrollFirstNewestFirst(
        DateTime aTime,
        string aType,
        DateTime bTime,
        string bType)
    {
        var pinA = IsPinnedPayrollSalaryEventType(aType);
        var pinB = IsPinnedPayrollSalaryEventType(bType);
        if (pinA != pinB)
            return pinA ? -1 : 1;

        var cmp = bTime.CompareTo(aTime);
        if (cmp != 0)
            return cmp;

        return string.CompareOrdinal(aType, bType);
    }
}
