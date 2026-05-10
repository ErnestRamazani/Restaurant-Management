namespace EliteRestaurant.Core.Models;

/// <summary>Persisted on <see cref="OrderRecord.OrderOrigin"/> as <see cref="Online"/> or <see cref="InStore"/>.</summary>
public static class OrderOrigin
{
    public const string Online = "Online";
    public const string InStore = "InStore";

    public static bool IsOnline(string? value) =>
        string.Equals(value, Online, StringComparison.OrdinalIgnoreCase);

    public static bool IsInStore(string? value) =>
        string.Equals(value, InStore, StringComparison.OrdinalIgnoreCase);
}
