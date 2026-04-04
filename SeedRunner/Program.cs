using EliteRestaurantPro.Data;
using EliteRestaurantPro.Models;
using EliteRestaurantPro.Utils;
using Microsoft.EntityFrameworkCore;

if (File.Exists(AppDbContext.DatabasePath))
    File.Delete(AppDbContext.DatabasePath);

Console.WriteLine("Creating empty schema...");
AppDbContext.Initialize();

using var db = new AppDbContext();
db.ChangeTracker.AutoDetectChangesEnabled = false;

Console.WriteLine("Seeding master data...");

var employees = new List<Employee>
{
    CreateEmployee("Ernest Cole", "Admin", "1024", 32m, "Morning Shift", "Morning Shift", "Morning Shift", "Morning Shift", "Morning Shift", "Off", "Off"),
    CreateEmployee("Sophia Grant", "Manager", "2048", 28m, "Morning Shift", "Morning Shift", "Morning Shift", "Morning Shift", "Morning Shift", "Morning Shift", "Off"),
    CreateEmployee("Marco Bellini", "Chef", "3301", 24m, "Morning Shift", "Morning Shift", "Morning Shift", "Morning Shift", "Morning Shift", "Night Shift", "Off"),
    CreateEmployee("Nina Alvarez", "Barman", "4411", 18m, "Night Shift", "Night Shift", "Night Shift", "Night Shift", "Night Shift", "Night Shift", "Off"),
    CreateEmployee("Liam Foster", "Server", "4042", 16m, "Morning Shift", "Morning Shift", "Morning Shift", "Morning Shift", "Night Shift", "Off", "Off"),
    CreateEmployee("Emma Russo", "Server", "5560", 16m, "Night Shift", "Night Shift", "Night Shift", "Night Shift", "Morning Shift", "Morning Shift", "Off"),
    CreateEmployee("Noah Rivers", "Server", "4100", 15m, "Morning Shift", "Off", "Morning Shift", "Off", "Morning Shift", "Night Shift", "Night Shift"),
    CreateEmployee("Ava Moretti", "Server", "4200", 15m, "Night Shift", "Morning Shift", "Night Shift", "Morning Shift", "Night Shift", "Off", "Off"),
    CreateEmployee("Lucas Bennett", "Cashier", "5102", 15m, "Morning Shift", "Morning Shift", "Morning Shift", "Morning Shift", "Morning Shift", "Off", "Off"),
    CreateEmployee("Mia Chen", "Server", "5103", 16m, "Morning Shift", "Night Shift", "Morning Shift", "Night Shift", "Morning Shift", "Off", "Off")
};
db.Employees.AddRange(employees);
db.SaveChanges();

var servers = employees.Where(e => e.Role == "Server").ToList();
var chef = employees.Single(e => e.Role == "Chef");
var barman = employees.Single(e => e.Role == "Barman");

var inventoryItems = new List<InventoryItem>
{
    CreateInventory("Beef", "kg", 3200m, 14),
    CreateInventory("Chicken", "kg", 2800m, 10),
    CreateInventory("Salmon", "kg", 1800m, 7),
    CreateInventory("Shrimp", "kg", 1600m, 6),
    CreateInventory("Rice", "kg", 2500m, 180),
    CreateInventory("Pasta", "kg", 2400m, 180),
    CreateInventory("Potatoes", "kg", 2600m, 45),
    CreateInventory("Tomatoes", "kg", 2200m, 8),
    CreateInventory("Lettuce", "kg", 1400m, 5),
    CreateInventory("Parmesan", "kg", 900m, 30),
    CreateInventory("Mozzarella", "kg", 1100m, 25),
    CreateInventory("Chocolate", "kg", 700m, 180),
    CreateInventory("Vanilla", "kg", 200m, 180),
    CreateInventory("Coffee Beans", "kg", 500m, 240),
    CreateInventory("Milk", "l", 1800m, 10),
    CreateInventory("Sparkling Water", "bottles", 4000m, 365),
    CreateInventory("Orange Juice", "l", 2400m, 30),
    CreateInventory("Mint", "kg", 300m, 7),
    CreateInventory("Soda", "bottles", 3500m, 365),
    CreateInventory("Gin", "bottles", 600m, 365),
    CreateInventory("Vodka", "bottles", 600m, 365),
    CreateInventory("Lemon", "pcs", 3000m, 12)
};
db.InventoryItems.AddRange(inventoryItems);
db.SaveChanges();

var inventoryByName = inventoryItems.ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);

