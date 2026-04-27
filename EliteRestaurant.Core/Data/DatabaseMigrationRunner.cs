using System.Data;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Data;

/// <summary>
/// Applies pending EF Core migrations and idempotent schema repair. Uses a PostgreSQL advisory lock so concurrent
/// processes (API + desktop) never run DDL in parallel. A second process blocks on the lock until the first
/// finishes — that is expected; both can run together against the same database.
/// </summary>
public static class DatabaseMigrationRunner
{
    /// <summary>Stable lock identity for EliteRestaurant schema migration (two 32-bit keys).</summary>
    private const int AdvisoryLockKey1 = 0x454C_5445; // 'ELTE' (Elite)
    private const int AdvisoryLockKey2 = 0x4442_4D47; // 'DBMG' (DB migration)

    public static void ApplyPendingMigrations()
    {
        using var db = new AppDbContext();
        var database = db.Database;
        var connection = database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen)
            connection.Open();

        try
        {
            database.ExecuteSqlRaw(
                "SELECT pg_advisory_lock({0}, {1});",
                AdvisoryLockKey1,
                AdvisoryLockKey2);
            database.Migrate();
            TabletSessionsSchemaRepair.EnsureTableExists(db);
        }
        finally
        {
            try
            {
                database.ExecuteSqlRaw(
                    "SELECT pg_advisory_unlock({0}, {1});",
                    AdvisoryLockKey1,
                    AdvisoryLockKey2);
            }
            catch
            {
                // Best-effort unlock; connection may already be torn down.
            }

            if (!wasOpen && connection.State == ConnectionState.Open)
                connection.Close();
        }
    }
}
