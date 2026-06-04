// Destructively truncates and reseeds the database. Interactive confirmation is required unless you pass --force (for automation only).
using EliteRestaurant.Core.Clients;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

DatabaseInitializer.Initialize();
using var db = new AppDbContext();

if (args.Any(a => string.Equals(a, "--seed-demo-clients", StringComparison.OrdinalIgnoreCase)))
{
    Console.WriteLine($"Target database: {AppDbContext.GetDatabaseTargetDescription()}");
    Console.WriteLine("(Elite Pro reads clients from the API database — if the app uses a cloud URL, seed that server or call POST /api/dev/seed-demo-clients on it in Development.)");
    Console.WriteLine();

    var result = DemoClientHistorySeed.Ensure(db);
    var message = result switch
    {
        DemoClientHistorySeed.EnsureResult.Seeded =>
            "Seeded 15 demo clients with order history, debt, and revenue.",
        DemoClientHistorySeed.EnsureResult.RepairedTenantScope =>
            "Repaired demo clients (assigned restaurant tenant). Refresh the Clients tab.",
        DemoClientHistorySeed.EnsureResult.AlreadyPresent =>
            "Demo clients already present (CLT-DEMO-*).",
        _ => "Could not seed demo clients — need products, tables, servers, and a restaurant row."
    };
    Console.WriteLine(message);
    return;
}

if (args.Any(a => string.Equals(a, "--cancel-all-open-orders", StringComparison.OrdinalIgnoreCase)))
{
    CancelAllOpenOrders(db);
    return;
}

var reduceArg = args.FirstOrDefault(a =>
    a.StartsWith("--reduce-active-orders=", StringComparison.OrdinalIgnoreCase));
if (!string.IsNullOrWhiteSpace(reduceArg))
{
    var value = reduceArg.Split('=', 2).LastOrDefault();
    if (!int.TryParse(value, out var keepActiveCount) || keepActiveCount < 0)
    {
        Console.WriteLine("Invalid value. Use: --reduce-active-orders=<non-negative number>");
        return;
    }

    ReduceActiveOrders(db, keepActiveCount);
    return;
}

if (!args.Any(a => string.Equals(a, "--force", StringComparison.OrdinalIgnoreCase)))
{
    var settings = SettingsManager.Load();
    var expectedName = string.IsNullOrWhiteSpace(settings.BusinessProfile.RestaurantName)
        ? "Elite Restaurant"
        : settings.BusinessProfile.RestaurantName.Trim();
    Console.WriteLine("WARNING: This will DELETE ALL DATA in the configured PostgreSQL database.");
    Console.WriteLine($"Target: {AppDbContext.GetDatabaseTargetDescription()}");
    Console.WriteLine($"Type the restaurant name exactly to confirm (expected: {expectedName}):");
    var line = Console.ReadLine();
    if (!string.Equals(line?.Trim(), expectedName, StringComparison.Ordinal))
    {
        Console.WriteLine("Aborted.");
        return;
    }
}

Console.WriteLine("Resetting PostgreSQL data...");
db.Database.ExecuteSqlRaw("""
    TRUNCATE TABLE
        "OrderItems",
        "Orders",
        "ProductIngredients",
        "InventoryItems",
        "Tables",
        "Products",
        "EmployeeAttendances",
        "AttendanceDayValidations",
        "SalaryAdvances",
        "PayrollPaymentRecords",
        "Transactions",
        "TabletSessions",
        "SharedOrderDrafts",
        "Reservations",
        "WaitlistEntries",
        "CustomerProfiles",
        "Employees"
    RESTART IDENTITY CASCADE;
    """);

