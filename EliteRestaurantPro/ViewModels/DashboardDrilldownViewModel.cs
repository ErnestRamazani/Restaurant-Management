using System.Collections.ObjectModel;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurantPro.ViewModels;

public enum DashboardDrilldownType
{
    TodaySales,
    ActiveOrders,
    LowStockAlerts,
    ClockedInStaff,
    WeeklyRevenue,
    RecentActivity
}

public class DashboardDrilldownItem
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Meta { get; set; } = string.Empty;
    public string AccentColor { get; set; } = "#D4AF37";
}

public class DashboardDrilldownViewModel : AdminBaseViewModel
{
    public override string ActivePage => "Dashboard";

    public string HeaderTitle { get; private set; }
    public string HeaderSubtitle { get; private set; }
    public ObservableCollection<DashboardDrilldownItem> Items { get; } = [];

    public DashboardDrilldownViewModel(
        Action<BaseViewModel> navigate,
        string title,
        string subtitle,
        IEnumerable<DashboardDrilldownItem>? items) : base(navigate)
    {
        HeaderTitle = title;
        HeaderSubtitle = subtitle;
        var list = items?.ToList() ?? [];
        if (list.Count == 0)
        {
            list.Add(new DashboardDrilldownItem
            {
                Title = "No records found",
                Subtitle = "There is no data for this section yet.",
                Detail = "Create more orders or attendance entries and try again.",
                Meta = string.Empty,
                AccentColor = "#D4AF37"
            });
        }

        foreach (var item in list)
            Items.Add(item);
    }

    public const int RecentActivityMaxItems = 25;

    public static List<ActivityItem> BuildRecentActivities(
        IReadOnlyList<OrderRecord> allOrders,
        IReadOnlyList<EmployeeAttendance> allAttendance,
        IReadOnlyDictionary<int, Employee> employeesById,
        IReadOnlyList<InventoryItem> allInventory)
        => DashboardRecentActivities.Build(
            allOrders,
            allAttendance,
            employeesById,
            allInventory,
            RecentActivityMaxItems);
}
