using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Employees;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EliteRestaurant.Tests;

public sealed class EmployeeDeleteVerificationTests
{
    [Fact]
    public async Task ValidateAsync_blocks_adminweb_delete()
    {
        using var db = CreateDb();
        SeedPasscode(db);
        var adminWeb = new Employee
        {
            Id = 1,
            UniqueId = "EMP-WEB",
            Name = "Web Admin",
            Role = "AdminWeb",
            PinCode = EmployeePinHasher.HashForStorage("1111"),
            EmploymentStatus = "Active"
        };
        db.Employees.Add(adminWeb);
        await db.SaveChangesAsync();

        var err = await EmployeeDeleteVerification.ValidateAsync(db, new EmployeeDeleteRequest
        {
            Employee = new Employee { Id = 1, UniqueId = "EMP-WEB" },
            EmployeeDeletePasscode = "delete-me"
        });

        Assert.Contains("admin web", err!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_server_requires_passcode_only()
    {
        using var db = CreateDb();
        SeedPasscode(db);
        db.Employees.Add(new Employee
        {
            Id = 2,
            UniqueId = "EMP-SRV",
            Name = "Server One",
            Role = "Server",
            SignInId = "srv1",
            PinCode = EmployeePinHasher.HashForStorage("2222"),
            EmploymentStatus = "Active"
        });
        await db.SaveChangesAsync();

        var err = await EmployeeDeleteVerification.ValidateAsync(db, new EmployeeDeleteRequest
        {
            Employee = new Employee { Id = 2, UniqueId = "EMP-SRV" },
            EmployeeDeletePasscode = "delete-me"
        });

        Assert.Null(err);
    }

    [Fact]
    public async Task ValidateAsync_admin_requires_matching_credentials()
    {
        using var db = CreateDb();
        SeedPasscode(db);
        db.Employees.Add(new Employee
        {
            Id = 3,
            UniqueId = "EMP-ADM",
            Name = "Admin Boss",
            Role = "Admin",
            SignInId = "adm01",
            PinCode = EmployeePinHasher.HashForStorage("3333"),
            EmploymentStatus = "Active"
        });
        await db.SaveChangesAsync();

        var wrong = await EmployeeDeleteVerification.ValidateAsync(db, new EmployeeDeleteRequest
        {
            Employee = new Employee { Id = 3, UniqueId = "EMP-ADM" },
            EmployeeDeletePasscode = "delete-me",
            ConfirmSignInId = "adm01",
            ConfirmPin = "wrong"
        });
        Assert.NotNull(wrong);

        var ok = await EmployeeDeleteVerification.ValidateAsync(db, new EmployeeDeleteRequest
        {
            Employee = new Employee { Id = 3, UniqueId = "EMP-ADM" },
            EmployeeDeletePasscode = "delete-me",
            ConfirmSignInId = "adm01",
            ConfirmPin = "3333"
        });
        Assert.Null(ok);
    }

    [Fact]
    public void CredentialsMatchEmployee_accepts_name_as_sign_in_id()
    {
        var employee = new Employee
        {
            Name = "Admin Boss",
            Role = "Admin",
            PinCode = EmployeePinHasher.HashForStorage("3333")
        };

        Assert.True(EmployeeDeleteVerification.CredentialsMatchEmployee(employee, "Admin Boss", "3333"));
    }

    private static void SeedPasscode(AppDbContext db)
    {
        db.PublicMenuSettings.Add(new PublicMenuSetting
        {
            Key = "default",
            EmployeeDeletePasscode = "delete-me"
        });
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
