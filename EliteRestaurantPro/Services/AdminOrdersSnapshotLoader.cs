using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ViewModels;
using Microsoft.EntityFrameworkCore;
using ModelTable = EliteRestaurant.Core.Models.Table;

namespace EliteRestaurantPro.Services;

public static class AdminOrdersSnapshotLoader
{
    private const int MaxPastOrdersToDisplay = 250;

    public static AdminOrdersLoadedSnapshot Load(bool showAdminAdvance, bool canViewTicket)
    {
        using var db = new AppDbContext();

        var activeOrders = db.Orders
            .AsNoTracking()
            .Include(o => o.Table)
            .Include(o => o.Server)
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Where(o => o.Status == "Waiting" || o.Status == "In Kitchen" || o.Status == "Ready" ||
                        o.Status == OrderWorkflow.Served)
            .OrderByDescending(o => o.CreatedAt)
            .ToList()
            .Select(o => AdminOrdersViewMapper.MapOrder(o, false, showAdminAdvance, canViewTicket))
            .ToList();

        var pastOrders = db.Orders
            .AsNoTracking()
            .Include(o => o.Table)
            .Include(o => o.Server)
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Where(o => o.Status == "Completed" || o.Status == "Cancelled")
            .OrderByDescending(o => o.CreatedAt)
            .Take(MaxPastOrdersToDisplay)
            .ToList()
            .Select(o => AdminOrdersViewMapper.MapOrder(o, true, showAdminAdvance, canViewTicket))
            .ToList();

        var tables = db.Tables
            .AsNoTracking()
            .Include(t => t.AssignedServer)
            .Where(t => t.Status == "Available" && t.AssignedServerId != null)
            .OrderBy(t => t.TableNumber)
            .ToList();

        var products = db.Products
            .AsNoTracking()
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Name)
            .Select(product => new ProductSelectionItemViewModel
            {
                ProductId = product.Id,
                Name = product.Name,
                Category = product.Category,
                Price = product.Price
            })
            .ToList();

        var pendingOrders = db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Where(o => o.Status == OrderWorkflow.PendingCashier)
            .OrderByDescending(o => o.CreatedAt)
            .ToList();

        var pendingRows = new List<CashierQueueRow>();
        foreach (var o in pendingOrders)
        {
            var subtotal = o.Items.Sum(i => (i.Product?.Price ?? 0m) * i.Quantity);
            var totals = OrderTotalsHelper.ComputeTotals(subtotal, o.DiscountMode, o.DiscountValue);
            var lines = string.Join(", ",
                o.Items.Select(i =>
                    $"{i.Product?.Name ?? "Item"} x{i.Quantity}"));
            pendingRows.Add(new CashierQueueRow
            {
                OrderId = o.Id,
                OrderCode = string.IsNullOrWhiteSpace(o.UniqueId) ? $"#{o.Id:000}" : o.UniqueId,
                TableLabel = $"{o.TableCode} · {o.TableName}".Trim(' ', '·'),
                ServerName = o.ServerName,
                CreatedAt = o.CreatedAt,
                CreatedAtText = o.CreatedAt.ToString("MMM d, yyyy · HH:mm"),
                GrandTotalUsd = totals.GrandTotal,
                GrandTotalText = $"$ {totals.GrandTotal:N2}",
                LinesSummary = string.IsNullOrWhiteSpace(lines) ? "No lines" : lines
            });
        }

        return new AdminOrdersLoadedSnapshot
        {
            PendingCashier = pendingRows,
            ActiveOrders = activeOrders,
            PastOrders = pastOrders,
            AvailableTables = tables,
            ProductSelections = products
        };
    }
}
