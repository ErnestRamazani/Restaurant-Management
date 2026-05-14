namespace EliteRestaurant.Api.Dtos;

public sealed record ArrivedReservationDto(
    int Id,
    string UniqueId,
    string ReservationName,
    string GuestName,
    DateTime ReservedFor,
    int? TableId,
    string TableLabel,
    int PartySize);

/// <summary>Floor reservation engagements for the cashier tablet (public /book pipeline).</summary>
public sealed record CashierEngagementListRow(
    int Id,
    string Status,
    string GuestName,
    string GuestPhone,
    int PartySize,
    DateTime PlannedStartUtc,
    DateTime PlannedEndUtc,
    string TableLabel,
    int PlacementUnitId);

public sealed record CashierEngagementDetailDto(
    int Id,
    string Status,
    string GuestName,
    string GuestPhone,
    string GuestEmail,
    int PartySize,
    string UserNotes,
    DateTime PlannedStartUtc,
    DateTime PlannedEndUtc,
    DateTime? ActualStartUtc,
    DateTime? ActualEndUtc,
    int TableId,
    string TableLabel,
    int PlacementUnitId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CashierRescheduleEngagementRequest(
    DateTime PlannedStartUtc,
    DateTime? PlannedEndUtc);
