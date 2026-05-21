using EliteRestaurant.Contracts.Admin;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Menu;
using EliteRestaurant.Core.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/admin/settings")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminSettingsController(AppDbContext db) : ControllerBase
{
    [HttpPost("cloud-profile")]
    public async Task<ActionResult<AdminCloudSettingsResponse>> SaveCloudProfile(
        AdminCloudSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var row = await db.PublicMenuSettings.FirstOrDefaultAsync(s => s.Key == "default", cancellationToken)
                  ?? new PublicMenuSetting { Key = "default" };
        row.RestaurantName = request.RestaurantName?.Trim() ?? string.Empty;
        row.Phone = request.Phone?.Trim() ?? string.Empty;
        row.Address = request.Address?.Trim() ?? string.Empty;
        row.WebsiteDomain = request.WebsiteDomain?.Trim() ?? string.Empty;
        row.SocialMedia = request.SocialMedia?.Trim() ?? string.Empty;
        row.CustomerMenuTagline = string.IsNullOrWhiteSpace(request.CustomerMenuTagline)
            ? null
            : request.CustomerMenuTagline.Trim();
        row.CustomerMenuAboutText = string.IsNullOrWhiteSpace(request.CustomerMenuAboutText)
            ? null
            : request.CustomerMenuAboutText.Trim();
        row.CustomerMenuContactIntro = string.IsNullOrWhiteSpace(request.CustomerMenuContactIntro)
            ? null
            : request.CustomerMenuContactIntro.Trim();
        row.CustomerMenuNotesText = string.IsNullOrWhiteSpace(request.CustomerMenuNotesText)
            ? null
            : request.CustomerMenuNotesText.Trim();
        row.StaffLoginPasscode = (request.StaffLoginPasscode ?? string.Empty).Trim();
        row.AdminWebSignInId = (request.AdminWebSignInId ?? string.Empty).Trim();
        row.AdminWebPin = (request.AdminWebPin ?? string.Empty).Trim();
        row.TicketFooterText = request.TicketFooterText?.Trim() ?? string.Empty;
        row.TaxIdLegalInfo = request.TaxIdLegalInfo?.Trim() ?? string.Empty;
        row.DefaultCurrencyDisplayMode = Normalize(request.DefaultCurrencyDisplayMode, "Dual");
        row.UsdToFcRate = request.UsdToFcRate > 0 ? request.UsdToFcRate : CurrencyHelper.DefaultFcPerUsd;
        row.RoundingLine = Normalize(request.RoundingLine, "Nearest");
        row.RoundingSubtotal = Normalize(request.RoundingSubtotal, "Nearest");
        row.RoundingGrandTotal = Normalize(request.RoundingGrandTotal, "Nearest");
        row.TaxPercent = Math.Max(0, request.TaxPercent);
        row.ServicePercent = Math.Max(0, request.ServicePercent);
        row.OnlineOrdersTableId = request.OnlineOrdersTableId;
        row.ReservationLeadDays = Math.Clamp(request.ReservationLeadDays, 0, 30);
        row.ReservationMaxMonthsAhead = Math.Clamp(request.ReservationMaxMonthsAhead, 1, 24);
        row.OnlinePromoTitle = string.IsNullOrWhiteSpace(request.OnlinePromoTitle)
            ? null
            : request.OnlinePromoTitle.Trim();
        row.OnlinePromoSubtitle = string.IsNullOrWhiteSpace(request.OnlinePromoSubtitle)
            ? null
            : request.OnlinePromoSubtitle.Trim();
        row.OnlinePromoCtaLabel = string.IsNullOrWhiteSpace(request.OnlinePromoCtaLabel)
            ? null
            : request.OnlinePromoCtaLabel.Trim();
        if (!string.IsNullOrWhiteSpace(request.MenuTaxonomyJson) &&
            MenuTaxonomyHelper.TryDeserialize(request.MenuTaxonomyJson.Trim(), out var menuTaxonomy) &&
            menuTaxonomy is not null)
        {
            row.MenuTaxonomyJson = MenuTaxonomyHelper.Serialize(menuTaxonomy);
        }

        row.PayrollLateDaysPerAttendanceUnit = Math.Max(1, request.PayrollLateDaysPerAttendanceUnit);
        row.PayrollAbsenceCountsAsAttendanceUnit = request.PayrollAbsenceCountsAsAttendanceUnit;
        row.PayrollSalesBonusPercent = Math.Clamp(request.PayrollSalesBonusPercent, 0m, 100m);
        row.PayrollMaxSalaryAdvancePercentOfGross = Math.Clamp(request.PayrollMaxSalaryAdvancePercentOfGross, 0m, 100m);

        row.UpdatedAtUtc = DateTime.UtcNow;
        if (row.Id == 0)
            db.PublicMenuSettings.Add(row);

        MergeLogoAssets(request);
        MergeOnlinePromoAssets(request);
        await db.SaveChangesAsync(cancellationToken);
        AdminWebLoginSeed.EnsureSeeded(db);

        // Keep the existing file-based settings as a local fallback for older deployments.
        var settings = SettingsManager.Load();
        var preservedTicketReceipt = settings.TicketReceipt ?? new TicketReceiptSettings();
        settings.BusinessProfile.RestaurantName = row.RestaurantName;
        settings.BusinessProfile.Phone = row.Phone;
        settings.BusinessProfile.Address = row.Address;
        settings.BusinessProfile.WebsiteDomain = row.WebsiteDomain;
        settings.BusinessProfile.SocialMedia = row.SocialMedia;
        settings.BusinessProfile.CustomerMenuTagline = row.CustomerMenuTagline;
        settings.BusinessProfile.CustomerMenuAboutText = row.CustomerMenuAboutText;
        settings.BusinessProfile.CustomerMenuContactIntro = row.CustomerMenuContactIntro;
        settings.BusinessProfile.CustomerMenuNotesText = row.CustomerMenuNotesText;
        settings.BusinessProfile.StaffLoginPasscode = row.StaffLoginPasscode;
        settings.BusinessProfile.AdminWebSignInId = row.AdminWebSignInId;
        settings.BusinessProfile.AdminWebPin = row.AdminWebPin;
        settings.BusinessProfile.TicketFooterText = row.TicketFooterText;
        settings.BusinessProfile.TaxIdLegalInfo = row.TaxIdLegalInfo;
        if (!string.IsNullOrWhiteSpace(request.PublicMenuBaseUrl))
            settings.BusinessProfile.PublicMenuBaseUrl =
                CloudEndpoints.NormalizeApiBaseUrl(request.PublicMenuBaseUrl.Trim());
        settings.CurrencyPricing.DefaultCurrencyDisplayMode = row.DefaultCurrencyDisplayMode;
        settings.CurrencyPricing.UsdToFcRate = row.UsdToFcRate;
        settings.CurrencyPricing.RoundingLine = row.RoundingLine;
        settings.CurrencyPricing.RoundingSubtotal = row.RoundingSubtotal;
        settings.CurrencyPricing.RoundingGrandTotal = row.RoundingGrandTotal;
        settings.CurrencyPricing.TaxPercent = row.TaxPercent;
        settings.CurrencyPricing.ServicePercent = row.ServicePercent;
        settings.CurrencyPricing.ExchangeRateLastUpdatedUtc = DateTime.UtcNow;
        settings.BusinessProfile.OnlineOrdersTableId = row.OnlineOrdersTableId;
        settings.BusinessProfile.ReservationLeadDays = Math.Clamp(request.ReservationLeadDays, 0, 30);
        settings.BusinessProfile.ReservationMaxMonthsAhead = Math.Clamp(request.ReservationMaxMonthsAhead, 1, 24);
        settings.BusinessProfile.OnlinePromoTitle = row.OnlinePromoTitle;
        settings.BusinessProfile.OnlinePromoSubtitle = row.OnlinePromoSubtitle;
        settings.BusinessProfile.OnlinePromoCtaLabel = row.OnlinePromoCtaLabel;
        if (!string.IsNullOrWhiteSpace(request.MenuTaxonomyJson) &&
            MenuTaxonomyHelper.TryDeserialize(request.MenuTaxonomyJson.Trim(), out var pushedTaxonomy))
            settings.MenuTaxonomy = pushedTaxonomy;
        settings.Salary ??= new SalarySettings();
        settings.Salary.LateDaysPerAttendanceUnit = Math.Max(1, request.PayrollLateDaysPerAttendanceUnit);
        settings.Salary.AbsenceCountsAsAttendanceUnit = request.PayrollAbsenceCountsAsAttendanceUnit;
        settings.Salary.SalesBonusPercent = Math.Clamp(request.PayrollSalesBonusPercent, 0m, 100m);
        settings.Salary.MaxSalaryAdvancePercentOfGross = Math.Clamp(request.PayrollMaxSalaryAdvancePercentOfGross, 0m, 100m);
        settings.TicketReceipt = preservedTicketReceipt;
        SettingsManager.Save(settings);
        return Ok(new AdminCloudSettingsResponse(true, "/api/public/menu/assets/logo", "Cloud settings saved."));
    }

    private void MergeLogoAssets(AdminCloudSettingsRequest request)
    {
        if (!request.ApplyLogoChanges)
            return;

        if (string.IsNullOrWhiteSpace(request.LogoBase64))
        {
            var stale = db.PublicMenuAssets.FirstOrDefault(a => a.Key == "logo");
            if (stale is not null)
                db.PublicMenuAssets.Remove(stale);

            return;
        }

        SaveLogoFromPayload(request);
    }

    private void SaveLogoFromPayload(AdminCloudSettingsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.LogoBase64))
            return;

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(request.LogoBase64);
        }
        catch
        {
            return;
        }

        if (bytes.Length == 0 || bytes.Length > 4 * 1024 * 1024)
            return;

        var extension = Path.GetExtension(request.LogoFileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(extension)
            && !string.IsNullOrWhiteSpace(request.LogoContentType)
            && new FileExtensionContentTypeProvider().Mappings
                .FirstOrDefault(m => string.Equals(m.Value, request.LogoContentType, StringComparison.OrdinalIgnoreCase)).Key is { } mapped)
        {
            extension = mapped;
        }

        if (string.IsNullOrWhiteSpace(extension))
            extension = ".png";

        var asset = db.PublicMenuAssets.FirstOrDefault(a => a.Key == "logo")
                    ?? new PublicMenuAsset { Key = "logo" };
        asset.FileName = string.IsNullOrWhiteSpace(request.LogoFileName)
            ? "restaurant-logo" + extension.ToLowerInvariant()
            : request.LogoFileName.Trim();
        asset.ContentType = string.IsNullOrWhiteSpace(request.LogoContentType)
            ? "image/png"
            : request.LogoContentType.Trim();
        asset.Content = bytes;
        asset.UpdatedAtUtc = DateTime.UtcNow;
        if (asset.Id == 0)
            db.PublicMenuAssets.Add(asset);
    }

    private void MergeOnlinePromoAssets(AdminCloudSettingsRequest request)
    {
        if (!request.ApplyOnlinePromoImageChanges)
            return;

        if (string.IsNullOrWhiteSpace(request.OnlinePromoImageBase64))
        {
            var stale = db.PublicMenuAssets.FirstOrDefault(a => a.Key == "online-promo");
            if (stale is not null)
                db.PublicMenuAssets.Remove(stale);
            return;
        }

        SaveOnlinePromoFromPayload(request);
    }

    private void SaveOnlinePromoFromPayload(AdminCloudSettingsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OnlinePromoImageBase64))
            return;

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(request.OnlinePromoImageBase64);
        }
        catch
        {
            return;
        }

        if (bytes.Length == 0 || bytes.Length > 4 * 1024 * 1024)
            return;

        var extension = Path.GetExtension(request.OnlinePromoImageFileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(extension)
            && !string.IsNullOrWhiteSpace(request.OnlinePromoImageContentType)
            && new FileExtensionContentTypeProvider().Mappings
                .FirstOrDefault(m => string.Equals(m.Value, request.OnlinePromoImageContentType, StringComparison.OrdinalIgnoreCase)).Key is { } mapped)
        {
            extension = mapped;
        }

        if (string.IsNullOrWhiteSpace(extension))
            extension = ".jpg";

        var asset = db.PublicMenuAssets.FirstOrDefault(a => a.Key == "online-promo")
                    ?? new PublicMenuAsset { Key = "online-promo" };
        asset.FileName = string.IsNullOrWhiteSpace(request.OnlinePromoImageFileName)
            ? "online-promo" + extension.ToLowerInvariant()
            : request.OnlinePromoImageFileName.Trim();
        asset.ContentType = string.IsNullOrWhiteSpace(request.OnlinePromoImageContentType)
            ? "image/jpeg"
            : request.OnlinePromoImageContentType.Trim();
        asset.Content = bytes;
        asset.UpdatedAtUtc = DateTime.UtcNow;
        if (asset.Id == 0)
            db.PublicMenuAssets.Add(asset);
    }

    private static string Normalize(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
