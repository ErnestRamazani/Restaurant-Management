using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.Services;
using EliteRestaurantPro.Views;
using ModelTable = EliteRestaurant.Core.Models.Table;

namespace EliteRestaurantPro.ViewModels;

/// <summary>Create-order screen: orchestrates <see cref="TableLoadingService"/>, <see cref="DraftPersistenceService"/>, <see cref="OrderTotalsCalculator"/>, and <see cref="OrderSubmissionService"/>.</summary>
public sealed class CreateOrderViewModel : AdminBaseViewModel
{
    private readonly OrderTotalsCalculator _totalsCalculator = new();
    private readonly TableLoadingService _tableLoading = new();
    private readonly DraftPersistenceService _draftPersistence = new();
    private readonly OrderSubmissionService _orderSubmission = new();
    private readonly AdminDataApiClient _cloudData = new();

    public sealed class DraftEntry
    {
        public string FilePath { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public override string ToString() => DisplayName;
    }

    private static DraftEntry EmptyDraftOption => new() { FilePath = string.Empty, DisplayName = "None (empty slot)" };

    private int _selectedTableId;
    private string _selectedOrderStatus = "Waiting";
    private string _selectedOrderCategory = "All";
    private string _selectedOrderSubCategory = "All";
    private string _productSearchText = string.Empty;
    private DraftEntry? _selectedDraft;
    private string _customerNotes = string.Empty;
    private string _allergyNotes = string.Empty;
    private bool _isLoading;
    private bool _isSubmitting;
    private decimal _liveSubtotal;
    private decimal _liveTaxAmount;
    private decimal _liveServiceAmount;
    private decimal _liveGrandTotal;
    private decimal _liveDiscountAmount;
    private string _selectedDiscountMode = "None";
    private string _discountInput = string.Empty;
    private string _liveDiscountLabel = string.Empty;
    private int _liveItemCount;
    private int _estimatedPrepMinutes;
    private readonly int? _serverEmployeeId;
    private readonly string _serverEmployeeName = string.Empty;
    private int? _openCheckOrderId;
    private string _openCheckCode = string.Empty;
    private string _openCheckStatus = string.Empty;
    private bool _suppressOpenCheckRefresh;
    private bool _suppressSelectionChanged;
    /// <summary>When true, live totals exclude the persisted open-check subtotal (e.g. user chose "separate ticket" while a check exists).</summary>
    private bool _skipPersistedSubtotalInTotals;
    /// <summary>Resolved via cloud API during load for admin draft ownership (never block UI synchronously).</summary>
    private int? _resolvedDraftOwnerEmployeeId;
    /// <summary>Subtotal for the open check's existing lines; refreshed with open-check banner.</summary>
    private decimal _persistedOpenOrderLineSubtotal;

    public override string ActivePage => "CreateOrder";
    public string PageTitle => "Create Order";
    public string PageSubtitle =>
        IsTabletStaffOrderFlow
            ? "Shared order pad for admin/server/cashier. If table already has an open check, you can append lines to the same ticket."
            : "Create and manage table tickets with live totals, discounts, and open-check append support.";

    public bool IsTabletStaffOrderFlow => AppSession.IsServerTablet || AppSession.IsCashierTablet;
    public bool CanEditTablePicker => !AppSession.IsServerTablet || AvailableTables.Count > 1;
    public bool CanEditTableForCurrentSource => CanEditTablePicker;
    public bool CanEditOrderStatusPicker => !AppSession.IsStaffTablet;
    public bool HasOpenCheckForTable => _openCheckOrderId.HasValue;
    public string OpenCheckBannerText =>
        HasOpenCheckForTable
            ? $"Open check {_openCheckCode} ({_openCheckStatus}) exists for this table. Submit will ask to append or create a separate ticket."
            : string.Empty;
    public string PrimaryActionLabel => IsTabletStaffOrderFlow ? "Send to cashier" : "Create Order";

    public ObservableCollection<ModelTable> AvailableTables { get; } = new();
    public ObservableCollection<string> OrderStatuses { get; } = new(["Waiting", "In Kitchen", "Ready"]);
    public ObservableCollection<string> OrderCategories { get; } = new();
    public ObservableCollection<string> OrderSubCategories { get; } = new();
    public ObservableCollection<string> DiscountModes { get; } = new(["None", "Percent", "Usd"]);
    public ObservableCollection<ProductSelectionItemViewModel> ProductSelections { get; } = new();
    public ObservableCollection<ProductSelectionItemViewModel> FilteredProductSelections { get; } = new();
    public ObservableCollection<ProductSelectionItemViewModel> SelectedProductSelections { get; } = new();
    public ObservableCollection<DraftEntry> SavedDrafts { get; } = new();

    public int SelectedTableId
    {
        get => _selectedTableId;
        set
        {
            if (!SetField(ref _selectedTableId, value))
                return;
            // New table → show live totals including that table's open check (if any).
            _skipPersistedSubtotalInTotals = false;
            if (!_suppressOpenCheckRefresh)
                RefreshOpenCheckBanner();
            RefreshSavedDrafts();
        }
    }

    public string SelectedOrderStatus
    {
        get => _selectedOrderStatus;
        set => SetField(ref _selectedOrderStatus, value);
    }

