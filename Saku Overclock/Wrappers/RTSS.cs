using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;

namespace Saku_Overclock.Wrappers;

public static class RtssHandler
{
    private const string DllName = "SakuRTSSCLI.dll";
    private static bool _isRtssInitialized;

    #region DLL Voids

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
    public static unsafe void ChangeOsdTextSpan(ReadOnlySpan<char> text)
    {
        var byteBuffer = ArrayPool<byte>.Shared.Rent(text.Length * 3 + 1); // В UTF-8 до 3 байт на символ

        try
        {
            var byteCount = 0;
            fixed (char* charPtr = text)
            fixed (byte* bytePtr = byteBuffer)
            {
                byteCount = ConvertToUtf8(charPtr, text.Length, bytePtr, byteBuffer.Length - 1);

                if (!_isRtssInitialized)
                {
                    displayTextSpan(bytePtr, byteCount);
                    _isRtssInitialized = true;
                }
                else
                {
                    UpdateOSDSpan(bytePtr, byteCount);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(byteBuffer);
        }
    }

    // Конвертация UTF-16 в UTF-8
    private static unsafe int ConvertToUtf8(char* source, int sourceLength, byte* destination, int destinationLength)
    {
        var bytesWritten = 0;

        for (var i = 0; i < sourceLength && bytesWritten < destinationLength; i++)
        {
            var c = source[i];

            // ASCII
            if (c < 0x80)
            {
                destination[bytesWritten++] = (byte)c;
            }
            // Двухбайтовые UTF-8
            else if (c < 0x800)
            {
                if (bytesWritten + 1 >= destinationLength)
                {
                    break;
                }

                destination[bytesWritten++] = (byte)(0xC0 | (c >> 6));
                destination[bytesWritten++] = (byte)(0x80 | (c & 0x3F));
            }
            // Трёхбайтовые UTF-8
            else
            {
                if (bytesWritten + 2 >= destinationLength)
                {
                    break;
                }

                destination[bytesWritten++] = (byte)(0xE0 | (c >> 12));
                destination[bytesWritten++] = (byte)(0x80 | ((c >> 6) & 0x3F));
                destination[bytesWritten++] = (byte)(0x80 | (c & 0x3F));
            }
        }

        destination[bytesWritten] = 0;
        return bytesWritten;
    }

    public static void ResetOsdText()
    {
        if (_isRtssInitialized)
        {
            _ = ReleaseOSD();
            _isRtssInitialized = false;
        }
    }

    #endregion

    #region DLL Imports

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

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern unsafe bool UpdateOSDSpan(byte* lpText, int length);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern unsafe void displayTextSpan(byte* text, int length);

    #endregion
}