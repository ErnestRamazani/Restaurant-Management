using EliteRestaurant.Contracts.Floor;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Reservations;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Api.Services;

public sealed class FloorSnapshotBuilder(AppDbContext db)
{
    public async Task<FloorSnapshotDto> BuildAsync(CancellationToken cancellationToken = default)
    {
        var placements = await db.PlacementUnits.AsNoTracking()
            .Include(p => p.Table)
            .OrderBy(p => p.LayoutY)
            .ThenBy(p => p.LayoutX)
            .ToListAsync(cancellationToken);

        var horizon = DateTime.UtcNow.AddDays(-1);
        var engagements = await db.ReservationEngagements.AsNoTracking()
            .Include(e => e.Table)
            .Where(e =>
                e.PlannedEndUtc >= horizon
                && e.Status != ReservationEngagementStatuses.Cancelled
                && e.Status != ReservationEngagementStatuses.NoShow)
            .OrderBy(e => e.PlannedStartUtc)
            .ToListAsync(cancellationToken);

        var placementDtos = placements.Select(ToPlacementDto).ToList();
        var engagementDtos = engagements.Select(ToEngagementDto).ToList();
        return new FloorSnapshotDto(placementDtos, engagementDtos);
    }

    private static FloorPlacementDto ToPlacementDto(PlacementUnit p)
    {
        var name = p.Table != null && !string.IsNullOrWhiteSpace(p.Table.Name)
            ? p.Table.Name
            : $"Table #{p.TableId}";

        return new FloorPlacementDto(
            p.Id,
            p.TableId,
            name,
            p.MinPartyCapacity,
            p.MaxPartyCapacity,
            p.LayoutX,
            p.LayoutY,
            p.Status,
            p.MergeClusterKey);
    }

    private static FloorEngagementDto ToEngagementDto(ReservationEngagement e)
    {
        var name = e.Table != null && !string.IsNullOrWhiteSpace(e.Table.Name)
            ? e.Table.Name
            : $"Table #{e.TableId}";

        return new FloorEngagementDto(
            e.Id,
            e.ConfirmationCode,
            e.PlacementUnitId,
            e.TableId,
            name,
            e.PlannedStartUtc,
            e.PlannedEndUtc,
            e.ActualStartUtc,
            e.ActualEndUtc,
            e.GuestName,
            e.GuestPhone,
            e.PartySize,
            e.Status,
            e.RotationOrOverstayFlag);
    }
}
