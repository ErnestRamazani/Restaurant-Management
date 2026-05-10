using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Reservations;

public sealed class PlacementUnitClusterResolver(AppDbContext db)
{
    /// <summary>
    /// All placement unit ids that share resources with <paramref name="placementUnitId"/> (includes self).
    /// </summary>
    public async Task<HashSet<int>> ResolveClusterIdsAsync(int placementUnitId, CancellationToken cancellationToken = default)
    {
        var key = await db.PlacementUnits.AsNoTracking()
            .Where(p => p.Id == placementUnitId)
            .Select(p => p.MergeClusterKey)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(key))
            return new HashSet<int> { placementUnitId };

        var ids = await db.PlacementUnits.AsNoTracking()
            .Where(p => p.MergeClusterKey == key)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        return ids.Count == 0 ? new HashSet<int> { placementUnitId } : ids.ToHashSet();
    }

    public async Task<IReadOnlyDictionary<int, HashSet<int>>> LoadAllClustersAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.PlacementUnits.AsNoTracking()
            .Select(p => new { p.Id, p.MergeClusterKey })
            .ToListAsync(cancellationToken);

        var map = new Dictionary<int, HashSet<int>>();
        var byKey = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.MergeClusterKey))
            {
                map[row.Id] = new HashSet<int> { row.Id };
                continue;
            }

            if (!byKey.TryGetValue(row.MergeClusterKey, out var list))
            {
                list = new List<int>();
                byKey[row.MergeClusterKey] = list;
            }

            list.Add(row.Id);
        }

        foreach (var list in byKey.Values)
        {
            var set = list.ToHashSet();
            foreach (var id in set)
                map[id] = set;
        }

        foreach (var row in rows)
        {
            if (!map.ContainsKey(row.Id))
                map[row.Id] = new HashSet<int> { row.Id };
        }

        return map;
    }

    public bool ClustersIntersect(HashSet<int> a, HashSet<int> b)
    {
        if (a.Count > b.Count)
            (a, b) = (b, a);

        foreach (var id in a)
        {
            if (b.Contains(id))
                return true;
        }

        return false;
    }
}
