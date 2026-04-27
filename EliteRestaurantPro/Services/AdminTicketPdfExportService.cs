using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EliteRestaurantPro.Services;

public static class AdminTicketPdfExportService
{
    public static void ExportPaymentReceiptPdf(string filePath, TicketReceiptPdfModel m)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A6);
                page.Margin(20);
                page.PageColor("#151515");
                page.DefaultTextStyle(x => x.FontSize(10).FontColor("#F0E6C8"));

                page.Content().Column(column =>
                {
                    column.Spacing(4);
                    column.Item().Text(m.RestaurantTitle).Bold().FontSize(16).FontColor("#D4AF37");
                    column.Item().LineHorizontal(1).LineColor("#7A6231");
                    column.Item().Text($"Date: {m.TicketDateTime:dd MMM yyyy}    Time: {m.TicketDateTime:HH:mm}");
                    column.Item().Text($"Order: {m.TicketOrderId}    Status: {m.TicketStatus}");
                    column.Item().Text($"Table: {m.TicketTable}");
                    column.Item().Text($"Server: {m.TicketServer}");
                    column.Item().LineHorizontal(1).LineColor("#7A6231");

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(24);
                            cols.RelativeColumn(2.8f);
                            cols.RelativeColumn(1.3f);
                            cols.RelativeColumn(1.3f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("QTY").Bold();
                            header.Cell().Text("ITEM").Bold();
                            header.Cell().AlignRight().Text("P.U").Bold();
                            header.Cell().AlignRight().Text("TOTAL").Bold();
                        });

                        foreach (var line in m.Lines)
                        {
                            table.Cell().Text(line.Quantity.ToString());
                            table.Cell().Text(line.Name);
                            table.Cell().AlignRight().Text($"$ {line.UnitPrice:N2}");
                            table.Cell().AlignRight().Text($"$ {line.LineTotal:N2}");
                        }
                    });

                    column.Item().LineHorizontal(1).LineColor("#7A6231");
                    column.Item().AlignRight().Text($"Subtotal: $ {m.TicketSubtotal:N2}").SemiBold();
                    if (m.TicketDiscountAmount > 0m)
                    {
                        column.Item().AlignRight().Text(m.TicketDiscountLineText).FontColor("#E57373");
                        column.Item().AlignRight()
                            .Text($"After discount: $ {m.TicketSubtotal - m.TicketDiscountAmount:N2}")
                            .FontSize(9)
                            .FontColor("#C1B28A");
                    }

                    column.Item().AlignRight().Text($"TVA ({m.TaxPercent:0.##}%): $ {m.TicketTaxAmount:N2}");
                    column.Item().AlignRight().Text($"Service ({m.ServicePercent:0.##}%): $ {m.TicketServiceAmount:N2}");
                    column.Item().AlignRight().Text($"GRAND TOTAL USD: $ {m.TicketGrandTotal:N2}").Bold().FontSize(14).FontColor("#D4AF37");
                    column.Item().AlignRight().Text($"Equivalent FC: {m.TicketEquivalentFcText}");
                    column.Item().AlignRight().Text($"Collected: {m.TicketPaymentText}");
                    column.Item().AlignRight().Text(m.TicketPaidBreakdownText).FontSize(9);
                    column.Item().AlignRight().Text(m.TicketChangeBreakdownText).FontSize(9);
                    column.Item().LineHorizontal(1).LineColor("#7A6231");
                    if (!string.IsNullOrWhiteSpace(m.LegalInfo))
                        column.Item().Text(m.LegalInfo).FontSize(9).FontColor("#C1B28A");
                    column.Item().Text($"Database Verification: {m.TicketVerification}").FontSize(9).FontColor("#C1B28A");
                    column.Item().Text(m.FooterText).Bold().FontColor("#D4AF37");
                });
            });
        }).GeneratePdf(filePath);
    }

    public static void ExportClientTicketPdf(string filePath, TicketReceiptPdfModel m)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A6);
                page.Margin(20);
                page.PageColor("#151515");
                page.DefaultTextStyle(x => x.FontSize(10).FontColor("#F0E6C8"));

                page.Content().Column(column =>
                {
                    column.Spacing(4);
                    column.Item().Text(m.RestaurantTitle).Bold().FontSize(16).FontColor("#D4AF37");
                    column.Item().LineHorizontal(1).LineColor("#7A6231");
                    column.Item().Text($"Date: {m.TicketDateTime:dd MMM yyyy}    Time: {m.TicketDateTime:HH:mm}");
                    column.Item().Text($"Order: {m.TicketOrderId}");
                    column.Item().Text($"Table: {m.TicketTable}");
                    column.Item().Text($"Server: {m.TicketServer}");
                    column.Item().LineHorizontal(1).LineColor("#7A6231");

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(24);
                            cols.RelativeColumn(2.8f);
                            cols.RelativeColumn(1.3f);
                            cols.RelativeColumn(1.3f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("QTY").Bold();
                            header.Cell().Text("ITEM").Bold();
                            header.Cell().AlignRight().Text("P.U").Bold();
                            header.Cell().AlignRight().Text("TOTAL").Bold();
                        });

                        foreach (var line in m.Lines)
                        {
                            table.Cell().Text(line.Quantity.ToString());
                            table.Cell().Text(line.Name);
                            table.Cell().AlignRight().Text($"$ {line.UnitPrice:N2}");
                            table.Cell().AlignRight().Text($"$ {line.LineTotal:N2}");
                        }
                    });

                    column.Item().LineHorizontal(1).LineColor("#7A6231");
                    column.Item().AlignRight().Text($"Subtotal: $ {m.TicketSubtotal:N2}").SemiBold();
                    if (m.TicketDiscountAmount > 0m)
                    {
                        column.Item().AlignRight().Text(m.TicketDiscountLineText).FontColor("#E57373");
                        column.Item().AlignRight()
                            .Text($"After discount: $ {m.TicketSubtotal - m.TicketDiscountAmount:N2}")
                            .FontSize(9)
                            .FontColor("#C1B28A");
                    }

                    column.Item().AlignRight().Text($"TVA ({m.TaxPercent:0.##}%): $ {m.TicketTaxAmount:N2}");
                    column.Item().AlignRight().Text($"Service ({m.ServicePercent:0.##}%): $ {m.TicketServiceAmount:N2}");
                    column.Item().AlignRight().Text($"GRAND TOTAL USD: $ {m.TicketGrandTotal:N2}").Bold().FontSize(14).FontColor("#D4AF37");
                    column.Item().AlignRight().Text($"Equivalent FC: {m.TicketEquivalentFcText}");
                    column.Item().LineHorizontal(1).LineColor("#7A6231");
                    if (!string.IsNullOrWhiteSpace(m.LegalInfo))
                        column.Item().Text(m.LegalInfo).FontSize(9).FontColor("#C1B28A");
                    column.Item().Text(m.FooterText).Bold().FontColor("#D4AF37");
                });
            });
        }).GeneratePdf(filePath);
    }

    public static string SanitizeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "order-ticket";

        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(input.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "order-ticket" : sanitized;
    }
}
