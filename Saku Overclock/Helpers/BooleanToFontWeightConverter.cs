using Windows.UI.Text;
using Microsoft.UI.Xaml.Data;

namespace Saku_Overclock.Helpers;

public partial class BooleanToFontWeightConverter: IValueConverter
{
    /// <summary>
    ///     Convert bool to font weight
    /// </summary>
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolean)
        {
            return boolean ? new FontWeight(500) : new FontWeight(400);
        }

        return null;
    }

    /// <summary>
    ///     Not used
    /// </summary>
    public object? ConvertBack(object value, Type targetType, object parameter, string language) => null;
}