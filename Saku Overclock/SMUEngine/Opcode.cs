using System.Reflection;
using System.Runtime.InteropServices;
/*This is a modified processor driver file. Its from Open Hardware monitor. Its author is https://github.com/openhardwaremonitor
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/openhardwaremonitor/openhardwaremonitor
there you can see the source files in detail*/
namespace Saku_Overclock.SMUEngine;

internal static class Opcode
{
    private static IntPtr codeBuffer;
    private static ulong size;
    public static RdtscDelegate? Rdtsc;
    private static readonly byte[] RDTSC_32 = new byte[3]
    {
        15,
        49,
        195
    };
    private static readonly byte[] RDTSC_64 = new byte[10]
    {
        15,
        49,
        72,
        193,
        226,
        32,
        72,
        11,
        194,
        195
    };
    public static CpuidDelegate? Cpuid;
    private static readonly byte[] CPUID_32 = {
        85,
        139,
        236,
        131,
        236,
        16,
        139,
        69,
        8,
        139,
        77,
        12,
        83,
        15,
        162,
        86,
        141,
        117,
        240,
        137,
        6,
        139,
        69,
        16,
        137,
        94,
        4,
        137,
        78,
        8,
        137,
        86,
        12,
        139,
        77,
        240,
        137,
        8,
        139,
        69,
        20,
        139,
        77,
        244,
        137,
        8,
        139,
        69,
        24,
        139,
        77,
        248,
        137,
        8,
        139,
        69,
        28,
        139,
        77,
        252,
        94,
        137,
        8,
        91,
        201,
        194,
        24,
        0
    };
    private static readonly byte[] CPUID_64_WINDOWS = {
        72,
        137,
        92,
        36,
        8,
        139,
        193,
        139,
        202,
        15,
        162,
        65,
        137,
        0,
        72,
        139,
        68,
        36,
        40,
        65,
        137,
        25,
        72,
        139,
        92,
        36,
        8,
        137,
        8,
        72,
        139,
        68,
        36,
        48,
        137,
        16,
        195
    };
    private static readonly byte[] CPUID_64_LINUX = {
        73,
        137,
        210,
        73,
        137,
        203,
        83,
        137,
        248,
        137,
        241,
        15,
        162,
        65,
        137,
        2,
        65,
        137,
        27,
        65,
        137,
        8,
        65,
        137,
        17,
        91,
        195
    };

    public static void Open()
    {
        byte[] source1;
        byte[] source2;
        if (IntPtr.Size == 4)
        {
            source1 = RDTSC_32;
            source2 = CPUID_32;
        }
        else
        {
            source1 = RDTSC_64;
            source2 = !OperatingSystem.IsUnix ? CPUID_64_WINDOWS : CPUID_64_LINUX;
        }
        size = (ulong)(source1.Length + source2.Length);
        if (OperatingSystem.IsUnix)
        {
            var assembly = Assembly.Load("Mono.Posix, Version=2.0.0.0, Culture=neutral, PublicKeyToken=0738eb9f132ed756");
            var method = assembly.GetType("Mono.Unix.Native.Syscall")!.GetMethod("mmap");
            var type1 = assembly.GetType("Mono.Unix.Native.MmapProts");
            var obj1 = Enum.ToObject(type1!, (int)type1!.GetField("PROT_READ")!.GetValue(null)! | (int)type1.GetField("PROT_WRITE")!.GetValue(null)! | (int)type1.GetField("PROT_EXEC")!.GetValue(null)!);
            var type2 = assembly.GetType("Mono.Unix.Native.MmapFlags");
            var obj2 = Enum.ToObject(type2!, (int)type2!.GetField("MAP_ANONYMOUS")!.GetValue(null)! | (int)type2.GetField("MAP_PRIVATE")!.GetValue(null)!);
            codeBuffer = (IntPtr)method!.Invoke(null, new[]
            {
                IntPtr.Zero,
                size,
                obj1,
                obj2,
                -1,
                0
            })!;
        }
        else
        {
            codeBuffer = NativeMethods.VirtualAlloc(IntPtr.Zero, (UIntPtr)size, AllocationType.COMMIT | AllocationType.RESERVE, MemoryProtection.EXECUTE_READWRITE);
        }

        Marshal.Copy(source1, 0, codeBuffer, source1.Length);
        Rdtsc = (Marshal.GetDelegateForFunctionPointer(codeBuffer, typeof(RdtscDelegate)) as RdtscDelegate)!;
        var num = (IntPtr)(codeBuffer + (long)source1.Length);
        Marshal.Copy(source2, 0, num, source2.Length);
        Cpuid = (Marshal.GetDelegateForFunctionPointer(num, typeof(CpuidDelegate)) as CpuidDelegate)!;
    }

