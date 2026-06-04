using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Clients;

/// <summary>
/// Idempotent demo data: 15 regular clients with multi-month order history, debt, and settled revenue.
/// Skips insert when <c>CLT-DEMO-*</c> clients already exist; repairs tenant scope when rows were seeded with <c>RestaurantId = 0</c>.
/// </summary>
public static class DemoClientHistorySeed
{
    public const string ClientIdPrefix = "CLT-DEMO-";

    public enum EnsureResult
    {
        FailedPrerequisites,
        AlreadyPresent,
        RepairedTenantScope,
        Seeded
    }

    public static EnsureResult Ensure(AppDbContext db)
    {
        RestaurantTenantBootstrap.EnsureDefaultRestaurant(db);

        var restaurantId = ResolveRestaurantId(db);
        if (restaurantId is null)
            return EnsureResult.FailedPrerequisites;

        if (RepairDemoTenantScope(db, restaurantId.Value))
            return EnsureResult.RepairedTenantScope;

        if (db.RestaurantClients.IgnoreQueryFilters().AsNoTracking()
                .Any(c => c.UniqueId.StartsWith(ClientIdPrefix)))
            return EnsureResult.AlreadyPresent;

        var products = db.Products.AsNoTracking().Where(p => p.Price > 0).ToList();
        var tables = db.Tables.AsNoTracking().ToList();
        var servers = db.Employees.AsNoTracking()
            .Where(e => e.EmploymentStatus == "Active" && e.Role == "Server")
            .ToList();
        var chef = db.Employees.AsNoTracking()
            .FirstOrDefault(e => e.EmploymentStatus == "Active" && e.Role == "Chef");
        var staffId = db.Employees.AsNoTracking()
            .Where(e => e.EmploymentStatus == "Active")
            .Select(e => e.Id)
            .FirstOrDefault();

        if (products.Count < 4 || tables.Count == 0 || servers.Count == 0)
            return EnsureResult.FailedPrerequisites;

        var rng = new Random(20260520);
        var priceById = products.ToDictionary(p => p.Id, p => p.Price);
        var profiles = BuildProfiles();

        foreach (var profile in profiles)
        {
            var client = new RestaurantClient
            {
                RestaurantId = restaurantId.Value,
                UniqueId = profile.UniqueId,
                FullName = profile.FullName,
                PrimaryPhone = profile.Phone,
                Email = profile.Email,
                InternalNotes = "Demo regular · seeded dining history",
                DebtBalanceUsd = 0m,
                IsStaffClient = false,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow.AddMonths(-profile.MonthsAsClient),
                UpdatedAtUtc = DateTime.UtcNow
            };
            db.RestaurantClients.Add(client);
            db.SaveChanges();

            var ledger = new List<ClientDebtLedgerEntry>();
            var ordersCreated = 0;

            for (var i = 0; i < profile.OrderCount; i++)
            {
                var daysAgo = rng.Next(7, profile.MonthsAsClient * 30);
                var createdAt = DateTime.Now.AddDays(-daysAgo)
                    .Date.AddHours(11 + rng.Next(0, 10))
                    .AddMinutes(rng.Next(0, 59));
                var completedAt = createdAt.AddMinutes(rng.Next(35, 110));

                var table = tables[rng.Next(tables.Count)];
                var server = servers[rng.Next(servers.Count)];

                var order = new OrderRecord
                {
                    RestaurantId = restaurantId.Value,
                    UniqueId = UniqueIdGenerator.NewId("ORD"),
                    TableId = table.Id,
                    TableCode = $"Table {table.TableNumber}",
                    TableName = table.Name,
                    ServerId = server.Id,
                    ServerName = server.Name,
                    Status = "Completed",
                    OrderOrigin = OrderOrigin.InStore,
                    OrderSource = "WalkIn",
                    PaymentTiming = OrderPaymentTiming.Immediate,
                    CustomerNotes = profile.Notes[rng.Next(profile.Notes.Length)],
                    AllergyNotes = string.Empty,
                    CreatedAt = createdAt,
                    CompletedAt = completedAt,
                    RestaurantClientId = client.Id,
                    ExchangeRateUsed = 2250m,
                    PaymentCurrencyCode = CurrencyHelper.Usd
                };

                var lineCount = rng.Next(1, 5);
                for (var line = 0; line < lineCount; line++)
                {
                    var product = products[rng.Next(products.Count)];
                    order.Items.Add(new OrderItem
                    {
                        ProductId = product.Id,
                        Quantity = rng.Next(1, 3),
                        PreparedByEmployeeId = chef?.Id,
                        PreparedByRole = chef is null ? "Chef" : "Chef",
                        PreparedByName = chef?.Name ?? "Kitchen"
                    });
                }

                if (rng.NextDouble() < profile.DiscountRate)
                {
                    order.DiscountMode = "Percent";
                    order.DiscountValue = rng.Next(5, 16);
                }

                var grand = Math.Round(ComputeGrand(order, priceById), 2);
                if (grand <= 0m)
                    continue;

                var settlementRoll = rng.NextDouble();
                var forceOpenDebt = profile.OpenDebtOrderIndexes.Contains(ordersCreated);
                var forceSettledDebt = profile.SettledDebtOrderIndexes.Contains(ordersCreated);

                db.Orders.Add(order);
                db.SaveChanges();

                if (forceOpenDebt || (profile.OpenDebtRate > 0 && settlementRoll < profile.OpenDebtRate && !forceSettledDebt))
                {
                    ApplyOpenOnAccount(order, client, grand, staffId, ledger, completedAt);
                    db.SaveChanges();
                }
                else if (forceSettledDebt || (profile.SettledDebtRate > 0 && settlementRoll < profile.SettledDebtRate + profile.OpenDebtRate && !forceOpenDebt))
                {
                    ApplySettledOnAccount(order, client, grand, staffId, ledger, completedAt, rng);
                    db.SaveChanges();
                }
                else
                {
                    ApplyPaidAtCompletion(order, grand, completedAt);
                    db.SaveChanges();
                }

                ordersCreated++;
            }

            client.DebtBalanceUsd = Math.Round(
                ledger.Count > 0
                    ? ledger.OrderBy(e => e.CreatedAtUtc).Last().BalanceAfterUsd
                    : 0m,
                2);
            client.UpdatedAtUtc = DateTime.UtcNow;

            foreach (var entry in ledger.OrderBy(e => e.CreatedAtUtc))
            {
                entry.RestaurantId = restaurantId.Value;
                db.ClientDebtLedgerEntries.Add(entry);
            }

            db.SaveChanges();
        }

        return EnsureResult.Seeded;
    }

