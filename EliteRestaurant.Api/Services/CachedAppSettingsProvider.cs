using EliteRestaurant.Core.Utils;
using Microsoft.Extensions.Caching.Memory;

namespace EliteRestaurant.Api.Services;

public sealed class CachedAppSettingsProvider
{
    private const string CacheKey = "app-settings";
    private readonly IMemoryCache _cache;

    public CachedAppSettingsProvider(IMemoryCache cache)
    {
        _cache = cache;
        SettingsManager.SettingsChanged += Invalidate;
    }

    public AppSettings Load() =>
        _cache.GetOrCreate(CacheKey, entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromSeconds(60);
            return SettingsManager.Load();
        }) ?? SettingsManager.Load();

    public void Invalidate() => _cache.Remove(CacheKey);
}
