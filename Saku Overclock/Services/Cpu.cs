using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Diagnostics;
using Microsoft.Win32.SafeHandles;
using System.Windows.Interop;
namespace Saku_Overclock.Services;
 partial class Cpu : IDisposable
{
    public enum Family
    {
        UNSUPPORTED = 0,
        FAMILY_15H = 21,
        FAMILY_17H = 23,
        FAMILY_18H = 24,
        FAMILY_19H = 25
    }

    public enum CodeName
    {
        Unsupported,
        DEBUG,
        BristolRidge,
        SummitRidge,
        Whitehaven,
        Naples,
        RavenRidge,
        PinnacleRidge,
        Colfax,
        Picasso,
        FireFlight,
        Matisse,
        CastlePeak,
        Rome,
        Dali,
        Renoir,
        VanGogh,
        Vermeer,
        Chagall,
        Milan,
        Cezanne,
        Rembrandt,
        Lucienne,
        Raphael,
        Phoenix,
        Mendocino,
        Genoa,
        StormPeak
    }

    public enum PackageType
    {
        FPX = 0,
        AM4 = 2,
        SP3 = 4,
        TRX = 7
    }

    public struct SVI2
    {
        public uint coreAddress;

        public uint socAddress;
    }

    public struct CpuTopology
    {
        public uint ccds;

        public uint ccxs;

        public uint coresPerCcx;

        public uint cores;

        public uint logicalCores;

        public uint physicalCores;

        public uint threadsPerCore;

        public uint cpuNodes;

        public uint coreDisableMap;

        public uint[] performanceOfCore;
    }

    public struct CPUInfo
    {
        public uint cpuid;

        public Family family;

        public CodeName codeName;

        public string cpuName;

        public string vendor;

        public PackageType packageType;

        public uint baseModel;

        public uint extModel;

        public uint model;

        public uint patchLevel;

        public uint stepping;

        public CpuTopology topology;

        public SVI2 svi2;
    }

    public bool disposedValue;

    public const string InitializationExceptionText = "CPU module initialization failed.";

    public readonly CPUInfo info;
    public Exception LastError
    {
        get;
    }

    public void Cpu_Init()
    {
        Ring0.Open();
        if (!Ring0.IsOpen)
        {
            string report = Ring0.GetReport();
            using (StreamWriter streamWriter = new StreamWriter("WinRing0.txt", append: true))
            {
                streamWriter.Write(report);
            }

            throw new ApplicationException("Error opening WinRing kernel driver");
        }

    }
    public bool ReadMsr(uint index, ref uint eax, ref uint edx)
    {
        return Ring0.Rdmsr(index, out eax, out edx);
    }


