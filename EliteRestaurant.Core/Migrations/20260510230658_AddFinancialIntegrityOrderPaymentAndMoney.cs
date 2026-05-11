using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteRestaurant.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialIntegrityOrderPaymentAndMoney : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PlacementUnits / ReservationEngagements are created in 20260510221209_AddReservationFloorModule.
            // Order/transaction columns are added in 20260510230552_AddCustomerFulfillmentStatus.
            // This migration only applies post-column data fixes for Orders.

            migrationBuilder.Sql(
                """
                UPDATE "Orders" SET "PaymentTiming" = 'Immediate' WHERE "PaymentTiming" = '' OR "PaymentTiming" IS NULL;
                UPDATE "Orders" SET "OrderOrigin" = 'InStore' WHERE "OrderOrigin" = '';
                UPDATE "Orders" SET "PaymentConfirmedAt" = COALESCE("CompletedAt", "CreatedAt")
                    WHERE "Status" = 'Completed' AND "PaymentConfirmedAt" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No tables or columns are created in Up; reverting those is handled by earlier/later migrations.
        }
    }
}
