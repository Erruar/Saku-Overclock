using System.Text;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.Win32.SafeHandles;
using static System.Runtime.InteropServices.UnmanagedType;

namespace Saku_Overclock.Services;

internal class Cpu : IDisposable
{
    public enum Family
    {
        Unsupported = 0,
        Family15H = 21,
        Family17H = 23,
        Family18H = 24,
        Family19H = 25
    }

    public enum CodeName
    {
        Unsupported,
        BristolRidge,
        SummitRidge,
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
        Fpx = 0,
        Am4 = 2,
        Sp3 = 4,
        Trx = 7
    }

    public struct Svi2
    {
        public uint CoreAddress;

        public uint SocAddress;

        public Svi2(uint coreAddress, uint socAddress)
        {
            CoreAddress = coreAddress;
            SocAddress = socAddress;
        }
    }

    public struct CpuTopology
    {
        // ReSharper disable once NotAccessedField.Global
        public uint Ccds;

        // ReSharper disable once NotAccessedField.Global
        public uint Ccxs;

        // ReSharper disable once NotAccessedField.Global
        public uint CoresPerCcx;

        // ReSharper disable once NotAccessedField.Global
        public uint Cores;

        public readonly uint LogicalCores;
        
        // ReSharper disable once NotAccessedField.Global
        public uint PhysicalCores;
        
        // ReSharper disable once NotAccessedField.Global
        public uint ThreadsPerCore;
        
        // ReSharper disable once NotAccessedField.Global
        public uint CpuNodes;
        
        // ReSharper disable once NotAccessedField.Global
        public uint CoreDisableMap;
        
        // ReSharper disable once NotAccessedField.Global
        public uint[] PerformanceOfCore;
        
        public CpuTopology(uint ccds, uint ccxs, uint coresPerCcx, uint cores, uint logicalCores, uint physicalCores, uint threadsPerCore, uint cpuNodes, uint coreDisableMap, uint[] performanceOfCore)
        {
            Ccds = ccds;
            Ccxs = ccxs;
            CoresPerCcx = coresPerCcx;
            Cores = cores;
            LogicalCores = logicalCores;
            PhysicalCores = physicalCores;
            ThreadsPerCore = threadsPerCore;
            CpuNodes = cpuNodes;
            CoreDisableMap = coreDisableMap;
            PerformanceOfCore = performanceOfCore;
        }
    }

    public struct CpuInfo
    {
        public uint Cpuid;

        public Family Family;

        public CodeName CodeName;

        public string CpuName;

        public string Vendor;

        public PackageType PackageType;

        public uint BaseModel;

        public uint ExtModel;

        public uint Model;

        public uint PatchLevel;

        public uint Stepping;

        public CpuTopology Topology;

        public Svi2 Svi2;

        public CpuInfo(uint cpuid, Family family, CodeName codeName, string cpuName, string vendor, PackageType packageType, uint baseModel, uint extModel, uint model, uint patchLevel, uint stepping, CpuTopology topology, Svi2 svi2)
        {
            Cpuid = cpuid;
            Family = family;
            CodeName = codeName;
            CpuName = cpuName;
            Vendor = vendor;
            PackageType = packageType;
            BaseModel = baseModel;
            ExtModel = extModel;
            Model = model;
            PatchLevel = patchLevel;
            Stepping = stepping;
            Topology = topology;
            Svi2 = svi2;
        }
    }

    private bool disposedValue;

    public const string InitializationExceptionText = "CPU module initialization failed.";

    public CpuInfo info;

    public static void Cpu_Init()
    {
        Ring0.Open();
        if (Ring0.IsOpen)
        {
            return;
        }

        var report = Ring0.GetReport();
        using var streamWriter = new StreamWriter("WinRing0.txt", append: true);
        streamWriter.Write(report);

        throw new ApplicationException("Error opening WinRing kernel driver");

    }
    [Obsolete("Obsolete")]
    public bool ReadMsr(uint index, ref uint eax, ref uint edx)
    {
        return Ring0.Rdmsr(index, out eax, out edx);
    }


