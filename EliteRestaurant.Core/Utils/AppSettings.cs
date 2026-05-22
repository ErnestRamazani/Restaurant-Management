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

    /// <summary>True when the API base URL targets this PC (localhost), not hosted production.</summary>
    public static bool IsLocalDevelopmentApiUrl(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return true;

        var normalized = NormalizeApiBaseUrl(baseUrl).ToLowerInvariant();
        return normalized.Contains("://localhost", StringComparison.Ordinal)
               || normalized.Contains("://127.0.0.1", StringComparison.Ordinal)
               || normalized.Contains("://[::1]", StringComparison.Ordinal);
    }
}

public sealed class AppSettings
{
    public BusinessProfileSettings BusinessProfile { get; set; } = new();
    /// <summary>Printed/PDF ticket layout (local paths; not synced to cloud).</summary>
    public TicketReceiptSettings TicketReceipt { get; set; } = new();
    public CurrencyPricingSettings CurrencyPricing { get; set; } = new();
    public NavigationBackgroundSettings NavigationBackgrounds { get; set; } = new();
    public DatabaseSettings Database { get; set; } = new();
    public CloudApiSettings CloudApi { get; set; } = new();
    /// <summary>Shift windows for attendance UI, payroll scheduled hours, and auto-absence logic.</summary>
    public AttendanceSettings Attendance { get; set; } = new();

    /// <summary>Payroll attendance units, sales bonus %, and advance caps (synced with cloud profile / DB).</summary>
    public SalarySettings Salary { get; set; } = new();

    /// <summary>Admin-defined menu type → category (Product.Category) → subcategory (Product.SubCategory) structure.</summary>
    public MenuTaxonomySettings? MenuTaxonomy { get; set; }

    /// <summary>Set true after <c>POST /api/setup/first-site</c> (or portable wizard) so the desktop app does not reopen setup.</summary>
    public bool FirstSiteSetupCompleted { get; set; }

    /// <summary>Matches API <c>Setup__PlatformSecret</c> — required for <c>POST /api/setup/new-site</c> when a tenant already exists.</summary>
    public string SetupPlatformSecret { get; set; } = string.Empty;
}

/// <summary>Restaurant shift boundaries (local time of day). Serialized to app-settings.json.</summary>
/// <remarks>Full Day employee shifts use <see cref="MorningShiftStart"/> through <see cref="NightShiftEnd"/> (operating hours).</remarks>
public sealed class AttendanceSettings
{
    public TimeSpan MorningShiftStart { get; set; } = new(12, 0, 0);
    public TimeSpan MorningShiftEnd { get; set; } = new(18, 0, 0);
    public TimeSpan NightShiftStart { get; set; } = new(18, 0, 0);
    public TimeSpan NightShiftEnd { get; set; } = new(23, 0, 0);
    public int LateClockInGraceMinutes { get; set; } = 30;
}

/// <summary>Salary and payroll calculation parameters (stored in <c>app-settings.json</c> and <see cref="Models.PublicMenuSetting"/>).</summary>
public sealed class SalarySettings
{
    /// <summary>Late clock-in days that combine into one absence-equivalent payroll deduction unit (minimum 1).</summary>
    public int LateDaysPerAttendanceUnit { get; set; } = 4;

    /// <summary>When true, each scheduled absence day counts as one deduction unit (in addition to units from lates).</summary>
    public bool AbsenceCountsAsAttendanceUnit { get; set; } = true;

    /// <summary>Bonus on server merchandise sales, as a percent (0–100), same basis as the legacy 5% rule.</summary>
    public decimal SalesBonusPercent { get; set; } = 5m;

    /// <summary>Maximum salary advances for a payroll month as a percent of that month’s scheduled gross (0–100).</summary>
    public decimal MaxSalaryAdvancePercentOfGross { get; set; } = 30m;
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
    public string RestaurantName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string WebsiteDomain { get; set; } = string.Empty;
    public string SocialMedia { get; set; } = string.Empty;
    public string LogoPath { get; set; } = string.Empty;

    /// <summary>Base URL for customer menu QR links (no trailing slash), e.g. http://192.168.1.50:5223</summary>
    public string PublicMenuBaseUrl { get; set; } = CloudEndpoints.ProductionApiBaseUrl;

    /// <summary>Optional tagline for the public customer menu (e.g. Cuisine moderne · Kinshasa).</summary>
    public string? CustomerMenuTagline { get; set; }

    /// <summary>Public menu About sheet (plain text).</summary>
    public string? CustomerMenuAboutText { get; set; }

    /// <summary>Public menu Contact sheet intro (plain text).</summary>
    public string? CustomerMenuContactIntro { get; set; }

    /// <summary>Public menu Notes sheet (plain text).</summary>
    public string? CustomerMenuNotesText { get; set; }

    /// <summary>Simple gate before exposing staff/admin workspace links from the public menu.</summary>
    public string StaffLoginPasscode { get; set; } = string.Empty;

    /// <summary>Sign-in ID for the read-only admin web portal (<c>/admin/</c>).</summary>
    public string AdminWebSignInId { get; set; } = string.Empty;

    /// <summary>PIN for the read-only admin web portal (pushed to cloud and synced to AdminWeb employee).</summary>
    public string AdminWebPin { get; set; } = string.Empty;

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

/// <summary>Local ticket/PDF receipt branding (paths on this machine).</summary>
public sealed class TicketReceiptSettings
{
    /// <summary>Optional logo image shown above the restaurant name on printed/PDF tickets only.</summary>
    public string HeaderLogoPath { get; set; } = string.Empty;

    public List<TicketSocialMediaRowSettings> SocialMediaRows { get; set; } = [];
}

public sealed class TicketSocialMediaRowSettings
{
    public string PlatformName { get; set; } = string.Empty;
    public string UserText { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
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
