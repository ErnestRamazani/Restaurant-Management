using System.Collections.ObjectModel;
using System.Globalization;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ViewModels;

namespace EliteRestaurantPro.Localization;

/// <summary>Create-order / take-order screen strings (<c>portals.admin.createOrder*</c>).</summary>
public static class CreateOrderUiLocalizer
{
    public static CultureInfo MoneyCulture => AdminTextLocalizer.UiCulture;

    public static string DialogTitle =>
        AppSession.IsServerTablet || AppSession.IsCashierTablet
            ? Loc.Admin("navTakeOrder", "Take Order")
            : Loc.Admin("navCreateOrder", "Create Order");

    public static string PageTitle => DialogTitle;

    public static string PageSubtitle =>
        AppSession.IsServerTablet || AppSession.IsCashierTablet
            ? Loc.Admin("createOrderSubtitleTablet",
                "Shared order pad for admin/server/cashier. If table already has an open check, you can append lines to the same ticket.")
            : Loc.Admin("createOrderSubtitleAdmin",
                "Create and manage table tickets with live totals, discounts, and open-check append support.");

    public static string PrimaryActionLabel =>
        AppSession.IsServerTablet || AppSession.IsCashierTablet
            ? Loc.Admin("createOrderSendToCashier", "Send to cashier")
            : Loc.Admin("navCreateOrder", "Create Order");

    public static string OpenCheckBanner(string code, string? rawStatus) =>
        Loc.Admin("createOrderOpenCheckBanner",
            "Open check {{code}} ({{status}}) exists for this table. Submit will ask to append or create a separate ticket.",
            new Dictionary<string, string>
            {
                ["code"] = code,
                ["status"] = AdminTextLocalizer.TranslateOrderStatus(rawStatus)
            });

    public static string EmptyDraftLabel =>
        Loc.Admin("createOrderDraftNone", "None (empty slot)");

    public static string NoClientLinked =>
        Loc.Admin("createOrderNoClientLinked", "No client linked");

    public static string TableComboPrefix =>
        Loc.Admin("createOrderTablePrefix", "Table ");

    public static string FormatDiscountLabel(string? discountMode, decimal discountValue, decimal discountApplied)
    {
        if (discountApplied <= 0m)
            return string.Empty;
        if (string.Equals(discountMode, "Percent", StringComparison.OrdinalIgnoreCase))
        {
            var pct = Math.Min(Math.Max(discountValue, 0m), 100m).ToString("0.##", CultureInfo.InvariantCulture);
            return Loc.Admin("createOrderDiscountPctLabel", "Discount ({{pct}}%)",
                new Dictionary<string, string> { ["pct"] = pct });
        }

        if (string.Equals(discountMode, "Usd", StringComparison.OrdinalIgnoreCase))
        {
            var amt = CurrencyHelper.FormatAmount(discountValue, CurrencyHelper.Usd, MoneyCulture);
            return Loc.Admin("createOrderDiscountUsdLabel", "Discount ({{amount}})",
                new Dictionary<string, string> { ["amount"] = amt });
        }

        return Loc.Admin("createOrderDiscountGeneric", "Discount");
    }

    public static string DiscountModeLabel(string value) =>
        value switch
        {
            "Percent" => Loc.Admin("createOrderDiscountPercent", "Percent"),
            "Usd" => Loc.Admin("createOrderDiscountUsd", "USD amount"),
            _ => Loc.Admin("createOrderDiscountNone", "None")
        };

    public static void RebuildDiscountModeOptions(ObservableCollection<LocalizedSelectOption> target)
    {
        target.Clear();
        foreach (var value in new[] { "None", "Percent", "Usd" })
        {
            target.Add(new LocalizedSelectOption
            {
                Value = value,
                Label = DiscountModeLabel(value)
            });
        }
    }

    public static string TaxRateLabel(decimal pct) =>
        Loc.Admin("createOrderTvaLabel", "TVA ({{pct}}%)",
            new Dictionary<string, string> { ["pct"] = pct.ToString("0.##", CultureInfo.InvariantCulture) });

    public static string ServiceRateLabel(decimal pct) =>
        Loc.Admin("createOrderServiceLabel", "Service ({{pct}}%)",
            new Dictionary<string, string> { ["pct"] = pct.ToString("0.##", CultureInfo.InvariantCulture) });

    public static string SubtotalCaption(bool hasSelection, bool includesOpenCheck) =>
        !hasSelection
            ? Loc.Admin("createOrderSubtotalItems", "Subtotal (items):")
            : includesOpenCheck
                ? Loc.Admin("createOrderSubtotalTicket", "Ticket subtotal (existing check + new lines):")
                : Loc.Admin("createOrderSubtotalItems", "Subtotal (items):");

    public static string NoDiscountSummary =>
        Loc.Admin("createOrderNoDiscountApplied", "No discount applied.");

    public static string LiveDiscountSummary(string label, decimal amount) =>
        $"{label}: -{CurrencyHelper.FormatAmount(amount, CurrencyHelper.Usd, MoneyCulture)}";

