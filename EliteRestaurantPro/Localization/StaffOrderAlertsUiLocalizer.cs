using EliteRestaurant.Core.Utils;

namespace EliteRestaurantPro.Localization;

/// <summary>Localized staff tablet pickup/checkout banner (mirrors <see cref="StaffOrderAlerts"/>).</summary>
public static class StaffOrderAlertsUiLocalizer
{
    public static string GetBannerText() => StaffOrderAlerts.GetBannerText() switch
    {
        { Length: 0 } => string.Empty,
        var raw => LocalizeBanner(raw)
    };

    private static string LocalizeBanner(string raw)
    {
        var lines = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < lines.Length; i++)
            lines[i] = LocalizeBannerLine(lines[i]);
        return string.Join("\n", lines);
    }

    private static string LocalizeBannerLine(string line)
    {
        if (line.StartsWith("Ready for pickup — ", StringComparison.Ordinal))
        {
            var rest = line["Ready for pickup — ".Length..];
            return Loc.Admin("staffAlertReadyForPickup", "Ready for pickup — ") + rest;
        }

        if (line.StartsWith("Awaiting checkout (Served) — ", StringComparison.Ordinal))
        {
            var rest = line["Awaiting checkout (Served) — ".Length..];
            return Loc.Admin("staffAlertAwaitingCheckout", "Awaiting checkout (Served) — ") + rest;
        }

        if (line.StartsWith("At table — ring cashier when paid — ", StringComparison.Ordinal))
        {
            var rest = line["At table — ring cashier when paid — ".Length..];
            return Loc.Admin("staffAlertAtTablePay", "At table — ring cashier when paid — ") + rest;
        }

        return line;
    }
}
