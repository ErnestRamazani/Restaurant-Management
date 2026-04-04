namespace EliteRestaurantPro.Models;

public class Table
{
    public int Id { get; set; }
    public string UniqueId { get; set; } = string.Empty;
    public int TableNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string Status { get; set; } = "Available";
    public int? AssignedServerId { get; set; }
    public Employee? AssignedServer { get; set; }
}
