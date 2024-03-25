using System.Diagnostics;
using System.Runtime.InteropServices;
/*This is a modified processor driver file. Its from Open Hardware monitor. Its author is https://github.com/openhardwaremonitor
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/openhardwaremonitor/openhardwaremonitor
there you can see the source files in detail*/
namespace Saku_Overclock.SMUEngine;

public static class OperatingSystem
{
    public static bool IsUnix
    {
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        get;
    }

    public static bool Is64BitOperatingSystem
    {
        get;
    }

    static OperatingSystem()
    {
        var platform = (int)Environment.OSVersion.Platform;
        IsUnix = platform is 4 or 6 or 128;
        Is64BitOperatingSystem = GetIs64BitOperatingSystem();
    }

    private static bool GetIs64BitOperatingSystem()
    {
        if (IntPtr.Size == 8)
        {
            return true;
        }
        try
        {
            var flag = IsWow64Process(Process.GetCurrentProcess().Handle, out var wow64Process);
            return flag && wow64Process;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);
}