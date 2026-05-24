using System.Globalization;
using EliteRestaurant.Core.Orders;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EliteRestaurant.Core.Tickets;

/// <summary>Order ticket PDFs: continuous thermal-width receipt, black on white (POS-style).</summary>
public static class AdminTicketPdfExportService
{
    private const float FontBody = 8.5f;
    private const float FontSmall = 7.5f;
    private const float FontTitle = 12f;
    private const float FontConfirmationCode = 22f;
    private const float FontGrandLabel = 10f;
    private const float FontGrandAmount = 15f;
    private const float FontThankYou = 10f;
    private const float ColQtyPt = 16f;
    private const float ColUnitPricePt = 44f;
    private const float ColLineTotalPt = 50f;
    private const string ReceiptFont = "Arial";
    private static readonly string ReceiptBlack = Colors.Black;

    private static float MmToPt(double mm) => (float)(mm * (72.0 / 25.4));

    private static TextStyle ReceiptTextStyle(float size, bool bold = false)
    {
        var style = TextStyle.Default.FontFamily(ReceiptFont).FontSize(size).FontColor(ReceiptBlack);
        return bold ? style.Bold() : style.SemiBold();
    }

    private static string FormatReceiptUsd(decimal amount) =>
        "$ " + amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static TextStyle ReceiptBaseStyle() => ReceiptTextStyle(FontBody);

    private static void ReceiptDashRule(ColumnDescriptor column) =>
        column.Item()
            .PaddingVertical(3)
            .AlignCenter()
            .Text("------------------------------------------------")
            .Style(ReceiptTextStyle(FontSmall));

    private static void ReceiptTotalRow(ColumnDescriptor column, string label, string amountText, bool muted = false)
    {
        column.Item().Row(row =>
        {
            row.RelativeItem().Text(label).Style(ReceiptTextStyle(FontBody));
            row.ConstantItem(ColLineTotalPt + 8).AlignRight().Text(amountText).Style(ReceiptTextStyle(FontBody));
        });
    }

    private static void ReceiptMoneyCell(IContainer cell, string amountText) =>
        cell.AlignTop().AlignRight().PaddingVertical(1)
            .Text(amountText)
            .Style(ReceiptTextStyle(FontBody));

    private static void CenterMutedLine(ColumnDescriptor column, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        column.Item().AlignCenter().Text(text).Style(ReceiptTextStyle(FontSmall)).LineHeight(1.15f);
    }

    private static void CenterBodyLine(ColumnDescriptor column, string text)
    {
        column.Item().AlignCenter().Text(text).Style(ReceiptTextStyle(FontBody));
    }

    private static void CenteredInlineRow(ColumnDescriptor column, Action<RowDescriptor> composeInlineRow)
    {
        column.Item().ExtendHorizontal().Row(outer =>
        {
            outer.RelativeItem();
            outer.AutoItem().Row(composeInlineRow);
            outer.RelativeItem();
        });
    }

    private static void ComposeDeliveryDetails(ColumnDescriptor column, DeliveryTicketInfo? guest, bool isDelivery)
    {
        if (guest is null)
            return;

        column.Item().PaddingTop(4);
        CenterBodyLine(column, isDelivery ? "— DELIVERY —" : "— PICKUP —");

        if (!string.IsNullOrWhiteSpace(guest.CustomerName))
            CenterBodyLine(column, $"Customer: {guest.CustomerName.Trim()}");

        var phone = (guest.Phone ?? string.Empty).Trim();
        CenterBodyLine(column, string.IsNullOrWhiteSpace(phone) ? "Phone: (not provided)" : $"Phone: {phone}");

        if (isDelivery && !string.IsNullOrWhiteSpace(guest.Address))
            CenterBodyLine(column, $"Address: {guest.Address.Trim()}");

        if (!string.IsNullOrWhiteSpace(guest.Instructions))
            CenterBodyLine(column, $"Notes: {guest.Instructions.Trim()}");
    }

    private static void CenterHeaderLogo(ColumnDescriptor column, byte[] logoBytes, float maxHeightPt = 56)
    {
        column.Item()
            .ExtendHorizontal()
            .AlignCenter()
            .Height(maxHeightPt)
            .Image(logoBytes)
            .FitHeight();
    }

