using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteRestaurant.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerFulfillmentStatus : Migration
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

            migrationBuilder.AlterColumn<string>(
                name: "OrderOrigin",
                table: "Orders",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "InStore");

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
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                name: "PaymentConfirmedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentTiming",
                table: "Orders");

            migrationBuilder.AlterColumn<string>(
                name: "OrderOrigin",
                table: "Orders",
                type: "text",
                nullable: false,
                defaultValue: "InStore",
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
