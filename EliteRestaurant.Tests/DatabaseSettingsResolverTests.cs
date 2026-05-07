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
    public void TryGetPostgreSqlConnectionString_PrioritizesDigitalOceanEnvironmentVariables()
    {
        var previousProvider = Environment.GetEnvironmentVariable("ELITE_DB_PROVIDER");
        var previousConnection = Environment.GetEnvironmentVariable("ELITE_POSTGRES_CONNECTION");
        try
        {
            Environment.SetEnvironmentVariable("ELITE_DB_PROVIDER", "PostgreSql");
            Environment.SetEnvironmentVariable(
                "ELITE_POSTGRES_CONNECTION",
                "postgresql://doadmin:secret@example.db.ondigitalocean.com:25060/defaultdb?sslmode=require");

            var ok = AppDbContext.TryGetPostgreSqlConnectionString(out var connectionString);

            Assert.True(ok);
            Assert.Contains("Host=example.db.ondigitalocean.com", connectionString);
            Assert.Contains("SSL Mode=Require", connectionString);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ELITE_DB_PROVIDER", previousProvider);
            Environment.SetEnvironmentVariable("ELITE_POSTGRES_CONNECTION", previousConnection);
        }
    }
}
