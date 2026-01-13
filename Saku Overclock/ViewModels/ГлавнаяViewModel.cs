using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using Saku_Overclock.Helpers;
using Windows.ApplicationModel; 

namespace Saku_Overclock.ViewModels;

#pragma warning disable CS0162 // Unreachable code detected

public partial class ГлавнаяViewModel : ObservableRecipient
{
    private static readonly string Versioning = SettingsViewModel.VersionId == 0 
        // ReSharper disable once HeuristicUnreachableCode
        ? $"CC-{Assembly.GetExecutingAssembly().GetName().Version.Revision}"
        : "v1.0";

    public string VersionDescription
    {
        get;
    } = GetVersionDescription();

    public string Version => Versioning;

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
            version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1,0,1,0);
        }
        return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }

    public static string GetPublicVersionDescription()
    {
        return GetVersionDescription();
    }
    public static string GetVersion()
    {
        return Versioning;
    }
}
