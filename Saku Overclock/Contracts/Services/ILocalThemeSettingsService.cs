using Saku_Overclock.Models;

namespace Saku_Overclock.Contracts.Services;

public interface ILocalThemeSettingsService
{
    public LocalThemeSettingsOptions LoadThemeSettings();

    public void SaveThemeSettings(LocalThemeSettingsOptions themeSettings);
}