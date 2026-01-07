using Saku_Overclock.Models;
using Saku_Overclock.Styles;

namespace Saku_Overclock.Contracts.Services;

public interface ILocalThemeSettingsService
{
    /// <summary>
    ///     Загрузить параметры тем приложения
    /// </summary>
    /// <returns></returns>
    LocalThemeSettingsOptions? LoadThemeSettings();

    /// <summary>
    ///     Сохранить темы приложения
    /// </summary>
    /// <param name="themeSettings">Темы приложения</param>
    void SaveThemeSettings(LocalThemeSettingsOptions themeSettings);

    /// <summary>
    ///     Возвращает дефолтные темы приложения
    /// </summary>
    /// <returns>Дефолтные темы приложения</returns>
    List<ThemeClass> GetDefaultThemes();
}