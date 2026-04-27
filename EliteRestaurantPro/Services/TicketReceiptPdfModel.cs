namespace EliteRestaurantPro.Services;

public sealed record TicketPdfLine(int Quantity, string Name, decimal UnitPrice, decimal LineTotal);

public sealed class TicketReceiptPdfModel
{
    public required IReadOnlyList<TicketPdfLine> Lines { get; init; }
    public string TicketOrderId { get; init; } = string.Empty;
    public string TicketStatus { get; init; } = string.Empty;
    public string TicketTable { get; init; } = string.Empty;
    public string TicketServer { get; init; } = string.Empty;
    public DateTime TicketDateTime { get; init; }
    public decimal TicketSubtotal { get; init; }
    public decimal TicketDiscountAmount { get; init; }
    public string TicketDiscountLineText { get; init; } = string.Empty;
    public decimal TicketTaxAmount { get; init; }
    public decimal TicketServiceAmount { get; init; }
    public decimal TicketGrandTotal { get; init; }
    public string TicketEquivalentFcText { get; init; } = string.Empty;
    public string TicketPaymentText { get; init; } = string.Empty;
    public string TicketPaidBreakdownText { get; init; } = string.Empty;
    public string TicketChangeBreakdownText { get; init; } = string.Empty;
    public string TicketVerification { get; init; } = string.Empty;
    public decimal TaxPercent { get; init; }
    public decimal ServicePercent { get; init; }
    public string RestaurantTitle { get; init; } = string.Empty;
    public string FooterText { get; init; } = string.Empty;
    public string LegalInfo { get; init; } = string.Empty;
}
