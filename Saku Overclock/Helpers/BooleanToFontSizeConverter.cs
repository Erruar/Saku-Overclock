using Microsoft.UI.Xaml.Data;

namespace Saku_Overclock.Helpers;

public partial class BooleanToFontSizeConverter: IValueConverter
{
    /// <summary>
    ///     Convert bool to text size
    /// </summary>
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolean)
        {
            return boolean ? 14d : 13d;
        }

        return null;
    }

    /// <summary>
    ///     Not used
    /// </summary>
    public object? ConvertBack(object value, Type targetType, object parameter, string language) => null;
}