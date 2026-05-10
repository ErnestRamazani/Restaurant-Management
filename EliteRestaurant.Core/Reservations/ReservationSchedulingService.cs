using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EliteRestaurant.Core.Reservations;

public sealed class ReservationSchedulingService(
    AppDbContext db,
    IOptions<ReservationSchedulingOptions> options,
    PlacementUnitClusterResolver clusters)
{
    private readonly ReservationSchedulingOptions _opt = options.Value;

    public DateTime DefaultEndUtc(DateTime startUtc) =>
        startUtc.AddMinutes(_opt.DefaultDurationMinutes);

    public async Task<ReservationConflictDetectionResult> DetectConflictAsync(
        int placementUnitId,
        DateTime plannedStartUtc,
        DateTime plannedEndUtc,
        int? excludeEngagementId,
        CancellationToken cancellationToken = default)
    {
        var cluster = await clusters.ResolveClusterIdsAsync(placementUnitId, cancellationToken);
        var clusterMap = await clusters.LoadAllClustersAsync(cancellationToken);
        var buffer = TimeSpan.FromMinutes(_opt.BufferMinutes);

        var candidates = await db.ReservationEngagements.AsNoTracking()
            .Where(e =>
                (e.Status == ReservationEngagementStatuses.Scheduled || e.Status == ReservationEngagementStatuses.CheckedIn)
                && e.PlannedStartUtc < plannedEndUtc.Add(buffer)
                && e.PlannedEndUtc > plannedStartUtc.Add(-buffer))
            .Select(e => new { e.Id, e.PlacementUnitId, e.PlannedStartUtc, e.PlannedEndUtc, e.Status })
            .ToListAsync(cancellationToken);

        var conflicts = new List<int>();
        foreach (var c in candidates)
        {
            if (excludeEngagementId is int ex && ex == c.Id)
                continue;

            if (!clusterMap.TryGetValue(c.PlacementUnitId, out var otherCluster))
                otherCluster = new HashSet<int> { c.PlacementUnitId };

            if (!clusters.ClustersIntersect(cluster, otherCluster))
                continue;

            if (!ReservationOverlapMath.IntervalsOverlap(
                    c.PlannedStartUtc,
                    c.PlannedEndUtc,
                    plannedStartUtc,
                    plannedEndUtc,
                    buffer))
                continue;

            conflicts.Add(c.Id);
        }

        return new ReservationConflictDetectionResult
        {
            HasConflict = conflicts.Count > 0,
            ConflictingEngagementIds = conflicts
        };
    }

    /// <summary>
    /// Best-effort ranked placements for party size and window (no cluster expansion for ranking distance).
    /// </summary>
    public async Task<IReadOnlyList<PlacementSuggestion>> SuggestPlacementsAsync(
        int partySize,
        DateTime plannedStartUtc,
        DateTime plannedEndUtc,
        CancellationToken cancellationToken = default)
    {
        var units = await db.PlacementUnits.AsNoTracking()
            .Include(p => p.Table)
            .Where(p => p.MinPartyCapacity <= partySize && p.MaxPartyCapacity >= partySize)
            .OrderBy(p => p.LayoutY)
            .ThenBy(p => p.LayoutX)
            .ToListAsync(cancellationToken);

        var results = new List<PlacementSuggestion>();
        foreach (var u in units)
        {
            var conflict = await DetectConflictAsync(u.Id, plannedStartUtc, plannedEndUtc, null, cancellationToken);
            if (conflict.HasConflict)
                continue;

            var tableName = u.Table != null && !string.IsNullOrWhiteSpace(u.Table.Name)
                ? u.Table.Name
                : $"Table #{u.TableId}";

            results.Add(new PlacementSuggestion(
                u.Id,
                u.TableId,
                tableName,
                u.LayoutX,
                u.LayoutY,
                Math.Abs(u.MaxPartyCapacity - partySize)));
        }

        return results
            .OrderBy(r => r.PartyFitPenalty)
            .ThenBy(r => r.LayoutY)
            .ThenBy(r => r.LayoutX)
            .ToList();
    }

    public async Task<IReadOnlyList<DateTime>> SuggestSlotStartsUtcAsync(
        int placementUnitId,
        int partySize,
        DateTime rangeStartUtc,
        DateTime rangeEndUtc,
        int maxSlots,
        CancellationToken cancellationToken = default)
    {
        var step = TimeSpan.FromMinutes(Math.Max(5, _opt.SuggestionSlotStepMinutes));
        var duration = TimeSpan.FromMinutes(_opt.DefaultDurationMinutes);
        var slots = new List<DateTime>();
        for (var t = rangeStartUtc; t.Add(duration) <= rangeEndUtc && slots.Count < maxSlots; t += step)
        {
            var end = t.Add(duration);
            var conflict = await DetectConflictAsync(placementUnitId, t, end, null, cancellationToken);
            if (conflict.HasConflict)
                continue;

            var unit = await db.PlacementUnits.AsNoTracking().FirstOrDefaultAsync(p => p.Id == placementUnitId, cancellationToken);
            if (unit is null || unit.MinPartyCapacity > partySize || unit.MaxPartyCapacity < partySize)
                continue;

            slots.Add(t);
        }

        return slots;
    }
}

public readonly record struct PlacementSuggestion(
    int PlacementUnitId,
    int TableId,
    string TableDisplayName,
    int LayoutX,
    int LayoutY,
    int PartyFitPenalty);
