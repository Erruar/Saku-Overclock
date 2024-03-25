using System.Runtime.InteropServices;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
namespace Saku_Overclock.SMUEngine;

internal static class ThreadAffinity
{
    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct GroupAffinity
        {
            public UIntPtr Mask;

            [MarshalAs(UnmanagedType.U2)]
            public ushort Group;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3, ArraySubType = UnmanagedType.U2)]
            public ushort[] Reserved;
        }

        [DllImport("kernel32.dll")]
#pragma warning disable SYSLIB1054
        public static extern UIntPtr SetThreadAffinityMask(IntPtr handle, UIntPtr mask);


        [DllImport("kernel32.dll")]
        public static extern IntPtr GetCurrentThread();

        [DllImport("kernel32.dll")]
        public static extern ushort GetActiveProcessorGroupCount();

        [DllImport("kernel32.dll")]
        public static extern bool SetThreadGroupAffinity(IntPtr thread, ref GroupAffinity groupAffinity, out GroupAffinity previousGroupAffinity);

        [DllImport("libc")]
        // ReSharper disable once UnusedMember.Local
        public static extern int sched_getaffinity(int pid, IntPtr maskSize, ref ulong mask);

        [DllImport("libc")]
        // ReSharper disable once UnusedMember.Local
        public static extern int sched_setaffinity(int pid, IntPtr maskSize, ref ulong mask);
    }

    public static int ProcessorGroupCount
    {
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        get;
    }

    static ThreadAffinity()
    {
        ProcessorGroupCount = GetProcessorGroupCount();
    }

    private static int GetProcessorGroupCount()
    {
        try
        {
            return NativeMethods.GetActiveProcessorGroupCount();
        }
        catch
        {
            return 1;
        }
    }

    public static bool IsValid(GroupAffinity affinity)
    {
        try
        {
            var groupAffinity = Set(affinity);
            if (groupAffinity == GroupAffinity.Undefined)
            {
                return false;
            }
            Set(groupAffinity);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static GroupAffinity Set(GroupAffinity affinity)
    {
        if (affinity == GroupAffinity.Undefined)
        {
            return GroupAffinity.Undefined;
        }
        UIntPtr mask3;
        try
        {
#pragma warning disable CA2020
            mask3 = (UIntPtr)affinity.Mask;
#pragma warning restore CA2020
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(nameof(affinity));
        }
        var gRoupAffinity = default(NativeMethods.GroupAffinity);
        gRoupAffinity.Group = affinity.Group;
        gRoupAffinity.Mask = mask3;
        var groupAffinity = gRoupAffinity;
        var currentThread = NativeMethods.GetCurrentThread();
        try
        {
            return NativeMethods.SetThreadGroupAffinity(currentThread, ref groupAffinity, out var previousGroupAffinity) ? new GroupAffinity(previousGroupAffinity.Group, previousGroupAffinity.Mask) : GroupAffinity.Undefined;
        }
        catch (EntryPointNotFoundException)
        {
            if (affinity.Group > 0)
            {
                throw new ArgumentOutOfRangeException(nameof(affinity));
            }
            var mask4 = (ulong)NativeMethods.SetThreadAffinityMask(currentThread, mask3);
            return new GroupAffinity(0, mask4);
        }
    }
}