    public static void Close()
    {
        Rdtsc = null;
        Cpuid = null;
        if (OperatingSystem.IsUnix)
        {
            Assembly.Load("Mono.Posix, Version=2.0.0.0, Culture=neutral, PublicKeyToken=0738eb9f132ed756").GetType("Mono.Unix.Native.Syscall")!.GetMethod("munmap")!.Invoke(null, new object[]
            {
                codeBuffer,
                size
            });
        }
        else
        {
            NativeMethods.VirtualFree(codeBuffer, UIntPtr.Zero, FreeType.RELEASE);
        }
    }

    public static bool CpuidTx(
        uint index,
        uint ecxValue,
        out uint eax,
        out uint ebx,
        out uint ecx,
        out uint edx,
        GroupAffinity affinity)
    {
        var affinity1 = ThreadAffinity.Set(affinity);
        if (affinity1 == GroupAffinity.Undefined)
        {
            eax = ebx = ecx = edx = 0U;
            return false;
        }
        _ = Cpuid!(index, ecxValue, out eax, out ebx, out ecx, out edx) ? 1 : 0;
        ThreadAffinity.Set(affinity1);
        return true;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate ulong RdtscDelegate();

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate bool CpuidDelegate(
        uint index,
        uint ecxValue,
        out uint eax,
        out uint ebx,
        out uint ecx,
        out uint edx);

    [Flags]
    public enum AllocationType : uint
    {
        COMMIT = 4096, // 0x00001000
        RESERVE = 8192, // 0x00002000
        RESET = 524288, // 0x00080000
        LARGE_PAGES = 536870912, // 0x20000000
        PHYSICAL = 4194304, // 0x00400000
        TOP_DOWN = 1048576, // 0x00100000
        WRITE_WATCH = 2097152, // 0x00200000
    }

    [Flags]
    public enum MemoryProtection : uint
    {
        EXECUTE = 16, // 0x00000010
        EXECUTE_READ = 32, // 0x00000020
        EXECUTE_READWRITE = 64, // 0x00000040
        EXECUTE_WRITECOPY = 128, // 0x00000080
        NOACCESS = 1,
        READONLY = 2,
        READWRITE = 4,
        WRITECOPY = 8,
        GUARD = 256, // 0x00000100
        NOCACHE = 512, // 0x00000200
        WRITECOMBINE = 1024, // 0x00000400
    }

    [Flags]
    public enum FreeType
    {
        DECOMMIT = 16384, // 0x00004000
        RELEASE = 32768, // 0x00008000
    }

    private static class NativeMethods
    {
        private const string KERNEL = "kernel32.dll";

        [DllImport("kernel32.dll")]
        public static extern IntPtr VirtualAlloc(
            IntPtr lpAddress,
            UIntPtr dwSize,
            AllocationType flAllocationType,
            MemoryProtection flProtect);

        [DllImport("kernel32.dll")]
        public static extern bool VirtualFree(
            IntPtr lpAddress,
            UIntPtr dwSize,
            FreeType dwFreeType);
    }
}