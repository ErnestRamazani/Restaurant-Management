namespace EliteRestaurant.Core.Orders;

/// <summary>Result of <see cref="AdminOrderOperationsService.TryAdvanceOrderWithOutcome"/>.</summary>
public sealed record AdvanceOrderOutcome(
    bool Missing,
    string? Error,
    bool BecameReady,
    OrderReadyNotification? ReadyNotification);

/// <summary>SignalR payload for cashier when an order moves to <c>Ready</c>.</summary>
public sealed record OrderReadyNotification(
    int OrderId,
    string OrderCode,
    string OrderOrigin,
    string OrderSource,
    string? TableLabel,
    string? GuestLabel,
    string CustomerFulfillmentStatus,
    string CustomerFulfillmentDisplay);
