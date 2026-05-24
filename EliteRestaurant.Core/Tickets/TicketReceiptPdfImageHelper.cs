namespace EliteRestaurant.Core.Tickets;

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

    public static string GetImageContentType(byte[] bytes)
    {
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            return "image/png";

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";

        if (bytes.Length >= 6 &&
            bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
            return "image/gif";

        if (bytes.Length >= 12 &&
            bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
            return "image/webp";

        if (bytes.Length >= 2 && bytes[0] == 0x42 && bytes[1] == 0x4D)
            return "image/bmp";

        return "image/png";
    }
}
