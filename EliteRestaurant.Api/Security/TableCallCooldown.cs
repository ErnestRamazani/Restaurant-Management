using System.Collections.Concurrent;

namespace EliteRestaurant.Api.Security;

/// <summary>Per-table cooldown for guest "call server" to prevent spam (in-memory, single instance).</summary>
public static class TableCallCooldown
{
    private static readonly ConcurrentDictionary<int, DateTime> LastCallUtc = new();
    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(60);

    public static bool TryAcquire(int tableId, out int retryAfterSeconds)
    {
        var now = DateTime.UtcNow;
        if (LastCallUtc.TryGetValue(tableId, out var last)
            && now - last < Cooldown)
        {
            retryAfterSeconds = (int)Math.Ceiling((Cooldown - (now - last)).TotalSeconds);
            if (retryAfterSeconds < 1) retryAfterSeconds = 1;
            return false;
        }

        LastCallUtc[tableId] = now;
        retryAfterSeconds = 0;
        return true;
    }
}
