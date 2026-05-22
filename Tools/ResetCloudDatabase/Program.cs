using EliteRestaurant.Core.Data;
using Microsoft.EntityFrameworkCore;

if (!args.Contains("--confirm", StringComparer.OrdinalIgnoreCase)
    || !args.Any(a => a.Equals("WIPE_ALL_DATA", StringComparison.OrdinalIgnoreCase)))
{
    Console.WriteLine(
        """
        DESTRUCTIVE: deletes ALL restaurant / menu / order / staff data in PostgreSQL.
        Schema and migrations are kept.

        Usage:
          dotnet run --project Tools/ResetCloudDatabase -- --confirm WIPE_ALL_DATA

        Requires ELITE_POSTGRES_CONNECTION, DATABASE_URL, or Api appsettings connection.
        """);
    return 1;
}

if (!AppDbContext.TryGetPostgreSqlConnectionString(out var connectionString)
    && !AppDbContext.TryGetDatabaseUrlLastResort(out connectionString))
{
    Console.Error.WriteLine("No PostgreSQL connection string found.");
    return 1;
}

Console.WriteLine("Target: " + AppDbContext.GetDatabaseTargetDescription());
Console.WriteLine("This will TRUNCATE all application tables (all tenants).");
Console.Write("Type WIPE to continue: ");
if (!string.Equals(Console.ReadLine()?.Trim(), "WIPE", StringComparison.Ordinal))
{
    Console.WriteLine("Cancelled.");
    return 1;
}

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(connectionString, n => n.EnableRetryOnFailure(5))
    .Options;

await using var db = new AppDbContext(options);

var restaurantCount = await db.Restaurants.IgnoreQueryFilters().CountAsync();
var employeeCount = await db.Employees.IgnoreQueryFilters().CountAsync();
var orderCount = await db.Orders.IgnoreQueryFilters().CountAsync();
Console.WriteLine($"Before wipe: Restaurants={restaurantCount}, Employees={employeeCount}, Orders={orderCount}");

const string truncateSql = """
    TRUNCATE TABLE
        "OrderItems",
        "Orders",
        "ProductIngredients",
        "Products",
        "InventoryItems",
        "Tables",
        "EmployeeAttendances",
        "SalaryAdvances",
        "PayrollPaymentRecords",
        "Employees",
        "Transactions",
        "CustomerProfiles",
        "ReservationEngagements",
        "Reservations",
        "PlacementUnits",
        "WaitlistEntries",
        "SharedOrderDrafts",
        "TabletSessions",
        "SyncOutbox",
        "PublicMenuAssets",
        "PublicMenuSettings",
        "AttendanceDayValidations",
        "Restaurants"
    RESTART IDENTITY CASCADE;
    """;

await db.Database.ExecuteSqlRawAsync(truncateSql);

restaurantCount = await db.Restaurants.IgnoreQueryFilters().CountAsync();
employeeCount = await db.Employees.IgnoreQueryFilters().CountAsync();
orderCount = await db.Orders.IgnoreQueryFilters().CountAsync();
Console.WriteLine($"After wipe:  Restaurants={restaurantCount}, Employees={employeeCount}, Orders={orderCount}");
Console.WriteLine("Done. Run first-site setup (desktop wizard or POST /api/setup/first-site).");
return 0;
