namespace EliteRestaurant.Core.Reservations;

public static class ReservationOverlapMath
{
    /// <summary>
    /// Inclusive-exclusive interval [start, end) overlap with symmetric buffer applied to both intervals.
    /// </summary>
    public static bool IntervalsOverlap(
        DateTime startA,
        DateTime endA,
        DateTime startB,
        DateTime endB,
        TimeSpan bufferBeforeAfter)
    {
        if (endA <= startA || endB <= startB)
            return false;

        var a0 = startA - bufferBeforeAfter;
        var a1 = endA + bufferBeforeAfter;
        var b0 = startB - bufferBeforeAfter;
        var b1 = endB + bufferBeforeAfter;
        return a0 < b1 && a1 > b0;
    }

    /// <summary>Buffer in minutes applied before start and after end for both intervals.</summary>
    public static bool IntervalsOverlapMinutes(
        DateTime startA,
        DateTime endA,
        DateTime startB,
        DateTime endB,
        int bufferMinutes) =>
        IntervalsOverlap(startA, endA, startB, endB, TimeSpan.FromMinutes(bufferMinutes));
}
