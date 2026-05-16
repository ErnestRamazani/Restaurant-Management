using EliteRestaurant.Api.Hubs;
using EliteRestaurant.Contracts.Admin;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ModelTable = EliteRestaurant.Core.Models.Table;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/admin/orders")]
[Authorize(Policy = "OperationalWrite")]
public sealed class AdminOrdersController(AppDbContext db, IHubContext<OrderHub> orderHub) : ControllerBase
{
    private readonly AdminOrderOperationsService _ops = new(db);

    private static string NormalizePaymentTiming(string? raw) =>
        string.Equals(raw, OrderPaymentTiming.Deferred, StringComparison.OrdinalIgnoreCase)
            ? OrderPaymentTiming.Deferred
            : OrderPaymentTiming.Immediate;

    [HttpPost("create")]
    public ActionResult<AdminCreateOrderResponse> Create(AdminCreateOrderRequest request)
    {
        var lines = request.Lines
            .Where(l => l.ProductId > 0 && l.Quantity > 0)
            .GroupBy(l => l.ProductId)
            .Select(g => new AdminOrderLineRequest(g.Key, g.Sum(x => x.Quantity)))
            .ToList();

        if (lines.Count == 0)
            return BadRequest(new AdminCreateOrderResponse(false, "Create Order", "No valid order lines were provided.", null));

        var isDelivery = string.Equals(request.SelectedOrderSource, "Delivery", StringComparison.OrdinalIgnoreCase);
        ModelTable? table = null;
        if (!isDelivery)
        {
            table = db.Tables.Include(t => t.AssignedServer).SingleOrDefault(t => t.Id == request.TableId);
            if (table is null || table.AssignedServerId is null || table.AssignedServer is null)
                return BadRequest(new AdminCreateOrderResponse(false, "Create Order", "Selected table must have an assigned server.", null));
        }

        var productIds = lines.Select(s => s.ProductId).Distinct().ToList();
        var activeStaff = db.Employees.AsNoTracking().Where(e => e.EmploymentStatus == "Active").ToList();
        var productById = db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionary(p => p.Id, p => p);
        if (productById.Count != productIds.Count)
            return BadRequest(new AdminCreateOrderResponse(false, "Create Order", "One or more products no longer exist.", null));

        if (request.AppendToOpenCheck && request.OpenOrderId is int openOrderId && table is not null)
            return AppendToExisting(request, openOrderId, table, lines, activeStaff, productById);

        return CreateNew(request, table, lines, activeStaff, productById);
    }

