namespace EliteRestaurant.Core.Models;

public class ReservationEntry
{
    public string ReservationId { get; set; } = string.Empty;
    public string GuestName { get; set; } = string.Empty;
    public string DateTime { get; set; } = string.Empty;
    public int PartySize { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Status { get; set; } = "Confirmed";
    public string StatusColor { get; set; } = "#D4AF37";
}
