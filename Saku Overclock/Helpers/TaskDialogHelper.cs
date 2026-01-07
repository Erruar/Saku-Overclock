using System.Runtime.InteropServices;
using System.Text;

namespace Saku_Overclock.Helpers;

public static class CredentialDialogHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CreduiInfo
    {
        public int cbSize;
        public IntPtr hwndParent;
        public string? pszMessageText;
        public string? pszCaptionText;
        public IntPtr hbmBanner;
    }

    [Flags]
    private enum PromptForWindowsCredentialsFlags
    {
        CreduiwinGeneric = 0x1,
        CreduiwinCheckbox = 0x2,
        CreduiwinAuthpackageOnly = 0x10,
        CreduiwinInCredOnly = 0x20,
        CreduiwinEnumerateAdmins = 0x100,
        CreduiwinEnumerateCurrentUser = 0x200,
        CreduiwinSecurePrompt = 0x1000,
        CreduiwinPack32Wow = 0x10000000,
    }

    [DllImport("credui.dll", CharSet = CharSet.Unicode)]
    private static extern uint CredUIPromptForWindowsCredentials(
        ref CreduiInfo pUiInfo,
        uint dwAuthError,
        ref uint pulAuthPackage,
        IntPtr pvInAuthBuffer,
        uint ulInAuthBufferSize,
        out IntPtr ppvOutAuthBuffer,
        out uint pulOutAuthBufferSize,
        ref bool pfSave,
        PromptForWindowsCredentialsFlags dwFlags);

    [DllImport("credui.dll", CharSet = CharSet.Unicode)]
    private static extern bool CredUnPackAuthenticationBuffer(
        uint dwFlags,
        IntPtr pAuthBuffer,
        uint cbAuthBuffer,
        StringBuilder pszUserName,
        ref int pcchMaxUserName,
        StringBuilder? pszDomainName,
        ref int pcchMaxDomainName,
        StringBuilder pszPassword,
        ref int pcchMaxPassword);

    [DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(IntPtr ptr);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern IntPtr GetThreadDesktop(uint dwThreadId);

    private const uint ResultSuccess = 0;

    /// <summary>
    /// Результат диалога учетных данных
    /// </summary>
    public class CredentialResult
    {
        public bool Success
        {
            get; set;
        }
        public string? Username
        {
            get; set;
        }
        public string? Domain
        {
            get; set;
        }
        public string? Password
        {
            get; set;
        }
        public bool Cancelled
        {
            get; set;
        }
    }

    /// <summary>
    /// Показывает диалог безопасности Windows Security - сообщение и кнопки OK/Cancel
    /// </summary>
    /// <param name="caption">Заголовок окна</param>
    /// <param name="message">Сообщение</param>
    /// <param name="parentWindowHandle">Hwnd</param>
    /// <returns>true если нажата OK, false если Cancel</returns>
    public static bool ShowMessageDialog(
        string caption,
        string message,
        IntPtr parentWindowHandle = default)
    {
        if (parentWindowHandle == IntPtr.Zero)
        {
            parentWindowHandle = GetForegroundWindow();
        }

        var credui = new CreduiInfo
        {
            cbSize = Marshal.SizeOf<CreduiInfo>(),
            hwndParent = parentWindowHandle,
            pszMessageText = message,
            pszCaptionText = caption,
            hbmBanner = IntPtr.Zero
        };

        uint authPackage = 0;
        var save = false;
        var outCredBuffer = IntPtr.Zero;

        // Используем GENERIC флаг для отображения диалога
        var flags = PromptForWindowsCredentialsFlags.CreduiwinEnumerateCurrentUser;

        try
        {
            var result = CredUIPromptForWindowsCredentials(
                ref credui,
                0,
                ref authPackage,
                IntPtr.Zero,
                0,
                out outCredBuffer,
                out var outCredSize,
                ref save,
                flags);

            return result == ResultSuccess;
        }
        finally
        {
            if (outCredBuffer != IntPtr.Zero)
            {
                CoTaskMemFree(outCredBuffer);
            }
        }
    }
}