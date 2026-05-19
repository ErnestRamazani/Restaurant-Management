namespace EliteRestaurant.Core.Utils;

public readonly record struct LiveTicketTotals(
    int LiveItemCount,
    decimal TicketSubtotal,
    decimal DiscountApplied,
    string DiscountLabel,
    decimal TaxAmount,
    decimal ServiceAmount,
    decimal GrandTotal,
    int EstimatedPrepMinutes);

/// <summary>Combines discount parsing, tax/service/grand totals, and prep estimate for a ticket subtotal.</summary>
public sealed class OrderTotalsCalculator
{
    public LiveTicketTotals ComputeTicket(
        decimal ticketSubtotal,
        int liveItemCount,
        string discountMode,
        string discountInput,
        IReadOnlyList<(int Quantity, int PrepMinutes, string Category, string SubCategory)> prepLines)
    {
        var discountRaw = OrderDiscountParser.Parse(discountInput);
        var totals = OrderTotalsHelper.ComputeTotals(ticketSubtotal, discountMode, discountRaw);
        var label = OrderTotalsHelper.FormatDiscountLabel(discountMode, discountRaw, totals.DiscountApplied);
        var prep = OrderPrepTimeEstimator.EstimateTicketPrepMinutes(prepLines);
        return new LiveTicketTotals(
            liveItemCount,
            ticketSubtotal,
            totals.DiscountApplied,
            label,
            totals.Tax,
            totals.Service,
            totals.GrandTotal,
            prep);
    }
}
