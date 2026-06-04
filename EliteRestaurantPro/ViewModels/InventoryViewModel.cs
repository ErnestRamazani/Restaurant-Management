using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.Localization;
using EliteRestaurantPro.Services;

namespace EliteRestaurantPro.ViewModels;

public sealed class InventoryAdjustmentOption
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    public override string ToString() => Label;
}

public class InventoryViewModel : AdminBaseViewModel
{
    private readonly AdminDataApiClient _data = new();
    private bool _isLoadingItems;
    private int? _editingItemId;
    private bool _isDialogOpen;
    private bool _isAdjustmentDialogOpen;
    private string _dialogTitle = string.Empty;
    private string _itemName = string.Empty;
    private string _unit = string.Empty;
    private string _stockText = string.Empty;
    private string _expirationDateText = string.Empty;
    private string _notes = string.Empty;
    private string _rawNotes = string.Empty;
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

    /// <summary>Add, edit, delete — admin only (not server/cashier/kitchen).</summary>
    public bool ShowInventoryManagementChrome => !AppSession.IsStaffTablet;

    /// <summary>Stock add/deduct with note — admin and kitchen/bar tablets.</summary>
    public bool ShowInventoryAdjustmentChrome =>
        !AppSession.IsStaffTablet || AppSession.IsKitchenBarTablet;

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

    public ObservableCollection<InventoryAdjustmentOption> AdjustmentTypeOptions { get; } = new();

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

    public string InvTitle => Loc.Admin("invTitle", "Inventory items");
    public string InvSubtitle => Loc.Admin("invSubtitle", "Single food/ingredient records with unique IDs.");
    public string InvSortExpLabel => ExpirationViewActive
        ? Loc.Admin("invSortExpActive", "By expiration ✓")
        : Loc.Admin("invSortExp", "By expiration");
    public string InvSortQtyLabel => QuantityViewActive
        ? Loc.Admin("invSortQtyActive", "By quantity ✓")
        : Loc.Admin("invSortQty", "By quantity");
    public string InvSortExpTooltip => Loc.Admin("invSortExpTooltip",
        "Sort by expiry date (most urgent first) and show red / orange / blue on each card. Click again to return to alphabetical list.");
    public string InvSortQtyTooltip => Loc.Admin("invSortQtyTooltip",
        "Sort by stock quantity (lowest first) and show stock urgency colors. Click again to return to alphabetical list.");
    public string InvAddItemLabel => Loc.Admin("invAddItem", "Add Inventory");
    public string InvSearchTooltip => Loc.Admin("invSearchTooltip", "Search by name, ID, unit, stock, notes, or expiry");
    public string InvColorKeyLabel => Loc.Admin("invColorKey", "Color key:");
    public string InvColorRed => Loc.Admin("invColorRed", "Red");
    public string InvColorOrange => Loc.Admin("invColorOrange", "Orange");
    public string InvColorBlue => Loc.Admin("invColorBlue", "Blue");
    public string InvColorNeutral => Loc.Admin("invColorNeutral", "Neutral");
    public string InvExpColorRedDesc => Loc.Admin("invExpLegendRed", " — expired or ≤7 days  ·  ");
    public string InvExpColorOrangeDesc => Loc.Admin("invExpLegendOrange", " — 8–14 days  ·  ");
    public string InvExpColorBlueDesc => Loc.Admin("invExpLegendBlue", " — 15+ days  ·  ");
    public string InvExpColorNeutralDesc => Loc.Admin("invExpLegendNeutral", " — no expiry date set");
    public string InvQtyColorRedDesc => Loc.Admin("invQtyLegendRed", " — out / critical stock (≤3)  ·  ");
    public string InvQtyColorOrangeDesc => Loc.Admin("invQtyLegendOrange", " — low stock (4–10)  ·  ");
    public string InvQtyColorBlueDesc => Loc.Admin("invQtyLegendBlue", " — healthy stock (11+)");
    public string InvStockPrefix => Loc.Admin("invStockColon", "Stock:");
    public string InvStockStatusPrefix => Loc.Admin("invStockStatus", "Stock status:");
    public string InvExpiresPrefix => Loc.Admin("invExpiresColon", "Expires:");
    public string InvEditLabel => Loc.Common("edit", "Edit");
    public string InvAddDeductLabel => Loc.Admin("invAddDeduct", "Add / deduct");
    public string InvDeleteLabel => Loc.Common("delete", "Delete");
    public string InvItemNameLabel => Loc.Admin("invFieldItemName", "ITEM NAME");
    public string InvUnitLabel => Loc.Admin("invFieldUnit", "UNIT");
    public string InvStockQtyLabel => Loc.Admin("invFieldStockQty", "STOCK QUANTITY");
    public string InvExpirationDateLabel => Loc.Admin("invFieldExpiration", "EXPIRATION DATE (YYYY-MM-DD)");
    public string InvNotesLabel => Loc.Admin("invFieldNotes", "NOTES");
    public string InvCancelLabel => Loc.Common("cancel", "Cancel");
    public string InvSaveItemLabel => Loc.Admin("invSaveItem", "Save Item");
    public string InvAdjustmentTitle => Loc.Admin("invAdjustDialog", "Manual Inventory Adjustment");
    public string InvAdjustmentTypeLabel => Loc.Admin("invAdjustType", "ADJUSTMENT TYPE");
    public string InvQuantityLabel => Loc.Admin("invQuantity", "QUANTITY");
    public string InvCommentRequiredLabel => Loc.Admin("invCommentRequired", "COMMENT (required)");
    public string InvApplyAdjustmentLabel => Loc.Admin("invApplyDeduction", "Apply Adjustment");

