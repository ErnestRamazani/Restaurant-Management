using EliteRestaurant.Core.Menu;
using EliteRestaurant.Core.Models;

namespace EliteRestaurant.Core.Orders;

/// <summary>Food vs drink split for kitchen (<c>/kitchen/</c>) and bar (<c>/bar/</c>) KDS queues.</summary>
public static class KitchenQueueKindFilter
{
    public const string PortalKitchen = "Kitchen";
    public const string PortalBar = "Bar";
    public const string PortalKitchenBar = "KitchenBar";

    public static bool IsPrepStationPortal(string? portal)
    {
        if (string.IsNullOrWhiteSpace(portal))
            return false;

        var p = portal.Trim();
        return p.Equals(PortalKitchen, StringComparison.OrdinalIgnoreCase)
               || p.Equals(PortalBar, StringComparison.OrdinalIgnoreCase)
               || p.Equals(PortalKitchenBar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Infer Food/Drink/Mixed from order lines. Returns null when empty or products missing.</summary>
    public static string? TryInferOrderCheckKind(OrderRecord order, MenuTaxonomySettings? taxonomy = null)
    {
        var products = GetProductsFromOrder(order).ToList();
        if (products.Count == 0)
            return null;

        return OpenCheckKindHelper.TryInferCheckKindFromProducts(products, taxonomy);
    }

    /// <summary>True when the order has at least one food line (non-drink category).</summary>
    public static bool OrderHasFoodLines(OrderRecord order, MenuTaxonomySettings? taxonomy = null) =>
        GetProductsFromOrder(order).Any(p => !MenuTaxonomyHelper.IsDrinkProduct(p, taxonomy));

    /// <summary>True when the order has at least one drink line.</summary>
    public static bool OrderHasDrinkLines(OrderRecord order, MenuTaxonomySettings? taxonomy = null) =>
        GetProductsFromOrder(order).Any(p => MenuTaxonomyHelper.IsDrinkProduct(p, taxonomy));

    /// <summary>
    /// Whether an order belongs on the portal queue. Legacy <c>KitchenBar</c> shows all KDS orders;
    /// <c>Kitchen</c> requires any food line; <c>Bar</c> requires any drink line (mixed orders appear on both).
    /// </summary>
    public static bool OrderMatchesPortalQueue(string? portal, OrderRecord order, MenuTaxonomySettings? taxonomy = null)
    {
        if (!IsPrepStationPortal(portal))
            return false;

        if (portal!.Equals(PortalKitchenBar, StringComparison.OrdinalIgnoreCase))
            return true;

        if (portal.Equals(PortalKitchen, StringComparison.OrdinalIgnoreCase))
            return OrderHasFoodLines(order, taxonomy);

        if (portal.Equals(PortalBar, StringComparison.OrdinalIgnoreCase))
            return OrderHasDrinkLines(order, taxonomy);

        return false;
    }

    /// <summary>Lines visible on a prep portal (food-only on Kitchen, drink-only on Bar).</summary>
    public static IReadOnlyList<OrderItem> FilterItemsForPortal(
        string? portal,
        IReadOnlyList<OrderItem> items,
        MenuTaxonomySettings? taxonomy = null)
    {
        if (items.Count == 0 || !IsPrepStationPortal(portal))
            return items;

        if (portal!.Equals(PortalKitchenBar, StringComparison.OrdinalIgnoreCase))
            return items;

        if (portal.Equals(PortalKitchen, StringComparison.OrdinalIgnoreCase))
            return items.Where(i => i.Product is not null && !MenuTaxonomyHelper.IsDrinkProduct(i.Product, taxonomy)).ToList();

        if (portal.Equals(PortalBar, StringComparison.OrdinalIgnoreCase))
            return items.Where(i => i.Product is not null && MenuTaxonomyHelper.IsDrinkProduct(i.Product, taxonomy)).ToList();

        return items;
    }

    public static IEnumerable<OrderRecord> FilterForPortal(
        string? portal,
        IEnumerable<OrderRecord> orders,
        MenuTaxonomySettings? taxonomy = null) =>
        orders.Where(o => OrderMatchesPortalQueue(portal, o, taxonomy));

    private static IEnumerable<Product> GetProductsFromOrder(OrderRecord order)
    {
        var items = order.Items?.ToList() ?? [];
        foreach (var item in items)
        {
            if (item.Product is not null)
                yield return item.Product;
        }
    }
}
