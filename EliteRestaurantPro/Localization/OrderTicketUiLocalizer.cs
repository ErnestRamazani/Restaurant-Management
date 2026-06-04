using System.Globalization;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ViewModels;

namespace EliteRestaurantPro.Localization;

/// <summary>Localized labels and formatted lines for the admin order ticket / receipt overlay.</summary>
public static class OrderTicketUiLocalizer
{
    public static string ConfirmationCodeLabel =>
        Loc.Admin("ordTicketConfirmationCode", "CONFIRMATION CODE");

    public static string DateLabel => Loc.Admin("ordTicketDate", "Date:");
    public static string TimeLabel => Loc.Admin("ordTicketTime", "Time:");
    public static string OrderLabel => Loc.Admin("ordTicketOrder", "Order:");
    public static string StatusLabel => Loc.Admin("ordTicketStatus", "Status:");
    public static string CustomerLabel => Loc.Admin("ordTicketCustomer", "Customer:");
    public static string PhoneLabel => Loc.Admin("ordTicketPhone", "Phone:");
    public static string AddressLabel => Loc.Admin("ordTicketAddress", "Address:");
    public static string NotesLabel => Loc.Admin("ordTicketNotes", "Notes:");
    public static string ServerLabel => Loc.Admin("ordServerPrefix", "Server:");
    public static string EquivalentFcLabel => Loc.Admin("ordTicketEquivalentFc", "Equivalent FC:");
    public static string QtyHeader => Loc.Admin("ordTicketQty", "QTY");
    public static string ItemHeader => Loc.Admin("ordTicketItem", "ITEM");
    public static string UnitPriceHeader => Loc.Admin("ordTicketUnitPrice", "UNIT PRICE");
    public static string TotalHeader => Loc.Admin("ordTicketTotal", "TOTAL");
    public static string SubtotalLabel => Loc.Admin("ordTicketSubtotal", "Subtotal:");
    public static string DeliveryLabel => Loc.Admin("ordTicketDelivery", "Delivery:");
    public static string GrandTotalUsdLabel => Loc.Admin("ordTicketGrandTotalUsd", "GRAND TOTAL USD:");
    public static string VerificationPrefix => Loc.Admin("ordTicketVerification", "Verification:");
    public static string CloseLabel => Loc.Common("close", "Close");
    public static string PrintClientTicketLabel => Loc.Admin("ordTicketPrintClient", "Print Client Ticket");
    public static string PrintPaymentReceiptLabel => Loc.Admin("ordTicketPrintPayment", "Print Payment Receipt");

    public static string PickupSectionTitle => Loc.Admin("ordTicketPickup", "PICKUP");
    public static string DeliverySectionTitle => Loc.Admin("ordTicketDeliverySection", "DELIVERY");

    public static string FormatTicketDate(DateTime restaurantLocal) =>
        restaurantLocal.ToString(Loc.Language == "fr" ? "dd MMM yyyy" : "MMM d, yyyy", AdminTextLocalizer.UiCulture);

    public static string FormatTicketTime(DateTime restaurantLocal) =>
        restaurantLocal.ToString("HH:mm", AdminTextLocalizer.UiCulture);

    public static string FormatUsdLine(decimal amount) =>
        Loc.Admin("ordTicketUsdAmount", "$ {{amount}}",
            new Dictionary<string, string> { ["amount"] = amount.ToString("N2", CultureInfo.InvariantCulture) });

    public static string FormatPaidBreakdown(decimal paidUsd, decimal paidFc) =>
        Loc.Admin("ordTicketPaidBreakdown",
            "Paid USD: {{paidUsd}} | Paid FC: {{paidFc}}",
            new Dictionary<string, string>
            {
                ["paidUsd"] = CurrencyHelper.FormatAmount(paidUsd, CurrencyHelper.Usd, AdminTextLocalizer.UiCulture),
                ["paidFc"] = CurrencyHelper.FormatAmount(paidFc, CurrencyHelper.CongoleseFranc, AdminTextLocalizer.UiCulture)
            });

    public static string FormatChangeBreakdown(decimal changeUsd, decimal changeFc) =>
        Loc.Admin("ordTicketChangeBreakdown",
            "Change USD: {{changeUsd}} | Change FC: {{changeFc}}",
            new Dictionary<string, string>
            {
                ["changeUsd"] = CurrencyHelper.FormatAmount(changeUsd, CurrencyHelper.Usd, AdminTextLocalizer.UiCulture),
                ["changeFc"] = CurrencyHelper.FormatAmount(changeFc, CurrencyHelper.CongoleseFranc, AdminTextLocalizer.UiCulture)
            });

    public static string FormatVerification(string code) =>
        Loc.Admin("ordTicketVerification", "Verification:") + " " + code;

    public static string FormatTaxLabel(decimal percent) =>
        Loc.Admin("ordTicketTax", "TVA ({{pct}}%)",
            new Dictionary<string, string> { ["pct"] = percent.ToString("0.##", CultureInfo.InvariantCulture) });

    public static string FormatServiceLabel(decimal percent) =>
        Loc.Admin("ordTicketService", "Service ({{pct}}%)",
            new Dictionary<string, string> { ["pct"] = percent.ToString("0.##", CultureInfo.InvariantCulture) });

    public static string TranslateLocationLine(string? raw) =>
        ReportsUiLocalizer.TranslateTableCaption(raw);

    public static void ApplyLine(AdminOrdersViewModel.TicketLineViewModel line)
    {
        line.DisplayUnitPrice = FormatUsdLine(line.UnitPrice);
        line.DisplayLineTotal = FormatUsdLine(line.LineTotal);
    }

    public static void ApplyAllLines(IEnumerable<AdminOrdersViewModel.TicketLineViewModel> lines)
    {
        foreach (var line in lines)
            ApplyLine(line);
    }
}
