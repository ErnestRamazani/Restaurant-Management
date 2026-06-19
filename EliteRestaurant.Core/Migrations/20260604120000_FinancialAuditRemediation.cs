using EliteRestaurant.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteRestaurant.Core.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260604120000_FinancialAuditRemediation")]
public class FinancialAuditRemediation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "DeliveryFeePercent",
            table: "PublicMenuSettings",
            type: "numeric",
            nullable: false,
            defaultValue: 20m);

        migrationBuilder.AddColumn<decimal>(
            name: "UnitPriceUsd",
            table: "OrderItems",
            type: "numeric",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "TaxPercentApplied",
            table: "Orders",
            type: "numeric",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "ServicePercentApplied",
            table: "Orders",
            type: "numeric",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<DateTime>(
            name: "RefundedAtUtc",
            table: "Orders",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE "OrderItems" AS oi
            SET "UnitPriceUsd" = p."Price"
            FROM "Products" AS p
            WHERE oi."ProductId" = p."Id"
              AND oi."UnitPriceUsd" = 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "DeliveryFeePercent", table: "PublicMenuSettings");
        migrationBuilder.DropColumn(name: "UnitPriceUsd", table: "OrderItems");
        migrationBuilder.DropColumn(name: "TaxPercentApplied", table: "Orders");
        migrationBuilder.DropColumn(name: "ServicePercentApplied", table: "Orders");
        migrationBuilder.DropColumn(name: "RefundedAtUtc", table: "Orders");
    }
}
