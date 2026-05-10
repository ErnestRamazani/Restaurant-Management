using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteRestaurant.Core.Migrations;

/// <summary>Aligns seeded AdminWeb sign-in with product default (was er4142).</summary>
public partial class ChangeAdminWebSeedSignInToEr4124 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """UPDATE "Employees" SET "SignInId" = 'er4124' WHERE "UniqueId" = 'EMP-SEED-ADMINWEB' AND "SignInId" = 'er4142';""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """UPDATE "Employees" SET "SignInId" = 'er4142' WHERE "UniqueId" = 'EMP-SEED-ADMINWEB' AND "SignInId" = 'er4124';""");
    }
}
