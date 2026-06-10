using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Tickets;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Api.Branding;

/// <summary>Resolves ticket header logo and social footer rows for server-generated receipts.</summary>
public static class TicketBrandingImageResolver
{
    public static byte[]? ResolveHeaderLogoBytes(AppSettings settings, AppDbContext db, IWebHostEnvironment env)
    {
        var ticketReceipt = settings.TicketReceipt ?? new TicketReceiptSettings();
        var fromTicketPath = TicketReceiptPdfImageHelper.TryLoadRasterImage(ticketReceipt.HeaderLogoPath);
        if (fromTicketPath is { Length: > 0 })
            return fromTicketPath;

        var ticketHeaderAsset = db.PublicMenuAssets.AsNoTracking()
            .FirstOrDefault(a => a.Key == TicketBrandingAssetKeys.HeaderLogo);
        if (ticketHeaderAsset?.Content is { Length: > 0 })
            return ticketHeaderAsset.Content;

        var asset = db.PublicMenuAssets.AsNoTracking().FirstOrDefault(a => a.Key == "logo");
        if (asset?.Content is { Length: > 0 })
            return asset.Content;

        var repoPath = RestaurantWebLogoResolver.TryResolveRepoLogoPath(env);
        if (!string.IsNullOrWhiteSpace(repoPath))
            return TicketReceiptPdfImageHelper.TryLoadRasterImage(repoPath);

        var businessLogoPath = settings.BusinessProfile.LogoPath?.Trim() ?? string.Empty;
        return TicketReceiptPdfImageHelper.TryLoadRasterImage(businessLogoPath);
    }

    public static IReadOnlyList<TicketSocialMediaPdfRow> ResolveSocialFooterRows(
        AppDbContext db,
        PublicMenuSetting? cloud,
        AppSettings settings)
    {
        var cloudRows = ResolveSocialFooterRowsFromCloud(db, cloud);
        if (cloudRows.Count > 0)
            return cloudRows;

        return ResolveSocialFooterRowsFromLocal(settings);
    }

    private static List<TicketSocialMediaPdfRow> ResolveSocialFooterRowsFromCloud(
        AppDbContext db,
        PublicMenuSetting? cloud)
    {
        var entries = TicketSocialMediaCloudJson.Deserialize(cloud?.TicketSocialMediaJson);
        if (entries.Count == 0)
            return [];

        var iconAssets = db.PublicMenuAssets.AsNoTracking()
            .Where(a => a.Key.StartsWith(TicketBrandingAssetKeys.SocialIconPrefix))
            .ToDictionary(a => a.Key, a => a.Content, StringComparer.Ordinal);

        var rows = new List<TicketSocialMediaPdfRow>();
        foreach (var entry in entries)
        {
            var plat = (entry.PlatformName ?? string.Empty).Trim();
            var user = (entry.UserText ?? string.Empty).Trim();
            if (plat.Length == 0 && user.Length == 0)
                continue;

            byte[]? iconBytes = null;
            var iconKey = (entry.IconKey ?? string.Empty).Trim();
            if (iconKey.Length > 0 &&
                iconAssets.TryGetValue(iconKey, out var bytes) &&
                bytes is { Length: > 0 })
            {
                iconBytes = bytes;
            }

            rows.Add(new TicketSocialMediaPdfRow(plat, user, iconBytes));
        }

        return rows;
    }

    private static List<TicketSocialMediaPdfRow> ResolveSocialFooterRowsFromLocal(AppSettings settings)
    {
        var ticketReceipt = settings.TicketReceipt ?? new TicketReceiptSettings();
        var rows = new List<TicketSocialMediaPdfRow>();
        foreach (var row in ticketReceipt.SocialMediaRows)
        {
            var plat = (row.PlatformName ?? string.Empty).Trim();
            var user = (row.UserText ?? string.Empty).Trim();
            if (plat.Length == 0 && user.Length == 0)
                continue;

            var iconBytes = TicketReceiptPdfImageHelper.TryLoadRasterImage(row.IconPath);
            rows.Add(new TicketSocialMediaPdfRow(plat, user, iconBytes));
        }

        return rows;
    }
}
