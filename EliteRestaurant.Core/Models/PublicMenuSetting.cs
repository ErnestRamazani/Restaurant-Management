namespace EliteRestaurant.Core.Models;

public sealed class PublicMenuSetting
{
    public int Id { get; set; }
    public string Key { get; set; } = "default";
    public string RestaurantName { get; set; } = "Elite Restaurant";
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string WebsiteDomain { get; set; } = string.Empty;
    public string SocialMedia { get; set; } = string.Empty;
    public string? CustomerMenuTagline { get; set; }
    public string StaffLoginPasscode { get; set; } = "er4124";
    public string TicketFooterText { get; set; } = "MERCI / THANK YOU";
    public string TaxIdLegalInfo { get; set; } = string.Empty;
    public string DefaultCurrencyDisplayMode { get; set; } = "Dual";
    public decimal UsdToFcRate { get; set; } = 2250m;
    public string RoundingLine { get; set; } = "Nearest";
    public string RoundingSubtotal { get; set; } = "Nearest";
    public string RoundingGrandTotal { get; set; } = "Nearest";
    public decimal TaxPercent { get; set; } = 7m;
    public decimal ServicePercent { get; set; } = 10m;
    /// <summary>Optional: POS table id for guest online orders (must have assigned server in ops).</summary>
    public int? OnlineOrdersTableId { get; set; }
    public string? OnlinePromoTitle { get; set; }
    public string? OnlinePromoSubtitle { get; set; }
    public string? OnlinePromoCtaLabel { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
