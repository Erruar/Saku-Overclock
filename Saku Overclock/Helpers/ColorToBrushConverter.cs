using Windows.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Saku_Overclock.Helpers;

public partial class ColorToBrushConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is Color color)
        {
            return new SolidColorBrush(color);
        }

        return null;
    }

    public object? ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is SolidColorBrush brush)
        {
            return brush.Color;
        }

        return null;
    }
}