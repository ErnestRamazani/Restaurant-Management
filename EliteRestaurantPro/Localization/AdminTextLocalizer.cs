using System.Globalization;
using System.Text.RegularExpressions;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurantPro.Localization;

/// <summary>Shared admin UI string translators (mirrors web <c>portals.admin</c> helpers).</summary>
public static class AdminTextLocalizer
{
    private static readonly Dictionary<string, string> StatusFallbackEn = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pending_approval"] = "Pending approval",
        ["pending_cashier"] = "Pending cashier",
        ["waiting"] = "Waiting",
        ["in_kitchen"] = "In kitchen",
        ["ready"] = "Ready",
        ["served"] = "Served",
        ["completed"] = "Completed",
        ["cancelled"] = "Cancelled",
        ["refunded"] = "Refunded",
        ["pending"] = "Pending",
        ["on_account"] = "On account"
    };

    private static readonly Dictionary<string, string> StatusFallbackFr = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pending_approval"] = "En attente d'approbation",
        ["pending_cashier"] = "En attente caisse",
        ["waiting"] = "En attente",
        ["in_kitchen"] = "En cuisine",
        ["ready"] = "Prête",
        ["served"] = "Servie",
        ["completed"] = "Terminée",
        ["cancelled"] = "Annulée",
        ["refunded"] = "Remboursée",
        ["pending"] = "En attente",
        ["on_account"] = "En compte"
    };

    public static CultureInfo UiCulture =>
        Loc.Language == "fr" ? CultureInfo.GetCultureInfo("fr-FR") : CultureInfo.GetCultureInfo("en-US");

    public static string TranslateOrderStatus(string? status)
    {
        var raw = (status ?? string.Empty).Trim();
        if (raw.Length == 0)
            return raw;

        var key = NormalizeOrderStatusKey(raw);
        if (key.Length == 0)
            return raw;

        var fallbacks = Loc.Language == "fr" ? StatusFallbackFr : StatusFallbackEn;
        var fallback = fallbacks.TryGetValue(key, out var fb) ? fb : raw;
        return Loc.T("portals.admin.status." + key, fallback);
    }

    public static string TranslateAttendanceRowStatus(string? status)
    {
        var raw = (status ?? string.Empty).Trim();
        if (raw.Length == 0)
            return raw;
        if (raw.Equals("Off Shift", StringComparison.OrdinalIgnoreCase))
            return Loc.Admin("attOffShift", "Off Shift");
        return TranslateAttendanceClockStatus(raw);
    }

    public static string FormatCalendarDay(DateTime day, bool validated)
    {
        var dateText = FormatOrderCalendarDate(day);
        return validated
            ? Loc.Admin("ordDayValidated", "{{date}} · Validated", new Dictionary<string, string> { ["date"] = dateText })
            : dateText;
    }

    public static string FormatTodayCalendarDay(DateTime day, bool validated)
    {
        var dateText = FormatOrderCalendarDate(day);
        return validated
            ? Loc.Admin("ordTodayValidated", "Today - {{date}} · Validated", new Dictionary<string, string> { ["date"] = dateText })
            : Loc.Admin("ordTodayHeader", "Today - {{date}}", new Dictionary<string, string> { ["date"] = dateText });
    }

    private static string FormatOrderCalendarDate(DateTime day) =>
        Loc.Language == "fr"
            ? day.ToString("dddd d MMMM yyyy", UiCulture)
            : day.ToString("dddd, MMMM d, yyyy", UiCulture);

    private static string NormalizeOrderStatusKey(string status)
    {
        var spaced = Regex.Replace(status.Trim(), @"([a-z])([A-Z])", "$1 $2");
        var n = Regex.Replace(spaced, @"\s+", " ").Trim().ToLowerInvariant();
        return n switch
        {
            "waiting" => "waiting",
            "pending approval" => "pending_approval",
            "pending cashier" => "pending_cashier",
            "in kitchen" => "in_kitchen",
            "ready" => "ready",
            "served" => "served",
            "completed" => "completed",
            "refunded" => "refunded",
            "cancelled" or "canceled" => "cancelled",
            "pending" => "pending",
            "on account" or "debt" => "on_account",
            _ => Regex.Replace(n, @"[^a-z0-9]+", "_").Trim('_')
        };
    }
    private static readonly Dictionary<string, string> RoleKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Admin"] = "admin",
        ["Manager"] = "manager",
        ["Cashier"] = "cashier",
        ["Server"] = "server",
        ["Chef"] = "chef",
        ["Barman"] = "barman",
        ["Bartender"] = "bartender",
        ["Sous Chef"] = "sous_chef",
        ["Front desk"] = "front_desk",
        ["Other"] = "other"
    };

    public static string TranslateRole(string? role)
    {
        var raw = (role ?? string.Empty).Trim();
        if (raw.Length == 0)
            return raw;
        if (RoleKeys.TryGetValue(raw, out var key))
            return Loc.Admin("role." + key, raw);
        return raw;
    }

    public static string TranslateEmploymentStatus(string? status)
    {
        var lower = (status ?? string.Empty).Trim().ToLowerInvariant();
        return lower switch
        {
            "active" => Loc.Admin("staffStatusActive", "Active"),
            "on leave" => Loc.Admin("empEmploymentOnLeave", "On Leave"),
            "inactive" => Loc.Admin("empEmploymentInactive", "Inactive"),
            _ => status ?? string.Empty
        };
    }

    public static string TranslateShift(string? shift)
    {
        var raw = (shift ?? string.Empty).Trim();
        if (raw.Length == 0 || raw.Equals("Off", StringComparison.OrdinalIgnoreCase))
            return Loc.Admin("empShiftOff", "Off");
        if (raw.Equals("Morning Shift", StringComparison.OrdinalIgnoreCase))
            return Loc.Admin("empShiftMorning", "Morning Shift");
        if (raw.Equals("Night Shift", StringComparison.OrdinalIgnoreCase))
            return Loc.Admin("empShiftNight", "Night Shift");
        if (raw.Equals("Full Day", StringComparison.OrdinalIgnoreCase))
            return Loc.Admin("empShiftFullDay", "Full Day");
        if (raw.Contains("Evening", StringComparison.OrdinalIgnoreCase))
            return Loc.Admin("empShiftEvening", "Evening");
        return raw;
    }

    public static string TranslateDayShort(string dayShort)
    {
        var key = dayShort.Trim().ToLowerInvariant() switch
        {
            "mon" => "monday",
            "tue" => "tuesday",
            "wed" => "wednesday",
            "thu" => "thursday",
            "fri" => "friday",
            "sat" => "saturday",
            "sun" => "sunday",
            _ => string.Empty
        };
        return key.Length == 0
            ? dayShort
            : Loc.Admin("empDay." + key, dayShort);
    }

    public static string TranslateAttendanceClockStatus(string? status)
    {
        var lower = (status ?? string.Empty).Trim().ToLowerInvariant();
        return lower switch
        {
            "late" => Loc.Admin("empAttLate", "Late"),
            "on time" => Loc.Admin("empAttOnTime", "On Time"),
            "absent" => Loc.Admin("empAttAbsent", "Absent"),
            "pending" => Loc.Admin("empAttPending", "Pending"),
            "recorded" => Loc.Admin("empAttRecorded", "Recorded"),
            _ => status ?? string.Empty
        };
    }

    public static string TranslateEmployeeClockText(string? text)
    {
        var raw = (text ?? string.Empty).Trim();
        if (raw.Length == 0)
            return raw;
        if (raw.Equals("Not clocked in", StringComparison.OrdinalIgnoreCase))
            return Loc.Admin("empNotClockedIn", "Not clocked in");
        if (raw.Equals("Not clocked out", StringComparison.OrdinalIgnoreCase))
            return Loc.Admin("empNotClockedOut", "Not clocked out");
        var m = Regex.Match(raw, @"^(\d{1,2}:\d{2})\s+\((.+)\)$");
        if (m.Success)
            return m.Groups[1].Value + " (" + TranslateAttendanceClockStatus(m.Groups[2].Value) + ")";
        return raw;
    }

    public static string TranslateStaffPresenceStatus(bool isClockedIn) =>
        isClockedIn
            ? Loc.Admin("staffStatusActive", "Active")
            : Loc.Admin("staffStatusOffDuty", "Off duty");

    public static string TranslateMoneyType(string? type)
    {
        var raw = (type ?? string.Empty).Trim();
        return raw.Equals("Revenue", StringComparison.OrdinalIgnoreCase)
            ? Loc.Admin("moneyTypeRevenue", "Revenue")
            : raw.Equals("Expense", StringComparison.OrdinalIgnoreCase)
                ? Loc.Admin("moneyTypeExpense", "Expense")
                : raw;
    }

    public static string TranslateMoneyCategory(string? category)
    {
        var raw = (category ?? string.Empty).Trim();
        return raw switch
        {
            _ when raw.Equals("Sale", StringComparison.OrdinalIgnoreCase) => Loc.Admin("moneyCatSale", "Sale"),
            _ when raw.Equals("Salary", StringComparison.OrdinalIgnoreCase) => Loc.Admin("moneyCatSalary", "Salary"),
            _ when raw.Equals("Fixed Cost", StringComparison.OrdinalIgnoreCase) => Loc.Admin("moneyCatFixedCost", "Fixed Cost"),
            _ when raw.Equals("Tip", StringComparison.OrdinalIgnoreCase) => Loc.Admin("moneyCatTip", "Tip"),
            _ when raw.Equals("Gift", StringComparison.OrdinalIgnoreCase) => Loc.Admin("moneyCatGift", "Gift"),
            _ when raw.Equals("Variable", StringComparison.OrdinalIgnoreCase) => Loc.Admin("moneyCatVariable", "Variable"),
            _ when raw.Equals("Other", StringComparison.OrdinalIgnoreCase) => Loc.Admin("moneyCatOther", "Other"),
            _ when raw.Equals("Delivery Fee", StringComparison.OrdinalIgnoreCase) => Loc.Admin("moneyCatDeliveryFee", "Delivery Fee"),
            _ when raw.Equals("Sale Change", StringComparison.OrdinalIgnoreCase) => Loc.Admin("moneyCatSaleChange", "Sale Change"),
            _ when raw.Equals("Comp", StringComparison.OrdinalIgnoreCase) => Loc.Admin("moneyCatComp", "Comp"),
            _ when raw.Equals("Refund", StringComparison.OrdinalIgnoreCase) => Loc.Admin("moneyCatRefund", "Refund"),
            _ => raw
        };
    }

    public static string TranslateMoneyPeriod(string? period)
    {
        var raw = (period ?? string.Empty).Trim();
        return raw switch
        {
            _ when raw.Equals("Today", StringComparison.OrdinalIgnoreCase) => Loc.Admin("moneyPresetToday", "Today"),
            _ when raw.Equals("Week", StringComparison.OrdinalIgnoreCase) => Loc.Admin("moneyPresetWeek", "Week"),
            _ when raw.Equals("Month", StringComparison.OrdinalIgnoreCase) => Loc.Admin("moneyPresetMonth", "Month"),
            _ when raw.Equals("Year", StringComparison.OrdinalIgnoreCase) => Loc.Admin("moneyPresetYear", "Year"),
            _ when raw.Equals("All", StringComparison.OrdinalIgnoreCase) => Loc.Admin("moneyPresetAll", "All"),
            _ => raw
        };
    }

    public static string TranslateMoneyPeriodLabel(string? period)
    {
        var raw = (period ?? string.Empty).Trim();
        return raw switch
        {
            _ when raw.Equals("Today", StringComparison.OrdinalIgnoreCase) => Loc.Admin("moneyPeriodThisDay", "This Day"),
            _ when raw.Equals("Week", StringComparison.OrdinalIgnoreCase) => Loc.Admin("moneyPeriodThisWeek", "This Week"),
            _ when raw.Equals("Month", StringComparison.OrdinalIgnoreCase) => Loc.Admin("moneyPeriodThisMonth", "This Month"),
            _ when raw.Equals("Year", StringComparison.OrdinalIgnoreCase) => Loc.Admin("moneyPeriodThisYear", "This Year"),
            _ when raw.Equals("All", StringComparison.OrdinalIgnoreCase) => Loc.Admin("moneyPeriodAllTime", "All Time"),
            _ => raw
        };
    }

    public static string TranslateInventoryQuantityBand(string? band)
    {
        var raw = (band ?? string.Empty).Trim();
        return raw switch
        {
            "Out" => Loc.Admin("invQtyOut", "Out"),
            "Critical" => Loc.Admin("invQtyCritical", "Critical"),
            "Low" => Loc.Admin("invQtyLow", "Low"),
            "Healthy" => Loc.Admin("invQtyHealthy", "Healthy"),
            _ => raw
        };
    }

    public static string FormatInventoryShelfStatusLine(InventoryItem item)
    {
        var days = item.DaysUntilExpiration;
        if (!days.HasValue)
            return Loc.Admin("invExpNoExpiry", "No expiry");

        var daysText = days.Value.ToString(CultureInfo.InvariantCulture);
        return item.ExpirationStatus switch
        {
            "Expired" => Loc.Admin("invExpExpired", "Expired · {{days}} day(s)",
                new Dictionary<string, string> { ["days"] = daysText }),
            "Critical" => Loc.Admin("invExpCritical", "Critical · {{days}} day(s)",
                new Dictionary<string, string> { ["days"] = daysText }),
            "Bad" => Loc.Admin("invExpBad", "Bad · {{days}} day(s)",
                new Dictionary<string, string> { ["days"] = daysText }),
            _ => Loc.Admin("invExpGood", "Good · {{days}} day(s)",
                new Dictionary<string, string> { ["days"] = daysText })
        };
    }

    public static string FormatInventoryExpirationDateText(DateTime? expirationDate) =>
        expirationDate.HasValue
            ? expirationDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : Loc.Admin("invNoExpiryDate", "N/A");

    public static string TranslateTableStatus(string? status)
    {
        var lower = (status ?? string.Empty).Trim().ToLowerInvariant();
        return lower switch
        {
            "available" => Loc.Admin("tblStatusAvailable", "Available"),
            "occupied" => Loc.Admin("tblStatusOccupied", "Occupied"),
            "closed" => Loc.Admin("tblStatusClosed", "Closed"),
            "maintenance" => Loc.Admin("tblStatusMaintenance", "Maintenance"),
            _ => status ?? string.Empty
        };
    }

    public static string FormatShiftHistoryTitle(string employeeName) =>
        Loc.Admin("empShiftHistoryTitle", "Shift history — {{name}}",
            new Dictionary<string, string> { ["name"] = employeeName });

    public static string ShiftHistoryLoadingText =>
        Loc.Admin("empShiftHistoryLoading", "Loading…");

    public static string FormatShiftHistoryRowCount(int count) =>
        count == 1
            ? Loc.Admin("empShiftHistoryRowCountOne", "1 row")
            : Loc.Admin("empShiftHistoryRowCount", "{{count}} rows",
                new Dictionary<string, string> { ["count"] = count.ToString(CultureInfo.InvariantCulture) });

    public static string ShiftHistoryEmptyText =>
        Loc.Admin("empShiftHistoryEmpty", "No attendance rows stored for this employee yet.");

    public static string ShiftHistoryEmployeeNotFoundText =>
        Loc.Admin("empShiftHistoryEmployeeNotFound", "Employee not found.");

    public static string TranslateShiftHistoryStatus(string? status)
    {
        var raw = (status ?? string.Empty).Trim();
        if (raw.Equals("Off Shift", StringComparison.OrdinalIgnoreCase))
            return Loc.Admin("empShiftHistoryOffShift", "Off Shift");
        return TranslateAttendanceClockStatus(raw);
    }

    public static string TranslateEngagementStatus(string? status)
    {
        var raw = (status ?? string.Empty).Trim();
        if (raw.Length == 0)
            return raw;

        var key = raw switch
        {
            _ when raw.Equals("Scheduled", StringComparison.OrdinalIgnoreCase) => "engagementScheduled",
            _ when raw.Equals("CheckedIn", StringComparison.OrdinalIgnoreCase) => "engagementCheckedIn",
            _ when raw.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) => "engagementCancelled",
            _ when raw.Equals("NoShow", StringComparison.OrdinalIgnoreCase) => "engagementNoShow",
            _ when raw.Equals("Completed", StringComparison.OrdinalIgnoreCase) => "engagementCompleted",
            _ => string.Empty
        };

        if (key.Length == 0)
            return raw;

        var fallback = key switch
        {
            "engagementScheduled" => "Scheduled",
            "engagementCheckedIn" => "Checked in",
            "engagementCancelled" => "Cancelled",
            "engagementNoShow" => "No show",
            "engagementCompleted" => "Completed",
            _ => raw
        };

        return Loc.Admin(key, fallback);
    }

    public static string FormatReservationPlannedStart(DateTime plannedStartUtc, string? restaurantTimeZoneId)
    {
        var local = RestaurantTimeZone.UtcToRestaurant(
            DateTime.SpecifyKind(plannedStartUtc, DateTimeKind.Utc),
            restaurantTimeZoneId);
        var format = Loc.Language == "fr" ? "d/M/yyyy HH:mm" : "M/d/yyyy h:mm tt";
        return local.ToString(format, UiCulture);
    }

    public static string FormatReservationDateTimeLocal(DateTime? utc, string? restaurantTimeZoneId)
    {
        if (utc is null)
            return "—";

        var local = RestaurantTimeZone.UtcToRestaurant(
            DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc),
            restaurantTimeZoneId);
        var format = Loc.Language == "fr" ? "d/M/yyyy HH:mm" : "M/d/yyyy h:mm tt";
        return local.ToString(format, UiCulture);
    }

    public static string FormatReservationTableTag(string? tableLabel)
    {
        var raw = (tableLabel ?? string.Empty).Trim();
        if (raw.Length == 0 || raw == "—")
            return "—";

        if (raw.StartsWith("Table ", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("Table #", StringComparison.OrdinalIgnoreCase))
            return raw;

        return Loc.Admin("resTableTag", "Table {{label}}",
            new Dictionary<string, string> { ["label"] = raw });
    }
}
