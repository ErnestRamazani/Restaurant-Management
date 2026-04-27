using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurantPro.Utils;

public static class ThemeManager
{
    private const string PaletteFileName = "theme-palette.json";

    public static ThemePalette GetDefaultPalette() => new();

    public static ThemePalette GetCurrentPalette()
    {
        return new ThemePalette
        {
            BackgroundDark = ReadHex("BackgroundDarkBrush", "#FF0F1322"),
            BackgroundMedium = ReadHex("BackgroundMediumBrush", "#FF151B2D"),
            Sidebar = ReadHex("SidebarBrush", "#FF0C1120"),
            CardBase = ReadHex("CardBaseBrush", "#FF1A2236"),
            GoldAccent = ReadHex("GoldAccentBrush", "#FFD8B24A"),
            TextSecondary = ReadHex("TextSecondaryBrush", "#FFB3BCD3"),
            BorderSubtle = ReadHex("BorderSubtleBrush", "#FF35405A"),
            StatBlue = ReadHex("StatBlueBrush", "#FF4DA3FF"),
            StatGreen = ReadHex("StatGreenBrush", "#FF59D18C"),
            StatRed = ReadHex("StatRedBrush", "#FFFF7676")
        };
    }

    public static void ApplySavedPalette()
    {
        var saved = LoadPalette();
        if (saved is null)
            return;

        ApplyPalette(saved);
    }

    public static void SavePalette(ThemePalette palette)
    {
        var dir = GetStorageDirectory();
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, PaletteFileName);
        var json = JsonSerializer.Serialize(palette, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public static void ApplyPalette(ThemePalette palette)
    {
        // Keep "glass" translucency globally for all menus, even when users
        // choose fully opaque custom colors in settings.
        var backgroundDarkGlass = ForceAlpha(palette.BackgroundDark, 0xB3);
        var backgroundMediumGlass = ForceAlpha(palette.BackgroundMedium, 0x7A);
        var sidebarGlass = ForceAlpha(palette.Sidebar, 0x96);
        var cardBaseGlass = ForceAlpha(palette.CardBase, 0x88);

        ApplyColor("BackgroundDarkColor", backgroundDarkGlass);
        ApplyColor("BackgroundMediumColor", backgroundMediumGlass);
        ApplyColor("SidebarColor", sidebarGlass);
        ApplyColor("CardBaseColor", cardBaseGlass);
        ApplyColor("GoldAccentColor", palette.GoldAccent);
        ApplyColor("TextSecondaryColor", palette.TextSecondary);
        ApplyColor("BorderSubtleColor", palette.BorderSubtle);
        ApplyColor("StatBlueColor", palette.StatBlue);
        ApplyColor("StatGreenColor", palette.StatGreen);
        ApplyColor("StatRedColor", palette.StatRed);

        ApplyBrush("BackgroundDarkBrush", backgroundDarkGlass);
        ApplyBrush("BackgroundMediumBrush", backgroundMediumGlass);
        ApplyBrush("SidebarBrush", sidebarGlass);
        ApplyBrush("CardBaseBrush", cardBaseGlass);
        ApplyBrush("GoldAccentBrush", palette.GoldAccent);
        ApplyBrush("TextSecondaryBrush", palette.TextSecondary);
        ApplyBrush("BorderSubtleBrush", palette.BorderSubtle);
        ApplyBrush("StatBlueBrush", palette.StatBlue);
        ApplyBrush("StatGreenBrush", palette.StatGreen);
        ApplyBrush("StatRedBrush", palette.StatRed);
    }

    private static ThemePalette? LoadPalette()
    {
        try
        {
            var path = Path.Combine(GetStorageDirectory(), PaletteFileName);
            if (!File.Exists(path))
                return null;
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ThemePalette>(json);
        }
        catch
        {
            return null;
        }
    }

    private static string GetStorageDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EliteRestaurantPro",
            "settings");
    }

    private static void ApplyColor(string key, string hex)
    {
        if (!TryParseColor(hex, out var color))
            return;

        if (TryGetResourceOwner(key, out var owner))
        {
            owner[key] = color;
            return;
        }

        Application.Current.Resources[key] = color;
    }

    private static void ApplyBrush(string key, string hex)
    {
        if (!TryParseColor(hex, out var color))
            return;

        if (TryFindResourceRecursive(Application.Current.Resources, key, out var value) && value is SolidColorBrush brush)
        {
            if (brush.IsFrozen)
            {
                if (TryGetResourceOwner(key, out var owner))
                {
                    owner[key] = new SolidColorBrush(color);
                }
            }
            else
            {
                brush.Color = color;
            }

            return;
        }

        Application.Current.Resources[key] = new SolidColorBrush(color);
    }

    private static string ReadHex(string brushKey, string fallback)
    {
        if (!TryFindResourceRecursive(Application.Current.Resources, brushKey, out var value))
            return fallback;
        if (value is not SolidColorBrush brush)
            return fallback;
        return brush.Color.ToString();
    }

    private static bool TryParseColor(string hex, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex))
            return false;

        try
        {
            var normalized = NormalizeHex(hex);
            color = (Color)ColorConverter.ConvertFromString(normalized);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string NormalizeHex(string value)
    {
        var trimmed = value.Trim();
        if (!trimmed.StartsWith('#'))
            trimmed = "#" + trimmed;

        if (trimmed.Length == 7)
            trimmed = "#FF" + trimmed[1..];

        return trimmed.ToUpper(CultureInfo.InvariantCulture);
    }

    private static string ForceAlpha(string value, byte alpha)
    {
        var normalized = NormalizeHex(value);
        if (!TryParseColor(normalized, out var color))
            return normalized;

        return Color.FromArgb(alpha, color.R, color.G, color.B).ToString();
    }

    private static bool TryGetResourceOwner(object key, out ResourceDictionary owner)
    {
        return TryGetResourceOwnerRecursive(Application.Current.Resources, key, out owner);
    }

    private static bool TryGetResourceOwnerRecursive(ResourceDictionary dictionary, object key, out ResourceDictionary owner)
    {
        if (dictionary.Contains(key))
        {
            owner = dictionary;
            return true;
        }

        foreach (var merged in dictionary.MergedDictionaries)
        {
            if (TryGetResourceOwnerRecursive(merged, key, out owner))
                return true;
        }

        owner = null!;
        return false;
    }

    private static bool TryFindResourceRecursive(ResourceDictionary dictionary, object key, out object value)
    {
        if (dictionary.Contains(key))
        {
            value = dictionary[key];
            return true;
        }

        foreach (var merged in dictionary.MergedDictionaries)
        {
            if (TryFindResourceRecursive(merged, key, out value))
                return true;
        }

        value = null!;
        return false;
    }
}
