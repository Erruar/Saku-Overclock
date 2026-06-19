using CommunityToolkit.Mvvm.ComponentModel;
using Saku_Overclock.Helpers;
using Windows.ApplicationModel; 

namespace Saku_Overclock.ViewModels;

public partial class ГлавнаяViewModel : ObservableRecipient
{
    private static string? _versioning;
    private static string Versioning => _versioning ??= GetSafeVersioning();

    public string VersionDescription => GetVersionDescription();
    public string Version => Versioning;

    private static Version GetSafeAssemblyVersion()
    {
        if (RuntimeHelper.IsMsix)
        {
            var packageVersion = Package.Current.Id.Version;
            return new Version(packageVersion.Major, packageVersion.Minor, packageVersion.Build, packageVersion.Revision);
        }
        
        return typeof(ГлавнаяViewModel).Assembly.GetName().Version ?? new Version(1, 0, 0, 0);
    }

    private static string GetSafeVersioning()
    {
        var version = GetSafeAssemblyVersion();
        
        return SettingsViewModel.VersionId == 0 
            // ReSharper disable once HeuristicUnreachableCode
#pragma warning disable CS0162 // Unreachable code detected
            ? $"CC-{version.Revision}"
#pragma warning restore CS0162 // Unreachable code detected
            : $"v{version.Major}.{version.Minor}";
    }

    private static string GetVersionDescription()
    {
        var version = GetSafeAssemblyVersion();
        return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }

    public static string GetPublicVersionDescription() => GetVersionDescription();
    public static string GetVersion() => Versioning;
}