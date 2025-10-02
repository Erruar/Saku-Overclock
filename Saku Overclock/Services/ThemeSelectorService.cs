using Microsoft.UI.Xaml;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Models;
using Saku_Overclock.Styles;

namespace Saku_Overclock.Services;

public class ThemeSelectorService(ILocalThemeSettingsService localThemeSettingsService) : IThemeSelectorService
{
    public ElementTheme Theme
    {
        get;
        private set;
    } = ElementTheme.Default;

    public List<ThemeClass> Themes
    {
        get;
        set;
    } = localThemeSettingsService.GetDefaultThemes();

    public async Task InitializeAsync()
    {
        LoadThemeFromSettings();
        await Task.CompletedTask;
    }

    public async Task SetThemeAsync(ElementTheme theme)
    {
        Theme = theme;

        await SetRequestedThemeAsync();
        SaveThemeInSettings();
    }

    public async Task SetRequestedThemeAsync()
    {
        if (App.MainWindow.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = Theme;

            TitleBarHelper.UpdateTitleBar(Theme);
        }

        await Task.CompletedTask;
    }

    public void LoadThemeFromSettings()
    {
        var retValue = localThemeSettingsService.LoadThemeSettings();
        if (retValue != null)
        {
            Themes = retValue.CustomThemes;

            try
            {
                if (Enum.TryParse(localThemeSettingsService.LoadThemeSettings().AppBackgroundRequestedTheme,
                        out ElementTheme cacheTheme))
                {
                    Theme = cacheTheme;
                }
            }
            catch
            {
                Theme = ElementTheme.Default;
            }
        }
    }

    public void SaveThemeInSettings()
    {
        localThemeSettingsService.SaveThemeSettings(new LocalThemeSettingsOptions
            { CustomThemes = Themes, AppBackgroundRequestedTheme = Theme.ToString() });
    }
}