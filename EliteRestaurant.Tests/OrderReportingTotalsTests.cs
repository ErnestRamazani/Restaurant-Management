using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Reporting;
using EliteRestaurant.Core.Utils;
using Xunit;

namespace EliteRestaurant.Tests;

public class OrderReportingTotalsTests
{
    [Fact]
    public void ResolveGrandTotalUsd_IncludesDeliveryFeeAndTax()
    {
        var order = new OrderRecord
        {
            DiscountMode = "None",
            DiscountValue = 0m,
            DeliveryFeeUsd = 10m,
            TaxPercentApplied = 7m,
            ServicePercentApplied = 10m,
            Items =
            [
                new OrderItem { ProductId = 1, Quantity = 2, UnitPriceUsd = 50m }
            ]
        };

        var grand = OrderReportingTotals.ResolveGrandTotalUsd(order);
        var expected = OrderTotalsHelper.ComputeTotalsWithDeliveryFee(
            100m, "None", 0m, 10m, 7m, 10m).GrandTotal;

        Assert.Equal(expected, grand);
    }

    [Fact]
    public void ResolveLineUnitPriceUsd_PrefersStampedPrice()
    {
        var item = new OrderItem { ProductId = 1, Quantity = 1, UnitPriceUsd = 42m, Product = new Product { Price = 99m } };
        Assert.Equal(42m, OrderReportingTotals.ResolveLineUnitPriceUsd(item));
    }

    [Fact]
    public void ResolvePaymentUsd_UsesComputedGrandWhenNoTender()
    {
        var order = new OrderRecord
        {
            PaymentAmountUsd = 0m,
            PaymentAmountFc = 0m,
            DeliveryFeeUsd = 0m,
            Items = [new OrderItem { ProductId = 1, Quantity = 1, UnitPriceUsd = 25m }]
        };

        var grand = OrderReportingTotals.ResolveGrandTotalUsd(order);
        Assert.Equal(grand, OrderReportingTotals.ResolvePaymentUsd(order, grand));
    }
}
