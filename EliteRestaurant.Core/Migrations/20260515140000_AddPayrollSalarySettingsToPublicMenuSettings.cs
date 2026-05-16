using EliteRestaurant.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteRestaurant.Core.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260515140000_AddPayrollSalarySettingsToPublicMenuSettings")]
public class AddPayrollSalarySettingsToPublicMenuSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "PayrollLateDaysPerAttendanceUnit",
            table: "PublicMenuSettings",
            type: "integer",
            nullable: false,
            defaultValue: 4);

        migrationBuilder.AddColumn<bool>(
            name: "PayrollAbsenceCountsAsAttendanceUnit",
            table: "PublicMenuSettings",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<decimal>(
            name: "PayrollSalesBonusPercent",
            table: "PublicMenuSettings",
            type: "numeric",
            nullable: false,
            defaultValue: 5m);

        migrationBuilder.AddColumn<decimal>(
            name: "PayrollMaxSalaryAdvancePercentOfGross",
            table: "PublicMenuSettings",
            type: "numeric",
            nullable: false,
            defaultValue: 30m);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PayrollLateDaysPerAttendanceUnit",
            table: "PublicMenuSettings");

        migrationBuilder.DropColumn(
            name: "PayrollAbsenceCountsAsAttendanceUnit",
            table: "PublicMenuSettings");

        migrationBuilder.DropColumn(
            name: "PayrollSalesBonusPercent",
            table: "PublicMenuSettings");

        migrationBuilder.DropColumn(
            name: "PayrollMaxSalaryAdvancePercentOfGross",
            table: "PublicMenuSettings");
    }
}
