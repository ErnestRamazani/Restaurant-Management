using EliteRestaurant.Core.Tenancy;

namespace EliteRestaurant.Core.Models;

public sealed class PublicMenuAsset : IRestaurantScoped
{
    public int Id { get; set; }
    public int RestaurantId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public byte[] Content { get; set; } = [];
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
