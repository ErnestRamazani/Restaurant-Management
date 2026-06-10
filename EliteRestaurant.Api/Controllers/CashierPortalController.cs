using EliteRestaurant.Api.Branding;
using EliteRestaurant.Api.Dtos;
using EliteRestaurant.Api.Hubs;
using EliteRestaurant.Api.Services;
using EliteRestaurant.Api.Orders;
using EliteRestaurant.Api.Security;
using EliteRestaurant.Contracts.Admin;
using EliteRestaurant.Core.Clients;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Tickets;
using EliteRestaurant.Core.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Serilog;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/cashier")]
[Authorize(Policy = "CashierOnly")]
public sealed class CashierPortalController(
    TabletAuthService authService,
    AppDbContext db,
    IHubContext<OrderHub> orderHub,
    IWebHostEnvironment env,
    PublicMenuSettingsCache menuSettings) : ControllerBase
{
    private readonly AdminOrderOperationsService _ops = new(db);

    [HttpGet("alerts")]
    public ActionResult<IReadOnlyList<string>> GetAlerts()
    {
        var session = RequireCashierSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token or non-cashier role." });

        return Ok(CashierOrderAlerts.GetBannerLines(db));
    }

    [HttpGet("orders/pending")]
    public ActionResult<IReadOnlyList<CashierPendingOrderDto>> GetPending()
    {
        var session = RequireCashierSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token or non-cashier role." });

        var pendingOrders = db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Where(o =>
                o.Status == OrderWorkflow.PendingCashier || o.Status == OrderWorkflow.PendingApproval)
            .OrderByDescending(o => o.CreatedAt)
            .ToList();

        var rows = pendingOrders.Select(o =>
        {
            var subtotal = o.Items.Sum(i => (i.Product?.Price ?? 0m) * i.Quantity);
            var totals = OrderTotalsHelper.ComputeTotalsWithDeliveryFee(
                subtotal,
                o.DiscountMode,
                o.DiscountValue,
                o.DeliveryFeeUsd);
            var lines = string.Join(", ",
                o.Items.Select(i => $"{i.Product?.Name ?? "Item"} x{i.Quantity}"));
            return new CashierPendingOrderDto(
                o.Id,
                string.IsNullOrWhiteSpace(o.UniqueId) ? $"#{o.Id:000}" : o.UniqueId,
                (o.ConfirmationCode ?? string.Empty).Trim(),
                OrderRecordUiLabels.TableCaption(o),
                OrderRecordUiLabels.ServerCaption(o),
                o.CreatedAt,
                o.CreatedAt.ToString("MMM d, yyyy · HH:mm"),
                totals.GrandTotal,
                $"$ {totals.GrandTotal:N2}",
                string.IsNullOrWhiteSpace(lines) ? "No lines" : lines,
                o.Status,
                o.OrderOrigin);
        }).ToList();

        return Ok(rows);
    }

    [HttpPost("orders/pending/{orderId:int}/release")]
    public async Task<ActionResult> ReleaseToKitchen(int orderId)
    {
        var session = RequireCashierSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token or non-cashier role." });

        var result = _ops.TryReleasePendingToKitchen(orderId);
        if (!result.Ok)
            return BadRequest(new { message = result.ErrorMessage ?? "Release failed." });

        Log.Information("Order {OrderId} released to kitchen by cashier {EmployeeId} ({OrderCode})",
            orderId, session.EmployeeId, result.ReleasedOrderCode ?? "");

        await OrderHubBroadcasts.NotifyKitchenQueueChangedAsync(orderHub, db, orderId, "cashier-release");
        await OrderHubBroadcasts.NotifyCashierOrderBoardChangedAsync(orderHub, db, orderId, "released-to-kitchen");
        await OrderHubBroadcasts.NotifyReceptionDeliveryPickupChangedAsync(orderHub, db, orderId, "released-to-kitchen");

        return Ok(new { ok = true, orderCode = result.ReleasedOrderCode });
    }

    [HttpPost("orders/pending/{orderId:int}/cancel")]
    public async Task<ActionResult> CancelPending(int orderId, [FromBody] OrderCancelRequest request)
    {
        var session = RequireCashierSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token or non-cashier role." });

        var err = _ops.TryCancelOrder(orderId, request.Passcode);
        if (err is not null)
            return BadRequest(new { message = err });

        await OrderHubBroadcasts.NotifyCashierOrderBoardChangedAsync(orderHub, db, orderId, "pending-cancelled");

        return Ok(new { ok = true });
    }

    [HttpGet("orders/active")]
    public ActionResult<IReadOnlyList<OrderEntry>> GetActive()
    {
        var session = RequireCashierSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token or non-cashier role." });

        const bool showAdminAdvance = false;
        const bool canViewTicket = true;

        var tz = ResolveRestaurantTimeZoneId();
        var orders = db.Orders
            .AsNoTracking()
            .Include(o => o.Table)
            .Include(o => o.Server)
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Where(o => o.Status == "Waiting" || o.Status == "In Kitchen" || o.Status == "Ready" ||
                        o.Status == OrderWorkflow.Served)
            .OrderByDescending(o => o.CreatedAt)
            .ToList()
            .Select(o => AdminOrdersViewMapper.MapOrder(o, false, showAdminAdvance, canViewTicket, tz))
            .ToList();

        return Ok(orders);
    }

    [HttpGet("orders/past")]
    public ActionResult<IReadOnlyList<OrderEntry>> GetPast()
    {
        var session = RequireCashierSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token or non-cashier role." });

        const bool showAdminAdvance = false;
        const bool canViewTicket = true;

        var tz = ResolveRestaurantTimeZoneId();
        var orders = db.Orders
            .AsNoTracking()
            .Include(o => o.Table)
            .Include(o => o.Server)
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Where(o => o.Status == "Completed" || o.Status == "Cancelled")
            .OrderByDescending(o => o.CreatedAt)
            .Take(250)
            .ToList()
            .Select(o => AdminOrdersViewMapper.MapOrder(o, true, showAdminAdvance, canViewTicket, tz))
            .ToList();

        return Ok(orders);
    }

    [HttpGet("orders/{orderId:int}")]
    public ActionResult<CashierOrderDetailDto> GetOrderDetail(int orderId)
    {
        var session = RequireCashierSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token or non-cashier role." });

        var order = db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Include(o => o.Table)
            .Include(o => o.Server)
            .SingleOrDefault(o => o.Id == orderId);
        if (order is null)
            return NotFound(new { message = "Order not found." });

        RestaurantClient? client = null;
        if (order.RestaurantClientId is int cid)
            client = db.RestaurantClients.AsNoTracking().FirstOrDefault(c => c.Id == cid);

        var cap = new ClientAccountService(db).GetDebtCapUsd();
        return Ok(CashierOrderDetailBuilder.Build(order, client, cap));
    }

    /// <summary>Alias for the full invoice breakdown (same payload as <see cref="GetOrderDetail"/>).</summary>
    [HttpGet("orders/{orderId:int}/invoice")]
    public ActionResult<CashierOrderDetailDto> GetOrderInvoice(int orderId) => GetOrderDetail(orderId);

    /// <summary>
    /// Thermal receipt PDF — same QuestPDF layout as Elite Pro (client ticket before payment, payment receipt after).
    /// </summary>
    [HttpGet("orders/{orderId:int}/ticket.pdf")]
    public ActionResult GetOrderTicketPdf(int orderId, [FromQuery] string? variant = null)
    {
        var session = RequireCashierSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token or non-cashier role." });

        var order = db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Include(o => o.Table)
            .Include(o => o.Server)
            .SingleOrDefault(o => o.Id == orderId);
        if (order is null)
            return NotFound(new { message = "Order not found." });
        if (order.Items.Count == 0)
            return BadRequest(new { message = "Order has no line items." });

        var usePayment = variant switch
        {
            "payment" or "receipt" => true,
            "client" or "ticket" => false,
            _ => OrderTicketPdfBuilder.UsePaymentReceiptVariant(order)
        };

        var model = BuildOrderTicketModel(order);
        var pdfBytes = usePayment
            ? AdminTicketPdfExportService.GeneratePaymentReceiptPdfBytes(model)
            : AdminTicketPdfExportService.GenerateClientTicketPdfBytes(model);

        Response.Headers.ContentDisposition = "inline; filename=\"receipt.pdf\"";
        return File(pdfBytes, "application/pdf");
    }

    /// <summary>Browser-printable receipt HTML (opens system print dialog; no PDF download).</summary>
    [HttpGet("orders/{orderId:int}/ticket.html")]
    public ContentResult GetOrderTicketHtml(int orderId, [FromQuery] string? variant = null)
    {
        var session = RequireCashierSession();
        if (session is null)
            return Content("<p>Unauthorized</p>", "text/html; charset=utf-8");

        var order = db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Include(o => o.Table)
            .Include(o => o.Server)
            .SingleOrDefault(o => o.Id == orderId);
        if (order is null)
            return Content("<p>Order not found.</p>", "text/html; charset=utf-8");
        if (order.Items.Count == 0)
            return Content("<p>Order has no line items.</p>", "text/html; charset=utf-8");

        var usePayment = variant switch
        {
            "payment" or "receipt" => true,
            "client" or "ticket" => false,
            _ => OrderTicketPdfBuilder.UsePaymentReceiptVariant(order)
        };

        var model = BuildOrderTicketModel(order);
        var html = usePayment
            ? OrderTicketHtmlBuilder.BuildPaymentReceiptHtml(model)
            : OrderTicketHtmlBuilder.BuildClientTicketHtml(model);

        Response.Headers.CacheControl = "no-store";
        return Content(html, "text/html; charset=utf-8");
    }

    [HttpPost("orders/{orderId:int}/complete")]
    public async Task<ActionResult> CompleteOrder(int orderId, [FromBody] CashierCompleteOrderRequest request)
    {
        var session = RequireCashierSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token or non-cashier role." });

        var order = db.Orders.AsNoTracking().SingleOrDefault(o => o.Id == orderId);
        if (order is null)
            return NotFound(new { message = "Order not found." });
        if (!OrderWorkflow.CanCashierComplete(order.Status, order.OrderOrigin))
            return BadRequest(new
            {
                message =
                    OrderOrigin.IsOnline(order.OrderOrigin)
                        ? "Complete is only available when the order is Ready (kitchen finished) or Served. For guest online orders you can pay as soon as it is Ready — no server step required."
                        : "Complete is only available when the order is Served. Flow: kitchen marks Ready → server marks Served → cashier completes payment."
            });

        var clients = new ClientAccountService(db);
        var settlement = (request.Settlement ?? "PayNow").Trim();
        if (string.Equals(settlement, "OnAccount", StringComparison.OrdinalIgnoreCase))
        {
            var err = clients.TryCompleteOrderOnAccount(orderId, null);
            if (err is not null)
                return BadRequest(new { message = err });
        }
        else
        {
            var err = clients.TryCompleteOrderPaid(
                orderId,
                request.PaymentCurrencyCode,
                request.PaidUsd,
                request.PaidFc,
                request.ChangeUsd,
                request.ChangeFc,
                _ops);
            if (err is not null)
                return BadRequest(new { message = err });
        }

        await OrderHubBroadcasts.NotifyReceptionDeliveryPickupChangedAsync(orderHub, db, orderId, "order-completed");
        await OrderHubBroadcasts.NotifyCashierOrderBoardChangedAsync(orderHub, db, orderId, "order-completed");

        return Ok(new { ok = true });
    }

    [HttpPost("orders/{orderId:int}/cancel")]
    public async Task<ActionResult> CancelActiveOrder(int orderId, [FromBody] OrderCancelRequest request)
    {
        var session = RequireCashierSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token or non-cashier role." });

        var err = _ops.TryCancelOrder(orderId, request.Passcode);
        if (err is not null)
            return BadRequest(new { message = err });

        await OrderHubBroadcasts.NotifyCashierOrderBoardChangedAsync(orderHub, db, orderId, "order-cancelled");

        return Ok(new { ok = true });
    }

    private string ResolveRestaurantTimeZoneId()
    {
        var business = SettingsManager.Load().BusinessProfile;
        var cloud = menuSettings.GetDefault();
        return RestaurantTimeZone.ResolveId(cloud, business);
    }

    private TicketReceiptPdfModel BuildOrderTicketModel(OrderRecord order)
    {
        var cloudRow = db.PublicMenuSettings.AsNoTracking().FirstOrDefault(s => s.Key == "default");
        var settings = TicketReceiptSettingsMerger.MergeForTicketReceipt(SettingsManager.Load(), cloudRow);
        var headerBytes = TicketBrandingImageResolver.ResolveHeaderLogoBytes(settings, db, env);
        var socialRows = TicketBrandingImageResolver.ResolveSocialFooterRows(db, cloudRow, settings);
        return OrderTicketPdfBuilder.Build(order, settings, headerBytes, socialRows);
    }

    private AuthenticatedStaffSession? RequireCashierSession()
    {
        var token = Request.ReadBearerToken();
        var session = authService.Validate(token);
        if (session is null)
            return null;
        return session.Role.Equals("Cashier", StringComparison.OrdinalIgnoreCase) ? session : null;
    }
}
