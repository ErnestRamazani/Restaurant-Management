using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Utils;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Api.Hubs;

public static class OrderHubBroadcasts
{
    public static bool IsReceptionTrackedOnlineFulfillment(OrderRecord order) =>
        OrderOrigin.IsOnline(order.OrderOrigin)
        && (string.Equals(order.OrderSource, "Delivery", StringComparison.OrdinalIgnoreCase)
            || string.Equals(order.OrderSource, "Pickup", StringComparison.OrdinalIgnoreCase)
            || string.Equals(order.OrderSource, "TakeOut", StringComparison.OrdinalIgnoreCase));

    public static async Task NotifyCashierOrderBoardChangedAsync(
        IHubContext<OrderHub> hub,
        AppDbContext db,
        int orderId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var order = await db.Orders.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        var orderCode = order is null
            ? null
            : string.IsNullOrWhiteSpace(order.UniqueId) ? $"#{order.Id:000}" : order.UniqueId;

        var payload = new { reason, orderId, orderCode };
        await hub.Clients.Group("Cashier")
            .SendAsync("CashierOrderBoardChanged", payload, cancellationToken);
        await hub.Clients.Group("Server")
            .SendAsync("CashierOrderBoardChanged", payload, cancellationToken);

        if (order is not null && IsReceptionTrackedOnlineFulfillment(order))
        {
            await NotifyReceptionDeliveryPickupChangedAsync(hub, db, orderId, reason, cancellationToken);
        }
    }

    public static async Task NotifyReceptionDeliveryPickupChangedAsync(
        IHubContext<OrderHub> hub,
        AppDbContext db,
        int orderId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var order = await db.Orders.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null || !IsReceptionTrackedOnlineFulfillment(order))
            return;

        var payload = new
        {
            reason,
            orderId,
            orderCode = string.IsNullOrWhiteSpace(order.UniqueId) ? $"#{order.Id:000}" : order.UniqueId,
            status = order.Status,
            isReady = string.Equals(order.Status, "Ready", StringComparison.OrdinalIgnoreCase)
        };

        await hub.Clients.Group("Reception")
            .SendAsync("ReceptionDeliveryPickupChanged", payload, cancellationToken);
    }

    public static async Task NotifyKitchenMarkedReadyAsync(
        IHubContext<OrderHub> hub,
        AppDbContext db,
        int orderId,
        OrderReadyNotification? cashierNotification,
        CancellationToken cancellationToken = default)
    {
        if (cashierNotification is not null)
        {
            await hub.Clients.Group("Cashier").SendAsync("OrderReady", cashierNotification, cancellationToken);
            await hub.Clients.Group("Cashier").SendAsync(
                "CashierOrderBoardChanged",
                new { reason = "order-ready", orderId = cashierNotification.OrderId, orderCode = cashierNotification.OrderCode },
                cancellationToken);
            await NotifyReceptionDeliveryPickupChangedAsync(
                hub, db, cashierNotification.OrderId, "order-ready", cancellationToken);
        }

        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
            return;

        var serverNotification = OrderHubNotificationFactory.TryBuildServerReady(order);
        if (serverNotification is not null)
        {
            await hub.Clients.Group("Server")
                .SendAsync("ServerReadyOrderArrived", serverNotification, cancellationToken);
        }
    }

    /// <summary>Guest at a table tapped "Call your Server" on the QR menu.</summary>
    public static async Task NotifyServerTableCallAsync(
        IHubContext<OrderHub> hub,
        Guid callId,
        int tableId,
        int tableNumber,
        string tableName,
        string reasonCode,
        string reasonLabel,
        int? assignedServerId,
        string? assignedServerName,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            callId,
            tableId,
            tableCode = tableNumber,
            tableName = tableName.Trim(),
            tableLabel = $"Table {tableNumber} · {tableName.Trim()}",
            reasonCode,
            reasonLabel,
            serverId = assignedServerId ?? 0,
            serverName = string.IsNullOrWhiteSpace(assignedServerName) ? null : assignedServerName.Trim(),
            calledAtUtc = DateTime.UtcNow
        };

        await hub.Clients.Group("Server")
            .SendAsync("ServerTableCall", payload, cancellationToken);
    }

    public static Task NotifyServerTableCallQueueChangedAsync(
        IHubContext<OrderHub> hub,
        string action,
        Guid callId,
        CancellationToken cancellationToken = default) =>
        hub.Clients.Group("Server")
            .SendAsync("ServerTableCallQueueChanged", new { action, callId }, cancellationToken);
}
