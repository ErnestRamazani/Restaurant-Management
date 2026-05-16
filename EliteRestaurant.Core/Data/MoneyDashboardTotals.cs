using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Reporting;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurant.Core.Data;

/// <summary>
/// Aggregates Money ledger rows the same way as the Money screen (per-currency revenue sums, USD expense totals).
/// </summary>
public static class MoneyDashboardTotals
{
    public const string RevenueType = "Revenue";
    public const string ExpenseType = "Expense";

    public static void EnsureSaleRevenueBackfill(AppDbContext db)
    {
        FinancialTransactionService.EnsureCompletedOrderRevenues(db);
        db.SaveChanges();
    }

    public static string NormalizeCurrency(string? code)
        => string.Equals(code, CurrencyHelper.CongoleseFranc, StringComparison.OrdinalIgnoreCase)
            ? CurrencyHelper.CongoleseFranc
            : CurrencyHelper.Usd;

    public static decimal SumRevenueByCurrency(IEnumerable<MoneyTransaction> rows, string currencyCode)
    {
        var revenue = rows.Where(r => string.Equals(r.Type, RevenueType, StringComparison.OrdinalIgnoreCase));
        return MoneyReportingHelpers.SumByCurrency(revenue, currencyCode);
    }

    /// <summary>All expenses in USD: prefers AmountUsd; FC rows with zero AmountUsd convert Amount from FC.</summary>
    public static decimal SumExpensesUsd(IEnumerable<MoneyTransaction> rows)
    {
        decimal t = 0m;
        foreach (var row in rows)
        {
            if (!string.Equals(row.Type, ExpenseType, StringComparison.OrdinalIgnoreCase))
                continue;
            if (row.AmountUsd > 0m)
                t += row.AmountUsd;
            else if (string.Equals(NormalizeCurrency(row.CurrencyCode), CurrencyHelper.CongoleseFranc, StringComparison.OrdinalIgnoreCase))
                t += CurrencyHelper.ConvertFcToUsd(row.Amount);
            else
                t += row.Amount;
        }

        return t;
    }
}
