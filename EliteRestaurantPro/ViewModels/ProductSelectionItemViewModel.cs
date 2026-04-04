namespace EliteRestaurantPro.ViewModels;

public class ProductSelectionItemViewModel : BaseViewModel
{
    private bool _isSelected;
    private int _quantity = 1;

    public int ProductId { get; set; }
    public string UniqueId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string SubCategory { get; set; } = string.Empty;
    public decimal Price { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public int Quantity
    {
        get => _quantity;
        set
        {
            if (!SetField(ref _quantity, value < 1 ? 1 : value))
                return;
            OnPropertyChanged(nameof(LineTotal));
        }
    }

    public decimal LineTotal => Quantity * Price;
}
