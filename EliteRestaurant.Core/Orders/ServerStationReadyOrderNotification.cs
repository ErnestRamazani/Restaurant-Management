namespace EliteRestaurant.Core.Orders;

/// <summary>SignalR when one station (kitchen or bar) finished prep on a mixed ticket still in the kitchen.</summary>
public sealed record ServerStationReadyOrderNotification(
    int OrderId,
    int ServerId,
    string OrderCode,
    string? TableLabel,
    string? GuestCustomerName,
    string PrepStation,
    string PrepSummary,
    string OrderOrigin,
    string OrderSource);
