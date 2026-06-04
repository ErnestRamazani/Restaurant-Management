using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Tickets;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurant.Api.Orders;

/// <summary>Builds the same <see cref="TicketReceiptPdfModel"/> as Elite Pro admin orders ticket preview / PDF export.</summary>
public static class OrderTicketPdfBuilder
{
    public static bool UsePaymentReceiptVariant(OrderRecord order) =>
        string.Equals(order.Status, "Completed", StringComparison.OrdinalIgnoreCase);

    public static TicketReceiptPdfModel Build(OrderRecord order, AppSettings settings, byte[]? headerLogoBytes)
    {
        var business = settings.BusinessProfile;
        var ticketReceipt = settings.TicketReceipt ?? new TicketReceiptSettings();
        var pricing = settings.CurrencyPricing;

        var lines = order.Items.Select(item =>
        {
            var unitPrice = item.Product?.Price ?? 0m;
            return new TicketPdfLine(item.Quantity, item.Product?.Name ?? "Unknown", unitPrice, unitPrice * item.Quantity);
        }).ToList();

        var lineSum = lines.Sum(l => l.LineTotal);
        var totals = OrderTotalsHelper.ComputeTotalsWithDeliveryFee(
            lineSum,
            order.DiscountMode,
            order.DiscountValue,
            order.DeliveryFeeUsd);

        var guestInfo = OrderRecordUiLabels.TryGetOnlineGuestTicketInfo(order);
        DeliveryTicketInfo? deliveryInfo = null;
        if (guestInfo is not null)
        {
            deliveryInfo = new DeliveryTicketInfo(
                guestInfo.CustomerName,
                guestInfo.Phone,
                guestInfo.Address,
                guestInfo.Instructions);
        }

        var ticketOrderId = string.IsNullOrWhiteSpace(order.UniqueId) ? $"#{order.Id:000}" : order.UniqueId;
        var uid = string.IsNullOrWhiteSpace(order.UniqueId) ? string.Empty : order.UniqueId;
        var ticketEquivalentFcText = CurrencyHelper.FormatAmount(
            order.PaymentCurrencyCode == CurrencyHelper.CongoleseFranc && order.PaymentAmount > 0m
                ? order.PaymentAmount
                : CurrencyHelper.ConvertUsdToFc(totals.GrandTotal),
            CurrencyHelper.CongoleseFranc);
        var ticketPaymentText = order.PaymentAmount > 0m
            ? CurrencyHelper.FormatAmount(
                order.PaymentAmount,
                string.IsNullOrWhiteSpace(order.PaymentCurrencyCode) ? CurrencyHelper.Usd : order.PaymentCurrencyCode)
            : CurrencyHelper.FormatAmount(totals.GrandTotal, CurrencyHelper.Usd);

        var socialRows = new List<TicketSocialMediaPdfRow>();
        foreach (var row in ticketReceipt.SocialMediaRows)
        {
            var plat = (row.PlatformName ?? string.Empty).Trim();
            var user = (row.UserText ?? string.Empty).Trim();
            if (plat.Length == 0 && user.Length == 0)
                continue;
            var iconBytes = TicketReceiptPdfImageHelper.TryLoadRasterImage(row.IconPath);
            socialRows.Add(new TicketSocialMediaPdfRow(plat, user, iconBytes));
        }

        return new TicketReceiptPdfModel
        {
            Lines = lines,
            TicketOrderId = ticketOrderId,
            TicketConfirmationCode = (order.ConfirmationCode ?? string.Empty).Trim(),
            TicketStatus = order.Status,
            TicketTable = OrderRecordUiLabels.TableCaption(order),
            TicketLocationLine = OrderRecordUiLabels.TicketLocationLine(order),
            DeliveryInfo = deliveryInfo,
            TicketIsDeliveryFulfillment = OrderRecordUiLabels.IsDeliveryOrder(order),
            ShowServerOnTicket = OrderRecordUiLabels.ShowServerOnTicket(order),
            TicketServer = OrderRecordUiLabels.ServerCaption(order),
            TicketDateTime = RestaurantTimeZone.OrderCreatedAtForDisplay(
                order.CreatedAt,
                business.RestaurantTimeZoneId),
            TicketSubtotal = lineSum,
            TicketDiscountAmount = totals.DiscountApplied,
            TicketDiscountLineText = totals.DiscountApplied > 0m
                ? $"{OrderTotalsHelper.FormatDiscountLabel(order.DiscountMode, order.DiscountValue, totals.DiscountApplied)}: -$ {totals.DiscountApplied:N2}"
                : string.Empty,
            TicketTaxAmount = totals.Tax,
            TicketServiceAmount = totals.Service,
            TicketDeliveryFeeUsd = Math.Round(Math.Max(0m, order.DeliveryFeeUsd), 2),
            TicketGrandTotal = totals.GrandTotal,
            TicketEquivalentFcText = ticketEquivalentFcText,
            TicketPaymentText = ticketPaymentText,
            TicketPaidBreakdownText =
                $"Paid USD: {CurrencyHelper.FormatAmount(order.CustomerPaidUsd, CurrencyHelper.Usd)} | Paid FC: {CurrencyHelper.FormatAmount(order.CustomerPaidFc, CurrencyHelper.CongoleseFranc)}",
            TicketChangeBreakdownText =
                $"Change USD: {CurrencyHelper.FormatAmount(order.ChangeGivenUsd, CurrencyHelper.Usd)} | Change FC: {CurrencyHelper.FormatAmount(order.ChangeGivenFc, CurrencyHelper.CongoleseFranc)}",
            TicketVerification = $"ERP-DB-{order.Id}-{uid[..Math.Min(4, uid.Length)]}",
            TaxPercent = pricing.TaxPercent,
            ServicePercent = pricing.ServicePercent,
            HeaderLogoBytes = headerLogoBytes,
            RestaurantTitle = string.IsNullOrWhiteSpace(business.RestaurantName)
                ? "ELITE RESTAURANT PRO"
                : business.RestaurantName.ToUpperInvariant(),
            RestaurantPhone = (business.Phone ?? string.Empty).Trim(),
            FooterText = string.IsNullOrWhiteSpace(business.TicketFooterText) ? "MERCI / THANK YOU" : business.TicketFooterText.Trim(),
            ReceiptAddress = (business.Address ?? string.Empty).Trim(),
            ReceiptWebsiteLine = FormatReceiptWebsiteLine(business.WebsiteDomain),
            SocialFooterRows = socialRows,
            LegalInfo = business.TaxIdLegalInfo
        };
    }

    private static string FormatReceiptWebsiteLine(string? domain)
    {
        var d = (domain ?? "").Trim();
        if (string.IsNullOrEmpty(d))
            return string.Empty;
        if (d.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            d.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return d;
        return $"https://{d}";
    }
}
