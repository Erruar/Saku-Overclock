using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
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

    public void Initialize()
    {
        LoadThemeFromSettings();
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

    public sealed class ThemeApplyResult
    {
        public ImageSource? BackgroundImageSource
        {
            get; init;
        }
        public double ThemeOpacity
        {
            get; init;
        }
        public double ThemeMaskOpacity
        {
            get; init;
        }
        public string? ThemeApplyMessage
        {
            get; init; 
        }
    }

    public async Task<ThemeApplyResult> UpdateAppliedTheme(int themeType)
    {
        const int BuiltInThemesCount = 2;

        ImageSource? imageSource = null;
        double themeOpacity = 1;
        double themeMaskOpacity = 1;
        var message = string.Empty;

        if (themeType < Themes.Count && themeType > -1)
        {
            var themeLight = Themes[themeType].ThemeLight
                    ? ElementTheme.Light
                    : ElementTheme.Dark;
            await SetThemeAsync(themeType == 0 ? ElementTheme.Default : themeLight);
            if (Themes[themeType].ThemeCustomBg ||
                Themes[themeType].ThemeName.Contains("Theme_"))
            {
                var themeBackground = Themes[themeType].ThemeBackground;

                if (themeType > BuiltInThemesCount &&
                    !string.IsNullOrEmpty(themeBackground) &&
                    (themeBackground.Contains("http") || themeBackground.Contains("appx") ||
                     File.Exists(themeBackground)))
                {
                    try
                    {
                        imageSource = new BitmapImage(new Uri(themeBackground));
                    }
                    catch
                    {
                        message = "ThemeNotFoundBg".GetLocalized();
                    }
                }
            }
            
            themeOpacity = Themes[themeType].ThemeOpacity;
            themeMaskOpacity = Themes[themeType].ThemeMaskOpacity;
        }
        else
        {
            message = "ThemeNotFound".GetLocalized();
        }

        return new ThemeApplyResult()
        {
            BackgroundImageSource = imageSource,
            ThemeOpacity = themeOpacity,
            ThemeMaskOpacity = themeMaskOpacity,
            ThemeApplyMessage = message
        };
    }

    public void LoadThemeFromSettings()
    {
        var retValue = localThemeSettingsService.LoadThemeSettings();
        if (retValue != null)
        {
            Themes = retValue.CustomThemes;

            try
            {
                if (Enum.TryParse(retValue.AppBackgroundRequestedTheme,
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