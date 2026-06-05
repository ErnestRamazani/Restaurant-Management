using EliteRestaurant.Api.Hubs;
using EliteRestaurant.Contracts.Admin;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/staff/orders")]
[Authorize(Policy = "CancelOrder")]
public sealed class StaffOrdersController(
    AppDbContext db,
    IHubContext<OrderHub> orderHub) : ControllerBase
{
    private readonly AdminOrderOperationsService _ops = new(db);

    [HttpPost("{orderId:int}/cancel")]
    public async Task<ActionResult<AdminOrderOpMessageResponse>> Cancel(int orderId, [FromBody] OrderCancelRequest request)
    {
        var err = _ops.TryCancelOrder(orderId, request.Passcode);
        if (err is not null)
            return BadRequest(new AdminOrderOpMessageResponse(false, err));

        await OrderHubBroadcasts.NotifyCashierOrderBoardChangedAsync(orderHub, db, orderId, "order-cancelled");
        return Ok(new AdminOrderOpMessageResponse(true, null));
    }
}
