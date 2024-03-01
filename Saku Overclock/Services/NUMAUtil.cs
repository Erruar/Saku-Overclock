using System.Runtime.InteropServices;

namespace Saku_Overclock.Services;
public partial class NUMAUtil
{
    public static ulong HighestNumaNode
    {
        get
        {
            ulong n = 0;
            GetNumaHighestNodeNumber(ref n);
            return n;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct GROUP_AFFINITY
    {
        public UIntPtr Mask;
        [MarshalAs(UnmanagedType.U2)]
        public ushort Group;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3, ArraySubType = UnmanagedType.U2)]
        public ushort[] Reserved;
    }

    [DllImport("kernel32", SetLastError = true)]
    private static extern bool SetThreadGroupAffinity(
        IntPtr hThread,
        ref GROUP_AFFINITY GroupAffinity,
        ref GROUP_AFFINITY PreviousGroupAffinity);

    [LibraryImport("kernel32", SetLastError = true)]
    private static partial IntPtr GetCurrentThread();

    [LibraryImport("kernel32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetNumaHighestNodeNumber(ref ulong HighestNodeNumer);

    /// <summary>
    /// Sets the processor group and the processor cpu affinity of the current thread.
    /// </summary>
    /// <param name="group">A processor group number.</param>
    /// <param name="cpus">A list of CPU numbers. The values should be
    /// between 0 and <see cref="Environment.ProcessorCount"/>.</param>
    public static void SetThreadProcessorAffinity(ushort groupId, params int[] cpus)
    {
        if (cpus == null) throw new ArgumentNullException(nameof(cpus));
        if (cpus.Length == 0) throw new ArgumentException("You must specify at least one CPU.", nameof(cpus));

        // Supports up to 64 processors
        long cpuMask = 0;
        foreach (var cpu in cpus)
        {
            if (cpu < 0 || cpu >= Environment.ProcessorCount)
                throw new ArgumentException("Invalid CPU number.");

            cpuMask |= 1L << cpu;
        }

        var hThread = GetCurrentThread();
        var previousAffinity = new GROUP_AFFINITY { Reserved = new ushort[3] };
        var newAffinity = new GROUP_AFFINITY
        {
            Group = groupId,
            Mask = new UIntPtr((ulong)cpuMask),
            Reserved = new ushort[3]
        };

        SetThreadGroupAffinity(hThread, ref newAffinity, ref previousAffinity);
    }
}