    private static void FooterIconCaptionRow(ColumnDescriptor column, byte[]? iconPng, string caption)
    {
        if (string.IsNullOrWhiteSpace(caption))
            return;

        CenteredInlineRow(column, row =>
        {
            row.Spacing(4);
            if (iconPng is not null)
            {
                row.ConstantItem(14).Height(14).AlignMiddle()
                    .Image(iconPng)
                    .FitArea()
                    .WithRasterDpi(120);
            }

            row.AutoItem().AlignMiddle()
                .Text(caption)
                .Style(ReceiptTextStyle(FontSmall));
        });
    }

    private static string FormatSocialFooterCaption(string platformName, string userText)
    {
        var p = (platformName ?? string.Empty).Trim();
        var u = (userText ?? string.Empty).Trim();
        if (p.Length == 0)
            return u;
        if (u.Length == 0)
            return p;
        return $"{p}: {u}";
    }

    private static void ComposeReceiptBody(ColumnDescriptor column, TicketReceiptPdfModel m, bool includePaymentSection)
    {
        column.Spacing(2);

        if (m.HeaderLogoBytes is { Length: > 0 })
        {
            CenterHeaderLogo(column, m.HeaderLogoBytes);
            column.Item().PaddingTop(2);
        }

        column.Item().AlignCenter()
            .Text(m.RestaurantTitle)
            .Style(ReceiptTextStyle(FontTitle, bold: true));

        if (!string.IsNullOrWhiteSpace(m.RestaurantPhone))
            CenterBodyLine(column, $"Phone: {m.RestaurantPhone.Trim()}");

        if (!string.IsNullOrWhiteSpace(m.TicketConfirmationCode))
        {
            column.Item().PaddingTop(4).AlignCenter()
                .Text(m.TicketConfirmationCode.Trim())
                .Style(ReceiptTextStyle(FontConfirmationCode, bold: true))
                .LetterSpacing(0.08f);
            CenterBodyLine(column, "CONFIRMATION CODE");
        }

        ReceiptDashRule(column);

        CenterBodyLine(column, $"Date: {m.TicketDateTime:dd MMM yyyy}    Time: {m.TicketDateTime:HH:mm}");
        CenterBodyLine(column, $"Order: {m.TicketOrderId}");
        if (!string.IsNullOrWhiteSpace(m.TicketLocationLine))
            CenterBodyLine(column, m.TicketLocationLine.Trim());
        ComposeDeliveryDetails(column, m.DeliveryInfo, m.TicketIsDeliveryFulfillment);
        if (m.ShowServerOnTicket && !string.IsNullOrWhiteSpace(m.TicketServer))
            CenterBodyLine(column, $"Server: {m.TicketServer.Trim()}");
        if (includePaymentSection && !string.IsNullOrWhiteSpace(m.TicketStatus))
            CenterBodyLine(column, $"Status: {m.TicketStatus}");

        ReceiptDashRule(column);

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(ColQtyPt);
                cols.RelativeColumn();
                cols.ConstantColumn(ColUnitPricePt);
                cols.ConstantColumn(ColLineTotalPt);
            });

            table.Header(header =>
            {
                header.Cell().Text("QTY").Style(ReceiptTextStyle(FontBody, bold: true));
                header.Cell().Text("ITEM").Style(ReceiptTextStyle(FontBody, bold: true));
                header.Cell().AlignTop().AlignRight().Text("P.U").Style(ReceiptTextStyle(FontBody, bold: true));
                header.Cell().AlignTop().AlignRight().Text("TOTAL").Style(ReceiptTextStyle(FontBody, bold: true));
            });

            foreach (var line in m.Lines)
            {
                table.Cell().AlignTop().PaddingVertical(1)
                    .Text(line.Quantity.ToString(CultureInfo.InvariantCulture))
                    .Style(ReceiptTextStyle(FontBody));
                table.Cell().AlignTop().PaddingVertical(1)
                    .Text(line.Name)
                    .Style(ReceiptTextStyle(FontBody))
                    .LineHeight(1.05f);
                table.Cell().Element(c => ReceiptMoneyCell(c, FormatReceiptUsd(line.UnitPrice)));
                table.Cell().Element(c => ReceiptMoneyCell(c, FormatReceiptUsd(line.LineTotal)));
            }
        });

        ReceiptDashRule(column);

        ReceiptTotalRow(column, "Subtotal:", FormatReceiptUsd(m.TicketSubtotal));
        if (m.TicketDiscountAmount > 0m && !string.IsNullOrWhiteSpace(m.TicketDiscountLineText))
            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Discount:").Style(ReceiptTextStyle(FontBody));
                row.ConstantItem(86).AlignRight()
                    .Text($"-$ {m.TicketDiscountAmount:N2}")
                    .Style(ReceiptTextStyle(FontBody));
            });
        else
            ReceiptTotalRow(column, "Discount:", FormatReceiptUsd(0m), muted: true);

        ReceiptTotalRow(column, $"Service ({m.ServicePercent:0.##}%):", FormatReceiptUsd(m.TicketServiceAmount));
        ReceiptTotalRow(column, $"TVA ({m.TaxPercent:0.##}%):", FormatReceiptUsd(m.TicketTaxAmount));
        if (m.TicketDeliveryFeeUsd > 0m)
            ReceiptTotalRow(column, "Delivery:", FormatReceiptUsd(m.TicketDeliveryFeeUsd));

        column.Item().PaddingTop(4).AlignRight()
            .Text("GRAND TOTAL USD")
            .Style(ReceiptTextStyle(FontGrandLabel, bold: true));
        column.Item().AlignRight()
            .Text(FormatReceiptUsd(m.TicketGrandTotal))
            .Style(ReceiptTextStyle(FontGrandAmount, bold: true));

        if (includePaymentSection)
        {
            column.Item().AlignRight().PaddingTop(2)
                .Text($"Equivalent FC: {m.TicketEquivalentFcText}")
                .Style(ReceiptTextStyle(FontBody));
            column.Item().AlignRight()
                .Text(m.TicketPaidBreakdownText)
                .Style(ReceiptTextStyle(FontBody));
            column.Item().AlignRight()
                .Text(m.TicketChangeBreakdownText)
                .Style(ReceiptTextStyle(FontBody));
        }

        column.Item().PaddingTop(16);

        var thankYou = string.IsNullOrWhiteSpace(m.FooterText) ? "MERCI / THANK YOU" : m.FooterText.Trim();
        column.Item().AlignCenter()
            .Text(thankYou)
            .Style(ReceiptTextStyle(FontThankYou, bold: true));

        column.Item().PaddingTop(12);

        foreach (var part in (m.ReceiptAddress ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            CenterMutedLine(column, part);

        CenterMutedLine(column, m.ReceiptWebsiteLine);

        foreach (var social in m.SocialFooterRows)
        {
            var caption = FormatSocialFooterCaption(social.PlatformName, social.UserText);
            if (string.IsNullOrWhiteSpace(caption))
                continue;
            FooterIconCaptionRow(column, social.IconBytes, caption);
        }

        if (!string.IsNullOrWhiteSpace(m.LegalInfo))
        {
            column.Item().PaddingTop(8);
            foreach (var part in m.LegalInfo.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                CenterMutedLine(column, part);
        }
    }

    private static void ConfigureReceiptPage(PageDescriptor page)
    {
        page.ContinuousSize(MmToPt(76));
        page.MarginHorizontal(MmToPt(3));
        page.MarginTop(MmToPt(4));
        page.MarginBottom(0);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(ReceiptBaseStyle());
    }

    public static byte[] GeneratePaymentReceiptPdfBytes(TicketReceiptPdfModel m) =>
        Document.Create(container =>
        {
            container.Page(page =>
            {
                ConfigureReceiptPage(page);
                page.Content()
                    .ShrinkVertical()
                    .Column(c => ComposeReceiptBody(c, m, includePaymentSection: true));
            });
        }).GeneratePdf();

    public static byte[] GenerateClientTicketPdfBytes(TicketReceiptPdfModel m) =>
        Document.Create(container =>
        {
            container.Page(page =>
            {
                ConfigureReceiptPage(page);
                page.Content()
                    .ShrinkVertical()
                    .Column(c => ComposeReceiptBody(c, m, includePaymentSection: false));
            });
        }).GeneratePdf();

    public static void ExportPaymentReceiptPdf(string filePath, TicketReceiptPdfModel m) =>
        File.WriteAllBytes(filePath, GeneratePaymentReceiptPdfBytes(m));

    public static void ExportClientTicketPdf(string filePath, TicketReceiptPdfModel m) =>
        File.WriteAllBytes(filePath, GenerateClientTicketPdfBytes(m));

    public static string SanitizeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "order-ticket";

        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(input.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "order-ticket" : sanitized;
    }
}
