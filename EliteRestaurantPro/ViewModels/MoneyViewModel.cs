using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Reporting;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.Services;
using Microsoft.Win32;

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
    private readonly FinancialPostingService _posting = new();

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
        ExportExcelCommand = new RelayCommand(_ => _ = ExportExcelAsync());
        BulkExportExcelCommand = new RelayCommand(_ => _ = BulkExportExcelWorkbookAsync());
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

        var result = _posting.AddManualLedgerEntry(
            amount,
            SelectedCurrency,
            EntryDate,
            SelectedType,
            SelectedCategory,
            Justification?.Trim() ?? string.Empty,
            FixedCategories.Contains(SelectedCategory));
        if (!result.Ok)
        {
            MessageBox.Show(result.Message, "Money Entry", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

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
            var snapshotTask = Task.Run(async () =>
            {
                var data = new AdminDataApiClient();
                var txs = await data.GetMoneyTransactionsAsync().ConfigureAwait(false);
                return MoneyDashboardSnapshotBuilder.BuildFromTransactions(txs.ToList(), selectedPeriod, MaxLedgerRows);
            });
            var completedTask = await Task.WhenAny(snapshotTask, Task.Delay(5000));
            if (completedTask != snapshotTask)
            {
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
            foreach (var row in snapshot.LedgerItems)
            {
                DailyLedger.Add(new MoneyLedgerItemViewModel
                {
                    Date = row.Date,
                    Type = row.Type,
                    Category = row.Category,
                    Justification = row.Justification,
                    AmountText = row.AmountText,
                    AmountColor = row.AmountColor
                });
            }

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

        MoneyFinancialPdfExportService.ExportLedgerPdf(fileDialog.FileName, fromDate, toDate, rangeEndExclusive);

        MessageBox.Show(
            $"Financial PDF exported:\n{fileDialog.FileName}",
            "Money Report",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async Task ExportExcelAsync()
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

        var data = new AdminDataApiClient();
        var moneyTask = data.GetMoneyTransactionsAsync();
        var ordersTask = data.GetOrdersAsync();
        var productsTask = data.GetProductsAsync();
        var ingTask = data.GetProductIngredientsAsync();
        var invTask = data.GetInventoryItemsAsync();
        var attTask = data.GetAttendanceAsync();
        var empTask = data.GetEmployeesAsync();
        await Task.WhenAll(moneyTask, ordersTask, productsTask, ingTask, invTask, attTask, empTask).ConfigureAwait(true);
        var rows = MoneyExcelReportRowsBuilder.BuildReportRowsFromData(
            SelectedReportType,
            fromDate,
            rangeEndExclusive,
            (await moneyTask.ConfigureAwait(true)).ToList(),
            (await ordersTask.ConfigureAwait(true)).ToList(),
            (await productsTask.ConfigureAwait(true)).ToList(),
            (await ingTask.ConfigureAwait(true)).ToList(),
            (await invTask.ConfigureAwait(true)).ToList(),
            (await attTask.ConfigureAwait(true)).ToList(),
            (await empTask.ConfigureAwait(true)).ToList());

        ExcelExportService.ExportSingleSheet(saveDialog.FileName, SelectedReportType, rows.Headers, rows.Rows);

        MessageBox.Show($"Excel exported:\n{saveDialog.FileName}", "Excel Export", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async Task BulkExportExcelWorkbookAsync()
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

        var data = new AdminDataApiClient();
        var moneyTask = data.GetMoneyTransactionsAsync();
        var ordersTask = data.GetOrdersAsync();
        var productsTask = data.GetProductsAsync();
        var ingTask = data.GetProductIngredientsAsync();
        var invTask = data.GetInventoryItemsAsync();
        var attTask = data.GetAttendanceAsync();
        var empTask = data.GetEmployeesAsync();
        await Task.WhenAll(moneyTask, ordersTask, productsTask, ingTask, invTask, attTask, empTask).ConfigureAwait(true);
        var money = (await moneyTask.ConfigureAwait(true)).ToList();
        var orders = (await ordersTask.ConfigureAwait(true)).ToList();
        var products = (await productsTask.ConfigureAwait(true)).ToList();
        var ing = (await ingTask.ConfigureAwait(true)).ToList();
        var inv = (await invTask.ConfigureAwait(true)).ToList();
        var att = (await attTask.ConfigureAwait(true)).ToList();
        var emp = (await empTask.ConfigureAwait(true)).ToList();

        var transactions = MoneyExcelReportRowsBuilder.BuildReportRowsFromData("Transactions", fromDate, rangeEndExclusive, money, orders, products, ing, inv, att, emp);
        var ordersRows = MoneyExcelReportRowsBuilder.BuildReportRowsFromData("Orders", fromDate, rangeEndExclusive, money, orders, products, ing, inv, att, emp);
        var inventory = MoneyExcelReportRowsBuilder.BuildReportRowsFromData("Inventory", fromDate, rangeEndExclusive, money, orders, products, ing, inv, att, emp);
        var attendance = MoneyExcelReportRowsBuilder.BuildReportRowsFromData("Attendance", fromDate, rangeEndExclusive, money, orders, products, ing, inv, att, emp);

        ExcelExportService.ExportWorkbook(saveDialog.FileName, [
            ("Transactions", transactions.Headers, transactions.Rows),
            ("Orders", ordersRows.Headers, ordersRows.Rows),
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

    private void SetPeriod(string? period)
    {
        if (string.IsNullOrWhiteSpace(period))
            return;

        if (!PeriodOptions.Contains(period))
            return;

        SelectedPeriod = period;
    }
}
