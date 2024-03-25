using System.ComponentModel;
using System.Runtime.InteropServices;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
namespace Saku_Overclock.SMUEngine;

public sealed class IOModule : IDisposable
{
    internal IntPtr ioModule;
    public readonly _GetPhysLong GetPhysLong = null!;
    public readonly _SetPhysLong SetPhysLong = null!;
    private readonly _MapPhysToLin MapPhysToLin = null!;
    private readonly _UnmapPhysicalMemory UnmapPhysicalMemory = null!;
    private readonly _IsInpOutDriverOpen64 IsInpOutDriverOpen64 = null!;
    private readonly _InitializeWinIo32 InitializeWinIo32 = null!;
    private readonly _ShutdownWinIo32 ShutdownWinIo32 = null!;

    [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPStr)] string lpFileName);

    [DllImport("kernel32", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr hModule);

    [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string lpProcName);

    private LibStatus WinIoStatus { get; } = LibStatus.INITIALIZE_ERROR;

    public bool IsInpOutDriverOpen() => Utils.Is64Bit ? IsInpOutDriverOpen64() > 0U : WinIoStatus == LibStatus.OK;

    public byte[]? ReadMemory(IntPtr baseAddress, int size)
    {
        var num1 = MapPhysToLin(baseAddress, (uint)size, out var pPhysicalMemoryHandle);
        if (num1 != IntPtr.Zero)
        {
            var destination = new byte[size];
            Marshal.Copy(num1, destination, 0, destination.Length);
            _ = UnmapPhysicalMemory(pPhysicalMemoryHandle, num1) ? 1 : 0;
            return destination;
        }
        return null;
    }

    public static IntPtr LoadDll(string filename)
    {
        var num = LoadLibrary(filename);
        if (num == IntPtr.Zero)
        {
            var lastWin32Error = Marshal.GetLastWin32Error();
            var innerException = new Win32Exception(lastWin32Error);
            innerException.Data.Add("LastWin32Error", lastWin32Error);
            throw new Exception("Can't load DLL " + filename, innerException);
        }
        return num;
    }

    public IOModule()
    {
        try
        {
            ioModule = LoadDll(Utils.Is64Bit ? "inpoutx64.dll" : "WinIo32.dll");
            GetPhysLong = (_GetPhysLong)GetDelegate(ioModule, nameof(GetPhysLong), typeof(_GetPhysLong));
            SetPhysLong = (_SetPhysLong)GetDelegate(ioModule, nameof(SetPhysLong), typeof(_SetPhysLong));
            MapPhysToLin = (_MapPhysToLin)GetDelegate(ioModule, nameof(MapPhysToLin), typeof(_MapPhysToLin));
            UnmapPhysicalMemory = (_UnmapPhysicalMemory)GetDelegate(ioModule, nameof(UnmapPhysicalMemory), typeof(_UnmapPhysicalMemory));
            if (Utils.Is64Bit)
            {
                IsInpOutDriverOpen64 = (_IsInpOutDriverOpen64)GetDelegate(ioModule, "IsInpOutDriverOpen", typeof(_IsInpOutDriverOpen64));
            }
            else
            {
                InitializeWinIo32 = (_InitializeWinIo32)GetDelegate(ioModule, "InitializeWinIo", typeof(_InitializeWinIo32));
                ShutdownWinIo32 = (_ShutdownWinIo32)GetDelegate(ioModule, "ShutdownWinIo", typeof(_ShutdownWinIo32));
                if (InitializeWinIo32())
                {
                    WinIoStatus = LibStatus.OK;
                }
            }
        }
        catch
        {
            //Ignored
        }
    }

    public void Dispose()
    {
        if (ioModule == IntPtr.Zero)
        {
            return;
        }

        if (!Utils.Is64Bit)
        {
            _ = ShutdownWinIo32() ? 1 : 0;
        }
        FreeLibrary(ioModule);
        ioModule = IntPtr.Zero;
    }

    public static Delegate GetDelegate(IntPtr moduleName, string procName, Type delegateType)
    {
        var procAddress = GetProcAddress(moduleName, procName);
        return procAddress != IntPtr.Zero ? Marshal.GetDelegateForFunctionPointer(procAddress, delegateType) : throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error())!;
    }

    public enum LibStatus
    {
        INITIALIZE_ERROR,
        OK,
        PARTIALLY_OK,
    }

    public delegate bool _GetPhysLong(UIntPtr memAddress, out uint data);

    public delegate bool _SetPhysLong(UIntPtr memAddress, uint data);

    private delegate IntPtr _MapPhysToLin(
        IntPtr pbPhysAddr,
        uint dwPhysSize,
        out IntPtr pPhysicalMemoryHandle);

    private delegate bool _UnmapPhysicalMemory(IntPtr PhysicalMemoryHandle, IntPtr pbLinAddr);

    private delegate uint _IsInpOutDriverOpen64();

    private delegate bool _InitializeWinIo32();

    private delegate bool _ShutdownWinIo32();
}