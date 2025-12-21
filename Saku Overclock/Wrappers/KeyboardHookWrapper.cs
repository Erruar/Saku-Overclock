using System.Runtime.InteropServices;

namespace Saku_Overclock.Wrappers;
public static class KeyboardHookWrapper
{
    private const string DllName = "user32.dll";

    #region Hook DLL Imports

    public const int WM_HOTKEY = 0x0312;

    [DllImport(DllName, SetLastError = true)]
    public static extern bool RegisterHotKey(
        IntPtr hWnd,
        int id,
        uint fsModifiers,
        uint vk);

    [DllImport(DllName, SetLastError = true)]
    public static extern bool UnregisterHotKey(
        IntPtr hWnd,
        int id);

    #endregion

    [Flags]
    public enum HotKeyModifiers : uint
    {
        None = 0,
        Alt = 0x0001,
        Ctrl = 0x0002,
        Shift = 0x0004,
        Win = 0x0008
    }
}
