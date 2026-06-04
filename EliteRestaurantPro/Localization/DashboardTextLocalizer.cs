using System.Globalization;
using System.Text.RegularExpressions;

namespace EliteRestaurantPro.Localization;

/// <summary>Ports web admin dashboard activity / inventory note translators for desktop.</summary>
public static class DashboardTextLocalizer
{
    private static readonly Dictionary<string, int> ShortMonths = new(StringComparer.OrdinalIgnoreCase)
    {
        ["jan"] = 0, ["feb"] = 1, ["mar"] = 2, ["apr"] = 3, ["may"] = 4, ["jun"] = 5,
        ["jul"] = 6, ["aug"] = 7, ["sep"] = 8, ["oct"] = 9, ["nov"] = 10, ["dec"] = 11
    };

    public static string TranslateKind(string? kind)
    {
        var k = (kind ?? string.Empty).Trim().ToUpperInvariant();
        return k switch
        {
            "ORDER" => Loc.Admin("actKindOrder", "ORDER"),
            "TEAM" => Loc.Admin("actKindTeam", "TEAM"),
            "STOCK" => Loc.Admin("actKindStock", "STOCK"),
            _ => kind ?? string.Empty
        };
    }

    public static string TranslateDetail(string? block)
    {
        var s = block ?? string.Empty;
        if (string.IsNullOrWhiteSpace(s))
            return s;

        var clockedIn = Regex.Match(s, @"^Clocked in \((.+)\)\r?\nShift date:\s*(.+)$", RegexOptions.Multiline);
        if (clockedIn.Success)
        {
            return Loc.Admin("actClockedInShift", "Clocked in ({{status}})\nShift date: {{date}}", new Dictionary<string, string>
            {
                ["status"] = AdminTextLocalizer.TranslateAttendanceClockStatus(clockedIn.Groups[1].Value.Trim()),
                ["date"] = clockedIn.Groups[2].Value.Trim()
            });
        }

        return string.Join("\n", s.Split('\n').Select(TranslateDetailLine));
    }

    private static string TranslateDetailLine(string line)
    {
        var raw = line.Trim();
        if (raw.Length == 0)
            return raw;

        var inv = TranslateInventoryNoteLine(raw);
        if (!string.Equals(inv, raw, StringComparison.Ordinal))
            return inv;

        if (Regex.IsMatch(raw, @"^[A-Za-z]{3} \d{2}, \d{4} · \d{2}:\d{2}$"))
            return TranslateActivityTimestamp(raw);

        var status = Regex.Match(raw, @"^Status:\s*(.+)$", RegexOptions.IgnoreCase);
        if (status.Success)
        {
            return Loc.Admin("actStatusLine", "Status: {{status}}", new Dictionary<string, string>
            {
                ["status"] = AdminTextLocalizer.TranslateOrderStatus(status.Groups[1].Value.Trim())
            });
        }

        return raw;
    }

    public static string TranslateInventoryNoteLine(string line)
    {
        var raw = line.Trim();
        if (raw.Length == 0)
            return raw;

        var m = Regex.Match(raw,
            @"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2})Z - ([\d.]+) (.+?) deducted \(add-on\) from order (ORD-[A-F0-9]+)\. Used by (.+)\.$",
            RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("invNoteDeductionAddon",
                "{{ts}}Z - {{qty}} {{item}} deducted (add-on) from order {{orderId}}. Used by {{actors}}.",
                Vars(m));
        }

        m = Regex.Match(raw,
            @"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2})Z - ([\d.]+) (.+?) deducted from order (ORD-[A-F0-9]+)\. Used by (.+)\.$",
            RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("invNoteDeduction",
                "{{ts}}Z - {{qty}} {{item}} deducted from order {{orderId}}. Used by {{actors}}.",
                Vars(m));
        }

        m = Regex.Match(raw, @"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}) - Stock added ([\d.]+) (.+?): (.+)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("invNoteStockAdded", "{{ts}} - Stock added {{qty}} {{unit}}: {{comment}}",
                new Dictionary<string, string>
                {
                    ["ts"] = m.Groups[1].Value,
                    ["qty"] = m.Groups[2].Value,
                    ["unit"] = m.Groups[3].Value,
                    ["comment"] = m.Groups[4].Value
                });
        }

