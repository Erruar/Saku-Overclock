using System.Runtime.InteropServices;

namespace Saku_Overclock.Activation;

internal static class ActivationInvokeHandler
{
    #region Public window state voids

    /// <summary>
    ///     Focus on current window
    /// </summary>
    public static void BringToFrontWindow(IntPtr hWnd) => SetForegroundWindow(hWnd);

    /// <summary>
    ///     Show current window instead of opening new ones
    /// </summary>
    public static void ChangeWindowState(IntPtr hWnd, int command) => ShowWindowAsync(hWnd, command);

    /// <summary>
    ///     Show all current windows
    /// </summary>
    public static void ChangeAllWindowState(IntPtr hWnd, int command) => ShowWindow(hWnd, command);

    /// <summary>
    ///     Searching for window
    /// </summary>
    /// <returns>Required window IntPtr</returns>
    public static IntPtr FindMainWindowHwnd(string? lpClassName, string lpWindowName) =>
        FindWindow(lpClassName, lpWindowName);

    public static void SwitchToMainWindow(IntPtr hWnd, bool fAltTab) => SwitchToThisWindow(hWnd, fAltTab);

    #endregion
    
    #region DLL usings

    [DllImport("User32")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int cmdShow);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int cmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

    #endregion
}