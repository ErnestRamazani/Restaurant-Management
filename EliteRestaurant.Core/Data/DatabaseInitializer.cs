using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Reservations;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EliteRestaurant.Core.Data;

/// <summary>
/// Application startup database setup: applies EF Core migrations (serialized via PostgreSQL advisory lock so API and desktop
/// can both call startup safely without interleaving DDL), then optional dev sample seeding. Prefer calling
/// <see cref="Initialize"/> once per process at startup.
/// </summary>
/// <remarks>
/// Existing databases created with the old <c>EnsureCreated</c> + raw SQL patches may already match this model.
/// If <see cref="DatabaseMigrationRunner.ApplyPendingMigrations"/> fails with "relation already exists", baseline by
/// inserting the corresponding row into <c>__EFMigrationsHistory</c> (see current <c>Migrations</c> folder name) after verifying the schema.
/// </remarks>
public static class DatabaseInitializer
{
    /// <param name="configuration">Web host configuration; supplies <c>DefaultConnection</c> so migrations match the API pool. Desktop/tools may pass null.</param>
    public static void Initialize(IConfiguration? configuration = null)
    {
        var configurationDefaultConnection = configuration?.GetConnectionString("DefaultConnection");
        DatabaseMigrationRunner.ApplyPendingMigrations(configurationDefaultConnection);

        if (!AppDbContext.TryGetPostgreSqlConnectionString(out var cs, configurationDefaultConnection)
            && !AppDbContext.TryGetDatabaseUrlLastResort(out cs))
            return;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(cs, npgsql => npgsql.EnableRetryOnFailure(5))
            .Options;
        using var db = new AppDbContext(options);
        RestaurantTenantBootstrap.EnsureDefaultRestaurant(db);
        AdminWebLoginSeed.EnsureSeeded(db);
        PendingCashierBulkRelease.ReleaseLegacyInStorePendingCashier(db);
        PlacementUnitProvisioner.EnsureAllTablesHavePlacementsAsync(db).GetAwaiter().GetResult();
        SampleDataBootstrapper.SeedIfEnabled(db);
        SharedOrderDraftStore.PurgeDraftsOlderThan(db, TimeSpan.FromDays(30));
    }
}
