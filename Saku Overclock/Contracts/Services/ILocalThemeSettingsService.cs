using Saku_Overclock.Models;
using Saku_Overclock.Styles;

namespace Saku_Overclock.Contracts.Services;

public interface ILocalThemeSettingsService
{
    public LocalThemeSettingsOptions LoadThemeSettings();

    public void SaveThemeSettings(LocalThemeSettingsOptions themeSettings);
    List<ThemeClass> GetDefaultThemes();
}