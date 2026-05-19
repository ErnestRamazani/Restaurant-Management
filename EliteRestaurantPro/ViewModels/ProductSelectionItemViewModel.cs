namespace EliteRestaurantPro.ViewModels;

public class ProductSelectionItemViewModel : BaseViewModel
{
    private bool _isAvailable = true;
    private bool _isSelected;
    private int _quantity = 1;

    public int ProductId { get; set; }
    public string UniqueId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string SubCategory { get; set; } = string.Empty;
    public decimal Price { get; set; }

    public int PrepMinutes { get; set; }

    /// <summary>False when ingredient stock cannot cover at least one unit (same rule as guest menu).</summary>
    public bool IsAvailable
    {
        get => _isAvailable;
        set
        {
            if (!SetField(ref _isAvailable, value))
                return;
            OnPropertyChanged(nameof(CanIncreaseQuantity));
            OnPropertyChanged(nameof(AvailabilityHint));
            OnPropertyChanged(nameof(CanToggleProductRow));
        }
    }

    public bool CanIncreaseQuantity => IsAvailable;

    public bool CanToggleProductRow => IsAvailable || IsSelected;

    public string AvailabilityHint =>
        IsAvailable ? string.Empty : "Out of stock (ingredients)";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!SetField(ref _isSelected, value))
                return;
            OnPropertyChanged(nameof(LineTotal));
            OnPropertyChanged(nameof(CanIncreaseQuantity));
            OnPropertyChanged(nameof(CanToggleProductRow));
        }
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
