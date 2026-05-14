using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Utils;

/// <summary>API-friendly cashier banner: all Ready orders + all Served (awaiting checkout), matching desktop <see cref="StaffOrderAlerts"/> for cashier.</summary>
public static class CashierOrderAlerts
{
    public static IReadOnlyList<string> GetBannerLines(AppDbContext db)
    {
        static string Label(string? uid, string? tableCode, string? orderOrigin, string? orderSource)
        {
            var id = string.IsNullOrWhiteSpace(uid) ? "?" : uid;
            if (OrderOrigin.IsOnline(orderOrigin))
            {
                var delivery = string.Equals(orderSource, "Delivery", StringComparison.OrdinalIgnoreCase);
                var cap = delivery ? "Online · Delivery" : "Online · Pickup";
                return $"{id} ({cap})";
            }

            var code = string.IsNullOrWhiteSpace(tableCode) ? "?" : tableCode;
            return $"{id} ({code})";
        }

        var ready = db.Orders
            .AsNoTracking()
            .Where(o => o.Status == "Ready")
            .OrderBy(o => o.CreatedAt)
            .Select(o => new { o.UniqueId, o.TableCode, o.OrderOrigin, o.OrderSource })
            .ToList();

        var served = db.Orders
            .AsNoTracking()
            .Where(o => o.Status == OrderWorkflow.Served)
            .OrderBy(o => o.CreatedAt)
            .Select(o => new { o.UniqueId, o.TableCode, o.OrderOrigin, o.OrderSource })
            .ToList();

        var lines = new List<string>();

        if (ready.Count > 0)
        {
            var parts = ready.Take(4).Select(o => Label(o.UniqueId, o.TableCode, o.OrderOrigin, o.OrderSource)).ToList();
            var more = ready.Count > 4 ? $" (+{ready.Count - 4} more)" : string.Empty;
            lines.Add("Ready for pickup — " + string.Join(" · ", parts) + more);
        }

        if (served.Count > 0)
        {
            var parts = served.Take(4).Select(o => Label(o.UniqueId, o.TableCode, o.OrderOrigin, o.OrderSource)).ToList();
            var more = served.Count > 4 ? $" (+{served.Count - 4} more)" : string.Empty;
            lines.Add("Awaiting checkout (Served) — " + string.Join(" · ", parts) + more);
        }

        return lines;
    }
}
