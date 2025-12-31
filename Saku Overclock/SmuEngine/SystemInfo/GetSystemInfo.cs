using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;
using Microsoft.Win32;
using Saku_Overclock.Helpers;
using static Saku_Overclock.Services.CpuService;

namespace Saku_Overclock.SmuEngine;

internal class GetSystemInfo
{
    private static readonly ManagementObjectSearcher ComputerSsystemInfo = new("root\\CIMV2", "SELECT * FROM Win32_ComputerSystemProduct");
    private static List<string> _cachedGpuList = [];
    private static bool _doNotTrackBattery;
    private static decimal _designCapacity;
    private static decimal _fullCapacity;

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

                if (pData is { Value: not null } && Enum.IsDefined(typeof(BatteryStatus), pData.Value))
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

        return _fullCapacity;
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

        doNotTrack = false;
        _doNotTrackBattery = false;
        return _designCapacity;
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

    public static string? GetOsVersion()
    {
        try
        {
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
            var sCpuSerialNumber = "";
            foreach (var naming in searcher.Get().Cast<ManagementObject>())
            {
                sCpuSerialNumber = naming["Name"].ToString()?.Trim();
            }
            var endString = sCpuSerialNumber?.Split("Windows");
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
    public static string GetBiosVersion()
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

    /// <summary>
    /// Возвращает все необходимые статические метрики
    /// </summary>
    /// <returns>(CpuName, CpuBaseClock), IntegratedGpuName, DiscreteGpuName,
    /// (L1CacheSize, L2CacheSize, L3CacheSize), InstructionSets, CpuCaption</returns>
    public static ((string, string), string?, string?, (double, double, double), string?, string?) GetCommonMetrics(bool avxAvailable)
    {
        return (ReadCpuInformation(), GetIntegratedGpuName(), GetDiscreteGpuName(),
            (CalculateCacheSize(CacheLevel.Level1), CalculateCacheSize(CacheLevel.Level2), CalculateCacheSize(CacheLevel.Level3)),
            InstructionSets(avxAvailable), GetCpuCaption());
    }

    private static readonly Guid GuidDevclassDisplay = new("4d36e968-e325-11ce-bfc1-08002be10318");
    private const uint DigcfPresent = 0x00000002;
    private const uint SpdrpDevicedesc = 0x00000000;

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(
        ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInfo(
        IntPtr deviceInfoSet, uint memberIndex, ref SpDevinfoData deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool SetupDiGetDeviceRegistryProperty(
        IntPtr deviceInfoSet, ref SpDevinfoData deviceInfoData, uint property,
        out uint propertyRegDataType, byte[] propertyBuffer, uint propertyBufferSize, out uint requiredSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDevinfoData
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
        var guid = GuidDevclassDisplay;
        var hDevInfo = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, DigcfPresent);

        if (hDevInfo == IntPtr.Zero || hDevInfo == new IntPtr(-1))
        {
            _cachedGpuList = result;
            return result;
        }

        try
        {
            var devInfo = new SpDevinfoData { cbSize = (uint)Marshal.SizeOf<SpDevinfoData>() };
            var buffer = new byte[1024];

            for (uint i = 0; SetupDiEnumDeviceInfo(hDevInfo, i, ref devInfo); i++)
            {
                if (SetupDiGetDeviceRegistryProperty(hDevInfo, ref devInfo, SpdrpDevicedesc,
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

    public static string? GetIntegratedGpuName() => GetGpuNames()
                .FirstOrDefault(key => key.StartsWith("AMD"));

    public static string? GetDiscreteGpuName() => GetGpuNames()
                .FirstOrDefault(key => key.StartsWith("nvidia", 
                    StringComparison.CurrentCultureIgnoreCase));

    public static (string, string) GetRegistryGpuDriverInformation(string gpuName, bool isNvidia = false)
    {
        try
        {
            using var videoKey = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\ControlSet001\Control\Video");

            if (videoKey == null)
            {
                LogHelper.LogError("[GetSystemInfo+GetRegistryGpuDriverInformation]@ videoKey is Null. Skipping");
                return ("-GB", "Unknown");
            }

            foreach (var providerKey in videoKey.GetSubKeyNames())
            {
                using var providerSubKey = videoKey.OpenSubKey(providerKey);
                if (providerSubKey == null)
                {
                    LogHelper.LogError("[GetSystemInfo+GetRegistryGpuDriverInformation]@ providerSubKey is Null. Skipping");
                    continue;
                }

                foreach (var gpuKey in providerSubKey.GetSubKeyNames())
                {
                    using var gpuSubKey = providerSubKey.OpenSubKey(gpuKey);
                    if (gpuSubKey == null)
                    {
                        LogHelper.LogError("[GetSystemInfo+GetRegistryGpuDriverInformation]@ gpuSubKey is Null. Skipping");
                        continue;
                    }

                    var registryGpuName = gpuSubKey.GetValue(isNvidia 
                        ? "HardwareInformation.AdapterString" : "DriverDesc");
                    if (registryGpuName as string == gpuName)
                    {
                        var driverVersion = gpuSubKey.GetValue(isNvidia ? "DriverVersion" : "RadeonSoftwareVersion") as string;
                        var memorySizeValue = gpuSubKey.GetValue("HardwareInformation.qwMemorySize");
                        if (!string.IsNullOrEmpty(driverVersion))
                        {
                            var memorySize = "-GB";
                            if (memorySizeValue is long memorySizeBytes)
                            {
                                if (memorySizeBytes > 0)
                                {
                                    // Делим на 1024 три раза для перевода в гигабайты
                                    memorySize = (memorySizeBytes / 1024.0 / 1024.0 / 1024.0) + "GB";
                                }
                            }
                            else
                            {
                                LogHelper.LogWarn("[GetSystemInfo+GetRegistryGpuDriverInformation]@ memorySizeValue is not a long type");
                                if (int.TryParse(memorySizeValue as string, out var memorySizeFromString))
                                {
                                    memorySize = (memorySizeFromString / 1024.0 / 1024.0 / 1024.0) + "GB";
                                }
                            }

                            if (isNvidia)
                            {
                                driverVersion = ParseNvidiaDriverVersion(driverVersion);
                            }

                            return (memorySize, driverVersion);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex);
        }

        return ("-GB", "Unknown");
    }

    public static string GetAmdGpuDriverVersion(string gpuName)
    {
        try
        {
            using var videoKey = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\ControlSet001\Control\Video");

            if (videoKey == null)
            {
                return "Unknown";
            }

            foreach (var providerKey in videoKey.GetSubKeyNames())
            {
                using var providerSubKey = videoKey.OpenSubKey(providerKey);
                if (providerSubKey == null)
                {
                    continue;
                }

                foreach (var gpuKey in providerSubKey.GetSubKeyNames())
                {
                    using var gpuSubKey = providerSubKey.OpenSubKey(gpuKey);
                    if (gpuSubKey == null)
                    {
                        continue;
                    }

                    var registryGpuName = gpuSubKey.GetValue("DriverDesc");
                    if (registryGpuName as string == gpuName)
                    {
                        var driverVersion = gpuSubKey.GetValue("RadeonSoftwareVersion") as string;
                        if (!string.IsNullOrEmpty(driverVersion))
                        {
                            return driverVersion;
                        }
                    }
                }
            }
        }
        catch
        {
            // Игнорируем ошибки реестра
        }

        return "Unknown";
    }

    public static double GetGpuVramSize(string gpuName)
    {
        try
        {
            using var videoKey = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\ControlSet001\Control\Video");

            if (videoKey == null)
            {
                LogHelper.LogError("[GetSystemInfo+GetGpuVramSize]@ videoKey is Null. Skipping");
                return 0;
            }

            foreach (var providerKey in videoKey.GetSubKeyNames())
            {
                using var providerSubKey = videoKey.OpenSubKey(providerKey);
                if (providerSubKey == null)
                {
                    LogHelper.LogError("[GetSystemInfo+GetGpuVramSize]@ providerSubKey is Null. Skipping");
                    continue;
                }

                foreach (var gpuKey in providerSubKey.GetSubKeyNames())
                {
                    using var gpuSubKey = providerSubKey.OpenSubKey(gpuKey);
                    if (gpuSubKey == null)
                    {
                        LogHelper.LogError("[GetSystemInfo+GetGpuVramSize]@ gpuSubKey is Null. Skipping");
                        continue;
                    }

                    var registryGpuName = gpuSubKey.GetValue("HardwareInformation.AdapterString");
                    LogHelper.LogError("[GetSystemInfo+GetGpuVramSize]@ gpuSubKey = " + registryGpuName + "\nRequired GPU: " + gpuName);
                    if (registryGpuName as string == gpuName)
                    {
                        var memorySizeValue = gpuSubKey.GetValue("HardwareInformation.qwMemorySize");
                        if (memorySizeValue != null && memorySizeValue is long memorySizeBytes)
                        {
                            if (memorySizeBytes > 0)
                            {
                                // Делим на 1024 три раза для перевода в гигабайты
                                return memorySizeBytes / 1024.0 / 1024.0 / 1024.0;
                            }
                        }
                        if (memorySizeValue != null)
                        {
                            LogHelper.LogError("[GetSystemInfo+GetGpuVramSize]@ memorySizeValue is not long");
                        }
                    }
                }
            }
        }
        catch (Exception ex) 
        {
            LogHelper.LogError(ex);
        }

        return 0;
    }

    public static string ParseNvidiaDriverVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            LogHelper.LogError("[ParseNvidiaDriverVersion]@ Empty string");
            return string.Empty;
        }

        var parts = version.Split('.');

        if (parts.Length != 4)
        {
            LogHelper.LogError("[ParseNvidiaDriverVersion]@ Wrong part count");
            return string.Empty;
        }


        if (!int.TryParse(parts[2], out var c) || !int.TryParse(parts[3], out var dddd))
        {
            LogHelper.LogError("[ParseNvidiaDriverVersion]@ Parse failed");
            return string.Empty;
        }

        var internalMajor = c * 100 + (dddd / 100);
        var major = internalMajor - 1000;
        var minor = dddd % 100;

        return $"{major}.{minor:D2}";
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

    public static double CalculateCacheSize(CacheLevel level)
    {
        var sum = 0u;
        foreach (var number in GetCacheSizes(level))
        {
            sum += number;
        }

        return sum / 1024.0;
    }

    public static string GetBigLittle(string cpuName, int cores)
    {
        if (cpuName.Contains("7540U") || cpuName.Contains("7440U"))
        {
            var bigCores = 2;
            return $"{cores} ({bigCores}P+{cores - bigCores}E)";
        }

        return cores.ToString();
    }

    public static string InstructionSets(bool avxAvailable)
    {
        var list = "AMD-V";

        if (Avx.IsSupported)
        {
            list += ", AVX";
        }
        if (Avx2.IsSupported)
        {
            list += ", AVX2";
        }
        if (CheckAvx512Support(avxAvailable))
        {
            list += ", AVX512";
        }
        if (Aes.IsSupported)
        {
            list += ", AES";
        }

        return list;
    }

    private static bool CheckAvx512Support(bool avxAvailable)
    {
        try
        {
            if (!avxAvailable)
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

    public static string GetCpuCaption() => Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER")?.Replace(", AuthenticAMD", "") ?? string.Empty;

    /// <summary>
    ///  Возвращает основные свойства оперативной памяти
    /// </summary>
    /// <param name="memoryConfig">Конфигурация памяти</param>
    /// <param name="memorySpeed">Скорость MemoryClock</param>
    /// <param name="umcBase">Значение адреса umcBase</param>
    /// <param name="umcOffset1">Значение адреса umcOffset1</param>
    /// <param name="umcOffset2">Значение адреса umcOffset2</param>
    /// <returns>Capacity, MemoryType, Speed, Producer, Model, ModulesCount, 
    /// MemoryTimings</returns>
    public static (string, string, string?, string?, string,
        MemoryTimings) 
        GetMemoryInformation(MemoryConfig memoryConfig)
    {
        var capacity = memoryConfig.TotalCapacity;

        var speed = memoryConfig.MemorySpeed;

        if (speed == 0 || memoryConfig.FrequencyFromTimings > speed)
        {
            speed = memoryConfig.FrequencyFromTimings;
        }

        var modules = memoryConfig.Modules;

        var producer = modules.Count == 0
            ? "Unknown"
            : string.Join(" / ", modules
                .Select(m => m.Manufacturer ?? "Unknown")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct());

        var model = modules.Count == 0
            ? "Unknown"
            : string.Join(" / ", modules
                .Select(m => m.PartNumber ?? "Unknown")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct());

        return ($"{capacity} GB {memoryConfig.Type} @ {speed} MT/s",
            speed + "MT/s", producer, model, $"{modules.Count} * 64 bit", memoryConfig.MemoryTimings);
    }

    #endregion
}

public static partial class NativeMethods
{

    [LibraryImport("kernel32.dll")]
    public static partial void GetSystemInfo(out SystemInfo lpSystemInfo);

    [StructLayout(LayoutKind.Sequential)]
    public struct SystemInfo
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