using Microsoft.UI.Xaml.Data;

namespace Saku_Overclock.Helpers;

public partial class BooleanToOpacityConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolean)
        {
            return boolean ? 0.4 : 1;
        }

        return null;
    }

    public object? ConvertBack(object value, Type targetType, object parameter, string language) => null;
}