    public string SelectedOrderCategory
    {
        get => _selectedOrderCategory;
        set
        {
            if (!SetField(ref _selectedOrderCategory, value))
                return;
            RebuildSubCategoryFilter();
            ApplyProductFilters();
        }
    }

    public string SelectedOrderSubCategory
    {
        get => _selectedOrderSubCategory;
        set
        {
            if (!SetField(ref _selectedOrderSubCategory, value))
                return;
            ApplyProductFilters();
        }
    }

    public string ProductSearchText
    {
        get => _productSearchText;
        set
        {
            if (!SetField(ref _productSearchText, value))
                return;
            ApplyProductFilters();
        }
    }

    public DraftEntry? SelectedDraft
    {
        get => _selectedDraft;
        set => SetField(ref _selectedDraft, value);
    }

    public string CustomerNotes
    {
        get => _customerNotes;
        set => SetField(ref _customerNotes, value);
    }

    public string AllergyNotes
    {
        get => _allergyNotes;
        set => SetField(ref _allergyNotes, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (!SetField(ref _isLoading, value))
                return;
            OnPropertyChanged(nameof(CanSubmitCreateOrder));
        }
    }

    public bool CanSubmitCreateOrder => !IsLoading && !_isSubmitting;

    public decimal LiveSubtotal
    {
        get => _liveSubtotal;
        private set => SetField(ref _liveSubtotal, value);
    }

    public decimal LiveTaxAmount
    {
        get => _liveTaxAmount;
        private set => SetField(ref _liveTaxAmount, value);
    }

    public decimal LiveServiceAmount
    {
        get => _liveServiceAmount;
        private set => SetField(ref _liveServiceAmount, value);
    }

    public decimal LiveGrandTotal
    {
        get => _liveGrandTotal;
        private set => SetField(ref _liveGrandTotal, value);
    }

    public decimal LiveGrandTotalFc => CurrencyHelper.ConvertUsdToFc(LiveGrandTotal);
    public string LiveGrandTotalUsdText => CurrencyHelper.FormatAmount(LiveGrandTotal, CurrencyHelper.Usd);
    public string LiveGrandTotalFcText => CurrencyHelper.FormatAmount(LiveGrandTotalFc, CurrencyHelper.CongoleseFranc);
    public string LiveTaxRateLabel
    {
        get
        {
            var pct = SettingsManager.Load().CurrencyPricing.TaxPercent;
            return $"TVA ({pct:0.##}%)";
        }
    }

    public string LiveServiceRateLabel
    {
        get
        {
            var pct = SettingsManager.Load().CurrencyPricing.ServicePercent;
            return $"Service ({pct:0.##}%)";
        }
    }

    public decimal LiveDiscountAmount
    {
        get => _liveDiscountAmount;
        private set => SetField(ref _liveDiscountAmount, value);
    }

    public string SelectedDiscountMode
    {
        get => _selectedDiscountMode;
        set
        {
            if (!SetField(ref _selectedDiscountMode, value))
                return;
            RecalculateTotals();
        }
    }

    public string DiscountInput
    {
        get => _discountInput;
        set
        {
            if (!SetField(ref _discountInput, value))
                return;
            RecalculateTotals();
        }
    }

    public string LiveDiscountLabel
    {
        get => _liveDiscountLabel;
        private set => SetField(ref _liveDiscountLabel, value);
    }

    public string LiveDiscountSummary =>
        LiveDiscountAmount <= 0m
            ? "No discount applied."
            : $"{LiveDiscountLabel}: -{CurrencyHelper.FormatAmount(LiveDiscountAmount, CurrencyHelper.Usd)}";

    /// <summary>Label for the subtotal line — reflects whether totals include the existing open check.</summary>
    public string SubtotalCaption
    {
        get
        {
            if (!ProductSelections.Any(p => p.IsSelected))
                return "Subtotal (items): $ ";
            if (!_skipPersistedSubtotalInTotals && HasOpenCheckForTable)
                return "Ticket subtotal (existing check + new lines): $ ";
            return "Subtotal (items): $ ";
        }
    }

    public int LiveItemCount
    {
        get => _liveItemCount;
        private set => SetField(ref _liveItemCount, value);
    }

    public int EstimatedPrepMinutes
    {
        get => _estimatedPrepMinutes;
        private set => SetField(ref _estimatedPrepMinutes, value);
    }

    public string EstimatedPrepText => EstimatedPrepMinutes <= 0 ? "-" : $"{EstimatedPrepMinutes} min";

    public string ChosenPaymentAmountText =>
        CurrencyHelper.FormatAmount(LiveGrandTotal, CurrencyHelper.Usd);

    public ICommand CreateOrderCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public ICommand ToggleProductSelectionCommand { get; }
    public ICommand IncreaseQuantityCommand { get; }
    public ICommand DecreaseQuantityCommand { get; }
    public ICommand RemoveSelectedLineCommand { get; }
    public ICommand SaveDraftCommand { get; }
    public ICommand LoadDraftCommand { get; }
    public ICommand DeleteDraftCommand { get; }
    public ICommand DeleteAllDraftsCommand { get; }

