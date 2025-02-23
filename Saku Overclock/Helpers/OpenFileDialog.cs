using System.Runtime.InteropServices;
using System.Text;

namespace Saku_Overclock.Helpers;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
public struct OpenFileName
{
    public int lStructSize;
    public IntPtr hwndOwner;
    public IntPtr hInstance;

    [MarshalAs(UnmanagedType.LPTStr)]
    public string lpstrFilter;

    [MarshalAs(UnmanagedType.LPTStr)]
    public string lpstrCustomFilter;

    public int nMaxCustFilter;
    public int nFilterIndex;

    // Поле для пути к файлу: фиксированная длина 256 символов
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string lpstrFile;

    public int nMaxFile;

    // Если необходимо получать только имя файла (без пути)
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string lpstrFileTitle;

    public int nMaxFileTitle;

    [MarshalAs(UnmanagedType.LPTStr)]
    public string lpstrInitialDir;

    [MarshalAs(UnmanagedType.LPTStr)]
    public string lpstrTitle;

    public int Flags;
    public short nFileOffset;
    public short nFileExtension;

    [MarshalAs(UnmanagedType.LPTStr)]
    public string lpstrDefExt;

    public IntPtr lCustData;
    public IntPtr lpfnHook;

    [MarshalAs(UnmanagedType.LPTStr)]
    public string lpTemplateName;

    public IntPtr pvReserved;
    public int dwReserved;
    public int flagsEx;
}
public static class OpenFileDialog
{
    [DllImport("Comdlg32.dll", SetLastError = true,
        ThrowOnUnmappableChar = true, CharSet = CharSet.Auto)]
    public static extern bool GetOpenFileName(ref OpenFileName ofn);
}