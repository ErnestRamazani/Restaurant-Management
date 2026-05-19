using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Orders;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Api.Hubs;

public static class OrderHubBroadcasts
{
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
}
