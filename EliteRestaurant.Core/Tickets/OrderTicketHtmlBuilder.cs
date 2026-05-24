using System.Globalization;
using System.Net;
using System.Text;
using EliteRestaurant.Core.Orders;

namespace EliteRestaurant.Core.Tickets;

/// <summary>Browser-printable thermal receipt HTML (same data as PDF tickets).</summary>
public static class OrderTicketHtmlBuilder
{
    private static string Enc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string Money(decimal amount) =>
        Enc("$ " + amount.ToString("0.00", CultureInfo.InvariantCulture));

    public static string BuildPaymentReceiptHtml(TicketReceiptPdfModel m) =>
        Build(m, includePaymentSection: true);

    public static string BuildClientTicketHtml(TicketReceiptPdfModel m) =>
        Build(m, includePaymentSection: false);

    private static string Build(TicketReceiptPdfModel m, bool includePaymentSection)
    {
        var sb = new StringBuilder(4096);
        sb.Append("""
            <!DOCTYPE html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <title></title>
            <style>
            @page { margin: 0; }
            html {
              width: 72mm;
              margin: 0 auto;
              padding: 0;
              background: #fff;
            }
            body {
              font-family: Arial, Helvetica, sans-serif;
              font-weight: 600;
              font-size: 11px;
              line-height: 1.25;
              width: 72mm;
              margin: 0;
              padding: 4mm 3mm 0;
              box-sizing: border-box;
              background: #fff;
              color: #000;
              -webkit-print-color-adjust: exact;
              print-color-adjust: exact;
            }
            @media print {
              html, body {
                width: 72mm !important;
                margin: 0 !important;
                padding: 0;
                height: auto !important;
                overflow: visible;
                background: #fff;
              }
              body { padding: 4mm 3mm 0 !important; }
            }
            .center { text-align: center; }
            .title { font-size: 14px; font-weight: 800; margin: 0 0 4px; }
            .code { font-size: 20px; font-weight: 800; letter-spacing: 0.08em; margin: 6px 0; }
            .rule { border-top: 1px dashed #000; margin: 8px 0; }
            table { width: 100%; border-collapse: collapse; }
            th { font-weight: 800; text-align: left; border-bottom: 1px solid #000; padding-bottom: 3px; }
            td { vertical-align: top; padding: 2px 0; }
            .qty { width: 12%; }
            .money { text-align: right; white-space: nowrap; }
            .totals td:first-child { padding-right: 8px; }
            .grand { font-size: 13px; font-weight: 800; text-align: right; margin-top: 6px; }
            .muted { font-size: 10px; }
            .social { margin: 3px 0; line-height: 1.3; }
            .social-icon { width: 14px; height: 14px; vertical-align: middle; margin-right: 4px; }
            </style>
            </head>
            <body>
            """);

        if (m.HeaderLogoBytes is { Length: > 0 })
        {
            var b64 = Convert.ToBase64String(m.HeaderLogoBytes);
            sb.Append("<div class=\"center\"><img alt=\"\" style=\"max-height:56px;max-width:100%\" src=\"data:image/png;base64,");
            sb.Append(b64);
            sb.Append("\"/></div>");
        }

        sb.Append("<div class=\"center title\">").Append(Enc(m.RestaurantTitle)).Append("</div>");
        if (!string.IsNullOrWhiteSpace(m.RestaurantPhone))
            sb.Append("<div class=\"center\">Phone: ").Append(Enc(m.RestaurantPhone.Trim())).Append("</div>");

        if (!string.IsNullOrWhiteSpace(m.TicketConfirmationCode))
        {
            sb.Append("<div class=\"center code\">").Append(Enc(m.TicketConfirmationCode.Trim())).Append("</div>");
            sb.Append("<div class=\"center muted\">CONFIRMATION CODE</div>");
        }

        sb.Append("<div class=\"rule\"></div>");
        sb.Append("<div class=\"center\">Date: ").Append(Enc(m.TicketDateTime.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)));
        sb.Append(" &nbsp; Time: ").Append(Enc(m.TicketDateTime.ToString("HH:mm", CultureInfo.InvariantCulture))).Append("</div>");
        sb.Append("<div class=\"center\">Order: ").Append(Enc(m.TicketOrderId)).Append("</div>");

        if (!string.IsNullOrWhiteSpace(m.TicketLocationLine))
            sb.Append("<div class=\"center\">").Append(Enc(m.TicketLocationLine.Trim())).Append("</div>");

        AppendDelivery(sb, m.DeliveryInfo, m.TicketIsDeliveryFulfillment);

        if (m.ShowServerOnTicket && !string.IsNullOrWhiteSpace(m.TicketServer))
            sb.Append("<div class=\"center\">Server: ").Append(Enc(m.TicketServer.Trim())).Append("</div>");

        if (includePaymentSection && !string.IsNullOrWhiteSpace(m.TicketStatus))
            sb.Append("<div class=\"center\">Status: ").Append(Enc(m.TicketStatus)).Append("</div>");

        sb.Append("<div class=\"rule\"></div>");
        sb.Append("""
            <table>
            <thead><tr>
            <th class="qty">QTY</th>
            <th>ITEM</th>
            <th class="money">P.U</th>
            <th class="money">TOTAL</th>
            </tr></thead>
            <tbody>
            """);

