using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Reporting;

public static class MoneyExcelReportRowsBuilder
{
    public static (IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows) BuildReportRows(
        AppDbContext db,
        string reportType,
        DateTime fromDate,
        DateTime toExclusive)
        => reportType switch
        {
            "Transactions" => BuildTransactionRows(db, fromDate, toExclusive),
            "Orders" => BuildOrderRows(db, fromDate, toExclusive),
            "Inventory" => BuildInventoryRows(db, fromDate, toExclusive),
            "Attendance" => BuildAttendanceRows(db, fromDate, toExclusive),
            _ => BuildTransactionRows(db, fromDate, toExclusive)
        };

    private static (IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows) BuildTransactionRows(
        AppDbContext db,
        DateTime fromDate,
        DateTime toExclusive)
    {
        var records = db.Transactions
            .AsNoTracking()
            .Where(t => t.Date >= fromDate && t.Date < toExclusive)
            .OrderBy(t => t.Date)
            .ThenBy(t => t.Id)
            .ToList();

        var rows = records
            .Select(t => (IReadOnlyList<string>)
            [
                t.Id.ToString(),
                t.Date.ToString("yyyy-MM-dd HH:mm"),
                t.Type,
                t.Category,
                MoneyReportingHelpers.NormalizeCurrencyCode(t.CurrencyCode),
                t.Amount.ToString("N2"),
                t.IsFixed ? "Yes" : "No",
                t.Justification
            ])
            .ToList();

        return (["Id", "Date", "Type", "Category", "Currency", "Amount", "IsFixed", "Justification"], rows);
    }

    private static (IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows) BuildOrderRows(
        AppDbContext db,
        DateTime fromDate,
        DateTime toExclusive)
    {
        var orders = db.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= fromDate && o.CreatedAt < toExclusive)
            .OrderBy(o => o.CreatedAt)
            .ToList();

        var orderItems = db.OrderItems
            .AsNoTracking()
            .Where(i => orders.Select(o => o.Id).Contains(i.OrderRecordId))
            .ToList();

        var products = db.Products
            .AsNoTracking()
            .ToDictionary(p => p.Id, p => p.Price);

        var totalsByOrder = orderItems
            .GroupBy(i => i.OrderRecordId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(item => (products.TryGetValue(item.ProductId, out var price) ? price : 0m) * item.Quantity));

        var rows = orders
            .Select(order => (IReadOnlyList<string>)
            [
                order.Id.ToString(),
                string.IsNullOrWhiteSpace(order.UniqueId) ? $"ORD-{order.Id:000}" : order.UniqueId,
                order.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                string.Equals(order.OrderSource, "Reservation", StringComparison.OrdinalIgnoreCase)
                    ? $"Reservation ({(string.IsNullOrWhiteSpace(order.ReservationCode) ? "-" : order.ReservationCode)})"
                    : "WalkIn",
                order.Status,
                order.TableCode,
                order.ServerName,
                (totalsByOrder.TryGetValue(order.Id, out var total) ? total : 0m).ToString("N2")
            ])
            .ToList();

        return (["Id", "OrderId", "Date", "Source", "Status", "Table", "Server", "Total"], rows);
    }

    private static (IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows) BuildInventoryRows(
        AppDbContext db,
        DateTime fromDate,
        DateTime toExclusive)
    {
        var orders = db.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= fromDate && o.CreatedAt < toExclusive && o.Status != "Cancelled")
            .Select(o => new { o.Id, o.UniqueId })
            .ToList();
        var orderIds = orders.Select(o => o.Id).ToList();

        var orderItems = db.OrderItems
            .AsNoTracking()
            .Where(i => orderIds.Contains(i.OrderRecordId))
            .ToList();
        var ingredients = db.ProductIngredients
            .AsNoTracking()
            .ToList();
        var inventory = db.InventoryItems
            .AsNoTracking()
            .ToDictionary(i => i.Id, i => i);

        var ingredientsByProduct = ingredients
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var usedByInventory = new Dictionary<int, decimal>();
        var orderCountByInventory = new Dictionary<int, int>();

        foreach (var line in orderItems)
        {
            if (!ingredientsByProduct.TryGetValue(line.ProductId, out var recipe))
                continue;

            foreach (var ingredient in recipe)
            {
                var consumed = ingredient.Quantity * line.Quantity;
                if (!usedByInventory.TryAdd(ingredient.InventoryItemId, consumed))
                    usedByInventory[ingredient.InventoryItemId] += consumed;

                if (!orderCountByInventory.TryAdd(ingredient.InventoryItemId, 1))
                    orderCountByInventory[ingredient.InventoryItemId]++;
            }
        }

        var rows = usedByInventory
            .OrderByDescending(kv => kv.Value)
            .Select(kv =>
            {
                var item = inventory.TryGetValue(kv.Key, out var inv) ? inv : null;
                var count = orderCountByInventory.TryGetValue(kv.Key, out var c) ? c : 0;
                return (IReadOnlyList<string>)
                [
                    item?.UniqueId ?? "N/A",
                    item?.Name ?? "Unknown",
                    item?.Unit ?? string.Empty,
                    kv.Value.ToString("0.##"),
                    (item?.StockQuantity ?? 0m).ToString("0.##"),
                    count.ToString()
                ];
            })
            .ToList();

        return (["ItemId", "Item", "Unit", "UsedQty", "CurrentStock", "LinkedOrders"], rows);
    }

    private static (IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows) BuildAttendanceRows(
        AppDbContext db,
        DateTime fromDate,
        DateTime toExclusive)
    {
        var fromUtc = AttendanceCalendar.DayAnchorUtc(fromDate.Date);
        var toExclusiveUtc = AttendanceCalendar.DayAnchorUtc(toExclusive.Date);
        var rows = db.EmployeeAttendances
            .AsNoTracking()
            .Include(a => a.Employee)
            .Where(a => a.WorkDate >= fromUtc && a.WorkDate < toExclusiveUtc)
            .OrderBy(a => a.WorkDate)
            .ThenBy(a => a.EmployeeId)
            .ToList()
            .Select(a => (IReadOnlyList<string>)
            [
                a.WorkDate.ToString("yyyy-MM-dd"),
                a.Employee?.UniqueId ?? string.Empty,
                a.Employee?.Name ?? "Unknown",
                a.ClockInTime?.ToString("HH:mm") ?? "-",
                a.ClockOutTime?.ToString("HH:mm") ?? "-",
                string.IsNullOrWhiteSpace(a.ClockInStatus) ? "Pending" : a.ClockInStatus,
                a.Justification
            ])
            .ToList();

        return (["Date", "EmployeeId", "Employee", "ClockIn", "ClockOut", "Status", "Justification"], rows);
    }
}
