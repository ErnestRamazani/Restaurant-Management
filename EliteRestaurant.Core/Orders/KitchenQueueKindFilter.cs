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



    /// <summary>Infer Food/Drink from order lines. Returns null when empty, mixed, or products missing.</summary>

    public static string? TryInferOrderCheckKind(OrderRecord order)

    {

        var products = GetProductsFromOrder(order).ToList();

        if (products.Count == 0)

            return null;



        return OpenCheckKindHelper.TryInferCheckKindFromProducts(products);

    }



    /// <summary>True when the order has at least one food line (non-drink category).</summary>

    public static bool OrderHasFoodLines(OrderRecord order) =>

        GetProductsFromOrder(order).Any(p => !OpenCheckKindHelper.IsDrinkCategory(p.Category));



    /// <summary>True when the order has at least one drink line.</summary>

    public static bool OrderHasDrinkLines(OrderRecord order) =>

        GetProductsFromOrder(order).Any(p => OpenCheckKindHelper.IsDrinkCategory(p.Category));



    /// <summary>

    /// Whether an order belongs on the portal queue. Legacy <c>KitchenBar</c> shows all KDS orders;

    /// <c>Kitchen</c> requires any food line; <c>Bar</c> requires any drink line (mixed orders appear on both).

    /// </summary>

    public static bool OrderMatchesPortalQueue(string? portal, OrderRecord order)

    {

        if (!IsPrepStationPortal(portal))

            return false;



        if (portal!.Equals(PortalKitchenBar, StringComparison.OrdinalIgnoreCase))

            return true;



        if (portal.Equals(PortalKitchen, StringComparison.OrdinalIgnoreCase))

            return OrderHasFoodLines(order);



        if (portal.Equals(PortalBar, StringComparison.OrdinalIgnoreCase))

            return OrderHasDrinkLines(order);



        return false;

    }



    /// <summary>Lines visible on a prep portal (food-only on Kitchen, drink-only on Bar).</summary>

    public static IReadOnlyList<OrderItem> FilterItemsForPortal(string? portal, IReadOnlyList<OrderItem> items)

    {

        if (items.Count == 0 || !IsPrepStationPortal(portal))

            return items;



        if (portal!.Equals(PortalKitchenBar, StringComparison.OrdinalIgnoreCase))

            return items;



        if (portal.Equals(PortalKitchen, StringComparison.OrdinalIgnoreCase))

            return items.Where(i => i.Product is not null && !OpenCheckKindHelper.IsDrinkCategory(i.Product.Category)).ToList();



        if (portal.Equals(PortalBar, StringComparison.OrdinalIgnoreCase))

            return items.Where(i => i.Product is not null && OpenCheckKindHelper.IsDrinkCategory(i.Product.Category)).ToList();



        return items;

    }



    public static IEnumerable<OrderRecord> FilterForPortal(string? portal, IEnumerable<OrderRecord> orders) =>

        orders.Where(o => OrderMatchesPortalQueue(portal, o));



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


