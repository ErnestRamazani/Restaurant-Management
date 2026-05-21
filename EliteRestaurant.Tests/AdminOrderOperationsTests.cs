using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EliteRestaurant.Tests;

public class AdminOrderOperationsTests
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

    private static (AppDbContext db, OrderRecord order) SeedPendingOnlineOrder(string dbName)
    {
        var db = BuildDb(dbName);
        var chef = new Employee
        {
            UniqueId = "E1",
            SignInId = "chef1",
            Name = "Chef",
            Role = "Chef",
            PinCode = "x",
            EmploymentStatus = "Active",
            JoinDate = DateTime.UtcNow
        };
        db.Employees.Add(chef);
        var product = new Product
        {
            UniqueId = "P1",
            Name = "Soup",
            Category = "Food",
            SubCategory = "Main",
            Price = 10m
        };
        db.Products.Add(product);
        db.SaveChanges();

        db.Tables.Add(new Table
        {
            UniqueId = "T1",
            TableNumber = 1,
            Name = "A1",
            Capacity = 4,
            Status = "Available",
            AssignedServerId = chef.Id
        });
        db.SaveChanges();

        var order = new OrderRecord
        {
            UniqueId = "ORD-T",
            TableId = 1,
            TableCode = "Table 1",
            TableName = "A1",
            ServerId = chef.Id,
            ServerName = chef.Name,
            Status = OrderWorkflow.PendingApproval,
            OrderOrigin = OrderOrigin.Online,
            CreatedAt = DateTime.UtcNow
        };
        order.Items.Add(new OrderItem
        {
            ProductId = product.Id,
            Quantity = 1,
            PreparedByEmployeeId = chef.Id,
            PreparedByRole = "Chef",
            PreparedByName = chef.Name
        });
        db.Orders.Add(order);
        db.SaveChanges();
        return (db, order);
    }

    [Fact]
    public void ReleaseOnlinePending_MovesToWaiting()
    {
        using var db = SeedPendingOnlineOrder($"rel-{Guid.NewGuid():N}").db;
        var order = db.Orders.Single();
        var ops = new AdminOrderOperationsService(db);
        var r = ops.TryReleasePendingToKitchen(order.Id);
        Assert.True(r.Ok);
        db.ChangeTracker.Clear();
        Assert.Equal("Waiting", db.Orders.AsNoTracking().Single(o => o.Id == order.Id).Status);
    }

    [Fact]
    public void ReleaseOnlinePending_MixedFoodAndDrink_VisibleOnKitchenAndBarQueues()
    {
        using var db = BuildDb($"mix-rel-{Guid.NewGuid():N}");
        var chef = new Employee
        {
            UniqueId = "E1",
            SignInId = "chef1",
            Name = "Chef",
            Role = "Chef",
            PinCode = "x",
            EmploymentStatus = "Active",
            JoinDate = DateTime.UtcNow
        };
        db.Employees.Add(chef);
        var food = new Product
        {
            UniqueId = "PF",
            Name = "Steak",
            Category = "Main",
            SubCategory = "Meat",
            Price = 20m
        };
        var drink = new Product
        {
            UniqueId = "PD",
            Name = "Spritz",
            Category = "Drink",
            SubCategory = "Cocktail",
            Price = 11m
        };
        db.Products.AddRange(food, drink);
        db.Tables.Add(new Table
        {
            UniqueId = "T1",
            TableNumber = 1,
            Name = "Online",
            Capacity = 4,
            Status = "Available",
            AssignedServerId = chef.Id
        });
        db.SaveChanges();

        var order = new OrderRecord
        {
            UniqueId = "ORD-MIX",
            TableId = 1,
            TableCode = "Table 1",
            TableName = "Online",
            Status = OrderWorkflow.PendingApproval,
            OrderOrigin = OrderOrigin.Online,
            CreatedAt = DateTime.UtcNow
        };
        order.Items.Add(new OrderItem
        {
            ProductId = food.Id,
            Quantity = 1,
            Product = food,
            PreparedByEmployeeId = chef.Id,
            PreparedByRole = "Chef",
            PreparedByName = chef.Name
        });
        order.Items.Add(new OrderItem
        {
            ProductId = drink.Id,
            Quantity = 2,
            Product = drink,
            PreparedByEmployeeId = chef.Id,
            PreparedByRole = "Barman",
            PreparedByName = "Bar"
        });
        db.Orders.Add(order);
        db.SaveChanges();

        var ops = new AdminOrderOperationsService(db);
        Assert.True(ops.TryReleasePendingToKitchen(order.Id).Ok);

        db.ChangeTracker.Clear();
        var released = db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Single(o => o.Id == order.Id);
        Assert.Equal("Waiting", released.Status);

        Assert.True(KitchenQueueKindFilter.OrderMatchesPortalQueue("Kitchen", released));
        Assert.True(KitchenQueueKindFilter.OrderMatchesPortalQueue("Bar", released));
        var kitchen = KitchenQueueKindFilter.FilterForPortal("Kitchen", [released]).ToList();
        var bar = KitchenQueueKindFilter.FilterForPortal("Bar", [released]).ToList();
        Assert.Single(kitchen);
        Assert.Single(bar);
        Assert.Single(KitchenOrderQueueMapper.ToQueueRow(released, "Kitchen").Items);
        Assert.Single(KitchenOrderQueueMapper.ToQueueRow(released, "Bar").Items);
    }

    [Fact]
    public void Release_Rejects_WhenPendingCashierButOnlineOrigin()
    {
        using var db = SeedPendingOnlineOrder($"mix-{Guid.NewGuid():N}").db;
        var order = db.Orders.Single();
        order.Status = OrderWorkflow.PendingCashier;
        order.OrderOrigin = OrderOrigin.Online;
        db.SaveChanges();

        var ops = new AdminOrderOperationsService(db);
        var r = ops.TryReleasePendingToKitchen(order.Id);
        Assert.False(r.Ok);
    }

    [Fact]
    public void Release_IsIdempotent_AfterKitchenQueue()
    {
        using var db = SeedPendingOnlineOrder($"idem-{Guid.NewGuid():N}").db;
        var orderId = db.Orders.Single().Id;
        var ops = new AdminOrderOperationsService(db);
        Assert.True(ops.TryReleasePendingToKitchen(orderId).Ok);

        var r2 = ops.TryReleasePendingToKitchen(orderId);
        Assert.False(r2.Ok);
        Assert.Contains("already released", r2.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
    }

    private static (AppDbContext db, int orderId, int foodItemId, int drinkItemId) SeedMixedInKitchen(string dbName)
    {
        var db = BuildDb(dbName);
        var chef = new Employee
        {
            UniqueId = "E1",
            SignInId = "chef1",
            Name = "Chef",
            Role = "Chef",
            PinCode = "x",
            EmploymentStatus = "Active",
            JoinDate = DateTime.UtcNow
        };
        db.Employees.Add(chef);
        var food = new Product
        {
            UniqueId = "PF",
            Name = "Steak",
            Category = "Main",
            SubCategory = "Meat",
            Price = 20m
        };
        var drink = new Product
        {
            UniqueId = "PD",
            Name = "Spritz",
            Category = "Drink",
            SubCategory = "Cocktail",
            Price = 11m
        };
        db.Products.AddRange(food, drink);
        db.SaveChanges();

        var order = new OrderRecord
        {
            UniqueId = "ORD-MIX-PREP",
            Status = "In Kitchen",
            OrderOrigin = OrderOrigin.Online,
            CreatedAt = DateTime.UtcNow
        };
        var foodItem = new OrderItem
        {
            ProductId = food.Id,
            Quantity = 1,
            Product = food,
            PreparedByEmployeeId = chef.Id,
            PreparedByRole = "Chef",
            PreparedByName = chef.Name
        };
        var drinkItem = new OrderItem
        {
            ProductId = drink.Id,
            Quantity = 1,
            Product = drink,
            PreparedByEmployeeId = chef.Id,
            PreparedByRole = "Barman",
            PreparedByName = "Bar"
        };
        order.Items.Add(foodItem);
        order.Items.Add(drinkItem);
        db.Orders.Add(order);
        db.SaveChanges();
        return (db, order.Id, foodItem.Id, drinkItem.Id);
    }

    [Fact]
    public void Advance_KitchenPortal_MixedOrder_OnlyStampsFood_StaysInKitchen()
    {
        var (db, orderId, foodItemId, drinkItemId) = SeedMixedInKitchen($"mix-k-{Guid.NewGuid():N}");
        using (db)
        {
        var ops = new AdminOrderOperationsService(db);

        var outcome = ops.TryAdvanceOrderWithOutcome(orderId, KitchenQueueKindFilter.PortalKitchen);
        Assert.Null(outcome.Error);
        Assert.False(outcome.BecameReady);

        db.ChangeTracker.Clear();
        var order = db.Orders.AsNoTracking().Single(o => o.Id == orderId);
        Assert.Equal("In Kitchen", order.Status);

        var items = db.OrderItems.AsNoTracking().Where(i => i.OrderRecordId == orderId).ToList();
        Assert.NotNull(items.Single(i => i.Id == foodItemId).KitchenPreparedAt);
        Assert.Null(items.Single(i => i.Id == drinkItemId).KitchenPreparedAt);
        }
    }

    [Fact]
    public void Advance_MixedOrder_ReadyOnlyAfterBothPortalsComplete()
    {
        var (db, orderId, foodItemId, drinkItemId) = SeedMixedInKitchen($"mix-both-{Guid.NewGuid():N}");
        using (db)
        {
        var ops = new AdminOrderOperationsService(db);

        Assert.Null(ops.TryAdvanceOrder(orderId, KitchenQueueKindFilter.PortalKitchen));
        db.ChangeTracker.Clear();
        Assert.Equal("In Kitchen", db.Orders.AsNoTracking().Single(o => o.Id == orderId).Status);

        var barReady = ops.TryAdvanceOrderWithOutcome(orderId, KitchenQueueKindFilter.PortalBar);
        Assert.Null(barReady.Error);
        Assert.True(barReady.BecameReady);

        db.ChangeTracker.Clear();
        Assert.Equal("Ready", db.Orders.AsNoTracking().Single(o => o.Id == orderId).Status);
        Assert.All(
            db.OrderItems.AsNoTracking().Where(i => i.OrderRecordId == orderId),
            i => Assert.NotNull(i.KitchenPreparedAt));
        }
    }

    [Fact]
    public void MarkKitchenReady_SetsFulfillment_AndSuppressesRepeatBroadcastState()
    {
        using var db = SeedPendingOnlineOrder($"rdy-{Guid.NewGuid():N}").db;
        var orderId = db.Orders.Single().Id;
        var ops = new AdminOrderOperationsService(db);
        Assert.True(ops.TryReleasePendingToKitchen(orderId).Ok);

        var fail = ops.TryMarkKitchenReadyForCashier(orderId);
        Assert.False(fail.Ok);

        db.ChangeTracker.Clear();
        var o = db.Orders.Single(x => x.Id == orderId);
        o.Status = "In Kitchen";
        o.OrderSource = "Delivery";
        db.SaveChanges();

        var ok = ops.TryMarkKitchenReadyForCashier(orderId);
        Assert.True(ok.Ok);
        Assert.False(ok.SuppressBroadcast);
        db.ChangeTracker.Clear();
        var ready = db.Orders.AsNoTracking().Single(x => x.Id == orderId);
        Assert.Equal("Ready", ready.Status);
        Assert.Equal(CustomerFulfillmentStatuses.OutForDelivery, ready.CustomerFulfillmentStatus);

        var again = ops.TryMarkKitchenReadyForCashier(orderId);
        Assert.True(again.Ok);
        Assert.True(again.SuppressBroadcast);

        db.ChangeTracker.Clear();
        var stamped = db.OrderItems.AsNoTracking().Where(i => i.OrderRecordId == orderId).ToList();
        Assert.All(stamped, i => Assert.NotNull(i.KitchenPreparedAt));
    }

    [Fact]
    public void UpdateOrderStatus_Completed_AcceptsChange_WhenDeliveryFeeIncludedInGrandTotal()
    {
        using var db = SeedPendingOnlineOrder($"chg-{Guid.NewGuid():N}").db;
        var order = db.Orders.Include(o => o.Items).ThenInclude(i => i.Product).Single();
        order.Status = "Ready";
        order.OrderSource = "Delivery";
        order.DeliveryFeeUsd = 2.60m;
        db.SaveChanges();

        var lineSubtotal = order.Items.Sum(i => (i.Product?.Price ?? 0m) * i.Quantity);
        var grandTotal = OrderTotalsHelper.ComputeTotalsWithDeliveryFee(
            lineSubtotal,
            order.DiscountMode,
            order.DiscountValue,
            order.DeliveryFeeUsd).GrandTotal;
        var paidUsd = Math.Round(grandTotal + 5m, 2);
        var changeUsd = 5m;

        var ops = new AdminOrderOperationsService(db);
        ops.UpdateOrderStatus(order.Id, "Completed", paidUsd: paidUsd, changeGivenUsd: changeUsd);

        db.ChangeTracker.Clear();
        var completed = db.Orders.AsNoTracking().Single(o => o.Id == order.Id);
        Assert.Equal("Completed", completed.Status);
        Assert.Equal(changeUsd, completed.ChangeGivenUsd);
    }
}
