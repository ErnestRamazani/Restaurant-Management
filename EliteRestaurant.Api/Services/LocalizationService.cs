using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace EliteRestaurant.Api.Services;

public sealed class LocalizationService
{
    private readonly IWebHostEnvironment _environment;
    private readonly LocalizationOptions _options;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    public LocalizationService(IWebHostEnvironment environment, IOptions<LocalizationOptions> options)
    {
        _environment = environment;
        _options = options.Value;
    }

    public static string NormalizeLanguage(string? language)
    {
        var code = (language ?? string.Empty).Trim().ToLowerInvariant();
        if (code.StartsWith("fr", StringComparison.Ordinal))
            return "fr";
        return "en";
    }

    public IReadOnlyList<string> SupportedLanguages =>
        _options.SupportedLanguages is { Length: > 0 } supported
            ? supported.Select(NormalizeLanguage).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            : ["en", "fr"];

    public string DefaultLanguage => NormalizeLanguage(_options.DefaultLanguage);

    public string? GetString(string key, string? language = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var doc = LoadDocument(NormalizeLanguage(language));
        if (doc is null)
            return null;

        if (!TryResolvePath(doc.RootElement, key.Split('.', StringSplitOptions.RemoveEmptyEntries), out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    public string GetStringOrDefault(string key, string? language, string fallback)
        => GetString(key, language) ?? GetString(key, DefaultLanguage) ?? fallback;

    public IReadOnlyDictionary<string, object?> GetAllStrings(string? language = null)
    {
        var doc = LoadDocument(NormalizeLanguage(language));
        if (doc is null)
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        return FlattenJson(doc.RootElement);
    }

    private JsonDocument? LoadDocument(string language)
    {
        var normalized = NormalizeLanguage(language);
        if (_options.EnableCaching && _cache.TryGetValue(normalized, out var entry))
        {
            if (DateTime.UtcNow < entry.ExpiresAtUtc)
                return entry.Document;
        }

        var path = Path.Combine(_environment.ContentRootPath, "wwwroot", "locales", $"{normalized}.json");
        if (!File.Exists(path) && normalized != DefaultLanguage)
            path = Path.Combine(_environment.ContentRootPath, "wwwroot", "locales", $"{DefaultLanguage}.json");

        if (!File.Exists(path))
            return null;

        var json = File.ReadAllText(path);
        var document = JsonDocument.Parse(json);
        if (_options.EnableCaching)
        {
            var minutes = Math.Max(1, _options.CacheDurationMinutes);
            _cache[normalized] = new CacheEntry(document, DateTime.UtcNow.AddMinutes(minutes));
        }

        return document;
    }

    private static bool TryResolvePath(JsonElement root, string[] segments, out JsonElement value)
    {
        value = root;
        foreach (var segment in segments)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
                return false;
        }

        return true;
    }

    private static Dictionary<string, object?> FlattenJson(JsonElement element, string prefix = "")
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
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
            case JsonValueKind.Array:
                result[prefix] = element.GetRawText();
                break;
            case JsonValueKind.String:
                result[prefix] = element.GetString();
                break;
            case JsonValueKind.Number:
                if (element.TryGetInt64(out var l))
                    result[prefix] = l;
                else if (element.TryGetDecimal(out var d))
                    result[prefix] = d;
                else
                    result[prefix] = element.GetDouble();
                break;
            case JsonValueKind.True:
                result[prefix] = true;
                break;
            case JsonValueKind.False:
                result[prefix] = false;
                break;
            case JsonValueKind.Null:
                result[prefix] = null;
                break;
        }

        return result;
    }

    private sealed class CacheEntry(JsonDocument document, DateTime expiresAtUtc)
    {
        public JsonDocument Document { get; } = document;
        public DateTime ExpiresAtUtc { get; } = expiresAtUtc;
    }
}
