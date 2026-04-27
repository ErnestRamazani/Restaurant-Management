namespace EliteRestaurant.Core.Utils;

/// <summary>
/// Merges API-level pricing overrides (<c>IOptions&lt;CurrencyPricingOptions&gt;</c> / appsettings) with
/// desktop file settings (<see cref="SettingsManager"/> / app-settings.json) for a single source of truth per request.
/// </summary>
public static class PricingResolver
{
    public static decimal ResolveTaxRate(decimal apiTaxPercent, decimal fileTaxPercent)
    {
        if (apiTaxPercent > 0m)
            return apiTaxPercent;
        if (fileTaxPercent > 0m)
            return fileTaxPercent;
        throw new InvalidOperationException(
            "TaxPercent is not configured in appsettings.json (CurrencyPricing:TaxPercent) " +
            "or app-settings.json (CurrencyPricing.TaxPercent). " +
            "At least one source must provide a positive value.");
    }

    public static decimal ResolveServicePercent(decimal apiServicePercent, decimal fileServicePercent)
    {
        if (apiServicePercent > 0m)
            return apiServicePercent;
        if (fileServicePercent > 0m)
            return fileServicePercent;
        throw new InvalidOperationException(
            "ServicePercent is not configured in appsettings.json (CurrencyPricing:ServicePercent) " +
            "or app-settings.json (CurrencyPricing.ServicePercent). " +
            "At least one source must provide a positive value.");
    }
}
