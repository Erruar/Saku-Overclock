using System.Management;
using System.ServiceProcess;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
namespace Saku_Overclock.SMUEngine;

[Serializable]
public class SystemInfo
{
    private static Cpu.CPUInfo cpuInfo;

    public SystemInfo(Cpu.CPUInfo info, SMU smu)
    {
        cpuInfo = info;
        SmuVersion = smu.Version;
        SmuTableVersion = smu.TableVersion;
        try
        {
            if (new ServiceController("Winmgmt").Status != ServiceControllerStatus.Running)
            {
                throw new ManagementException("Windows Management Instrumentation service is not running");
            }

            var managementScope = new ManagementScope("root\\cimv2");
            managementScope.Connect();
            if (!managementScope.IsConnected)
            {
                throw new ManagementException("Failed to connect to root\\cimv2");
            }

            var managementObjectSearcher1 = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
            foreach (var managementBaseObject in managementObjectSearcher1.Get())
            {
                MbVendor = ((string)managementBaseObject["Manufacturer"]).Trim();
                MbName = ((string)managementBaseObject["Product"]).Trim();
            }
            managementObjectSearcher1.Dispose();
            var managementObjectSearcher2 = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
            foreach (var managementBaseObject in managementObjectSearcher2.Get())
            {
                BiosVersion = ((string)managementBaseObject["SMBIOSBIOSVersion"]).Trim();
            }

            managementObjectSearcher2.Dispose();
        }
        catch (ManagementException ex)
        {
            Console.WriteLine("WMI: {0}", ex.Message);
        }
    }

    public string CpuName => cpuInfo.cpuName ?? "N/A";

    public string CodeName => cpuInfo.codeName.ToString();

    public uint CpuId => cpuInfo.cpuid;

    public uint BaseModel => cpuInfo.baseModel;

    public uint ExtendedModel => cpuInfo.extModel;

    public uint Model => cpuInfo.model;

    public uint Stepping => cpuInfo.stepping;

    public string PackageType => string.Format("{0} ({1})", cpuInfo.packageType, (int)cpuInfo.packageType);

    public int FusedCoreCount => (int)cpuInfo.topology.cores;

    public int PhysicalCoreCount => (int)cpuInfo.topology.physicalCores;

    public int NodesPerProcessor => (int)cpuInfo.topology.cpuNodes;

    public int Threads => (int)cpuInfo.topology.logicalCores;

    public bool SMT => (int)cpuInfo.topology.threadsPerCore > 1;

    public int CCDCount => (int)cpuInfo.topology.ccds;

    public int CCXCount => (int)cpuInfo.topology.ccxs;

    public int NumCoresInCCX => (int)cpuInfo.topology.coresPerCcx;

    public string MbVendor
    {
        get; private set;
    }

    public string MbName
    {
        get; private set;
    }

    public string BiosVersion
    {
        get; private set;
    }

    public uint SmuVersion
    {
        get; private set;
    }

    public uint SmuTableVersion
    {
        get; private set;
    }

    public uint PatchLevel => cpuInfo.patchLevel;

    public string GetSmuVersionString() => SmuVersionToString(SmuVersion);

    public string GetCpuIdString() => CpuId.ToString("X8").TrimStart('0');

    private static string SmuVersionToString(uint ver)
    {
        if ((ver & 4278190080U) <= 0U)
        {
            return string.Format("{0}.{1}.{2}", (uint)((int)(ver >> 16) & byte.MaxValue), (uint)((int)(ver >> 8) & byte.MaxValue), (uint)((int)ver & byte.MaxValue));
        }

        return string.Format("{0}.{1}.{2}.{3}", (uint)((int)(ver >> 24) & byte.MaxValue), (uint)((int)(ver >> 16) & byte.MaxValue), (uint)((int)(ver >> 8) & byte.MaxValue), (uint)((int)ver & byte.MaxValue));
    }
}