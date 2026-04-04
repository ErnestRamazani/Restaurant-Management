namespace EliteRestaurantPro.ViewModels;

public class InventorySelectionItemViewModel : BaseViewModel
{
    private bool _isSelected;
    private decimal _quantity = 1m;

    public int InventoryItemId { get; set; }
    public string UniqueId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal StockQuantity { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public decimal Quantity
    {
        get => _quantity;
        set => SetField(ref _quantity, value <= 0 ? 0.1m : value);
    }
}
