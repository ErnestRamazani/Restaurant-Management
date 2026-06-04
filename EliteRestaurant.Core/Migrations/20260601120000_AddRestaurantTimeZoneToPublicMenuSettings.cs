using EliteRestaurant.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteRestaurant.Core.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260601120000_AddRestaurantTimeZoneToPublicMenuSettings")]
public class AddRestaurantTimeZoneToPublicMenuSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "RestaurantTimeZoneId",
            table: "PublicMenuSettings",
            type: "text",
            nullable: false,
            defaultValue: "Africa/Kinshasa");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "RestaurantTimeZoneId", table: "PublicMenuSettings");
    }
}
