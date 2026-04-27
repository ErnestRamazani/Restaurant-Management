using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EliteRestaurant.Tests;

public class FinancialLedgerTests
{
    private static AppDbContext BuildDb(string dbName)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new AppDbContext(opts);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public void CompletedOrder_CreatesRevenueLedgerEntry()
    {
        using var db = BuildDb($"ledger-{Guid.NewGuid():N}");

        var product = new Product
        {
            UniqueId = "P-LEDGER-1",
            Name = "Test dish",
            Category = "Food",
            SubCategory = "Main",
            Price = 42.75m
        };
        db.Products.Add(product);
        db.SaveChanges();

        var order = new OrderRecord
        {
            UniqueId = "ORD001",
            Status = "Completed",
            PaymentAmountUsd = 85.50m,
            PaymentAmountFc = 0m,
            PaymentAmount = 85.50m,
            TableCode = "Table 1",
            TableName = "Patio",
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
        order.Items.Add(new OrderItem { ProductId = product.Id, Quantity = 2 });
        db.Orders.Add(order);
        db.SaveChanges();

        FinancialTransactionService.EnsureCompletedOrderRevenues(db);
        db.SaveChanges();

        var transactions = db.Transactions.AsNoTracking()
            .Where(t => t.Type == "Revenue" && t.Category == "Sale")
            .ToList();
        Assert.Single(transactions);
        Assert.Equal(85.50m, transactions[0].AmountUsd);
        Assert.Contains("ORD001", transactions[0].Justification);
    }

    [Fact]
    public void EnsureCompletedOrderRevenues_IsIdempotent()
    {
        using var db = BuildDb($"ledger-idem-{Guid.NewGuid():N}");

        var product = new Product
        {
            UniqueId = "P-LEDGER-2",
            Name = "Item",
            Category = "Food",
            SubCategory = "Main",
            Price = 50m
        };
        db.Products.Add(product);
        db.SaveChanges();

        var order = new OrderRecord
        {
            UniqueId = "ORD002",
            Status = "Completed",
            PaymentAmountUsd = 50m,
            PaymentAmount = 50m,
            TableCode = "Table 2",
            TableName = "Bar",
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
        order.Items.Add(new OrderItem { ProductId = product.Id, Quantity = 1 });
        db.Orders.Add(order);
        db.SaveChanges();

        FinancialTransactionService.EnsureCompletedOrderRevenues(db);
        FinancialTransactionService.EnsureCompletedOrderRevenues(db);
        db.SaveChanges();

        var count = db.Transactions.AsNoTracking().Count(t => t.Type == "Revenue" && t.Category == "Sale");
        Assert.Equal(1, count);
    }
}
