using System.Text.Json.Serialization;
using EliteRestaurant.Core.Menu;

namespace EliteRestaurant.Core.Utils;

public static class CloudEndpoints
{
    public const string ProductionApiBaseUrl = "https://starfish-app-owtoz.ondigitalocean.app";
    public const string LocalApiBaseUrl = "http://localhost:8080";

    public static string NormalizeApiBaseUrl(string? baseUrl, bool preferProduction = true)
    {
        var trimmed = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(trimmed))
            return preferProduction ? ProductionApiBaseUrl : LocalApiBaseUrl;

        // Older docs/tools used :5223; current API launch profile listens on :8080. Rewire localhost URLs so
        // the desktop client hits the same host as the browser (avoid silently forcing production).
        trimmed = trimmed.Replace("localhost:5223", "localhost:8080", StringComparison.OrdinalIgnoreCase);
        trimmed = trimmed.Replace("127.0.0.1:5223", "127.0.0.1:8080", StringComparison.OrdinalIgnoreCase);

        return trimmed;
    }
}

public sealed class AppSettings
{
    public BusinessProfileSettings BusinessProfile { get; set; } = new();
    public CurrencyPricingSettings CurrencyPricing { get; set; } = new();
    public NavigationBackgroundSettings NavigationBackgrounds { get; set; } = new();
    public DatabaseSettings Database { get; set; } = new();
    public CloudApiSettings CloudApi { get; set; } = new();
    /// <summary>Shift windows for attendance UI, payroll scheduled hours, and auto-absence logic.</summary>
    public AttendanceSettings Attendance { get; set; } = new();

    /// <summary>Admin-defined menu type → category (Product.Category) → subcategory (Product.SubCategory) structure.</summary>
    public MenuTaxonomySettings? MenuTaxonomy { get; set; }
}

/// <summary>Restaurant shift boundaries (local time of day). Serialized to app-settings.json.</summary>
public sealed class AttendanceSettings
{
    public TimeSpan MorningShiftStart { get; set; } = new(12, 0, 0);
    public TimeSpan MorningShiftEnd { get; set; } = new(18, 0, 0);
    public TimeSpan NightShiftStart { get; set; } = new(18, 0, 0);
    public TimeSpan NightShiftEnd { get; set; } = new(23, 0, 0);
    public int LateClockInGraceMinutes { get; set; } = 30;
}

public sealed class DatabaseSettings
{
    // Supported value: PostgreSql
    public string Provider { get; set; } = "PostgreSql";

    /// <summary>Legacy plaintext; cleared after migration. Use <see langword="null"/> so JSON omits the property (empty string would still serialize).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? PostgreSqlConnectionString { get; set; }

    public string PostgreSqlHost { get; set; } = string.Empty;
    public int PostgreSqlPort { get; set; } = 5432;
    public string PostgreSqlDatabase { get; set; } = string.Empty;
    public string PostgreSqlUsername { get; set; } = string.Empty;

    /// <summary>Windows DPAPI-protected password (Base64), CurrentUser scope.</summary>
    public string PostgreSqlPasswordProtected { get; set; } = string.Empty;
}

public sealed class CloudApiSettings
{
    public string BaseUrl { get; set; } = CloudEndpoints.ProductionApiBaseUrl;
    public string AccessToken { get; set; } = string.Empty;
    public DateTime? TokenExpiresAtUtc { get; set; }
}

public sealed class BusinessProfileSettings
{
    public string RestaurantName { get; set; } = "Elite Restaurant";
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string WebsiteDomain { get; set; } = string.Empty;
    public string SocialMedia { get; set; } = string.Empty;
    public string LogoPath { get; set; } = string.Empty;

    /// <summary>Base URL for customer menu QR links (no trailing slash), e.g. http://192.168.1.50:5223</summary>
    public string PublicMenuBaseUrl { get; set; } = CloudEndpoints.ProductionApiBaseUrl;

    /// <summary>Optional tagline for the public customer menu (e.g. Fine Dining · Est. 2024).</summary>
    public string? CustomerMenuTagline { get; set; }

    /// <summary>Simple gate before exposing staff/admin workspace links from the public menu.</summary>
    public string StaffLoginPasscode { get; set; } = "er4124";

    public string HomepageBackgroundImagePath { get; set; } = string.Empty;
    public string TicketFooterText { get; set; } = "MERCI / THANK YOU";
    public string TaxIdLegalInfo { get; set; } = string.Empty;

    /// <summary>Hero card on public online order PWA (synced to cloud).</summary>
    public string? OnlinePromoTitle { get; set; }
    public string? OnlinePromoSubtitle { get; set; }
    public string? OnlinePromoCtaLabel { get; set; }
    /// <summary>Local image path; pushed to public menu assets as <c>online-promo</c>.</summary>
    public string OnlinePromoImagePath { get; set; } = string.Empty;
    /// <summary>Optional table id for routing guest online orders in the POS (matches <see cref="PublicMenuSetting.OnlineOrdersTableId"/>).</summary>
    public int? OnlineOrdersTableId { get; set; }
    /// <summary>Minimum calendar days from today required for public booking.</summary>
    public int ReservationLeadDays { get; set; } = 2;
    /// <summary>Maximum months ahead allowed for public booking.</summary>
    public int ReservationMaxMonthsAhead { get; set; } = 6;
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
