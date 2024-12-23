using Microsoft.UI.Xaml;
using Saku_Overclock.Styles;

namespace Saku_Overclock.Contracts.Services;

public interface IThemeSelectorService
{
    ElementTheme Theme
    {
        get;
    }
    public List<ThemeClass> Themes
    {
        get;
        set;
    }
    
    Task InitializeAsync();

    Task SetThemeAsync(ElementTheme theme);

    Task SetRequestedThemeAsync();
    
    void LoadThemeFromSettings();
    void SaveThemeInSettings();
}
