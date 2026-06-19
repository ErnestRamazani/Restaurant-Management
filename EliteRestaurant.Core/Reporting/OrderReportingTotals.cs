using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurant.Core.Reporting;

/// <summary>Consistent order grand totals for admin reports, CSV export, and ledger fallbacks.</summary>
public static class OrderReportingTotals
{
    public static decimal ResolveLineSubtotalUsd(
        OrderRecord order,
        IReadOnlyDictionary<int, decimal>? productPrices = null)
    {
        return order.Items.Sum(i => ResolveLineUnitPriceUsd(i, productPrices) * i.Quantity);
    }

    public static decimal ResolveLineUnitPriceUsd(
        OrderItem item,
        IReadOnlyDictionary<int, decimal>? productPrices = null)
    {
        if (item.UnitPriceUsd > 0m)
            return item.UnitPriceUsd;

        if (productPrices is not null && productPrices.TryGetValue(item.ProductId, out var mapped))
            return mapped;

        return item.Product?.Price ?? 0m;
    }

    public static decimal ResolveGrandTotalUsd(
        OrderRecord order,
        IReadOnlyDictionary<int, decimal>? productPrices = null)
    {
        if (order.Items.Count > 0)
        {
            var subtotal = ResolveLineSubtotalUsd(order, productPrices);
            return OrderTotalsHelper.ComputeTotalsWithDeliveryFee(
                subtotal,
                order.DiscountMode,
                order.DiscountValue,
                order.DeliveryFeeUsd,
                order.TaxPercentApplied,
                order.ServicePercentApplied).GrandTotal;
        }

        if (order.MerchandiseGrandTotalUsd > 0m)
        {
            return Math.Round(
                order.MerchandiseGrandTotalUsd + Math.Max(0m, order.DeliveryFeeUsd),
                2);
        }

        return 0m;
    }

    public static decimal ResolvePaymentUsd(OrderRecord order, decimal computedGrandUsd)
    {
        if (order.PaymentAmountUsd > 0m)
            return order.PaymentAmountUsd;

        if (order.PaymentAmountFc <= 0m)
            return computedGrandUsd;

        return 0m;
    }
}
