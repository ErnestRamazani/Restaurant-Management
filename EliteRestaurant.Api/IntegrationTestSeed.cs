using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Api;

/// <summary>Minimal staff rows for API integration tests (<c>Testing</c> environment only).</summary>
public static class IntegrationTestSeed
{
    public const string AdminWebTestSignInId = "admwebtest";
    public const string AdminWebTestPin = "4124";

    public static void Ensure(WebApplication app)
    {
        if (!app.Environment.IsEnvironment("Testing"))
            return;

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.Employees.Any(e => e.SignInId == AdminWebTestSignInId))
            return;

        db.Employees.Add(new Employee
        {
            UniqueId = "EMP-ADMWEB-INTTEST",
            SignInId = AdminWebTestSignInId,
            Name = "Integration Admin Web",
            Role = "AdminWeb",
            PinCode = EmployeePinHasher.HashForStorage(AdminWebTestPin),
            EmploymentStatus = "Active",
            JoinDate = DateTime.Today
        });

        db.Employees.Add(new Employee
        {
            UniqueId = "EMP-CHEF-INTTEST",
            SignInId = "chefint",
            Name = "Integration Chef",
            Role = "Chef",
            PinCode = EmployeePinHasher.HashForStorage("9999"),
            EmploymentStatus = "Active",
            JoinDate = DateTime.Today
        });

        db.Employees.Add(new Employee
        {
            UniqueId = "EMP-SRV-FLOOR-INT",
            SignInId = "srvfloor",
            Name = "Integration Server",
            Role = "Server",
            PinCode = EmployeePinHasher.HashForStorage("1111"),
            EmploymentStatus = "Active",
            JoinDate = DateTime.Today
        });

        db.Employees.Add(new Employee
        {
            UniqueId = "EMP-CASH-FLOOR-INT",
            SignInId = "cashfloor",
            Name = "Integration Cashier",
            Role = "Cashier",
            PinCode = EmployeePinHasher.HashForStorage("2222"),
            EmploymentStatus = "Active",
            JoinDate = DateTime.Today
        });

        db.SaveChanges();
    }
}
