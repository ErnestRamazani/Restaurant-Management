using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Utils;
using Xunit;

namespace EliteRestaurant.Tests;

public sealed class AdminOrdersViewMapperTests
{
    [Fact]
    public void MapOrder_IncludesDeliveryFee_InDisplayedTotal()
    {
        var order = new OrderRecord
        {
            UniqueId = "ORD-DEL",
            Status = "Ready",
            DeliveryFeeUsd = 2.60m,
            Items =
            [
                new OrderItem
                {
                    Quantity = 1,
                    Product = new Product { Name = "Soup", Price = 10m }
                }
            ]
        };

        var entry = AdminOrdersViewMapper.MapOrder(order, isPast: false, showAdminAdvance: false, canViewTicket: true);
        var expected = OrderTotalsHelper.ComputeTotalsWithDeliveryFee(10m, order.DiscountMode, order.DiscountValue, 2.60m).GrandTotal;

        Assert.Equal(expected, entry.Total);
        Assert.True(entry.Total > 10m, "Delivery orders should show a total above merchandise-only subtotal.");
    }

    [Theory]
    [InlineData(OrderOrigin.Online, "Delivery", "DELIVERY", "Online · Delivery")]
    [InlineData(OrderOrigin.Online, "Pickup", "TO GO", "Online · Pickup")]
    [InlineData(OrderOrigin.InStore, "WalkIn", "PLATED", "T1 · Main")]
    public void KitchenLabels_OnlineDelivery_MatchWebKds(string origin, string source, string headline, string tableCaption)
    {
        var order = new OrderRecord
        {
            OrderOrigin = origin,
            OrderSource = source,
            TableCode = "T1",
            TableName = "Main"
        };

        Assert.Equal(headline, OrderRecordUiLabels.KitchenFulfillmentHeadline(order));
        Assert.Equal(tableCaption, OrderRecordUiLabels.TableCaption(order));
    }
}
