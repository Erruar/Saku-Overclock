using System.Text;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.Win32.SafeHandles;
using static System.Runtime.InteropServices.UnmanagedType;
using System.Reflection.Emit;
using System.ComponentModel;
using System.Management;
using System.ServiceProcess;

namespace Saku_Overclock.Services;
/*This is a modified processor driver file. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
internal class Cpu : IDisposable
{
    private bool disposedValue;
    private const string InitializationExceptionText = "CPU module initialization failed.";
    public readonly IOModule io = new IOModule();
    private readonly ACPI_MMIO mmio;
    public readonly Cpu.CPUInfo info;
    public readonly SystemInfo systemInfo;
    public readonly SMU smu;
    public readonly PowerTable powerTable;

    public IOModule.LibStatus Statuss
    {
        get;
    }

    public Exception LastError
    {
        get;
    }

    private Cpu.CpuTopology GetCpuTopology(Cpu.Family family, Cpu.CodeName codeName, uint model)
    {
        Cpu.CpuTopology cpuTopology = new Cpu.CpuTopology();
        uint eax;
        uint ebx;
        uint ecx;
        uint edx;
        if (!Opcode.Cpuid(1U, 0U, out eax, out ebx, out ecx, out edx))
            throw new ApplicationException("CPU module initialization failed.");
        cpuTopology.logicalCores = Utils.GetBits(ebx, 16, 8);
        if (!Opcode.Cpuid(2147483678U, 0U, out eax, out ebx, out ecx, out edx))
            throw new ApplicationException("CPU module initialization failed.");
        cpuTopology.threadsPerCore = Utils.GetBits(ebx, 8, 4) + 1U;
        cpuTopology.cpuNodes = (uint)(((int)(ecx >> 8) & 7) + 1);
        cpuTopology.cores = cpuTopology.threadsPerCore != 0U ? cpuTopology.logicalCores / cpuTopology.threadsPerCore : cpuTopology.logicalCores;
        try
        {
            cpuTopology.performanceOfCore = new uint[(int)cpuTopology.cores];
            for (int index = 0; (long)index < (long)cpuTopology.logicalCores; index += (int)cpuTopology.threadsPerCore)
                cpuTopology.performanceOfCore[(long)index / (long)cpuTopology.threadsPerCore] = !Ring0.RdmsrTx(3221291699U, out eax, out edx, GroupAffinity.Single((ushort)0, index)) ? 0U : eax & (uint)byte.MaxValue;
        }
        catch
        {
        }
        uint data1 = 0;
        uint data2 = 0;
        uint data3 = 0;
        uint addr1 = 381464;
        uint addr2 = 381468;
        uint num1 = 568;
        uint num2 = 2;
        int num3;
        switch (family)
        {
            case Cpu.Family.FAMILY_17H:
                if (model != 113U)
                {
                    num3 = model != 49U ? 1 : 0;
                    break;
                }
                goto default;
            case Cpu.Family.FAMILY_19H:
                num1 = 1432U;
                num2 = 1U;
                if (codeName == Cpu.CodeName.Raphael)
                {
                    num1 = 1232U;
                    addr1 += 420U;
                    addr2 += 420U;
                    goto label_17;
                }
                else
                    goto label_17;
            default:
                num3 = 0;
                break;
        }
        if (num3 != 0)
        {
            addr1 += 64U;
            addr2 += 64U;
        }
    label_17:
        if (ReadDwordEx(addr1, ref data1) && ReadDwordEx(addr2, ref data2))
        {
            uint bits = Utils.GetBits(data1, 22, 8);
            uint num4 = Utils.GetBits(data1, 30, 2) | Utils.GetBits(data2, 0, 6) << 2;
            uint addr3 = 805836800U + num1;
            uint num5 = Utils.CountSetBits(bits);
            cpuTopology.ccds = num5 > 0U ? num5 : 1U;
            cpuTopology.ccxs = cpuTopology.ccds * num2;
            cpuTopology.physicalCores = cpuTopology.ccxs * 8U / num2;
            if (ReadDwordEx(addr3, ref data3))
                cpuTopology.coresPerCcx = (8U - Utils.CountSetBits(data3 & (uint)byte.MaxValue)) / num2;
            else
                Console.WriteLine("Could not read core fuse!");
            for (int offset = 0; (long)offset < (long)cpuTopology.ccds; ++offset)
            {
                if (Utils.GetBits(bits, offset, 1) == 1U)
                {
                    if (ReadDwordEx((uint)(offset << 25) + addr3, ref data3))
                        cpuTopology.coreDisableMap |= (uint)(((int)data3 & (int)byte.MaxValue) << offset * 8);
                    else
                        Console.WriteLine(string.Format("Could not read core fuse for CCD{0}!", (object)offset));
                }
            }
        }
        else
            Console.WriteLine("Could not read CCD fuse!");
        return cpuTopology;
    }

    public Cpu()
    {
        Ring0.Open();
        if (!Ring0.IsOpen)
        {
            string report = Ring0.GetReport();
            using (StreamWriter streamWriter = new StreamWriter("WinRing0.txt", true))
                streamWriter.Write(report);
            throw new ApplicationException("Error opening WinRing kernel driver");
        }
        Opcode.Open();
        mmio = new ACPI_MMIO(io);
        info.vendor = GetVendor();
        if (info.vendor != "AuthenticAMD" && info.vendor != "HygonGenuine")
            throw new Exception("Not an AMD CPU");
        uint eax;
        uint ebx;
        uint ecx;
        uint edx;
        if (!Opcode.Cpuid(1U, 0U, out eax, out ebx, out ecx, out edx))
            throw new ApplicationException("CPU module initialization failed.");
        info.cpuid = eax;
        info.family = (Cpu.Family)((int)((eax & 3840U) >> 8) + (int)((eax & 267386880U) >> 20));
        info.baseModel = (eax & 240U) >> 4;
        info.extModel = (eax & 983040U) >> 12;
        info.model = info.baseModel + info.extModel;
        info.stepping = eax & 15U;
        info.cpuName = GetCpuName();
        if (!Opcode.Cpuid(2147483649U, 0U, out eax, out ebx, out ecx, out edx))
            throw new ApplicationException("CPU module initialization failed.");
        info.packageType = (Cpu.PackageType)(ebx >> 28);
        info.codeName = GetCodeName(info);
        smu = GetMaintainedSettings.GetByType(info.codeName);
        smu.Version = GetSmuVersion();
        smu.TableVersion = GetTableVersion();
        try
        {
            info.topology = GetCpuTopology(info.family, info.codeName, info.model);
        }
        catch (Exception ex)
        {
            LastError = ex;
            Statuss = IOModule.LibStatus.PARTIALLY_OK;
        }
        try
        {
            info.patchLevel = GetPatchLevel();
            info.svi2 = GetSVI2Info(info.codeName);
            systemInfo = new SystemInfo(info, smu);
            powerTable = new PowerTable(smu, io, mmio);
            info.aod = new AOD(io);
            if (!SendTestMessage())
                LastError = (Exception)new ApplicationException("SMU is not responding to test message!");
            Statuss = IOModule.LibStatus.OK;
        }
        catch (Exception ex)
        {
            LastError = ex;
            Statuss = IOModule.LibStatus.PARTIALLY_OK;
        }
    }

    public uint MakeCoreMask(uint core = 0, uint ccd = 0, uint ccx = 0)
    {
        uint num1 = info.family == Cpu.Family.FAMILY_19H ? 1U : 2U;
        uint num2 = 8U / num1;
        return (uint)((((int)ccd << 4 | (int)(ccx % num1) & 15) << 4 | (int)(core % num2) & 15) << 20);
    }
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
    public bool ReadDwordEx(uint addr, ref uint data)
    {
        bool flag = false;
        if (Ring0.WaitPciBusMutex(10))
        {
            if (Ring0.WritePciConfig(smu.SMU_PCI_ADDR, smu.SMU_OFFSET_ADDR, addr))
                flag = Ring0.ReadPciConfig(smu.SMU_PCI_ADDR, smu.SMU_OFFSET_DATA, out data);
            Ring0.ReleasePciBusMutex();
        }
        return flag;
    }

    public uint ReadDword(uint addr)
    {
        uint num = 0;
        if (Ring0.WaitPciBusMutex(10))
        {
            Ring0.WritePciConfig(smu.SMU_PCI_ADDR, (uint)(byte)smu.SMU_OFFSET_ADDR, addr);
            Ring0.ReadPciConfig(smu.SMU_PCI_ADDR, (uint)(byte)smu.SMU_OFFSET_DATA, out num);
            Ring0.ReleasePciBusMutex();
        }
        return num;
    }

    public bool WriteDwordEx(uint addr, uint data)
    {
        bool flag = false;
        if (Ring0.WaitPciBusMutex(10))
        {
            if (Ring0.WritePciConfig(smu.SMU_PCI_ADDR, (uint)(byte)smu.SMU_OFFSET_ADDR, addr))
                flag = Ring0.WritePciConfig(smu.SMU_PCI_ADDR, (uint)(byte)smu.SMU_OFFSET_DATA, data);
            Ring0.ReleasePciBusMutex();
        }
        return flag;
    }

    public double GetCoreMulti(int index = 0)
    {
        uint eax;
        return !Ring0.RdmsrTx(3221291667U, out eax, out uint _, GroupAffinity.Single((ushort)0, index)) ? 0.0 : Math.Round((double)(uint)(25 * ((int)eax & (int)byte.MaxValue)) / (12.5 * (double)(eax >> 8 & 63U)) * 4.0, MidpointRounding.ToEven) / 4.0;
    }

    public bool Cpuid(uint index, ref uint eax, ref uint ebx, ref uint ecx, ref uint edx) => Opcode.Cpuid(index, 0U, out eax, out ebx, out ecx, out edx);

    public bool ReadMsr(uint index, ref uint eax, ref uint edx) => Ring0.Rdmsr(index, out eax, out edx);

    public bool ReadMsrTx(uint index, ref uint eax, ref uint edx, int i)
    {
        GroupAffinity affinity = GroupAffinity.Single((ushort)0, i);
        return Ring0.RdmsrTx(index, out eax, out edx, affinity);
    }

    [Obsolete("Obsolete")]
    public bool WriteMsr(uint msr, uint eax, uint edx)
    {
        var result = true;
        for (var i = 0; i < info.topology.logicalCores; i++)
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
            result = Ring0.WrmsrTx(msr, eax, edx, GroupAffinity.Single(0, i));
        }
        return result;
    }
    public void WriteIoPort(uint port, byte value) => Ring0.WriteIoPort(port, value);

    public byte ReadIoPort(uint port) => Ring0.ReadIoPort(port);

    public bool ReadPciConfig(uint pciAddress, uint regAddress, ref uint value) => Ring0.ReadPciConfig(pciAddress, regAddress, out value);

    public uint GetPciAddress(byte bus, byte device, byte function) => Ring0.GetPciAddress(bus, device, function);

    public Cpu.CodeName GetCodeName(Cpu.CPUInfo cpuInfo)
    {
        Cpu.CodeName codeName = Cpu.CodeName.Unsupported;
        if (cpuInfo.family == Cpu.Family.FAMILY_15H)
        {
            if (cpuInfo.model == 101U)
                codeName = Cpu.CodeName.BristolRidge;
        }
        else if (cpuInfo.family == Cpu.Family.FAMILY_17H)
        {
            switch (cpuInfo.model)
            {
                case 1:
                    codeName = cpuInfo.packageType != Cpu.PackageType.SP3 ? (cpuInfo.packageType != Cpu.PackageType.TRX ? Cpu.CodeName.SummitRidge : Cpu.CodeName.Whitehaven) : Cpu.CodeName.Naples;
                    break;
                case 8:
                    codeName = cpuInfo.packageType != Cpu.PackageType.SP3 && cpuInfo.packageType != Cpu.PackageType.TRX ? Cpu.CodeName.PinnacleRidge : Cpu.CodeName.Colfax;
                    break;
                case 17:
                    codeName = Cpu.CodeName.RavenRidge;
                    break;
                case 24:
                    codeName = !info.cpuName.Contains("3000G") ? Cpu.CodeName.Picasso : Cpu.CodeName.Dali;
                    break;
                case 32:
                    codeName = Cpu.CodeName.Dali;
                    break;
                case 49:
                    codeName = cpuInfo.packageType != Cpu.PackageType.TRX ? Cpu.CodeName.Rome : Cpu.CodeName.CastlePeak;
                    break;
                case 80:
                    codeName = Cpu.CodeName.FireFlight;
                    break;
                case 96:
                    codeName = Cpu.CodeName.Renoir;
                    break;
                case 104:
                    codeName = Cpu.CodeName.Lucienne;
                    break;
                case 113:
                    codeName = Cpu.CodeName.Matisse;
                    break;
                case 144:
                    codeName = Cpu.CodeName.VanGogh;
                    break;
                default:
                    codeName = Cpu.CodeName.Unsupported;
                    break;
            }
        }
        else if (cpuInfo.family == Cpu.Family.FAMILY_19H)
        {
            switch (cpuInfo.model)
            {
                case 1:
                    codeName = Cpu.CodeName.Milan;
                    break;
                case 8:
                    codeName = Cpu.CodeName.Chagall;
                    break;
                case 17:
                    codeName = Cpu.CodeName.Genoa;
                    break;
                case 24:
                    codeName = Cpu.CodeName.StormPeak;
                    break;
                case 33:
                    codeName = Cpu.CodeName.Vermeer;
                    break;
                case 68:
                    codeName = Cpu.CodeName.Rembrandt;
                    break;
                case 80:
                    codeName = Cpu.CodeName.Cezanne;
                    break;
                case 97:
                    codeName = Cpu.CodeName.Raphael;
                    break;
                case 116:
                case 120:
                    codeName = Cpu.CodeName.Phoenix;
                    break;
                case 160:
                    codeName = Cpu.CodeName.Mendocino;
                    break;
                default:
                    codeName = Cpu.CodeName.Unsupported;
                    break;
            }
        }
        return codeName;
    }

    public Cpu.SVI2 GetSVI2Info(Cpu.CodeName codeName)
    {
        Cpu.SVI2 svI2Info = new Cpu.SVI2();
        switch (codeName)
        {
            case Cpu.CodeName.BristolRidge:
                return svI2Info;
            case Cpu.CodeName.SummitRidge:
            case Cpu.CodeName.RavenRidge:
            case Cpu.CodeName.PinnacleRidge:
            case Cpu.CodeName.FireFlight:
            case Cpu.CodeName.Dali:
                svI2Info.coreAddress = 368652U;
                svI2Info.socAddress = 368656U;
                goto case Cpu.CodeName.BristolRidge;
            case Cpu.CodeName.Whitehaven:
            case Cpu.CodeName.Naples:
            case Cpu.CodeName.Colfax:
                svI2Info.coreAddress = 368656U;
                svI2Info.socAddress = 368652U;
                goto case Cpu.CodeName.BristolRidge;
            case Cpu.CodeName.Picasso:
                if ((smu.Version & 4278190080U) > 0U)
                {
                    svI2Info.coreAddress = 368652U;
                    svI2Info.socAddress = 368656U;
                    goto case Cpu.CodeName.BristolRidge;
                }
                else
                {
                    svI2Info.coreAddress = 368656U;
                    svI2Info.socAddress = 368652U;
                    goto case Cpu.CodeName.BristolRidge;
                }
            case Cpu.CodeName.Matisse:
                svI2Info.coreAddress = 368656U;
                svI2Info.socAddress = 368652U;
                goto case Cpu.CodeName.BristolRidge;
            case Cpu.CodeName.CastlePeak:
            case Cpu.CodeName.Rome:
                svI2Info.coreAddress = 368660U;
                svI2Info.socAddress = 368656U;
                goto case Cpu.CodeName.BristolRidge;
            case Cpu.CodeName.Renoir:
            case Cpu.CodeName.VanGogh:
            case Cpu.CodeName.Cezanne:
            case Cpu.CodeName.Rembrandt:
            case Cpu.CodeName.Lucienne:
            case Cpu.CodeName.Phoenix:
            case Cpu.CodeName.Mendocino:
                svI2Info.coreAddress = 454712U;
                svI2Info.socAddress = 454716U;
                goto case Cpu.CodeName.BristolRidge;
            case Cpu.CodeName.Vermeer:
            case Cpu.CodeName.Raphael:
                svI2Info.coreAddress = 368656U;
                svI2Info.socAddress = 368652U;
                goto case Cpu.CodeName.BristolRidge;
            case Cpu.CodeName.Chagall:
            case Cpu.CodeName.Milan:
                svI2Info.coreAddress = 368660U;
                svI2Info.socAddress = 368656U;
                goto case Cpu.CodeName.BristolRidge;
            default:
                svI2Info.coreAddress = 368652U;
                svI2Info.socAddress = 368656U;
                goto case Cpu.CodeName.BristolRidge;
        }
    }

    public string GetVendor()
    {
        uint eax;
        uint ebx;
        uint ecx;
        uint edx;
        return Opcode.Cpuid(0U, 0U, out eax, out ebx, out ecx, out edx) ? Utils.IntToStr(ebx) + Utils.IntToStr(edx) + Utils.IntToStr(ecx) : "";
    }

    public string GetCpuName()
    {
        string str = "";
        uint eax;
        uint ebx;
        uint ecx;
        uint edx;
        if (Opcode.Cpuid(2147483650U, 0U, out eax, out ebx, out ecx, out edx))
            str = str + Utils.IntToStr(eax) + Utils.IntToStr(ebx) + Utils.IntToStr(ecx) + Utils.IntToStr(edx);
        if (Opcode.Cpuid(2147483651U, 0U, out eax, out ebx, out ecx, out edx))
            str = str + Utils.IntToStr(eax) + Utils.IntToStr(ebx) + Utils.IntToStr(ecx) + Utils.IntToStr(edx);
        if (Opcode.Cpuid(2147483652U, 0U, out eax, out ebx, out ecx, out edx))
            str = str + Utils.IntToStr(eax) + Utils.IntToStr(ebx) + Utils.IntToStr(ecx) + Utils.IntToStr(edx);
        return str.Trim();
    }

    public uint GetPatchLevel()
    {
        uint eax;
        return Ring0.Rdmsr(139U, out eax, out uint _) ? eax : 0U;
    }

    public bool GetOcMode()
    {
        uint eax;
        return info.codeName == Cpu.CodeName.SummitRidge ? Ring0.Rdmsr(3221291107U, out eax, out uint _) && Convert.ToBoolean(eax >> 1 & 1U) : info.family != Cpu.Family.FAMILY_15H && object.Equals((object)GetPBOScalar(), (object)0.0f);
    }

    public float GetPBOScalar()
    {
        var getPboScalar = new GetPBOScalar(smu);
        getPboScalar.Execute();
        return getPboScalar.Scalar;
    }

    public bool SendTestMessage(uint arg = 1, Mailbox mbox = null)
    {
        var sendTestMessage = new SendTestMessage(smu, mbox);
        return sendTestMessage.Execute(arg).Success && sendTestMessage.IsSumCorrect;
    }

    public uint GetSmuVersion() => new GetSmuVersion(smu).Execute().args[0];

    public double? GetBclk() => mmio.GetBclk();

    public bool SetBclk(double blck) => mmio.SetBclk(blck);

    public SMU.Status TransferTableToDram() => new TransferTableToDram(smu).Execute().status;

    public uint GetTableVersion() => new GetTableVersion(smu).Execute().args[0];

    public uint GetDramBaseAddress() => new GetDramAddress(smu).Execute().args[0];

    public long GetDramBaseAddress64()
    {
        CmdResult cmdResult = new GetDramAddress(smu).Execute();
        return (long)cmdResult.args[1] << 32 | (long)cmdResult.args[0];
    }

    public bool GetLN2Mode() => new GetLN2Mode(smu).Execute().args[0] == 1U;

    public SMU.Status SetPPTLimit(uint arg = 0) => new SetSmuLimit(smu).Execute(smu.Rsmu.SMU_MSG_SetPPTLimit, arg).status;

    public SMU.Status SetEDCVDDLimit(uint arg = 0) => new SetSmuLimit(smu).Execute(smu.Rsmu.SMU_MSG_SetEDCVDDLimit, arg).status;

    public SMU.Status SetEDCSOCLimit(uint arg = 0) => new SetSmuLimit(smu).Execute(smu.Rsmu.SMU_MSG_SetEDCSOCLimit, arg).status;

    public SMU.Status SetTDCVDDLimit(uint arg = 0) => new SetSmuLimit(smu).Execute(smu.Rsmu.SMU_MSG_SetTDCVDDLimit, arg).status;

    public SMU.Status SetTDCSOCLimit(uint arg = 0) => new SetSmuLimit(smu).Execute(smu.Rsmu.SMU_MSG_SetTDCSOCLimit, arg).status;

    public SMU.Status SetOverclockCpuVid(byte arg) => new SetOverclockCpuVid(smu).Execute(arg).status;

    public SMU.Status EnableOcMode() => new SetOcMode(smu).Execute(true).status;

    public SMU.Status DisableOcMode() => new SetOcMode(smu).Execute(false).status;

    public SMU.Status SetPBOScalar(uint scalar) => new SetPBOScalar(smu).Execute(scalar).status;

    public SMU.Status RefreshPowerTable() => powerTable == null ? SMU.Status.FAILED : powerTable.Refresh();

    public int? GetPsmMarginSingleCore(uint coreMask)
    {
        CmdResult cmdResult = new GetPsmMarginSingleCore(smu).Execute(coreMask);
        return cmdResult.Success ? new int?((int)cmdResult.args[0]) : new int?();
    }

    public int? GetPsmMarginSingleCore(uint core, uint ccd, uint ccx) => GetPsmMarginSingleCore(MakeCoreMask(core, ccd, ccx));

    public bool SetPsmMarginAllCores(int margin) => new SetPsmMarginAllCores(smu).Execute(margin).Success;

    public bool SetPsmMarginSingleCore(uint coreMask, int margin) => new SetPsmMarginSingleCore(smu).Execute(coreMask, margin).Success;

    public bool SetPsmMarginSingleCore(uint core, uint ccd, uint ccx, int margin) => SetPsmMarginSingleCore(MakeCoreMask(core, ccd, ccx), margin);

    public bool SetFrequencyAllCore(uint frequency) => new SetFrequencyAllCore(smu).Execute(frequency).Success;

    public bool SetFrequencySingleCore(uint coreMask, uint frequency) => new SetFrequencySingleCore(smu).Execute(coreMask, frequency).Success;

    public bool SetFrequencySingleCore(uint core, uint ccd, uint ccx, uint frequency) => SetFrequencySingleCore(MakeCoreMask(core, ccd, ccx), frequency);

    private bool SetFrequencyMultipleCores(uint mask, uint frequency, int count)
    {
        for (uint newVal = 0; (long)newVal < (long)count; ++newVal)
        {
            mask = Utils.SetBits(mask, 20, 2, newVal);
            if (!SetFrequencySingleCore(mask, frequency))
                return false;
        }
        return true;
    }

    public bool SetFrequencyCCX(uint mask, uint frequency) => SetFrequencyMultipleCores(mask, frequency, 8);

    public bool SetFrequencyCCD(uint mask, uint frequency)
    {
        bool flag = true;
        for (uint newVal = 0; (long)newVal < (long)(systemInfo.CCXCount / systemInfo.CCDCount); ++newVal)
        {
            mask = Utils.SetBits(mask, 24, 1, newVal);
            flag = SetFrequencyCCX(mask, frequency);
        }
        return flag;
    }

    public bool IsProchotEnabled() => ((int)ReadDword(366596U) & 1) == 1;

    public float? GetCpuTemperature()
    {
        uint data = 0;
        if (!ReadDwordEx(366592U, ref data))
            return new float?();
        float num1 = 0.0f;
        if (info.cpuName.Contains("2700X"))
            num1 = -10f;
        else if (info.cpuName.Contains("1600X") || info.cpuName.Contains("1700X") || info.cpuName.Contains("1800X"))
            num1 = -20f;
        else if (info.cpuName.Contains("Threadripper 19") || info.cpuName.Contains("Threadripper 29"))
            num1 = -27f;
        float num2 = (float)(data >> 21) * 0.125f + num1;
        if ((data & 524288U) > 0U)
            num2 -= 49f;
        return new float?(num2);
    }

    public float? GetSingleCcdTemperature(uint ccd)
    {
        uint data = 0;
        if (!ReadDwordEx((uint)(366932 + (int)ccd * 4), ref data))
            return new float?();
        float num = (float)((double)(data & 4095U) * 0.125 - 305.0);
        return (double)num > 0.0 && (double)num < 125.0 ? new float?(num) : new float?(0.0f);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposedValue)
            return;
        if (disposing)
        {
            io.Dispose();
            Ring0.Close();
            Opcode.Close();
        }
        disposedValue = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize((object)this);
    }

    public enum Family
    {
        UNSUPPORTED = 0,
        FAMILY_15H = 21, // 0x00000015
        FAMILY_17H = 23, // 0x00000017
        FAMILY_18H = 24, // 0x00000018
        FAMILY_19H = 25, // 0x00000019
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
        StormPeak,
    }

    public enum PackageType
    {
        FPX = 0,
        AM4 = 2,
        SP3 = 4,
        TRX = 7,
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
        public Cpu.Family family;
        public Cpu.CodeName codeName;
        public string cpuName;
        public string vendor;
        public Cpu.PackageType packageType;
        public uint baseModel;
        public uint extModel;
        public uint model;
        public uint patchLevel;
        public uint stepping;
        public Cpu.CpuTopology topology;
        public Cpu.SVI2 svi2;
        public AOD aod;
    }
}
/*internal class Cpu : IDisposable
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
        StormPeak,
        Whitehaven
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
        //public readonly SMU smu;

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
    }

    private bool disposedValue;

    public const string InitializationExceptionText = "CPU module initialization failed.";

    public CpuInfo info;
    //public readonly SMU smu;


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
    public bool ReadDwordEx(uint addr, ref uint data)
    {
        bool flag = false;
        if (Ring0.WaitPciBusMutex(10))
        {
            if (Ring0.WritePciConfig(this.SMU_PCI_ADDR, this.SMU_OFFSET_ADDR, addr))
                flag = Ring0.ReadPciConfig(this.SMU_PCI_ADDR, this.SMU_OFFSET_DATA, out data);
            Ring0.ReleasePciBusMutex();
        }
        return flag;
    }
    [Obsolete("Obsolete")]
    public uint ReadDword(uint addr)
    {
        uint num = 0;
        if (Ring0.WaitPciBusMutex(10))
        {
            Ring0.WritePciConfig(this.SMU_PCI_ADDR, (uint)(byte)this.SMU_OFFSET_ADDR, addr);
            Ring0.ReadPciConfig(this.SMU_PCI_ADDR, (uint)(byte)this.SMU_OFFSET_DATA, out num);
            Ring0.ReleasePciBusMutex();
        }
        return num;
    }
    [Obsolete("Obsolete")]
    public bool WriteDwordEx(uint addr, uint data)
    {
        bool flag = false;
        if (Ring0.WaitPciBusMutex(10))
        {
            if (Ring0.WritePciConfig(this.SMU_PCI_ADDR, (uint)(byte)this.SMU_OFFSET_ADDR, addr))
                flag = Ring0.WritePciConfig(this.SMU_PCI_ADDR, (uint)(byte)this.SMU_OFFSET_DATA, data);
            Ring0.ReleasePciBusMutex();
        }
        return flag;
    }
    public Cpu.CodeName GetCodeName(Cpu.CpuInfo cpuInfo)
    {
        Cpu.CodeName codeName = Cpu.CodeName.Unsupported;
        if (cpuInfo.family == Cpu.Family.FAMILY_15H)
        {
            if (cpuInfo.model == 101U)
                codeName = Cpu.CodeName.BristolRidge;
        }
        else if (cpuInfo.family == Cpu.Family.FAMILY_17H)
        {
            switch (cpuInfo.model)
            {
                case 1:
                    codeName = cpuInfo.packageType != Cpu.PackageType.SP3 ? (cpuInfo.packageType != Cpu.PackageType.TRX ? Cpu.CodeName.SummitRidge : Cpu.CodeName.Whitehaven) : Cpu.CodeName.Naples;
                    break;
                case 8:
                    codeName = cpuInfo.packageType != Cpu.PackageType.SP3 && cpuInfo.packageType != Cpu.PackageType.TRX ? Cpu.CodeName.PinnacleRidge : Cpu.CodeName.Colfax;
                    break;
                case 17:
                    codeName = Cpu.CodeName.RavenRidge;
                    break;
                case 24:
                    codeName = !this.info.cpuName.Contains("3000G") ? Cpu.CodeName.Picasso : Cpu.CodeName.Dali;
                    break;
                case 32:
                    codeName = Cpu.CodeName.Dali;
                    break;
                case 49:
                    codeName = cpuInfo.packageType != Cpu.PackageType.TRX ? Cpu.CodeName.Rome : Cpu.CodeName.CastlePeak;
                    break;
                case 80:
                    codeName = Cpu.CodeName.FireFlight;
                    break;
                case 96:
                    codeName = Cpu.CodeName.Renoir;
                    break;
                case 104:
                    codeName = Cpu.CodeName.Lucienne;
                    break;
                case 113:
                    codeName = Cpu.CodeName.Matisse;
                    break;
                case 144:
                    codeName = Cpu.CodeName.VanGogh;
                    break;
                default:
                    codeName = Cpu.CodeName.Unsupported;
                    break;
            }
        }
        else if (cpuInfo.family == Cpu.Family.FAMILY_19H)
        {
            switch (cpuInfo.model)
            {
                case 1:
                    codeName = Cpu.CodeName.Milan;
                    break;
                case 8:
                    codeName = Cpu.CodeName.Chagall;
                    break;
                case 17:
                    codeName = Cpu.CodeName.Genoa;
                    break;
                case 24:
                    codeName = Cpu.CodeName.StormPeak;
                    break;
                case 33:
                    codeName = Cpu.CodeName.Vermeer;
                    break;
                case 68:
                    codeName = Cpu.CodeName.Rembrandt;
                    break;
                case 80:
                    codeName = Cpu.CodeName.Cezanne;
                    break;
                case 97:
                    codeName = Cpu.CodeName.Raphael;
                    break;
                case 116:
                case 120:
                    codeName = Cpu.CodeName.Phoenix;
                    break;
                case 160:
                    codeName = Cpu.CodeName.Mendocino;
                    break;
                default:
                    codeName = Cpu.CodeName.Unsupported;
                    break;
            }
        }
        return codeName;
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
    //SMU??????
    public uint Version
    {
        get; set;
    }

    public uint TableVersion
    {
        get; set;
    }

    public Cpu.SmuType SMU_TYPE
    {
        get; protected set;
    }

    public uint SMU_PCI_ADDR
    {
        get; protected set;
    }

    public uint SMU_OFFSET_ADDR
    {
        get; protected set;
    }

    public uint SMU_OFFSET_DATA
    {
        get; protected set;
    }

    public RSMUMailbox Rsmu
    {
        get; protected set;
    }

    public MP1Mailbox Mp1Smu
    {
        get; protected set;
    }

    public HSMPMailbox Hsmp
    {
        get; protected set;
    }
    [Obsolete]
    private bool SmuWriteReg(uint addr, uint data) => Ring0.WritePciConfig(SMU_PCI_ADDR, SMU_OFFSET_ADDR, addr) && Ring0.WritePciConfig(SMU_PCI_ADDR, SMU_OFFSET_DATA, data);

    [Obsolete]
    private bool SmuReadReg(uint addr, ref uint data) => Ring0.WritePciConfig(SMU_PCI_ADDR, SMU_OFFSET_ADDR, addr) && Ring0.ReadPciConfig(SMU_PCI_ADDR, SMU_OFFSET_DATA, out data);

    [Obsolete]
    private bool SmuWaitDone(Mailbox mailbox)
    {
        ushort num = 8192;
        uint data = 0;
        do
            ;
        while ((!SmuReadReg(mailbox.SMU_ADDR_RSP, ref data) || data == 0U) && --num > (ushort)0);
        return num != (ushort)0 && data > 0U;
    }

    [Obsolete]
    public Status SendSmuCommand(Mailbox mailbox, uint msg, ref uint[] args)
    {
        uint maxValue = (uint)byte.MaxValue;
        if (msg == 0U || mailbox == null || mailbox.SMU_ADDR_MSG == 0U || mailbox.SMU_ADDR_ARG == 0U || mailbox.SMU_ADDR_RSP == 0U)
            return (Status)Cpu.Status.UNKNOWN_CMD;
        if (Ring0.WaitPciBusMutex(10))
        {
            if (!this.SmuWaitDone(mailbox))
            {
                Ring0.ReleasePciBusMutex();
                return (Status)Cpu.Status.FAILED;
            }
            this.SmuWriteReg(mailbox.SMU_ADDR_RSP, 0U);
            uint[] numArray = Utils.MakeCmdArgs(args, mailbox.MAX_ARGS);
            for (int index = 0; index < numArray.Length; ++index)
                this.SmuWriteReg(mailbox.SMU_ADDR_ARG + (uint)(index * 4), numArray[index]);
            this.SmuWriteReg(mailbox.SMU_ADDR_MSG, msg);
            if (!this.SmuWaitDone(mailbox))
            {
                Ring0.ReleasePciBusMutex();
                return (Status)Cpu.Status.FAILED;
            }
            this.SmuReadReg(mailbox.SMU_ADDR_RSP, ref maxValue);
            if ((byte)maxValue == (byte)1)
            {
                for (int index = 0; index < args.Length; ++index)
                    this.SmuReadReg(mailbox.SMU_ADDR_ARG + (uint)(index * 4), ref args[index]);
            }
            Ring0.ReleasePciBusMutex();
        }
        return (Status)maxValue;
    }

    [Obsolete]
    public void SendSmuCommandNV(Mailbox mailbox, uint msg, ref uint[] args)
    {
        Ring0.Close();
        Ring0.Open();
        var maxValue = (uint)byte.MaxValue;
        if (msg == 0U || mailbox == null || mailbox.SMU_ADDR_MSG == 0U || mailbox.SMU_ADDR_ARG == 0U || mailbox.SMU_ADDR_RSP == 0U)
        {
            App.MainWindow.ShowMessageDialogAsync("Cpu.Status.UNKNOWN_CMD","Status");
        }

        if (Ring0.WaitPciBusMutex(10))
            {
            if (!SmuWaitDone(mailbox: mailbox!))
            {
                Ring0.ReleasePciBusMutex();
                App.MainWindow.ShowMessageDialogAsync("Cpu.Status.FAILED", "Status");
            }
            SmuWriteReg(mailbox!.SMU_ADDR_RSP, 0U);
            var numArray = Utils.MakeCmdArgs(args, mailbox.MAX_ARGS);
            for (var index = 0; index < numArray.Length; ++index)
            {
                SmuWriteReg(mailbox.SMU_ADDR_ARG + (uint)(index * 4), numArray[index]);
            }

            SmuWriteReg(mailbox.SMU_ADDR_MSG, msg);
                if (!SmuWaitDone(mailbox))
                {
                    Ring0.ReleasePciBusMutex();
                App.MainWindow.ShowMessageDialogAsync("Cpu.Status.FAILED","Status");
                }
                SmuReadReg(mailbox.SMU_ADDR_RSP, ref maxValue);
                if ((byte)maxValue == 1)
                {
                for (var index = 0; index < args.Length; ++index)
                {
                    SmuReadReg(mailbox.SMU_ADDR_ARG + (uint)(index * 4), ref args[index]);
                }
            }
                Ring0.ReleasePciBusMutex();
            }
        App.MainWindow.ShowMessageDialogAsync(maxValue.ToString() + " " + mailbox.ToString(), "Status");
    }

    [Obsolete("SendSmuCommand with one argument is deprecated, please use SendSmuCommand with full 6 args")]
    public bool SendSmuCommand(Mailbox mailbox, uint msg, uint arg)
    {
        uint[] args = Utils.MakeCmdArgs(arg, mailbox.MAX_ARGS);
        return SendSmuCommand(mailbox, msg, ref args) == (Status)Cpu.Status.OK;
    }
    [Obsolete]
    public Cpu.Status SendMp1Command(uint msg, ref uint[] args) => (Cpu.Status)SendSmuCommand((Mailbox)Mp1Smu, msg, ref args);
    [Obsolete]
    public Cpu.Status SendRsmuCommand(uint msg, ref uint[] args) => (Cpu.Status)SendSmuCommand((Mailbox)Rsmu, msg, ref args);
    [Obsolete]
    public Cpu.Status SendHsmpCommand(uint msg, ref uint[] args) => Hsmp.IsSupported && msg <= Hsmp.HighestSupportedFunction ? (Cpu.Status)SendSmuCommand((Mailbox)Hsmp, msg, ref args) : Cpu.Status.UNKNOWN_CMD;

    public enum MailboxType
    {
        UNSUPPORTED,
        RSMU,
        MP1,
        HSMP,
    }

    public enum SmuType
    {
        TYPE_CPU0 = 0,
        TYPE_CPU1 = 1,
        TYPE_CPU2 = 2,
        TYPE_CPU3 = 3,
        TYPE_CPU4 = 4,
        TYPE_CPU9 = 9,
        TYPE_APU0 = 16, // 0x00000010
        TYPE_APU1 = 17, // 0x00000011
        TYPE_APU2 = 18, // 0x00000012
        TYPE_UNSUPPORTED = 255, // 0x000000FF
    }

    public enum Status : byte
    {
        OK = 1,
        CMD_REJECTED_BUSY = 252, // 0xFC
        CMD_REJECTED_PREREQ = 253, // 0xFD
        UNKNOWN_CMD = 254, // 0xFE
        FAILED = 255, // 0xFF
    }

}*/
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
public partial class SMU
{
    protected internal SMU()
    {
        Version = 0U;
        SMU_TYPE = SMU.SmuType.TYPE_UNSUPPORTED;
        SMU_PCI_ADDR = 0U;
        SMU_OFFSET_ADDR = 96U;
        SMU_OFFSET_DATA = 100U;
        Rsmu = new RSMUMailbox();
        Mp1Smu = new MP1Mailbox();
        Hsmp = new HSMPMailbox();
    }

