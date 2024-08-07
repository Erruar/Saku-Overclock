﻿using Microsoft.Win32;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Windows;
/*This is a modified processor WMI info file. It from Universal x86 Tuning Utility. Its author is https://github.com/JamesCJ60
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/JamesCJ60/Universal-x86-Tuning-Utility
there you can see the source files in detail*/
namespace Saku_Overclock.SMUEngine;
internal class GetSystemInfo
{
    private static readonly ManagementObjectSearcher baseboardSearcher = new("root\\CIMV2", "SELECT * FROM Win32_BaseBoard");
    private static readonly ManagementObjectSearcher motherboardSearcher = new("root\\CIMV2", "SELECT * FROM Win32_MotherboardDevice");
    private static readonly ManagementObjectSearcher ComputerSsystemInfo = new("root\\CIMV2", "SELECT * FROM Win32_ComputerSystemProduct");
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

    public static string? GetCPUName()
    {
        try
        {
            var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Processor");
            var collection = searcher.Get();
            foreach (var obj in collection.Cast<ManagementObject>())
            {
                return obj["Name"].ToString();
            }
        }
        catch { }
        return "";
    }
    public static string? GetGPUName(int i)
    {
        try
        {
            var count = 0;
            var searcher = new ManagementObjectSearcher("root\\CIMV2", $"SELECT * FROM Win32_VideoController"); // Change AdapterCompatibility as per your requirement
            var collection = searcher.Get();

            foreach (var obj in collection.Cast<ManagementObject>())
            {
                if (count == i)
                {
                    _ = Garbage.Garbage_Collect();
                    return obj["Name"].ToString();
                }
                count++;
            }
        }
        catch (Exception ex) { MessageBox.Show(ex.ToString()); }

        _ = Garbage.Garbage_Collect();
        return "";
    } 
    public static string? Availability
    {
        get
        {
            try
            {
                foreach (var queryObj in motherboardSearcher.Get().Cast<ManagementObject>())
                {
                    return GetAvailability(int.Parse(queryObj[nameof(Availability)].ToString()!));
                }
                return "";
            }
            catch 
            {
                return "";
            }
        }
    } 
    public static bool HostingBoard
    {
        get
        {
            try
            {
                foreach (var queryObj in baseboardSearcher.Get().Cast<ManagementObject>())
                {
                    if (queryObj[nameof(HostingBoard)].ToString() == "True")
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                return false;
            }
            catch 
            {
                return false;
            }
        }
    } 
    public static string InstallDate
    {
        get
        {
            try
            {
                foreach (var queryObj in baseboardSearcher.Get().Cast<ManagementObject>())
                {
                    return ConvertToDateTime(queryObj[nameof(InstallDate)].ToString()!);
                }
                return "";
            }
            catch 
            {
                return "";
            }
        }
    } 
    public static string? Manufacturer
    {
        get
        {
            try
            {
                foreach (var queryObj in baseboardSearcher.Get().Cast<ManagementObject>())
                {
                    return queryObj[nameof(Manufacturer)].ToString();
                }
                return "";
            }
            catch 
            {
                return "";
            }
        }
    }

    public static string? Model
    {
        get
        {
            try
            {
                foreach (var queryObj in baseboardSearcher.Get().Cast<ManagementObject>())
                {
                    return Convert.ToString(queryObj[nameof(Model)]);
                }
                return "";
            }
            catch 
            {
                return "";
            }
        }
    }

    public static string? PartNumber
    {
        get
        {
            try
            {
                foreach (var queryObj in baseboardSearcher.Get().Cast<ManagementObject>())
                {
                    return queryObj[nameof(PartNumber)].ToString();
                }
                return "";
            }
            catch 
            {
                return "";
            }
        }
    }

    public static string? PNPDeviceID
    {
        get
        {
            try
            {
                foreach (var queryObj in motherboardSearcher.Get().Cast<ManagementObject>())
                {
                    return queryObj[nameof(PNPDeviceID)].ToString();
                }
                return "";
            }
            catch {
                return "";
            }
        }
    }

    public static string? PrimaryBusType
    {
        get
        {
            try
            {
                foreach (var queryObj in motherboardSearcher.Get().Cast<ManagementObject>())
                {
                    return queryObj[nameof(PrimaryBusType)].ToString();
                }
                return "";
            }
            catch {
                return "";
            }
        }
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
            catch {
                return "";
            }
        }
    }

