using System;
using System.Text.Json;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql;

namespace EliteRestaurant.Core.Data;

public class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

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
    public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();
    public DbSet<ReservationBooking> Reservations => Set<ReservationBooking>();
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();
    public DbSet<SharedOrderDraft> SharedOrderDrafts => Set<SharedOrderDraft>();
    public DbSet<TabletSession> TabletSessions => Set<TabletSession>();

    [Obsolete("Use DatabaseInitializer.Initialize() (EF Core migrations + optional sample seed).")]
    public static void Initialize() => DatabaseInitializer.Initialize();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured)
            return;

        if (TryGetPostgreSqlConnectionString(out var postgresConnectionString))
        {
            optionsBuilder.UseNpgsql(
                postgresConnectionString,
                npgsql => npgsql.EnableRetryOnFailure(5));
            return;
        }

        if (TryGetDatabaseUrlLastResort(out var databaseUrlConnectionString))
        {
            optionsBuilder.UseNpgsql(
                databaseUrlConnectionString,
                npgsql => npgsql.EnableRetryOnFailure(5));
            return;
        }

        throw new InvalidOperationException(
            "PostgreSQL is required but no connection string was found. " +
            "Preferred: set ELITE_DB_PROVIDER=PostgreSql and ELITE_POSTGRES_CONNECTION. " +
            "Alternatively configure Database (host, port, database, user, DPAPI password) in app settings.");
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
        modelBuilder.Entity<CustomerProfile>().ToTable("CustomerProfiles");
        modelBuilder.Entity<ReservationBooking>().ToTable("Reservations");
        modelBuilder.Entity<WaitlistEntry>().ToTable("WaitlistEntries");
        modelBuilder.Entity<SharedOrderDraft>().ToTable("SharedOrderDrafts");
        modelBuilder.Entity<TabletSession>().ToTable("TabletSessions");
        modelBuilder.Entity<TabletSession>().HasKey(t => t.Token);

        modelBuilder.Entity<TabletSession>()
            .Property(t => t.Token)
            .HasMaxLength(32);
        modelBuilder.Entity<TabletSession>()
            .HasIndex(t => t.ExpiresAtUtc);
        modelBuilder.Entity<TabletSession>()
            .HasIndex(t => t.EmployeeId);
        modelBuilder.Entity<TabletSession>()
            .HasOne<Employee>()
            .WithMany()
            .HasForeignKey(t => t.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Employee>().HasIndex(e => e.UniqueId).IsUnique();
        modelBuilder.Entity<Employee>()
            .HasIndex(e => e.SignInId)
            .IsUnique()
            .HasFilter("\"SignInId\" IS NOT NULL AND \"SignInId\" <> ''");
        modelBuilder.Entity<Product>().HasIndex(p => p.UniqueId).IsUnique();
        modelBuilder.Entity<Table>().HasIndex(t => t.UniqueId).IsUnique();
        modelBuilder.Entity<Table>().HasIndex(t => t.TableNumber).IsUnique();
        modelBuilder.Entity<OrderRecord>().HasIndex(o => o.UniqueId).IsUnique();
        modelBuilder.Entity<InventoryItem>().HasIndex(i => i.UniqueId).IsUnique();
        modelBuilder.Entity<CustomerProfile>().HasIndex(c => c.UniqueId).IsUnique();
        modelBuilder.Entity<CustomerProfile>().HasIndex(c => c.PrimaryPhone);
        modelBuilder.Entity<ReservationBooking>().HasIndex(r => r.UniqueId).IsUnique();
        modelBuilder.Entity<ReservationBooking>().HasIndex(r => r.ReservedFor);
        modelBuilder.Entity<ReservationBooking>().HasIndex(r => r.Status);
        modelBuilder.Entity<WaitlistEntry>().HasIndex(w => w.UniqueId).IsUnique();
        modelBuilder.Entity<WaitlistEntry>().HasIndex(w => w.CreatedAt);
        modelBuilder.Entity<WaitlistEntry>().HasIndex(w => w.Status);
        modelBuilder.Entity<SharedOrderDraft>().HasIndex(d => d.UniqueId).IsUnique();
        modelBuilder.Entity<SharedOrderDraft>().HasIndex(d => new { d.EmployeeId, d.Portal, d.UpdatedAtUtc });
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

        modelBuilder.Entity<ReservationBooking>()
            .HasOne(r => r.CustomerProfile)
            .WithMany()
            .HasForeignKey(r => r.CustomerProfileId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ReservationBooking>()
            .HasOne(r => r.Table)
            .WithMany()
            .HasForeignKey(r => r.TableId)
            .OnDelete(DeleteBehavior.SetNull);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(
                        new ValueConverter<DateTime, DateTime>(
                            v => ToUtcDateTime(v),
                            v => DateTime.SpecifyKind(v, DateTimeKind.Utc)));
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(
                        new ValueConverter<DateTime?, DateTime?>(
                            v => v.HasValue ? ToUtcDateTime(v.Value) : v,
                            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v));
                }
            }
        }
    }

    private static DateTime ToUtcDateTime(DateTime v) =>
        v.Kind switch
        {
            DateTimeKind.Utc => v,
            DateTimeKind.Local => v.ToUniversalTime(),
            _ => DateTime.SpecifyKind(v, DateTimeKind.Utc)
        };

    public static bool TryGetPostgreSqlConnectionString(out string connectionString, string? defaultConnection = null)
    {
        connectionString = string.Empty;

        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (DatabaseSettingsResolver.TryNormalizePostgreSqlConnectionString(
                databaseUrl,
                out connectionString,
                ensureCloudSsl: true))
            return true;

        var eliteConnection = Environment.GetEnvironmentVariable("ELITE_POSTGRES_CONNECTION");
        if (DatabaseSettingsResolver.TryNormalizePostgreSqlConnectionString(
                eliteConnection,
                out connectionString,
                ensureCloudSsl: true))
            return true;

        defaultConnection ??= Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        defaultConnection ??= TryReadDefaultConnectionFromAppSettings(out var appSettingsConnection)
            ? appSettingsConnection
            : null;

        if (DatabaseSettingsResolver.TryNormalizePostgreSqlConnectionString(defaultConnection, out connectionString))
            return true;

        DatabaseSettings settings;
        try
        {
            settings = SettingsManager.Load().Database ?? new DatabaseSettings();
        }
        catch
        {
            settings = new DatabaseSettings();
        }

        return DatabaseSettingsResolver.TryBuildFromSettings(settings, out connectionString);
    }

    public static bool TryGetDatabaseUrlLastResort(out string connectionString)
    {
        connectionString = string.Empty;
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (string.IsNullOrWhiteSpace(databaseUrl))
            return false;

        Console.WriteLine($"[EliteRestaurant] DATABASE_URL found as last resort. Length={databaseUrl.Length}.");
        return DatabaseSettingsResolver.TryNormalizePostgreSqlConnectionString(
            databaseUrl,
            out connectionString,
            ensureCloudSsl: true);
    }

    private static bool TryReadDefaultConnectionFromAppSettings(out string connectionString)
    {
        connectionString = string.Empty;
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                              ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        var fileNames = string.IsNullOrWhiteSpace(environmentName)
            ? new[] { "appsettings.json" }
            : new[] { $"appsettings.{environmentName}.json", "appsettings.json" };

        foreach (var basePath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }.Distinct())
        {
            foreach (var fileName in fileNames)
            {
                var path = Path.Combine(basePath, fileName);
                if (!File.Exists(path))
                    continue;

                try
                {
                    using var stream = File.OpenRead(path);
                    using var document = JsonDocument.Parse(stream);
                    if (document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings)
                        && connectionStrings.TryGetProperty("DefaultConnection", out var defaultConnection)
                        && defaultConnection.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(defaultConnection.GetString()))
                    {
                        connectionString = defaultConnection.GetString()!;
                        return true;
                    }
                }
                catch
                {
                    // Invalid local config should not block environment-variable based deployments.
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Human-readable database target for operational tools (host/database only; no password).
    /// </summary>
    public static string GetDatabaseTargetDescription()
    {
        if (!TryGetPostgreSqlConnectionString(out var cs))
        {
            return "PostgreSQL (not configured — set ELITE_DB_PROVIDER=PostgreSql and ELITE_POSTGRES_CONNECTION, " +
                   "or Database host/database/user in app settings).";
        }

        try
        {
            var b = new NpgsqlConnectionStringBuilder(cs);
            var host = string.IsNullOrWhiteSpace(b.Host) ? "?" : b.Host;
            var db = string.IsNullOrWhiteSpace(b.Database) ? "?" : b.Database;
            return $"PostgreSQL {host}:{b.Port}/{db}";
        }
        catch
        {
            return "PostgreSQL (configured)";
        }
    }
}
