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
    string? ConfirmationCode,
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
    string? ConfirmationCode,
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

/// <summary>Reservation scheduling window for cashier / reception portals (desktop settings + appsettings).</summary>
public sealed record CashierReservationSchedulingDto(
    int ReservationLeadDays,
    int ReservationMaxMonthsAhead,
    int BufferMinutes,
    int DefaultDurationMinutes,
    int SuggestionSlotStepMinutes,
    int SuggestionHorizonDays);

/// <summary>Walk-in reservation created at the front desk (table optional).</summary>
public sealed record CashierCreateWalkInEngagementRequest(
    string GuestName,
    string GuestPhone,
    string? GuestEmail,
    DateTime PlannedStartUtc,
    DateTime? PlannedEndUtc,
    int PartySize,
    int? TableId,
    int? PlacementUnitId,
    string? UserNotes);

public sealed record CashierCreateWalkInEngagementResponse(
    int EngagementId,
    string ConfirmationCode,
    DateTime PlannedStartUtc,
    DateTime PlannedEndUtc);

public sealed record ReceptionDeliveryPickupOrderRow(
    int OrderId,
    string OrderCode,
    string GuestName,
    string GuestPhone,
    string FulfillmentType,
    string Status,
    DateTime CreatedAt,
    string CreatedAtDisplay,
    string ItemsSummary,
    bool IsReadyForHandoff);
