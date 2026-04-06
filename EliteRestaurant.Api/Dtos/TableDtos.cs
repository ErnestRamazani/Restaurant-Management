namespace EliteRestaurant.Api.Dtos;

public sealed record TableSummaryDto(
    int Id,
    string UniqueId,
    int TableNumber,
    string Name,
    int Capacity,
    string Status,
    int? AssignedServerId,
    string? AssignedServerName);
