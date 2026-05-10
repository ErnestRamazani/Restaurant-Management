using EliteRestaurant.Core.Reservations;
using Xunit;

namespace EliteRestaurant.Tests;

public sealed class ReservationOverlapTests
{
    private static readonly DateTime T0 = new(2026, 5, 10, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IntervalsOverlap_True_ForIdenticalIntervals_NoBuffer()
    {
        var end = T0.AddHours(2);
        Assert.True(ReservationOverlapMath.IntervalsOverlap(T0, end, T0, end, TimeSpan.Zero));
    }

    [Fact]
    public void IntervalsOverlap_False_WhenEndEqualsStart()
    {
        Assert.False(ReservationOverlapMath.IntervalsOverlap(T0, T0, T0, T0.AddHours(1), TimeSpan.Zero));
    }

    [Fact]
    public void IntervalsOverlap_Buffer_PushesNonOverlappingIntoConflict()
    {
        // A: 18:00–19:00, B: 19:15–20:15 — no overlap without buffer
        var aEnd = T0.AddHours(1);
        var bStart = T0.AddHours(1).AddMinutes(15);
        var bEnd = bStart.AddHours(1);
        Assert.False(ReservationOverlapMath.IntervalsOverlap(T0, aEnd, bStart, bEnd, TimeSpan.Zero));

        // 30m buffer extends A end to 19:30, B start to 18:45 → overlap
        Assert.True(ReservationOverlapMath.IntervalsOverlapMinutes(T0, aEnd, bStart, bEnd, 30));
    }

    [Fact]
    public void IntervalsOverlap_False_WhenSeparatedBeyondSymmetricBuffer()
    {
        var aStart = T0;
        var aEnd = T0.AddHours(1);
        var bStart = T0.AddHours(2);
        var bEnd = T0.AddHours(3);
        Assert.False(ReservationOverlapMath.IntervalsOverlapMinutes(aStart, aEnd, bStart, bEnd, 15));
    }
}
