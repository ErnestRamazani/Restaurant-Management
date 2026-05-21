using System.Collections.Concurrent;

namespace EliteRestaurant.Api.Services;

public sealed class TableServerCallEntry
{
    public Guid Id { get; init; }
    public int RestaurantId { get; init; }
    public int TableId { get; init; }
    public int TableNumber { get; init; }
    public string TableName { get; init; } = string.Empty;
    public string ReasonCode { get; init; } = string.Empty;
    public string ReasonLabel { get; init; } = string.Empty;
    public int? AssignedServerId { get; init; }
    public string? AssignedServerName { get; init; }
    public DateTime CalledAtUtc { get; init; }
    public DateTime? AcceptedAtUtc { get; set; }
    public int? AcceptedByEmployeeId { get; set; }
    public bool IsPending => AcceptedAtUtc is null;
}

/// <summary>In-memory queue of guest table calls (single API instance).</summary>
public static class TableServerCallQueue
{
    private static readonly ConcurrentDictionary<Guid, TableServerCallEntry> Calls = new();
    private static readonly TimeSpan Retention = TimeSpan.FromHours(12);

    public static TableServerCallEntry Enqueue(
        int restaurantId,
        int tableId,
        int tableNumber,
        string tableName,
        string reasonCode,
        string reasonLabel,
        int? assignedServerId,
        string? assignedServerName)
    {
        PurgeOld();
        var entry = new TableServerCallEntry
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurantId,
            TableId = tableId,
            TableNumber = tableNumber,
            TableName = tableName.Trim(),
            ReasonCode = reasonCode,
            ReasonLabel = reasonLabel,
            AssignedServerId = assignedServerId,
            AssignedServerName = assignedServerName,
            CalledAtUtc = DateTime.UtcNow
        };
        Calls[entry.Id] = entry;
        return entry;
    }

    public static IReadOnlyList<TableServerCallEntry> ListForServer(int restaurantId, int? viewerServerEmployeeId, bool pendingOnly)
    {
        PurgeOld();
        return Calls.Values
            .Where(c => c.RestaurantId == restaurantId)
            .Where(c => MatchesServer(c, viewerServerEmployeeId))
            .Where(c => !pendingOnly || c.IsPending)
            .OrderByDescending(c => c.CalledAtUtc)
            .ToList();
    }

    public static int CountPendingForServer(int? viewerServerEmployeeId)
    {
        PurgeOld();
        return Calls.Values.Count(c => c.IsPending && MatchesServer(c, viewerServerEmployeeId));
    }

    public static bool TryAccept(Guid callId, int employeeId, out TableServerCallEntry? entry)
    {
        PurgeOld();
        if (!Calls.TryGetValue(callId, out var existing) || !existing.IsPending)
        {
            entry = null;
            return false;
        }

        existing.AcceptedAtUtc = DateTime.UtcNow;
        existing.AcceptedByEmployeeId = employeeId;
        entry = existing;
        return true;
    }

    public static bool TryGet(Guid callId, out TableServerCallEntry? entry)
    {
        PurgeOld();
        if (Calls.TryGetValue(callId, out var e))
        {
            entry = e;
            return true;
        }
        entry = null;
        return false;
    }

    private static bool MatchesServer(TableServerCallEntry c, int? viewerServerEmployeeId)
    {
        if (viewerServerEmployeeId is null or <= 0)
            return true;
        if (c.AssignedServerId is null or <= 0)
            return true;
        return c.AssignedServerId == viewerServerEmployeeId;
    }

    private static void PurgeOld()
    {
        var cutoff = DateTime.UtcNow - Retention;
        foreach (var kv in Calls)
        {
            if (kv.Value.CalledAtUtc < cutoff)
                Calls.TryRemove(kv.Key, out _);
        }
    }
}
