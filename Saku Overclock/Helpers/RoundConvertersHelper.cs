using Microsoft.UI.Xaml.Data;

namespace Saku_Overclock.Helpers;

public partial class DoubleRound2Converter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string input) => Math.Round((double)value, 2);
    public object ConvertBack(object value, Type targetType, object parameter, string input) => new NotImplementedException();
}

public partial class DoubleRound3Converter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string input) => Math.Round((double)value, 3);
    public object ConvertBack(object value, Type targetType, object parameter, string input) => new NotImplementedException();
}
