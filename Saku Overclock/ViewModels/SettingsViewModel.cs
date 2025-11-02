using System.Reflection;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.UI.Xaml;

using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;

using Windows.ApplicationModel;
// ReSharper disable HeuristicUnreachableCode

namespace Saku_Overclock.ViewModels;

public partial class SettingsViewModel : ObservableRecipient
{
    private ElementTheme _elementTheme;
    public ElementTheme ElementTheme
    {
        get => _elementTheme;
        set => SetProperty(ref _elementTheme, value);
    }

    private string _versionDescription;

    public string VersionDescription
    {
        get => _versionDescription;
        set => SetProperty(ref _versionDescription, value);
    }
    public ICommand SwitchThemeCommand
    {
        get;
    }

    public const int VersionId = 0; //"Consumer Creative" = 0; "Release Candidate" = 1
    private static string? _versionString;

    public SettingsViewModel(IThemeSelectorService themeSelectorService)
    {
        _versionString = VersionId switch
        {
            0 => "Consumer Creative", //Debug for tests, for all
            1 => "Release Candidate", //For all
            2 => "Release", //For all
            5 => "Debug Lanore", //ONLY FOR TESTS
            _ => "Unknown Version" //Yes
        };
        _elementTheme = themeSelectorService.Theme;
        _versionDescription = GetVersionDescription();

        SwitchThemeCommand = new RelayCommand<ElementTheme>(
            async void (param) =>
            {
                try
                {
                    if (ElementTheme != param)
                    {
                        ElementTheme = param;
                        await themeSelectorService.SetThemeAsync(param);
                    }
                }
                catch (Exception ex)
                {
                    await LogHelper.LogError(ex);
                }
            });
    }

    private static string GetVersionDescription()
    {
        Version version;

        if (RuntimeHelper.IsMsix)
        {
            var packageVersion = Package.Current.Id.Version;
            version = new Version(packageVersion.Major, packageVersion.Minor, packageVersion.Build, packageVersion.Revision);
        }
        else
        {
            version = Assembly.GetExecutingAssembly().GetName().Version!;
        }
        return $"{"AppDisplayName".GetLocalized()} {version.Major}.{version.Minor}.{version.Build}.{version.Revision} {_versionString}";
    }

}
