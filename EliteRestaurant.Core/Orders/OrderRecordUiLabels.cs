using EliteRestaurant.Core.Models;

namespace EliteRestaurant.Core.Orders;

/// <summary>Human-facing table / server captions for POS, cashier, and admin lists.</summary>
public static class OrderRecordUiLabels
{
    public static string TableCaption(OrderRecord order)
    {
        if (OrderOrigin.IsOnline(order.OrderOrigin))
        {
            var delivery = string.Equals(order.OrderSource, "Delivery", StringComparison.OrdinalIgnoreCase);
            return delivery ? "Online · Delivery" : "Online · Pickup";
        }

        return string.IsNullOrWhiteSpace(order.TableCode)
            ? $"Table {order.Table?.TableNumber ?? 0}"
            : $"{order.TableCode} · {order.TableName}";
    }

    public static string ServerCaption(OrderRecord order) =>
        OrderOrigin.IsOnline(order.OrderOrigin)
            ? "—"
            : string.IsNullOrWhiteSpace(order.ServerName)
                ? (order.Server?.Name ?? "Unassigned")
                : order.ServerName;
}
