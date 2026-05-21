namespace EliteRestaurant.Core.Models;

public class OrderItem
{
    public int Id { get; set; }
    public int OrderRecordId { get; set; }
    public OrderRecord? OrderRecord { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; } = 1;
    public int? PreparedByEmployeeId { get; set; }
    public string PreparedByRole { get; set; } = string.Empty;
    public string PreparedByName { get; set; } = string.Empty;

    /// <summary>Set when the kitchen marks the ticket ready — line was prepared in a prior cycle.</summary>
    public DateTime? KitchenPreparedAt { get; set; }
}
