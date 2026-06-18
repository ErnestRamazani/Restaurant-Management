using EliteRestaurant.Api.Dtos;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Menu;
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

    /// <summary>
    /// Unified pipeline alert: each target hub group receives <see cref="OrderStageChangedDto"/>
    /// (staff portals show toast + play ring).
    /// </summary>
    public static async Task BroadcastOrderStageAsync(
        IHubContext<OrderHub> hub,
        AppDbContext db,
        int orderId,
        string stage,
        string? previousStatus = null,
        string? messageOverride = null,
        CancellationToken cancellationToken = default)
    {
        var order = await db.Orders.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
            return;

        var audiences = ResolveAudiences(stage, order);
        if (audiences.Count == 0)
            return;

        var orderCode = string.IsNullOrWhiteSpace(order.UniqueId) ? $"#{order.Id:000}" : order.UniqueId.Trim();
        var message = messageOverride ?? BuildStageMessage(stage, orderCode, order);
        var payload = new OrderStageChangedDto
        {
            OrderId = order.Id,
            OrderCode = orderCode,
            PreviousStatus = previousStatus,
            NewStatus = order.Status ?? string.Empty,
            Stage = stage,
            Message = message,
            Audiences = audiences
        };

        foreach (var group in audiences)
        {
            await hub.Clients.Group(group)
                .SendAsync("OrderStageChanged", payload, cancellationToken);
        }
    }

    public static async Task BroadcastOrderStageFromStatusAsync(
        IHubContext<OrderHub> hub,
        AppDbContext db,
        int orderId,
        string? previousStatus,
        CancellationToken cancellationToken = default)
    {
        var order = await db.Orders.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
            return;

        var stage = ResolveStageFromStatus(order.Status, order);
        await BroadcastOrderStageAsync(hub, db, orderId, stage, previousStatus, cancellationToken: cancellationToken);
    }

    public static string ResolveStageFromStatus(string? status, OrderRecord? order = null)
    {
        if (OrderWorkflow.IsPendingCashier(status))
            return "pending-cashier";
        if (OrderWorkflow.IsPendingApproval(status))
            return "pending-approval";
        if (string.Equals(status, "Waiting", StringComparison.OrdinalIgnoreCase))
            return "released-to-kitchen";
        if (string.Equals(status, "In Kitchen", StringComparison.OrdinalIgnoreCase))
            return "status-in-kitchen";
        if (OrderWorkflow.IsReady(status))
            return "status-ready";
        if (OrderWorkflow.IsServed(status))
            return "status-served";
        if (string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase))
            return "status-completed";
        if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            return "order-cancelled";

        if (order is not null && IsReceptionTrackedOnlineFulfillment(order))
            return "online-order-update";

        return "order-updated";
    }

    public static string ResolveStageFromBoardReason(string reason, OrderRecord order)
    {
        var r = (reason ?? string.Empty).Trim();
        return r switch
        {
            "online-order-submitted" => OrderWorkflow.IsPendingApproval(order.Status)
                ? "pending-approval"
                : "pending-cashier",
            "server-order-submitted" or "admin-order-submitted" =>
                OrderWorkflow.IsPendingApproval(order.Status)
                    ? "pending-approval"
                    : "released-to-kitchen",
            "server-order-appended" or "admin-order-appended" => "released-to-kitchen",
            "released-to-kitchen" => "released-to-kitchen",
            "pending-cancelled" or "order-cancelled" => "order-cancelled",
            "order-completed" => "status-completed",
            "order-ready" => "status-ready",
            _ => ResolveStageFromStatus(order.Status, order)
        };
    }

    private static IReadOnlyList<string> ResolveAudiences(string stage, OrderRecord order)
    {
        var list = stage switch
        {
            "customer-draft" => new[] { "Server" },
            "pending-cashier" => new[] { "Cashier" },
            "pending-approval" => new[] { "Cashier", "Reception" },
            "released-to-kitchen" or "status-waiting" => new[] { "Kitchen" },
            "status-in-kitchen" => new[] { "Kitchen" },
            "status-ready" => new[] { "Server", "Cashier" },
            "status-served" => new[] { "Cashier" },
            "status-completed" => new[] { "Cashier", "Server" },
            "order-cancelled" => new[] { "Server", "Cashier", "Kitchen" },
            _ => Array.Empty<string>()
        };

        var audiences = list.ToList();
        if (IsReceptionTrackedOnlineFulfillment(order)
            && stage is "pending-approval" or "released-to-kitchen" or "status-ready" or "status-completed" or "online-order-update")
        {
            if (!audiences.Contains("Reception", StringComparer.OrdinalIgnoreCase))
                audiences.Add("Reception");
        }

        return audiences;
    }

    private static string BuildStageMessage(string stage, string orderCode, OrderRecord order)
    {
        var table = OrderRecordUiLabels.TableCaption(order);
        var loc = string.IsNullOrWhiteSpace(table) ? orderCode : $"{orderCode} · {table}";
        return stage switch
        {
            "customer-draft" => $"Customer menu order · {loc}",
            "pending-cashier" => $"New ticket awaiting cashier · {loc}",
            "pending-approval" => $"Online order awaiting approval · {loc}",
            "released-to-kitchen" => $"Sent to kitchen · {loc}",
            "status-waiting" => $"Waiting in kitchen queue · {loc}",
            "status-in-kitchen" => $"Now preparing · {loc}",
            "status-ready" => $"Ready for pickup · {loc}",
            "status-served" => $"Served — ready to pay · {loc}",
            "status-completed" => $"Payment completed · {loc}",
            "order-cancelled" => $"Order cancelled · {loc}",
            _ => $"Order updated · {loc}"
        };
    }

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

        if (order is not null
            && !string.Equals(reason, "order-ready", StringComparison.OrdinalIgnoreCase))
        {
            var stage = ResolveStageFromBoardReason(reason, order);
            await BroadcastOrderStageAsync(hub, db, orderId, stage, previousStatus: null, cancellationToken: cancellationToken);
        }

        if (order is not null && IsReceptionTrackedOnlineFulfillment(order))
            await NotifyReceptionDeliveryPickupChangedAsync(hub, db, orderId, reason, cancellationToken);
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

    public static async Task NotifyKitchenQueueChangedAsync(
        IHubContext<OrderHub> hub,
        AppDbContext db,
        int orderId,
        string reason,
        string? previousStatus = null,
        CancellationToken cancellationToken = default)
    {
        await hub.Clients.Group("Kitchen")
            .SendAsync("KitchenQueueChanged", new { reason, orderId }, cancellationToken);

        var stage = reason switch
        {
            "cashier-release" or "release-pending" or "release-to-kitchen" or "hub-start-preparation"
                or "server-order-submitted" or "admin-order-submitted"
                or "server-order-appended" or "admin-order-appended" or "order-placed" =>
                "released-to-kitchen",
            "advance" => null,
            _ => "released-to-kitchen"
        };

        if (stage is not null)
            await BroadcastOrderStageAsync(hub, db, orderId, stage, previousStatus, cancellationToken: cancellationToken);
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

        await BroadcastOrderStageAsync(
            hub, db, orderId, "status-ready", "In Kitchen", cancellationToken: cancellationToken);

        var serverNotification = OrderHubNotificationFactory.TryBuildServerReady(order);
        if (serverNotification is not null)
        {
            await hub.Clients.Group("Server")
                .SendAsync("ServerReadyOrderArrived", serverNotification, cancellationToken);
        }
    }

    /// <summary>Bar or kitchen finished their side of a mixed ticket (order may still be In Kitchen).</summary>
    public static async Task NotifyServerStationPrepReadyAsync(
        IHubContext<OrderHub> hub,
        AppDbContext db,
        int orderId,
        string? prepStationPortal,
        CancellationToken cancellationToken = default)
    {
        if (!KitchenQueueKindFilter.IsPrepStationPortal(prepStationPortal)
            || prepStationPortal!.Equals(KitchenQueueKindFilter.PortalKitchenBar, StringComparison.OrdinalIgnoreCase))
            return;

        var taxonomyJson = await db.PublicMenuSettings.AsNoTracking()
            .Where(s => s.Key == "default")
            .Select(s => s.MenuTaxonomyJson)
            .FirstOrDefaultAsync(cancellationToken);
        var taxonomy = MenuTaxonomyHelper.ResolveEffective(taxonomyJson);
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
            return;

        var notification = OrderHubNotificationFactory.TryBuildServerStationReady(order, prepStationPortal, taxonomy);
        if (notification is null)
            return;

        await hub.Clients.Group("Server")
            .SendAsync("ServerStationReadyArrived", notification, cancellationToken);
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

    /// <summary>Guest QR cart saved as draft (not yet an order).</summary>
    public static Task NotifyCustomerDraftArrivedAsync(
        IHubContext<OrderHub> hub,
        object draftPayload,
        CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>
        {
            hub.Clients.Group("Server").SendAsync("CustomerDraftArrived", draftPayload, cancellationToken),
            hub.Clients.Group("Server").SendAsync("OrderStageChanged", new OrderStageChangedDto
            {
                OrderId = 0,
                OrderCode = string.Empty,
                NewStatus = "Draft",
                Stage = "customer-draft",
                Message = "Customer sent items from the QR menu",
                Audiences = new[] { "Server" }
            }, cancellationToken)
        };
        return Task.WhenAll(tasks);
    }
}
