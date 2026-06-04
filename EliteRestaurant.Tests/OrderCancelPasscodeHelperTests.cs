using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EliteRestaurant.Tests;

public class OrderCancelPasscodeHelperTests
{
    private static AppDbContext BuildDb(string name, string? cloudPasscode = null)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        var db = new AppDbContext(opts);
        db.Database.EnsureCreated();

        if (cloudPasscode is not null)
        {
            db.PublicMenuSettings.Add(new PublicMenuSetting
            {
                Key = "default",
                OrderCancelPasscode = cloudPasscode
            });
            db.SaveChanges();
        }

        return db;
    }

    [Fact]
    public void Validate_Rejects_WhenNotConfigured()
    {
        using var db = BuildDb($"ocfg-{Guid.NewGuid():N}");
        var err = OrderCancelPasscodeHelper.Validate(db, "1234");
        Assert.NotNull(err);
        var configured = OrderCancelPasscodeHelper.ResolveConfigured(db);
        if (string.IsNullOrEmpty(configured))
            Assert.Contains("not configured", err!, StringComparison.OrdinalIgnoreCase);
        else
            Assert.Contains("Incorrect", err!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Accepts_CloudPasscode()
    {
        using var db = BuildDb($"ccloud-{Guid.NewGuid():N}", "secret");
        Assert.Null(OrderCancelPasscodeHelper.Validate(db, "secret"));
        Assert.NotNull(OrderCancelPasscodeHelper.Validate(db, "wrong"));
    }

    [Fact]
    public void Validate_TrimsSubmittedPasscode()
    {
        using var db = BuildDb($"trim-{Guid.NewGuid():N}", "abc");
        Assert.Null(OrderCancelPasscodeHelper.Validate(db, "  abc  "));
    }
}
