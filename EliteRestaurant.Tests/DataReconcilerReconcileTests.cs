using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EliteRestaurant.Tests;

public class DataReconcilerReconcileTests
{
    [Fact]
    public void ReconcileTableStatusesWithOrders_MarksOccupiedWhenKitchenActiveOrderExists()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"reconcile-{Guid.NewGuid():N}")
            .Options;
        using var db = new AppDbContext(options);
        db.Database.EnsureCreated();

        db.Tables.Add(new Table
        {
            Id = 1,
            UniqueId = "T1",
            TableNumber = 1,
            Name = "Patio 1",
            Capacity = 4,
            Status = "Available"
        });
        db.Orders.Add(new OrderRecord
        {
            UniqueId = "O1",
            TableId = 1,
            TableCode = "Table 1",
            TableName = "Patio 1",
            Status = "Waiting",
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();

        DataReconciler.ReconcileTableStatusesWithOrders(db);
        db.SaveChanges();

        Assert.Equal("Occupied", db.Tables.Single(t => t.Id == 1).Status);
    }

    [Fact]
    public void ReconcileTableStatusesWithOrders_LeavesAvailableWhenOnlyCompletedOrders()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"reconcile2-{Guid.NewGuid():N}")
            .Options;
        using var db = new AppDbContext(options);
        db.Database.EnsureCreated();

        db.Tables.Add(new Table
        {
            Id = 2,
            UniqueId = "T2",
            TableNumber = 2,
            Name = "Patio 2",
            Capacity = 2,
            Status = "Occupied"
        });
        db.Orders.Add(new OrderRecord
        {
            UniqueId = "O2",
            TableId = 2,
            TableCode = "Table 2",
            TableName = "Patio 2",
            Status = "Completed",
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();

        DataReconciler.ReconcileTableStatusesWithOrders(db);
        db.SaveChanges();

        Assert.Equal("Available", db.Tables.Single(t => t.Id == 2).Status);
    }

    [Fact]
    public void ReconcileTableStatusesWithOrders_SkipsMaintenanceTables()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"reconcile3-{Guid.NewGuid():N}")
            .Options;
        using var db = new AppDbContext(options);
        db.Database.EnsureCreated();

        db.Tables.Add(new Table
        {
            Id = 3,
            UniqueId = "T3",
            TableNumber = 3,
            Name = "Closed",
            Capacity = 2,
            Status = "Maintenance"
        });
        db.Orders.Add(new OrderRecord
        {
            UniqueId = "O3",
            TableId = 3,
            TableCode = "Table 3",
            TableName = "Closed",
            Status = "Waiting",
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();

        DataReconciler.ReconcileTableStatusesWithOrders(db);
        db.SaveChanges();

        Assert.Equal("Maintenance", db.Tables.Single(t => t.Id == 3).Status);
    }

    [Fact]
    public void ReconcileTableStatusesWithOrders_MarksOccupiedWhenOrderExistsOnlyInChangeTracker()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"reconcile-tracked-{Guid.NewGuid():N}")
            .Options;
        using var db = new AppDbContext(options);
        db.Database.EnsureCreated();

        db.Tables.Add(new Table
        {
            Id = 10,
            UniqueId = "T10",
            TableNumber = 10,
            Name = "Patio 10",
            Capacity = 4,
            Status = "Available"
        });
        db.SaveChanges();

        db.Orders.Add(new OrderRecord
        {
            UniqueId = "NEW",
            TableId = 10,
            TableCode = "Table 10",
            TableName = "Patio 10",
            Status = OrderWorkflow.PendingCashier,
            CreatedAt = DateTime.UtcNow
        });

        DataReconciler.ReconcileTableStatusesWithOrders(db);
        db.SaveChanges();

        Assert.Equal("Occupied", db.Tables.Single(t => t.Id == 10).Status);
    }
}
