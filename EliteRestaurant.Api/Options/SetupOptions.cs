namespace EliteRestaurant.Api.Options;

/// <summary>Protects <c>POST /api/setup/new-site</c> and <c>POST /api/setup/wipe-all-data</c>.</summary>
public sealed class SetupOptions
{
    /// <summary>Shared secret sent as <c>X-Setup-Secret</c>. Set via <c>Setup__PlatformSecret</c> in production.</summary>
    public string? PlatformSecret { get; set; }
}
