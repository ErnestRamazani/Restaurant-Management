namespace EliteRestaurantPro.Models;

public class OrderEntry
{
    public int Id { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public string TableNumber { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string Items { get; set; } = string.Empty;
    public string CustomerNotes { get; set; } = string.Empty;
    public string AllergyNotes { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public decimal Total { get; set; }

    private string _statusColor = "#4CAF50";
    public string StatusColor
    {
        get => _statusColor;
        set => _statusColor = value;
    }

    /// <summary>Admin Orders: show manual advance for Waiting / In Kitchen only.</summary>
    public bool ShowAdvanceInOrders { get; set; }

    /// <summary>Admin Orders: complete payment only when status is Served.</summary>
    public bool ShowCompleteInOrders { get; set; }

    /// <summary>Cashier and full admin may open ticket preview.</summary>
    public bool ShowViewTicketInOrders { get; set; }
}
