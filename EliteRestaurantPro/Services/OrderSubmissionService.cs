using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.Utils;
using Microsoft.EntityFrameworkCore;
using ModelTable = EliteRestaurant.Core.Models.Table;

namespace EliteRestaurantPro.Services;

/// <summary>Persists create-order flows: phase validation, append to open check, new order.</summary>
public sealed class OrderSubmissionService
{
    public CreateOrderPhaseResult LoadPhase1(CreateOrderSubmitSnapshot snap)
    {
        if (string.Equals(snap.SelectedOrderSource, "Delivery", StringComparison.OrdinalIgnoreCase))
        {
            return new CreateOrderPhaseResult(
                true,
                "Create Order",
                string.Empty,
                0,
                "Delivery",
                new CreateOrderOpenCheckInfo(null, string.Empty, string.Empty));
        }

        using var db = new AppDbContext();
        var table = db.Tables.Include(t => t.AssignedServer).SingleOrDefault(t => t.Id == snap.TableId);
        if (table is null || table.AssignedServerId is null || table.AssignedServer is null)
            return new CreateOrderPhaseResult(false, "Create Order", "Selected table must have an assigned server.", 0, string.Empty, new CreateOrderOpenCheckInfo(null, string.Empty, string.Empty));

        if (AppSession.IsServerTablet && table.AssignedServerId != snap.ServerEmployeeId)
            return new CreateOrderPhaseResult(false, "Create Order", "This table is not assigned to your session.", 0, string.Empty, new CreateOrderOpenCheckInfo(null, string.Empty, string.Empty));

        var open = db.Orders.AsNoTracking()
            .WhereOpenCheckForTable(table.Id)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault();
        var code = open is null ? string.Empty : string.IsNullOrWhiteSpace(open.UniqueId) ? $"#{open.Id:000}" : open.UniqueId;
        var tableName = string.IsNullOrWhiteSpace(table.Name) ? $"Table {table.TableNumber}" : table.Name;

        return new CreateOrderPhaseResult(
            true,
            "Create Order",
            string.Empty,
            table.TableNumber,
            tableName,
            new CreateOrderOpenCheckInfo(open?.Id, code, open?.Status ?? string.Empty));
    }

