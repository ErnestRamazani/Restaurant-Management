using EliteRestaurant.Contracts.Floor;
using EliteRestaurant.Api.Services;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Reservations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/public/floor")]
[AllowAnonymous]
public sealed class PublicFloorReservationController(
    AppDbContext db,
    ReservationSchedulingService scheduling,
    ReservationFloorRealtimePublisher realtime,
    FloorSnapshotBuilder snapshotBuilder) : ControllerBase
{
    [HttpPost("book")]
    [EnableRateLimiting("PublicMenuDraft")]
    [ProducesResponseType(typeof(PublicBookFloorResponse), 200)]
    [ProducesResponseType(typeof(PublicFloorConflictDto), 409)]
    public async Task<ActionResult> Book([FromBody] PublicBookFloorRequest request, CancellationToken cancellationToken)
    {
        var placement = await db.PlacementUnits.FirstOrDefaultAsync(p => p.Id == request.PlacementUnitId, cancellationToken);
        if (placement is null)
            return NotFound(new { message = "Table placement not found." });

        if (request.PartySize < placement.MinPartyCapacity || request.PartySize > placement.MaxPartyCapacity)
            return BadRequest(new { message = "Party size is out of range for this table." });

        if (!string.Equals(placement.Status, PlacementUnitStatuses.Available, StringComparison.OrdinalIgnoreCase))
            return Conflict(new PublicFloorConflictDto(true, null, "This table is not available for new bookings right now."));

        var end = request.PlannedEndUtc ?? scheduling.DefaultEndUtc(request.PlannedStartUtc);
        if (end <= request.PlannedStartUtc)
            return BadRequest(new { message = "End time must be after start time." });

        var conflict = await scheduling.DetectConflictAsync(
            placement.Id,
            request.PlannedStartUtc,
            end,
            excludeEngagementId: null,
            cancellationToken);

        if (conflict.HasConflict)
        {
            return Conflict(new PublicFloorConflictDto(
                true,
                conflict.ConflictingEngagementIds,
                "That time conflicts with an existing reservation."));
        }

        var now = DateTime.UtcNow;
        var engagement = new ReservationEngagement
        {
            PlacementUnitId = placement.Id,
            TableId = placement.TableId,
            PlannedStartUtc = request.PlannedStartUtc,
            PlannedEndUtc = end,
            GuestName = request.GuestName.Trim(),
            GuestPhone = request.GuestPhone.Trim(),
            GuestEmail = (request.GuestEmail ?? string.Empty).Trim(),
            PartySize = request.PartySize,
            UserNotes = (request.UserNotes ?? string.Empty).Trim(),
            Status = ReservationEngagementStatuses.Scheduled,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        placement.Status = PlacementUnitStatuses.Reserved;

        db.ReservationEngagements.Add(engagement);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await realtime.PublishFloorAsync(await snapshotBuilder.BuildAsync(cancellationToken), cancellationToken);
        }
        catch
        {
            // booking succeeded; realtime is best-effort
        }

        return Ok(new PublicBookFloorResponse(engagement.Id, engagement.PlannedStartUtc, engagement.PlannedEndUtc));
    }

    [HttpPost("availability")]
    [EnableRateLimiting("PublicMenuRead")]
    [ProducesResponseType(typeof(IReadOnlyList<SuggestedSlotDto>), 200)]
    public async Task<ActionResult<IReadOnlyList<SuggestedSlotDto>>> Availability(
        [FromBody] PublicAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        var maxSlots = Math.Clamp(request.MaxSlots, 1, 48);
        var starts = await scheduling.SuggestSlotStartsUtcAsync(
            request.PlacementUnitId,
            request.PartySize,
            request.RangeStartUtc,
            request.RangeEndUtc,
            maxSlots,
            cancellationToken);

        if (starts.Count == 0)
            return Ok(Array.Empty<SuggestedSlotDto>());

        var slotDuration = scheduling.DefaultEndUtc(starts[0]) - starts[0];
        var dtos = starts.Select(s => new SuggestedSlotDto(s, s + slotDuration)).ToList();
        return Ok(dtos);
    }

    [HttpPost("suggest")]
    [EnableRateLimiting("PublicMenuRead")]
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
}
