using EliteRestaurant.Core.Models;

namespace EliteRestaurant.Core.Tenancy;

public sealed class TenantContext : ITenantContext
{
    public bool IsResolved { get; private set; }
    public int RestaurantId { get; private set; }
    public string? Host { get; private set; }
    public Restaurant? Restaurant { get; private set; }

    public void SetRestaurant(Restaurant restaurant, string host)
    {
        ArgumentNullException.ThrowIfNull(restaurant);
        if (restaurant.Id <= 0)
            throw new ArgumentException("Restaurant id is required.", nameof(restaurant));

        Restaurant = restaurant;
        RestaurantId = restaurant.Id;
        Host = host;
        IsResolved = true;
    }

    public void Clear()
    {
        IsResolved = false;
        RestaurantId = 0;
        Host = null;
        Restaurant = null;
    }
}

/// <summary>Used by migrations and tools when no HTTP tenant is present.</summary>
public sealed class NullTenantContext : ITenantContext
{
    public bool IsResolved => false;
    public int RestaurantId => 0;
    public string? Host => null;
    public Restaurant? Restaurant => null;
    public void SetRestaurant(Restaurant restaurant, string host) { }
    public void Clear() { }
}
