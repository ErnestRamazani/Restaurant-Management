using System.Text;
using System.Text.Json;

namespace EliteRestaurantPro.Utils;

internal static class JwtPayloadReader
{
    public static bool TryGetRestaurantId(string jwt, out int restaurantId)
    {
        restaurantId = 0;
        if (string.IsNullOrWhiteSpace(jwt))
            return false;

        var parts = jwt.Split('.');
        if (parts.Length < 2)
            return false;

        try
        {
            var json = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("restaurantId", out var claim))
            {
                if (claim.ValueKind == JsonValueKind.Number && claim.TryGetInt32(out restaurantId))
                    return restaurantId > 0;
                if (claim.ValueKind == JsonValueKind.String
                    && int.TryParse(claim.GetString(), out restaurantId))
                    return restaurantId > 0;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static byte[] Base64UrlDecode(string segment)
    {
        var padded = segment.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Convert.FromBase64String(padded);
    }
}
