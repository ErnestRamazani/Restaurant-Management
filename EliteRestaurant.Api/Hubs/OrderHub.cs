using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EliteRestaurant.Api.Hubs;

[Authorize(Policy = "StaffAny")]
public sealed class OrderHub(IServiceScopeFactory scopeFactory) : Hub
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    public async Task JoinServer()
    {
        var role = Context.User?.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (role.Equals("Server", StringComparison.OrdinalIgnoreCase)
            || role.Equals("Cashier", StringComparison.OrdinalIgnoreCase)
            || role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
            || role.Equals("Manager", StringComparison.OrdinalIgnoreCase))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Server");
        }
    }

    /// <summary>Cashier dashboard / payment desk — not for unauthenticated guests.</summary>
    public async Task JoinCashierDashboard()
    {
        var role = Context.User?.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (role.Equals("Cashier", StringComparison.OrdinalIgnoreCase)
            || role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
            || role.Equals("Manager", StringComparison.OrdinalIgnoreCase))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Cashier");
        }
    }

    public async Task JoinKitchen()
    {
        var role = Context.User?.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (role.Equals("Chef", StringComparison.OrdinalIgnoreCase)
            || role.Equals("Barman", StringComparison.OrdinalIgnoreCase)
            || role.Equals("Bartender", StringComparison.OrdinalIgnoreCase)
            || role.Equals("Sous Chef", StringComparison.OrdinalIgnoreCase))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Kitchen");
        }
    }

    /// <summary>Cashier or admin releases a pending ticket to the kitchen (same rules as REST release).</summary>
    public async Task StartPreparation(int orderId)
    {
        var role = Context.User?.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (!role.Equals("Cashier", StringComparison.OrdinalIgnoreCase)
            && !role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
            && !role.Equals("Manager", StringComparison.OrdinalIgnoreCase))
            throw new HubException("Only cashier or admin can release orders to the kitchen.");

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ops = new AdminOrderOperationsService(db);
        var hub = scope.ServiceProvider.GetRequiredService<IHubContext<OrderHub>>();
        var r = ops.TryReleasePendingToKitchen(orderId);
        if (!r.Ok)
            throw new HubException(r.ErrorMessage ?? "Release failed.");

        await hub.Clients.Group("Kitchen").SendAsync("KitchenQueueChanged", new { reason = "hub-start-preparation", orderId });
    }

    /// <summary>Kitchen marks an order ready; notifies cashier dashboard listeners.</summary>
    public async Task MarkOrderReadyForCashier(int orderId)
    {
        var role = Context.User?.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (!role.Equals("Chef", StringComparison.OrdinalIgnoreCase)
            && !role.Equals("Barman", StringComparison.OrdinalIgnoreCase)
            && !role.Equals("Bartender", StringComparison.OrdinalIgnoreCase)
            && !role.Equals("Sous Chef", StringComparison.OrdinalIgnoreCase))
            throw new HubException("Only kitchen staff can mark orders ready.");

        await using var scope = _scopeFactory.CreateAsyncScope();
        var ops = new AdminOrderOperationsService(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        var hub = scope.ServiceProvider.GetRequiredService<IHubContext<OrderHub>>();
        var result = ops.TryMarkKitchenReadyForCashier(orderId);
        if (!result.Ok)
            throw new HubException(result.ErrorMessage ?? "Cannot mark ready.");

        if (result.SuppressBroadcast || result.Notification is null)
            return;

        await hub.Clients.Group("Cashier").SendAsync("OrderReady", result.Notification);
        await hub.Clients.Group("Cashier").SendAsync(
            "CashierOrderBoardChanged",
            new { reason = "order-ready", orderId = result.Notification.OrderId, orderCode = result.Notification.OrderCode });
    }
}
