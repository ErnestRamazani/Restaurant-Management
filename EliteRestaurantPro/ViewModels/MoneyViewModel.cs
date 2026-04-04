using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using EliteRestaurantPro.Data;
using EliteRestaurantPro.Models;
using EliteRestaurantPro.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EliteRestaurantPro.ViewModels;

public sealed class MoneyLedgerItemViewModel
{
    public DateTime Date { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Justification { get; init; } = string.Empty;
    public string AmountText { get; init; } = "$ 0.00";
    public string AmountColor { get; init; } = "#2ECC71";
}

public class MoneyViewModel : AdminBaseViewModel
{
    private const string RevenueType = "Revenue";
    private const string ExpenseType = "Expense";
    private const int MaxLedgerRows = 200;

    private static readonly HashSet<string> FixedCategories =
    [
        "Sale",
        "Salary",
        "Fixed Cost"
    ];

    private static readonly HashSet<string> JustificationRequiredCategories =
    [
        "Variable",
        "Other"
    ];

    private string _amountInput = string.Empty;
    private DateTime _entryDate = DateTime.Today;
    private string _selectedType = RevenueType;
    private string _selectedCategory = "Variable";
    private string _selectedCurrency = CurrencyHelper.Usd;
    private string _justification = string.Empty;
    private bool _isJustificationEnabled = true;
    private bool _isJustificationRequired = true;
    private DateTime _reportStartDate = DateTime.Today;
    private DateTime _reportEndDate = DateTime.Today;
    private string _totalRevenueText = "$ 0.00 | FC 0";
    private string _totalExpensesText = "$ 0.00 | FC 0";
    private string _netProfitText = "$ 0.00 | FC 0";
    private string _netProfitColor = "#2ECC71";
    private string _salesSummaryText = "$ 0.00 | FC 0";
    private string _tipsSummaryText = "$ 0.00 | FC 0";
    private string _payrollSummaryText = "$ 0.00 | FC 0";
    private string _selectedReportType = "Transactions";
    private string _selectedPeriod = "Week";
    private string _selectedPeriodLabel = "This Week";
    private string _todayRevenueText = "$ 0.00 | FC 0";
    private string _todayExpensesText = "$ 0.00 | FC 0";
    private string _todayNetProfitText = "$ 0.00 | FC 0";
    private string _todayNetProfitColor = "#2ECC71";
    private bool _isLoading;

    public override string ActivePage => "Money";

    public ObservableCollection<MoneyLedgerItemViewModel> DailyLedger { get; } = [];
    public ObservableCollection<string> EntryTypes { get; } = new([RevenueType, ExpenseType]);
    public ObservableCollection<string> Categories { get; } = new(["Sale", "Salary", "Fixed Cost", "Tip", "Gift", "Variable", "Other"]);
    public ObservableCollection<string> EntryCurrencies { get; } = new([CurrencyHelper.Usd, CurrencyHelper.CongoleseFranc]);
    public ObservableCollection<string> ReportTypes { get; } = new(["Transactions", "Orders", "Inventory", "Attendance", "All Reports"]);
    public ObservableCollection<string> PeriodOptions { get; } = new(["Week", "Month", "Year", "All"]);

    public string AmountInput
    {
        get => _amountInput;
        set => SetField(ref _amountInput, value);
    }

    public DateTime EntryDate
    {
        get => _entryDate;
        set => SetField(ref _entryDate, value);
    }

    public string SelectedType
    {
        get => _selectedType;
        set => SetField(ref _selectedType, value);
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (!SetField(ref _selectedCategory, value))
                return;

            ApplyCategoryRules();
        }
    }

    public string SelectedCurrency
    {
        get => _selectedCurrency;
        set => SetField(ref _selectedCurrency, value);
    }

    public string Justification
    {
        get => _justification;
        set => SetField(ref _justification, value);
    }

    public bool IsJustificationEnabled
    {
        get => _isJustificationEnabled;
        private set => SetField(ref _isJustificationEnabled, value);
    }

    public bool IsJustificationRequired
    {
        get => _isJustificationRequired;
        private set => SetField(ref _isJustificationRequired, value);
    }

    public DateTime ReportStartDate
    {
        get => _reportStartDate;
        set => SetField(ref _reportStartDate, value);
    }

    public DateTime ReportEndDate
    {
        get => _reportEndDate;
        set => SetField(ref _reportEndDate, value);
    }