var products = new List<Product>
{
    CreateProduct("Truffle Arancini", "Starter/Appetizer", "Starter/Appetizer", 14.50m),
    CreateProduct("Bruschetta Trio", "Starter/Appetizer", "Starter/Appetizer", 12.00m),
    CreateProduct("Calamari Fritti", "Starter/Appetizer", "Starter/Appetizer", 13.50m),
    CreateProduct("Caesar Salad", "Starter/Appetizer", "Salad", 10.00m),
    CreateProduct("Greek Salad", "Starter/Appetizer", "Salad", 10.50m),
    CreateProduct("Minestrone", "Starter/Appetizer", "Soup", 9.50m),
    CreateProduct("Filet Mignon", "Main", "Meat Meal", 34.00m),
    CreateProduct("Chicken Parmesan", "Main", "Meat Meal", 23.00m),
    CreateProduct("Ribeye Steak", "Main", "Meat Meal", 33.00m),
    CreateProduct("Grilled Salmon", "Main", "Seafood", 27.00m),
    CreateProduct("Shrimp Risotto", "Main", "Seafood", 26.00m),
    CreateProduct("Seafood Linguine", "Main", "Pasta", 24.00m),
    CreateProduct("Spaghetti Carbonara", "Main", "Pasta", 19.00m),
    CreateProduct("Penne Arrabbiata", "Main", "Pasta", 17.50m),
    CreateProduct("Margherita Pizza", "Main", "Pizza", 18.00m),
    CreateProduct("Pepperoni Pizza", "Main", "Pizza", 19.50m),
    CreateProduct("Veggie Burger", "Main", "Burger", 17.00m),
    CreateProduct("Cheeseburger Deluxe", "Main", "Burger", 18.50m),
    CreateProduct("Creme Brulee", "Dessert", "Dessert", 9.50m),
    CreateProduct("Chocolate Lava Cake", "Dessert", "Dessert", 10.00m),
    CreateProduct("Tiramisu", "Dessert", "Dessert", 9.50m),
    CreateProduct("Espresso", "Drink", "Coffee", 4.00m),
    CreateProduct("Cappuccino", "Drink", "Coffee", 5.00m),
    CreateProduct("Latte", "Drink", "Coffee", 5.50m),
    CreateProduct("Iced Tea", "Drink", "Soft Drink", 4.75m),
    CreateProduct("Lemonade", "Drink", "Soft Drink", 4.50m),
    CreateProduct("Fresh Orange Juice", "Drink", "Juice", 5.25m),
    CreateProduct("Sapphire Spritz", "Drink", "Cocktail", 11.00m),
    CreateProduct("Mojito", "Drink", "Cocktail", 12.00m),
    CreateProduct("Negroni", "Drink", "Cocktail", 13.00m),
    CreateProduct("Margarita", "Drink", "Cocktail", 12.50m),
    CreateProduct("Virgin Mojito", "Drink", "Mocktail", 8.00m)
};
db.Products.AddRange(products);
db.SaveChanges();

var productByName = products.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

var tables = Enumerable.Range(1, 12)
    .Select(i => new Table
    {
        UniqueId = UniqueIdGenerator.NewId("TBL"),
        TableNumber = i,
        Name = $"Table {i}",
        Capacity = i % 3 == 0 ? 6 : (i % 2 == 0 ? 4 : 2),
        Status = "Available",
        AssignedServerId = servers[(i - 1) % servers.Count].Id
    })
    .ToList();
db.Tables.AddRange(tables);
db.SaveChanges();

Console.WriteLine("Seeding product ingredients...");

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
Link("Bruschetta Trio", "Mozzarella", 0.05m);
Link("Calamari Fritti", "Shrimp", 0.18m);
Link("Caesar Salad", "Lettuce", 0.10m);
Link("Greek Salad", "Tomatoes", 0.10m);
Link("Greek Salad", "Lettuce", 0.08m);
Link("Minestrone", "Tomatoes", 0.12m);
Link("Filet Mignon", "Beef", 0.35m);
Link("Filet Mignon", "Potatoes", 0.20m);
Link("Chicken Parmesan", "Chicken", 0.30m);
Link("Chicken Parmesan", "Parmesan", 0.04m);
Link("Ribeye Steak", "Beef", 0.40m);
Link("Grilled Salmon", "Salmon", 0.32m);
Link("Shrimp Risotto", "Shrimp", 0.22m);
Link("Shrimp Risotto", "Rice", 0.18m);
Link("Seafood Linguine", "Shrimp", 0.18m);
Link("Seafood Linguine", "Pasta", 0.22m);
Link("Spaghetti Carbonara", "Pasta", 0.22m);
Link("Penne Arrabbiata", "Pasta", 0.22m);
Link("Penne Arrabbiata", "Tomatoes", 0.10m);
Link("Margherita Pizza", "Mozzarella", 0.12m);
Link("Margherita Pizza", "Tomatoes", 0.10m);
Link("Pepperoni Pizza", "Mozzarella", 0.12m);
Link("Veggie Burger", "Lettuce", 0.05m);
Link("Cheeseburger Deluxe", "Beef", 0.20m);
Link("Cheeseburger Deluxe", "Mozzarella", 0.04m);
Link("Creme Brulee", "Vanilla", 0.02m);
Link("Chocolate Lava Cake", "Chocolate", 0.08m);
Link("Tiramisu", "Chocolate", 0.04m);
Link("Espresso", "Coffee Beans", 0.02m);
Link("Cappuccino", "Coffee Beans", 0.02m);
Link("Cappuccino", "Milk", 0.20m);
Link("Latte", "Coffee Beans", 0.02m);
Link("Latte", "Milk", 0.25m);
Link("Iced Tea", "Sparkling Water", 1.00m);
Link("Lemonade", "Lemon", 2.00m);
Link("Fresh Orange Juice", "Orange Juice", 0.30m);
Link("Sapphire Spritz", "Sparkling Water", 1.00m);
Link("Sapphire Spritz", "Gin", 0.10m);
Link("Mojito", "Mint", 0.02m);
Link("Mojito", "Soda", 1.00m);
Link("Negroni", "Gin", 0.10m);
Link("Margarita", "Vodka", 0.10m);
Link("Virgin Mojito", "Mint", 0.02m);
Link("Virgin Mojito", "Soda", 1.00m);
db.SaveChanges();

