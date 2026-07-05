using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Shared;
using Saku_Overclock.Shared.Models;

namespace Saku_Overclock.Services;

public class LocalThemeSettingsService(IpcConnectionService ipc)
    : SimpleIpcSettingsBase<LocalThemeSettingsOptions>(
            ipc, "ThemeSettings", IpcJsonContext.Default.LocalThemeSettingsOptions,
            new LocalThemeSettingsOptions { AppBackgroundRequestedTheme = "Default", CustomThemes = DefaultThemesProvider.DefaultThemes }),
        ILocalThemeSettingsService
{
    public LocalThemeSettingsOptions LoadThemeSettings() => Get(s => s);
    
    /// <summary>
    ///    Implementation for more variables
    /// </summary>
    /// <param name="themeSettings"></param>
    // public void SaveThemeSettings(LocalThemeSettingsOptions themeSettings) => Set(_ => { });
    public void SaveThemeSettings(LocalThemeSettingsOptions themeSettings) =>
        Set(cache =>
        {
            cache.AppBackgroundRequestedTheme = themeSettings.AppBackgroundRequestedTheme;
            cache.CustomThemes = themeSettings.CustomThemes;
        });
    
    public List<ThemeClass> GetDefaultThemes() => DefaultThemesProvider.DefaultThemes;
}