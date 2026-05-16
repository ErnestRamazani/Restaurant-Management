using System.IO;

namespace EliteRestaurantPro.Services;

/// <summary>Loads local raster files for QuestPDF ticket images (PNG/JPEG/WebP/GIF/BMP).</summary>
public static class TicketReceiptPdfImageHelper
{
    private static readonly HashSet<string> AllowedExt =
    [
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp"
    ];

    public static byte[]? TryLoadRasterImage(string? path, int maxBytes = 4 * 1024 * 1024)
    {
        try
        {
            var p = (path ?? string.Empty).Trim();
            if (p.Length == 0 || !File.Exists(p))
                return null;
            var ext = Path.GetExtension(p);
            if (!AllowedExt.Contains(ext, StringComparer.OrdinalIgnoreCase))
                return null;
            var bytes = File.ReadAllBytes(p);
            if (bytes.Length == 0 || bytes.Length > maxBytes)
                return null;
            return bytes;
        }
        catch
        {
            return null;
        }
    }
}
