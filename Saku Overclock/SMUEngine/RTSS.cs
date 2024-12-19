using System.Runtime.InteropServices;

namespace Saku_Overclock.SMUEngine;

public static class RtssHandler
{
    private const string DllName = "SakuRTSSCLI.dll";
    private static bool _isRtssInitialized;

    public static void ChangeOsdText(string text)
    {
        if (!_isRtssInitialized)
        {
            displayText(text);
            _isRtssInitialized = true;
        }
        else
        {
            UpdateOSD(text.Replace("<Br>", "\n"));
        }
    }

    public static void ResetOsdText()
    {
        if (_isRtssInitialized)
        {
            _ = ReleaseOSD();
            _isRtssInitialized = false;
        }
    }

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern void displayText(string text);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int Refresh();

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern uint EmbedGraph(uint dwOffset, float[] lpBuffer, uint dwBufferPos, uint dwBufferSize,
        int dwWidth, int dwHeight, int dwMargin, float fltMin, float fltMax, uint dwFlags);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern uint GetClientsNum();

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern uint GetSharedMemoryVersion();

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern bool UpdateOSD(string lpText);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int ReleaseOSD();
}