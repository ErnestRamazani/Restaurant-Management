using System.Globalization;
using System.Windows.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurantPro.Utils;

namespace EliteRestaurantPro.Converters;

public class ProductToMenuCardPhotoConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Product product || product.Id <= 0)
            return null;

        var url = MenuImagePreview.GetProductPhotoAssetUrl(product.Id);
        return MenuImagePreview.TryLoadFromPathOrUrl(url);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
