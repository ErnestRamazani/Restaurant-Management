using System.Globalization;
using System.Text.RegularExpressions;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurantPro.Localization;

/// <summary>Reports screen translators (mirrors web admin <c>translateReport*</c>).</summary>
public static class ReportsUiLocalizer
{
    private static readonly Dictionary<string, string> EventTypeKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Clock in"] = "clockIn",
        ["Clock out"] = "clockOut",
        ["Absence"] = "absence",
        ["Scheduled shift"] = "repEvScheduledShift",
        ["Menu Activity"] = "repEvMenuActivity",
        ["Menu Ordered"] = "repEvMenuOrdered",
        ["Inventory Activity"] = "repEvInventoryActivity",
        ["Inventory Note"] = "repEvInventoryNote",
        ["Online order"] = "repEvOnlineOrder",
        ["Table Service"] = "repEvTableService",
        ["Served Order"] = "repEvServedOrder",
        ["Table Order"] = "repEvTableOrder",
        ["Reservation"] = "repEvReservation",
        ["Salary advance (Money)"] = "repEvSalaryAdvanceMoney",
        ["Salary payment (Money)"] = "repEvSalaryPaymentMoney",
        ["Salary advance (record)"] = "repEvSalaryAdvanceRecord",
        ["Payroll month (record)"] = "repEvPayrollMonthRecord",
        ["Order"] = "repEvOrder"
    };

    private static readonly Dictionary<string, string> EventTypeFallbackEn = new(StringComparer.OrdinalIgnoreCase)
    {
        ["repEvMenuOrdered"] = "Menu Ordered",
        ["repEvOrder"] = "Order"
    };

    public static void ApplyDayGroup(ViewModels.ReportDayGroupDto group)
    {
        group.DayText = FormatReportDay(group.Day);
        group.TotalsText = TranslateTotalsText(group.TotalsText);
        foreach (var entry in group.Entries)
            ApplyEntry(entry);
    }

    public static void ApplyEntry(ViewModels.ReportTimeEntryDto entry)
    {
        entry.EventType = TranslateEventType(entry.EventType);
        entry.Summary = TranslateSummary(entry.Summary);
        entry.RelatedInfo = TranslateRelated(entry.RelatedInfo);
        entry.EntityContext = TranslateContext(entry.EntityContext);
        entry.MetricsText = FormatEntryMetric(entry.OrdersCount, entry.ItemCount, entry.UnitUsage);
    }

    public static void ApplyEntityItem(ViewModels.ReportEntityItem item, string kind)
    {
        item.Subtitle = kind switch
        {
            "employee" => AdminTextLocalizer.TranslateRole(item.RawSubtitle),
            "table" => AdminTextLocalizer.TranslateTableStatus(item.RawSubtitle),
            _ => item.RawSubtitle
        };
    }

    public static string FormatReportDay(DateTime day) =>
        day.ToString("dddd, MMM dd yyyy", AdminTextLocalizer.UiCulture);

    public static string TranslateTotalsText(string? totals)
    {
        var raw = (totals ?? string.Empty).Trim();
        if (raw.Length == 0)
            return raw;

        var m = Regex.Match(raw, @"^(\d+) events \| (\d+) orders \| (\d+) items \| ([\d.]+) units$");
        if (m.Success)
        {
            return Loc.Admin("repTotalsFull", "{{events}} events | {{orders}} orders | {{items}} items | {{units}} units",
                Vars(m));
        }

        m = Regex.Match(raw, @"^(\d+) order\(s\) \| (\d+) items$");
        if (m.Success)
        {
            return Loc.Admin("repTotalsOrders", "{{orders}} order(s) | {{items}} items",
                new Dictionary<string, string>
                {
                    ["orders"] = m.Groups[1].Value,
                    ["items"] = m.Groups[2].Value
                });
        }

        if (raw == "0 events | 0 orders | 0 items | 0 units")
            return Loc.Admin("repTotalsEmpty", raw);

        return raw;
    }

    public static string FormatEntryMetric(int ordersCount, int itemCount, decimal unitUsage)
    {
        if (ordersCount > 0)
        {
            return ordersCount == 1
                ? Loc.Admin("metricOrders_one", "{{count}} order", CountVar(ordersCount))
                : Loc.Admin("metricOrders_other", "{{count}} orders", CountVar(ordersCount));
        }

        if (itemCount > 0)
        {
            return itemCount == 1
                ? Loc.Admin("metricItems_one", "{{count}} item", CountVar(itemCount))
                : Loc.Admin("metricItems_other", "{{count}} items", CountVar(itemCount));
        }

        if (unitUsage > 0m)
            return Loc.Admin("metricUnits", "{{count}} units", CountVar(unitUsage));

        return "—";
    }

    public static string FormatOrdersRangeSummary(DateTime start, DateTime end, int orderCount, int dayCount, int itemCount) =>
        Loc.Admin("repOrdersRangeSummary",
            "Range {{start}} → {{end}}. {{orderCount}} order(s) over {{dayCount}} day(s), {{itemCount}} items total.",
            new Dictionary<string, string>
            {
                ["start"] = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["end"] = end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["orderCount"] = orderCount.ToString(CultureInfo.InvariantCulture),
                ["dayCount"] = dayCount.ToString(CultureInfo.InvariantCulture),
                ["itemCount"] = itemCount.ToString(CultureInfo.InvariantCulture)
            });

    public static string FormatDailyRangeSummary(DateTime start, DateTime end, int eventCount) =>
        Loc.Admin("repDailyRangeSummary",
            "Daily timeline {{start}} → {{end}}: {{eventCount}} events (clock-ins/sign-ins, orders, reservations, menu, inventory, payroll records, salary advances, Money salary ledger).",
            new Dictionary<string, string>
            {
                ["start"] = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["end"] = end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["eventCount"] = eventCount.ToString(CultureInfo.InvariantCulture)
            });

    /// <summary>Translates multiline raw inventory notes for detail panels.</summary>
    public static string TranslateInventoryNotes(string? notes) =>
        InventoryUiLocalizer.TranslateNotesForDisplay(notes);

    /// <summary>Translates a single raw inventory note line stored in the DB.</summary>
    public static string TranslateInventoryNote(string? line) =>
        DashboardTextLocalizer.TranslateInventoryNoteLine((line ?? string.Empty).Trim());

    public static string TranslateExportReportType(string canonical) =>
        canonical switch
        {
            "Daily" => Loc.Admin("repExportTypeDaily", "Daily"),
            "Orders" => Loc.Admin("repExportTypeOrders", "Orders"),
            "Employees" => Loc.Admin("repExportTypeEmployees", "Employees"),
            "Tables" => Loc.Admin("repExportTypeTables", "Tables"),
            "Inventory" => Loc.Admin("repExportTypeInventory", "Inventory"),
            "Menu" => Loc.Admin("repExportTypeMenu", "Menu"),
            "All Reports" => Loc.Admin("repExportTypeAll", "All Reports"),
            _ => canonical
        };

    public static string TranslateTableListCaption(int tableNumber, string tableName) =>
        Loc.Admin("repTableListCaption", "Table {{num}} - {{name}}",
            new Dictionary<string, string>
            {
                ["num"] = tableNumber.ToString(CultureInfo.InvariantCulture),
                ["name"] = tableName
            });

    public static string TranslateEventType(string? rawType)
    {
        var raw = (rawType ?? string.Empty).Trim();
        if (raw.Length == 0)
            return raw;

        if (EventTypeKeys.TryGetValue(raw, out var key))
        {
            if (EventTypeFallbackEn.TryGetValue(key, out var fb))
                return Loc.Admin(key, fb);
            return Loc.Admin(key, raw);
        }

        var orderSt = AdminTextLocalizer.TranslateOrderStatus(raw);
        return !string.Equals(orderSt, raw, StringComparison.Ordinal) ? orderSt : raw;
    }

    public static string TranslateSummary(string? text)
    {
        var raw = (text ?? string.Empty).Trim();
        if (raw.Length == 0)
            return raw;

        var m = Regex.Match(raw, @"^(.+?) marked absent$", RegexOptions.IgnoreCase);
        if (m.Success)
            return Loc.Admin("repAttMarkedAbsent", "{{name}} marked absent", NameVar(m.Groups[1].Value));

        m = Regex.Match(raw, @"^(.+?) signed in at (\d{1,2}:\d{2}) \((.+)\)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("repAttSignedIn", "{{name}} signed in at {{time}} ({{status}})",
                new Dictionary<string, string>
                {
                    ["name"] = m.Groups[1].Value,
                    ["time"] = m.Groups[2].Value,
                    ["status"] = AdminTextLocalizer.TranslateAttendanceClockStatus(m.Groups[3].Value)
                });
        }

        m = Regex.Match(raw, @"^(.+?) scheduled \(not clocked in yet\)$", RegexOptions.IgnoreCase);
        if (m.Success)
            return Loc.Admin("repAttScheduledNotIn", "{{name}} scheduled (not clocked in yet)", NameVar(m.Groups[1].Value));

        m = Regex.Match(raw, @"^(.+?) clocked out at (\d{1,2}:\d{2})$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("repAttClockedOut", "{{name}} clocked out at {{time}}",
                new Dictionary<string, string>
                {
                    ["name"] = m.Groups[1].Value,
                    ["time"] = m.Groups[2].Value
                });
        }

        m = Regex.Match(raw, @"^Absent \((.+)\)$", RegexOptions.IgnoreCase);
        if (m.Success)
            return Loc.Admin("repAttAbsentStatus", "Absent ({{status}})",
                new Dictionary<string, string> { ["status"] = AdminTextLocalizer.TranslateAttendanceClockStatus(m.Groups[1].Value) });

        m = Regex.Match(raw, @"^Sign in: (.+?) \((.+)\) \| Clock out: (.+)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("repAttSignInOut", "Sign in: {{in}} ({{status}}) | Clock out: {{out}}",
                new Dictionary<string, string>
                {
                    ["in"] = m.Groups[1].Value,
                    ["status"] = AdminTextLocalizer.TranslateAttendanceClockStatus(m.Groups[2].Value),
                    ["out"] = TranslateNotClockedOut(m.Groups[3].Value)
                });
        }

        if (raw.StartsWith("Scheduled | Clock out:", StringComparison.OrdinalIgnoreCase))
        {
            var outVal = raw["Scheduled | Clock out:".Length..].Trim();
            return Loc.Admin("repAttScheduledClockOut", "Scheduled | Clock out: {{out}}",
                new Dictionary<string, string> { ["out"] = TranslateNotClockedOut(outVal) });
        }

        m = Regex.Match(raw, @"^Order (ORD-[A-F0-9]+|#[0-9]+) · (\d+) item\(s\)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("repOrderItems", "Order {{orderId}} · {{count}} item(s)",
                new Dictionary<string, string>
                {
                    ["orderId"] = m.Groups[1].Value,
                    ["count"] = m.Groups[2].Value
                });
        }

        m = Regex.Match(raw, @"^Order (ORD-[A-F0-9]+) \((.+)\)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("repOrderStatus", "Order {{orderId}} ({{status}})",
                new Dictionary<string, string>
                {
                    ["orderId"] = m.Groups[1].Value,
                    ["status"] = AdminTextLocalizer.TranslateOrderStatus(m.Groups[2].Value)
                });
        }

        m = Regex.Match(raw, @"^Reservation (ORD-[A-Z0-9-]+|RES-[A-Z0-9-]+|\S+) · (.+?) · (.+)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("repReservationLine", "Reservation {{id}} · {{name}} · {{status}}",
                new Dictionary<string, string>
                {
                    ["id"] = m.Groups[1].Value,
                    ["name"] = m.Groups[2].Value,
                    ["status"] = AdminTextLocalizer.TranslateOrderStatus(m.Groups[3].Value)
                });
        }

        m = Regex.Match(raw, @"^Reservation (RES-[A-F0-9]+|\S+) \((.+)\)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("repReservationStatus", "Reservation {{id}} ({{status}})",
                new Dictionary<string, string>
                {
                    ["id"] = m.Groups[1].Value,
                    ["status"] = AdminTextLocalizer.TranslateOrderStatus(m.Groups[2].Value)
                });
        }

        m = Regex.Match(raw, @"^(.+?) x(\d+)$", RegexOptions.IgnoreCase);
        if (m.Success)
            return Loc.Admin("repMenuQty", "{{name}} x{{qty}}", MenuQtyVars(m));

        m = Regex.Match(raw, @"^(.+?) ×(\d+)$");
        if (m.Success)
            return Loc.Admin("repMenuQty", "{{name}} x{{qty}}", MenuQtyVars(m));

        m = Regex.Match(raw, @"^(.+?) x(\d+) in order (ORD-[A-F0-9]+)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("repMenuInOrder", "{{name}} x{{qty}} in order {{orderId}}",
                new Dictionary<string, string>
                {
                    ["name"] = m.Groups[1].Value,
                    ["qty"] = m.Groups[2].Value,
                    ["orderId"] = m.Groups[3].Value
                });
        }

        if (raw.Equals("Text-based inventory history entry", StringComparison.OrdinalIgnoreCase))
            return Loc.Admin("repInvTextEntry", raw);

        var moneyJ = MoneyUiLocalizer.TranslateJustification(raw);
        if (!string.Equals(moneyJ, raw, StringComparison.Ordinal))
            return moneyJ;

        var invJ = DashboardTextLocalizer.TranslateInventoryNoteLine(raw);
        if (!string.Equals(invJ, raw, StringComparison.Ordinal))
            return invJ;

        return raw;
    }

    public static string TranslateRelated(string? text)
    {
        var raw = (text ?? string.Empty).Trim();
        if (raw.Length == 0)
            return raw;

        var m = Regex.Match(raw, @"^Justification: (.+)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("repJustification", "Justification: {{value}}",
                new Dictionary<string, string> { ["value"] = DashIfMinus(m.Groups[1].Value) });
        }

        m = Regex.Match(raw, @"^Employee: (.+)$", RegexOptions.IgnoreCase);
        if (m.Success)
            return Loc.Admin("repEmployee", "Employee: {{name}}", NameVar(m.Groups[1].Value));

        m = Regex.Match(raw, @"^Order (ORD-[A-F0-9]+) \| (.+)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("repOrderTable", "Order {{orderId}} | {{table}}",
                new Dictionary<string, string>
                {
                    ["orderId"] = m.Groups[1].Value,
                    ["table"] = TranslateTableCaption(m.Groups[2].Value)
                });
        }

        m = Regex.Match(raw, @"^Work day (\d{4}-\d{2}-\d{2}) \| Justification: (.+)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("repWorkDayJustification", "Work day {{date}} | Justification: {{value}}",
                new Dictionary<string, string>
                {
                    ["date"] = m.Groups[1].Value,
                    ["value"] = DashIfMinus(m.Groups[2].Value)
                });
        }

        m = Regex.Match(raw, @"^Work day (\d{4}-\d{2}-\d{2}) \| Status: (.+)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("repWorkDayStatus", "Work day {{date}} | Status: {{status}}",
                new Dictionary<string, string>
                {
                    ["date"] = m.Groups[1].Value,
                    ["status"] = AdminTextLocalizer.TranslateAttendanceClockStatus(m.Groups[2].Value)
                });
        }

        m = Regex.Match(raw, @"^Shift (\d{1,2}:\d{2}) → (\d{1,2}:\d{2})$");
        if (m.Success)
        {
            return Loc.Admin("repShiftRange", "Shift {{start}} → {{end}}",
                new Dictionary<string, string>
                {
                    ["start"] = m.Groups[1].Value,
                    ["end"] = m.Groups[2].Value
                });
        }

        m = Regex.Match(raw, @"^Server: (.+?) \| (.+)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var items = m.Groups[2].Value.Equals("No order items.", StringComparison.OrdinalIgnoreCase)
                ? Loc.Admin("repNoOrderItems", "No order items.")
                : m.Groups[2].Value;
            return Loc.Admin("repServerItems", "Server: {{server}} | {{items}}",
                new Dictionary<string, string>
                {
                    ["server"] = TranslateServerName(m.Groups[1].Value),
                    ["items"] = items
                });
        }

        m = Regex.Match(raw, @"^(.+?) \| Server: (.+?) \| Items: (\d+)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("repTableServerItems", "{{table}} | Server: {{server}} | Items: {{count}}",
                new Dictionary<string, string>
                {
                    ["table"] = TranslateTableCaption(m.Groups[1].Value),
                    ["server"] = TranslateServerName(m.Groups[2].Value),
                    ["count"] = m.Groups[3].Value
                });
        }

        m = Regex.Match(raw, @"^(.+?) \| Server: (.+?) \| Status: (.+)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("repTableServerStatus", "{{table}} | Server: {{server}} | Status: {{status}}",
                new Dictionary<string, string>
                {
                    ["table"] = TranslateTableCaption(m.Groups[1].Value),
                    ["server"] = TranslateServerName(m.Groups[2].Value),
                    ["status"] = AdminTextLocalizer.TranslateOrderStatus(m.Groups[3].Value)
                });
        }

        m = Regex.Match(raw, @"^Reservation (RES-[A-F0-9]+|\S+) \| Guest: (.+?) \| Party: (\d+)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("repReservationGuest", "Reservation {{id}} | Guest: {{guest}} | Party: {{party}}",
                new Dictionary<string, string>
                {
                    ["id"] = m.Groups[1].Value,
                    ["guest"] = m.Groups[2].Value,
                    ["party"] = m.Groups[3].Value
                });
        }

        m = Regex.Match(raw, @"^Reserved for (\d{4}-\d{2}-\d{2} \d{2}:\d{2}) \| Party: (\d+) \| Table: (.+)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("repReservedFor", "Reserved for {{when}} | Party: {{party}} | Table: {{table}}",
                new Dictionary<string, string>
                {
                    ["when"] = m.Groups[1].Value,
                    ["party"] = m.Groups[2].Value,
                    ["table"] = m.Groups[3].Value == "-" ? "—" : TranslateTableCaption(m.Groups[3].Value)
                });
        }

        m = Regex.Match(raw, @"^Item: (.+?) \((INV-[A-F0-9]+)\)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("repInvItem", "Item: {{name}} ({{id}})",
                new Dictionary<string, string>
                {
                    ["name"] = m.Groups[1].Value,
                    ["id"] = m.Groups[2].Value
                });
        }

        if (raw.Contains(" | ", StringComparison.Ordinal))
        {
            return string.Join(" | ", raw.Split(" | ", StringSplitOptions.TrimEntries)
                .Select(TranslateRelated));
        }

        var invJ = DashboardTextLocalizer.TranslateInventoryNoteLine(raw);
        if (!string.Equals(invJ, raw, StringComparison.Ordinal))
            return invJ;

        return TranslateSummary(raw);
    }

    public static string TranslateContext(string? text)
    {
        var raw = (text ?? string.Empty).Trim();
        if (raw.Length == 0)
            return raw;

        var m = Regex.Match(raw, @"^Menu: (.+)$", RegexOptions.IgnoreCase);
        if (m.Success)
            return Loc.Admin("repMenuCtx", "Menu: {{name}}", NameVar(m.Groups[1].Value));

        m = Regex.Match(raw, @"^Employee: (.+)$", RegexOptions.IgnoreCase);
        if (m.Success)
            return Loc.Admin("repEmployee", "Employee: {{name}}", NameVar(m.Groups[1].Value));

        m = Regex.Match(raw, @"^Inventory: (.+)$", RegexOptions.IgnoreCase);
        if (m.Success)
            return Loc.Admin("repInventoryCtx", "Inventory: {{name}}", NameVar(m.Groups[1].Value));

        m = Regex.Match(raw, @"^Table: (.+)$", RegexOptions.IgnoreCase);
        if (m.Success)
            return Loc.Admin("repTablePrefix", "Table: {{label}}",
                new Dictionary<string, string> { ["label"] = TranslateTableCaption(m.Groups[1].Value) });

        m = Regex.Match(raw, @"^Table (\d+)$", RegexOptions.IgnoreCase);
        if (m.Success)
            return Loc.Admin("repTableNumber", "Table {{num}}",
                new Dictionary<string, string> { ["num"] = m.Groups[1].Value });

        if (raw.Equals("Reservations", StringComparison.OrdinalIgnoreCase))
            return Loc.Admin("repReservations", "Reservations");
        if (raw.Equals("Payroll", StringComparison.OrdinalIgnoreCase))
            return Loc.Admin("repEntityPayroll", "Payroll");
        if (raw.Equals("Salary advances", StringComparison.OrdinalIgnoreCase))
            return Loc.Admin("repEntitySalaryAdvances", "Salary advances");
        if (raw.Equals("Money", StringComparison.OrdinalIgnoreCase))
            return Loc.Admin("repEntityMoney", "Money");
        if (raw.Equals("Unassigned", StringComparison.OrdinalIgnoreCase))
            return Loc.Admin("invActorUnassigned", "Unassigned");

        return raw;
    }

    public static string TranslateTableCaption(string? caption)
    {
        var raw = (caption ?? string.Empty).Trim();
        if (raw.Length == 0)
            return raw;

        if (raw.Equals("Online · Delivery", StringComparison.OrdinalIgnoreCase))
            return Loc.Admin("repOnlineDelivery", "Online · Delivery");
        if (raw.Equals("Online · Pickup", StringComparison.OrdinalIgnoreCase))
            return Loc.Admin("repOnlinePickup", "Online · Pickup");

        var m = Regex.Match(raw, @"^Table (\d+)$", RegexOptions.IgnoreCase);
        if (m.Success)
            return Loc.Admin("repTableNumber", "Table {{num}}",
                new Dictionary<string, string> { ["num"] = m.Groups[1].Value });

        m = Regex.Match(raw, @"^Table: (.+)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("repTablePrefix", "Table: {{label}}",
                new Dictionary<string, string> { ["label"] = TranslateTableCaption(m.Groups[1].Value) });
        }

        return raw;
    }

    private static string TranslateServerName(string name) =>
        name.Equals("Unassigned", StringComparison.OrdinalIgnoreCase)
            ? Loc.Admin("invActorUnassigned", "Unassigned")
            : name;

    private static string TranslateNotClockedOut(string value) =>
        value.Equals("Not clocked out", StringComparison.OrdinalIgnoreCase)
            ? Loc.Admin("empNotClockedOut", "Not clocked out")
            : value;

    private static string DashIfMinus(string value) =>
        value == "-" ? "—" : value;

    private static Dictionary<string, string> NameVar(string name) =>
        new() { ["name"] = name };

    private static Dictionary<string, string> CountVar(decimal count) =>
        new() { ["count"] = count.ToString("0.##", CultureInfo.InvariantCulture) };

    private static Dictionary<string, string> Vars(Match m) => new()
    {
        ["events"] = m.Groups[1].Value,
        ["orders"] = m.Groups[2].Value,
        ["items"] = m.Groups[3].Value,
        ["units"] = m.Groups[4].Value
    };

    private static Dictionary<string, string> MenuQtyVars(Match m) => new()
    {
        ["name"] = m.Groups[1].Value,
        ["qty"] = m.Groups[2].Value
    };
}
