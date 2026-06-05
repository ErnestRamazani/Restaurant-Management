using System.Globalization;
using System.Windows.Data;
using EliteRestaurantPro.Localization;

namespace EliteRestaurantPro.Converters;

public sealed class MenuIngredientCountConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var count = value switch
        {
            int i => i,
            _ when int.TryParse(value?.ToString(), out var parsed) => parsed,
            _ => 0
        };

        return Loc.Admin("menuIngredientsHeader", "Ingredients ({{count}})",
            new Dictionary<string, string>
            {
                ["count"] = count.ToString(CultureInfo.InvariantCulture)
            });
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
