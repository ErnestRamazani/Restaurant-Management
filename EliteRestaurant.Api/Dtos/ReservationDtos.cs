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