    public static bool Removable
    {
        get
        {
            try
            {
                foreach (var queryObj in baseboardSearcher.Get().Cast<ManagementObject>())
                {
                    return queryObj[nameof(Removable)].ToString() == "True";
                }
                return false;
            }
            catch {
                return false;
            }
        }
    }

    public static bool Replaceable
    {
        get
        {
            try
            {
                foreach (var queryObj in baseboardSearcher.Get().Cast<ManagementObject>())
                {
                    return queryObj[nameof(Replaceable)].ToString() == "True";
                }
                return false;
            }
            catch {
                return false;
            }
        }
    }

    public static string? RevisionNumber
    {
        get
        {
            try
            {
                foreach (var queryObj in motherboardSearcher.Get().Cast<ManagementObject>())
                {
                    return queryObj[nameof(RevisionNumber)].ToString();
                }
                return "";
            }
            catch {
                return "";
            }
        }
    }

    public static string? SecondaryBusType
    {
        get
        {
            try
            {
                foreach (var queryObj in motherboardSearcher.Get().Cast<ManagementObject>())
                {
                    return queryObj[nameof(SecondaryBusType)].ToString();
                }
                return "";
            }
            catch {
                return "";
            }
        }
    }

    public static string? SerialNumber
    {
        get
        {
            try
            {
                foreach (var queryObj in baseboardSearcher.Get().Cast<ManagementObject>())
                {
                    return queryObj[nameof(SerialNumber)].ToString();
                }
                return "";
            }
            catch {
                return "";
            }
        }
    }

    public static string? Status
    {
        get
        {
            try
            {
                foreach (var querObj in baseboardSearcher.Get().Cast<ManagementObject>())
                {
                    return querObj[nameof(Status)].ToString();
                }
                return "";
            }
            catch {
                return "";
            }
        }
    }

    public static string? SystemName
    {
        get
        {
            try
            {
                foreach (var queryObj in motherboardSearcher.Get().Cast<ManagementObject>())
                {
                    return queryObj[nameof(SystemName)].ToString();
                }
                return "";
            }
            catch {
                return "";
            }
        }
    }

    public static string? Version
    {
        get
        {
            try
            {
                foreach (var queryObj in baseboardSearcher.Get().Cast<ManagementObject>())
                {
                    return queryObj[nameof(Version)].ToString();
                }
                return "";
            }
            catch {
                return "";
            }
        }
    }

    private static string GetAvailability(int availability)
    {
        return availability switch
        {
            1 => "Other",
            2 => "Unknown",
            3 => "Running or Full Power",
            4 => "Warning",
            5 => "In Test",
            6 => "Not Applicable",
            7 => "Power Off",
            8 => "Off Line",
            9 => "Off Duty",
            10 => "Degraded",
            11 => "Not Installed",
            12 => "Install Error",
            13 => "Power Save - Unknown",
            14 => "Power Save - Low Power Mode",
            15 => "Power Save - Standby",
            16 => "Power Cycle",
            17 => "Power Save - Warning",
            _ => "Unknown",
        };
    }

