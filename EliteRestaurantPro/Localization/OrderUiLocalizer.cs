using System.Globalization;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ViewModels;

namespace EliteRestaurantPro.Localization;

public static class OrderUiLocalizer
{
    public static void Apply(OrderEntry order)
    {
        order.DisplayStatus = AdminTextLocalizer.TranslateOrderStatus(order.Status);
        order.DisplayTableLabel = ReportsUiLocalizer.TranslateTableCaption(order.TableNumber);
        order.DisplayServerLine = string.IsNullOrWhiteSpace(order.ServerName)
            ? string.Empty
            : Loc.Admin("ordServerPrefix", "Server:") + " " + TranslateServerName(order.ServerName);
        order.DisplayConfirmationLine = order.ShowConfirmationCode
            ? Loc.Admin("ordCodePrefix", "Code:") + " " + order.ConfirmationCode
            : string.Empty;

        var tz = SettingsManager.Load().BusinessProfile.RestaurantTimeZoneId;
        var timeFmt = Loc.Language == "fr" ? "HH:mm" : "HH:mm";
        order.DisplayTime = RestaurantTimeZone.FormatUtc(
            order.CreatedAt, tz, timeFmt, AdminTextLocalizer.UiCulture);
    }

    public static void Apply(CashierQueueRow row)
    {
        row.DisplayTableLabel = ReportsUiLocalizer.TranslateTableCaption(row.TableLabel);
        row.DisplayServerLine = string.IsNullOrWhiteSpace(row.ServerName)
            ? string.Empty
            : Loc.Admin("ordServerPrefix", "Server:") + " " + TranslateServerName(row.ServerName);

        var tz = SettingsManager.Load().BusinessProfile.RestaurantTimeZoneId;
        var dateFmt = Loc.Language == "fr" ? "d MMM yyyy · HH:mm" : "MMM d, yyyy · HH:mm";
        row.DisplayCreatedAtText = RestaurantTimeZone.FormatUtc(
            row.CreatedAt, tz, dateFmt, AdminTextLocalizer.UiCulture);
    }

    public static void ApplyDayGroup(AdminOrdersViewModel.PastOrderDayGroup group, DateTime today)
    {
        group.DayText = group.Day == today.Date
            ? AdminTextLocalizer.FormatTodayCalendarDay(group.Day, false)
            : AdminTextLocalizer.FormatCalendarDay(group.Day, false);
        group.OrdersCountText = Loc.Admin("ordPastDayCount", "({{count}} orders)",
            new Dictionary<string, string> { ["count"] = group.Count.ToString(CultureInfo.InvariantCulture) });
    }

    public static void ApplyAll(IEnumerable<OrderEntry> orders)
    {
        foreach (var order in orders)
            Apply(order);
    }

    private static string TranslateServerName(string name) =>
        name.Equals("Unassigned", StringComparison.OrdinalIgnoreCase)
            ? Loc.Admin("ordServerUnassigned", "Unassigned")
            : name.Equals("—", StringComparison.Ordinal)
                ? "—"
                : name;
}
