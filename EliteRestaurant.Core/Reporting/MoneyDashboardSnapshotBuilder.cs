using System.IO;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Reporting;

public static class MoneyDashboardSnapshotBuilder
{
    private const string RevenueType = "Revenue";
    private const string ExpenseType = "Expense";

    public static MoneyDashboardSnapshotData Build(string selectedPeriod, int maxLedgerRows = 200)
    {
        LogMoneyDebug($"Build snapshot start | period={selectedPeriod}");
        var startedAt = DateTime.UtcNow;
        using var db = new AppDbContext();
        db.Database.SetCommandTimeout(5);
        FinancialTransactionService.EnsureCompletedOrderRevenues(db);
        db.SaveChanges();
        var txs = db.Transactions.AsNoTracking().ToList();
        var result = BuildFromTransactions(txs, selectedPeriod, maxLedgerRows);
        LogMoneyDebug($"Build snapshot done in {(DateTime.UtcNow - startedAt).TotalMilliseconds:N0} ms");
        return result;
    }

    /// <summary>Desktop HTTP path: same dashboard as <see cref="Build"/> without opening SQL.</summary>
    /// <param name="originFilter">Optional: <c>Online</c>, <c>InStore</c>, or null / <c>All</c> for no filter.</param>
    public static MoneyDashboardSnapshotData BuildFromTransactions(
        IReadOnlyList<MoneyTransaction> transactions,
        string selectedPeriod,
        int maxLedgerRows = 200,
        string? originFilter = null)
    {
        LogMoneyDebug($"Build snapshot (in-memory) start | period={selectedPeriod}");
        var startedAt = DateTime.UtcNow;
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        var scoped = transactions.Where(t => MatchesMoneyOriginFilter(t, originFilter)).ToList();

        var todaysTransactions = scoped
            .Where(t => t.Date >= today && t.Date < tomorrow)
            .ToList();
        LogMoneyDebug($"Loaded today transactions: {todaysTransactions.Count}");

        var todayRevenue = todaysTransactions
            .Where(t => string.Equals(t.Type, RevenueType, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var todayExpenses = todaysTransactions
            .Where(t => string.Equals(t.Type, ExpenseType, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var todayRevenueUsd = MoneyReportingHelpers.SumByCurrency(todayRevenue, CurrencyHelper.Usd);
        var todayRevenueFc = MoneyReportingHelpers.SumByCurrency(todayRevenue, CurrencyHelper.CongoleseFranc);
        var todayExpensesUsd = MoneyReportingHelpers.SumByCurrency(todayExpenses, CurrencyHelper.Usd);
        var todayExpensesFc = MoneyReportingHelpers.SumByCurrency(todayExpenses, CurrencyHelper.CongoleseFranc);

        var period = ResolvePeriodRange(scoped, today, selectedPeriod);
        var periodRows = scoped
            .Where(t => t.Date >= period.FromDate && t.Date < period.ToExclusive)
            .Select(t => new
            {
                t.Id,
                t.Date,
                t.Type,
                t.Category,
                t.Amount,
                t.CurrencyCode,
                t.Justification,
                t.IsFixed
            })
            .ToList();
        LogMoneyDebug($"Loaded period rows (full): {periodRows.Count} | range={period.FromDate:yyyy-MM-dd}->{period.ToDate:yyyy-MM-dd}");

        var periodLedgerRows = periodRows
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .Take(maxLedgerRows)
            .ToList();
        LogMoneyDebug($"Loaded period ledger rows: {periodLedgerRows.Count} | range={period.FromDate:yyyy-MM-dd}->{period.ToDate:yyyy-MM-dd}");

        var ledger = periodLedgerRows.Select(row =>
        {
            var isRevenue = string.Equals(row.Type, RevenueType, StringComparison.OrdinalIgnoreCase);
            return new MoneyLedgerRowData
            {
                Date = row.Date,
                Type = row.Type,
                Category = row.Category,
                Justification = string.IsNullOrWhiteSpace(row.Justification)
                    ? (row.IsFixed ? "Fixed scheduled transaction" : "No justification")
                    : row.Justification,
                AmountText =
                    $"{(isRevenue ? "+" : "-")}{CurrencyHelper.FormatAmount(row.Amount, MoneyReportingHelpers.NormalizeCurrencyCode(row.CurrencyCode))}",
                AmountColor = isRevenue ? "#2ECC71" : "#DC143C"
            };
        }).ToList();

        var totalRevenue = periodRows
            .Where(t => t.Type == RevenueType)
            .ToList();
        var totalExpenses = periodRows
            .Where(t => t.Type == ExpenseType)
            .ToList();

        var salesTotal = periodRows
            .Where(t => t.Type == RevenueType && t.Category == "Sale")
            .ToList();
        var deliveryFeesTotal = periodRows
            .Where(t => t.Type == RevenueType && string.Equals(t.Category, "Delivery Fee", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var tipsTotal = periodRows
            .Where(t => t.Type == RevenueType && t.Category == "Tip")
            .ToList();
        var payrollTotal = periodRows
            .Where(t => t.Type == ExpenseType && t.Category == "Salary")
            .ToList();

        var totalRevenueUsd = MoneyReportingHelpers.SumByCurrency(totalRevenue, CurrencyHelper.Usd);
        var totalRevenueFc = MoneyReportingHelpers.SumByCurrency(totalRevenue, CurrencyHelper.CongoleseFranc);
        var totalExpensesUsd = MoneyReportingHelpers.SumByCurrency(totalExpenses, CurrencyHelper.Usd);
        var totalExpensesFc = MoneyReportingHelpers.SumByCurrency(totalExpenses, CurrencyHelper.CongoleseFranc);
        var netUsd = totalRevenueUsd - totalExpensesUsd;
        var netFc = totalRevenueFc - totalExpensesFc;
        var salesUsd = MoneyReportingHelpers.SumByCurrency(salesTotal, CurrencyHelper.Usd);
        var salesFc = MoneyReportingHelpers.SumByCurrency(salesTotal, CurrencyHelper.CongoleseFranc);
        var deliveryFeesUsd = MoneyReportingHelpers.SumByCurrency(deliveryFeesTotal, CurrencyHelper.Usd);
        var deliveryFeesFc = MoneyReportingHelpers.SumByCurrency(deliveryFeesTotal, CurrencyHelper.CongoleseFranc);
        var tipsUsd = MoneyReportingHelpers.SumByCurrency(tipsTotal, CurrencyHelper.Usd);
        var tipsFc = MoneyReportingHelpers.SumByCurrency(tipsTotal, CurrencyHelper.CongoleseFranc);
        var payrollUsd = MoneyReportingHelpers.SumByCurrency(payrollTotal, CurrencyHelper.Usd);
        var payrollFc = MoneyReportingHelpers.SumByCurrency(payrollTotal, CurrencyHelper.CongoleseFranc);

        var originLabel = FormatOriginFilterLabel(originFilter);

        var snapshot = new MoneyDashboardSnapshotData
        {
            TodayRevenueText = CurrencyHelper.FormatDualCurrency(todayRevenueUsd, todayRevenueFc),
            TodayExpensesText = CurrencyHelper.FormatDualCurrency(todayExpensesUsd, todayExpensesFc),
            TodayNetProfitText = CurrencyHelper.FormatDualCurrency(
                todayRevenueUsd - todayExpensesUsd,
                todayRevenueFc - todayExpensesFc),
            TodayNetProfitColor = todayRevenueUsd - todayExpensesUsd >= 0m && todayRevenueFc - todayExpensesFc >= 0m
                ? "#2ECC71"
                : "#DC143C",
            SelectedPeriodLabel = period.Label,
            ReportStartDate = period.FromDate,
            ReportEndDate = period.ToDate,
            LedgerItems = ledger,
            TotalRevenueText = CurrencyHelper.FormatDualCurrency(totalRevenueUsd, totalRevenueFc),
            TotalExpensesText = CurrencyHelper.FormatDualCurrency(totalExpensesUsd, totalExpensesFc),
            NetProfitText = CurrencyHelper.FormatDualCurrency(netUsd, netFc),
            NetProfitColor = netUsd >= 0m && netFc >= 0m ? "#2ECC71" : "#DC143C",
            SalesSummaryText = CurrencyHelper.FormatDualCurrency(salesUsd, salesFc),
            TipsSummaryText = CurrencyHelper.FormatDualCurrency(tipsUsd, tipsFc),
            PayrollSummaryText = CurrencyHelper.FormatDualCurrency(payrollUsd, payrollFc),
            DeliveryFeesSummaryText = CurrencyHelper.FormatDualCurrency(deliveryFeesUsd, deliveryFeesFc),
            OriginFilterLabel = originLabel
        };
        LogMoneyDebug($"Build snapshot (in-memory) done in {(DateTime.UtcNow - startedAt).TotalMilliseconds:N0} ms");
        return snapshot;
    }

    private static bool MatchesMoneyOriginFilter(MoneyTransaction t, string? originFilter)
    {
        if (string.IsNullOrWhiteSpace(originFilter) || string.Equals(originFilter, "All", StringComparison.OrdinalIgnoreCase))
            return true;
        if (OrderOrigin.IsOnline(originFilter))
            return OrderOrigin.IsOnline(t.OrderOriginType);
        if (OrderOrigin.IsInStore(originFilter))
            return string.IsNullOrWhiteSpace(t.OrderOriginType) || OrderOrigin.IsInStore(t.OrderOriginType);
        return true;
    }

    private static string? FormatOriginFilterLabel(string? originFilter)
    {
        if (string.IsNullOrWhiteSpace(originFilter) || string.Equals(originFilter, "All", StringComparison.OrdinalIgnoreCase))
            return null;
        if (OrderOrigin.IsOnline(originFilter))
            return "Online orders";
        if (OrderOrigin.IsInStore(originFilter))
            return "In-store orders";
        return originFilter.Trim();
    }

    private static (DateTime FromDate, DateTime ToDate, DateTime ToExclusive, string Label) ResolvePeriodRange(
        IReadOnlyList<MoneyTransaction> transactions,
        DateTime today,
        string selectedPeriod)
        => selectedPeriod switch
        {
            "Today" => ResolveTodayRange(today),
            "Month" => ResolveMonthRange(today),
            "Year" => ResolveYearRange(today),
            "All" => ResolveAllRange(transactions, today),
            _ => ResolveWeekRange(today)
        };

    private static (DateTime FromDate, DateTime ToDate, DateTime ToExclusive, string Label) ResolvePeriodRange(
        AppDbContext db,
        DateTime today,
        string selectedPeriod)
        => selectedPeriod switch
        {
            "Today" => ResolveTodayRange(today),
            "Month" => ResolveMonthRange(today),
            "Year" => ResolveYearRange(today),
            "All" => ResolveAllRange(db, today),
            _ => ResolveWeekRange(today)
        };

    private static (DateTime FromDate, DateTime ToDate, DateTime ToExclusive, string Label) ResolveTodayRange(DateTime today)
    {
        var d = today.Date;
        return (d, d, d.AddDays(1), "Today");
    }

    private static (DateTime FromDate, DateTime ToDate, DateTime ToExclusive, string Label) ResolveWeekRange(DateTime today)
    {
        var dayOfWeek = ((int)today.DayOfWeek + 6) % 7;
        var from = today.AddDays(-dayOfWeek).Date;
        var to = today.Date;
        return (from, to, to.AddDays(1), "This Week");
    }

    private static (DateTime FromDate, DateTime ToDate, DateTime ToExclusive, string Label) ResolveMonthRange(DateTime today)
    {
        var from = new DateTime(today.Year, today.Month, 1);
        var to = today.Date;
        return (from, to, to.AddDays(1), "This Month");
    }

    private static (DateTime FromDate, DateTime ToDate, DateTime ToExclusive, string Label) ResolveYearRange(DateTime today)
    {
        var from = new DateTime(today.Year, 1, 1);
        var to = today.Date;
        return (from, to, to.AddDays(1), "This Year");
    }

    private static (DateTime FromDate, DateTime ToDate, DateTime ToExclusive, string Label) ResolveAllRange(
        IReadOnlyList<MoneyTransaction> transactions,
        DateTime today)
    {
        var from = transactions.Count == 0 ? today.Date : transactions.Min(t => t.Date).Date;
        var to = today.Date;
        return (from, to, to.AddDays(1), "All Time");
    }

    private static (DateTime FromDate, DateTime ToDate, DateTime ToExclusive, string Label) ResolveAllRange(AppDbContext db, DateTime today)
    {
        var firstDate = db.Transactions
            .AsNoTracking()
            .OrderBy(t => t.Date)
            .Select(t => (DateTime?)t.Date)
            .FirstOrDefault();

        var from = firstDate?.Date ?? today.Date;
        var to = today.Date;
        return (from, to, to.AddDays(1), "All Time");
    }

    private static void LogMoneyDebug(string message)
    {
        try
        {
            var appFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EliteRestaurantPro",
                "logs");
            Directory.CreateDirectory(appFolder);
            var path = Path.Combine(appFolder, "money-debug.log");
            File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | {message}{Environment.NewLine}");
        }
        catch
        {
            // Best-effort logging only.
        }
    }
}