    public InventoryViewModel(Action<BaseViewModel> navigate) : base(navigate)
    {
        OpenAddDialogCommand = new RelayCommand(_ => OpenAddDialog());
        ToggleExpirationViewCommand = new RelayCommand(_ => SetInventoryViewMode(ExpirationViewActive ? "Default" : "Expiration"));
        ToggleQuantityViewCommand = new RelayCommand(_ => SetInventoryViewMode(QuantityViewActive ? "Default" : "Quantity"));
        EditItemCommand = new RelayCommand(item => OpenEditDialog(item as InventoryItem));
        DeleteItemCommand = new RelayCommand(item => _ = DeleteItemAsync(item as InventoryItem));
        OpenAdjustDialogCommand = new RelayCommand(item => OpenAdjustDialog(item as InventoryItem));
        ApplyAdjustmentCommand = new RelayCommand(_ => ApplyAdjustment());
        CancelAdjustmentDialogCommand = new RelayCommand(_ => CloseAdjustmentDialog());
        SaveItemCommand = new RelayCommand(_ => SaveItem());
        CancelDialogCommand = new RelayCommand(_ => CloseDialog());

        RefreshAdjustmentTypeOptions();
        _ = LoadItemsAsync();
    }

    protected override void RefreshLocalizedStrings()
    {
        base.RefreshLocalizedStrings();
        RefreshAdjustmentTypeOptions();
        InventoryUiLocalizer.ApplyAll(_allInventoryItems);
        ApplyInventoryFilter();
        if (IsDialogOpen)
        {
            DialogTitle = IsEditingExistingItem
                ? Loc.Admin("invEditDialog", "Edit Inventory Item")
                : Loc.Admin("invAddDialog", "Add Inventory Item");
            if (IsEditingExistingItem)
                Notes = InventoryUiLocalizer.TranslateNotesForDisplay(_rawNotes);
        }

        Notify(
            nameof(InvTitle),
            nameof(InvSubtitle),
            nameof(InvSortExpLabel),
            nameof(InvSortQtyLabel),
            nameof(InvSortExpTooltip),
            nameof(InvSortQtyTooltip),
            nameof(InvAddItemLabel),
            nameof(InvSearchTooltip),
            nameof(InvColorKeyLabel),
            nameof(InvColorRed),
            nameof(InvColorOrange),
            nameof(InvColorBlue),
            nameof(InvColorNeutral),
            nameof(InvExpColorRedDesc),
            nameof(InvExpColorOrangeDesc),
            nameof(InvExpColorBlueDesc),
            nameof(InvExpColorNeutralDesc),
            nameof(InvQtyColorRedDesc),
            nameof(InvQtyColorOrangeDesc),
            nameof(InvQtyColorBlueDesc),
            nameof(InvStockPrefix),
            nameof(InvStockStatusPrefix),
            nameof(InvExpiresPrefix),
            nameof(InvEditLabel),
            nameof(InvAddDeductLabel),
            nameof(InvDeleteLabel),
            nameof(InvItemNameLabel),
            nameof(InvUnitLabel),
            nameof(InvStockQtyLabel),
            nameof(InvExpirationDateLabel),
            nameof(InvNotesLabel),
            nameof(InvCancelLabel),
            nameof(InvSaveItemLabel),
            nameof(InvAdjustmentTitle),
            nameof(InvAdjustmentTypeLabel),
            nameof(InvQuantityLabel),
            nameof(InvCommentRequiredLabel),
            nameof(InvApplyAdjustmentLabel),
            nameof(DialogTitle));
    }

