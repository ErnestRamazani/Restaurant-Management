using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Utils;
using Xunit;

namespace EliteRestaurant.Tests;

public class KitchenStationPrepTests
{
    private static OrderItem Line(int id, Product product, DateTime? prepared = null) => new()
    {
        Id = id,
        ProductId = product.Id,
        Product = product,
        Quantity = 1,
        KitchenPreparedAt = prepared
    };

    [Fact]
    public void MarkPortalUnpreparedLinesPrepared_KitchenOnlyTouchesFood()
    {
        var food = new Product { Id = 1, Category = "Main", Name = "Steak", Price = 1m, UniqueId = "F" };
        var drink = new Product { Id = 2, Category = "Drink", Name = "Cola", Price = 1m, UniqueId = "D" };
        var items = new List<OrderItem> { Line(10, food), Line(11, drink) };

        KitchenStationPrep.MarkPortalUnpreparedLinesPrepared(KitchenQueueKindFilter.PortalKitchen, items);

        Assert.NotNull(items[0].KitchenPreparedAt);
        Assert.Null(items[1].KitchenPreparedAt);
    }

    [Fact]
    public void AllOrderLinesPrepared_RequiresEveryLine()
    {
        var food = new Product { Id = 1, Category = "Main", Name = "Steak", Price = 1m, UniqueId = "F" };
        var drink = new Product { Id = 2, Category = "Drink", Name = "Cola", Price = 1m, UniqueId = "D" };
        var stamp = DateTime.UtcNow;
        var items = new List<OrderItem> { Line(10, food, stamp), Line(11, drink) };

        Assert.False(KitchenStationPrep.AllOrderLinesPrepared(items));
        KitchenStationPrep.MarkPortalUnpreparedLinesPrepared(KitchenQueueKindFilter.PortalBar, items);
        Assert.True(KitchenStationPrep.AllOrderLinesPrepared(items));
    }
}
