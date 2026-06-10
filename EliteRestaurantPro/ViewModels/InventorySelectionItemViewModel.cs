using System.Globalization;

namespace EliteRestaurantPro.ViewModels;

public class InventorySelectionItemViewModel : BaseViewModel
{
    private bool _isSelected;
    private decimal _quantity = 1m;
    private string _quantityText = "1";

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

    public string QuantityText
    {
        get => _quantityText;
        set => SetField(ref _quantityText, value);
    }

    public decimal Quantity
    {
        get => _quantity;
        set => ApplyQuantity(value <= 0 ? 0.1m : value, syncText: true);
    }

    public void CommitQuantityFromText()
    {
        var raw = NormalizeQuantityInput(_quantityText);
        if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var qty) && qty > 0)
        {
            ApplyQuantity(qty, syncText: true);
            return;
        }

        SyncQuantityTextFromValue();
    }

    private static string NormalizeQuantityInput(string? text)
    {
        var raw = (text ?? string.Empty).Trim().Replace(',', '.');
        if (raw.StartsWith('.'))
            raw = "0" + raw;
        return raw;
    }

    public void ResetQuantity(decimal qty = 1m)
    {
        ApplyQuantity(qty <= 0 ? 0.1m : qty, syncText: true);
    }

    private void ApplyQuantity(decimal normalized, bool syncText)
    {
        if (!SetField(ref _quantity, normalized))
        {
            if (syncText)
                SyncQuantityTextFromValue();
            return;
        }

        if (syncText)
            SyncQuantityTextFromValue();
    }

    private void SyncQuantityTextFromValue()
    {
        var formatted = FormatQuantity(_quantity);
        if (_quantityText == formatted)
            return;

        _quantityText = formatted;
        OnPropertyChanged(nameof(QuantityText));
    }

    private static string FormatQuantity(decimal qty) =>
        qty.ToString("0.###", CultureInfo.InvariantCulture);
}
