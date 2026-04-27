using System.Globalization;
using System.Linq;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Data;

internal static class SampleDataBootstrapper
{
    private static readonly bool BootstrapSampleData = false;

    public static void SeedIfEnabled(AppDbContext db)
    {
        if (!BootstrapSampleData)
            return;
        if (!db.Employees.Any())
        {
            db.Employees.AddRange(
                new Employee { UniqueId = UniqueIdGenerator.NewId("EMP"), Name = "Ernest Cole", Role = "Admin", PinCode = EmployeePinHasher.HashForStorage("1024"), PhoneNumber = "+1 555-0101", HourlyRate = 32m, JoinDate = DateTime.Today.AddYears(-2), EmploymentStatus = "Active" },
                new Employee { UniqueId = UniqueIdGenerator.NewId("EMP"), Name = "Sophia Grant", Role = "Manager", PinCode = EmployeePinHasher.HashForStorage("2048"), PhoneNumber = "+1 555-0102", HourlyRate = 28m, JoinDate = DateTime.Today.AddYears(-1), EmploymentStatus = "Active" },
                new Employee { UniqueId = UniqueIdGenerator.NewId("EMP"), SignInId = "MARCO", Name = "Marco Bellini", Role = "Chef", PinCode = EmployeePinHasher.HashForStorage("3301"), PhoneNumber = "+1 555-0103", HourlyRate = 24m, JoinDate = DateTime.Today.AddMonths(-18), EmploymentStatus = "Active" },
                new Employee { UniqueId = UniqueIdGenerator.NewId("EMP"), SignInId = "LIAM", Name = "Liam Foster", Role = "Server", PinCode = EmployeePinHasher.HashForStorage("4042"), PhoneNumber = "+1 555-0104", HourlyRate = 16m, JoinDate = DateTime.Today.AddMonths(-8), EmploymentStatus = "Active" },
                new Employee { UniqueId = UniqueIdGenerator.NewId("EMP"), SignInId = "EMMA", Name = "Emma Russo", Role = "Server", PinCode = EmployeePinHasher.HashForStorage("5560"), PhoneNumber = "+1 555-0105", HourlyRate = 16m, JoinDate = DateTime.Today.AddMonths(-6), EmploymentStatus = "Active" },
                new Employee { UniqueId = UniqueIdGenerator.NewId("EMP"), SignInId = "CASH", Name = "Jordan Blake", Role = "Cashier", PinCode = EmployeePinHasher.HashForStorage("6001"), PhoneNumber = "+1 555-0108", HourlyRate = 18m, JoinDate = DateTime.Today.AddMonths(-10), EmploymentStatus = "Active" });
        }

        if (!db.Employees.Any(e => e.Role.ToLower() == "server"))
        {
            db.Employees.AddRange(
                new Employee { UniqueId = UniqueIdGenerator.NewId("EMP"), SignInId = "NOAH", Name = "Noah Rivers", Role = "Server", PinCode = EmployeePinHasher.HashForStorage("4100"), PhoneNumber = "+1 555-0106", HourlyRate = 15m, JoinDate = DateTime.Today.AddMonths(-4), EmploymentStatus = "Active" },
                new Employee { UniqueId = UniqueIdGenerator.NewId("EMP"), SignInId = "AVA", Name = "Ava Moretti", Role = "Server", PinCode = EmployeePinHasher.HashForStorage("4200"), PhoneNumber = "+1 555-0107", HourlyRate = 15m, JoinDate = DateTime.Today.AddMonths(-3), EmploymentStatus = "Active" });
        }

        if (!db.Employees.Any(e => e.Role.Equals("Cashier", StringComparison.OrdinalIgnoreCase)))
        {
            db.Employees.Add(new Employee
            {
                UniqueId = UniqueIdGenerator.NewId("EMP"),
                SignInId = "CASH",
                Name = "Jordan Blake",
                Role = "Cashier",
                PinCode = EmployeePinHasher.HashForStorage("6001"),
                PhoneNumber = "+1 555-0108",
                HourlyRate = 18m,
                JoinDate = DateTime.Today.AddMonths(-10),
                EmploymentStatus = "Active"
            });
        }

        if (!db.Products.Any())
        {
            db.Products.AddRange(
                new Product { UniqueId = UniqueIdGenerator.NewId("MEN"), Name = "Truffle Arancini", Category = "Starter/Appetizer", SubCategory = "Starter/Appetizer", Price = 14.50m },
                new Product { UniqueId = UniqueIdGenerator.NewId("MEN"), Name = "Filet Mignon", Category = "Main", SubCategory = "Meat Meal", Price = 34.00m },
                new Product { UniqueId = UniqueIdGenerator.NewId("MEN"), Name = "Sapphire Spritz", Category = "Drink", SubCategory = "Cocktail", Price = 11.00m },
                new Product { UniqueId = UniqueIdGenerator.NewId("MEN"), Name = "Creme Brulee", Category = "Dessert", SubCategory = "Dessert", Price = 9.50m });
        }

        if (!db.InventoryItems.Any())
        {
            db.InventoryItems.AddRange(
                new InventoryItem { UniqueId = UniqueIdGenerator.NewId("INV"), Name = "Chicken", Unit = "kg", StockQuantity = 30, ExpirationDate = DateTime.Today.AddDays(5), Notes = "" },
                new InventoryItem { UniqueId = UniqueIdGenerator.NewId("INV"), Name = "Rice", Unit = "kg", StockQuantity = 60, ExpirationDate = DateTime.Today.AddMonths(6), Notes = "" },
                new InventoryItem { UniqueId = UniqueIdGenerator.NewId("INV"), Name = "Avocado", Unit = "pcs", StockQuantity = 40, ExpirationDate = DateTime.Today.AddDays(4), Notes = "" },
                new InventoryItem { UniqueId = UniqueIdGenerator.NewId("INV"), Name = "Beef", Unit = "kg", StockQuantity = 25, ExpirationDate = DateTime.Today.AddDays(6), Notes = "" },
                new InventoryItem { UniqueId = UniqueIdGenerator.NewId("INV"), Name = "Potatoes", Unit = "kg", StockQuantity = 50, ExpirationDate = DateTime.Today.AddDays(30), Notes = "" },
                new InventoryItem { UniqueId = UniqueIdGenerator.NewId("INV"), Name = "Sparkling Water", Unit = "bottles", StockQuantity = 120, ExpirationDate = DateTime.Today.AddMonths(12), Notes = "" });
        }

        db.SaveChanges();

        if (!db.Tables.Any())
        {
            var firstServerId = db.Employees
                .Where(e => e.Role.ToLower() == "server")
                .OrderBy(e => e.Id)
                .Select(e => e.Id)
                .FirstOrDefault();

            db.Tables.AddRange(
                new Table { UniqueId = UniqueIdGenerator.NewId("TBL"), TableNumber = 1, Name = "Oasis", Capacity = 2, Status = "Available", AssignedServerId = firstServerId == 0 ? null : firstServerId },
                new Table { UniqueId = UniqueIdGenerator.NewId("TBL"), TableNumber = 2, Name = "Aurora", Capacity = 4, Status = "Occupied", AssignedServerId = firstServerId == 0 ? null : firstServerId },
                new Table { UniqueId = UniqueIdGenerator.NewId("TBL"), TableNumber = 7, Name = "Velvet", Capacity = 6, Status = "Available", AssignedServerId = firstServerId == 0 ? null : firstServerId },
                new Table { UniqueId = UniqueIdGenerator.NewId("TBL"), TableNumber = 11, Name = "Imperial", Capacity = 8, Status = "Occupied", AssignedServerId = firstServerId == 0 ? null : firstServerId });
        }

        foreach (var table in db.Tables.Where(t => string.IsNullOrWhiteSpace(t.Name)))
        {
            table.Name = $"Table {table.TableNumber}";
        }

        EnsureUniqueIds(db);
        EnsureNoDuplicateUniqueIds(db);
        EnsureUniqueTableNumbers(db);
        NormalizeProductSections(db);
        SeedDefaultProductIngredients(db);
        EnsureMinimumStaff(db, minimumEmployees: 12);
        EnsureExpandedInventory(db, minimumInventoryItems: 20);
        EnsureExpandedMenuCatalog(db, minimumProducts: 56);
        EnsureShiftCoverage(db);
        EnsureTablesCoverage(db, minimumTables: 12);
        EnsureProductIngredientCoverage(db);

        EnsureUniqueIds(db);
        EnsureNoDuplicateUniqueIds(db);
        EnsureUniqueTableNumbers(db);
        NormalizeProductSections(db);

        db.SaveChanges();
        EnsureHistoricalActivity(db, days: 14);
        DataReconciler.RunFinancialConsistency(db);
        db.SaveChanges();
    }

