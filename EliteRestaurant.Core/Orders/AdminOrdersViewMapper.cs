using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurant.Core.Orders;

public static class AdminOrdersViewMapper
{
    public static OrderEntry MapOrder(
        OrderRecord order,
        bool isPast,
        bool showAdminAdvance,
        bool canViewTicket,
        string? restaurantTimeZoneId = null)
    {
        var tz = RestaurantTimeZone.NormalizeId(restaurantTimeZoneId);
        var total = OrderTotalsHelper.ComputeOrderGrandTotalUsd(order);
        var items = string.Join(", ",
            order.Items.Select(i => $"{i.Product?.Name ?? "Unknown"} x{i.Quantity}"));

        var displayStatus = OrderDisplayStatus.ForOrder(order);

        return new OrderEntry
        {
            Id = order.Id,
            OrderId = string.IsNullOrWhiteSpace(order.UniqueId) ? $"#{order.Id:000}" : order.UniqueId,
            ConfirmationCode = (order.ConfirmationCode ?? string.Empty).Trim(),
            TableNumber = OrderRecordUiLabels.TableCaption(order),
            ServerName = OrderRecordUiLabels.ServerCaption(order),
            Items = items,
            CustomerNotes = order.CustomerNotes ?? string.Empty,
            AllergyNotes = order.AllergyNotes ?? string.Empty,
            Status = displayStatus,
            CreatedAt = order.CreatedAt,
            Time = RestaurantTimeZone.FormatUtc(order.CreatedAt, tz, "HH:mm"),
            Total = total,
            StatusColor = GetStatusColor(displayStatus),
            OrderOrigin = string.IsNullOrWhiteSpace(order.OrderOrigin) ? OrderOrigin.InStore : order.OrderOrigin,
            RefundedAtUtc = order.RefundedAtUtc,
            ShowAdvanceInOrders = !isPast && showAdminAdvance && OrderWorkflow.CanAdminAdvanceOrderStatus(order.Status),
            ShowCompleteInOrders = !isPast && OrderWorkflow.CanCashierComplete(order.Status, order.OrderOrigin),
            ShowViewTicketInOrders = canViewTicket,
            ShowRefundInOrders = isPast
                && string.Equals(order.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                && !order.RefundedAtUtc.HasValue
                && !OrderDisplayStatus.HasOpenOnAccountDebt(order)
        };
    }

    public static string GetStatusColor(string status) => status switch
    {
        "Waiting" => "#2196F3",
        "In Kitchen" => "#FF9800",
        "Ready" => "#4CAF50",
        OrderWorkflow.Served => "#9C27B0",
        "Completed" => "#4CAF50",
        OrderDisplayStatus.Debt => "#F59E0B",
        OrderDisplayStatus.Refunded => "#78909C",
        "Cancelled" => "#F44336",
        var s when string.Equals(s, OrderWorkflow.PendingCashier, StringComparison.OrdinalIgnoreCase) => "#CE93D8",
        var s when string.Equals(s, OrderWorkflow.PendingApproval, StringComparison.OrdinalIgnoreCase) => "#B39DDB",
        _ => "#D4AF37"
    };
}
