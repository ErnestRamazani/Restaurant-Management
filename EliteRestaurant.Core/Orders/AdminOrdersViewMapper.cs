using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurant.Core.Orders;

public static class AdminOrdersViewMapper
{
    public static OrderEntry MapOrder(OrderRecord order, bool isPast, bool showAdminAdvance, bool canViewTicket)
    {
        var lineSubtotal = order.Items.Sum(i => (i.Product?.Price ?? 0m) * i.Quantity);
        var totals = OrderTotalsHelper.ComputeTotals(lineSubtotal, order.DiscountMode, order.DiscountValue);
        var total = totals.GrandTotal;
        var items = string.Join(", ",
            order.Items.Select(i => $"{i.Product?.Name ?? "Unknown"} x{i.Quantity}"));

        return new OrderEntry
        {
            Id = order.Id,
            OrderId = string.IsNullOrWhiteSpace(order.UniqueId) ? $"#{order.Id:000}" : order.UniqueId,
            TableNumber = string.IsNullOrWhiteSpace(order.TableCode)
                ? $"Table {order.Table?.TableNumber ?? 0}"
                : $"{order.TableCode} · {order.TableName}",
            ServerName = string.IsNullOrWhiteSpace(order.ServerName)
                ? (order.Server?.Name ?? "Unassigned")
                : order.ServerName,
            Items = items,
            CustomerNotes = order.CustomerNotes ?? string.Empty,
            AllergyNotes = order.AllergyNotes ?? string.Empty,
            Status = order.Status,
            CreatedAt = order.CreatedAt,
            Time = order.CreatedAt.ToString("HH:mm"),
            Total = total,
            StatusColor = GetStatusColor(order.Status),
            ShowAdvanceInOrders = !isPast && showAdminAdvance && OrderWorkflow.CanAdminAdvanceOrderStatus(order.Status),
            ShowCompleteInOrders = !isPast && OrderWorkflow.CanCashierComplete(order.Status),
            ShowViewTicketInOrders = canViewTicket
        };
    }

    public static string GetStatusColor(string status) => status switch
    {
        "Waiting" => "#2196F3",
        "In Kitchen" => "#FF9800",
        "Ready" => "#4CAF50",
        OrderWorkflow.Served => "#9C27B0",
        "Completed" => "#4CAF50",
        "Cancelled" => "#F44336",
        var s when string.Equals(s, OrderWorkflow.PendingCashier, StringComparison.OrdinalIgnoreCase) => "#CE93D8",
        var s when string.Equals(s, OrderWorkflow.PendingApproval, StringComparison.OrdinalIgnoreCase) => "#B39DDB",
        _ => "#D4AF37"
    };
}
