using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Employees;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EliteRestaurant.Tests;

public sealed class EmployeeDeletePasscodeHelperTests
{
    [Fact]
    public void Validate_uses_cloud_setting_when_present()
    {
        using var db = CreateDb();
        db.PublicMenuSettings.Add(new PublicMenuSetting
        {
            Key = "default",
            EmployeeDeletePasscode = "9876"
        });
        db.SaveChanges();

        Assert.Null(EmployeeDeletePasscodeHelper.Validate(db, "9876"));
        Assert.NotNull(EmployeeDeletePasscodeHelper.Validate(db, "0000"));
    }

    [Fact]
    public void Validate_trims_submitted_passcode()
    {
        using var db = CreateDb();
        db.PublicMenuSettings.Add(new PublicMenuSetting { Key = "default", EmployeeDeletePasscode = "abc" });
        db.SaveChanges();

        Assert.Null(EmployeeDeletePasscodeHelper.Validate(db, "  abc  "));
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
