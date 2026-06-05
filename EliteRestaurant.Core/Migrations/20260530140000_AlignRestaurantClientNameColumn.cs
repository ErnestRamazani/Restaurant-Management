using EliteRestaurant.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteRestaurant.Core.Migrations;

/// <summary>Unify client display name to <c>FullName</c> and remove legacy unique index that broke staff sync.</summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260530140000_AlignRestaurantClientNameColumn")]
public class AlignRestaurantClientNameColumn : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
              IF EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema = current_schema()
                  AND table_name = 'RestaurantClients'
                  AND column_name = 'Name')
              AND NOT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema = current_schema()
                  AND table_name = 'RestaurantClients'
                  AND column_name = 'FullName')
              THEN
                ALTER TABLE "RestaurantClients" RENAME COLUMN "Name" TO "FullName";
              ELSIF EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema = current_schema()
                  AND table_name = 'RestaurantClients'
                  AND column_name = 'Name')
              AND EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema = current_schema()
                  AND table_name = 'RestaurantClients'
                  AND column_name = 'FullName')
              THEN
                UPDATE "RestaurantClients"
                SET "FullName" = "Name"
                WHERE btrim(coalesce("FullName", '')) = ''
                  AND btrim(coalesce("Name", '')) <> '';
                ALTER TABLE "RestaurantClients" DROP COLUMN "Name";
              END IF;
            END $$;
            """);

        migrationBuilder.Sql(
            """DROP INDEX IF EXISTS "IX_RestaurantClients_RestaurantId_Name";""");

        migrationBuilder.Sql(
            """
            UPDATE "RestaurantClients"
            SET "FullName" = 'Staff #' || "EmployeeId"::text
            WHERE btrim(coalesce("FullName", '')) = ''
              AND "EmployeeId" IS NOT NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // No-op: do not reintroduce the legacy Name column or index.
    }
}
