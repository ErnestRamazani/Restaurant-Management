using Npgsql;

namespace EliteRestaurant.Core.Utils;

/// <summary>Migrates legacy plaintext <see cref="DatabaseSettings.PostgreSqlConnectionString"/> to structured fields + DPAPI password.</summary>
public static class DatabaseSettingsMigration
{
    /// <returns>True if settings were changed and should be persisted.</returns>
    public static bool TryMigrateInMemory(DatabaseSettings db)
    {
        var legacy = db.PostgreSqlConnectionString?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(legacy))
            return false;

        if (!string.IsNullOrWhiteSpace(db.PostgreSqlHost))
            return false;

        try
        {
            var b = new NpgsqlConnectionStringBuilder(legacy);
            db.PostgreSqlHost = (b.Host ?? string.Empty).Trim();
            db.PostgreSqlPort = b.Port > 0 ? b.Port : 5432;
            db.PostgreSqlDatabase = (b.Database ?? string.Empty).Trim();
            db.PostgreSqlUsername = (b.Username ?? string.Empty).Trim();
            var pw = b.Password ?? string.Empty;
            if (!string.IsNullOrEmpty(pw))
            {
                if (!DatabaseConnectionSecret.IsDpapiAvailable)
                    return false;
                db.PostgreSqlPasswordProtected = DatabaseConnectionSecret.ProtectUtf8(pw);
            }

            db.PostgreSqlConnectionString = null;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
