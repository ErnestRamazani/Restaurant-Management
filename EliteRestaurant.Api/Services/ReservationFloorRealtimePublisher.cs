using EliteRestaurant.Contracts.Floor;
using EliteRestaurant.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace EliteRestaurant.Api.Services;

public sealed class ReservationFloorRealtimePublisher(IHubContext<ReservationFloorHub> hub)
{
    public Task PublishFloorAsync(FloorSnapshotDto snapshot, CancellationToken cancellationToken = default) =>
        hub.Clients.Group("Floor").SendAsync("floorUpdated", snapshot, cancellationToken: cancellationToken);
}