    private static string ConvertToDateTime(string unconvertedTime)
    {
        var year = int.Parse(unconvertedTime[..4]);
        var month = int.Parse(unconvertedTime.Substring(4, 2));
        var date = int.Parse(unconvertedTime.Substring(6, 2));
        var hours = int.Parse(unconvertedTime.Substring(8, 2));
        var minutes = int.Parse(unconvertedTime.Substring(10, 2));
        var seconds = int.Parse(unconvertedTime.Substring(12, 2));
        var meridian = "AM";
        if (hours > 12)
        {
            hours -= 12;
            meridian = "PM";
        }
        var convertedTime = date.ToString() + "/" + month.ToString() + "/" + year.ToString() + " " + hours.ToString() + ":" + minutes.ToString() + ":" + seconds.ToString() + " " + meridian;
        return convertedTime;
    }

    public static decimal GetBatteryRate()
    { 
        try
        {
            var scope = new ManagementScope("root\\WMI");
            var query = new ObjectQuery("SELECT * FROM BatteryStatus");

            using var searcher = new ManagementObjectSearcher(scope, query);
            foreach (var obj in searcher.Get().Cast<ManagementObject>())
            {
                var chargeRate = Convert.ToUInt32(obj["ChargeRate"]);
                var dischargeRate = Convert.ToUInt32(obj["DischargeRate"]);
                if (chargeRate > 0)
                {
                    return chargeRate;
                }
                else
                {
                    return -dischargeRate;
                }
            } 
            return 0; 
        }
        catch  
        {
            return 0;
        }
    }

    public static decimal ReadFullChargeCapacity()
    { 
        try
        {
            var scope = new ManagementScope("root\\WMI");
            var query = new ObjectQuery("SELECT * FROM BatteryFullChargedCapacity");

            using var searcher = new ManagementObjectSearcher(scope, query);
            foreach (var obj in searcher.Get().Cast<ManagementObject>())
            {
                return Convert.ToDecimal(obj["FullChargedCapacity"]);
            }
            return 0;
        }
        catch  
        {
            return 0;
        } 
    }

    public static decimal ReadDesignCapacity()
    {
        try
        {
            var scope = new ManagementScope("root\\WMI");
            var query = new ObjectQuery("SELECT * FROM BatteryStaticData");

            using var searcher = new ManagementObjectSearcher(scope, query);
            foreach (var obj in searcher.Get().Cast<ManagementObject>())
            {
                return Convert.ToDecimal(obj["DesignedCapacity"]);
            }
            return 0;
        }
        catch  
        {
            return 0;
        }
    }

    public static int GetBatteryCycle()
    {
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
        if (GetSystemPowerStatus(out var sps))
        {
            return sps.BatteryLifePercent != 100 ? sps.BatteryLifePercent + 0.1m : sps.BatteryLifePercent; // Примерно добавляет 0.1 для иллюстрации, если Windows Power Management даст не точное значение
        }
        else
        {
            throw new InvalidOperationException("Unable to get power status.");
        }
    }
    public static int GetBatteryLifeTime()
    {
        if (GetSystemPowerStatus(out var sps))
        {
            // Проверяем, подключено ли устройство к сети
            if (sps.ACLineStatus == 1)
            {
                return -1; // От сети
            }
            else
            {
                return sps.BatteryLifeTime; // Возвращаем оставшееся время работы от батареи в секундах
            }
        }
        else
        {
            throw new InvalidOperationException("Unable to get power status.");
        }
    }
    public static string? GetBatteryName()
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
    public static BatteryStatus GetBatteryStatus()
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

    public static decimal GetBatteryHealth()
    {
        var designCap = ReadDesignCapacity();
        var fullCap = ReadFullChargeCapacity();

        var health = fullCap / designCap;

        return health;
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
          .Where(p => (ushort)(p.Properties["Level"].Value) == (ushort)level)
          .Select(p => (uint)(p.Properties["MaxCacheSize"].Value)));