    private ActionResult<AdminCreateOrderResponse> AppendToExisting(
        AdminCreateOrderRequest request,
        int openOrderId,
        ModelTable table,
        IReadOnlyList<AdminOrderLineRequest> lines,
        IReadOnlyList<Employee> activeStaff,
        IReadOnlyDictionary<int, Product> productById)
    {
        var existing = db.Orders.Include(o => o.Items).SingleOrDefault(o => o.Id == openOrderId);
        if (existing is null || existing.TableId != table.Id)
            return BadRequest(new AdminCreateOrderResponse(false, "Create Order", "Open check was closed or moved. Refresh and try again.", null));

        var newItems = BuildOrderItems(lines, productById, activeStaff);

        if (!request.IsTabletStaffOrderFlow)
        {
            var newLineTuples = lines.Select(l => (l.ProductId, l.Quantity)).ToList();
            var inventoryPrecheck = OrderInventoryDeduction.TryValidateInventoryForProductQuantities(
                db,
                newLineTuples,
                OrderInventoryDeduction.InventoryValidationKind.AdditionalLinesOnly);
            if (inventoryPrecheck is not null)
                return BadRequest(new AdminCreateOrderResponse(false, "Insufficient Inventory", inventoryPrecheck, null));
        }

        if (request.IsTabletStaffOrderFlow)
        {
            ApplyOpenCheckAppendMutations(existing, newItems, request, table);
            db.SaveChanges();
            return OkAppendResponse(existing, newItems.Count);
        }

        return DatabaseResilientTransaction.Execute<ActionResult<AdminCreateOrderResponse>>(db, () =>
        {
            if (IsInMemoryDatabase(db))
            {
                var invErrMem = OrderInventoryDeduction.TryApplyForAdditionalItems(db, existing, newItems);
                if (invErrMem is not null)
                    return BadRequest(new AdminCreateOrderResponse(false, "Insufficient Inventory", invErrMem, null));

                ApplyOpenCheckAppendMutations(existing, newItems, request, table);
                db.SaveChanges();
                return OkAppendResponse(existing, newItems.Count);
            }

            using var tx = db.Database.BeginTransaction();
            try
            {
                var invErr = OrderInventoryDeduction.TryApplyForAdditionalItems(db, existing, newItems);
                if (invErr is not null)
                {
                    tx.Rollback();
                    return BadRequest(new AdminCreateOrderResponse(false, "Insufficient Inventory", invErr, null));
                }

                ApplyOpenCheckAppendMutations(existing, newItems, request, table);
                db.SaveChanges();
                tx.Commit();
                return OkAppendResponse(existing, newItems.Count);
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        });
    }

    private void ApplyOpenCheckAppendMutations(
        OrderRecord existing,
        IReadOnlyList<OrderItem> newItems,
        AdminCreateOrderRequest request,
        ModelTable table)
    {
        foreach (var item in newItems)
            existing.Items.Add(item);

        AppendNotes(existing, request.CustomerNotes, request.AllergyNotes);
        if (string.Equals(existing.Status, "Ready", StringComparison.OrdinalIgnoreCase)
            || string.Equals(existing.Status, OrderWorkflow.Served, StringComparison.OrdinalIgnoreCase))
        {
            existing.Status = "In Kitchen";
            existing.CustomerFulfillmentStatus = null;
        }

        if (OrderDiscountParser.ShouldApplyDiscount(request.DiscountMode, request.DiscountInput))
        {
            existing.DiscountMode = request.DiscountMode;
            existing.DiscountValue = string.Equals(request.DiscountMode, "None", StringComparison.OrdinalIgnoreCase)
                ? 0m
                : OrderDiscountParser.Parse(request.DiscountInput);
        }

        var mergedIds = existing.Items.Select(i => i.ProductId).Distinct().ToList();
        var fullProducts = db.Products.AsNoTracking().Where(p => mergedIds.Contains(p.Id)).ToDictionary(p => p.Id, p => p);
        var merchSub = existing.Items.Sum(i =>
        {
            var price = fullProducts.TryGetValue(i.ProductId, out var p) ? p.Price : 0m;
            return price * i.Quantity;
        });
        if (string.Equals(existing.OrderSource, "Delivery", StringComparison.OrdinalIgnoreCase))
            existing.DeliveryFeeUsd = Math.Round(merchSub * 0.20m, 2);

        OrderSubmissionHelper.SyncPaymentFields(existing, fullProducts);
        OrderSubmissionHelper.ApplyReservationLink(
            existing,
            db,
            request.SelectedOrderSource,
            request.SourceReference,
            request.ReservationCode,
            request.ReservationGuestName);
        table.Status = "Occupied";
        DataReconciler.ReconcileTableStatusesWithOrders(db);
    }

    private ActionResult<AdminCreateOrderResponse> OkAppendResponse(OrderRecord existing, int linesAdded)
    {
        var code = string.IsNullOrWhiteSpace(existing.UniqueId) ? $"#{existing.Id:000}" : existing.UniqueId;
        return Ok(new AdminCreateOrderResponse(true, "Create Order", $"Added {linesAdded} line(s) to check {code}.", code));
    }

    private ActionResult<AdminCreateOrderResponse> CreateNew(
        AdminCreateOrderRequest request,
        ModelTable? table,
        IReadOnlyList<AdminOrderLineRequest> lines,
        IReadOnlyList<Employee> activeStaff,
        IReadOnlyDictionary<int, Product> productById)
    {
        var discountRaw = OrderDiscountParser.Parse(request.DiscountInput);
        var discountValue = string.Equals(request.DiscountMode, "None", StringComparison.OrdinalIgnoreCase) ? 0m : discountRaw;
        var paymentCurrency = request.SelectedPaymentCurrency;
        var status = request.IsTabletStaffOrderFlow ? OrderWorkflow.PendingCashier : request.SelectedOrderStatus;

        var merchSubtotal = lines.Sum(l =>
        {
            var price = productById[l.ProductId].Price;
            return price * l.Quantity;
        });
        var isDeliverySource = string.Equals(request.SelectedOrderSource, "Delivery", StringComparison.OrdinalIgnoreCase);
        var deliveryFee = isDeliverySource ? Math.Round(merchSubtotal * 0.20m, 2) : 0m;

        var order = new OrderRecord
        {
            UniqueId = UniqueIdGenerator.NewId("ORD"),
            TableId = table?.Id,
            TableCode = table is null ? "Delivery" : $"Table {table.TableNumber}",
            TableName = table is null
                ? (string.IsNullOrWhiteSpace(request.SourceReference) ? "Delivery" : request.SourceReference)
                : (string.IsNullOrWhiteSpace(table.Name) ? $"Table {table.TableNumber}" : table.Name),
            ServerId = table is null
                ? request.ServerEmployeeId
                : (request.IsTabletStaffOrderFlow ? request.ServerEmployeeId : table.AssignedServerId),
            ServerName = table is null
                ? (string.IsNullOrWhiteSpace(request.ServerEmployeeName) ? "Delivery desk" : request.ServerEmployeeName)
                : (request.IsTabletStaffOrderFlow
                    ? (string.IsNullOrWhiteSpace(request.ServerEmployeeName) ? table.AssignedServer!.Name : request.ServerEmployeeName)
                    : table.AssignedServer!.Name),
            Status = status,
            CustomerNotes = request.CustomerNotes.Trim(),
            AllergyNotes = request.AllergyNotes.Trim(),
            DiscountMode = request.DiscountMode,
            DiscountValue = discountValue,
            DiscountAmountUsd = request.LiveDiscountAmount,
            PaymentCurrencyCode = paymentCurrency,
            ExchangeRateUsed = CurrencyHelper.FcPerUsd,
            CreatedAt = DateTime.Now,
            OrderSource = isDeliverySource ? "Delivery" : "WalkIn",
            OrderOrigin = isDeliverySource ? OrderOrigin.Online : OrderOrigin.InStore,
            PaymentTiming = NormalizePaymentTiming(request.PaymentTiming),
            DeliveryFeeUsd = deliveryFee,
            ReservationGuestName = isDeliverySource
                ? request.SourceReference.Trim()
                : string.Empty
        };

        foreach (var item in BuildOrderItems(lines, productById, activeStaff))
            order.Items.Add(item);

        order.DeliveryFeeUsd = deliveryFee;
        OrderSubmissionHelper.SyncPaymentFields(order, productById);

        if (request.IsTabletStaffOrderFlow)
        {
            OrderSubmissionHelper.ApplyReservationLink(
                order,
                db,
                request.SelectedOrderSource,
                request.SourceReference,
                request.ReservationCode,
                request.ReservationGuestName);
            db.Orders.Add(order);
            if (table is not null)
                table.Status = "Occupied";
            DataReconciler.ReconcileTableStatusesWithOrders(db);
            db.SaveChanges();

            return Ok(new AdminCreateOrderResponse(
                true,
                "Sent to cashier",
                $"Ticket {order.UniqueId} sent to the cashier.",
                order.UniqueId));
        }

        return DatabaseResilientTransaction.Execute<ActionResult<AdminCreateOrderResponse>>(db, () =>
        {
            if (IsInMemoryDatabase(db))
            {
                var invErrMem = OrderInventoryDeduction.TryApplyForPlacedOrder(db, order);
                if (invErrMem is not null)
                    return BadRequest(new AdminCreateOrderResponse(false, "Insufficient Inventory", invErrMem, null));

                OrderSubmissionHelper.ApplyReservationLink(
                    order,
                    db,
                    request.SelectedOrderSource,
                    request.SourceReference,
                    request.ReservationCode,
                    request.ReservationGuestName);
                db.Orders.Add(order);
                if (table is not null)
                    table.Status = "Occupied";
                DataReconciler.ReconcileTableStatusesWithOrders(db);
                db.SaveChanges();

                return Ok(new AdminCreateOrderResponse(true, "Create Order", $"Order {order.UniqueId} created.", order.UniqueId));
            }

            using var tx = db.Database.BeginTransaction();
            try
            {
                var invErr = OrderInventoryDeduction.TryApplyForPlacedOrder(db, order);
                if (invErr is not null)
                {
                    tx.Rollback();
                    return BadRequest(new AdminCreateOrderResponse(false, "Insufficient Inventory", invErr, null));
                }

                OrderSubmissionHelper.ApplyReservationLink(
                    order,
                    db,
                    request.SelectedOrderSource,
                    request.SourceReference,
                    request.ReservationCode,
                    request.ReservationGuestName);
                db.Orders.Add(order);
                if (table is not null)
                    table.Status = "Occupied";
                DataReconciler.ReconcileTableStatusesWithOrders(db);
                db.SaveChanges();
                tx.Commit();

                return Ok(new AdminCreateOrderResponse(true, "Create Order", $"Order {order.UniqueId} created.", order.UniqueId));
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        });
    }

    private static bool IsInMemoryDatabase(AppDbContext context) =>
        context.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true;

    [HttpPost("pending/{orderId:int}/release-to-kitchen")]
    public async Task<ActionResult<AdminOrderReleasePendingResponse>> ReleasePendingToKitchen(int orderId)
    {
        var r = _ops.TryReleasePendingToKitchen(orderId);
        if (r.Ok)
            await orderHub.Clients.Group("Kitchen").SendAsync("KitchenQueueChanged", new { reason = "release-pending", orderId });

        return Ok(new AdminOrderReleasePendingResponse(r.Ok, r.ErrorMessage, r.ReleasedOrderCode));
    }

    [HttpPost("pending/{orderId:int}/cancel")]
    public ActionResult<AdminOrderOpMessageResponse> CancelPending(int orderId)
    {
        var err = _ops.TryCancelPendingCashier(orderId);
        return Ok(new AdminOrderOpMessageResponse(err is null, err));
    }

    [HttpPost("walk-in")]
    public ActionResult<AdminOrderOpMessageResponse> CreateWalkInFromDesk(AdminWalkInOrderDeskRequest request)
    {
        var lines = request.Lines
            .Where(l => l.ProductId > 0 && l.Quantity > 0)
            .GroupBy(l => l.ProductId)
            .Select(g => new AdminWalkInLine(g.Key, g.Sum(x => x.Quantity)))
            .ToList();
        if (lines.Count == 0)
            return Ok(new AdminOrderOpMessageResponse(false, "No valid order lines were provided."));

        var err = _ops.TryCreateWalkInOrder(request.TableId, request.SelectedOrderStatus, lines);
        return Ok(new AdminOrderOpMessageResponse(err is null, err));
    }

    [HttpPost("{orderId:int}/advance")]
    public async Task<ActionResult<AdminOrderAdvanceResponse>> Advance(int orderId)
    {
        var outcome = _ops.TryAdvanceOrderWithOutcome(orderId);
        if (outcome.Missing)
            return Ok(new AdminOrderAdvanceResponse("missing", null));
        if (outcome.Error is not null)
            return Ok(new AdminOrderAdvanceResponse("error", outcome.Error));

        await orderHub.Clients.Group("Kitchen").SendAsync("KitchenQueueChanged", new { reason = "advance", orderId });

        if (outcome.BecameReady && outcome.ReadyNotification is not null)
        {
            await orderHub.Clients.Group("Cashier").SendAsync("OrderReady", outcome.ReadyNotification);
            await orderHub.Clients.Group("Cashier").SendAsync(
                "CashierOrderBoardChanged",
                new { reason = "order-ready", orderId, orderCode = outcome.ReadyNotification.OrderCode });
        }

        return Ok(new AdminOrderAdvanceResponse("advanced", null));
    }

    [HttpPost("{orderId:int}/status")]
    public ActionResult<AdminOrderOpMessageResponse> UpdateStatus(int orderId, AdminOrderStatusUpdateRequest request)
    {
        try
        {
            _ops.UpdateOrderStatus(
                orderId,
                request.Status,
                request.PaymentCurrencyOverride,
                request.PaidUsd,
                request.PaidFc,
                request.ChangeGivenUsd,
                request.ChangeGivenFc);
            return Ok(new AdminOrderOpMessageResponse(true, null));
        }
        catch (InvalidOperationException ex)
        {
            return Ok(new AdminOrderOpMessageResponse(false, ex.Message));
        }
    }

    private static List<OrderItem> BuildOrderItems(
        IReadOnlyList<AdminOrderLineRequest> lines,
        IReadOnlyDictionary<int, Product> products,
        IReadOnlyList<Employee> activeStaff)
    {
        return lines.Select(line =>
        {
            var assignee = OrderSubmissionHelper.ResolveAssignee(products, activeStaff, line.ProductId);
            return new OrderItem
            {
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                PreparedByEmployeeId = assignee.EmployeeId,
                PreparedByRole = assignee.Role,
                PreparedByName = assignee.Name
            };
        }).ToList();
    }

    private static void AppendNotes(OrderRecord order, string customerNotes, string allergyNotes)
    {
        if (!string.IsNullOrWhiteSpace(customerNotes))
        {
            order.CustomerNotes = string.IsNullOrWhiteSpace(order.CustomerNotes)
                ? customerNotes.Trim()
                : $"{order.CustomerNotes.Trim()}\n{customerNotes.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(allergyNotes))
        {
            order.AllergyNotes = string.IsNullOrWhiteSpace(order.AllergyNotes)
                ? allergyNotes.Trim()
                : $"{order.AllergyNotes.Trim()}\n{allergyNotes.Trim()}";
        }
    }
}
