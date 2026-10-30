using Microsoft.Win32;

namespace Saku_Overclock.Helpers;

public static class FirstRunManager
{
    private const string RegistryKeyPath = @"Software\SakuOverclock";
    private const string RegistryValueName = "AppFirstRunPass";

    /// <summary>
    ///     Checks whether the current run is the first one
    /// </summary>
    public static bool GetFirstRun()
    {
        var value = Registry.CurrentUser.OpenSubKey(RegistryKeyPath)?.GetValue(RegistryValueName);
        return value == null || Convert.ToBoolean(value);
    }

    /// <summary>
    ///     Set first run pass flag
    /// </summary>
    public static void PassFirstRun()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
        key.SetValue(RegistryValueName, 0, RegistryValueKind.DWord);
    }
}