    private static int? ResolveRestaurantId(AppDbContext db) =>
        db.Restaurants.IgnoreQueryFilters().OrderBy(r => r.Id).Select(r => r.Id).FirstOrDefault() is int id && id > 0
            ? id
            : null;

    /// <summary>Demo rows created without tenant context are invisible to the API until scoped to the default restaurant.</summary>
    private static bool RepairDemoTenantScope(AppDbContext db, int restaurantId)
    {
        var demoClients = db.RestaurantClients.IgnoreQueryFilters()
            .Where(c => c.UniqueId.StartsWith(ClientIdPrefix) && c.RestaurantId != restaurantId)
            .ToList();
        if (demoClients.Count == 0)
            return false;

        var clientIds = demoClients.Select(c => c.Id).ToHashSet();
        foreach (var client in demoClients)
            client.RestaurantId = restaurantId;

        var orders = db.Orders.IgnoreQueryFilters()
            .Where(o => o.RestaurantClientId != null
                        && clientIds.Contains(o.RestaurantClientId.Value)
                        && o.RestaurantId != restaurantId)
            .ToList();
        foreach (var order in orders)
            order.RestaurantId = restaurantId;

        var ledger = db.ClientDebtLedgerEntries.IgnoreQueryFilters()
            .Where(e => clientIds.Contains(e.RestaurantClientId) && e.RestaurantId != restaurantId)
            .ToList();
        foreach (var entry in ledger)
            entry.RestaurantId = restaurantId;

        db.SaveChanges();
        return true;
    }

