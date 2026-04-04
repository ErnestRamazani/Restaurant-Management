using System.Windows.Media;

namespace EliteRestaurantPro.Models;

public class ReservationEntry
{
    public string ReservationId { get; set; } = string.Empty;
    public string GuestName { get; set; } = string.Empty;
    public string DateTime { get; set; } = string.Empty;
    public int PartySize { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Status { get; set; } = "Confirmed";

    private string _statusColor = "#D4AF37";
    public string StatusColor
    {
        get => _statusColor;
        set
        {
            _statusColor = value;
            StatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        }
    }

    public SolidColorBrush StatusBrush { get; private set; } =
        new SolidColorBrush(Color.FromRgb(0xD4, 0xAF, 0x37));
}