        return cacheSizes;
    }

    public static string? GetWindowsEdition()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        return key?.GetValue("EditionID")?.ToString();
    }

    public static string? GetWindowsVersion()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        return key?.GetValue("CurrentVersion")?.ToString();
    }

    public static DateTime GetWindowsInstallDate()
    {
        using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
        {
            var installDateValue = key?.GetValue("InstallDate")?.ToString();
            if (installDateValue != null && long.TryParse(installDateValue, out var installDateTicks))
            {
                return DateTime.FromFileTime(installDateTicks);
            }
        }

        return DateTime.MinValue;
    }

    public static string? GetWindowsFeaturePack()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        return key?.GetValue("ProductName")?.ToString();
    }

    public static string Codename()
    {
        var cpuName = FamilyHelpers.CPUName;
        if (FamilyHelpers.TYPE == Family.ProcessorType.Intel)
        {
            if (cpuName.Contains("6th"))
            {
                return "Skylake";
            }

            if (cpuName.Contains("7th"))
            {
                return "Kaby Lake";
            }

            if (cpuName.Contains("8th") && cpuName.Contains('G'))
            {
                return "Kaby Lake";
            }
            else if (cpuName.Contains("8121U") || cpuName.Contains("8114Y"))
            {
                return "Cannon Lake";
            }
            else if (cpuName.Contains("8th"))
            {
                return "Coffee Lake";
            }

            if (cpuName.Contains("9th"))
            {
                return "Coffee Lake";
            }

            if (cpuName.Contains("10th") && cpuName.Contains('G'))
            {
                return "Ice Lake";
            }
            else if (cpuName.Contains("10th"))
            {
                return "Comet Lake";
            }

            if (cpuName.Contains("11th") && cpuName.Contains('G') || cpuName.Contains("11th") && cpuName.Contains('U') || cpuName.Contains("11th") && cpuName.Contains('H') || cpuName.Contains("11th") && cpuName.Contains("KB"))
            {
                return "Tiger Lake";
            }
            else if (cpuName.Contains("11th"))
            {
                return "Rocket Lake";
            }

            if (cpuName.Contains("12th"))
            {
                return "Alder Lake";
            }

            if (cpuName.Contains("13th") || cpuName.Contains("14th") || cpuName.Contains("Core") && cpuName.Contains("100") && !cpuName.Contains("th"))
            {
                return "Raptor Lake";
            }

            if (cpuName.Contains("Core") && cpuName.Contains("Ultra") && cpuName.Contains("100"))
            {
                return "Meteor Lake";
            }
        }
        else
        {
            switch (FamilyHelpers.FAM)
            {
                case Family.RyzenFamily.SummitRidge:
                    return "Summit Ridge";
                case Family.RyzenFamily.PinnacleRidge:
                    return "Pinnacle Ridge";
                case Family.RyzenFamily.RavenRidge:
                    return "Raven Ridge";
                case Family.RyzenFamily.Dali:
                    return "Dali";
                case Family.RyzenFamily.Pollock:
                    return "Pollock";
                case Family.RyzenFamily.Picasso:
                    return "Picasso";
                case Family.RyzenFamily.FireFlight:
                    return "Fire Flight";
                case Family.RyzenFamily.Matisse:
                    return "Matisse";
                case Family.RyzenFamily.Renoir:
                    return "Renoir";
                case Family.RyzenFamily.Lucienne:
                    return "Lucienne";
                case Family.RyzenFamily.VanGogh:
                    return "Van Gogh";
                case Family.RyzenFamily.Mendocino:
                    return "Mendocino";
                case Family.RyzenFamily.Vermeer:
                    return "Vermeer";
                case Family.RyzenFamily.Cezanne_Barcelo:
                    if (cpuName.Contains("25") || cpuName.Contains("75") || cpuName.Contains("30"))
                    {
                        return "Barcelo";
                    }
                    else
                    {
                        return "Cezanne";
                    }

                case Family.RyzenFamily.Rembrandt:
                    return "Rembrandt";
                case Family.RyzenFamily.Raphael:
                    return "Raphael";
                case Family.RyzenFamily.DragonRange:
                    return "Dragon Range";
                case Family.RyzenFamily.PhoenixPoint:
                    return "Phoenix Point";
                case Family.RyzenFamily.PhoenixPoint2:
                    return "Phoenix Point 2";
                case Family.RyzenFamily.HawkPoint:
                    return "Hawk Point";
                case Family.RyzenFamily.SonomaValley:
                    return "Sonoma Valley";
                case Family.RyzenFamily.GraniteRidge:
                    return "Granite Ridge";
                case Family.RyzenFamily.FireRange:
                    return "Fire Range";
                case Family.RyzenFamily.StrixPoint:
                    return "Strix Point";
                case Family.RyzenFamily.StrixPoint2:
                    return "Strix Point 2";
                case Family.RyzenFamily.Sarlak:
                    return "Sarlak";
                default:
                    return "";
            }
        }
        return "";
    }
    public static string GetBigLITTLE(int cores, double l2)
    {
        var bigCores = 0;
        int smallCores;
        if (FamilyHelpers.TYPE == Family.ProcessorType.Intel)
        {
            if (FamilyHelpers.CPUName.Contains("12th") || FamilyHelpers.CPUName.Contains("13th") || FamilyHelpers.CPUName.Contains("14th") || FamilyHelpers.CPUName.Contains("Core") && FamilyHelpers.CPUName.Contains("1000") && !FamilyHelpers.CPUName.Contains('i'))
            {
                if (l2 % 1.25 == 0)
                {
                    bigCores = (int)(l2 / 1.25);
                }
                else if (l2 % 2 == 0)
                {
                    bigCores = (int)(l2 / 2);
                } 
                smallCores = cores - bigCores;

                if (smallCores > 0)
                {
                    return FamilyHelpers.CPUName.Contains("Ultra") && FamilyHelpers.CPUName.Contains("100")
                        ? $"{cores} ({bigCores} Performance Cores + {smallCores - 2} Efficiency Cores + 2 LP Efficiency Cores)"
                        : $"{cores} ({bigCores} Performance Cores + {smallCores} Efficiency Cores)";
                }
                else
                {
                    return cores.ToString();
                }
            }
            else
            {
                return cores.ToString();
            }
        }
        else
        {
            if (FamilyHelpers.CPUName.Contains("7540U") || FamilyHelpers.CPUName.Contains("7440U"))
            {
                bigCores = 2;
                smallCores = cores - bigCores;
                return $"{cores} ({bigCores} Prime Cores + {smallCores} Compact Cores)";
            }
            else
            {
                return cores.ToString();
            }
        }
    }

    public static string InstructionSets()
    {
        var list = "";
        if (IsMMXSupported())
        {
            list += "MMX";
        } 
        if (Sse.IsSupported)
        {
            list += ", SSE";
        } 
        if (Sse2.IsSupported)
        {
            list += ", SSE2";
        } 
        if (Sse3.IsSupported)
        {
            list += ", SSE3";
        } 
        if (Ssse3.IsSupported)
        {
            list += ", SSSE3";
        } 
        if (Sse41.IsSupported)
        {
            list += ", SSE4.1";
        } 
        if (Sse42.IsSupported)
        {
            list += ", SSE4.2";
        } 
        if (IsEM64TSupported())
        {
            list += ", EM64T";
        } 
        if (Environment.Is64BitProcess)
        {
            list += ", x86-64";
        } 
        if (IsVirtualizationEnabled() && FamilyHelpers.TYPE == Family.ProcessorType.Intel)
        {
            list += ", VT-x";
        }
        else if (IsVirtualizationEnabled())
        {
            list += ", AMD-V";
        } 
        if (Aes.IsSupported)
        {
            list += ", AES";
        } 
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
            list += ", FMA3";
        } 
        var result = RemoveCommaSpaceFromStart(list);
        list = result; 
        return list;
    }

    private static string RemoveCommaSpaceFromStart(string input)
    {
        var prefixToRemove = ", ";
        if (input.StartsWith(prefixToRemove))
        {
            input = input.Remove(0, prefixToRemove.Length);
        }
        return input;
    } 
    private static bool IsVirtualizationEnabled()
    {
        try
        {
            var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Processor");

            foreach (var queryObj in searcher.Get().Cast<ManagementObject>())
            {
                var virtualizationFirmwareEnabled = queryObj["VirtualizationFirmwareEnabled"] as int?;

                // Check if virtualization is enabled
                if (virtualizationFirmwareEnabled == 1)
                {
                    return true;
                }
            }
        }
        catch 
        {

        } 
        return false;
    } 
    public static bool IsEM64TSupported()
    {
        ManagementObject mo;
        mo = new ManagementObject("Win32_Processor.DeviceID='CPU0'");
        var i = (ushort)mo["Architecture"];

        return i == 9;
    } 
    private static bool CheckAVX512Support()
    {
        try
        {
            if (FamilyHelpers.TYPE != Family.ProcessorType.Intel)
            {
                if (FamilyHelpers.FAM < Family.RyzenFamily.Raphael)
                {
                    return false;
                }
            }

            return NativeMethods.IsProcessorFeaturePresent(NativeMethods.PF_AVX512F_INSTRUCTIONS_AVAILABLE);
        }
        catch
        {
            // If there's an exception during CPUID call, AVX-512 is not supported
            return false;
        }
    } 
    private static bool IsMMXSupported()
    {
        if (Environment.Is64BitProcess)
        {
            // For 64-bit processes, MMX is always supported on Windows.
            return true;
        }
        else
        {
            // For 32-bit processes, check for MMX support on Windows.
            return NativeMethods.IsProcessorFeaturePresent(NativeMethods.PF_MMX_INSTRUCTIONS_AVAILABLE);
        }
    }
} 
public static partial class NativeMethods
{
    // Import the CPUID intrinsic (Intel x86 instruction)
    [LibraryImport("cpuid_x64.dll")]
    public static partial void Cpuid(int leafNumber, int subleafNumber, ref int eax, ref int ebx, ref int ecx, ref int edx);
    public const int PF_MMX_INSTRUCTIONS_AVAILABLE = 3;
    public const int PF_AVX512F_INSTRUCTIONS_AVAILABLE = 49;
    // Import the GetSystemInfo function (Windows API) to check MMX support.
    [LibraryImport("kernel32.dll")]
    public static partial void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);
    // Helper struct for GetSystemInfo function.
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
    // Helper method to check MMX support on Windows.
    public static bool IsProcessorFeaturePresent(int processorFeature)
    {
        GetSystemInfo(out var sysInfo);
        return (sysInfo.wProcessorArchitecture == PROCESSOR_ARCHITECTURE_INTEL ||
                sysInfo.wProcessorArchitecture == PROCESSOR_ARCHITECTURE_AMD64) &&
               (sysInfo.wProcessorLevel & processorFeature) != 0;
    }
    private const ushort PROCESSOR_ARCHITECTURE_INTEL = 0;
    private const ushort PROCESSOR_ARCHITECTURE_AMD64 = 9;
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
public class Family
{
    public enum RyzenFamily
    {
        Unknown = -1,
        SummitRidge,
        PinnacleRidge,
        RavenRidge,
        Dali,
        Pollock,
        Picasso,
        FireFlight,
        Matisse,
        Renoir,
        Lucienne,
        VanGogh,
        Mendocino,
        Vermeer,
        Cezanne_Barcelo,
        Rembrandt,
        Raphael,
        DragonRange,
        PhoenixPoint,
        PhoenixPoint2,
        HawkPoint,
        SonomaValley,
        GraniteRidge,
        FireRange,
        StrixPoint,
        StrixPoint2,
        Sarlak,
    }

    public enum ProcessorType
    {
        Unknown = -1,
        Amd_Apu,
        Amd_Desktop_Cpu,
        Amd_Laptop_Cpu,
        Intel,
    }
}