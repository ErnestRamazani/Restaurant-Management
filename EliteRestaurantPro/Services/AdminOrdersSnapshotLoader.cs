using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.ViewModels;

namespace EliteRestaurantPro.Services;

public static class AdminOrdersSnapshotLoader
{
    private const int MaxPastOrdersToDisplay = 250;

    public static async Task<AdminOrdersLoadedSnapshot> LoadAsync(
        bool showAdminAdvance,
        bool canViewTicket,
        CancellationToken cancellationToken = default)
    {
        var data = new AdminDataApiClient();
        var ordersTask = data.GetOrdersAsync(cancellationToken);
        var tablesTask = data.GetTablesAsync(cancellationToken);
        var productsTask = data.GetProductsAsync(cancellationToken);
        var employeesTask = data.GetEmployeesAsync(cancellationToken);
        await Task.WhenAll(ordersTask, tablesTask, productsTask, employeesTask).ConfigureAwait(false);

        var orders = (await ordersTask.ConfigureAwait(false)).ToList();
        var tables = (await tablesTask.ConfigureAwait(false)).ToList();
        var products = (await productsTask.ConfigureAwait(false)).ToList();
        var employees = (await employeesTask.ConfigureAwait(false)).ToList();

        var productById = products.ToDictionary(p => p.Id);
        var tablesById = tables.ToDictionary(t => t.Id);
        var employeesById = employees.ToDictionary(e => e.Id);

        foreach (var t in tables)
        {
            if (t.AssignedServerId is int aid && employeesById.TryGetValue(aid, out var srv))
                t.AssignedServer = srv;
        }

        foreach (var o in orders)
        {
            if (o.TableId is int tid && tablesById.TryGetValue(tid, out var tbl))
                o.Table = tbl;
            if (o.ServerId is int sid && employeesById.TryGetValue(sid, out var emp))
                o.Server = emp;
            foreach (var item in o.Items)
            {
                if (productById.TryGetValue(item.ProductId, out var p))
                    item.Product = p;
            }
        }

        var activeOrders = orders
            .Where(o => o.Status == "Waiting" || o.Status == "In Kitchen" || o.Status == "Ready" ||
                        o.Status == OrderWorkflow.Served)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => AdminOrdersViewMapper.MapOrder(o, false, showAdminAdvance, canViewTicket))
            .ToList();

        var pastOrders = orders
            .Where(o => string.Equals(o.Status, "Completed", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(o.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(o => o.CreatedAt)
            .Take(MaxPastOrdersToDisplay)
            .Select(o => AdminOrdersViewMapper.MapOrder(o, true, showAdminAdvance, canViewTicket))
            .ToList();

        var availableTables = tables
            .Where(t => t.Status == "Available" && t.AssignedServerId != null)
            .OrderBy(t => t.TableNumber)
            .ToList();

        var productSelections = products
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

        var pendingOrders = orders
            .Where(o => OrderWorkflow.AwaitsCashierOrApprovalBeforeKitchen(o.Status))
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
            AvailableTables = availableTables,
            ProductSelections = productSelections
        };
    }
}
