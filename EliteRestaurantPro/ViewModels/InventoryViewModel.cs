using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurantPro.ViewModels;

public class InventoryViewModel : AdminBaseViewModel
{
    private bool _isLoadingItems;
    private int? _editingItemId;
    private bool _isDialogOpen;
    private bool _isAdjustmentDialogOpen;
    private string _dialogTitle = "Add Inventory Item";
    private string _itemName = string.Empty;
    private string _unit = string.Empty;
    private string _stockText = string.Empty;
    private string _expirationDateText = string.Empty;
    private string _notes = string.Empty;
    private bool _isEditingExistingItem;
    private int? _adjustingItemId;
    private string _adjustmentItemName = string.Empty;
    private string _selectedAdjustmentType = "Deduct";
    private string _adjustmentQuantityText = string.Empty;
    private string _adjustmentComment = string.Empty;
    private readonly List<InventoryItem> _allInventoryItems = [];
    private string _searchText = string.Empty;
    private string _inventoryViewMode = "Default";

    public override string ActivePage => "Inventory";

    /// <summary>Add, edit, delete, and adjustments — not available on staff tablets (including kitchen view).</summary>
    public bool ShowInventoryManagementChrome => !AppSession.IsStaffTablet;

    /// <summary>When true, list is sorted by expiration urgency and cards show red / orange / blue accents.</summary>
    public bool ExpirationViewActive
    {
        get => string.Equals(_inventoryViewMode, "Expiration", StringComparison.Ordinal);
    }