    private static void EnsureUniqueIds(AppDbContext db)
    {
        foreach (var employee in db.Employees.Where(e => string.IsNullOrWhiteSpace(e.UniqueId)))
            employee.UniqueId = UniqueIdGenerator.NewId("EMP");

        foreach (var table in db.Tables.Where(t => string.IsNullOrWhiteSpace(t.UniqueId)))
            table.UniqueId = UniqueIdGenerator.NewId("TBL");

        foreach (var product in db.Products.Where(p => string.IsNullOrWhiteSpace(p.UniqueId)))
            product.UniqueId = UniqueIdGenerator.NewId("MEN");

        foreach (var item in db.InventoryItems.Where(i => string.IsNullOrWhiteSpace(i.UniqueId)))
            item.UniqueId = UniqueIdGenerator.NewId("INV");

        foreach (var order in db.Orders.Where(o => string.IsNullOrWhiteSpace(o.UniqueId)))
            order.UniqueId = UniqueIdGenerator.NewId("ORD");
    }

    private static void EnsureNoDuplicateUniqueIds(AppDbContext db)
    {
        FixDuplicateIds(db.Employees, e => e.UniqueId, (e, id) => e.UniqueId = id, "EMP");
        FixDuplicateIds(db.Tables, t => t.UniqueId, (t, id) => t.UniqueId = id, "TBL");
        FixDuplicateIds(db.Products, p => p.UniqueId, (p, id) => p.UniqueId = id, "MEN");
        FixDuplicateIds(db.InventoryItems, i => i.UniqueId, (i, id) => i.UniqueId = id, "INV");
        FixDuplicateIds(db.Orders, o => o.UniqueId, (o, id) => o.UniqueId = id, "ORD");
    }

