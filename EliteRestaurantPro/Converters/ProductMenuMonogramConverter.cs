using System.Globalization;
using System.Windows.Data;
using EliteRestaurant.Core.Models;

namespace EliteRestaurantPro.Converters;

/// <summary>Shows a single-letter fallback on menu cards when no product photo is available.</summary>
public class ProductMenuMonogramConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Product { Name: var name } || string.IsNullOrWhiteSpace(name))
            return "?";

        var trimmed = name.Trim();
        return char.ToUpperInvariant(trimmed[0]).ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
