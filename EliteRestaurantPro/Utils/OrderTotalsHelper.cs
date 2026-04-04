namespace EliteRestaurantPro.Utils;

public static class OrderTotalsHelper
{
    public const decimal TaxRate = 0.07m;
    public const decimal ServiceRate = 0.10m;

    public static (decimal DiscountApplied, decimal TaxableSubtotal, decimal Tax, decimal Service, decimal GrandTotal) ComputeTotals(
        decimal lineItemsSubtotal,
        string? discountMode,
        decimal discountValue)
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
        var taxable = lineItemsSubtotal - discountApplied;
        var tax = Math.Round(taxable * TaxRate, 2);
        var service = Math.Round(taxable * ServiceRate, 2);
        var grand = taxable + tax + service;
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
}
