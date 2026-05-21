using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurant.Core.Orders;

/// <summary>
/// Per-portal (kitchen vs bar) prep on mixed tickets. Order <c>Ready</c> requires every line prepared;
/// each portal only stamps and completes its own food or drink lines.
/// </summary>
public static class KitchenStationPrep
{
    /// <summary>True for <c>Kitchen</c> or <c>Bar</c> (not legacy <c>KitchenBar</c> or null).</summary>
    public static bool AppliesStationScope(string? prepStationPortal) =>
        KitchenQueueKindFilter.IsPrepStationPortal(prepStationPortal)
        && !prepStationPortal!.Equals(KitchenQueueKindFilter.PortalKitchenBar, StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<OrderItem> GetPortalLines(string? prepStationPortal, IReadOnlyList<OrderItem> allItems) =>
        KitchenQueueKindFilter.FilterItemsForPortal(prepStationPortal, allItems);

    public static bool AllPortalLinesPrepared(string? prepStationPortal, IReadOnlyList<OrderItem> allItems)
    {
        var portalLines = GetPortalLines(prepStationPortal, allItems);
        return portalLines.Count > 0 && portalLines.All(KitchenLineVisibility.IsLinePrepared);
    }

    public static bool AllOrderLinesPrepared(IReadOnlyList<OrderItem> allItems) =>
        allItems.Count > 0 && allItems.All(KitchenLineVisibility.IsLinePrepared);

    public static void MarkPortalUnpreparedLinesPrepared(
        string? prepStationPortal,
        IEnumerable<OrderItem> allItems,
        DateTime? preparedAt = null)
    {
        var toStamp = AppliesStationScope(prepStationPortal)
            ? GetPortalLines(prepStationPortal, allItems.ToList()).Where(i => !KitchenLineVisibility.IsLinePrepared(i))
            : allItems.Where(i => !KitchenLineVisibility.IsLinePrepared(i));
        KitchenLineVisibility.MarkUnpreparedLinesPrepared(toStamp, preparedAt);
    }
}
