namespace EliteRestaurant.Api.Options;

/// <summary>
/// When the request Host is one of <see cref="PlatformApiHosts"/> (shared App Platform URL),
/// resolve the first active restaurant so desktop sign-in works before a custom domain is configured.
/// </summary>
public sealed class TenancyOptions
{
    public const string SectionName = "Tenancy";

    public string[] PlatformApiHosts { get; set; } =
    [
        "starfish-app-owtoz.ondigitalocean.app"
    ];
}
