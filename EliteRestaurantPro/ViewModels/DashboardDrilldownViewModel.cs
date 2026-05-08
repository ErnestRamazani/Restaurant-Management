using System.Collections.ObjectModel;
using System.Globalization;
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
    {
        var activities = new List<(long SortKey, ActivityItem Item)>();
        var attendanceWindowStart = DateTime.Today.AddDays(-30);
        var attendanceWindowStartUtc = AttendanceCalendar.DayAnchorUtc(attendanceWindowStart);

        var latestOrders = allOrders
            .OrderByDescending(o => o.CreatedAt)
            .Take(60)
            .ToList();
        foreach (var order in latestOrders)
        {
            var orderId = string.IsNullOrWhiteSpace(order.UniqueId) ? $"Order #{order.Id:000}" : order.UniqueId;
            var tableLine = string.IsNullOrWhiteSpace(order.TableCode) && string.IsNullOrWhiteSpace(order.TableName)
                ? string.Empty
                : $"{order.TableCode} · {order.TableName}".Trim(' ', '·');
            var desc = string.IsNullOrEmpty(tableLine)
                ? $"Status: {order.Status}"
                : $"Status: {order.Status}\n{tableLine}";
            activities.Add((order.CreatedAt.Ticks, new ActivityItem
            {
                Time = order.CreatedAt.ToString("MMM dd, yyyy · HH:mm", CultureInfo.InvariantCulture),
                Title = orderId,
                Description = desc,
                ActivityKind = "Order",
                NavigationTarget = DashboardActivityNav.Orders
            }));
        }

        var latestAttendance = allAttendance
            .Where(a => a.WorkDate >= attendanceWindowStartUtc && a.ClockInTime != null)
            .OrderByDescending(a => a.ClockInTime)
            .Take(60)
            .ToList();
        foreach (var attendance in latestAttendance)
        {
            var at = attendance.ClockInTime ?? DateTime.Now;
            var status = string.IsNullOrWhiteSpace(attendance.ClockInStatus) ? "Recorded" : attendance.ClockInStatus;
            var empName = employeesById.TryGetValue(attendance.EmployeeId, out var e) ? e.Name : "Employee";
            activities.Add((at.Ticks, new ActivityItem
            {
                Time = at.ToString("MMM dd, yyyy · HH:mm", CultureInfo.InvariantCulture),
                Title = empName,
                Description = $"Clocked in ({status})\nShift date: {attendance.WorkDate:yyyy-MM-dd}",
                ActivityKind = "Attendance",
                NavigationTarget = DashboardActivityNav.Attendance
            }));
        }

        var inventoryWithNotes = allInventory
            .Where(i => !string.IsNullOrWhiteSpace(i.Notes))
            .OrderByDescending(i => i.Id)
            .Take(400)
            .ToList();
        foreach (var item in inventoryWithNotes)
        {
            var sortKey = ParseLatestInventoryNoteTimestampTicks(item.Notes);
            if (sortKey == 0)
                sortKey = item.Id * 10_000_000L;
            activities.Add((sortKey, new ActivityItem
            {
                Time = string.Empty,
                Title = item.Name,
                Description = item.Notes.Trim(),
                ActivityKind = "Inventory",
                NavigationTarget = DashboardActivityNav.Inventory
            }));
        }

        return activities
            .OrderByDescending(a => a.SortKey)
            .Take(RecentActivityMaxItems)
            .Select(a => a.Item)
            .ToList();
    }

    /// <summary>Parses leading <c>yyyy-MM-dd HH:mm</c> on the last non-empty note line (order deductions, manual adjustments).</summary>
    private static long ParseLatestInventoryNoteTimestampTicks(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return 0;
        var lines = notes.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            var line = lines[i];
            if (line.Length >= 16 &&
                DateTime.TryParseExact(line.AsSpan(0, 16), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt.Ticks;
        }

        return 0;
    }
}
