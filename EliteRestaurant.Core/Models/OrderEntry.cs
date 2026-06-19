using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace EliteRestaurant.Core.Models;

public class OrderEntry
{
    public int Id { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public string ConfirmationCode { get; set; } = string.Empty;
    public bool ShowConfirmationCode => !string.IsNullOrWhiteSpace(ConfirmationCode);
    public string TableNumber { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string Items { get; set; } = string.Empty;
    public string CustomerNotes { get; set; } = string.Empty;
    public string AllergyNotes { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public decimal Total { get; set; }

    /// <summary><see cref="Models.OrderOrigin.Online"/> or <see cref="Models.OrderOrigin.InStore"/>.</summary>
    public string OrderOrigin { get; set; } = global::EliteRestaurant.Core.Models.OrderOrigin.InStore;

    private string _statusColor = "#4CAF50";
    public string StatusColor
    {
        get => _statusColor;
        set => _statusColor = value;
    }

    /// <summary>Admin Orders: show manual advance for Waiting / In Kitchen only.</summary>
    public bool ShowAdvanceInOrders { get; set; }

    /// <summary>Cashier Orders: complete when Served (dine-in) or Ready/Served (online pickup/delivery).</summary>
    public bool ShowCompleteInOrders { get; set; }

    /// <summary>Cashier and full admin may open ticket preview.</summary>
    public bool ShowViewTicketInOrders { get; set; }

    /// <summary>Past completed orders not yet refunded may issue a refund (admin/cashier).</summary>
    public bool ShowRefundInOrders { get; set; }

    /// <summary>UTC timestamp when a completed order was refunded; null if never refunded.</summary>
    public DateTime? RefundedAtUtc { get; set; }

    public bool IsRefunded => RefundedAtUtc.HasValue;

    /// <summary>Kitchen queue: approve/release pending ticket to Waiting.</summary>
    public bool ShowReleaseToKitchen { get; set; }

    /// <summary>Kitchen queue: receive Waiting ticket into In Kitchen.</summary>
    public bool ShowReceiveInKitchen { get; set; }

    /// <summary>Kitchen queue: mark In Kitchen ticket Ready.</summary>
    public bool ShowMarkReadyForPickup { get; set; }

    /// <summary>KDS origin badge (DELIVERY / TO GO / PLATED).</summary>
    public string FulfillmentHeadline { get; set; } = string.Empty;

    /// <summary>e.g. "1 new item · 3 already prepared" when ticket returned after partial service.</summary>
    public string KitchenWorkSummary { get; set; } = string.Empty;

    public bool ShowKitchenWorkSummary => !string.IsNullOrWhiteSpace(KitchenWorkSummary);

    [NotMapped]
    [JsonIgnore]
    public string DisplayStatus { get; set; } = string.Empty;

    [NotMapped]
    [JsonIgnore]
    public string DisplayServerLine { get; set; } = string.Empty;

    [NotMapped]
    [JsonIgnore]
    public string DisplayConfirmationLine { get; set; } = string.Empty;

    [NotMapped]
    [JsonIgnore]
    public string DisplayTableLabel { get; set; } = string.Empty;

    [NotMapped]
    [JsonIgnore]
    public string DisplayTime { get; set; } = string.Empty;
}
