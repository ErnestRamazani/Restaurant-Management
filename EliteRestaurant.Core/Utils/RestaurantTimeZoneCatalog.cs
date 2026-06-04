namespace EliteRestaurant.Core.Utils;

/// <summary>Common IANA ids for Appearance settings picker.</summary>
public static class RestaurantTimeZoneCatalog
{
    public static IReadOnlyList<string> CommonIds { get; } =
    [
        "Africa/Kinshasa",
        "Africa/Lagos",
        "Africa/Johannesburg",
        "Africa/Cairo",
        "Europe/Paris",
        "Europe/London",
        "America/New_York",
        "America/Chicago",
        "America/Denver",
        "America/Los_Angeles",
        "UTC",
    ];
}
