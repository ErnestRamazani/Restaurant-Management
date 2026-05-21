using EliteRestaurant.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteRestaurant.Core.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260521120000_AddPreferredLanguage")]
public class AddPreferredLanguage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PreferredLanguage",
            table: "Employees",
            type: "text",
            nullable: false,
            defaultValue: "en");

        migrationBuilder.AddColumn<string>(
            name: "PreferredLanguage",
            table: "CustomerProfiles",
            type: "text",
            nullable: false,
            defaultValue: "en");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "PreferredLanguage", table: "Employees");
        migrationBuilder.DropColumn(name: "PreferredLanguage", table: "CustomerProfiles");
    }
}
