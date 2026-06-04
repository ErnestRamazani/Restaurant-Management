using EliteRestaurant.Core.Tenancy;

namespace EliteRestaurant.Core.Models;

/// <summary>Tracked restaurant client (dine-in accounts, debt, revenue). Separate from reservation <see cref="CustomerProfile"/>.</summary>
public sealed class RestaurantClient : IRestaurantScoped
{
    public int Id { get; set; }
    public int RestaurantId { get; set; }
    public string UniqueId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PrimaryPhone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string InternalNotes { get; set; } = string.Empty;
    public decimal DebtBalanceUsd { get; set; }
    public bool IsStaffClient { get; set; }
    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<OrderRecord> Orders { get; set; } = new List<OrderRecord>();
    public ICollection<ClientDebtLedgerEntry> LedgerEntries { get; set; } = new List<ClientDebtLedgerEntry>();
}
