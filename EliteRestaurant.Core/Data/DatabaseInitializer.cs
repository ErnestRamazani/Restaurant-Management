using EliteRestaurant.Core.Utils;

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
    public static void Initialize()
    {
        DatabaseMigrationRunner.ApplyPendingMigrations();
        using var db = new AppDbContext();
        SampleDataBootstrapper.SeedIfEnabled(db);
        SharedOrderDraftStore.PurgeDraftsOlderThan(TimeSpan.FromDays(30));
    }
}
