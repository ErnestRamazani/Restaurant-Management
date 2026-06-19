using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurant.Core.Utils;

public static class DeliveryFeeHelper
{
    public const decimal DefaultPercent = 20m;

    public static decimal ResolvePercent(PublicMenuSetting? pricing = null)
    {
        if (pricing is not null && pricing.DeliveryFeePercent > 0m)
            return pricing.DeliveryFeePercent;

        var fromFile = SettingsManager.Load().CurrencyPricing.DeliveryFeePercent;
        return fromFile > 0m ? fromFile : DefaultPercent;
    }

    public static decimal ResolveFeeUsd(decimal merchandiseSubtotal, PublicMenuSetting? pricing = null)
    {
        var subtotal = Math.Round(Math.Max(0m, merchandiseSubtotal), 2);
        if (subtotal <= 0m)
            return 0m;

        var pct = Math.Clamp(ResolvePercent(pricing), 0m, 100m);
        return Math.Round(subtotal * pct / 100m, 2);
    }
}
