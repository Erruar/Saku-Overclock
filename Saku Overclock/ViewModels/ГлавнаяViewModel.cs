using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using Saku_Overclock.Helpers;
using Windows.ApplicationModel; 

namespace Saku_Overclock.ViewModels;

public partial class ГлавнаяViewModel : ObservableRecipient
{
    private const string Versioning = "RC-7";
    public ГлавнаяViewModel()
    {
        _versionDescription = GetVersionDescription();
        _version = GetVersion();
    }
    [ObservableProperty]
    private string _versionDescription;
    [ObservableProperty]
    private string _version = Versioning;
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
        return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }
    public static string GetVersion()
    {
        return Versioning;
    }
}
