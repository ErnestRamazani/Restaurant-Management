using System.Globalization;
using System.Security.Claims;
using EliteRestaurant.Contracts.Admin;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Data;

public static class AdminWebDashboardAggregator
{
    public static AdminDashboardDto Build(AppDbContext db, ClaimsPrincipal? user)
    {
        var welcome = user?.FindFirst(ClaimTypes.Name)?.Value?.Trim();
        if (string.IsNullOrWhiteSpace(welcome))
            welcome = "Owner";

        var today = DateTime.Today;
        var yesterday = today.AddDays(-1);
        var tomorrow = today.AddDays(1);
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var dataStart = weekStart > yesterday ? yesterday : weekStart;
        var moneyWindowStart = today.AddDays(-6);

        var pricing = db.PublicMenuSettings.AsNoTracking()
                         .OrderBy(p => p.Id)
                         .FirstOrDefault()
                     ?? new PublicMenuSetting();

        var activeStatuses = new[]
        {
            "Waiting", "In Kitchen", "Ready", OrderWorkflow.Served, OrderWorkflow.PendingCashier,
            OrderWorkflow.PendingApproval
        };

        var pendingCashier = db.Orders.AsNoTracking()
            .Count(o =>
                o.Status == OrderWorkflow.PendingCashier || o.Status == OrderWorkflow.PendingApproval);
        var readyOrders = db.Orders.AsNoTracking()
            .Count(o => o.Status == "Ready");
        var activeOrdersCount = db.Orders.AsNoTracking()
            .Count(o => activeStatuses.Contains(o.Status));
        // Do not use string.Equals(..., StringComparison) here — EF Core cannot translate it to SQL.
        var inKitchenCount = db.Orders.AsNoTracking()
            .Count(o => o.Status != null && o.Status.ToLower() == "in kitchen");

        var occupiedTables = db.Tables.AsNoTracking().Count(t => t.Status == "Occupied");
        var totalTables = db.Tables.AsNoTracking().Count();
        var availableTables = db.Tables.AsNoTracking().Count(t => t.Status == "Available");

        var aggIds = db.Orders.AsNoTracking()
            .Where(o => o.CreatedAt >= dataStart && o.CreatedAt < tomorrow)
            .Select(o => o.Id);
        var activeIds = db.Orders.AsNoTracking()
            .Where(o => activeStatuses.Contains(o.Status))
            .Select(o => o.Id);
        var recentIds = db.Orders.AsNoTracking()
            .OrderByDescending(o => o.CreatedAt)
            .Take(60)
            .Select(o => o.Id);

        var orderIdList = aggIds
            .Union(activeIds)
            .Union(recentIds)
            .Distinct()
            .ToList();

        var orders = db.Orders.AsNoTracking()
            .Where(o => orderIdList.Contains(o.Id))
            .ToList();

        var orderItems = db.OrderItems.AsNoTracking()
            .Where(i => orderIdList.Contains(i.OrderRecordId))
            .ToList();

        var products = db.Products.AsNoTracking().ToList();
        var productPriceById = products.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First().Price);
        var productNameById = products.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First().Name);

        var orderLineSubtotals = orderItems
            .GroupBy(item => item.OrderRecordId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(item => (productPriceById.TryGetValue(item.ProductId, out var price) ? price : 0m) * item.Quantity));

        decimal GrandTotal(OrderRecord o)
        {
            var sub = orderLineSubtotals.TryGetValue(o.Id, out var t) ? t : 0m;
            return OrderTotalsHelper.ComputeTotals(sub, o.DiscountMode, o.DiscountValue, pricing).GrandTotal;
        }

        static bool IsCompleted(OrderRecord o)
            => string.Equals(o.Status, "Completed", StringComparison.OrdinalIgnoreCase);

        static bool IsCancelled(OrderRecord o)
            => string.Equals(o.Status, "Cancelled", StringComparison.OrdinalIgnoreCase);

        var latestOrdersForActivity = db.Orders.AsNoTracking()
            .OrderByDescending(o => o.CreatedAt)
            .Take(60)
            .ToList();

        var txMoneyWindow = db.Transactions.AsNoTracking()
            .Where(t => t.Date >= moneyWindowStart && t.Date < tomorrow)
            .ToList();

        var todaysOrders = orders
            .Where(o => o.CreatedAt >= today && o.CreatedAt < tomorrow && !IsCancelled(o))
            .OrderByDescending(o => o.CreatedAt)
            .ToList();
        var yesterdaysOrders = orders
            .Where(o => o.CreatedAt >= yesterday && o.CreatedAt < today && !IsCancelled(o))
            .ToList();

        var txToday = txMoneyWindow.Where(t => t.Date >= today && t.Date < tomorrow).ToList();
        var txYesterday = txMoneyWindow.Where(t => t.Date >= yesterday && t.Date < today).ToList();

        var todayRevenueUsdValue = MoneyDashboardTotals.SumRevenueByCurrency(txToday, CurrencyHelper.Usd);
        var todayRevenueFcValue = MoneyDashboardTotals.SumRevenueByCurrency(txToday, CurrencyHelper.CongoleseFranc);
        var todayExpensesUsdValue = MoneyDashboardTotals.SumExpensesUsd(txToday);

        var yesterdayRevenueUsdValue = MoneyDashboardTotals.SumRevenueByCurrency(txYesterday, CurrencyHelper.Usd);
        var yesterdayRevenueFcValue = MoneyDashboardTotals.SumRevenueByCurrency(txYesterday, CurrencyHelper.CongoleseFranc);
        var yesterdayExpensesUsdValue = MoneyDashboardTotals.SumExpensesUsd(txYesterday);

        var revenueUsdDelta = yesterdayRevenueUsdValue > 0m
            ? (todayRevenueUsdValue - yesterdayRevenueUsdValue) / yesterdayRevenueUsdValue * 100m
            : todayRevenueUsdValue > 0m ? 100m : 0m;
        var revenueFcDelta = yesterdayRevenueFcValue > 0m
            ? (todayRevenueFcValue - yesterdayRevenueFcValue) / yesterdayRevenueFcValue * 100m
            : todayRevenueFcValue > 0m ? 100m : 0m;
        var expensesUsdDelta = yesterdayExpensesUsdValue > 0m
            ? (todayExpensesUsdValue - yesterdayExpensesUsdValue) / yesterdayExpensesUsdValue * 100m
            : todayExpensesUsdValue > 0m ? 100m : 0m;

        var completedToday = todaysOrders.Where(IsCompleted).ToList();
        var completedYesterday = yesterdaysOrders.Where(IsCompleted).ToList();
        var ordersCompletedToday = completedToday.Count;
        var ordersCompletedYesterday = completedYesterday.Count;

        var completedDelta = ordersCompletedYesterday > 0
            ? (ordersCompletedToday - ordersCompletedYesterday) / (decimal)ordersCompletedYesterday * 100m
            : ordersCompletedToday > 0 ? 100m : 0m;

        var inventorySnapshot = db.InventoryItems.AsNoTracking().ToList();
        var lowStockAlerts = inventorySnapshot.Count(i => i.StockQuantity <= 10);

        var employeesSnapshot = db.Employees.AsNoTracking().ToList();
        var totalActiveEmployees = employeesSnapshot.Count(e => e.EmploymentStatus == "Active");

        var (attendanceTodayStartUtc, attendanceTodayEndExclusiveUtc) = AttendanceCalendar.DayRangeUtc(today);

        var attendancesSnapshot = db.EmployeeAttendances.AsNoTracking()
            .Where(a => a.WorkDate >= AttendanceCalendar.DayAnchorUtc(today.AddDays(-30)))
            .ToList();

        var clockedInCount = attendancesSnapshot.Count(a =>
            a.WorkDate >= attendanceTodayStartUtc && a.WorkDate < attendanceTodayEndExclusiveUtc && a.ClockInTime != null);

        var inventoryAlerts = inventorySnapshot
            .OrderBy(i => i.StockQuantity)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .Select(i => new AdminInventoryAlertRowDto(
                i.Name ?? string.Empty,
                $"{i.StockQuantity:0.##} {i.Unit ?? string.Empty} · status: {i.QuantityStatus}",
                i.UniqueId ?? string.Empty,
                InventoryDashboardAlertTier(i)))
            .ToList();

        var last7Days = Enumerable.Range(-6, 7).Select(i => today.AddDays(i)).ToList();
        var revenueLast7 = last7Days
            .Select(day =>
            {
                var d0 = day.Date;
                var d1 = d0.AddDays(1);
                return MoneyDashboardTotals.SumRevenueByCurrency(
                    txMoneyWindow.Where(t => t.Date >= d0 && t.Date < d1),
                    CurrencyHelper.Usd);
            })
            .ToList();

        var hourlySales = new decimal[24];
        foreach (var order in todaysOrders)
        {
            var hour = order.CreatedAt.Hour;
            hourlySales[hour] += GrandTotal(order);
        }

        var hourlyMax = hourlySales.DefaultIfEmpty().Max();

        var topDishes = orderItems
            .Where(i => todaysOrders.Any(o => o.Id == i.OrderRecordId))
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Quantity) })
            .OrderByDescending(x => x.Qty)
            .Take(5)
            .ToList();
        var maxDishQty = topDishes.Count == 0 ? 1 : topDishes.Max(d => d.Qty);
        HashSet<string> productPhotoKeys = [];
        if (topDishes.Count > 0)
        {
            var keys = topDishes.Select(d => $"product:{d.ProductId}").Distinct().ToList();
            var found = db.PublicMenuAssets.AsNoTracking()
                .Where(a => keys.Contains(a.Key) && a.Content.Length > 0)
                .Select(a => a.Key)
                .ToList();
            productPhotoKeys = found.ToHashSet();
        }

        var topDishDtos = topDishes
            .Select(d => new AdminTopDishDto(
                productNameById.TryGetValue(d.ProductId, out var n) ? n : "Unknown",
                d.ProductId,
                productPhotoKeys.Contains($"product:{d.ProductId}")
                    ? $"/api/public/menu/assets/product/{d.ProductId}"
                    : null,
                d.Qty,
                maxDishQty > 0 ? d.Qty * 100.0 / maxDishQty : 0))
            .ToList();

        var todayAttendance = attendancesSnapshot
            .Where(a => a.WorkDate >= attendanceTodayStartUtc && a.WorkDate < attendanceTodayEndExclusiveUtc)
            .GroupBy(a => a.EmployeeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.Id).First());

        var employeesById = employeesSnapshot.GroupBy(e => e.Id).ToDictionary(g => g.Key, g => g.First());
        var staffRoster = employeesSnapshot
            .Where(e => e.EmploymentStatus == "Active")
            .OrderBy(e => e.Name)
            .Select(emp =>
            {
                var inAt = todayAttendance.TryGetValue(emp.Id, out var a) && a.ClockInTime != null;
                return new AdminStaffRosterRowDto(
                    emp.Name,
                    emp.Role,
                    inAt,
                    inAt ? "Active" : "Off duty");
            })
            .ToList();

        var activities = DashboardRecentActivities.Build(
            latestOrdersForActivity,
            attendancesSnapshot,
            employeesById,
            inventorySnapshot);

        var activityDtos = activities.Select(ToActivityDto).ToList();

        AdminPayrollAlertDto? payroll = null;
        try
        {
            var salaryOverdue = FinancialTransactionService.GetSalaryOverdueState(db, DateTime.Now);
            if (salaryOverdue.ShowWarning)
            {
                payroll = new AdminPayrollAlertDto(
                    true,
                    "Salary payroll overdue",
                    salaryOverdue.Message,
                    salaryOverdue.DaysPastPayDay);
            }
        }
        catch
        {
            // Hourly payroll / attendance edge cases must not take down the whole dashboard.
        }

        var revenueUsdSub = yesterdayRevenueUsdValue <= 0m && todayRevenueUsdValue <= 0m
            ? "No USD revenue yesterday to compare. Money — entries in USD only."
            : $"{revenueUsdDelta:+0.0;-0.0;0}% vs yesterday · Money (USD).";
        var revenueFcSub = yesterdayRevenueFcValue <= 0m && todayRevenueFcValue <= 0m
            ? "No FC revenue yesterday to compare. Money — entries in FC only (not converted from USD)."
            : $"{revenueFcDelta:+0.0;-0.0;0}% vs yesterday · Money (FC).";
        var expensesSub = yesterdayExpensesUsdValue <= 0m && todayExpensesUsdValue <= 0m
            ? "No expenses yesterday to compare. All expenses in USD (FC rows use stored USD equivalent)."
            : $"{expensesUsdDelta:+0.0;-0.0;0}% vs yesterday · all in USD.";

        var completedSub = ordersCompletedYesterday == 0 && ordersCompletedToday == 0
            ? "No completions yesterday. From orders (not Money ledger)."
            : $"{completedDelta:+0.0;-0.0;0}% vs yesterday.";

        var kpi = BuildKpiCards(
            todayRevenueUsdValue,
            todayRevenueFcValue,
            todayExpensesUsdValue,
            revenueUsdSub,
            revenueFcSub,
            expensesSub,
            occupiedTables,
            totalTables,
            activeOrdersCount,
            inKitchenCount,
            ordersCompletedToday,
            completedSub,
            revenueLast7);

        var summary = new AdminDashboardSummaryDto(
            activeOrdersCount,
            pendingCashier,
            readyOrders,
            occupiedTables,
            availableTables,
            totalTables,
            todayRevenueUsdValue,
            todayRevenueFcValue,
            todayExpensesUsdValue,
            DateTime.UtcNow,
            ordersCompletedToday,
            inKitchenCount,
            lowStockAlerts,
            clockedInCount,
            totalActiveEmployees);

        return new AdminDashboardDto(
            welcome,
            "Operations command center — today's performance, inventory risk, and team status.",
            "Cloud API — Connected",
            true,
            payroll,
            summary,
            kpi,
            inventoryAlerts,
            topDishDtos,
            hourlySales.ToList(),
            hourlyMax > 0.005m ? hourlyMax : 0m,
            staffRoster,
            activityDtos);
    }

    private static IReadOnlyList<AdminKpiCardDto> BuildKpiCards(
        decimal todayUsd,
        decimal todayFc,
        decimal todayExp,
        string usdSub,
        string fcSub,
        string expSub,
        int occupied,
        int totalTables,
        int activeOrders,
        int inKitchen,
        int completedToday,
        string completedSub,
        IReadOnlyList<decimal> sparkUsd7)
    {
        var oTotal = Math.Max(totalTables, 1);
        return new AdminKpiCardDto[]
        {
            new(
                "todayUsd",
                "Today's revenue (USD)",
                CurrencyHelper.FormatAmount(todayUsd, CurrencyHelper.Usd),
                usdSub,
                "accent-blue",
                "money",
                null,
                sparkUsd7),
            new(
                "todayFc",
                "Today's revenue (FC)",
                CurrencyHelper.FormatAmount(todayFc, CurrencyHelper.CongoleseFranc),
                fcSub,
                "accent-gold",
                "money",
                null,
                null),
            new(
                "todayExp",
                "Today's expenses",
                CurrencyHelper.FormatAmount(todayExp, CurrencyHelper.Usd),
                expSub,
                "accent-red",
                "money",
                null,
                null),
            new(
                "tables",
                "Tables occupied",
                $"{occupied}/{oTotal} occupied",
                $"{activeOrders} active orders",
                "accent-cyan",
                "tables",
                null,
                null),
            new(
                "activeOrders",
                "Active orders",
                activeOrders.ToString(CultureInfo.InvariantCulture),
                $"{inKitchen} in kitchen now",
                "accent-green",
                "orders",
                null,
                null),
            new(
                "completedToday",
                "Orders completed today",
                completedToday.ToString(CultureInfo.InvariantCulture),
                completedSub,
                "accent-purple",
                "orders",
                null,
                null)
        };
    }

    private static AdminActivityCardDto ToActivityDto(ActivityItem item)
    {
        var nav = item.NavigationTarget switch
        {
            DashboardActivityNav.Orders => "orders",
            DashboardActivityNav.Attendance => "employees",
            DashboardActivityNav.Inventory => "inventory",
            DashboardActivityNav.Money => "money",
            _ => "dashboard"
        };
        var filter = item.ActivityKind switch
        {
            "Order" => item.Title,
            "Attendance" => item.Title,
            "Inventory" => item.Title,
            _ => null
        };
        return new AdminActivityCardDto(
            item.Title,
            item.KindLabel,
            item.DetailBlock,
            nav,
            filter);
    }

    private static string InventoryDashboardAlertTier(InventoryItem item)
    {
        var q = item.QuantityStatus;
        if (string.Equals(q, "Out", StringComparison.OrdinalIgnoreCase)
            || string.Equals(q, "Critical", StringComparison.OrdinalIgnoreCase))
            return "critical";
        if (string.Equals(q, "Low", StringComparison.OrdinalIgnoreCase))
            return "reorder";
        return "info";
    }
}
