using EliteRestaurant.Core.Clients;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EliteRestaurant.Tests;

public class ClientAccountServiceTests
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

    private static (RestaurantClient client, OrderRecord order) SeedClientAndOrder(AppDbContext db, decimal debt = 0m)
    {
        var product = new Product
        {
            UniqueId = "P1",
            Name = "Item",
            Category = "Food",
            SubCategory = "Main",
            Price = 20m
        };
        db.Products.Add(product);
        var client = new RestaurantClient
        {
            UniqueId = "CLT-1",
            FullName = "Test Client",
            DebtBalanceUsd = debt,
            IsActive = true
        };
        db.RestaurantClients.Add(client);
        db.SaveChanges();

        var order = new OrderRecord
        {
            UniqueId = "ORD-1",
            Status = OrderWorkflow.Served,
            ServerName = "Srv",
            CreatedAt = DateTime.Now
        };
        order.Items.Add(new OrderItem { ProductId = product.Id, Quantity = 1 });
        db.Orders.Add(order);
        db.SaveChanges();
        return (client, order);
    }

    [Fact]
    public void TryCompleteOrderOnAccount_RejectsWhenCapWouldBeExceeded()
    {
        using var db = BuildDb(nameof(TryCompleteOrderOnAccount_RejectsWhenCapWouldBeExceeded));
        db.PublicMenuSettings.Add(new PublicMenuSetting { Key = "default", ClientDebtCapUsd = 250m });
        var (client, order) = SeedClientAndOrder(db, debt: 240m);
        var svc = new ClientAccountService(db);
        svc.TryLinkOrderToClient(order.Id, client.Id);

        var err = svc.TryCompleteOrderOnAccount(order.Id, null);
        Assert.NotNull(err);
        Assert.Contains("limit", err, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryCompleteOrderOnAccount_SkipsLedgerRevenueUntilSettled()
    {
        using var db = BuildDb(nameof(TryCompleteOrderOnAccount_SkipsLedgerRevenueUntilSettled));
        db.PublicMenuSettings.Add(new PublicMenuSetting { Key = "default", ClientDebtCapUsd = 250m, OrderCancelPasscode = "1234" });
        var (client, order) = SeedClientAndOrder(db);
        var svc = new ClientAccountService(db);
        svc.TryLinkOrderToClient(order.Id, client.Id);

        Assert.Null(svc.TryCompleteOrderOnAccount(order.Id, null));

        var orderAfter = db.Orders.AsNoTracking().First(o => o.Id == order.Id);
        Assert.Equal("Completed", orderAfter.Status);
        Assert.True(ClientSettlement.IsOnAccount(orderAfter.ClientSettlement));
        Assert.Equal(0, db.Transactions.Count(t => t.Category == "Sale"));

        var (ok, _, applied, _) = svc.TrySettleDebt(client.Id, orderAfter.AmountOnAccountUsd, "1234", null, null);
        Assert.True(ok);
        Assert.True(applied > 0m);
    }

    [Fact]
    public void ApplyStaffDiscountIfNeeded_SetsPercentDiscount()
    {
        var order = new OrderRecord { DiscountMode = "None", DiscountValue = 0m };
        var client = new RestaurantClient
        {
            IsStaffClient = true,
            EmployeeId = 1,
            Employee = new Employee { Id = 1, StaffMealDiscountPercent = 15m }
        };

        ClientAccountService.ApplyStaffDiscountIfNeeded(order, client);

        Assert.Equal("Percent", order.DiscountMode);
        Assert.Equal(15m, order.DiscountValue);
    }

    [Fact]
    public void TrySettleDebt_RequiresPasscode()
    {
        using var db = BuildDb(nameof(TrySettleDebt_RequiresPasscode));
        db.PublicMenuSettings.Add(new PublicMenuSetting { Key = "default", OrderCancelPasscode = "secret" });
        var (client, _) = SeedClientAndOrder(db, debt: 10m);
        var svc = new ClientAccountService(db);

        var (ok, msg, _, _) = svc.TrySettleDebt(client.Id, 5m, "wrong", null, null);
        Assert.False(ok);
        Assert.NotNull(msg);
    }

    [Fact]
    public void TryCompleteOrderOnAccount_DoesNotDuplicateChargeLedger()
    {
        using var db = BuildDb(nameof(TryCompleteOrderOnAccount_DoesNotDuplicateChargeLedger));
        db.PublicMenuSettings.Add(new PublicMenuSetting { Key = "default", ClientDebtCapUsd = 250m });
        var (client, order) = SeedClientAndOrder(db);
        var svc = new ClientAccountService(db);
        svc.TryLinkOrderToClient(order.Id, client.Id);

        Assert.Null(svc.TryCompleteOrderOnAccount(order.Id, null));
        Assert.NotNull(svc.TryCompleteOrderOnAccount(order.Id, null));

        Assert.Equal(1, db.ClientDebtLedgerEntries.Count(e =>
            e.OrderId == order.Id && e.EntryType == ClientDebtLedgerEntryType.Charge));
    }

    [Fact]
    public void TrySettleDebt_DoesNotDuplicatePaymentLedger()
    {
        using var db = BuildDb(nameof(TrySettleDebt_DoesNotDuplicatePaymentLedger));
        db.PublicMenuSettings.Add(new PublicMenuSetting { Key = "default", ClientDebtCapUsd = 250m, OrderCancelPasscode = "1234" });
        var (client, order) = SeedClientAndOrder(db);
        var svc = new ClientAccountService(db);
        svc.TryLinkOrderToClient(order.Id, client.Id);
        Assert.Null(svc.TryCompleteOrderOnAccount(order.Id, null));

        var orderAfter = db.Orders.AsNoTracking().First(o => o.Id == order.Id);
        var (ok, _, _, _) = svc.TrySettleDebt(client.Id, orderAfter.AmountOnAccountUsd, "1234", null, null);
        Assert.True(ok);

        Assert.Equal(1, db.ClientDebtLedgerEntries.Count(e =>
            e.OrderId == order.Id && e.EntryType == ClientDebtLedgerEntryType.Payment));
        Assert.Equal(1, db.ClientDebtLedgerEntries.Count(e =>
            e.OrderId == order.Id && e.EntryType == ClientDebtLedgerEntryType.RevenueRecognized));

        var (ok2, msg2, _, _) = svc.TrySettleDebt(client.Id, orderAfter.AmountOnAccountUsd, "1234", null, null);
        Assert.False(ok2);
        Assert.Contains("no open debt", msg2 ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, db.ClientDebtLedgerEntries.Count(e =>
            e.OrderId == order.Id && e.EntryType == ClientDebtLedgerEntryType.Payment));
    }

    [Fact]
    public void DedupeLedgerEntriesForDisplay_CollapsesDuplicateOrderRows()
    {
        var clientId = 1;
        var orderId = 10;
        var entries = new[]
        {
            new ClientDebtLedgerEntry { Id = 1, RestaurantClientId = clientId, OrderId = orderId, EntryType = ClientDebtLedgerEntryType.Charge, AmountUsd = 20m, CreatedAtUtc = DateTime.UtcNow.AddMinutes(-2) },
            new ClientDebtLedgerEntry { Id = 2, RestaurantClientId = clientId, OrderId = orderId, EntryType = ClientDebtLedgerEntryType.Charge, AmountUsd = 20m, CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1) },
            new ClientDebtLedgerEntry { Id = 3, RestaurantClientId = clientId, OrderId = orderId, EntryType = ClientDebtLedgerEntryType.Payment, AmountUsd = 20m, CreatedAtUtc = DateTime.UtcNow }
        };

        var deduped = ClientAccountService.DedupeLedgerEntriesForDisplay(entries);

        Assert.Equal(2, deduped.Count);
        Assert.Equal(2, deduped.Single(e => e.EntryType == ClientDebtLedgerEntryType.Charge).Id);
        Assert.Equal(3, deduped.Single(e => e.EntryType == ClientDebtLedgerEntryType.Payment).Id);
    }

    [Fact]
    public void TrySettleDebt_SecondCallDoesNotDuplicateRevenueOrPaymentLedger()
    {
        using var db = BuildDb(nameof(TrySettleDebt_SecondCallDoesNotDuplicateRevenueOrPaymentLedger));
        db.PublicMenuSettings.Add(new PublicMenuSetting { Key = "default", ClientDebtCapUsd = 250m, OrderCancelPasscode = "1234" });
        var (client, order) = SeedClientAndOrder(db);
        var svc = new ClientAccountService(db);
        svc.TryLinkOrderToClient(order.Id, client.Id);
        Assert.Null(svc.TryCompleteOrderOnAccount(order.Id, null));

        var orderAfter = db.Orders.AsNoTracking().First(o => o.Id == order.Id);
        var (ok, _, _, _) = svc.TrySettleDebt(client.Id, orderAfter.AmountOnAccountUsd, "1234", null, null);
        Assert.True(ok);

        var revenueRows = db.ClientDebtLedgerEntries.Count(e =>
            e.OrderId == order.Id && e.EntryType == ClientDebtLedgerEntryType.RevenueRecognized);
        var paymentRows = db.ClientDebtLedgerEntries.Count(e =>
            e.RestaurantClientId == client.Id && e.EntryType == ClientDebtLedgerEntryType.Payment);
        Assert.Equal(1, revenueRows);
        Assert.Equal(1, paymentRows);

        var (ok2, msg2, _, _) = svc.TrySettleDebt(client.Id, orderAfter.AmountOnAccountUsd, "1234", null, null);
        Assert.False(ok2);
        Assert.Contains("no open debt", msg2 ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, db.ClientDebtLedgerEntries.Count(e =>
            e.OrderId == order.Id && e.EntryType == ClientDebtLedgerEntryType.RevenueRecognized));
        Assert.Equal(1, db.ClientDebtLedgerEntries.Count(e =>
            e.RestaurantClientId == client.Id && e.EntryType == ClientDebtLedgerEntryType.Payment));
    }

    [Fact]
    public void ComputeTotalGeneratedRevenueUsd_ExcludesOpenOnAccount_IncludesAfterSettlement()
    {
        using var db = BuildDb(nameof(ComputeTotalGeneratedRevenueUsd_ExcludesOpenOnAccount_IncludesAfterSettlement));
        db.PublicMenuSettings.Add(new PublicMenuSetting { Key = "default", ClientDebtCapUsd = 250m, OrderCancelPasscode = "1234" });
        var (client, order) = SeedClientAndOrder(db);
        var svc = new ClientAccountService(db);
        svc.TryLinkOrderToClient(order.Id, client.Id);
        Assert.Null(svc.TryCompleteOrderOnAccount(order.Id, null));

        Assert.Equal(0m, svc.ComputeTotalGeneratedRevenueUsd(client.Id));
        Assert.Equal(0m, svc.ComputeSettledRevenueUsd(client.Id));

        var orderAfter = db.Orders.AsNoTracking().First(o => o.Id == order.Id);
        var (ok, _, applied, _) = svc.TrySettleDebt(client.Id, orderAfter.AmountOnAccountUsd, "1234", null, null);
        Assert.True(ok);
        Assert.True(applied > 0m);

        Assert.Equal(orderAfter.AmountOnAccountUsd, svc.ComputeTotalGeneratedRevenueUsd(client.Id));
        Assert.Equal(orderAfter.AmountOnAccountUsd, svc.ComputeSettledRevenueUsd(client.Id));
    }
}
