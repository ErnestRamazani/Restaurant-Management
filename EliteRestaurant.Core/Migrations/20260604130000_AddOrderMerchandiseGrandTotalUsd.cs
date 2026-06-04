using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteRestaurant.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderMerchandiseGrandTotalUsd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MerchandiseGrandTotalUsd",
                table: "Orders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(
                """
                UPDATE "Orders"
                SET "MerchandiseGrandTotalUsd" = "PaymentAmountUsd"
                WHERE "MerchandiseGrandTotalUsd" = 0
                  AND "PaymentAmountUsd" > 0
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MerchandiseGrandTotalUsd",
                table: "Orders");
        }
    }
}
