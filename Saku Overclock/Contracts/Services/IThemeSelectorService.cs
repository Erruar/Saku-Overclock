using Microsoft.UI.Xaml;
using Saku_Overclock.Styles;
using static Saku_Overclock.Services.ThemeSelectorService;

namespace Saku_Overclock.Contracts.Services;

public interface IThemeSelectorService
{
    ElementTheme Theme
    {
        get;
    }

    List<ThemeClass> Themes
    {
        get;
    }

    void Initialize();

    Task SetThemeAsync(ElementTheme theme);

    Task SetRequestedThemeAsync();
    Task<ThemeApplyResult> UpdateAppliedTheme(int themeType);

    void LoadThemeFromSettings();
    void SaveThemeInSettings();
}