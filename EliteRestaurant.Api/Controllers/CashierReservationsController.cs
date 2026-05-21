using EliteRestaurant.Api.Branding;
using EliteRestaurant.Api.Dtos;
using EliteRestaurant.Api.Security;
using EliteRestaurant.Api.Services;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Reservations;
using EliteRestaurant.Core.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/cashier/reservations")]
[Authorize(Policy = "CashierOrAdmin")]
public sealed class CashierReservationsController(
    TabletAuthService authService,
    AppDbContext db,
    ReservationSchedulingService scheduling,
    ReservationFloorRealtimePublisher realtime,
    FloorSnapshotBuilder snapshotBuilder,
    IOptions<ReservationSchedulingOptions> schedulingOptions) : ControllerBase
{
    [HttpGet("scheduling")]
    public ActionResult<CashierReservationSchedulingDto> GetScheduling()
    {
        if (RequireCashierOrAdminSession() is null)
            return Unauthorized(new { message = "Missing/invalid token or non-cashier role." });

        var (leadDays, maxMonthsAhead) = PublicMenuReservationSettings.Resolve(db);
        var opt = schedulingOptions.Value;
        return Ok(new CashierReservationSchedulingDto(
            leadDays,
            maxMonthsAhead,
            opt.BufferMinutes,
            opt.DefaultDurationMinutes,
            opt.SuggestionSlotStepMinutes,
            opt.SuggestionHorizonDays));
    }

    [HttpPost("engagements")]
    public async Task<ActionResult<CashierCreateWalkInEngagementResponse>> CreateWalkIn(
        [FromBody] CashierCreateWalkInEngagementRequest request,
        CancellationToken cancellationToken)
    {
        if (RequireCashierOrAdminSession() is null)
            return Unauthorized(new { message = "Missing/invalid token or non-cashier role." });

        var guestName = (request.GuestName ?? string.Empty).Trim();
        var guestPhone = (request.GuestPhone ?? string.Empty).Trim();
        if (guestName.Length is < 1 or > 80)
            return BadRequest(new { message = "Guest name is required (1–80 characters)." });
        if (guestPhone.Length is < 5 or > 40)
            return BadRequest(new { message = "Phone is required (5–40 characters)." });
        var guestEmail = (request.GuestEmail ?? string.Empty).Trim();
        if (guestEmail.Length > 120)
            return BadRequest(new { message = "Email must be 120 characters or fewer." });
        if (request.PartySize < 1 || request.PartySize > 99)
            return BadRequest(new { message = "Party size must be between 1 and 99." });

        if (TryGetWalkInScheduleError(request.PlannedStartUtc, out var scheduleErr))
            return BadRequest(new { message = scheduleErr });

        var end = request.PlannedEndUtc ?? scheduling.DefaultEndUtc(request.PlannedStartUtc);
        if (end <= request.PlannedStartUtc)
            return BadRequest(new { message = "End time must be after start time." });

        PlacementUnit? placement = null;
        if (request.PlacementUnitId is > 0)
        {
            placement = await db.PlacementUnits.FirstOrDefaultAsync(p => p.Id == request.PlacementUnitId, cancellationToken);
            if (placement is null)
                return NotFound(new { message = "Table placement not found." });
        }
        else if (request.TableId is > 0)
        {
            placement = await db.PlacementUnits
                .Where(p => p.TableId == request.TableId)
                .OrderBy(p => p.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (placement is null)
                return NotFound(new { message = "No floor placement for that table." });
        }

        if (placement is not null)
        {
            if (request.PartySize < placement.MinPartyCapacity || request.PartySize > placement.MaxPartyCapacity)
                return BadRequest(new { message = "Party size is out of range for this table." });

            if (!string.Equals(placement.Status, PlacementUnitStatuses.Available, StringComparison.OrdinalIgnoreCase))
                return Conflict(new { message = "This table is not available for new bookings right now." });

            var conflict = await scheduling.DetectConflictAsync(
                placement.Id,
                request.PlannedStartUtc,
                end,
                excludeEngagementId: null,
                cancellationToken);
            if (conflict.HasConflict)
            {
                return Conflict(new
                {
                    message = "That time conflicts with another reservation on this or a linked table.",
                    conflictingEngagementIds = conflict.ConflictingEngagementIds
                });
            }
        }

        var now = DateTime.UtcNow;
        var engagement = new ReservationEngagement
        {
            PlacementUnitId = placement?.Id ?? 0,
            TableId = placement?.TableId ?? (request.TableId ?? 0),
            PlannedStartUtc = request.PlannedStartUtc,
            PlannedEndUtc = end,
            GuestName = guestName,
            GuestPhone = guestPhone,
            GuestEmail = guestEmail,
            PartySize = request.PartySize,
            UserNotes = (request.UserNotes ?? string.Empty).Trim(),
            Status = ReservationEngagementStatuses.Scheduled,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        if (placement is not null)
            placement.Status = PlacementUnitStatuses.Reserved;

        db.ReservationEngagements.Add(engagement);
        await db.SaveChangesAsync(cancellationToken);
        await PublishFloorAsync(cancellationToken);

        return Ok(new CashierCreateWalkInEngagementResponse(
            engagement.Id,
            engagement.PlannedStartUtc,
            engagement.PlannedEndUtc));
    }

    [HttpGet("engagements")]
    public async Task<ActionResult<IReadOnlyList<CashierEngagementListRow>>> ListEngagements(CancellationToken cancellationToken)
    {
        if (RequireCashierOrAdminSession() is null)
            return Unauthorized(new { message = "Missing/invalid token or non-cashier role." });

        var rows = await db.ReservationEngagements
            .AsNoTracking()
            .Include(e => e.Table)
            .Where(e => e.Status == ReservationEngagementStatuses.Scheduled
                        || e.Status == ReservationEngagementStatuses.CheckedIn)
            .OrderBy(e => e.PlannedStartUtc)
            .Take(200)
            .Select(e => new CashierEngagementListRow(
                e.Id,
                e.Status,
                e.GuestName,
                e.GuestPhone,
                e.PartySize,
                e.PlannedStartUtc,
                e.PlannedEndUtc,
                e.Table != null && !string.IsNullOrWhiteSpace(e.Table.Name)
                    ? $"Table {e.Table.TableNumber} · {e.Table.Name}"
                    : (e.TableId > 0 ? $"Table #{e.TableId}" : "—"),
                e.PlacementUnitId))
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    [HttpGet("engagements/{id:int}")]
    public async Task<ActionResult<CashierEngagementDetailDto>> GetEngagement(int id, CancellationToken cancellationToken)
    {
        if (RequireCashierOrAdminSession() is null)
            return Unauthorized(new { message = "Missing/invalid token or non-cashier role." });

        var e = await db.ReservationEngagements
            .AsNoTracking()
            .Include(x => x.Table)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (e is null)
            return NotFound(new { message = "Reservation not found." });

        var tableLabel = e.Table is { } t && !string.IsNullOrWhiteSpace(t.Name)
            ? $"Table {t.TableNumber} · {t.Name}"
            : (e.TableId > 0 ? $"Table #{e.TableId}" : "—");

        return Ok(new CashierEngagementDetailDto(
            e.Id,
            e.Status,
            e.GuestName,
            e.GuestPhone,
            e.GuestEmail ?? string.Empty,
            e.PartySize,
            e.UserNotes ?? string.Empty,
            e.PlannedStartUtc,
            e.PlannedEndUtc,
            e.ActualStartUtc,
            e.ActualEndUtc,
            e.TableId,
            tableLabel,
            e.PlacementUnitId,
            e.CreatedAtUtc,
            e.UpdatedAtUtc));
    }

    [HttpPost("engagements/{id:int}/cancel")]
    public async Task<ActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        if (RequireCashierOrAdminSession() is null)
            return Unauthorized(new { message = "Missing/invalid token or non-cashier role." });

        var engagement = await db.ReservationEngagements
            .Include(e => e.PlacementUnit)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (engagement is null)
            return NotFound(new { message = "Reservation not found." });
        if (!string.Equals(engagement.Status, ReservationEngagementStatuses.Scheduled, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only scheduled reservations can be cancelled." });

        var now = DateTime.UtcNow;
        engagement.Status = ReservationEngagementStatuses.Cancelled;
        engagement.UpdatedAtUtc = now;
        ReleaseReservedPlacementIfNeeded(engagement);
        await db.SaveChangesAsync(cancellationToken);
        await PublishFloorAsync(cancellationToken);
        return Ok(new { ok = true });
    }

    [HttpPost("engagements/{id:int}/no-show")]
    public async Task<ActionResult> NoShow(int id, CancellationToken cancellationToken)
    {
        if (RequireCashierOrAdminSession() is null)
            return Unauthorized(new { message = "Missing/invalid token or non-cashier role." });

        var engagement = await db.ReservationEngagements
            .Include(e => e.PlacementUnit)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (engagement is null)
            return NotFound(new { message = "Reservation not found." });
        if (!string.Equals(engagement.Status, ReservationEngagementStatuses.Scheduled, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only scheduled reservations can be marked as no-show." });

        var now = DateTime.UtcNow;
        engagement.Status = ReservationEngagementStatuses.NoShow;
        engagement.UpdatedAtUtc = now;
        ReleaseReservedPlacementIfNeeded(engagement);
        await db.SaveChangesAsync(cancellationToken);
        await PublishFloorAsync(cancellationToken);
        return Ok(new { ok = true });
    }

    [HttpPost("engagements/{id:int}/arrived")]
    public async Task<ActionResult> Arrived(int id, CancellationToken cancellationToken)
    {
        if (RequireCashierOrAdminSession() is null)
            return Unauthorized(new { message = "Missing/invalid token or non-cashier role." });

        var engagement = await db.ReservationEngagements
            .Include(e => e.PlacementUnit)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (engagement is null)
            return NotFound(new { message = "Reservation not found." });
        if (!string.Equals(engagement.Status, ReservationEngagementStatuses.Scheduled, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only scheduled reservations can be marked as arrived (check-in)." });

        var now = DateTime.UtcNow;
        engagement.Status = ReservationEngagementStatuses.CheckedIn;
        engagement.ActualStartUtc = now;
        engagement.RotationOrOverstayFlag = false;
        engagement.UpdatedAtUtc = now;

        if (engagement.PlacementUnit is not null)
            engagement.PlacementUnit.Status = PlacementUnitStatuses.Occupied;

        await db.SaveChangesAsync(cancellationToken);
        await PublishFloorAsync(cancellationToken);
        return Ok(new { ok = true });
    }

    [HttpPost("engagements/{id:int}/reschedule")]
    public async Task<ActionResult> Reschedule(int id, [FromBody] CashierRescheduleEngagementRequest request, CancellationToken cancellationToken)
    {
        if (RequireCashierOrAdminSession() is null)
            return Unauthorized(new { message = "Missing/invalid token or non-cashier role." });

        var engagement = await db.ReservationEngagements
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (engagement is null)
            return NotFound(new { message = "Reservation not found." });
        if (!string.Equals(engagement.Status, ReservationEngagementStatuses.Scheduled, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only scheduled reservations can be rescheduled." });

        var end = request.PlannedEndUtc ?? scheduling.DefaultEndUtc(request.PlannedStartUtc);
        if (end <= request.PlannedStartUtc)
            return BadRequest(new { message = "End time must be after start time." });

        var conflict = await scheduling.DetectConflictAsync(
            engagement.PlacementUnitId,
            request.PlannedStartUtc,
            end,
            engagement.Id,
            cancellationToken);
        if (conflict.HasConflict)
        {
            return Conflict(new
            {
                message = "That time conflicts with another reservation on this or a linked table.",
                conflictingEngagementIds = conflict.ConflictingEngagementIds
            });
        }

        var now = DateTime.UtcNow;
        engagement.PlannedStartUtc = request.PlannedStartUtc;
        engagement.PlannedEndUtc = end;
        engagement.UpdatedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        await PublishFloorAsync(cancellationToken);
        return Ok(new { ok = true });
    }

    private static void ReleaseReservedPlacementIfNeeded(ReservationEngagement engagement)
    {
        var placement = engagement.PlacementUnit;
        if (placement is null) return;
        if (string.Equals(placement.Status, PlacementUnitStatuses.Reserved, StringComparison.OrdinalIgnoreCase))
            placement.Status = PlacementUnitStatuses.Available;
    }

    private async Task PublishFloorAsync(CancellationToken cancellationToken)
    {
        try
        {
            await realtime.PublishFloorAsync(await snapshotBuilder.BuildAsync(cancellationToken), cancellationToken);
        }
        catch
        {
            //
        }
    }

    /// <summary>Walk-in desk bookings: must be in the future, within max horizon; no online lead-time delay.</summary>
    private bool TryGetWalkInScheduleError(DateTime plannedStartUtc, out string? message)
    {
        message = null;
        var startUtc = plannedStartUtc.Kind switch
        {
            DateTimeKind.Utc => plannedStartUtc,
            DateTimeKind.Local => plannedStartUtc.ToUniversalTime(),
            _ => DateTime.SpecifyKind(plannedStartUtc, DateTimeKind.Utc)
        };

        if (startUtc <= DateTime.UtcNow)
        {
            message = "Reservation time must be in the future.";
            return true;
        }

        var startDay = startUtc.Date;
        var todayUtc = DateTime.UtcNow.Date;
        if (startDay < todayUtc)
        {
            message = "You cannot reserve a past date.";
            return true;
        }

        var (_, maxMonthsAhead) = PublicMenuReservationSettings.Resolve(db);
        var latestAllowed = todayUtc.AddMonths(maxMonthsAhead);
        if (startDay > latestAllowed)
        {
            message = $"Reservations may be booked up to {maxMonthsAhead} month(s) ahead.";
            return true;
        }

        return false;
    }

    private AuthenticatedStaffSession? RequireCashierOrAdminSession()
    {
        var token = Request.ReadBearerToken();
        var session = authService.Validate(token);
        if (session is null)
            return null;
        return StaffPortalAuthentication.IsReceptionRole(session.Role) ? session : null;
    }
}
