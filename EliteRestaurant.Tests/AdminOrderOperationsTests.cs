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
    }
}
