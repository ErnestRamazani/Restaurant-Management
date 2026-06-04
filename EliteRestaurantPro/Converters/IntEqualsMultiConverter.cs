using System.Globalization;
using System.Windows.Data;

namespace EliteRestaurantPro.Converters;

public sealed class IntEqualsMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is not { Length: >= 2 })
            return false;

        if (!TryToInt(values[0], out var left) || !TryToInt(values[1], out var right))
            return false;

        return left == right;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static bool TryToInt(object? value, out int result)
    {
        result = 0;
        if (value is null)
            return false;
        if (value is int i)
        {
            result = i;
            return true;
        }

        return int.TryParse(value.ToString(), out result);
    }
}
