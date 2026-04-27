using System.IO;
using System.Text.Json;

namespace EliteRestaurant.Core.Utils;

public static class SettingsManager
{
    private const string SettingsFileName = "app-settings.json";
    public static event Action? SettingsChanged;

    public static AppSettings Load()
    {
        try
        {
            var path = GetSettingsPath();
            if (!File.Exists(path))
                return new AppSettings();

            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            loaded.Database ??= new DatabaseSettings();
            if (DatabaseSettingsMigration.TryMigrateInMemory(loaded.Database))
                Save(loaded);
            return loaded;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        var dir = GetStorageDirectory();
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, SettingsFileName);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
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
