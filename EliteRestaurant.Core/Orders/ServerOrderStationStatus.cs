using EliteRestaurant.Core.Menu;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurant.Core.Orders;

/// <summary>Food vs bar prep and server pickup for mixed tickets.</summary>
public static class ServerOrderStationStatus
{
    public sealed record StationState(
        bool HasFoodLines,
        bool HasDrinkLines,
        bool FoodPrepReady,
        bool BarPrepReady,
        bool FoodServed,
        bool BarServed,
        bool IsFullyPrepReady,
        bool IsFullyServed,
        bool ShowOnServerPickup,
        bool CanServeFoodStation,
        bool CanServeBarStation);

    public static StationState Compute(OrderRecord order, MenuTaxonomySettings? taxonomy = null)
    {
        var items = order.Items?.ToList() ?? [];
        var food = items.Where(i => i.Product is not null && !MenuTaxonomyHelper.IsDrinkProduct(i.Product, taxonomy)).ToList();
        var drink = items.Where(i => i.Product is not null && MenuTaxonomyHelper.IsDrinkProduct(i.Product, taxonomy)).ToList();

        var hasFood = food.Count > 0;
        var hasDrink = drink.Count > 0;
        var foodPrepReady = !hasFood || food.All(KitchenLineVisibility.IsLinePrepared);
        var barPrepReady = !hasDrink || drink.All(KitchenLineVisibility.IsLinePrepared);
        var foodServed = !hasFood || food.All(IsLineServed);
        var barServed = !hasDrink || drink.All(IsLineServed);
        var fullyPrep = items.Count > 0 && items.All(KitchenLineVisibility.IsLinePrepared);
        var fullyServed = items.Count > 0 && items.All(IsLineServed);

        var status = order.Status ?? string.Empty;
        var inKitchen = string.Equals(status, "In Kitchen", StringComparison.OrdinalIgnoreCase);
        var ready = OrderWorkflow.IsReady(status);
        var showPickup = ready
            || (inKitchen && ((hasFood && foodPrepReady && !foodServed) || (hasDrink && barPrepReady && !barServed)));

        return new StationState(
            hasFood,
            hasDrink,
            foodPrepReady,
            barPrepReady,
            foodServed,
            barServed,
            fullyPrep,
            fullyServed,
            showPickup,
            hasFood && foodPrepReady && !foodServed,
            hasDrink && barPrepReady && !barServed);
    }

    public static bool IsLineServed(OrderItem item) => item.ServerServedAt is not null;

    public static string BuildPrepSummary(StationState s)
    {
        if (s.IsFullyPrepReady && !s.IsFullyServed)
            return "All items ready to serve";

        var parts = new List<string>();
        if (s.HasDrinkLines)
        {
            if (s.BarServed) parts.Add("Drinks served");
            else if (s.BarPrepReady) parts.Add("Drinks ready");
            else parts.Add("Drinks cooking");
        }

        if (s.HasFoodLines)
        {
            if (s.FoodServed) parts.Add("Food served");
            else if (s.FoodPrepReady) parts.Add("Food ready");
            else parts.Add("Food cooking");
        }

        return parts.Count > 0 ? string.Join(" · ", parts) : string.Empty;
    }

    public static void MarkStationServed(IEnumerable<OrderItem> items, string prepStationPortal, MenuTaxonomySettings? taxonomy)
    {
        var portalLines = KitchenStationPrep.GetPortalLines(prepStationPortal, items.ToList());
        var now = DateTime.UtcNow;
        foreach (var line in portalLines)
        {
            if (!KitchenLineVisibility.IsLinePrepared(line))
                throw new InvalidOperationException("This station has not finished preparing its items yet.");
            if (line.ServerServedAt is null)
                line.ServerServedAt = now;
        }
    }
}
