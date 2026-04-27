namespace EliteRestaurant.Core.Data;

/// <summary>
/// Legacy ad-hoc schema patches previously lived on <see cref="AppDbContext"/>; they are superseded by
/// EF Core migrations (<see cref="DatabaseMigrationRunner"/>). Reserve this type for future optional
/// idempotent data repairs that are not expressible as migrations.
/// </summary>
public static class SchemaRepairer
{
}
