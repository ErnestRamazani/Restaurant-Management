namespace EliteRestaurant.Core.Models;

/// <summary>Persisted API tablet/staff bearer session (survives process restarts).</summary>
public sealed class TabletSession
{
    /// <summary>32-character hex token (GUID without dashes).</summary>
    public string Token { get; set; } = string.Empty;

    public int EmployeeId { get; set; }

    /// <summary>Portal used at login (e.g. Server, Cashier, KitchenBar).</summary>
    public string Portal { get; set; } = "Server";

    public string EmployeeUniqueId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string SignInId { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
