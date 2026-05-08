using System.Text.Json;
using EliteRestaurant.Core.Sync;
using EliteRestaurantPro.ApiClients;

namespace EliteRestaurantPro.Services;

/// <summary>Persists entity changes through the cloud sync HTTP API (desktop has no direct database access).</summary>
public static class DesktopCloudPersistence
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly AdminSyncApiClient Sync = new();

    public static async Task PushUpsertAsync(object entity, CancellationToken cancellationToken = default)
    {
        await PushAsync(entity.GetType().Name, "Upsert", entity, cancellationToken).ConfigureAwait(false);
    }

    public static async Task PushDeleteAsync(object entity, CancellationToken cancellationToken = default)
    {
        await PushAsync(entity.GetType().Name, "Delete", entity, cancellationToken).ConfigureAwait(false);
    }

    public static async Task PushBatchAsync(IReadOnlyList<CloudSyncOperation> operations, CancellationToken cancellationToken = default)
    {
        if (operations.Count == 0)
            return;

        var results = await Sync.PushAsync(operations, cancellationToken).ConfigureAwait(false);
        var fail = results.FirstOrDefault(r => !r.Success);
        if (fail is not null)
            throw new InvalidOperationException(fail.Message ?? "Cloud sync failed.");
    }

    /// <summary>Builds upsert operations for heterogeneous entity payloads (salary, attendance batches, etc.).</summary>
    public static List<CloudSyncOperation> ToUpsertOperations(IReadOnlyList<object> entities)
    {
        var ops = new List<CloudSyncOperation>(entities.Count);
        foreach (var entity in entities)
        {
            var payloadJson = JsonSerializer.Serialize(entity, entity.GetType(), JsonOptions);
            ops.Add(new CloudSyncOperation(
                Guid.NewGuid().ToString("N"),
                entity.GetType().Name,
                "Upsert",
                payloadJson,
                DateTime.UtcNow));
        }

        return ops;
    }

    /// <summary>Blocking upsert from synchronous UI handlers (runs HTTP off the UI thread).</summary>
    public static void PushUpsertBlocking(object entity) =>
        Task.Run(async () => await PushUpsertAsync(entity).ConfigureAwait(false)).GetAwaiter().GetResult();

    /// <summary>Blocking delete from synchronous UI handlers.</summary>
    public static void PushDeleteBlocking(object entity) =>
        Task.Run(async () => await PushDeleteAsync(entity).ConfigureAwait(false)).GetAwaiter().GetResult();

    /// <summary>Blocking batch from synchronous UI handlers.</summary>
    public static void PushBatchBlocking(IReadOnlyList<CloudSyncOperation> operations) =>
        Task.Run(async () => await PushBatchAsync(operations).ConfigureAwait(false)).GetAwaiter().GetResult();

    public static CloudSyncOperation UpsertOperation(object entity)
    {
        var payloadJson = JsonSerializer.Serialize(entity, entity.GetType(), JsonOptions);
        return new CloudSyncOperation(
            Guid.NewGuid().ToString("N"),
            entity.GetType().Name,
            "Upsert",
            payloadJson,
            DateTime.UtcNow);
    }

    public static CloudSyncOperation DeleteOperation(object entity)
    {
        var payloadJson = JsonSerializer.Serialize(entity, entity.GetType(), JsonOptions);
        return new CloudSyncOperation(
            Guid.NewGuid().ToString("N"),
            entity.GetType().Name,
            "Delete",
            payloadJson,
            DateTime.UtcNow);
    }

    private static async Task PushAsync(string entityName, string operation, object entity, CancellationToken cancellationToken)
    {
        var payloadJson = JsonSerializer.Serialize(entity, entity.GetType(), JsonOptions);
        var op = new CloudSyncOperation(Guid.NewGuid().ToString("N"), entityName, operation, payloadJson, DateTime.UtcNow);
        await PushBatchAsync([op], cancellationToken).ConfigureAwait(false);
    }
}