Console.WriteLine("Seeding 6 months of activity...");

var random = new Random(8808);
var startDate = DateTime.Today.AddDays(-179);

for (var date = startDate; date <= DateTime.Today; date = date.AddDays(1))
{
    foreach (var employee in employees.Where(e => e.EmploymentStatus == "Active"))
    {
        var shift = GetShiftForDate(employee, date.DayOfWeek);
        if (shift == "Off")
            continue;

        var shiftStart = shift == "Night Shift" ? 18 : 12;
        var shiftLength = shift == "Night Shift" ? 5 : 6;
        var minuteOffset = random.Next(-8, 28);
        var clockIn = date.Date.AddHours(shiftStart).AddMinutes(minuteOffset);
        var clockOut = date.Date.AddHours(shiftStart + shiftLength).AddMinutes(random.Next(-12, 18));
        var status = minuteOffset > 15 ? "Late" : minuteOffset < 0 ? "Early" : "On Time";

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

    var ordersToday = random.Next(8, 15);
    for (var i = 0; i < ordersToday; i++)
    {
        var table = tables[random.Next(tables.Count)];
        var server = servers[random.Next(servers.Count)];
        var createdAt = date.Date.AddHours(12 + random.Next(0, 10)).AddMinutes(random.Next(0, 60));
        var completed = random.NextDouble() < 0.86;
        var cancelled = !completed && random.NextDouble() < 0.35;
        var status = completed ? "Completed" : cancelled ? "Cancelled" : "Ready";

        var order = new OrderRecord
        {
            UniqueId = UniqueIdGenerator.NewId("ORD"),
            TableId = table.Id,
            TableCode = $"Table {table.TableNumber}",
            TableName = table.Name,
            ServerId = server.Id,
            ServerName = server.Name,
            Status = status,
            CustomerNotes = string.Empty,
            AllergyNotes = string.Empty,
            CreatedAt = createdAt
        };

        var itemCount = random.Next(1, 5);
        decimal orderRevenue = 0m;

        for (var line = 0; line < itemCount; line++)
        {
            var product = products[random.Next(products.Count)];
            var qty = random.Next(1, 4);
            var isDrink = product.Category == "Drink";
            var maker = isDrink ? barman : chef;

            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                Quantity = qty,
                PreparedByEmployeeId = maker.Id,
                PreparedByRole = isDrink ? "Barman" : "Chef",
                PreparedByName = maker.Name
            });

            orderRevenue += product.Price * qty;

            foreach (var ingredient in db.ProductIngredients.Local.Where(pi => pi.ProductId == product.Id))
            {
                var inventory = inventoryItems.First(i => i.Id == ingredient.InventoryItemId);
                inventory.StockQuantity = Math.Max(0m, inventory.StockQuantity - ingredient.Quantity * qty);
            }
        }

        db.Orders.Add(order);

        if (completed && orderRevenue > 0)
        {
            db.Transactions.Add(new MoneyTransaction
            {
                Amount = Math.Round(orderRevenue, 2),
                Date = createdAt,
                Type = "Revenue",
                Category = "Sale",
                IsFixed = true,
                Justification = $"Auto revenue from {order.UniqueId}"
            });
        }
    }

    if (date.DayOfWeek == DayOfWeek.Friday)
    {
        foreach (var employee in employees.Where(e => e.EmploymentStatus == "Active"))
        {
            db.Transactions.Add(new MoneyTransaction
            {
                Amount = Math.Round(employee.HourlyRate * 40m, 2),
                Date = date.Date.AddHours(18),
                Type = "Expense",
                Category = "Salary",
                IsFixed = true,
                Justification = $"Scheduled salary payout: {employee.UniqueId} ({employee.Name}) @ {date:yyyy-MM-dd}"
            });
        }
    }

    if ((date - startDate).Days % 14 == 0)
    {
        Console.WriteLine($"Seeded through {date:yyyy-MM-dd}...");
        db.SaveChanges();
    }
}

foreach (var table in tables)
    table.Status = "Available";

db.SaveChanges();
Console.WriteLine("6 months of data seeded successfully.");

static Employee CreateEmployee(
    string name,
    string role,
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
        Name = name,
        Role = role,
        PinCode = pin,
        PhoneNumber = "+250 700 000 000",
        HourlyRate = hourlyRate,
        JoinDate = DateTime.Today.AddMonths(-randomJoinMonths(role)),
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

static int randomJoinMonths(string role) => role switch
{
    "Admin" => 24,
    "Manager" => 18,
    "Chef" => 16,
    "Barman" => 14,
    _ => 10
};

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
