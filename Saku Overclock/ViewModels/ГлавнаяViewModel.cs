using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using Saku_Overclock.Helpers;
using Windows.ApplicationModel; 

namespace Saku_Overclock.ViewModels;

public partial class ГлавнаяViewModel : ObservableRecipient
{
    private static readonly string Versioning = $"CC-{Assembly.GetExecutingAssembly().GetName().Version?.Revision}"; // RC-9
    public ГлавнаяViewModel()
    {
        _versionDescription = GetVersionDescription();
        _version = Versioning;
        _version = GetVersion();
    }
    private string _versionDescription;
    public string VersionDescription
    {
        get => _versionDescription;
        set => SetProperty(ref _versionDescription, value);
    } 
    private string _version;
    public string Version
    {
        get => _version;
        set => SetProperty(ref _version, value);
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
