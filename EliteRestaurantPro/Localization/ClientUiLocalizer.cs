using System.Globalization;
using System.Text.RegularExpressions;
using EliteRestaurant.Contracts.Clients;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ViewModels;

namespace EliteRestaurantPro.Localization;

public static class ClientUiLocalizer
{
    public static void Apply(ClientOrderListItem item)
    {
        var d = item.Dto;
        item.DisplayStatus = AdminTextLocalizer.TranslateOrderStatus(
            OrderDisplayStatus.ForOrder(d.Status, d.ClientSettlement, d.AmountOnAccountUsd, d.ClientDebtSettledUsd));
        item.DisplaySettlementText = FormatSettlement(d);
        item.DisplayCreatedText = d.CreatedAt.ToString(
            Loc.Language == "fr" ? "d MMM yyyy · HH:mm" : "MMM d, yyyy · HH:mm",
            AdminTextLocalizer.UiCulture);
    }

    public static void Apply(ClientLedgerListItem item)
    {
        item.DisplayTypeLabel = item.Dto.EntryType switch
        {
            ClientDebtLedgerEntryType.Charge => Loc.Admin("cltLedgerTypeCharge", "Charge"),
            ClientDebtLedgerEntryType.Payment => Loc.Admin("cltLedgerTypePayment", "Payment"),
            ClientDebtLedgerEntryType.RevenueRecognized => Loc.Admin("cltLedgerTypeRevenue", "Revenue"),
            _ => item.Dto.EntryType
        };
        item.DisplayNoteText = TranslateLedgerNote(item.Dto.Note);
        item.DisplayCreatedText = RestaurantTimeZone.FormatUtc(
            item.Dto.CreatedAtUtc,
            SettingsManager.Load().BusinessProfile.RestaurantTimeZoneId,
            Loc.Language == "fr" ? "d MMM · HH:mm" : "MMM d · HH:mm",
            AdminTextLocalizer.UiCulture);
        item.DisplayOrderPrefix = Loc.Admin("cltLedgerOrderPrefix", "Order:");
        item.DisplayBalPrefix = Loc.Admin("cltLedgerBalPrefix", "Bal");
    }

    public static void ApplyAll(IEnumerable<ClientOrderListItem> orders)
    {
        foreach (var item in orders)
            Apply(item);
    }

    public static void ApplyAll(IEnumerable<ClientLedgerListItem> ledger)
    {
        foreach (var item in ledger)
            Apply(item);
    }

    public static string FormatClientCount(int count) =>
        Loc.Admin("cltClientCount", "{{count}} client(s)",
            new Dictionary<string, string> { ["count"] = count.ToString(CultureInfo.InvariantCulture) });

    private static string FormatSettlement(ClientOrderTicketDto d)
    {
        if (ClientSettlement.IsOnAccount(d.ClientSettlement))
        {
            if (d.ClientDebtSettledUsd >= d.AmountOnAccountUsd - 0.01m)
                return Loc.Admin("cltOnAccountSettled", "On account · settled");
            return Loc.Admin("cltOnAccountOpen", "On account · {{amount}} open",
                new Dictionary<string, string> { ["amount"] = $"${d.AmountOnAccountUsd:N2}" });
        }

        if (d.RevenueRecognized)
            return Loc.Admin("cltPaidRevenueRecognized", "Paid · revenue recognized");
        if (ClientSettlement.IsPaidAtCompletion(d.ClientSettlement) || d.ClientSettlement == ClientSettlement.None)
            return Loc.Admin("cltPaidAtCompletion", "Paid at completion");
        return string.IsNullOrWhiteSpace(d.ClientSettlement) ? "—" : d.ClientSettlement;
    }

    public static string TranslateLedgerNote(string? note)
    {
        var raw = (note ?? string.Empty).Trim();
        if (raw.Length == 0)
            return "—";

        var m = Regex.Match(raw, @"^Revenue recognized\s*[·\-]\s*(ORD-[A-F0-9]+)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("cltNoteRevenueRecognized", "Revenue recognized · {{orderId}}",
                new Dictionary<string, string> { ["orderId"] = m.Groups[1].Value });
        }

        m = Regex.Match(raw, @"^Order\s+(ORD-[A-F0-9]+)\s+on account$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return Loc.Admin("cltNoteOrderOnAccount", "Order {{orderId}} on account",
                new Dictionary<string, string> { ["orderId"] = m.Groups[1].Value });
        }

        if (string.Equals(raw, "Debt payment", StringComparison.OrdinalIgnoreCase))
            return Loc.Admin("cltNoteDebtPayment", "Debt payment");

        if (string.Equals(raw, "Debt payment · demo seed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "Debt payment - demo seed", StringComparison.OrdinalIgnoreCase))
            return Loc.Admin("cltNoteDebtPaymentDemo", "Debt payment · demo seed");

        return raw;
    }
}