        foreach (var line in m.Lines)
        {
            sb.Append("<tr><td class=\"qty\">").Append(line.Quantity.ToString(CultureInfo.InvariantCulture));
            sb.Append("</td><td>").Append(Enc(line.Name));
            sb.Append("</td><td class=\"money\">").Append(Money(line.UnitPrice));
            sb.Append("</td><td class=\"money\">").Append(Money(line.LineTotal)).Append("</td></tr>");
        }

        sb.Append("</tbody></table><div class=\"rule\"></div>");
        sb.Append("<table class=\"totals\">");
        AppendTotalRow(sb, "Subtotal:", m.TicketSubtotal);
        if (m.TicketDiscountAmount > 0m)
            AppendTotalRow(sb, "Discount:", -m.TicketDiscountAmount, negative: true);
        else
            AppendTotalRow(sb, "Discount:", 0m);
        AppendTotalRow(sb, $"Service ({m.ServicePercent:0.##}%):", m.TicketServiceAmount);
        AppendTotalRow(sb, $"TVA ({m.TaxPercent:0.##}%):", m.TicketTaxAmount);
        if (m.TicketDeliveryFeeUsd > 0m)
            AppendTotalRow(sb, "Delivery:", m.TicketDeliveryFeeUsd);
        sb.Append("</table>");

        sb.Append("<div class=\"grand\">GRAND TOTAL USD<br/>").Append(Money(m.TicketGrandTotal)).Append("</div>");
        sb.Append("<div class=\"money\">Equivalent FC: ").Append(Enc(m.TicketEquivalentFcText)).Append("</div>");

        if (includePaymentSection)
        {
            sb.Append("<div class=\"money\">Collected: ").Append(Enc(m.TicketPaymentText)).Append("</div>");
            sb.Append("<div class=\"muted money\">").Append(Enc(m.TicketPaidBreakdownText)).Append("</div>");
            sb.Append("<div class=\"muted money\">").Append(Enc(m.TicketChangeBreakdownText)).Append("</div>");
        }

        var thankYou = string.IsNullOrWhiteSpace(m.FooterText) ? "MERCI / THANK YOU" : m.FooterText.Trim();
        sb.Append("<div class=\"center\" style=\"margin-top:14px;font-weight:800\">").Append(Enc(thankYou)).Append("</div>");

        foreach (var part in (m.ReceiptAddress ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            sb.Append("<div class=\"center muted\">").Append(Enc(part)).Append("</div>");

        if (!string.IsNullOrWhiteSpace(m.ReceiptWebsiteLine))
            sb.Append("<div class=\"center muted\">").Append(Enc(m.ReceiptWebsiteLine)).Append("</div>");

        foreach (var social in m.SocialFooterRows)
            AppendSocialFooterRow(sb, social);

        if (!string.IsNullOrWhiteSpace(m.LegalInfo))
        {
            foreach (var part in m.LegalInfo.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                sb.Append("<div class=\"center muted\">").Append(Enc(part)).Append("</div>");
        }

        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static void AppendDelivery(StringBuilder sb, DeliveryTicketInfo? guest, bool isDelivery)
    {
        if (guest is null)
            return;

        sb.Append("<div class=\"center\" style=\"margin-top:6px\">").Append(isDelivery ? "— DELIVERY —" : "— PICKUP —").Append("</div>");
        if (!string.IsNullOrWhiteSpace(guest.CustomerName))
            sb.Append("<div class=\"center\">Customer: ").Append(Enc(guest.CustomerName.Trim())).Append("</div>");

        var phone = (guest.Phone ?? string.Empty).Trim();
        sb.Append("<div class=\"center\">Phone: ").Append(Enc(string.IsNullOrWhiteSpace(phone) ? "(not provided)" : phone)).Append("</div>");

        if (isDelivery && !string.IsNullOrWhiteSpace(guest.Address))
            sb.Append("<div class=\"center\">Address: ").Append(Enc(guest.Address.Trim())).Append("</div>");

        if (!string.IsNullOrWhiteSpace(guest.Instructions))
            sb.Append("<div class=\"center\">Notes: ").Append(Enc(guest.Instructions.Trim())).Append("</div>");
    }

    private static void AppendTotalRow(StringBuilder sb, string label, decimal amount, bool negative = false)
    {
        sb.Append("<tr><td>").Append(Enc(label)).Append("</td><td class=\"money\">");
        if (negative && amount > 0m)
            sb.Append(Enc($"-$ {amount:N2}"));
        else
            sb.Append(Money(amount));
        sb.Append("</td></tr>");
    }

    private static void AppendSocialFooterRow(StringBuilder sb, TicketSocialMediaPdfRow social)
    {
        var caption = FormatSocialCaption(social.PlatformName, social.UserText);
        if (string.IsNullOrWhiteSpace(caption))
            return;

        sb.Append("<div class=\"center muted social\">");
        if (social.IconBytes is { Length: > 0 })
        {
            var mime = TicketReceiptPdfImageHelper.GetImageContentType(social.IconBytes);
            sb.Append("<img class=\"social-icon\" alt=\"\" src=\"data:");
            sb.Append(mime);
            sb.Append(";base64,");
            sb.Append(Convert.ToBase64String(social.IconBytes));
            sb.Append("\"/>");
        }

        sb.Append(Enc(caption));
        sb.Append("</div>");
    }

    private static string FormatSocialCaption(string platformName, string userText)
    {
        var p = (platformName ?? string.Empty).Trim();
        var u = (userText ?? string.Empty).Trim();
        if (p.Length == 0)
            return u;
        if (u.Length == 0)
            return p;
        return $"{p}: {u}";
    }
}
