using EliteRestaurant.Core.Tenancy;

namespace EliteRestaurant.Core.Models;

public sealed class ClientDebtLedgerEntry : IRestaurantScoped
{
    public int Id { get; set; }
    public int RestaurantId { get; set; }
    public int RestaurantClientId { get; set; }
    public RestaurantClient? RestaurantClient { get; set; }
    public int? OrderId { get; set; }
    public OrderRecord? Order { get; set; }
    /// <summary><see cref="ClientDebtLedgerEntryType"/>.</summary>
    public string EntryType { get; set; } = string.Empty;
    public decimal AmountUsd { get; set; }
    public decimal BalanceAfterUsd { get; set; }
    public string Note { get; set; } = string.Empty;
    public int? CreatedByEmployeeId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
