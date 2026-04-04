using System.IO;
using System.Globalization;
using System.Linq;
using EliteRestaurantPro.Models;
using EliteRestaurantPro.Utils;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurantPro.Data;

public class AppDbContext : DbContext
{
    private static readonly bool BootstrapSampleData = false;
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Table> Tables => Set<Table>();
    public DbSet<OrderRecord> Orders => Set<OrderRecord>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<ProductIngredient> ProductIngredients => Set<ProductIngredient>();
    public DbSet<EmployeeAttendance> EmployeeAttendances => Set<EmployeeAttendance>();
    public DbSet<AttendanceDayValidation> AttendanceDayValidations => Set<AttendanceDayValidation>();
    public DbSet<SalaryAdvance> SalaryAdvances => Set<SalaryAdvance>();
    public DbSet<PayrollPaymentRecord> PayrollPaymentRecords => Set<PayrollPaymentRecord>();
    public DbSet<MoneyTransaction> Transactions => Set<MoneyTransaction>();

    public static string DatabasePath
    {
        get
        {
            var appFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EliteRestaurantPro");

            Directory.CreateDirectory(appFolder);
            return Path.Combine(appFolder, "elite-restaurant-pro.db");
        }
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 30
        };
        optionsBuilder.UseSqlite(csb.ToString());
    }

    public static void Initialize()
    {
        using var db = new AppDbContext();
        db.Database.EnsureCreated();
        EnsureSchema(db);

        if (!BootstrapSampleData)
        {
            EnsureUniqueIndexes();
            return;
        }

        if (!db.Employees.Any())
        {
            db.Employees.AddRange(
                new Employee { UniqueId = UniqueIdGenerator.NewId("EMP"), Name = "Ernest Cole", Role = "Admin", PinCode = "1024", PhoneNumber = "+1 555-0101", HourlyRate = 32m, JoinDate = DateTime.Today.AddYears(-2), EmploymentStatus = "Active" },
                new Employee { UniqueId = UniqueIdGenerator.NewId("EMP"), Name = "Sophia Grant", Role = "Manager", PinCode = "2048", PhoneNumber = "+1 555-0102", HourlyRate = 28m, JoinDate = DateTime.Today.AddYears(-1), EmploymentStatus = "Active" },
                new Employee { UniqueId = UniqueIdGenerator.NewId("EMP"), SignInId = "MARCO", Name = "Marco Bellini", Role = "Chef", PinCode = "3301", PhoneNumber = "+1 555-0103", HourlyRate = 24m, JoinDate = DateTime.Today.AddMonths(-18), EmploymentStatus = "Active" },
                new Employee { UniqueId = UniqueIdGenerator.NewId("EMP"), SignInId = "LIAM", Name = "Liam Foster", Role = "Server", PinCode = "4042", PhoneNumber = "+1 555-0104", HourlyRate = 16m, JoinDate = DateTime.Today.AddMonths(-8), EmploymentStatus = "Active" },
                new Employee { UniqueId = UniqueIdGenerator.NewId("EMP"), SignInId = "EMMA", Name = "Emma Russo", Role = "Server", PinCode = "5560", PhoneNumber = "+1 555-0105", HourlyRate = 16m, JoinDate = DateTime.Today.AddMonths(-6), EmploymentStatus = "Active" },
                new Employee { UniqueId = UniqueIdGenerator.NewId("EMP"), SignInId = "CASH", Name = "Jordan Blake", Role = "Cashier", PinCode = "6001", PhoneNumber = "+1 555-0108", HourlyRate = 18m, JoinDate = DateTime.Today.AddMonths(-10), EmploymentStatus = "Active" });
        }

        if (!db.Employees.Any(e => e.Role.ToLower() == "server"))
        {
            db.Employees.AddRange(
                new Employee { UniqueId = UniqueIdGenerator.NewId("EMP"), SignInId = "NOAH", Name = "Noah Rivers", Role = "Server", PinCode = "4100", PhoneNumber = "+1 555-0106", HourlyRate = 15m, JoinDate = DateTime.Today.AddMonths(-4), EmploymentStatus = "Active" },
                new Employee { UniqueId = UniqueIdGenerator.NewId("EMP"), SignInId = "AVA", Name = "Ava Moretti", Role = "Server", PinCode = "4200", PhoneNumber = "+1 555-0107", HourlyRate = 15m, JoinDate = DateTime.Today.AddMonths(-3), EmploymentStatus = "Active" });
        }

        if (!db.Employees.Any(e => e.Role.Equals("Cashier", StringComparison.OrdinalIgnoreCase)))
        {
            db.Employees.Add(new Employee
            {
                UniqueId = UniqueIdGenerator.NewId("EMP"),
                SignInId = "CASH",
                Name = "Jordan Blake",
                Role = "Cashier",
                PinCode = "6001",
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
        FinancialTransactionService.EnsureCompletedOrderRevenues(db);
        db.SaveChanges();
        EnsureUniqueIndexes();
    }

    private static void EnsureSchema(AppDbContext db)
    {
        using var conn = new SqliteConnection($"Data Source={DatabasePath}");
        conn.Open();

        EnsureColumn(conn, "Employees", "UniqueId", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(conn, "Employees", "SignInId", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(conn, "Employees", "ProfileImagePath", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(conn, "Employees", "PhoneNumber", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(conn, "Employees", "HourlyRate", "REAL NOT NULL DEFAULT 0");
        EnsureColumn(conn, "Employees", "MonthlySalaryUSD", "REAL NOT NULL DEFAULT 0");
        EnsureColumn(conn, "Employees", "JoinDate", "TEXT NOT NULL DEFAULT '2000-01-01 00:00:00'");
        EnsureColumn(conn, "Employees", "EmploymentStatus", "TEXT NOT NULL DEFAULT 'Active'");
        EnsureColumn(conn, "Employees", "Notes", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(conn, "Employees", "MondayShift", "TEXT NOT NULL DEFAULT 'Off'");
        EnsureColumn(conn, "Employees", "TuesdayShift", "TEXT NOT NULL DEFAULT 'Off'");
        EnsureColumn(conn, "Employees", "WednesdayShift", "TEXT NOT NULL DEFAULT 'Off'");
        EnsureColumn(conn, "Employees", "ThursdayShift", "TEXT NOT NULL DEFAULT 'Off'");
        EnsureColumn(conn, "Employees", "FridayShift", "TEXT NOT NULL DEFAULT 'Off'");
        EnsureColumn(conn, "Employees", "SaturdayShift", "TEXT NOT NULL DEFAULT 'Off'");
        EnsureColumn(conn, "Employees", "SundayShift", "TEXT NOT NULL DEFAULT 'Off'");
        EnsureColumn(conn, "Products", "UniqueId", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(conn, "Products", "SubCategory", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(conn, "Tables", "Name", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(conn, "Tables", "UniqueId", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(conn, "Tables", "AssignedServerId", "INTEGER NULL");
        EnsureColumn(conn, "InventoryItems", "ExpirationDate", "TEXT NULL");
        EnsureColumn(conn, "InventoryItems", "Notes", "TEXT NOT NULL DEFAULT ''");

        if (!TableExists(conn, "Orders"))
        {
            CreateOrdersTable(conn);
        }
        else
        {
            var ordersSql = GetTableSql(conn, "Orders");
            var requiresRebuild =
                !ColumnExists(conn, "Orders", "TableCode") ||
                !ColumnExists(conn, "Orders", "TableName") ||
                !ColumnExists(conn, "Orders", "ServerId") ||
                !ColumnExists(conn, "Orders", "ServerName") ||
                !ColumnExists(conn, "Orders", "UniqueId") ||
                ordersSql.Contains("ON DELETE CASCADE", StringComparison.OrdinalIgnoreCase);

            if (requiresRebuild)
            {
                RebuildOrdersTable(conn);
            }
        }
        EnsureColumn(conn, "Orders", "CustomerNotes", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(conn, "Orders", "AllergyNotes", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(conn, "Orders", "PaymentCurrencyCode", "TEXT NOT NULL DEFAULT 'USD'");
        EnsureColumn(conn, "Orders", "PaymentAmount", "REAL NOT NULL DEFAULT 0");
        EnsureColumn(conn, "Orders", "PaymentAmountUsd", "REAL NOT NULL DEFAULT 0");
        EnsureColumn(conn, "Orders", "PaymentAmountFc", "REAL NOT NULL DEFAULT 0");
        EnsureColumn(conn, "Orders", "CustomerPaidUsd", "REAL NOT NULL DEFAULT 0");
        EnsureColumn(conn, "Orders", "CustomerPaidFc", "REAL NOT NULL DEFAULT 0");
        EnsureColumn(conn, "Orders", "ChangeGivenUsd", "REAL NOT NULL DEFAULT 0");
        EnsureColumn(conn, "Orders", "ChangeGivenFc", "REAL NOT NULL DEFAULT 0");
        EnsureColumn(conn, "Orders", "ExchangeRateUsed", "REAL NOT NULL DEFAULT 2250");
        EnsureColumn(conn, "Orders", "DiscountMode", "TEXT NOT NULL DEFAULT 'None'");
        EnsureColumn(conn, "Orders", "DiscountValue", "REAL NOT NULL DEFAULT 0");
        EnsureColumn(conn, "Orders", "DiscountAmountUsd", "REAL NOT NULL DEFAULT 0");
        EnsureColumn(conn, "Orders", "CompletedAt", "TEXT NULL");

        using (var backfill = conn.CreateCommand())
        {
            backfill.CommandText =
                "UPDATE Orders SET CompletedAt = CreatedAt WHERE Status = 'Completed' AND CompletedAt IS NULL;";
            backfill.ExecuteNonQuery();
        }

        if (!TableExists(conn, "OrderItems"))
        {
            CreateOrderItemsTable(conn);
        }
        else
        {
            EnsureColumn(conn, "OrderItems", "PreparedByEmployeeId", "INTEGER NULL");
            EnsureColumn(conn, "OrderItems", "PreparedByRole", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(conn, "OrderItems", "PreparedByName", "TEXT NOT NULL DEFAULT ''");
        }

        if (!TableExists(conn, "InventoryItems"))
        {
            CreateInventoryItemsTable(conn);
        }

        if (!TableExists(conn, "ProductIngredients"))
        {
            CreateProductIngredientsTable(conn);
        }

        if (!TableExists(conn, "EmployeeAttendances"))
        {
            CreateEmployeeAttendancesTable(conn);
        }
        else
        {
            EnsureColumn(conn, "EmployeeAttendances", "ClockInStatus", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(conn, "EmployeeAttendances", "Justification", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(conn, "EmployeeAttendances", "IsAbsence", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumn(conn, "EmployeeAttendances", "AbsenceJustification", "TEXT NOT NULL DEFAULT ''");
        }

        if (!TableExists(conn, "AttendanceDayValidations"))
            CreateAttendanceDayValidationsTable(conn);

        if (!TableExists(conn, "SalaryAdvances"))
            CreateSalaryAdvancesTable(conn);
        else
        {
            EnsureColumn(conn, "SalaryAdvances", "ForPayrollYear", "INTEGER NULL");
            EnsureColumn(conn, "SalaryAdvances", "ForPayrollMonth", "INTEGER NULL");
        }

        if (!TableExists(conn, "PayrollPaymentRecords"))
            CreatePayrollPaymentRecordsTable(conn);

        if (!TableExists(conn, "Transactions"))
        {
            CreateTransactionsTable(conn);
        }
        else
        {
            EnsureColumn(conn, "Transactions", "Amount", "REAL NOT NULL DEFAULT 0");
            EnsureColumn(conn, "Transactions", "AmountUsd", "REAL NOT NULL DEFAULT 0");
            EnsureColumn(conn, "Transactions", "AmountFc", "REAL NOT NULL DEFAULT 0");
            EnsureColumn(conn, "Transactions", "Date", "TEXT NOT NULL DEFAULT '2000-01-01 00:00:00'");
            EnsureColumn(conn, "Transactions", "Type", "TEXT NOT NULL DEFAULT 'Expense'");
            EnsureColumn(conn, "Transactions", "Category", "TEXT NOT NULL DEFAULT 'Variable'");
            EnsureColumn(conn, "Transactions", "CurrencyCode", "TEXT NOT NULL DEFAULT 'USD'");
            EnsureColumn(conn, "Transactions", "ExchangeRateUsed", "REAL NOT NULL DEFAULT 2250");
            EnsureColumn(conn, "Transactions", "Justification", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(conn, "Transactions", "IsFixed", "INTEGER NOT NULL DEFAULT 0");
        }

    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Table>()
            .HasOne(t => t.AssignedServer)
            .WithMany()
            .HasForeignKey(t => t.AssignedServerId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<OrderRecord>().ToTable("Orders");
        modelBuilder.Entity<OrderItem>().ToTable("OrderItems");
        modelBuilder.Entity<InventoryItem>().ToTable("InventoryItems");
        modelBuilder.Entity<ProductIngredient>().ToTable("ProductIngredients");
        modelBuilder.Entity<EmployeeAttendance>().ToTable("EmployeeAttendances");
        modelBuilder.Entity<AttendanceDayValidation>().ToTable("AttendanceDayValidations");
        modelBuilder.Entity<SalaryAdvance>().ToTable("SalaryAdvances");
        modelBuilder.Entity<PayrollPaymentRecord>().ToTable("PayrollPaymentRecords");
        modelBuilder.Entity<MoneyTransaction>().ToTable("Transactions");

        modelBuilder.Entity<Employee>().HasIndex(e => e.UniqueId).IsUnique();
        modelBuilder.Entity<Employee>()
            .HasIndex(e => e.SignInId)
            .IsUnique()
            .HasFilter("SignInId IS NOT NULL AND SignInId <> ''");
        modelBuilder.Entity<Product>().HasIndex(p => p.UniqueId).IsUnique();
        modelBuilder.Entity<Table>().HasIndex(t => t.UniqueId).IsUnique();
        modelBuilder.Entity<Table>().HasIndex(t => t.TableNumber).IsUnique();
        modelBuilder.Entity<OrderRecord>().HasIndex(o => o.UniqueId).IsUnique();
        modelBuilder.Entity<InventoryItem>().HasIndex(i => i.UniqueId).IsUnique();
        modelBuilder.Entity<EmployeeAttendance>()
            .HasIndex(a => new { a.EmployeeId, a.WorkDate })
            .IsUnique();

        modelBuilder.Entity<AttendanceDayValidation>()
            .HasIndex(v => v.WorkDate)
            .IsUnique();

        modelBuilder.Entity<SalaryAdvance>()
            .HasOne(a => a.Employee)
            .WithMany()
            .HasForeignKey(a => a.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PayrollPaymentRecord>()
            .HasOne(p => p.Employee)
            .WithMany()
            .HasForeignKey(p => p.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PayrollPaymentRecord>()
            .HasIndex(p => new { p.EmployeeId, p.Year, p.Month })
            .IsUnique();

        modelBuilder.Entity<OrderRecord>()
            .HasMany(o => o.Items)
            .WithOne(i => i.OrderRecord)
            .HasForeignKey(i => i.OrderRecordId);

        modelBuilder.Entity<OrderItem>()
            .HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId);

        modelBuilder.Entity<OrderRecord>()
            .HasOne(o => o.Table)
            .WithMany()
            .HasForeignKey(o => o.TableId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<OrderRecord>()
            .HasOne(o => o.Server)
            .WithMany()
            .HasForeignKey(o => o.ServerId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ProductIngredient>()
            .HasOne(pi => pi.Product)
            .WithMany(p => p.Ingredients)
            .HasForeignKey(pi => pi.ProductId);

        modelBuilder.Entity<ProductIngredient>()
            .HasOne(pi => pi.InventoryItem)
            .WithMany(i => i.ProductIngredients)
            .HasForeignKey(pi => pi.InventoryItemId);

        modelBuilder.Entity<EmployeeAttendance>()
            .HasOne(a => a.Employee)
            .WithMany()
            .HasForeignKey(a => a.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MoneyTransaction>()
            .HasIndex(t => new { t.Date, t.Type });
    }

    private static bool TableExists(SqliteConnection conn, string tableName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name;";
        cmd.Parameters.AddWithValue("$name", tableName);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static bool ColumnExists(SqliteConnection conn, string tableName, string columnName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string GetTableSql(SqliteConnection conn, string tableName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name=$name;";
        cmd.Parameters.AddWithValue("$name", tableName);
        return cmd.ExecuteScalar()?.ToString() ?? string.Empty;
    }

    private static void EnsureColumn(SqliteConnection conn, string tableName, string columnName, string columnDefinition)
    {
        if (ColumnExists(conn, tableName, columnName))
            return;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
        cmd.ExecuteNonQuery();
    }

    private static void CreateOrdersTable(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Orders (
                Id INTEGER NOT NULL CONSTRAINT PK_Orders PRIMARY KEY AUTOINCREMENT,
                UniqueId TEXT NOT NULL DEFAULT '',
                TableId INTEGER NULL,
                TableCode TEXT NOT NULL DEFAULT '',
                TableName TEXT NOT NULL DEFAULT '',
                ServerId INTEGER NULL,
                ServerName TEXT NOT NULL DEFAULT '',
                Status TEXT NOT NULL,
                CustomerNotes TEXT NOT NULL DEFAULT '',
                AllergyNotes TEXT NOT NULL DEFAULT '',
                PaymentCurrencyCode TEXT NOT NULL DEFAULT 'USD',
                PaymentAmount REAL NOT NULL DEFAULT 0,
                PaymentAmountUsd REAL NOT NULL DEFAULT 0,
                PaymentAmountFc REAL NOT NULL DEFAULT 0,
                CustomerPaidUsd REAL NOT NULL DEFAULT 0,
                CustomerPaidFc REAL NOT NULL DEFAULT 0,
                ChangeGivenUsd REAL NOT NULL DEFAULT 0,
                ChangeGivenFc REAL NOT NULL DEFAULT 0,
                ExchangeRateUsed REAL NOT NULL DEFAULT 2250,
                CreatedAt TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    private static void CreateOrderItemsTable(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS OrderItems (
                Id INTEGER NOT NULL CONSTRAINT PK_OrderItems PRIMARY KEY AUTOINCREMENT,
                OrderRecordId INTEGER NOT NULL,
                ProductId INTEGER NOT NULL,
                Quantity INTEGER NOT NULL,
                PreparedByEmployeeId INTEGER NULL,
                PreparedByRole TEXT NOT NULL DEFAULT '',
                PreparedByName TEXT NOT NULL DEFAULT ''
            );
            """;
        cmd.ExecuteNonQuery();
    }

    private static void RebuildOrdersTable(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            PRAGMA foreign_keys=OFF;

            ALTER TABLE Orders RENAME TO Orders_old;

            CREATE TABLE Orders (
                Id INTEGER NOT NULL CONSTRAINT PK_Orders PRIMARY KEY AUTOINCREMENT,
                UniqueId TEXT NOT NULL DEFAULT '',
                TableId INTEGER NULL,
                TableCode TEXT NOT NULL DEFAULT '',
                TableName TEXT NOT NULL DEFAULT '',
                ServerId INTEGER NULL,
                ServerName TEXT NOT NULL DEFAULT '',
                Status TEXT NOT NULL,
                CustomerNotes TEXT NOT NULL DEFAULT '',
                AllergyNotes TEXT NOT NULL DEFAULT '',
                PaymentCurrencyCode TEXT NOT NULL DEFAULT 'USD',
                PaymentAmount REAL NOT NULL DEFAULT 0,
                PaymentAmountUsd REAL NOT NULL DEFAULT 0,
                PaymentAmountFc REAL NOT NULL DEFAULT 0,
                CustomerPaidUsd REAL NOT NULL DEFAULT 0,
                CustomerPaidFc REAL NOT NULL DEFAULT 0,
                ChangeGivenUsd REAL NOT NULL DEFAULT 0,
                ChangeGivenFc REAL NOT NULL DEFAULT 0,
                ExchangeRateUsed REAL NOT NULL DEFAULT 2250,
                CreatedAt TEXT NOT NULL
            );

            INSERT INTO Orders (Id, UniqueId, TableId, TableCode, TableName, ServerId, ServerName, Status, CustomerNotes, AllergyNotes, PaymentCurrencyCode, PaymentAmount, PaymentAmountUsd, PaymentAmountFc, CustomerPaidUsd, CustomerPaidFc, ChangeGivenUsd, ChangeGivenFc, ExchangeRateUsed, CreatedAt)
            SELECT
                o.Id,
                '',
                o.TableId,
                CASE
                    WHEN t.TableNumber IS NOT NULL THEN 'Table ' || t.TableNumber
                    ELSE 'Table #' || COALESCE(o.TableId, 0)
                END,
                CASE
                    WHEN t.Name IS NOT NULL AND t.Name <> '' THEN t.Name
                    WHEN t.TableNumber IS NOT NULL THEN 'Table ' || t.TableNumber
                    ELSE 'Deleted Table'
                END,
                NULL,
                '',
                o.Status,
                '',
                '',
                'USD',
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                2250,
                o.CreatedAt
            FROM Orders_old o
            LEFT JOIN Tables t ON t.Id = o.TableId;

            DROP TABLE Orders_old;

            ALTER TABLE OrderItems RENAME TO OrderItems_old;

            CREATE TABLE OrderItems (
                Id INTEGER NOT NULL CONSTRAINT PK_OrderItems PRIMARY KEY AUTOINCREMENT,
                OrderRecordId INTEGER NOT NULL,
                ProductId INTEGER NOT NULL,
                Quantity INTEGER NOT NULL,
                PreparedByEmployeeId INTEGER NULL,
                PreparedByRole TEXT NOT NULL DEFAULT '',
                PreparedByName TEXT NOT NULL DEFAULT ''
            );

            INSERT INTO OrderItems (Id, OrderRecordId, ProductId, Quantity, PreparedByEmployeeId, PreparedByRole, PreparedByName)
            SELECT Id, OrderRecordId, ProductId, Quantity, NULL, '', ''
            FROM OrderItems_old;

            DROP TABLE OrderItems_old;

            PRAGMA foreign_keys=ON;
            """;
        cmd.ExecuteNonQuery();
    }

    private static void CreateInventoryItemsTable(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS InventoryItems (
                Id INTEGER NOT NULL CONSTRAINT PK_InventoryItems PRIMARY KEY AUTOINCREMENT,
                UniqueId TEXT NOT NULL DEFAULT '',
                Name TEXT NOT NULL,
                Unit TEXT NOT NULL,
                StockQuantity REAL NOT NULL,
                ExpirationDate TEXT NULL,
                Notes TEXT NOT NULL DEFAULT ''
            );
            """;
        cmd.ExecuteNonQuery();
    }

    private static void CreateProductIngredientsTable(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS ProductIngredients (
                Id INTEGER NOT NULL CONSTRAINT PK_ProductIngredients PRIMARY KEY AUTOINCREMENT,
                ProductId INTEGER NOT NULL,
                InventoryItemId INTEGER NOT NULL,
                Quantity REAL NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    private static void CreateEmployeeAttendancesTable(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS EmployeeAttendances (
                Id INTEGER NOT NULL CONSTRAINT PK_EmployeeAttendances PRIMARY KEY AUTOINCREMENT,
                EmployeeId INTEGER NOT NULL,
                WorkDate TEXT NOT NULL,
                ClockInTime TEXT NULL,
                ClockOutTime TEXT NULL,
                ClockInStatus TEXT NOT NULL DEFAULT '',
                Justification TEXT NOT NULL DEFAULT '',
                IsAbsence INTEGER NOT NULL DEFAULT 0,
                AbsenceJustification TEXT NOT NULL DEFAULT ''
            );
            """;
        cmd.ExecuteNonQuery();
    }

    private static void CreateTransactionsTable(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Transactions (
                Id INTEGER NOT NULL CONSTRAINT PK_Transactions PRIMARY KEY AUTOINCREMENT,
                Amount REAL NOT NULL,
                AmountUsd REAL NOT NULL DEFAULT 0,
                AmountFc REAL NOT NULL DEFAULT 0,
                Date TEXT NOT NULL,
                Type TEXT NOT NULL DEFAULT 'Expense',
                Category TEXT NOT NULL DEFAULT 'Variable',
                CurrencyCode TEXT NOT NULL DEFAULT 'USD',
                ExchangeRateUsed REAL NOT NULL DEFAULT 2250,
                Justification TEXT NOT NULL DEFAULT '',
                IsFixed INTEGER NOT NULL DEFAULT 0
            );
            """;
        cmd.ExecuteNonQuery();
    }

    private static void EnsureUniqueIndexes(SqliteConnection conn)
    {
        ExecuteNonQuery(conn, "CREATE UNIQUE INDEX IF NOT EXISTS IX_Employees_UniqueId ON Employees (UniqueId);");
        ExecuteNonQuery(conn, """
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Employees_SignInId ON Employees (SignInId COLLATE NOCASE) WHERE SignInId <> '';
            """);
        ExecuteNonQuery(conn, "CREATE UNIQUE INDEX IF NOT EXISTS IX_Products_UniqueId ON Products (UniqueId);");
        ExecuteNonQuery(conn, "CREATE UNIQUE INDEX IF NOT EXISTS IX_Tables_UniqueId ON Tables (UniqueId);");
        ExecuteNonQuery(conn, "CREATE UNIQUE INDEX IF NOT EXISTS IX_Tables_TableNumber ON Tables (TableNumber);");
        ExecuteNonQuery(conn, "CREATE UNIQUE INDEX IF NOT EXISTS IX_Orders_UniqueId ON Orders (UniqueId);");
        ExecuteNonQuery(conn, "CREATE UNIQUE INDEX IF NOT EXISTS IX_InventoryItems_UniqueId ON InventoryItems (UniqueId);");
        ExecuteNonQuery(conn, "CREATE UNIQUE INDEX IF NOT EXISTS IX_EmployeeAttendances_EmployeeId_WorkDate ON EmployeeAttendances (EmployeeId, WorkDate);");
        ExecuteNonQuery(conn, "CREATE UNIQUE INDEX IF NOT EXISTS IX_AttendanceDayValidations_WorkDate ON AttendanceDayValidations (WorkDate);");
        ExecuteNonQuery(conn, "CREATE UNIQUE INDEX IF NOT EXISTS IX_PayrollPaymentRecords_Employee_Year_Month ON PayrollPaymentRecords (EmployeeId, Year, Month);");
    }

    private static void CreateSalaryAdvancesTable(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS SalaryAdvances (
                Id INTEGER NOT NULL CONSTRAINT PK_SalaryAdvances PRIMARY KEY AUTOINCREMENT,
                EmployeeId INTEGER NOT NULL,
                AmountUsd REAL NOT NULL DEFAULT 0,
                GivenAt TEXT NOT NULL,
                ForPayrollYear INTEGER NULL,
                ForPayrollMonth INTEGER NULL,
                AppliedPayrollYear INTEGER NULL,
                AppliedPayrollMonth INTEGER NULL,
                Note TEXT NOT NULL DEFAULT '',
                FOREIGN KEY (EmployeeId) REFERENCES Employees (Id) ON DELETE CASCADE
            );
            """;
        cmd.ExecuteNonQuery();
        ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS IX_SalaryAdvances_EmployeeId ON SalaryAdvances (EmployeeId);");
    }

    private static void CreatePayrollPaymentRecordsTable(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS PayrollPaymentRecords (
                Id INTEGER NOT NULL CONSTRAINT PK_PayrollPaymentRecords PRIMARY KEY AUTOINCREMENT,
                EmployeeId INTEGER NOT NULL,
                Year INTEGER NOT NULL,
                Month INTEGER NOT NULL,
                MonthlySalaryUsd REAL NOT NULL DEFAULT 0,
                AbsenceDays INTEGER NOT NULL DEFAULT 0,
                LateDays INTEGER NOT NULL DEFAULT 0,
                LatePenaltyUnits INTEGER NOT NULL DEFAULT 0,
                TotalDeductionUnits INTEGER NOT NULL DEFAULT 0,
                MoneyGeneratedUsd REAL NOT NULL DEFAULT 0,
                BonusFivePercentUsd REAL NOT NULL DEFAULT 0,
                AdvancesDeductedUsd REAL NOT NULL DEFAULT 0,
                NetPayUsd REAL NOT NULL DEFAULT 0,
                PaidAtUtc TEXT NOT NULL,
                FOREIGN KEY (EmployeeId) REFERENCES Employees (Id) ON DELETE CASCADE
            );
            """;
        cmd.ExecuteNonQuery();
        ExecuteNonQuery(conn, "CREATE UNIQUE INDEX IF NOT EXISTS IX_PayrollPaymentRecords_Employee_Year_Month ON PayrollPaymentRecords (EmployeeId, Year, Month);");
    }

    private static void CreateAttendanceDayValidationsTable(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS AttendanceDayValidations (
                Id INTEGER NOT NULL CONSTRAINT PK_AttendanceDayValidations PRIMARY KEY AUTOINCREMENT,
                WorkDate TEXT NOT NULL,
                ValidatedAtUtc TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
        ExecuteNonQuery(conn, "CREATE UNIQUE INDEX IF NOT EXISTS IX_AttendanceDayValidations_WorkDate ON AttendanceDayValidations (WorkDate);");
    }

    private static void EnsureUniqueIndexes()
    {
        using var conn = new SqliteConnection($"Data Source={DatabasePath}");
        conn.Open();
        EnsureUniqueIndexes(conn);
    }

    private static void ExecuteNonQuery(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
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
                PinCode = pin,
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
            ("Rosé by Glass", "Drink", "Wine", 10.00m),
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

                var alreadyExists = db.EmployeeAttendances.Any(a => a.EmployeeId == employee.Id && a.WorkDate.Date == date.Date);
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
                    WorkDate = date.Date,
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

    /// <summary>Same definition as admin active list: Waiting, In Kitchen, Ready, or Served (case-insensitive).</summary>
    public static bool IsActiveOrderStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;
        var s = status.Trim();
        return string.Equals(s, "Waiting", StringComparison.OrdinalIgnoreCase)
               || string.Equals(s, "In Kitchen", StringComparison.OrdinalIgnoreCase)
               || string.Equals(s, "Ready", StringComparison.OrdinalIgnoreCase)
               || string.Equals(s, OrderWorkflow.Served, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Deletes all in-progress orders and their line items, then sets affected tables to Available when no other active order uses them.
    /// Completed and Cancelled orders are left unchanged.
    /// </summary>
    /// <returns>Number of orders removed.</returns>
    public static int DeleteAllActiveOrders()
    {
        using var db = new AppDbContext();
        var active = db.Orders
            .Include(o => o.Items)
            .AsEnumerable()
            .Where(o => IsActiveOrderStatus(o.Status))
            .ToList();

        if (active.Count > 0)
        {
            foreach (var order in active)
            {
                db.OrderItems.RemoveRange(order.Items);
                db.Orders.Remove(order);
            }

            db.SaveChanges();
        }

        ReconcileTableStatusesWithOrders(db);
        db.SaveChanges();
        return active.Count;
    }

    /// <summary>Sets each table to Occupied iff it has a Waiting / In Kitchen / Ready / Served order; otherwise Available (skips Maintenance).</summary>
    public static void ReconcileTableStatusesWithOrders(AppDbContext db)
    {
        var occupiedTableIds = db.Orders.AsNoTracking()
            .Where(o =>
                o.Status == OrderWorkflow.PendingCashier
                || o.Status == "Waiting"
                || o.Status == "In Kitchen"
                || o.Status == "Ready"
                || o.Status == OrderWorkflow.Served)
            .Select(o => o.TableId)
            .Distinct()
            .ToHashSet();

        foreach (var table in db.Tables.ToList())
        {
            if (string.Equals(table.Status, "Maintenance", StringComparison.OrdinalIgnoreCase))
                continue;

            table.Status = occupiedTableIds.Contains(table.Id) ? "Occupied" : "Available";
        }
    }
}