    public string TotalRevenueText
    {
        get => _totalRevenueText;
        private set => SetField(ref _totalRevenueText, value);
    }

    public string TotalExpensesText
    {
        get => _totalExpensesText;
        private set => SetField(ref _totalExpensesText, value);
    }

    public string NetProfitText
    {
        get => _netProfitText;
        private set => SetField(ref _netProfitText, value);
    }

    public string NetProfitColor
    {
        get => _netProfitColor;
        private set => SetField(ref _netProfitColor, value);
    }

    public string SalesSummaryText
    {
        get => _salesSummaryText;
        private set => SetField(ref _salesSummaryText, value);
    }

    public string TipsSummaryText
    {
        get => _tipsSummaryText;
        private set => SetField(ref _tipsSummaryText, value);
    }

    public string PayrollSummaryText
    {
        get => _payrollSummaryText;
        private set => SetField(ref _payrollSummaryText, value);
    }

    public string SelectedReportType
    {
        get => _selectedReportType;
        set => SetField(ref _selectedReportType, value);
    }

    public string SelectedPeriod
    {
        get => _selectedPeriod;
        set
        {
            if (!SetField(ref _selectedPeriod, value))
                return;

            OnPropertyChanged(nameof(IsWeekSelected));
            OnPropertyChanged(nameof(IsMonthSelected));
            OnPropertyChanged(nameof(IsYearSelected));
            OnPropertyChanged(nameof(IsAllSelected));
            LoadData();
        }
    }

    public string SelectedPeriodLabel
    {
        get => _selectedPeriodLabel;
        private set => SetField(ref _selectedPeriodLabel, value);
    }

    public string TodayRevenueText
    {
        get => _todayRevenueText;
        private set => SetField(ref _todayRevenueText, value);
    }

    public string TodayExpensesText
    {
        get => _todayExpensesText;
        private set => SetField(ref _todayExpensesText, value);
    }

    public string TodayNetProfitText
    {
        get => _todayNetProfitText;
        private set => SetField(ref _todayNetProfitText, value);
    }

    public string TodayNetProfitColor
    {
        get => _todayNetProfitColor;
        private set => SetField(ref _todayNetProfitColor, value);
    }

    public bool IsWeekSelected => SelectedPeriod == "Week";
    public bool IsMonthSelected => SelectedPeriod == "Month";
    public bool IsYearSelected => SelectedPeriod == "Year";
    public bool IsAllSelected => SelectedPeriod == "All";
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    public ICommand AddEntryCommand { get; }
    public ICommand RefreshLedgerCommand { get; }
    public ICommand GenerateReportCommand { get; }
    public ICommand ExportExcelCommand { get; }
    public ICommand BulkExportExcelCommand { get; }
    public ICommand SelectPeriodCommand { get; }

    public MoneyViewModel(Action<BaseViewModel> navigate) : base(navigate)
    {
        AddEntryCommand = new RelayCommand(_ => AddEntry());
        RefreshLedgerCommand = new RelayCommand(_ => LoadData());
        GenerateReportCommand = new RelayCommand(_ => GenerateReport());
        ExportExcelCommand = new RelayCommand(_ => ExportExcel());
        BulkExportExcelCommand = new RelayCommand(_ => BulkExportExcelWorkbook());
        SelectPeriodCommand = new RelayCommand(period => SetPeriod(period as string));

        ApplyCategoryRules();
        LoadData();
    }

    private void ApplyCategoryRules()
    {
        IsJustificationRequired = JustificationRequiredCategories.Contains(SelectedCategory);
        IsJustificationEnabled = !FixedCategories.Contains(SelectedCategory);

        if (!IsJustificationEnabled)
            Justification = string.Empty;
    }