    [Obsolete("Obsolete")]
    public bool WriteMsr(uint msr, uint eax, uint edx)
    {
        var result = true;
        for (var i = 0; i < info.Topology.LogicalCores; i++)
        {
            result = Ring0.WrmsrTx(msr, eax, edx, GroupAffinity.Single(0, i));
        }

        return result;
    }
    [Obsolete("Obsolete")]
    public bool WriteMsrWn(uint msr, uint eax, uint edx)
    {
        var result = true;
        for (var i = 0; i < 17; i++)
        {
            result =  Ring0.WrmsrTx(msr, eax, edx, GroupAffinity.Single(0, i));
        }
        return result;
    }
    [Obsolete("Obsolete")]
    protected virtual void Dispose(bool disposing)
    {
        if (disposedValue)
        {
            return;
        }

        if (disposing)
        {
            Ring0.Close();
        }

        disposedValue = true;
    }

    [Obsolete("Obsolete")]
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

    private static KernelDriver? _driver;

    private static string? _fileName;

    private static Mutex? _isaBusMutex;

    private static Mutex? _pciBusMutex;

    private static readonly StringBuilder Report = new();

    private static readonly IoControlCode IoctlOlsGetRefcount = new(40000u, 2049u, IoControlCode.Access.Any);

    private static readonly IoControlCode IoctlOlsReadMsr = new(40000u, 2081u, IoControlCode.Access.Any);

    private static readonly IoControlCode IoctlOlsWriteMsr = new(40000u, 2082u, IoControlCode.Access.Any);

    private static readonly IoControlCode IoctlOlsReadIoPortByte = new(40000u, 2099u, IoControlCode.Access.Read);

    private static readonly IoControlCode IoctlOlsWriteIoPortByte = new(40000u, 2102u, IoControlCode.Access.Write);

    private static readonly IoControlCode IoctlOlsReadPciConfig = new(40000u, 2129u, IoControlCode.Access.Read);

    private static readonly IoControlCode IoctlOlsWritePciConfig = new(40000u, 2130u, IoControlCode.Access.Write);

    private static readonly IoControlCode IoctlOlsReadMemory = new(40000u, 2113u, IoControlCode.Access.Read);

    public const uint InvalidPciAddress = uint.MaxValue;

    public static bool IsOpen => _driver != null;

    private static Assembly GetAssembly()
    {
        return typeof(Ring0).Assembly;
    }

