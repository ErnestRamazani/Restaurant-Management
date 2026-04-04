namespace EliteRestaurantPro.Models;

public enum DashboardActivityNav
{
    None,
    Orders,
    Attendance,
    Inventory,
    Money
}

public class ActivityItem
{
    public string Time { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>UI strip color / badge: Order, Attendance, Inventory.</summary>
    public string ActivityKind { get; set; } = "Order";

    public DashboardActivityNav NavigationTarget { get; set; } = DashboardActivityNav.None;

    public string KindLabel => ActivityKind switch
    {
        "Attendance" => "TEAM",
        "Inventory" => "STOCK",
        _ => "ORDER"
    };

    /// <summary>Multi-line body under the title (timestamp + details, or full stock notes).</summary>
    public string DetailBlock =>
        ActivityKind == "Inventory" && !string.IsNullOrWhiteSpace(Description)
            ? Description.Trim()
            : string.IsNullOrWhiteSpace(Time) || Time == "—"
                ? Description.Trim()
                : string.IsNullOrWhiteSpace(Description)
                    ? Time.Trim()
                    : $"{Time.Trim()}\n{Description.Trim()}";
}
