using Windows.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Saku_Overclock.Helpers;

internal partial class HexColorInverterConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        try
        {
            var newColor = string.Empty;
            switch (value)
            {
                case SolidColorBrush colBrush:
                {
                    var color = colBrush.Color;
                    newColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                    break;
                }
                case Color color:
                    newColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                    break;
                case string colString:
                    newColor = colString;
                    break;
            }

            var r = 0;
            var g = 0;
            var b = 0;

            if (!string.IsNullOrEmpty(newColor))
            {
                // Убираем символ #, если он присутствует
                var valuestring = newColor.TrimStart('#');

                // Парсим цветовые компоненты
                r = System.Convert.ToInt32(valuestring[..2], 16);
                g = System.Convert.ToInt32(valuestring.Substring(2, 2), 16);
                b = System.Convert.ToInt32(valuestring.Substring(4, 2), 16);
            }

            r = 255 - r;
            g = 255 - g;
            b = 255 - b;

            return new SolidColorBrush(Color.FromArgb(255, System.Convert.ToByte(r), System.Convert.ToByte(g),
                System.Convert.ToByte(b)));
        }
        catch
        {
            return new SolidColorBrush(Color.FromArgb(0, 249, 255, 163));
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        Convert(value, targetType, parameter, language);
}