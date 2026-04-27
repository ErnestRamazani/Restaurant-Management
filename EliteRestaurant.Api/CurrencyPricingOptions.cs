namespace EliteRestaurant.Api;

/// <summary>API-level overrides for tax/service shown on tablet config; falls back to shared app settings when zero.</summary>
public sealed class CurrencyPricingOptions
{
    public decimal TaxPercent { get; set; } = 7m;
    public decimal ServicePercent { get; set; } = 10m;
}
