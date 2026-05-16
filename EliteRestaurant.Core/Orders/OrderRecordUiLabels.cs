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

    public static bool IsDeliveryOrder(OrderRecord order) =>
        string.Equals(order.OrderSource, "Delivery", StringComparison.OrdinalIgnoreCase);

    /// <summary>Kitchen display origin headline (matches web KDS badges).</summary>
    public static string KitchenFulfillmentHeadline(OrderRecord order) =>
        OrderOrigin.IsOnline(order.OrderOrigin)
            ? (IsDeliveryOrder(order) ? "DELIVERY" : "TO GO")
            : "PLATED";

    public static DeliveryTicketInfo? TryGetOnlineGuestTicketInfo(OrderRecord order) =>
        DeliveryTicketInfoParser.TryParse(order);

    public static DeliveryTicketInfo? TryGetDeliveryTicketInfo(OrderRecord order) =>
        TryGetOnlineGuestTicketInfo(order);

    /// <summary>Receipt header line for table vs online fulfillment (no "Table:" prefix for delivery/pickup).</summary>
    public static string TicketLocationLine(OrderRecord order)
    {
        if (OrderOrigin.IsOnline(order.OrderOrigin))
            return TableCaption(order);
        return $"Table: {TableCaption(order)}";
    }

    public static bool ShowServerOnTicket(OrderRecord order) => !OrderOrigin.IsOnline(order.OrderOrigin);
}
