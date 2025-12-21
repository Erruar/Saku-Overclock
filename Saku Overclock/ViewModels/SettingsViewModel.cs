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
    private string _versionDescription;

    public string VersionDescription
    {
        get => _versionDescription;
        set => SetProperty(ref _versionDescription, value);
    }

    public const int VersionId = 0;
    private static string? _versionString;

    public SettingsViewModel()
    {
        _versionString = VersionId switch
        {
            0 => "Consumer Creative", // Debug for tests, available for testers and pre-revisions
            1 => "Release Candidate", // Available for everyone
            2 => "Release", // Available for everyone
            5 => "Debug Lanore", // Available only for internal testing
            _ => "Unknown Version"
        };

        _versionDescription = GetVersionDescription();
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
        return $"{"AppDisplayName".GetLocalized()} {version} {_versionString}";
    }

}
