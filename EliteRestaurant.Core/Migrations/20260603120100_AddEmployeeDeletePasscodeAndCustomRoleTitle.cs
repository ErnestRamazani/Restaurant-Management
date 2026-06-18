using EliteRestaurant.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteRestaurant.Core.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260603120100_AddEmployeeDeletePasscodeAndCustomRoleTitle")]
public class AddEmployeeDeletePasscodeAndCustomRoleTitle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "EmployeeDeletePasscode",
            table: "PublicMenuSettings",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "CustomRoleTitle",
            table: "Employees",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "EmployeeDeletePasscode", table: "PublicMenuSettings");
        migrationBuilder.DropColumn(name: "CustomRoleTitle", table: "Employees");
    }
}
