namespace EliteRestaurantPro.Utils;

public sealed class AppSettings
{
    public BusinessProfileSettings BusinessProfile { get; set; } = new();
    public CurrencyPricingSettings CurrencyPricing { get; set; } = new();
    public NavigationBackgroundSettings NavigationBackgrounds { get; set; } = new();
    public DatabaseSettings Database { get; set; } = new();
}

public sealed class DatabaseSettings
{
    // Supported value: PostgreSql
    public string Provider { get; set; } = "PostgreSql";
    public string PostgreSqlConnectionString { get; set; } = string.Empty;
}

public sealed class BusinessProfileSettings
{
    public string RestaurantName { get; set; } = "Elite Restaurant";
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string WebsiteDomain { get; set; } = string.Empty;
    public string SocialMedia { get; set; } = string.Empty;
    public string LogoPath { get; set; } = string.Empty;
    public string HomepageBackgroundImagePath { get; set; } = string.Empty;
    public string TicketFooterText { get; set; } = "MERCI / THANK YOU";
    public string TaxIdLegalInfo { get; set; } = string.Empty;
}

public sealed class CurrencyPricingSettings
{
    public string DefaultCurrencyDisplayMode { get; set; } = "Dual";
    public decimal UsdToFcRate { get; set; } = 2250m;
    public DateTime ExchangeRateLastUpdatedUtc { get; set; } = DateTime.UtcNow;
    public string RoundingLine { get; set; } = "Nearest";
    public string RoundingSubtotal { get; set; } = "Nearest";
    public string RoundingGrandTotal { get; set; } = "Nearest";
    public decimal TaxPercent { get; set; } = 7m;
    public decimal ServicePercent { get; set; } = 10m;
}

public sealed class NavigationBackgroundSettings
{
    public Dictionary<string, string> PageImagePaths { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public double DimStrength { get; set; } = 0.18;
    public double ContrastIntensity { get; set; } = 0.22;
}