Console.WriteLine("Seeding requested staff (1 admin, 1 chef, 1 barman, 1 front desk, 7 servers, 2 cashiers)...");
var employees = new List<Employee>
{
    CreateEmployee("Ernest Cole", "Admin", "ADM01", "1100", 32m, "Morning", "Morning", "Morning", "Morning", "Morning", "Off", "Off"),
    CreateEmployee("Marco Bellini", "Chef", "CHF01", "2200", 24m, "Morning", "Morning", "Morning", "Morning", "Morning", "Evening", "Off"),
    CreateEmployee("Sofia Vega", "Barman", "BAR01", "5201", 20m, "Evening", "Evening", "Evening", "Evening", "Evening", "Morning", "Off"),
    CreateEmployee("Hannah Reed", "Front desk", "REC01", "5101", 18m, "Morning", "Morning", "Morning", "Morning", "Morning", "Off", "Off"),

    CreateEmployee("Liam Foster", "Server", "SRV01", "3101", 16m, "Morning", "Morning", "Morning", "Morning", "Evening", "Off", "Off"),
    CreateEmployee("Emma Russo", "Server", "SRV02", "3102", 16m, "Evening", "Evening", "Evening", "Evening", "Morning", "Morning", "Off"),
    CreateEmployee("Noah Rivers", "Server", "SRV03", "3103", 15m, "Morning", "Off", "Morning", "Off", "Morning", "Evening", "Evening"),
    CreateEmployee("Ava Moretti", "Server", "SRV04", "3104", 15m, "Evening", "Morning", "Evening", "Morning", "Evening", "Off", "Off"),
    CreateEmployee("Lucas Bennett", "Server", "SRV05", "3105", 16m, "Morning", "Evening", "Morning", "Evening", "Morning", "Off", "Off"),
    CreateEmployee("Mia Chen", "Server", "SRV06", "3106", 16m, "Morning", "Morning", "Off", "Morning", "Evening", "Off", "Off"),
    CreateEmployee("Daniel Carter", "Server", "SRV07", "3107", 15m, "Evening", "Evening", "Morning", "Morning", "Evening", "Off", "Off"),

    CreateEmployee("Nora Diaz", "Cashier", "CSH01", "4101", 18m, "Morning", "Morning", "Morning", "Morning", "Morning", "Off", "Off"),
    CreateEmployee("Owen Blake", "Cashier", "CSH02", "4102", 18m, "Evening", "Evening", "Evening", "Evening", "Evening", "Off", "Off")
};
db.Employees.AddRange(employees);
db.SaveChanges();

var servers = employees.Where(e => e.Role == "Server").ToList();
var chef = employees.Single(e => e.Role == "Chef");
var cashiers = employees.Where(e => e.Role == "Cashier").ToList();

Console.WriteLine("Seeding inventory + products + 15 tables...");
var inventoryItems = new List<InventoryItem>
{
    CreateInventory("Beef", "kg", 420m, 10),
    CreateInventory("Chicken", "kg", 360m, 8),
    CreateInventory("Rice", "kg", 500m, 160),
    CreateInventory("Pasta", "kg", 470m, 150),
    CreateInventory("Potatoes", "kg", 600m, 30),
    CreateInventory("Tomatoes", "kg", 390m, 7),
    CreateInventory("Lettuce", "kg", 210m, 4),
    CreateInventory("Cheese", "kg", 220m, 20),
    CreateInventory("Milk", "l", 340m, 7),
    CreateInventory("Coffee Beans", "kg", 80m, 180),
    CreateInventory("Orange Juice", "l", 280m, 25),
    CreateInventory("Sparkling Water", "bottles", 680m, 300),
    CreateInventory("Mint", "kg", 25m, 5),
    CreateInventory("Chocolate", "kg", 70m, 200),
    CreateInventory("Vanilla", "kg", 15m, 220)
};
db.InventoryItems.AddRange(inventoryItems);
db.SaveChanges();
var inventoryByName = inventoryItems.ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);

