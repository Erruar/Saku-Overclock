using Microsoft.UI.Xaml.Data;

namespace Saku_Overclock.Helpers;

public partial class DoubleRound2Converter : IValueConverter
{
    /// <summary>
    ///     Double to 2-round converter
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, string input) =>
        Math.Round((double)value, 2);

    /// <summary>
    ///     Not used
    /// </summary>
    public object? ConvertBack(object value, Type targetType, object parameter, string input) =>
        null;
}

public partial class DoubleRound3Converter : IValueConverter
{
    /// <summary>
    ///     Double to 3-round converter
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, string input) =>
        Math.Round((double)value, 3);

    /// <summary>
    ///     Not used
    /// </summary>
    public object? ConvertBack(object value, Type targetType, object parameter, string input) =>
        null;
}