    public CreateOrderAppendResult AppendToExisting(CreateOrderSubmitSnapshot snap, int openOrderId)
    {
        using var db = new AppDbContext();
        var table = db.Tables.Include(t => t.AssignedServer).SingleOrDefault(t => t.Id == snap.TableId);
        if (table is null)
            return new CreateOrderAppendResult(false, "Create Order", "Table not found.");

        var existing = db.Orders.Include(o => o.Items).SingleOrDefault(o => o.Id == openOrderId);
        if (existing is null || existing.TableId != table.Id)
            return new CreateOrderAppendResult(false, "Create Order", "Open check was closed or moved. Refresh and try again.");

        var productIds = snap.SelectedLines.Select(s => s.ProductId).Distinct().ToList();
        var activeStaff = db.Employees.AsNoTracking().Where(e => e.EmploymentStatus == "Active").ToList();
        var productById = db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionary(p => p.Id, p => p);

        var newItems = new List<OrderItem>();
        foreach (var (productId, qty) in snap.SelectedLines)
        {
            var assignee = OrderSubmissionHelper.ResolveAssignee(productById, activeStaff, productId);
            newItems.Add(new OrderItem
            {
                ProductId = productId,
                Quantity = qty,
                PreparedByEmployeeId = assignee.EmployeeId,
                PreparedByRole = assignee.Role,
                PreparedByName = assignee.Name
            });
        }

        foreach (var item in newItems)
            existing.Items.Add(item);

        if (!string.IsNullOrWhiteSpace(snap.CustomerNotes))
        {
            existing.CustomerNotes = string.IsNullOrWhiteSpace(existing.CustomerNotes)
                ? snap.CustomerNotes.Trim()
                : $"{existing.CustomerNotes.Trim()}\n{snap.CustomerNotes.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(snap.AllergyNotes))
        {
            existing.AllergyNotes = string.IsNullOrWhiteSpace(existing.AllergyNotes)
                ? snap.AllergyNotes.Trim()
                : $"{existing.AllergyNotes.Trim()}\n{snap.AllergyNotes.Trim()}";
        }

        if (string.Equals(existing.Status, "Ready", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(existing.Status, OrderWorkflow.Served, StringComparison.OrdinalIgnoreCase))
            existing.Status = "In Kitchen";

        if (OrderDiscountParser.ShouldApplyDiscount(snap.DiscountMode, snap.DiscountInput))
        {
            var discountRaw = OrderDiscountParser.Parse(snap.DiscountInput);
            existing.DiscountMode = snap.DiscountMode;
            existing.DiscountValue = string.Equals(snap.DiscountMode, "None", StringComparison.OrdinalIgnoreCase)
                ? 0m
                : discountRaw;
        }

        OrderSubmissionHelper.SyncPaymentFields(existing, db);
        OrderSubmissionHelper.ApplyReservationLink(
            existing,
            db,
            snap.SelectedOrderSource,
            snap.SourceReference,
            snap.ReservationCode,
            snap.ReservationGuestName);
        table.Status = "Occupied";

        return DatabaseResilientTransaction.Execute(db, () =>
        {
            using var tx = db.Database.BeginTransaction();
            try
            {
                if (!OrderWorkflow.IsPendingCashier(existing.Status))
                {
                    var invErr = OrderInventoryDeduction.TryApplyForAdditionalItems(db, existing, newItems);
                    if (invErr is not null)
                    {
                        tx.Rollback();
                        return new CreateOrderAppendResult(false, "Insufficient Inventory", invErr);
                    }
                }

                DataReconciler.ReconcileTableStatusesWithOrders(db);
                db.SaveChanges();
                tx.Commit();
                var code = string.IsNullOrWhiteSpace(existing.UniqueId) ? $"#{existing.Id:000}" : existing.UniqueId;
                return new CreateOrderAppendResult(true, "Create Order", $"Added {newItems.Count} line(s) to check {code}.");
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        });
    }

    public CreateOrderSaveResult SaveNew(CreateOrderSubmitSnapshot snap)
    {
        var discountRaw = OrderDiscountParser.Parse(snap.DiscountInput);
        using var db = new AppDbContext();
        ModelTable? table = null;
        if (!string.Equals(snap.SelectedOrderSource, "Delivery", StringComparison.OrdinalIgnoreCase))
        {
            table = db.Tables.Include(t => t.AssignedServer).SingleOrDefault(t => t.Id == snap.TableId);
            if (table is null || table.AssignedServerId is null || table.AssignedServer is null)
                return new CreateOrderSaveResult(false, "Create Order", "Selected table must have an assigned server.");
        }

        var status = snap.IsTabletStaffOrderFlow ? OrderWorkflow.PendingCashier : snap.SelectedOrderStatus;
        var discountValue = string.Equals(snap.DiscountMode, "None", StringComparison.OrdinalIgnoreCase) ? 0m : discountRaw;
        var paymentCurrency = snap.SelectedPaymentCurrency;
        var payUsd = Math.Round(snap.LiveGrandTotal, 2);
        var payFc = snap.LiveGrandTotalFc;

        var order = new OrderRecord
        {
            UniqueId = UniqueIdGenerator.NewId("ORD"),
            TableId = table?.Id,
            TableCode = table is null ? "Delivery" : $"Table {table.TableNumber}",
            TableName = table is null
                ? (string.IsNullOrWhiteSpace(snap.SourceReference) ? "Delivery" : snap.SourceReference)
                : (string.IsNullOrWhiteSpace(table.Name) ? $"Table {table.TableNumber}" : table.Name),
            ServerId = table is null
                ? AppSession.StaffEmployeeId
                : (AppSession.IsServerTablet ? snap.ServerEmployeeId : table.AssignedServerId),
            ServerName = table is null
                ? (string.IsNullOrWhiteSpace(snap.ServerEmployeeName) ? "Delivery desk" : snap.ServerEmployeeName)
                : (AppSession.IsServerTablet
                    ? (string.IsNullOrWhiteSpace(snap.ServerEmployeeName) ? table.AssignedServer!.Name : snap.ServerEmployeeName)
                    : table.AssignedServer!.Name),
            Status = status,
            CustomerNotes = snap.CustomerNotes.Trim(),
            AllergyNotes = snap.AllergyNotes.Trim(),
            DiscountMode = snap.DiscountMode,
            DiscountValue = discountValue,
            DiscountAmountUsd = snap.LiveDiscountAmount,
            PaymentCurrencyCode = paymentCurrency,
            PaymentAmountUsd = payUsd,
            PaymentAmountFc = payFc,
            PaymentAmount = string.Equals(paymentCurrency, CurrencyHelper.CongoleseFranc, StringComparison.OrdinalIgnoreCase) ? payFc : payUsd,
            ExchangeRateUsed = CurrencyHelper.FcPerUsd,
            CreatedAt = DateTime.Now
        };

        var productIds = snap.SelectedLines.Select(s => s.ProductId).Distinct().ToList();
        var activeStaff = db.Employees.AsNoTracking().Where(e => e.EmploymentStatus == "Active").ToList();
        var productById = db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionary(p => p.Id, p => p);
        foreach (var (productId, qty) in snap.SelectedLines)
        {
            var assignee = OrderSubmissionHelper.ResolveAssignee(productById, activeStaff, productId);
            order.Items.Add(new OrderItem
            {
                ProductId = productId,
                Quantity = qty,
                PreparedByEmployeeId = assignee.EmployeeId,
                PreparedByRole = assignee.Role,
                PreparedByName = assignee.Name
            });
        }

        return DatabaseResilientTransaction.Execute(db, () =>
        {
            using var tx = db.Database.BeginTransaction();
            try
            {
                if (!snap.IsTabletStaffOrderFlow)
                {
                    var invErr = OrderInventoryDeduction.TryApplyForPlacedOrder(db, order);
                    if (invErr is not null)
                    {
                        tx.Rollback();
                        return new CreateOrderSaveResult(false, "Insufficient Inventory", invErr);
                    }
                }

                OrderSubmissionHelper.ApplyReservationLink(
                    order,
                    db,
                    snap.SelectedOrderSource,
                    snap.SourceReference,
                    snap.ReservationCode,
                    snap.ReservationGuestName);
                db.Orders.Add(order);
                if (table is not null)
                    table.Status = "Occupied";
                DataReconciler.ReconcileTableStatusesWithOrders(db);
                db.SaveChanges();
                tx.Commit();
                return snap.IsTabletStaffOrderFlow
                    ? new CreateOrderSaveResult(true, "Sent to cashier", $"Ticket {order.UniqueId} sent to the cashier.")
                    : new CreateOrderSaveResult(true, "Create Order", $"Order {order.UniqueId} created.");
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        });
    }
}
