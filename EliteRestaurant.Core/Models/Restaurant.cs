namespace EliteRestaurant.Core.Models;

/// <summary>Logical site / tenant (one restaurant brand on shared infrastructure).</summary>
public sealed class Restaurant
{
    public int Id { get; set; }
    public string UniqueId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>URL slug (subdomain or path), lowercase, unique.</summary>
    public string Slug { get; set; } = string.Empty;
    /// <summary>Primary public host without scheme, e.g. etoilegourmandekin.com (www stripped at lookup).</summary>
    public string? CustomDomain { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
