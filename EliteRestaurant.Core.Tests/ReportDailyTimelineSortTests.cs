using EliteRestaurant.Core.Reporting;
using Xunit;

namespace EliteRestaurant.Core.Tests;

public sealed class ReportDailyTimelineSortTests
{
    [Fact]
    public void Pinned_types_sort_before_non_pinned_on_same_day_when_time_equal()
    {
        var noon = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Local);
        var cmp = ReportDailyTimelineSort.CompareWithinDayPayrollFirstNewestFirst(
            noon,
            "Table Service",
            noon,
            "Salary payment (Money)");
        Assert.True(cmp > 0, "Non-salary should sort after salary when times tie.");
    }

    [Fact]
    public void Newer_time_sorts_before_older_when_both_pinned()
    {
        var older = new DateTime(2026, 5, 10, 9, 0, 0, DateTimeKind.Local);
        var newer = new DateTime(2026, 5, 10, 18, 0, 0, DateTimeKind.Local);
        var cmp = ReportDailyTimelineSort.CompareWithinDayPayrollFirstNewestFirst(
            newer,
            "Salary payment (Money)",
            older,
            "Payroll month (record)");
        Assert.True(cmp < 0, "Newer salary event should come first.");
    }
}
