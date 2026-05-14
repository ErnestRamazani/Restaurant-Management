using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Utils;

/// <summary>In-app banner: Ready (pickup) and Served (await checkout) for server / cashier.</summary>
public static class StaffOrderAlerts
{
    public static string GetBannerText()
    {
        if (!AppSession.IsServerTablet && !AppSession.IsCashierTablet)
            return string.Empty;

        var useLocalAlerts = string.Equals(
            Environment.GetEnvironmentVariable("ELITE_DESKTOP_USE_LOCAL_ALERTS"),
            "1",
            StringComparison.OrdinalIgnoreCase);
        if (!useLocalAlerts)
            return string.Empty;

        using var db = new AppDbContext();

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
            .Select(o => new { o.UniqueId, o.ServerId, o.TableCode, o.TableName, o.OrderOrigin, o.OrderSource })
            .ToList();

        if (AppSession.IsServerTablet)
            ready = ready.Where(o => o.ServerId == AppSession.StaffEmployeeId).ToList();

        var served = db.Orders
            .AsNoTracking()
            .Where(o => o.Status == OrderWorkflow.Served)
            .OrderBy(o => o.CreatedAt)
            .Select(o => new { o.UniqueId, o.ServerId, o.TableCode, o.OrderOrigin, o.OrderSource })
            .ToList();

        if (AppSession.IsServerTablet)
            served = served.Where(o => o.ServerId == AppSession.StaffEmployeeId).ToList();

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
            var prefix = AppSession.IsCashierTablet
                ? "Awaiting checkout (Served) — "
                : "At table — ring cashier when paid — ";
            lines.Add(prefix + string.Join(" · ", parts) + more);
        }

        return lines.Count == 0 ? string.Empty : string.Join("\n", lines);
    }
}
