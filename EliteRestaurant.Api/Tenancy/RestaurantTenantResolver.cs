using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Api.Tenancy;

public sealed class RestaurantTenantResolver(AppDbContext db)
{
    public const string TenantHeader = "X-Restaurant-Id";
    public const string SlugHeader = "X-Restaurant-Slug";

    public async Task<Restaurant?> ResolveAsync(
        string? host,
        IHeaderDictionary headers,
        bool allowDevelopmentFallback,
        CancellationToken cancellationToken = default)
    {
        var normalizedHost = RestaurantHostNormalizer.NormalizeHost(host);
        if (!string.IsNullOrEmpty(normalizedHost))
        {
            var byDomain = await db.Restaurants.IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => r.IsActive && r.CustomDomain != null)
                .ToListAsync(cancellationToken);
            var match = byDomain.FirstOrDefault(r =>
                RestaurantHostNormalizer.NormalizeHost(r.CustomDomain) == normalizedHost);
            if (match is not null)
                return match;
        }

        if (headers.TryGetValue(SlugHeader, out var slugValues))
        {
            var slug = slugValues.ToString().Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(slug))
            {
                var bySlug = await FindBySlugAsync(slug, cancellationToken);
                if (bySlug is not null)
                    return bySlug;
            }
        }

        if (headers.TryGetValue(TenantHeader, out var idValues)
            && int.TryParse(idValues.ToString(), out var restaurantId)
            && restaurantId > 0)
        {
            return await db.Restaurants.IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == restaurantId && r.IsActive, cancellationToken);
        }

        if (allowDevelopmentFallback)
        {
            return await db.Restaurants.IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => r.IsActive)
                .OrderBy(r => r.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return null;
    }

    private Task<Restaurant?> FindBySlugAsync(string slug, CancellationToken cancellationToken) =>
        db.Restaurants.IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.IsActive && r.Slug == slug, cancellationToken);
}
