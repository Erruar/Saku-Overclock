using Microsoft.UI.Xaml;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Core.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Styles;

namespace Saku_Overclock.Services;

public class ThemeSelectorService : IThemeSelectorService
{
    private const string SettingsKey = "AppBackgroundRequestedTheme";

    public ElementTheme Theme
    {
        get;
        set;
    } = ElementTheme.Default;

    private readonly ILocalSettingsService _localThemeSettingsService;

    public List<ThemeClass> Themes
    {
        get;
        set;
    }

    public ThemeSelectorService(ILocalSettingsService localThemeSettingsService)
    {
        _localThemeSettingsService = localThemeSettingsService;
        Themes =
        [
            new()
            {
                ThemeName = "Theme_Default",
                ThemeLight = false,
                ThemeCustom = false,
                ThemeOpacity = 0.0,
                ThemeMaskOpacity = 0.0,
                ThemeCustomBg = false,
                ThemeBackground = ""
            },
            new()
            {
                ThemeName = "Theme_Light",
                ThemeLight = true,
                ThemeCustom = false,
                ThemeOpacity = 0.0,
                ThemeMaskOpacity = 0.0,
                ThemeCustomBg = false,
                ThemeBackground = ""
            },
            new()
            {
                ThemeName = "Theme_Dark",
                ThemeLight = false,
                ThemeCustom = false,
                ThemeOpacity = 0.0,
                ThemeMaskOpacity = 0.0,
                ThemeCustomBg = false,
                ThemeBackground = ""
            },
            new()
            {
                ThemeName = "Theme_Clouds",
                ThemeLight = true,
                ThemeCustom = false,
                ThemeOpacity = 0.5,
                ThemeMaskOpacity = 1.0,
                ThemeCustomBg = false,
                ThemeBackground = "https://i.imgur.com/DuwlKmK.png"
            },
            new()
            {
                ThemeName = "Theme_Neon",
                ThemeLight = false,
                ThemeCustom = false,
                ThemeOpacity = 0.3,
                ThemeMaskOpacity = 0.3,
                ThemeCustomBg = false,
                ThemeBackground = "https://i.imgur.com/DuwlKmK.png"
            },
            new()
            {
                ThemeName = "Theme_Raspberry",
                ThemeLight = true,
                ThemeCustom = false,
                ThemeOpacity = 1.0,
                ThemeMaskOpacity = 1.0,
                ThemeCustomBg = false,
                ThemeBackground = "https://i.imgur.com/fw41KXN.png"
            },
            new()
            {
                ThemeName = "Theme_Sand",
                ThemeLight = true,
                ThemeCustom = false,
                ThemeOpacity = 0.7,
                ThemeMaskOpacity = 1.0,
                ThemeCustomBg = false,
                ThemeBackground = "https://i.imgur.com/ZqjqlOs.png"
            },
            new()
            {
                ThemeName = "Theme_Coffee",
                ThemeLight = false,
                ThemeCustom = false,
                ThemeOpacity = 0.3,
                ThemeMaskOpacity = 1.0,
                ThemeCustomBg = false,
                ThemeBackground = "https://i.imgur.com/ZqjqlOs.png"
            }
        ];
    }

    public async Task InitializeAsync()
    {
        Theme = await LoadThemeFromSettingsAsync();
        LoadCustomTheme();
        await Task.CompletedTask;
    }

    public async Task SetThemeAsync(ElementTheme theme)
    {
        Theme = theme;

        await SetRequestedThemeAsync();
        await SaveThemeInSettingsAsync(Theme);
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
    
    public void LoadCustomTheme()
    {
        Themes = _localThemeSettingsService.LoadCustomThemes();
    }

    public void SaveCustomTheme()
    {
        _localThemeSettingsService.SaveCustomThemes(Themes);
    }

    private async Task<ElementTheme> LoadThemeFromSettingsAsync()
    {
        try
        {
            var themeName = await _localThemeSettingsService.ReadSettingAsync<string>(SettingsKey);
            if (Enum.TryParse(themeName, out ElementTheme cacheTheme))
            {
                return cacheTheme;
            }
        }
        catch
        {
            return ElementTheme.Default;
        }
        return ElementTheme.Default;
    } 

    private async Task SaveThemeInSettingsAsync(ElementTheme theme)
    {
        await _localThemeSettingsService.SaveSettingAsync(SettingsKey, theme.ToString());
    }
}