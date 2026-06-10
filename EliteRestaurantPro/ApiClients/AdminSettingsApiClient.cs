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
        bool applyTicketBrandingChanges = false,
        CancellationToken cancellationToken = default)
    {
        var logo = ReadImageFile(settings.BusinessProfile.LogoPath);
        var promo = ReadImageFile(settings.BusinessProfile.OnlinePromoImagePath);
        var ticketHeader = ReadImageFile(settings.TicketReceipt?.HeaderLogoPath);
        var ticketSocialRows = ReadTicketSocialRows(settings.TicketReceipt);
        var pushApiBaseUrl = EliteApiClient.ResolvePublicMenuCloudBaseUrl(settings);
        var menuBaseUrl = pushApiBaseUrl;

        settings.Salary ??= new SalarySettings();
        settings.TicketReceipt ??= new TicketReceiptSettings();

        var request = new AdminCloudSettingsRequest(
            settings.BusinessProfile.RestaurantName,
            settings.BusinessProfile.Phone,
            settings.BusinessProfile.Address,
            settings.BusinessProfile.WebsiteDomain,
            settings.BusinessProfile.SocialMedia,
            settings.BusinessProfile.CustomerMenuTagline,
            settings.BusinessProfile.StaffLoginPasscode,
            settings.BusinessProfile.OrderCancelPasscode,
            settings.BusinessProfile.AdminWebSignInId,
            settings.BusinessProfile.AdminWebPin,
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
            MenuTaxonomyHelper.Serialize(MenuTaxonomyHelper.Resolve(settings.MenuTaxonomy)),
            settings.Salary.LateDaysPerAttendanceUnit,
            settings.Salary.AbsenceCountsAsAttendanceUnit,
            settings.Salary.SalesBonusPercent,
            settings.Salary.MaxSalaryAdvancePercentOfGross,
            settings.BusinessProfile.CustomerMenuAboutText,
            settings.BusinessProfile.CustomerMenuContactIntro,
            settings.BusinessProfile.CustomerMenuNotesText,
            settings.BusinessProfile.ClientDebtCapUsd,
            settings.BusinessProfile.RestaurantTimeZoneId,
            ticketHeader.FileName,
            ticketHeader.ContentType,
            ticketHeader.Base64,
            applyTicketBrandingChanges,
            ticketSocialRows);

        await _apiClient.PostAsync<AdminCloudSettingsRequest, AdminCloudSettingsResponse>(
            pushApiBaseUrl,
            "api/admin/settings/cloud-profile",
            request,
            cancellationToken);
    }

    private static IReadOnlyList<TicketSocialMediaCloudRowDto> ReadTicketSocialRows(TicketReceiptSettings? ticketReceipt)
    {
        var rows = ticketReceipt?.SocialMediaRows ?? [];
        return rows.Select(row =>
        {
            var icon = ReadImageFile(row.IconPath);
            return new TicketSocialMediaCloudRowDto(
                row.PlatformName ?? string.Empty,
                row.UserText ?? string.Empty,
                icon.FileName,
                icon.ContentType,
                icon.Base64);
        }).ToList();
    }

    private static (string? FileName, string? ContentType, string? Base64) ReadImageFile(string? path)
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
            ".bmp" => "image/bmp",
            _ => "image/png"
        };

        return (Path.GetFileName(path), contentType, Convert.ToBase64String(bytes));
    }
}
