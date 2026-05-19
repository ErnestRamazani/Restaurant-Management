using EliteRestaurant.Api.Hubs;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace EliteRestaurant.Api.Controllers;

/// <summary>Cashier/admin order actions on canonical <c>api/orders</c> routes.</summary>
[ApiController]
[Route("api/orders")]
[Authorize(Policy = "CashierDesk")]
public sealed class OrdersStaffController(AppDbContext db, IHubContext<OrderHub> orderHub) : ControllerBase
{
    private readonly AdminOrderOperationsService _ops = new(db);

    [HttpPost("{orderId:int}/release-to-kitchen")]
    public async Task<IActionResult> ReleaseToKitchen(int orderId)
    {
        var r = _ops.TryReleasePendingToKitchen(orderId);
        if (!r.Ok)
            return BadRequest(new { message = r.ErrorMessage ?? "Release failed." });

        await orderHub.Clients.Group("Kitchen").SendAsync("KitchenQueueChanged", new { reason = "release-to-kitchen", orderId });
        return Ok(new { ok = true, orderCode = r.ReleasedOrderCode });
    }
}
