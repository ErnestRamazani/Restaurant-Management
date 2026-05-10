using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteRestaurant.Core.Migrations
{
    /// <inheritdoc />
    public partial class SeedAdminWebEmployee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var pinHash = EmployeePinHasher.HashForStorage("4124");
            migrationBuilder.InsertData(
                table: "Employees",
                columns:
                [
                    "UniqueId", "SignInId", "Name", "Role", "PinCode", "ProfileImagePath", "PhoneNumber",
                    "HourlyRate", "MonthlySalaryUSD", "JoinDate", "EmploymentStatus", "Notes",
                    "MondayShift", "TuesdayShift", "WednesdayShift", "ThursdayShift", "FridayShift",
                    "SaturdayShift", "SundayShift"
                ],
                values: new object[]
                {
                    "EMP-SEED-ADMINWEB",
                    "er4124",
                    "Web Admin (seed)",
                    "AdminWeb",
                    pinHash,
                    "",
                    "",
                    0m,
                    0m,
                    new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    "Active",
                    "Seed account for read-only admin web; rotate from Manager desktop or SQL.",
                    "Off", "Off", "Off", "Off", "Off", "Off", "Off"
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Employees",
                keyColumn: "UniqueId",
                keyValue: "EMP-SEED-ADMINWEB");
        }
    }
}