    private void RefreshAdjustmentTypeOptions()
    {
        var selected = SelectedAdjustmentType;
        AdjustmentTypeOptions.Clear();
        AdjustmentTypeOptions.Add(new InventoryAdjustmentOption
        {
            Key = "Add",
            Label = Loc.Admin("invAdjustAdd", "Add")
        });
        AdjustmentTypeOptions.Add(new InventoryAdjustmentOption
        {
            Key = "Deduct",
            Label = Loc.Admin("invAdjustDeduct", "Deduct")
        });
        if (string.IsNullOrWhiteSpace(selected))
            SelectedAdjustmentType = "Deduct";
        else
            SelectedAdjustmentType = selected;
    }

    private async Task LoadItemsAsync()
    {
        if (_isLoadingItems)
            return;

        _isLoadingItems = true;
        try
        {
            var items = (await _data.GetInventoryItemsAsync().ConfigureAwait(true))
                .OrderBy(i => i.Name)
                .ToList();

            _allInventoryItems.Clear();
            _allInventoryItems.AddRange(items);
            ApplyInventoryFilter();
            RefreshReadyPickupBanner();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                Loc.Admin("invLoadErrorBody", "Inventory could not be loaded safely.\n\n{{message}}",
                    new Dictionary<string, string> { ["message"] = ex.Message }),
                Loc.Admin("invLoadErrorTitle", "Inventory Load Error"),
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
        {
            InventoryUiLocalizer.Apply(item);
            InventoryItems.Add(item);
        }
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
        Notify(nameof(InvSortExpLabel), nameof(InvSortQtyLabel));
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
        DialogTitle = Loc.Admin("invAddDialog", "Add Inventory Item");
        ItemName = string.Empty;
        Unit = string.Empty;
        StockText = string.Empty;
        ExpirationDateText = string.Empty;
        _rawNotes = string.Empty;
        Notes = string.Empty;
        IsDialogOpen = true;
    }

    private void OpenEditDialog(InventoryItem? item)
    {
        if (item is null || AppSession.IsStaffTablet) return;
        _editingItemId = item.Id;
        IsEditingExistingItem = true;
        DialogTitle = Loc.Admin("invEditDialog", "Edit Inventory Item");
        ItemName = item.Name;
        Unit = item.Unit;
        StockText = item.StockQuantity.ToString("0.##", CultureInfo.InvariantCulture);
        ExpirationDateText = item.ExpirationDate?.ToString("yyyy-MM-dd") ?? string.Empty;
        _rawNotes = item.Notes ?? string.Empty;
        Notes = InventoryUiLocalizer.TranslateNotesForDisplay(_rawNotes);
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
                    Loc.Admin("invInvalidExpirationDate", "Expiration date format is invalid. Use YYYY-MM-DD."),
                    Loc.Admin("invValidationTitle", "Validation"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            expirationDate = parsedExpiration.Date;
        }

        try
        {
            if (_editingItemId is int itemId)
            {
                var shell = _allInventoryItems.FirstOrDefault(i => i.Id == itemId)
                    ?? throw new InvalidOperationException("Item not found. Refresh and try again.");
                var toSave = new InventoryItem
                {
                    Id = shell.Id,
                    UniqueId = shell.UniqueId,
                    Name = ItemName.Trim(),
                    Unit = Unit.Trim(),
                    StockQuantity = shell.StockQuantity,
                    ExpirationDate = expirationDate,
                    Notes = _rawNotes.Trim()
                };
                DesktopCloudPersistence.PushUpsertBlocking(toSave);
            }
            else
            {
                var confirm = MessageBox.Show(
                    Loc.Admin("invConfirmAddBody", "Add this inventory item?"),
                    Loc.Admin("invConfirmAddTitle", "Confirm Add Inventory Item"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirm != MessageBoxResult.Yes)
                    return;

                var newItem = new InventoryItem
                {
                    UniqueId = UniqueIdGenerator.NewId("INV"),
                    Name = ItemName.Trim(),
                    Unit = Unit.Trim(),
                    StockQuantity = qty,
                    ExpirationDate = expirationDate,
                    Notes = Notes.Trim()
                };
                DesktopCloudPersistence.PushUpsertBlocking(newItem);
            }

            CloseDialog();
            _ = LoadItemsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.GetBaseException().Message,
                Loc.Admin("invSaveFailed", "Save inventory failed"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async Task DeleteItemAsync(InventoryItem? item)
    {
        if (item is null || AppSession.IsStaffTablet) return;

        var confirm = MessageBox.Show(
            Loc.Admin("invConfirmDeleteBody", "Delete inventory item '{{name}}'?",
                new Dictionary<string, string> { ["name"] = item.Name }),
            Loc.Admin("invConfirmDeleteTitle", "Confirm Delete Inventory Item"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            var links = await _data.GetProductIngredientsAsync().ConfigureAwait(true);
            if (links.Any(pi => pi.InventoryItemId == item.Id))
            {
                MessageBox.Show(
                    Loc.Admin("invDeleteBlocked", "This ingredient is used by menu items and cannot be deleted."),
                    Loc.Admin("invDeleteBlockedTitle", "Delete Blocked"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            DesktopCloudPersistence.PushDeleteBlocking(new InventoryItem { Id = item.Id, UniqueId = item.UniqueId });
            _ = LoadItemsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.GetBaseException().Message,
                Loc.Admin("invDeleteFailed", "Delete inventory failed"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OpenAdjustDialog(InventoryItem? item)
    {
        if (item is null || (AppSession.IsStaffTablet && !AppSession.IsKitchenBarTablet)) return;
        _adjustingItemId = item.Id;
        AdjustmentItemName = $"{item.Name} ({item.UniqueId})";
        SelectedAdjustmentType = "Deduct";
        AdjustmentQuantityText = string.Empty;
        AdjustmentComment = string.Empty;
        IsAdjustmentDialogOpen = true;
    }

    private void ApplyAdjustment()
    {
        if (AppSession.IsStaffTablet && !AppSession.IsKitchenBarTablet) return;

        if (_adjustingItemId is null)
            return;

        if (!decimal.TryParse(AdjustmentQuantityText, NumberStyles.Number, CultureInfo.InvariantCulture, out var deductionQty) || deductionQty <= 0)
            return;

        if (string.IsNullOrWhiteSpace(AdjustmentComment))
        {
            MessageBox.Show(
                Loc.Admin("invJustification", "Add a justification for this change."),
                Loc.Admin("invValidationTitle", "Validation"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            var shell = _allInventoryItems.FirstOrDefault(i => i.Id == _adjustingItemId.Value);
            if (shell is null)
                return;

            var adjustmentType = SelectedAdjustmentType.Trim();
            if (adjustmentType == "Deduct" && shell.StockQuantity < deductionQty)
            {
                MessageBox.Show(
                    Loc.Admin("invCannotDeduct",
                        "Cannot deduct {{qty}} {{unit}}. Current stock is {{stock}} {{unit}}.",
                        new Dictionary<string, string>
                        {
                            ["qty"] = deductionQty.ToString("0.##", CultureInfo.InvariantCulture),
                            ["unit"] = shell.Unit,
                            ["stock"] = shell.StockQuantity.ToString("0.##", CultureInfo.InvariantCulture)
                        }),
                    Loc.Admin("invInsufficientStock", "Insufficient Stock"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var newQty = adjustmentType == "Add"
                ? shell.StockQuantity + deductionQty
                : shell.StockQuantity - deductionQty;

            var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            var qty = deductionQty.ToString("0.##", CultureInfo.InvariantCulture);
            var logEntry = adjustmentType == "Add"
                ? Loc.Admin("invNoteStockAdded", "{{ts}} - Stock added {{qty}} {{unit}}: {{comment}}",
                    new Dictionary<string, string>
                    {
                        ["ts"] = ts,
                        ["qty"] = qty,
                        ["unit"] = shell.Unit,
                        ["comment"] = AdjustmentComment.Trim()
                    })
                : Loc.Admin("invNoteManualDeduction", "{{ts}} - Manual deduction {{qty}} {{unit}}: {{comment}}",
                    new Dictionary<string, string>
                    {
                        ["ts"] = ts,
                        ["qty"] = qty,
                        ["unit"] = shell.Unit,
                        ["comment"] = AdjustmentComment.Trim()
                    });
            var mergedNotes = string.IsNullOrWhiteSpace(shell.Notes) ? logEntry : $"{shell.Notes}\n{logEntry}";

            var toSave = new InventoryItem
            {
                Id = shell.Id,
                UniqueId = shell.UniqueId,
                Name = shell.Name,
                Unit = shell.Unit,
                StockQuantity = newQty,
                ExpirationDate = shell.ExpirationDate,
                Notes = mergedNotes
            };

            DesktopCloudPersistence.PushUpsertBlocking(toSave);
            CloseAdjustmentDialog();
            _ = LoadItemsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.GetBaseException().Message,
                Loc.Admin("invAdjustmentFailed", "Adjustment failed"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void CloseDialog()
    {
        IsDialogOpen = false;
        _editingItemId = null;
        IsEditingExistingItem = false;
        _rawNotes = string.Empty;
    }

    private void CloseAdjustmentDialog()
    {
        IsAdjustmentDialogOpen = false;
        _adjustingItemId = null;
    }

}
