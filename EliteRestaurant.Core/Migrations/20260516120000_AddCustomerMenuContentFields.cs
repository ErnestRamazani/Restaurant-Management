using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteRestaurant.Core.Migrations;

/// <inheritdoc />
public partial class AddCustomerMenuContentFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CustomerMenuAboutText",
            table: "PublicMenuSettings",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CustomerMenuContactIntro",
            table: "PublicMenuSettings",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CustomerMenuNotesText",
            table: "PublicMenuSettings",
            type: "text",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CustomerMenuAboutText", table: "PublicMenuSettings");
        migrationBuilder.DropColumn(name: "CustomerMenuContactIntro", table: "PublicMenuSettings");
        migrationBuilder.DropColumn(name: "CustomerMenuNotesText", table: "PublicMenuSettings");
    }
}
