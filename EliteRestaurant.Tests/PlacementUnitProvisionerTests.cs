using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Reservations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EliteRestaurant.Tests;

public sealed class PlacementUnitProvisionerTests
{
    [Fact]
    public async Task EnsureForTableAsync_CreatesPlacementUnit_WhenMissing()
    {
        await using var db = CreateDb();
        var table = new Table
        {
            UniqueId = "TBL-TEST-01",
            TableNumber = 12,
            Name = "Patio 12",
            Capacity = 6,
            Status = "Available",
        };
        db.Tables.Add(table);
        await db.SaveChangesAsync();

        await PlacementUnitProvisioner.EnsureForTableAsync(db, table);
        await db.SaveChangesAsync();

        var placement = await db.PlacementUnits.SingleAsync(p => p.TableId == table.Id);
        Assert.Equal(1, placement.MinPartyCapacity);
        Assert.Equal(6, placement.MaxPartyCapacity);
        Assert.Equal(PlacementUnitStatuses.Available, placement.Status);
    }

    [Fact]
    public async Task EnsureForTableAsync_UpdatesMaxCapacity_WhenTableCapacityChanges()
    {
        await using var db = CreateDb();
        var table = new Table
        {
            UniqueId = "TBL-TEST-02",
            TableNumber = 3,
            Name = "Booth",
            Capacity = 4,
            Status = "Available",
        };
        db.Tables.Add(table);
        await db.SaveChangesAsync();
        await PlacementUnitProvisioner.EnsureForTableAsync(db, table);
        await db.SaveChangesAsync();

        table.Capacity = 8;
        db.Tables.Update(table);
        await db.SaveChangesAsync();

        await PlacementUnitProvisioner.EnsureForTableAsync(db, table);
        await db.SaveChangesAsync();

        var placement = await db.PlacementUnits.SingleAsync(p => p.TableId == table.Id);
        Assert.Equal(8, placement.MaxPartyCapacity);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
