using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Orders;

/// <summary>
/// One-time style migration: in-store tickets still in <see cref="OrderWorkflow.PendingCashier"/>
/// after removing the cashier gate are released to the kitchen with inventory deduction.
/// </summary>
public static class PendingCashierBulkRelease
{
    public static int ReleaseLegacyInStorePendingCashier(AppDbContext db)
    {
        var pendingIds = db.Orders.AsNoTracking()
            .Where(o =>
                o.Status == OrderWorkflow.PendingCashier
                && o.OrderOrigin == OrderOrigin.InStore)
            .Select(o => o.Id)
            .ToList();

        if (pendingIds.Count == 0)
            return 0;

        var ops = new AdminOrderOperationsService(db);
        var released = 0;
        foreach (var id in pendingIds)
        {
            if (ops.TryReleasePendingToKitchen(id).Ok)
                released++;
        }

        return released;
    }
}
