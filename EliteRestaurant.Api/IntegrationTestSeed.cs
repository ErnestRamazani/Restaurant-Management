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
        RestaurantTenantBootstrap.EnsureDefaultRestaurant(db);

        if (!db.Employees.Any(e => e.SignInId == AdminWebTestSignInId))
        {
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

            db.Employees.Add(new Employee
            {
                UniqueId = "EMP-BAR-INT",
                SignInId = "barint",
                Name = "Integration Barman",
                Role = "Barman",
                PinCode = EmployeePinHasher.HashForStorage("5201"),
                EmploymentStatus = "Active",
                JoinDate = DateTime.Today
            });

            db.Employees.Add(new Employee
            {
                UniqueId = "EMP-REC-INT",
                SignInId = "recint",
                Name = "Integration Front Desk",
                Role = "Front desk",
                PinCode = EmployeePinHasher.HashForStorage("5101"),
                EmploymentStatus = "Active",
                JoinDate = DateTime.Today
            });

            db.SaveChanges();
        }

        EnsurePublicMenuOnlineOrderFixture(db);
    }

    /// <summary>Table + products + cloud menu row so <c>/api/public/menu/orders/online</c> can be tested.</summary>
    private static void EnsurePublicMenuOnlineOrderFixture(AppDbContext db)
    {
        var server = db.Employees.FirstOrDefault(e => e.SignInId == "srvfloor");
        if (server is null)
            return;

        if (!db.Tables.Any(t => t.TableNumber == 99))
        {
            db.Tables.Add(new Table
            {
                UniqueId = "TBL-ONLINE-INT",
                TableNumber = 99,
                Name = "Online orders",
                Capacity = 4,
                Status = "Available",
                AssignedServerId = server.Id
            });
            db.SaveChanges();
        }

        if (!db.Products.Any(p => p.UniqueId == "P-INT-FOOD"))
        {
            db.Products.Add(new Product
            {
                UniqueId = "P-INT-FOOD",
                Name = "Integration Burger",
                Category = "Food",
                SubCategory = "Mains",
                Price = 10m
            });
            db.Products.Add(new Product
            {
                UniqueId = "P-INT-DRINK",
                Name = "Integration Cola",
                Category = "Drink",
                SubCategory = "General",
                Price = 3m
            });
            db.SaveChanges();
        }

        if (!db.PublicMenuSettings.Any(s => s.Key == "default"))
        {
            var table99 = db.Tables.First(t => t.TableNumber == 99);
            db.PublicMenuSettings.Add(new PublicMenuSetting
            {
                Key = "default",
                RestaurantName = "Integration Bistro",
                Phone = "000",
                Address = "Test",
                WebsiteDomain = "",
                SocialMedia = "",
                StaffLoginPasscode = "staffgate",
                AdminWebSignInId = AdminWebTestSignInId,
                AdminWebPin = AdminWebTestPin,
                TicketFooterText = "Thanks",
                TaxIdLegalInfo = "",
                DefaultCurrencyDisplayMode = "Dual",
                UsdToFcRate = 2250m,
                RoundingLine = "Nearest",
                RoundingSubtotal = "Nearest",
                RoundingGrandTotal = "Nearest",
                TaxPercent = 7m,
                ServicePercent = 10m,
                OnlineOrdersTableId = table99.Id,
                UpdatedAtUtc = DateTime.UtcNow
            });
            db.SaveChanges();
        }
    }
}
