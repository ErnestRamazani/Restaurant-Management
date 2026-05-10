namespace EliteRestaurant.Core.Models;

public sealed class PlacementUnit
{
    public int Id { get; set; }
    public int TableId { get; set; }
    public Table? Table { get; set; }

    public int MinPartyCapacity { get; set; } = 1;
    public int MaxPartyCapacity { get; set; } = 8;
    public int LayoutX { get; set; }
    public int LayoutY { get; set; }

    /// <summary>Available, Reserved, Occupied, ToClean</summary>
    public string Status { get; set; } = Reservations.PlacementUnitStatuses.Available;

    /// <summary>Non-empty: merged with every other unit sharing the same key for conflict detection.</summary>
    public string? MergeClusterKey { get; set; }
}
