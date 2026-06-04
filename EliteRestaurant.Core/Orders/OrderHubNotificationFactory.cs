using EliteRestaurant.Core.Menu;
using EliteRestaurant.Core.Models;

namespace EliteRestaurant.Core.Orders;

public static class OrderHubNotificationFactory
{
    public static ServerStationReadyOrderNotification? TryBuildServerStationReady(
        OrderRecord order,
        string? prepStationPortal,
        MenuTaxonomySettings? taxonomy = null)
    {
        if (order.ServerId is null or <= 0)
            return null;
        if (!string.Equals(order.Status, "In Kitchen", StringComparison.OrdinalIgnoreCase))
            return null;
        if (!KitchenStationPrep.AppliesStationScope(prepStationPortal))
            return null;

        var state = ServerOrderStationStatus.Compute(order, taxonomy);
        if (!state.ShowOnServerPickup)
            return null;

        var portal = prepStationPortal!.Trim();
        if (portal.Equals(KitchenQueueKindFilter.PortalKitchen, StringComparison.OrdinalIgnoreCase)
            && (!state.FoodPrepReady || state.FoodServed || !state.HasFoodLines))
            return null;
        if (portal.Equals(KitchenQueueKindFilter.PortalBar, StringComparison.OrdinalIgnoreCase)
            && (!state.BarPrepReady || state.BarServed || !state.HasDrinkLines))
            return null;

        var code = string.IsNullOrWhiteSpace(order.UniqueId) ? $"#{order.Id:000}" : order.UniqueId.Trim();
        var guest = ResolveGuestCustomerName(order);
        var summary = ServerOrderStationStatus.BuildPrepSummary(state);

        return new ServerStationReadyOrderNotification(
            order.Id,
            order.ServerId.Value,
            code,
            OrderRecordUiLabels.TableCaption(order),
            guest,
            portal,
            summary,
            order.OrderOrigin ?? OrderOrigin.InStore,
            order.OrderSource ?? "WalkIn");
    }

    public static ServerReadyOrderNotification? TryBuildServerReady(OrderRecord order)
    {
        if (order.ServerId is null or <= 0)
            return null;
        if (!string.Equals(order.Status, "Ready", StringComparison.OrdinalIgnoreCase))
            return null;

        var code = string.IsNullOrWhiteSpace(order.UniqueId) ? $"#{order.Id:000}" : order.UniqueId.Trim();
        var guest = ResolveGuestCustomerName(order);
        var items = order.Items ?? Array.Empty<OrderItem>();
        var summary = items.Count == 0
            ? "-"
            : string.Join(", ", items.Select(i => $"{(string.IsNullOrWhiteSpace(i.Product?.Name) ? "Unknown" : i.Product!.Name.Trim())} x{i.Quantity}"));
        var count = items.Sum(i => i.Quantity);

        return new ServerReadyOrderNotification(
            order.Id,
            order.ServerId.Value,
            code,
            OrderRecordUiLabels.TableCaption(order),
            guest,
            summary,
            count,
            order.OrderOrigin ?? OrderOrigin.InStore,
            order.OrderSource ?? "WalkIn");
    }

    private static string? ResolveGuestCustomerName(OrderRecord order)
    {
        var ticket = DeliveryTicketInfoParser.TryParse(order);
        if (ticket is not null && !string.IsNullOrWhiteSpace(ticket.CustomerName))
            return ticket.CustomerName.Trim();

        var reservation = (order.ReservationGuestName ?? string.Empty).Trim();
        return reservation.Length > 0 ? reservation : null;
    }
}
