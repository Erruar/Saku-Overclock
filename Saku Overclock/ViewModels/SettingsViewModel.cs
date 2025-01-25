using System.Reflection;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.UI.Xaml;

using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;

using Windows.ApplicationModel;

namespace Saku_Overclock.ViewModels;

public partial class SettingsViewModel : ObservableRecipient
{
    private readonly IThemeSelectorService _themeSelectorService;

    [ObservableProperty]
    private ElementTheme _elementTheme;

    [ObservableProperty]
    private string _versionDescription; 
    public ICommand SwitchThemeCommand
    {
        get;
    }

    public const int VersionId = 0; //"Consumer Creative" = 0; "Release Candidate" = 1
    private static string? VersionString;

    public SettingsViewModel(IThemeSelectorService themeSelectorService)
    {
        VersionString = VersionId switch
        {
            0 => "Consumer Creative", //Debug for tests, for all
            1 => "Release Candidate", //For all
            2 => "Release", //For all
            5 => "Debug Lanore", //ONLY FOR TESTS
            _ => "Unknown Version" //Yes
        };
        _themeSelectorService = themeSelectorService;
        _elementTheme = _themeSelectorService.Theme;
        _versionDescription = GetVersionDescription();

        SwitchThemeCommand = new RelayCommand<ElementTheme>(
            async (param) =>
            {
                if (ElementTheme != param)
                {
                    ElementTheme = param;
                    await _themeSelectorService.SetThemeAsync(param);
                }
            });
    }

    private static string GetVersionDescription()
    {
        Version version;

        if (RuntimeHelper.IsMSIX)
        {
            var packageVersion = Package.Current.Id.Version; 
            version = new(packageVersion.Major, packageVersion.Minor, packageVersion.Build, packageVersion.Revision);
        }
        else
        {
            version = Assembly.GetExecutingAssembly().GetName().Version!;
        } 
        return $"{"AppDisplayName".GetLocalized()} {version.Major}.{version.Minor}.{version.Build}.{version.Revision} {VersionString}";
    }
    
}
