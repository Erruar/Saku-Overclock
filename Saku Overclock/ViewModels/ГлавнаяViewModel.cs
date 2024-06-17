using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using Saku_Overclock.Helpers;
using Windows.ApplicationModel;

namespace Saku_Overclock.ViewModels;

public partial class ГлавнаяViewModel : ObservableRecipient
{
    public ГлавнаяViewModel()
    {
        _versionDescription = GetVersionDescription();
    }
    [ObservableProperty]
    private string _versionDescription;
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
        return $"Ver {version.Major}.{version.Minor}.{version.Build}.{version.Revision} Rel-Cand v4";
    }  
}