var products = new List<Product>
{
    CreateProduct("Truffle Arancini", "Starter/Appetizer", "Starter/Appetizer", 14.50m),
    CreateProduct("Bruschetta Trio", "Starter/Appetizer", "Starter/Appetizer", 12.00m),
    CreateProduct("Caesar Salad", "Starter/Appetizer", "Salad", 10.00m),
    CreateProduct("Filet Mignon", "Main", "Meat Meal", 34.00m),
    CreateProduct("Chicken Parmesan", "Main", "Meat Meal", 23.00m),
    CreateProduct("Spaghetti Carbonara", "Main", "Pasta", 19.00m),
    CreateProduct("Penne Arrabbiata", "Main", "Pasta", 17.50m),
    CreateProduct("Margherita Pizza", "Main", "Pizza", 18.00m),
    CreateProduct("Pepperoni Pizza", "Main", "Pizza", 19.50m),
    CreateProduct("Veggie Burger", "Main", "Burger", 17.00m),
    CreateProduct("Creme Brulee", "Dessert", "Dessert", 9.50m),
    CreateProduct("Chocolate Lava Cake", "Dessert", "Dessert", 10.00m),
    CreateProduct("Espresso", "Drink", "Coffee", 4.00m),
    CreateProduct("Cappuccino", "Drink", "Coffee", 5.00m),
    CreateProduct("Latte", "Drink", "Coffee", 5.50m),
    CreateProduct("Lemonade", "Drink", "Soft Drink", 4.50m),
    CreateProduct("Fresh Orange Juice", "Drink", "Juice", 5.25m),
    CreateProduct("Virgin Mojito", "Drink", "Mocktail", 8.00m)
};
db.Products.AddRange(products);
db.SaveChanges();
var productByName = products.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

var tables = Enumerable.Range(1, 15)
    .Select(i => new Table
    {
        UniqueId = UniqueIdGenerator.NewId("TBL"),
        TableNumber = i,
        Name = $"Table {i}",
        Capacity = i % 5 == 0 ? 8 : (i % 2 == 0 ? 4 : 2),
        Status = "Available",
        AssignedServerId = servers[(i - 1) % servers.Count].Id
    })
    .ToList();
db.Tables.AddRange(tables);
db.SaveChanges();

void Link(string productName, string inventoryName, decimal qty)
{
    db.ProductIngredients.Add(new ProductIngredient
    {
        ProductId = productByName[productName].Id,
        InventoryItemId = inventoryByName[inventoryName].Id,
        Quantity = qty
    });
}

Link("Truffle Arancini", "Rice", 0.15m);
Link("Bruschetta Trio", "Tomatoes", 0.12m);
Link("Caesar Salad", "Lettuce", 0.10m);
Link("Filet Mignon", "Beef", 0.35m);
Link("Filet Mignon", "Potatoes", 0.20m);
Link("Chicken Parmesan", "Chicken", 0.30m);
Link("Spaghetti Carbonara", "Pasta", 0.22m);
Link("Penne Arrabbiata", "Pasta", 0.20m);
Link("Penne Arrabbiata", "Tomatoes", 0.08m);
Link("Margherita Pizza", "Cheese", 0.12m);
Link("Margherita Pizza", "Tomatoes", 0.08m);
Link("Pepperoni Pizza", "Cheese", 0.12m);
Link("Veggie Burger", "Lettuce", 0.05m);
Link("Creme Brulee", "Vanilla", 0.02m);
Link("Chocolate Lava Cake", "Chocolate", 0.08m);
Link("Espresso", "Coffee Beans", 0.02m);
Link("Cappuccino", "Coffee Beans", 0.02m);
Link("Cappuccino", "Milk", 0.20m);
Link("Latte", "Coffee Beans", 0.02m);
Link("Latte", "Milk", 0.25m);
Link("Lemonade", "Sparkling Water", 1.00m);
Link("Fresh Orange Juice", "Orange Juice", 0.30m);
Link("Virgin Mojito", "Mint", 0.02m);
Link("Virgin Mojito", "Sparkling Water", 1.00m);
db.SaveChanges();

