namespace EliteRestaurant.Core.Reservations;

public static class PlacementUnitStatuses
{
    public const string Available = "Available";
    public const string Reserved = "Reserved";
    public const string Occupied = "Occupied";
    public const string ToClean = "ToClean";

    public static bool IsAssignable(string status) =>
        string.Equals(status, Available, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, ToClean, StringComparison.OrdinalIgnoreCase);
}