        m = Regex.Match(raw, @"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}) - Manual deduction ([\d.]+) (.+?): (.+)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("invNoteManualDeduction", "{{ts}} - Manual deduction {{qty}} {{unit}}: {{comment}}",
                new Dictionary<string, string>
                {
                    ["ts"] = m.Groups[1].Value,
                    ["qty"] = m.Groups[2].Value,
                    ["unit"] = m.Groups[3].Value,
                    ["comment"] = m.Groups[4].Value
                });
        }

        return raw;
    }

    private static Dictionary<string, string> Vars(Match m) => new()
    {
        ["ts"] = m.Groups[1].Value,
        ["qty"] = m.Groups[2].Value,
        ["item"] = m.Groups[3].Value,
        ["orderId"] = m.Groups[4].Value,
        ["actors"] = TranslateInventoryActorText(m.Groups[5].Value)
    };

    private static string TranslateInventoryActorText(string actorText)
    {
        var raw = actorText.Trim();
        if (raw.Length == 0)
            return raw;
        if (string.Equals(raw, "Unassigned", StringComparison.OrdinalIgnoreCase))
            return Loc.Admin("invActorUnassigned", "Unassigned");
        return string.Join(", ", raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(TranslateInventoryActorSegment));
    }

    private static string TranslateInventoryActorSegment(string segment)
    {
        var raw = segment.Trim();
        if (raw.Length == 0)
            return raw;
        if (string.Equals(raw, "Unassigned", StringComparison.OrdinalIgnoreCase))
            return Loc.Admin("invActorUnassigned", "Unassigned");
        if (string.Equals(raw, "Unassigned Chef", StringComparison.OrdinalIgnoreCase))
            return Loc.Admin("invActorUnassignedChef", "Unassigned Chef");
        if (string.Equals(raw, "Unassigned Barman", StringComparison.OrdinalIgnoreCase))
            return Loc.Admin("invActorUnassignedBarman", "Unassigned Barman");

        var m = Regex.Match(raw, @"^(Chef|Barman|Unknown)\s+(.+):\s*([\d.]+)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("invActorSegment", "{{role}} {{name}}: {{qty}}",
                new Dictionary<string, string>
                {
                    ["role"] = AdminTextLocalizer.TranslateRole(m.Groups[1].Value),
                    ["name"] = TranslateInventoryActorName(m.Groups[2].Value),
                    ["qty"] = m.Groups[3].Value
                });
        }

        return raw;
    }

    private static string TranslateInventoryActorName(string name)
    {
        var n = name.Trim();
        if (string.Equals(n, "Unassigned Chef", StringComparison.OrdinalIgnoreCase))
            return Loc.Admin("invActorUnassignedChef", "Unassigned Chef");
        if (string.Equals(n, "Unassigned Barman", StringComparison.OrdinalIgnoreCase))
            return Loc.Admin("invActorUnassignedBarman", "Unassigned Barman");
        return n;
    }

    private static string TranslateActivityTimestamp(string raw)
    {
        var m = Regex.Match(raw, @"^([A-Za-z]{3}) (\d{2}), (\d{4}) · (\d{2}):(\d{2})$");
        if (!m.Success)
            return raw;

        var monthKey = m.Groups[1].Value[..Math.Min(3, m.Groups[1].Value.Length)].ToLowerInvariant();
        if (!ShortMonths.TryGetValue(monthKey, out var monthIdx))
            return raw;

        var dt = new DateTime(
            int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture),
            monthIdx + 1,
            int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
            int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture),
            int.Parse(m.Groups[5].Value, CultureInfo.InvariantCulture),
            0,
            DateTimeKind.Unspecified);

        var culture = Loc.Language == "fr" ? "fr-FR" : "en-US";
        var ci = CultureInfo.GetCultureInfo(culture);
        var datePart = dt.ToString("MMM dd, yyyy", ci);
        var timePart = dt.ToString(Loc.Language == "fr" ? "HH:mm" : "hh:mm tt", ci);
        return datePart + " · " + timePart;
    }
}
