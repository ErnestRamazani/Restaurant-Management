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
    public void TryApplyForPlacedOrderMemory_AfterMarkExisting_DeductsOnlyNewLineQty()
    {
        var inv = new InventoryItem
        {
            Id = 1,
            UniqueId = "INV-FLOUR",
            Name = "Flour",
            Unit = "kg",
            StockQuantity = 100m
        };
        var product = new Product
        {
            Id = 10,
            UniqueId = "P4",
            Name = "Bread",
            Category = "Food",
            SubCategory = "Bakery",
            Price = 5m
        };
        var ingredientRows = new List<ProductIngredient>
        {
            new() { ProductId = product.Id, InventoryItemId = inv.Id, Quantity = 2m }
        };
        var activeStaff = new List<Employee>
        {
            new()
            {
                Id = 1,
                UniqueId = "EMP-CHEF-1",
                SignInId = "chef1",
                Name = "Chef",
                Role = "Chef",
                PinCode = "x",
                EmploymentStatus = "Active",
                JoinDate = DateTime.Today
            }
        };
        var productById = new Dictionary<int, Product> { [product.Id] = product };
        var inventoryById = new Dictionary<int, InventoryItem> { [inv.Id] = inv };

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
        OrderInventoryDeduction.MarkExistingLinesAsDeducted(order, [newLine]);
        order.Items.Add(newLine);

        var err = OrderInventoryDeduction.TryApplyForPlacedOrderMemory(
            order, inventoryById, ingredientRows, activeStaff, productById);
        Assert.Null(err);
        Assert.Equal(94m, inv.StockQuantity);
        Assert.NotNull(newLine.InventoryDeductedAt);
    }

    [Fact]
    public void TryApplyForAdditionalItems_SkipsAlreadyDeductedLines()
    {
        using var db = BuildDb($"inv-addon-idem-{Guid.NewGuid():N}");
        var inv = new InventoryItem
        {
            UniqueId = "INV-FLOUR2",
            Name = "Flour",
            Unit = "kg",
            StockQuantity = 100m
        };
        db.InventoryItems.Add(inv);
        var product = new Product
        {
            UniqueId = "P5",
            Name = "Bread",
            Category = "Food",
            SubCategory = "Bakery",
            Price = 5m
        };
        db.Products.Add(product);
        db.ProductIngredients.Add(new ProductIngredient
        {
            ProductId = product.Id,
            InventoryItemId = inv.Id,
            Quantity = 2m
        });
        db.Employees.Add(new Employee
        {
            UniqueId = "EMP-CHEF-2",
            SignInId = "chef2",
            Name = "Chef",
            Role = "Chef",
            PinCode = "x",
            EmploymentStatus = "Active",
            JoinDate = DateTime.Today
        });
        db.SaveChanges();

        var order = new OrderRecord
        {
            UniqueId = "ORD-5",
            Status = "Waiting",
            OrderOrigin = OrderOrigin.InStore,
            CreatedAt = DateTime.UtcNow
        };
        var alreadyDeducted = new OrderItem
        {
            ProductId = product.Id,
            Quantity = 1,
            InventoryDeductedAt = DateTime.UtcNow
        };
        order.Items.Add(alreadyDeducted);
        db.Orders.Add(order);
        db.SaveChanges();

        var err = OrderInventoryDeduction.TryApplyForAdditionalItems(db, order, [alreadyDeducted]);
        Assert.Null(err);
        Assert.Equal(100m, inv.StockQuantity);
    }

    [Fact]
    public void AppendFlow_AfterFirstLineDeducted_SecondAppendMarksOnlyNewLine()
    {
        using var db = BuildDb($"inv-append-flow-{Guid.NewGuid():N}");
        var product = new Product
        {
            UniqueId = "P6",
            Name = "Bread",
            Category = "Food",
            SubCategory = "Bakery",
            Price = 5m
        };
        db.Products.Add(product);
        db.SaveChanges();

        var order = new OrderRecord
        {
            UniqueId = "ORD-6",
            Status = "Waiting",
            OrderOrigin = OrderOrigin.InStore,
            CreatedAt = DateTime.UtcNow
        };
        var firstLine = new OrderItem
        {
            ProductId = product.Id,
            Quantity = 1,
            InventoryDeductedAt = DateTime.UtcNow
        };
        order.Items.Add(firstLine);
        db.Orders.Add(order);
        db.SaveChanges();

        var secondLine = new OrderItem { ProductId = product.Id, Quantity = 2 };
        order.Items.Add(secondLine);
        Assert.Null(OrderInventoryAppendHelper.TryDeductNewLinesForAppend(db, order, [secondLine]));
        db.SaveChanges();

        Assert.NotNull(firstLine.InventoryDeductedAt);
        Assert.NotNull(secondLine.InventoryDeductedAt);
        Assert.Null(OrderInventoryDeduction.TryApplyForPlacedOrder(db, order));
    }

    [Fact]
    public void ShouldDeductNewLinesImmediately_WhenPendingCashierButPriorLinesDeducted()
    {
        var order = new OrderRecord
        {
            Status = OrderWorkflow.PendingCashier,
            OrderOrigin = OrderOrigin.InStore
        };
        order.Items.Add(new OrderItem
        {
            ProductId = 1,
            Quantity = 1,
            InventoryDeductedAt = DateTime.UtcNow
        });

        Assert.True(OrderInventoryAppendHelper.ShouldDeductNewLinesImmediately(order));
    }

    [Fact]
    public void ShouldNotDeductNewLinesImmediately_WhenPurePendingCashier()
    {
        var order = new OrderRecord
        {
            Status = OrderWorkflow.PendingCashier,
            OrderOrigin = OrderOrigin.InStore
        };
        order.Items.Add(new OrderItem { ProductId = 1, Quantity = 1 });

        Assert.False(OrderInventoryAppendHelper.ShouldDeductNewLinesImmediately(order));
    }

    [Fact]
    public void TryRestockCancelledOrder_RestoresStockForDeductedLines()
    {
        using var db = BuildDb($"inv-restock-{Guid.NewGuid():N}");
        var inv = new InventoryItem
        {
            UniqueId = "INV-RESTOCK",
            Name = "Flour",
            Unit = "kg",
            StockQuantity = 50m
        };
        db.InventoryItems.Add(inv);
        var product = new Product
        {
            UniqueId = "P-RESTOCK",
            Name = "Bread",
            Category = "Food",
            SubCategory = "Bakery",
            Price = 5m
        };
        db.Products.Add(product);
        db.ProductIngredients.Add(new ProductIngredient
        {
            ProductId = product.Id,
            InventoryItemId = inv.Id,
            Quantity = 2m
        });
        db.SaveChanges();

        var order = new OrderRecord
        {
            UniqueId = "ORD-RESTOCK",
            Status = "Waiting",
            OrderOrigin = OrderOrigin.InStore,
            CreatedAt = DateTime.UtcNow
        };
        var line = new OrderItem { ProductId = product.Id, Quantity = 3, InventoryDeductedAt = DateTime.UtcNow };
        order.Items.Add(line);
        db.Orders.Add(order);
        inv.StockQuantity = 44m;
        db.SaveChanges();

        Assert.Null(OrderInventoryDeduction.TryRestockCancelledOrder(db, order));
        db.SaveChanges();
        Assert.Equal(50m, inv.StockQuantity);
        Assert.Null(line.InventoryDeductedAt);
    }
}