    public CreateOrderViewModel(Action<BaseViewModel> navigate) : base(navigate)
    {
        SettingsManager.SettingsChanged += OnAppSettingsChanged;
        if (AppSession.IsServerTablet && AppSession.StaffEmployeeId is int sid)
        {
            _serverEmployeeId = sid;
            _serverEmployeeName = AppSession.StaffEmployeeName;
        }

        CreateOrderCommand = new RelayCommand(_ => CreateOrder(), _ => CanSubmitCreateOrder);
        ClearSelectionCommand = new RelayCommand(_ => ClearSelection());
        ToggleProductSelectionCommand = new RelayCommand(item => ToggleProductSelection(item as ProductSelectionItemViewModel));
        IncreaseQuantityCommand = new RelayCommand(item => IncreaseQuantity(item as ProductSelectionItemViewModel));
        DecreaseQuantityCommand = new RelayCommand(item => DecreaseQuantity(item as ProductSelectionItemViewModel));
        RemoveSelectedLineCommand = new RelayCommand(item => RemoveSelectedLine(item as ProductSelectionItemViewModel));
        SaveDraftCommand = new RelayCommand(_ => SaveDraft());
        LoadDraftCommand = new RelayCommand(_ => LoadSelectedDraft());
        DeleteDraftCommand = new RelayCommand(_ => DeleteSelectedDraft());
        DeleteAllDraftsCommand = new RelayCommand(_ => DeleteAllDrafts());

        _ = LoadDataAsync();
    }

