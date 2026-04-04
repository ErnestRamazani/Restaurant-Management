using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using EliteRestaurantPro.Data;
using EliteRestaurantPro.Models;
using EliteRestaurantPro.Utils;
using Microsoft.EntityFrameworkCore;
using ModelTable = EliteRestaurantPro.Models.Table;

namespace EliteRestaurantPro.ViewModels;

public sealed class CreateOrderViewModel : AdminBaseViewModel
{
    private sealed class DraftItem
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    private sealed class CreateOrderDraft
    {
        public string DraftLabel { get; set; } = string.Empty;
        public int SelectedTableId { get; set; }
        public string SelectedOrderStatus { get; set; } = "Waiting";
        public string SelectedOrderCategory { get; set; } = "All";
        public string SelectedOrderSubCategory { get; set; } = "All";
        public string ProductSearchText { get; set; } = string.Empty;
        public string CustomerNotes { get; set; } = string.Empty;
        public string AllergyNotes { get; set; } = string.Empty;
        public string SelectedPaymentCurrency { get; set; } = CurrencyHelper.Usd;
        public string DiscountMode { get; set; } = "None";
        public string DiscountInput { get; set; } = string.Empty;
        public List<DraftItem> Items { get; set; } = [];
    }

    private sealed record ProductRow(
        int ProductId,
        string UniqueId,
        string Name,
        string Category,
        string SubCategory,
        decimal Price);

    private sealed record SubmitSnapshot(
        int TableId,
        List<(int ProductId, int Quantity)> SelectedLines,
        string CustomerNotes,
        string AllergyNotes,
        string DiscountMode,
        string DiscountInput,
        decimal LiveDiscountAmount,
        decimal LiveSubtotal,
        decimal LiveGrandTotal,
        decimal LiveGrandTotalFc,
        string LiveDiscountLabel,
        string LiveGrandTotalUsdText,
        string LiveGrandTotalFcText,
        string SelectedPaymentCurrency,
        string ChosenPaymentAmountText,
        string EstimatedPrepText,
        string SelectedOrderStatus,
        bool IsTabletStaffOrderFlow,
        int? ServerEmployeeId,
        string ServerEmployeeName);

    private sealed record OpenCheckInfo(int? OrderId, string Code, string Status);
    private sealed record PhaseResult(bool Ok, string Caption, string Message, int TableNumber, string TableName, OpenCheckInfo OpenCheck);
    private sealed record AppendResult(bool Ok, string Caption, string Message);
    private sealed record SaveResult(bool Ok, string Caption, string Message);

    public sealed class DraftEntry
    {
        public string FilePath { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public override string ToString() => DisplayName;
    }

    private static string LegacyDraftFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EliteRestaurantPro",
        "create-order-draft.json");

