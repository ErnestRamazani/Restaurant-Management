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
        migrationBuilder.Sql(
            """
            ALTER TABLE "PublicMenuSettings"
            ADD COLUMN IF NOT EXISTS "EmployeeDeletePasscode" text NOT NULL DEFAULT '';
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE "Employees"
            ADD COLUMN IF NOT EXISTS "CustomRoleTitle" character varying(64);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "EmployeeDeletePasscode", table: "PublicMenuSettings");
        migrationBuilder.DropColumn(name: "CustomRoleTitle", table: "Employees");
    }
}
