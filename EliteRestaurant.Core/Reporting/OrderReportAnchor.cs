using EliteRestaurant.Core.Models;
using System.Linq.Expressions;

namespace EliteRestaurant.Core.Reporting;

/// <summary>
/// Aligns general reports with the Money ledger: revenue is recognized on payment confirmation / completion when set.
/// </summary>
public static class OrderReportAnchor
{
    /// <summary>EF-translatable anchor for SQL <c>ORDER BY</c> / filters (do not use <see cref="Anchor"/> in IQueryable).</summary>
    public static Expression<Func<OrderRecord, DateTime>> AnchorExpression { get; } =
        o => o.PaymentConfirmedAt ?? o.CompletedAt ?? o.CreatedAt;

    /// <summary>Same precedence as <see cref="Data.FinancialTransactionService"/> ledger dating.</summary>
    public static DateTime Anchor(OrderRecord o) =>
        o.PaymentConfirmedAt ?? o.CompletedAt ?? o.CreatedAt;

    public static IOrderedQueryable<OrderRecord> OrderByAnchor(IQueryable<OrderRecord> source) =>
        source.OrderBy(AnchorExpression);

    public static IOrderedQueryable<OrderRecord> OrderByAnchorDescending(IQueryable<OrderRecord> source) =>
        source.OrderByDescending(AnchorExpression);

    public static DateTime LocalCalendarDay(DateTime t) =>
        t.Kind switch
        {
            DateTimeKind.Utc => t.ToLocalTime().Date,
            DateTimeKind.Local => t.Date,
            _ => t.Date,
        };

    /// <param name="startDay">Local midnight start (inclusive).</param>
    /// <param name="endExclusive">Local midnight after last inclusive day.</param>
    public static bool IsAnchorInHalfOpenLocalRange(OrderRecord o, DateTime startDay, DateTime endExclusive)
    {
        var local = Anchor(o);
        if (local.Kind == DateTimeKind.Utc)
            local = local.ToLocalTime();
        return local >= startDay && local < endExclusive;
    }
}

/// <summary>Pick a single timestamp for reservation rows in day-grouped reports.</summary>
public static class ReservationReportTime
{
    public static DateTime DisplayEventTime(ReservationBooking r, DateTime rangeStart, DateTime rangeEndExclusive)
    {
        var updIn = r.UpdatedAt >= rangeStart && r.UpdatedAt < rangeEndExclusive;
        var rsIn = r.ReservedFor >= rangeStart && r.ReservedFor < rangeEndExclusive;
        if (updIn && rsIn)
            return r.UpdatedAt;
        if (updIn)
            return r.UpdatedAt;
        if (rsIn)
            return r.ReservedFor;
        return r.UpdatedAt;
    }
}
