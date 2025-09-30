using Saku_Overclock.Styles;

namespace Saku_Overclock.Models;

public class LocalThemeSettingsOptions
{
    /// <summary>
    ///     Основная строка настройки темы
    /// </summary>
    public string AppBackgroundRequestedTheme
    {
        get;
        init;
    } = "Default";

    /// <summary>
    ///     Массив кастомных тем
    /// </summary>
    public List<ThemeClass> CustomThemes
    {
        get;
        init;
    } = [];
}