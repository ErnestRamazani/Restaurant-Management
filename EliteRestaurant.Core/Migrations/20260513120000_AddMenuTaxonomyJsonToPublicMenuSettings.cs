using EliteRestaurant.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteRestaurant.Core.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260513120000_AddMenuTaxonomyJsonToPublicMenuSettings")]
public class AddMenuTaxonomyJsonToPublicMenuSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "MenuTaxonomyJson",
            table: "PublicMenuSettings",
            type: "text",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "MenuTaxonomyJson",
            table: "PublicMenuSettings");
    }
}
