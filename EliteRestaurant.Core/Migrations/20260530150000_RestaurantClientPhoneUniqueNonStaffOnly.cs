using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteRestaurant.Core.Migrations;

/// <summary>Phone uniqueness applies to regular clients only; staff mirrors may share numbers.</summary>
public partial class RestaurantClientPhoneUniqueNonStaffOnly : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """DROP INDEX IF EXISTS "IX_RestaurantClients_RestaurantId_PrimaryPhone";""");

        migrationBuilder.Sql(
            """
            CREATE UNIQUE INDEX "IX_RestaurantClients_RestaurantId_PrimaryPhone"
            ON "RestaurantClients" ("RestaurantId", "PrimaryPhone")
            WHERE "PrimaryPhone" IS NOT NULL
              AND btrim("PrimaryPhone") <> ''
              AND NOT "IsStaffClient";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """DROP INDEX IF EXISTS "IX_RestaurantClients_RestaurantId_PrimaryPhone";""");

        migrationBuilder.Sql(
            """
            CREATE UNIQUE INDEX "IX_RestaurantClients_RestaurantId_PrimaryPhone"
            ON "RestaurantClients" ("RestaurantId", "PrimaryPhone")
            WHERE "PrimaryPhone" IS NOT NULL AND btrim("PrimaryPhone") <> '';
            """);
    }
}
