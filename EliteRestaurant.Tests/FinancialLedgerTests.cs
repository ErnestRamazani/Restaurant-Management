using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Reporting;
using EliteRestaurant.Core.Utils;
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
            CompletedAt = DateTime.UtcNow,
            PaymentConfirmedAt = DateTime.UtcNow
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
            CompletedAt = DateTime.UtcNow,
            PaymentConfirmedAt = DateTime.UtcNow
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

    [Fact]
    public void UsdOnlyRevenue_DoesNotInflateFcFromConvertedAmountFc()
    {
        var txs = new List<MoneyTransaction>
        {
            new()
            {
                Date = DateTime.Today,
                Type = "Revenue",
                Category = "Sale",
                CurrencyCode = CurrencyHelper.Usd,
                Amount = 30m,
                AmountUsd = 30m,
                AmountFc = 67_500m
            }
        };

        Assert.Equal(30m, MoneyReportingHelpers.SumByCurrency(txs, CurrencyHelper.Usd));
        Assert.Equal(0m, MoneyReportingHelpers.SumByCurrency(txs, CurrencyHelper.CongoleseFranc));
    }

    [Fact]
    public void MixedCurrencyRevenue_CountsFcFromAmountFc()
    {
        var txs = new List<MoneyTransaction>
        {
            new()
            {
                Date = DateTime.Today,
                Type = "Revenue",
                Category = "Sale",
                CurrencyCode = MoneyReportingHelpers.MixedCurrency,
                Amount = 40m,
                AmountUsd = 40m,
                AmountFc = 90_000m
            },
            new()
            {
                Date = DateTime.Today,
                Type = "Expense",
                Category = "Sale Change",
                CurrencyCode = CurrencyHelper.CongoleseFranc,
                Amount = 10_000m,
                AmountUsd = 4.44m,
                AmountFc = 10_000m
            }
        };

        Assert.Equal(40m, MoneyReportingHelpers.SumByCurrency(
            txs.Where(t => t.Type == "Revenue"), CurrencyHelper.Usd));
        Assert.Equal(90_000m, MoneyReportingHelpers.SumByCurrency(
            txs.Where(t => t.Type == "Revenue"), CurrencyHelper.CongoleseFranc));
        Assert.Equal(10_000m, MoneyReportingHelpers.SumByCurrency(
            txs.Where(t => t.Type == "Expense"), CurrencyHelper.CongoleseFranc));
    }

    [Fact]
    public void CompletedOrder_WithFcPayment_PostsFcRevenueRow()
    {
        using var db = BuildDb($"ledger-fc-{Guid.NewGuid():N}");

        var product = new Product
        {
            UniqueId = "P-FC-1",
            Name = "Plate",
            Category = "Food",
            SubCategory = "Main",
            Price = 20m
        };
        db.Products.Add(product);
        db.SaveChanges();

        var merchGrand = OrderTotalsHelper.ComputeMerchandiseGrandUsd(20m, "None", 0m);
        var order = new OrderRecord
        {
            UniqueId = "ORD-FC-1",
            Status = "Completed",
            PaymentCurrencyCode = CurrencyHelper.CongoleseFranc,
            MerchandiseGrandTotalUsd = merchGrand,
            // Net FC tender retained (no synthetic USD leg on the sale row).
            PaymentAmountUsd = 0m,
            PaymentAmountFc = CurrencyHelper.ConvertUsdToFc(merchGrand),
            PaymentAmount = CurrencyHelper.ConvertUsdToFc(merchGrand),
            PaymentConfirmedAt = DateTime.Today.AddHours(12),
            CompletedAt = DateTime.Today.AddHours(12),
            CreatedAt = DateTime.Today.AddHours(11)
        };
        order.Items.Add(new OrderItem { ProductId = product.Id, Quantity = 1 });
        db.Orders.Add(order);
        db.SaveChanges();

        FinancialTransactionService.RecordCompletedOrderRevenue(db, order.Id);
        db.SaveChanges();

        var sale = db.Transactions.AsNoTracking()
            .Single(t => t.Type == "Revenue" && t.Category == "Sale");
        Assert.Equal(CurrencyHelper.CongoleseFranc, sale.CurrencyCode);
        Assert.Equal(CurrencyHelper.ConvertUsdToFc(merchGrand), sale.Amount);
        Assert.Equal(0m, sale.AmountUsd);
        Assert.Equal(CurrencyHelper.ConvertUsdToFc(merchGrand), sale.AmountFc);

        var txs = db.Transactions.AsNoTracking().ToList();
        Assert.Equal(0m, MoneyReportingHelpers.SumByCurrency(
            txs.Where(t => string.Equals(t.Type, "Revenue", StringComparison.OrdinalIgnoreCase)),
            CurrencyHelper.Usd));
        Assert.Equal(CurrencyHelper.ConvertUsdToFc(merchGrand), MoneyReportingHelpers.SumByCurrency(
            txs.Where(t => string.Equals(t.Type, "Revenue", StringComparison.OrdinalIgnoreCase)),
            CurrencyHelper.CongoleseFranc));
    }

    [Fact]
    public void FcOnlyLedgerRow_DoesNotAddReferenceUsdToUsdRevenueBucket()
    {
        var txs = new List<MoneyTransaction>
        {
            new()
            {
                Date = DateTime.Today,
                Type = "Revenue",
                Category = "Sale",
                CurrencyCode = CurrencyHelper.CongoleseFranc,
                Amount = 45_000m,
                AmountUsd = 20m,
                AmountFc = 45_000m
            }
        };

        Assert.Equal(0m, MoneyReportingHelpers.SumByCurrency(txs, CurrencyHelper.Usd));
        Assert.Equal(45_000m, MoneyReportingHelpers.SumByCurrency(txs, CurrencyHelper.CongoleseFranc));
    }

    [Fact]
    public void CompletedOrder_WithChange_PostsMerchandiseGrandNotNetCash()
    {
        using var db = BuildDb($"ledger-change-{Guid.NewGuid():N}");

        var product = new Product
        {
            UniqueId = "P-CHG-1",
            Name = "Steak",
            Category = "Food",
            SubCategory = "Main",
            Price = 50m
        };
        db.Products.Add(product);
        db.SaveChanges();

        var order = new OrderRecord
        {
            UniqueId = "ORD-CHG-1",
            Status = OrderWorkflow.Served,
            OrderOrigin = OrderOrigin.InStore,
            PaymentCurrencyCode = CurrencyHelper.Usd,
            MerchandiseGrandTotalUsd = 50m,
            PaymentAmountUsd = 50m,
            PaymentAmount = 50m,
            CreatedAt = DateTime.UtcNow
        };
        order.Items.Add(new OrderItem { ProductId = product.Id, Quantity = 1 });
        db.Orders.Add(order);
        db.SaveChanges();

        var merchandiseGrand = OrderTotalsHelper.ComputeTotalsWithDeliveryFee(50m, "None", 0m, 0m).GrandTotal;
        var ops = new AdminOrderOperationsService(db);
        ops.UpdateOrderStatus(order.Id, "Completed", paidUsd: merchandiseGrand + 10m, changeGivenUsd: 10m);

        db.ChangeTracker.Clear();
        var completed = db.Orders.AsNoTracking().Single(o => o.Id == order.Id);
        Assert.Equal(merchandiseGrand, completed.PaymentAmountUsd);
        Assert.Equal(merchandiseGrand, completed.MerchandiseGrandTotalUsd);

        var sale = db.Transactions.AsNoTracking()
            .Single(t => t.Type == "Revenue" && t.Category == "Sale");
        Assert.Equal(merchandiseGrand, sale.AmountUsd);
    }

    [Fact]
    public void RecordCompletedOrderRevenue_LegacyNetCashWithChange_UsesNetPlusChange()
    {
        using var db = BuildDb($"ledger-legacy-{Guid.NewGuid():N}");

        var product = new Product
        {
            UniqueId = "P-LEG-1",
            Name = "Soup",
            Category = "Food",
            SubCategory = "Main",
            Price = 50m
        };
        db.Products.Add(product);
        db.SaveChanges();

        var order = new OrderRecord
        {
            UniqueId = "ORD-LEG-1",
            Status = "Completed",
            OrderOrigin = OrderOrigin.InStore,
            PaymentCurrencyCode = CurrencyHelper.Usd,
            MerchandiseGrandTotalUsd = 0m,
            PaymentAmountUsd = 40m,
            ChangeGivenUsd = 10m,
            ExchangeRateUsed = CurrencyHelper.FcPerUsd,
            PaymentConfirmedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        order.Items.Add(new OrderItem { ProductId = product.Id, Quantity = 1 });
        db.Orders.Add(order);
        db.SaveChanges();

        FinancialTransactionService.RecordCompletedOrderRevenue(db, order.Id);
        db.SaveChanges();

        var sale = db.Transactions.AsNoTracking()
            .Single(t => t.Type == "Revenue" && t.Category == "Sale");
        Assert.Equal(50m, sale.AmountUsd);
    }

    [Fact]
    public void MixedCurrencyRevenue_LedgerShowsDualAmount()
    {
        var txs = new List<MoneyTransaction>
        {
            new()
            {
                Date = DateTime.Today,
                Type = "Revenue",
                Category = "Sale",
                CurrencyCode = MoneyReportingHelpers.MixedCurrency,
                Amount = 25m,
                AmountUsd = 25m,
                AmountFc = 56_250m,
                Justification = "Mixed pay"
            }
        };

        var snapshot = MoneyDashboardSnapshotBuilder.BuildFromTransactions(txs, "Today");
        Assert.Single(snapshot.LedgerItems);
        Assert.Contains("$ 25.00", snapshot.LedgerItems[0].AmountText, StringComparison.Ordinal);
        Assert.Contains("FC 56,250", snapshot.LedgerItems[0].AmountText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CompletedOrder_WithDeliveryFee_PostsSeparateDeliveryRevenue()
    {
        using var db = BuildDb($"ledger-delivery-{Guid.NewGuid():N}");

        var product = new Product
        {
            UniqueId = "P-DEL-1",
            Name = "Pizza",
            Category = "Food",
            SubCategory = "Main",
            Price = 100m
        };
        db.Products.Add(product);
        db.PublicMenuSettings.Add(new PublicMenuSetting { Key = "default", DeliveryFeePercent = 20m });
        db.SaveChanges();

        var merchGrand = OrderTotalsHelper.ComputeMerchandiseGrandUsd(100m, "None", 0m);
        var fullGrand = OrderTotalsHelper.ComputeTotalsWithDeliveryFee(100m, "None", 0m, 20m).GrandTotal;
        var order = new OrderRecord
        {
            UniqueId = "ORD-DEL-1",
            Status = "Completed",
            PaymentCurrencyCode = CurrencyHelper.Usd,
            MerchandiseGrandTotalUsd = merchGrand,
            DeliveryFeeUsd = 20m,
            PaymentAmountUsd = fullGrand,
            PaymentAmount = fullGrand,
            PaymentConfirmedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        order.Items.Add(new OrderItem { ProductId = product.Id, Quantity = 1, UnitPriceUsd = 100m });
        db.Orders.Add(order);
        db.SaveChanges();

        FinancialTransactionService.PostCompletedOrderLedgerEntries(db, order.Id);
        db.SaveChanges();

        var sale = db.Transactions.AsNoTracking().Single(t => t.Category == "Sale");
        var delivery = db.Transactions.AsNoTracking().Single(t => t.Category == "Delivery Fee");
        Assert.True(sale.AmountUsd > 0m);
        Assert.True(delivery.AmountUsd > 0m);
        Assert.Contains("Delivery fee (20%)", delivery.Justification);
    }

    [Fact]
    public void CompletedOrder_WithZeroPayment_PostsCompExpense()
    {
        using var db = BuildDb($"ledger-comp-{Guid.NewGuid():N}");

        var product = new Product
        {
            UniqueId = "P-COMP-1",
            Name = "Comp dish",
            Category = "Food",
            SubCategory = "Main",
            Price = 30m
        };
        db.Products.Add(product);
        db.SaveChanges();

        var order = new OrderRecord
        {
            UniqueId = "ORD-COMP-1",
            Status = "Completed",
            PaymentCurrencyCode = CurrencyHelper.Usd,
            MerchandiseGrandTotalUsd = 30m,
            PaymentAmountUsd = 0m,
            PaymentAmountFc = 0m,
            PaymentConfirmedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        order.Items.Add(new OrderItem { ProductId = product.Id, Quantity = 1, UnitPriceUsd = 30m });
        db.Orders.Add(order);
        db.SaveChanges();

        FinancialTransactionService.PostCompletedOrderLedgerEntries(db, order);
        db.SaveChanges();

        Assert.Single(db.Transactions.AsNoTracking().Where(t => t.Category == "Sale"));
        var comp = db.Transactions.AsNoTracking().Single(t => t.Category == "Comp");
        Assert.Equal("Expense", comp.Type);
        Assert.Contains("|ORDER:", comp.Justification);
    }

    [Fact]
    public void RefundCompletedOrder_PostsRefundExpenseRows()
    {
        using var db = BuildDb($"ledger-refund-{Guid.NewGuid():N}");

        var product = new Product
        {
            UniqueId = "P-REF-1",
            Name = "Refund dish",
            Category = "Food",
            SubCategory = "Main",
            Price = 40m
        };
        db.Products.Add(product);
        db.SaveChanges();

        var merchGrand = OrderTotalsHelper.ComputeMerchandiseGrandUsd(40m, "None", 0m);
        var order = new OrderRecord
        {
            UniqueId = "ORD-REF-1",
            Status = "Completed",
            PaymentCurrencyCode = CurrencyHelper.Usd,
            MerchandiseGrandTotalUsd = merchGrand,
            PaymentAmountUsd = merchGrand,
            PaymentAmount = merchGrand,
            PaymentConfirmedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        order.Items.Add(new OrderItem { ProductId = product.Id, Quantity = 1, UnitPriceUsd = 40m });
        db.Orders.Add(order);
        db.SaveChanges();

        FinancialTransactionService.PostCompletedOrderLedgerEntries(db, order);
        db.SaveChanges();

        order.RefundedAtUtc = DateTime.UtcNow;
        FinancialTransactionService.PostRefundLedgerEntries(db, order);
        db.SaveChanges();

        var refunds = db.Transactions.AsNoTracking().Where(t => t.Category == "Refund").ToList();
        Assert.NotEmpty(refunds);
        Assert.Equal(merchGrand, refunds.Sum(t => t.AmountUsd));
    }
}