    public static string EstimatedPrepText(int minutes) =>
        minutes <= 0
            ? "-"
            : Loc.Admin("createOrderPrepMinutes", "{{minutes}} min",
                new Dictionary<string, string> { ["minutes"] = minutes.ToString(CultureInfo.InvariantCulture) });

    public static string FormatMoneyLine(string labelKey, string labelFallback, decimal amount) =>
        $"{Loc.Admin(labelKey, labelFallback)} {CurrencyHelper.FormatAmount(amount, CurrencyHelper.Usd, MoneyCulture)}";

    // —— Open check dialog ——
    public static string OpenCheckDialogTitle =>
        Loc.Admin("createOrderDlgOpenCheckTitle", "Open check on table");

    public static string OpenCheckDialogHeading =>
        Loc.Admin("createOrderDlgOpenCheckHeading", "Open check on this table");

    public static string OpenCheckDialogSummary(string tableName, string checkCode, string? rawStatus) =>
        Loc.Admin("createOrderDlgOpenCheckSummary",
            "{{table}} already has an open ticket {{code}}.\nStatus: {{status}}",
            new Dictionary<string, string>
            {
                ["table"] = tableName,
                ["code"] = checkCode,
                ["status"] = AdminTextLocalizer.TranslateOrderStatus(rawStatus)
            });

    public static string OpenCheckNewLinesPrompt(int count) =>
        count == 1
            ? Loc.Admin("createOrderDlgNewLineOne", "You are sending 1 new line on this order.")
            : Loc.Admin("createOrderDlgNewLineMany", "You are sending {{count}} new lines on this order.",
                new Dictionary<string, string> { ["count"] = count.ToString(CultureInfo.InvariantCulture) });

    public static string OpenCheckNewLinesSubtotal(decimal usd) =>
        Loc.Admin("createOrderDlgNewLinesSubtotal", "Subtotal for new lines: {{amount}}",
            new Dictionary<string, string>
            {
                ["amount"] = CurrencyHelper.FormatAmount(usd, CurrencyHelper.Usd, MoneyCulture)
            });

    // —— Confirm dialog ——
    public static string ConfirmDialogTitle(bool tabletStaff) =>
        tabletStaff
            ? Loc.Admin("createOrderDlgConfirmSendCashier", "Send to cashier")
            : Loc.Admin("createOrderDlgConfirmTitle", "Confirm create order");

    public static string ConfirmDialogPrimaryButton(bool tabletStaff) =>
        tabletStaff
            ? Loc.Admin("createOrderSendToCashier", "Send to cashier")
            : Loc.Admin("createOrderDlgCreateOrderBtn", "Create order");

    public static string ConfirmWalkInQuestion(int tableNumber, string tableName, int itemCount) =>
        Loc.Admin("createOrderConfirmWalkIn",
            "Create walk-in order for Table {{num}} ({{name}}) with {{count}} selected item(s)?",
            new Dictionary<string, string>
            {
                ["num"] = tableNumber.ToString(CultureInfo.InvariantCulture),
                ["name"] = tableName,
                ["count"] = itemCount.ToString(CultureInfo.InvariantCulture)
            });

    public static string ConfirmDetailsBlock(
        decimal subtotal,
        string? discountLine,
        string grandTotalUsd,
        string grandTotalFc,
        string amountToCollect,
        string estimatedPrep) =>
        $"{Loc.Admin("createOrderDetailsSubtotal", "Subtotal:")} {CurrencyHelper.FormatAmount(subtotal, CurrencyHelper.Usd, MoneyCulture)}{discountLine}\n" +
        $"{Loc.Admin("createOrderDetailsGrandTotal", "Grand Total:")} {grandTotalUsd}\n" +
        $"{Loc.Admin("createOrderDetailsEquivalentFc", "Equivalent FC:")} {grandTotalFc}\n" +
        $"{Loc.Admin("createOrderDetailsAmountCollect", "Amount To Collect:")} {amountToCollect}\n" +
        $"{Loc.Admin("createOrderDetailsEstimatedPrep", "Estimated Prep:")} {estimatedPrep}";

    // —— Order submission ——
    public static string ErrTableNeedsServer =>
        Loc.Admin("createOrderErrNoServer", "Selected table must have an assigned server.");

    public static string ErrTableNotAssignedToYou =>
        Loc.Admin("createOrderErrNotYourTable", "This table is not assigned to your session.");

    public static string InsufficientInventoryTitle =>
        Loc.Admin("createOrderInsufficientInvTitle", "Not enough inventory");

    public static string InsufficientInventoryBody(string? apiBody)
    {
        var intro = Loc.Admin("createOrderInsufficientInvIntro",
            "This order cannot be created until inventory is updated or the order is changed.");
        var body = (apiBody ?? string.Empty).Trim();
        return string.IsNullOrEmpty(body) ? intro : $"{intro}\n\n{body}";
    }

    public static string CloudApiCaption => Loc.Admin("createOrderCloudApi", "Cloud API");
}
