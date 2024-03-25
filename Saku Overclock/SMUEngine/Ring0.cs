using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
/*This is a modified processor driver file. Its from Open Hardware monitor. Its author is https://github.com/openhardwaremonitor
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/openhardwaremonitor/openhardwaremonitor
there you can see the source files in detail*/
namespace Saku_Overclock.SMUEngine;

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
        var text = "" + (OperatingSystem.Is64BitOperatingSystem ? "WinRing0x64.sys" : "WinRing0.sys");
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