using System.Linq;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Data;

/// <summary>Idempotent data consistency repairs and active-order maintenance (not schema migrations).</summary>
public static class DataReconciler
{
    public static void RunFinancialConsistency(AppDbContext db) =>
        FinancialTransactionService.EnsureCompletedOrderRevenues(db);

    /// <summary>Same definition as admin active list: Waiting, In Kitchen, Ready, or Served (case-insensitive).</summary>
    public static bool IsActiveOrderStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;
        var s = status.Trim();
        return string.Equals(s, "Waiting", StringComparison.OrdinalIgnoreCase)
               || string.Equals(s, "In Kitchen", StringComparison.OrdinalIgnoreCase)
               || string.Equals(s, "Ready", StringComparison.OrdinalIgnoreCase)
               || string.Equals(s, OrderWorkflow.Served, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Deletes all in-progress orders and their line items, then sets affected tables to Available when no other active order uses them.
    /// Completed and Cancelled orders are left unchanged.
    /// </summary>
    /// <returns>Number of orders removed.</returns>
    public static int DeleteAllActiveOrders()
    {
        using var db = new AppDbContext();
        var active = db.Orders
            .Include(o => o.Items)
            .Where(o =>
                o.Status.ToLower() == "waiting"
                || o.Status.ToLower() == "in kitchen"
                || o.Status.ToLower() == "ready"
                || o.Status.ToLower() == "served")
            .ToList();

        if (active.Count > 0)
        {
            foreach (var order in active)
            {
                db.OrderItems.RemoveRange(order.Items);
                db.Orders.Remove(order);
            }

            db.SaveChanges();
        }

        ReconcileTableStatusesWithOrders(db);
        db.SaveChanges();
        return active.Count;
    }

    /// <summary>Sets each table to Occupied iff it has a Waiting / In Kitchen / Ready / Served order; otherwise Available (skips Maintenance).</summary>
    public static void ReconcileTableStatusesWithOrders(AppDbContext db)
    {
        var occupiedTableIds = new HashSet<int>();

        var deletedOrderIds = db.ChangeTracker.Entries<OrderRecord>()
            .Where(e => e.State == EntityState.Deleted)
            .Select(e => e.Entity.Id)
            .Where(id => id != 0)
            .ToHashSet();

        foreach (var entry in db.ChangeTracker.Entries<OrderRecord>())
        {
            if (entry.State == EntityState.Deleted)
                continue;
            var o = entry.Entity;
            if (!o.TableId.HasValue || !OrderWorkflow.OccupiesTable(o.Status))
                continue;
            occupiedTableIds.Add(o.TableId.Value);
        }

        var trackedPersistedOrderIds = db.ChangeTracker.Entries<OrderRecord>()
            .Where(e => e.State != EntityState.Deleted)
            .Select(e => e.Entity.Id)
            .Where(id => id != 0)
            .ToHashSet();

        foreach (var o in db.Orders.AsNoTracking().WhereOccupiesTable())
        {
            if (trackedPersistedOrderIds.Contains(o.Id))
                continue;
            if (deletedOrderIds.Contains(o.Id))
                continue;
            occupiedTableIds.Add(o.TableId!.Value);
        }

        foreach (var table in db.Tables.ToList())
        {
            if (string.Equals(table.Status, "Maintenance", StringComparison.OrdinalIgnoreCase))
                continue;

            table.Status = occupiedTableIds.Contains(table.Id) ? "Occupied" : "Available";
        }
    }

    /// <summary>HTTP/sync clients: same occupancy rules as <see cref="ReconcileTableStatusesWithOrders(AppDbContext)"/> without EF.</summary>
    public static void ReconcileTableStatusesWithOrders(IEnumerable<Table> tables, IReadOnlyList<OrderRecord> orders)
    {
        var occupiedTableIds = new HashSet<int>();
        foreach (var o in orders)
        {
            if (o.TableId is int tid && OrderWorkflow.OccupiesTable(o.Status))
                occupiedTableIds.Add(tid);
        }

        foreach (var table in tables)
        {
            if (string.Equals(table.Status, "Maintenance", StringComparison.OrdinalIgnoreCase))
                continue;

            table.Status = occupiedTableIds.Contains(table.Id) ? "Occupied" : "Available";
        }
    }
}
