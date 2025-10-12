using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;
using Microsoft.Win32;
using Saku_Overclock.Helpers;

namespace Saku_Overclock.SMUEngine;

internal class GetSystemInfo
{
    private static readonly ManagementObjectSearcher ComputerSsystemInfo = new("root\\CIMV2", "SELECT * FROM Win32_ComputerSystemProduct");
    private static List<string> _cachedGpuList = [];
    private static bool _doNotTrackBattery;
    private static decimal _designCapacity = 0;
    private static decimal _fullCapacity = 0;

    #region Battery Information

    public static string? GetBatteryName()
    {
        try
        {
            var wmi = new ManagementClass("Win32_Battery");
            var allBatteries = wmi.GetInstances();
            var batteryName = "Battery not found";
            foreach (var battery in allBatteries)
            {
                if (battery["Name"] != null)
                {
                    batteryName = battery["Name"].ToString();
                    break;
                }
            }
            return batteryName;
        }
        catch
        {
            return string.Empty;
        }
    }

    public enum BatteryStatus : ushort
    {
        Discharging = 1,
        AcConnected,
        FullyCharged,
        Low,
        Critical,
        Charging,
        ChargingAndHigh,
        ChargingAndLow,
        ChargingAndCritical,
        Undefined,
        PartiallyCharged
    }

    public static BatteryStatus GetBatteryStatus()
    {
        try
        {
            var wmi = new ManagementClass("Win32_Battery");
            var allBatteries = wmi.GetInstances();
            var status = BatteryStatus.Undefined;

            foreach (var battery in allBatteries)
            {
                var pData = battery.Properties["BatteryStatus"];

                if (pData != null && pData.Value != null && Enum.IsDefined(typeof(BatteryStatus), pData.Value))
                {
                    status = (BatteryStatus)pData.Value;
                }
            }
            return status;
        }
        catch
        {
            return BatteryStatus.Undefined;
        }
    }

    public static decimal GetBatteryHealth()
    {
        try
        {
            var designCap = ReadDesignCapacity(out _);
            var fullCap = ReadFullChargeCapacity();
            if (designCap == 0) { return 0; }
            var health = fullCap / designCap;

            return health;
        }
        catch
        {
            return 100;
        }
    }

    public static decimal GetBatteryRate()
    {
        if (!HasBattery())
        {
            _doNotTrackBattery = true;
            return 0;
        }

        try
        {
            using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT ChargeRate, DischargeRate FROM BatteryStatus");
            using var results = searcher.Get();

            foreach (var obj in results.OfType<ManagementObject>())
            {
                var chargeRate = Convert.ToUInt32(obj["ChargeRate"]);
                var dischargeRate = Convert.ToUInt32(obj["DischargeRate"]);

                if (chargeRate > 0)
                {
                    return chargeRate;
                }

                if (dischargeRate > 0)
                {
                    return -dischargeRate;
                }
            }

            return 0; // Батареи нет
        }
        catch
        {
            return 0;
        }
    }
    public static bool HasBattery()
    {
        if (GetSystemPowerStatus(out var status))
        {
            // 0xFF означает "нет батареи"
            return status.BatteryFlag != 0xFF && status.BatteryLifePercent != 0xFF;
        }
        return false;
    }

