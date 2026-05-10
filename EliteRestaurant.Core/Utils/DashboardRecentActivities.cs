using System.Globalization;
using EliteRestaurant.Core.Models;

namespace EliteRestaurant.Core.Utils;

/// <summary>Builds the cross-cutting activity feed (orders, attendance, inventory notes) for admin dashboards.</summary>
public static class DashboardRecentActivities
{
    public const int DefaultMaxItems = 75;

    public static List<ActivityItem> Build(
        IReadOnlyList<OrderRecord> allOrders,
        IReadOnlyList<EmployeeAttendance> allAttendance,
        IReadOnlyDictionary<int, Employee> employeesById,
        IReadOnlyList<InventoryItem> allInventory,
        int maxItems = DefaultMaxItems)
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
            .Take(maxItems)
            .Select(a => a.Item)
            .ToList();
    }

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
