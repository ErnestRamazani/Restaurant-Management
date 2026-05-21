namespace EliteRestaurant.Core.Tenancy;

/// <summary>Rows partitioned by <see cref="RestaurantId"/> for multi-tenant hosting.</summary>
public interface IRestaurantScoped
{
    int RestaurantId { get; set; }
}
