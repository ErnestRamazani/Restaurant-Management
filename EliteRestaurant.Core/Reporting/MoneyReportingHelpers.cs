using System.Globalization;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurant.Core.Reporting;

public static class MoneyReportingHelpers
{
    public const string MixedCurrency = "MIXED";

    public static string NormalizeCurrencyCode(string? currencyCode) =>
        string.Equals(currencyCode, CurrencyHelper.CongoleseFranc, StringComparison.OrdinalIgnoreCase)
            ? CurrencyHelper.CongoleseFranc
            : CurrencyHelper.Usd;

    public static bool IsMixedCurrency(string? currencyCode) =>
        string.Equals(currencyCode, MixedCurrency, StringComparison.OrdinalIgnoreCase);

    public static decimal SumByCurrency<T>(IEnumerable<T> rows, string currencyCode) where T : class
    {
        var wantFc = string.Equals(currencyCode, CurrencyHelper.CongoleseFranc, StringComparison.OrdinalIgnoreCase);
        decimal total = 0m;
        foreach (var row in rows)
        {
            var type = row.GetType();
            var rawCurrency = type.GetProperty("CurrencyCode")?.GetValue(row) as string;
            var amountUsd = ReadDecimal(type.GetProperty("AmountUsd")?.GetValue(row));
            var amountFc = ReadDecimal(type.GetProperty("AmountFc")?.GetValue(row));
            var amount = ReadDecimal(type.GetProperty("Amount")?.GetValue(row));

            if (IsMixedCurrency(rawCurrency))
            {
                // Dual tender: each bucket uses its explicit column only (no cross-currency fallback).
                total += wantFc ? amountFc : amountUsd;
                continue;
            }

            var rowCurrency = NormalizeCurrencyCode(rawCurrency);
            if (wantFc)
            {
                if (rowCurrency == CurrencyHelper.CongoleseFranc)
                    total += amountFc > 0m ? amountFc : amount;
                continue;
            }

            // USD bucket: count USD rows and MIXED only (handled above). Do not add AmountUsd from FC-only rows.
            if (rowCurrency == CurrencyHelper.CongoleseFranc)
                continue;

            total += amountUsd > 0m ? amountUsd : amount;
        }

        return total;
    }

    public static string FormatLedgerAmount(
        decimal amount,
        decimal amountUsd,
        decimal amountFc,
        string? currencyCode,
        bool isRevenue,
        CultureInfo? culture = null)
    {
        var prefix = isRevenue ? "+" : "-";
        if (IsMixedCurrency(currencyCode) && (amountUsd > 0m || amountFc > 0m))
            return $"{prefix}{CurrencyHelper.FormatDualCurrency(amountUsd, amountFc, culture)}";

        var code = NormalizeCurrencyCode(currencyCode);
        var displayAmount = code == CurrencyHelper.CongoleseFranc
            ? (amountFc > 0m ? amountFc : amount)
            : (amountUsd > 0m ? amountUsd : amount);
        return $"{prefix}{CurrencyHelper.FormatAmount(displayAmount, code, culture)}";
    }

    private static decimal ReadDecimal(object? value) =>
        value is decimal d ? d : 0m;
}
