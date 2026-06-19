using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Utils;

public static class OrderTotalsHelper
{
    public const decimal DefaultTaxRate = 0.07m;
    public const decimal DefaultServiceRate = 0.10m;

    public static (decimal DiscountApplied, decimal TaxableSubtotal, decimal Tax, decimal Service, decimal GrandTotal) ComputeTotals(
        decimal lineItemsSubtotal,
        string? discountMode,
        decimal discountValue) =>
        ComputeTotals(lineItemsSubtotal, discountMode, discountValue, 0m, 0m);

    public static (decimal DiscountApplied, decimal TaxableSubtotal, decimal Tax, decimal Service, decimal GrandTotal) ComputeTotals(
        decimal lineItemsSubtotal,
        string? discountMode,
        decimal discountValue,
        decimal taxPercentApplied,
        decimal servicePercentApplied,
        PublicMenuSetting? pricing = null)
    {
        var (taxRate, serviceRate, roundingSubtotal, roundingLine, roundingGrandTotal) =
            ResolveRateSettings(taxPercentApplied, servicePercentApplied, pricing);

        return ComputeTotalsWithRates(
            lineItemsSubtotal,
            discountMode,
            discountValue,
            taxRate,
            serviceRate,
            roundingSubtotal,
            roundingLine,
            roundingGrandTotal);
    }

    /// <summary>Order grand totals using stored public-menu pricing (API-safe; avoids file-based <see cref="SettingsManager"/>).</summary>
    public static (decimal DiscountApplied, decimal TaxableSubtotal, decimal Tax, decimal Service, decimal GrandTotal) ComputeTotals(
        decimal lineItemsSubtotal,
        string? discountMode,
        decimal discountValue,
        PublicMenuSetting pricing) =>
        ComputeTotals(lineItemsSubtotal, discountMode, discountValue, pricing.TaxPercent, pricing.ServicePercent, pricing);

    public static (decimal DiscountApplied, decimal TaxableSubtotal, decimal Tax, decimal Service, decimal GrandTotal) ComputeTotalsWithDeliveryFee(
        decimal lineItemsSubtotal,
        string? discountMode,
        decimal discountValue,
        decimal deliveryFeeUsd) =>
        ComputeTotalsWithDeliveryFee(lineItemsSubtotal, discountMode, discountValue, deliveryFeeUsd, 0m, 0m);

    public static (decimal DiscountApplied, decimal TaxableSubtotal, decimal Tax, decimal Service, decimal GrandTotal) ComputeTotalsWithDeliveryFee(
        decimal lineItemsSubtotal,
        string? discountMode,
        decimal discountValue,
        decimal deliveryFeeUsd,
        decimal taxPercentApplied,
        decimal servicePercentApplied,
        PublicMenuSetting? pricing = null)
    {
        var fee = Math.Round(Math.Max(0m, deliveryFeeUsd), 2);
        var subtotalWithFee = lineItemsSubtotal + fee;
        return ComputeTotals(
            subtotalWithFee,
            discountMode,
            discountValue,
            taxPercentApplied,
            servicePercentApplied,
            pricing);
    }

    /// <summary>Delivery fee variant using resolved public-menu pricing (cloud profile + file fallback).</summary>
    public static (decimal DiscountApplied, decimal TaxableSubtotal, decimal Tax, decimal Service, decimal GrandTotal) ComputeTotalsWithDeliveryFee(
        decimal lineItemsSubtotal,
        string? discountMode,
        decimal discountValue,
        decimal deliveryFeeUsd,
        PublicMenuSetting pricing) =>
        ComputeTotalsWithDeliveryFee(
            lineItemsSubtotal,
            discountMode,
            discountValue,
            deliveryFeeUsd,
            pricing.TaxPercent,
            pricing.ServicePercent,
            pricing);

    public static decimal ComputeMerchandiseGrandUsd(
        decimal lineItemsSubtotal,
        string? discountMode,
        decimal discountValue,
        decimal taxPercentApplied = 0m,
        decimal servicePercentApplied = 0m,
        PublicMenuSetting? pricing = null) =>
        ComputeTotals(
            lineItemsSubtotal,
            discountMode,
            discountValue,
            taxPercentApplied,
            servicePercentApplied,
            pricing).GrandTotal;

    public static decimal ComputeOrderGrandTotalUsd(OrderRecord order)
    {
        var lineSubtotal = order.Items.Sum(i =>
            (i.UnitPriceUsd > 0m ? i.UnitPriceUsd : i.Product?.Price ?? 0m) * i.Quantity);
        return ComputeTotalsWithDeliveryFee(
            lineSubtotal,
            order.DiscountMode,
            order.DiscountValue,
            order.DeliveryFeeUsd,
            order.TaxPercentApplied,
            order.ServicePercentApplied).GrandTotal;
    }

    private static (decimal TaxRate, decimal ServiceRate, string? RoundingSubtotal, string? RoundingLine, string? RoundingGrandTotal)
        ResolveRateSettings(decimal taxPercentApplied, decimal servicePercentApplied, PublicMenuSetting? pricing)
    {
        if (pricing is not null)
        {
            var taxRate = ResolvePercentRate(taxPercentApplied, pricing.TaxPercent, DefaultTaxRate);
            var serviceRate = ResolvePercentRate(servicePercentApplied, pricing.ServicePercent, DefaultServiceRate);
            return (taxRate, serviceRate, pricing.RoundingSubtotal, pricing.RoundingLine, pricing.RoundingGrandTotal);
        }

        var settings = SettingsManager.Load().CurrencyPricing;
        return (
            ResolvePercentRate(taxPercentApplied, settings.TaxPercent, DefaultTaxRate),
            ResolvePercentRate(servicePercentApplied, settings.ServicePercent, DefaultServiceRate),
            settings.RoundingSubtotal,
            settings.RoundingLine,
            settings.RoundingGrandTotal);
    }

    private static decimal ResolvePercentRate(decimal appliedPercent, decimal configuredPercent, decimal defaultRate)
    {
        if (appliedPercent > 0m)
            return appliedPercent / 100m;
        if (configuredPercent > 0m)
            return configuredPercent / 100m;
        return defaultRate;
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