    private static void FixDuplicateIds<TEntity>(
        IQueryable<TEntity> query,
        Func<TEntity, string> getter,
        Action<TEntity, string> setter,
        string prefix) where TEntity : class
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in query)
        {
            var id = getter(entity);
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
            {
                setter(entity, UniqueIdGenerator.NewId(prefix));
            }
        }
    }

    private static void EnsureUniqueTableNumbers(AppDbContext db)
    {
        var used = new HashSet<int>();
        var tables = db.Tables.OrderBy(t => t.Id).ToList();
        var next = tables.Count == 0 ? 1 : tables.Max(t => t.TableNumber) + 1;

        foreach (var table in tables)
        {
            if (table.TableNumber <= 0 || !used.Add(table.TableNumber))
            {
                while (!used.Add(next))
                    next++;
                table.TableNumber = next;
                next++;
            }
        }
    }

    private static void NormalizeProductSections(AppDbContext db)
    {
        foreach (var product in db.Products)
        {
            var category = product.Category.Trim();
            if (category.Equals("Starter", StringComparison.OrdinalIgnoreCase))
                product.Category = "Starter/Appetizer";

            if (string.IsNullOrWhiteSpace(product.SubCategory))
            {
                product.SubCategory = product.Category switch
                {
                    "Drink" => "Soft Drink",
                    "Main" => "Meat Meal",
                    "Dessert" => "Dessert",
                    "Starter/Appetizer" => "Starter/Appetizer",
                    _ => product.Category
                };
            }
        }
    }

    private static void SeedDefaultProductIngredients(AppDbContext db)
    {
        if (db.ProductIngredients.Any())
            return;

        var invByName = db.InventoryItems.ToDictionary(i => i.Name, i => i);
        var prodByName = db.Products.ToDictionary(p => p.Name, p => p);

        void Link(string productName, string ingredientName, decimal qty)
        {
            if (!prodByName.TryGetValue(productName, out var product)) return;
            if (!invByName.TryGetValue(ingredientName, out var ingredient)) return;
            db.ProductIngredients.Add(new ProductIngredient
            {
                ProductId = product.Id,
                InventoryItemId = ingredient.Id,
                Quantity = qty
            });
        }

        Link("Filet Mignon", "Beef", 0.35m);
        Link("Filet Mignon", "Potatoes", 0.20m);
        Link("Truffle Arancini", "Rice", 0.15m);
        Link("Creme Brulee", "Avocado", 0.00m);
        Link("Sapphire Spritz", "Sparkling Water", 1.00m);
    }

    private static void EnsureMinimumStaff(AppDbContext db, int minimumEmployees)
    {
        if (db.Employees.Count() >= minimumEmployees)
            return;

        var templates = new (string Name, string Role, decimal HourlyRate, string Phone)[]
        {
            ("Noah Rivers", "Server", 15m, "+1 555-0106"),
            ("Ava Moretti", "Server", 15m, "+1 555-0107"),
            ("Lucas Bennett", "Server", 16m, "+1 555-0108"),
            ("Mia Chen", "Server", 16m, "+1 555-0109"),
            ("Daniel Carter", "Host", 14m, "+1 555-0110"),
            ("Nina Alvarez", "Barman", 18m, "+1 555-0111"),
            ("Oscar Hughes", "Chef", 24m, "+1 555-0112"),
            ("Priya Shah", "Sous Chef", 22m, "+1 555-0113"),
            ("Isabella Lopez", "Runner", 13m, "+1 555-0114"),
            ("Ethan Blake", "Server", 16m, "+1 555-0115"),
            ("Grace Donovan", "Server", 16m, "+1 555-0116"),
            ("Henry Silva", "Manager", 29m, "+1 555-0117")
        };

        var existingNames = db.Employees
            .Select(e => e.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var index = 0;
        while (db.Employees.Count() < minimumEmployees && index < templates.Length)
        {
            var item = templates[index++];
            if (existingNames.Contains(item.Name))
                continue;

            var pin = (5000 + index * 17).ToString(CultureInfo.InvariantCulture);
            var signIn = item.Role.Equals("Server", StringComparison.OrdinalIgnoreCase)
                || item.Role.Equals("Cashier", StringComparison.OrdinalIgnoreCase)
                ? $"S{index:D2}"
                : item.Role.Equals("Chef", StringComparison.OrdinalIgnoreCase)
                  || item.Role.Equals("Barman", StringComparison.OrdinalIgnoreCase)
                  || item.Role.Equals("Sous Chef", StringComparison.OrdinalIgnoreCase)
                    ? $"K{index:D2}"
                    : string.Empty;

            db.Employees.Add(new Employee
            {
                UniqueId = UniqueIdGenerator.NewId("EMP"),
                SignInId = signIn,
                Name = item.Name,
                Role = item.Role,
                PinCode = EmployeePinHasher.HashForStorage(pin),
                PhoneNumber = item.Phone,
                HourlyRate = item.HourlyRate,
                JoinDate = DateTime.Today.AddDays(-(40 + index * 9)),
                EmploymentStatus = "Active"
            });
        }
    }

    private static void EnsureExpandedInventory(AppDbContext db, int minimumInventoryItems)
    {
        if (db.InventoryItems.Count() >= minimumInventoryItems)
            return;

        var templates = new (string Name, string Unit, decimal Stock, int ExpiryDays)[]
        {
            ("Salmon", "kg", 18m, 5), ("Shrimp", "kg", 14m, 4), ("Pasta", "kg", 50m, 300),
            ("Tomatoes", "kg", 35m, 7), ("Onions", "kg", 32m, 20), ("Garlic", "kg", 10m, 25),
            ("Parmesan", "kg", 12m, 30), ("Lettuce", "kg", 20m, 5), ("Mushrooms", "kg", 22m, 6),
            ("Cream", "l", 26m, 7), ("Butter", "kg", 20m, 25), ("Eggs", "pcs", 300m, 12),
            ("Flour", "kg", 70m, 180), ("Chocolate", "kg", 16m, 120), ("Lemon", "pcs", 160m, 10),
            ("Basil", "kg", 5m, 4), ("Olive Oil", "l", 35m, 200), ("Sparkling Wine", "bottles", 42m, 365),
            ("Orange Juice", "l", 40m, 20), ("Mint", "kg", 4m, 5), ("Soda", "bottles", 180m, 365),
            ("Mozzarella", "kg", 17m, 14), ("Bread", "pcs", 120m, 3), ("Vanilla", "kg", 3m, 200)
        };

        var existing = db.InventoryItems
            .Select(i => i.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var item in templates)
        {
            if (db.InventoryItems.Count() >= minimumInventoryItems)
                break;
            if (existing.Contains(item.Name))
                continue;

            db.InventoryItems.Add(new InventoryItem
            {
                UniqueId = UniqueIdGenerator.NewId("INV"),
                Name = item.Name,
                Unit = item.Unit,
                StockQuantity = item.Stock,
                ExpirationDate = DateTime.Today.AddDays(item.ExpiryDays),
                Notes = string.Empty
            });
        }
    }

    private static void EnsureExpandedMenuCatalog(AppDbContext db, int minimumProducts)
    {
        if (db.Products.Count() >= minimumProducts)
            return;

        var catalog = new (string Name, string Category, string SubCategory, decimal Price)[]
        {
            ("Bruschetta Trio", "Starter/Appetizer", "Starter/Appetizer", 12.00m),
            ("Calamari Fritti", "Starter/Appetizer", "Starter/Appetizer", 13.50m),
            ("Caprese Skewers", "Starter/Appetizer", "Starter/Appetizer", 11.00m),
            ("Crispy Zucchini", "Starter/Appetizer", "Starter/Appetizer", 10.50m),
            ("Smoked Salmon Bites", "Starter/Appetizer", "Starter/Appetizer", 14.00m),
            ("Minestrone", "Starter/Appetizer", "Soup", 9.50m),
            ("Lobster Bisque", "Starter/Appetizer", "Soup", 15.00m),
            ("Caesar Salad", "Starter/Appetizer", "Salad", 10.00m),
            ("Greek Salad", "Starter/Appetizer", "Salad", 10.50m),
            ("Roasted Beet Salad", "Starter/Appetizer", "Salad", 11.00m),
            ("Margherita Pizza", "Main", "Pizza", 18.00m),
            ("Pepperoni Pizza", "Main", "Pizza", 19.50m),
            ("Mushroom Truffle Pizza", "Main", "Pizza", 22.00m),
            ("Spaghetti Carbonara", "Main", "Pasta", 19.00m),
            ("Penne Arrabbiata", "Main", "Pasta", 17.50m),
            ("Seafood Linguine", "Main", "Pasta", 24.00m),
            ("Lasagna Classica", "Main", "Pasta", 20.00m),
            ("Grilled Salmon", "Main", "Seafood", 27.00m),
            ("Shrimp Risotto", "Main", "Seafood", 26.00m),
            ("Chicken Parmesan", "Main", "Meat Meal", 23.00m),
            ("Ribeye Steak", "Main", "Meat Meal", 33.00m),
            ("Braised Lamb", "Main", "Meat Meal", 31.50m),
            ("Vegetable Risotto", "Main", "Vegetarian", 20.00m),
            ("Eggplant Parmigiana", "Main", "Vegetarian", 19.50m),
            ("Mushroom Gnocchi", "Main", "Vegetarian", 21.00m),
            ("Cheeseburger Deluxe", "Main", "Burger", 18.50m),
            ("Chicken Burger", "Main", "Burger", 17.50m),
            ("Veggie Burger", "Main", "Burger", 17.00m),
            ("Fish and Chips", "Main", "Seafood", 22.50m),
            ("Saffron Paella", "Main", "Seafood", 29.00m),
            ("Creme Caramel", "Dessert", "Dessert", 9.00m),
            ("Chocolate Lava Cake", "Dessert", "Dessert", 10.00m),
            ("Tiramisu", "Dessert", "Dessert", 9.50m),
            ("Panna Cotta", "Dessert", "Dessert", 9.00m),
            ("Gelato Trio", "Dessert", "Dessert", 8.50m),
            ("Apple Tart", "Dessert", "Dessert", 9.25m),
            ("Berry Cheesecake", "Dessert", "Dessert", 9.75m),
            ("Espresso", "Drink", "Coffee", 4.00m),
            ("Cappuccino", "Drink", "Coffee", 5.00m),
            ("Latte", "Drink", "Coffee", 5.50m),
            ("Americano", "Drink", "Coffee", 4.50m),
            ("Hot Chocolate", "Drink", "Hot Drink", 5.50m),
            ("Iced Tea", "Drink", "Soft Drink", 4.75m),
            ("Lemonade", "Drink", "Soft Drink", 4.50m),
            ("Fresh Orange Juice", "Drink", "Juice", 5.25m),
            ("Sparkling Lemon Soda", "Drink", "Soft Drink", 4.95m),
            ("Mojito", "Drink", "Cocktail", 12.00m),
            ("Negroni", "Drink", "Cocktail", 13.00m),
            ("Margarita", "Drink", "Cocktail", 12.50m),
            ("Aperol Spritz", "Drink", "Cocktail", 11.50m),
            ("Old Fashioned", "Drink", "Cocktail", 13.50m),
            ("Virgin Mojito", "Drink", "Mocktail", 8.00m),
            ("Sunset Cooler", "Drink", "Mocktail", 8.50m),
            ("Cucumber Fizz", "Drink", "Mocktail", 8.25m),
            ("House Red Wine", "Drink", "Wine", 9.50m),
            ("House White Wine", "Drink", "Wine", 9.50m),
            ("RosÃ© by Glass", "Drink", "Wine", 10.00m),
            ("Prosecco Glass", "Drink", "Wine", 10.50m)
        };

        var existing = db.Products
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var product in catalog)
        {
            if (db.Products.Count() >= minimumProducts)
                break;
            if (existing.Contains(product.Name))
                continue;

            db.Products.Add(new Product
            {
                UniqueId = UniqueIdGenerator.NewId("MEN"),
                Name = product.Name,
                Category = product.Category,
                SubCategory = product.SubCategory,
                Price = product.Price
            });
        }
    }

    private static void EnsureShiftCoverage(AppDbContext db)
    {
        var patterns = new[]
        {
            new[] { "Morning", "Morning", "Morning", "Morning", "Morning", "Off", "Off" },
            new[] { "Evening", "Evening", "Evening", "Evening", "Evening", "Evening", "Off" },
            new[] { "Afternoon", "Afternoon", "Afternoon", "Afternoon", "Afternoon", "Off", "Morning" },
            new[] { "Morning", "Off", "Morning", "Off", "Morning", "Evening", "Evening" }
        };

        var employees = db.Employees.OrderBy(e => e.Id).ToList();
        for (var i = 0; i < employees.Count; i++)
        {
            var e = employees[i];
            var p = patterns[i % patterns.Length];
            e.MondayShift = p[0];
            e.TuesdayShift = p[1];
            e.WednesdayShift = p[2];
            e.ThursdayShift = p[3];
            e.FridayShift = p[4];
            e.SaturdayShift = p[5];
            e.SundayShift = p[6];
        }
    }

    private static void EnsureTablesCoverage(AppDbContext db, int minimumTables)
    {
        if (db.Tables.Count() >= minimumTables)
            return;

        var servers = db.Employees
            .Where(e => e.Role.ToLower().Contains("server"))
            .OrderBy(e => e.Id)
            .ToList();

        var random = new Random(991);
        var nextTableNumber = db.Tables.Any() ? db.Tables.Max(t => t.TableNumber) + 1 : 1;
        var tableNames = new[]
        {
            "Cedar", "Iris", "Nexus", "Luna", "Orchid", "Atlas", "Sapphire", "Coral", "Maple", "Willow", "Nova", "Marina"
        };

        var index = 0;
        while (db.Tables.Count() < minimumTables)
        {
            var server = servers.Count == 0 ? null : servers[index % servers.Count];
            db.Tables.Add(new Table
            {
                UniqueId = UniqueIdGenerator.NewId("TBL"),
                TableNumber = nextTableNumber++,
                Name = tableNames[index % tableNames.Length],
                Capacity = 2 + random.Next(0, 4) * 2,
                Status = random.Next(0, 10) > 2 ? "Available" : "Occupied",
                AssignedServerId = server?.Id
            });
            index++;
        }
    }

    private static void EnsureProductIngredientCoverage(AppDbContext db)
    {
        var inventoryByName = db.InventoryItems
            .ToDictionary(i => i.Name, i => i, StringComparer.OrdinalIgnoreCase);
        if (inventoryByName.Count == 0)
            return;

        var existingPairs = db.ProductIngredients
            .Select(pi => new { pi.ProductId, pi.InventoryItemId })
            .ToHashSet();

        var random = new Random(3277);
        foreach (var product in db.Products.ToList())
        {
            var linkedCount = db.ProductIngredients.Count(pi => pi.ProductId == product.Id);
            if (linkedCount >= 2)
                continue;

            var preferredNames = product.Category switch
            {
                "Drink" => new[] { "Sparkling Water", "Orange Juice", "Lemon", "Mint", "Soda", "Sparkling Wine" },
                "Dessert" => new[] { "Chocolate", "Cream", "Eggs", "Vanilla", "Flour" },
                "Starter/Appetizer" => new[] { "Tomatoes", "Lettuce", "Rice", "Mushrooms", "Olive Oil", "Parmesan" },
                _ => new[] { "Beef", "Chicken", "Pasta", "Tomatoes", "Onions", "Garlic", "Parmesan", "Olive Oil" }
            };

            var candidates = preferredNames
                .Where(inventoryByName.ContainsKey)
                .Select(name => inventoryByName[name])
                .DistinctBy(i => i.Id)
                .ToList();

            if (candidates.Count == 0)
            {
                candidates = inventoryByName.Values
                    .OrderBy(i => i.Name)
                    .Take(4)
                    .ToList();
            }

            foreach (var inventory in candidates.OrderBy(_ => random.Next()).Take(2))
            {
                if (existingPairs.Contains(new { ProductId = product.Id, InventoryItemId = inventory.Id }))
                    continue;

                var qty = product.Category switch
                {
                    "Drink" => 1.00m,
                    "Dessert" => 0.10m,
                    "Starter/Appetizer" => 0.15m,
                    _ => 0.25m
                };

                db.ProductIngredients.Add(new ProductIngredient
                {
                    ProductId = product.Id,
                    InventoryItemId = inventory.Id,
                    Quantity = qty
                });

                existingPairs.Add(new { ProductId = product.Id, InventoryItemId = inventory.Id });
            }
        }
    }

    private static void EnsureHistoricalActivity(AppDbContext db, int days)
    {
        var employees = db.Employees.OrderBy(e => e.Id).ToList();
        var servers = employees.Where(e => e.Role.ToLower().Contains("server")).ToList();
        var tables = db.Tables.OrderBy(t => t.TableNumber).ToList();
        var products = db.Products.OrderBy(p => p.Id).ToList();
        if (employees.Count == 0 || tables.Count == 0 || products.Count == 0)
            return;

        var random = new Random(8808);
        var startDate = DateTime.Today.AddDays(-(days - 1));

        for (var date = startDate.Date; date <= DateTime.Today; date = date.AddDays(1))
        {
            foreach (var employee in employees)
            {
                var shift = GetShiftForDate(employee, date.DayOfWeek);
                if (shift.Equals("Off", StringComparison.OrdinalIgnoreCase))
                    continue;

                var (dayStartUtc, dayEndUtc) = AttendanceCalendar.DayRangeUtc(date);
                var alreadyExists = db.EmployeeAttendances.Any(a =>
                    a.EmployeeId == employee.Id && a.WorkDate >= dayStartUtc && a.WorkDate < dayEndUtc);
                if (alreadyExists)
                    continue;

                var baseHour = shift.Equals("Morning", StringComparison.OrdinalIgnoreCase)
                    ? 9
                    : shift.Equals("Afternoon", StringComparison.OrdinalIgnoreCase)
                        ? 13
                        : 17;

                var minuteOffset = random.Next(0, 18);
                var clockIn = date.Date.AddHours(baseHour).AddMinutes(minuteOffset);
                var clockOut = clockIn.AddHours(8).AddMinutes(random.Next(-12, 16));
                var late = minuteOffset >= 10;

                db.EmployeeAttendances.Add(new EmployeeAttendance
                {
                    EmployeeId = employee.Id,
                    WorkDate = dayStartUtc,
                    ClockInTime = clockIn,
                    ClockOutTime = clockOut,
                    ClockInStatus = late ? "Late" : "On Time",
                    Justification = late ? "Traffic delay" : string.Empty
                });
            }

            var dayStart = date.Date;
            var dayEnd = dayStart.AddDays(1);
            var existingOrders = db.Orders.Count(o => o.CreatedAt >= dayStart && o.CreatedAt < dayEnd);
            var targetOrders = 18;
            var missingOrders = Math.Max(0, targetOrders - existingOrders);

            for (var i = 0; i < missingOrders; i++)
            {
                var table = tables[random.Next(tables.Count)];
                var assignedServer = servers.Count == 0 ? null : servers[random.Next(servers.Count)];
                var createdAt = dayStart.AddHours(11 + random.Next(0, 11)).AddMinutes(random.Next(0, 60));
                var status = random.Next(0, 10) > 1 ? "Completed" : "Waiting";

                var order = new OrderRecord
                {
                    UniqueId = UniqueIdGenerator.NewId("ORD"),
                    TableId = table.Id,
                    TableCode = $"Table {table.TableNumber}",
                    TableName = string.IsNullOrWhiteSpace(table.Name) ? $"Table {table.TableNumber}" : table.Name,
                    ServerId = assignedServer?.Id,
                    ServerName = assignedServer?.Name ?? string.Empty,
                    Status = status,
                    CreatedAt = createdAt,
                    CompletedAt = string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase) ? createdAt : null
                };

                var lineCount = 2 + random.Next(0, 4);
                for (var line = 0; line < lineCount; line++)
                {
                    var product = products[random.Next(products.Count)];
                    var isDrink = string.Equals(product.Category, "Drink", StringComparison.OrdinalIgnoreCase);
                    var preparedBy = isDrink
                        ? employees.FirstOrDefault(e => e.Role.Equals("Barman", StringComparison.OrdinalIgnoreCase))
                        : employees.FirstOrDefault(e => e.Role.Equals("Chef", StringComparison.OrdinalIgnoreCase));
                    order.Items.Add(new OrderItem
                    {
                        ProductId = product.Id,
                        Quantity = 1 + random.Next(0, 3),
                        PreparedByEmployeeId = preparedBy?.Id,
                        PreparedByRole = isDrink ? "Barman" : "Chef",
                        PreparedByName = preparedBy?.Name ?? (isDrink ? "Unassigned Barman" : "Unassigned Chef")
                    });
                }

                db.Orders.Add(order);
            }
        }
    }

    private static string GetShiftForDate(Employee employee, DayOfWeek dayOfWeek)
        => dayOfWeek switch
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
