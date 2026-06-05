using Npgsql;

namespace EliteRestaurant.Core.Utils;

/// <summary>Builds a PostgreSQL connection string from <see cref="DatabaseSettings"/> (structured + DPAPI password) or legacy plaintext.</summary>
public static class DatabaseSettingsResolver
{
    /// <summary>
    /// Accepts either an Npgsql key/value connection string or a PostgreSQL URL such as DigitalOcean's DATABASE_URL format.
    /// </summary>
    public static bool TryNormalizePostgreSqlConnectionString(
        string? raw,
        out string connectionString,
        bool ensureCloudSsl = false)
    {
        connectionString = string.Empty;
        raw = raw?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        if (!raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return TryBuildConnectionString(raw, ensureCloudSsl, out connectionString);
        }

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)
            || (uri.Scheme != "postgres" && uri.Scheme != "postgresql"))
        {
            return false;
        }

        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty,
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty
        };

        foreach (var (key, value) in ParseQuery(uri.Query))
        {
            if (key.Equals("sslmode", StringComparison.OrdinalIgnoreCase))
            {
                builder.SslMode = Enum.TryParse<SslMode>(value, ignoreCase: true, out var sslMode)
                    ? sslMode
                    : builder.SslMode;
            }
        }

        EnsureCloudSsl(builder, ensureCloudSsl);
        ApplyProductionPoolTuning(builder);
        connectionString = builder.ConnectionString;
        return true;
    }

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
            return TryNormalizePostgreSqlConnectionString(legacy, out connectionString);
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
        ApplyProductionPoolTuning(b);
        connectionString = b.ConnectionString;
        return true;
    }

    private static IEnumerable<(string Key, string Value)> ParseQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            yield break;

        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            var key = Uri.UnescapeDataString(pair[0]);
            var value = pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : string.Empty;
            yield return (key, value);
        }
    }

    private static bool TryBuildConnectionString(string raw, bool ensureCloudSsl, out string connectionString)
    {
        connectionString = string.Empty;
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(raw);
            EnsureCloudSsl(builder, ensureCloudSsl);
            ApplyProductionPoolTuning(builder);
            connectionString = builder.ConnectionString;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void EnsureCloudSsl(NpgsqlConnectionStringBuilder builder, bool ensureCloudSsl)
    {
        if (!ensureCloudSsl)
            return;

        if (builder.SslMode is SslMode.Disable or SslMode.Allow or SslMode.Prefer)
            builder.SslMode = SslMode.Require;
    }

    private static void ApplyProductionPoolTuning(NpgsqlConnectionStringBuilder builder)
    {
        if (builder.MaxPoolSize is 0 or > 25)
            builder.MaxPoolSize = 25;
        if (builder.MinPoolSize < 2)
            builder.MinPoolSize = 2;
        builder.ConnectionIdleLifetime = 300;
    }
}
