using EliteRestaurant.Core.Tenancy;

namespace EliteRestaurant.Core.Models;

public sealed class SharedOrderDraft : IRestaurantScoped
{
    public int Id { get; set; }
    public int RestaurantId { get; set; }
    public string UniqueId { get; set; } = string.Empty;
    public int EmployeeId { get; set; }
    /// <summary>Which floor table this draft belongs to (0 = unknown / pre-migration). Customer menu drafts always set this.</summary>
    public int TableId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Portal { get; set; } = "Server";
    public string DraftLabel { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
