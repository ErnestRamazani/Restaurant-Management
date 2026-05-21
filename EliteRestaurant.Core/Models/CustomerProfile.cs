namespace EliteRestaurant.Core.Models;

public class CustomerProfile
{
    public int Id { get; set; }
    public string UniqueId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PrimaryPhone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PreferredContactChannel { get; set; } = "Phone";
    public string Notes { get; set; } = string.Empty;
    public int NoShowCount { get; set; }
    public int CompletedReservationCount { get; set; }
    public DateTime? LastVisitAt { get; set; }

    /// <summary>UI language preference: <c>en</c> or <c>fr</c>.</summary>
    public string PreferredLanguage { get; set; } = "en";
}
