using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Reporting;
using Xunit;

namespace EliteRestaurant.Core.Tests;

public sealed class OrderReportAnchorTests
{
    [Fact]
    public void Anchor_prefers_payment_confirmed_then_completed_then_created()
    {
        var created = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var completed = new DateTime(2026, 1, 2, 11, 0, 0, DateTimeKind.Utc);
        var paid = new DateTime(2026, 1, 3, 12, 0, 0, DateTimeKind.Utc);
        var o = new OrderRecord { CreatedAt = created, CompletedAt = completed, PaymentConfirmedAt = paid };
        Assert.Equal(paid, OrderReportAnchor.Anchor(o));

        o.PaymentConfirmedAt = null;
        Assert.Equal(completed, OrderReportAnchor.Anchor(o));

        o.CompletedAt = null;
        Assert.Equal(created, OrderReportAnchor.Anchor(o));
    }

    [Fact]
    public void IsAnchorInHalfOpenLocalRange_uses_local_midnight_window()
    {
        var start = new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Unspecified);
        var endExclusive = new DateTime(2026, 5, 12, 0, 0, 0, DateTimeKind.Unspecified);
        var o = new OrderRecord
        {
            CreatedAt = new DateTime(2026, 5, 11, 15, 0, 0, DateTimeKind.Utc),
            CompletedAt = null,
            PaymentConfirmedAt = null
        };
        Assert.True(OrderReportAnchor.IsAnchorInHalfOpenLocalRange(o, start, endExclusive));
    }

    [Fact]
    public void ReservationReportTime_prefers_update_when_both_in_range()
    {
        var start = new DateTime(2026, 5, 10, 0, 0, 0);
        var endEx = new DateTime(2026, 5, 12, 0, 0, 0);
        var r = new ReservationBooking
        {
            ReservedFor = new DateTime(2026, 5, 11, 18, 0, 0),
            UpdatedAt = new DateTime(2026, 5, 11, 9, 0, 0)
        };
        Assert.Equal(r.UpdatedAt, ReservationReportTime.DisplayEventTime(r, start, endEx));
    }
}