Console.WriteLine("Seeding exactly 2 months of activity...");
var rng = new Random(20260405);
var startDate = DateTime.Today.AddDays(-59);
var ingredientByProduct = db.ProductIngredients.AsNoTracking()
    .ToList()
    .GroupBy(x => x.ProductId)
    .ToDictionary(g => g.Key, g => g.ToList());

for (var date = startDate; date <= DateTime.Today; date = date.AddDays(1))
{
    foreach (var employee in employees)
    {
        var shift = GetShiftForDate(employee, date.DayOfWeek);
        if (shift.Equals("Off", StringComparison.OrdinalIgnoreCase))
            continue;

        int shiftStart;
        int shiftLength;
        if (EliteRestaurant.Core.Utils.AttendanceScheduleHelper.IsFullDayShift(shift))
        {
            shiftStart = 10;
            shiftLength = 11;
        }
        else if (shift.Equals("Evening", StringComparison.OrdinalIgnoreCase))
        {
            shiftStart = 17;
            shiftLength = 7;
        }
        else
        {
            shiftStart = 10;
            shiftLength = 8;
        }
        var minuteOffset = rng.Next(-5, 24);
        var clockIn = date.Date.AddHours(shiftStart).AddMinutes(minuteOffset);
        var clockOut = clockIn.AddHours(shiftLength).AddMinutes(rng.Next(-10, 15));
        var status = minuteOffset > 12 ? "Late" : minuteOffset < 0 ? "Early" : "On Time";

        db.EmployeeAttendances.Add(new EmployeeAttendance
        {
            EmployeeId = employee.Id,
            WorkDate = date.Date,
            ClockInTime = clockIn,
            ClockOutTime = clockOut,
            ClockInStatus = status,
            Justification = status == "Late" ? "Traffic delay" : string.Empty
        });
    }

    var ordersToday = date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday ? rng.Next(22, 30) : rng.Next(15, 24);
    for (var i = 0; i < ordersToday; i++)
    {
        var table = tables[rng.Next(tables.Count)];
        var server = servers[rng.Next(servers.Count)];
        var createdAt = date.Date.AddHours(11 + rng.Next(0, 11)).AddMinutes(rng.Next(0, 60));
        var completed = rng.NextDouble() < 0.88;
        var status = completed ? "Completed" : (rng.NextDouble() < 0.5 ? "Cancelled" : "Ready");

        var order = new OrderRecord
        {
            UniqueId = UniqueIdGenerator.NewId("ORD"),
            TableId = table.Id,
            TableCode = $"Table {table.TableNumber}",
            TableName = table.Name,
            ServerId = server.Id,
            ServerName = server.Name,
            Status = status,
            OrderOrigin = OrderOrigin.InStore,
            CustomerNotes = string.Empty,
            AllergyNotes = string.Empty,
            CreatedAt = createdAt,
            CompletedAt = completed ? createdAt.AddMinutes(rng.Next(25, 95)) : null
        };

        decimal subtotal = 0m;
        var lineCount = rng.Next(1, 5);
        for (var line = 0; line < lineCount; line++)
        {
            var product = products[rng.Next(products.Count)];
            var qty = rng.Next(1, 4);

            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                Quantity = qty,
                PreparedByEmployeeId = chef.Id,
                PreparedByRole = "Chef",
                PreparedByName = chef.Name
            });

            subtotal += product.Price * qty;

            if (ingredientByProduct.TryGetValue(product.Id, out var ingredients))
            {
                foreach (var ingredient in ingredients)
                {
                    var inventory = inventoryItems.First(x => x.Id == ingredient.InventoryItemId);
                    inventory.StockQuantity = Math.Max(0m, inventory.StockQuantity - (ingredient.Quantity * qty));
                }
            }
        }

        order.PaymentCurrencyCode = "USD";
        order.ExchangeRateUsed = 2250m;
        order.PaymentAmountUsd = decimal.Round(subtotal, 2);
        order.PaymentAmountFc = decimal.Round(subtotal * order.ExchangeRateUsed, 2);
        order.PaymentAmount = order.PaymentAmountUsd;
        order.CustomerPaidUsd = order.PaymentAmountUsd;
        order.CustomerPaidFc = 0m;
        order.ChangeGivenUsd = 0m;
        order.ChangeGivenFc = 0m;

        db.Orders.Add(order);

        if (completed)
        {
            db.Transactions.Add(new MoneyTransaction
            {
                Amount = order.PaymentAmountUsd,
                AmountUsd = order.PaymentAmountUsd,
                AmountFc = order.PaymentAmountFc,
                Date = order.CompletedAt ?? createdAt,
                Type = "Revenue",
                Category = "Sale",
                CurrencyCode = "USD",
                ExchangeRateUsed = order.ExchangeRateUsed,
                IsFixed = true,
                Justification = $"Auto sale {order.UniqueId}"
            });
        }
    }

    if (date.DayOfWeek == DayOfWeek.Friday)
    {
        foreach (var employee in employees)
        {
            var payroll = decimal.Round(employee.HourlyRate * 40m, 2);
            db.Transactions.Add(new MoneyTransaction
            {
                Amount = payroll,
                AmountUsd = payroll,
                AmountFc = payroll * 2250m,
                Date = date.Date.AddHours(20),
                Type = "Expense",
                Category = "Salary",
                CurrencyCode = "USD",
                ExchangeRateUsed = 2250m,
                IsFixed = true,
                Justification = $"Weekly payroll: {employee.Name}"
            });
        }
    }

    if ((date - startDate).Days % 10 == 0)
    {
        Console.WriteLine($"Seeded through {date:yyyy-MM-dd}");
        db.SaveChanges();
    }
}