    public bool WriteMsr(uint msr, uint eax, uint edx)
    {
        bool result = true;
        for (int i = 0; i < info.topology.logicalCores; i++)
        {
            result = Ring0.WrmsrTx(msr, eax, edx, GroupAffinity.Single(0, i));
        }

        return result;
    }
    public bool WriteMsrWN(uint msr, uint eax, uint edx)
    {
        bool result = true;
        for (int i = 0; i < 17; i++)
        {
            result =  Ring0.WrmsrTx(msr, eax, edx, GroupAffinity.Single(0, i));
        }
        return result;
    }
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                Ring0.Close();
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
//RING0 - WinRing0 modded CPU Driver 
internal static class Ring0
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct WrmsrInput
    {
        public uint Register;

        public ulong Value;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct WriteIoPortInput
    {
        public uint PortNumber;

        public byte Value;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct ReadPciConfigInput
    {
        public uint PciAddress;

        public uint RegAddress;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct WritePciConfigInput
    {
        public uint PciAddress;

        public uint RegAddress;

        public uint Value;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct ReadMemoryInput
    {
        public ulong address;

        public uint unitSize;

        public uint count;
    }

    private static KernelDriver driver;

    private static string fileName;

    private static Mutex isaBusMutex;

    private static Mutex pciBusMutex;

    private static readonly StringBuilder report = new StringBuilder();

    private const uint OLS_TYPE = 40000u;

    private static IOControlCode IOCTL_OLS_GET_REFCOUNT = new IOControlCode(40000u, 2049u, IOControlCode.Access.Any);

    private static IOControlCode IOCTL_OLS_GET_DRIVER_VERSION = new IOControlCode(40000u, 2048u, IOControlCode.Access.Any);

    private static IOControlCode IOCTL_OLS_READ_MSR = new IOControlCode(40000u, 2081u, IOControlCode.Access.Any);

    private static IOControlCode IOCTL_OLS_WRITE_MSR = new IOControlCode(40000u, 2082u, IOControlCode.Access.Any);

    private static IOControlCode IOCTL_OLS_READ_IO_PORT_BYTE = new IOControlCode(40000u, 2099u, IOControlCode.Access.Read);

    private static IOControlCode IOCTL_OLS_WRITE_IO_PORT_BYTE = new IOControlCode(40000u, 2102u, IOControlCode.Access.Write);

    private static IOControlCode IOCTL_OLS_READ_PCI_CONFIG = new IOControlCode(40000u, 2129u, IOControlCode.Access.Read);

    private static IOControlCode IOCTL_OLS_WRITE_PCI_CONFIG = new IOControlCode(40000u, 2130u, IOControlCode.Access.Write);

    private static IOControlCode IOCTL_OLS_READ_MEMORY = new IOControlCode(40000u, 2113u, IOControlCode.Access.Read);

    public const uint InvalidPciAddress = uint.MaxValue;

    public static bool IsOpen => driver != null;

    private static Assembly GetAssembly()
    {
        return typeof(Ring0).Assembly;
    }

    private static string GetTempFileName()
    {
        string location = GetAssembly().Location;
        if (!string.IsNullOrEmpty(location))
        {
            try
            {
                string text = Path.ChangeExtension(location, ".sys");
                using (File.Create(text))
                {
                    return text;
                }
            }
            catch (Exception)
            {
            }
        }
        try
        {
            return Path.GetTempFileName();
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (NotSupportedException)
        {
        }
        return null;
    }

    private static bool ExtractDriver(string fileName)
    {
        string text = "ZenStates.Core." + (OperatingSystem.Is64BitOperatingSystem ? "WinRing0x64.sys" : "WinRing0.sys");
        string[] manifestResourceNames = GetAssembly().GetManifestResourceNames();
        byte[] array = null;
        for (int i = 0; i < manifestResourceNames.Length; i++)
        {
            if (manifestResourceNames[i].Replace('\\', '.') == text)
            {
                using Stream stream = GetAssembly().GetManifestResourceStream(manifestResourceNames[i]);
                array = new byte[stream.Length];
                stream.Read(array, 0, array.Length);
            }
        }
        if (array == null)
        {
            return false;
        }
        try
        {
            using FileStream fileStream = new FileStream(fileName, FileMode.Create);
            fileStream.Write(array, 0, array.Length);
            fileStream.Flush();
        }
        catch (IOException)
        {
            return false;
        }
        for (int j = 0; j < 20; j++)
        {
            try
            {
                if (File.Exists(fileName) && new FileInfo(fileName).Length == array.Length)
                {
                    return true;
                }
                Thread.Sleep(100);
            }
            catch (IOException)
            {
                Thread.Sleep(10);
            }
        }
        return false;
    }

    public static void Open()
    {
        report.Length = 0;
        driver = new KernelDriver("WinRing0_1_2_0");
        driver.Open();
        if (!driver.IsOpen)
        {
            fileName = GetTempFileName();
            if (fileName != null && ExtractDriver(fileName))
            {
                if (driver.Install(fileName, out var errorMessage))
                {
                    driver.Open();
                    if (!driver.IsOpen)
                    {
                        driver.Delete();
                        report.AppendLine("Status: Opening driver failed after install");
                    }
                }
                else
                {
                    string text = errorMessage;
                    driver.Delete();
                    Thread.Sleep(2000);
                    if (driver.Install(fileName, out var errorMessage2))
                    {
                        driver.Open();
                        if (!driver.IsOpen)
                        {
                            driver.Delete();
                            report.AppendLine("Status: Opening driver failed after reinstall");
                        }
                    }
                    else
                    {
                        report.AppendLine("Status: Installing driver \"" + fileName + "\" failed" + (File.Exists(fileName) ? " and file exists" : ""));
                        report.AppendLine("First Exception: " + text);
                        report.AppendLine("Second Exception: " + errorMessage2);
                    }
                }
            }
            else
            {
                report.AppendLine("Status: Extracting driver failed");
            }
            try
            {
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }
                fileName = null;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
        if (!driver.IsOpen)
        {
            driver = null;
        }
        string text2 = "Global\\Access_ISABUS.HTP.Method";
        try
        {
            isaBusMutex = new Mutex(initiallyOwned: false, text2);
        }
        catch (UnauthorizedAccessException)
        {
            try
            {
                isaBusMutex = Mutex.OpenExisting(text2);
            }
            catch
            {
            }
        }
        string text3 = "Global\\Access_PCI";
        try
        {
            pciBusMutex = new Mutex(initiallyOwned: false, text3);
        }
        catch (UnauthorizedAccessException)
        {
            try
            {
                pciBusMutex = Mutex.OpenExisting(text3);
            }
            catch
            {
            }
        }
    }

    public static void Close()
    {
        if (driver == null)
        {
            return;
        }
        uint outBuffer = 0u;
        driver.DeviceIOControl(IOCTL_OLS_GET_REFCOUNT, null, ref outBuffer);
        driver.Close();
        if (outBuffer <= 1)
        {
            driver.Delete();
        }
        driver = null;
        if (isaBusMutex != null)
        {
            isaBusMutex.Close();
            isaBusMutex = null;
        }
        if (pciBusMutex != null)
        {
            pciBusMutex.Close();
            pciBusMutex = null;
        }
        if (fileName == null || !File.Exists(fileName))
        {
            return;
        }
        try
        {
            File.Delete(fileName);
            fileName = null;
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public static string GetReport()
    {
        if (report.Length > 0)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("Ring0");
            stringBuilder.AppendLine();
            stringBuilder.Append((object?)report);
            stringBuilder.AppendLine();
            return stringBuilder.ToString();
        }
        return null;
    }

    public static bool WaitIsaBusMutex(int millisecondsTimeout)
    {
        if (isaBusMutex == null)
        {
            return true;
        }
        try
        {
            return isaBusMutex.WaitOne(millisecondsTimeout, exitContext: false);
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static void ReleaseIsaBusMutex()
    {
        if (isaBusMutex != null)
        {
            isaBusMutex.ReleaseMutex();
        }
    }

    public static bool WaitPciBusMutex(int millisecondsTimeout)
    {
        if (pciBusMutex == null)
        {
            return true;
        }
        try
        {
            return pciBusMutex.WaitOne(millisecondsTimeout, exitContext: false);
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static void ReleasePciBusMutex()
    {
        if (pciBusMutex != null)
        {
            pciBusMutex.ReleaseMutex();
        }
    }

    public static bool Rdmsr(uint index, out uint eax, out uint edx)
    {
        if (driver == null)
        {
            eax = 0u;
            edx = 0u;
            return false;
        }
        ulong outBuffer = 0uL;
        bool result = driver.DeviceIOControl(IOCTL_OLS_READ_MSR, index, ref outBuffer);
        edx = (uint)((outBuffer >> 32) & 0xFFFFFFFFu);
        eax = (uint)(outBuffer & 0xFFFFFFFFu);
        return result;
    }

    public static bool RdmsrTx(uint index, out uint eax, out uint edx, GroupAffinity affinity)
    {
        GroupAffinity affinity2 = ThreadAffinity.Set(affinity);
        bool result = Rdmsr(index, out eax, out edx);
        ThreadAffinity.Set(affinity2);
        return result;
    }

    public static bool Wrmsr(uint index, uint eax, uint edx)
    {
        if (driver == null)
        {
            return false;
        }
        WrmsrInput wrmsrInput = default(WrmsrInput);
        wrmsrInput.Register = index;
        wrmsrInput.Value = ((ulong)edx << 32) | eax;
        return driver.DeviceIOControl(IOCTL_OLS_WRITE_MSR, wrmsrInput);
    }

    public static bool WrmsrTx(uint index, uint eax, uint edx, GroupAffinity affinity)
    {
        if (driver == null)
        {
            return false;
        }
        WrmsrInput wrmsrInput = default(WrmsrInput);
        wrmsrInput.Register = index;
        wrmsrInput.Value = ((ulong)edx << 32) | eax;
        WrmsrInput wrmsrInput2 = wrmsrInput;
        GroupAffinity affinity2 = ThreadAffinity.Set(affinity);
        bool result = driver.DeviceIOControl(IOCTL_OLS_WRITE_MSR, wrmsrInput2);
        ThreadAffinity.Set(affinity2);
        return result;
    }

    public static byte ReadIoPort(uint port)
    {
        if (driver == null)
        {
            return 0;
        }
        uint outBuffer = 0u;
        driver.DeviceIOControl(IOCTL_OLS_READ_IO_PORT_BYTE, port, ref outBuffer);
        return (byte)(outBuffer & 0xFFu);
    }

    public static void WriteIoPort(uint port, byte value)
    {
        if (driver != null)
        {
            WriteIoPortInput writeIoPortInput = default(WriteIoPortInput);
            writeIoPortInput.PortNumber = port;
            writeIoPortInput.Value = value;
            driver.DeviceIOControl(IOCTL_OLS_WRITE_IO_PORT_BYTE, writeIoPortInput);
        }
    }

    public static uint GetPciAddress(byte bus, byte device, byte function)
    {
        return (uint)(((bus & 0xFF) << 8) | ((device & 0x1F) << 3)) | (function & 7u);
    }

    public static bool ReadPciConfig(uint pciAddress, uint regAddress, out uint value)
    {
        if (driver == null || (regAddress & 3u) != 0)
        {
            value = 0u;
            return false;
        }
        ReadPciConfigInput readPciConfigInput = default(ReadPciConfigInput);
        readPciConfigInput.PciAddress = pciAddress;
        readPciConfigInput.RegAddress = regAddress;
        value = 0u;
        return driver.DeviceIOControl(IOCTL_OLS_READ_PCI_CONFIG, readPciConfigInput, ref value);
    }

    public static bool WritePciConfig(uint pciAddress, uint regAddress, uint value)
    {
        if (driver == null || (regAddress & 3u) != 0)
        {
            return false;
        }
        WritePciConfigInput writePciConfigInput = default(WritePciConfigInput);
        writePciConfigInput.PciAddress = pciAddress;
        writePciConfigInput.RegAddress = regAddress;
        writePciConfigInput.Value = value;
        return driver.DeviceIOControl(IOCTL_OLS_WRITE_PCI_CONFIG, writePciConfigInput);
    }

    public static bool ReadMemory<T>(ulong address, ref T buffer)
    {
        if (driver == null)
        {
            return false;
        }
        ReadMemoryInput readMemoryInput = default(ReadMemoryInput);
        readMemoryInput.address = address;
        readMemoryInput.unitSize = 1u;
        readMemoryInput.count = (uint)Marshal.SizeOf((object)buffer);
        return driver.DeviceIOControl(IOCTL_OLS_READ_MEMORY, readMemoryInput, ref buffer);
    }
}


internal static class ThreadAffinity
{
    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct GROUP_AFFINITY
        {
            public UIntPtr Mask;

            [MarshalAs(UnmanagedType.U2)]
            public ushort Group;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3, ArraySubType = UnmanagedType.U2)]
            public ushort[] Reserved;
        }

        private const string KERNEL = "kernel32.dll";

        private const string LIBC = "libc";

        [DllImport("kernel32.dll")]
        public static extern UIntPtr SetThreadAffinityMask(IntPtr handle, UIntPtr mask);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetCurrentThread();

        [DllImport("kernel32.dll")]
        public static extern ushort GetActiveProcessorGroupCount();

        [DllImport("kernel32.dll")]
        public static extern bool SetThreadGroupAffinity(IntPtr thread, ref GROUP_AFFINITY groupAffinity, out GROUP_AFFINITY previousGroupAffinity);

        [DllImport("libc")]
        public static extern int sched_getaffinity(int pid, IntPtr maskSize, ref ulong mask);

        [DllImport("libc")]
        public static extern int sched_setaffinity(int pid, IntPtr maskSize, ref ulong mask);
    }

    public static int ProcessorGroupCount
    {
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
            GroupAffinity groupAffinity = Set(affinity);
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
            mask3 = (UIntPtr)affinity.Mask;
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException("affinity.Mask");
        }
        NativeMethods.GROUP_AFFINITY gROUP_AFFINITY = default(NativeMethods.GROUP_AFFINITY);
        gROUP_AFFINITY.Group = affinity.Group;
        gROUP_AFFINITY.Mask = mask3;
        NativeMethods.GROUP_AFFINITY groupAffinity = gROUP_AFFINITY;
        IntPtr currentThread = NativeMethods.GetCurrentThread();
        try
        {
            if (NativeMethods.SetThreadGroupAffinity(currentThread, ref groupAffinity, out var previousGroupAffinity))
            {
                return new GroupAffinity(previousGroupAffinity.Group, (ulong)previousGroupAffinity.Mask);
            }
            return GroupAffinity.Undefined;
        }
        catch (EntryPointNotFoundException)
        {
            if (affinity.Group > 0)
            {
                throw new ArgumentOutOfRangeException("affinity.Group");
            }
            ulong mask4 = (ulong)NativeMethods.SetThreadAffinityMask(currentThread, mask3);
            return new GroupAffinity(0, mask4);
        }
    }
}
internal struct GroupAffinity
{
    public static GroupAffinity Undefined = new GroupAffinity(ushort.MaxValue, 0uL);

    public ushort Group
    {
        get;
    }

    public ulong Mask
    {
        get;
    }

    public GroupAffinity(ushort group, ulong mask)
    {
        Group = group;
        Mask = mask;
    }

    public static GroupAffinity Single(ushort group, int index)
    {
        return new GroupAffinity(group, (ulong)(1L << index));
    }

    public override bool Equals(object o)
    {
        if (o == null || (object)GetType() != o.GetType())
        {
            return false;
        }
        GroupAffinity groupAffinity = (GroupAffinity)o;
        return Group == groupAffinity.Group && Mask == groupAffinity.Mask;
    }

    public override int GetHashCode()
    {
        return Group.GetHashCode() ^ Mask.GetHashCode();
    }

    public static bool operator ==(GroupAffinity a1, GroupAffinity a2)
    {
        return a1.Group == a2.Group && a1.Mask == a2.Mask;
    }

    public static bool operator !=(GroupAffinity a1, GroupAffinity a2)
    {
        return a1.Group != a2.Group || a1.Mask != a2.Mask;
    }
}
public static class OperatingSystem
{
    public static bool IsUnix
    {
        get;
    }

    public static bool Is64BitOperatingSystem
    {
        get;
    }

    static OperatingSystem()
    {
        int platform = (int)Environment.OSVersion.Platform;
        IsUnix = platform == 4 || platform == 6 || platform == 128;
        Is64BitOperatingSystem = GetIs64BitOperatingSystem();
    }

    private static bool GetIs64BitOperatingSystem()
    {
        if (IntPtr.Size == 8)
        {
            return true;
        }
        try
        {
            bool wow64Process;
            bool flag = IsWow64Process(Process.GetCurrentProcess().Handle, out wow64Process);
            return flag && wow64Process;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);
}
internal struct IOControlCode
{
    public enum Method : uint
    {
        Buffered,
        InDirect,
        OutDirect,
        Neither
    }

    public enum Access : uint
    {
        Any,
        Read,
        Write
    }

    private readonly uint code;

    public IOControlCode(uint deviceType, uint function, Access access)
        : this(deviceType, function, Method.Buffered, access)
    {
    }

    public IOControlCode(uint deviceType, uint function, Method method, Access access)
    {
        code = (deviceType << 16) | ((uint)access << 14) | (function << 2) | (uint)method;
    }
}
internal class KernelDriver
{
    private enum ServiceAccessRights : uint
    {
        SERVICE_ALL_ACCESS = 983551u
    }

    private enum ServiceControlManagerAccessRights : uint
    {
        SC_MANAGER_ALL_ACCESS = 983103u
    }

    private enum ServiceType : uint
    {
        SERVICE_KERNEL_DRIVER = 1u,
        SERVICE_FILE_SYSTEM_DRIVER
    }

    private enum StartType : uint
    {
        SERVICE_BOOT_START,
        SERVICE_SYSTEM_START,
        SERVICE_AUTO_START,
        SERVICE_DEMAND_START,
        SERVICE_DISABLED
    }

    private enum ErrorControl : uint
    {
        SERVICE_ERROR_IGNORE,
        SERVICE_ERROR_NORMAL,
        SERVICE_ERROR_SEVERE,
        SERVICE_ERROR_CRITICAL
    }

    private enum ServiceControl : uint
    {
        SERVICE_CONTROL_STOP = 1u,
        SERVICE_CONTROL_PAUSE,
        SERVICE_CONTROL_CONTINUE,
        SERVICE_CONTROL_INTERROGATE,
        SERVICE_CONTROL_SHUTDOWN,
        SERVICE_CONTROL_PARAMCHANGE,
        SERVICE_CONTROL_NETBINDADD,
        SERVICE_CONTROL_NETBINDREMOVE,
        SERVICE_CONTROL_NETBINDENABLE,
        SERVICE_CONTROL_NETBINDDISABLE,
        SERVICE_CONTROL_DEVICEEVENT,
        SERVICE_CONTROL_HARDWAREPROFILECHANGE,
        SERVICE_CONTROL_POWEREVENT,
        SERVICE_CONTROL_SESSIONCHANGE
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct ServiceStatus
    {
        public uint dwServiceType;

        public uint dwCurrentState;

        public uint dwControlsAccepted;

        public uint dwWin32ExitCode;

        public uint dwServiceSpecificExitCode;

        public uint dwCheckPoint;

        public uint dwWaitHint;
    }

    private enum FileAccess : uint
    {
        GENERIC_READ = 2147483648u,
        GENERIC_WRITE = 1073741824u
    }

    private enum CreationDisposition : uint
    {
        CREATE_NEW = 1u,
        CREATE_ALWAYS,
        OPEN_EXISTING,
        OPEN_ALWAYS,
        TRUNCATE_EXISTING
    }

    private enum FileAttributes : uint
    {
        FILE_ATTRIBUTE_NORMAL = 0x80u
    }

    private static class NativeMethods
    {
        private const string ADVAPI = "advapi32.dll";

        private const string KERNEL = "kernel32.dll";

        [DllImport("advapi32.dll")]
        public static extern IntPtr OpenSCManager(string machineName, string databaseName, ServiceControlManagerAccessRights dwAccess);

        [DllImport("advapi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseServiceHandle(IntPtr hSCObject);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern IntPtr CreateService(IntPtr hSCManager, string lpServiceName, string lpDisplayName, ServiceAccessRights dwDesiredAccess, ServiceType dwServiceType, StartType dwStartType, ErrorControl dwErrorControl, string lpBinaryPathName, string lpLoadOrderGroup, string lpdwTagId, string lpDependencies, string lpServiceStartName, string lpPassword);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, ServiceAccessRights dwDesiredAccess);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteService(IntPtr hService);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool StartService(IntPtr hService, uint dwNumServiceArgs, string[] lpServiceArgVectors);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ControlService(IntPtr hService, ServiceControl dwControl, ref ServiceStatus lpServiceStatus);

        [DllImport("kernel32.dll")]
        public static extern bool DeviceIoControl(SafeFileHandle device, IOControlCode ioControlCode, [In][MarshalAs(UnmanagedType.AsAny)] object inBuffer, uint inBufferSize, [Out][MarshalAs(UnmanagedType.AsAny)] object outBuffer, uint nOutBufferSize, out uint bytesReturned, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateFile(string lpFileName, FileAccess dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, CreationDisposition dwCreationDisposition, FileAttributes dwFlagsAndAttributes, IntPtr hTemplateFile);
    }

    private string id;

    private SafeFileHandle device;

    private const int ERROR_SERVICE_EXISTS = -2147023823;

    private const int ERROR_SERVICE_ALREADY_RUNNING = -2147023840;

    public bool IsOpen => device != null;

    public KernelDriver(string id)
    {
        this.id = id;
    }

    public bool Install(string path, out string errorMessage)
    {
        IntPtr intPtr = NativeMethods.OpenSCManager(null, null, ServiceControlManagerAccessRights.SC_MANAGER_ALL_ACCESS);
        if (intPtr == IntPtr.Zero)
        {
            errorMessage = "OpenSCManager returned zero.";
            return false;
        }
        IntPtr intPtr2 = NativeMethods.CreateService(intPtr, id, id, ServiceAccessRights.SERVICE_ALL_ACCESS, ServiceType.SERVICE_KERNEL_DRIVER, StartType.SERVICE_DEMAND_START, ErrorControl.SERVICE_ERROR_NORMAL, path, null, null, null, null, null);
        if (intPtr2 == IntPtr.Zero)
        {
            if (Marshal.GetHRForLastWin32Error() == -2147023823)
            {
                errorMessage = "Service already exists";
                return false;
            }
            errorMessage = "CreateService returned the error: " + Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()).Message;
            NativeMethods.CloseServiceHandle(intPtr);
            return false;
        }
        if (!NativeMethods.StartService(intPtr2, 0u, null) && Marshal.GetHRForLastWin32Error() != -2147023840)
        {
            errorMessage = "StartService returned the error: " + Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()).Message;
            NativeMethods.CloseServiceHandle(intPtr2);
            NativeMethods.CloseServiceHandle(intPtr);
            return false;
        }
        NativeMethods.CloseServiceHandle(intPtr2);
        NativeMethods.CloseServiceHandle(intPtr);
        try
        {
            string fileName = "\\\\.\\" + id;
            FileInfo fileInfo = new FileInfo(fileName);
            FileSecurity accessControl = fileInfo.GetAccessControl();
            accessControl.SetSecurityDescriptorSddlForm("O:BAG:SYD:(A;;FA;;;SY)(A;;FA;;;BA)");
            fileInfo.SetAccessControl(accessControl);
        }
        catch
        {
        }
        errorMessage = null;
        return true;
    }

    public bool Open()
    {
        device = new SafeFileHandle(NativeMethods.CreateFile("\\\\.\\" + id, (FileAccess)3221225472u, 0u, IntPtr.Zero, CreationDisposition.OPEN_EXISTING, FileAttributes.FILE_ATTRIBUTE_NORMAL, IntPtr.Zero), ownsHandle: true);
        if (device.IsInvalid)
        {
            device.Close();
            device.Dispose();
            device = null;
        }
        return device != null;
    }

    public bool DeviceIOControl(IOControlCode ioControlCode, object inBuffer)
    {
        if (device == null)
        {
            return false;
        }
        uint bytesReturned;
        return NativeMethods.DeviceIoControl(device, ioControlCode, inBuffer, (inBuffer != null) ? ((uint)Marshal.SizeOf(inBuffer)) : 0u, null, 0u, out bytesReturned, IntPtr.Zero);
    }

    public bool DeviceIOControl<T>(IOControlCode ioControlCode, object inBuffer, ref T outBuffer)
    {
        if (device == null)
        {
            return false;
        }
        object obj = outBuffer;
        uint bytesReturned;
        bool result = NativeMethods.DeviceIoControl(device, ioControlCode, inBuffer, (inBuffer != null) ? ((uint)Marshal.SizeOf(inBuffer)) : 0u, obj, (uint)Marshal.SizeOf(obj), out bytesReturned, IntPtr.Zero);
        outBuffer = (T)obj;
        return result;
    }

    public void Close()
    {
        if (device != null)
        {
            device.Close();
            device.Dispose();
            device = null;
        }
    }

    public bool Delete()
    {
        IntPtr intPtr = NativeMethods.OpenSCManager(null, null, ServiceControlManagerAccessRights.SC_MANAGER_ALL_ACCESS);
        if (intPtr == IntPtr.Zero)
        {
            return false;
        }
        IntPtr intPtr2 = NativeMethods.OpenService(intPtr, id, ServiceAccessRights.SERVICE_ALL_ACCESS);
        if (intPtr2 == IntPtr.Zero)
        {
            return true;
        }
        ServiceStatus lpServiceStatus = default(ServiceStatus);
        NativeMethods.ControlService(intPtr2, ServiceControl.SERVICE_CONTROL_STOP, ref lpServiceStatus);
        NativeMethods.DeleteService(intPtr2);
        NativeMethods.CloseServiceHandle(intPtr2);
        NativeMethods.CloseServiceHandle(intPtr);
        return true;
    }
}