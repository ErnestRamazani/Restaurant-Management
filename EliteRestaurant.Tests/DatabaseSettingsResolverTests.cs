using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Utils;
using Xunit;

namespace EliteRestaurant.Tests;

public class DatabaseSettingsResolverTests
{
    [Fact]
    public void TryNormalizePostgreSqlConnectionString_ConvertsPostgresUrl()
    {
        var ok = DatabaseSettingsResolver.TryNormalizePostgreSqlConnectionString(
            "postgresql://doadmin:secret@example.db.ondigitalocean.com:25060/defaultdb?sslmode=require",
            out var connectionString);

        Assert.True(ok);
        Assert.Contains("Host=example.db.ondigitalocean.com", connectionString);
        Assert.Contains("Port=25060", connectionString);
        Assert.Contains("Database=defaultdb", connectionString);
        Assert.Contains("Username=doadmin", connectionString);
        Assert.Contains("SSL Mode=Require", connectionString);
    }

    [Fact]
    public void TryNormalizePostgreSqlConnectionString_EnsuresCloudSsl()
    {
        var ok = DatabaseSettingsResolver.TryNormalizePostgreSqlConnectionString(
            "postgresql://doadmin:secret@example.db.ondigitalocean.com:25060/defaultdb",
            out var connectionString,
            ensureCloudSsl: true);

        Assert.True(ok);
        Assert.Contains("SSL Mode=Require", connectionString);
    }

    [Fact]
    public void TryGetPostgreSqlConnectionString_PrioritizesDatabaseUrlEnvironmentVariable()
    {
        var previousDatabaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        var previousProvider = Environment.GetEnvironmentVariable("ELITE_DB_PROVIDER");
        var previousConnection = Environment.GetEnvironmentVariable("ELITE_POSTGRES_CONNECTION");
        var previousDefaultConnection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        try
        {
            Environment.SetEnvironmentVariable(
                "DATABASE_URL",
                "postgresql://standard:secret@standard.example.com:25060/defaultdb");
            Environment.SetEnvironmentVariable("ELITE_DB_PROVIDER", null);
            Environment.SetEnvironmentVariable(
                "ELITE_POSTGRES_CONNECTION",
                "postgresql://custom:secret@custom.example.com:25060/defaultdb");
            Environment.SetEnvironmentVariable(
                "ConnectionStrings__DefaultConnection",
                "Host=local.example.com;Port=5432;Database=defaultdb;Username=local");

            var ok = AppDbContext.TryGetPostgreSqlConnectionString(out var connectionString);

            Assert.True(ok);
            Assert.Contains("Host=standard.example.com", connectionString);
            Assert.Contains("SSL Mode=Require", connectionString);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DATABASE_URL", previousDatabaseUrl);
            Environment.SetEnvironmentVariable("ELITE_DB_PROVIDER", previousProvider);
            Environment.SetEnvironmentVariable("ELITE_POSTGRES_CONNECTION", previousConnection);
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", previousDefaultConnection);
        }
    }

    [Fact]
    public void TryGetDatabaseUrlLastResort_UsesDatabaseUrlWhenPresent()
    {
        var previousDatabaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        try
        {
            Environment.SetEnvironmentVariable(
                "DATABASE_URL",
                "postgresql://fallback:secret@fallback.example.com:25060/defaultdb");

            var ok = AppDbContext.TryGetDatabaseUrlLastResort(out var connectionString);

            Assert.True(ok);
            Assert.Contains("Host=fallback.example.com", connectionString);
            Assert.Contains("SSL Mode=Require", connectionString);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DATABASE_URL", previousDatabaseUrl);
        }
    }
}
