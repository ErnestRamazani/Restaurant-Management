using EliteRestaurant.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteRestaurant.Core.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260521130000_AddReservationEngagementConfirmationCode")]
public class AddReservationEngagementConfirmationCode : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ConfirmationCode",
            table: "ReservationEngagements",
            type: "character varying(6)",
            maxLength: 6,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_ReservationEngagements_ConfirmationCode",
            table: "ReservationEngagements",
            column: "ConfirmationCode",
            unique: true,
            filter: "\"ConfirmationCode\" IS NOT NULL AND \"ConfirmationCode\" <> ''");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ReservationEngagements_ConfirmationCode",
            table: "ReservationEngagements");

        migrationBuilder.DropColumn(
            name: "ConfirmationCode",
            table: "ReservationEngagements");
    }
}
