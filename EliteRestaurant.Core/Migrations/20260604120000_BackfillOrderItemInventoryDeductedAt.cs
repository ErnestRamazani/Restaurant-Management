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
            migrationBuilder.Sql(
                """
                UPDATE "OrderItems" oi
                SET "InventoryDeductedAt" = COALESCE(o."PaymentConfirmedAt", o."CreatedAt")
                FROM "Orders" o
                WHERE oi."OrderId" = o."Id"
                  AND oi."InventoryDeductedAt" IS NULL
                  AND o."Status" IN ('Waiting', 'In Kitchen', 'Ready', 'Served', 'Completed')
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Cannot reliably distinguish backfilled rows from organically set flags.
        }
    }
}
