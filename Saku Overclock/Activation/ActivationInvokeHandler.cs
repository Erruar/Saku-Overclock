using System.Runtime.InteropServices;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace Saku_Overclock.Activation;
internal class ActivationInvokeHandler
{
    public const int HWND_BROADCAST = 0xffff;

    public static int WM_WAKEUP_WINDOW =
        RegisterWindowMessage("{bf5237db-0781-4c89-af6e-2a860733ddd2}");

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AllowSetForegroundWindowMethod(int dwProcessId);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool PostMessage(IntPtr hWnd, int Msg,
                                          IntPtr wParam, IntPtr lParam);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int RegisterWindowMessage(string lpString);

    [System.Runtime.InteropServices.DllImport("User32")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    public static bool PostMessageWindow(IntPtr hWnd, int Msg,
                                          IntPtr wParam, IntPtr lParam)
    {
        return PostMessage(hWnd, Msg, wParam, lParam);
    }
    public static int RegisterWindowMessageReg(string lpString)
    {
        return RegisterWindowMessage(lpString);
    }
    public static bool SetForegroundWindowState(IntPtr hWnd)
    {
        return SetForegroundWindow(hWnd);
    }
    public static bool AllowSetForegroundWindow(int processId)
    {
        return AllowSetForegroundWindowMethod(processId);
    }
} 