using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using Saku_Overclock.Helpers;
using Windows.ApplicationModel; 

namespace Saku_Overclock.ViewModels;

public partial class ГлавнаяViewModel : ObservableRecipient
{
    private const string Versioning = "CC-31"; // RC-9
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

        if (RuntimeHelper.IsMSIX)
        {
            var packageVersion = Package.Current.Id.Version;
            version = new Version(packageVersion.Major, packageVersion.Minor, packageVersion.Build, packageVersion.Revision);
        }
        else
        {
            version = Assembly.GetExecutingAssembly().GetName().Version!;
        }
        return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }
    public static string GetVersion()
    {
        return Versioning;
    }
}
