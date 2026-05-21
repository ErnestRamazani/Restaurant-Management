namespace EliteRestaurant.Contracts.Floor;

public sealed record FloorPlacementDto(
    int Id,
    int TableId,
    string TableDisplayName,
    int MinPartyCapacity,
    int MaxPartyCapacity,
    int LayoutX,
    int LayoutY,
    string Status,
    string? MergeClusterKey);

public sealed record FloorEngagementDto(
    int Id,
    string? ConfirmationCode,
    int PlacementUnitId,
    int TableId,
    string TableDisplayName,
    DateTime PlannedStartUtc,
    DateTime PlannedEndUtc,
    DateTime? ActualStartUtc,
    DateTime? ActualEndUtc,
    string GuestName,
    string GuestPhone,
    int PartySize,
    string Status,
    bool RotationOrOverstayFlag);

public sealed record FloorSnapshotDto(
    IReadOnlyList<FloorPlacementDto> Placements,
    IReadOnlyList<FloorEngagementDto> Engagements);

public sealed record PublicBookFloorRequest(
    int PlacementUnitId,
    DateTime PlannedStartUtc,
    DateTime? PlannedEndUtc,
    string GuestName,
    string GuestPhone,
    string? GuestEmail,
    int PartySize,
    string? UserNotes);

public sealed record PublicBookFloorResponse(
    int EngagementId,
    string ConfirmationCode,
    DateTime PlannedStartUtc,
    DateTime PlannedEndUtc,
    string TableDisplayName,
    string GuestName,
    string GuestPhone,
    int PartySize,
    string? UserNotes);

public sealed record PublicFloorConflictDto(
    bool HasConflict,
    IReadOnlyList<int>? ConflictingEngagementIds,
    string Message);

public sealed record SuggestedSlotDto(DateTime StartUtc, DateTime EndUtc);

public sealed record PublicAvailabilityRequest(
    int PlacementUnitId,
    int PartySize,
    DateTime RangeStartUtc,
    DateTime RangeEndUtc,
    int MaxSlots);

public sealed record PlacementSuggestionDto(
    int PlacementUnitId,
    int TableId,
    string TableDisplayName,
    int LayoutX,
    int LayoutY);

public sealed record MergePlacementsRequest(IReadOnlyList<int> PlacementUnitIds, string? ClusterKey);

public sealed record SuggestPlacementRequest(
    int PartySize,
    DateTime PlannedStartUtc,
    DateTime PlannedEndUtc);
