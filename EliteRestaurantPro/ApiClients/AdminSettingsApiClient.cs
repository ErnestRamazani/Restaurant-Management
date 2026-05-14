using System.IO;
using EliteRestaurant.Contracts.Admin;
using EliteRestaurant.Core.Menu;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurantPro.ApiClients;

public sealed class AdminSettingsApiClient(EliteApiClient? apiClient = null)
{
    private readonly EliteApiClient _apiClient = apiClient ?? new EliteApiClient();

    public async Task PushSettingsAsync(
        AppSettings settings,
        bool applyLogoChanges = false,
        bool applyOnlinePromoImageChanges = false,
        CancellationToken cancellationToken = default)
    {
        var logo = ReadLogo(settings.BusinessProfile.LogoPath);
        var promo = ReadOnlinePromo(settings.BusinessProfile.OnlinePromoImagePath);
        var menuBaseUrl = CloudEndpoints.NormalizeApiBaseUrl(
            string.IsNullOrWhiteSpace(settings.BusinessProfile.PublicMenuBaseUrl?.Trim())
                ? settings.CloudApi.BaseUrl
                : settings.BusinessProfile.PublicMenuBaseUrl);

        var request = new AdminCloudSettingsRequest(
            settings.BusinessProfile.RestaurantName,
            settings.BusinessProfile.Phone,
            settings.BusinessProfile.Address,
            settings.BusinessProfile.WebsiteDomain,
            settings.BusinessProfile.SocialMedia,
            settings.BusinessProfile.CustomerMenuTagline,
            settings.BusinessProfile.StaffLoginPasscode,
            settings.BusinessProfile.TicketFooterText,
            settings.BusinessProfile.TaxIdLegalInfo,
            settings.CurrencyPricing.DefaultCurrencyDisplayMode,
            settings.CurrencyPricing.UsdToFcRate,
            settings.CurrencyPricing.RoundingLine,
            settings.CurrencyPricing.RoundingSubtotal,
            settings.CurrencyPricing.RoundingGrandTotal,
            settings.CurrencyPricing.TaxPercent,
            settings.CurrencyPricing.ServicePercent,
            logo.FileName,
            logo.ContentType,
            logo.Base64,
            applyLogoChanges,
            menuBaseUrl,
            settings.BusinessProfile.OnlineOrdersTableId,
            settings.BusinessProfile.ReservationLeadDays,
            settings.BusinessProfile.ReservationMaxMonthsAhead,
            settings.BusinessProfile.OnlinePromoTitle,
            settings.BusinessProfile.OnlinePromoSubtitle,
            settings.BusinessProfile.OnlinePromoCtaLabel,
            promo.FileName,
            promo.ContentType,
            promo.Base64,
            applyOnlinePromoImageChanges,
            MenuTaxonomyHelper.Serialize(MenuTaxonomyHelper.Resolve(settings.MenuTaxonomy)));

        await _apiClient.PostAsync<AdminCloudSettingsRequest, AdminCloudSettingsResponse>(
            "api/admin/settings/cloud-profile",
            request,
            cancellationToken);
    }

    private static (string? FileName, string? ContentType, string? Base64) ReadLogo(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return (null, null, null);

        var bytes = File.ReadAllBytes(path);
        if (bytes.Length == 0 || bytes.Length > 4 * 1024 * 1024)
            return (null, null, null);

        var contentType = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            _ => "image/png"
        };

        return (Path.GetFileName(path), contentType, Convert.ToBase64String(bytes));
    }

    private static (string? FileName, string? ContentType, string? Base64) ReadOnlinePromo(string? path)
        => ReadLogo(path);
}
