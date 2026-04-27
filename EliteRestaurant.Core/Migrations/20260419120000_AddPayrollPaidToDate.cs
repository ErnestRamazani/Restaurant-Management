using EliteRestaurant.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteRestaurant.Core.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260419120000_AddPayrollPaidToDate")]
    /// <inheritdoc />
    public partial class AddPayrollPaidToDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PaidToDateUsd",
                table: "PayrollPaymentRecords",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            // Existing rows were always paid in full in one step.
            migrationBuilder.Sql("""UPDATE "PayrollPaymentRecords" SET "PaidToDateUsd" = "NetPayUsd";""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaidToDateUsd",
                table: "PayrollPaymentRecords");
        }
    }
}
