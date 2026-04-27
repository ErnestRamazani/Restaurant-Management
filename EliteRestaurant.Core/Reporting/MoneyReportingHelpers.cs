using EliteRestaurant.Core.Utils;

namespace EliteRestaurant.Core.Reporting;

public static class MoneyReportingHelpers
{
    public static string NormalizeCurrencyCode(string? currencyCode) =>
        string.Equals(currencyCode, CurrencyHelper.CongoleseFranc, StringComparison.OrdinalIgnoreCase)
            ? CurrencyHelper.CongoleseFranc
            : CurrencyHelper.Usd;

    public static decimal SumByCurrency<T>(IEnumerable<T> rows, string currencyCode) where T : class
    {
        decimal total = 0m;
        foreach (var row in rows)
        {
            var type = row.GetType();
            var rowCurrency = NormalizeCurrencyCode(type.GetProperty("CurrencyCode")?.GetValue(row) as string);
            if (!string.Equals(rowCurrency, currencyCode, StringComparison.OrdinalIgnoreCase))
                continue;

            var amountValue = type.GetProperty("Amount")?.GetValue(row);
            if (amountValue is decimal amount)
                total += amount;
        }

        return total;
    }
}
