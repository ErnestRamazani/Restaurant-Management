using EliteRestaurant.Core.Tenancy;

namespace EliteRestaurant.Core.Models;

public class WaitlistEntry : IRestaurantScoped
{
    public int Id { get; set; }
    public int RestaurantId { get; set; }
    public string UniqueId { get; set; } = string.Empty;
    public string GuestName { get; set; } = string.Empty;
    public string GuestPhone { get; set; } = string.Empty;
    public int PartySize { get; set; } = 2;
    public int? QuotedWaitMinutes { get; set; }
    public string UserNotes { get; set; } = string.Empty;
    public string Status { get; set; } = "Waiting";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
