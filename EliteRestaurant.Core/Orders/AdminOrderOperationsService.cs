using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Menu;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Reporting;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Orders;

public sealed record AdminWalkInLine(int ProductId, int Quantity);

public sealed class AdminOrderOperationsService(AppDbContext db)
{
    private readonly AppDbContext _db = db;

    public sealed record ReleasePendingResult(bool Ok, string? ErrorMessage, string? ReleasedOrderCode);

    /// <param name="SuppressBroadcast">True when no new transition happened (already ready, or concurrent advance won).</param>
    public sealed record KitchenReadyResult(bool Ok, string? ErrorMessage, bool SuppressBroadcast, OrderReadyNotification? Notification);

    public ReleasePendingResult TryReleasePendingToKitchen(int orderId)
    {
        var order = _db.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefault(o => o.Id == orderId);
        if (order is null)
            return new ReleasePendingResult(false, "Order not found.", null);

        if (OrderWorkflow.IsKitchenQueueStatus(order.Status))
            return new ReleasePendingResult(false, "This order was already released to the kitchen.", null);

        var releaseAllowed =
            (OrderWorkflow.IsPendingCashier(order.Status) && OrderOrigin.IsInStore(order.OrderOrigin))
            || (OrderWorkflow.IsPendingApproval(order.Status) && OrderOrigin.IsOnline(order.OrderOrigin));

        if (!releaseAllowed)
            return new ReleasePendingResult(false, "Order not found or not awaiting release.", null);

        return DatabaseResilientTransaction.Execute(_db, () =>
        {
            if (IsInMemoryDatabase(_db))
            {
                var err = OrderInventoryDeduction.TryApplyForPlacedOrder(_db, order);
                if (err is not null)
                    return new ReleasePendingResult(false, err, null);

                order.Status = "Waiting";
                DataReconciler.ReconcileTableStatusesWithOrders(_db);
                _db.SaveChanges();
                var codeInMem = string.IsNullOrWhiteSpace(order.UniqueId) ? $"#{order.Id:000}" : order.UniqueId;
                return new ReleasePendingResult(true, null, codeInMem);
            }

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
        var order = _db.Orders.FirstOrDefault(o => o.Id == orderId);
        if (order is null)
            return "Order not found or already processed.";

        var cancelAllowed =
            (OrderWorkflow.IsPendingCashier(order.Status) && OrderOrigin.IsInStore(order.OrderOrigin))
            || (OrderWorkflow.IsPendingApproval(order.Status) && OrderOrigin.IsOnline(order.OrderOrigin));

        if (!cancelAllowed)
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
            OrderOrigin = OrderOrigin.InStore,
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

    /// <summary>Kitchen marks <c>In Kitchen</c> → <c>Ready</c>. Idempotent: no broadcast if already <c>Ready</c> or a concurrent request won the transition.</summary>
    public KitchenReadyResult TryMarkKitchenReadyForCashier(int orderId, string? prepStationPortal = null) =>
        TryMarkKitchenReadyForCashierCore(orderId, prepStationPortal);

    private KitchenReadyResult TryMarkKitchenReadyForCashierCore(int orderId, string? prepStationPortal)
    {
        var order = _db.Orders.AsNoTracking().SingleOrDefault(o => o.Id == orderId);
        if (order is null)
            return new KitchenReadyResult(false, "Order not found.", true, null);

        if (string.Equals(order.Status, "Ready", StringComparison.OrdinalIgnoreCase))
            return new KitchenReadyResult(true, null, true, null);

        if (!string.Equals(order.Status, "In Kitchen", StringComparison.OrdinalIgnoreCase))
            return new KitchenReadyResult(false, "Only orders being prepared can be marked ready.", true, null);

        return TryMarkPrepStationReady(orderId, prepStationPortal);
    }

    /// <summary>
    /// Returns <c>null</c> when the order was advanced; empty string when the order no longer exists (silent no-op);
    /// otherwise an error message for the user.
    /// </summary>
    public string? TryAdvanceOrder(int orderId, string? prepStationPortal = null)
    {
        var o = TryAdvanceOrderWithOutcome(orderId, prepStationPortal);
        if (o.Missing)
            return string.Empty;
        if (o.Error is not null)
            return o.Error;
        return null;
    }

    /// <summary>Same semantics as <see cref="TryAdvanceOrder"/> with kitchen→ready payload for SignalR.</summary>
    public AdvanceOrderOutcome TryAdvanceOrderWithOutcome(int orderId, string? prepStationPortal = null)
    {
        var order = _db.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .SingleOrDefault(o => o.Id == orderId);
        if (order is null)
            return new AdvanceOrderOutcome(true, string.Empty, false, null);

        if (!OrderWorkflow.CanAdminAdvanceOrderStatus(order.Status))
            return new AdvanceOrderOutcome(false, "Advance is not available for this status.", false, null);

        if (string.Equals(order.Status, "In Kitchen", StringComparison.OrdinalIgnoreCase))
        {
            var ready = TryMarkPrepStationReady(orderId, prepStationPortal);
            if (!ready.Ok)
                return new AdvanceOrderOutcome(false, ready.ErrorMessage, false, null);
            return new AdvanceOrderOutcome(
                false,
                null,
                !ready.SuppressBroadcast && ready.Notification is not null,
                ready.SuppressBroadcast ? null : ready.Notification);
        }

        if (KitchenStationPrep.AppliesStationScope(prepStationPortal))
        {
            var items = order.Items?.ToList() ?? [];
            if (KitchenStationPrep.GetPortalLines(prepStationPortal, items).Count == 0)
                return new AdvanceOrderOutcome(false, "This ticket has no items for your station.", false, null);
        }

        return DatabaseResilientTransaction.Execute(_db, () =>
        {
            using var tx = _db.Database.BeginTransaction();
            try
            {
                var tracked = _db.Orders.Single(o => o.Id == orderId);
                tracked.Status = tracked.Status switch
                {
                    "Waiting" => "In Kitchen",
                    "Ready" => OrderWorkflow.Served,
                    _ => tracked.Status
                };

                DataReconciler.ReconcileTableStatusesWithOrders(_db);
                _db.SaveChanges();
                tx.Commit();
                return new AdvanceOrderOutcome(false, null, false, null);
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        });
    }

    /// <summary>
    /// Marks portal lines prepared; moves order to <c>Ready</c> only when every line on the ticket is prepared.
    /// Admin / legacy callers (no portal) stamp all lines and finalize in one step.
    /// </summary>
    private KitchenReadyResult TryMarkPrepStationReady(int orderId, string? prepStationPortal)
    {
        var order = _db.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .SingleOrDefault(o => o.Id == orderId);
        if (order is null)
            return new KitchenReadyResult(false, "Order not found.", true, null);

        if (string.Equals(order.Status, "Ready", StringComparison.OrdinalIgnoreCase))
            return new KitchenReadyResult(true, null, true, null);

        if (!string.Equals(order.Status, "In Kitchen", StringComparison.OrdinalIgnoreCase))
            return new KitchenReadyResult(false, "Only orders being prepared can be marked ready.", true, null);

        var items = order.Items?.ToList() ?? [];
        if (KitchenStationPrep.AppliesStationScope(prepStationPortal))
        {
            if (KitchenStationPrep.GetPortalLines(prepStationPortal, items).Count == 0)
                return new KitchenReadyResult(false, "This ticket has no items for your station.", true, null);

            if (KitchenStationPrep.AllPortalLinesPrepared(prepStationPortal, items))
            {
                if (KitchenStationPrep.AllOrderLinesPrepared(items))
                    return TryAtomicInKitchenToReady(orderId, stampRemainingLines: false);
                return new KitchenReadyResult(true, null, true, null);
            }
        }

        if (IsInMemoryDatabase(_db))
            return TryMarkPrepStationReadyInMemory(orderId, prepStationPortal);

        return DatabaseResilientTransaction.Execute(_db, () =>
        {
            using var tx = _db.Database.BeginTransaction();
            try
            {
                var tracked = _db.Orders
                    .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                    .Single(o => o.Id == orderId);

                var trackedItems = tracked.Items?.ToList() ?? [];
                KitchenStationPrep.MarkPortalUnpreparedLinesPrepared(prepStationPortal, trackedItems);
                _db.SaveChanges();

                if (!KitchenStationPrep.AllOrderLinesPrepared(trackedItems))
                {
                    DataReconciler.ReconcileTableStatusesWithOrders(_db);
                    _db.SaveChanges();
                    tx.Commit();
                    _db.ChangeTracker.Clear();
                    return new KitchenReadyResult(true, null, true, null);
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            _db.ChangeTracker.Clear();
            return TryAtomicInKitchenToReady(orderId, stampRemainingLines: false);
        });
    }

    private KitchenReadyResult TryMarkPrepStationReadyInMemory(int orderId, string? prepStationPortal)
    {
        var tracked = _db.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Single(o => o.Id == orderId);

        var trackedItems = tracked.Items?.ToList() ?? [];
        KitchenStationPrep.MarkPortalUnpreparedLinesPrepared(prepStationPortal, trackedItems);
        DataReconciler.ReconcileTableStatusesWithOrders(_db);
        _db.SaveChanges();

        if (!KitchenStationPrep.AllOrderLinesPrepared(trackedItems))
        {
            _db.ChangeTracker.Clear();
            return new KitchenReadyResult(true, null, true, null);
        }

        _db.ChangeTracker.Clear();
        var fulfill = CustomerFulfillmentStatuses.ResolveCodeForOrder(tracked.OrderSource);
        return TryInKitchenToReadyForInMemory(orderId, fulfill, stampRemainingLines: false);
    }

    /// <summary>Single row update — only one caller gets a broadcast when racing (SQL providers). In-memory uses a tracked update for tests.</summary>
    private KitchenReadyResult TryAtomicInKitchenToReady(int orderId, bool stampRemainingLines = true)
    {
        var src = _db.Orders.AsNoTracking().Where(o => o.Id == orderId).Select(o => o.OrderSource).FirstOrDefault();
        var fulfill = CustomerFulfillmentStatuses.ResolveCodeForOrder(src);

        if (IsInMemoryDatabase(_db))
            return TryInKitchenToReadyForInMemory(orderId, fulfill, stampRemainingLines);

        return DatabaseResilientTransaction.Execute(_db, () =>
        {
            using var tx = _db.Database.BeginTransaction();
            try
            {
                var affected = _db.Database.ExecuteSqlRaw(
                    """UPDATE "Orders" SET "Status" = {0}, "CustomerFulfillmentStatus" = {1} WHERE "Id" = {2} AND "Status" = {3}""",
                    "Ready",
                    fulfill,
                    orderId,
                    "In Kitchen");

                if (affected == 0)
                {
                    _db.ChangeTracker.Clear();
                    var now = _db.Orders.AsNoTracking().Single(o => o.Id == orderId);
                    if (string.Equals(now.Status, "Ready", StringComparison.OrdinalIgnoreCase))
                    {
                        DataReconciler.ReconcileTableStatusesWithOrders(_db);
                        _db.SaveChanges();
                        tx.Commit();
                        return new KitchenReadyResult(true, null, true, null);
                    }

                    tx.Rollback();
                    return new KitchenReadyResult(false, "Order changed — refresh and try again.", true, null);
                }

                if (stampRemainingLines)
                    StampKitchenPreparedLines(orderId);
                _db.ChangeTracker.Clear();
                DataReconciler.ReconcileTableStatusesWithOrders(_db);
                _db.SaveChanges();
                tx.Commit();

                var fresh = _db.Orders.AsNoTracking().Single(o => o.Id == orderId);
                var note = BuildOrderReadyNotification(fresh);
                return new KitchenReadyResult(true, null, false, note);
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        });
    }

    private KitchenReadyResult TryInKitchenToReadyForInMemory(int orderId, string fulfill, bool stampRemainingLines = true)
    {
        var order = _db.Orders
            .Include(o => o.Items)
            .SingleOrDefault(o => o.Id == orderId);
        if (order is null)
            return new KitchenReadyResult(false, "Order not found.", true, null);

        if (string.Equals(order.Status, "Ready", StringComparison.OrdinalIgnoreCase))
            return new KitchenReadyResult(true, null, true, null);

        if (!string.Equals(order.Status, "In Kitchen", StringComparison.OrdinalIgnoreCase))
            return new KitchenReadyResult(false, "Only orders being prepared can be marked ready.", true, null);

        order.Status = "Ready";
        order.CustomerFulfillmentStatus = fulfill;
        if (stampRemainingLines)
            KitchenLineVisibility.MarkUnpreparedLinesPrepared(order.Items);
        DataReconciler.ReconcileTableStatusesWithOrders(_db);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        var fresh = _db.Orders.AsNoTracking().Single(o => o.Id == orderId);
        return new KitchenReadyResult(true, null, false, BuildOrderReadyNotification(fresh));
    }

    private void StampKitchenPreparedLines(int orderId)
    {
        var items = _db.OrderItems.Where(i => i.OrderRecordId == orderId && i.KitchenPreparedAt == null).ToList();
        KitchenLineVisibility.MarkUnpreparedLinesPrepared(items);
        _db.SaveChanges();
    }

    private static bool IsInMemoryDatabase(AppDbContext db) =>
        db.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true;

    private static OrderReadyNotification BuildOrderReadyNotification(OrderRecord o)
    {
        var code = string.IsNullOrWhiteSpace(o.UniqueId) ? $"#{o.Id:000}" : o.UniqueId;
        string? table = string.IsNullOrWhiteSpace(o.TableCode)
            ? null
            : (string.IsNullOrWhiteSpace(o.TableName) ? o.TableCode : $"{o.TableCode} · {o.TableName}");
        if (string.IsNullOrWhiteSpace(table))
            table = null;
        var guest = string.IsNullOrWhiteSpace(o.ReservationGuestName) ? null : o.ReservationGuestName.Trim();
        var fc = o.CustomerFulfillmentStatus ?? CustomerFulfillmentStatuses.ResolveCodeForOrder(o.OrderSource);
        return new OrderReadyNotification(
            o.Id,
            code,
            o.OrderOrigin,
            o.OrderSource,
            table,
            guest,
            fc,
            CustomerFulfillmentStatuses.ToDisplay(fc));
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
            var totals = OrderTotalsHelper.ComputeTotalsWithDeliveryFee(
                lineSubtotal,
                order.DiscountMode,
                order.DiscountValue,
                order.DeliveryFeeUsd);
            var grandTotalUsd = totals.GrandTotal;
            var paidUsdRounded = Math.Round(Math.Max(0m, paidUsd), 2);
            var paidFcRounded = Math.Round(Math.Max(0m, paidFc), 2);
            var changeUsdRounded = Math.Round(Math.Max(0m, changeGivenUsd), 2);
            var changeFcRounded = Math.Round(Math.Max(0m, changeGivenFc), 2);
            var changeUsdEquivalent = Math.Round(changeUsdRounded + CurrencyHelper.ConvertFcToUsd(changeFcRounded), 2);

            var paymentCurrency = string.IsNullOrWhiteSpace(paymentCurrencyOverride)
                ? (string.IsNullOrWhiteSpace(order.PaymentCurrencyCode) ? CurrencyHelper.Usd : order.PaymentCurrencyCode)
                : paymentCurrencyOverride;

            var netPaymentUsd = Math.Round(Math.Max(0m, paidUsdRounded - changeUsdRounded), 2);
            var netPaymentFc = Math.Round(Math.Max(0m, paidFcRounded - changeFcRounded), 2);

            order.PaymentCurrencyCode = paymentCurrency;
            order.ExchangeRateUsed = CurrencyHelper.FcPerUsd;
            // Revenue split for the ledger uses PaymentAmountUsd/Fc as net cash retained per currency (tender − change).
            order.PaymentAmountUsd = netPaymentUsd;
            order.PaymentAmountFc = netPaymentFc;
            order.PaymentAmount = MoneyReportingHelpers.IsMixedCurrency(paymentCurrency)
                ? netPaymentUsd
                : string.Equals(paymentCurrency, CurrencyHelper.CongoleseFranc, StringComparison.OrdinalIgnoreCase)
                    ? (netPaymentFc > 0m ? netPaymentFc : netPaymentUsd)
                    : (netPaymentUsd > 0m ? netPaymentUsd : netPaymentFc);
            order.CustomerPaidUsd = paidUsdRounded;
            order.CustomerPaidFc = paidFcRounded;
            order.ChangeGivenUsd = changeUsdRounded;
            order.ChangeGivenFc = changeFcRounded;
            if (!string.Equals(previousStatus, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                order.CompletedAt = DateTime.Now;
                order.PaymentConfirmedAt = DateTime.Now;
            }

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

        var isDrink = MenuTaxonomyHelper.IsDrinkProduct(product);
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

    private static bool HasPostedSaleChangeExpense(AppDbContext db, int orderId, string currencyCode) =>
        db.Transactions.Local.Any(t =>
            t.RelatedOrderId == orderId &&
            t.Type == "Expense" &&
            t.Category == "Sale Change" &&
            t.CurrencyCode == currencyCode)
        || db.Transactions.AsNoTracking().Any(t =>
            t.RelatedOrderId == orderId &&
            t.Type == "Expense" &&
            t.Category == "Sale Change" &&
            t.CurrencyCode == currencyCode);

    private static void RecordChangeExpense(AppDbContext db, OrderRecord order)
    {
        var usd = Math.Round(Math.Max(0m, order.ChangeGivenUsd), 2);
        var fc = Math.Round(Math.Max(0m, order.ChangeGivenFc), 2);
        if (usd <= 0m && fc <= 0m)
            return;

        var orderCode = string.IsNullOrWhiteSpace(order.UniqueId) ? $"#{order.Id:000}" : order.UniqueId;
        var usdMarker = $"| CHANGE_ORDER:{order.Id}:USD|";
        var fcMarker = $"| CHANGE_ORDER:{order.Id}:FC|";

        if (usd > 0m && !HasPostedSaleChangeExpense(db, order.Id, CurrencyHelper.Usd))
        {
            db.Transactions.Add(new MoneyTransaction
            {
                RelatedOrderId = order.Id,
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

        if (fc > 0m && !HasPostedSaleChangeExpense(db, order.Id, CurrencyHelper.CongoleseFranc))
        {
            db.Transactions.Add(new MoneyTransaction
            {
                RelatedOrderId = order.Id,
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
             o.Status == OrderWorkflow.PendingCashier ||
             o.Status == OrderWorkflow.PendingApproval));

        if (table.Status == "Maintenance")
            return;

        table.Status = hasActiveOrders ? "Occupied" : "Available";
    }
}
