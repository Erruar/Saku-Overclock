using Saku_Overclock.Shared.Models;

namespace Saku_Overclock.Contracts.Services;

public interface ILocalThemeSettingsService
{
    /// <summary>
    ///     Load application theme settings
    /// </summary>
    /// <returns>LocalThemeSettingsOptions themes</returns>
    LocalThemeSettingsOptions? LoadThemeSettings();

    /// <summary>
    ///     Save application theme settings
    /// </summary>
    /// <param name="themeSettings">LocalThemeSettingsOptions themes</param>
    void SaveThemeSettings(LocalThemeSettingsOptions themeSettings);

    /// <summary>
    ///     Return default themes
    /// </summary>
    /// <returns>Default application themes</returns>
    List<ThemeClass> GetDefaultThemes();
}