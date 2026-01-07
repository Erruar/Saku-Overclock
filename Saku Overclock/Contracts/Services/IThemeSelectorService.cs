using Microsoft.UI.Xaml;
using Saku_Overclock.Styles;
using static Saku_Overclock.Services.ThemeSelectorService;

namespace Saku_Overclock.Contracts.Services;

public interface IThemeSelectorService
{
    /// <summary>
    ///     Коллекция кастомных тем приложения
    /// </summary>
    List<ThemeClass> Themes
    {
        get;
    }

    /// <summary>
    ///     Инициализация тем приложения
    /// </summary>
    void Initialize();

    /// <summary>
    ///     Установить тип темы приложения (светлая или тёмная)
    /// </summary>
    /// <param name="theme">Тип темы приложения</param>
    /// <returns>Результат выполнения задачи</returns>
    void SetThemeAsync(ElementTheme theme);

    /// <summary>
    ///     Установить тип темы приложения (светлая или тёмная)
    /// </summary>
    /// <returns>Результат выполнения задачи</returns>
    void SetRequestedThemeAsync();

    /// <summary>
    ///     Применяет тему приложения (включая кастомные)
    /// </summary>
    /// <param name="themeType">Индекс применяемой темы</param>
    /// <returns>
    ///     ThemeApplyResult:
    ///     Фон темы,
    ///     Интенсивность цвета,
    ///     Прозрачность маски
    /// </returns>
    ThemeApplyResult UpdateAppliedTheme(int themeType);

    /// <summary>
    ///     Сохранить темы приложения
    /// </summary>
    void SaveThemeInSettings();
}