    /// <summary>When true, list is sorted by stock quantity (lowest first) and cards show stock urgency accents.</summary>
    public bool QuantityViewActive
    {
        get => string.Equals(_inventoryViewMode, "Quantity", StringComparison.Ordinal);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetField(ref _searchText, value))
                return;
            ApplyInventoryFilter();
        }
    }

    public ObservableCollection<InventoryItem> InventoryItems { get; } = new();

    public bool IsDialogOpen
    {
        get => _isDialogOpen;
        set => SetField(ref _isDialogOpen, value);
    }

    public string DialogTitle
    {
        get => _dialogTitle;
        set => SetField(ref _dialogTitle, value);
    }

    public string ItemName
    {
        get => _itemName;
        set => SetField(ref _itemName, value);
    }

    public string Unit
    {
        get => _unit;
        set => SetField(ref _unit, value);
    }

    public string StockText
    {
        get => _stockText;
        set => SetField(ref _stockText, value);
    }

    public string ExpirationDateText
    {
        get => _expirationDateText;
        set => SetField(ref _expirationDateText, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetField(ref _notes, value);
    }

    public bool IsEditingExistingItem
    {
        get => _isEditingExistingItem;
        set => SetField(ref _isEditingExistingItem, value);
    }

    public bool IsAdjustmentDialogOpen
    {
        get => _isAdjustmentDialogOpen;
        set => SetField(ref _isAdjustmentDialogOpen, value);
    }

    public string AdjustmentItemName
    {
        get => _adjustmentItemName;
        set => SetField(ref _adjustmentItemName, value);
    }

    public ObservableCollection<string> AdjustmentTypes { get; } = new(["Add", "Deduct"]);

    public string SelectedAdjustmentType
    {
        get => _selectedAdjustmentType;
        set => SetField(ref _selectedAdjustmentType, value);
    }

    public string AdjustmentQuantityText
    {
        get => _adjustmentQuantityText;
        set => SetField(ref _adjustmentQuantityText, value);
    }

    public string AdjustmentComment
    {
        get => _adjustmentComment;
        set => SetField(ref _adjustmentComment, value);
    }

    public ICommand OpenAddDialogCommand { get; }
    public ICommand ToggleExpirationViewCommand { get; }
    public ICommand ToggleQuantityViewCommand { get; }
    public ICommand EditItemCommand { get; }
    public ICommand DeleteItemCommand { get; }
    public ICommand OpenAdjustDialogCommand { get; }
    public ICommand ApplyAdjustmentCommand { get; }
    public ICommand CancelAdjustmentDialogCommand { get; }
    public ICommand SaveItemCommand { get; }
    public ICommand CancelDialogCommand { get; }

    public InventoryViewModel(Action<BaseViewModel> navigate) : base(navigate)
    {
        OpenAddDialogCommand = new RelayCommand(_ => OpenAddDialog());
        ToggleExpirationViewCommand = new RelayCommand(_ => SetInventoryViewMode(ExpirationViewActive ? "Default" : "Expiration"));
        ToggleQuantityViewCommand = new RelayCommand(_ => SetInventoryViewMode(QuantityViewActive ? "Default" : "Quantity"));
        EditItemCommand = new RelayCommand(item => OpenEditDialog(item as InventoryItem));
        DeleteItemCommand = new RelayCommand(item => DeleteItem(item as InventoryItem));
        OpenAdjustDialogCommand = new RelayCommand(item => OpenAdjustDialog(item as InventoryItem));
        ApplyAdjustmentCommand = new RelayCommand(_ => ApplyAdjustment());
        CancelAdjustmentDialogCommand = new RelayCommand(_ => CloseAdjustmentDialog());
        SaveItemCommand = new RelayCommand(_ => SaveItem());
        CancelDialogCommand = new RelayCommand(_ => CloseDialog());

        _ = LoadItemsAsync();
    }

    private async Task LoadItemsAsync()
    {
        if (_isLoadingItems)
            return;

        _isLoadingItems = true;
        try
        {
            var items = await Task.Run(() =>
            {
                using var db = new AppDbContext();
                return db.InventoryItems.AsNoTracking().OrderBy(i => i.Name).ToList();
            });

            _allInventoryItems.Clear();
            _allInventoryItems.AddRange(items);
            ApplyInventoryFilter();
            RefreshReadyPickupBanner();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Inventory could not be loaded safely.\n\n{ex.Message}",
                "Inventory Load Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _isLoadingItems = false;
        }
    }

    private void ApplyInventoryFilter()
    {
        var q = (_searchText ?? string.Empty).Trim();
        IEnumerable<InventoryItem> seq = _allInventoryItems;
        if (q.Length > 0)
            seq = seq.Where(i => InventoryItemMatches(i, q));

        if (ExpirationViewActive)
        {
            seq = seq
                .OrderBy(ExpirationSortRank)
                .ThenBy(i => i.ExpirationDate ?? DateTime.MaxValue)
                .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase);
        }
        else if (QuantityViewActive)
        {
            seq = seq
                .OrderBy(QuantitySortRank)
                .ThenBy(i => i.StockQuantity)
                .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            seq = seq.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase);
        }

        InventoryItems.Clear();
        foreach (var item in seq)
            InventoryItems.Add(item);
    }

    /// <summary>Lower sorts first (most urgent).</summary>
    private static int ExpirationSortRank(InventoryItem item)
        => item.ExpirationStatus switch
        {
            "Expired" => 0,
            "Critical" => 1,
            "Bad" => 2,
            "Good" => 3,
            _ => 4
        };

    private static int QuantitySortRank(InventoryItem item)
        => item.QuantityStatus switch
        {
            "Out" => 0,
            "Critical" => 1,
            "Low" => 2,
            _ => 3
        };

    private void SetInventoryViewMode(string mode)
    {
        var normalized = string.IsNullOrWhiteSpace(mode) ? "Default" : mode.Trim();
        if (string.Equals(_inventoryViewMode, normalized, StringComparison.Ordinal))
            return;

        _inventoryViewMode = normalized;
        OnPropertyChanged(nameof(ExpirationViewActive));
        OnPropertyChanged(nameof(QuantityViewActive));
        ApplyInventoryFilter();
    }

    private static bool InventoryItemMatches(InventoryItem item, string q)
    {
        bool Hit(string? s)
            => !string.IsNullOrEmpty(s) && s.Contains(q, StringComparison.OrdinalIgnoreCase);

        var stockText = item.StockQuantity.ToString("0.##", CultureInfo.InvariantCulture);
        var expText = item.ExpirationDate?.ToString("yyyy-MM-dd") ?? string.Empty;
        return Hit(item.Name)
               || Hit(item.UniqueId)
               || Hit(item.Unit)
               || Hit(item.Notes)
               || stockText.Contains(q, StringComparison.OrdinalIgnoreCase)
               || (!string.IsNullOrEmpty(expText) && expText.Contains(q, StringComparison.OrdinalIgnoreCase));
    }

    private void OpenAddDialog()
    {
        if (AppSession.IsStaffTablet) return;

        _editingItemId = null;
        IsEditingExistingItem = false;
        DialogTitle = "Add Inventory Item";
        ItemName = string.Empty;
        Unit = string.Empty;
        StockText = string.Empty;
        ExpirationDateText = string.Empty;
        Notes = string.Empty;
        IsDialogOpen = true;
    }

    private void OpenEditDialog(InventoryItem? item)
    {
        if (item is null || AppSession.IsStaffTablet) return;
        _editingItemId = item.Id;
        IsEditingExistingItem = true;
        DialogTitle = "Edit Inventory Item";
        ItemName = item.Name;
        Unit = item.Unit;
        StockText = item.StockQuantity.ToString("0.##", CultureInfo.InvariantCulture);
        ExpirationDateText = item.ExpirationDate?.ToString("yyyy-MM-dd") ?? string.Empty;
        Notes = item.Notes;
        IsDialogOpen = true;
    }

    private void SaveItem()
    {
        if (AppSession.IsStaffTablet) return;

        if (string.IsNullOrWhiteSpace(ItemName) || string.IsNullOrWhiteSpace(Unit))
            return;

        var hasStock = decimal.TryParse(StockText, NumberStyles.Number, CultureInfo.InvariantCulture, out var qty);
        if (!IsEditingExistingItem && !hasStock)
            return;

        DateTime? expirationDate = null;
        if (!string.IsNullOrWhiteSpace(ExpirationDateText))
        {
            if (!DateTime.TryParse(ExpirationDateText, out var parsedExpiration))
            {
                MessageBox.Show(
                    "Expiration date format is invalid. Use YYYY-MM-DD.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            expirationDate = parsedExpiration.Date;
        }

        using var db = new AppDbContext();

        if (_editingItemId is int itemId)
        {
            var existing = db.InventoryItems.Single(i => i.Id == itemId);
            existing.Name = ItemName.Trim();
            existing.Unit = Unit.Trim();
            existing.ExpirationDate = expirationDate;
            existing.Notes = Notes.Trim();
        }
        else
        {
            var confirm = MessageBox.Show(
                "Add this inventory item?",
                "Confirm Add Inventory Item",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            db.InventoryItems.Add(new InventoryItem
            {
                UniqueId = UniqueIdGenerator.NewId("INV"),
                Name = ItemName.Trim(),
                Unit = Unit.Trim(),
                StockQuantity = qty,
                ExpirationDate = expirationDate,
                Notes = Notes.Trim()
            });
        }

        db.SaveChanges();
        CloseDialog();
        _ = LoadItemsAsync();
    }

    private void DeleteItem(InventoryItem? item)
    {
        if (item is null || AppSession.IsStaffTablet) return;

        var confirm = MessageBox.Show(
            $"Delete inventory item '{item.Name}'?",
            "Confirm Delete Inventory Item",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
            return;

        using var db = new AppDbContext();
        var existing = db.InventoryItems.Include(i => i.ProductIngredients).SingleOrDefault(i => i.Id == item.Id);
        if (existing is null) return;

        if (existing.ProductIngredients.Any())
        {
            MessageBox.Show(
                "This ingredient is used by menu items and cannot be deleted.",
                "Delete Blocked",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        db.InventoryItems.Remove(existing);
        db.SaveChanges();
        _ = LoadItemsAsync();
    }

    private void OpenAdjustDialog(InventoryItem? item)
    {
        if (item is null || AppSession.IsStaffTablet) return;
        _adjustingItemId = item.Id;
        AdjustmentItemName = $"{item.Name} ({item.UniqueId})";
        SelectedAdjustmentType = "Deduct";
        AdjustmentQuantityText = string.Empty;
        AdjustmentComment = string.Empty;
        IsAdjustmentDialogOpen = true;
    }

    private void ApplyAdjustment()
    {
        if (AppSession.IsStaffTablet) return;

        if (_adjustingItemId is null)
            return;

        if (!decimal.TryParse(AdjustmentQuantityText, NumberStyles.Number, CultureInfo.InvariantCulture, out var deductionQty) || deductionQty <= 0)
            return;

        if (string.IsNullOrWhiteSpace(AdjustmentComment))
        {
            MessageBox.Show(
                "Add a comment for this manual deduction.",
                "Validation",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        using var db = new AppDbContext();
        var item = db.InventoryItems.SingleOrDefault(i => i.Id == _adjustingItemId.Value);
        if (item is null) return;

        var adjustmentType = SelectedAdjustmentType.Trim();
        if (adjustmentType == "Deduct" && item.StockQuantity < deductionQty)
        {
            MessageBox.Show(
                $"Cannot deduct {deductionQty:0.##} {item.Unit}. Current stock is {item.StockQuantity:0.##} {item.Unit}.",
                "Insufficient Stock",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (adjustmentType == "Add")
            item.StockQuantity += deductionQty;
        else
            item.StockQuantity -= deductionQty;

        var verb = adjustmentType == "Add" ? "Stock added" : "Manual deduction";
        var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm} - {verb} {deductionQty:0.##} {item.Unit}: {AdjustmentComment.Trim()}";
        item.Notes = string.IsNullOrWhiteSpace(item.Notes) ? logEntry : $"{item.Notes}\n{logEntry}";

        db.SaveChanges();
        CloseAdjustmentDialog();
        _ = LoadItemsAsync();
    }

    private void CloseDialog()
    {
        IsDialogOpen = false;
        _editingItemId = null;
        IsEditingExistingItem = false;
    }

    private void CloseAdjustmentDialog()
    {
        IsAdjustmentDialogOpen = false;
        _adjustingItemId = null;
    }

}