foreach (var table in tables)
    table.Status = "Available";

db.SaveChanges();
Console.WriteLine("Done: 2 months of data seeded.");
Console.WriteLine();
Console.WriteLine("=== SIGN-IN CREDENTIALS (PINs match seed script; stored hashed in DB) ===");
var seedPinBySignInId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["ADM01"] = "1100",
    ["CHF01"] = "2200",
    ["BAR01"] = "5201",
    ["REC01"] = "5101",
    ["SRV01"] = "3101",
    ["SRV02"] = "3102",
    ["SRV03"] = "3103",
    ["SRV04"] = "3104",
    ["SRV05"] = "3105",
    ["SRV06"] = "3106",
    ["SRV07"] = "3107",
    ["CSH01"] = "4101",
    ["CSH02"] = "4102",
};
foreach (var employee in employees.OrderBy(e => e.Role).ThenBy(e => e.Name))
{
    var sid = (employee.SignInId ?? string.Empty).Trim();
    var pinDisplay = seedPinBySignInId.TryGetValue(sid, out var p) ? p : "?";
    Console.WriteLine($"{employee.Role,-8} | {employee.Name,-16} | ID: {employee.SignInId,-5} | PIN: {pinDisplay}");
}

static Employee CreateEmployee(
    string name,
    string role,
    string signInId,
    string pin,
    decimal hourlyRate,
    string monday,
    string tuesday,
    string wednesday,
    string thursday,
    string friday,
    string saturday,
    string sunday)
{
    return new Employee
    {
        UniqueId = UniqueIdGenerator.NewId("EMP"),
        SignInId = signInId,
        Name = name,
        Role = role,
        PinCode = EmployeePinHasher.HashForStorage(pin),
        PhoneNumber = "+1 555 000 0000",
        HourlyRate = hourlyRate,
        JoinDate = DateTime.Today.AddMonths(-6),
        EmploymentStatus = "Active",
        MondayShift = monday,
        TuesdayShift = tuesday,
        WednesdayShift = wednesday,
        ThursdayShift = thursday,
        FridayShift = friday,
        SaturdayShift = saturday,
        SundayShift = sunday
    };
}

