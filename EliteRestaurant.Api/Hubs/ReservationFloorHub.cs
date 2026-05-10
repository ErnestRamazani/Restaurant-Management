using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace EliteRestaurant.Api.Hubs;

[Authorize(Policy = "CashierOrAdmin")]
public sealed class ReservationFloorHub : Hub
{
    public async Task JoinFloor()
    {
        var role = Context.User?.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (role.Length == 0)
            return;

        await Groups.AddToGroupAsync(Context.ConnectionId, "Floor");
    }
}
