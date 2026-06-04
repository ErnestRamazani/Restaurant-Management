using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteRestaurant.Core.Migrations
{
    /// <inheritdoc />
    public partial class BackfillOrderItemInventoryDeductedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Only kitchen-pipeline statuses (post–cashier release). Excludes Pending*, Waiting, and Completed
            // so we do not mark never-deducted lines as deducted (under-deduction) or rewrite history on closed checks.
            migrationBuilder.Sql(
                """
                UPDATE "OrderItems" oi
                SET "InventoryDeductedAt" = COALESCE(o."PaymentConfirmedAt", o."CreatedAt")
                FROM "Orders" o
                WHERE oi."OrderId" = o."Id"
                  AND oi."InventoryDeductedAt" IS NULL
                  AND o."Status" IN ('In Kitchen', 'Ready', 'Served')
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Cannot reliably distinguish backfilled rows from organically set flags.
        }
    }
}
