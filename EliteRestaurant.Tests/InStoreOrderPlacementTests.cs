using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EliteRestaurant.Tests;

public class InStoreOrderPlacementTests
{
    private static AppDbContext BuildDb(string name)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        var db = new AppDbContext(opts);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public void TryPlaceNewInStoreOrder_SetsWaitingStatus()
    {
        using var db = BuildDb($"place-{Guid.NewGuid():N}");
        db.Products.Add(new Product
        {
            UniqueId = "P1",
            Name = "Item",
            Category = "Food",
            SubCategory = "Main",
            Price = 5m
        });
        db.SaveChanges();

        var order = new OrderRecord
        {
            UniqueId = "ORD-1",
            Status = OrderWorkflow.PendingCashier,
            OrderOrigin = OrderOrigin.InStore,
            CreatedAt = DateTime.UtcNow
        };
        order.Items.Add(new OrderItem { ProductId = 1, Quantity = 1 });
        db.Orders.Add(order);
        db.SaveChanges();

        Assert.Null(InStoreOrderPlacement.TryPlaceNewInStoreOrder(db, order));
        Assert.Equal(InStoreOrderPlacement.KitchenWaitingStatus, order.Status);
    }

    [Fact]
    public void RequeueOpenCheckToKitchen_FromReady_SetsWaiting()
    {
        var order = new OrderRecord { Status = "Ready" };
        InStoreOrderPlacement.RequeueOpenCheckToKitchen(order);
        Assert.Equal("Waiting", order.Status);
    }

    [Fact]
    public void BulkRelease_MovesInStorePendingCashierToWaiting()
    {
        using var db = BuildDb($"bulk-{Guid.NewGuid():N}");
        db.Products.Add(new Product
        {
            UniqueId = "P2",
            Name = "Soup",
            Category = "Food",
            SubCategory = "Main",
            Price = 8m
        });
        db.SaveChanges();

        var order = new OrderRecord
        {
            UniqueId = "ORD-2",
            Status = OrderWorkflow.PendingCashier,
            OrderOrigin = OrderOrigin.InStore,
            CreatedAt = DateTime.UtcNow
        };
        order.Items.Add(new OrderItem { ProductId = 1, Quantity = 1 });
        db.Orders.Add(order);
        db.SaveChanges();

        var released = PendingCashierBulkRelease.ReleaseLegacyInStorePendingCashier(db);
        Assert.Equal(1, released);
        db.ChangeTracker.Clear();
        Assert.Equal("Waiting", db.Orders.AsNoTracking().Single().Status);
    }
}
