using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Reporting;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.Localization;
using EliteRestaurantPro.Services;
using Microsoft.Win32;

namespace EliteRestaurantPro.ViewModels;

public sealed class MoneyLedgerItemViewModel : INotifyPropertyChanged
{
    public DateTime Date { get; init; }
    public string RawType { get; init; } = string.Empty;
    public string RawCategory { get; init; } = string.Empty;
    public string RawJustification { get; init; } = string.Empty;
    public string RawAmountText { get; init; } = "$ 0.00";
    public string Type { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string Justification { get; private set; } = string.Empty;
    public string DateText { get; private set; } = string.Empty;
    public string AmountText { get; private set; } = "$ 0.00";
    public string AmountColor { get; init; } = "#2ECC71";

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ApplyLocalization()
    {
        Type = AdminTextLocalizer.TranslateMoneyType(RawType);
        Category = AdminTextLocalizer.TranslateMoneyCategory(RawCategory);
        Justification = MoneyUiLocalizer.TranslateJustification(RawJustification);
        DateText = MoneyUiLocalizer.FormatLedgerDate(Date);
        AmountText = RawAmountText;
        OnPropertyChanged(nameof(Type));
        OnPropertyChanged(nameof(Category));
        OnPropertyChanged(nameof(Justification));
        OnPropertyChanged(nameof(DateText));
        OnPropertyChanged(nameof(AmountText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
    private LocalizedSelectOption? _selectedTypeOption;
    private LocalizedSelectOption? _selectedCategoryOption;
    private LocalizedSelectOption? _selectedCurrencyOption;
    private string _justification = string.Empty;
    private bool _isJustificationEnabled = true;
    private bool _isJustificationRequired = true;
    private DateTime _reportStartDate = DateTime.Today;
    private DateTime _reportEndDate = DateTime.Today;
    private string _totalRevenueText = "$ 0.00 | FC 0";
    private string _totalExpensesText = "$ 0.00 | FC 0";
    private string _netProfitText = "$ 0.00 | FC 0";
    private string _netProfitColor = "#2ECC71";
    private string _salesSummaryValue = "$ 0.00 | FC 0";
    private string _tipsSummaryValue = "$ 0.00 | FC 0";
    private string _deliveryFeesSummaryValue = "$ 0.00 | FC 0";
    private string _payrollSummaryValue = "$ 0.00 | FC 0";
    private string _selectedReportType = "Transactions";
    private string _selectedPeriod = "Today";
    private string _todayRevenueText = "$ 0.00 | FC 0";
    private string _todayExpensesText = "$ 0.00 | FC 0";
    private string _todayNetProfitText = "$ 0.00 | FC 0";
    private string _todayNetProfitColor = "#2ECC71";
    private bool _isLoading;
    private readonly FinancialPostingService _posting = new();
    private List<MoneyTransaction>? _cachedTransactions;
    private string _cachedPeriod = "Today";

    public override string ActivePage => "Money";

    public string MoneyTitle => Loc.Admin("moneyTitle", "Money Dashboard");
    public string MoneySubtitle => Loc.Admin("moneySubtitle", "Daily revenue and expenses in real-time.");
    public string TodayRevenueLabel => Loc.Admin("moneyTodayRev", "Today revenue");
    public string TodayExpensesLabel => Loc.Admin("moneyTodayExp", "Today expenses");
    public string TodayNetLabel => Loc.Admin("moneyTodayNet", "Today net");
    public string ShowingPeriodText => Loc.Admin("moneyShowingFmt", "Showing: {{period}}",
        new Dictionary<string, string> { ["period"] = AdminTextLocalizer.TranslateMoneyPeriodLabel(_selectedPeriod) });
    public string TodayLabel => AdminTextLocalizer.TranslateMoneyPeriod("Today");
    public string WeekLabel => AdminTextLocalizer.TranslateMoneyPeriod("Week");
    public string MonthLabel => AdminTextLocalizer.TranslateMoneyPeriod("Month");
    public string YearLabel => AdminTextLocalizer.TranslateMoneyPeriod("Year");
    public string AllLabel => AdminTextLocalizer.TranslateMoneyPeriod("All");
    public string RefreshLabel => Loc.Admin("moneyRefresh", "Refresh");
    public string QuickEntryLabel => Loc.Admin("moneyQuickEntry", "Quick Entry");
    public string AmountLabel => Loc.Admin("moneyAmount", "Amount");
    public string CurrencyLabel => Loc.Admin("moneyCurrency", "Currency");
    public string TypeLabel => Loc.Admin("moneyColType", "Type");
    public string CategoryLabel => Loc.Admin("moneyColCategory", "Category");
    public string DateLabel => Loc.Admin("moneyDate", "Date");
    public string JustificationLabel => Loc.Admin("moneyColJustification", "Justification");
    public string AddEntryLabel => Loc.Admin("moneyAddEntry", "Add Entry");
    public string LedgerTitle => Loc.Admin("moneyLedgerTitle", "Ledger — latest 200 entries");
    public string ColDateLabel => Loc.Admin("moneyColDate", "Date");
    public string ColTypeLabel => Loc.Admin("moneyColType", "Type");
    public string ColCategoryLabel => Loc.Admin("moneyColCategory", "Category");
    public string ColJustificationLabel => Loc.Admin("moneyColJustification", "Justification");
    public string ColAmountLabel => Loc.Admin("moneyColAmount", "Amount");
    public string PeriodRevenueLabel => Loc.Admin("moneyPeriodRev", "Period revenue");
    public string PeriodExpensesLabel => Loc.Admin("moneyPeriodExp", "Period expenses");
    public string PeriodNetLabel => Loc.Admin("moneyPeriodNet", "Period net");
    public string SummaryLabel => Loc.Admin("moneySummary", "Summary");
    public string SalesSummaryLine => Loc.Admin("moneySummarySales", "Sales: {{value}}",
        new Dictionary<string, string> { ["value"] = _salesSummaryValue });
    public string TipsSummaryLine => Loc.Admin("moneySummaryTips", "Tips: {{value}}",
        new Dictionary<string, string> { ["value"] = _tipsSummaryValue });
    public string DeliveryFeesSummaryLine => Loc.Admin("moneySummaryDelivery", "Delivery fees: {{value}}",
        new Dictionary<string, string> { ["value"] = _deliveryFeesSummaryValue });
    public string PayrollSummaryLine => Loc.Admin("moneySummaryPayroll", "Payroll: {{value}}",
        new Dictionary<string, string> { ["value"] = _payrollSummaryValue });
    public string GenerateReportLabel => Loc.Admin("moneyGenReport", "Generate Report (PDF)");
    public string LoadingText => Loc.Admin("moneyLoading", "Loading money dashboard…");

    public ObservableCollection<MoneyLedgerItemViewModel> DailyLedger { get; } = [];
    public ObservableCollection<LocalizedSelectOption> EntryTypes { get; } = new();
    public ObservableCollection<LocalizedSelectOption> Categories { get; } = new();
    public ObservableCollection<LocalizedSelectOption> EntryCurrencies { get; } = new();
    public ObservableCollection<string> ReportTypes { get; } = new(["Transactions", "Orders", "Inventory", "Attendance", "All Reports"]);
    public ObservableCollection<string> PeriodOptions { get; } = new(["Today", "Week", "Month", "Year", "All"]);

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

    public LocalizedSelectOption? SelectedTypeOption
    {
        get => _selectedTypeOption;
        set
        {
            if (!SetField(ref _selectedTypeOption, value) || value is null)
                return;
            _selectedType = value.Value;
            OnPropertyChanged(nameof(SelectedType));
        }
    }

    public string SelectedType
    {
        get => _selectedType;
        set
        {
            if (!SetField(ref _selectedType, value))
                return;
            SyncSelectOption(ref _selectedTypeOption, EntryTypes, value, nameof(SelectedTypeOption));
        }
    }

    public LocalizedSelectOption? SelectedCategoryOption
    {
        get => _selectedCategoryOption;
        set
        {
            if (!SetField(ref _selectedCategoryOption, value) || value is null)
                return;
            _selectedCategory = value.Value;
            OnPropertyChanged(nameof(SelectedCategory));
            ApplyCategoryRules();
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (!SetField(ref _selectedCategory, value))
                return;

            SyncSelectOption(ref _selectedCategoryOption, Categories, value, nameof(SelectedCategoryOption));
            ApplyCategoryRules();
        }
    }

    public LocalizedSelectOption? SelectedCurrencyOption
    {
        get => _selectedCurrencyOption;
        set
        {
            if (!SetField(ref _selectedCurrencyOption, value) || value is null)
                return;
            _selectedCurrency = value.Value;
            OnPropertyChanged(nameof(SelectedCurrency));
        }
    }

    public string SelectedCurrency
    {
        get => _selectedCurrency;
        set
        {
            if (!SetField(ref _selectedCurrency, value))
                return;
            SyncSelectOption(ref _selectedCurrencyOption, EntryCurrencies, value, nameof(SelectedCurrencyOption));
        }
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

            OnPropertyChanged(nameof(IsTodaySelected));
            OnPropertyChanged(nameof(IsWeekSelected));
            OnPropertyChanged(nameof(IsMonthSelected));
            OnPropertyChanged(nameof(IsYearSelected));
            OnPropertyChanged(nameof(IsAllSelected));
            OnPropertyChanged(nameof(ShowingPeriodText));
            LoadData();
        }
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

    public bool IsTodaySelected => SelectedPeriod == "Today";
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

        RebuildEntrySelectLists();
        ApplyCategoryRules();
        LoadData();
    }

    protected override void RefreshLocalizedStrings()
    {
        base.RefreshLocalizedStrings();
        RebuildEntrySelectLists();
        if (_cachedTransactions is not null)
            ApplySnapshot(MoneyDashboardSnapshotBuilder.BuildFromTransactions(
                _cachedTransactions,
                _cachedPeriod,
                MaxLedgerRows,
                formatCulture: AdminTextLocalizer.UiCulture));
        else
            RelocalizeLedger();
        Notify(
            nameof(MoneyTitle),
            nameof(MoneySubtitle),
            nameof(TodayRevenueLabel),
            nameof(TodayExpensesLabel),
            nameof(TodayNetLabel),
            nameof(ShowingPeriodText),
            nameof(TodayLabel),
            nameof(WeekLabel),
            nameof(MonthLabel),
            nameof(YearLabel),
            nameof(AllLabel),
            nameof(RefreshLabel),
            nameof(QuickEntryLabel),
            nameof(AmountLabel),
            nameof(CurrencyLabel),
            nameof(TypeLabel),
            nameof(CategoryLabel),
            nameof(DateLabel),
            nameof(JustificationLabel),
            nameof(AddEntryLabel),
            nameof(LedgerTitle),
            nameof(ColDateLabel),
            nameof(ColTypeLabel),
            nameof(ColCategoryLabel),
            nameof(ColJustificationLabel),
            nameof(ColAmountLabel),
            nameof(PeriodRevenueLabel),
            nameof(PeriodExpensesLabel),
            nameof(PeriodNetLabel),
            nameof(SummaryLabel),
            nameof(SalesSummaryLine),
            nameof(TipsSummaryLine),
            nameof(DeliveryFeesSummaryLine),
            nameof(PayrollSummaryLine),
            nameof(GenerateReportLabel),
            nameof(LoadingText));
    }

    private void RelocalizeLedger()
    {
        foreach (var row in DailyLedger)
            row.ApplyLocalization();
    }

    private void ApplySnapshot(MoneyDashboardSnapshotData snapshot)
    {
        TodayRevenueText = snapshot.TodayRevenueText;
        TodayExpensesText = snapshot.TodayExpensesText;
        TodayNetProfitText = snapshot.TodayNetProfitText;
        TodayNetProfitColor = snapshot.TodayNetProfitColor;

        OnPropertyChanged(nameof(ShowingPeriodText));
        ReportStartDate = snapshot.ReportStartDate;
        ReportEndDate = snapshot.ReportEndDate;

        DailyLedger.Clear();
        foreach (var row in snapshot.LedgerItems)
        {
            var item = new MoneyLedgerItemViewModel
            {
                Date = row.Date,
                RawType = row.Type,
                RawCategory = row.Category,
                RawJustification = row.Justification,
                RawAmountText = row.AmountText,
                AmountColor = row.AmountColor
            };
            item.ApplyLocalization();
            DailyLedger.Add(item);
        }

        TotalRevenueText = snapshot.TotalRevenueText;
        TotalExpensesText = snapshot.TotalExpensesText;
        NetProfitText = snapshot.NetProfitText;
        NetProfitColor = snapshot.NetProfitColor;
        _salesSummaryValue = snapshot.SalesSummaryText;
        _tipsSummaryValue = snapshot.TipsSummaryText;
        _deliveryFeesSummaryValue = snapshot.DeliveryFeesSummaryText;
        _payrollSummaryValue = snapshot.PayrollSummaryText;
        OnPropertyChanged(nameof(SalesSummaryLine));
        OnPropertyChanged(nameof(TipsSummaryLine));
        OnPropertyChanged(nameof(DeliveryFeesSummaryLine));
        OnPropertyChanged(nameof(PayrollSummaryLine));
    }

    private void RebuildEntrySelectLists()
    {
        RebuildOptionList(EntryTypes, [RevenueType, ExpenseType], AdminTextLocalizer.TranslateMoneyType);
        RebuildOptionList(
            Categories,
            ["Sale", "Salary", "Fixed Cost", "Tip", "Gift", "Variable", "Other"],
            AdminTextLocalizer.TranslateMoneyCategory);
        RebuildOptionList(
            EntryCurrencies,
            [CurrencyHelper.Usd, CurrencyHelper.CongoleseFranc],
            MoneyUiLocalizer.TranslateMoneyCurrency);

        SyncSelectOption(ref _selectedTypeOption, EntryTypes, _selectedType, nameof(SelectedTypeOption));
        SyncSelectOption(ref _selectedCategoryOption, Categories, _selectedCategory, nameof(SelectedCategoryOption));
        SyncSelectOption(ref _selectedCurrencyOption, EntryCurrencies, _selectedCurrency, nameof(SelectedCurrencyOption));
    }

    private static void RebuildOptionList(
        ObservableCollection<LocalizedSelectOption> target,
        IReadOnlyList<string> canonicalValues,
        Func<string?, string> translate)
    {
        target.Clear();
        foreach (var value in canonicalValues)
            target.Add(new LocalizedSelectOption { Value = value, Label = translate(value) });
    }

    private void SyncSelectOption(
        ref LocalizedSelectOption? field,
        IEnumerable<LocalizedSelectOption> options,
        string canonical,
        string propertyName)
    {
        var match = options.FirstOrDefault(o => o.Value.Equals(canonical, StringComparison.OrdinalIgnoreCase))
                    ?? options.FirstOrDefault();
        if (ReferenceEquals(field, match))
            return;
        field = match;
        OnPropertyChanged(propertyName);
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
                MessageBox.Show(
                    Loc.Admin("moneyMsgValidAmount", "Enter a valid amount."),
                    Loc.Admin("moneyMsgEntryTitle", "Money Entry"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
        }

        amount = Math.Round(amount, 2);
        if (amount <= 0)
        {
            MessageBox.Show(
                Loc.Admin("moneyMsgAmountPositive", "Amount must be greater than zero."),
                Loc.Admin("moneyMsgEntryTitle", "Money Entry"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (IsJustificationRequired && string.IsNullOrWhiteSpace(Justification))
        {
            MessageBox.Show(
                Loc.Admin("moneyMsgJustificationRequired", "Justification is required for Variable/Other transactions."),
                Loc.Admin("moneyMsgEntryTitle", "Money Entry"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
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
            MessageBox.Show(
                result.Message,
                Loc.Admin("moneyMsgEntryTitle", "Money Entry"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
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
                var list = txs.ToList();
                var snap = MoneyDashboardSnapshotBuilder.BuildFromTransactions(
                    list,
                    selectedPeriod,
                    MaxLedgerRows,
                    formatCulture: AdminTextLocalizer.UiCulture);
                return (list, snap);
            });
            var completedTask = await Task.WhenAny(snapshotTask, Task.Delay(5000));
            if (completedTask != snapshotTask)
            {
                MessageBox.Show(
                    Loc.Admin("moneyMsgLoadTimeout", "Money dashboard timed out while loading data. Please try Refresh again."),
                    Loc.Admin("navMoney", "Money"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var (transactions, snapshot) = await snapshotTask;
            _cachedTransactions = transactions;
            _cachedPeriod = selectedPeriod;
            ApplySnapshot(snapshot);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                Loc.Admin("moneyMsgLoadFailed", "Money dashboard failed to load:\n{{message}}",
                    new Dictionary<string, string> { ["message"] = ex.Message }),
                Loc.Admin("navMoney", "Money"),
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
            Title = Loc.Admin("moneyMsgSavePdfTitle", "Save Financial Report"),
            Filter = "PDF files (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            AddExtension = true,
            FileName = $"money-report-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.pdf"
        };

        if (fileDialog.ShowDialog() != true)
            return;

        MoneyFinancialPdfExportService.ExportLedgerPdf(fileDialog.FileName, fromDate, toDate, rangeEndExclusive);

        MessageBox.Show(
            Loc.Admin("moneyMsgPdfExported", "Financial PDF exported:\n{{path}}",
                new Dictionary<string, string> { ["path"] = fileDialog.FileName }),
            Loc.Admin("moneyMsgReportTitle", "Money Report"),
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
            MessageBox.Show(
                Loc.Admin("moneyMsgExcelSingleType", "Select a single report type for this export. Use Bulk Export for all reports."),
                Loc.Admin("moneyMsgExcelExportTitle", "Excel Export"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var (fromDate, toDate, rangeEndExclusive) = range.Value;
        var saveDialog = new SaveFileDialog
        {
            Title = Loc.Admin("moneyMsgSaveExcelTitle", "Save Excel Report"),
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

        MessageBox.Show(
            Loc.Admin("moneyMsgExcelExported", "Excel exported:\n{{path}}",
                new Dictionary<string, string> { ["path"] = saveDialog.FileName }),
            Loc.Admin("moneyMsgExcelExportTitle", "Excel Export"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async Task BulkExportExcelWorkbookAsync()
    {
        var range = TryGetReportRange();
        if (range is null)
            return;

        var (fromDate, toDate, rangeEndExclusive) = range.Value;
        var saveDialog = new SaveFileDialog
        {
            Title = Loc.Admin("moneyMsgSaveBulkExcelTitle", "Save Bulk Excel Report"),
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
            Loc.Admin("moneyMsgBulkExported", "Bulk Excel report exported:\n{{path}}",
                new Dictionary<string, string> { ["path"] = saveDialog.FileName }),
            Loc.Admin("moneyMsgBulkExportTitle", "Bulk Export"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private (DateTime FromDate, DateTime ToDate, DateTime ToExclusive)? TryGetReportRange()
    {
        var fromDate = ReportStartDate.Date;
        var toDate = ReportEndDate.Date;
        if (toDate < fromDate)
        {
            MessageBox.Show(
                Loc.Admin("moneyMsgReportRange", "End date must be after start date."),
                Loc.Admin("moneyMsgReportRangeTitle", "Report Range"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
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