    private static string? GetTempFileName()
    {
        var location = GetAssembly().Location;
        if (!string.IsNullOrEmpty(location))
        {
            try
            {
                var text = Path.ChangeExtension(location, ".sys");
                using (File.Create(text))
                {
                    return text;
                }
            }
            catch (Exception)
            {
                // ignored
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

    private static bool ExtractDriver(string? fileName)
    {
        var text = "ZenStates.Core." + (OperatingSystem.Is64BitOperatingSystem ? "WinRing0x64.sys" : "WinRing0.sys");
        var manifestResourceNames = GetAssembly().GetManifestResourceNames();
        byte[]? array = null;
        foreach (var t in manifestResourceNames)
        {
            if (t.Replace('\\', '.') != text)
            {
                continue;
            }

            using var stream = GetAssembly().GetManifestResourceStream(t);
            if (stream == null)
            {
                continue;
            }

            array = new byte[stream.Length];
        }
        if (array == null)
        {
            return false;
        }
        try
        {
            if (fileName != null)
            {
                using var fileStream = new FileStream(fileName, FileMode.Create);
                fileStream.Write(array, 0, array.Length);
                fileStream.Flush();
            }
        }
        catch (IOException)
        {
            return false;
        }
        for (var j = 0; j < 20; j++)
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
        Report.Length = 0;
        _driver = new KernelDriver("WinRing0_1_2_0");
        _driver.Open();
        if (!KernelDriver.IsOpen)
        {
            _fileName = GetTempFileName();
            if (_fileName != null && ExtractDriver(_fileName))
            {
                if (_driver.Install(_fileName, out var errorMessage))
                {
                    _driver.Open();
                    if (!KernelDriver.IsOpen)
                    {
                        _driver.Delete();
                        Report.AppendLine("Status: Opening driver failed after install");
                    }
                }
                else
                {
                    _driver.Delete();
                    Thread.Sleep(2000);
                    if (_driver.Install(_fileName, out var errorMessage2))
                    {
                        _driver.Open();
                        if (!KernelDriver.IsOpen)
                        {
                            _driver.Delete();
                            Report.AppendLine("Status: Opening driver failed after reinstall");
                        }
                    }
                    else
                    {
                        Report.AppendLine("Status: Installing driver \"" + _fileName + "\" failed" + (File.Exists(_fileName) ? " and file exists" : ""));
                        Report.AppendLine("First Exception: " + errorMessage);
                        Report.AppendLine("Second Exception: " + errorMessage2);
                    }
                }
            }
            else
            {
                Report.AppendLine("Status: Extracting driver failed");
            }
            try
            {
                if (File.Exists(_fileName))
                {
                    File.Delete(_fileName);
                }
                _fileName = null;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
        if (!KernelDriver.IsOpen)
        {
            _driver = null;
        }
        const string text2 = "Global\\Access_ISABUS.HTP.Method";
        try
        {
            _isaBusMutex = new Mutex(initiallyOwned: false, text2);
        }
        catch (UnauthorizedAccessException)
        {
            try
            {
                _isaBusMutex = Mutex.OpenExisting(text2);
            }
            catch
            {
                // ignored
            }
        }
        const string text3 = "Global\\Access_PCI";
        try
        {
            _pciBusMutex = new Mutex(initiallyOwned: false, text3);
        }
        catch (UnauthorizedAccessException)
        {
            try
            {
                _pciBusMutex = Mutex.OpenExisting(text3);
            }
            catch
            {
                // ignored
            }
        }
    }

    [Obsolete("Obsolete")]
    public static void Close()
    {
        if (_driver == null)
        {
            return;
        }
        var outBuffer = 0u;
        _driver.DeviceIoControl(IoctlOlsGetRefcount, null, ref outBuffer);
        _driver.Close();
        if (outBuffer <= 1)
        {
            _driver.Delete();
        }
        _driver = null;
        if (_isaBusMutex != null)
        {
            _isaBusMutex.Close();
            _isaBusMutex = null;
        }
        if (_pciBusMutex != null)
        {
            _pciBusMutex.Close();
            _pciBusMutex = null;
        }
        if (_fileName == null || !File.Exists(_fileName))
        {
            return;
        }
        try
        {
            File.Delete(_fileName);
            _fileName = null;
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public static string? GetReport()
    {
        if (Report.Length <= 0)
        {
            return null;
        }

        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("Ring0");
        stringBuilder.AppendLine();
        stringBuilder.Append((object?)Report);
        stringBuilder.AppendLine();
        return stringBuilder.ToString();
    }

    public static bool WaitIsaBusMutex(int millisecondsTimeout)
    {
        if (_isaBusMutex == null)
        {
            return true;
        }
        try
        {
            return _isaBusMutex.WaitOne(millisecondsTimeout, exitContext: false);
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
        _isaBusMutex?.ReleaseMutex();
    }

    public static bool WaitPciBusMutex(int millisecondsTimeout)
    {
        if (_pciBusMutex == null)
        {
            return true;
        }
        try
        {
            return _pciBusMutex.WaitOne(millisecondsTimeout, exitContext: false);
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
        _pciBusMutex?.ReleaseMutex();
    }

    [Obsolete("Obsolete")]
    public static bool Rdmsr(uint index, out uint eax, out uint edx)
    {
        if (_driver == null)
        {
            eax = 0u;
            edx = 0u;
            return false;
        }
        var outBuffer = 0uL;
        var result = _driver.DeviceIoControl(IoctlOlsReadMsr, index, ref outBuffer);
        edx = (uint)((outBuffer >> 32) & 0xFFFFFFFFu);
        eax = (uint)(outBuffer & 0xFFFFFFFFu);
        return result;
    }

    [Obsolete("Obsolete")]
    public static bool RdmsrTx(uint index, out uint eax, out uint edx, GroupAffinity affinity)
    {
        var affinity2 = ThreadAffinity.Set(affinity);
        var result = Rdmsr(index, out eax, out edx);
        ThreadAffinity.Set(affinity2);
        return result;
    }

    [Obsolete("Obsolete")]
    public static bool Wrmsr(uint index, uint eax, uint edx)
    {
        if (_driver == null)
        {
            return false;
        }
        var wrmsrInput = default(WrmsrInput);
        wrmsrInput.Register = index;
        wrmsrInput.Value = ((ulong)edx << 32) | eax;
        return _driver.DeviceIoControl(IoctlOlsWriteMsr, wrmsrInput);
    }

    [Obsolete("Obsolete")]
    public static bool WrmsrTx(uint index, uint eax, uint edx, GroupAffinity affinity)
    {
        if (_driver == null)
        {
            return false;
        }
        var wrmsrInput = default(WrmsrInput);
        wrmsrInput.Register = index;
        wrmsrInput.Value = ((ulong)edx << 32) | eax;
        var wrmsrInput2 = wrmsrInput;
        var affinity2 = ThreadAffinity.Set(affinity);
        var result = _driver.DeviceIoControl(IoctlOlsWriteMsr, wrmsrInput2);
        ThreadAffinity.Set(affinity2);
        return result;
    }

    [Obsolete("Obsolete")]
    public static byte ReadIoPort(uint port)
    {
        if (_driver == null)
        {
            return 0;
        }
        var outBuffer = 0u;
        _driver.DeviceIoControl(IoctlOlsReadIoPortByte, port, ref outBuffer);
        return (byte)(outBuffer & 0xFFu);
    }

    [Obsolete("Obsolete")]
    public static void WriteIoPort(uint port, byte value)
    {
        if (_driver == null)
        {
            return;
        }

        var writeIoPortInput = default(WriteIoPortInput);
        writeIoPortInput.PortNumber = port;
        writeIoPortInput.Value = value;
        _driver.DeviceIoControl(IoctlOlsWriteIoPortByte, writeIoPortInput);
    }

    public static uint GetPciAddress(byte bus, byte device, byte function)
    {
        return (uint)(((bus & 0xFF) << 8) | ((device & 0x1F) << 3)) | (function & 7u);
    }

    [Obsolete("Obsolete")]
    public static bool ReadPciConfig(uint pciAddress, uint regAddress, out uint value)
    {
        if (_driver == null || (regAddress & 3u) != 0)
        {
            value = 0u;
            return false;
        }
        var readPciConfigInput = default(ReadPciConfigInput);
        readPciConfigInput.PciAddress = pciAddress;
        readPciConfigInput.RegAddress = regAddress;
        value = 0u;
        return _driver.DeviceIoControl(IoctlOlsReadPciConfig, readPciConfigInput, ref value);
    }

    [Obsolete("Obsolete")]
    public static bool WritePciConfig(uint pciAddress, uint regAddress, uint value)
    {
        if (_driver == null || (regAddress & 3u) != 0)
        {
            return false;
        }
        var writePciConfigInput = default(WritePciConfigInput);
        writePciConfigInput.PciAddress = pciAddress;
        writePciConfigInput.RegAddress = regAddress;
        writePciConfigInput.Value = value;
        return _driver.DeviceIoControl(IoctlOlsWritePciConfig, writePciConfigInput);
    }

    [Obsolete("Obsolete")]
    public static bool ReadMemory<T>(ulong address, ref T? buffer)
    {
        if (_driver == null)
        {
            return false;
        }
        var readMemoryInput = default(ReadMemoryInput);
        readMemoryInput.address = address;
        readMemoryInput.unitSize = 1u;
        readMemoryInput.count = (uint)Marshal.SizeOf(structure: (object)buffer! ?? throw new InvalidOperationException());
        return _driver.DeviceIoControl(IoctlOlsReadMemory, readMemoryInput, ref buffer);
    }
}


internal static class ThreadAffinity
{
    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct GroupAffinity
        {
            public UIntPtr Mask;

            [MarshalAs(U2)]
            public ushort Group;

            [MarshalAs(ByValArray, SizeConst = 3, ArraySubType = U2)]
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
internal readonly struct GroupAffinity
{
    public static GroupAffinity Undefined = new(ushort.MaxValue, 0uL);

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

    public override bool Equals(object? o)
    {
        if (o == null || (object)GetType() != o.GetType())
        {
            return false;
        }
        var groupAffinity = (GroupAffinity)o;
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
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        get;
    }

    public static bool Is64BitOperatingSystem
    {
        get;
    }

    static OperatingSystem()
    {
        var platform = (int)Environment.OSVersion.Platform;
        IsUnix = platform is 4 or 6 or 128;
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
            var flag = IsWow64Process(Process.GetCurrentProcess().Handle, out var wow64Process);
            return flag && wow64Process;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(Bool)]
    private static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);
}
internal readonly struct IoControlCode
{
    private enum Method : uint
    {
        Buffered, // ReSharper disable once UnusedMember.Local
        InDirect, // ReSharper disable once UnusedMember.Local
        OutDirect, // ReSharper disable once UnusedMember.Local
        Neither
    }

    public enum Access : uint
    {
        Any,
        Read,
        Write
    }

    // ReSharper disable once NotAccessedField.Local
    private readonly uint code;

    public IoControlCode(uint deviceType, uint function, Access access)
        : this(deviceType, function, Method.Buffered, access)
    {
    }

    private IoControlCode(uint deviceType, uint function, Method method, Access access)
    {
        code = (deviceType << 16) | ((uint)access << 14) | (function << 2) | (uint)method;
    }
}
internal class KernelDriver
{
    private enum ServiceAccessRights : uint
    {
        ServiceAllAccess = 983551u
    }

    private enum ServiceControlManagerAccessRights : uint
    {
        ScManagerAllAccess = 983103u
    }

    private enum ServiceType : uint
    {
        ServiceKernelDriver = 1u,
        // ReSharper disable once UnusedMember.Local
        ServiceFileSystemDriver
    }

    private enum StartType : uint
    {
        // ReSharper disable once UnusedMember.Local
        ServiceBootStart, // ReSharper disable once UnusedMember.Local
        ServiceSystemStart, // ReSharper disable once UnusedMember.Local
        ServiceAutoStart,
        ServiceDemandStart, // ReSharper disable once UnusedMember.Local
        ServiceDisabled
    }

    private enum ErrorControl : uint
    {
        // ReSharper disable once UnusedMember.Local
        ServiceErrorIgnore,
        ServiceErrorNormal, // ReSharper disable once UnusedMember.Local
        ServiceErrorSevere, // ReSharper disable once UnusedMember.Local
        ServiceErrorCritical
    }

    private enum ServiceControl : uint
    {
        ServiceControlStop = 1u, // ReSharper disable once UnusedMember.Local
        ServiceControlPause, // ReSharper disable once UnusedMember.Local
        ServiceControlContinue, // ReSharper disable once UnusedMember.Local
        ServiceControlInterrogate, // ReSharper disable once UnusedMember.Local
        ServiceControlShutdown 
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
        // ReSharper disable once UnusedMember.Local
        GenericRead = 2147483648u, // ReSharper disable once UnusedMember.Local
        GenericWrite = 1073741824u
    }

    private enum CreationDisposition : uint
    {
        // ReSharper disable once UnusedMember.Local
        CreateNew = 1u, // ReSharper disable once UnusedMember.Local
        CreateAlways, 
        OpenExisting, // ReSharper disable once UnusedMember.Local
        OpenAlways, // ReSharper disable once UnusedMember.Local
        TruncateExisting
    }

    private enum FileAttributes : uint
    {
        FileAttributeNormal = 0x80u
    }

    private static class NativeMethods
    {
        // ReSharper disable once UnusedMember.Local
        private const string Kernel = "kernel32.dll";

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr OpenSCManager(string? machineName, string? databaseName, ServiceControlManagerAccessRights dwAccess);

        [DllImport("advapi32.dll")]
        [return: MarshalAs(Bool)]
        public static extern bool CloseServiceHandle(IntPtr hScObject);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateService(IntPtr hScManager, string lpServiceName, string lpDisplayName, ServiceAccessRights dwDesiredAccess, ServiceType dwServiceType, StartType dwStartType, ErrorControl dwErrorControl, string? lpBinaryPathName, string? lpLoadOrderGroup, string? lpdwTagId, string? lpDependencies, string? lpServiceStartName, string? lpPassword);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr OpenService(IntPtr hScManager, string lpServiceName, ServiceAccessRights dwDesiredAccess);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(Bool)]
        public static extern bool DeleteService(IntPtr hService);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(Bool)]
        public static extern bool StartService(IntPtr hService, uint dwNumServiceArgs, string[]? lpServiceArgVectors);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(Bool)]
        public static extern bool ControlService(IntPtr hService, ServiceControl dwControl, ref ServiceStatus lpServiceStatus);

        [DllImport("kernel32.dll")]
        [Obsolete("Obsolete")]
        public static extern bool DeviceIoControl(SafeFileHandle? device, IoControlCode ioControlCode, [In][MarshalAs(AsAny)] object? inBuffer, uint inBufferSize, [Out][MarshalAs(AsAny)] object? outBuffer, uint nOutBufferSize, out uint bytesReturned, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateFile(string lpFileName, FileAccess dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, CreationDisposition dwCreationDisposition, FileAttributes dwFlagsAndAttributes, IntPtr hTemplateFile);
    }

    private readonly string id;

    private SafeFileHandle? device;
    // ReSharper disable once UnusedMember.Local
    private const int ErrorServiceExists = -2147023823; // ReSharper disable once UnusedMember.Local

    private const int ErrorServiceAlreadyRunning = -2147023840;

    public static bool IsOpen => true;

    public KernelDriver(string id)
    {
        this.id = id;
    }

    public bool Install(string? path, out string? errorMessage)
    {
        var intPtr = NativeMethods.OpenSCManager(null, null, ServiceControlManagerAccessRights.ScManagerAllAccess);
        if (intPtr == IntPtr.Zero)
        {
            errorMessage = "OpenSCManager returned zero.";
            return false;
        }
        var intPtr2 = NativeMethods.CreateService(intPtr, id, id, ServiceAccessRights.ServiceAllAccess, ServiceType.ServiceKernelDriver, StartType.ServiceDemandStart, ErrorControl.ServiceErrorNormal, path, null, null, null, null, null);
        if (intPtr2 == IntPtr.Zero)
        {
            if (Marshal.GetHRForLastWin32Error() == -2147023823)
            {
                errorMessage = "Service already exists";
                return false;
            }
            errorMessage = "CreateService returned the error: " + Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error())?.Message;
            NativeMethods.CloseServiceHandle(intPtr);
            return false;
        }
        if (!NativeMethods.StartService(intPtr2, 0u, null) && Marshal.GetHRForLastWin32Error() != -2147023840)
        {
            errorMessage = "StartService returned the error: " + Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error())?.Message;
            NativeMethods.CloseServiceHandle(intPtr2);
            NativeMethods.CloseServiceHandle(intPtr);
            return false;
        }
        NativeMethods.CloseServiceHandle(intPtr2);
        NativeMethods.CloseServiceHandle(intPtr);
        try
        {
            var fileName = @"\\.\" + id;
            var fileInfo = new FileInfo(fileName);
            var accessControl = fileInfo.GetAccessControl();
            accessControl.SetSecurityDescriptorSddlForm("O:BAG:SYD:(A;;FA;;;SY)(A;;FA;;;BA)");
            fileInfo.SetAccessControl(accessControl);
        }
        catch
        {
            // ignored
        }

        errorMessage = null;
        return true;
    }

    public bool Open()
    {
        device = new SafeFileHandle(NativeMethods.CreateFile(@"\\.\" + id, (FileAccess)3221225472u, 0u, IntPtr.Zero, CreationDisposition.OpenExisting, FileAttributes.FileAttributeNormal, IntPtr.Zero), ownsHandle: true);
        if (!device.IsInvalid)
        {
            return device != null;
        }

        device.Close();
        device.Dispose();
        device = null;
        return device != null;
    }

    [Obsolete("Obsolete")]
    public bool DeviceIoControl(IoControlCode ioControlCode, object? inBuffer)
    {
        return device != null && NativeMethods.DeviceIoControl(device, ioControlCode, inBuffer, (inBuffer != null) ? ((uint)Marshal.SizeOf(inBuffer)) : 0u, null, 0u, out _, IntPtr.Zero);
    }

    [Obsolete("Obsolete")]
    public bool DeviceIoControl<T>(IoControlCode ioControlCode, object? inBuffer, ref T? outBuffer)
    {
        if (device == null)
        {
            return false;
        }
        object? obj = outBuffer;
        var result = obj != null && NativeMethods.DeviceIoControl(device, ioControlCode, inBuffer, (inBuffer != null) ? ((uint)Marshal.SizeOf(inBuffer)) : 0u, obj, (uint)Marshal.SizeOf(obj), out _, IntPtr.Zero);
        outBuffer = (T)obj!;
        return result;
    }

    public void Close()
    {
        if (device == null)
        {
            return;
        }

        device.Close();
        device.Dispose();
        device = null;
    }

    public bool Delete()
    {
        var intPtr = NativeMethods.OpenSCManager(null, null, ServiceControlManagerAccessRights.ScManagerAllAccess);
        if (intPtr == IntPtr.Zero)
        {
            return false;
        }
        var intPtr2 = NativeMethods.OpenService(intPtr, id, ServiceAccessRights.ServiceAllAccess);
        if (intPtr2 == IntPtr.Zero)
        {
            return true;
        }
        var lpServiceStatus = default(ServiceStatus);
        NativeMethods.ControlService(intPtr2, ServiceControl.ServiceControlStop, ref lpServiceStatus);
        NativeMethods.DeleteService(intPtr2);
        NativeMethods.CloseServiceHandle(intPtr2);
        NativeMethods.CloseServiceHandle(intPtr);
        return true;
    }
}