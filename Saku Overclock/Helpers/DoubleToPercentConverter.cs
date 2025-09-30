using Microsoft.UI.Xaml.Data;

namespace Saku_Overclock.Helpers;

public partial class DoubleToPercentConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double doubleValue)
        {
            return doubleValue - 50 > 0 ? $"+{doubleValue - 50}%" : $"{doubleValue - 50}%";
        }

        return null;
    }

    public object? ConvertBack(object value, Type targetType, object parameter, string language) => null;
}