using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Data;

namespace Saku_Overclock.Helpers;

public partial class BooleanToGlyphConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolean)
        {
            return boolean ? "\uE73E" : "\uE711";
        }
        return null;
    }

    public object? ConvertBack(object value, Type targetType, object parameter, string language)
    { 
        return null;
    }
}
