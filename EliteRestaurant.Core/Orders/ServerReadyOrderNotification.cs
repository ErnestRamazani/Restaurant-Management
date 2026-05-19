namespace EliteRestaurant.Core.Orders;

/// <summary>SignalR payload when a Ready order is assigned to a server (Pick Up &amp; Serve).</summary>
public sealed record ServerReadyOrderNotification(
    int OrderId,
    int ServerId,
    string OrderCode,
    string? TableLabel,
    string? GuestCustomerName,
    string ItemsSummary,
    int ItemCount,
    string OrderOrigin,
    string OrderSource);