    public uint Version
    {
        get; set;
    }

    public uint TableVersion
    {
        get; set;
    }

    public SMU.SmuType SMU_TYPE
    {
        get; protected set;
    }

    public uint SMU_PCI_ADDR
    {
        get; protected set;
    }

    public uint SMU_OFFSET_ADDR
    {
        get; protected set;
    }

    public uint SMU_OFFSET_DATA
    {
        get; protected set;
    }

    public RSMUMailbox Rsmu
    {
        get; protected set;
    }

    public MP1Mailbox Mp1Smu
    {
        get; protected set;
    }

    public HSMPMailbox Hsmp
    {
        get; protected set;
    }
    [Obsolete]
    private bool SmuWriteReg(uint addr, uint data) => Ring0.WritePciConfig(SMU_PCI_ADDR, SMU_OFFSET_ADDR, addr) && Ring0.WritePciConfig(SMU_PCI_ADDR, SMU_OFFSET_DATA, data);

    [Obsolete]
    private bool SmuReadReg(uint addr, ref uint data) => Ring0.WritePciConfig(SMU_PCI_ADDR, SMU_OFFSET_ADDR, addr) && Ring0.ReadPciConfig(SMU_PCI_ADDR, SMU_OFFSET_DATA, out data);

    [Obsolete]
    private bool SmuWaitDone(Mailbox mailbox)
    {
        ushort num = 8192;
        uint data = 0;
        do
            ;
        while ((!SmuReadReg(mailbox.SMU_ADDR_RSP, ref data) || data == 0U) && --num > (ushort)0);
        return num != (ushort)0 && data > 0U;
    }

