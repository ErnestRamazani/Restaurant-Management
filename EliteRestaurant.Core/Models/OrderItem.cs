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

    /// <summary>Set when the kitchen or bar marks the line prepared on their station.</summary>
    public DateTime? KitchenPreparedAt { get; set; }

    /// <summary>Set when the server delivers this line (or station batch) to the guest.</summary>
    public DateTime? ServerServedAt { get; set; }

    /// <summary>Set when inventory was deducted for this line (release or add-on while in progress).</summary>
    public DateTime? InventoryDeductedAt { get; set; }
}
