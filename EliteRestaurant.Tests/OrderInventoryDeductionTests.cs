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

    [Fact]
    public void TryApplyForAdditionalItems_DeductsOnlyNewLines_NotPriorLines()
    {
        using var db = BuildDb($"inv-addon-{Guid.NewGuid():N}");
        var inv = new InventoryItem
        {
            UniqueId = "INV-FLOUR",
            Name = "Flour",
            Unit = "kg",
            StockQuantity = 100m
        };
        var product = new Product
        {
            UniqueId = "P4",
            Name = "Bread",
            Category = "Food",
            SubCategory = "Bakery",
            Price = 5m
        };
        db.InventoryItems.Add(inv);
        db.Products.Add(product);
        db.SaveChanges();
        db.ProductIngredients.Add(new ProductIngredient
        {
            ProductId = product.Id,
            InventoryItemId = inv.Id,
            Quantity = 2m
        });
        db.Employees.Add(new Employee
        {
            UniqueId = "EMP-CHEF-1",
            SignInId = "chef1",
            Name = "Chef",
            Role = "Chef",
            PinCode = "x",
            EmploymentStatus = "Active",
            JoinDate = DateTime.Today
        });
        db.SaveChanges();

        var order = new OrderRecord
        {
            UniqueId = "ORD-4",
            Status = "Ready",
            OrderOrigin = OrderOrigin.InStore,
            CreatedAt = DateTime.UtcNow
        };
        var existingLine = new OrderItem
        {
            ProductId = product.Id,
            Quantity = 1,
            InventoryDeductedAt = DateTime.UtcNow
        };
        var newLine = new OrderItem { ProductId = product.Id, Quantity = 3 };
        order.Items.Add(existingLine);
        order.Items.Add(newLine);
        db.Orders.Add(order);
        db.SaveChanges();

        var stockBefore = db.InventoryItems.AsNoTracking().Single(i => i.Id == inv.Id).StockQuantity;
        var err = OrderInventoryDeduction.TryApplyForAdditionalItems(db, order, [newLine]);
        Assert.Null(err);
        db.SaveChanges();

        var stockAfter = db.InventoryItems.AsNoTracking().Single(i => i.Id == inv.Id).StockQuantity;
        Assert.Equal(stockBefore - 6m, stockAfter);
        Assert.NotNull(newLine.InventoryDeductedAt);
    }
}