    [Obsolete]
    public Status SendSmuCommand(Mailbox mailbox, uint msg, ref uint[] args)
    {
        uint maxValue = (uint)byte.MaxValue;
        if (msg == 0U || mailbox == null || mailbox.SMU_ADDR_MSG == 0U || mailbox.SMU_ADDR_ARG == 0U || mailbox.SMU_ADDR_RSP == 0U)
            return SMU.Status.UNKNOWN_CMD;
        if (Ring0.WaitPciBusMutex(10))
        {
            if (!SmuWaitDone(mailbox))
            {
                Ring0.ReleasePciBusMutex();
                return SMU.Status.FAILED;
            }
            SmuWriteReg(mailbox.SMU_ADDR_RSP, 0U);
            uint[] numArray = Utils.MakeCmdArgs(args, mailbox.MAX_ARGS);
            for (int index = 0; index < numArray.Length; ++index)
                SmuWriteReg(mailbox.SMU_ADDR_ARG + (uint)(index * 4), numArray[index]);
            SmuWriteReg(mailbox.SMU_ADDR_MSG, msg);
            if (!SmuWaitDone(mailbox))
            {
                Ring0.ReleasePciBusMutex();
                return SMU.Status.FAILED;
            }
            SmuReadReg(mailbox.SMU_ADDR_RSP, ref maxValue);
            if ((byte)maxValue == (byte)1)
            {
                for (int index = 0; index < args.Length; ++index)
                    SmuReadReg(mailbox.SMU_ADDR_ARG + (uint)(index * 4), ref args[index]);
            }
            Ring0.ReleasePciBusMutex();
        }
        return (SMU.Status)maxValue;
    }

    [Obsolete]
    public void SendSmuCommandNV(Mailbox mailbox, uint msg, ref uint[] args)
    {
        uint maxValue = (uint)byte.MaxValue;
        if (msg == 0U || mailbox == null || mailbox.SMU_ADDR_MSG == 0U || mailbox.SMU_ADDR_ARG == 0U || mailbox.SMU_ADDR_RSP == 0U)
            //status = SMU.Status.UNKNOWN_CMD;
        if (Ring0.WaitPciBusMutex(10))
        {
            if (!SmuWaitDone(mailbox: mailbox))
            {
                Ring0.ReleasePciBusMutex();
               // status = SMU.Status.FAILED;
            }
            SmuWriteReg(mailbox.SMU_ADDR_RSP, 0U);
            uint[] numArray = Utils.MakeCmdArgs(args, mailbox.MAX_ARGS);
            for (int index = 0; index < numArray.Length; ++index)
                SmuWriteReg(mailbox.SMU_ADDR_ARG + (uint)(index * 4), numArray[index]);
            SmuWriteReg(mailbox.SMU_ADDR_MSG, msg);
            if (!SmuWaitDone(mailbox))
            {
                Ring0.ReleasePciBusMutex();
              //  status = SMU.Status.FAILED;
            }
            SmuReadReg(mailbox.SMU_ADDR_RSP, ref maxValue);
            if ((byte)maxValue == (byte)1)
            {
                for (int index = 0; index < args.Length; ++index)
                    SmuReadReg(mailbox.SMU_ADDR_ARG + (uint)(index * 4), ref args[index]);
            }
            Ring0.ReleasePciBusMutex();
        }
       // status = (SMU.Status)maxValue;
    }

    [Obsolete("SendSmuCommand with one argument is deprecated, please use SendSmuCommand with full 6 args")]
    public bool SendSmuCommand(Mailbox mailbox, uint msg, uint arg)
    {
        uint[] args = Utils.MakeCmdArgs(arg, mailbox.MAX_ARGS);
        return SendSmuCommand(mailbox, msg, ref args) == SMU.Status.OK;
    }
    [Obsolete]
    public SMU.Status SendMp1Command(uint msg, ref uint[] args) => SendSmuCommand((Mailbox)Mp1Smu, msg, ref args);
    [Obsolete]
    public SMU.Status SendRsmuCommand(uint msg, ref uint[] args) => SendSmuCommand((Mailbox)Rsmu, msg, ref args);
    [Obsolete]
    public SMU.Status SendHsmpCommand(uint msg, ref uint[] args) => Hsmp.IsSupported && msg <= Hsmp.HighestSupportedFunction ? SendSmuCommand((Mailbox)Hsmp, msg, ref args) : SMU.Status.UNKNOWN_CMD;

    public enum MailboxType
    {
        UNSUPPORTED,
        RSMU,
        MP1,
        HSMP,
    }

    public enum SmuType
    {
        TYPE_CPU0 = 0,
        TYPE_CPU1 = 1,
        TYPE_CPU2 = 2,
        TYPE_CPU3 = 3,
        TYPE_CPU4 = 4,
        TYPE_CPU9 = 9,
        TYPE_APU0 = 16, // 0x00000010
        TYPE_APU1 = 17, // 0x00000011
        TYPE_APU2 = 18, // 0x00000012
        TYPE_UNSUPPORTED = 255, // 0x000000FF
    }

    public enum Status : byte
    {
        OK = 1,
        CMD_REJECTED_BUSY = 252, // 0xFC
        CMD_REJECTED_PREREQ = 253, // 0xFD
        UNKNOWN_CMD = 254, // 0xFE
        FAILED = 255, // 0xFF
    }
}
public class Mailbox
{
    public Mailbox(int maxArgs = 6)
    {
        MAX_ARGS = maxArgs;
    }

    public int MAX_ARGS
    {
        get; protected set;
    }

    public uint SMU_ADDR_MSG { get; set; } = 0;

    public uint SMU_ADDR_RSP { get; set; } = 0;

    public uint SMU_ADDR_ARG { get; set; } = 0;

    public uint SMU_MSG_TestMessage { get; } = 1;

    public uint SMU_MSG_GetSmuVersion { get; } = 2;
}
public static class Utils
{
    public static bool Is64Bit => OpenHardwareMonitor.Hardware.OperatingSystem.Is64BitOperatingSystem;

    public static uint SetBits(uint val, int offset, int n, uint newVal) => (uint)((int)val & ~((1 << n) - 1 << offset) | (int)newVal << offset);

    public static uint GetBits(uint val, int offset, int n) => val >> offset & (uint)~(-1 << n);

    public static uint CountSetBits(uint v)
    {
        uint num = 0;
        for (; v > 0U; v >>= 1)
        {
            if (((int)v & 1) == 1)
                ++num;
        }
        return num;
    }

    public static string GetStringPart(uint val) => val != 0U ? Convert.ToChar(val).ToString() : "";

    public static string IntToStr(uint val)
    {
        uint val1 = val & (uint)byte.MaxValue;
        uint val2 = val >> 8 & (uint)byte.MaxValue;
        uint val3 = val >> 16 & (uint)byte.MaxValue;
        uint val4 = val >> 24 & (uint)byte.MaxValue;
        return Utils.GetStringPart(val1) + Utils.GetStringPart(val2) + Utils.GetStringPart(val3) + Utils.GetStringPart(val4);
    }

    public static double VidToVoltage(uint vid) => 1.55 - (double)vid * (1.0 / 160.0);

    public static double VidToVoltageSVI3(uint vid) => 0.245 + (double)vid * 0.005;

    private static bool CheckAllZero<T>(ref T[] typedArray)
    {
        if (typedArray == null)
            return true;
        foreach (T obj in typedArray)
        {
            if (Convert.ToUInt32((object)obj) > 0U)
                return false;
        }
        return true;
    }

    public static bool AllZero(byte[] arr) => Utils.CheckAllZero<byte>(ref arr);

    public static bool AllZero(int[] arr) => Utils.CheckAllZero<int>(ref arr);

    public static bool AllZero(uint[] arr) => Utils.CheckAllZero<uint>(ref arr);

    public static bool AllZero(float[] arr) => Utils.CheckAllZero<float>(ref arr);

    public static uint[] MakeCmdArgs(uint[] args, int maxArgs = 6)
    {
        uint[] numArray = new uint[maxArgs];
        int num = Math.Min(maxArgs, args.Length);
        for (int index = 0; index < num; ++index)
            numArray[index] = args[index];
        return numArray;
    }

    public static uint[] MakeCmdArgs(uint arg = 0, int maxArgs = 6) => Utils.MakeCmdArgs(new uint[1]
    {
      arg
    }, maxArgs);

    public static uint MakePsmMarginArg(int margin) => Convert.ToUInt32((margin < 0 ? 1048576 : 0) + margin) & (uint)ushort.MaxValue;

    public static T ByteArrayToStructure<T>(byte[] byteArray) where T : new()
    {
        GCHandle gcHandle = GCHandle.Alloc((object)byteArray, GCHandleType.Pinned);
        T structure;
        try
        {
            structure = (T)Marshal.PtrToStructure(gcHandle.AddrOfPinnedObject(), typeof(T));
        }
        finally
        {
            gcHandle.Free();
        }
        return structure;
    }

    public static uint ReverseBytes(uint value) => (uint)(((int)value & (int)byte.MaxValue) << 24 | ((int)value & 65280) << 8) | (value & 16711680U) >> 8 | (value & 4278190080U) >> 24;

    public static string GetStringFromBytes(uint value) => Encoding.ASCII.GetString(BitConverter.GetBytes(value)).Replace("\0", " ");

    public static string GetStringFromBytes(ulong value) => Encoding.ASCII.GetString(BitConverter.GetBytes(value)).Replace("\0", " ");

    public static string GetStringFromBytes(byte[] value) => Encoding.ASCII.GetString(value).Replace("\0", " ");

    public static int FindSequence(byte[] array, int start, byte[] sequence)
    {
        int num1 = array.Length - sequence.Length;
        byte num2 = sequence[0];
    label_8:
        for (; start <= num1; ++start)
        {
            if ((int)array[start] == (int)num2)
            {
                for (int index = 1; index != sequence.Length; ++index)
                {
                    if ((int)array[start + index] != (int)sequence[index])
                    {
                        goto label_8;
                    } else { break; }
                }
                return start;
            }
        }
        return -1;
    }

    public static bool ArrayMembersEqual(float[] array1, float[] array2, int numElements)
    {
        if (array1.Length < numElements || array2.Length < numElements)
            throw new ArgumentException("Arrays are not long enough to compare the specified number of elements.");
        for (int index = 0; index < numElements; ++index)
        {
            if ((double)array1[index] != (double)array2[index])
                return false;
        }
        return true;
    }
}
public sealed class RSMUMailbox : Mailbox
{
    public uint SMU_MSG_GetTableVersion { get; set; } = 0;