    private void OnAppSettingsChanged()
    {
        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            RecalculateTotals();
            OnPropertyChanged(nameof(LiveTaxRateLabel));
            OnPropertyChanged(nameof(LiveServiceRateLabel));
        }));
    }

    private static Window? DialogOwner() =>
        Application.Current?.Windows.OfType<Window>().FirstOrDefault(static w => w.IsActive)
        ?? Application.Current?.MainWindow;

    private static MessageBoxResult ShowDialog(string text, string caption, MessageBoxButton button, MessageBoxImage icon)
    {
        var owner = DialogOwner();
        return owner is null
            ? MessageBox.Show(text, caption, button, icon)
            : MessageBox.Show(owner, text, caption, button, icon);
    }

    private void SetSubmitting(bool value)
    {
        if (_isSubmitting == value)
            return;
        _isSubmitting = value;
        OnPropertyChanged(nameof(CanSubmitCreateOrder));
    }

    private async Task LoadDataAsync()
    {
        var shouldRun = await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (IsLoading)
                return false;
            IsLoading = true;
            return true;
        });

        if (!shouldRun)
            return;

        try
        {
            _resolvedDraftOwnerEmployeeId = null;
            var catalogTask = _tableLoading.LoadCatalogAsync(_serverEmployeeId);
            Task<IReadOnlyList<Employee>>? employeesTask = null;
            string? draftOwnerCandidateName = null;
            if (!_serverEmployeeId.HasValue && !AppSession.StaffEmployeeId.HasValue)
            {
                draftOwnerCandidateName = ResolveDraftOwnerName();
                if (!string.IsNullOrWhiteSpace(draftOwnerCandidateName)
                    && !string.Equals(draftOwnerCandidateName, "Server", StringComparison.OrdinalIgnoreCase))
                    employeesTask = _cloudData.GetEmployeesAsync();
            }

            if (employeesTask is not null)
                await Task.WhenAll(catalogTask, employeesTask).ConfigureAwait(false);
            else
                await catalogTask.ConfigureAwait(false);

            if (employeesTask is not null)
            {
                try
                {
                    var employees = await employeesTask.ConfigureAwait(false);
                    var candidateName = draftOwnerCandidateName!;
                    _resolvedDraftOwnerEmployeeId = employees
                        .Where(e => e.EmploymentStatus == "Active")
                        .Where(e =>
                            e.Name == candidateName
                            || e.SignInId == candidateName
                            || e.UniqueId == candidateName)
                        .Select(e => (int?)e.Id)
                        .FirstOrDefault();
                }
                catch
                {
                    _resolvedDraftOwnerEmployeeId = null;
                }
            }

            _skipPersistedSubtotalInTotals = false;
            var catalog = await catalogTask.ConfigureAwait(false);

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                AvailableTables.Clear();
                foreach (var t in catalog.Tables)
                {
                    AvailableTables.Add(new ModelTable
                    {
                        Id = t.Id,
                        UniqueId = t.UniqueId,
                        TableNumber = t.TableNumber,
                        Name = t.Name,
                        Capacity = t.Capacity,
                        Status = t.Status,
                        AssignedServerId = t.AssignedServerId
                    });
                }

                ProductSelections.Clear();
                foreach (var p in catalog.Products)
                {
                    var vm = new ProductSelectionItemViewModel
                    {
                        ProductId = p.ProductId,
                        UniqueId = p.UniqueId,
                        Name = p.Name,
                        Category = p.Category,
                        SubCategory = p.SubCategory,
                        Price = p.Price,
                        Quantity = 1,
                        IsAvailable = p.IsAvailable
                    };
                    vm.PropertyChanged += OnSelectionChanged;
                    ProductSelections.Add(vm);
                }

                _suppressOpenCheckRefresh = true;
                try
                {
                    SelectedTableId = AvailableTables.FirstOrDefault()?.Id ?? 0;
                }
                finally
                {
                    _suppressOpenCheckRefresh = false;
                }

                if (IsTabletStaffOrderFlow)
                {
                    OrderStatuses.Clear();
                    OrderStatuses.Add(OrderWorkflow.PendingCashier);
                    SelectedOrderStatus = OrderWorkflow.PendingCashier;
                }
                else if (!OrderStatuses.Contains(SelectedOrderStatus))
                {
                    SelectedOrderStatus = OrderStatuses.First();
                }

                RebuildCategoryFilter();
                RebuildSubCategoryFilter();
                ApplyProductFilters();
                RefreshSavedDrafts();
                var productPrices = ProductSelections.ToDictionary(p => p.ProductId, p => p.Price);
                ApplyOpenCheckUi(SelectedTableId, catalog.OrdersSnapshot, productPrices);
                RefreshReadyPickupBanner();
                OnPropertyChanged(nameof(CanEditOrderStatusPicker));
                OnPropertyChanged(nameof(CanEditTablePicker));
                OnPropertyChanged(nameof(CanEditTableForCurrentSource));
            });
        }
        catch (Exception ex)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
                ShowDialog($"Create Order failed to load:\n\n{ex.Message}", "Create Order", MessageBoxButton.OK, MessageBoxImage.Error));
        }
        finally
        {
            await Application.Current.Dispatcher.InvokeAsync(() => { IsLoading = false; });
        }
    }

    private void RefreshOpenCheckBanner()
    {
        _ = RefreshOpenCheckBannerAsync();
    }

    private static OrderRecord? FindOpenCheckForTable(IReadOnlyList<OrderRecord> orders, int tableId)
    {
        if (tableId == 0)
            return null;
        return orders
            .Where(o => o.TableId == tableId && OrderWorkflow.IsOpenCheckStatus(o.Status))
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault();
    }

    private static decimal ComputePersistedOpenLineSubtotal(OrderRecord? open, IReadOnlyDictionary<int, decimal> productPrices)
    {
        var items = open?.Items?.ToList() ?? [];
        if (items.Count == 0)
            return 0m;
        return items.Sum(i =>
            (productPrices.TryGetValue(i.ProductId, out var price) ? price : 0m) * i.Quantity);
    }

    /// <summary>UI thread: applies open-check banner, discount fields from server order, and totals.</summary>
    private void ApplyOpenCheckUi(int tableId, IReadOnlyList<OrderRecord> orders, IReadOnlyDictionary<int, decimal> productPrices)
    {
        if (tableId == 0)
        {
            _openCheckOrderId = null;
            _openCheckCode = string.Empty;
            _openCheckStatus = string.Empty;
            _persistedOpenOrderLineSubtotal = 0m;
            OnPropertyChanged(nameof(HasOpenCheckForTable));
            OnPropertyChanged(nameof(OpenCheckBannerText));
            ApplyDiscountFieldsFromOpen(null);
            RecalculateTotals();
            OnPropertyChanged(nameof(SubtotalCaption));
            return;
        }

        var open = FindOpenCheckForTable(orders, tableId);
        _openCheckOrderId = open?.Id;
        _openCheckCode = open is null
            ? string.Empty
            : string.IsNullOrWhiteSpace(open.UniqueId) ? $"#{open.Id:000}" : open.UniqueId;
        _openCheckStatus = open?.Status ?? string.Empty;
        _persistedOpenOrderLineSubtotal = ComputePersistedOpenLineSubtotal(open, productPrices);

        OnPropertyChanged(nameof(HasOpenCheckForTable));
        OnPropertyChanged(nameof(OpenCheckBannerText));
        ApplyDiscountFieldsFromOpen(open);
        RecalculateTotals();
        OnPropertyChanged(nameof(SubtotalCaption));
    }

    private async Task RefreshOpenCheckBannerAsync()
    {
        var (tableId, productPrices) = await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var prices = ProductSelections.ToDictionary(p => p.ProductId, p => p.Price);
            return (SelectedTableId, (IReadOnlyDictionary<int, decimal>)prices);
        });

        if (tableId == 0)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
                ApplyOpenCheckUi(0, Array.Empty<OrderRecord>(), productPrices));
            return;
        }

        try
        {
            var orders = await _cloudData.GetOrdersAsync().ConfigureAwait(false);
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (SelectedTableId != tableId)
                    return;
                ApplyOpenCheckUi(tableId, orders, productPrices);
            });
        }
        catch
        {
            // Best-effort: leave prior open-check state if refresh fails.
        }
    }

    private void ApplyDiscountFieldsFromOpen(OrderRecord? open)
    {
        string mode;
        string input;
        if (open is null || string.Equals(open.DiscountMode, "None", StringComparison.OrdinalIgnoreCase) || open.DiscountValue <= 0m)
        {
            mode = "None";
            input = string.Empty;
        }
        else if (string.Equals(open.DiscountMode, "Percent", StringComparison.OrdinalIgnoreCase))
        {
            mode = "Percent";
            input = open.DiscountValue.ToString("0.##", CultureInfo.InvariantCulture);
        }
        else if (string.Equals(open.DiscountMode, "Usd", StringComparison.OrdinalIgnoreCase))
        {
            mode = "Usd";
            input = open.DiscountValue.ToString("0.##", CultureInfo.InvariantCulture);
        }
        else
        {
            mode = "None";
            input = string.Empty;
        }

        if (string.Equals(_selectedDiscountMode, mode, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(_discountInput, input, StringComparison.Ordinal))
            return;

        _selectedDiscountMode = mode;
        _discountInput = input;
        OnPropertyChanged(nameof(SelectedDiscountMode));
        OnPropertyChanged(nameof(DiscountInput));
    }

    private decimal GetPersistedOpenOrderLineSubtotal()
    {
        if (_skipPersistedSubtotalInTotals || _openCheckOrderId is null)
            return 0m;
        return _persistedOpenOrderLineSubtotal;
    }

    private void RebuildCategoryFilter()
    {
        OrderCategories.Clear();
        OrderCategories.Add("All");
        foreach (var c in ProductSelections
                     .Select(p => p.Category)
                     .Where(c => !string.IsNullOrWhiteSpace(c))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(c => c))
        {
            OrderCategories.Add(c);
        }

        if (!OrderCategories.Contains(SelectedOrderCategory))
            SelectedOrderCategory = "All";
    }

    private void RebuildSubCategoryFilter()
    {
        var sub = ProductSelections
            .Where(p => SelectedOrderCategory == "All" || p.Category.Equals(SelectedOrderCategory, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.SubCategory)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();

        OrderSubCategories.Clear();
        OrderSubCategories.Add("All");
        foreach (var s in sub)
            OrderSubCategories.Add(s);

        if (!OrderSubCategories.Contains(SelectedOrderSubCategory))
            SelectedOrderSubCategory = "All";
    }

    private void ApplyProductFilters()
    {
        var search = ProductSearchText.Trim();
        var filtered = ProductSelections
            .Where(p => SelectedOrderCategory == "All" || p.Category.Equals(SelectedOrderCategory, StringComparison.OrdinalIgnoreCase))
            .Where(p => SelectedOrderSubCategory == "All" || p.SubCategory.Equals(SelectedOrderSubCategory, StringComparison.OrdinalIgnoreCase))
            .Where(p => string.IsNullOrWhiteSpace(search)
                        || p.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                        || p.UniqueId.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Category)
            .ThenBy(p => p.SubCategory)
            .ThenBy(p => p.Name)
            .ToList();

        FilteredProductSelections.Clear();
        foreach (var f in filtered)
            FilteredProductSelections.Add(f);
    }

    private void OnSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressSelectionChanged)
            return;
        if (e.PropertyName is nameof(ProductSelectionItemViewModel.IsSelected) or nameof(ProductSelectionItemViewModel.Quantity))
            RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        var selected = ProductSelections.Where(p => p.IsSelected).ToList();
        var newLinesSubtotal = selected.Sum(s => s.LineTotal);
        // No selection → live panel shows $0 (open-check totals belong in the banner, not as a phantom grand total).
        decimal ticketSubtotal;
        if (selected.Count == 0)
            ticketSubtotal = 0m;
        else if (_skipPersistedSubtotalInTotals)
            ticketSubtotal = newLinesSubtotal;
        else
            ticketSubtotal = GetPersistedOpenOrderLineSubtotal() + newLinesSubtotal;
        LiveSubtotal = ticketSubtotal;

        var prepLines = selected.Select(s => (s.Quantity, s.Category, s.SubCategory)).ToList();
        var ticket = _totalsCalculator.ComputeTicket(
            ticketSubtotal,
            selected.Sum(s => s.Quantity),
            SelectedDiscountMode,
            DiscountInput,
            prepLines);
        LiveItemCount = ticket.LiveItemCount;
        LiveDiscountAmount = ticket.DiscountApplied;
        LiveDiscountLabel = ticket.DiscountLabel;
        LiveTaxAmount = ticket.TaxAmount;
        LiveServiceAmount = ticket.ServiceAmount;
        LiveGrandTotal = ticket.GrandTotal;
        EstimatedPrepMinutes = ticket.EstimatedPrepMinutes;

        SelectedProductSelections.Clear();
        foreach (var row in selected.OrderBy(s => s.Name))
            SelectedProductSelections.Add(row);

        OnPropertyChanged(nameof(SubtotalCaption));
        OnPropertyChanged(nameof(LiveDiscountSummary));
        OnPropertyChanged(nameof(EstimatedPrepText));
        OnPropertyChanged(nameof(LiveGrandTotalFc));
        OnPropertyChanged(nameof(LiveGrandTotalUsdText));
        OnPropertyChanged(nameof(LiveGrandTotalFcText));
        OnPropertyChanged(nameof(LiveTaxRateLabel));
        OnPropertyChanged(nameof(LiveServiceRateLabel));
        OnPropertyChanged(nameof(ChosenPaymentAmountText));
    }

    private void IncreaseQuantity(ProductSelectionItemViewModel? item)
    {
        if (item is null || !item.IsAvailable)
            return;
        item.Quantity += 1;
        item.IsSelected = true;
    }

    private void DecreaseQuantity(ProductSelectionItemViewModel? item)
    {
        if (item is null) return;
        item.Quantity = Math.Max(1, item.Quantity - 1);
    }

    private void RemoveSelectedLine(ProductSelectionItemViewModel? item)
    {
        if (item is null)
            return;
        item.Quantity = 1;
        item.IsSelected = false;
    }

    private void ToggleProductSelection(ProductSelectionItemViewModel? item)
    {
        if (item is null)
            return;
        if (!item.IsAvailable && !item.IsSelected)
            return;
        item.IsSelected = !item.IsSelected;
    }

    private CreateOrderSubmitSnapshot BuildSubmitSnapshot(List<ProductSelectionItemViewModel> selected) =>
        new(
            SelectedTableId,
            selected.Select(s => (s.ProductId, s.Quantity)).ToList(),
            CustomerNotes,
            AllergyNotes,
            SelectedDiscountMode,
            DiscountInput,
            LiveDiscountAmount,
            LiveSubtotal,
            LiveGrandTotal,
            LiveGrandTotalFc,
            LiveDiscountLabel,
            LiveGrandTotalUsdText,
            LiveGrandTotalFcText,
            CurrencyHelper.Usd,
            ChosenPaymentAmountText,
            EstimatedPrepText,
            SelectedOrderStatus,
            "WalkIn",
            string.Empty,
            IsTabletStaffOrderFlow,
            _serverEmployeeId,
            _serverEmployeeName,
            string.Empty,
            string.Empty,
            null);

    private void CreateOrder()
    {
        _ = CreateOrderAsync();
    }

    private async Task CreateOrderAsync()
    {
        if (IsLoading || _isSubmitting)
            return;

        var selected = ProductSelections.Where(p => p.IsSelected).ToList();
        if (selected.Count == 0)
        {
            ShowDialog("Select at least one menu item.", "Create Order", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (SelectedTableId == 0)
        {
            ShowDialog("Select a table for this order.", "Create Order", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SetSubmitting(true);
        try
        {
            var snap = BuildSubmitSnapshot(selected);
            var phase = await _orderSubmission.LoadPhase1Async(snap).ConfigureAwait(true);
            if (!phase.Ok)
            {
                ShowDialog(phase.Message, phase.Caption, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (phase.OpenCheck.OrderId is int openOrderId)
            {
                var newLinesSubtotal = selected.Sum(s => s.LineTotal);
                var openDlg = new OpenCheckChoiceDialog(
                    phase.TableNumber,
                    phase.TableName,
                    phase.OpenCheck.Code,
                    phase.OpenCheck.Status,
                    snap.SelectedLines.Count,
                    newLinesSubtotal)
                {
                    Owner = DialogOwner(),
                };
                openDlg.ShowDialog();

                if (openDlg.Choice == OpenCheckChoice.Cancel)
                    return;
                if (openDlg.Choice == OpenCheckChoice.AppendToSameTicket)
                {
                    var append = await _orderSubmission.AppendToExistingAsync(snap, openOrderId).ConfigureAwait(true);
                    if (!append.Ok)
                    {
                        var appendImage = string.Equals(append.Caption, "Cloud API", StringComparison.OrdinalIgnoreCase)
                            ? MessageBoxImage.Error
                            : MessageBoxImage.Warning;
                        ShowDialog(append.Message, append.Caption, MessageBoxButton.OK, appendImage);
                        RefreshOpenCheckBanner();
                        return;
                    }

                    ShowDialog(append.Message, append.Caption, MessageBoxButton.OK, MessageBoxImage.Information);
                    ClearSelection(excludeOpenCheckFromLiveTotals: false);
                    RefreshOpenCheckBanner();
                    RefreshReadyPickupBanner();
                    return;
                }

                _skipPersistedSubtotalInTotals = true;
                RecalculateTotals();
                OnPropertyChanged(nameof(SubtotalCaption));
                snap = BuildSubmitSnapshot(selected);
            }

            var discountRaw = OrderDiscountParser.Parse(snap.DiscountInput);
            if (string.Equals(snap.DiscountMode, "Percent", StringComparison.OrdinalIgnoreCase) &&
                (discountRaw <= 0m || discountRaw > 100m))
            {
                ShowDialog("Enter a discount percent between 0 and 100.", "Create Order", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.Equals(snap.DiscountMode, "Usd", StringComparison.OrdinalIgnoreCase) && discountRaw <= 0m)
            {
                ShowDialog("Enter a discount amount greater than zero (USD).", "Create Order", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var discountLine = snap.LiveDiscountAmount > 0m
                ? $"\n{snap.LiveDiscountLabel}: -{CurrencyHelper.FormatAmount(snap.LiveDiscountAmount, CurrencyHelper.Usd)}"
                : string.Empty;

            var sourceLine = $"Create walk-in order for Table {phase.TableNumber} ({phase.TableName})";

            var detailsBlock =
                $"Subtotal: {CurrencyHelper.FormatAmount(snap.LiveSubtotal, CurrencyHelper.Usd)}{discountLine}\n" +
                $"Grand Total: {snap.LiveGrandTotalUsdText}\n" +
                $"Equivalent FC: {snap.LiveGrandTotalFcText}\n" +
                $"Amount To Collect: {snap.ChosenPaymentAmountText}\n" +
                $"Estimated Prep: {snap.EstimatedPrepText}";

            var confirmDlg = new ConfirmCreateOrderDialog(
                    snap.IsTabletStaffOrderFlow,
                    $"{sourceLine} with {snap.SelectedLines.Count} selected item(s)?",
                    detailsBlock)
                { Owner = DialogOwner() };

            if (confirmDlg.ShowDialog() != true)
                return;

            var save = await _orderSubmission.SaveNewAsync(snap).ConfigureAwait(true);
            if (!save.Ok)
            {
                var saveImage = string.Equals(save.Caption, "Cloud API", StringComparison.OrdinalIgnoreCase)
                    ? MessageBoxImage.Error
                    : MessageBoxImage.Warning;
                ShowDialog(save.Message, save.Caption, MessageBoxButton.OK, saveImage);
                return;
            }

            ShowDialog(save.Message, save.Caption, MessageBoxButton.OK, MessageBoxImage.Information);
            ClearSelection(excludeOpenCheckFromLiveTotals: false);
            RefreshOpenCheckBanner();
            RefreshReadyPickupBanner();
        }
        catch (Exception ex)
        {
            ShowDialog($"Create order could not be completed.\n\n{ex.Message}", "Create Order", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetSubmitting(false);
        }
    }

    /// <param name="excludeOpenCheckFromLiveTotals">
    /// When <c>true</c> (Clear button), live totals show only newly selected lines so staff can start a separate ticket mentally.
    /// When <c>false</c> (after a successful submit), idle totals include the table's open check again.
    /// </param>
    private void ClearSelection(bool excludeOpenCheckFromLiveTotals = true)
    {
        _skipPersistedSubtotalInTotals = excludeOpenCheckFromLiveTotals;
        _suppressSelectionChanged = true;
        try
        {
            foreach (var item in ProductSelections)
            {
                item.IsSelected = false;
                item.Quantity = 1;
            }
        }
        finally
        {
            _suppressSelectionChanged = false;
        }

        ProductSearchText = string.Empty;
        CustomerNotes = string.Empty;
        AllergyNotes = string.Empty;
        RefreshOpenCheckBanner();
        RecalculateTotals();
        OnPropertyChanged(nameof(SubtotalCaption));
        ApplyProductFilters();
    }

    private void SaveDraft()
    {
        var ownerId = ResolveDraftOwnerEmployeeId();
        if (!ownerId.HasValue)
        {
            ShowDialog("Drafts are available for signed-in server sessions only.", "Create Order", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var table = AvailableTables.FirstOrDefault(t => t.Id == SelectedTableId);
        var selectedCount = ProductSelections.Where(p => p.IsSelected).Sum(p => p.Quantity);
        var tableLabel = table is null ? "No Table" : $"Table {table.TableNumber}";

        var draft = new CreateOrderDraftPayload
        {
            DraftLabel = $"{DateTime.Now:dd MMM HH:mm:ss} | {tableLabel} | {selectedCount} items | {SelectedOrderStatus}",
            SelectedTableId = SelectedTableId,
            SelectedOrderSource = "WalkIn",
            SelectedDeliveryReference = string.Empty,
            SelectedReservationCode = string.Empty,
            SelectedOrderStatus = SelectedOrderStatus,
            SelectedOrderCategory = SelectedOrderCategory,
            SelectedOrderSubCategory = SelectedOrderSubCategory,
            ProductSearchText = ProductSearchText,
            CustomerNotes = CustomerNotes,
            AllergyNotes = AllergyNotes,
            SelectedPaymentCurrency = CurrencyHelper.Usd,
            DiscountMode = SelectedDiscountMode,
            DiscountInput = DiscountInput,
            Items = ProductSelections.Where(p => p.IsSelected)
                .Select(p => new CreateOrderDraftItemPayload { ProductId = p.ProductId, Quantity = p.Quantity })
                .ToList()
        };

        var saved = _draftPersistence.Save(ownerId.Value, ResolveDraftOwnerName(), draft);
        RefreshSavedDrafts();
        SelectedDraft = SavedDrafts.FirstOrDefault(d => d.FilePath == saved.Id) ?? EmptyDraftOption;
        ShowDialog("Draft saved.", "Create Order", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void LoadSelectedDraft()
    {
        if (!LoadDraft(SelectedDraft, showMessage: true, autoDeleteAfterLoad: false))
            ShowDialog("No saved draft found.", "Create Order", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private bool LoadDraft(DraftEntry? entry, bool showMessage, bool autoDeleteAfterLoad)
    {
        if (entry is null || string.IsNullOrWhiteSpace(entry.FilePath))
            return false;

        var ownerId = ResolveDraftOwnerEmployeeId();
        if (!ownerId.HasValue)
            return false;

        if (!DraftPersistenceService.TryGetPayload(ownerId.Value, entry.FilePath, SelectedTableId, AppSession.IsServerTablet, out var draftRow) || draftRow is null)
            return false;

        var draft = DraftPersistenceService.Deserialize(draftRow.PayloadJson);
        if (draft is null)
            return false;

        SelectedTableId = draft.SelectedTableId;
        SelectedOrderStatus = string.IsNullOrWhiteSpace(draft.SelectedOrderStatus) ? "Waiting" : draft.SelectedOrderStatus;
        // Keep full menu visible after loading a draft.
        SelectedOrderCategory = "All";
        SelectedOrderSubCategory = "All";
        ProductSearchText = string.Empty;
        CustomerNotes = draft.CustomerNotes ?? string.Empty;
        AllergyNotes = draft.AllergyNotes ?? string.Empty;
        SelectedDiscountMode = string.IsNullOrWhiteSpace(draft.DiscountMode) ? "None" : draft.DiscountMode;
        DiscountInput = draft.DiscountInput ?? string.Empty;

        var qtyByProduct = draft.Items.ToDictionary(i => i.ProductId, i => Math.Max(1, i.Quantity));
        _suppressSelectionChanged = true;
        try
        {
            foreach (var item in ProductSelections)
            {
                if (qtyByProduct.TryGetValue(item.ProductId, out var qty))
                {
                    if (item.IsAvailable)
                    {
                        item.IsSelected = true;
                        item.Quantity = qty;
                    }
                    else
                    {
                        item.IsSelected = false;
                        item.Quantity = 1;
                    }
                }
                else
                {
                    item.IsSelected = false;
                    item.Quantity = 1;
                }
            }
        }
        finally
        {
            _suppressSelectionChanged = false;
        }

        RebuildSubCategoryFilter();
        ApplyProductFilters();
        RecalculateTotals();

        if (autoDeleteAfterLoad)
        {
            DraftPersistenceService.Delete(ownerId.Value, entry.FilePath, SelectedTableId, AppSession.IsServerTablet);
            RefreshSavedDrafts();
            SelectedDraft = EmptyDraftOption;
        }

        if (showMessage)
            ShowDialog(autoDeleteAfterLoad ? "Draft loaded and removed." : "Draft loaded.", "Create Order", MessageBoxButton.OK, MessageBoxImage.Information);

        return true;
    }

    private void RefreshSavedDrafts()
    {
        var previousDraftId = SelectedDraft?.FilePath ?? string.Empty;
        SavedDrafts.Clear();
        SavedDrafts.Add(EmptyDraftOption);

        var ownerId = ResolveDraftOwnerEmployeeId();
        if (ownerId.HasValue)
        {
            var restrictCustomer = AppSession.IsServerTablet;
            foreach (var row in DraftPersistenceService.ListForEmployee(ownerId.Value, SelectedTableId, restrictCustomer))
                SavedDrafts.Add(new DraftEntry { FilePath = row.Id, DisplayName = row.Label });
        }

        SelectedDraft = SavedDrafts.FirstOrDefault(d => d.FilePath == previousDraftId)
            ?? SavedDrafts.FirstOrDefault();
    }

    private void DeleteSelectedDraft()
    {
        if (SelectedDraft is null || string.IsNullOrWhiteSpace(SelectedDraft.FilePath))
        {
            ShowDialog("Select a draft to delete.", "Create Order", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = ShowDialog($"Delete draft \"{SelectedDraft.DisplayName}\"?", "Delete Draft", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
            return;

        var ownerId = ResolveDraftOwnerEmployeeId();
        if (!ownerId.HasValue)
            return;

        DraftPersistenceService.Delete(ownerId.Value, SelectedDraft.FilePath, SelectedTableId, AppSession.IsServerTablet);
        RefreshSavedDrafts();
    }

    private void DeleteAllDrafts()
    {
        var ownerId = ResolveDraftOwnerEmployeeId();
        if (!ownerId.HasValue)
        {
            ShowDialog("No drafts to delete.", "Create Order", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var restrictCustomer = AppSession.IsServerTablet;
        var drafts = DraftPersistenceService.ListForEmployee(ownerId.Value, SelectedTableId, restrictCustomer);
        var hasDrafts = drafts.Count > 0;
        if (!hasDrafts)
        {
            ShowDialog("No drafts to delete.", "Create Order", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = ShowDialog("Delete ALL saved drafts?", "Delete All Drafts", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
            return;

        foreach (var draft in drafts)
            DraftPersistenceService.Delete(ownerId.Value, draft.Id, SelectedTableId, AppSession.IsServerTablet);

        RefreshSavedDrafts();
        SelectedDraft = EmptyDraftOption;
    }

    private int? ResolveDraftOwnerEmployeeId()
    {
        if (_serverEmployeeId.HasValue)
            return _serverEmployeeId;
        if (AppSession.StaffEmployeeId.HasValue)
            return AppSession.StaffEmployeeId;

        return _resolvedDraftOwnerEmployeeId;
    }

    private string ResolveDraftOwnerName()
    {
        if (!string.IsNullOrWhiteSpace(_serverEmployeeName))
            return _serverEmployeeName;
        if (!string.IsNullOrWhiteSpace(AppSession.StaffEmployeeName))
            return AppSession.StaffEmployeeName;
        if (!string.IsNullOrWhiteSpace(AppSession.AdminLoginDisplayName))
            return AppSession.AdminLoginDisplayName;
        return "Server";
    }
}
