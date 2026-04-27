using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EliteRestaurant.Core.Data;

/// <summary>Design-time factory for <c>dotnet ef</c> migrations (uses the same connection resolution as the app).</summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        if (!AppDbContext.TryGetPostgreSqlConnectionString(out var connectionString))
        {
            throw new InvalidOperationException(
                "Cannot create DbContext for migrations: configure PostgreSQL. " +
                "Set ELITE_DB_PROVIDER=PostgreSql and ELITE_POSTGRES_CONNECTION, " +
                "or Database settings in app-settings.json for EliteRestaurantPro / Api.");
        }

        optionsBuilder.UseNpgsql(
            connectionString,
            npgsql => npgsql.EnableRetryOnFailure(5));
        return new AppDbContext(optionsBuilder.Options);
    }
}
