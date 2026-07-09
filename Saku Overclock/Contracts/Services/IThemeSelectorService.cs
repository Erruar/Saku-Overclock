using Microsoft.UI.Xaml;
using Saku_Overclock.Shared.Models;
using Saku_Overclock.Styles;
using static Saku_Overclock.Services.ThemeSelectorService;

namespace Saku_Overclock.Contracts.Services;

public interface IThemeSelectorService
{
    /// <summary>
    ///     Application themes collection
    /// </summary>
    List<ThemeClass> Themes
    {
        get;
    }

    /// <summary>
    ///     Theme initialization
    /// </summary>
    void Initialize();

    /// <summary>
    ///     Set theme type (light or dark)
    /// </summary>
    /// <param name="theme">Theme type</param>
    void SetThemeAsync(ElementTheme theme);

    /// <summary>
    ///     Set requested theme type
    /// </summary>
    void SetRequestedThemeAsync();

    /// <summary>
    ///     Apply application theme
    /// </summary>
    /// <param name="themeType">Theme index</param>
    /// <returns>
    ///     ThemeApplyResult:
    ///     Theme background,
    ///     Color intensity,
    ///     Background mask opacity
    /// </returns>
    ThemeApplyResult UpdateAppliedTheme(int themeType);

    /// <summary>
    ///     Save application themes
    /// </summary>
    void SaveThemeInSettings();
}