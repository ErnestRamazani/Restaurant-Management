using EliteRestaurant.Contracts.Admin;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
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
        row.RestaurantName = Normalize(request.RestaurantName, "Elite Restaurant");
        row.Phone = request.Phone?.Trim() ?? string.Empty;
        row.Address = request.Address?.Trim() ?? string.Empty;
        row.WebsiteDomain = request.WebsiteDomain?.Trim() ?? string.Empty;
        row.SocialMedia = request.SocialMedia?.Trim() ?? string.Empty;
        row.CustomerMenuTagline = string.IsNullOrWhiteSpace(request.CustomerMenuTagline)
            ? null
            : request.CustomerMenuTagline.Trim();
        row.StaffLoginPasscode = string.IsNullOrWhiteSpace(request.StaffLoginPasscode)
            ? "er4124"
            : request.StaffLoginPasscode.Trim();
        row.TicketFooterText = Normalize(request.TicketFooterText, "MERCI / THANK YOU");
        row.TaxIdLegalInfo = request.TaxIdLegalInfo?.Trim() ?? string.Empty;
        row.DefaultCurrencyDisplayMode = Normalize(request.DefaultCurrencyDisplayMode, "Dual");
        row.UsdToFcRate = request.UsdToFcRate > 0 ? request.UsdToFcRate : CurrencyHelper.DefaultFcPerUsd;
        row.RoundingLine = Normalize(request.RoundingLine, "Nearest");
        row.RoundingSubtotal = Normalize(request.RoundingSubtotal, "Nearest");
        row.RoundingGrandTotal = Normalize(request.RoundingGrandTotal, "Nearest");
        row.TaxPercent = Math.Max(0, request.TaxPercent);
        row.ServicePercent = Math.Max(0, request.ServicePercent);
        row.UpdatedAtUtc = DateTime.UtcNow;
        if (row.Id == 0)
            db.PublicMenuSettings.Add(row);

        SaveLogoIfPresent(request);
        await db.SaveChangesAsync(cancellationToken);

        // Keep the existing file-based settings as a local fallback for older deployments.
        var settings = SettingsManager.Load();
        settings.BusinessProfile.RestaurantName = row.RestaurantName;
        settings.BusinessProfile.Phone = row.Phone;
        settings.BusinessProfile.Address = row.Address;
        settings.BusinessProfile.WebsiteDomain = row.WebsiteDomain;
        settings.BusinessProfile.SocialMedia = row.SocialMedia;
        settings.BusinessProfile.CustomerMenuTagline = row.CustomerMenuTagline;
        settings.BusinessProfile.StaffLoginPasscode = row.StaffLoginPasscode;
        settings.BusinessProfile.TicketFooterText = row.TicketFooterText;
        settings.BusinessProfile.TaxIdLegalInfo = row.TaxIdLegalInfo;
        settings.BusinessProfile.PublicMenuBaseUrl = CloudEndpoints.ProductionApiBaseUrl;
        settings.CurrencyPricing.DefaultCurrencyDisplayMode = row.DefaultCurrencyDisplayMode;
        settings.CurrencyPricing.UsdToFcRate = row.UsdToFcRate;
        settings.CurrencyPricing.RoundingLine = row.RoundingLine;
        settings.CurrencyPricing.RoundingSubtotal = row.RoundingSubtotal;
        settings.CurrencyPricing.RoundingGrandTotal = row.RoundingGrandTotal;
        settings.CurrencyPricing.TaxPercent = row.TaxPercent;
        settings.CurrencyPricing.ServicePercent = row.ServicePercent;
        settings.CurrencyPricing.ExchangeRateLastUpdatedUtc = DateTime.UtcNow;
        SettingsManager.Save(settings);
        return Ok(new AdminCloudSettingsResponse(true, "/api/public/menu/assets/logo", "Cloud settings saved."));
    }

    private void SaveLogoIfPresent(AdminCloudSettingsRequest request)
    {
        // Persists to DB for deployments without a repo logo file. When assets/images/logo contains an image,
        // RestaurantWebLogoResolver serves that file first; DB logo is used only if no on-disk file is found.
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

    private static string Normalize(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
