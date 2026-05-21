using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Tickets;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Api.Branding;

/// <summary>Resolves ticket header logo bytes for server-generated PDFs (same sources as desktop when available).</summary>
public static class TicketBrandingImageResolver
{
    public static byte[]? ResolveHeaderLogoBytes(AppSettings settings, AppDbContext db, IWebHostEnvironment env)
    {
        var ticketReceipt = settings.TicketReceipt ?? new TicketReceiptSettings();
        var fromTicketPath = TicketReceiptPdfImageHelper.TryLoadRasterImage(ticketReceipt.HeaderLogoPath);
        if (fromTicketPath is { Length: > 0 })
            return fromTicketPath;

        var asset = db.PublicMenuAssets.AsNoTracking().FirstOrDefault(a => a.Key == "logo");
        if (asset?.Content is { Length: > 0 })
            return asset.Content;

        var repoPath = RestaurantWebLogoResolver.TryResolveRepoLogoPath(env);
        if (!string.IsNullOrWhiteSpace(repoPath))
            return TicketReceiptPdfImageHelper.TryLoadRasterImage(repoPath);

        var businessLogoPath = settings.BusinessProfile.LogoPath?.Trim() ?? string.Empty;
        return TicketReceiptPdfImageHelper.TryLoadRasterImage(businessLogoPath);
    }
}
