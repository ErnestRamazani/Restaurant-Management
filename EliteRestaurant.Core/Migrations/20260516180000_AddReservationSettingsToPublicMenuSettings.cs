using EliteRestaurant.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteRestaurant.Core.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260516180000_AddReservationSettingsToPublicMenuSettings")]
public class AddReservationSettingsToPublicMenuSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ReservationLeadDays",
            table: "PublicMenuSettings",
            type: "integer",
            nullable: false,
            defaultValue: 2);

        migrationBuilder.AddColumn<int>(
            name: "ReservationMaxMonthsAhead",
            table: "PublicMenuSettings",
            type: "integer",
            nullable: false,
            defaultValue: 6);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ReservationLeadDays",
            table: "PublicMenuSettings");

        migrationBuilder.DropColumn(
            name: "ReservationMaxMonthsAhead",
            table: "PublicMenuSettings");
    }
}
