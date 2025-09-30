using System.Runtime.InteropServices;

namespace Saku_Overclock.Activation;

internal static class ActivationInvokeHandler
{
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

    #region Public window state voids

    /// <summary>
    ///     Сфокусироваться на текущем окне
    /// </summary>
    public static void BringToFrontWindow(IntPtr hWnd) => SetForegroundWindow(hWnd);

    /// <summary>
    ///     Показать текущее окно, вместо открытия второго экземпляра программы
    /// </summary>
    public static void ChangeWindowState(IntPtr hWnd, int command) => ShowWindowAsync(hWnd, command);

    /// <summary>
    ///     Второй метод для того, чтобы показать вообще все окна
    /// </summary>
    public static void ChangeAllWindowState(IntPtr hWnd, int command) => ShowWindow(hWnd, command);

    /// <summary>
    ///     Метод для нахождения окна программы
    /// </summary>
    /// <returns>IntPtr главного окна</returns>
    public static IntPtr FindMainWindowHwnd(string? lpClassName, string lpWindowName) =>
        FindWindow(lpClassName, lpWindowName);

    public static void SwitchToMainWindow(IntPtr hWnd, bool fAltTab) => SwitchToThisWindow(hWnd, fAltTab);

    #endregion
}