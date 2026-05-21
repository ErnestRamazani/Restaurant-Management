namespace EliteRestaurant.Api.Services;

public sealed class LocalizationOptions
{
    public string DefaultLanguage { get; set; } = "en";
    public string[] SupportedLanguages { get; set; } = ["en", "fr"];
    public bool EnableCaching { get; set; } = true;
    public int CacheDurationMinutes { get; set; } = 60;
}