    private static string DraftsFolderPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EliteRestaurantPro",
        "drafts",
        "create-order");

    private int _selectedTableId;
    private string _selectedOrderStatus = "Waiting";
    private string _selectedOrderCategory = "All";
    private string _selectedOrderSubCategory = "All";
    private string _productSearchText = string.Empty;
    private DraftEntry? _selectedDraft;
    private string _customerNotes = string.Empty;
    private string _allergyNotes = string.Empty;
    private string _selectedPaymentCurrency = CurrencyHelper.Usd;
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

    public override string ActivePage => "CreateOrder";
    public string PageTitle => "Create Order";
    public string PageSubtitle =>
        IsTabletStaffOrderFlow
            ? "Shared order pad for admin/server/cashier. If table already has an open check, you can append lines to the same ticket."
            : "Create and manage table tickets with live totals, discounts, and open-check append support.";

    public bool IsTabletStaffOrderFlow => AppSession.IsServerTablet || AppSession.IsCashierTablet;
    public bool CanEditTablePicker => !AppSession.IsServerTablet || AvailableTables.Count > 1;
    public bool CanEditOrderStatusPicker => !AppSession.IsStaffTablet;
    public bool HasOpenCheckForTable => _openCheckOrderId.HasValue;
    public string OpenCheckBannerText =>
        HasOpenCheckForTable
            ? $"Open check {_openCheckCode} ({_openCheckStatus}) exists for this table. Submit will ask to append or create a separate ticket."
            : string.Empty;
    public string PrimaryActionLabel => IsTabletStaffOrderFlow ? "Send to cashier" : "Create Real Order";

    public ObservableCollection<ModelTable> AvailableTables { get; } = new();
    public ObservableCollection<string> OrderStatuses { get; } = new(["Waiting", "In Kitchen", "Ready"]);
    public ObservableCollection<string> OrderCategories { get; } = new();
    public ObservableCollection<string> OrderSubCategories { get; } = new();
    public ObservableCollection<string> PaymentCurrencies { get; } = new([CurrencyHelper.Usd, CurrencyHelper.CongoleseFranc]);
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
            if (!_suppressOpenCheckRefresh)
                RefreshOpenCheckBanner();
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

    public string SelectedPaymentCurrency
    {
        get => _selectedPaymentCurrency;
        set
        {
            if (!SetField(ref _selectedPaymentCurrency, value))
                return;
            OnPropertyChanged(nameof(ChosenPaymentAmountText));
        }
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
        string.Equals(SelectedPaymentCurrency, CurrencyHelper.CongoleseFranc, StringComparison.OrdinalIgnoreCase)
            ? CurrencyHelper.FormatAmount(LiveGrandTotalFc, CurrencyHelper.CongoleseFranc)
            : CurrencyHelper.FormatAmount(LiveGrandTotal, CurrencyHelper.Usd);

    public ICommand CreateOrderCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public ICommand IncreaseQuantityCommand { get; }
    public ICommand DecreaseQuantityCommand { get; }
    public ICommand SaveDraftCommand { get; }
    public ICommand LoadDraftCommand { get; }
    public ICommand DeleteDraftCommand { get; }
    public ICommand DeleteAllDraftsCommand { get; }

    public CreateOrderViewModel(Action<BaseViewModel> navigate) : base(navigate)
    {
        if (AppSession.IsServerTablet && AppSession.StaffEmployeeId is int sid)
        {
            _serverEmployeeId = sid;
            _serverEmployeeName = AppSession.StaffEmployeeName;
        }

        CreateOrderCommand = new RelayCommand(_ => CreateOrder(), _ => CanSubmitCreateOrder);
        ClearSelectionCommand = new RelayCommand(_ => ClearSelection());
        IncreaseQuantityCommand = new RelayCommand(item => IncreaseQuantity(item as ProductSelectionItemViewModel));
        DecreaseQuantityCommand = new RelayCommand(item => DecreaseQuantity(item as ProductSelectionItemViewModel));
        SaveDraftCommand = new RelayCommand(_ => SaveDraft());
        LoadDraftCommand = new RelayCommand(_ => LoadSelectedDraft());
        DeleteDraftCommand = new RelayCommand(_ => DeleteSelectedDraft());
        DeleteAllDraftsCommand = new RelayCommand(_ => DeleteAllDrafts());

        RefreshSavedDrafts();
        LoadData();
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

    private void LoadData()
    {
        if (IsLoading)
            return;

        IsLoading = true;
        try
        {
            using var db = new AppDbContext();

            var tableQuery = db.Tables.AsNoTracking()
                .Include(t => t.AssignedServer)
                .Where(t => t.Status != "Maintenance" && t.AssignedServerId != null);
            if (_serverEmployeeId.HasValue)
                tableQuery = tableQuery.Where(t => t.AssignedServerId == _serverEmployeeId.Value);

            var tables = tableQuery.OrderBy(t => t.TableNumber).ToList();
            var products = db.Products.AsNoTracking()
                .OrderBy(p => p.Category)
                .ThenBy(p => p.SubCategory)
                .ThenBy(p => p.Name)
                .Select(p => new ProductRow(
                    p.Id,
                    p.UniqueId,
                    p.Name,
                    p.Category,
                    string.IsNullOrWhiteSpace(p.SubCategory) ? "General" : p.SubCategory!,
                    p.Price))
                .ToList();

            AvailableTables.Clear();
            foreach (var t in tables)
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
            foreach (var p in products)
            {
                var vm = new ProductSelectionItemViewModel
                {
                    ProductId = p.ProductId,
                    UniqueId = p.UniqueId,
                    Name = p.Name,
                    Category = p.Category,
                    SubCategory = p.SubCategory,
                    Price = p.Price,
                    Quantity = 1
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

            SelectedPaymentCurrency = CurrencyHelper.Usd;
            SelectedDiscountMode = "None";
            DiscountInput = string.Empty;

            RebuildCategoryFilter();
            RebuildSubCategoryFilter();
            ApplyProductFilters();
            RecalculateTotals();
            RefreshOpenCheckBanner();
            RefreshReadyPickupBanner();
            OnPropertyChanged(nameof(CanEditOrderStatusPicker));
            OnPropertyChanged(nameof(CanEditTablePicker));
        }
        catch (Exception ex)
        {
            ShowDialog($"Create Order failed to load:\n\n{ex.Message}", "Create Order", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RefreshOpenCheckBanner()
    {
        if (SelectedTableId == 0)
        {
            _openCheckOrderId = null;
            _openCheckCode = string.Empty;
            _openCheckStatus = string.Empty;
            OnPropertyChanged(nameof(HasOpenCheckForTable));
            OnPropertyChanged(nameof(OpenCheckBannerText));
            return;
        }

        using var db = new AppDbContext();
        var open = db.Orders.AsNoTracking()
            .WhereOpenCheckForTable(SelectedTableId)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault();

        _openCheckOrderId = open?.Id;
        _openCheckCode = open is null
            ? string.Empty
            : string.IsNullOrWhiteSpace(open.UniqueId) ? $"#{open.Id:000}" : open.UniqueId;
        _openCheckStatus = open?.Status ?? string.Empty;

        OnPropertyChanged(nameof(HasOpenCheckForTable));
        OnPropertyChanged(nameof(OpenCheckBannerText));
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

    private static decimal ParseDiscount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0m;
        var t = text.Trim();
        if (decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out var v))
            return v;
        return decimal.TryParse(t, NumberStyles.Number, CultureInfo.CurrentCulture, out v) ? v : 0m;
    }

    private static int GetPrepMinutes(ProductSelectionItemViewModel item)
    {
        var minutes = item.Category switch
        {
            "Drink" => 3,
            "Starter/Appetizer" => 8,
            "Main" => 16,
            "Dessert" => 6,
            _ => 10
        };

        if (item.SubCategory.Equals("Cocktail", StringComparison.OrdinalIgnoreCase))
            minutes += 2;
        if (item.SubCategory.Equals("Seafood", StringComparison.OrdinalIgnoreCase))
            minutes += 3;
        if (item.SubCategory.Equals("Meat Meal", StringComparison.OrdinalIgnoreCase))
            minutes += 4;
        if (item.SubCategory.Equals("Pasta", StringComparison.OrdinalIgnoreCase))
            minutes += 2;
        return minutes;
    }

    private void RecalculateTotals()
    {
        var selected = ProductSelections.Where(p => p.IsSelected).ToList();
        LiveItemCount = selected.Sum(s => s.Quantity);
        LiveSubtotal = selected.Sum(s => s.LineTotal);

        var discountRaw = ParseDiscount(DiscountInput);
        var totals = OrderTotalsHelper.ComputeTotals(LiveSubtotal, SelectedDiscountMode, discountRaw);
        LiveDiscountAmount = totals.DiscountApplied;
        LiveDiscountLabel = OrderTotalsHelper.FormatDiscountLabel(SelectedDiscountMode, discountRaw, totals.DiscountApplied);
        LiveTaxAmount = totals.Tax;
        LiveServiceAmount = totals.Service;
        LiveGrandTotal = totals.GrandTotal;

        var prep = selected.SelectMany(s => Enumerable.Repeat(GetPrepMinutes(s), s.Quantity)).ToList();
        EstimatedPrepMinutes = prep.Count == 0 ? 0 : prep.Max() + Math.Min(10, Math.Max(0, prep.Count - 1));

        SelectedProductSelections.Clear();
        foreach (var row in selected.OrderBy(s => s.Name))
            SelectedProductSelections.Add(row);

        OnPropertyChanged(nameof(LiveDiscountSummary));
        OnPropertyChanged(nameof(EstimatedPrepText));
        OnPropertyChanged(nameof(LiveGrandTotalFc));
        OnPropertyChanged(nameof(LiveGrandTotalUsdText));
        OnPropertyChanged(nameof(LiveGrandTotalFcText));
        OnPropertyChanged(nameof(ChosenPaymentAmountText));
    }

    private void IncreaseQuantity(ProductSelectionItemViewModel? item)
    {
        if (item is null) return;
        item.Quantity += 1;
        item.IsSelected = true;
    }

    private void DecreaseQuantity(ProductSelectionItemViewModel? item)
    {
        if (item is null) return;
        item.Quantity = Math.Max(1, item.Quantity - 1);
    }

    private SubmitSnapshot BuildSubmitSnapshot(List<ProductSelectionItemViewModel> selected) =>
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
            SelectedPaymentCurrency,
            ChosenPaymentAmountText,
            EstimatedPrepText,
            SelectedOrderStatus,
            IsTabletStaffOrderFlow,
            _serverEmployeeId,
            _serverEmployeeName);

    private PhaseResult LoadPhase1(SubmitSnapshot snap)
    {
        using var db = new AppDbContext();
        var table = db.Tables.Include(t => t.AssignedServer).SingleOrDefault(t => t.Id == snap.TableId);
        if (table is null || table.AssignedServerId is null || table.AssignedServer is null)
            return new PhaseResult(false, "Create Order", "Selected table must have an assigned server.", 0, string.Empty, new OpenCheckInfo(null, string.Empty, string.Empty));

        if (AppSession.IsServerTablet && table.AssignedServerId != snap.ServerEmployeeId)
            return new PhaseResult(false, "Create Order", "This table is not assigned to your session.", 0, string.Empty, new OpenCheckInfo(null, string.Empty, string.Empty));

        var open = db.Orders.AsNoTracking()
            .WhereOpenCheckForTable(table.Id)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault();
        var code = open is null ? string.Empty : string.IsNullOrWhiteSpace(open.UniqueId) ? $"#{open.Id:000}" : open.UniqueId;
        var tableName = string.IsNullOrWhiteSpace(table.Name) ? $"Table {table.TableNumber}" : table.Name;

        return new PhaseResult(
            true,
            "Create Order",
            string.Empty,
            table.TableNumber,
            tableName,
            new OpenCheckInfo(open?.Id, code, open?.Status ?? string.Empty));
    }

    private static (int? EmployeeId, string Role, string Name) ResolveAssignee(
        IReadOnlyDictionary<int, Product> productById,
        IReadOnlyList<Employee> activeStaff,
        int productId)
    {
        if (!productById.TryGetValue(productId, out var product))
            return (null, "Unknown", "Unassigned");

        if (string.Equals(product.Category, "Drink", StringComparison.OrdinalIgnoreCase))
        {
            var barman = activeStaff.FirstOrDefault(e =>
                e.Role.Equals("Barman", StringComparison.OrdinalIgnoreCase) ||
                e.Role.Equals("Bartender", StringComparison.OrdinalIgnoreCase));
            return barman is null ? (null, "Barman", "Unassigned Barman") : (barman.Id, "Barman", barman.Name);
        }

        var chef = activeStaff.FirstOrDefault(e => e.Role.Equals("Chef", StringComparison.OrdinalIgnoreCase));
        return chef is null ? (null, "Chef", "Unassigned Chef") : (chef.Id, "Chef", chef.Name);
    }

    private static void SyncPaymentFields(OrderRecord order, AppDbContext db)
    {
        var items = order.Items.ToList();
        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var prices = db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionary(p => p.Id, p => p.Price);
        var subtotal = items.Sum(i => (prices.TryGetValue(i.ProductId, out var price) ? price : 0m) * i.Quantity);
        var totals = OrderTotalsHelper.ComputeTotals(subtotal, order.DiscountMode, order.DiscountValue);
        var grand = totals.GrandTotal;
        order.DiscountAmountUsd = totals.DiscountApplied;
        order.PaymentAmountUsd = Math.Round(grand, 2);
        order.PaymentAmountFc = CurrencyHelper.ConvertUsdToFc(grand);
        order.PaymentAmount = string.Equals(order.PaymentCurrencyCode, CurrencyHelper.CongoleseFranc, StringComparison.OrdinalIgnoreCase)
            ? order.PaymentAmountFc
            : order.PaymentAmountUsd;
    }

    private AppendResult AppendToExisting(SubmitSnapshot snap, int openOrderId)
    {
        using var db = new AppDbContext();
        var table = db.Tables.Include(t => t.AssignedServer).SingleOrDefault(t => t.Id == snap.TableId);
        if (table is null)
            return new AppendResult(false, "Create Order", "Table not found.");

        var existing = db.Orders.Include(o => o.Items).SingleOrDefault(o => o.Id == openOrderId);
        if (existing is null || existing.TableId != table.Id)
            return new AppendResult(false, "Create Order", "Open check was closed or moved. Refresh and try again.");

        var productIds = snap.SelectedLines.Select(s => s.ProductId).Distinct().ToList();
        var activeStaff = db.Employees.AsNoTracking().Where(e => e.EmploymentStatus == "Active").ToList();
        var productById = db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionary(p => p.Id, p => p);

        var newItems = new List<OrderItem>();
        foreach (var (productId, qty) in snap.SelectedLines)
        {
            var assignee = ResolveAssignee(productById, activeStaff, productId);
            newItems.Add(new OrderItem
            {
                ProductId = productId,
                Quantity = qty,
                PreparedByEmployeeId = assignee.EmployeeId,
                PreparedByRole = assignee.Role,
                PreparedByName = assignee.Name
            });
        }

        if (!OrderWorkflow.IsPendingCashier(existing.Status))
        {
            var invErr = OrderInventoryDeduction.TryApplyForAdditionalItems(db, existing, newItems);
            if (invErr is not null)
                return new AppendResult(false, "Insufficient Inventory", invErr);
        }

        foreach (var item in newItems)
            existing.Items.Add(item);

        if (!string.IsNullOrWhiteSpace(snap.CustomerNotes))
        {
            existing.CustomerNotes = string.IsNullOrWhiteSpace(existing.CustomerNotes)
                ? snap.CustomerNotes.Trim()
                : $"{existing.CustomerNotes.Trim()}\n{snap.CustomerNotes.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(snap.AllergyNotes))
        {
            existing.AllergyNotes = string.IsNullOrWhiteSpace(existing.AllergyNotes)
                ? snap.AllergyNotes.Trim()
                : $"{existing.AllergyNotes.Trim()}\n{snap.AllergyNotes.Trim()}";
        }

        if (string.Equals(existing.Status, "Ready", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(existing.Status, OrderWorkflow.Served, StringComparison.OrdinalIgnoreCase))
            existing.Status = "In Kitchen";

        SyncPaymentFields(existing, db);
        table.Status = "Occupied";
        db.SaveChanges();
        AppDbContext.ReconcileTableStatusesWithOrders(db);
        db.SaveChanges();

        var code = string.IsNullOrWhiteSpace(existing.UniqueId) ? $"#{existing.Id:000}" : existing.UniqueId;
        return new AppendResult(true, "Create Order", $"Added {newItems.Count} line(s) to check {code}.");
    }

    private SaveResult SaveNew(SubmitSnapshot snap)
    {
        var discountRaw = ParseDiscount(snap.DiscountInput);
        using var db = new AppDbContext();
        var table = db.Tables.Include(t => t.AssignedServer).SingleOrDefault(t => t.Id == snap.TableId);
        if (table is null || table.AssignedServerId is null || table.AssignedServer is null)
            return new SaveResult(false, "Create Order", "Selected table must have an assigned server.");

        var status = snap.IsTabletStaffOrderFlow ? OrderWorkflow.PendingCashier : snap.SelectedOrderStatus;
        var discountValue = string.Equals(snap.DiscountMode, "None", StringComparison.OrdinalIgnoreCase) ? 0m : discountRaw;
        var paymentCurrency = snap.SelectedPaymentCurrency;
        var payUsd = Math.Round(snap.LiveGrandTotal, 2);
        var payFc = snap.LiveGrandTotalFc;

        var order = new OrderRecord
        {
            UniqueId = UniqueIdGenerator.NewId("ORD"),
            TableId = table.Id,
            TableCode = $"Table {table.TableNumber}",
            TableName = string.IsNullOrWhiteSpace(table.Name) ? $"Table {table.TableNumber}" : table.Name,
            ServerId = AppSession.IsServerTablet ? snap.ServerEmployeeId : table.AssignedServerId,
            ServerName = AppSession.IsServerTablet
                ? (string.IsNullOrWhiteSpace(snap.ServerEmployeeName) ? table.AssignedServer.Name : snap.ServerEmployeeName)
                : table.AssignedServer.Name,
            Status = status,
            CustomerNotes = snap.CustomerNotes.Trim(),
            AllergyNotes = snap.AllergyNotes.Trim(),
            DiscountMode = snap.DiscountMode,
            DiscountValue = discountValue,
            DiscountAmountUsd = snap.LiveDiscountAmount,
            PaymentCurrencyCode = paymentCurrency,
            PaymentAmountUsd = payUsd,
            PaymentAmountFc = payFc,
            PaymentAmount = string.Equals(paymentCurrency, CurrencyHelper.CongoleseFranc, StringComparison.OrdinalIgnoreCase) ? payFc : payUsd,
            ExchangeRateUsed = CurrencyHelper.FcPerUsd,
            CreatedAt = DateTime.Now
        };

        var productIds = snap.SelectedLines.Select(s => s.ProductId).Distinct().ToList();
        var activeStaff = db.Employees.AsNoTracking().Where(e => e.EmploymentStatus == "Active").ToList();
        var productById = db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionary(p => p.Id, p => p);
        foreach (var (productId, qty) in snap.SelectedLines)
        {
            var assignee = ResolveAssignee(productById, activeStaff, productId);
            order.Items.Add(new OrderItem
            {
                ProductId = productId,
                Quantity = qty,
                PreparedByEmployeeId = assignee.EmployeeId,
                PreparedByRole = assignee.Role,
                PreparedByName = assignee.Name
            });
        }

        if (!snap.IsTabletStaffOrderFlow)
        {
            var invErr = OrderInventoryDeduction.TryApplyForPlacedOrder(db, order);
            if (invErr is not null)
                return new SaveResult(false, "Insufficient Inventory", invErr);
        }

        db.Orders.Add(order);
        table.Status = "Occupied";
        db.SaveChanges();
        AppDbContext.ReconcileTableStatusesWithOrders(db);
        db.SaveChanges();

        return snap.IsTabletStaffOrderFlow
            ? new SaveResult(true, "Sent to cashier", $"Ticket {order.UniqueId} sent to the cashier.")
            : new SaveResult(true, "Create Order", $"Order {order.UniqueId} created.");
    }

    private void CreateOrder()
    {
        if (IsLoading || _isSubmitting)
            return;

        var selected = ProductSelections.Where(p => p.IsSelected).ToList();
        if (SelectedTableId == 0 || selected.Count == 0)
        {
            ShowDialog("Select a table and at least one menu item.", "Create Order", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SetSubmitting(true);
        try
        {
            var snap = BuildSubmitSnapshot(selected);
            var phase = LoadPhase1(snap);
            if (!phase.Ok)
            {
                ShowDialog(phase.Message, phase.Caption, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (phase.OpenCheck.OrderId is int openOrderId)
            {
                var choice = ShowDialog(
                    $"Table {phase.TableNumber} ({phase.TableName}) already has open check {phase.OpenCheck.Code} — status: {phase.OpenCheck.Status}.\n\n" +
                    $"Add {snap.SelectedLines.Count} new item line(s) to THIS ticket?\nSubtotal for new lines: {CurrencyHelper.FormatAmount(snap.LiveSubtotal, CurrencyHelper.Usd)}\n\n" +
                    "Yes = append to same ticket\nNo = create separate ticket\nCancel = go back",
                    "Open check on table",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (choice == MessageBoxResult.Cancel)
                    return;
                if (choice == MessageBoxResult.Yes)
                {
                    var append = AppendToExisting(snap, openOrderId);
                    if (!append.Ok)
                    {
                        ShowDialog(append.Message, append.Caption, MessageBoxButton.OK, MessageBoxImage.Warning);
                        RefreshOpenCheckBanner();
                        return;
                    }

                    ShowDialog(append.Message, append.Caption, MessageBoxButton.OK, MessageBoxImage.Information);
                    ClearSelection();
                    RefreshOpenCheckBanner();
                    RefreshReadyPickupBanner();
                    return;
                }
            }

            var discountRaw = ParseDiscount(snap.DiscountInput);
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

            var confirm = ShowDialog(
                $"Create order for Table {phase.TableNumber} ({phase.TableName}) with {snap.SelectedLines.Count} selected item(s)?\n\n" +
                $"Subtotal: {CurrencyHelper.FormatAmount(snap.LiveSubtotal, CurrencyHelper.Usd)}{discountLine}\n" +
                $"Grand Total: {snap.LiveGrandTotalUsdText}\n" +
                $"Equivalent FC: {snap.LiveGrandTotalFcText}\n" +
                $"Payment Currency: {snap.SelectedPaymentCurrency}\n" +
                $"Amount To Collect: {snap.ChosenPaymentAmountText}\n" +
                $"Estimated Prep: {snap.EstimatedPrepText}",
                snap.IsTabletStaffOrderFlow ? "Send to cashier" : "Confirm Create Order",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            var save = SaveNew(snap);
            if (!save.Ok)
            {
                ShowDialog(save.Message, save.Caption, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ShowDialog(save.Message, save.Caption, MessageBoxButton.OK, MessageBoxImage.Information);
            ClearSelection();
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

    private void ClearSelection()
    {
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
        SelectedPaymentCurrency = CurrencyHelper.Usd;
        SelectedDiscountMode = "None";
        DiscountInput = string.Empty;
        RecalculateTotals();
        ApplyProductFilters();
    }

    private void SaveDraft()
    {
        Directory.CreateDirectory(DraftsFolderPath);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var filePath = Path.Combine(DraftsFolderPath, $"{stamp}-{Guid.NewGuid():N}.json");
        var table = AvailableTables.FirstOrDefault(t => t.Id == SelectedTableId);
        var selectedCount = ProductSelections.Where(p => p.IsSelected).Sum(p => p.Quantity);
        var tableLabel = table is null ? "No Table" : $"Table {table.TableNumber}";

        var draft = new CreateOrderDraft
        {
            DraftLabel = $"{DateTime.Now:dd MMM HH:mm:ss} | {tableLabel} | {selectedCount} items | {SelectedOrderStatus}",
            SelectedTableId = SelectedTableId,
            SelectedOrderStatus = SelectedOrderStatus,
            SelectedOrderCategory = SelectedOrderCategory,
            SelectedOrderSubCategory = SelectedOrderSubCategory,
            ProductSearchText = ProductSearchText,
            CustomerNotes = CustomerNotes,
            AllergyNotes = AllergyNotes,
            SelectedPaymentCurrency = SelectedPaymentCurrency,
            DiscountMode = SelectedDiscountMode,
            DiscountInput = DiscountInput,
            Items = ProductSelections.Where(p => p.IsSelected)
                .Select(p => new DraftItem { ProductId = p.ProductId, Quantity = p.Quantity })
                .ToList()
        };

        File.WriteAllText(filePath, JsonSerializer.Serialize(draft, new JsonSerializerOptions { WriteIndented = true }));
        RefreshSavedDrafts();
        SelectedDraft = SavedDrafts.FirstOrDefault(d => d.FilePath == filePath);
        ShowDialog("Draft saved.", "Create Order", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void LoadSelectedDraft()
    {
        if (!LoadDraft(SelectedDraft, showMessage: true, autoDeleteAfterLoad: false))
            ShowDialog("No saved draft found.", "Create Order", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private bool LoadDraft(DraftEntry? entry, bool showMessage, bool autoDeleteAfterLoad)
    {
        if (entry is null || string.IsNullOrWhiteSpace(entry.FilePath) || !File.Exists(entry.FilePath))
            return false;

        var text = File.ReadAllText(entry.FilePath);
        var draft = JsonSerializer.Deserialize<CreateOrderDraft>(text);
        if (draft is null)
            return false;

        SelectedTableId = draft.SelectedTableId;
        SelectedOrderStatus = string.IsNullOrWhiteSpace(draft.SelectedOrderStatus) ? "Waiting" : draft.SelectedOrderStatus;
        SelectedOrderCategory = string.IsNullOrWhiteSpace(draft.SelectedOrderCategory) ? "All" : draft.SelectedOrderCategory;
        SelectedOrderSubCategory = string.IsNullOrWhiteSpace(draft.SelectedOrderSubCategory) ? "All" : draft.SelectedOrderSubCategory;
        ProductSearchText = draft.ProductSearchText ?? string.Empty;
        CustomerNotes = draft.CustomerNotes ?? string.Empty;
        AllergyNotes = draft.AllergyNotes ?? string.Empty;
        SelectedPaymentCurrency = string.IsNullOrWhiteSpace(draft.SelectedPaymentCurrency) ? CurrencyHelper.Usd : draft.SelectedPaymentCurrency;
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
                    item.IsSelected = true;
                    item.Quantity = qty;
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
            try { File.Delete(entry.FilePath); } catch { }
            RefreshSavedDrafts();
            SelectedDraft = SavedDrafts.FirstOrDefault();
        }

        if (showMessage)
            ShowDialog(autoDeleteAfterLoad ? "Draft loaded and removed." : "Draft loaded.", "Create Order", MessageBoxButton.OK, MessageBoxImage.Information);

        return true;
    }

    private void RefreshSavedDrafts()
    {
        SavedDrafts.Clear();
        if (Directory.Exists(DraftsFolderPath))
        {
            foreach (var path in Directory.GetFiles(DraftsFolderPath, "*.json").OrderByDescending(File.GetLastWriteTime))
                SavedDrafts.Add(ReadDraft(path));
        }

        if (SavedDrafts.Count == 0 && File.Exists(LegacyDraftFilePath))
            SavedDrafts.Add(ReadDraft(LegacyDraftFilePath));

        if (SelectedDraft is not null)
        {
            SelectedDraft = SavedDrafts.FirstOrDefault(d => d.FilePath == SelectedDraft.FilePath);
            return;
        }

        SelectedDraft = SavedDrafts.FirstOrDefault();
    }

    private static DraftEntry ReadDraft(string path)
    {
        try
        {
            var text = File.ReadAllText(path);
            var draft = JsonSerializer.Deserialize<CreateOrderDraft>(text);
            var label = string.IsNullOrWhiteSpace(draft?.DraftLabel)
                ? Path.GetFileNameWithoutExtension(path)
                : draft.DraftLabel;
            return new DraftEntry { FilePath = path, DisplayName = label };
        }
        catch
        {
            return new DraftEntry { FilePath = path, DisplayName = Path.GetFileNameWithoutExtension(path) };
        }
    }

    private void DeleteSelectedDraft()
    {
        if (SelectedDraft is null)
        {
            ShowDialog("Select a draft to delete.", "Create Order", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = ShowDialog($"Delete draft \"{SelectedDraft.DisplayName}\"?", "Delete Draft", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
            return;

        try { File.Delete(SelectedDraft.FilePath); } catch { }
        RefreshSavedDrafts();
    }

    private void DeleteAllDrafts()
    {
        var hasDrafts = SavedDrafts.Count > 0 || File.Exists(LegacyDraftFilePath);
        if (!hasDrafts)
        {
            ShowDialog("No drafts to delete.", "Create Order", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = ShowDialog("Delete ALL saved drafts?", "Delete All Drafts", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
            return;

        if (Directory.Exists(DraftsFolderPath))
        {
            foreach (var path in Directory.GetFiles(DraftsFolderPath, "*.json"))
            {
                try { File.Delete(path); } catch { }
            }
        }

        if (File.Exists(LegacyDraftFilePath))
        {
            try { File.Delete(LegacyDraftFilePath); } catch { }
        }

        RefreshSavedDrafts();
    }
}
#if false
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using EliteRestaurantPro.Data;
using EliteRestaurantPro.Models;
using EliteRestaurantPro.Utils;
using Microsoft.EntityFrameworkCore;
using ModelTable = EliteRestaurantPro.Models.Table;

namespace EliteRestaurantPro.ViewModels;

public sealed class CreateOrderViewModel : AdminBaseViewModel
{
    private sealed class DraftItem
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    private sealed class CreateOrderDraft
    {
        public string DraftLabel { get; set; } = string.Empty;
        public int SelectedTableId { get; set; }
        public string SelectedOrderStatus { get; set; } = "Waiting";
        public string SelectedOrderCategory { get; set; } = "All";
        public string SelectedOrderSubCategory { get; set; } = "All";
        public string ProductSearchText { get; set; } = string.Empty;
        public string CustomerNotes { get; set; } = string.Empty;
        public string AllergyNotes { get; set; } = string.Empty;
        public string SelectedPaymentCurrency { get; set; } = CurrencyHelper.Usd;
        public string DiscountMode { get; set; } = "None";
        public string DiscountInput { get; set; } = string.Empty;
        public List<DraftItem> Items { get; set; } = [];
    }

    private sealed record ProductRow(
        int ProductId,
        string UniqueId,
        string Name,
        string Category,
        string SubCategory,
        decimal Price);

    private sealed record TableRow(
        int Id,
        string UniqueId,
        int TableNumber,
        string Name,
        int Capacity,
        string Status,
        int? AssignedServerId);

    private sealed record SubmitSnapshot(
        int TableId,
        List<(int ProductId, int Quantity)> SelectedLines,
        string CustomerNotes,
        string AllergyNotes,
        string DiscountMode,
        string DiscountInput,
        decimal LiveDiscountAmount,
        decimal LiveSubtotal,
        decimal LiveGrandTotal,
        decimal LiveGrandTotalFc,
        string LiveDiscountLabel,
        string LiveGrandTotalUsdText,
        string LiveGrandTotalFcText,
        string SelectedPaymentCurrency,
        string ChosenPaymentAmountText,
        string EstimatedPrepText,
        string SelectedOrderStatus,
        bool IsTabletStaffOrderFlow,
        int? ServerEmployeeId,
        string ServerEmployeeName);

    private sealed record OpenCheckInfo(int? OrderId, string UniqueCode, string Status);

    private sealed record CreatePhaseResult(
        bool Ok,
        string Caption,
        string Message,
        int TableId,
        int TableNumber,
        string TableName,
        OpenCheckInfo OpenCheck);

    private sealed record AppendResult(bool Ok, string Caption, string Message, int LinesAdded, string TicketCode);

    private sealed record SaveResult(bool Ok, string Caption, string Message);

    public sealed class DraftEntry
    {
        public string FilePath { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public override string ToString() => DisplayName;
    }

    private static string LegacyDraftFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EliteRestaurantPro",
        "create-order-draft.json");

    private static string DraftsFolderPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EliteRestaurantPro",
        "drafts",
        "create-order");

    private int _selectedTableId;
    private string _selectedOrderStatus = "Waiting";
    private string _selectedOrderCategory = "All";
    private string _selectedOrderSubCategory = "All";
    private string _productSearchText = string.Empty;
    private DraftEntry? _selectedDraft;
    private string _customerNotes = string.Empty;
    private string _allergyNotes = string.Empty;
    private string _selectedPaymentCurrency = CurrencyHelper.Usd;
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
    private int? _openCheckOrderId;
    private string _openCheckCode = string.Empty;
    private string _openCheckStatus = string.Empty;
    private readonly int? _serverEmployeeId;
    private readonly string _serverEmployeeName = string.Empty;
    private bool _suppressTableBannerRefresh;
    private bool _suppressSelectionChanged;

    public override string ActivePage => "CreateOrder";
    public string PageTitle => "Create Order";
    public string PageSubtitle =>
        IsTabletStaffOrderFlow
            ? "Shared order pad for server/cashier/admin. If a table already has an open check, you can append new lines to the same ticket."
            : "Create and manage table tickets with live totals, discount handling, and open-check append support.";

    public bool IsTabletStaffOrderFlow => AppSession.IsServerTablet || AppSession.IsCashierTablet;
    public bool CanEditOrderStatusPicker => !AppSession.IsStaffTablet;
    public bool CanEditTablePicker => !AppSession.IsServerTablet || AvailableTables.Count > 1;
    public string PrimaryActionLabel => IsTabletStaffOrderFlow ? "Send to cashier" : "Create Real Order";
    public bool HasOpenCheckForTable => _openCheckOrderId.HasValue;
    public string OpenCheckBannerText =>
        !HasOpenCheckForTable
            ? string.Empty
            : $"Open check {_openCheckCode} ({_openCheckStatus}) found for this table. Submit will ask if you want to add to that same ticket.";

    public ObservableCollection<ModelTable> AvailableTables { get; } = new();
    public ObservableCollection<string> OrderStatuses { get; } = new(["Waiting", "In Kitchen", "Ready"]);
    public ObservableCollection<string> OrderCategories { get; } = new();
    public ObservableCollection<string> OrderSubCategories { get; } = new();
    public ObservableCollection<string> PaymentCurrencies { get; } = new([CurrencyHelper.Usd, CurrencyHelper.CongoleseFranc]);
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
            if (!_suppressTableBannerRefresh)
                RefreshOpenCheckBanner();
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

    public string SelectedPaymentCurrency
    {
        get => _selectedPaymentCurrency;
        set
        {
            if (!SetField(ref _selectedPaymentCurrency, value))
                return;
            OnPropertyChanged(nameof(ChosenPaymentAmountText));
        }
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
        string.Equals(SelectedPaymentCurrency, CurrencyHelper.CongoleseFranc, StringComparison.OrdinalIgnoreCase)
            ? CurrencyHelper.FormatAmount(LiveGrandTotalFc, CurrencyHelper.CongoleseFranc)
            : CurrencyHelper.FormatAmount(LiveGrandTotal, CurrencyHelper.Usd);

    public ICommand CreateOrderCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public ICommand IncreaseQuantityCommand { get; }
    public ICommand DecreaseQuantityCommand { get; }
    public ICommand SaveDraftCommand { get; }
    public ICommand LoadDraftCommand { get; }
    public ICommand DeleteDraftCommand { get; }
    public ICommand DeleteAllDraftsCommand { get; }

    public CreateOrderViewModel(Action<BaseViewModel> navigate) : base(navigate)
    {
        if (AppSession.IsServerTablet && AppSession.StaffEmployeeId is int serverId)
        {
            _serverEmployeeId = serverId;
            _serverEmployeeName = AppSession.StaffEmployeeName;
        }

        CreateOrderCommand = new RelayCommand(_ => CreateOrder(), _ => CanSubmitCreateOrder);
        ClearSelectionCommand = new RelayCommand(_ => ClearSelection());
        IncreaseQuantityCommand = new RelayCommand(item => IncreaseQuantity(item as ProductSelectionItemViewModel));
        DecreaseQuantityCommand = new RelayCommand(item => DecreaseQuantity(item as ProductSelectionItemViewModel));
        SaveDraftCommand = new RelayCommand(_ => SaveDraft());
        LoadDraftCommand = new RelayCommand(_ => LoadSelectedDraft());
        DeleteDraftCommand = new RelayCommand(_ => DeleteSelectedDraft());
        DeleteAllDraftsCommand = new RelayCommand(_ => DeleteAllDrafts());

        RefreshSavedDrafts();
        LoadInitialData();
    }

    private static Window? OwnerWindow() =>
        Application.Current?.Windows.OfType<Window>().FirstOrDefault(static w => w.IsActive)
        ?? Application.Current?.MainWindow;

    private static MessageBoxResult ShowDialog(string message, string caption, MessageBoxButton button, MessageBoxImage image)
    {
        var owner = OwnerWindow();
        return owner is null
            ? MessageBox.Show(message, caption, button, image)
            : MessageBox.Show(owner, message, caption, button, image);
    }

    private void SetSubmitting(bool value)
    {
        if (_isSubmitting == value)
            return;
        _isSubmitting = value;
        OnPropertyChanged(nameof(CanSubmitCreateOrder));
    }

    private void LoadInitialData()
    {
        if (IsLoading)
            return;

        IsLoading = true;
        try
        {
            using var db = new AppDbContext();

            var tableQuery = db.Tables
                .AsNoTracking()
                .Include(t => t.AssignedServer)
                .Where(t => t.Status != "Maintenance" && t.AssignedServerId != null);

            if (_serverEmployeeId.HasValue)
                tableQuery = tableQuery.Where(t => t.AssignedServerId == _serverEmployeeId.Value);

            var tableRows = tableQuery
                .OrderBy(t => t.TableNumber)
                .Select(t => new TableRow(
                    t.Id,
                    t.UniqueId,
                    t.TableNumber,
                    t.Name,
                    t.Capacity,
                    t.Status,
                    t.AssignedServerId))
                .ToList();

            var products = db.Products
                .AsNoTracking()
                .OrderBy(p => p.Category)
                .ThenBy(p => p.SubCategory)
                .ThenBy(p => p.Name)
                .Select(p => new ProductRow(
                    p.Id,
                    p.UniqueId,
                    p.Name,
                    p.Category,
                    p.SubCategory ?? "General",
                    p.Price))
                .ToList();

            AvailableTables.Clear();
            foreach (var row in tableRows)
            {
                AvailableTables.Add(new ModelTable
                {
                    Id = row.Id,
                    UniqueId = row.UniqueId,
                    TableNumber = row.TableNumber,
                    Name = row.Name,
                    Capacity = row.Capacity,
                    Status = row.Status,
                    AssignedServerId = row.AssignedServerId
                });
            }

            ProductSelections.Clear();
            foreach (var p in products)
            {
                var vm = new ProductSelectionItemViewModel
                {
                    ProductId = p.ProductId,
                    UniqueId = p.UniqueId,
                    Name = p.Name,
                    Category = p.Category,
                    SubCategory = string.IsNullOrWhiteSpace(p.SubCategory) ? "General" : p.SubCategory,
                    Price = p.Price,
                    Quantity = 1
                };
                vm.PropertyChanged += OnProductSelectionChanged;
                ProductSelections.Add(vm);
            }

            _suppressTableBannerRefresh = true;
            try
            {
                var firstTableId = AvailableTables.FirstOrDefault()?.Id ?? 0;
                SelectedTableId = firstTableId;
            }
            finally
            {
                _suppressTableBannerRefresh = false;
            }

            if (IsTabletStaffOrderFlow)
            {
                OrderStatuses.Clear();
                OrderStatuses.Add(OrderWorkflow.PendingCashier);
                SelectedOrderStatus = OrderWorkflow.PendingCashier;
            }
            else
            {
                if (!OrderStatuses.Contains(SelectedOrderStatus))
                    SelectedOrderStatus = OrderStatuses.First();
            }

            SelectedPaymentCurrency = CurrencyHelper.Usd;
            SelectedDiscountMode = "None";
            DiscountInput = string.Empty;

            RebuildCategoryFilter();
            RebuildSubCategoryFilter();
            ApplyProductFilters();
            RecalculateTotals();
            RefreshOpenCheckBanner();
            RefreshReadyPickupBanner();

            OnPropertyChanged(nameof(CanEditTablePicker));
            OnPropertyChanged(nameof(CanEditOrderStatusPicker));
        }
        catch (Exception ex)
        {
            ShowDialog($"Create Order failed to load:\n\n{ex.Message}", "Create Order", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static decimal ParseDiscountValue(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return 0m;
        var t = input.Trim();
        if (decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariant))
            return invariant;
        return decimal.TryParse(t, NumberStyles.Number, CultureInfo.CurrentCulture, out var local) ? local : 0m;
    }

    private void RebuildCategoryFilter()
    {
        var categories = ProductSelections
            .Select(p => p.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();

        OrderCategories.Clear();
        OrderCategories.Add("All");
        foreach (var c in categories)
            OrderCategories.Add(c);

        if (!OrderCategories.Contains(SelectedOrderCategory))
            SelectedOrderCategory = "All";
    }

    private void RebuildSubCategoryFilter()
    {
        var category = SelectedOrderCategory;
        var sub = ProductSelections
            .Where(p => category == "All" || p.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
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
        var category = SelectedOrderCategory;
        var subCategory = SelectedOrderSubCategory;

        var filtered = ProductSelections
            .Where(p => category == "All" || p.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .Where(p => subCategory == "All" || p.SubCategory.Equals(subCategory, StringComparison.OrdinalIgnoreCase))
            .Where(p => string.IsNullOrWhiteSpace(search)
                        || p.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                        || p.UniqueId.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Category)
            .ThenBy(p => p.SubCategory)
            .ThenBy(p => p.Name)
            .ToList();

        FilteredProductSelections.Clear();
        foreach (var item in filtered)
            FilteredProductSelections.Add(item);
    }

    private void OnProductSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressSelectionChanged)
            return;
        if (e.PropertyName is nameof(ProductSelectionItemViewModel.IsSelected) or nameof(ProductSelectionItemViewModel.Quantity))
            RecalculateTotals();
    }

    private static int BasePrepMinutes(ProductSelectionItemViewModel row)
    {
        var baseMinutes = row.Category switch
        {
            "Drink" => 3,
            "Starter/Appetizer" => 8,
            "Main" => 16,
            "Dessert" => 6,
            _ => 10
        };

        if (row.SubCategory.Equals("Cocktail", StringComparison.OrdinalIgnoreCase))
            baseMinutes += 2;
        if (row.SubCategory.Equals("Seafood", StringComparison.OrdinalIgnoreCase))
            baseMinutes += 3;
        if (row.SubCategory.Equals("Meat Meal", StringComparison.OrdinalIgnoreCase))
            baseMinutes += 4;
        if (row.SubCategory.Equals("Pasta", StringComparison.OrdinalIgnoreCase))
            baseMinutes += 2;

        return baseMinutes;
    }

    private void RecalculateTotals()
    {
        var selected = ProductSelections.Where(p => p.IsSelected).ToList();
        LiveItemCount = selected.Sum(x => x.Quantity);
        LiveSubtotal = selected.Sum(x => x.LineTotal);

        var discountRaw = ParseDiscountValue(DiscountInput);
        var totals = OrderTotalsHelper.ComputeTotals(LiveSubtotal, SelectedDiscountMode, discountRaw);
        LiveDiscountAmount = totals.DiscountApplied;
        LiveDiscountLabel = OrderTotalsHelper.FormatDiscountLabel(SelectedDiscountMode, discountRaw, totals.DiscountApplied);
        LiveTaxAmount = totals.Tax;
        LiveServiceAmount = totals.Service;
        LiveGrandTotal = totals.GrandTotal;

        var prepLines = selected
            .SelectMany(s => Enumerable.Repeat(BasePrepMinutes(s), s.Quantity))
            .ToList();
        EstimatedPrepMinutes = prepLines.Count == 0 ? 0 : prepLines.Max() + Math.Min(10, Math.Max(0, prepLines.Count - 1));

        SelectedProductSelections.Clear();
        foreach (var item in selected.OrderBy(x => x.Name))
            SelectedProductSelections.Add(item);

        OnPropertyChanged(nameof(EstimatedPrepText));
        OnPropertyChanged(nameof(LiveGrandTotalFc));
        OnPropertyChanged(nameof(LiveGrandTotalUsdText));
        OnPropertyChanged(nameof(LiveGrandTotalFcText));
        OnPropertyChanged(nameof(ChosenPaymentAmountText));
        OnPropertyChanged(nameof(LiveDiscountSummary));
    }

    private void IncreaseQuantity(ProductSelectionItemViewModel? item)
    {
        if (item is null)
            return;
        item.Quantity += 1;
        item.IsSelected = true;
    }

    private void DecreaseQuantity(ProductSelectionItemViewModel? item)
    {
        if (item is null)
            return;
        item.Quantity = Math.Max(1, item.Quantity - 1);
    }

    private void RefreshOpenCheckBanner()
    {
        if (SelectedTableId == 0)
        {
            _openCheckOrderId = null;
            _openCheckCode = string.Empty;
            _openCheckStatus = string.Empty;
            OnPropertyChanged(nameof(HasOpenCheckForTable));
            OnPropertyChanged(nameof(OpenCheckBannerText));
            return;
        }

        using var db = new AppDbContext();
        var open = db.Orders.AsNoTracking()
            .WhereOpenCheckForTable(SelectedTableId)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault();

        _openCheckOrderId = open?.Id;
        _openCheckCode = open is null
            ? string.Empty
            : string.IsNullOrWhiteSpace(open.UniqueId) ? $"#{open.Id:000}" : open.UniqueId;
        _openCheckStatus = open?.Status ?? string.Empty;

        OnPropertyChanged(nameof(HasOpenCheckForTable));
        OnPropertyChanged(nameof(OpenCheckBannerText));
    }

    private static (int? EmployeeId, string Role, string Name) ResolveAssignee(
        IReadOnlyDictionary<int, Product> productById,
        IReadOnlyList<Employee> activeStaff,
        int productId)
    {
        if (!productById.TryGetValue(productId, out var product))
            return (null, "Unknown", "Unassigned");

        var isDrink = string.Equals(product.Category, "Drink", StringComparison.OrdinalIgnoreCase);
        if (isDrink)
        {
            var barman = activeStaff.FirstOrDefault(e =>
                e.Role.Equals("Barman", StringComparison.OrdinalIgnoreCase) ||
                e.Role.Equals("Bartender", StringComparison.OrdinalIgnoreCase));
            return barman is null ? (null, "Barman", "Unassigned Barman") : (barman.Id, "Barman", barman.Name);
        }

        var chef = activeStaff.FirstOrDefault(e => e.Role.Equals("Chef", StringComparison.OrdinalIgnoreCase));
        return chef is null ? (null, "Chef", "Unassigned Chef") : (chef.Id, "Chef", chef.Name);
    }

    private static void UpdatePaymentFieldsFromItems(OrderRecord order, AppDbContext db)
    {
        var items = order.Items.ToList();
        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var prices = db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionary(p => p.Id, p => p.Price);

        var subtotal = items.Sum(i => (prices.TryGetValue(i.ProductId, out var p) ? p : 0m) * i.Quantity);
        var totals = OrderTotalsHelper.ComputeTotals(subtotal, order.DiscountMode, order.DiscountValue);
        var grand = totals.GrandTotal;
        order.DiscountAmountUsd = totals.DiscountApplied;
        order.PaymentAmountUsd = Math.Round(grand, 2);
        order.PaymentAmountFc = CurrencyHelper.ConvertUsdToFc(grand);
        order.PaymentAmount = string.Equals(order.PaymentCurrencyCode, CurrencyHelper.CongoleseFranc, StringComparison.OrdinalIgnoreCase)
            ? order.PaymentAmountFc
            : order.PaymentAmountUsd;
    }

    private CreatePhaseResult LoadCreatePhase(SubmitSnapshot snap)
    {
        using var db = new AppDbContext();
        var table = db.Tables.Include(t => t.AssignedServer).SingleOrDefault(t => t.Id == snap.TableId);
        if (table is null || table.AssignedServerId is null || table.AssignedServer is null)
            return new CreatePhaseResult(false, "Create Order", "Selected table must have an assigned server.", 0, 0, string.Empty, new OpenCheckInfo(null, string.Empty, string.Empty));

        if (AppSession.IsServerTablet && table.AssignedServerId != snap.ServerEmployeeId)
            return new CreatePhaseResult(false, "Create Order", "This table is not assigned to your session.", 0, 0, string.Empty, new OpenCheckInfo(null, string.Empty, string.Empty));

        var open = db.Orders.AsNoTracking()
            .WhereOpenCheckForTable(table.Id)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault();

        var openCode = open is null ? string.Empty : string.IsNullOrWhiteSpace(open.UniqueId) ? $"#{open.Id:000}" : open.UniqueId;
        var tableName = string.IsNullOrWhiteSpace(table.Name) ? $"Table {table.TableNumber}" : table.Name;

        return new CreatePhaseResult(
            true,
            "Create Order",
            string.Empty,
            table.Id,
            table.TableNumber,
            tableName,
            new OpenCheckInfo(open?.Id, openCode, open?.Status ?? string.Empty));
    }

    private AppendResult AppendToOpenOrder(SubmitSnapshot snap, int openOrderId)
    {
        using var db = new AppDbContext();
        var table = db.Tables.Include(t => t.AssignedServer).SingleOrDefault(t => t.Id == snap.TableId);
        if (table is null)
            return new AppendResult(false, "Create Order", "Table not found.", 0, string.Empty);

        var order = db.Orders.Include(o => o.Items).SingleOrDefault(o => o.Id == openOrderId);
        if (order is null || order.TableId != table.Id)
            return new AppendResult(false, "Create Order", "Open check was closed or moved. Refresh and try again.", 0, string.Empty);

        var productIds = snap.SelectedLines.Select(s => s.ProductId).Distinct().ToList();
        var activeStaff = db.Employees.AsNoTracking().Where(e => e.EmploymentStatus == "Active").ToList();
        var productById = db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionary(p => p.Id, p => p);

        var newItems = new List<OrderItem>();
        foreach (var (productId, qty) in snap.SelectedLines)
        {
            var assignee = ResolveAssignee(productById, activeStaff, productId);
            newItems.Add(new OrderItem
            {
                ProductId = productId,
                Quantity = qty,
                PreparedByEmployeeId = assignee.EmployeeId,
                PreparedByRole = assignee.Role,
                PreparedByName = assignee.Name
            });
        }

        if (!OrderWorkflow.IsPendingCashier(order.Status))
        {
            var invErr = OrderInventoryDeduction.TryApplyForAdditionalItems(db, order, newItems);
            if (invErr is not null)
                return new AppendResult(false, "Insufficient Inventory", invErr, 0, string.Empty);
        }

        foreach (var item in newItems)
            order.Items.Add(item);

        if (!string.IsNullOrWhiteSpace(snap.CustomerNotes))
        {
            order.CustomerNotes = string.IsNullOrWhiteSpace(order.CustomerNotes)
                ? snap.CustomerNotes.Trim()
                : $"{order.CustomerNotes.Trim()}\n{snap.CustomerNotes.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(snap.AllergyNotes))
        {
            order.AllergyNotes = string.IsNullOrWhiteSpace(order.AllergyNotes)
                ? snap.AllergyNotes.Trim()
                : $"{order.AllergyNotes.Trim()}\n{snap.AllergyNotes.Trim()}";
        }

        if (string.Equals(order.Status, "Ready", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(order.Status, OrderWorkflow.Served, StringComparison.OrdinalIgnoreCase))
        {
            order.Status = "In Kitchen";
        }

        UpdatePaymentFieldsFromItems(order, db);
        table.Status = "Occupied";
        db.SaveChanges();
        AppDbContext.ReconcileTableStatusesWithOrders(db);
        db.SaveChanges();

        var code = string.IsNullOrWhiteSpace(order.UniqueId) ? $"#{order.Id:000}" : order.UniqueId;
        return new AppendResult(true, "Create Order", $"Added {newItems.Count} line(s) to check {code}.", newItems.Count, code);
    }

    private SaveResult SaveNewOrder(SubmitSnapshot snap)
    {
        var discountRaw = ParseDiscountValue(snap.DiscountInput);
        using var db = new AppDbContext();
        var table = db.Tables.Include(t => t.AssignedServer).SingleOrDefault(t => t.Id == snap.TableId);
        if (table is null || table.AssignedServerId is null || table.AssignedServer is null)
            return new SaveResult(false, "Create Order", "Selected table must have an assigned server.");

        var status = snap.IsTabletStaffOrderFlow ? OrderWorkflow.PendingCashier : snap.SelectedOrderStatus;
        var discountValue = string.Equals(snap.DiscountMode, "None", StringComparison.OrdinalIgnoreCase) ? 0m : discountRaw;
        var paymentCurrency = snap.SelectedPaymentCurrency;

        var order = new OrderRecord
        {
            UniqueId = UniqueIdGenerator.NewId("ORD"),
            TableId = table.Id,
            TableCode = $"Table {table.TableNumber}",
            TableName = string.IsNullOrWhiteSpace(table.Name) ? $"Table {table.TableNumber}" : table.Name,
            ServerId = AppSession.IsServerTablet ? snap.ServerEmployeeId : table.AssignedServerId,
            ServerName = AppSession.IsServerTablet
                ? (string.IsNullOrWhiteSpace(snap.ServerEmployeeName) ? table.AssignedServer.Name : snap.ServerEmployeeName)
                : table.AssignedServer.Name,
            Status = status,
            CustomerNotes = snap.CustomerNotes.Trim(),
            AllergyNotes = snap.AllergyNotes.Trim(),
            DiscountMode = snap.DiscountMode,
            DiscountValue = discountValue,
            DiscountAmountUsd = snap.LiveDiscountAmount,
            PaymentCurrencyCode = paymentCurrency,
            PaymentAmountUsd = Math.Round(snap.LiveGrandTotal, 2),
            PaymentAmountFc = snap.LiveGrandTotalFc,
            PaymentAmount = string.Equals(paymentCurrency, CurrencyHelper.CongoleseFranc, StringComparison.OrdinalIgnoreCase)
                ? snap.LiveGrandTotalFc
                : Math.Round(snap.LiveGrandTotal, 2),
            ExchangeRateUsed = CurrencyHelper.FcPerUsd,
            CreatedAt = DateTime.Now
        };

        var productIds = snap.SelectedLines.Select(s => s.ProductId).Distinct().ToList();
        var activeStaff = db.Employees.AsNoTracking().Where(e => e.EmploymentStatus == "Active").ToList();
        var productById = db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionary(p => p.Id, p => p);

        foreach (var (productId, qty) in snap.SelectedLines)
        {
            var assignee = ResolveAssignee(productById, activeStaff, productId);
            order.Items.Add(new OrderItem
            {
                ProductId = productId,
                Quantity = qty,
                PreparedByEmployeeId = assignee.EmployeeId,
                PreparedByRole = assignee.Role,
                PreparedByName = assignee.Name
            });
        }

        if (!snap.IsTabletStaffOrderFlow)
        {
            var invErr = OrderInventoryDeduction.TryApplyForPlacedOrder(db, order);
            if (invErr is not null)
                return new SaveResult(false, "Insufficient Inventory", invErr);
        }

        db.Orders.Add(order);
        table.Status = "Occupied";
        db.SaveChanges();
        AppDbContext.ReconcileTableStatusesWithOrders(db);
        db.SaveChanges();

        return snap.IsTabletStaffOrderFlow
            ? new SaveResult(true, "Sent to cashier", $"Ticket {order.UniqueId} sent to the cashier.")
            : new SaveResult(true, "Create Order", $"Order {order.UniqueId} created.");
    }

    private void CreateOrder()
    {
        if (IsLoading || _isSubmitting)
            return;

        var selected = ProductSelections.Where(p => p.IsSelected).ToList();
        if (SelectedTableId == 0 || selected.Count == 0)
        {
            ShowDialog("Select a table and at least one menu item.", "Create Order", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var snap = new SubmitSnapshot(
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
            SelectedPaymentCurrency,
            ChosenPaymentAmountText,
            EstimatedPrepText,
            SelectedOrderStatus,
            IsTabletStaffOrderFlow,
            _serverEmployeeId,
            _serverEmployeeName);

        SetSubmitting(true);
        try
        {
            var phase = LoadCreatePhase(snap);
            if (!phase.Ok)
            {
                ShowDialog(phase.Message, phase.Caption, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (phase.OpenCheck.OrderId is int openOrderId)
            {
                var choice = ShowDialog(
                    $"Table {phase.TableNumber} ({phase.TableName}) already has open check {phase.OpenCheck.UniqueCode} — status: {phase.OpenCheck.Status}.\n\n" +
                    $"Add {snap.SelectedLines.Count} new item line(s) to THIS ticket?\nSubtotal for new lines: {CurrencyHelper.FormatAmount(snap.LiveSubtotal, CurrencyHelper.Usd)}\n\n" +
                    "Yes = append to same ticket\nNo = create separate ticket\nCancel = go back",
                    "Open check on table",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (choice == MessageBoxResult.Cancel)
                    return;

                if (choice == MessageBoxResult.Yes)
                {
                    var append = AppendToOpenOrder(snap, openOrderId);
                    if (!append.Ok)
                    {
                        ShowDialog(append.Message, append.Caption, MessageBoxButton.OK, MessageBoxImage.Warning);
                        RefreshOpenCheckBanner();
                        return;
                    }

                    ShowDialog(append.Message, append.Caption, MessageBoxButton.OK, MessageBoxImage.Information);
                    ClearSelection();
                    RefreshOpenCheckBanner();
                    RefreshReadyPickupBanner();
                    return;
                }
            }

            var discountValue = ParseDiscountValue(snap.DiscountInput);
            if (string.Equals(snap.DiscountMode, "Percent", StringComparison.OrdinalIgnoreCase) &&
                (discountValue <= 0m || discountValue > 100m))
            {
                ShowDialog("Enter a discount percent between 0 and 100.", "Create Order", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.Equals(snap.DiscountMode, "Usd", StringComparison.OrdinalIgnoreCase) &&
                discountValue <= 0m)
            {
                ShowDialog("Enter a discount amount greater than zero (USD).", "Create Order", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var discountLine = snap.LiveDiscountAmount > 0m
                ? $"\n{snap.LiveDiscountLabel}: -{CurrencyHelper.FormatAmount(snap.LiveDiscountAmount, CurrencyHelper.Usd)}"
                : string.Empty;

            var confirm = ShowDialog(
                $"Create order for Table {phase.TableNumber} ({phase.TableName}) with {snap.SelectedLines.Count} selected item(s)?\n\n" +
                $"Subtotal: {CurrencyHelper.FormatAmount(snap.LiveSubtotal, CurrencyHelper.Usd)}{discountLine}\n" +
                $"Grand Total: {snap.LiveGrandTotalUsdText}\n" +
                $"Equivalent FC: {snap.LiveGrandTotalFcText}\n" +
                $"Payment Currency: {snap.SelectedPaymentCurrency}\n" +
                $"Amount To Collect: {snap.ChosenPaymentAmountText}\n" +
                $"Estimated Prep: {snap.EstimatedPrepText}",
                snap.IsTabletStaffOrderFlow ? "Send to cashier" : "Confirm Create Order",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            var save = SaveNewOrder(snap);
            if (!save.Ok)
            {
                ShowDialog(save.Message, save.Caption, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ShowDialog(save.Message, save.Caption, MessageBoxButton.OK, MessageBoxImage.Information);
            ClearSelection();
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

    private void ClearSelection()
    {
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
        SelectedPaymentCurrency = CurrencyHelper.Usd;
        SelectedDiscountMode = "None";
        DiscountInput = string.Empty;
        RecalculateTotals();
        ApplyProductFilters();
    }

    private void SaveDraft()
    {
        Directory.CreateDirectory(DraftsFolderPath);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var path = Path.Combine(DraftsFolderPath, $"{stamp}-{Guid.NewGuid():N}.json");
        var table = AvailableTables.FirstOrDefault(t => t.Id == SelectedTableId);
        var selectedCount = ProductSelections.Where(p => p.IsSelected).Sum(p => p.Quantity);
        var tableLabel = table is null ? "No table" : $"Table {table.TableNumber}";

        var draft = new CreateOrderDraft
        {
            DraftLabel = $"{DateTime.Now:dd MMM HH:mm:ss} | {tableLabel} | {selectedCount} items | {SelectedOrderStatus}",
            SelectedTableId = SelectedTableId,
            SelectedOrderStatus = SelectedOrderStatus,
            SelectedOrderCategory = SelectedOrderCategory,
            SelectedOrderSubCategory = SelectedOrderSubCategory,
            ProductSearchText = ProductSearchText,
            CustomerNotes = CustomerNotes,
            AllergyNotes = AllergyNotes,
            SelectedPaymentCurrency = SelectedPaymentCurrency,
            DiscountMode = SelectedDiscountMode,
            DiscountInput = DiscountInput,
            Items = ProductSelections
                .Where(p => p.IsSelected)
                .Select(p => new DraftItem { ProductId = p.ProductId, Quantity = p.Quantity })
                .ToList()
        };

        File.WriteAllText(path, JsonSerializer.Serialize(draft, new JsonSerializerOptions { WriteIndented = true }));
        RefreshSavedDrafts();
        SelectedDraft = SavedDrafts.FirstOrDefault(d => d.FilePath == path);
        ShowDialog("Draft saved.", "Create Order", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void LoadSelectedDraft()
    {
        if (!LoadDraft(SelectedDraft, showMessage: true, autoDeleteAfterLoad: false))
            ShowDialog("No saved draft found.", "Create Order", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private bool LoadDraft(DraftEntry? entry, bool showMessage, bool autoDeleteAfterLoad)
    {
        if (entry is null || string.IsNullOrWhiteSpace(entry.FilePath) || !File.Exists(entry.FilePath))
            return false;

        var text = File.ReadAllText(entry.FilePath);
        var draft = JsonSerializer.Deserialize<CreateOrderDraft>(text);
        if (draft is null)
            return false;

        SelectedTableId = draft.SelectedTableId;
        SelectedOrderStatus = string.IsNullOrWhiteSpace(draft.SelectedOrderStatus) ? "Waiting" : draft.SelectedOrderStatus;
        SelectedOrderCategory = string.IsNullOrWhiteSpace(draft.SelectedOrderCategory) ? "All" : draft.SelectedOrderCategory;
        SelectedOrderSubCategory = string.IsNullOrWhiteSpace(draft.SelectedOrderSubCategory) ? "All" : draft.SelectedOrderSubCategory;
        ProductSearchText = draft.ProductSearchText ?? string.Empty;
        CustomerNotes = draft.CustomerNotes ?? string.Empty;
        AllergyNotes = draft.AllergyNotes ?? string.Empty;
        SelectedPaymentCurrency = string.IsNullOrWhiteSpace(draft.SelectedPaymentCurrency) ? CurrencyHelper.Usd : draft.SelectedPaymentCurrency;
        SelectedDiscountMode = string.IsNullOrWhiteSpace(draft.DiscountMode) ? "None" : draft.DraftLabel;
        DiscountInput = draft.DiscountInput ?? string.Empty;

        var qtyByProductId = draft.Items.ToDictionary(i => i.ProductId, i => Math.Max(1, i.Quantity));
        _suppressSelectionChanged = true;
        try
        {
            foreach (var item in ProductSelections)
            {
                if (qtyByProductId.TryGetValue(item.ProductId, out var qty))
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
            try { File.Delete(entry.FilePath); } catch { }
            RefreshSavedDrafts();
            SelectedDraft = SavedDrafts.FirstOrDefault();
        }

        if (showMessage)
            ShowDialog(autoDeleteAfterLoad ? "Draft loaded and removed." : "Draft loaded.", "Create Order", MessageBoxButton.OK, MessageBoxImage.Information);

        return true;
    }

    private void RefreshSavedDrafts()
    {
        SavedDrafts.Clear();
        if (Directory.Exists(DraftsFolderPath))
        {
            foreach (var path in Directory.GetFiles(DraftsFolderPath, "*.json").OrderByDescending(File.GetLastWriteTime))
                SavedDrafts.Add(ReadDraftEntry(path));
        }

        if (SavedDrafts.Count == 0 && File.Exists(LegacyDraftFilePath))
            SavedDrafts.Add(ReadDraftEntry(LegacyDraftFilePath));

        if (SelectedDraft is not null)
        {
            SelectedDraft = SavedDrafts.FirstOrDefault(x => x.FilePath == SelectedDraft.FilePath);
            return;
        }

        SelectedDraft = SavedDrafts.FirstOrDefault();
    }

    private static DraftEntry ReadDraftEntry(string path)
    {
        try
        {
            var text = File.ReadAllText(path);
            var draft = JsonSerializer.Deserialize<CreateOrderDraft>(text);
            var label = string.IsNullOrWhiteSpace(draft?.DraftLabel)
                ? Path.GetFileNameWithoutExtension(path)
                : draft.DraftLabel;
            return new DraftEntry { FilePath = path, DisplayName = label };
        }
        catch
        {
            return new DraftEntry { FilePath = path, DisplayName = Path.GetFileNameWithoutExtension(path) };
        }
    }

    private void DeleteSelectedDraft()
    {
        if (SelectedDraft is null)
        {
            ShowDialog("Select a draft to delete.", "Create Order", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = ShowDialog($"Delete draft \"{SelectedDraft.DisplayName}\"?", "Delete Draft", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
            return;

        try { File.Delete(SelectedDraft.FilePath); } catch { }
        RefreshSavedDrafts();
    }

    private void DeleteAllDrafts()
    {
        var hasDrafts = SavedDrafts.Count > 0 || File.Exists(LegacyDraftFilePath);
        if (!hasDrafts)
        {
            ShowDialog("No drafts to delete.", "Create Order", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = ShowDialog("Delete ALL saved drafts?", "Delete All Drafts", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
            return;

        if (Directory.Exists(DraftsFolderPath))
        {
            foreach (var path in Directory.GetFiles(DraftsFolderPath, "*.json"))
            {
                try { File.Delete(path); } catch { }
            }
        }

        if (File.Exists(LegacyDraftFilePath))
        {
            try { File.Delete(LegacyDraftFilePath); } catch { }
        }

        RefreshSavedDrafts();
    }
}
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using EliteRestaurantPro.Data;
using EliteRestaurantPro.Models;
using EliteRestaurantPro.Utils;
using Microsoft.EntityFrameworkCore;
using ModelTable = EliteRestaurantPro.Models.Table;

namespace EliteRestaurantPro.ViewModels;

public sealed class CreateOrderViewModel : AdminBaseViewModel
{
    private sealed class DraftItem
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    private sealed class CreateOrderDraft
    {
        public string DraftLabel { get; set; } = string.Empty;
        public int SelectedTableId { get; set; }
        public string SelectedOrderStatus { get; set; } = "Waiting";
        public string SelectedOrderCategory { get; set; } = "All";
        public string SelectedOrderSubCategory { get; set; } = "All";
        public string ProductSearchText { get; set; } = string.Empty;
        public string CustomerNotes { get; set; } = string.Empty;
        public string AllergyNotes { get; set; } = string.Empty;
        public string SelectedPaymentCurrency { get; set; } = CurrencyHelper.Usd;
        public string DiscountMode { get; set; } = "None";
        public string DiscountInput { get; set; } = string.Empty;
        public List<DraftItem> Items { get; set; } = [];
    }

    private sealed record TableSnapshotRow(
        int Id,
        string UniqueId,
        int TableNumber,
        string Name,
        int Capacity,
        string Status,
        int? AssignedServerId);

    private sealed record ProductSnapshotRow(
        int ProductId,
        string UniqueId,
        string Name,
        string Category,
        string SubCategory,
        decimal Price);

    private sealed class CreateOrderDataSnapshot
    {
        public List<TableSnapshotRow> Tables { get; init; } = [];
        public List<ProductSnapshotRow> Products { get; init; } = [];
        public int? OpenCheckOrderId { get; init; }
        public string OpenCheckCode { get; init; } = string.Empty;
        public string OpenCheckStatus { get; init; } = string.Empty;
    }

    private sealed class CreateOrderSubmitSnapshot
    {
        public List<(int ProductId, int Quantity)> SelectedLines { get; init; } = [];
        public int TableId { get; init; }
        public string CustomerNotes { get; init; } = string.Empty;
        public string AllergyNotes { get; init; } = string.Empty;
        public string SelectedDiscountMode { get; init; } = "None";
        public string DiscountInput { get; init; } = string.Empty;
        public decimal LiveDiscountAmount { get; init; }
        public string LiveDiscountLabel { get; init; } = string.Empty;
        public decimal LiveSubtotal { get; init; }
        public string LiveGrandTotalUsdText { get; init; } = string.Empty;
        public string LiveGrandTotalFcText { get; init; } = string.Empty;
        public string SelectedPaymentCurrency { get; init; } = CurrencyHelper.Usd;
        public string ChosenPaymentAmountText { get; init; } = string.Empty;
        public string EstimatedPrepText { get; init; } = "-";
        public string SelectedOrderStatus { get; init; } = "Waiting";
        public decimal LiveGrandTotal { get; init; }
        public decimal LiveGrandTotalFc { get; init; }
        public bool IsTabletStaffOrderFlow { get; init; }
        public int? ServerEmployeeId { get; init; }
        public string ServerEmployeeName { get; init; } = string.Empty;
    }

    private sealed record Phase1LoadResult(
        bool Ok,
        string? ErrorCaption,
        string? ErrorText,
        int TableId,
        int TableNumber,
        string TableName,
        int? OpenCheckOrderId,
        string? OpenCheckUniqueId,
        string? OpenCheckStatus)
    {
        public static Phase1LoadResult Fail(string caption, string text) =>
            new(false, caption, text, 0, 0, "", null, null, null);

        public static Phase1LoadResult Succeeded(
            int tableId, int tableNumber, string tableName, int? openId, string? openUid, string? openStatus) =>
            new(true, null, null, tableId, tableNumber, tableName, openId, openUid, openStatus);
    }

    private sealed record LinesAppendResult(bool Ok, string? ErrorCaption, string? ErrorMessage, int LinesAdded, string CheckUniqueId);

    private sealed record NewOrderSaveResult(
        bool Ok,
        string? ErrorMessage,
        string? ErrorCaption,
        string? SuccessCaption,
        string? SuccessMessage);

    public sealed class DraftEntry
    {
        public string FilePath { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public override string ToString() => DisplayName;
    }

    private static string LegacyDraftFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EliteRestaurantPro",
        "create-order-draft.json");
    private static string DraftsFolderPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EliteRestaurantPro",
        "drafts",
        "create-order");

    private int _selectedTableId;
    private string _selectedOrderStatus = "Waiting";
    private string _selectedOrderCategory = "All";
    private string _selectedOrderSubCategory = "All";
    private string _productSearchText = string.Empty;
    private DraftEntry? _selectedDraft;
    private string _customerNotes = string.Empty;
    private string _allergyNotes = string.Empty;
    private string _selectedPaymentCurrency = CurrencyHelper.Usd;
    private bool _isLoading;
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
    private bool _suppressTableOpenCheckRefresh;
    private bool _suppressSelectionRecalc;
    private bool _submitOrderInProgress;

    public override string ActivePage => "CreateOrder";

    /// <summary>Server or cashier tablet: ticket goes to cashier queue first (no kitchen stock yet).</summary>
    public bool IsTabletStaffOrderFlow =>
        AppSession.IsServerTablet || AppSession.IsCashierTablet;

    /// <summary>Admin always; server may switch among assigned tables if more than one; cashier may pick any staffed table.</summary>
    public bool CanEditTablePicker =>
        !AppSession.IsStaffTablet || AppSession.IsCashierTablet || AvailableTables.Count > 1;

    /// <summary>Order status is fixed for staff tablet tickets (Pending cashier).</summary>
    public bool CanEditOrderStatusPicker => !AppSession.IsStaffTablet;

    public string PageTitle => "Create Order";

    public string PageSubtitle =>
        IsTabletStaffOrderFlow
            ? "Same layout as admin — status is fixed for the cashier queue. Servers: your assigned tables. Cashiers: any table with a floor server. If the table already has an open check, you can add items to the same ticket."
            : "Cascading product selection with real-time totals. If the table already has an open check, you can add items to the same ticket.";

    public bool HasOpenCheckForTable => _openCheckOrderId.HasValue;

    public string OpenCheckBannerText =>
        !HasOpenCheckForTable
            ? string.Empty
            : $"Open check {_openCheckCode} ({_openCheckStatus}) — new items can be added to this ticket (you will be asked when you submit).";

    public string PrimaryActionLabel => IsTabletStaffOrderFlow ? "Send to cashier" : "Create Real Order";

    public ObservableCollection<ModelTable> AvailableTables { get; } = new();
    public ObservableCollection<string> OrderStatuses { get; } = new(["Waiting", "In Kitchen", "Ready"]);
    public ObservableCollection<string> OrderCategories { get; } = new();
    public ObservableCollection<string> OrderSubCategories { get; } = new();
    public ObservableCollection<string> PaymentCurrencies { get; } = new([CurrencyHelper.Usd, CurrencyHelper.CongoleseFranc]);
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
            if (!_suppressTableOpenCheckRefresh)
                RefreshOpenCheckBanner();
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

    public string SelectedPaymentCurrency
    {
        get => _selectedPaymentCurrency;
        set
        {
            if (!SetField(ref _selectedPaymentCurrency, value))
                return;

            OnPropertyChanged(nameof(ChosenPaymentAmountText));
        }
    }

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

    public decimal LiveDiscountAmount
    {
        get => _liveDiscountAmount;
        private set => SetField(ref _liveDiscountAmount, value);
    }

    public string LiveDiscountLabel
    {
        get => _liveDiscountLabel;
        private set => SetField(ref _liveDiscountLabel, value);
    }

    public string LiveDiscountSummary => LiveDiscountAmount <= 0m
        ? string.Empty
        : $"{LiveDiscountLabel} · -$ {LiveDiscountAmount:N2}";

    public int LiveItemCount
    {
        get => _liveItemCount;
        private set => SetField(ref _liveItemCount, value);
    }

    public int EstimatedPrepMinutes
    {
        get => _estimatedPrepMinutes;
        private set
        {
            if (!SetField(ref _estimatedPrepMinutes, value))
                return;
            OnPropertyChanged(nameof(EstimatedPrepText));
        }
    }

    public string EstimatedPrepText => EstimatedPrepMinutes <= 0 ? "-" : $"{EstimatedPrepMinutes} min";
    public decimal LiveGrandTotalFc => CurrencyHelper.ConvertUsdToFc(LiveGrandTotal);
    public string LiveGrandTotalUsdText => CurrencyHelper.FormatAmount(LiveGrandTotal, CurrencyHelper.Usd);
    public string LiveGrandTotalFcText => CurrencyHelper.FormatAmount(LiveGrandTotalFc, CurrencyHelper.CongoleseFranc);
    public string ChosenPaymentAmountText => SelectedPaymentCurrency == CurrencyHelper.CongoleseFranc
        ? LiveGrandTotalFcText
        : LiveGrandTotalUsdText;

    public bool CanSubmitCreateOrder => !IsLoading && !_submitOrderInProgress;

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

    public ICommand CreateOrderCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public ICommand IncreaseQuantityCommand { get; }
    public ICommand DecreaseQuantityCommand { get; }
    public ICommand SaveDraftCommand { get; }
    public ICommand LoadDraftCommand { get; }
    public ICommand DeleteDraftCommand { get; }
    public ICommand DeleteAllDraftsCommand { get; }

    public CreateOrderViewModel(Action<BaseViewModel> navigate)
        : base(navigate)
    {
        if (AppSession.IsServerTablet && AppSession.StaffEmployeeId is int sid)
        {
            _serverEmployeeId = sid;
            _serverEmployeeName = AppSession.StaffEmployeeName;
        }
        CreateOrderCommand = new RelayCommand(
            _ => CreateOrderAsync(),
            _ => CanSubmitCreateOrder);
        ClearSelectionCommand = new RelayCommand(_ => ClearSelection());
        IncreaseQuantityCommand = new RelayCommand(item => IncreaseQuantity(item as ProductSelectionItemViewModel));
        DecreaseQuantityCommand = new RelayCommand(item => DecreaseQuantity(item as ProductSelectionItemViewModel));
        SaveDraftCommand = new RelayCommand(_ => SaveDraft());
        LoadDraftCommand = new RelayCommand(_ => LoadSelectedDraft());
        DeleteDraftCommand = new RelayCommand(_ => DeleteSelectedDraft());
        DeleteAllDraftsCommand = new RelayCommand(_ => DeleteAllDrafts());
        RefreshSavedDrafts();
        _ = LoadCreateOrderDataAsync();
    }

    private async Task LoadCreateOrderDataAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;
        try
        {
            var snapshot = await Task.Run(() =>
            {
                using var db = new AppDbContext();
                var tableQuery = db.Tables
                    .AsNoTracking()
                    .Include(t => t.AssignedServer)
                    .Where(t => t.Status != "Maintenance" && t.AssignedServerId != null);
                if (_serverEmployeeId.HasValue)
                    tableQuery = tableQuery.Where(t => t.AssignedServerId == _serverEmployeeId.Value);

                var tableRows = tableQuery
                    .OrderBy(t => t.TableNumber)
                    .Select(t => new TableSnapshotRow(
                        t.Id,
                        t.UniqueId,
                        t.TableNumber,
                        t.Name,
                        t.Capacity,
                        t.Status,
                        t.AssignedServerId))
                    .ToList();

                var productRows = db.Products
                    .AsNoTracking()
                    .OrderBy(p => p.Category)
                    .ThenBy(p => p.SubCategory)
                    .ThenBy(p => p.Name)
                    .Select(p => new ProductSnapshotRow(
                        p.Id,
                        p.UniqueId,
                        p.Name,
                        p.Category,
                        p.SubCategory ?? string.Empty,
                        p.Price))
                    .ToList();

                var firstTableId = tableRows.FirstOrDefault()?.Id ?? 0;
                int? openId = null;
                var openCode = string.Empty;
                var openStatus = string.Empty;
                if (firstTableId != 0)
                {
                    var o = db.Orders.AsNoTracking()
                        .WhereOpenCheckForTable(firstTableId)
                        .OrderByDescending(x => x.CreatedAt)
                        .FirstOrDefault();
                    if (o is not null)
                    {
                        openId = o.Id;
                        openCode = string.IsNullOrWhiteSpace(o.UniqueId) ? $"#{o.Id:000}" : o.UniqueId;
                        openStatus = o.Status;
                    }
                }

                return new CreateOrderDataSnapshot
                {
                    Tables = tableRows,
                    Products = productRows,
                    OpenCheckOrderId = openId,
                    OpenCheckCode = openCode,
                    OpenCheckStatus = openStatus
                };
            });

            AvailableTables.Clear();
            ProductSelections.Clear();
            FilteredProductSelections.Clear();
            SelectedProductSelections.Clear();

            _suppressTableOpenCheckRefresh = true;
            try
            {
                foreach (var row in snapshot.Tables)
                {
                    AvailableTables.Add(new ModelTable
                    {
                        Id = row.Id,
                        UniqueId = row.UniqueId,
                        TableNumber = row.TableNumber,
                        Name = row.Name,
                        Capacity = row.Capacity,
                        Status = row.Status,
                        AssignedServerId = row.AssignedServerId
                    });
                }

                foreach (var row in snapshot.Products)
                {
                    var selection = new ProductSelectionItemViewModel
                    {
                        ProductId = row.ProductId,
                        UniqueId = row.UniqueId,
                        Name = row.Name,
                        Category = row.Category,
                        SubCategory = string.IsNullOrWhiteSpace(row.SubCategory) ? "General" : row.SubCategory,
                        Price = row.Price,
                        Quantity = 1
                    };
                    selection.PropertyChanged += OnSelectionChanged;
                    ProductSelections.Add(selection);
                }

                ApplyOpenCheckBannerFromSnapshot(snapshot);
                var firstId = snapshot.Tables.FirstOrDefault()?.Id ?? 0;
                if (_selectedTableId != firstId)
                {
                    _selectedTableId = firstId;
                    OnPropertyChanged(nameof(SelectedTableId));
                }
                else
                    OnPropertyChanged(nameof(SelectedTableId));
            }
            finally
            {
                _suppressTableOpenCheckRefresh = false;
            }

            RebuildCategoryFilter();
            RebuildSubCategoryFilter();
            ApplyProductFilters();

            if (IsTabletStaffOrderFlow)
            {
                OrderStatuses.Clear();
                OrderStatuses.Add(OrderWorkflow.PendingCashier);
                SelectedOrderStatus = OrderWorkflow.PendingCashier;
            }
            else
            {
                if (OrderStatuses.Count == 0)
                {
                    OrderStatuses.Add("Waiting");
                    OrderStatuses.Add("In Kitchen");
                    OrderStatuses.Add("Ready");
                }

                if (!OrderStatuses.Contains(SelectedOrderStatus))
                    SelectedOrderStatus = OrderStatuses.First();
            }

            SelectedPaymentCurrency = PaymentCurrencies.First();
            RecalculateTotals();
            OnPropertyChanged(nameof(CanEditTablePicker));
            OnPropertyChanged(nameof(CanEditOrderStatusPicker));
            RefreshReadyPickupBanner();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Create Order failed to load:\n{ex.Message}",
                "Create Order",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyOpenCheckBannerFromSnapshot(CreateOrderDataSnapshot snapshot)
    {
        _openCheckOrderId = snapshot.OpenCheckOrderId;
        _openCheckCode = snapshot.OpenCheckCode;
        _openCheckStatus = snapshot.OpenCheckStatus;
        OnPropertyChanged(nameof(HasOpenCheckForTable));
        OnPropertyChanged(nameof(OpenCheckBannerText));
    }

    private void RebuildCategoryFilter()
    {
        OrderCategories.Clear();
        OrderCategories.Add("All");
        foreach (var category in ProductSelections
                     .Select(p => p.Category)
                     .Where(c => !string.IsNullOrWhiteSpace(c))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(c => c))
        {
            OrderCategories.Add(category);
        }

        if (!OrderCategories.Contains(SelectedOrderCategory))
            SelectedOrderCategory = "All";
    }

    private void RebuildSubCategoryFilter()
    {
        var subCategories = ProductSelections
            .Where(p => SelectedOrderCategory == "All" || p.Category.Equals(SelectedOrderCategory, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.SubCategory)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();

        OrderSubCategories.Clear();
        OrderSubCategories.Add("All");
        foreach (var subCategory in subCategories)
            OrderSubCategories.Add(subCategory);

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
        foreach (var item in filtered)
            FilteredProductSelections.Add(item);
    }

    private void OnSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressSelectionRecalc)
            return;
        if (e.PropertyName is nameof(ProductSelectionItemViewModel.IsSelected) or nameof(ProductSelectionItemViewModel.Quantity))
            RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        var selected = ProductSelections.Where(p => p.IsSelected).ToList();
        LiveItemCount = selected.Sum(s => s.Quantity);
        var lineSubtotal = selected.Sum(s => s.LineTotal);
        LiveSubtotal = lineSubtotal;

        var discountValue = TryParseDiscountValue(DiscountInput);
        var totals = OrderTotalsHelper.ComputeTotals(lineSubtotal, SelectedDiscountMode, discountValue);
        LiveDiscountAmount = totals.DiscountApplied;
        LiveDiscountLabel = OrderTotalsHelper.FormatDiscountLabel(SelectedDiscountMode, discountValue, totals.DiscountApplied);
        OnPropertyChanged(nameof(LiveDiscountSummary));
        LiveTaxAmount = totals.Tax;
        LiveServiceAmount = totals.Service;
        LiveGrandTotal = totals.GrandTotal;

        OnPropertyChanged(nameof(LiveGrandTotalFc));
        OnPropertyChanged(nameof(LiveGrandTotalUsdText));
        OnPropertyChanged(nameof(LiveGrandTotalFcText));
        OnPropertyChanged(nameof(ChosenPaymentAmountText));

        var prepTimes = selected
            .SelectMany(s => Enumerable.Repeat(GetItemPrepMinutes(s), s.Quantity))
            .ToList();
        EstimatedPrepMinutes = prepTimes.Count == 0
            ? 0
            : prepTimes.Max() + Math.Min(10, Math.Max(0, prepTimes.Count - 1));

        SelectedProductSelections.Clear();
        foreach (var row in selected.OrderBy(s => s.Name))
            SelectedProductSelections.Add(row);
    }

    private static decimal TryParseDiscountValue(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0m;
        var t = text.Trim();
        if (decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out var v))
            return v;
        return decimal.TryParse(t, NumberStyles.Number, CultureInfo.CurrentCulture, out v) ? v : 0m;
    }

    private static int GetItemPrepMinutes(ProductSelectionItemViewModel item)
    {
        var minutes = item.Category switch
        {
            "Drink" => 3,
            "Starter/Appetizer" => 8,
            "Main" => 16,
            "Dessert" => 6,
            _ => 10
        };

        if (item.SubCategory.Equals("Cocktail", StringComparison.OrdinalIgnoreCase))
            minutes += 2;
        if (item.SubCategory.Equals("Seafood", StringComparison.OrdinalIgnoreCase))
            minutes += 3;
        if (item.SubCategory.Equals("Meat Meal", StringComparison.OrdinalIgnoreCase))
            minutes += 4;
        if (item.SubCategory.Equals("Pasta", StringComparison.OrdinalIgnoreCase))
            minutes += 2;

        return minutes;
    }

    private void IncreaseQuantity(ProductSelectionItemViewModel? item)
    {
        if (item is null)
            return;
        item.Quantity += 1;
        item.IsSelected = true;
    }

    private void DecreaseQuantity(ProductSelectionItemViewModel? item)
    {
        if (item is null)
            return;
        item.Quantity = Math.Max(1, item.Quantity - 1);
    }

    private void SaveDraft()
    {
        Directory.CreateDirectory(DraftsFolderPath);
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var draftFilePath = Path.Combine(DraftsFolderPath, $"{timestamp}-{Guid.NewGuid():N}.json");
        var table = AvailableTables.FirstOrDefault(t => t.Id == SelectedTableId);
        var selectedItemCount = ProductSelections.Where(p => p.IsSelected).Sum(p => p.Quantity);
        var tableLabel = table is null ? "No Table" : $"Table {table.TableNumber}";
        var label = $"{DateTime.Now:dd MMM HH:mm:ss} | {tableLabel} | {selectedItemCount} items | {SelectedOrderStatus}";

        var draft = new CreateOrderDraft
        {
            DraftLabel = label,
            SelectedTableId = SelectedTableId,
            SelectedOrderStatus = SelectedOrderStatus,
            SelectedOrderCategory = SelectedOrderCategory,
            SelectedOrderSubCategory = SelectedOrderSubCategory,
            ProductSearchText = ProductSearchText,
            CustomerNotes = CustomerNotes,
            AllergyNotes = AllergyNotes,
            SelectedPaymentCurrency = SelectedPaymentCurrency,
            DiscountMode = SelectedDiscountMode,
            DiscountInput = DiscountInput,
            Items = ProductSelections
                .Where(p => p.IsSelected)
                .Select(p => new DraftItem { ProductId = p.ProductId, Quantity = p.Quantity })
                .ToList()
        };

        File.WriteAllText(draftFilePath, JsonSerializer.Serialize(draft, new JsonSerializerOptions { WriteIndented = true }));
        RefreshSavedDrafts();
        SelectedDraft = SavedDrafts.FirstOrDefault(d => d.FilePath == draftFilePath);
        MessageBox.Show("Draft saved.", "Create Order", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void LoadSelectedDraft()
    {
        if (!LoadDraft(SelectedDraft, showMessage: true, autoDeleteAfterLoad: false))
            MessageBox.Show("No saved draft found.", "Create Order", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private bool LoadDraft(DraftEntry? entry, bool showMessage, bool autoDeleteAfterLoad)
    {
        if (entry is null || string.IsNullOrWhiteSpace(entry.FilePath) || !File.Exists(entry.FilePath))
        {
            if (showMessage)
                MessageBox.Show("No saved draft found.", "Create Order", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        var text = File.ReadAllText(entry.FilePath);
        var draft = JsonSerializer.Deserialize<CreateOrderDraft>(text);
        if (draft is null)
            return false;

        SelectedTableId = draft.SelectedTableId;
        SelectedOrderStatus = string.IsNullOrWhiteSpace(draft.SelectedOrderStatus) ? "Waiting" : draft.SelectedOrderStatus;
        SelectedOrderCategory = string.IsNullOrWhiteSpace(draft.SelectedOrderCategory) ? "All" : draft.SelectedOrderCategory;
        SelectedOrderSubCategory = string.IsNullOrWhiteSpace(draft.SelectedOrderSubCategory) ? "All" : draft.SelectedOrderSubCategory;
        ProductSearchText = draft.ProductSearchText ?? string.Empty;
        CustomerNotes = draft.CustomerNotes ?? string.Empty;
        AllergyNotes = draft.AllergyNotes ?? string.Empty;
        SelectedPaymentCurrency = string.IsNullOrWhiteSpace(draft.SelectedPaymentCurrency)
            ? CurrencyHelper.Usd
            : draft.SelectedPaymentCurrency;
        SelectedDiscountMode = string.IsNullOrWhiteSpace(draft.DiscountMode) ? "None" : draft.DiscountMode;
        DiscountInput = draft.DiscountInput ?? string.Empty;

        var qtyByProduct = draft.Items.ToDictionary(i => i.ProductId, i => Math.Max(1, i.Quantity));
        _suppressSelectionRecalc = true;
        try
        {
            foreach (var selection in ProductSelections)
            {
                if (qtyByProduct.TryGetValue(selection.ProductId, out var qty))
                {
                    selection.IsSelected = true;
                    selection.Quantity = qty;
                }
                else
                {
                    selection.IsSelected = false;
                    selection.Quantity = 1;
                }
            }
        }
        finally
        {
            _suppressSelectionRecalc = false;
        }

        RebuildSubCategoryFilter();
        ApplyProductFilters();
        RecalculateTotals();
        if (autoDeleteAfterLoad)
        {
            try { File.Delete(entry.FilePath); } catch { }
            if (entry.FilePath.Equals(LegacyDraftFilePath, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(LegacyDraftFilePath); } catch { }
            }
            RefreshSavedDrafts();
            SelectedDraft = SavedDrafts.FirstOrDefault();
        }
        if (showMessage)
            MessageBox.Show(autoDeleteAfterLoad ? "Draft loaded and removed from list." : "Draft loaded.", "Create Order", MessageBoxButton.OK, MessageBoxImage.Information);
        return true;
    }

    private sealed record OpenCheckBannerData(int? OrderId, string Code, string Status);

    /// <summary>Loads open-check state off the UI thread; avoids SQLite blocking the dispatcher during submit / table change.</summary>
    private void RefreshOpenCheckBanner()
    {
        var tableId = SelectedTableId;
        _ = Task.Run(() =>
        {
            OpenCheckBannerData data;
            if (tableId == 0)
            {
                data = new OpenCheckBannerData(null, string.Empty, string.Empty);
            }
            else
            {
                using var db = new AppDbContext();
                var o = db.Orders.AsNoTracking()
                    .WhereOpenCheckForTable(tableId)
                    .OrderByDescending(x => x.CreatedAt)
                    .FirstOrDefault();
                if (o is null)
                    data = new OpenCheckBannerData(null, string.Empty, string.Empty);
                else
                {
                    var code = string.IsNullOrWhiteSpace(o.UniqueId) ? $"#{o.Id:000}" : o.UniqueId;
                    data = new OpenCheckBannerData(o.Id, code, o.Status);
                }
            }

            Application.Current?.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                () => ApplyOpenCheckBannerData(data, tableId));
        });
    }

    private void ApplyOpenCheckBannerData(OpenCheckBannerData data, int capturedTableId)
    {
        if (capturedTableId != SelectedTableId)
            return;

        if (data.OrderId is null)
        {
            _openCheckOrderId = null;
            _openCheckCode = string.Empty;
            _openCheckStatus = string.Empty;
        }
        else
        {
            _openCheckOrderId = data.OrderId;
            _openCheckCode = data.Code;
            _openCheckStatus = data.Status;
        }

        OnPropertyChanged(nameof(HasOpenCheckForTable));
        OnPropertyChanged(nameof(OpenCheckBannerText));
    }

    /// <summary>
    /// Marshals to the UI with <see cref="Dispatcher.BeginInvoke"/> (never <see cref="Dispatcher.Invoke"/> from the pool).
    /// Uses <see cref="DispatcherPriority.Normal"/> — <see cref="DispatcherPriority.ApplicationIdle"/> can starve if the dispatcher
    /// never reaches an idle state, so follow-up dialogs never appear (looks like a freeze).
    /// </summary>
    private static void PostToUiThread(Action action, DispatcherPriority priority)
    {
        var dispatcher = Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("Application dispatcher is not available.");
        dispatcher.BeginInvoke(action, priority);
    }

    private static Window? GetOrderMessageOwner() =>
        Application.Current?.Windows.OfType<Window>().FirstOrDefault(static w => w.IsActive)
        ?? Application.Current?.MainWindow;

    private static void TraceCreateOrder(string message)
    {
        try
        {
            var appFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EliteRestaurantPro",
                "logs");
            Directory.CreateDirectory(appFolder);
            var path = Path.Combine(appFolder, "create-order-flow.log");
            File.AppendAllText(
                path,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [T{Environment.CurrentManagedThreadId}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Never fail order flow because logging failed.
        }
    }

    private static MessageBoxResult ShowOrderMessage(
        string messageBoxText,
        string caption,
        MessageBoxButton button,
        MessageBoxImage image)
    {
        var owner = GetOrderMessageOwner();
        return owner is null
            ? MessageBox.Show(messageBoxText, caption, button, image)
            : MessageBox.Show(owner, messageBoxText, caption, button, image);
    }

    /// <summary>Move focus off the last focused button after a modal closes so Enter does not re-invoke commands.</summary>
    private static void RestoreFocusAfterOrderModal()
    {
        var owner = GetOrderMessageOwner();
        if (owner is null)
            return;
        try
        {
            FocusManager.SetFocusedElement(owner, owner);
        }
        catch
        {
            // Ignore focus edge cases (e.g. window closing).
        }
    }

    private void EndCreateOrderSubmit() => SetSubmitOrderInProgress(false);

    private void SetSubmitOrderInProgress(bool value)
    {
        if (_submitOrderInProgress == value)
            return;
        _submitOrderInProgress = value;
        OnPropertyChanged(nameof(CanSubmitCreateOrder));
    }

    private static void SyncOrderPaymentFieldsFromLines(OrderRecord order, AppDbContext db)
    {
        var items = order.Items.ToList();
        if (items.Count == 0)
            return;

        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var prices = db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionary(p => p.Id, p => p.Price);
        var lineSubtotal = items.Sum(i => (prices.TryGetValue(i.ProductId, out var p) ? p : 0m) * i.Quantity);
        var totals = OrderTotalsHelper.ComputeTotals(lineSubtotal, order.DiscountMode, order.DiscountValue);
        order.DiscountAmountUsd = totals.DiscountApplied;
        var grand = totals.GrandTotal;
        order.PaymentAmountUsd = Math.Round(grand, 2);
        order.PaymentAmountFc = CurrencyHelper.ConvertUsdToFc(grand);
        order.PaymentAmount = string.Equals(order.PaymentCurrencyCode, CurrencyHelper.CongoleseFranc, StringComparison.OrdinalIgnoreCase)
            ? order.PaymentAmountFc
            : order.PaymentAmountUsd;
    }

    private static Phase1LoadResult LoadCreateOrderPhase1(CreateOrderSubmitSnapshot snap)
    {
        using var db = new AppDbContext();
        var table = db.Tables.Include(t => t.AssignedServer).SingleOrDefault(t => t.Id == snap.TableId);
        if (table is null || table.AssignedServerId is null || table.AssignedServer is null)
            return Phase1LoadResult.Fail("Create Order", "Selected table must have an assigned server.");

        if (AppSession.IsServerTablet)
        {
            if (table.AssignedServerId != snap.ServerEmployeeId)
            {
                return Phase1LoadResult.Fail(
                    "Create Order",
                    "This table is not assigned to you. Ask an admin to assign it in Tables.");
            }
        }

        var openHeader = db.Orders.AsNoTracking()
            .WhereOpenCheckForTable(table.Id)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault();

        var displayName = string.IsNullOrWhiteSpace(table.Name) ? $"Table {table.TableNumber}" : table.Name;
        return Phase1LoadResult.Succeeded(
            table.Id,
            table.TableNumber,
            displayName,
            openHeader?.Id,
            openHeader?.UniqueId,
            openHeader?.Status);
    }

    private static LinesAppendResult TryAppendLinesToOpenOrder(CreateOrderSubmitSnapshot snap, int openOrderId)
    {
        using var db = new AppDbContext();
        var table = db.Tables.Include(t => t.AssignedServer).SingleOrDefault(t => t.Id == snap.TableId);
        if (table is null)
            return new LinesAppendResult(false, "Create Order", "Table not found.", 0, string.Empty);

        var existing = db.Orders
            .Include(o => o.Items)
            .SingleOrDefault(o => o.Id == openOrderId);
        if (existing is null || existing.TableId != table.Id)
            return new LinesAppendResult(false, "Create Order", "Open check was closed or moved. Refresh and try again.", 0, string.Empty);

        var selectedLines = snap.SelectedLines;
        var productIds = selectedLines.Select(s => s.ProductId).Distinct().ToList();
        var activeStaff = db.Employees
            .AsNoTracking()
            .Where(e => e.EmploymentStatus == "Active")
            .ToList();
        var productById = db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionary(p => p.Id, p => p);

        (int? EmployeeId, string Role, string Name) ResolvePreparationAssignee(int productId)
        {
            if (!productById.TryGetValue(productId, out var product))
                return (null, "Unknown", "Unassigned");

            var isDrink = string.Equals(product.Category, "Drink", StringComparison.OrdinalIgnoreCase);
            if (isDrink)
            {
                var barman = activeStaff.FirstOrDefault(e =>
                    e.Role.Equals("Barman", StringComparison.OrdinalIgnoreCase) ||
                    e.Role.Equals("Bartender", StringComparison.OrdinalIgnoreCase));
                return barman is null ? (null, "Barman", "Unassigned Barman") : (barman.Id, "Barman", barman.Name);
            }

            var chef = activeStaff.FirstOrDefault(e =>
                e.Role.Equals("Chef", StringComparison.OrdinalIgnoreCase));
            return chef is null ? (null, "Chef", "Unassigned Chef") : (chef.Id, "Chef", chef.Name);
        }

        var newItems = new List<OrderItem>();
        foreach (var (productId, quantity) in selectedLines)
        {
            var assignee = ResolvePreparationAssignee(productId);
            newItems.Add(new OrderItem
            {
                ProductId = productId,
                Quantity = quantity,
                PreparedByEmployeeId = assignee.EmployeeId,
                PreparedByRole = assignee.Role,
                PreparedByName = assignee.Name
            });
        }

        if (!OrderWorkflow.IsPendingCashier(existing.Status))
        {
            var invErr = OrderInventoryDeduction.TryApplyForAdditionalItems(db, existing, newItems);
            if (invErr is not null)
                return new LinesAppendResult(false, "Insufficient Inventory", invErr, 0, string.Empty);
        }

        foreach (var item in newItems)
            existing.Items.Add(item);

        var cn = snap.CustomerNotes.Trim();
        if (!string.IsNullOrWhiteSpace(cn))
        {
            existing.CustomerNotes = string.IsNullOrWhiteSpace(existing.CustomerNotes)
                ? cn
                : $"{existing.CustomerNotes.Trim()}\n{cn}";
        }

        var an = snap.AllergyNotes.Trim();
        if (!string.IsNullOrWhiteSpace(an))
        {
            existing.AllergyNotes = string.IsNullOrWhiteSpace(existing.AllergyNotes)
                ? an
                : $"{existing.AllergyNotes.Trim()}\n{an}";
        }

        if (string.Equals(existing.Status, "Ready", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(existing.Status, "Served", StringComparison.OrdinalIgnoreCase))
            existing.Status = "In Kitchen";

        SyncOrderPaymentFieldsFromLines(existing, db);
        table.Status = "Occupied";
        db.SaveChanges();
        AppDbContext.ReconcileTableStatusesWithOrders(db);
        db.SaveChanges();

        var uid = string.IsNullOrWhiteSpace(existing.UniqueId) ? $"#{existing.Id:000}" : existing.UniqueId;
        return new LinesAppendResult(true, null, null, newItems.Count, uid);
    }

    private static NewOrderSaveResult TrySaveNewOrder(CreateOrderSubmitSnapshot snap)
    {
        var parsedDiscountValue = TryParseDiscountValue(snap.DiscountInput);
        using var db = new AppDbContext();
        var table = db.Tables.Include(t => t.AssignedServer).SingleOrDefault(t => t.Id == snap.TableId);
        if (table is null || table.AssignedServerId is null || table.AssignedServer is null)
            return new NewOrderSaveResult(false, "Selected table must have an assigned server.", "Create Order", null, null);

        var statusForOrder = snap.IsTabletStaffOrderFlow ? OrderWorkflow.PendingCashier : snap.SelectedOrderStatus;
        var discountModeForOrder = snap.SelectedDiscountMode;
        var discountValueForOrder = string.Equals(discountModeForOrder, "None", StringComparison.OrdinalIgnoreCase)
            ? 0m
            : parsedDiscountValue;
        var discountAmountForOrder = snap.LiveDiscountAmount;
        var paymentCurrency = snap.SelectedPaymentCurrency;
        var payUsd = Math.Round(snap.LiveGrandTotal, 2);
        var payFc = snap.LiveGrandTotalFc;

        var order = new OrderRecord
        {
            UniqueId = UniqueIdGenerator.NewId("ORD"),
            TableId = table.Id,
            TableCode = $"Table {table.TableNumber}",
            TableName = string.IsNullOrWhiteSpace(table.Name) ? $"Table {table.TableNumber}" : table.Name,
            ServerId = AppSession.IsServerTablet ? snap.ServerEmployeeId : table.AssignedServerId,
            ServerName = AppSession.IsServerTablet
                ? (string.IsNullOrWhiteSpace(snap.ServerEmployeeName) ? table.AssignedServer!.Name : snap.ServerEmployeeName)
                : table.AssignedServer!.Name,
            Status = statusForOrder,
            CustomerNotes = snap.CustomerNotes.Trim(),
            AllergyNotes = snap.AllergyNotes.Trim(),
            DiscountMode = discountModeForOrder,
            DiscountValue = discountValueForOrder,
            DiscountAmountUsd = discountAmountForOrder,
            PaymentCurrencyCode = paymentCurrency,
            PaymentAmount = paymentCurrency == CurrencyHelper.CongoleseFranc
                ? payFc
                : payUsd,
            PaymentAmountUsd = payUsd,
            PaymentAmountFc = payFc,
            ExchangeRateUsed = CurrencyHelper.FcPerUsd,
            CreatedAt = DateTime.Now
        };

        var productIds = snap.SelectedLines.Select(s => s.ProductId).Distinct().ToList();
        var activeStaff = db.Employees
            .AsNoTracking()
            .Where(e => e.EmploymentStatus == "Active")
            .ToList();
        var productById = db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionary(p => p.Id, p => p);

        (int? EmployeeId, string Role, string Name) ResolvePreparationAssignee(int productId)
        {
            if (!productById.TryGetValue(productId, out var product))
                return (null, "Unknown", "Unassigned");

            var isDrink = string.Equals(product.Category, "Drink", StringComparison.OrdinalIgnoreCase);
            if (isDrink)
            {
                var barman = activeStaff.FirstOrDefault(e =>
                    e.Role.Equals("Barman", StringComparison.OrdinalIgnoreCase) ||
                    e.Role.Equals("Bartender", StringComparison.OrdinalIgnoreCase));
                return barman is null ? (null, "Barman", "Unassigned Barman") : (barman.Id, "Barman", barman.Name);
            }

            var chef = activeStaff.FirstOrDefault(e =>
                e.Role.Equals("Chef", StringComparison.OrdinalIgnoreCase));
            return chef is null ? (null, "Chef", "Unassigned Chef") : (chef.Id, "Chef", chef.Name);
        }

        foreach (var (productId, quantity) in snap.SelectedLines)
        {
            var assignee = ResolvePreparationAssignee(productId);
            order.Items.Add(new OrderItem
            {
                ProductId = productId,
                Quantity = quantity,
                PreparedByEmployeeId = assignee.EmployeeId,
                PreparedByRole = assignee.Role,
                PreparedByName = assignee.Name
            });
        }

        if (!snap.IsTabletStaffOrderFlow)
        {
            var invErr = OrderInventoryDeduction.TryApplyForPlacedOrder(db, order);
            if (invErr is not null)
                return new NewOrderSaveResult(false, invErr, "Insufficient Inventory", null, null);
        }

        db.Orders.Add(order);
        table.Status = "Occupied";
        db.SaveChanges();
        AppDbContext.ReconcileTableStatusesWithOrders(db);
        db.SaveChanges();

        var caption = snap.IsTabletStaffOrderFlow ? "Sent to cashier" : "Create Order";
        var body = snap.IsTabletStaffOrderFlow
            ? $"Ticket {order.UniqueId} sent to the cashier."
            : $"Order {order.UniqueId} created successfully.";
        return new NewOrderSaveResult(true, null, null, caption, body);
    }

    /// <summary>
    /// DB work runs on the thread pool; all prompts and VM updates run synchronously on the UI dispatcher.
    /// Avoids async/await + WPF dispatcher sync context interacting badly with nested modal message loops.
    /// </summary>
    private void CreateOrderAsync()
    {
        TraceCreateOrder("CreateOrderAsync invoked.");
        if (Application.Current?.Dispatcher.CheckAccess() != true)
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(CreateOrderAsync));
            return;
        }

        if (IsLoading || _submitOrderInProgress)
            return;

        var selectedProducts = ProductSelections.Where(p => p.IsSelected).ToList();
        if (SelectedTableId == 0 || selectedProducts.Count == 0)
        {
            ShowOrderMessage(
                "Select a table and at least one menu item.",
                "Create Order",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var snapshot = new CreateOrderSubmitSnapshot
        {
            SelectedLines = selectedProducts.Select(p => (p.ProductId, p.Quantity)).ToList(),
            TableId = SelectedTableId,
            CustomerNotes = CustomerNotes,
            AllergyNotes = AllergyNotes,
            SelectedDiscountMode = SelectedDiscountMode,
            DiscountInput = DiscountInput,
            LiveDiscountAmount = LiveDiscountAmount,
            LiveDiscountLabel = LiveDiscountLabel,
            LiveSubtotal = LiveSubtotal,
            LiveGrandTotalUsdText = LiveGrandTotalUsdText,
            LiveGrandTotalFcText = LiveGrandTotalFcText,
            SelectedPaymentCurrency = SelectedPaymentCurrency,
            ChosenPaymentAmountText = ChosenPaymentAmountText,
            EstimatedPrepText = EstimatedPrepText,
            SelectedOrderStatus = SelectedOrderStatus,
            LiveGrandTotal = LiveGrandTotal,
            LiveGrandTotalFc = LiveGrandTotalFc,
            IsTabletStaffOrderFlow = IsTabletStaffOrderFlow,
            ServerEmployeeId = _serverEmployeeId,
            ServerEmployeeName = _serverEmployeeName
        };

        SetSubmitOrderInProgress(true);
        TraceCreateOrder("Phase1 load starting on worker.");
        Task.Run(() =>
        {
            try
            {
                var phase1 = LoadCreateOrderPhase1(snapshot);
                TraceCreateOrder($"Phase1 done. OpenCheckOrderId={(phase1.OpenCheckOrderId?.ToString() ?? "null")}.");
                PostToUiThread(() => ContinueCreateOrderAfterPhase1(snapshot, phase1, null),
                    DispatcherPriority.Normal);
            }
            catch (Exception ex)
            {
                TraceCreateOrder($"Phase1 exception: {ex.GetType().Name} - {ex.Message}");
                PostToUiThread(() => ContinueCreateOrderAfterPhase1(snapshot, null, ex),
                    DispatcherPriority.Normal);
            }
        });
    }

    private void ContinueCreateOrderAfterPhase1(
        CreateOrderSubmitSnapshot snapshot,
        Phase1LoadResult? phase1,
        Exception? phase1Ex)
    {
        TraceCreateOrder("ContinueCreateOrderAfterPhase1 on UI.");
        if (phase1Ex is not null)
        {
            ShowOrderMessage(
                $"Create order could not be completed.\n\n{phase1Ex.Message}",
                "Create Order",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            EndCreateOrderSubmit();
            return;
        }

        if (phase1 is null || !phase1.Ok)
        {
            ShowOrderMessage(
                phase1?.ErrorText ?? "Create order could not be completed.",
                phase1?.ErrorCaption ?? "Create Order",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            EndCreateOrderSubmit();
            return;
        }

        var discountLine = snapshot.LiveDiscountAmount > 0m
            ? $"\n{snapshot.LiveDiscountLabel}: -{CurrencyHelper.FormatAmount(snapshot.LiveDiscountAmount, CurrencyHelper.Usd)}\nTaxable subtotal: {CurrencyHelper.FormatAmount(snapshot.LiveSubtotal - snapshot.LiveDiscountAmount, CurrencyHelper.Usd)}"
            : string.Empty;
        var cashierNote = snapshot.IsTabletStaffOrderFlow
            ? "\n\nTicket goes to the cashier first — not the kitchen until they validate it."
            : string.Empty;
        var lineCount = snapshot.SelectedLines.Count;

        if (phase1.OpenCheckOrderId is int openOid)
        {
            var openLabel = string.IsNullOrWhiteSpace(phase1.OpenCheckUniqueId)
                ? $"#{openOid:000}"
                : phase1.OpenCheckUniqueId!;
            var addOnSubtotal = CurrencyHelper.FormatAmount(snapshot.LiveSubtotal, CurrencyHelper.Usd);
            var appendChoice = ShowOrderMessage(
                $"Table {phase1.TableNumber} ({phase1.TableName}) already has open check {openLabel} — status: {phase1.OpenCheckStatus}.\n\n" +
                $"Add {lineCount} new item line(s) to THIS ticket?\nNew items subtotal: {addOnSubtotal}\n\n" +
                "Yes = same ticket (one bill)\n" +
                "No = new separate order (second ticket)\n" +
                "Cancel = go back",
                "Open check on table",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (appendChoice == MessageBoxResult.Cancel)
            {
                EndCreateOrderSubmit();
                return;
            }

            if (appendChoice == MessageBoxResult.Yes)
            {
                TraceCreateOrder("Append existing selected = Yes. Starting append worker.");
                var snapForAppend = snapshot;
                var openOrderId = openOid;
                Task.Run(() =>
                {
                    try
                    {
                        var appendResult = TryAppendLinesToOpenOrder(snapForAppend, openOrderId);
                        TraceCreateOrder($"Append worker done. Ok={appendResult.Ok} LinesAdded={appendResult.LinesAdded}.");
                        PostToUiThread(() => ContinueCreateOrderAfterAppend(appendResult, null),
                            DispatcherPriority.Normal);
                    }
                    catch (Exception ex)
                    {
                        TraceCreateOrder($"Append worker exception: {ex.GetType().Name} - {ex.Message}");
                        PostToUiThread(() => ContinueCreateOrderAfterAppend(null, ex),
                            DispatcherPriority.Normal);
                    }
                });
                return;
            }
        }

        var parsedDiscountValue = TryParseDiscountValue(snapshot.DiscountInput);
        if (string.Equals(snapshot.SelectedDiscountMode, "Percent", StringComparison.OrdinalIgnoreCase) &&
            (parsedDiscountValue <= 0m || parsedDiscountValue > 100m))
        {
            ShowOrderMessage(
                "Enter a discount percent between 0 and 100.",
                "Create Order",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            EndCreateOrderSubmit();
            return;
        }

        if (string.Equals(snapshot.SelectedDiscountMode, "Usd", StringComparison.OrdinalIgnoreCase) &&
            parsedDiscountValue <= 0m)
        {
            ShowOrderMessage(
                "Enter a discount amount greater than zero (USD).",
                "Create Order",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            EndCreateOrderSubmit();
            return;
        }

        var confirm = ShowOrderMessage(
            $"Create order for Table {phase1.TableNumber} ({phase1.TableName}) with {lineCount} selected item(s)?\n\nSubtotal: {CurrencyHelper.FormatAmount(snapshot.LiveSubtotal, CurrencyHelper.Usd)}{discountLine}\nGrand Total: {snapshot.LiveGrandTotalUsdText}\nEquivalent FC: {snapshot.LiveGrandTotalFcText}\nPayment Currency: {snapshot.SelectedPaymentCurrency}\nAmount To Collect: {snapshot.ChosenPaymentAmountText}\nEstimated Prep: {snapshot.EstimatedPrepText}{cashierNote}",
            snapshot.IsTabletStaffOrderFlow ? "Send to cashier" : "Confirm Create Order",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
        {
            TraceCreateOrder("Confirm create order = No.");
            EndCreateOrderSubmit();
            return;
        }

        RestoreFocusAfterOrderModal();
        TraceCreateOrder("Confirm create order = Yes. Running save on UI thread.");

        var sw = Stopwatch.StartNew();
        try
        {
            var saveResult = TrySaveNewOrder(snapshot);
            sw.Stop();
            TraceCreateOrder($"Save UI-thread done in {sw.ElapsedMilliseconds}ms. Ok={saveResult.Ok} Caption={saveResult.ErrorCaption ?? saveResult.SuccessCaption ?? "n/a"}.");
            ContinueCreateOrderAfterSave(saveResult, null);
        }
        catch (Exception ex)
        {
            sw.Stop();
            TraceCreateOrder($"Save UI-thread exception after {sw.ElapsedMilliseconds}ms: {ex.GetType().Name} - {ex.Message}");
            ContinueCreateOrderAfterSave(null, ex);
        }
    }

    private void ContinueCreateOrderAfterAppend(LinesAppendResult? appendResult, Exception? ex)
    {
        try
        {
            if (ex is not null)
            {
                ShowOrderMessage(
                    $"Could not add lines to the open check.\n\n{ex.Message}",
                    "Create Order",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (appendResult is null || !appendResult.Ok)
            {
                ShowOrderMessage(
                    appendResult?.ErrorMessage ?? "Could not add lines to the open check.",
                    appendResult?.ErrorCaption ?? "Create Order",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                if (string.Equals(appendResult?.ErrorCaption, "Create Order", StringComparison.Ordinal))
                    RefreshOpenCheckBanner();
                return;
            }

            ShowOrderMessage(
                $"Added {appendResult.LinesAdded} line(s) to check {appendResult.CheckUniqueId}.",
                "Create Order",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            ClearSelection();
            RefreshOpenCheckBanner();
        }
        finally
        {
            EndCreateOrderSubmit();
        }
    }

    private void ContinueCreateOrderAfterSave(NewOrderSaveResult? saveResult, Exception? ex)
    {
        TraceCreateOrder($"ContinueCreateOrderAfterSave on UI. ex={(ex is null ? "none" : ex.GetType().Name)} saveResultOk={(saveResult?.Ok.ToString() ?? "null")}.");
        try
        {
            if (ex is not null)
            {
                ShowOrderMessage(
                    $"Create order could not be completed.\n\n{ex.Message}",
                    "Create Order",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (saveResult is null || !saveResult.Ok)
            {
                ShowOrderMessage(
                    saveResult?.ErrorMessage ?? "Create order could not be completed.",
                    saveResult?.ErrorCaption ?? "Create Order",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Avoid a final modal close cycle here; this path has shown re-entrancy freezes on some machines.
            // Order is already saved at this point, so we finalize the UI state silently.
            TraceCreateOrder("Save success; skipping final success MessageBox and applying UI refresh.");
            ClearSelection();
            RefreshOpenCheckBanner();
        }
        finally
        {
            TraceCreateOrder("EndCreateOrderSubmit from save continuation.");
            EndCreateOrderSubmit();
        }
    }

    private void ClearSelection()
    {
        _suppressSelectionRecalc = true;
        try
        {
            foreach (var selection in ProductSelections)
            {
                selection.IsSelected = false;
                selection.Quantity = 1;
            }
        }
        finally
        {
            _suppressSelectionRecalc = false;
        }

        ProductSearchText = string.Empty;
        CustomerNotes = string.Empty;
        AllergyNotes = string.Empty;
        SelectedPaymentCurrency = CurrencyHelper.Usd;
        SelectedDiscountMode = "None";
        DiscountInput = string.Empty;
        RecalculateTotals();
        ApplyProductFilters();
    }

    private void RefreshSavedDrafts()
    {
        SavedDrafts.Clear();
        if (Directory.Exists(DraftsFolderPath))
        {
            foreach (var path in Directory.GetFiles(DraftsFolderPath, "*.json").OrderByDescending(File.GetLastWriteTime))
            {
                SavedDrafts.Add(ReadDraftEntry(path));
            }
        }

        if (SavedDrafts.Count == 0 && File.Exists(LegacyDraftFilePath))
        {
            SavedDrafts.Add(ReadDraftEntry(LegacyDraftFilePath));
        }

        if (SelectedDraft is not null)
        {
            var preserved = SavedDrafts.FirstOrDefault(d => d.FilePath == SelectedDraft.FilePath);
            SelectedDraft = preserved;
            return;
        }

        SelectedDraft = SavedDrafts.FirstOrDefault();
    }

    private DraftEntry ReadDraftEntry(string path)
    {
        try
        {
            var text = File.ReadAllText(path);
            var draft = JsonSerializer.Deserialize<CreateOrderDraft>(text);
            var label = string.IsNullOrWhiteSpace(draft?.DraftLabel)
                ? Path.GetFileNameWithoutExtension(path)
                : draft!.DraftLabel;

            return new DraftEntry
            {
                FilePath = path,
                DisplayName = label
            };
        }
        catch
        {
            return new DraftEntry
            {
                FilePath = path,
                DisplayName = Path.GetFileNameWithoutExtension(path)
            };
        }
    }

    private void DeleteSelectedDraft()
    {
        if (SelectedDraft is null)
        {
            MessageBox.Show("Select a draft to delete.", "Create Order", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"Delete draft \"{SelectedDraft.DisplayName}\"?",
            "Delete Draft",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
            return;

        try { File.Delete(SelectedDraft.FilePath); } catch { }
        RefreshSavedDrafts();
    }

    private void DeleteAllDrafts()
    {
        var hasDrafts = SavedDrafts.Count > 0 || File.Exists(LegacyDraftFilePath);
        if (!hasDrafts)
        {
            MessageBox.Show("No drafts to delete.", "Create Order", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            "Delete ALL saved drafts?",
            "Delete All Drafts",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
            return;

        if (Directory.Exists(DraftsFolderPath))
        {
            foreach (var path in Directory.GetFiles(DraftsFolderPath, "*.json"))
            {
                try { File.Delete(path); } catch { }
            }
        }

        if (File.Exists(LegacyDraftFilePath))
        {
            try { File.Delete(LegacyDraftFilePath); } catch { }
        }

        RefreshSavedDrafts();
    }
}
#endif
