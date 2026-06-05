using EliteRestaurant.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteRestaurant.Core.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260604130000_AddOrderMerchandiseGrandTotalUsd")]
public class AddOrderMerchandiseGrandTotalUsd : Migration
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
                SET "MerchandiseGrandTotalUsd" = ROUND(
                    "PaymentAmountUsd" + "ChangeGivenUsd" + CASE
                        WHEN COALESCE("ExchangeRateUsed", 0) > 0
                        THEN "ChangeGivenFc" / "ExchangeRateUsed"
                        ELSE 0
                    END, 2)
                WHERE "MerchandiseGrandTotalUsd" = 0
                  AND "PaymentAmountUsd" > 0
                  AND ("ChangeGivenUsd" > 0 OR "ChangeGivenFc" > 0);

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
