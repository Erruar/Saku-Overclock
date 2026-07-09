using Microsoft.UI.Xaml.Data;

namespace Saku_Overclock.Helpers;

public partial class BooleanInverterConverter : IValueConverter
{
    /// <summary>
    ///     Invert bool
    /// </summary>
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolean)
        {
            return !boolean;
        }

        return null;
    }

    /// <summary>
    ///     Invert bool
    /// </summary>
    public object? ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolean)
        {
            return !boolean;
        }

        return null;
    }
}