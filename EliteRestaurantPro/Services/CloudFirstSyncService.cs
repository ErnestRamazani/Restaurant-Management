using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Sync;
using EliteRestaurantPro.ApiClients;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurantPro.Services;

public static class CloudFirstSyncService
{
    private static readonly SemaphoreSlim SyncLock = new(1, 1);
    private static readonly AdminSyncApiClient SyncClient = new();
    private static readonly AdminSettingsApiClient SettingsClient = new();
    private static Timer? _timer;

    public static event Action? StatusChanged;

    public static int PendingCount { get; private set; }
    public static string LastSyncError { get; private set; } = string.Empty;

    public static void Start()
    {
        AppDbContext.CloudSyncDispatcher = DispatchAsync;
        _timer ??= new Timer(
            async _ => await RetryPendingAsync(),
            null,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(30));
        _ = RefreshStatusAsync();
        _ = BootstrapLocalBackupToCloudAsync();
    }

    public static void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        AppDbContext.CloudSyncDispatcher = null;
    }

    public static async Task<IReadOnlyList<CloudSyncResult>> DispatchAsync(
        IReadOnlyList<CloudSyncOperation> operations,
        CancellationToken cancellationToken = default)
    {
        if (operations.Count == 0)
            return [];

        var results = await SyncClient.PushAsync(operations, cancellationToken);
        var failed = results.FirstOrDefault(r => !r.Success);
        LastSyncError = failed?.Message ?? string.Empty;
        await RefreshStatusAsync(cancellationToken);
        return results;
    }

    public static async Task RetryPendingAsync(CancellationToken cancellationToken = default)
    {
        if (!await SyncLock.WaitAsync(0, cancellationToken))
            return;

        try
        {
            using var db = new AppDbContext();
            var pending = await db.SyncOutbox
                .Where(o => o.Status == "Pending")
                .OrderBy(o => o.QueuedAtUtc)
                .Take(50)
                .ToListAsync(cancellationToken);

            if (pending.Count == 0)
            {
                await RefreshStatusAsync(cancellationToken);
                return;
            }

            foreach (var row in pending)
            {
                row.Attempts++;
                row.LastAttemptAtUtc = DateTime.UtcNow;
            }

            var operations = pending
                .Select(row => new CloudSyncOperation(
                    row.IdempotencyKey,
                    row.EntityName,
                    row.Operation,
                    row.PayloadJson,
                    row.QueuedAtUtc))
                .ToList();

            IReadOnlyList<CloudSyncResult> results;
            try
            {
                results = await SyncClient.PushAsync(operations, cancellationToken);
            }
            catch (Exception ex)
            {
                LastSyncError = ex.GetBaseException().Message;
                foreach (var row in pending)
                    row.LastError = LastSyncError;
                await db.SaveChangesAsync(cancellationToken);
                await RefreshStatusAsync(cancellationToken);
                return;
            }

            var resultMap = results.ToDictionary(r => r.IdempotencyKey, StringComparer.OrdinalIgnoreCase);
            foreach (var row in pending)
            {
                if (!resultMap.TryGetValue(row.IdempotencyKey, out var result))
                {
                    row.LastError = "Cloud did not return a result for this operation.";
                    continue;
                }

                if (result.Success)
                {
                    row.Status = "Synced";
                    row.SyncedAtUtc = DateTime.UtcNow;
                    row.LastError = string.Empty;
                }
                else
                {
                    row.Status = "Conflict";
                    row.LastError = result.Message ?? "Cloud rejected the queued operation.";
                    LastSyncError = row.LastError;
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            await RefreshStatusAsync(cancellationToken);
        }
        finally
        {
            SyncLock.Release();
        }
    }

    public static async Task BootstrapLocalBackupToCloudAsync(CancellationToken cancellationToken = default)
    {
        if (!await SyncLock.WaitAsync(0, cancellationToken))
            return;

        try
        {
            await SettingsClient.PushSettingsAsync(EliteRestaurant.Core.Utils.SettingsManager.Load(), cancellationToken);

            using var db = new AppDbContext();
            var operations = new List<CloudSyncOperation>();

            // Parent/reference tables first, then children/detail tables, so FK relationships can be restored.
            AddBootstrapOperations(operations, "Employee", db.Employees.AsNoTracking().OrderBy(e => e.Id).ToList(), e => e.UniqueId);
            AddBootstrapOperations(operations, "CustomerProfile", db.CustomerProfiles.AsNoTracking().OrderBy(c => c.Id).ToList(), c => c.UniqueId);
            AddBootstrapOperations(operations, "InventoryItem", db.InventoryItems.AsNoTracking().OrderBy(i => i.Id).ToList(), i => i.UniqueId);
            AddBootstrapOperations(operations, "Product", db.Products.AsNoTracking().OrderBy(p => p.Id).ToList(), p => p.UniqueId);
            AddBootstrapOperations(operations, "ProductIngredient", db.ProductIngredients.AsNoTracking().OrderBy(pi => pi.Id).ToList(), pi => pi.Id.ToString());
            AddBootstrapOperations(operations, "Table", db.Tables.AsNoTracking().OrderBy(t => t.Id).ToList(), t => t.UniqueId);
            AddBootstrapOperations(operations, "ReservationBooking", db.Reservations.AsNoTracking().OrderBy(r => r.Id).ToList(), r => r.UniqueId);
            AddBootstrapOperations(operations, "WaitlistEntry", db.WaitlistEntries.AsNoTracking().OrderBy(w => w.Id).ToList(), w => w.UniqueId);
            AddBootstrapOperations(operations, "OrderRecord", db.Orders.AsNoTracking().OrderBy(o => o.Id).ToList(), o => o.UniqueId);
            AddBootstrapOperations(operations, "OrderItem", db.OrderItems.AsNoTracking().OrderBy(oi => oi.Id).ToList(), oi => oi.Id.ToString());
            AddBootstrapOperations(operations, "MoneyTransaction", db.Transactions.AsNoTracking().OrderBy(t => t.Id).ToList(), t => t.Id.ToString());
            AddBootstrapOperations(operations, "EmployeeAttendance", db.EmployeeAttendances.AsNoTracking().OrderBy(a => a.Id).ToList(), a => $"{a.EmployeeId}-{a.WorkDate:yyyyMMdd}");
            AddBootstrapOperations(operations, "AttendanceDayValidation", db.AttendanceDayValidations.AsNoTracking().OrderBy(v => v.Id).ToList(), v => v.WorkDate.ToString("yyyyMMdd"));
            AddBootstrapOperations(operations, "SalaryAdvance", db.SalaryAdvances.AsNoTracking().OrderBy(a => a.Id).ToList(), a => a.Id.ToString());
            AddBootstrapOperations(operations, "PayrollPaymentRecord", db.PayrollPaymentRecords.AsNoTracking().OrderBy(p => p.Id).ToList(), p => $"{p.EmployeeId}-{p.Year}-{p.Month}");
            AddBootstrapOperations(operations, "SharedOrderDraft", db.SharedOrderDrafts.AsNoTracking().OrderBy(d => d.Id).ToList(), d => d.UniqueId);
            AddBootstrapOperations(operations, "TabletSession", db.TabletSessions.AsNoTracking().OrderBy(s => s.CreatedAtUtc).ToList(), s => s.Token);

            foreach (var batch in operations.Chunk(50))
                await SyncClient.PushAsync(batch, cancellationToken);

            LastSyncError = string.Empty;
        }
        catch (Exception ex)
        {
            LastSyncError = ex.GetBaseException().Message;
        }
        finally
        {
            SyncLock.Release();
            await RefreshStatusAsync(cancellationToken);
        }
    }

    private static void AddBootstrapOperations<T>(
        List<CloudSyncOperation> operations,
        string entityName,
        IEnumerable<T> rows,
        Func<T, string?> identity)
    {
        foreach (var row in rows)
        {
            var keyPart = identity(row);
            if (string.IsNullOrWhiteSpace(keyPart))
                continue;

            operations.Add(new CloudSyncOperation(
                $"bootstrap-{entityName}-{keyPart}".ToLowerInvariant(),
                entityName,
                "Upsert",
                System.Text.Json.JsonSerializer.Serialize(row, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),
                DateTime.UtcNow));
        }
    }

    public static async Task RefreshStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var db = new AppDbContext();
            PendingCount = await db.SyncOutbox.CountAsync(o => o.Status == "Pending", cancellationToken);
            var lastProblem = await db.SyncOutbox
                .AsNoTracking()
                .Where(o => o.Status == "Conflict" || !string.IsNullOrWhiteSpace(o.LastError))
                .OrderByDescending(o => o.LastAttemptAtUtc ?? o.QueuedAtUtc)
                .Select(o => o.LastError)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(lastProblem))
                LastSyncError = lastProblem;
        }
        catch (Exception ex)
        {
            LastSyncError = ex.GetBaseException().Message;
        }
        finally
        {
            StatusChanged?.Invoke();
        }
    }
}
