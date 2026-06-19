using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EliteRestaurant.Tests;

public class PayrollSupportUnitPriceTests
{
    [Fact]
    public void SumServerCompletedOrderMerchandiseUsd_UsesStampedUnitPrice()
    {
        using var db = BuildDb($"payroll-unit-{Guid.NewGuid():N}");
        var server = new Employee
        {
            UniqueId = "EMP-PAY-1",
            SignInId = "srv1",
            Name = "Server",
            Role = "Server",
            PinCode = "x",
            EmploymentStatus = "Active",
            JoinDate = DateTime.Today
        };
        db.Employees.Add(server);
        var product = new Product
        {
            UniqueId = "P-PAY-1",
            Name = "Steak",
            Category = "Food",
            SubCategory = "Main",
            Price = 100m
        };
        db.Products.Add(product);
        db.SaveChanges();

        var order = new OrderRecord
        {
            UniqueId = "ORD-PAY-1",
            Status = "Completed",
            ServerId = server.Id,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
        order.Items.Add(new OrderItem { ProductId = product.Id, Quantity = 2, UnitPriceUsd = 45m });
        db.Orders.Add(order);
        db.SaveChanges();

        var sum = PayrollSupport.SumServerCompletedOrderMerchandiseUsd(
            db,
            server.Id,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(1));

        Assert.Equal(90m, sum);
    }

    [Fact]
    public void TryRefundCompletedOrder_SetsRefundedAtAndPostsLedger()
    {
        using var db = BuildDb($"refund-op-{Guid.NewGuid():N}");
        db.PublicMenuSettings.Add(new PublicMenuSetting { Key = "default", OrderCancelPasscode = "1234" });
        var product = new Product
        {
            UniqueId = "P-REF-OP",
            Name = "Soup",
            Category = "Food",
            SubCategory = "Main",
            Price = 20m
        };
        db.Products.Add(product);
        db.SaveChanges();

        var order = new OrderRecord
        {
            UniqueId = "ORD-REF-OP",
            Status = "Completed",
            PaymentCurrencyCode = "USD",
            MerchandiseGrandTotalUsd = 20m,
            PaymentAmountUsd = 20m,
            PaymentAmount = 20m,
            PaymentConfirmedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        order.Items.Add(new OrderItem { ProductId = product.Id, Quantity = 1, UnitPriceUsd = 20m });
        db.Orders.Add(order);
        db.SaveChanges();

        FinancialTransactionService.PostCompletedOrderLedgerEntries(db, order);
        db.SaveChanges();

        var ops = new AdminOrderOperationsService(db);
        var err = ops.TryRefundCompletedOrder(order.Id, "1234");
        Assert.Null(err);

        db.ChangeTracker.Clear();
        var refreshed = db.Orders.AsNoTracking().Single(o => o.Id == order.Id);
        Assert.NotNull(refreshed.RefundedAtUtc);
        Assert.True(db.Transactions.AsNoTracking().Any(t => t.Category == "Refund"));
    }

    private static AppDbContext BuildDb(string name)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        var db = new AppDbContext(opts);
        db.Database.EnsureCreated();
        return db;
    }
}
