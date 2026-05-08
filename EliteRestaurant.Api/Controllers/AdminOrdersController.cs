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
[AllowAnonymous]
public sealed class AdminOrdersController(AppDbContext db, IHubContext<OrderHub> orderHub) : ControllerBase
{
    private readonly AdminOrderOperationsService _ops = new(db);

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
        foreach (var item in newItems)
            existing.Items.Add(item);

        AppendNotes(existing, request.CustomerNotes, request.AllergyNotes);
        if (string.Equals(existing.Status, "Ready", StringComparison.OrdinalIgnoreCase)
            || string.Equals(existing.Status, OrderWorkflow.Served, StringComparison.OrdinalIgnoreCase))
            existing.Status = "In Kitchen";

        if (OrderDiscountParser.ShouldApplyDiscount(request.DiscountMode, request.DiscountInput))
        {
            existing.DiscountMode = request.DiscountMode;
            existing.DiscountValue = string.Equals(request.DiscountMode, "None", StringComparison.OrdinalIgnoreCase)
                ? 0m
                : OrderDiscountParser.Parse(request.DiscountInput);
        }

        OrderSubmissionHelper.SyncPaymentFields(existing, productById);
        OrderSubmissionHelper.ApplyReservationLink(
            existing,
            db,
            request.SelectedOrderSource,
            request.SourceReference,
            request.ReservationCode,
            request.ReservationGuestName);
        table.Status = "Occupied";
        DataReconciler.ReconcileTableStatusesWithOrders(db);
        db.SaveChanges();

        var code = string.IsNullOrWhiteSpace(existing.UniqueId) ? $"#{existing.Id:000}" : existing.UniqueId;
        return Ok(new AdminCreateOrderResponse(true, "Create Order", $"Added {newItems.Count} line(s) to check {code}.", code));
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
        var payUsd = Math.Round(request.LiveGrandTotal, 2);
        var payFc = request.LiveGrandTotalFc;
        var status = request.IsTabletStaffOrderFlow ? OrderWorkflow.PendingCashier : request.SelectedOrderStatus;

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
            PaymentAmountUsd = payUsd,
            PaymentAmountFc = payFc,
            PaymentAmount = string.Equals(paymentCurrency, CurrencyHelper.CongoleseFranc, StringComparison.OrdinalIgnoreCase) ? payFc : payUsd,
            ExchangeRateUsed = CurrencyHelper.FcPerUsd,
            CreatedAt = DateTime.Now,
            OrderSource = string.Equals(request.SelectedOrderSource, "Delivery", StringComparison.OrdinalIgnoreCase) ? "Delivery" : "WalkIn",
            ReservationGuestName = string.Equals(request.SelectedOrderSource, "Delivery", StringComparison.OrdinalIgnoreCase)
                ? request.SourceReference.Trim()
                : string.Empty
        };

        foreach (var item in BuildOrderItems(lines, productById, activeStaff))
            order.Items.Add(item);

        if (!request.IsTabletStaffOrderFlow)
        {
            var invErr = OrderInventoryDeduction.TryApplyForPlacedOrder(db, order);
            if (invErr is not null)
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

        return Ok(request.IsTabletStaffOrderFlow
            ? new AdminCreateOrderResponse(true, "Sent to cashier", $"Ticket {order.UniqueId} sent to the cashier.", order.UniqueId)
            : new AdminCreateOrderResponse(true, "Create Order", $"Order {order.UniqueId} created.", order.UniqueId));
    }

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
        var msg = _ops.TryAdvanceOrder(orderId);
        if (msg == string.Empty)
            return Ok(new AdminOrderAdvanceResponse("missing", null));
        if (msg is not null)
            return Ok(new AdminOrderAdvanceResponse("error", msg));

        await orderHub.Clients.Group("Kitchen").SendAsync("KitchenQueueChanged", new { reason = "advance", orderId });
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
