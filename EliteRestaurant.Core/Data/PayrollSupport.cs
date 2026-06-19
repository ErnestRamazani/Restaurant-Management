using EliteRestaurant.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Data;

public static class PayrollSupport
{
    /// <summary>True if this unapplied advance counts toward the given payroll month.</summary>
    public static bool AdvanceAppliesToPayrollMonth(SalaryAdvance a, int year, int month)
    {
        var start = new DateTime(year, month, 1);
        var endEx = start.AddMonths(1);
        if (a.ForPayrollYear.HasValue && a.ForPayrollMonth.HasValue)
            return a.ForPayrollYear.Value == year && a.ForPayrollMonth.Value == month;
        return a.GivenAt >= start && a.GivenAt < endEx;
    }

    /// <summary>Sum of advances not yet applied that match this payroll month (evaluated in memory).</summary>
    public static decimal SumPendingAdvancesForPayrollMonth(AppDbContext db, int employeeId, int year, int month)
    {
        var list = db.SalaryAdvances.AsNoTracking()
            .Where(a => a.EmployeeId == employeeId && a.AppliedPayrollYear == null)
            .ToList();

        var sum = list.Where(a => AdvanceAppliesToPayrollMonth(a, year, month)).Sum(a => a.AmountUsd);
        return Math.Round(sum, 2);
    }

    public static decimal SumPendingAdvancesForPayrollMonth(
        IReadOnlyList<SalaryAdvance> advances,
        int employeeId,
        int year,
        int month)
    {
        var sum = advances
            .Where(a => a.EmployeeId == employeeId && a.AppliedPayrollYear == null)
            .Where(a => AdvanceAppliesToPayrollMonth(a, year, month))
            .Sum(a => a.AmountUsd);
        return Math.Round(sum, 2);
    }

    /// <summary>Merchandise subtotal (USD) from line items for completed orders served by this employee in the range.</summary>
    public static decimal SumServerCompletedOrderMerchandiseUsd(
        AppDbContext db,
        int serverEmployeeId,
        DateTime rangeStartInclusive,
        DateTime rangeEndExclusive)
    {
        var orderIds = db.Orders.AsNoTracking()
            .Where(o =>
                o.ServerId == serverEmployeeId &&
                o.Status == "Completed" &&
                o.CreatedAt >= rangeStartInclusive &&
                o.CreatedAt < rangeEndExclusive)
            .Select(o => o.Id)
            .ToList();

        if (orderIds.Count == 0)
            return 0m;

        var totals = db.OrderItems.AsNoTracking()
            .Where(i => orderIds.Contains(i.OrderRecordId))
            .Join(
                db.Products.AsNoTracking(),
                i => i.ProductId,
                p => p.Id,
                (i, p) => i.Quantity * (i.UnitPriceUsd > 0m ? i.UnitPriceUsd : p.Price))
            .ToList();

        return Math.Round(totals.Sum(), 2);
    }

    public static decimal SumServerCompletedOrderMerchandiseUsd(
        IReadOnlyList<OrderRecord> orders,
        IReadOnlyDictionary<int, decimal> productPriceById,
        int serverEmployeeId,
        DateTime rangeStartInclusive,
        DateTime rangeEndExclusive)
    {
        decimal sum = 0;
        foreach (var o in orders)
        {
            if (o.ServerId != serverEmployeeId || !string.Equals(o.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                continue;
            if (o.CreatedAt < rangeStartInclusive || o.CreatedAt >= rangeEndExclusive)
                continue;

            foreach (var i in o.Items)
            {
                var price = i.UnitPriceUsd > 0m
                    ? i.UnitPriceUsd
                    : (productPriceById.TryGetValue(i.ProductId, out var p)
                        ? p
                        : (i.Product?.Price ?? 0m));
                sum += price * i.Quantity;
            }
        }

        return Math.Round(sum, 2);
    }
}
