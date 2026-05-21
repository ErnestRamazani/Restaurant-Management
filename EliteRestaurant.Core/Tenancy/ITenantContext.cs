using EliteRestaurant.Core.Models;

namespace EliteRestaurant.Core.Tenancy;

public interface ITenantContext
{
    bool IsResolved { get; }
    int RestaurantId { get; }
    string? Host { get; }
    Restaurant? Restaurant { get; }
    void SetRestaurant(Restaurant restaurant, string host);
    void Clear();
}
