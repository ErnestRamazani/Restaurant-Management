using EliteRestaurant.Api.Dtos;
using EliteRestaurant.Api.Security;
using EliteRestaurant.Api.Tables;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/reception")]
[Authorize(Policy = "ReceptionDesk")]
public sealed class ReceptionController(TabletAuthService authService, AppDbContext db) : ControllerBase
{
    /// <summary>All dining tables for front desk (guest menu links). Same data as <c>GET /api/tables/my</c> for non-server roles.</summary>
    [HttpGet("tables")]
    public ActionResult<IReadOnlyList<TableSummaryDto>> ListTables()
    {
        var session = RequireReceptionSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token or role not allowed for reception." });

        return Ok(StaffTableListQuery.ListForSession(db, session));
    }

    [HttpGet("delivery-pickup-orders")]
    public async Task<ActionResult<IReadOnlyList<ReceptionDeliveryPickupOrderRow>>> ListDeliveryPickupOrders(
        CancellationToken cancellationToken)
    {
        if (RequireReceptionSession() is null)
            return Unauthorized(new { message = "Missing/invalid token or role not allowed for reception." });

        var orders = await db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .WhereOnlineDeliveryOrPickup()
            .Where(o => o.Status != "Cancelled")
            .OrderByDescending(o => o.Status == "Ready")
            .ThenByDescending(o => o.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        var rows = orders.Select(MapDeliveryPickupRow).ToList();
        return Ok(rows);
    }

    private static ReceptionDeliveryPickupOrderRow MapDeliveryPickupRow(OrderRecord order)
    {
        var ticket = DeliveryTicketInfoParser.TryParse(order);
        var guestName = (order.ReservationGuestName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(guestName))
            guestName = ticket?.CustomerName ?? "Guest";

        var guestPhone = ticket?.Phone ?? string.Empty;
        var fulfillment = string.Equals(order.OrderSource, "Delivery", StringComparison.OrdinalIgnoreCase)
            ? "Delivery"
            : "Pickup";

        var lines = order.Items
            .Select(i => $"{i.Product?.Name ?? "Item"} x{i.Quantity}")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
        var itemsSummary = lines.Count == 0
            ? "No lines"
            : string.Join(", ", lines.Take(6)) + (lines.Count > 6 ? "…" : "");

        var code = string.IsNullOrWhiteSpace(order.UniqueId) ? $"#{order.Id:000}" : order.UniqueId;
        var isReady = string.Equals(order.Status, "Ready", StringComparison.OrdinalIgnoreCase);

        return new ReceptionDeliveryPickupOrderRow(
            order.Id,
            code,
            guestName,
            guestPhone,
            fulfillment,
            order.Status,
            order.CreatedAt,
            order.CreatedAt.ToString("MMM d, yyyy · HH:mm"),
            itemsSummary,
            isReady);
    }

    private AuthenticatedStaffSession? RequireReceptionSession()
    {
        var token = Request.ReadBearerToken();
        var session = authService.Validate(token);
        if (session is null)
            return null;

        return StaffPortalAuthentication.IsReceptionRole(session.Role) ? session : null;
    }
}
