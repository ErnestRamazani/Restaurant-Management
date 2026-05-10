using EliteRestaurant.Core.Models;

namespace EliteRestaurant.Core.Utils;

public static class OrderTotalsHelper
{
    public const decimal DefaultTaxRate = 0.07m;
    public const decimal DefaultServiceRate = 0.10m;

    public static (decimal DiscountApplied, decimal TaxableSubtotal, decimal Tax, decimal Service, decimal GrandTotal) ComputeTotals(
        decimal lineItemsSubtotal,
        string? discountMode,
        decimal discountValue)
    {
        var settings = SettingsManager.Load().CurrencyPricing;
        var taxRate = settings.TaxPercent > 0m ? settings.TaxPercent / 100m : DefaultTaxRate;
        var serviceRate = settings.ServicePercent > 0m ? settings.ServicePercent / 100m : DefaultServiceRate;
        return ComputeTotalsWithRates(
            lineItemsSubtotal,
            discountMode,
            discountValue,
            taxRate,
            serviceRate,
            settings.RoundingSubtotal,
            settings.RoundingLine,
            settings.RoundingGrandTotal);
    }

    /// <summary>Order grand totals using stored public-menu pricing (API-safe; avoids file-based <see cref="SettingsManager"/>).</summary>
    public static (decimal DiscountApplied, decimal TaxableSubtotal, decimal Tax, decimal Service, decimal GrandTotal) ComputeTotals(
        decimal lineItemsSubtotal,
        string? discountMode,
        decimal discountValue,
        PublicMenuSetting pricing)
    {
        var taxRate = pricing.TaxPercent > 0m ? pricing.TaxPercent / 100m : DefaultTaxRate;
        var serviceRate = pricing.ServicePercent > 0m ? pricing.ServicePercent / 100m : DefaultServiceRate;
        return ComputeTotalsWithRates(
            lineItemsSubtotal,
            discountMode,
            discountValue,
            taxRate,
            serviceRate,
            pricing.RoundingSubtotal,
            pricing.RoundingLine,
            pricing.RoundingGrandTotal);
    }

    private static (decimal DiscountApplied, decimal TaxableSubtotal, decimal Tax, decimal Service, decimal GrandTotal) ComputeTotalsWithRates(
        decimal lineItemsSubtotal,
        string? discountMode,
        decimal discountValue,
        decimal taxRate,
        decimal serviceRate,
        string? roundingSubtotal,
        string? roundingLine,
        string? roundingGrandTotal)
    {
        var mode = discountMode?.Trim() ?? "None";
        decimal discountApplied = 0m;

        if (string.Equals(mode, "Percent", StringComparison.OrdinalIgnoreCase) && discountValue > 0m)
        {
            var pct = Math.Min(Math.Max(discountValue, 0m), 100m);
            discountApplied = Math.Round(lineItemsSubtotal * pct / 100m, 2);
        }
        else if (string.Equals(mode, "Usd", StringComparison.OrdinalIgnoreCase) && discountValue > 0m)
        {
            discountApplied = Math.Round(Math.Min(discountValue, lineItemsSubtotal), 2);
        }

        discountApplied = Math.Min(discountApplied, lineItemsSubtotal);
        var taxable = ApplyRounding(lineItemsSubtotal - discountApplied, roundingSubtotal);
        var tax = ApplyRounding(taxable * taxRate, roundingLine);
        var service = ApplyRounding(taxable * serviceRate, roundingLine);
        var grand = ApplyRounding(taxable + tax + service, roundingGrandTotal);
        return (discountApplied, taxable, tax, service, grand);
    }

    public static string FormatDiscountLabel(string? discountMode, decimal discountValue, decimal discountApplied)
    {
        if (discountApplied <= 0m)
            return string.Empty;
        if (string.Equals(discountMode, "Percent", StringComparison.OrdinalIgnoreCase))
            return $"Discount ({Math.Min(Math.Max(discountValue, 0m), 100m):0.##}%)";
        if (string.Equals(discountMode, "Usd", StringComparison.OrdinalIgnoreCase))
            return $"Discount (${discountValue:N2} USD)";
        return "Discount";
    }

    private static decimal ApplyRounding(decimal value, string? roundingMode)
    {
        var mode = roundingMode?.Trim() ?? "Nearest";
        if (string.Equals(mode, "Up", StringComparison.OrdinalIgnoreCase))
            return Math.Ceiling(value * 100m) / 100m;
        if (string.Equals(mode, "Down", StringComparison.OrdinalIgnoreCase))
            return Math.Floor(value * 100m) / 100m;
        if (string.Equals(mode, "None", StringComparison.OrdinalIgnoreCase))
            return value;
        return Math.Round(value, 2);
    }
}
