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
    private ElementTheme _theme = ElementTheme.Default;

    public List<ThemeClass> Themes
    {
        get;
        set;
    } = localThemeSettingsService.GetDefaultThemes();

    public void Initialize()
    {
        LoadThemeFromSettings();
    }

    public void SetThemeAsync(ElementTheme theme)
    {
        _theme = theme;

        SetRequestedThemeAsync();
        SaveThemeInSettings();
    }

    public void SetRequestedThemeAsync()
    {
        if (App.MainWindow.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = _theme;

            TitleBarHelper.UpdateTitleBar(_theme);
        }
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
    }

    public ThemeApplyResult UpdateAppliedTheme(int themeType)
    {
        const int builtInThemesCount = 2;

        ImageSource? imageSource = null;
        double themeOpacity = 1;
        double themeMaskOpacity = 1;

        if (themeType < Themes.Count && themeType > -1)
        {
            var themeLight = Themes[themeType].ThemeLight
                    ? ElementTheme.Light
                    : ElementTheme.Dark;
            SetThemeAsync(themeType == 0 ? ElementTheme.Default : themeLight);
            if (Themes[themeType].ThemeCustomBg ||
                Themes[themeType].ThemeName.Contains("Theme_"))
            {
                var themeBackground = Themes[themeType].ThemeBackground;

                if (themeType > builtInThemesCount &&
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
                        LogHelper.TraceIt_TraceError("ThemeNotFoundBg".GetLocalized());
                    }
                }
            }
            
            themeOpacity = Themes[themeType].ThemeOpacity;
            themeMaskOpacity = Themes[themeType].ThemeMaskOpacity;
        }
        else
        {
            LogHelper.TraceIt_TraceError("ThemeNotFound".GetLocalized());
        }

        return new ThemeApplyResult
        {
            BackgroundImageSource = imageSource,
            ThemeOpacity = themeOpacity,
            ThemeMaskOpacity = themeMaskOpacity
        };
    }

    private void LoadThemeFromSettings()
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
                    _theme = cacheTheme;
                }
            }
            catch
            {
                _theme = ElementTheme.Default;
            }
        }
    }

    public void SaveThemeInSettings()
    {
        localThemeSettingsService.SaveThemeSettings(new LocalThemeSettingsOptions
        {
            CustomThemes = Themes, 
            AppBackgroundRequestedTheme = _theme.ToString()
        });
    }
}