    public static decimal ReadFullChargeCapacity()
    {
        if (_doNotTrackBattery) { return 0; }
        if (_fullCapacity == 0)
        {
            try
            {
                var scope = new ManagementScope("root\\WMI");
                var query = new ObjectQuery("SELECT * FROM BatteryFullChargedCapacity");

                using var searcher = new ManagementObjectSearcher(scope, query);
                foreach (var obj in searcher.Get().Cast<ManagementObject>())
                {
                    var fullCharged = Convert.ToDecimal(obj["FullChargedCapacity"]);
                    _fullCapacity = fullCharged;
                    return fullCharged;
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }
        else
        {
            return _fullCapacity;
        }
    }

    public static decimal ReadDesignCapacity(out bool doNotTrack)
    {
        if (!HasBattery())
        {
            _doNotTrackBattery = true;
        }

        if (_doNotTrackBattery)
        {
            doNotTrack = true;
            return 0;
        }

        if (_designCapacity == 0)
        {
            try
            {
                var scope = new ManagementScope("root\\WMI");
                var query = new ObjectQuery("SELECT * FROM BatteryStaticData");
                var searcher = new ManagementObjectSearcher(scope, query);
                if (searcher == null)
                {
                    doNotTrack = true;
                    _doNotTrackBattery = true;
                    return 0;
                }

                foreach (var obj in searcher.Get().Cast<ManagementObject>())
                {
                    doNotTrack = false; _doNotTrackBattery = false;
                    var returnCapacity = Convert.ToDecimal(obj["DesignedCapacity"]);
                    _designCapacity = returnCapacity;
                    return returnCapacity;
                }
                doNotTrack = true;
                _doNotTrackBattery = true;
                return 0;
            }
            catch
            {
                doNotTrack = true;
                _doNotTrackBattery = true;
                return 0;
            }
        }
        else
        {
            doNotTrack = false;
            _doNotTrackBattery = false;
            return _designCapacity;
        }
    }

    public static int GetBatteryCycle()
    {
        if (_doNotTrackBattery)
        {
            return 0;
        }

        try
        {
            var searcher =
                new ManagementObjectSearcher("root\\WMI",
                "SELECT * FROM BatteryCycleCount");

            foreach (var queryObj in searcher.Get().Cast<ManagementObject>())
            {
                return Convert.ToInt32(queryObj["CycleCount"]);
            }
            return 0;
        }
        catch
        {
            return 0;
        }
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus sps);

    [StructLayout(LayoutKind.Sequential)]
    public struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    public static decimal GetBatteryPercent()
    {
        try
        {
            if (GetSystemPowerStatus(out var sps))
            {
                return sps.BatteryLifePercent; // Примерно добавляет 0.1 для иллюстрации, если Windows Power Management даст не точное значение
            }
            return 0;
        }
        catch
        {
            return 0;
        }
    }

    public static int GetBatteryLifeTime()
    {
        try
        {
            if (GetSystemPowerStatus(out var sps))
            {
                // Проверяем, подключено ли устройство к сети
                if (sps.ACLineStatus == 1)
                {
                    return -1; // От сети
                }

                return sps.BatteryLifeTime; // Возвращаем оставшееся время работы от батареи в секундах
            }
            return 0;
        }
        catch
        {
            return 0;
        }
    }

    public static string ConvertBatteryLifeTime(int input)
    {
        var timeSpan = TimeSpan.FromSeconds(input); // Секунды в TimeSpan
        var batTime = "";
        if ((int)timeSpan.TotalHours > 0)
        {
            batTime += $"{(int)timeSpan.TotalHours}h"; // Часы, если они есть
        }

        if (timeSpan.Minutes > 0)
        {
            batTime += $"{timeSpan.Minutes}m"; // Минуты, если они есть
        }

        if (timeSpan.Seconds > 0 || batTime == string.Empty)
        {
            batTime += $"{timeSpan.Seconds}s"; // Секунды
        }
        return batTime;
    }

    #endregion

    #region OS Information

    public static string? GetOSVersion()
    {
        try
        {
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
            var sCPUSerialNumber = "";
            foreach (var naming in searcher.Get().Cast<ManagementObject>())
            {
                sCPUSerialNumber = naming["Name"].ToString()?.Trim();
            }
            var endString = sCPUSerialNumber?.Split("Windows");
            if (endString != null && endString.Length > 1)
            {
                return string.Concat("Windows ", endString[1].AsSpan(0, Math.Min(3, endString[1].Length)).Trim());
            }

            return "Windows 10";
        }
        catch
        {
            return "Windows 10";
        }
    }
    public static string GetBIOSVersion()
    {
        try
        {
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
            var biosVersion = string.Empty;
            foreach (var bios in searcher.Get())
            {
                biosVersion = bios["SMBIOSBIOSVersion"]?.ToString()?.Trim();
                break; // Выход из цикла после первого найденного объекта
            }
            return string.IsNullOrEmpty(biosVersion) ? "BIOS: Unknown" : $"BIOS: {biosVersion}";
        }
        catch
        {
            return "BIOS: Unknown";
        }
    }
    public static string? GetWindowsEdition()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        return key?.GetValue("EditionID")?.ToString();
    }

    #endregion

    #region Motherboard and GPU Information

    private static readonly Guid GUID_DEVCLASS_DISPLAY = new("4d36e968-e325-11ce-bfc1-08002be10318");
    private const uint DIGCF_PRESENT = 0x00000002;
    private const uint SPDRP_DEVICEDESC = 0x00000000;

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(
        ref Guid ClassGuid, IntPtr Enumerator, IntPtr hwndParent, uint Flags);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInfo(
        IntPtr DeviceInfoSet, uint MemberIndex, ref SP_DEVINFO_DATA DeviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool SetupDiGetDeviceRegistryProperty(
        IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, uint Property,
        out uint PropertyRegDataType, byte[] PropertyBuffer, uint PropertyBufferSize, out uint RequiredSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    public static List<string> GetGpuNames()
    {
        if (_cachedGpuList.Count != 0)
        {
            return _cachedGpuList;
        }

        var result = new List<string>();
        var guid = GUID_DEVCLASS_DISPLAY;
        var hDevInfo = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT);

        if (hDevInfo == IntPtr.Zero || hDevInfo == new IntPtr(-1))
        {
            _cachedGpuList = result;
            return result;
        }

        try
        {
            var devInfo = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
            var buffer = new byte[1024];

            for (uint i = 0; SetupDiEnumDeviceInfo(hDevInfo, i, ref devInfo); i++)
            {
                if (SetupDiGetDeviceRegistryProperty(hDevInfo, ref devInfo, SPDRP_DEVICEDESC,
                    out _, buffer, (uint)buffer.Length, out var requiredSize) && requiredSize > 2)
                {
                    var name = Encoding.Unicode.GetString(buffer, 0, (int)requiredSize - 2);

                    if (!string.IsNullOrWhiteSpace(name) &&
                        !name.Contains("virtual", StringComparison.OrdinalIgnoreCase) &&
                        !name.Contains("parsec", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(name);
                    }
                }
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(hDevInfo);
        }

        _cachedGpuList = result;
        return result;
    }

    public static string? Product
    {
        get
        {
            try
            {
                foreach (var queryObj in ComputerSsystemInfo.Get().Cast<ManagementObject>())
                {
                    return queryObj["Name"].ToString();
                }
                return "";
            }
            catch
            {
                return "";
            }
        }
    }

    #endregion

    #region CPU Information

    public static (string name, string baseClock) ReadCpuInformation()
    {
        const string key = @"HARDWARE\DESCRIPTION\System\CentralProcessor\0";
        using var reg = Registry.LocalMachine.OpenSubKey(key);
        var name = reg?.GetValue("ProcessorNameString") as string ?? "";
        var mhz = reg?.GetValue("~MHz")?.ToString() ?? "";
        return (name, mhz);
    }

    public enum CacheLevel : ushort
    {
        Level1 = 3,
        Level2 = 4,
        Level3 = 5,
    }

    public static List<uint> GetCacheSizes(CacheLevel level)
    {
        var mc = new ManagementClass("Win32_CacheMemory");
        var moc = mc.GetInstances();
        var cacheSizes = new List<uint>(moc.Count);

        cacheSizes.AddRange(moc
          .Cast<ManagementObject>()
          .Where(p => (ushort)p.Properties["Level"].Value == (ushort)level)
          .Select(p => (uint)p.Properties["MaxCacheSize"].Value));

        return cacheSizes;
    }

    public static double CalculateCacheSize(GetSystemInfo.CacheLevel level)
    {
        var sum = 0u;
        foreach (var number in GetSystemInfo.GetCacheSizes(level))
        {
            sum += number;
        }

        return sum / 1024.0;
    }

    public static string GetBigLITTLE(int cores)
    {
        var cpuName = CpuSingleton.GetInstance().info.cpuName;
        if (cpuName.Contains("7540U") || cpuName.Contains("7440U"))
        {
            var bigCores = 2;
            return $"{cores} ({bigCores}P+{cores - bigCores}E)";
        }

        return cores.ToString();
    }

    public static string InstructionSets()
    {
        var list = "x86-64, AMD-V";

        if (Avx.IsSupported)
        {
            list += ", AVX";
        }
        if (Avx2.IsSupported)
        {
            list += ", AVX2";
        }
        if (CheckAVX512Support())
        {
            list += ", AVX512";
        }
        if (Fma.IsSupported)
        {
            list += ", FMA";
        }
        if (Aes.IsSupported)
        {
            list += ", AES";
        }

        return list;
    }

    private static bool CheckAVX512Support()
    {
        try
        {
            if (CpuSingleton.GetInstance().info.codeName < ZenStates.Core.Cpu.CodeName.Raphael)
            {
                return false;
            }

            return NativeMethods.IsProcessorFeaturePresent(49 /*PF_AVX512F_INSTRUCTIONS_AVAILABLE*/ );
        }
        catch
        {
            return false;
        }
    }

    #endregion
}

public static partial class NativeMethods
{

    [LibraryImport("kernel32.dll")]
    public static partial void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_INFO
    {
        public ushort wProcessorArchitecture;
        public ushort wReserved;
        public uint dwPageSize;
        public nint lpMinimumApplicationAddress;
        public nint lpMaximumApplicationAddress;
        public nint dwActiveProcessorMask;
        public uint dwNumberOfProcessors;
        public uint dwProcessorType;
        public uint dwAllocationGranularity;
        public ushort wProcessorLevel;
        public ushort wProcessorRevision;
    }

    public static bool IsProcessorFeaturePresent(int processorFeature)
    {
        GetSystemInfo(out var sysInfo);
        return (sysInfo.wProcessorArchitecture == 9 /*Процессор AMD*/ ) &&
               (sysInfo.wProcessorLevel & processorFeature) != 0;
    }
}

internal partial class Garbage
{
    [LibraryImport("psapi.dll")]
    public static partial int EmptyWorkingSet(IntPtr hwProc);
    public static async Task Garbage_Collect()
    {
        try
        {
            await Task.Run(() =>
            {
                _ = EmptyWorkingSet(Process.GetCurrentProcess().Handle);
                var usedMemory = GC.GetTotalMemory(true);
            });
        }
        catch
        {

        }
    }
}