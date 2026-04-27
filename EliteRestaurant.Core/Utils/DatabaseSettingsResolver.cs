using Npgsql;

namespace EliteRestaurant.Core.Utils;

/// <summary>Builds a PostgreSQL connection string from <see cref="DatabaseSettings"/> (structured + DPAPI password) or legacy plaintext.</summary>
public static class DatabaseSettingsResolver
{
    /// <summary>
    /// Resolves a connection string from app settings (not environment variables).
    /// </summary>
    public static bool TryBuildFromSettings(DatabaseSettings? db, out string connectionString)
    {
        connectionString = string.Empty;
        if (db is null)
            return false;

        var legacy = db.PostgreSqlConnectionString?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(legacy) && string.IsNullOrWhiteSpace(db.PostgreSqlHost))
        {
            connectionString = legacy;
            return true;
        }

        var host = db.PostgreSqlHost?.Trim() ?? string.Empty;
        var database = db.PostgreSqlDatabase?.Trim() ?? string.Empty;
        var username = db.PostgreSqlUsername?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(database) || string.IsNullOrWhiteSpace(username))
            return false;

        var port = db.PostgreSqlPort > 0 ? db.PostgreSqlPort : 5432;

        string password = string.Empty;
        var protectedPw = db.PostgreSqlPasswordProtected?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(protectedPw))
        {
            if (!DatabaseConnectionSecret.IsDpapiAvailable)
                return false;
            try
            {
                password = DatabaseConnectionSecret.UnprotectUtf8(protectedPw);
            }
            catch
            {
                return false;
            }
        }

        var b = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = database,
            Username = username,
            Password = password
        };
        connectionString = b.ConnectionString;
        return true;
    }
}
