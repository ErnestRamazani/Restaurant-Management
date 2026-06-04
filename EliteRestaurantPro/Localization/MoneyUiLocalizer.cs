using System.Globalization;
using System.Text.RegularExpressions;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurantPro.Localization;

/// <summary>Money dashboard display strings (mirrors web <c>translateMoneyJustification</c>).</summary>
public static class MoneyUiLocalizer
{
    private static readonly string[] MoneyEnMonths =
    [
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December"
    ];

    public static string TranslateMoneyCurrency(string? code)
    {
        var normalized = CurrencyHelper.NormalizeCurrencyCode(code);
        return normalized == CurrencyHelper.CongoleseFranc
            ? Loc.Admin("moneyCurrencyFc", "Congolese franc (FC)")
            : Loc.Admin("moneyCurrencyUsd", "US dollar (USD)");
    }

    public static string TranslateJustification(string? justification)
    {
        var raw = (justification ?? string.Empty).Trim();
        if (raw.Length == 0)
            return "—";

        if (raw.Equals("Fixed scheduled transaction", StringComparison.OrdinalIgnoreCase))
            return Loc.Admin("moneyJustFixedScheduled", "Fixed scheduled transaction");

        if (raw.Equals("No justification", StringComparison.OrdinalIgnoreCase))
            return Loc.Admin("moneyJustNone", "No justification");

        var m = Regex.Match(
            raw,
            @"^Auto revenue from (ORD-[A-F0-9]+|Order #\d+) \(Reservation: (.+)\)$",
            RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("moneyJustAutoRevenueRes", "Auto revenue from {{orderId}} (Reservation: {{reservation}})",
                new Dictionary<string, string>
                {
                    ["orderId"] = m.Groups[1].Value,
                    ["reservation"] = m.Groups[2].Value
                });
        }

        m = Regex.Match(raw, @"^Auto revenue from (ORD-[A-F0-9]+|Order #\d+)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("moneyJustAutoRevenue", "Auto revenue from {{orderId}}",
                new Dictionary<string, string> { ["orderId"] = m.Groups[1].Value });
        }

        m = Regex.Match(
            raw,
            @"^Cash change returned for order (ORD-[A-F0-9]+) \((USD|FC)\)\. (\| CHANGE_ORDER:\d+:(USD|FC)\|)$",
            RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("moneyJustCashChange", "Cash change returned for order {{orderId}} ({{currency}}). {{marker}}",
                new Dictionary<string, string>
                {
                    ["orderId"] = m.Groups[1].Value,
                    ["currency"] = m.Groups[2].Value,
                    ["marker"] = m.Groups[3].Value
                });
        }

        m = Regex.Match(raw, @"^Delivery fee \(20%\) — (.+?) (\|ORDER:\d+:DELIVERY\|)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("moneyJustDeliveryFee", "Delivery fee (20%) — {{reference}} {{marker}}",
                new Dictionary<string, string>
                {
                    ["reference"] = TranslateJustification(m.Groups[1].Value),
                    ["marker"] = m.Groups[2].Value
                });
        }

        m = Regex.Match(
            raw,
            @"^(Partial|Final) monthly salary payment to (.+?) for (\w+ \d{4}) \(units:(\d+) sales USD:([\d.]+) bonus:([\d.]+) advances deducted:([\d.]+)\) (\| EMP:\d+\| NET:[\d.]+ THIS:[\d.]+ CUMULATIVE:[\d.]+\|)$",
            RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var partKey = m.Groups[1].Value.Equals("Final", StringComparison.OrdinalIgnoreCase)
                ? "moneySalaryFinal"
                : "moneySalaryPartial";
            return Loc.Admin("moneyJustSalaryPayment",
                "{{part}} monthly salary payment to {{name}} for {{month}} (units:{{units}} sales USD:{{sales}} bonus:{{bonus}} advances deducted:{{advances}}) {{marker}}",
                new Dictionary<string, string>
                {
                    ["part"] = Loc.Admin(partKey, m.Groups[1].Value),
                    ["name"] = m.Groups[2].Value,
                    ["month"] = TranslateMoneyMonthYear(m.Groups[3].Value),
                    ["units"] = m.Groups[4].Value,
                    ["sales"] = m.Groups[5].Value,
                    ["bonus"] = m.Groups[6].Value,
                    ["advances"] = m.Groups[7].Value,
                    ["marker"] = m.Groups[8].Value
                });
        }

        m = Regex.Match(raw, @"^Salary advance to (.+?) (\| EMP:\d+\| ADVANCE:\d+\| USD:[\d.]+\|?)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("moneyJustSalaryAdvance", "Salary advance to {{name}} {{marker}}",
                new Dictionary<string, string>
                {
                    ["name"] = m.Groups[1].Value,
                    ["marker"] = m.Groups[2].Value
                });
        }

        return raw;
    }

    public static string TranslateMoneyMonthYear(string? label)
    {
        var raw = (label ?? string.Empty).Trim();
        var m = Regex.Match(raw, @"^(\w+)\s+(\d{4})$");
        if (!m.Success)
            return raw;

        var idx = Array.IndexOf(MoneyEnMonths, m.Groups[1].Value);
        if (idx < 0)
            return raw;

        var d = new DateTime(int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), idx + 1, 1);
        return d.ToString("MMMM yyyy", AdminTextLocalizer.UiCulture);
    }

    public static string FormatLedgerDate(DateTime date) =>
        date.ToString(
            Loc.Language == "fr" ? "dd MMM HH:mm" : "dd MMM HH:mm",
            AdminTextLocalizer.UiCulture);
}
