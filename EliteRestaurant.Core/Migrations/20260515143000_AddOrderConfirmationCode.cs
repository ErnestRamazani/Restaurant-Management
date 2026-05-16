using EliteRestaurant.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteRestaurant.Core.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260515143000_AddOrderConfirmationCode")]
public class AddOrderConfirmationCode : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
                name: "ConfirmationCode",
                table: "Orders",
                type: "character varying(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ConfirmationCode",
                table: "Orders",
                column: "ConfirmationCode",
                unique: true,
                filter: "\"ConfirmationCode\" IS NOT NULL AND \"ConfirmationCode\" <> ''");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Orders_ConfirmationCode",
            table: "Orders");

        migrationBuilder.DropColumn(
            name: "ConfirmationCode",
            table: "Orders");
    }
}
