using EliteRestaurant.Core.Reporting;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EliteRestaurantPro.Services;

public static class MoneyFinancialPdfExportService
{
    private const string RevenueType = "Revenue";
    private const string ExpenseType = "Expense";

    public static void ExportLedgerPdf(string filePath, DateTime fromDate, DateTime toDate, DateTime rangeEndExclusive)
    {
        var transactions = Task.Run(async () =>
        {
            var data = new AdminDataApiClient();
            var all = await data.GetMoneyTransactionsAsync().ConfigureAwait(false);
            return all
                .Where(t => t.Date >= fromDate && t.Date < rangeEndExclusive)
                .OrderBy(t => t.Date)
                .ThenBy(t => t.Id)
                .ToList();
        }).GetAwaiter().GetResult();

        var totalSales = transactions.Where(t => t.Type == RevenueType && t.Category == "Sale").ToList();
        var tipsCollected = transactions.Where(t => t.Type == RevenueType && t.Category == "Tip").ToList();
        var payrollDeductions = transactions.Where(t => t.Type == ExpenseType && t.Category == "Salary").ToList();
        var totalRevenue = transactions.Where(t => t.Type == RevenueType).ToList();
        var totalExpenses = transactions.Where(t => t.Type == ExpenseType).ToList();
        var totalSalesText = CurrencyHelper.FormatDualCurrency(
            MoneyReportingHelpers.SumByCurrency(totalSales, CurrencyHelper.Usd),
            MoneyReportingHelpers.SumByCurrency(totalSales, CurrencyHelper.CongoleseFranc));
        var tipsCollectedText = CurrencyHelper.FormatDualCurrency(
            MoneyReportingHelpers.SumByCurrency(tipsCollected, CurrencyHelper.Usd),
            MoneyReportingHelpers.SumByCurrency(tipsCollected, CurrencyHelper.CongoleseFranc));
        var payrollDeductionsText = CurrencyHelper.FormatDualCurrency(
            MoneyReportingHelpers.SumByCurrency(payrollDeductions, CurrencyHelper.Usd),
            MoneyReportingHelpers.SumByCurrency(payrollDeductions, CurrencyHelper.CongoleseFranc));
        var netUsd = MoneyReportingHelpers.SumByCurrency(totalRevenue, CurrencyHelper.Usd) -
                     MoneyReportingHelpers.SumByCurrency(totalExpenses, CurrencyHelper.Usd);
        var netFc = MoneyReportingHelpers.SumByCurrency(totalRevenue, CurrencyHelper.CongoleseFranc) -
                    MoneyReportingHelpers.SumByCurrency(totalExpenses, CurrencyHelper.CongoleseFranc);
        var finalNetBalanceText = CurrencyHelper.FormatDualCurrency(netUsd, netFc);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(26);
                page.PageColor("#111427");
                page.DefaultTextStyle(style => style.FontColor("#F3E8C5").FontSize(10));

                page.Header().Column(column =>
                {
                    column.Item().Text("EliteRestaurantPro - MoneyView Financial Report")
                        .FontSize(18)
                        .Bold()
                        .FontColor("#D4AF37");
                    column.Item().Text($"{fromDate:dd MMM yyyy} to {toDate:dd MMM yyyy}")
                        .FontColor("#CFC39A");
                    column.Item().PaddingTop(4).LineHorizontal(1).LineColor("#6E5930");
                });

                page.Content().Column(column =>
                {
                    column.Spacing(12);

                    column.Item().Text("Financial Summary").Bold().FontSize(13).FontColor("#D4AF37");
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                        });

                        table.Cell().PaddingVertical(4).Text("Total Sales");
                        table.Cell().AlignRight().PaddingVertical(4).Text(totalSalesText).FontColor("#2ECC71").Bold();

                        table.Cell().PaddingVertical(4).Text("Tips Collected");
                        table.Cell().AlignRight().PaddingVertical(4).Text(tipsCollectedText).FontColor("#2ECC71").Bold();

                        table.Cell().PaddingVertical(4).Text("Payroll Deductions");
                        table.Cell().AlignRight().PaddingVertical(4).Text(payrollDeductionsText).FontColor("#DC143C").Bold();

                        table.Cell().PaddingVertical(6).Text("Final Net Balance").Bold();
                        table.Cell().AlignRight().PaddingVertical(6).Text(finalNetBalanceText)
                            .FontColor(netUsd >= 0m && netFc >= 0m ? "#2ECC71" : "#DC143C")
                            .Bold()
                            .FontSize(12);
                    });

                    column.Item().LineHorizontal(1).LineColor("#6E5930");
                    column.Item().Text("Detailed Ledger").Bold().FontSize(13).FontColor("#D4AF37");

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(80);
                            columns.ConstantColumn(70);
                            columns.ConstantColumn(90);
                            columns.RelativeColumn(2.3f);
                            columns.ConstantColumn(90);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Date").Bold();
                            header.Cell().Text("Type").Bold();
                            header.Cell().Text("Category").Bold();
                            header.Cell().Text("Justification").Bold();
                            header.Cell().AlignRight().Text("Amount").Bold();
                        });

                        foreach (var transaction in transactions)
                        {
                            var isRevenue = transaction.Type == RevenueType;
                            table.Cell().PaddingVertical(3).Text(transaction.Date.ToString("dd/MM/yyyy HH:mm"));
                            table.Cell().PaddingVertical(3).Text(transaction.Type);
                            table.Cell().PaddingVertical(3).Text(transaction.Category);
                            table.Cell().PaddingVertical(3).Text(string.IsNullOrWhiteSpace(transaction.Justification) ? "-" : transaction.Justification);
                            table.Cell().PaddingVertical(3).AlignRight()
                                .Text(MoneyReportingHelpers.FormatLedgerAmount(
                                    transaction.Amount,
                                    transaction.AmountUsd,
                                    transaction.AmountFc,
                                    transaction.CurrencyCode,
                                    isRevenue))
                                .FontColor(isRevenue ? "#2ECC71" : "#DC143C");
                        }
                    });
                });

                page.Footer().AlignCenter().Text($"EliteRestaurantPro MoneyView  |  Generated {DateTime.Now:dd MMM yyyy HH:mm}")
                    .FontColor("#A99867");
            });
        }).GeneratePdf(filePath);
    }
}
