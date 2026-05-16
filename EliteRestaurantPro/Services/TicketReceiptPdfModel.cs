using EliteRestaurant.Core.Orders;

namespace EliteRestaurantPro.Services;

public sealed record TicketPdfLine(int Quantity, string Name, decimal UnitPrice, decimal LineTotal);

public sealed record TicketSocialMediaPdfRow(string PlatformName, string UserText, byte[]? IconBytes);

public sealed class TicketReceiptPdfModel
{
    public required IReadOnlyList<TicketPdfLine> Lines { get; init; }
    public string TicketOrderId { get; init; } = string.Empty;
    public string TicketConfirmationCode { get; init; } = string.Empty;
    public string TicketStatus { get; init; } = string.Empty;
    public string TicketTable { get; init; } = string.Empty;
    /// <summary>Online · Delivery / Pickup, or <c>Table: …</c> for dine-in.</summary>
    public string TicketLocationLine { get; init; } = string.Empty;
    public DeliveryTicketInfo? DeliveryInfo { get; init; }
    public bool TicketIsDeliveryFulfillment { get; init; }
    public bool ShowServerOnTicket { get; init; } = true;
    public string TicketServer { get; init; } = string.Empty;
    public DateTime TicketDateTime { get; init; }
    public decimal TicketSubtotal { get; init; }
    public decimal TicketDiscountAmount { get; init; }
    public string TicketDiscountLineText { get; init; } = string.Empty;
    public decimal TicketTaxAmount { get; init; }
    public decimal TicketServiceAmount { get; init; }
    public decimal TicketDeliveryFeeUsd { get; init; }
    public decimal TicketGrandTotal { get; init; }
    public string TicketEquivalentFcText { get; init; } = string.Empty;
    public string TicketPaymentText { get; init; } = string.Empty;
    public string TicketPaidBreakdownText { get; init; } = string.Empty;
    public string TicketChangeBreakdownText { get; init; } = string.Empty;
    public string TicketVerification { get; init; } = string.Empty;
    public decimal TaxPercent { get; init; }
    public decimal ServicePercent { get; init; }
    /// <summary>Raster bytes for ticket-only header logo (optional).</summary>
    public byte[]? HeaderLogoBytes { get; init; }
    public string RestaurantTitle { get; init; } = string.Empty;
    public string RestaurantPhone { get; init; } = string.Empty;
    /// <summary>Thank-you / footer line from Business Profile → “Footer text for tickets”.</summary>
    public string FooterText { get; init; } = string.Empty;
    public string ReceiptAddress { get; init; } = string.Empty;
    public string ReceiptWebsiteLine { get; init; } = string.Empty;
    public IReadOnlyList<TicketSocialMediaPdfRow> SocialFooterRows { get; init; } = Array.Empty<TicketSocialMediaPdfRow>();
    public string LegalInfo { get; init; } = string.Empty;
}