    private static decimal ComputeGrand(OrderRecord order, Dictionary<int, decimal> priceById)
    {
        var lineSub = order.Items.Sum(i =>
            (priceById.TryGetValue(i.ProductId, out var p) ? p : 0m) * i.Quantity);
        return OrderTotalsHelper.ComputeTotalsWithDeliveryFee(
            lineSub,
            order.DiscountMode,
            order.DiscountValue,
            order.DeliveryFeeUsd).GrandTotal;
    }

    private static void ApplyPaidAtCompletion(OrderRecord order, decimal grand, DateTime completedAt)
    {
        order.ClientSettlement = ClientSettlement.PaidAtCompletion;
        order.PaymentConfirmedAt = completedAt;
        order.PaymentAmountUsd = grand;
        order.PaymentAmount = grand;
        order.PaymentAmountFc = Math.Round(grand * order.ExchangeRateUsed, 2);
        order.CustomerPaidUsd = grand;
        order.CustomerPaidFc = 0m;
        order.ChangeGivenUsd = 0m;
        order.ChangeGivenFc = 0m;
    }

    private static void ApplyOpenOnAccount(
        OrderRecord order,
        RestaurantClient client,
        decimal grand,
        int staffId,
        List<ClientDebtLedgerEntry> ledger,
        DateTime completedAt)
    {
        order.ClientSettlement = ClientSettlement.OnAccount;
        order.AmountOnAccountUsd = grand;
        order.ClientDebtSettledUsd = 0m;
        order.PaymentConfirmedAt = null;
        order.PaymentAmountUsd = 0m;
        order.PaymentAmount = 0m;
        order.PaymentAmountFc = 0m;
        order.CustomerPaidUsd = 0m;
        order.CustomerPaidFc = 0m;

        var balance = Math.Round((ledger.Count > 0 ? ledger[^1].BalanceAfterUsd : client.DebtBalanceUsd) + grand, 2);
        ledger.Add(new ClientDebtLedgerEntry
        {
            RestaurantClientId = client.Id,
            OrderId = order.Id,
            EntryType = ClientDebtLedgerEntryType.Charge,
            AmountUsd = grand,
            BalanceAfterUsd = balance,
            Note = $"Order {order.UniqueId} on account",
            CreatedByEmployeeId = staffId > 0 ? staffId : null,
            CreatedAtUtc = completedAt.ToUniversalTime()
        });
    }

    private static void ApplySettledOnAccount(
        OrderRecord order,
        RestaurantClient client,
        decimal grand,
        int staffId,
        List<ClientDebtLedgerEntry> ledger,
        DateTime completedAt,
        Random rng)
    {
        order.ClientSettlement = ClientSettlement.OnAccount;
        order.AmountOnAccountUsd = grand;
        order.ClientDebtSettledUsd = grand;
        var paidAt = completedAt.AddDays(rng.Next(3, 21));
        order.PaymentConfirmedAt = paidAt;
        order.PaymentAmountUsd = grand;
        order.PaymentAmount = grand;
        order.PaymentAmountFc = Math.Round(grand * order.ExchangeRateUsed, 2);
        order.CustomerPaidUsd = grand;
        order.CustomerPaidFc = 0m;

        var balanceAfterCharge = Math.Round((ledger.Count > 0 ? ledger[^1].BalanceAfterUsd : 0m) + grand, 2);
        ledger.Add(new ClientDebtLedgerEntry
        {
            RestaurantClientId = client.Id,
            OrderId = order.Id,
            EntryType = ClientDebtLedgerEntryType.Charge,
            AmountUsd = grand,
            BalanceAfterUsd = balanceAfterCharge,
            Note = $"Order {order.UniqueId} on account",
            CreatedByEmployeeId = staffId > 0 ? staffId : null,
            CreatedAtUtc = completedAt.ToUniversalTime()
        });

        var balanceAfterPay = Math.Round(Math.Max(0m, balanceAfterCharge - grand), 2);
        ledger.Add(new ClientDebtLedgerEntry
        {
            RestaurantClientId = client.Id,
            OrderId = order.Id,
            EntryType = ClientDebtLedgerEntryType.Payment,
            AmountUsd = grand,
            BalanceAfterUsd = balanceAfterPay,
            Note = "Debt payment · demo seed",
            CreatedByEmployeeId = staffId > 0 ? staffId : null,
            CreatedAtUtc = paidAt.ToUniversalTime()
        });

        ledger.Add(new ClientDebtLedgerEntry
        {
            RestaurantClientId = client.Id,
            OrderId = order.Id,
            EntryType = ClientDebtLedgerEntryType.RevenueRecognized,
            AmountUsd = grand,
            BalanceAfterUsd = balanceAfterPay,
            Note = $"Revenue recognized · {order.UniqueId}",
            CreatedByEmployeeId = staffId > 0 ? staffId : null,
            CreatedAtUtc = paidAt.ToUniversalTime()
        });
    }

