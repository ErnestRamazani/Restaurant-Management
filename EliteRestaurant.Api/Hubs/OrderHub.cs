using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace EliteRestaurant.Api.Hubs;

[Authorize(Policy = "StaffAny")]
public sealed class OrderHub : Hub
{
    public async Task JoinServer()
    {
        var role = Context.User?.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (role.Equals("Server", StringComparison.OrdinalIgnoreCase)
            || role.Equals("Cashier", StringComparison.OrdinalIgnoreCase)
            || role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
            || role.Equals("Manager", StringComparison.OrdinalIgnoreCase))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Server");
        }
    }

    public async Task JoinKitchen()
    {
        var role = Context.User?.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (role.Equals("Chef", StringComparison.OrdinalIgnoreCase)
            || role.Equals("Barman", StringComparison.OrdinalIgnoreCase)
            || role.Equals("Bartender", StringComparison.OrdinalIgnoreCase)
            || role.Equals("Sous Chef", StringComparison.OrdinalIgnoreCase))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Kitchen");
        }
    }
}
