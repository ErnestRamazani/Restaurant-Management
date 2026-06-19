using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Orders;

public static class OrderPricingStampHelper
{
    public static void StampRatesIfUnset(OrderRecord order, AppDbContext? db = null)
    {
        if (order.TaxPercentApplied > 0m && order.ServicePercentApplied > 0m)
            return;

        PublicMenuSetting? row = null;
        if (db is not null)
        {
            row = db.PublicMenuSettings.AsNoTracking()
                .FirstOrDefault(s => s.Key == "default");
        }

        if (order.TaxPercentApplied <= 0m)
        {
            order.TaxPercentApplied = row?.TaxPercent > 0m
                ? row.TaxPercent
                : SettingsManager.Load().CurrencyPricing.TaxPercent;
            if (order.TaxPercentApplied <= 0m)
                order.TaxPercentApplied = OrderTotalsHelper.DefaultTaxRate * 100m;
        }

        if (order.ServicePercentApplied <= 0m)
        {
            order.ServicePercentApplied = row?.ServicePercent > 0m
                ? row.ServicePercent
                : SettingsManager.Load().CurrencyPricing.ServicePercent;
            if (order.ServicePercentApplied <= 0m)
                order.ServicePercentApplied = OrderTotalsHelper.DefaultServiceRate * 100m;
        }
    }

    public static void StampLinePrices(
        IEnumerable<OrderItem> items,
        IReadOnlyDictionary<int, decimal> productPrices)
    {
        foreach (var item in items)
        {
            if (item.UnitPriceUsd > 0m)
                continue;

            if (productPrices.TryGetValue(item.ProductId, out var price))
                item.UnitPriceUsd = Math.Round(Math.Max(0m, price), 2);
        }
    }
}
