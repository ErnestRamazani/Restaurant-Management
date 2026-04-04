using EliteRestaurantPro.Models;
using EliteRestaurantPro.Utils;

namespace EliteRestaurantPro.Data;

/// <summary>EF-translatable filter for open checks on a table (replaces non-translatable IsOpenCheckStatus in LINQ).</summary>
public static class OrderRecordQueryExtensions
{
    public static IQueryable<OrderRecord> WhereOpenCheckForTable(this IQueryable<OrderRecord> query, int tableId) =>
        query.Where(o => o.TableId == tableId &&
                         (o.Status == OrderWorkflow.PendingCashier
                          || o.Status == "Waiting"
                          || o.Status == "In Kitchen"
                          || o.Status == "Ready"
                          || o.Status == OrderWorkflow.Served));
}
