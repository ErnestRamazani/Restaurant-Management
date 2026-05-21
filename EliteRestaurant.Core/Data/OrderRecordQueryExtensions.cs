using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurant.Core.Data;

/// <summary>EF-translatable filters (replaces non-translatable <see cref="OrderWorkflow"/> helpers in LINQ).</summary>
public static class OrderRecordQueryExtensions
{
    public static IQueryable<OrderRecord> WhereOpenCheckForTable(this IQueryable<OrderRecord> query, int tableId) =>
        query.Where(o => o.TableId == tableId &&
                         (o.Status == OrderWorkflow.PendingCashier
                          || o.Status == OrderWorkflow.PendingApproval
                          || o.Status == "Waiting"
                          || o.Status == "In Kitchen"
                          || o.Status == "Ready"
                          || o.Status == OrderWorkflow.Served));

    /// <summary>Post–cashier-release kitchen pipeline only (translatable; matches <see cref="OrderWorkflow.IsKitchenKdsVisibleStatus"/>).</summary>
    public static IQueryable<OrderRecord> WhereKitchenKdsVisible(this IQueryable<OrderRecord> query) =>
        query.Where(o =>
            o.Status.ToLower() == "waiting"
            || o.Status.ToLower() == "in kitchen"
            || o.Status.ToLower() == "ready");

    /// <summary>Online guest pickup (<c>TakeOut</c>/<c>Pickup</c>) or delivery — EF-translatable (do not use <see cref="OrderOrigin.IsOnline"/> in LINQ).</summary>
    public static IQueryable<OrderRecord> WhereOnlineDeliveryOrPickup(this IQueryable<OrderRecord> query) =>
        query.Where(o =>
            o.OrderOrigin == OrderOrigin.Online
            && (o.OrderSource == "Delivery"
                || o.OrderSource == "Pickup"
                || o.OrderSource == "TakeOut"));

    /// <summary>Same cases as <see cref="OrderWorkflow.OccupiesTable"/> (lowercase compare — translatable to SQL).</summary>
    public static IQueryable<OrderRecord> WhereOccupiesTable(this IQueryable<OrderRecord> query) =>
        query.Where(o =>
            o.TableId != null
            && (o.Status.ToLower() == "pending cashier"
                || o.Status.ToLower() == "pending approval"
                || o.Status.ToLower() == "waiting"
                || o.Status.ToLower() == "in kitchen"
                || o.Status.ToLower() == "ready"
                || o.Status.ToLower() == "served"));
}