static InventoryItem CreateInventory(string name, string unit, decimal stock, int expiryDays)
{
    return new InventoryItem
    {
        UniqueId = UniqueIdGenerator.NewId("INV"),
        Name = name,
        Unit = unit,
        StockQuantity = stock,
        ExpirationDate = DateTime.Today.AddDays(expiryDays),
        Notes = string.Empty
    };
}

static Product CreateProduct(string name, string category, string subCategory, decimal price)
{
    return new Product
    {
        UniqueId = UniqueIdGenerator.NewId("MEN"),
        Name = name,
        Category = category,
        SubCategory = subCategory,
        Price = price
    };
}

static string GetShiftForDate(Employee employee, DayOfWeek dayOfWeek)
{
    return dayOfWeek switch
    {
        DayOfWeek.Monday => employee.MondayShift,
        DayOfWeek.Tuesday => employee.TuesdayShift,
        DayOfWeek.Wednesday => employee.WednesdayShift,
        DayOfWeek.Thursday => employee.ThursdayShift,
        DayOfWeek.Friday => employee.FridayShift,
        DayOfWeek.Saturday => employee.SaturdayShift,
        DayOfWeek.Sunday => employee.SundayShift,
        _ => "Off"
    };
}

static void CancelAllOpenOrders(AppDbContext db)
{
    var openStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        OrderWorkflow.PendingCashier,
        "Waiting",
        "In Kitchen",
        "Ready",
        OrderWorkflow.Served
    };

    var orders = db.Orders.Where(o => openStatuses.Contains(o.Status)).ToList();
    foreach (var o in orders)
        o.Status = "Cancelled";

    db.SaveChanges();
    DataReconciler.ReconcileTableStatusesWithOrders(db);
    db.SaveChanges();

    Console.WriteLine($"Cancelled {orders.Count} open order(s). Tables reconciled.");
}

static void ReduceActiveOrders(AppDbContext db, int keepActiveCount)
{
    var activeStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Waiting",
        "In Kitchen",
        "Ready",
        "Served"
    };

    var activeOrders = db.Orders
        .Include(o => o.Items)
            .ThenInclude(i => i.Product)
        .Where(o => activeStatuses.Contains(o.Status))
        .OrderByDescending(o => o.CreatedAt)
        .ToList();

    if (activeOrders.Count <= keepActiveCount)
    {
        Console.WriteLine($"No change needed. Active orders: {activeOrders.Count}.");
        return;
    }

    var toClose = activeOrders.Skip(keepActiveCount).ToList();
    foreach (var order in toClose)
    {
        order.Status = "Completed";
        order.CompletedAt ??= order.CreatedAt.AddMinutes(45);

        if (order.PaymentAmountUsd <= 0m)
        {
            var subtotal = order.Items.Sum(i => (i.Product?.Price ?? 0m) * i.Quantity);
            var amountUsd = decimal.Round(subtotal, 2);
            order.PaymentCurrencyCode = "USD";
            order.ExchangeRateUsed = order.ExchangeRateUsed <= 0m ? 2250m : order.ExchangeRateUsed;
            order.PaymentAmount = amountUsd;
            order.PaymentAmountUsd = amountUsd;
            order.PaymentAmountFc = decimal.Round(amountUsd * order.ExchangeRateUsed, 2);
            order.CustomerPaidUsd = amountUsd;
            order.CustomerPaidFc = 0m;
            order.ChangeGivenUsd = 0m;
            order.ChangeGivenFc = 0m;
        }
    }

    db.SaveChanges();
    DataReconciler.ReconcileTableStatusesWithOrders(db);
    db.SaveChanges();

    var remainingActive = db.Orders.Count(o => activeStatuses.Contains(o.Status));
    Console.WriteLine($"Active orders reduced from {activeOrders.Count} to {remainingActive}.");
}
