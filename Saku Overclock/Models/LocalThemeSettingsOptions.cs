using Saku_Overclock.Styles;

namespace Saku_Overclock.Models;
public class LocalThemeSettingsOptions
{
    public string AppBackgroundRequestedTheme { get; set; } = "Default"; // Основная строка настройки темы
    public List<ThemeClass> CustomThemes { get; set; } = []; // Массив кастомных тем
}
