using EliteRestaurant.Core.Menu;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using Xunit;

namespace EliteRestaurant.Tests;

public class ServerOrderStationStatusTests
{
    [Fact]
    public void MixedOrder_ShowsOnPickup_WhenBarReadyButFoodStillCooking()
    {
        var food = new Product { Category = "Main", SubCategory = "Meat", Price = 10m };
        var drink = new Product { Category = "Drink", SubCategory = "Soft", Price = 5m };
        var order = new OrderRecord
        {
            Status = "In Kitchen",
            Items =
            [
                new OrderItem { Product = food, ProductId = 1, Quantity = 1 },
                new OrderItem { Product = drink, ProductId = 2, Quantity = 1, KitchenPreparedAt = DateTime.UtcNow }
            ]
        };

        var state = ServerOrderStationStatus.Compute(order);
        Assert.True(state.BarPrepReady);
        Assert.False(state.FoodPrepReady);
        Assert.True(state.ShowOnServerPickup);
        Assert.True(state.CanServeBarStation);
        Assert.False(state.CanServeFoodStation);
    }

    [Fact]
    public void MarkStationServed_OnlyStampsPortalLines()
    {
        var food = new Product { Category = "Main", SubCategory = "Meat", Price = 10m };
        var drink = new Product { Category = "Drink", SubCategory = "Soft", Price = 5m };
        var foodItem = new OrderItem { Product = food, ProductId = 1, Quantity = 1, KitchenPreparedAt = DateTime.UtcNow };
        var drinkItem = new OrderItem { Product = drink, ProductId = 2, Quantity = 1, KitchenPreparedAt = DateTime.UtcNow };

        ServerOrderStationStatus.MarkStationServed([foodItem, drinkItem], KitchenQueueKindFilter.PortalBar, taxonomy: null);

        Assert.Null(foodItem.ServerServedAt);
        Assert.NotNull(drinkItem.ServerServedAt);
    }
}
