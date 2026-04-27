using Microsoft.AspNetCore.SignalR;

namespace EliteRestaurant.Api.Hubs;

public sealed class OrderHub : Hub
{
    public Task JoinServer() => Groups.AddToGroupAsync(Context.ConnectionId, "Server");
}
