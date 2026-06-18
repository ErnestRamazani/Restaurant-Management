using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Data;

/// <summary>
/// Self-heal when <c>EmployeeDeletePasscode</c> / <c>CustomRoleTitle</c> columns exist but EF history
/// does not include the current migration id (e.g. after renaming <c>20260603120000_...</c> to <c>20260603120100_...</c>).
/// </summary>
internal static class EmployeeDeletePasscodeMigrationRepair
{
    public const string LegacyMigrationId = "20260603120000_AddEmployeeDeletePasscodeAndCustomRoleTitle";
    public const string CurrentMigrationId = "20260603120100_AddEmployeeDeletePasscodeAndCustomRoleTitle";

    public static void Reconcile(AppDbContext db)
    {
        db.Database.ExecuteSqlRaw(
            """
            ALTER TABLE "PublicMenuSettings"
            ADD COLUMN IF NOT EXISTS "EmployeeDeletePasscode" text NOT NULL DEFAULT '';

            ALTER TABLE "Employees"
            ADD COLUMN IF NOT EXISTS "CustomRoleTitle" character varying(64);
            """);

        db.Database.ExecuteSqlRaw(
            $"""
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            SELECT '{CurrentMigrationId}', '{DatabaseMigrationMetadata.ProductVersion}'
            WHERE NOT EXISTS (
                SELECT 1 FROM "__EFMigrationsHistory"
                WHERE "MigrationId" = '{CurrentMigrationId}'
            )
            AND EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = 'PublicMenuSettings'
                  AND column_name = 'EmployeeDeletePasscode'
            )
            AND EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = 'Employees'
                  AND column_name = 'CustomRoleTitle'
            );
            """);
    }
}
