using Microsoft.UI.Xaml.Data;

namespace Saku_Overclock.Helpers;

public partial class BooleanToGlyphConverter : IValueConverter
{
    /// <summary>
    ///     Convert bool to glyph
    /// </summary>
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolean)
        {
            return boolean ? "\uE73E" : "\uE711";
        }

        return null;
    }

    /// <summary>
    ///     Not used
    /// </summary>
    public object? ConvertBack(object value, Type targetType, object parameter, string language) => null;
}