using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace EliteRestaurant.Api.Services;

public sealed class PublicMenuSettingsCache(IMemoryCache cache, AppDbContext db)
{
    private const string DefaultKey = "public-menu-settings:default";

    public async Task<PublicMenuSetting?> GetDefaultAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(DefaultKey, out PublicMenuSetting? cached))
            return cached;

        var row = await db.PublicMenuSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == "default", cancellationToken);

        cache.Set(DefaultKey, row, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(5)
        });
        return row;
    }

    public PublicMenuSetting? GetDefault()
    {
        if (cache.TryGetValue(DefaultKey, out PublicMenuSetting? cached))
            return cached;

        var row = db.PublicMenuSettings.AsNoTracking().FirstOrDefault(s => s.Key == "default");
        cache.Set(DefaultKey, row, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(5)
        });
        return row;
    }

    public void Invalidate() => cache.Remove(DefaultKey);
}
