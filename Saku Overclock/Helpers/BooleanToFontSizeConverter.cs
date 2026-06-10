using Windows.UI.Text;
using Microsoft.UI.Xaml.Data;

namespace Saku_Overclock.Helpers;

public partial class BooleanToFontSizeConverter: IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolean)
        {
            return boolean ? 14d : 13d;
        }

        return null;
    }

    public object? ConvertBack(object value, Type targetType, object parameter, string language) => null;
}