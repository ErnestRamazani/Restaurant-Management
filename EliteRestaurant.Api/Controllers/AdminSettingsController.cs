using EliteRestaurant.Contracts.Admin;
using EliteRestaurant.Core.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/admin/settings")]
[AllowAnonymous]
public sealed class AdminSettingsController : ControllerBase
{
    [HttpPost("cloud-profile")]
    public ActionResult<AdminCloudSettingsResponse> SaveCloudProfile(AdminCloudSettingsRequest request)
    {
        var settings = SettingsManager.Load();
        settings.BusinessProfile.RestaurantName = Normalize(request.RestaurantName, "Elite Restaurant");
        settings.BusinessProfile.Phone = request.Phone?.Trim() ?? string.Empty;
        settings.BusinessProfile.Address = request.Address?.Trim() ?? string.Empty;
        settings.BusinessProfile.WebsiteDomain = request.WebsiteDomain?.Trim() ?? string.Empty;
        settings.BusinessProfile.SocialMedia = request.SocialMedia?.Trim() ?? string.Empty;
        settings.BusinessProfile.CustomerMenuTagline = string.IsNullOrWhiteSpace(request.CustomerMenuTagline)
            ? null
            : request.CustomerMenuTagline.Trim();
        settings.BusinessProfile.StaffLoginPasscode = string.IsNullOrWhiteSpace(request.StaffLoginPasscode)
            ? "er4124"
            : request.StaffLoginPasscode.Trim();
        settings.BusinessProfile.TicketFooterText = Normalize(request.TicketFooterText, "MERCI / THANK YOU");
        settings.BusinessProfile.TaxIdLegalInfo = request.TaxIdLegalInfo?.Trim() ?? string.Empty;
        settings.BusinessProfile.PublicMenuBaseUrl = CloudEndpoints.ProductionApiBaseUrl;

        settings.CurrencyPricing.DefaultCurrencyDisplayMode = Normalize(request.DefaultCurrencyDisplayMode, "Dual");
        settings.CurrencyPricing.UsdToFcRate = request.UsdToFcRate > 0 ? request.UsdToFcRate : CurrencyHelper.DefaultFcPerUsd;
        settings.CurrencyPricing.RoundingLine = Normalize(request.RoundingLine, "Nearest");
        settings.CurrencyPricing.RoundingSubtotal = Normalize(request.RoundingSubtotal, "Nearest");
        settings.CurrencyPricing.RoundingGrandTotal = Normalize(request.RoundingGrandTotal, "Nearest");
        settings.CurrencyPricing.TaxPercent = Math.Max(0, request.TaxPercent);
        settings.CurrencyPricing.ServicePercent = Math.Max(0, request.ServicePercent);
        settings.CurrencyPricing.ExchangeRateLastUpdatedUtc = DateTime.UtcNow;

        var logoPath = SaveLogoIfPresent(request);
        if (!string.IsNullOrWhiteSpace(logoPath))
            settings.BusinessProfile.LogoPath = logoPath;

        SettingsManager.Save(settings);
        return Ok(new AdminCloudSettingsResponse(true, "/api/public/menu/assets/logo", "Cloud settings saved."));
    }

    private static string? SaveLogoIfPresent(AdminCloudSettingsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.LogoBase64))
            return null;

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(request.LogoBase64);
        }
        catch
        {
            return null;
        }

        if (bytes.Length == 0 || bytes.Length > 4 * 1024 * 1024)
            return null;

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

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EliteRestaurantPro",
            "cloud-assets");
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, "restaurant-logo" + extension.ToLowerInvariant());
        System.IO.File.WriteAllBytes(path, bytes);
        return path;
    }

    private static string Normalize(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