    public uint SMU_MSG_GetBiosIfVersion { get; set; } = 0;

    public uint SMU_MSG_TransferTableToDram { get; set; } = 0;

    public uint SMU_MSG_GetDramBaseAddress { get; set; } = 0;

    public uint SMU_MSG_EnableSmuFeatures { get; set; } = 0;

    public uint SMU_MSG_DisableSmuFeatures { get; set; } = 0;

    public uint SMU_MSG_SetOverclockFrequencyAllCores { get; set; } = 0;

    public uint SMU_MSG_SetOverclockFrequencyPerCore { get; set; } = 0;

    public uint SMU_MSG_SetBoostLimitFrequencyAllCores { get; set; } = 0;

    public uint SMU_MSG_SetBoostLimitFrequency { get; set; } = 0;

    public uint SMU_MSG_SetOverclockCpuVid { get; set; } = 0;

    public uint SMU_MSG_EnableOcMode { get; set; } = 0;

    public uint SMU_MSG_DisableOcMode { get; set; } = 0;

    public uint SMU_MSG_GetPBOScalar { get; set; } = 0;

    public uint SMU_MSG_SetPBOScalar { get; set; } = 0;

    public uint SMU_MSG_SetPPTLimit { get; set; } = 0;

    public uint SMU_MSG_SetTDCVDDLimit { get; set; } = 0;

    public uint SMU_MSG_SetTDCSOCLimit { get; set; } = 0;

    public uint SMU_MSG_SetEDCVDDLimit { get; set; } = 0;

    public uint SMU_MSG_SetEDCSOCLimit { get; set; } = 0;

    public uint SMU_MSG_SetHTCLimit { get; set; } = 0;

    public uint SMU_MSG_GetTjMax { get; set; } = 0;

    public uint SMU_MSG_SetTjMax { get; set; } = 0;

    public uint SMU_MSG_PBO_EN { get; set; } = 0;

    public uint SMU_MSG_SetDldoPsmMargin { get; set; } = 0;

    public uint SMU_MSG_SetAllDldoPsmMargin { get; set; } = 0;

    public uint SMU_MSG_GetDldoPsmMargin { get; set; } = 0;

    public uint SMU_MSG_SetGpuPsmMargin { get; set; } = 0;

    public uint SMU_MSG_GetGpuPsmMargin { get; set; } = 0;

    public uint SMU_MSG_ReadBoostLimit { get; set; } = 0;

    public uint SMU_MSG_GetFastestCoreofSocket { get; set; } = 0;

    public uint SMU_MSG_GetLN2Mode { get; set; } = 0;

    public RSMUMailbox()
      : base()
    {
    }
}
public sealed class MP1Mailbox : Mailbox
{
    public uint SMU_MSG_SetToolsDramAddress { get; set; } = 0;

    public uint SMU_MSG_EnableOcMode { get; set; } = 0;

    public uint SMU_MSG_DisableOcMode { get; set; } = 0;

    public uint SMU_MSG_SetOverclockFrequencyAllCores { get; set; } = 0;

    public uint SMU_MSG_SetOverclockFrequencyPerCore { get; set; } = 0;

    public uint SMU_MSG_SetBoostLimitFrequencyAllCores { get; set; } = 0;

    public uint SMU_MSG_SetBoostLimitFrequency { get; set; } = 0;

    public uint SMU_MSG_SetOverclockCpuVid { get; set; } = 0;

    public uint SMU_MSG_SetDldoPsmMargin { get; set; } = 0;

    public uint SMU_MSG_SetAllDldoPsmMargin { get; set; } = 0;

    public uint SMU_MSG_GetDldoPsmMargin { get; set; } = 0;

    public uint SMU_MSG_SetPBOScalar { get; set; } = 0;

    public uint SMU_MSG_SetEDCVDDLimit { get; set; } = 0;

    public uint SMU_MSG_SetTDCVDDLimit { get; set; } = 0;

    public uint SMU_MSG_SetPPTLimit { get; set; } = 0;

    public uint SMU_MSG_SetHTCLimit { get; set; } = 0;

    public MP1Mailbox()
      : base()
    {
    }
}
public sealed class HSMPMailbox : Mailbox
{
    public void Init()
    {
    }
    public uint InterfaceVersion;
    public uint HighestSupportedFunction;

    public HSMPMailbox(int maxArgs = 8)
      : base(maxArgs)
    {
    }

    public bool IsSupported => InterfaceVersion > 0U;

    public uint GetInterfaceVersion { get; set; } = 3;

    public uint ReadSocketPower { get; set; } = 4;

    public uint WriteSocketPowerLimit { get; set; } = 5;

    public uint ReadSocketPowerLimit { get; set; } = 6;

    public uint ReadMaxSocketPowerLimit { get; set; } = 7;

    public uint WriteBoostLimit { get; set; } = 8;

    public uint WriteBoostLimitAllCores { get; set; } = 9;

    public uint ReadBoostLimit { get; set; } = 10;

    public uint ReadProchotStatus { get; set; } = 11;

    public uint SetXgmiLinkWidthRange { get; set; } = 12;

    public uint APBDisable { get; set; } = 13;

    public uint APBEnable { get; set; } = 14;

    public uint ReadCurrentFclkMemclk { get; set; } = 15;

    public uint ReadCclkFrequencyLimit { get; set; } = 16;

    public uint ReadSocketC0Residency { get; set; } = 17;

    public uint SetLclkDpmLevelRange { get; set; } = 18;

    public uint GetLclkDpmLevelRange { get; set; } = 19;

    public uint GetMaxDDRBandwidthAndUtilization { get; set; } = 20;

    public uint GetDIMMTempRangeAndRefreshRate { get; set; } = 22;

    public uint GetDIMMPowerConsumption { get; set; } = 23;

    public uint GetDIMMThermalSensor { get; set; } = 24;

    public uint PwrCurrentActiveFreqLimitSocket { get; set; } = 25;

    public uint PwrCurrentActiveFreqLimitCore { get; set; } = 26;

    public uint PwrSviTelemetryAllRails { get; set; } = 27;

    public uint GetSocketFreqRange { get; set; } = 28;

    public uint GetCurrentIoBandwidth { get; set; } = 29;

    public uint GetCurrentXgmiBandwidth { get; set; } = 14;

    public uint SetGMI3LinkWidthRange { get; set; } = 31;

    public uint ControlPcieLinkRate { get; set; } = 32;

    public uint PwrEfficiencyModeSelection { get; set; } = 33;

    public uint SetDfPstateRange { get; set; } = 34;
}
internal static class GetSMUStatus
{
    public static readonly Dictionary<SMU.Status, string> status = new()
    {
      {
        SMU.Status.OK,
        "OK"
      },
      {
        SMU.Status.FAILED,
        "Failed"
      },
      {
        SMU.Status.UNKNOWN_CMD,
        "Unknown Command"
      },
      {
         SMU.Status.CMD_REJECTED_PREREQ,
        "CMD Rejected Prereq"
      },
      {
        SMU.Status.CMD_REJECTED_BUSY,
        "CMD Rejected Busy"
      }
    };
}
public sealed class IOModule : IDisposable
{
    internal IntPtr ioModule;
    public readonly IOModule._GetPhysLong GetPhysLong;
    public readonly IOModule._SetPhysLong SetPhysLong;
    private readonly IOModule._MapPhysToLin MapPhysToLin;
    private readonly IOModule._UnmapPhysicalMemory UnmapPhysicalMemory;
    private readonly IOModule._IsInpOutDriverOpen64 IsInpOutDriverOpen64;
    private readonly IOModule._InitializeWinIo32 InitializeWinIo32;
    private readonly IOModule._ShutdownWinIo32 ShutdownWinIo32;

