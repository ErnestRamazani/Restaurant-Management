using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EliteRestaurantPro.Services;

/// <summary>Printable PDF with one table QR per page (QuestPDF + PNG QR bytes).</summary>
public static class MenuQrPdfExportService
{
    public static void Save(string filePath, IReadOnlyList<MenuQrPdfPage> pages)
    {
        Document.Create(container =>
        {
            foreach (var p in pages)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(t => t.FontSize(12));
                    page.Content().Column(c =>
                    {
                        c.Spacing(16);
                        c.Item().AlignCenter().Text("Customer menu — scan to order").FontSize(14).SemiBold();
                        c.Item().AlignCenter().Text(p.TableTitle).FontSize(20).Bold();
                        c.Item().AlignCenter().Text(p.Url).FontSize(9).FontColor(Colors.Grey.Medium);
                        c.Item().AlignCenter()
                            .Width(220)
                            .Height(220)
                            .Image(p.PngBytes)
                            .WithRasterDpi(200);
                    });
                });
            }
        }).GeneratePdf(filePath);
    }
}

public sealed record MenuQrPdfPage(string TableTitle, string Url, byte[] PngBytes);
