using EliteRestaurant.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteRestaurant.Core.Migrations;

/// <summary>Public menu online promo fields, optional online order table id, guest payment intent on orders.</summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260511190000_AddOnlinePromoAndGuestPayment")]
public class AddOnlinePromoAndGuestPayment : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "GuestPaymentMethod",
            table: "Orders",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "OnlineOrdersTableId",
            table: "PublicMenuSettings",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OnlinePromoCtaLabel",
            table: "PublicMenuSettings",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OnlinePromoSubtitle",
            table: "PublicMenuSettings",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OnlinePromoTitle",
            table: "PublicMenuSettings",
            type: "text",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "OnlinePromoTitle",
            table: "PublicMenuSettings");

        migrationBuilder.DropColumn(
            name: "OnlinePromoSubtitle",
            table: "PublicMenuSettings");

        migrationBuilder.DropColumn(
            name: "OnlinePromoCtaLabel",
            table: "PublicMenuSettings");

        migrationBuilder.DropColumn(
            name: "OnlineOrdersTableId",
            table: "PublicMenuSettings");

        migrationBuilder.DropColumn(
            name: "GuestPaymentMethod",
            table: "Orders");
    }
}
