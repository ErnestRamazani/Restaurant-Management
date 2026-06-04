using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EliteRestaurant.Tests;

public class OrderInventoryDeductionTests
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
    public void TryApplyForPlacedOrder_WhenAllLinesAlreadyDeducted_ReturnsNull()
    {
        using var db = BuildDb($"inv-skip-{Guid.NewGuid():N}");
        var product = new Product
        {
            UniqueId = "P1",
            Name = "Bread",
            Category = "Food",
            SubCategory = "Bakery",
            Price = 5m
        };
        db.Products.Add(product);
        db.SaveChanges();

        var order = new OrderRecord
        {
            UniqueId = "ORD-1",
            Status = OrderWorkflow.PendingCashier,
            OrderOrigin = OrderOrigin.InStore,
            CreatedAt = DateTime.UtcNow
        };
        order.Items.Add(new OrderItem
        {
            ProductId = product.Id,
            Quantity = 2,
            InventoryDeductedAt = DateTime.UtcNow
        });
        db.Orders.Add(order);
        db.SaveChanges();

        var err = OrderInventoryDeduction.TryApplyForPlacedOrder(db, order);
        Assert.Null(err);
    }

    [Fact]
    public void MarkExistingLinesAsDeducted_FlagsOnlyPriorLines()
    {
        using var db = BuildDb($"inv-mark-{Guid.NewGuid():N}");
        var product = new Product
        {
            UniqueId = "P2",
            Name = "Soup",
            Category = "Food",
            SubCategory = "Main",
            Price = 8m
        };
        db.Products.Add(product);
        db.SaveChanges();

        var order = new OrderRecord
        {
            UniqueId = "ORD-2",
            Status = "Ready",
            OrderOrigin = OrderOrigin.InStore,
            CreatedAt = DateTime.UtcNow
        };
        var existingLine = new OrderItem { ProductId = product.Id, Quantity = 1 };
        var newLine = new OrderItem { ProductId = product.Id, Quantity = 2 };
        order.Items.Add(existingLine);
        db.Orders.Add(order);
        db.SaveChanges();

        OrderInventoryDeduction.MarkExistingLinesAsDeducted(order, [newLine]);

        Assert.NotNull(existingLine.InventoryDeductedAt);
        Assert.Null(newLine.InventoryDeductedAt);
    }

    [Fact]
    public void TryApplyForPlacedOrder_AfterMarkExisting_SkipsLegacyLinesOnReRelease()
    {
        using var db = BuildDb($"inv-append-{Guid.NewGuid():N}");
        var product = new Product
        {
            UniqueId = "P3",
            Name = "Bread",
            Category = "Food",
            SubCategory = "Bakery",
            Price = 5m
        };
        db.Products.Add(product);
        db.SaveChanges();

        var order = new OrderRecord
        {
            UniqueId = "ORD-3",
            Status = "Ready",
            OrderOrigin = OrderOrigin.InStore,
            CreatedAt = DateTime.UtcNow
        };
        var legacyLine = new OrderItem { ProductId = product.Id, Quantity = 1 };
        order.Items.Add(legacyLine);
        db.Orders.Add(order);
        db.SaveChanges();

        var newLine = new OrderItem { ProductId = product.Id, Quantity = 1, InventoryDeductedAt = DateTime.UtcNow };
        OrderInventoryDeduction.MarkExistingLinesAsDeducted(order, [newLine]);
        order.Items.Add(newLine);
        order.Status = OrderWorkflow.PendingCashier;
        db.SaveChanges();

        Assert.NotNull(legacyLine.InventoryDeductedAt);
        Assert.Null(OrderInventoryDeduction.TryApplyForPlacedOrder(db, order));
    }
}
