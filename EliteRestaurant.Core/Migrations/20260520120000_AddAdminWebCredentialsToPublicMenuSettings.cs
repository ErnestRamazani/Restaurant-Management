using EliteRestaurant.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteRestaurant.Core.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260520120000_AddAdminWebCredentialsToPublicMenuSettings")]
public class AddAdminWebCredentialsToPublicMenuSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "AdminWebPin",
            table: "PublicMenuSettings",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "AdminWebSignInId",
            table: "PublicMenuSettings",
            type: "text",
            nullable: false,
            defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "AdminWebPin", table: "PublicMenuSettings");
        migrationBuilder.DropColumn(name: "AdminWebSignInId", table: "PublicMenuSettings");
    }
}
