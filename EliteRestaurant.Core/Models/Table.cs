using EliteRestaurant.Core.Tenancy;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

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

    [NotMapped]
    [JsonIgnore]
    public string DisplayStatus { get; set; } = string.Empty;

    [NotMapped]
    [JsonIgnore]
    public string DisplayCapacityText { get; set; } = string.Empty;

    [NotMapped]
    [JsonIgnore]
    public string DisplayTableIdLine { get; set; } = string.Empty;

    [NotMapped]
    [JsonIgnore]
    public string DisplayServerLine { get; set; } = string.Empty;
}
