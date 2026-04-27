using System.Globalization;

namespace EliteRestaurant.Core.Utils;

public static class OrderDiscountParser
{
    public static decimal Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0m;
        var t = text.Trim();
        if (decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out var v))
            return v;
        return decimal.TryParse(t, NumberStyles.Number, CultureInfo.CurrentCulture, out v) ? v : 0m;
    }

    public static bool ShouldApplyDiscount(string discountMode, string discountInput)
    {
        if (string.Equals(discountMode, "None", StringComparison.OrdinalIgnoreCase))
            return false;
        var raw = Parse(discountInput);
        if (string.Equals(discountMode, "Percent", StringComparison.OrdinalIgnoreCase))
            return raw > 0m && raw <= 100m;
        if (string.Equals(discountMode, "Usd", StringComparison.OrdinalIgnoreCase))
            return raw > 0m;
        return false;
    }
}
