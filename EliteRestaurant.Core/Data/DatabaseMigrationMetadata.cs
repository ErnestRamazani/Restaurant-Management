namespace EliteRestaurant.Core.Data;

/// <summary>Must stay in sync with the initial EF Core migration in <c>Migrations/</c>.</summary>
public static class DatabaseMigrationMetadata
{
    public const string InitialMigrationId = "20260415140305_InitialSchema";
    public const string ProductVersion = "8.0.11";

    /// <summary>SQL to run once when an existing DB has schema but no migration history (e.g. after EnsureCreated).</summary>
    public static string BaselineInitialMigrationSql =>
        $"""
        CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
            "MigrationId" character varying(150) NOT NULL,
            "ProductVersion" character varying(32) NOT NULL,
            CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
        );

        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
        VALUES ('{InitialMigrationId}', '{ProductVersion}')
        ON CONFLICT ("MigrationId") DO NOTHING;
        """;
}
