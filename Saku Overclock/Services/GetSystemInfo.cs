using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace Saku_Overclock.Services;
internal class GetSystemInfo
{
    private static ManagementObjectSearcher baseboardSearcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_BaseBoard");
    private static ManagementObjectSearcher motherboardSearcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_MotherboardDevice");
    private static ManagementObjectSearcher ComputerSsystemInfo = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_ComputerSystemProduct");

    public static string GetCPUName()
    {
        try
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Processor");
            ManagementObjectCollection collection = searcher.Get();
            foreach (ManagementObject obj in collection)
            {
                return obj["Name"].ToString();
            }
        }
        catch (Exception ex) { }
        return "";
    }
    public static string GetGPUName(int i)
    {
        try
        {
            int count = 0;
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\CIMV2", $"SELECT * FROM Win32_VideoController"); // Change AdapterCompatibility as per your requirement
            ManagementObjectCollection collection = searcher.Get();

            foreach (ManagementObject obj in collection)
            {
                if (count == i)
                {
                    Garbage.Garbage_Collect();
                    return obj["Name"].ToString();
                }
                count++;
            }
        }
        catch (Exception ex) { MessageBox.Show(ex.ToString()); }

        Garbage.Garbage_Collect();
        return "";
    }

    static public string Availability
    {
        get
        {
            try
            {
                foreach (ManagementObject queryObj in motherboardSearcher.Get())
                {
                    return GetAvailability(int.Parse(queryObj["Availability"].ToString()));
                }
                return "";
            }
            catch (Exception e)
            {
                return "";
            }
        }
    }

    static public bool HostingBoard
    {
        get
        {
            try
            {
                foreach (ManagementObject queryObj in baseboardSearcher.Get())
                {
                    if (queryObj["HostingBoard"].ToString() == "True")
                        return true;
                    else
                        return false;
                }
                return false;
            }
            catch (Exception e)
            {
                return false;
            }
        }
    }

    static public string InstallDate
    {
        get
        {
            try
            {
                foreach (ManagementObject queryObj in baseboardSearcher.Get())
                {
                    return ConvertToDateTime(queryObj["InstallDate"].ToString());
                }
                return "";
            }
            catch (Exception e)
            {
                return "";
            }
        }
    }

    static public string Manufacturer
    {
        get
        {
            try
            {
                foreach (ManagementObject queryObj in baseboardSearcher.Get())
                {
                    return queryObj["Manufacturer"].ToString();
                }
                return "";
            }
            catch (Exception e)
            {
                return "";
            }
        }
    }

    static public string Model
    {
        get
        {
            try
            {
                foreach (ManagementObject queryObj in baseboardSearcher.Get())
                {
                    return Convert.ToString(queryObj["Model"]);
                }
                return "";
            }
            catch (Exception e)
            {
                return "";
            }
        }
    }

    static public string PartNumber
    {
        get
        {
            try
            {
                foreach (ManagementObject queryObj in baseboardSearcher.Get())
                {
                    return queryObj["PartNumber"].ToString();
                }
                return "";
            }
            catch (Exception e)
            {
                return "";
            }
        }
    }

    static public string PNPDeviceID
    {
        get
        {
            try
            {
                foreach (ManagementObject queryObj in motherboardSearcher.Get())
                {
                    return queryObj["PNPDeviceID"].ToString();
                }
                return "";
            }
            catch (Exception e)
            {
                return "";
            }
        }
    }

    static public string PrimaryBusType
    {
        get
        {
            try
            {
                foreach (ManagementObject queryObj in motherboardSearcher.Get())
                {
                    return queryObj["PrimaryBusType"].ToString();
                }
                return "";
            }
            catch (Exception e)
            {
                return "";
            }
        }
    }

    static public string Product
    {
        get
        {
            try
            {
                foreach (ManagementObject queryObj in ComputerSsystemInfo.Get())
                {
                    return queryObj["Name"].ToString();
                }
                return "";
            }
            catch (Exception e)
            {
                return "";
            }
        }
    }

    static public bool Removable
    {
        get
        {
            try
            {
                foreach (ManagementObject queryObj in baseboardSearcher.Get())
                {
                    if (queryObj["Removable"].ToString() == "True")
                        return true;
                    else
                        return false;
                }
                return false;
            }
            catch (Exception e)
            {
                return false;
            }
        }
    }

    static public bool Replaceable
    {
        get
        {
            try
            {
                foreach (ManagementObject queryObj in baseboardSearcher.Get())
                {
                    if (queryObj["Replaceable"].ToString() == "True")
                        return true;
                    else
                        return false;
                }
                return false;
            }
            catch (Exception e)
            {
                return false;
            }
        }
    }

    static public string RevisionNumber
    {
        get
        {
            try
            {
                foreach (ManagementObject queryObj in motherboardSearcher.Get())
                {
                    return queryObj["RevisionNumber"].ToString();
                }
                return "";
            }
            catch (Exception e)
            {
                return "";
            }
        }
    }

    static public string SecondaryBusType
    {
        get
        {
            try
            {
                foreach (ManagementObject queryObj in motherboardSearcher.Get())
                {
                    return queryObj["SecondaryBusType"].ToString();
                }
                return "";
            }
            catch (Exception e)
            {
                return "";
            }
        }
    }

    static public string SerialNumber
    {
        get
        {
            try
            {
                foreach (ManagementObject queryObj in baseboardSearcher.Get())
                {
                    return queryObj["SerialNumber"].ToString();
                }
                return "";
            }
            catch (Exception e)
            {
                return "";
            }
        }
    }

    static public string Status
    {
        get
        {
            try
            {
                foreach (ManagementObject querObj in baseboardSearcher.Get())
                {
                    return querObj["Status"].ToString();
                }
                return "";
            }
            catch (Exception e)
            {
                return "";
            }
        }
    }

    static public string SystemName
    {
        get
        {
            try
            {
                foreach (ManagementObject queryObj in motherboardSearcher.Get())
                {
                    return queryObj["SystemName"].ToString();
                }
                return "";
            }
            catch (Exception e)
            {
                return "";
            }
        }
    }

    static public string Version
    {
        get
        {
            try
            {
                foreach (ManagementObject queryObj in baseboardSearcher.Get())
                {
                    return queryObj["Version"].ToString();
                }
                return "";
            }
            catch (Exception e)
            {
                return "";
            }
        }
    }

    private static string GetAvailability(int availability)
    {
        switch (availability)
        {
            case 1: return "Other";
            case 2: return "Unknown";
            case 3: return "Running or Full Power";
            case 4: return "Warning";
            case 5: return "In Test";
            case 6: return "Not Applicable";
            case 7: return "Power Off";
            case 8: return "Off Line";
            case 9: return "Off Duty";
            case 10: return "Degraded";
            case 11: return "Not Installed";
            case 12: return "Install Error";
            case 13: return "Power Save - Unknown";
            case 14: return "Power Save - Low Power Mode";
            case 15: return "Power Save - Standby";
            case 16: return "Power Cycle";
            case 17: return "Power Save - Warning";
            default: return "Unknown";
        }
    }

    private static string ConvertToDateTime(string unconvertedTime)
    {
        string convertedTime = "";
        int year = int.Parse(unconvertedTime.Substring(0, 4));
        int month = int.Parse(unconvertedTime.Substring(4, 2));
        int date = int.Parse(unconvertedTime.Substring(6, 2));
        int hours = int.Parse(unconvertedTime.Substring(8, 2));
        int minutes = int.Parse(unconvertedTime.Substring(10, 2));
        int seconds = int.Parse(unconvertedTime.Substring(12, 2));
        string meridian = "AM";
        if (hours > 12)
        {
            hours -= 12;
            meridian = "PM";
        }
        convertedTime = date.ToString() + "/" + month.ToString() + "/" + year.ToString() + " " +
        hours.ToString() + ":" + minutes.ToString() + ":" + seconds.ToString() + " " + meridian;
        return convertedTime;
    }

    public static decimal GetBatteryRate()
    {

        try
        {
            ManagementScope scope = new ManagementScope("root\\WMI");
            ObjectQuery query = new ObjectQuery("SELECT * FROM BatteryStatus");

            using ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, query);
            foreach (ManagementObject obj in searcher.Get().Cast<ManagementObject>())
            {
                decimal chargeRate = Convert.ToDecimal(obj["ChargeRate"]);
                decimal dischargeRate = Convert.ToDecimal(obj["DischargeRate"]);
                if (chargeRate > 0)
                    return chargeRate;
                else
                    return -dischargeRate;
            }

            return 0;

        }
        catch (Exception ex)
        {
            return 0;
        }
    }

    public static decimal ReadFullChargeCapacity()
    {

        try
        {
            ManagementScope scope = new ManagementScope("root\\WMI");
            ObjectQuery query = new ObjectQuery("SELECT * FROM BatteryFullChargedCapacity");

            using ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, query);
            foreach (ManagementObject obj in searcher.Get().Cast<ManagementObject>())
            {
                return Convert.ToDecimal(obj["FullChargedCapacity"]);
            }
            return 0;
        }
        catch (Exception ex)
        {
            return 0;
        }

    }

    public static decimal ReadDesignCapacity()
    {
        try
        {
            ManagementScope scope = new ManagementScope("root\\WMI");
            ObjectQuery query = new ObjectQuery("SELECT * FROM BatteryStaticData");

            using ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, query);
            foreach (ManagementObject obj in searcher.Get().Cast<ManagementObject>())
            {
                return Convert.ToDecimal(obj["DesignedCapacity"]);
            }
            return 0;
        }
        catch (Exception ex)
        {
            return 0;
        }
    }

    public static int GetBatteryCycle()
    {
        try
        {
            ManagementObjectSearcher searcher =
                new ManagementObjectSearcher("root\\WMI",
                "SELECT * FROM BatteryCycleCount");

            foreach (ManagementObject queryObj in searcher.Get())
            {

                return Convert.ToInt32(queryObj["CycleCount"]);
            }
            return 0;
        }
        catch (ManagementException e)
        {
            return 0;
        }
    }

    public static decimal GetBatteryHealth()
    {
        var designCap = ReadDesignCapacity();
        var fullCap = ReadFullChargeCapacity();

        decimal health = (decimal)fullCap / (decimal)designCap;

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
        ManagementClass mc = new ManagementClass("Win32_CacheMemory");
        ManagementObjectCollection moc = mc.GetInstances();
        List<uint> cacheSizes = new List<uint>(moc.Count);

        cacheSizes.AddRange(moc
          .Cast<ManagementObject>()
          .Where(p => (ushort)(p.Properties["Level"].Value) == (ushort)level)
          .Select(p => (uint)(p.Properties["MaxCacheSize"].Value)));

        return cacheSizes;
    }

    public static string GetWindowsEdition()
    {
        using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
        {
            return key?.GetValue("EditionID")?.ToString();
        }
    }

    public static string GetWindowsVersion()
    {
        using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
        {
            return key?.GetValue("CurrentVersion")?.ToString();
        }
    }

    public static DateTime GetWindowsInstallDate()
    {
        using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
        {
            string installDateValue = key?.GetValue("InstallDate")?.ToString();
            if (installDateValue != null && long.TryParse(installDateValue, out long installDateTicks))
            {
                return DateTime.FromFileTime(installDateTicks);
            }
        }

        return DateTime.MinValue;
    }

    public static string GetWindowsFeaturePack()
    {
        using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
        {
            return key?.GetValue("ProductName")?.ToString();
        }
    }

    public static string Codename()
    {
        string cpuName = Family.CPUName;
        if (Family.TYPE == Family.ProcessorType.Intel)
        {
            if (cpuName.Contains("6th")) return "Skylake";
            if (cpuName.Contains("7th")) return "Kaby Lake";
            if (cpuName.Contains("8th") && cpuName.Contains("G")) return "Kaby Lake";
            else if (cpuName.Contains("8121U") || cpuName.Contains("8114Y")) return "Cannon Lake";
            else if (cpuName.Contains("8th")) return "Coffee Lake";
            if (cpuName.Contains("9th")) return "Coffee Lake";
            if (cpuName.Contains("10th") && cpuName.Contains("G")) return "Ice Lake";
            else if (cpuName.Contains("10th")) return "Comet Lake";
            if (cpuName.Contains("11th") && cpuName.Contains("G") || cpuName.Contains("11th") && cpuName.Contains("U") || cpuName.Contains("11th") && cpuName.Contains("H") || cpuName.Contains("11th") && cpuName.Contains("KB")) return "Tiger Lake";
            else if (cpuName.Contains("11th")) return "Rocket Lake";
            if (cpuName.Contains("12th")) return "Alder Lake";
            if (cpuName.Contains("13th") || cpuName.Contains("14th") || cpuName.Contains("Core") && cpuName.Contains("100") && !cpuName.Contains("th")) return "Raptor Lake";
            if (cpuName.Contains("Core") && cpuName.Contains("Ultra") && cpuName.Contains("100")) return "Meteor Lake";
        }
        else
        {
            switch (Family.FAM)
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
                    if (cpuName.Contains("25") || cpuName.Contains("75") || cpuName.Contains("30")) return "Barcelo";
                    else return "Cezanne";
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

    public static string getBigLITTLE(int cores, double l2)
    {
        int bigCores = 0;
        int smallCores = 0;
        if (Family.TYPE == Family.ProcessorType.Intel)
        {
            if (Family.CPUName.Contains("12th") || Family.CPUName.Contains("13th") || Family.CPUName.Contains("14th") || Family.CPUName.Contains("Core") && Family.CPUName.Contains("1000") && !Family.CPUName.Contains("i"))
            {
                if (l2 % 1.25 == 0) bigCores = (int)(l2 / 1.25);
                else if (l2 % 2 == 0) bigCores = (int)(l2 / 2);

                smallCores = cores - bigCores;

                if (smallCores > 0)
                {
                    if (Family.CPUName.Contains("Ultra") && Family.CPUName.Contains("100")) return $"{cores} ({bigCores} Performance Cores + {smallCores - 2} Efficiency Cores + 2 LP Efficiency Cores)";
                    else return $"{cores} ({bigCores} Performance Cores + {smallCores} Efficiency Cores)";
                }
                else return cores.ToString();
            }
            else return cores.ToString();
        }
        else
        {
            if (Family.CPUName.Contains("7540U") || Family.CPUName.Contains("7440U"))
            {
                bigCores = 2;
                smallCores = cores - bigCores;
                return $"{cores} ({bigCores} Prime Cores + {smallCores} Compact Cores)";
            }
            else return cores.ToString();
        }
    }

    public static string InstructionSets()
    {
        string list = "";
        if (IsMMXSupported()) list = list + "MMX";
        if (Sse.IsSupported) list = list + ", SSE";
        if (Sse2.IsSupported) list = list + ", SSE2";
        if (Sse3.IsSupported) list = list + ", SSE3";
        if (Ssse3.IsSupported) list = list + ", SSSE3";
        if (Sse41.IsSupported) list = list + ", SSE4.1";
        if (Sse42.IsSupported) list = list + ", SSE4.2";
        if (IsEM64TSupported()) list = list + ", EM64T";
        if (Environment.Is64BitProcess) list = list + ", x86-64";
        if (IsVirtualizationEnabled() && Family.TYPE == Family.ProcessorType.Intel) list = list + ", VT-x";
        else if (IsVirtualizationEnabled()) list = list + ", AMD-V";
        if (Aes.IsSupported) list = list + ", AES";
        if (Avx.IsSupported) list = list + ", AVX";
        if (Avx2.IsSupported) list = list + ", AVX2";
        if (CheckAVX512Support()) list = list + ", AVX512";
        if (Fma.IsSupported) list = list + ", FMA3";

        string result = RemoveCommaSpaceFromStart(list);
        list = result;

        return list;
    }

    private static string RemoveCommaSpaceFromStart(string input)
    {
        string prefixToRemove = ", ";
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
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Processor");

            foreach (ManagementObject queryObj in searcher.Get())
            {
                int? virtualizationFirmwareEnabled = queryObj["VirtualizationFirmwareEnabled"] as int?;

                // Check if virtualization is enabled
                if (virtualizationFirmwareEnabled == 1)
                {
                    return true;
                }
            }
        }
        catch (ManagementException ex)
        {

        }

        return false;
    }

    public static bool IsEM64TSupported()
    {
        ManagementObject mo;
        mo = new ManagementObject("Win32_Processor.DeviceID='CPU0'");
        ushort i = (ushort)mo["Architecture"];

        return i == 9;
    }

    private static bool CheckAVX512Support()
    {
        try
        {
            if (Family.TYPE != Family.ProcessorType.Intel)
                if (Family.FAM < Family.RyzenFamily.Raphael) return false;
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

public static class NativeMethods
{
    // Import the CPUID intrinsic (Intel x86 instruction)
    [System.Runtime.InteropServices.DllImport("cpuid_x64.dll")]
    public static extern void Cpuid(int leafNumber, int subleafNumber, ref int eax, ref int ebx, ref int ecx, ref int edx);

    public const int PF_MMX_INSTRUCTIONS_AVAILABLE = 3;
    public const int PF_AVX512F_INSTRUCTIONS_AVAILABLE = 49;

    // Import the GetSystemInfo function (Windows API) to check MMX support.
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    public static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

    // Helper struct for GetSystemInfo function.
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct SYSTEM_INFO
    {
        public ushort wProcessorArchitecture;
        public ushort wReserved;
        public uint dwPageSize;
        public System.IntPtr lpMinimumApplicationAddress;
        public System.IntPtr lpMaximumApplicationAddress;
        public System.IntPtr dwActiveProcessorMask;
        public uint dwNumberOfProcessors;
        public uint dwProcessorType;
        public uint dwAllocationGranularity;
        public ushort wProcessorLevel;
        public ushort wProcessorRevision;
    }

    // Helper method to check MMX support on Windows.
    public static bool IsProcessorFeaturePresent(int processorFeature)
    {
        GetSystemInfo(out SYSTEM_INFO sysInfo);
        return (sysInfo.wProcessorArchitecture == PROCESSOR_ARCHITECTURE_INTEL ||
                sysInfo.wProcessorArchitecture == PROCESSOR_ARCHITECTURE_AMD64) &&
               (sysInfo.wProcessorLevel & processorFeature) != 0;
    }

    private const ushort PROCESSOR_ARCHITECTURE_INTEL = 0;
    private const ushort PROCESSOR_ARCHITECTURE_AMD64 = 9;
}
class Garbage
{
    [DllImport("psapi.dll")]
    static extern int EmptyWorkingSet(IntPtr hwProc);
    public static async Task Garbage_Collect()
    {
        try
        {
            await Task.Run(() =>
            {
                EmptyWorkingSet(Process.GetCurrentProcess().Handle);

                long usedMemory = GC.GetTotalMemory(true);
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

    public static RyzenFamily FAM = RyzenFamily.Unknown;

    public enum ProcessorType
    {
        Unknown = -1,
        Amd_Apu,
        Amd_Desktop_Cpu,
        Amd_Laptop_Cpu,
        Intel,
    }

    public static ProcessorType TYPE = ProcessorType.Unknown;


    public static string CPUName = "";
    public static int CPUFamily = 0, CPUModel = 0, CPUStepping = 0;
    public static async void setCpuFamily()
    {
        try
        {
            string processorIdentifier = System.Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");

            // Split the string into individual words
            string[] words = processorIdentifier.Split(' ');

            // Find the indices of the words "Family", "Model", and "Stepping"
            int familyIndex = Array.IndexOf(words, "Family") + 1;
            int modelIndex = Array.IndexOf(words, "Model") + 1;
            int steppingIndex = Array.IndexOf(words, "Stepping") + 1;

            // Extract the family, model, and stepping values from the corresponding words
            CPUFamily = int.Parse(words[familyIndex]);
            CPUModel = int.Parse(words[modelIndex]);
            CPUStepping = int.Parse(words[steppingIndex].TrimEnd(','));

            ManagementObjectSearcher mos = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Processor");
            foreach (ManagementObject mo in mos.Get())
            {
                CPUName = mo["Name"].ToString();
            }
        }
        catch (ManagementException e)
        {
            Debug.WriteLine("Error: " + e.Message);
        }

        if (CPUName.Contains("Intel")) TYPE = ProcessorType.Intel;
        else
        {
            //Zen1 - Zen2
            if (CPUFamily == 23)
            {
                if (CPUModel == 1) FAM = RyzenFamily.SummitRidge;

                if (CPUModel == 8) FAM = RyzenFamily.PinnacleRidge;

                if (CPUModel == 17 || CPUModel == 18) FAM = RyzenFamily.RavenRidge;

                if (CPUModel == 24) FAM = RyzenFamily.Picasso;

                if (CPUModel == 32 && CPUName.Contains("15e") || CPUModel == 32 && CPUName.Contains("15Ce") || CPUModel == 32 && CPUName.Contains("20e")) FAM = RyzenFamily.Pollock;
                else if (CPUModel == 32) FAM = RyzenFamily.Dali;

                if (CPUModel == 80) FAM = RyzenFamily.FireFlight;

                if (CPUModel == 96) FAM = RyzenFamily.Renoir;

                if (CPUModel == 104) FAM = RyzenFamily.Lucienne;

                if (CPUModel == 113) FAM = RyzenFamily.Matisse;

                if (CPUModel == 144) FAM = RyzenFamily.VanGogh;

                if (CPUModel == 160) FAM = RyzenFamily.Mendocino;
            }

            //Zen3 - Zen4
            if (CPUFamily == 25)
            {
                if (CPUModel == 33) FAM = RyzenFamily.Vermeer;

                if (CPUModel == 63 || CPUModel == 68) FAM = RyzenFamily.Rembrandt;

                if (CPUModel == 80) FAM = RyzenFamily.Cezanne_Barcelo;

                if (CPUModel == 97 && CPUName.Contains("HX")) FAM = RyzenFamily.DragonRange;
                else if (CPUModel == 97) FAM = RyzenFamily.Raphael;

                if (CPUModel == 116) FAM = RyzenFamily.PhoenixPoint;

                if (CPUModel == 120) FAM = RyzenFamily.PhoenixPoint2;
            }

            // Zen5 - Zen6
            if (CPUFamily == 26)
            {
                if (CPUModel == 32) FAM = RyzenFamily.StrixPoint;
                else FAM = RyzenFamily.GraniteRidge;
            }

            if (FAM == RyzenFamily.SummitRidge || FAM == RyzenFamily.PinnacleRidge || FAM == RyzenFamily.Matisse || FAM == RyzenFamily.Vermeer || FAM == RyzenFamily.Raphael || FAM == RyzenFamily.GraniteRidge) TYPE = ProcessorType.Amd_Desktop_Cpu;
            else TYPE = ProcessorType.Amd_Apu;

        }

        //Clipboard.SetText(System.Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER").ToString());
        //MessageBox.Show(CPUFamily.ToString() + " "  + FAM.ToString());
    }
}