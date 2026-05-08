using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Orders;

public sealed record AdminWalkInLine(int ProductId, int Quantity);

public sealed class AdminOrderOperationsService(AppDbContext db)
{
    private readonly AppDbContext _db = db;

    public sealed record ReleasePendingResult(bool Ok, string? ErrorMessage, string? ReleasedOrderCode);

    public ReleasePendingResult TryReleasePendingToKitchen(int orderId)
    {
        var order = _db.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefault(o => o.Id == orderId && o.Status == OrderWorkflow.PendingCashier);
        if (order is null)
            return new ReleasePendingResult(false, "Order not found or already processed.", null);

        return DatabaseResilientTransaction.Execute(_db, () =>
        {
            using var tx = _db.Database.BeginTransaction();
            try
            {
                var err = OrderInventoryDeduction.TryApplyForPlacedOrder(_db, order);
                if (err is not null)
                {
                    tx.Rollback();
                    return new ReleasePendingResult(false, err, null);
                }

                order.Status = "Waiting";
                DataReconciler.ReconcileTableStatusesWithOrders(_db);
                _db.SaveChanges();
                tx.Commit();
                var code = string.IsNullOrWhiteSpace(order.UniqueId) ? $"#{order.Id:000}" : order.UniqueId;
                return new ReleasePendingResult(true, null, code);
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        });
    }

    public string? TryCancelPendingCashier(int orderId)
    {
        var order = _db.Orders.FirstOrDefault(o => o.Id == orderId && o.Status == OrderWorkflow.PendingCashier);
        if (order is null)
            return "Order not found or already processed.";

        return DatabaseResilientTransaction.Execute(_db, () =>
        {
            using var tx = _db.Database.BeginTransaction();
            try
            {
                order.Status = "Cancelled";
                DataReconciler.ReconcileTableStatusesWithOrders(_db);
                _db.SaveChanges();
                tx.Commit();
                return (string?)null;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        });
    }

    public string? TryCreateWalkInOrder(int tableId, string selectedOrderStatus, IReadOnlyList<AdminWalkInLine> lines)
    {
        var table = _db.Tables.Include(t => t.AssignedServer).SingleOrDefault(t => t.Id == tableId);
        if (table is null || table.AssignedServerId is null || table.AssignedServer is null)
            return null;

        var lineSum = lines.Sum(l =>
        {
            var price = _db.Products.AsNoTracking().Where(p => p.Id == l.ProductId).Select(p => p.Price).FirstOrDefault();
            return price * l.Quantity;
        });
        var totals = OrderTotalsHelper.ComputeTotals(lineSum, "None", 0m);
        var grandTotalUsd = totals.GrandTotal;

        var order = new OrderRecord
        {
            UniqueId = UniqueIdGenerator.NewId("ORD"),
            TableId = table.Id,
            TableCode = $"Table {table.TableNumber}",
            TableName = string.IsNullOrWhiteSpace(table.Name) ? $"Table {table.TableNumber}" : table.Name,
            ServerId = table.AssignedServerId,
            ServerName = table.AssignedServer.Name,
            Status = selectedOrderStatus,
            PaymentCurrencyCode = CurrencyHelper.Usd,
            PaymentAmount = Math.Round(grandTotalUsd, 2),
            PaymentAmountUsd = Math.Round(grandTotalUsd, 2),
            PaymentAmountFc = CurrencyHelper.ConvertUsdToFc(grandTotalUsd),
            ExchangeRateUsed = CurrencyHelper.FcPerUsd,
            CreatedAt = DateTime.Now
        };

        var activeStaff = _db.Employees
            .AsNoTracking()
            .Where(e => e.EmploymentStatus == "Active")
            .ToList();
        var productById = _db.Products
            .AsNoTracking()
            .Where(p => lines.Select(s => s.ProductId).Contains(p.Id))
            .ToDictionary(p => p.Id, p => p);

        foreach (var line in lines)
        {
            var assignee = ResolvePreparationAssignee(productById, activeStaff, line.ProductId);
            order.Items.Add(new OrderItem
            {
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                PreparedByEmployeeId = assignee.EmployeeId,
                PreparedByRole = assignee.Role,
                PreparedByName = assignee.Name
            });
        }

        return DatabaseResilientTransaction.Execute(_db, () =>
        {
            using var tx = _db.Database.BeginTransaction();
            try
            {
                var err = OrderInventoryDeduction.TryApplyForPlacedOrder(_db, order);
                if (err is not null)
                {
                    tx.Rollback();
                    return err;
                }

                _db.Orders.Add(order);
                table.Status = "Occupied";
                DataReconciler.ReconcileTableStatusesWithOrders(_db);
                _db.SaveChanges();
                tx.Commit();
                return (string?)null;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        });
    }

    /// <summary>
    /// Returns <c>null</c> when the order was advanced; empty string when the order no longer exists (silent no-op);
    /// otherwise an error message for the user.
    /// </summary>
    public string? TryAdvanceOrder(int orderId)
    {
        var order = _db.Orders.SingleOrDefault(o => o.Id == orderId);
        if (order is null)
            return string.Empty;

        if (!OrderWorkflow.CanAdminAdvanceOrderStatus(order.Status))
            return "Advance is not available for this status.";

        return DatabaseResilientTransaction.Execute(_db, () =>
        {
            using var tx = _db.Database.BeginTransaction();
            try
            {
                order.Status = order.Status switch
                {
                    "Waiting" => "In Kitchen",
                    "In Kitchen" => "Ready",
                    "Ready" => OrderWorkflow.Served,
                    _ => order.Status
                };

                DataReconciler.ReconcileTableStatusesWithOrders(_db);
                _db.SaveChanges();
                tx.Commit();
                return (string?)null;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        });
    }

    public void UpdateOrderStatus(
        int orderId,
        string status,
        string? paymentCurrencyOverride = null,
        decimal paidUsd = 0m,
        decimal paidFc = 0m,
        decimal changeGivenUsd = 0m,
        decimal changeGivenFc = 0m)
    {
        var order = _db.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .SingleOrDefault(o => o.Id == orderId);
        if (order is null)
            return;

        var previousStatus = order.Status;
        if (status == "Completed")
        {
            var lineSubtotal = order.Items.Sum(i => (i.Product?.Price ?? 0m) * i.Quantity);
            var totals = OrderTotalsHelper.ComputeTotals(lineSubtotal, order.DiscountMode, order.DiscountValue);
            var grandTotalUsd = totals.GrandTotal;
            var paidUsdRounded = Math.Round(Math.Max(0m, paidUsd), 2);
            var paidFcRounded = Math.Round(Math.Max(0m, paidFc), 2);
            var changeUsdRounded = Math.Round(Math.Max(0m, changeGivenUsd), 2);
            var changeFcRounded = Math.Round(Math.Max(0m, changeGivenFc), 2);
            var changeUsdEquivalent = Math.Round(changeUsdRounded + CurrencyHelper.ConvertFcToUsd(changeFcRounded), 2);

            var paymentCurrency = string.IsNullOrWhiteSpace(paymentCurrencyOverride)
                ? (string.IsNullOrWhiteSpace(order.PaymentCurrencyCode) ? CurrencyHelper.Usd : order.PaymentCurrencyCode)
                : paymentCurrencyOverride;

            order.PaymentCurrencyCode = paymentCurrency;
            order.ExchangeRateUsed = CurrencyHelper.FcPerUsd;
            order.PaymentAmount = paymentCurrency == CurrencyHelper.CongoleseFranc
                ? CurrencyHelper.ConvertUsdToFc(grandTotalUsd)
                : Math.Round(grandTotalUsd, 2);
            order.PaymentAmountUsd = Math.Round(grandTotalUsd, 2);
            order.PaymentAmountFc = CurrencyHelper.ConvertUsdToFc(grandTotalUsd);
            order.CustomerPaidUsd = paidUsdRounded;
            order.CustomerPaidFc = paidFcRounded;
            order.ChangeGivenUsd = changeUsdRounded;
            order.ChangeGivenFc = changeFcRounded;
            if (!string.Equals(previousStatus, "Completed", StringComparison.OrdinalIgnoreCase))
                order.CompletedAt = DateTime.Now;

            var expectedChangeUsd = Math.Max(0m,
                Math.Round((paidUsdRounded + CurrencyHelper.ConvertFcToUsd(paidFcRounded)) - grandTotalUsd, 2));
            if (Math.Abs(expectedChangeUsd - changeUsdEquivalent) > 0.02m)
                throw new InvalidOperationException("Change allocation does not match expected change amount.");
        }

        order.Status = status;
        _db.SaveChanges();

        if (status == "Completed" && previousStatus != "Completed")
        {
            FinancialTransactionService.RecordCompletedOrderRevenue(_db, order.Id);
            RecordChangeExpense(_db, order);
            _db.SaveChanges();
        }

        RefreshTableStatus(_db, order.TableId);
        _db.SaveChanges();
    }

    private static (int? EmployeeId, string Role, string Name) ResolvePreparationAssignee(
        IReadOnlyDictionary<int, Product> productById,
        IReadOnlyList<Employee> activeStaff,
        int productId)
    {
        if (!productById.TryGetValue(productId, out var product))
            return (null, "Unknown", "Unassigned");

        var isDrink = string.Equals(product.Category, "Drink", StringComparison.OrdinalIgnoreCase);
        if (isDrink)
        {
            var barman = activeStaff.FirstOrDefault(e =>
                e.Role.Equals("Barman", StringComparison.OrdinalIgnoreCase) ||
                e.Role.Equals("Bartender", StringComparison.OrdinalIgnoreCase));
            return barman is null ? (null, "Barman", "Unassigned Barman") : (barman.Id, "Barman", barman.Name);
        }

        var chef = activeStaff.FirstOrDefault(e =>
            e.Role.Equals("Chef", StringComparison.OrdinalIgnoreCase));
        return chef is null ? (null, "Chef", "Unassigned Chef") : (chef.Id, "Chef", chef.Name);
    }

    private static void RecordChangeExpense(AppDbContext db, OrderRecord order)
    {
        var usd = Math.Round(Math.Max(0m, order.ChangeGivenUsd), 2);
        var fc = Math.Round(Math.Max(0m, order.ChangeGivenFc), 2);
        if (usd <= 0m && fc <= 0m)
            return;

        var orderCode = string.IsNullOrWhiteSpace(order.UniqueId) ? $"#{order.Id:000}" : order.UniqueId;
        var usdMarker = $"| CHANGE_ORDER:{order.Id}:USD|";
        var fcMarker = $"| CHANGE_ORDER:{order.Id}:FC|";

        if (usd > 0m && !db.Transactions.Any(t => t.Justification.Contains(usdMarker)))
        {
            db.Transactions.Add(new MoneyTransaction
            {
                Amount = usd,
                AmountUsd = usd,
                AmountFc = CurrencyHelper.ConvertUsdToFc(usd),
                Date = order.CompletedAt ?? DateTime.Now,
                Type = "Expense",
                Category = "Sale Change",
                CurrencyCode = CurrencyHelper.Usd,
                ExchangeRateUsed = CurrencyHelper.FcPerUsd,
                IsFixed = false,
                Justification = $"Cash change returned for order {orderCode} (USD). {usdMarker}"
            });
        }

        if (fc > 0m && !db.Transactions.Any(t => t.Justification.Contains(fcMarker)))
        {
            db.Transactions.Add(new MoneyTransaction
            {
                Amount = fc,
                AmountUsd = CurrencyHelper.ConvertFcToUsd(fc),
                AmountFc = fc,
                Date = order.CompletedAt ?? DateTime.Now,
                Type = "Expense",
                Category = "Sale Change",
                CurrencyCode = CurrencyHelper.CongoleseFranc,
                ExchangeRateUsed = CurrencyHelper.FcPerUsd,
                IsFixed = false,
                Justification = $"Cash change returned for order {orderCode} (FC). {fcMarker}"
            });
        }
    }

    private static void RefreshTableStatus(AppDbContext db, int? tableId)
    {
        if (tableId is null)
            return;

        var table = db.Tables.SingleOrDefault(t => t.Id == tableId);
        if (table is null)
            return;

        var hasActiveOrders = db.Orders.Any(o =>
            o.TableId == tableId &&
            (o.Status == "Waiting" || o.Status == "In Kitchen" || o.Status == "Ready" ||
             o.Status == OrderWorkflow.Served ||
             o.Status == OrderWorkflow.PendingCashier));

        if (table.Status == "Maintenance")
            return;

        table.Status = hasActiveOrders ? "Occupied" : "Available";
    }
}
