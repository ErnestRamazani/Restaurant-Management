using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EliteRestaurant.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantClientsAndDebt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ClientDebtCapUsd",
                table: "PublicMenuSettings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountOnAccountUsd",
                table: "Orders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ClientDebtSettledUsd",
                table: "Orders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ClientSettlement",
                table: "Orders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RestaurantClientId",
                table: "Orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StaffMealDiscountPercent",
                table: "Employees",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "RestaurantClients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RestaurantId = table.Column<int>(type: "integer", nullable: false),
                    UniqueId = table.Column<string>(type: "text", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    PrimaryPhone = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    InternalNotes = table.Column<string>(type: "text", nullable: false),
                    DebtBalanceUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    IsStaffClient = table.Column<bool>(type: "boolean", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestaurantClients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestaurantClients_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ClientDebtLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RestaurantId = table.Column<int>(type: "integer", nullable: false),
                    RestaurantClientId = table.Column<int>(type: "integer", nullable: false),
                    OrderId = table.Column<int>(type: "integer", nullable: true),
                    EntryType = table.Column<string>(type: "text", nullable: false),
                    AmountUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    BalanceAfterUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    CreatedByEmployeeId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientDebtLedgerEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientDebtLedgerEntries_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ClientDebtLedgerEntries_RestaurantClients_RestaurantClientId",
                        column: x => x.RestaurantClientId,
                        principalTable: "RestaurantClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RestaurantClientId",
                table: "Orders",
                column: "RestaurantClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientDebtLedgerEntries_OrderId",
                table: "ClientDebtLedgerEntries",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientDebtLedgerEntries_RestaurantClientId",
                table: "ClientDebtLedgerEntries",
                column: "RestaurantClientId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantClients_EmployeeId",
                table: "RestaurantClients",
                column: "EmployeeId",
                unique: true,
                filter: "\"EmployeeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantClients_RestaurantId_PrimaryPhone",
                table: "RestaurantClients",
                columns: new[] { "RestaurantId", "PrimaryPhone" },
                unique: true,
                filter: "\"PrimaryPhone\" IS NOT NULL AND \"PrimaryPhone\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantClients_RestaurantId_UniqueId",
                table: "RestaurantClients",
                columns: new[] { "RestaurantId", "UniqueId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_RestaurantClients_RestaurantClientId",
                table: "Orders",
                column: "RestaurantClientId",
                principalTable: "RestaurantClients",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_RestaurantClients_RestaurantClientId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "ClientDebtLedgerEntries");

            migrationBuilder.DropTable(
                name: "RestaurantClients");

            migrationBuilder.DropIndex(
                name: "IX_Orders_RestaurantClientId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ClientDebtCapUsd",
                table: "PublicMenuSettings");

            migrationBuilder.DropColumn(
                name: "AmountOnAccountUsd",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ClientDebtSettledUsd",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ClientSettlement",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RestaurantClientId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "StaffMealDiscountPercent",
                table: "Employees");
        }
    }
}
