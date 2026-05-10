using EliteRestaurant.Core.Models;

namespace EliteRestaurant.Core.Utils;

/// <summary>Kitchen tickets for remote / delivery checks may need explicit packaging callouts.</summary>
public static class KitchenTicketPackaging
{
    /// <summary>True for remote / packaging-first tickets (online channel, delivery, or missing table).</summary>
    public static bool IsOnlinePackagingOrder(OrderRecord order) =>
        OrderOrigin.IsOnline(order.OrderOrigin)
        || string.Equals(order.OrderSource, "Delivery", StringComparison.OrdinalIgnoreCase)
        || order.TableId is null;
}
