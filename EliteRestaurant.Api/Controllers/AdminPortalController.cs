using EliteRestaurant.Api.Dtos;
using EliteRestaurant.Api.Branding;
using EliteRestaurant.Api.Services;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/admin/portal")]
public sealed class AdminPortalController(
    AppDbContext db,
    IWebHostEnvironment environment,
    PublicMenuSettingsCache menuSettings) : ControllerBase
{
    [HttpGet("config")]
    [Authorize(Policy = "AdminRead")]
    public ActionResult<AdminPortalConfigDto> GetConfig()
    {
        var allSettings = SettingsManager.Load();
        var business = allSettings.BusinessProfile;
        var cloudSettings = menuSettings.GetDefault();
        var restaurantName = PublicMenuBrandingMerge.RestaurantDisplayName(cloudSettings, business);
        var logoUrl = "/api/admin/portal/assets/restaurant-logo";
        var (adminWebSignInId, _) = AdminWebSettingsResolver.Resolve(db);
        var signInHint = string.IsNullOrWhiteSpace(adminWebSignInId) ? null : adminWebSignInId.Trim();
        return Ok(new AdminPortalConfigDto(
            restaurantName,
            logoUrl,
            signInHint,
            RestaurantTimeZone.ResolveId(cloudSettings, business)));
    }

    [HttpGet("login-hint")]
    [AllowAnonymous]
    public ActionResult<AdminPortalLoginHintDto> GetLoginHint()
    {
        var (adminWebSignInId, _) = AdminWebSettingsResolver.Resolve(db);
        var signInHint = string.IsNullOrWhiteSpace(adminWebSignInId) ? null : adminWebSignInId.Trim();
        return Ok(new AdminPortalLoginHintDto(signInHint));
    }

    [HttpGet("assets/restaurant-logo")]
    [Authorize(Policy = "AdminRead")]
    public IActionResult GetRestaurantLogo()
    {
        var repoLogo = RestaurantWebLogoResolver.TryResolveRepoLogoPath(environment);

        var asset = db.PublicMenuAssets.AsNoTracking().FirstOrDefault(a => a.Key == "logo");
        if (asset is { Content.Length: > 0 })
        {
            var contentType = string.IsNullOrWhiteSpace(asset.ContentType)
                ? "image/png"
                : asset.ContentType;
            return File(asset.Content, contentType);
        }

        if (repoLogo is not null && System.IO.File.Exists(repoLogo))
        {
            var bytes = System.IO.File.ReadAllBytes(repoLogo);
            return File(bytes, RestaurantWebLogoResolver.GetContentTypeForPath(repoLogo));
        }

        var logoPath = SettingsManager.Load().BusinessProfile.LogoPath?.Trim() ?? string.Empty;
        return ServeImageFromPath(logoPath);
    }

    private IActionResult ServeImageFromPath(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath) || !System.IO.File.Exists(absolutePath))
            return NotFound();

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(absolutePath, out var contentType))
            contentType = "application/octet-stream";

        var bytes = System.IO.File.ReadAllBytes(absolutePath);
        return File(bytes, contentType);
    }
}
