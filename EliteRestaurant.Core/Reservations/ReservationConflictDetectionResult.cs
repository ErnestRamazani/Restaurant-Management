namespace EliteRestaurant.Core.Reservations;

public sealed class ReservationConflictDetectionResult
{
    public bool HasConflict { get; init; }
    public IReadOnlyList<int> ConflictingEngagementIds { get; init; } = Array.Empty<int>();
}