    private static IReadOnlyList<DemoClientProfile> BuildProfiles() =>
    [
        new("CLT-DEMO-001", "Marcus Whitfield", "55501001001", "marcus.whitfield@example.com", 20, 18, 0, 0, 0, [], [], ["Window table", "Business lunch", ""]),
        new("CLT-DEMO-002", "Elena Vasquez", "55501001002", "elena.v@example.com", 16, 14, 0.12, 0.08, 0, [14, 15], [], ["Celebrating anniversary", ""]),
        new("CLT-DEMO-003", "James Okafor", "55501001003", "j.okafor@example.com", 15, 12, 0, 0, 0, [], [], ["Regular Friday visit", ""]),
        new("CLT-DEMO-004", "Sophie Laurent", "55501001004", "s.laurent@example.com", 18, 16, 0.18, 0.05, 0, [16, 17], [], ["Prefers quiet corner", "No shellfish"]),
        new("CLT-DEMO-005", "Robert Kim", "55501001005", "robert.kim@example.com", 19, 15, 0.10, 0.15, 0.05, [18], [12, 13], [""]),
        new("CLT-DEMO-006", "Isabelle Dubois", "55501001006", "isabelle.d@example.com", 12, 10, 0.08, 0, 0, [10], [], ["Wine pairing", ""]),
        new("CLT-DEMO-007", "Thomas Nakamura", "55501001007", "t.nakamura@example.com", 14, 11, 0, 0, 0, [], [], [""]),
        new("CLT-DEMO-008", "Patricia Hughes", "55501001008", "patricia.h@example.com", 17, 14, 0, 0.25, 0, [], [8, 9, 10, 11], ["Hostess knows name", ""]),
        new("CLT-DEMO-009", "Omar Hassan", "55501001009", "omar.hassan@example.com", 11, 9, 0.10, 0.10, 0, [9], [6], [""]),
        new("CLT-DEMO-010", "Claire Morrison", "55501001010", "claire.m@example.com", 20, 17, 0.15, 0, 0, [19], [], ["Large party contact", ""]),
        new("CLT-DEMO-011", "Derek Sullivan", "55501001011", "derek.s@example.com", 9, 6, 0, 0, 0, [], [], ["New regular", ""]),
        new("CLT-DEMO-012", "Fatima Al-Rashid", "55501001012", "fatima.ar@example.com", 16, 13, 0.08, 0.12, 0, [14], [10, 11], [""]),
        new("CLT-DEMO-013", "Vincent Cole", "55501001013", "vincent.cole@example.com", 13, 11, 0.06, 0, 0, [12], [], [""]),
        new("CLT-DEMO-014", "Gabriella Rossi", "55501001014", "g.rossi@example.com", 22, 20, 0, 0.10, 0, [], [18, 19, 20], ["VIP · sommelier table", ""]),
        new("CLT-DEMO-015", "Harrison Pike", "55501001015", "h.pike@example.com", 8, 7, 0.12, 0, 0, [6, 7], [], [""])
    ];

    private sealed record DemoClientProfile(
        string UniqueId,
        string FullName,
        string Phone,
        string Email,
        int OrderCount,
        int MonthsAsClient,
        double OpenDebtRate,
        double SettledDebtRate,
        double DiscountRate,
        int[] OpenDebtOrderIndexes,
        int[] SettledDebtOrderIndexes,
        string[] Notes);
}
