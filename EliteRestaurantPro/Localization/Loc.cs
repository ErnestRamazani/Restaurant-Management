using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurantPro.Localization;

public static class Loc
{
    private static readonly ConcurrentDictionary<string, string> Strings = new(StringComparer.OrdinalIgnoreCase);
    private static string _language = "fr";

    public static event Action? LanguageChanged;

    public static string Language => _language;

    public static void Initialize(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.UiLanguage))
        {
            settings.UiLanguage = "fr";
            SettingsManager.Save(settings);
        }

        _language = NormalizeLanguage(settings.UiLanguage);
        LoadFromBundledFile(_language);
        _ = RefreshFromApiAsync(settings);
    }

    public static async Task SetLanguageAsync(string language, AppSettings settings)
    {
        var normalized = NormalizeLanguage(language);
        if (string.Equals(normalized, _language, StringComparison.OrdinalIgnoreCase) && Strings.Count > 0)
            return;

        settings.UiLanguage = normalized;
        SettingsManager.Save(settings);
        _language = normalized;
        LoadFromBundledFile(normalized);
        await RefreshFromApiAsync(settings);
        LanguageChanged?.Invoke();
    }

    public static string T(string key, string fallback, IReadOnlyDictionary<string, string>? vars = null)
    {
        var text = Strings.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : fallback;
        return ApplyVars(text, vars);
    }

    public static string Admin(string key, string fallback, IReadOnlyDictionary<string, string>? vars = null)
        => T("portals.admin." + key, fallback, vars);

    public static string Common(string key, string fallback, IReadOnlyDictionary<string, string>? vars = null)
        => T("common." + key, fallback, vars);

    public static string Auth(string key, string fallback, IReadOnlyDictionary<string, string>? vars = null)
        => T("auth." + key, fallback, vars);

    public static string Vars(string template, IReadOnlyDictionary<string, string> vars)
        => ApplyVars(template, vars);

    public static string NormalizeLanguage(string? language)
    {
        var code = (language ?? string.Empty).Trim().ToLowerInvariant();
        return code.StartsWith("fr", StringComparison.Ordinal) ? "fr" : "en";
    }

    private static string ApplyVars(string text, IReadOnlyDictionary<string, string>? vars)
    {
        if (vars is null || vars.Count == 0)
            return text;

        var result = text;
        foreach (var (key, value) in vars)
            result = result.Replace("{{" + key + "}}", value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        return result;
    }

    private static void LoadFromBundledFile(string language)
    {
        Strings.Clear();
        var path = ResolveLocaleFilePath(language);
        if (!File.Exists(path) && !string.Equals(language, "fr", StringComparison.OrdinalIgnoreCase))
            path = ResolveLocaleFilePath("fr");
        if (!File.Exists(path))
            path = ResolveLocaleFilePath("en");
        if (!File.Exists(path))
            return;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var kv in FlattenJson(doc.RootElement))
                Strings[kv.Key] = kv.Value;
        }
        catch
        {
            // Bundled fallback only — API refresh may still succeed.
        }
    }

    private static string ResolveLocaleFilePath(string language)
    {
        var normalized = NormalizeLanguage(language);
        var exeDir = AppContext.BaseDirectory;
        return Path.Combine(exeDir, "Locales", $"{normalized}.json");
    }

    private static async Task RefreshFromApiAsync(AppSettings settings)
    {
        var baseUrl = CloudEndpoints.NormalizeApiBaseUrl(settings.CloudApi.BaseUrl);
        if (string.IsNullOrWhiteSpace(baseUrl))
            return;

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            var uri = new Uri($"{baseUrl.TrimEnd('/')}/api/language/strings?lang={Uri.EscapeDataString(_language)}");
            using var response = await http.GetAsync(uri).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return;

            var payload = await response.Content.ReadFromJsonAsync<LanguageStringsResponse>().ConfigureAwait(false);
            if (payload?.Strings is null)
                return;

            foreach (var kv in payload.Strings)
            {
                if (kv.Value is string s)
                    Strings[kv.Key] = s;
            }
        }
        catch
        {
            // Offline or unreachable — bundled strings remain in use.
        }
    }

    private static Dictionary<string, string> FlattenJson(JsonElement element, string prefix = "")
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
                    foreach (var kv in FlattenJson(prop.Value, key))
                        result[kv.Key] = kv.Value;
                }
                break;
            case JsonValueKind.String:
                result[prefix] = element.GetString() ?? string.Empty;
                break;
        }

        return result;
    }

    private sealed record LanguageStringsResponse(string Language, Dictionary<string, object?> Strings);
}
