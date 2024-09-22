using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace Saku_Overclock.Helpers; 
public class FocusToSpinButtonPlacementModeConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is FocusState focusState)
        { 
            if (focusState > 0)
            {
                return NumberBoxSpinButtonPlacementMode.Hidden;
            }
            else
            {
                return NumberBoxSpinButtonPlacementMode.Inline;
            }
        }
        else if (value is bool focusState1)
        {
            if (focusState1 == true)
            {
                return NumberBoxSpinButtonPlacementMode.Hidden;
            }
            else
            {
                return NumberBoxSpinButtonPlacementMode.Inline;
            }
        }
        return null;
    }

    public object? ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return null;
    }
}