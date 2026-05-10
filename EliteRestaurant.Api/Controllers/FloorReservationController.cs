using EliteRestaurant.Contracts.Floor;
using EliteRestaurant.Api.Services;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Reservations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/floor")]
[Authorize(Policy = "StaffAny")]
public sealed class FloorReservationController(
    AppDbContext db,
    ReservationSchedulingService scheduling,
    ReservationFloorRealtimePublisher realtime,
    FloorSnapshotBuilder snapshotBuilder,
    IOptions<ReservationSchedulingOptions> schedulingOptions) : ControllerBase
{
    [HttpGet("snapshot")]
    [ProducesResponseType(typeof(FloorSnapshotDto), 200)]
    public async Task<ActionResult<FloorSnapshotDto>> GetSnapshot(CancellationToken cancellationToken)
    {
        var dto = await snapshotBuilder.BuildAsync(cancellationToken);
        return Ok(dto);
    }

    [HttpPost("engagements/{id:int}/check-in")]
    public async Task<ActionResult> CheckIn(int id, CancellationToken cancellationToken)
    {
        var engagement = await db.ReservationEngagements
            .Include(e => e.PlacementUnit)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (engagement is null)
            return NotFound();

        if (!string.Equals(engagement.Status, ReservationEngagementStatuses.Scheduled, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only scheduled engagements can check in." });

        var now = DateTime.UtcNow;
        engagement.Status = ReservationEngagementStatuses.CheckedIn;
        engagement.ActualStartUtc = now;
        engagement.RotationOrOverstayFlag = false;
        engagement.UpdatedAtUtc = now;

        if (engagement.PlacementUnit is not null)
        {
            engagement.PlacementUnit.Status = PlacementUnitStatuses.Occupied;
        }

        await db.SaveChangesAsync(cancellationToken);
        await realtime.PublishFloorAsync(await snapshotBuilder.BuildAsync(cancellationToken), cancellationToken);
        return Ok();
    }

    [HttpPost("engagements/{id:int}/release")]
    public async Task<ActionResult> Release(int id, CancellationToken cancellationToken)
    {
        var engagement = await db.ReservationEngagements
            .Include(e => e.PlacementUnit)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (engagement is null)
            return NotFound();

        if (!string.Equals(engagement.Status, ReservationEngagementStatuses.CheckedIn, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only checked-in engagements can be released." });

        var now = DateTime.UtcNow;
        engagement.Status = ReservationEngagementStatuses.Completed;
        engagement.ActualEndUtc = now;
        engagement.RotationOrOverstayFlag = false;
        engagement.UpdatedAtUtc = now;

        if (engagement.PlacementUnit is not null)
            engagement.PlacementUnit.Status = PlacementUnitStatuses.ToClean;

        await db.SaveChangesAsync(cancellationToken);
        await realtime.PublishFloorAsync(await snapshotBuilder.BuildAsync(cancellationToken), cancellationToken);
        return Ok();
    }

    [HttpPost("placements/{id:int}/mark-clean")]
    public async Task<ActionResult> MarkClean(int id, CancellationToken cancellationToken)
    {
        var placement = await db.PlacementUnits.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (placement is null)
            return NotFound();

        if (!string.Equals(placement.Status, PlacementUnitStatuses.ToClean, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Placement is not waiting for cleaning." });

        placement.Status = PlacementUnitStatuses.Available;
        await db.SaveChangesAsync(cancellationToken);
        await realtime.PublishFloorAsync(await snapshotBuilder.BuildAsync(cancellationToken), cancellationToken);
        return Ok();
    }

    [HttpPost("placements/merge")]
    public async Task<ActionResult> Merge([FromBody] MergePlacementsRequest request, CancellationToken cancellationToken)
    {
        if (request.PlacementUnitIds.Count < 2)
            return BadRequest(new { message = "Select at least two placements to merge." });

        var ids = request.PlacementUnitIds.Distinct().ToList();
        var placements = await db.PlacementUnits.Where(p => ids.Contains(p.Id)).ToListAsync(cancellationToken);
        if (placements.Count != ids.Count)
            return BadRequest(new { message = "Unknown placement id." });

        var clusterKey = string.IsNullOrWhiteSpace(request.ClusterKey)
            ? $"merge-{Guid.NewGuid():N}"
            : request.ClusterKey!.Trim();

        if (!await ValidateNoTimeOverlapInClusterAsync(ids, cancellationToken))
            return Conflict(new { message = "Cannot merge: active engagements on these placements overlap in time." });

        foreach (var p in placements)
        {
            p.MergeClusterKey = clusterKey;
        }

        await db.SaveChangesAsync(cancellationToken);
        await realtime.PublishFloorAsync(await snapshotBuilder.BuildAsync(cancellationToken), cancellationToken);
        return Ok(new { mergeClusterKey = clusterKey });
    }

    [HttpPost("placements/unmerge")]
    public async Task<ActionResult> Unmerge([FromBody] IReadOnlyList<int> placementUnitIds, CancellationToken cancellationToken)
    {
        var ids = placementUnitIds.Distinct().ToList();
        if (ids.Count == 0)
            return BadRequest();

        var placements = await db.PlacementUnits.Where(p => ids.Contains(p.Id)).ToListAsync(cancellationToken);
        foreach (var p in placements)
            p.MergeClusterKey = null;

        await db.SaveChangesAsync(cancellationToken);
        await realtime.PublishFloorAsync(await snapshotBuilder.BuildAsync(cancellationToken), cancellationToken);
        return Ok();
    }

    [HttpPost("suggest")]
    [ProducesResponseType(typeof(IReadOnlyList<PlacementSuggestionDto>), 200)]
    public async Task<ActionResult<IReadOnlyList<PlacementSuggestionDto>>> Suggest(
        [FromBody] SuggestPlacementRequest request,
        CancellationToken cancellationToken)
    {
        var suggestions = await scheduling.SuggestPlacementsAsync(
            request.PartySize,
            request.PlannedStartUtc,
            request.PlannedEndUtc,
            cancellationToken);

        var dto = suggestions
            .Select(s => new PlacementSuggestionDto(
                s.PlacementUnitId,
                s.TableId,
                s.TableDisplayName,
                s.LayoutX,
                s.LayoutY))
            .ToList();

        return Ok(dto);
    }

    private async Task<bool> ValidateNoTimeOverlapInClusterAsync(
        IReadOnlyList<int> placementUnitIds,
        CancellationToken cancellationToken)
    {
        var engagements = await db.ReservationEngagements
            .AsNoTracking()
            .Where(e =>
                placementUnitIds.Contains(e.PlacementUnitId)
                && (e.Status == ReservationEngagementStatuses.Scheduled || e.Status == ReservationEngagementStatuses.CheckedIn))
            .Select(e => new { e.Id, e.PlacementUnitId, e.PlannedStartUtc, e.PlannedEndUtc })
            .ToListAsync(cancellationToken);

        var buffer = TimeSpan.FromMinutes(schedulingOptions.Value.BufferMinutes);
        for (var i = 0; i < engagements.Count; i++)
        {
            for (var j = i + 1; j < engagements.Count; j++)
            {
                var a = engagements[i];
                var b = engagements[j];
                if (a.PlacementUnitId == b.PlacementUnitId)
                    continue;

                if (ReservationOverlapMath.IntervalsOverlap(
                        a.PlannedStartUtc,
                        a.PlannedEndUtc,
                        b.PlannedStartUtc,
                        b.PlannedEndUtc,
                        buffer))
                    return false;
            }
        }

        return true;
    }
}
