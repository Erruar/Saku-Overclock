using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Data;

namespace Saku_Overclock.Helpers;

public partial class DoubleToPercentConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double doubleValue)
        {
            if ((doubleValue - 50) > 0)
            {
                return $"+{doubleValue - 50}%";
            }
            return $"{doubleValue - 50}%";
        }
        return null;
    }

    public object? ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
