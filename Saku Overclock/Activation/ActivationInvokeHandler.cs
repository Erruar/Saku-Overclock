using System.Runtime.InteropServices;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace Saku_Overclock.Activation;
internal static class ActivationInvokeHandler
{
    #region DLL usings
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AllowSetForegroundWindowMethod(int dwProcessId);

    [System.Runtime.InteropServices.DllImport("User32")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(System.IntPtr hWnd, int cmdShow); // Показать текущее окно, вместо открытия второго экземпляра программы

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(System.IntPtr hWnd, int cmdShow); // Второй метод для того, чтобы показать вообще все окна

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName); // Метод для нахождения окна программы

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);
    #endregion 
    #region Public window state voids
    public static bool BringToFrontWindow(IntPtr hWnd)
    {
        return SetForegroundWindow(hWnd);
    } 
    public static bool ChangeWindowState(IntPtr hWnd, int command)
    {
        return ShowWindowAsync(hWnd, command);
    }
    public static bool ChangeAllWindowState(IntPtr hWnd, int command)
    {
        return ShowWindow(hWnd, command);
    }
    public static IntPtr FindMainWindowHWND(string? lpClassName, string lpWindowName)
    {
        return FindWindow(lpClassName, lpWindowName);
    }
    public static void SwitchToMainWindow(IntPtr hWnd, bool fAltTab)
    {
        SwitchToThisWindow(hWnd, fAltTab);
    }
    #endregion
}