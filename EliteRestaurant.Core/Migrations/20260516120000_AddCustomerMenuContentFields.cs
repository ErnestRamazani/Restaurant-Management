using EliteRestaurant.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteRestaurant.Core.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260516120000_AddCustomerMenuContentFields")]
public class AddCustomerMenuContentFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CustomerMenuAboutText",
            table: "PublicMenuSettings",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CustomerMenuContactIntro",
            table: "PublicMenuSettings",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CustomerMenuNotesText",
            table: "PublicMenuSettings",
            type: "text",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CustomerMenuAboutText", table: "PublicMenuSettings");
        migrationBuilder.DropColumn(name: "CustomerMenuContactIntro", table: "PublicMenuSettings");
        migrationBuilder.DropColumn(name: "CustomerMenuNotesText", table: "PublicMenuSettings");
    }
}
