using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Data;

/// <summary>
/// Guest-menu style availability: for one sold unit, every recipe line must satisfy
/// <c>InventoryItem.StockQuantity &gt;= ProductIngredient.Quantity</c> (products with no recipe are available).
/// </summary>
public static class OrderInventoryAvailability
{
    public static async Task<Dictionary<int, bool>> GetProductAvailabilityMapAsync(
        AppDbContext db,
        IReadOnlyList<int> productIds,
        CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0)
            return new Dictionary<int, bool>();

        var distinctIds = productIds.Distinct().ToList();

        var lines = await db.ProductIngredients.AsNoTracking()
            .Where(pi => distinctIds.Contains(pi.ProductId))
            .Select(pi => new { pi.ProductId, pi.Quantity, Stock = pi.InventoryItem!.StockQuantity })
            .ToListAsync(cancellationToken);

        var map = distinctIds.ToDictionary(id => id, _ => true);

        foreach (var g in lines.GroupBy(x => x.ProductId))
            map[g.Key] = g.All(x => x.Stock >= x.Quantity);

        foreach (var id in distinctIds)
        {
            if (!lines.Any(l => l.ProductId == id))
                map[id] = true;
        }

        return map;
    }
}
