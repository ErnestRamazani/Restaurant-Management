using EliteRestaurant.Core.Tenancy;

namespace EliteRestaurant.Core.Models;

public class Table : IRestaurantScoped
{
    public int Id { get; set; }
    public int RestaurantId { get; set; }
    public string UniqueId { get; set; } = string.Empty;
    public int TableNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string Status { get; set; } = "Available";
    public int? AssignedServerId { get; set; }
    public Employee? AssignedServer { get; set; }
}
