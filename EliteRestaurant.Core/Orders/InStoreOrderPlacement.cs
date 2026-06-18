using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurant.Core.Orders;

/// <summary>
/// Dine-in server/admin tickets enter the kitchen queue immediately (no cashier release gate).
/// Online <see cref="OrderWorkflow.PendingApproval"/> still uses cashier release.
/// </summary>
public static class InStoreOrderPlacement
{
    public const string KitchenWaitingStatus = "Waiting";

    /// <summary>Validate stock, deduct inventory, and set <see cref="KitchenWaitingStatus"/>.</summary>
    public static string? TryPlaceNewInStoreOrder(AppDbContext db, OrderRecord order)
    {
        var err = OrderInventoryDeduction.TryApplyForPlacedOrder(db, order);
        if (err is not null)
            return err;

        order.Status = KitchenWaitingStatus;
        return null;
    }

    /// <summary>Re-open kitchen incoming queue after new lines on an in-progress check.</summary>
    public static void RequeueOpenCheckToKitchen(OrderRecord order)
    {
        if (string.Equals(order.Status, "Ready", StringComparison.OrdinalIgnoreCase)
            || OrderWorkflow.IsServed(order.Status)
            || OrderWorkflow.IsKitchenQueueStatus(order.Status))
        {
            order.Status = KitchenWaitingStatus;
            order.CustomerFulfillmentStatus = null;
        }
    }
}
