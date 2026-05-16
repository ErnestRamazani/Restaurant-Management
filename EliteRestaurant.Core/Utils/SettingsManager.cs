using System.IO;
using System.Linq;
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
            loaded.Salary ??= new SalarySettings();
            loaded.TicketReceipt ??= new TicketReceiptSettings();
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
        var toSave = settings;
        if (File.Exists(path))
        {
            try
            {
                var previousJson = File.ReadAllText(path);
                var previous = JsonSerializer.Deserialize<AppSettings>(previousJson, LoadAppSettingsOptions);
                if (previous is not null)
                    toSave = MergePreservingDesktopLocalSections(previous, settings);
            }
            catch
            {
                // If merge fails, still attempt to write incoming settings.
            }
        }

        var json = JsonSerializer.Serialize(toSave, SaveAppSettingsOptions);
        File.WriteAllText(path, json);
        SettingsChanged?.Invoke();
    }

    /// <summary>
    /// Ticket/receipt layout is stored only on the desktop settings file. Cloud profile sync and older
    /// API builds must not wipe it when they rewrite <c>app-settings.json</c>.
    /// </summary>
    private static AppSettings MergePreservingDesktopLocalSections(AppSettings previous, AppSettings incoming)
    {
        incoming.TicketReceipt ??= new TicketReceiptSettings();
        var previousTicket = previous.TicketReceipt ?? new TicketReceiptSettings();
        if (!HasTicketReceiptContent(incoming.TicketReceipt) && HasTicketReceiptContent(previousTicket))
            incoming.TicketReceipt = previousTicket;

        // Salary: some writers deserialize partial JSON or older payloads with null Salary. Never drop a
        // populated on-disk subtree when the incoming graph omitted it (would fall back to defaults on next Load).
        if (incoming.Salary is null && previous.Salary is not null)
            incoming.Salary = CloneSalary(previous.Salary);

        return incoming;
    }

    private static SalarySettings CloneSalary(SalarySettings s) => new()
    {
        LateDaysPerAttendanceUnit = s.LateDaysPerAttendanceUnit,
        AbsenceCountsAsAttendanceUnit = s.AbsenceCountsAsAttendanceUnit,
        SalesBonusPercent = s.SalesBonusPercent,
        MaxSalaryAdvancePercentOfGross = s.MaxSalaryAdvancePercentOfGross
    };

    private static bool HasTicketReceiptContent(TicketReceiptSettings ticket) =>
        !string.IsNullOrWhiteSpace(ticket.HeaderLogoPath) ||
        ticket.SocialMediaRows.Any(row =>
            !string.IsNullOrWhiteSpace(row.PlatformName) ||
            !string.IsNullOrWhiteSpace(row.UserText) ||
            !string.IsNullOrWhiteSpace(row.IconPath));

    private static string GetSettingsPath() => Path.Combine(GetStorageDirectory(), SettingsFileName);

    private static string GetStorageDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EliteRestaurantPro",
            "settings");
    }
}
