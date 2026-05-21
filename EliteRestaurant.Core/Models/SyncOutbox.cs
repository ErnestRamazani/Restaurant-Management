using EliteRestaurant.Core.Tenancy;

namespace EliteRestaurant.Core.Models;

public class SyncOutbox : IRestaurantScoped
{
    public int Id { get; set; }
    public int RestaurantId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string Status { get; set; } = "Pending";
    public int Attempts { get; set; }
    public string LastError { get; set; } = string.Empty;
    public DateTime QueuedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? SyncedAtUtc { get; set; }
}