    [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPStr)] string lpFileName);

    [DllImport("kernel32", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr hModule);

    [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string lpProcName);

    private IOModule.LibStatus WinIoStatus { get; } = IOModule.LibStatus.INITIALIZE_ERROR;

    public bool IsInpOutDriverOpen() => Utils.Is64Bit ? IsInpOutDriverOpen64() > 0U : WinIoStatus == IOModule.LibStatus.OK;

    public byte[] ReadMemory(IntPtr baseAddress, int size)
    {
        if (MapPhysToLin != null && UnmapPhysicalMemory != null)
        {
            IntPtr pPhysicalMemoryHandle;
            IntPtr num1 = MapPhysToLin(baseAddress, (uint)size, out pPhysicalMemoryHandle);
            if (num1 != IntPtr.Zero)
            {
                byte[] destination = new byte[size];
                Marshal.Copy(num1, destination, 0, destination.Length);
                int num2 = UnmapPhysicalMemory(pPhysicalMemoryHandle, num1) ? 1 : 0;
                return destination;
            }
        }
        return (byte[])null;
    }

    public static IntPtr LoadDll(string filename)
    {
        IntPtr num = IOModule.LoadLibrary(filename);
        if (num == IntPtr.Zero)
        {
            int lastWin32Error = Marshal.GetLastWin32Error();
            Win32Exception innerException = new Win32Exception(lastWin32Error);
            innerException.Data.Add((object)"LastWin32Error", (object)lastWin32Error);
            throw new Exception("Can't load DLL " + filename, (Exception)innerException);
        }
        return num;
    }

    public IOModule()
    {
        try
        {
            ioModule = IOModule.LoadDll(Utils.Is64Bit ? "inpoutx64.dll" : "WinIo32.dll");
            GetPhysLong = (IOModule._GetPhysLong)IOModule.GetDelegate(ioModule, nameof(GetPhysLong), typeof(IOModule._GetPhysLong));
            SetPhysLong = (IOModule._SetPhysLong)IOModule.GetDelegate(ioModule, nameof(SetPhysLong), typeof(IOModule._SetPhysLong));
            MapPhysToLin = (IOModule._MapPhysToLin)IOModule.GetDelegate(ioModule, nameof(MapPhysToLin), typeof(IOModule._MapPhysToLin));
            UnmapPhysicalMemory = (IOModule._UnmapPhysicalMemory)IOModule.GetDelegate(ioModule, nameof(UnmapPhysicalMemory), typeof(IOModule._UnmapPhysicalMemory));
            if (Utils.Is64Bit)
            {
                IsInpOutDriverOpen64 = (IOModule._IsInpOutDriverOpen64)IOModule.GetDelegate(ioModule, "IsInpOutDriverOpen", typeof(IOModule._IsInpOutDriverOpen64));
            }
            else
            {
                InitializeWinIo32 = (IOModule._InitializeWinIo32)IOModule.GetDelegate(ioModule, "InitializeWinIo", typeof(IOModule._InitializeWinIo32));
                ShutdownWinIo32 = (IOModule._ShutdownWinIo32)IOModule.GetDelegate(ioModule, "ShutdownWinIo", typeof(IOModule._ShutdownWinIo32));
                if (InitializeWinIo32())
                    WinIoStatus = IOModule.LibStatus.OK;
            }
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    public void Dispose()
    {
        if (ioModule == IntPtr.Zero)
            return;
        if (!Utils.Is64Bit)
        {
            int num = ShutdownWinIo32() ? 1 : 0;
        }
        IOModule.FreeLibrary(ioModule);
        ioModule = IntPtr.Zero;
    }

    public static Delegate GetDelegate(IntPtr moduleName, string procName, Type delegateType)
    {
        IntPtr procAddress = IOModule.GetProcAddress(moduleName, procName);
        return procAddress != IntPtr.Zero ? Marshal.GetDelegateForFunctionPointer(procAddress, delegateType) : throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());
    }

    public enum LibStatus
    {
        INITIALIZE_ERROR,
        OK,
        PARTIALLY_OK,
    }

    public delegate bool _GetPhysLong(UIntPtr memAddress, out uint data);

    public delegate bool _SetPhysLong(UIntPtr memAddress, uint data);

    private delegate IntPtr _MapPhysToLin(
      IntPtr pbPhysAddr,
      uint dwPhysSize,
      out IntPtr pPhysicalMemoryHandle);

    private delegate bool _UnmapPhysicalMemory(IntPtr PhysicalMemoryHandle, IntPtr pbLinAddr);

    private delegate uint _IsInpOutDriverOpen64();

    private delegate bool _InitializeWinIo32();

    private delegate bool _ShutdownWinIo32();
}
public class ACPI_MMIO
{
    internal const uint ACPI_MMIO_BASE_ADDRESS = 4275568640;
    internal const uint MISC_BASE = 4275572224;
    internal const uint MISC_GPPClkCntrl = 4275572224;
    internal const uint MISC_ClkOutputCntrl = 4275572228;
    internal const uint MISC_CGPLLConfig1 = 4275572232;
    internal const uint MISC_CGPLLConfig2 = 4275572236;
    internal const uint MISC_CGPLLConfig3 = 4275572240;
    internal const uint MISC_CGPLLConfig4 = 4275572244;
    internal const uint MISC_CGPLLConfig5 = 4275572248;
    internal const uint MISC_ClkCntl1 = 4275572288;
    internal const uint MISC_StrapStatus = 4275572352;
    private readonly IOModule io;

    public ACPI_MMIO(IOModule io) => this.io = io;

    private static int CalculateBclkIndex(int bclk)
    {
        if (bclk > 151)
            bclk = 151;
        else if (bclk < 96)
            bclk = 96;
        return (bclk & 128) != 0 ? bclk ^ 164 : bclk ^ 100;
    }

    private static int CalculateBclkFromIndex(int index) => index < 32 ? index ^ 100 : index ^ 164;

    public int GetStrapStatus()
    {
        uint data;
        return io.GetPhysLong((UIntPtr)4275572352U, out data) ? (int)Utils.GetBits(data, 17, 1) : -1;
    }

    private bool DisableSpreadSpectrum()
    {
        uint data;
        return io.GetPhysLong((UIntPtr)4275572232U, out data) && io.SetPhysLong((UIntPtr)4275572232U, Utils.SetBits(data, 0, 0, 0U));
    }

    private bool CG1AtomicUpdate()
    {
        uint data;
        return io.GetPhysLong((UIntPtr)4275572288U, out data) && io.SetPhysLong((UIntPtr)4275572288U, Utils.SetBits(data, 30, 1, 1U));
    }

    public bool SetBclk(double bclk)
    {
        DisableSpreadSpectrum();
        uint data;
        bool flag1 = io.GetPhysLong((UIntPtr)4275572288U, out data);
        bool flag2 = io.SetPhysLong((UIntPtr)4275572288U, Utils.SetBits(data, 25, 1, 1U));
        if (flag2)
        {
            int bclkIndex = ACPI_MMIO.CalculateBclkIndex((int)bclk);
            uint newVal = (uint)((bclk - (double)(int)bclk) / (1.0 / 16.0));
            if (newVal > 15U)
                newVal = 15U;
            flag2 = io.GetPhysLong((UIntPtr)4275572240U, out data);
            if (io.SetPhysLong((UIntPtr)4275572240U, Utils.SetBits(Utils.SetBits(data, 4, 9, (uint)bclkIndex), 25, 4, newVal)))
                return CG1AtomicUpdate();
        }
        return flag2;
    }

    public double? GetBclk()
    {
        uint data;
        if (!io.GetPhysLong((UIntPtr)4275572240U, out data))
            return new double?();
        uint bits1 = Utils.GetBits(data, 4, 9);
        uint bits2 = Utils.GetBits(data, 25, 4);
        return new double?((double)ACPI_MMIO.CalculateBclkFromIndex((int)bits1) + (double)bits2 * (1.0 / 16.0));
    }
}
[Serializable]
internal class SystemInfo
{
    private static Cpu.CPUInfo cpuInfo;

    public SystemInfo(Cpu.CPUInfo info, SMU smu)
    {
        SystemInfo.cpuInfo = info;
        SmuVersion = smu.Version;
        SmuTableVersion = smu.TableVersion;
        try
        {
            if (new ServiceController("Winmgmt").Status != ServiceControllerStatus.Running)
                throw new ManagementException("Windows Management Instrumentation service is not running");
            ManagementScope managementScope = new ManagementScope("root\\cimv2");
            managementScope.Connect();
            if (!managementScope.IsConnected)
                throw new ManagementException("Failed to connect to root\\cimv2");
            ManagementObjectSearcher managementObjectSearcher1 = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
            foreach (ManagementBaseObject managementBaseObject in managementObjectSearcher1.Get())
            {
                MbVendor = ((string)managementBaseObject["Manufacturer"]).Trim();
                MbName = ((string)managementBaseObject["Product"]).Trim();
            }
            managementObjectSearcher1.Dispose();
            ManagementObjectSearcher managementObjectSearcher2 = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
            foreach (ManagementBaseObject managementBaseObject in managementObjectSearcher2.Get())
                BiosVersion = ((string)managementBaseObject["SMBIOSBIOSVersion"]).Trim();
            managementObjectSearcher2.Dispose();
        }
        catch (ManagementException ex)
        {
            Console.WriteLine("WMI: {0}", (object)ex.Message);
        }
    }

    public string CpuName => SystemInfo.cpuInfo.cpuName ?? "N/A";

    public string CodeName => SystemInfo.cpuInfo.codeName.ToString();

    public uint CpuId => SystemInfo.cpuInfo.cpuid;

    public uint BaseModel => SystemInfo.cpuInfo.baseModel;

    public uint ExtendedModel => SystemInfo.cpuInfo.extModel;

    public uint Model => SystemInfo.cpuInfo.model;

    public uint Stepping => SystemInfo.cpuInfo.stepping;

    public string PackageType => string.Format("{0} ({1})", (object)SystemInfo.cpuInfo.packageType, (object)(int)SystemInfo.cpuInfo.packageType);

    public int FusedCoreCount => (int)SystemInfo.cpuInfo.topology.cores;

    public int PhysicalCoreCount => (int)SystemInfo.cpuInfo.topology.physicalCores;

    public int NodesPerProcessor => (int)SystemInfo.cpuInfo.topology.cpuNodes;

    public int Threads => (int)SystemInfo.cpuInfo.topology.logicalCores;

    public bool SMT => (int)SystemInfo.cpuInfo.topology.threadsPerCore > 1;

    public int CCDCount => (int)SystemInfo.cpuInfo.topology.ccds;

    public int CCXCount => (int)SystemInfo.cpuInfo.topology.ccxs;

    public int NumCoresInCCX => (int)SystemInfo.cpuInfo.topology.coresPerCcx;

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

    public uint PatchLevel => SystemInfo.cpuInfo.patchLevel;

    public string GetSmuVersionString() => SystemInfo.SmuVersionToString(SmuVersion);

    public string GetCpuIdString() => CpuId.ToString("X8").TrimStart('0');

    private static string SmuVersionToString(uint ver)
    {
        if ((ver & 4278190080U) <= 0U)
            return string.Format("{0}.{1}.{2}", (object)(uint)((int)(ver >> 16) & (int)byte.MaxValue), (object)(uint)((int)(ver >> 8) & (int)byte.MaxValue), (object)(uint)((int)ver & (int)byte.MaxValue));
        return string.Format("{0}.{1}.{2}.{3}", (object)(uint)((int)(ver >> 24) & (int)byte.MaxValue), (object)(uint)((int)(ver >> 16) & (int)byte.MaxValue), (object)(uint)((int)(ver >> 8) & (int)byte.MaxValue), (object)(uint)((int)ver & (int)byte.MaxValue));
    }
}
public class AOD
{
    internal readonly IOModule io;
    internal readonly ACPI acpi;
    public AOD.AodTable Table;
    private static readonly Dictionary<int, string> ProcOdtDict = new Dictionary<int, string>()
    {
      {
        0,
        "Hi-Z"
      },
      {
        1,
        "480.0 Ω"
      },
      {
        2,
        "240.0 Ω"
      },
      {
        3,
        "160.0 Ω"
      },
      {
        4,
        "120.0 Ω"
      },
      {
        5,
        "96.0 Ω"
      },
      {
        6,
        "80.0 Ω"
      },
      {
        7,
        "68.6 Ω"
      },
      {
        12,
        "60.0 Ω"
      },
      {
        13,
        "53.3 Ω"
      },
      {
        14,
        "48.0 Ω"
      },
      {
        15,
        "43.6 Ω"
      },
      {
        28,
        "40.0 Ω"
      },
      {
        29,
        "36.9 Ω"
      },
      {
        30,
        "34.3 Ω"
      },
      {
        31,
        "32.0 Ω"
      },
      {
        60,
        "30.0 Ω"
      },
      {
        61,
        "28.2 Ω"
      },
      {
        62,
        "26.7 Ω"
      },
      {
        63,
        "25.3 Ω"
      }
    };
    private static readonly Dictionary<int, string> ProcDataDrvStrenDict = new Dictionary<int, string>()
    {
      {
        2,
        "240.0 Ω"
      },
      {
        4,
        "120.0 Ω"
      },
      {
        6,
        "80.0 Ω"
      },
      {
        12,
        "60.0 Ω"
      },
      {
        14,
        "48.0 Ω"
      },
      {
        28,
        "40.0 Ω"
      },
      {
        30,
        "34.3 Ω"
      }
    };
    private static readonly Dictionary<int, string> DramDataDrvStrenDict = new Dictionary<int, string>()
    {
      {
        0,
        "34.0 Ω"
      },
      {
        1,
        "40.0 Ω"
      },
      {
        2,
        "48.0 Ω"
      }
    };
    private static readonly Dictionary<int, string> CadBusDrvStrenDict = new Dictionary<int, string>()
    {
      {
        30,
        "30.0 Ω"
      },
      {
        40,
        "40.0 Ω"
      },
      {
        60,
        "60.0 Ω"
      },
      {
        120,
        "120.0 Ω"
      }
    };
    private static readonly Dictionary<int, string> RttDict = new Dictionary<int, string>()
    {
      {
        0,
        "Off"
      },
      {
        1,
        "RZQ/1"
      },
      {
        2,
        "RZQ/2"
      },
      {
        3,
        "RZQ/3"
      },
      {
        4,
        "RZQ/4"
      },
      {
        5,
        "RZQ/5"
      },
      {
        6,
        "RZQ/6"
      },
      {
        7,
        "RZQ/7"
      }
    };

    private static string GetByKey(Dictionary<int, string> dict, int key)
    {
        string str;
        return dict.TryGetValue(key, out str) ? str : "N/A";
    }

    public static string GetProcODTString(int key) => AOD.GetByKey(AOD.ProcOdtDict, key);

    public static string GetProcDataDrvStrenString(int key) => AOD.GetByKey(AOD.ProcOdtDict, key);

    public static string GetDramDataDrvStrenString(int key) => AOD.GetByKey(AOD.DramDataDrvStrenDict, key);

    public static string GetCadBusDrvStrenString(int key) => AOD.GetByKey(AOD.CadBusDrvStrenDict, key);

    public static string GetRttString(int key) => AOD.GetByKey(AOD.RttDict, key);

    public AOD(IOModule io)
    {
        this.io = io;
        acpi = new ACPI(io);
        Table = new AOD.AodTable();
        //Init();
    }

    private ACPI.ACPITable? GetAcpiTable()
    {
        try
        {
            foreach (uint address in acpi.GetRSDT().Data)
            {
                if (address > 0U)
                {
                    try
                    {
                        ACPI.SDTHeader header = acpi.GetHeader<ACPI.SDTHeader>(address);
                        if ((int)header.Signature == (int)Table.Signature && ((long)header.OEMTableID == (long)Table.OemTableId || (long)header.OEMTableID == (long)ACPI.SignatureUL("AMD AOD")))
                            return new ACPI.ACPITable?(ACPI.ParseSdtTable(io.ReadMemory(new IntPtr((long)address), (int)header.Length)));
                    }
                    catch
                    {
                    }
                }
            }
        }
        catch
        {
        }
        return new ACPI.ACPITable?();
    }

    private void Init()
    {
        Table.acpiTable = GetAcpiTable();
        if (Table.acpiTable.HasValue)
        {
            ref ACPI.ACPITable? local1 = ref Table.acpiTable;
            int sequence = Utils.FindSequence(local1.HasValue ? local1.GetValueOrDefault().Data : (byte[])null, 0, ACPI.ByteSignature("AODE"));
            if (sequence == -1)
            {
                ref ACPI.ACPITable? local2 = ref Table.acpiTable;
                sequence = Utils.FindSequence(local2.HasValue ? local2.GetValueOrDefault().Data : (byte[])null, 0, ACPI.ByteSignature("AODT"));
            }
            if (sequence == -1)
                return;
            byte[] numArray = new byte[16];
            ref ACPI.ACPITable? local3 = ref Table.acpiTable;
            Buffer.BlockCopy(local3.HasValue ? (Array)local3.GetValueOrDefault().Data : (Array)null, sequence, (Array)numArray, 0, 16);
            ACPI.OperationRegion structure = Utils.ByteArrayToStructure<ACPI.OperationRegion>(numArray);
            Table.BaseAddress = structure.Offset;
            Table.Length = (int)structure.Length[1] << 8 | (int)structure.Length[0];
        }
        Refresh();
    }

    public bool Refresh()
    {
        try
        {
            Table.rawAodTable = io.ReadMemory(new IntPtr((long)Table.BaseAddress), Table.Length);
            Table.Data = Utils.ByteArrayToStructure<AOD.AodData>(Table.rawAodTable);
            return true;
        }
        catch
        {
        }
        return false;
    }

    public class TProcODT
    {
        public override string ToString() => base.ToString();
    }

    [Serializable]
    [StructLayout(LayoutKind.Explicit, Pack = 4)]
    public struct AodData
    {
        [FieldOffset(8920)]
        public int SMTEn;
        [FieldOffset(8924)]
        public int MemClk;
        [FieldOffset(8928)]
        public int Tcl;
        [FieldOffset(8932)]
        public int Trcd;
        [FieldOffset(8936)]
        public int Trp;
        [FieldOffset(8940)]
        public int Tras;
        [FieldOffset(8944)]
        public int Trc;
        [FieldOffset(8948)]
        public int Twr;
        [FieldOffset(8952)]
        public int Trfc;
        [FieldOffset(8956)]
        public int Trfc2;
        [FieldOffset(8960)]
        public int Trfcsb;
        [FieldOffset(8964)]
        public int Trtp;
        [FieldOffset(8968)]
        public int TrrdL;
        [FieldOffset(8972)]
        public int TrrdS;
        [FieldOffset(8976)]
        public int Tfaw;
        [FieldOffset(8980)]
        public int TwtrL;
        [FieldOffset(8984)]
        public int TwtrS;
        [FieldOffset(8988)]
        public int TrdrdScL;
        [FieldOffset(8992)]
        public int TrdrdSc;
        [FieldOffset(8996)]
        public int TrdrdSd;
        [FieldOffset(9000)]
        public int TrdrdDd;
        [FieldOffset(9004)]
        public int TwrwrScL;
        [FieldOffset(9008)]
        public int TwrwrSc;
        [FieldOffset(9012)]
        public int TwrwrSd;
        [FieldOffset(9016)]
        public int TwrwrDd;
        [FieldOffset(9020)]
        public int Twrrd;
        [FieldOffset(9024)]
        public int Trdwr;
        [FieldOffset(9028)]
        public int CadBusDrvStren;
        [FieldOffset(9032)]
        public int ProcDataDrvStren;
        [FieldOffset(9036)]
        public int ProcODT;
        [FieldOffset(9040)]
        public int DramDataDrvStren;
        [FieldOffset(9044)]
        public int RttNomWr;
        [FieldOffset(9048)]
        public int RttNomRd;
        [FieldOffset(9052)]
        public int RttWr;
        [FieldOffset(9056)]
        public int RttPark;
        [FieldOffset(9060)]
        public int RttParkDqs;
        [FieldOffset(9096)]
        public int MemVddio;
        [FieldOffset(9100)]
        public int MemVddq;
        [FieldOffset(9104)]
        public int MemVpp;
    }

    [Serializable]
    public class AodTable
    {
        public readonly uint Signature;
        public ulong OemTableId;
        public uint BaseAddress;
        public int Length;
        public ACPI.ACPITable? acpiTable;
        public AOD.AodData Data;
        public byte[] rawAodTable;

        public AodTable()
        {
            Signature = ACPI.Signature("SSDT");
            OemTableId = ACPI.SignatureUL("AOD     ");
        }
    }
}
public class PowerTable : INotifyPropertyChanged
{
    private readonly IOModule io;
    private readonly SMU smu;
    private readonly ACPI_MMIO mmio;
    private readonly PowerTable.PTDef tableDef;
    public readonly uint DramBaseAddressLo;
    public readonly uint DramBaseAddressHi;
    public readonly uint DramBaseAddress;
    public readonly int TableSize;
    private const int NUM_ELEMENTS_TO_COMPARE = 6;
    private static readonly PowerTable.PowerTableDef PowerTables = new PowerTable.PowerTableDef()
    {
      {
        1966081,
        1392,
        1120,
        1124,
        1128,
        268,
        248,
        -1,
        -1,
        -1,
        -1
      },
      {
        1966082,
        1392,
        1140,
        1144,
        1148,
        268,
        248,
        -1,
        -1,
        -1,
        -1
      },
      {
        1966083,
        1552,
        664,
        668,
        672,
        260,
        240,
        -1,
        -1,
        -1,
        -1
      },
      {
        1966084,
        1552,
        664,
        668,
        672,
        260,
        240,
        -1,
        -1,
        -1,
        -1
      },
      {
        16,
        1552,
        664,
        668,
        672,
        260,
        240,
        -1,
        -1,
        -1,
        -1
      },
      {
        2490369,
        1552,
        40,
        44,
        48,
        16,
        -1,
        -1,
        -1,
        -1,
        -1
      },
      {
        3604480,
        1948,
        1204,
        1208,
        1212,
        400,
        1836,
        -1,
        -1,
        -1,
        -1
      },
      {
        3604481,
        2188,
        1444,
        1448,
        1452,
        400,
        2076,
        -1,
        -1,
        -1,
        -1
      },
      {
        3604482,
        2196,
        1452,
        1456,
        1460,
        408,
        2084,
        -1,
        -1,
        -1,
        -1
      },
      {
        3604483,
        2228,
        1484,
        1488,
        1492,
        408,
        2116,
        -1,
        -1,
        -1,
        -1
      },
      {
        3604485,
        2256,
        1512,
        1516,
        1520,
        408,
        2156,
        -1,
        -1,
        -1,
        -1
      },
      {
        17,
        2256,
        1512,
        1516,
        1520,
        408,
        2156,
        -1,
        -1,
        -1,
        -1
      },
      {
        4194305,
        2256,
        1572,
        1576,
        1580,
        412,
        2204,
        -1,
        -1,
        -1,
        -1
      },
      {
        4194306,
        2256,
        1596,
        1600,
        1604,
        412,
        2228,
        -1,
        -1,
        -1,
        -1
      },
      {
        4194307,
        2372,
        1632,
        1636,
        1640,
        412,
        2256,
        -1,
        -1,
        -1,
        -1
      },
      {
        4194308,
        2372,
        1636,
        1640,
        1644,
        412,
        2260,
        -1,
        -1,
        -1,
        -1
      },
      {
        4194309,
        2372,
        1636,
        1640,
        1644,
        412,
        2260,
        -1,
        -1,
        -1,
        -1
      },
      {
        4521988,
        2724,
        1636,
        1640,
        1644,
        412,
        2260,
        -1,
        -1,
        -1,
        -1
      },
      {
        4521989,
        2724,
        1712,
        1716,
        1720,
        456,
        2260,
        -1,
        -1,
        -1,
        -1
      },
      {
        18,
        2376,
        1636,
        1640,
        1644,
        412,
        2260,
        -1,
        -1,
        -1,
        -1
      },
      {
        256,
        2020,
        132,
        132,
        132,
        104,
        68,
        -1,
        -1,
        -1,
        -1
      },
      {
        257,
        2020,
        132,
        132,
        132,
        96,
        60,
        -1,
        -1,
        -1,
        -1
      },
      {
        512,
        2020,
        176,
        184,
        188,
        164,
        484,
        488,
        -1,
        -1,
        -1
      },
      {
        514,
        2020,
        188,
        196,
        200,
        176,
        496,
        500,
        -1,
        -1,
        -1
      },
      {
        515,
        2020,
        192,
        200,
        204,
        180,
        500,
        504,
        -1,
        588,
        -1
      },
      {
        2951427,
        2020,
        188,
        196,
        200,
        176,
        544,
        548,
        -1,
        -1,
        -1
      },
      {
        3670021,
        7088,
        192,
        200,
        204,
        180,
        548,
        552,
        556,
        -1,
        -1
      },
      {
        3671301,
        3888,
        192,
        200,
        204,
        180,
        548,
        552,
        556,
        -1,
        -1
      },
      {
        3672068,
        2212,
        192,
        200,
        204,
        180,
        548,
        552,
        556,
        588,
        -1
      },
      {
        3672069,
        2288,
        192,
        200,
        204,
        180,
        548,
        552,
        556,
        688,
        -1
      },
      {
        3672324,
        1444,
        192,
        200,
        204,
        180,
        548,
        552,
        556,
        676,
        -1
      },
      {
        3672325,
        1488,
        192,
        200,
        204,
        180,
        548,
        552,
        556,
        688,
        -1
      },
      {
        768,
        2376,
        192,
        200,
        204,
        180,
        548,
        552,
        556,
        -1,
        -1
      },
      {
        5505284,
        1704,
        280,
        296,
        312,
        208,
        1072,
        -1,
        -1,
        -1,
        -1
      },
      {
        5505280,
        1560,
        280,
        296,
        312,
        208,
        1072,
        -1,
        -1,
        -1,
        224
      },
      {
        5505281,
        1564,
        280,
        296,
        312,
        208,
        1072,
        -1,
        -1,
        -1,
        224
      },
      {
        5505282,
        1644,
        280,
        296,
        312,
        208,
        1072,
        -1,
        -1,
        -1,
        224
      },
      {
        5505283,
        1676,
        280,
        296,
        312,
        208,
        1072,
        -1,
        -1,
        -1,
        224
      },
      {
        5505024,
        2088,
        280,
        296,
        312,
        208,
        1072,
        -1,
        -1,
        -1,
        224
      },
      {
        5505025,
        2092,
        280,
        296,
        312,
        208,
        1072,
        -1,
        -1,
        -1,
        224
      },
      {
        5505026,
        2172,
        280,
        296,
        312,
        208,
        1072,
        -1,
        -1,
        -1,
        224
      },
      {
        5505027,
        2204,
        280,
        296,
        312,
        208,
        1072,
        -1,
        -1,
        -1,
        224
      },
      {
        5505028,
        2236,
        280,
        296,
        312,
        208,
        1072,
        -1,
        -1,
        -1,
        224
      },
      {
        6030082,
        3480,
        404,
        424,
        444,
        308,
        -1,
        -1,
        -1,
        -1,
        -1
      },
      {
        6030083,
        3492,
        412,
        432,
        452,
        316,
        -1,
        -1,
        -1,
        -1,
        -1
      },
      {
        1472,
        3492,
        412,
        432,
        452,
        316,
        -1,
        -1,
        -1,
        -1,
        -1
      },
      {
        1024,
        2376,
        280,
        296,
        312,
        208,
        1072,
        -1,
        -1,
        -1,
        224
      }
    };
    private float fclk;
    private float mclk;
    private float uclk;
    private float vddcr_soc;
    private float cldo_vddp;
    private float cldo_vddg_iod;
    private float cldo_vddg_ccd;
    private float vdd_misc;

    public event PropertyChangedEventHandler PropertyChanged;

    private void OnPropertyChanged(PropertyChangedEventArgs eventArgs)
    {
        PropertyChangedEventHandler propertyChanged = PropertyChanged;
        if (propertyChanged == null)
            return;
        propertyChanged((object)this, eventArgs);
    }

    private bool SetProperty<T>(ref T storage, T value, PropertyChangedEventArgs args)
    {
        if (object.Equals((object)storage, (object)value))
            return false;
        storage = value;
        OnPropertyChanged(args);
        return true;
    }

    private PowerTable.PTDef GetDefByVersion(uint version) => PowerTable.PowerTables.Find((Predicate<PowerTable.PTDef>)(x => (long)x.tableVersion == (long)version));

    private PowerTable.PTDef GetDefaultTableDef(uint tableVersion, SMU.SmuType smutype)
    {
        uint version = 0;
        switch (smutype)
        {
            case SMU.SmuType.TYPE_CPU0:
                version = 256U;
                break;
            case SMU.SmuType.TYPE_CPU1:
                version = 257U;
                break;
            case SMU.SmuType.TYPE_CPU2:
                uint num1 = tableVersion & 7U;
                int num2;
                switch (num1)
                {
                    case 0:
                        version = 512U;
                        goto label_12;
                    case 1:
                    case 2:
                        num2 = 1;
                        break;
                    default:
                        num2 = num1 == 4U ? 1 : 0;
                        break;
                }
                version = num2 == 0 ? 515U : 514U;
                break;
            case SMU.SmuType.TYPE_CPU3:
                version = 768U;
                break;
            case SMU.SmuType.TYPE_CPU4:
                version = tableVersion >> 16 != 92U ? 1024U : 1472U;
                break;
            case SMU.SmuType.TYPE_APU0:
                version = 16U;
                break;
            case SMU.SmuType.TYPE_APU1:
            case SMU.SmuType.TYPE_APU2:
                version = tableVersion >> 16 != 55U ? 18U : 17U;
                break;
        }
    label_12:
        return GetDefByVersion(version);
    }

    private PowerTable.PTDef GetPowerTableDef(uint tableVersion, SMU.SmuType smutype)
    {
        PowerTable.PTDef defByVersion = GetDefByVersion(tableVersion);
        return defByVersion.tableSize != 0 ? defByVersion : GetDefaultTableDef(tableVersion, smutype);
    }

    public PowerTable(SMU smuInstance, IOModule ioInstance, ACPI_MMIO mmio)
    {
        smu = smuInstance ?? throw new ArgumentNullException(nameof(smuInstance));
        io = ioInstance ?? throw new ArgumentNullException(nameof(ioInstance));
        this.mmio = mmio ?? throw new ArgumentNullException(nameof(mmio));
        CmdResult cmdResult = new GetDramAddress(smu).Execute();
        DramBaseAddressLo = DramBaseAddress = cmdResult.args[0];
        DramBaseAddressHi = cmdResult.args[1];
        if (DramBaseAddress == 0U)
            throw new ApplicationException("Could not get DRAM base address.");
        if (!Utils.Is64Bit)
            new SetToolsDramAddress(smu).Execute(DramBaseAddress);
        tableDef = GetPowerTableDef(smu.TableVersion, smu.SMU_TYPE);
        TableSize = tableDef.tableSize;
        Table = new float[TableSize / 4];
    }

    private float GetDiscreteValue(float[] pt, int index) => index > -1 && index < TableSize ? pt[index / 4] : 0.0f;

    private void ParseTable(float[] pt)
    {
        if (pt == null)
            return;
        float num = 1f;
        double? bclk = mmio.GetBclk();
        if (bclk.HasValue)
            num = (float)bclk.Value / 100f;
        MCLK = GetDiscreteValue(pt, tableDef.offsetMclk) * num;
        FCLK = GetDiscreteValue(pt, tableDef.offsetFclk) * num;
        UCLK = GetDiscreteValue(pt, tableDef.offsetUclk) * num;
        VDDCR_SOC = GetDiscreteValue(pt, tableDef.offsetVddcrSoc);
        CLDO_VDDP = GetDiscreteValue(pt, tableDef.offsetCldoVddp);
        CLDO_VDDG_IOD = GetDiscreteValue(pt, tableDef.offsetCldoVddgIod);
        CLDO_VDDG_CCD = GetDiscreteValue(pt, tableDef.offsetCldoVddgCcd);
        VDD_MISC = GetDiscreteValue(pt, tableDef.offsetVddMisc);
    }

    private float[] ReadTableFromMemory(int tableSize)
    {
        float[] dst = new float[tableSize];
        if (Utils.Is64Bit)
        {
            byte[] src = (smu.SMU_TYPE < SMU.SmuType.TYPE_CPU4 || smu.SMU_TYPE >= SMU.SmuType.TYPE_CPU9) && smu.SMU_TYPE != SMU.SmuType.TYPE_APU2 ? io.ReadMemory(new IntPtr((long)DramBaseAddressLo), tableSize * 4) : io.ReadMemory(new IntPtr((long)DramBaseAddressHi << 32 | (long)DramBaseAddressLo), tableSize * 4);
            if (src != null && src.Length != 0)
                Buffer.BlockCopy((Array)src, 0, (Array)dst, 0, src.Length);
        }
        else
        {
            try
            {
                for (int index = 0; index < dst.Length; ++index)
                {
                    int dstOffset = index * 4;
                    uint data;
                    int num = io.GetPhysLong((UIntPtr)((ulong)DramBaseAddress + (ulong)dstOffset), out data) ? 1 : 0;
                    byte[] bytes = BitConverter.GetBytes(data);
                    Buffer.BlockCopy((Array)bytes, 0, (Array)dst, dstOffset, bytes.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error occurred while reading table: " + ex.Message);
            }
        }
        return dst;
    }

    public SMU.Status Refresh()
    {
        SMU.Status status = SMU.Status.FAILED;
        if (DramBaseAddress > 0U)
        {
            try
            {
                float[] numArray = ReadTableFromMemory(6);
                if (Utils.AllZero(numArray) || Utils.ArrayMembersEqual(Table, numArray, 6))
                {
                    status = new TransferTableToDram(smu).Execute().status;
                    if (status != SMU.Status.OK)
                        return status;
                }
                Table = ReadTableFromMemory(TableSize);
                if (!Utils.AllZero(Table))
                {
                    ParseTable(Table);
                    return SMU.Status.OK;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error occurred while reading table: " + ex.Message);
                return SMU.Status.FAILED;
            }
        }
        return status;
    }

    public float ConfiguredClockSpeed { get; set; } = 0.0f;

    public float MemRatio { get; set; } = 0.0f;

    public float[] Table
    {
        get; private set;
    }

    public float FCLK
    {
        get => fclk;
        set => SetProperty<float>(ref fclk, value, InternalEventArgsCache.FCLK);
    }

    public float MCLK
    {
        get => mclk;
        set => SetProperty<float>(ref mclk, value, InternalEventArgsCache.MCLK);
    }

    public float UCLK
    {
        get => uclk;
        set => SetProperty<float>(ref uclk, value, InternalEventArgsCache.UCLK);
    }

    public float VDDCR_SOC
    {
        get => vddcr_soc;
        set => SetProperty<float>(ref vddcr_soc, value, InternalEventArgsCache.VDDCR_SOC);
    }

    public float CLDO_VDDP
    {
        get => cldo_vddp;
        set => SetProperty<float>(ref cldo_vddp, value, InternalEventArgsCache.CLDO_VDDP);
    }

    public float CLDO_VDDG_IOD
    {
        get => cldo_vddg_iod;
        set => SetProperty<float>(ref cldo_vddg_iod, value, InternalEventArgsCache.CLDO_VDDG_IOD);
    }

    public float CLDO_VDDG_CCD
    {
        get => cldo_vddg_ccd;
        set => SetProperty<float>(ref cldo_vddg_ccd, value, InternalEventArgsCache.CLDO_VDDG_CCD);
    }

    public float VDD_MISC
    {
        get => vdd_misc;
        set => SetProperty<float>(ref vdd_misc, value, InternalEventArgsCache.VDD_MISC);
    }

    private struct PTDef
    {
        public int tableVersion;
        public int tableSize;
        public int offsetFclk;
        public int offsetUclk;
        public int offsetMclk;
        public int offsetVddcrSoc;
        public int offsetCldoVddp;
        public int offsetCldoVddgIod;
        public int offsetCldoVddgCcd;
        public int offsetCoresPower;
        public int offsetVddMisc;
    }

    private class PowerTableDef : List<PowerTable.PTDef>
    {
        public void Add(
          int tableVersion,
          int tableSize,
          int offsetFclk,
          int offsetUclk,
          int offsetMclk,
          int offsetVddcrSoc,
          int offsetCldoVddp,
          int offsetCldoVddgIod,
          int offsetCldoVddgCcd,
          int offsetCoresPower,
          int offsetVddMisc)
        {
            Add(new PowerTable.PTDef()
            {
                tableVersion = tableVersion,
                tableSize = tableSize,
                offsetFclk = offsetFclk,
                offsetUclk = offsetUclk,
                offsetMclk = offsetMclk,
                offsetVddcrSoc = offsetVddcrSoc,
                offsetCldoVddp = offsetCldoVddp,
                offsetCldoVddgIod = offsetCldoVddgIod,
                offsetCldoVddgCcd = offsetCldoVddgCcd,
                offsetCoresPower = offsetCoresPower,
                offsetVddMisc = offsetVddMisc
            });
        }
    }
}
public class ACPI
{
    internal const uint RSDP_REGION_BASE_ADDRESS = 917504;
    internal const int RSDP_REGION_LENGTH = 131071;
    private readonly IOModule io;

    public ACPI(IOModule io) => this.io = io ?? throw new ArgumentNullException(nameof(io));

    public static ACPI.ParsedSDTHeader ParseRawHeader(ACPI.SDTHeader rawHeader) => new ACPI.ParsedSDTHeader()
    {
        Signature = Utils.GetStringFromBytes(rawHeader.Signature),
        Length = rawHeader.Length,
        Revision = rawHeader.Revision,
        Checksum = rawHeader.Checksum,
        OEMID = Utils.GetStringFromBytes(rawHeader.OEMID),
        OEMTableID = Utils.GetStringFromBytes(rawHeader.OEMTableID),
        OEMRevision = rawHeader.OEMRevision,
        CreatorID = Utils.GetStringFromBytes(rawHeader.CreatorID),
        CreatorRevision = rawHeader.CreatorRevision
    };

    public static uint Signature(string ascii)
    {
        uint num1 = 0;
        int num2 = Math.Min(ascii.Length, 4);
        for (int index = 0; index < num2; ++index)
            num1 |= (uint)ascii[index] << index * 8;
        return num1;
    }

    public static ulong SignatureUL(string ascii)
    {
        ulong num1 = 0;
        int num2 = Math.Min(ascii.Length, 8);
        for (int index = 0; index < num2; ++index)
            num1 |= (ulong)ascii[index] << index * 8;
        return num1;
    }

    public static byte[] ByteSignature(string ascii) => BitConverter.GetBytes(ACPI.Signature(ascii));

    public static byte[] ByteSignatureUL(string ascii) => BitConverter.GetBytes(ACPI.SignatureUL(ascii));

    public T GetHeader<T>(uint address, int length = 36) where T : new() => Utils.ByteArrayToStructure<T>(io.ReadMemory(new IntPtr((long)address), length));

    public ACPI.RSDP GetRsdp()
    {
        int sequence = Utils.FindSequence(io.ReadMemory(new IntPtr(917504L), 131071), 0, ACPI.ByteSignatureUL("RSD PTR "));
        return sequence >= 0 ? Utils.ByteArrayToStructure<ACPI.RSDP>(io.ReadMemory(new IntPtr(917504L + (long)sequence), 36)) : throw new SystemException("ACPI: Could not find RSDP signature");
    }

    public ACPI.RSDT GetRSDT()
    {
        ACPI.RSDP rsdp = GetRsdp();
        ACPI.SDTHeader header = GetHeader<ACPI.SDTHeader>(rsdp.RsdtAddress);
        byte[] src = io.ReadMemory(new IntPtr((long)rsdp.RsdtAddress), (int)header.Length);
        GCHandle gcHandle = GCHandle.Alloc((object)src, GCHandleType.Pinned);
        ACPI.RSDT rsdt;
        try
        {
            int srcOffset = Marshal.SizeOf((object)header);
            int count = (int)header.Length - srcOffset;
            rsdt = new ACPI.RSDT()
            {
                Header = header,
                Data = new uint[count]
            };
            Buffer.BlockCopy((Array)src, srcOffset, (Array)rsdt.Data, 0, count);
        }
        finally
        {
            gcHandle.Free();
        }
        return rsdt;
    }

    public static ACPI.ACPITable ParseSdtTable(byte[] rawTable)
    {
        GCHandle gcHandle = GCHandle.Alloc((object)rawTable, GCHandleType.Pinned);
        ACPI.ACPITable sdtTable;
        try
        {
            ACPI.SDTHeader structure = (ACPI.SDTHeader)Marshal.PtrToStructure(gcHandle.AddrOfPinnedObject(), typeof(ACPI.SDTHeader));
            int srcOffset = Marshal.SizeOf((object)structure);
            int count = (int)structure.Length - srcOffset;
            sdtTable = new ACPI.ACPITable()
            {
                RawHeader = structure,
                Header = ACPI.ParseRawHeader(structure),
                Data = new byte[count]
            };
            Buffer.BlockCopy((Array)rawTable, srcOffset, (Array)sdtTable.Data, 0, count);
        }
        finally
        {
            gcHandle.Free();
        }
        return sdtTable;
    }

    public static class TableSignature
    {
        public const string RSDP = "RSD PTR ";
        public const string RSDT = "RSDT";
        public const string XSDT = "XSDT";
        public const string SSDT = "SSDT";
        public const string AOD_ = "AOD     ";
        public const string AAOD = "AMD AOD";
        public const string AODE = "AODE";
        public const string AODT = "AODT";
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Size = 36, Pack = 1)]
    public struct RSDP
    {
        public ulong Signature;
        public byte Checksum;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public byte[] OEMID;
        public byte Revision;
        public uint RsdtAddress;
        public uint Length;
        public ulong XsdtAddress;
        public byte ExtendedChecksum;
        public byte Reserved1;
        public byte Reserved2;
        public byte Reserved3;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Size = 36, Pack = 1)]
    public struct SDTHeader
    {
        public uint Signature;
        public uint Length;
        public byte Revision;
        public byte Checksum;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public byte[] OEMID;
        public ulong OEMTableID;
        public uint OEMRevision;
        public uint CreatorID;
        public uint CreatorRevision;
    }

    [Serializable]
    public struct ParsedSDTHeader
    {
        public string Signature;
        public uint Length;
        public byte Revision;
        public byte Checksum;
        public string OEMID;
        public string OEMTableID;
        public uint OEMRevision;
        public string CreatorID;
        public uint CreatorRevision;
    }

    [Serializable]
    public struct ACPITable
    {
        public ACPI.SDTHeader RawHeader;
        public ACPI.ParsedSDTHeader Header;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public byte[] Data;
    }

    [Serializable]
    public struct RSDT
    {
        public ACPI.SDTHeader Header;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public uint[] Data;
    }

    [Serializable]
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct FADT
    {
        [FieldOffset(0)]
        public ACPI.SDTHeader Header;
        [FieldOffset(36)]
        public uint FIRMWARE_CTRL;
        [FieldOffset(40)]
        public uint DSDT;
        [FieldOffset(132)]
        public ulong X_FIRMWARE_CTRL;
        [FieldOffset(140)]
        public ulong X_DSDT;
    }

    public enum AddressSpace : byte
    {
        SystemMemory,
        SystemIo,
        PciConfigSpace,
        EmbeddedController,
        SMBus,
        SystemCmos,
        PciBarTarget,
        Ipmi,
        GeneralIo,
        GenericSerialBus,
        PlatformCommunicationsChannel,
        FunctionalFixedHardware,
        OemDefined,
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Size = 16, Pack = 1)]
    public struct OperationRegion
    {
        public uint RegionName;
        public ACPI.AddressSpace RegionSpace;
        public byte _unknown1;
        public uint Offset;
        public byte _unknown2;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public byte[] Length;
        public byte _unknown3;
        public byte _unknown4;
        public byte _unknown5;
    }
}
internal static class InternalEventArgsCache
{
    internal static PropertyChangedEventArgs FCLK = new PropertyChangedEventArgs(nameof(FCLK));
    internal static PropertyChangedEventArgs MCLK = new PropertyChangedEventArgs(nameof(MCLK));
    internal static PropertyChangedEventArgs UCLK = new PropertyChangedEventArgs(nameof(UCLK));
    internal static PropertyChangedEventArgs VDDCR_SOC = new PropertyChangedEventArgs(nameof(VDDCR_SOC));
    internal static PropertyChangedEventArgs CLDO_VDDP = new PropertyChangedEventArgs(nameof(CLDO_VDDP));
    internal static PropertyChangedEventArgs CLDO_VDDG_IOD = new PropertyChangedEventArgs(nameof(CLDO_VDDG_IOD));
    internal static PropertyChangedEventArgs CLDO_VDDG_CCD = new PropertyChangedEventArgs(nameof(CLDO_VDDG_CCD));
    internal static PropertyChangedEventArgs VDD_MISC = new PropertyChangedEventArgs(nameof(VDD_MISC));
}
internal static class Opcode
{
    private static IntPtr codeBuffer;
    private static ulong size;
    public static Opcode.RdtscDelegate Rdtsc;
    private static readonly byte[] RDTSC_32 = new byte[3]
    {
      (byte) 15,
      (byte) 49,
      (byte) 195
    };
    private static readonly byte[] RDTSC_64 = new byte[10]
    {
      (byte) 15,
      (byte) 49,
      (byte) 72,
      (byte) 193,
      (byte) 226,
      (byte) 32,
      (byte) 72,
      (byte) 11,
      (byte) 194,
      (byte) 195
    };
    public static Opcode.CpuidDelegate Cpuid;
    private static readonly byte[] CPUID_32 = new byte[68]
    {
      (byte) 85,
      (byte) 139,
      (byte) 236,
      (byte) 131,
      (byte) 236,
      (byte) 16,
      (byte) 139,
      (byte) 69,
      (byte) 8,
      (byte) 139,
      (byte) 77,
      (byte) 12,
      (byte) 83,
      (byte) 15,
      (byte) 162,
      (byte) 86,
      (byte) 141,
      (byte) 117,
      (byte) 240,
      (byte) 137,
      (byte) 6,
      (byte) 139,
      (byte) 69,
      (byte) 16,
      (byte) 137,
      (byte) 94,
      (byte) 4,
      (byte) 137,
      (byte) 78,
      (byte) 8,
      (byte) 137,
      (byte) 86,
      (byte) 12,
      (byte) 139,
      (byte) 77,
      (byte) 240,
      (byte) 137,
      (byte) 8,
      (byte) 139,
      (byte) 69,
      (byte) 20,
      (byte) 139,
      (byte) 77,
      (byte) 244,
      (byte) 137,
      (byte) 8,
      (byte) 139,
      (byte) 69,
      (byte) 24,
      (byte) 139,
      (byte) 77,
      (byte) 248,
      (byte) 137,
      (byte) 8,
      (byte) 139,
      (byte) 69,
      (byte) 28,
      (byte) 139,
      (byte) 77,
      (byte) 252,
      (byte) 94,
      (byte) 137,
      (byte) 8,
      (byte) 91,
      (byte) 201,
      (byte) 194,
      (byte) 24,
      (byte) 0
    };
    private static readonly byte[] CPUID_64_WINDOWS = new byte[37]
    {
      (byte) 72,
      (byte) 137,
      (byte) 92,
      (byte) 36,
      (byte) 8,
      (byte) 139,
      (byte) 193,
      (byte) 139,
      (byte) 202,
      (byte) 15,
      (byte) 162,
      (byte) 65,
      (byte) 137,
      (byte) 0,
      (byte) 72,
      (byte) 139,
      (byte) 68,
      (byte) 36,
      (byte) 40,
      (byte) 65,
      (byte) 137,
      (byte) 25,
      (byte) 72,
      (byte) 139,
      (byte) 92,
      (byte) 36,
      (byte) 8,
      (byte) 137,
      (byte) 8,
      (byte) 72,
      (byte) 139,
      (byte) 68,
      (byte) 36,
      (byte) 48,
      (byte) 137,
      (byte) 16,
      (byte) 195
    };
    private static readonly byte[] CPUID_64_LINUX = new byte[27]
    {
      (byte) 73,
      (byte) 137,
      (byte) 210,
      (byte) 73,
      (byte) 137,
      (byte) 203,
      (byte) 83,
      (byte) 137,
      (byte) 248,
      (byte) 137,
      (byte) 241,
      (byte) 15,
      (byte) 162,
      (byte) 65,
      (byte) 137,
      (byte) 2,
      (byte) 65,
      (byte) 137,
      (byte) 27,
      (byte) 65,
      (byte) 137,
      (byte) 8,
      (byte) 65,
      (byte) 137,
      (byte) 17,
      (byte) 91,
      (byte) 195
    };

    public static void Open()
    {
        byte[] source1;
        byte[] source2;
        if (IntPtr.Size == 4)
        {
            source1 = Opcode.RDTSC_32;
            source2 = Opcode.CPUID_32;
        }
        else
        {
            source1 = Opcode.RDTSC_64;
            source2 = !OperatingSystem.IsUnix ? Opcode.CPUID_64_WINDOWS : Opcode.CPUID_64_LINUX;
        }
        Opcode.size = (ulong)(source1.Length + source2.Length);
        if (OperatingSystem.IsUnix)
        {
            Assembly assembly = Assembly.Load("Mono.Posix, Version=2.0.0.0, Culture=neutral, PublicKeyToken=0738eb9f132ed756");
            MethodInfo method = assembly.GetType("Mono.Unix.Native.Syscall").GetMethod("mmap");
            Type type1 = assembly.GetType("Mono.Unix.Native.MmapProts");
            object obj1 = Enum.ToObject(type1, (int)type1.GetField("PROT_READ").GetValue((object)null) | (int)type1.GetField("PROT_WRITE").GetValue((object)null) | (int)type1.GetField("PROT_EXEC").GetValue((object)null));
            Type type2 = assembly.GetType("Mono.Unix.Native.MmapFlags");
            object obj2 = Enum.ToObject(type2, (int)type2.GetField("MAP_ANONYMOUS").GetValue((object)null) | (int)type2.GetField("MAP_PRIVATE").GetValue((object)null));
            Opcode.codeBuffer = (IntPtr)method.Invoke((object)null, new object[6]
            {
          (object) IntPtr.Zero,
          (object) Opcode.size,
          obj1,
          obj2,
          (object) -1,
          (object) 0
            });
        }
        else
            Opcode.codeBuffer = Opcode.NativeMethods.VirtualAlloc(IntPtr.Zero, (UIntPtr)Opcode.size, Opcode.AllocationType.COMMIT | Opcode.AllocationType.RESERVE, Opcode.MemoryProtection.EXECUTE_READWRITE);
        Marshal.Copy(source1, 0, Opcode.codeBuffer, source1.Length);
        Opcode.Rdtsc = Marshal.GetDelegateForFunctionPointer(Opcode.codeBuffer, typeof(Opcode.RdtscDelegate)) as Opcode.RdtscDelegate;
        IntPtr num = (IntPtr)((long)Opcode.codeBuffer + (long)source1.Length);
        Marshal.Copy(source2, 0, num, source2.Length);
        Opcode.Cpuid = Marshal.GetDelegateForFunctionPointer(num, typeof(Opcode.CpuidDelegate)) as Opcode.CpuidDelegate;
    }

    public static void Close()
    {
        Opcode.Rdtsc = (Opcode.RdtscDelegate)null;
        Opcode.Cpuid = (Opcode.CpuidDelegate)null;
        if (OperatingSystem.IsUnix)
            Assembly.Load("Mono.Posix, Version=2.0.0.0, Culture=neutral, PublicKeyToken=0738eb9f132ed756").GetType("Mono.Unix.Native.Syscall").GetMethod("munmap").Invoke((object)null, new object[2]
            {
          (object) Opcode.codeBuffer,
          (object) Opcode.size
            });
        else
            Opcode.NativeMethods.VirtualFree(Opcode.codeBuffer, UIntPtr.Zero, Opcode.FreeType.RELEASE);
    }

    public static bool CpuidTx(
      uint index,
      uint ecxValue,
      out uint eax,
      out uint ebx,
      out uint ecx,
      out uint edx,
      GroupAffinity affinity)
    {
        GroupAffinity affinity1 = ThreadAffinity.Set(affinity);
        if (affinity1 == GroupAffinity.Undefined)
        {
            eax = ebx = ecx = edx = 0U;
            return false;
        }
        int num = Opcode.Cpuid(index, ecxValue, out eax, out ebx, out ecx, out edx) ? 1 : 0;
        ThreadAffinity.Set(affinity1);
        return true;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate ulong RdtscDelegate();

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate bool CpuidDelegate(
      uint index,
      uint ecxValue,
      out uint eax,
      out uint ebx,
      out uint ecx,
      out uint edx);

    [Flags]
    public enum AllocationType : uint
    {
        COMMIT = 4096, // 0x00001000
        RESERVE = 8192, // 0x00002000
        RESET = 524288, // 0x00080000
        LARGE_PAGES = 536870912, // 0x20000000
        PHYSICAL = 4194304, // 0x00400000
        TOP_DOWN = 1048576, // 0x00100000
        WRITE_WATCH = 2097152, // 0x00200000
    }

    [Flags]
    public enum MemoryProtection : uint
    {
        EXECUTE = 16, // 0x00000010
        EXECUTE_READ = 32, // 0x00000020
        EXECUTE_READWRITE = 64, // 0x00000040
        EXECUTE_WRITECOPY = 128, // 0x00000080
        NOACCESS = 1,
        READONLY = 2,
        READWRITE = 4,
        WRITECOPY = 8,
        GUARD = 256, // 0x00000100
        NOCACHE = 512, // 0x00000200
        WRITECOMBINE = 1024, // 0x00000400
    }

    [Flags]
    public enum FreeType
    {
        DECOMMIT = 16384, // 0x00004000
        RELEASE = 32768, // 0x00008000
    }

    private static class NativeMethods
    {
        private const string KERNEL = "kernel32.dll";

        [DllImport("kernel32.dll")]
        public static extern IntPtr VirtualAlloc(
          IntPtr lpAddress,
          UIntPtr dwSize,
          Opcode.AllocationType flAllocationType,
          Opcode.MemoryProtection flProtect);

        [DllImport("kernel32.dll")]
        public static extern bool VirtualFree(
          IntPtr lpAddress,
          UIntPtr dwSize,
          Opcode.FreeType dwFreeType);
    }
}
internal static class GetMaintainedSettings
{
    private static readonly Dictionary<Cpu.CodeName, SMU> settings = new Dictionary<Cpu.CodeName, SMU>()
    {
      {
        Cpu.CodeName.BristolRidge,
        (SMU) new BristolRidgeSettings()
      },
      {
        Cpu.CodeName.SummitRidge,
        (SMU) new SummitRidgeSettings()
      },
      {
        Cpu.CodeName.Naples,
        (SMU) new SummitRidgeSettings()
      },
      {
        Cpu.CodeName.Whitehaven,
        (SMU) new SummitRidgeSettings()
      },
      {
        Cpu.CodeName.PinnacleRidge,
        (SMU) new ZenPSettings()
      },
      {
        Cpu.CodeName.Colfax,
        (SMU) new ColfaxSettings()
      },
      {
        Cpu.CodeName.Matisse,
        (SMU) new Zen2Settings()
      },
      {
        Cpu.CodeName.CastlePeak,
        (SMU) new Zen2Settings()
      },
      {
        Cpu.CodeName.Rome,
        (SMU) new RomeSettings()
      },
      {
        Cpu.CodeName.Vermeer,
        (SMU) new Zen3Settings()
      },
      {
        Cpu.CodeName.Chagall,
        (SMU) new Zen3Settings()
      },
      {
        Cpu.CodeName.Milan,
        (SMU) new Zen3Settings()
      },
      {
        Cpu.CodeName.Raphael,
        (SMU) new Zen4Settings()
      },
      {
        Cpu.CodeName.Genoa,
        (SMU) new Zen4Settings()
      },
      {
        Cpu.CodeName.StormPeak,
        (SMU) new Zen4Settings()
      },
      {
        Cpu.CodeName.RavenRidge,
        (SMU) new APUSettings0()
      },
      {
        Cpu.CodeName.Dali,
        (SMU) new APUSettings0()
      },
      {
        Cpu.CodeName.FireFlight,
        (SMU) new APUSettings0()
      },
      {
        Cpu.CodeName.Picasso,
        (SMU) new APUSettings0_Picasso()
      },
      {
        Cpu.CodeName.Renoir,
        (SMU) new APUSettings1()
      },
      {
        Cpu.CodeName.Lucienne,
        (SMU) new APUSettings1()
      },
      {
        Cpu.CodeName.Cezanne,
        (SMU) new APUSettings1_Cezanne()
      },
      {
        Cpu.CodeName.VanGogh,
        (SMU) new APUSettings1()
      },
      {
        Cpu.CodeName.Rembrandt,
        (SMU) new APUSettings1_Rembrandt()
      },
      {
        Cpu.CodeName.Phoenix,
        (SMU) new APUSettings1_Rembrandt()
      },
      {
        Cpu.CodeName.Mendocino,
        (SMU) new APUSettings1_Rembrandt()
      },
      {
        Cpu.CodeName.Unsupported,
        (SMU) new UnsupportedSettings()
      }
    };

    public static SMU GetByType(Cpu.CodeName type)
    {
        SMU smu;
        return !GetMaintainedSettings.settings.TryGetValue(type, out smu) ? (SMU)new UnsupportedSettings() : smu;
    }
}
public class BristolRidgeSettings : SMU
{
    public BristolRidgeSettings()
    {
        this.SMU_TYPE = SMU.SmuType.TYPE_CPU9;
        this.SMU_OFFSET_ADDR = 184U;
        this.SMU_OFFSET_DATA = 188U;
        this.Rsmu.SMU_ADDR_MSG = 318767104U;
        this.Rsmu.SMU_ADDR_RSP = 318767120U;
        this.Rsmu.SMU_ADDR_ARG = 318767136U;
    }
}
public class SummitRidgeSettings : SMU
{
    public SummitRidgeSettings()
    {
        this.SMU_TYPE = SMU.SmuType.TYPE_CPU0;
        this.Rsmu.SMU_ADDR_MSG = 61932828U;
        this.Rsmu.SMU_ADDR_RSP = 61932904U;
        this.Rsmu.SMU_ADDR_ARG = 61932944U;
        this.Rsmu.SMU_MSG_TransferTableToDram = 10U;
        this.Rsmu.SMU_MSG_GetDramBaseAddress = 12U;
        this.Rsmu.SMU_MSG_EnableOcMode = 99U;
        this.Mp1Smu.SMU_ADDR_MSG = 61932840U;
        this.Mp1Smu.SMU_ADDR_RSP = 61932900U;
        this.Mp1Smu.SMU_ADDR_ARG = 61932952U;
        this.Mp1Smu.SMU_MSG_EnableOcMode = 35U;
        this.Mp1Smu.SMU_MSG_DisableOcMode = 36U;
        this.Mp1Smu.SMU_MSG_SetOverclockFrequencyAllCores = 38U;
        this.Mp1Smu.SMU_MSG_SetOverclockFrequencyPerCore = 39U;
        this.Mp1Smu.SMU_MSG_SetOverclockCpuVid = 40U;
    }
}
public class ZenPSettings : SMU
{
    public ZenPSettings()
    {
        this.SMU_TYPE = SMU.SmuType.TYPE_CPU1;
        this.Rsmu.SMU_ADDR_MSG = 61932828U;
        this.Rsmu.SMU_ADDR_RSP = 61932904U;
        this.Rsmu.SMU_ADDR_ARG = 61932944U;
        this.Rsmu.SMU_MSG_TransferTableToDram = 10U;
        this.Rsmu.SMU_MSG_GetDramBaseAddress = 12U;
        this.Rsmu.SMU_MSG_EnableOcMode = 107U;
        this.Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 108U;
        this.Rsmu.SMU_MSG_SetOverclockFrequencyPerCore = 109U;
        this.Rsmu.SMU_MSG_SetOverclockCpuVid = 110U;
        this.Rsmu.SMU_MSG_SetPPTLimit = 100U;
        this.Rsmu.SMU_MSG_SetTDCVDDLimit = 101U;
        this.Rsmu.SMU_MSG_SetEDCVDDLimit = 102U;
        this.Rsmu.SMU_MSG_SetHTCLimit = 104U;
        this.Rsmu.SMU_MSG_SetPBOScalar = 106U;
        this.Rsmu.SMU_MSG_GetPBOScalar = 111U;
        this.Mp1Smu.SMU_ADDR_MSG = 61932840U;
        this.Mp1Smu.SMU_ADDR_RSP = 61932900U;
        this.Mp1Smu.SMU_ADDR_ARG = 61932952U;
    }
}
public class ColfaxSettings : SMU
{
    public ColfaxSettings()
    {
        this.SMU_TYPE = SMU.SmuType.TYPE_CPU1;
        this.Rsmu.SMU_ADDR_MSG = 61932828U;
        this.Rsmu.SMU_ADDR_RSP = 61932904U;
        this.Rsmu.SMU_ADDR_ARG = 61932944U;
        this.Rsmu.SMU_MSG_TransferTableToDram = 10U;
        this.Rsmu.SMU_MSG_GetDramBaseAddress = 12U;
        this.Rsmu.SMU_MSG_EnableOcMode = 99U;
        this.Rsmu.SMU_MSG_DisableOcMode = 100U;
        this.Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 104U;
        this.Rsmu.SMU_MSG_SetOverclockFrequencyPerCore = 105U;
        this.Rsmu.SMU_MSG_SetOverclockCpuVid = 106U;
        this.Rsmu.SMU_MSG_SetTDCVDDLimit = 107U;
        this.Rsmu.SMU_MSG_SetEDCVDDLimit = 108U;
        this.Rsmu.SMU_MSG_SetHTCLimit = 110U;
        this.Rsmu.SMU_MSG_SetPBOScalar = 111U;
        this.Rsmu.SMU_MSG_GetPBOScalar = 112U;
        this.Mp1Smu.SMU_ADDR_MSG = 61932840U;
        this.Mp1Smu.SMU_ADDR_RSP = 61932900U;
        this.Mp1Smu.SMU_ADDR_ARG = 61932952U;
    }
}
public class Zen2Settings : SMU
{
    public Zen2Settings()
    {
        this.SMU_TYPE = SMU.SmuType.TYPE_CPU2;
        this.Rsmu.SMU_ADDR_MSG = 61932836U;
        this.Rsmu.SMU_ADDR_RSP = 61932912U;
        this.Rsmu.SMU_ADDR_ARG = 61934144U;
        this.Rsmu.SMU_MSG_TransferTableToDram = 5U;
        this.Rsmu.SMU_MSG_GetDramBaseAddress = 6U;
        this.Rsmu.SMU_MSG_GetTableVersion = 8U;
        this.Rsmu.SMU_MSG_EnableOcMode = 90U;
        this.Rsmu.SMU_MSG_DisableOcMode = 91U;
        this.Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 92U;
        this.Rsmu.SMU_MSG_SetOverclockFrequencyPerCore = 93U;
        this.Rsmu.SMU_MSG_SetOverclockCpuVid = 97U;
        this.Rsmu.SMU_MSG_SetPPTLimit = 83U;
        this.Rsmu.SMU_MSG_SetTDCVDDLimit = 84U;
        this.Rsmu.SMU_MSG_SetEDCVDDLimit = 85U;
        this.Rsmu.SMU_MSG_SetHTCLimit = 86U;
        this.Rsmu.SMU_MSG_GetFastestCoreofSocket = 89U;
        this.Rsmu.SMU_MSG_SetPBOScalar = 88U;
        this.Rsmu.SMU_MSG_GetPBOScalar = 108U;
        this.Rsmu.SMU_MSG_ReadBoostLimit = 110U;
        this.Mp1Smu.SMU_ADDR_MSG = 61932848U;
        this.Mp1Smu.SMU_ADDR_RSP = 61932924U;
        this.Mp1Smu.SMU_ADDR_ARG = 61934020U;
        this.Mp1Smu.SMU_MSG_SetToolsDramAddress = 6U;
        this.Mp1Smu.SMU_MSG_EnableOcMode = 36U;
        this.Mp1Smu.SMU_MSG_DisableOcMode = 37U;
        this.Mp1Smu.SMU_MSG_SetOverclockFrequencyPerCore = 39U;
        this.Mp1Smu.SMU_MSG_SetOverclockCpuVid = 40U;
        this.Mp1Smu.SMU_MSG_SetPBOScalar = 47U;
        this.Mp1Smu.SMU_MSG_SetEDCVDDLimit = 60U;
        this.Mp1Smu.SMU_MSG_SetTDCVDDLimit = 59U;
        this.Mp1Smu.SMU_MSG_SetPPTLimit = 61U;
        this.Mp1Smu.SMU_MSG_SetHTCLimit = 62U;
        this.Hsmp.SMU_ADDR_MSG = 61932852U;
        this.Hsmp.SMU_ADDR_RSP = 61933952U;
        this.Hsmp.SMU_ADDR_ARG = 61934048U;
    }
}
public class RomeSettings : SMU
{
    public RomeSettings()
    {
        this.SMU_TYPE = SMU.SmuType.TYPE_CPU2;
        this.Rsmu.SMU_ADDR_MSG = 61932836U;
        this.Rsmu.SMU_ADDR_RSP = 61932912U;
        this.Rsmu.SMU_ADDR_ARG = 61934144U;
        this.Rsmu.SMU_MSG_TransferTableToDram = 5U;
        this.Rsmu.SMU_MSG_GetDramBaseAddress = 6U;
        this.Rsmu.SMU_MSG_GetTableVersion = 8U;
        this.Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 24U;
        this.Rsmu.SMU_MSG_SetOverclockCpuVid = 18U;
        this.Mp1Smu.SMU_ADDR_MSG = 61932848U;
        this.Mp1Smu.SMU_ADDR_RSP = 61932924U;
        this.Mp1Smu.SMU_ADDR_ARG = 61934020U;
    }
}
public class Zen3Settings : Zen2Settings
{
    public Zen3Settings()
    {
        this.SMU_TYPE = SMU.SmuType.TYPE_CPU3;
        this.Rsmu.SMU_MSG_SetDldoPsmMargin = 10U;
        this.Rsmu.SMU_MSG_SetAllDldoPsmMargin = 11U;
        this.Rsmu.SMU_MSG_GetDldoPsmMargin = 124U;
        this.Mp1Smu.SMU_MSG_SetDldoPsmMargin = 53U;
        this.Mp1Smu.SMU_MSG_SetAllDldoPsmMargin = 54U;
        this.Mp1Smu.SMU_MSG_GetDldoPsmMargin = 72U;
    }
}
public class Zen4Settings : Zen3Settings
{
    public Zen4Settings()
    {
        this.SMU_TYPE = SMU.SmuType.TYPE_CPU4;
        this.Mp1Smu.SMU_MSG_SetTDCVDDLimit = 60U;
        this.Mp1Smu.SMU_MSG_SetEDCVDDLimit = 61U;
        this.Mp1Smu.SMU_MSG_SetPPTLimit = 62U;
        this.Mp1Smu.SMU_MSG_SetHTCLimit = 63U;
        this.Rsmu.SMU_ADDR_MSG = 61932836U;
        this.Rsmu.SMU_ADDR_RSP = 61932912U;
        this.Rsmu.SMU_ADDR_ARG = 61934144U;
        this.Rsmu.SMU_MSG_TransferTableToDram = 3U;
        this.Rsmu.SMU_MSG_GetDramBaseAddress = 4U;
        this.Rsmu.SMU_MSG_GetTableVersion = 5U;
        this.Rsmu.SMU_MSG_EnableOcMode = 93U;
        this.Rsmu.SMU_MSG_DisableOcMode = 94U;
        this.Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 95U;
        this.Rsmu.SMU_MSG_SetOverclockFrequencyPerCore = 96U;
        this.Rsmu.SMU_MSG_SetOverclockCpuVid = 97U;
        this.Rsmu.SMU_MSG_SetPPTLimit = 86U;
        this.Rsmu.SMU_MSG_SetTDCVDDLimit = 87U;
        this.Rsmu.SMU_MSG_SetEDCVDDLimit = 88U;
        this.Rsmu.SMU_MSG_SetHTCLimit = 89U;
        this.Rsmu.SMU_MSG_SetPBOScalar = 91U;
        this.Rsmu.SMU_MSG_GetPBOScalar = 109U;
        this.Rsmu.SMU_MSG_SetDldoPsmMargin = 6U;
        this.Rsmu.SMU_MSG_SetAllDldoPsmMargin = 7U;
        this.Rsmu.SMU_MSG_GetDldoPsmMargin = 213U;
        this.Rsmu.SMU_MSG_GetLN2Mode = 221U;
        this.Hsmp.SMU_ADDR_MSG = 61932852U;
        this.Hsmp.SMU_ADDR_RSP = 61933952U;
        this.Hsmp.SMU_ADDR_ARG = 61934048U;
    }
}
public class APUSettings0 : SMU
{
    public APUSettings0()
    {
        this.SMU_TYPE = SMU.SmuType.TYPE_APU0;
        this.Rsmu.SMU_ADDR_MSG = 61934112U;
        this.Rsmu.SMU_ADDR_RSP = 61934208U;
        this.Rsmu.SMU_ADDR_ARG = 61934216U;
        this.Rsmu.SMU_MSG_GetDramBaseAddress = 11U;
        this.Rsmu.SMU_MSG_GetTableVersion = 12U;
        this.Rsmu.SMU_MSG_TransferTableToDram = 61U;
        this.Rsmu.SMU_MSG_EnableOcMode = 105U;
        this.Rsmu.SMU_MSG_DisableOcMode = 106U;
        this.Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 125U;
        this.Rsmu.SMU_MSG_SetOverclockFrequencyPerCore = 126U;
        this.Rsmu.SMU_MSG_SetOverclockCpuVid = (uint)sbyte.MaxValue;
        this.Rsmu.SMU_MSG_GetPBOScalar = 104U;
        this.Mp1Smu.SMU_ADDR_MSG = 61932840U;
        this.Mp1Smu.SMU_ADDR_RSP = 61932900U;
        this.Mp1Smu.SMU_ADDR_ARG = 61933976U;
    }
}
public class APUSettings0_Picasso : APUSettings0
{
    public APUSettings0_Picasso() => this.Rsmu.SMU_MSG_GetPBOScalar = 98U;
}
public class APUSettings1 : SMU
{
    public APUSettings1()
    {
        this.SMU_TYPE = SMU.SmuType.TYPE_APU1;
        this.Rsmu.SMU_ADDR_MSG = 61934112U;
        this.Rsmu.SMU_ADDR_RSP = 61934208U;
        this.Rsmu.SMU_ADDR_ARG = 61934216U;
        this.Rsmu.SMU_MSG_GetTableVersion = 6U;
        this.Rsmu.SMU_MSG_TransferTableToDram = 101U;
        this.Rsmu.SMU_MSG_GetDramBaseAddress = 102U;
        this.Rsmu.SMU_MSG_EnableOcMode = 23U;
        this.Rsmu.SMU_MSG_DisableOcMode = 24U;
        this.Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 25U;
        this.Rsmu.SMU_MSG_SetOverclockFrequencyPerCore = 26U;
        this.Rsmu.SMU_MSG_SetOverclockCpuVid = 27U;
        this.Rsmu.SMU_MSG_SetPPTLimit = 51U;
        this.Rsmu.SMU_MSG_SetHTCLimit = 55U;
        this.Rsmu.SMU_MSG_SetTDCVDDLimit = 56U;
        this.Rsmu.SMU_MSG_SetTDCSOCLimit = 57U;
        this.Rsmu.SMU_MSG_SetEDCVDDLimit = 58U;
        this.Rsmu.SMU_MSG_SetEDCSOCLimit = 59U;
        this.Rsmu.SMU_MSG_SetPBOScalar = 63U;
        this.Rsmu.SMU_MSG_GetPBOScalar = 15U;
        this.Mp1Smu.SMU_ADDR_MSG = 61932840U;
        this.Mp1Smu.SMU_ADDR_RSP = 61932900U;
        this.Mp1Smu.SMU_ADDR_ARG = 61933976U;
        this.Mp1Smu.SMU_MSG_EnableOcMode = 47U;
        this.Mp1Smu.SMU_MSG_DisableOcMode = 48U;
        this.Mp1Smu.SMU_MSG_SetOverclockFrequencyPerCore = 50U;
        this.Mp1Smu.SMU_MSG_SetOverclockCpuVid = 51U;
        this.Mp1Smu.SMU_MSG_SetHTCLimit = 62U;
        this.Mp1Smu.SMU_MSG_SetPBOScalar = 73U;
    }
}
public class APUSettings1_Cezanne : APUSettings1
{
    public APUSettings1_Cezanne()
    {
        this.Rsmu.SMU_MSG_SetDldoPsmMargin = 82U;
        this.Rsmu.SMU_MSG_SetAllDldoPsmMargin = 177U;
        this.Rsmu.SMU_MSG_GetDldoPsmMargin = 195U;
        this.Rsmu.SMU_MSG_SetGpuPsmMargin = 83U;
        this.Rsmu.SMU_MSG_GetGpuPsmMargin = 198U;
    }
}
public class APUSettings1_Rembrandt : APUSettings1
{
    public APUSettings1_Rembrandt()
    {
        this.SMU_TYPE = SMU.SmuType.TYPE_APU2;
        this.Rsmu.SMU_MSG_SetPBOScalar = 62U;
        this.Rsmu.SMU_MSG_SetDldoPsmMargin = 83U;
        this.Rsmu.SMU_MSG_SetAllDldoPsmMargin = 93U;
        this.Rsmu.SMU_MSG_GetDldoPsmMargin = 47U;
        this.Rsmu.SMU_MSG_SetGpuPsmMargin = 183U;
        this.Rsmu.SMU_MSG_GetGpuPsmMargin = 48U;
        this.Mp1Smu.SMU_ADDR_MSG = 61932840U;
        this.Mp1Smu.SMU_ADDR_RSP = 61932920U;
        this.Mp1Smu.SMU_ADDR_ARG = 61933976U;
    }
}
public class UnsupportedSettings : SMU
{
    public UnsupportedSettings() => this.SMU_TYPE = SMU.SmuType.TYPE_UNSUPPORTED;
}
public class MailboxListItem
{
    public uint msgAddr
    {
        get;
    }
    public uint rspAddr
    {
        get;
    }
    public uint argAddr
    {
        get;
    }
    public string label
    {
        get;
    }

    public MailboxListItem(string label, SmuAddressSet addressSet)
    {
        this.label = label;
        msgAddr = addressSet.MsgAddress;
        rspAddr = addressSet.RspAddress;
        argAddr = addressSet.ArgAddress;
    }

    public MailboxListItem(string label, Mailbox mailbox)
    {
        this.label = label;
        msgAddr = mailbox.SMU_ADDR_MSG;
        rspAddr = mailbox.SMU_ADDR_RSP;
        argAddr = mailbox.SMU_ADDR_ARG;
    }

    public override string ToString()
    {
        return this.label;
    }
}