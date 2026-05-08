using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EliteRestaurant.Core.Migrations
{
    /// <inheritdoc />
    public partial class StorePublicMenuSettingsInDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublicMenuAssets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "text", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicMenuAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PublicMenuSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "text", nullable: false),
                    RestaurantName = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    WebsiteDomain = table.Column<string>(type: "text", nullable: false),
                    SocialMedia = table.Column<string>(type: "text", nullable: false),
                    CustomerMenuTagline = table.Column<string>(type: "text", nullable: true),
                    StaffLoginPasscode = table.Column<string>(type: "text", nullable: false),
                    TicketFooterText = table.Column<string>(type: "text", nullable: false),
                    TaxIdLegalInfo = table.Column<string>(type: "text", nullable: false),
                    DefaultCurrencyDisplayMode = table.Column<string>(type: "text", nullable: false),
                    UsdToFcRate = table.Column<decimal>(type: "numeric", nullable: false),
                    RoundingLine = table.Column<string>(type: "text", nullable: false),
                    RoundingSubtotal = table.Column<string>(type: "text", nullable: false),
                    RoundingGrandTotal = table.Column<string>(type: "text", nullable: false),
                    TaxPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    ServicePercent = table.Column<decimal>(type: "numeric", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicMenuSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublicMenuAssets_Key",
                table: "PublicMenuAssets",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicMenuSettings_Key",
                table: "PublicMenuSettings",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublicMenuAssets");

            migrationBuilder.DropTable(
                name: "PublicMenuSettings");
        }
    }
}
