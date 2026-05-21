using System;
using System.Text.Json;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Sync;
using EliteRestaurant.Core.Tenancy;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql;

namespace EliteRestaurant.Core.Data;

public class AppDbContext : DbContext
{
    private static readonly JsonSerializerOptions SyncJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ITenantContext _tenantContext;

    public AppDbContext()
    {
        _tenantContext = new NullTenantContext();
    }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        _tenantContext = new NullTenantContext();
    }

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext) : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Restaurant> Restaurants => Set<Restaurant>();
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
    public DbSet<PlacementUnit> PlacementUnits => Set<PlacementUnit>();
    public DbSet<ReservationEngagement> ReservationEngagements => Set<ReservationEngagement>();
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();
    public DbSet<SharedOrderDraft> SharedOrderDrafts => Set<SharedOrderDraft>();
    public DbSet<TabletSession> TabletSessions => Set<TabletSession>();
    public DbSet<SyncOutbox> SyncOutbox => Set<SyncOutbox>();
    public DbSet<PublicMenuSetting> PublicMenuSettings => Set<PublicMenuSetting>();
    public DbSet<PublicMenuAsset> PublicMenuAssets => Set<PublicMenuAsset>();

    public static Func<IReadOnlyList<CloudSyncOperation>, CancellationToken, Task<IReadOnlyList<CloudSyncResult>>>?
        CloudSyncDispatcher { get; set; }
    public static Action? CloudSyncQueued { get; set; }

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

        if (TryGetDatabaseUrlLastResort(out var databaseUrl))
        {
            optionsBuilder.UseNpgsql(
                databaseUrl,
                npgsql => npgsql.EnableRetryOnFailure(5));
            return;
        }

        Console.WriteLine(
            "[EliteRestaurant] Warning: no PostgreSQL connection string was found. " +
            "Continuing without configuring a database provider.");
    }

    public override int SaveChanges()
    {
        ApplyTenantOnInsert();
        var queued = QueueCloudSyncOperations();
        var result = base.SaveChanges(acceptAllChangesOnSuccess: true);
        NotifyCloudSyncQueued(queued);
        return result;
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTenantOnInsert();
        var queued = QueueCloudSyncOperations();
        var result = base.SaveChanges(acceptAllChangesOnSuccess);
        NotifyCloudSyncQueued(queued);
        return result;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyTenantOnInsert();
        var queued = QueueCloudSyncOperations();
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);
        NotifyCloudSyncQueued(queued);
        return result;
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyTenantOnInsert();
        var queued = QueueCloudSyncOperations();
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        NotifyCloudSyncQueued(queued);
        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Restaurant>().ToTable("Restaurants");
        modelBuilder.Entity<Restaurant>().HasIndex(r => r.Slug).IsUnique();
        modelBuilder.Entity<Restaurant>().HasIndex(r => r.UniqueId).IsUnique();
        modelBuilder.Entity<Restaurant>()
            .HasIndex(r => r.CustomDomain)
            .IsUnique()
            .HasFilter("\"CustomDomain\" IS NOT NULL AND \"CustomDomain\" <> ''");

        ApplyTenantQueryFilters(modelBuilder);

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
        modelBuilder.Entity<PlacementUnit>().ToTable("PlacementUnits");
        modelBuilder.Entity<ReservationEngagement>().ToTable("ReservationEngagements");
        modelBuilder.Entity<WaitlistEntry>().ToTable("WaitlistEntries");
        modelBuilder.Entity<SharedOrderDraft>().ToTable("SharedOrderDrafts");
        modelBuilder.Entity<TabletSession>().ToTable("TabletSessions");
        modelBuilder.Entity<SyncOutbox>().ToTable("SyncOutbox");
        modelBuilder.Entity<PublicMenuSetting>().ToTable("PublicMenuSettings");
        modelBuilder.Entity<PublicMenuAsset>().ToTable("PublicMenuAssets");
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

        modelBuilder.Entity<Employee>().HasIndex(e => new { e.RestaurantId, e.UniqueId }).IsUnique();
        modelBuilder.Entity<Employee>()
            .HasIndex(e => new { e.RestaurantId, e.SignInId })
            .IsUnique()
            .HasFilter("\"SignInId\" IS NOT NULL AND \"SignInId\" <> ''");
        modelBuilder.Entity<Product>().HasIndex(p => new { p.RestaurantId, p.UniqueId }).IsUnique();
        modelBuilder.Entity<Table>().HasIndex(t => new { t.RestaurantId, t.UniqueId }).IsUnique();
        modelBuilder.Entity<Table>().HasIndex(t => new { t.RestaurantId, t.TableNumber }).IsUnique();
        modelBuilder.Entity<OrderRecord>().HasIndex(o => new { o.RestaurantId, o.UniqueId }).IsUnique();
        modelBuilder.Entity<OrderRecord>()
            .Property(o => o.ConfirmationCode)
            .HasMaxLength(6);
        modelBuilder.Entity<OrderRecord>()
            .HasIndex(o => new { o.RestaurantId, o.ConfirmationCode })
            .IsUnique()
            .HasFilter("\"ConfirmationCode\" IS NOT NULL AND \"ConfirmationCode\" <> ''");
        modelBuilder.Entity<InventoryItem>().HasIndex(i => new { i.RestaurantId, i.UniqueId }).IsUnique();
        modelBuilder.Entity<CustomerProfile>().HasIndex(c => new { c.RestaurantId, c.UniqueId }).IsUnique();
        modelBuilder.Entity<CustomerProfile>().HasIndex(c => c.PrimaryPhone);
        modelBuilder.Entity<ReservationBooking>().HasIndex(r => new { r.RestaurantId, r.UniqueId }).IsUnique();
        modelBuilder.Entity<ReservationBooking>().HasIndex(r => r.ReservedFor);
        modelBuilder.Entity<ReservationBooking>().HasIndex(r => r.Status);
        modelBuilder.Entity<PlacementUnit>().HasIndex(p => p.TableId).IsUnique();
        modelBuilder.Entity<PlacementUnit>().HasIndex(p => p.MergeClusterKey);
        modelBuilder.Entity<ReservationEngagement>()
            .Property(e => e.ConfirmationCode)
            .HasMaxLength(6);
        modelBuilder.Entity<ReservationEngagement>()
            .HasIndex(e => new { e.RestaurantId, e.ConfirmationCode })
            .IsUnique()
            .HasFilter("\"ConfirmationCode\" IS NOT NULL AND \"ConfirmationCode\" <> ''");
        modelBuilder.Entity<ReservationEngagement>().HasIndex(e => e.PlannedStartUtc);
        modelBuilder.Entity<ReservationEngagement>().HasIndex(e => e.Status);
        modelBuilder.Entity<ReservationEngagement>().HasIndex(e => e.PlacementUnitId);
        modelBuilder.Entity<ReservationEngagement>().HasIndex(e => e.TableId);
        modelBuilder.Entity<WaitlistEntry>().HasIndex(w => new { w.RestaurantId, w.UniqueId }).IsUnique();
        modelBuilder.Entity<WaitlistEntry>().HasIndex(w => w.CreatedAt);
        modelBuilder.Entity<WaitlistEntry>().HasIndex(w => w.Status);
        modelBuilder.Entity<SharedOrderDraft>().HasIndex(d => new { d.RestaurantId, d.UniqueId }).IsUnique();
        modelBuilder.Entity<SharedOrderDraft>().HasIndex(d => new { d.EmployeeId, d.Portal, d.UpdatedAtUtc });
        modelBuilder.Entity<SyncOutbox>().HasIndex(o => o.IdempotencyKey).IsUnique();
        modelBuilder.Entity<SyncOutbox>().HasIndex(o => new { o.Status, o.QueuedAtUtc });
        modelBuilder.Entity<PublicMenuSetting>().HasIndex(s => new { s.RestaurantId, s.Key }).IsUnique();
        modelBuilder.Entity<PublicMenuAsset>().HasIndex(a => new { a.RestaurantId, a.Key }).IsUnique();
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

        modelBuilder.Entity<PlacementUnit>()
            .HasOne(p => p.Table)
            .WithMany()
            .HasForeignKey(p => p.TableId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReservationEngagement>()
            .HasOne(e => e.PlacementUnit)
            .WithMany()
            .HasForeignKey(e => e.PlacementUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ReservationEngagement>()
            .HasOne(e => e.Table)
            .WithMany()
            .HasForeignKey(e => e.TableId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ReservationEngagement>()
            .HasOne(e => e.ReservationBooking)
            .WithMany()
            .HasForeignKey(e => e.ReservationBookingId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Restaurant>()
            .HasMany<Employee>()
            .WithOne()
            .HasForeignKey(e => e.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict);

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

    private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(IRestaurantScoped).IsAssignableFrom(entityType.ClrType))
                continue;

            var method = typeof(AppDbContext)
                .GetMethod(nameof(ConfigureTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType);
            method.Invoke(this, [modelBuilder]);
        }
    }

    private void ConfigureTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, IRestaurantScoped
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(e => !_tenantContext.IsResolved || e.RestaurantId == _tenantContext.RestaurantId);
    }

    private void ApplyTenantOnInsert()
    {
        if (!_tenantContext.IsResolved)
            return;

        foreach (var entry in ChangeTracker.Entries<IRestaurantScoped>())
        {
            if (entry.State != EntityState.Added)
                continue;
            if (entry.Entity.RestaurantId <= 0)
                entry.Entity.RestaurantId = _tenantContext.RestaurantId;
        }
    }

    private int QueueCloudSyncOperations()
    {
        if (CloudSyncQueued is null && CloudSyncDispatcher is null)
            return 0;

        ChangeTracker.DetectChanges();
        var operations = ChangeTracker.Entries()
            .Where(IsSyncableEntry)
            .Select(CreateSyncOperation)
            .ToList();

        if (operations.Count == 0)
            return 0;

        QueueFailedOperations(operations, "Queued for cloud sync.");
        return operations.Count;
    }

    private static void NotifyCloudSyncQueued(int queuedCount)
    {
        if (queuedCount <= 0)
            return;

        try
        {
            CloudSyncQueued?.Invoke();
        }
        catch
        {
            // Saving local data must never fail because the background sync notifier failed.
        }
    }

    private static bool IsSyncableEntry(EntityEntry entry)
    {
        if (entry.Entity is SyncOutbox)
            return false;

        return entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted;
    }

    private static CloudSyncOperation CreateSyncOperation(EntityEntry entry)
    {
        var operation = entry.State switch
        {
            EntityState.Added => "Upsert",
            EntityState.Modified => "Upsert",
            EntityState.Deleted => "Delete",
            _ => "Unknown"
        };

        return new CloudSyncOperation(
            Guid.NewGuid().ToString("N"),
            entry.Entity.GetType().Name,
            operation,
            SerializeEntry(entry),
            DateTime.UtcNow);
    }

    private static string SerializeEntry(EntityEntry entry)
    {
        var values = entry.State == EntityState.Deleted
            ? entry.OriginalValues
            : entry.CurrentValues;

        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in values.Properties)
            payload[property.Name] = values[property];

        return JsonSerializer.Serialize(payload, SyncJsonOptions);
    }

    private void QueueFailedOperations(IEnumerable<CloudSyncOperation> operations, string error)
    {
        foreach (var operation in operations)
            QueueFailedOperation(operation, error);
    }

    private void QueueFailedOperation(CloudSyncOperation operation, string error)
    {
        SyncOutbox.Add(new SyncOutbox
        {
            IdempotencyKey = operation.IdempotencyKey,
            EntityName = operation.EntityName,
            Operation = operation.Operation,
            PayloadJson = operation.PayloadJson,
            Status = "Pending",
            Attempts = 0,
            LastError = error,
            QueuedAtUtc = operation.QueuedAtUtc
        });
    }

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
        connectionString = databaseUrl;
        return true;
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
