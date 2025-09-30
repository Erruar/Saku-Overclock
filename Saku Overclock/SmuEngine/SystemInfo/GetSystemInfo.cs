using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using Microsoft.Win32;
using Saku_Overclock.Helpers;

namespace Saku_Overclock.SMUEngine;

internal class GetSystemInfo
{
    private static readonly ManagementObjectSearcher ComputerSsystemInfo = new("root\\CIMV2", "SELECT * FROM Win32_ComputerSystemProduct");
    private static List<ManagementObject>? CachedGPUList;
    private static bool DoNotTrackBattery;
    private static decimal DesignCapacity = 0;
    private static decimal FullCapacity = 0;

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
            DoNotTrackBattery = true;
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
        if (DoNotTrackBattery) { return 0; }
        if (FullCapacity == 0)
        {
            try
            {
                var scope = new ManagementScope("root\\WMI");
                var query = new ObjectQuery("SELECT * FROM BatteryFullChargedCapacity");

                using var searcher = new ManagementObjectSearcher(scope, query);
                foreach (var obj in searcher.Get().Cast<ManagementObject>())
                {
                    var fullCharged = Convert.ToDecimal(obj["FullChargedCapacity"]);
                    FullCapacity = fullCharged;
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
            return FullCapacity;
        }
    }

    public static decimal ReadDesignCapacity(out bool doNotTrack)
    {
        if (!HasBattery())
        {
            DoNotTrackBattery = true;
        }

        if (DoNotTrackBattery)
        {
            doNotTrack = true;
            return 0;
        }

        if (DesignCapacity == 0)
        {
            try
            {
                var scope = new ManagementScope("root\\WMI");
                var query = new ObjectQuery("SELECT * FROM BatteryStaticData");
                var searcher = new ManagementObjectSearcher(scope, query);
                if (searcher == null)
                {
                    doNotTrack = true;
                    DoNotTrackBattery = true;
                    return 0;
                }

                foreach (var obj in searcher.Get().Cast<ManagementObject>())
                {
                    doNotTrack = false; DoNotTrackBattery = false;
                    var returnCapacity = Convert.ToDecimal(obj["DesignedCapacity"]);
                    DesignCapacity = returnCapacity;
                    return returnCapacity;
                }
                doNotTrack = true;
                DoNotTrackBattery = true;
                return 0;
            }
            catch
            {
                doNotTrack = true;
                DoNotTrackBattery = true;
                return 0;
            }
        }
        else
        {
            doNotTrack = false;
            DoNotTrackBattery = false;
            return DesignCapacity;
        }
    }

    public static int GetBatteryCycle()
    {
        if (DoNotTrackBattery)
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

    public static string? GetGPUName(int i)
    {
        try
        {
            // Отфильтрованный список видеокарт
            CachedGPUList ??= [.. new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_VideoController")
            .Get()
            .Cast<ManagementObject>()
            .Where(element =>
            {
                var name = element["Name"]?.ToString() ?? string.Empty;
                return !name.Contains("Parsec", StringComparison.OrdinalIgnoreCase) &&
                       !name.Contains("virtual", StringComparison.OrdinalIgnoreCase);
            })];


            if (i >= 0 && i < CachedGPUList.Count)
            {
                // Читаем имя видеокарты
                var gpuName = CachedGPUList[i]["Name"]?.ToString() ?? "Unknown GPU";
                _ = Garbage.Garbage_Collect();
                return gpuName;
            }

            return "Unknown GPU";
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError($"Error retrieving GPU name: {ex}");
        }

        _ = Garbage.Garbage_Collect();
        return "Unknown GPU";
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