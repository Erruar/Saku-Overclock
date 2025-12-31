using System.Runtime.InteropServices;

namespace Saku_Overclock.Wrappers;
public static partial class WindowAttributesWrapper
{
    private const int GwlExstyle = -20;
    private const int WsExToolwindow = 0x00000080;
    private const int WsExTransparent = 0x00000020;
    private const int WsExLayered = 0x00080000;
    private const uint LwaAlpha = 0x02;

    /// <summary>
    ///     Устанавливаем стиль окна как POPUP
    /// </summary>
    /// <param name="hwnd">окно приложения</param>
    public static void SetWindowStyle(IntPtr hwnd)
    {
        const int gwlStyle = -16;
        const uint wsPopup = 0x80000000;
        const uint wsVisible = 0x10000000;
        var style = GetWindowLong(hwnd, gwlStyle);
        _ = SetWindowLong(hwnd, gwlStyle, (int)(style & ~(wsPopup | wsVisible)));
    }

    /// <summary>
    ///     Устанавливаем прозрачный фон окна
    /// </summary>
    /// <param name="hwnd">окно приложения</param>
    public static void SetTransparentBackground(IntPtr hwnd)
    {
        var extendedStyle = GetWindowLong(hwnd, GwlExstyle);
        SetWindowLong(hwnd, GwlExstyle, extendedStyle | WsExLayered | WsExToolwindow | WsExTransparent);
        SetLayeredWindowAttributes(hwnd, 0, 255, LwaAlpha);
    }

    private static int GetWindowLong(IntPtr hWnd, int nIndex)
        => GetWindowLongW(hWnd, nIndex);

    private static int SetWindowLong(IntPtr hWnd, int nIndex, int newLong)
        => SetWindowLongW(hWnd, nIndex, newLong);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial int GetWindowLongW(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial int SetWindowLongW(IntPtr hWnd, int nIndex, int dwNewLong);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial void SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
}
