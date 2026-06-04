using EliteRestaurant.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteRestaurant.Core.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260530120000_AddOrderCancelPasscodeToPublicMenuSettings")]
public class AddOrderCancelPasscodeToPublicMenuSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "OrderCancelPasscode",
            table: "PublicMenuSettings",
            type: "text",
            nullable: false,
            defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "OrderCancelPasscode", table: "PublicMenuSettings");
    }
}
