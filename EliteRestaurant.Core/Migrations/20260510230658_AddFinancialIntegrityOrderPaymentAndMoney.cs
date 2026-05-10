using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EliteRestaurant.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialIntegrityOrderPaymentAndMoney : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrderOriginType",
                table: "Transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RelatedOrderId",
                table: "Transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerFulfillmentStatus",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryFeeUsd",
                table: "Orders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "OrderOrigin",
                table: "Orders",
                type: "text",
                nullable: false,
                defaultValue: "InStore");

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentConfirmedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentTiming",
                table: "Orders",
                type: "text",
                nullable: false,
                defaultValue: "Immediate");

            migrationBuilder.CreateTable(
                name: "PlacementUnits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TableId = table.Column<int>(type: "integer", nullable: false),
                    MinPartyCapacity = table.Column<int>(type: "integer", nullable: false),
                    MaxPartyCapacity = table.Column<int>(type: "integer", nullable: false),
                    LayoutX = table.Column<int>(type: "integer", nullable: false),
                    LayoutY = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    MergeClusterKey = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlacementUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlacementUnits_Tables_TableId",
                        column: x => x.TableId,
                        principalTable: "Tables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReservationEngagements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlacementUnitId = table.Column<int>(type: "integer", nullable: false),
                    TableId = table.Column<int>(type: "integer", nullable: false),
                    ReservationBookingId = table.Column<int>(type: "integer", nullable: true),
                    PlannedStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PlannedEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActualStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GuestName = table.Column<string>(type: "text", nullable: false),
                    GuestPhone = table.Column<string>(type: "text", nullable: false),
                    GuestEmail = table.Column<string>(type: "text", nullable: false),
                    PartySize = table.Column<int>(type: "integer", nullable: false),
                    UserNotes = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ReminderTwoHoursBeforeSentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RotationOrOverstayFlag = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationEngagements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReservationEngagements_PlacementUnits_PlacementUnitId",
                        column: x => x.PlacementUnitId,
                        principalTable: "PlacementUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservationEngagements_Reservations_ReservationBookingId",
                        column: x => x.ReservationBookingId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReservationEngagements_Tables_TableId",
                        column: x => x.TableId,
                        principalTable: "Tables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlacementUnits_MergeClusterKey",
                table: "PlacementUnits",
                column: "MergeClusterKey");

            migrationBuilder.CreateIndex(
                name: "IX_PlacementUnits_TableId",
                table: "PlacementUnits",
                column: "TableId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReservationEngagements_PlacementUnitId",
                table: "ReservationEngagements",
                column: "PlacementUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationEngagements_PlannedStartUtc",
                table: "ReservationEngagements",
                column: "PlannedStartUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationEngagements_ReservationBookingId",
                table: "ReservationEngagements",
                column: "ReservationBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationEngagements_Status",
                table: "ReservationEngagements",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationEngagements_TableId",
                table: "ReservationEngagements",
                column: "TableId");

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
            migrationBuilder.DropTable(
                name: "ReservationEngagements");

            migrationBuilder.DropTable(
                name: "PlacementUnits");

            migrationBuilder.DropColumn(
                name: "OrderOriginType",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "RelatedOrderId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CustomerFulfillmentStatus",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryFeeUsd",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "OrderOrigin",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentConfirmedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentTiming",
                table: "Orders");
        }
    }
}
