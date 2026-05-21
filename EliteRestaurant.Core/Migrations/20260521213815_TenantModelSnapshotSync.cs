using System;
using EliteRestaurant.Core.Data;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EliteRestaurant.Core.Migrations
{
    /// <inheritdoc />
    public partial class TenantModelSnapshotSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WaitlistEntries_UniqueId",
                table: "WaitlistEntries");

            migrationBuilder.DropIndex(
                name: "IX_Tables_TableNumber",
                table: "Tables");

            migrationBuilder.DropIndex(
                name: "IX_Tables_UniqueId",
                table: "Tables");

            migrationBuilder.DropIndex(
                name: "IX_SharedOrderDrafts_UniqueId",
                table: "SharedOrderDrafts");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_UniqueId",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_ReservationEngagements_ConfirmationCode",
                table: "ReservationEngagements");

            migrationBuilder.DropIndex(
                name: "IX_PublicMenuSettings_Key",
                table: "PublicMenuSettings");

            migrationBuilder.DropIndex(
                name: "IX_PublicMenuAssets_Key",
                table: "PublicMenuAssets");

            migrationBuilder.DropIndex(
                name: "IX_Products_UniqueId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Orders_ConfirmationCode",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_UniqueId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_UniqueId",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_Employees_SignInId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_UniqueId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_CustomerProfiles_UniqueId",
                table: "CustomerProfiles");

            migrationBuilder.AddColumn<int>(
                name: "RestaurantId",
                table: "WaitlistEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RestaurantId",
                table: "Transactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RestaurantId",
                table: "Tables",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RestaurantId",
                table: "SyncOutbox",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RestaurantId",
                table: "SharedOrderDrafts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RestaurantId",
                table: "Reservations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RestaurantId",
                table: "ReservationEngagements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RestaurantId",
                table: "PublicMenuSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RestaurantId",
                table: "PublicMenuAssets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RestaurantId",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RestaurantId",
                table: "PlacementUnits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RestaurantId",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RestaurantId",
                table: "InventoryItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RestaurantId",
                table: "Employees",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RestaurantId",
                table: "CustomerProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Restaurants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UniqueId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    CustomDomain = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Restaurants", x => x.Id);
                });

            migrationBuilder.Sql(
                $"""
                INSERT INTO "Restaurants" ("UniqueId", "Name", "Slug", "CustomDomain", "IsActive", "CreatedAtUtc")
                SELECT '{RestaurantTenantBootstrap.DefaultUniqueId}',
                       'Elite Restaurant',
                       '{RestaurantTenantBootstrap.DefaultSlug}',
                       '{RestaurantTenantBootstrap.DefaultDomain}',
                       TRUE,
                       NOW() AT TIME ZONE 'UTC'
                WHERE NOT EXISTS (SELECT 1 FROM "Restaurants");
                """);

            foreach (var table in new[]
                     {
                         "Employees", "Products", "Tables", "Orders", "InventoryItems", "CustomerProfiles",
                         "Reservations", "PlacementUnits", "ReservationEngagements", "WaitlistEntries",
                         "SharedOrderDrafts", "PublicMenuSettings", "PublicMenuAssets", "Transactions", "SyncOutbox"
                     })
            {
                migrationBuilder.Sql(
                    $"""UPDATE "{table}" SET "RestaurantId" = (SELECT "Id" FROM "Restaurants" ORDER BY "Id" LIMIT 1) WHERE "RestaurantId" = 0;""");
            }

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_RestaurantId_UniqueId",
                table: "WaitlistEntries",
                columns: new[] { "RestaurantId", "UniqueId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tables_RestaurantId_TableNumber",
                table: "Tables",
                columns: new[] { "RestaurantId", "TableNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tables_RestaurantId_UniqueId",
                table: "Tables",
                columns: new[] { "RestaurantId", "UniqueId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SharedOrderDrafts_RestaurantId_UniqueId",
                table: "SharedOrderDrafts",
                columns: new[] { "RestaurantId", "UniqueId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_RestaurantId_UniqueId",
                table: "Reservations",
                columns: new[] { "RestaurantId", "UniqueId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReservationEngagements_RestaurantId_ConfirmationCode",
                table: "ReservationEngagements",
                columns: new[] { "RestaurantId", "ConfirmationCode" },
                unique: true,
                filter: "\"ConfirmationCode\" IS NOT NULL AND \"ConfirmationCode\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_PublicMenuSettings_RestaurantId_Key",
                table: "PublicMenuSettings",
                columns: new[] { "RestaurantId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicMenuAssets_RestaurantId_Key",
                table: "PublicMenuAssets",
                columns: new[] { "RestaurantId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_RestaurantId_UniqueId",
                table: "Products",
                columns: new[] { "RestaurantId", "UniqueId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RestaurantId_ConfirmationCode",
                table: "Orders",
                columns: new[] { "RestaurantId", "ConfirmationCode" },
                unique: true,
                filter: "\"ConfirmationCode\" IS NOT NULL AND \"ConfirmationCode\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RestaurantId_UniqueId",
                table: "Orders",
                columns: new[] { "RestaurantId", "UniqueId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_RestaurantId_UniqueId",
                table: "InventoryItems",
                columns: new[] { "RestaurantId", "UniqueId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_RestaurantId_SignInId",
                table: "Employees",
                columns: new[] { "RestaurantId", "SignInId" },
                unique: true,
                filter: "\"SignInId\" IS NOT NULL AND \"SignInId\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_RestaurantId_UniqueId",
                table: "Employees",
                columns: new[] { "RestaurantId", "UniqueId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerProfiles_RestaurantId_UniqueId",
                table: "CustomerProfiles",
                columns: new[] { "RestaurantId", "UniqueId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Restaurants_CustomDomain",
                table: "Restaurants",
                column: "CustomDomain",
                unique: true,
                filter: "\"CustomDomain\" IS NOT NULL AND \"CustomDomain\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_Restaurants_Slug",
                table: "Restaurants",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Restaurants_UniqueId",
                table: "Restaurants",
                column: "UniqueId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Restaurants_RestaurantId",
                table: "Employees",
                column: "RestaurantId",
                principalTable: "Restaurants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Restaurants_RestaurantId",
                table: "Employees");

            migrationBuilder.DropTable(
                name: "Restaurants");

            migrationBuilder.DropIndex(
                name: "IX_WaitlistEntries_RestaurantId_UniqueId",
                table: "WaitlistEntries");

            migrationBuilder.DropIndex(
                name: "IX_Tables_RestaurantId_TableNumber",
                table: "Tables");

            migrationBuilder.DropIndex(
                name: "IX_Tables_RestaurantId_UniqueId",
                table: "Tables");

            migrationBuilder.DropIndex(
                name: "IX_SharedOrderDrafts_RestaurantId_UniqueId",
                table: "SharedOrderDrafts");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_RestaurantId_UniqueId",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_ReservationEngagements_RestaurantId_ConfirmationCode",
                table: "ReservationEngagements");

            migrationBuilder.DropIndex(
                name: "IX_PublicMenuSettings_RestaurantId_Key",
                table: "PublicMenuSettings");

            migrationBuilder.DropIndex(
                name: "IX_PublicMenuAssets_RestaurantId_Key",
                table: "PublicMenuAssets");

            migrationBuilder.DropIndex(
                name: "IX_Products_RestaurantId_UniqueId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Orders_RestaurantId_ConfirmationCode",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_RestaurantId_UniqueId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_RestaurantId_UniqueId",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_Employees_RestaurantId_SignInId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_RestaurantId_UniqueId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_CustomerProfiles_RestaurantId_UniqueId",
                table: "CustomerProfiles");

            migrationBuilder.DropColumn(
                name: "RestaurantId",
                table: "WaitlistEntries");

            migrationBuilder.DropColumn(
                name: "RestaurantId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "RestaurantId",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "RestaurantId",
                table: "SyncOutbox");

            migrationBuilder.DropColumn(
                name: "RestaurantId",
                table: "SharedOrderDrafts");

            migrationBuilder.DropColumn(
                name: "RestaurantId",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "RestaurantId",
                table: "ReservationEngagements");

            migrationBuilder.DropColumn(
                name: "RestaurantId",
                table: "PublicMenuSettings");

            migrationBuilder.DropColumn(
                name: "RestaurantId",
                table: "PublicMenuAssets");

            migrationBuilder.DropColumn(
                name: "RestaurantId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "RestaurantId",
                table: "PlacementUnits");

            migrationBuilder.DropColumn(
                name: "RestaurantId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RestaurantId",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "RestaurantId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "RestaurantId",
                table: "CustomerProfiles");

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_UniqueId",
                table: "WaitlistEntries",
                column: "UniqueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tables_TableNumber",
                table: "Tables",
                column: "TableNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tables_UniqueId",
                table: "Tables",
                column: "UniqueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SharedOrderDrafts_UniqueId",
                table: "SharedOrderDrafts",
                column: "UniqueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_UniqueId",
                table: "Reservations",
                column: "UniqueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReservationEngagements_ConfirmationCode",
                table: "ReservationEngagements",
                column: "ConfirmationCode",
                unique: true,
                filter: "\"ConfirmationCode\" IS NOT NULL AND \"ConfirmationCode\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_PublicMenuSettings_Key",
                table: "PublicMenuSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicMenuAssets_Key",
                table: "PublicMenuAssets",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_UniqueId",
                table: "Products",
                column: "UniqueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ConfirmationCode",
                table: "Orders",
                column: "ConfirmationCode",
                unique: true,
                filter: "\"ConfirmationCode\" IS NOT NULL AND \"ConfirmationCode\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UniqueId",
                table: "Orders",
                column: "UniqueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_UniqueId",
                table: "InventoryItems",
                column: "UniqueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_SignInId",
                table: "Employees",
                column: "SignInId",
                unique: true,
                filter: "\"SignInId\" IS NOT NULL AND \"SignInId\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_UniqueId",
                table: "Employees",
                column: "UniqueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerProfiles_UniqueId",
                table: "CustomerProfiles",
                column: "UniqueId",
                unique: true);
        }
    }
}
