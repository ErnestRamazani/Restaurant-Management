using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EliteRestaurant.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SyncOutbox",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    EntityName = table.Column<string>(type: "text", nullable: false),
                    Operation = table.Column<string>(type: "text", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: false),
                    QueuedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SyncedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncOutbox", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SyncOutbox_IdempotencyKey",
                table: "SyncOutbox",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncOutbox_Status_QueuedAtUtc",
                table: "SyncOutbox",
                columns: new[] { "Status", "QueuedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncOutbox");
        }
    }
}
