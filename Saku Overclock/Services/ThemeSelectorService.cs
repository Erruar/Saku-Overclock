using Microsoft.UI.Xaml;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Styles;

namespace Saku_Overclock.Services;

public class ThemeSelectorService : IThemeSelectorService
{
    public ElementTheme Theme
    {
        get;
        set;
    } = ElementTheme.Default;

    private readonly ILocalThemeSettingsService _localThemeSettingsService;

    public List<ThemeClass> Themes
    {
        get;
        set;
    }

    public ThemeSelectorService(ILocalThemeSettingsService localThemeSettingsService)
    {
        _localThemeSettingsService = localThemeSettingsService;
        Themes = [
        new ()
        {
            ThemeName = "Theme_Default",
            ThemeLight = false,
            ThemeCustom = false,
            ThemeOpacity = 0.0,
            ThemeMaskOpacity = 0.0,
            ThemeCustomBg = false,
            ThemeBackground = ""
        },
        new ()
        {
            ThemeName = "Theme_Light",
            ThemeLight = true,
            ThemeCustom = false,
            ThemeOpacity = 0.0,
            ThemeMaskOpacity = 0.0,
            ThemeCustomBg = false,
            ThemeBackground = ""
        },
        new ()
        {
            ThemeName = "Theme_Dark",
            ThemeLight = false,
            ThemeCustom = false,
            ThemeOpacity = 0.0,
            ThemeMaskOpacity = 0.0,
            ThemeCustomBg = false,
            ThemeBackground = ""
        },
        new ()
        {
            ThemeName = "Theme_Clouds",
            ThemeLight = true,
            ThemeCustom = false,
            ThemeOpacity = 0.5,
            ThemeMaskOpacity = 1.0,
            ThemeCustomBg = false,
            ThemeBackground = "ms-appx:///Assets/Themes/DuwlKmK.png"
        },
        new ()
        {
            ThemeName = "Theme_Neon",
            ThemeLight = false,
            ThemeCustom = false,
            ThemeOpacity = 0.3,
            ThemeMaskOpacity = 0.3,
            ThemeCustomBg = false,
            ThemeBackground = "ms-appx:///Assets/Themes/DuwlKmK.png"
        },
        new ()
        {
            ThemeName = "Theme_Raspberry",
            ThemeLight = true,
            ThemeCustom = false,
            ThemeOpacity = 1.0,
            ThemeMaskOpacity = 1.0,
            ThemeCustomBg = false,
            ThemeBackground = "ms-appx:///Assets/Themes/fw41KXN.png"
        },
        new ()
        {
            ThemeName = "Theme_Sand",
            ThemeLight = true,
            ThemeCustom = false,
            ThemeOpacity = 0.7,
            ThemeMaskOpacity = 1.0,
            ThemeCustomBg = false,
            ThemeBackground = "ms-appx:///Assets/Themes/ZqjqlOs.png"
        },
        new ()
        {
            ThemeName = "Theme_Coffee",
            ThemeLight = false,
            ThemeCustom = false,
            ThemeOpacity = 0.3,
            ThemeMaskOpacity = 1.0,
            ThemeCustomBg = false,
            ThemeBackground = "ms-appx:///Assets/Themes/ZqjqlOs.png"
        }
    ];
    }

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
        Themes = _localThemeSettingsService.LoadThemeSettings().CustomThemes;
        try
        {
            if (Enum.TryParse(_localThemeSettingsService.LoadThemeSettings().AppBackgroundRequestedTheme, out ElementTheme cacheTheme))
            {
                Theme = cacheTheme;
            }
        }
        catch
        {
            Theme = ElementTheme.Default;
        }
        
    }

    public void SaveThemeInSettings()
    {
        _localThemeSettingsService.SaveThemeSettings(new Models.LocalThemeSettingsOptions() { CustomThemes = Themes, AppBackgroundRequestedTheme = Theme.ToString()});
    } 
}