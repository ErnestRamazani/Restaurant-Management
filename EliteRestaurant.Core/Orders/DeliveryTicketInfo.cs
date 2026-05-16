namespace EliteRestaurant.Core.Orders;

/// <summary>Customer-facing delivery details for printed/PDF tickets and driver handoff.</summary>
public sealed record DeliveryTicketInfo(
    string CustomerName,
    string Phone,
    string Address,
    string Instructions);