    private void AddEntry()
    {
        if (!decimal.TryParse(AmountInput, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            if (!decimal.TryParse(AmountInput, NumberStyles.Number, CultureInfo.CurrentCulture, out amount))
            {
                MessageBox.Show("Enter a valid amount.", "Money Entry", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        amount = Math.Round(amount, 2);
        if (amount <= 0)
        {
            MessageBox.Show("Amount must be greater than zero.", "Money Entry", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (IsJustificationRequired && string.IsNullOrWhiteSpace(Justification))
        {
            MessageBox.Show("Justification is required for Variable/Other transactions.", "Money Entry", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        using var db = new AppDbContext();
        db.Transactions.Add(new MoneyTransaction
        {
            Amount = amount,
            AmountUsd = CurrencyHelper.ResolveUsdAmount(amount, SelectedCurrency),
            AmountFc = CurrencyHelper.ResolveFcAmount(amount, SelectedCurrency),
            Date = EntryDate.Date.AddHours(DateTime.Now.Hour).AddMinutes(DateTime.Now.Minute),
            Type = SelectedType,
            Category = SelectedCategory,
            CurrencyCode = SelectedCurrency,
            ExchangeRateUsed = CurrencyHelper.FcPerUsd,
            Justification = Justification?.Trim() ?? string.Empty,
            IsFixed = FixedCategories.Contains(SelectedCategory)
        });
        db.SaveChanges();

        AmountInput = string.Empty;
        SelectedCurrency = CurrencyHelper.Usd;
        if (IsJustificationEnabled)
            Justification = string.Empty;
        EntryDate = DateTime.Today;

        LoadData();
    }

    private async void LoadData()
    {
        if (IsLoading)
            return;

        IsLoading = true;
        try
        {
            var selectedPeriod = SelectedPeriod;
            var snapshotTask = Task.Run(() => BuildDashboardSnapshot(selectedPeriod));
            var completedTask = await Task.WhenAny(snapshotTask, Task.Delay(5000));
            if (completedTask != snapshotTask)
            {
                LogMoneyDebug("Snapshot build timeout (>5000ms).");
                MessageBox.Show(
                    "Money dashboard timed out while loading data. Please try Refresh again.",
                    "Money",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var snapshot = await snapshotTask;

            TodayRevenueText = snapshot.TodayRevenueText;
            TodayExpensesText = snapshot.TodayExpensesText;
            TodayNetProfitText = snapshot.TodayNetProfitText;
            TodayNetProfitColor = snapshot.TodayNetProfitColor;

            SelectedPeriodLabel = snapshot.SelectedPeriodLabel;
            ReportStartDate = snapshot.ReportStartDate;
            ReportEndDate = snapshot.ReportEndDate;

            DailyLedger.Clear();
            foreach (var item in snapshot.LedgerItems)
                DailyLedger.Add(item);

            TotalRevenueText = snapshot.TotalRevenueText;
            TotalExpensesText = snapshot.TotalExpensesText;
            NetProfitText = snapshot.NetProfitText;
            NetProfitColor = snapshot.NetProfitColor;
            SalesSummaryText = snapshot.SalesSummaryText;
            TipsSummaryText = snapshot.TipsSummaryText;
            PayrollSummaryText = snapshot.PayrollSummaryText;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Money dashboard failed to load:\n{ex.Message}",
                "Money",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void GenerateReport()
    {
        var range = TryGetReportRange();
        if (range is null)
            return;

        var (fromDate, toDate, rangeEndExclusive) = range.Value;

        using var db = new AppDbContext();
        var transactions = db.Transactions
            .AsNoTracking()
            .Where(t => t.Date >= fromDate && t.Date < rangeEndExclusive)
            .OrderBy(t => t.Date)
            .ThenBy(t => t.Id)
            .ToList();

        var totalSales = transactions.Where(t => t.Type == RevenueType && t.Category == "Sale").ToList();
        var tipsCollected = transactions.Where(t => t.Type == RevenueType && t.Category == "Tip").ToList();
        var payrollDeductions = transactions.Where(t => t.Type == ExpenseType && t.Category == "Salary").ToList();
        var totalRevenue = transactions.Where(t => t.Type == RevenueType).ToList();
        var totalExpenses = transactions.Where(t => t.Type == ExpenseType).ToList();
        var totalSalesText = CurrencyHelper.FormatDualCurrency(
            SumByCurrency(totalSales, CurrencyHelper.Usd),
            SumByCurrency(totalSales, CurrencyHelper.CongoleseFranc));
        var tipsCollectedText = CurrencyHelper.FormatDualCurrency(
            SumByCurrency(tipsCollected, CurrencyHelper.Usd),
            SumByCurrency(tipsCollected, CurrencyHelper.CongoleseFranc));
        var payrollDeductionsText = CurrencyHelper.FormatDualCurrency(
            SumByCurrency(payrollDeductions, CurrencyHelper.Usd),
            SumByCurrency(payrollDeductions, CurrencyHelper.CongoleseFranc));
        var netUsd = SumByCurrency(totalRevenue, CurrencyHelper.Usd) - SumByCurrency(totalExpenses, CurrencyHelper.Usd);
        var netFc = SumByCurrency(totalRevenue, CurrencyHelper.CongoleseFranc) - SumByCurrency(totalExpenses, CurrencyHelper.CongoleseFranc);
        var finalNetBalanceText = CurrencyHelper.FormatDualCurrency(netUsd, netFc);

        var fileDialog = new SaveFileDialog
        {
            Title = "Save Financial Report",
            Filter = "PDF files (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            AddExtension = true,
            FileName = $"money-report-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.pdf"
        };

        if (fileDialog.ShowDialog() != true)
            return;

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
                            table.Cell().PaddingVertical(3).AlignRight().Text($"{(isRevenue ? "+" : "-")}{CurrencyHelper.FormatAmount(transaction.Amount, NormalizeCurrencyCode(transaction.CurrencyCode))}")
                                .FontColor(isRevenue ? "#2ECC71" : "#DC143C");
                        }
                    });
                });

                page.Footer().AlignCenter().Text($"EliteRestaurantPro MoneyView  |  Generated {DateTime.Now:dd MMM yyyy HH:mm}")
                    .FontColor("#A99867");
            });
        }).GeneratePdf(fileDialog.FileName);

        MessageBox.Show(
            $"Financial PDF exported:\n{fileDialog.FileName}",
            "Money Report",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ExportExcel()
    {
        var range = TryGetReportRange();
        if (range is null)
            return;

        if (SelectedReportType == "All Reports")
        {
            MessageBox.Show("Select a single report type for this export. Use Bulk Export for all reports.", "Excel Export", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var (fromDate, toDate, rangeEndExclusive) = range.Value;
        var saveDialog = new SaveFileDialog
        {
            Title = "Save Excel Report",
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            DefaultExt = ".xlsx",
            AddExtension = true,
            FileName = $"{SelectedReportType.ToLowerInvariant()}-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.xlsx"
        };

        if (saveDialog.ShowDialog() != true)
            return;

        using var db = new AppDbContext();
        FinancialTransactionService.EnsureCompletedOrderRevenues(db);
        FinancialTransactionService.EnsureScheduledSalaryExpenses(db, fromDate, toDate);
        db.SaveChanges();

        var data = BuildReportRows(db, SelectedReportType, fromDate, rangeEndExclusive);
        ExcelExportService.ExportSingleSheet(saveDialog.FileName, SelectedReportType, data.Headers, data.Rows);

        MessageBox.Show($"Excel exported:\n{saveDialog.FileName}", "Excel Export", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BulkExportExcelWorkbook()
    {
        var range = TryGetReportRange();
        if (range is null)
            return;

        var (fromDate, toDate, rangeEndExclusive) = range.Value;
        var saveDialog = new SaveFileDialog
        {
            Title = "Save Bulk Excel Report",
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            DefaultExt = ".xlsx",
            AddExtension = true,
            FileName = $"bulk-reports-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.xlsx"
        };

        if (saveDialog.ShowDialog() != true)
            return;

        using var db = new AppDbContext();
        FinancialTransactionService.EnsureCompletedOrderRevenues(db);
        FinancialTransactionService.EnsureScheduledSalaryExpenses(db, fromDate, toDate);
        db.SaveChanges();

        var transactions = BuildReportRows(db, "Transactions", fromDate, rangeEndExclusive);
        var orders = BuildReportRows(db, "Orders", fromDate, rangeEndExclusive);
        var inventory = BuildReportRows(db, "Inventory", fromDate, rangeEndExclusive);
        var attendance = BuildReportRows(db, "Attendance", fromDate, rangeEndExclusive);

        ExcelExportService.ExportWorkbook(saveDialog.FileName, [
            ("Transactions", transactions.Headers, transactions.Rows),
            ("Orders", orders.Headers, orders.Rows),
            ("Inventory", inventory.Headers, inventory.Rows),
            ("Attendance", attendance.Headers, attendance.Rows)
        ]);

        MessageBox.Show(
            $"Bulk Excel report exported:\n{saveDialog.FileName}",
            "Bulk Export",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private (DateTime FromDate, DateTime ToDate, DateTime ToExclusive)? TryGetReportRange()
    {
        var fromDate = ReportStartDate.Date;
        var toDate = ReportEndDate.Date;
        if (toDate < fromDate)
        {
            MessageBox.Show("End date must be after start date.", "Report Range", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        return (fromDate, toDate, toDate.AddDays(1));
    }

    private static (IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows) BuildReportRows(
        AppDbContext db,
        string reportType,
        DateTime fromDate,
        DateTime toExclusive)
        => reportType switch
        {
            "Transactions" => BuildTransactionRows(db, fromDate, toExclusive),
            "Orders" => BuildOrderRows(db, fromDate, toExclusive),
            "Inventory" => BuildInventoryRows(db, fromDate, toExclusive),
            "Attendance" => BuildAttendanceRows(db, fromDate, toExclusive),
            _ => BuildTransactionRows(db, fromDate, toExclusive)
        };

    private static (IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows) BuildTransactionRows(AppDbContext db, DateTime fromDate, DateTime toExclusive)
    {
        var records = db.Transactions
            .AsNoTracking()
            .Where(t => t.Date >= fromDate && t.Date < toExclusive)
            .OrderBy(t => t.Date)
            .ThenBy(t => t.Id)
            .ToList();

        var rows = records
            .Select(t => (IReadOnlyList<string>)
            [
                t.Id.ToString(),
                t.Date.ToString("yyyy-MM-dd HH:mm"),
                t.Type,
                t.Category,
                NormalizeCurrencyCode(t.CurrencyCode),
                t.Amount.ToString("N2"),
                t.IsFixed ? "Yes" : "No",
                t.Justification
            ])
            .ToList();

        return (["Id", "Date", "Type", "Category", "Currency", "Amount", "IsFixed", "Justification"], rows);
    }

    private static (IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows) BuildOrderRows(AppDbContext db, DateTime fromDate, DateTime toExclusive)
    {
        var orders = db.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= fromDate && o.CreatedAt < toExclusive)
            .OrderBy(o => o.CreatedAt)
            .ToList();

        var orderItems = db.OrderItems
            .AsNoTracking()
            .Where(i => orders.Select(o => o.Id).Contains(i.OrderRecordId))
            .ToList();

        var products = db.Products
            .AsNoTracking()
            .ToDictionary(p => p.Id, p => p.Price);

        var totalsByOrder = orderItems
            .GroupBy(i => i.OrderRecordId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(item => (products.TryGetValue(item.ProductId, out var price) ? price : 0m) * item.Quantity));

        var rows = orders
            .Select(order => (IReadOnlyList<string>)
            [
                order.Id.ToString(),
                string.IsNullOrWhiteSpace(order.UniqueId) ? $"ORD-{order.Id:000}" : order.UniqueId,
                order.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                order.Status,
                order.TableCode,
                order.ServerName,
                (totalsByOrder.TryGetValue(order.Id, out var total) ? total : 0m).ToString("N2")
            ])
            .ToList();

        return (["Id", "OrderId", "Date", "Status", "Table", "Server", "Total"], rows);
    }

    private static (IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows) BuildInventoryRows(AppDbContext db, DateTime fromDate, DateTime toExclusive)
    {
        var orders = db.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= fromDate && o.CreatedAt < toExclusive && o.Status != "Cancelled")
            .Select(o => new { o.Id, o.UniqueId })
            .ToList();
        var orderIds = orders.Select(o => o.Id).ToList();

        var orderItems = db.OrderItems
            .AsNoTracking()
            .Where(i => orderIds.Contains(i.OrderRecordId))
            .ToList();
        var ingredients = db.ProductIngredients
            .AsNoTracking()
            .ToList();
        var inventory = db.InventoryItems
            .AsNoTracking()
            .ToDictionary(i => i.Id, i => i);

        var ingredientsByProduct = ingredients
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var usedByInventory = new Dictionary<int, decimal>();
        var orderCountByInventory = new Dictionary<int, int>();

        foreach (var line in orderItems)
        {
            if (!ingredientsByProduct.TryGetValue(line.ProductId, out var recipe))
                continue;

            foreach (var ingredient in recipe)
            {
                var consumed = ingredient.Quantity * line.Quantity;
                if (!usedByInventory.TryAdd(ingredient.InventoryItemId, consumed))
                    usedByInventory[ingredient.InventoryItemId] += consumed;

                if (!orderCountByInventory.TryAdd(ingredient.InventoryItemId, 1))
                    orderCountByInventory[ingredient.InventoryItemId]++;
            }
        }

        var rows = usedByInventory
            .OrderByDescending(kv => kv.Value)
            .Select(kv =>
            {
                var item = inventory.TryGetValue(kv.Key, out var inv) ? inv : null;
                var count = orderCountByInventory.TryGetValue(kv.Key, out var c) ? c : 0;
                return (IReadOnlyList<string>)
                [
                    item?.UniqueId ?? "N/A",
                    item?.Name ?? "Unknown",
                    item?.Unit ?? string.Empty,
                    kv.Value.ToString("0.##"),
                    (item?.StockQuantity ?? 0m).ToString("0.##"),
                    count.ToString()
                ];
            })
            .ToList();

        return (["ItemId", "Item", "Unit", "UsedQty", "CurrentStock", "LinkedOrders"], rows);
    }

    private static (IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows) BuildAttendanceRows(AppDbContext db, DateTime fromDate, DateTime toExclusive)
    {
        var rows = db.EmployeeAttendances
            .AsNoTracking()
            .Include(a => a.Employee)
            .Where(a => a.WorkDate >= fromDate && a.WorkDate < toExclusive)
            .OrderBy(a => a.WorkDate)
            .ThenBy(a => a.EmployeeId)
            .ToList()
            .Select(a => (IReadOnlyList<string>)
            [
                a.WorkDate.ToString("yyyy-MM-dd"),
                a.Employee?.UniqueId ?? string.Empty,
                a.Employee?.Name ?? "Unknown",
                a.ClockInTime?.ToString("HH:mm") ?? "-",
                a.ClockOutTime?.ToString("HH:mm") ?? "-",
                string.IsNullOrWhiteSpace(a.ClockInStatus) ? "Pending" : a.ClockInStatus,
                a.Justification
            ])
            .ToList();

        return (["Date", "EmployeeId", "Employee", "ClockIn", "ClockOut", "Status", "Justification"], rows);
    }

    private void SetPeriod(string? period)
    {
        if (string.IsNullOrWhiteSpace(period))
            return;

        if (!PeriodOptions.Contains(period))
            return;

        SelectedPeriod = period;
    }

    private static (DateTime FromDate, DateTime ToDate, DateTime ToExclusive, string Label) ResolvePeriodRange(AppDbContext db, DateTime today, string selectedPeriod)
    {
        return selectedPeriod switch
        {
            "Month" => ResolveMonthRange(today),
            "Year" => ResolveYearRange(today),
            "All" => ResolveAllRange(db, today),
            _ => ResolveWeekRange(today)
        };
    }

    private static (DateTime FromDate, DateTime ToDate, DateTime ToExclusive, string Label) ResolveWeekRange(DateTime today)
    {
        var dayOfWeek = ((int)today.DayOfWeek + 6) % 7;
        var from = today.AddDays(-dayOfWeek).Date;
        var to = today.Date;
        return (from, to, to.AddDays(1), "This Week");
    }

    private static (DateTime FromDate, DateTime ToDate, DateTime ToExclusive, string Label) ResolveMonthRange(DateTime today)
    {
        var from = new DateTime(today.Year, today.Month, 1);
        var to = today.Date;
        return (from, to, to.AddDays(1), "This Month");
    }

    private static (DateTime FromDate, DateTime ToDate, DateTime ToExclusive, string Label) ResolveYearRange(DateTime today)
    {
        var from = new DateTime(today.Year, 1, 1);
        var to = today.Date;
        return (from, to, to.AddDays(1), "This Year");
    }

    private static (DateTime FromDate, DateTime ToDate, DateTime ToExclusive, string Label) ResolveAllRange(AppDbContext db, DateTime today)
    {
        var firstDate = db.Transactions
            .AsNoTracking()
            .OrderBy(t => t.Date)
            .Select(t => (DateTime?)t.Date)
            .FirstOrDefault();

        var from = firstDate?.Date ?? today.Date;
        var to = today.Date;
        return (from, to, to.AddDays(1), "All Time");
    }

    private static MoneyDashboardSnapshot BuildDashboardSnapshot(string selectedPeriod)
    {
        LogMoneyDebug($"Build snapshot start | period={selectedPeriod}");
        var startedAt = DateTime.UtcNow;
        using var db = new AppDbContext();
        db.Database.SetCommandTimeout(5);
        // Backfill sale rows for completed orders (e.g. if revenue was skipped before a save-order fix).
        FinancialTransactionService.EnsureCompletedOrderRevenues(db);
        db.SaveChanges();
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        var todaysTransactions = db.Transactions
            .AsNoTracking()
            .Where(t => t.Date >= today && t.Date < tomorrow)
            .ToList();
        LogMoneyDebug($"Loaded today transactions: {todaysTransactions.Count}");

        var todayRevenue = todaysTransactions
            .Where(t => string.Equals(t.Type, RevenueType, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var todayExpenses = todaysTransactions
            .Where(t => string.Equals(t.Type, ExpenseType, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var todayRevenueUsd = SumByCurrency(todayRevenue, CurrencyHelper.Usd);
        var todayRevenueFc = SumByCurrency(todayRevenue, CurrencyHelper.CongoleseFranc);
        var todayExpensesUsd = SumByCurrency(todayExpenses, CurrencyHelper.Usd);
        var todayExpensesFc = SumByCurrency(todayExpenses, CurrencyHelper.CongoleseFranc);

        var period = ResolvePeriodRange(db, today, selectedPeriod);
        var periodRows = db.Transactions
            .AsNoTracking()
            .Where(t => t.Date >= period.FromDate && t.Date < period.ToExclusive)
            .Select(t => new
            {
                t.Id,
                t.Date,
                t.Type,
                t.Category,
                t.Amount,
                t.CurrencyCode,
                t.Justification,
                t.IsFixed
            })
            .ToList();
        LogMoneyDebug($"Loaded period rows (full): {periodRows.Count} | range={period.FromDate:yyyy-MM-dd}->{period.ToDate:yyyy-MM-dd}");

        var periodLedgerRows = periodRows
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .Take(MaxLedgerRows)
            .ToList();
        LogMoneyDebug($"Loaded period ledger rows: {periodLedgerRows.Count} | range={period.FromDate:yyyy-MM-dd}->{period.ToDate:yyyy-MM-dd}");

        var ledger = periodLedgerRows.Select(row =>
        {
            var isRevenue = string.Equals(row.Type, RevenueType, StringComparison.OrdinalIgnoreCase);
            return new MoneyLedgerItemViewModel
            {
                Date = row.Date,
                Type = row.Type,
                Category = row.Category,
                Justification = string.IsNullOrWhiteSpace(row.Justification)
                    ? (row.IsFixed ? "Fixed scheduled transaction" : "No justification")
                    : row.Justification,
                AmountText = $"{(isRevenue ? "+" : "-")}{CurrencyHelper.FormatAmount(row.Amount, NormalizeCurrencyCode(row.CurrencyCode))}",
                AmountColor = isRevenue ? "#2ECC71" : "#DC143C"
            };
        }).ToList();

        var totalRevenue = periodRows
            .Where(t => t.Type == RevenueType)
            .ToList();
        var totalExpenses = periodRows
            .Where(t => t.Type == ExpenseType)
            .ToList();

        var salesTotal = periodRows
            .Where(t => t.Type == RevenueType && t.Category == "Sale")
            .ToList();
        var tipsTotal = periodRows
            .Where(t => t.Type == RevenueType && t.Category == "Tip")
            .ToList();
        var payrollTotal = periodRows
            .Where(t => t.Type == ExpenseType && t.Category == "Salary")
            .ToList();

        var totalRevenueUsd = SumByCurrency(totalRevenue, CurrencyHelper.Usd);
        var totalRevenueFc = SumByCurrency(totalRevenue, CurrencyHelper.CongoleseFranc);
        var totalExpensesUsd = SumByCurrency(totalExpenses, CurrencyHelper.Usd);
        var totalExpensesFc = SumByCurrency(totalExpenses, CurrencyHelper.CongoleseFranc);
        var netUsd = totalRevenueUsd - totalExpensesUsd;
        var netFc = totalRevenueFc - totalExpensesFc;
        var salesUsd = SumByCurrency(salesTotal, CurrencyHelper.Usd);
        var salesFc = SumByCurrency(salesTotal, CurrencyHelper.CongoleseFranc);
        var tipsUsd = SumByCurrency(tipsTotal, CurrencyHelper.Usd);
        var tipsFc = SumByCurrency(tipsTotal, CurrencyHelper.CongoleseFranc);
        var payrollUsd = SumByCurrency(payrollTotal, CurrencyHelper.Usd);
        var payrollFc = SumByCurrency(payrollTotal, CurrencyHelper.CongoleseFranc);

        var snapshot = new MoneyDashboardSnapshot
        {
            TodayRevenueText = CurrencyHelper.FormatDualCurrency(todayRevenueUsd, todayRevenueFc),
            TodayExpensesText = CurrencyHelper.FormatDualCurrency(todayExpensesUsd, todayExpensesFc),
            TodayNetProfitText = CurrencyHelper.FormatDualCurrency(todayRevenueUsd - todayExpensesUsd, todayRevenueFc - todayExpensesFc),
            TodayNetProfitColor = todayRevenueUsd - todayExpensesUsd >= 0m && todayRevenueFc - todayExpensesFc >= 0m ? "#2ECC71" : "#DC143C",
            SelectedPeriodLabel = period.Label,
            ReportStartDate = period.FromDate,
            ReportEndDate = period.ToDate,
            LedgerItems = ledger,
            TotalRevenueText = CurrencyHelper.FormatDualCurrency(totalRevenueUsd, totalRevenueFc),
            TotalExpensesText = CurrencyHelper.FormatDualCurrency(totalExpensesUsd, totalExpensesFc),
            NetProfitText = CurrencyHelper.FormatDualCurrency(netUsd, netFc),
            NetProfitColor = netUsd >= 0m && netFc >= 0m ? "#2ECC71" : "#DC143C",
            SalesSummaryText = CurrencyHelper.FormatDualCurrency(salesUsd, salesFc),
            TipsSummaryText = CurrencyHelper.FormatDualCurrency(tipsUsd, tipsFc),
            PayrollSummaryText = CurrencyHelper.FormatDualCurrency(payrollUsd, payrollFc)
        };
        LogMoneyDebug($"Build snapshot done in {(DateTime.UtcNow - startedAt).TotalMilliseconds:N0} ms");
        return snapshot;
    }

    private static string NormalizeCurrencyCode(string? currencyCode)
        => string.Equals(currencyCode, CurrencyHelper.CongoleseFranc, StringComparison.OrdinalIgnoreCase)
            ? CurrencyHelper.CongoleseFranc
            : CurrencyHelper.Usd;

    private static decimal SumByCurrency<T>(IEnumerable<T> rows, string currencyCode) where T : class
    {
        decimal total = 0m;
        foreach (var row in rows)
        {
            var type = row.GetType();
            var rowCurrency = NormalizeCurrencyCode(type.GetProperty("CurrencyCode")?.GetValue(row) as string);
            if (!string.Equals(rowCurrency, currencyCode, StringComparison.OrdinalIgnoreCase))
                continue;

            var amountValue = type.GetProperty("Amount")?.GetValue(row);
            if (amountValue is decimal amount)
                total += amount;
        }

        return total;
    }

    private sealed class MoneyDashboardSnapshot
    {
        public string TodayRevenueText { get; init; } = "$ 0.00 | FC 0";
        public string TodayExpensesText { get; init; } = "$ 0.00 | FC 0";
        public string TodayNetProfitText { get; init; } = "$ 0.00 | FC 0";
        public string TodayNetProfitColor { get; init; } = "#2ECC71";
        public string SelectedPeriodLabel { get; init; } = "This Week";
        public DateTime ReportStartDate { get; init; } = DateTime.Today;
        public DateTime ReportEndDate { get; init; } = DateTime.Today;
        public List<MoneyLedgerItemViewModel> LedgerItems { get; init; } = [];
        public string TotalRevenueText { get; init; } = "$ 0.00 | FC 0";
        public string TotalExpensesText { get; init; } = "$ 0.00 | FC 0";
        public string NetProfitText { get; init; } = "$ 0.00 | FC 0";
        public string NetProfitColor { get; init; } = "#2ECC71";
        public string SalesSummaryText { get; init; } = "$ 0.00 | FC 0";
        public string TipsSummaryText { get; init; } = "$ 0.00 | FC 0";
        public string PayrollSummaryText { get; init; } = "$ 0.00 | FC 0";
    }

    private static void LogMoneyDebug(string message)
    {
        try
        {
            var appFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EliteRestaurantPro",
                "logs");
            Directory.CreateDirectory(appFolder);
            var path = Path.Combine(appFolder, "money-debug.log");
            File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | {message}{Environment.NewLine}");
        }
        catch
        {
            // Best-effort logging only.
        }
    }
}
