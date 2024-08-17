using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Saku_Overclock.Helpers;
class HexColorInverterConverter : IValueConverter
{
    public HexColorInverterConverter()
    {
    }
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        try
        {
            var newColor = string.Empty;
            if (value is SolidColorBrush colBrush)
            {
                var color = colBrush.Color;
                // Преобразуем цвет в строку HEX
                newColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            }
            else if (value is Color color)
            {
                newColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            }
            else if (value is string colString)
            {
                // Копируем значение строки
                newColor = colString;
            }
            var r = 0;
            var g = 0;
            var b = 0;
            if (!string.IsNullOrEmpty(newColor))
            {
                // Убираем символ #, если он присутствует
                var valuestring = newColor.TrimStart('#');
                // Парсим цветовые компоненты
                r = System.Convert.ToInt32(valuestring!.Substring(0, 2), 16);
                g = System.Convert.ToInt32(valuestring!.Substring(2, 2), 16);
                b = System.Convert.ToInt32(valuestring!.Substring(4, 2), 16);
            }
            r = 255 - r;
            g = 255 - g;
            b = 255 - b;
            return new SolidColorBrush(Color.FromArgb(255, System.Convert.ToByte(r), System.Convert.ToByte(g), System.Convert.ToByte(b)));
        }
        catch
        {
            return new SolidColorBrush(Color.FromArgb(0, 249, 255, 163));
        } 
        throw new ArgumentException("Invalid Hex Color");
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return Convert(value, targetType, parameter, language);
        throw new ArgumentException("Invalid Hex Color");
    }
}
