using System.Text;
using System.Text.RegularExpressions;

namespace EliteRestaurant.Core.Utils;

public static partial class RestaurantSlug
{
    public static string Normalize(string? value, string? fallbackName = null)
    {
        var source = string.IsNullOrWhiteSpace(value)
            ? (fallbackName ?? string.Empty)
            : value.Trim();

        var lower = source.ToLowerInvariant();
        var sb = new StringBuilder(lower.Length);
        var lastWasDash = false;

        foreach (var ch in lower)
        {
            if (char.IsAsciiLetterOrDigit(ch))
            {
                sb.Append(ch);
                lastWasDash = false;
            }
            else if (!lastWasDash && sb.Length > 0)
            {
                sb.Append('-');
                lastWasDash = true;
            }
        }

        var slug = sb.ToString().Trim('-');
        if (slug.Length > 64)
            slug = slug[..64].TrimEnd('-');

        return slug;
    }

    public static bool IsValid(string slug) =>
        slug.Length >= 2
        && slug.Length <= 64
        && SlugPattern().IsMatch(slug);

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();
}
