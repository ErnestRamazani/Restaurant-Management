using System.IO;
using System.Text.Json;
using EliteRestaurant.Core.Menu;

namespace EliteRestaurant.Core.Utils;

public static class SettingsManager
{
    private const string SettingsFileName = "app-settings.json";

    /// <summary>
    /// Property names in JSON may be PascalCase (System.Text.Json default) or camelCase (hand-edited / other tools).
    /// Without case-insensitive matching, nested <c>menuTaxonomy.types</c> would not bind and would be replaced by defaults on load.
    /// </summary>
    private static readonly JsonSerializerOptions LoadAppSettingsOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions SaveAppSettingsOptions = new()
    {
        WriteIndented = true
    };

    public static event Action? SettingsChanged;

    public static AppSettings Load()
    {
        try
        {
            var path = GetSettingsPath();
            if (!File.Exists(path))
            {
                var fresh = new AppSettings();
                fresh.MenuTaxonomy = MenuTaxonomyHelper.Resolve(fresh.MenuTaxonomy);
                return fresh;
            }

            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, LoadAppSettingsOptions) ?? new AppSettings();
            loaded.Database ??= new DatabaseSettings();
            loaded.CloudApi ??= new CloudApiSettings();
            loaded.Attendance ??= new AttendanceSettings();
            loaded.MenuTaxonomy = MenuTaxonomyHelper.Resolve(loaded.MenuTaxonomy);
            if (DatabaseSettingsMigration.TryMigrateInMemory(loaded.Database))
                Save(loaded);
            return loaded;
        }
        catch
        {
            var fallback = new AppSettings();
            fallback.MenuTaxonomy = MenuTaxonomyHelper.Resolve(fallback.MenuTaxonomy);
            return fallback;
        }
    }

    public static void Save(AppSettings settings)
    {
        var dir = GetStorageDirectory();
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, SettingsFileName);
        var json = JsonSerializer.Serialize(settings, SaveAppSettingsOptions);
        File.WriteAllText(path, json);
        SettingsChanged?.Invoke();
    }

    private static string GetSettingsPath() => Path.Combine(GetStorageDirectory(), SettingsFileName);

    private static string GetStorageDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EliteRestaurantPro",
            "settings");
    }
}
