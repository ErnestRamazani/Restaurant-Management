using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Reservations;

/// <summary>
/// Online reservations book <see cref="PlacementUnit"/> rows, not <see cref="Table"/> directly.
/// Keeps placements in sync when tables are added/updated via admin sync.
/// </summary>
public static class PlacementUnitProvisioner
{
    public static async Task EnsureForTableAsync(
        AppDbContext db,
        Table table,
        CancellationToken cancellationToken = default)
    {
        if (table.Id <= 0)
            return;

        var maxCapacity = Math.Max(table.Capacity, 1);
        var placement = await db.PlacementUnits
            .FirstOrDefaultAsync(p => p.TableId == table.Id, cancellationToken);

        if (placement is null)
        {
            db.PlacementUnits.Add(new PlacementUnit
            {
                RestaurantId = table.RestaurantId,
                TableId = table.Id,
                MinPartyCapacity = 1,
                MaxPartyCapacity = maxCapacity,
                LayoutX = (table.TableNumber % 10) * 120,
                LayoutY = (table.TableNumber / 10) * 140,
                Status = PlacementUnitStatuses.Available,
            });
            return;
        }

        if (table.RestaurantId > 0)
            placement.RestaurantId = table.RestaurantId;

        placement.MaxPartyCapacity = maxCapacity;
        if (placement.MinPartyCapacity > maxCapacity)
            placement.MinPartyCapacity = 1;
    }

    public static async Task RemoveForTableAsync(
        AppDbContext db,
        int tableId,
        CancellationToken cancellationToken = default)
    {
        var placement = await db.PlacementUnits
            .FirstOrDefaultAsync(p => p.TableId == tableId, cancellationToken);
        if (placement is null)
            return;

        var hasActiveEngagements = await db.ReservationEngagements.AnyAsync(
            e => e.PlacementUnitId == placement.Id
                 && (e.Status == ReservationEngagementStatuses.Scheduled
                     || e.Status == ReservationEngagementStatuses.CheckedIn),
            cancellationToken);
        if (hasActiveEngagements)
            throw new InvalidOperationException("This table has active reservations and cannot be deleted.");

        db.PlacementUnits.Remove(placement);
    }

    /// <summary>Backfill placements for tables created before sync provisioning existed.</summary>
    public static async Task EnsureAllTablesHavePlacementsAsync(
        AppDbContext db,
        CancellationToken cancellationToken = default)
    {
        if (!db.Database.IsRelational())
            return;

        var tables = await db.Tables.IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken);
        if (tables.Count == 0)
            return;

        var existingTableIds = await db.PlacementUnits.IgnoreQueryFilters()
            .Select(p => p.TableId)
            .ToListAsync(cancellationToken);
        var existing = existingTableIds.ToHashSet();

        foreach (var table in tables)
        {
            if (existing.Contains(table.Id))
                continue;

            await EnsureForTableAsync(db, table, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
