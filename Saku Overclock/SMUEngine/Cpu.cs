namespace Saku_Overclock.SMUEngine;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
public sealed class Cpu : IDisposable
{
    private bool disposedValue; 
    private readonly IOModule io = new();
    private readonly ACPI_MMIO mmio;
    public readonly CPUInfo Info;
    public readonly SystemInfo? SystemInfo;
    public readonly SMU Smu;
    public readonly PowerTable? PowerTable;
    [Obsolete("Obsolete")]
    private CpuTopology GetCpuTopology(Family family, CodeName codeName, uint model)
    {
        var cpuTopology = new CpuTopology();
        if (!Opcode.Cpuid(1U, 0U, out var eax, out var ebx, out var ecx, out _))
        {
            throw new ApplicationException("CPU module initialization failed.");
        } 
        cpuTopology.logicalCores = Utils.GetBits(ebx, 16, 8);
        if (!Opcode.Cpuid(2147483678U, 0U, out eax, out ebx, out ecx, out _))
        {
            throw new ApplicationException("CPU module initialization failed.");
        }

        cpuTopology.threadsPerCore = Utils.GetBits(ebx, 8, 4) + 1U;
        cpuTopology.cpuNodes = (uint)(((int)(ecx >> 8) & 7) + 1);
        cpuTopology.cores = cpuTopology.threadsPerCore != 0U ? cpuTopology.logicalCores / cpuTopology.threadsPerCore : cpuTopology.logicalCores;
        try
        {
            cpuTopology.performanceOfCore = new uint[(int)cpuTopology.cores];
            for (var index = 0; index < cpuTopology.logicalCores; index += (int)cpuTopology.threadsPerCore)
            {
                cpuTopology.performanceOfCore[index / cpuTopology.threadsPerCore] = !Ring0.RdmsrTx(3221291699U, out eax, out _, GroupAffinity.Single(0, index)) ? 0U : eax & byte.MaxValue;
            }
        }
        catch
        {
            // ignored
        }

        uint data1 = 0;
        uint data2 = 0;
        uint data3 = 0;
        uint addr1 = 381464;
        uint addr2 = 381468;
        uint num1 = 568;
        uint num2 = 2;
        switch (family)
        {
            case Family.FAMILY_17H:
                if (model != 113U && model != 49U)
                {
                    addr1 += 64U;
                    addr2 += 64U;
                }
                break;
            case Family.FAMILY_19H:
                num1 = 1432U;
                num2 = 1U;
                if (codeName == CodeName.Raphael)
                {
                    num1 = 1232U;
                    addr1 += 420U;
                    addr2 += 420U;
                }
                break;
        }
        if (ReadDwordEx(addr1, ref data1) && ReadDwordEx(addr2, ref data2))
        {
            var bits1 = Utils.GetBits(data1, 22, 8); 
            var addr3 = 805836800U + num1;
            var num3 = Utils.CountSetBits(bits1);
            cpuTopology.ccds = num3 > 0U ? num3 : 1U;
            cpuTopology.ccxs = cpuTopology.ccds * num2;
            cpuTopology.physicalCores = cpuTopology.ccxs * 8U / num2;
            if (ReadDwordEx(addr3, ref data3))
            {
                cpuTopology.coresPerCcx = (8U - Utils.CountSetBits(data3 & byte.MaxValue)) / num2;
            }
            else
            {
                Console.WriteLine("Could not read core fuse!");
            } 
            for (var offset = 0; offset < cpuTopology.ccds; ++offset)
            {
                if (Utils.GetBits(bits1, offset, 1) == 1U)
                {
                    if (ReadDwordEx((uint)(offset << 25) + addr3, ref data3))
                    {
                        cpuTopology.coreDisableMap |= (uint)(((int)data3 & byte.MaxValue) << offset * 8);
                    }
                    else
                    {
                        Console.WriteLine("Could not read core fuse for CCD{0}!", offset);
                    }
                }
            }
        }
        else
        {
            Console.WriteLine("Could not read CCD fuse!");
        } 
        return cpuTopology;
    } 
    [Obsolete("Obsolete")]
    public Cpu()
    {
        Ring0.Open();
        if (!Ring0.IsOpen)
        {
            var report = Ring0.GetReport();
            using var streamWriter = new StreamWriter("WinRing0.txt", true);
            streamWriter.Write(report);

            throw new ApplicationException("Error opening WinRing kernel driver");
        }
        Opcode.Open();
        mmio = new ACPI_MMIO(io);
        Info.vendor = GetVendor();
        if (Info.vendor != "AuthenticAMD" && Info.vendor != "HygonGenuine")
        {
            throw new Exception("Not an AMD CPU");
        }

        if (!Opcode.Cpuid(1U, 0U, out var eax, out var ebx, out _, out _))
        {
            throw new ApplicationException("CPU module initialization failed.");
        }

        Info.cpuid = eax;
        Info.family = (Family)((int)((eax & 3840U) >> 8) + (int)((eax & 267386880U) >> 20));
        Info.baseModel = (eax & 240U) >> 4;
        Info.extModel = (eax & 983040U) >> 12;
        Info.model = Info.baseModel + Info.extModel;
        Info.stepping = eax & 15U;
        Info.cpuName = GetCpuName();
        if (!Opcode.Cpuid(2147483649U, 0U, out eax, out ebx, out _, out _))
        {
            throw new ApplicationException("CPU module initialization failed.");
        }

        Info.packageType = (PackageType)(ebx >> 28);
        Info.codeName = GetCodeName(Info);
        Smu = GetMaintainedSettings.GetByType(Info.codeName);
        Smu.Hsmp.Init(this);
        Smu.Version = GetSmuVersion();
        Smu.TableVersion = GetTableVersion();
        try
        {
            Info.topology = GetCpuTopology(Info.family, Info.codeName, Info.model);
        }
        catch  
        {
            // ignored
        }

        try
        {
            Info.patchLevel = GetPatchLevel();
            Info.svi2 = GetSVI2Info(Info.codeName);
            Info.aod = new AOD(io, Info.codeName);
            SystemInfo = new SystemInfo(Info, Smu);
            PowerTable = new PowerTable(Smu, io, mmio);
            if (!SendTestMessage())
            {
            }
        }
        catch  
        {
            //Ignored
        }
    }

    public uint MakeCoreMask(uint core = 0, uint ccd = 0, uint ccx = 0)
    {
        var num1 = Info.family == Family.FAMILY_19H ? 1U : 2U;
        var num2 = 8U / num1;
        return (uint)((((int)ccd << 4 | (int)(ccx % num1) & 15) << 4 | (int)(core % num2) & 15) << 20);
    }

    [Obsolete("Obsolete")]
    public bool ReadDwordEx(uint addr, ref uint data)
    {
        var flag = false;
        if (!Ring0.WaitPciBusMutex(10))
        {
            return flag;
        }
        if (Ring0.WritePciConfig(Smu.SMU_PCI_ADDR, Smu.SMU_OFFSET_ADDR, addr))
        {
            flag = Ring0.ReadPciConfig(Smu.SMU_PCI_ADDR, Smu.SMU_OFFSET_DATA, out data);
        }
        Ring0.ReleasePciBusMutex();
        return flag;
    }
    [Obsolete("Obsolete")]
    public uint ReadDword(uint addr)
    {
        uint num = 0;
        if (!Ring0.WaitPciBusMutex(10))
        {
            return num;
        }
        Ring0.WritePciConfig(Smu.SMU_PCI_ADDR, (byte)Smu.SMU_OFFSET_ADDR, addr);
        Ring0.ReadPciConfig(Smu.SMU_PCI_ADDR, (byte)Smu.SMU_OFFSET_DATA, out num);
        Ring0.ReleasePciBusMutex();
        return num;
    }

    [Obsolete("Obsolete")]
    public bool WriteDwordEx(uint addr, uint data)
    {
        var flag = false;
        if (!Ring0.WaitPciBusMutex(10))
        {
            return flag;
        }
        if (Ring0.WritePciConfig(Smu.SMU_PCI_ADDR, (byte)Smu.SMU_OFFSET_ADDR, addr))
        {
            flag = Ring0.WritePciConfig(Smu.SMU_PCI_ADDR, (byte)Smu.SMU_OFFSET_DATA, data);
        }
        Ring0.ReleasePciBusMutex();
        return flag;
    }
    [Obsolete("Obsolete")]
    public double GetCoreMulti(int index = 0)
    {
        return !Ring0.RdmsrTx(3221291667U, out var eax, out var _, GroupAffinity.Single(0, index)) ? 0.0 : Math.Round((uint)(25 * ((int)eax & byte.MaxValue)) / (12.5 * (eax >> 8 & 63U)) * 4.0, MidpointRounding.ToEven) / 4.0;
    }
    public static bool Cpuid(uint index, ref uint eax, ref uint ebx, ref uint ecx, ref uint edx)
    {
        if (eax <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(eax));
        }
        return Opcode.Cpuid(index, 0U, out eax, out ebx, out ecx, out edx);
    }

    [Obsolete("Obsolete")]
    public static bool ReadMsr(uint index, ref uint eax, ref uint edx)
    { 
        return Ring0.Rdmsr(index, out eax, out edx);
    }

    [Obsolete("Obsolete")]
    public bool ReadMsrTx(uint index, ref uint eax, ref uint edx, int i)
    {
        var affinity = GroupAffinity.Single(0, i);
        return Ring0.RdmsrTx(index, out eax, out edx, affinity);
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
    [Obsolete("Obsolete")]
    public bool WriteMsr(uint msr, uint eax, uint edx)
    {
        var flag = true;
        for (var index = 0; index < Info.topology.logicalCores; ++index)
        {
            flag = Ring0.WrmsrTx(msr, eax, edx, GroupAffinity.Single(0, index));
        }

        return flag;
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
    [Obsolete("Obsolete")]
    public void WriteIoPort(uint port, byte value) => Ring0.WriteIoPort(port, value);

    [Obsolete("Obsolete")]
    public byte ReadIoPort(uint port) => Ring0.ReadIoPort(port);

    [Obsolete("Obsolete")]
    public bool ReadPciConfig(uint pciAddress, uint regAddress, ref uint value)
    {
        return Ring0.ReadPciConfig(pciAddress, regAddress, out value);
    }

    public uint GetPciAddress(byte bus, byte device, byte function)
    {
        return Ring0.GetPciAddress(bus, device, function);
    }

    public CodeName GetCodeName(CPUInfo cpuInfo)
    {
        var codeName = CodeName.Unsupported;
        switch (cpuInfo.family)
        {
            case Family.FAMILY_15H:
            {
                if (cpuInfo.model == 101U)
                {
                    codeName = CodeName.BristolRidge;
                }

                break;
            }
            case Family.FAMILY_17H:
                switch (cpuInfo.model)
                {
                    case 1:
                        codeName = cpuInfo.packageType != PackageType.SP3 ? (cpuInfo.packageType != PackageType.TRX ? CodeName.SummitRidge : CodeName.Whitehaven) : CodeName.Naples;
                        break;
                    case 8:
                        codeName = cpuInfo.packageType == PackageType.SP3 || cpuInfo.packageType == PackageType.TRX ? CodeName.Colfax : CodeName.PinnacleRidge;
                        break;
                    case 17:
                        codeName = CodeName.RavenRidge;
                        break;
                    case 24:
                        codeName = !Utils.PartialStringMatch(Info.cpuName, Constants.MISIDENTIFIED_DALI_APU) ? CodeName.Picasso : CodeName.Dali;
                        break;
                    case 32:
                        codeName = CodeName.Dali;
                        break;
                    case 49:
                        codeName = cpuInfo.packageType != PackageType.TRX ? CodeName.Rome : CodeName.CastlePeak;
                        break;
                    case 80:
                        codeName = CodeName.FireFlight;
                        break;
                    case 96:
                        codeName = CodeName.Renoir;
                        break;
                    case 104:
                        codeName = CodeName.Lucienne;
                        break;
                    case 113:
                        codeName = CodeName.Matisse;
                        break;
                    case 144:
                        codeName = CodeName.VanGogh;
                        break;
                    default:
                        codeName = CodeName.Unsupported;
                        break;
                }

                break;
            case Family.FAMILY_19H:
                switch (cpuInfo.model)
                {
                    case 1:
                        codeName = CodeName.Milan;
                        break;
                    case 8:
                        codeName = CodeName.Chagall;
                        break;
                    case 17:
                        codeName = CodeName.Genoa;
                        break;
                    case 24:
                        codeName = CodeName.StormPeak;
                        break;
                    case 33:
                        codeName = CodeName.Vermeer;
                        break;
                    case 68:
                        codeName = CodeName.Rembrandt;
                        break;
                    case 80:
                        codeName = CodeName.Cezanne;
                        break;
                    case 97:
                        codeName = CodeName.Raphael;
                        break;
                    case 116:
                    case 120:
                        codeName = CodeName.Phoenix;
                        break;
                    case 160:
                        codeName = CodeName.Mendocino;
                        break;
                    default:
                        codeName = CodeName.Unsupported;
                        break;
                }

                break;
        }
        return codeName;
    }

    public SVI2 GetSVI2Info(CodeName codeName)
    {
        var svI2Info = new SVI2();
        switch (codeName)
        {
            case CodeName.BristolRidge:
                return svI2Info;
            case CodeName.SummitRidge:
            case CodeName.RavenRidge:
            case CodeName.PinnacleRidge:
            case CodeName.FireFlight:
            case CodeName.Dali:
                svI2Info.coreAddress = 368652U;
                svI2Info.socAddress = 368656U;
                goto case CodeName.BristolRidge;
            case CodeName.Whitehaven:
            case CodeName.Naples:
            case CodeName.Colfax:
                svI2Info.coreAddress = 368656U;
                svI2Info.socAddress = 368652U;
                goto case CodeName.BristolRidge;
            case CodeName.Picasso:
                if ((Smu.Version & 4278190080U) > 0U)
                {
                    svI2Info.coreAddress = 368652U;
                    svI2Info.socAddress = 368656U;
                    goto case CodeName.BristolRidge;
                }

                svI2Info.coreAddress = 368656U;
                svI2Info.socAddress = 368652U;
                goto case CodeName.BristolRidge;
            case CodeName.Matisse:
                svI2Info.coreAddress = 368656U;
                svI2Info.socAddress = 368652U;
                goto case CodeName.BristolRidge;
            case CodeName.CastlePeak:
            case CodeName.Rome:
                svI2Info.coreAddress = 368660U;
                svI2Info.socAddress = 368656U;
                goto case CodeName.BristolRidge;
            case CodeName.Renoir:
            case CodeName.VanGogh:
            case CodeName.Cezanne:
            case CodeName.Rembrandt:
            case CodeName.Lucienne:
            case CodeName.Phoenix:
            case CodeName.Mendocino:
                svI2Info.coreAddress = 454712U;
                svI2Info.socAddress = 454716U;
                goto case CodeName.BristolRidge;
            case CodeName.Vermeer:
            case CodeName.Raphael:
                svI2Info.coreAddress = 368656U;
                svI2Info.socAddress = 368652U;
                goto case CodeName.BristolRidge;
            case CodeName.Chagall:
            case CodeName.Milan:
                svI2Info.coreAddress = 368660U;
                svI2Info.socAddress = 368656U;
                goto case CodeName.BristolRidge;
            default:
                svI2Info.coreAddress = 368652U;
                svI2Info.socAddress = 368656U;
                goto case CodeName.BristolRidge;
        }
    }

    public string GetVendor()
    {
        return Opcode.Cpuid(0U, 0U, out _, out var ebx, out var ecx, out var edx) ? Utils.IntToStr(ebx) + Utils.IntToStr(edx) + Utils.IntToStr(ecx) : "";
    }

    public string GetCpuName()
    {
        var str = "";
        if (Opcode.Cpuid(2147483650U, 0U, out var eax, out var ebx, out var ecx, out var edx))
        {
            str = str + Utils.IntToStr(eax) + Utils.IntToStr(ebx) + Utils.IntToStr(ecx) + Utils.IntToStr(edx);
        }

        if (Opcode.Cpuid(2147483651U, 0U, out eax, out ebx, out ecx, out edx))
        {
            str = str + Utils.IntToStr(eax) + Utils.IntToStr(ebx) + Utils.IntToStr(ecx) + Utils.IntToStr(edx);
        }

        if (Opcode.Cpuid(2147483652U, 0U, out eax, out ebx, out ecx, out edx))
        {
            str = str + Utils.IntToStr(eax) + Utils.IntToStr(ebx) + Utils.IntToStr(ecx) + Utils.IntToStr(edx);
        }

        return str.Trim();
    }
    [Obsolete("Obsolete")]
    public uint GetPatchLevel()
    {
        return Ring0.Rdmsr(139U, out var eax, out var _) ? eax : 0U;
    }
    [Obsolete("Obsolete")]
    public bool GetOcMode()
    {
        return Info.codeName == CodeName.SummitRidge ? Ring0.Rdmsr(3221291107U, out var eax, out var _) && Convert.ToBoolean(eax >> 1 & 1U) : Info.family != Family.FAMILY_15H && Equals(GetPBOScalar(), 0.0f);
    } 
    public float GetPBOScalar()
    {
        var getPboScalar = new GetPBOScalar(Smu);
        getPboScalar.Execute();
        return getPboScalar.Scalar;
    }

    public bool SendTestMessage(uint arg = 1, Mailbox mbox = null!)
    {
        var sendTestMessage = new SendTestMessage(Smu, mbox);
        return sendTestMessage.Execute(arg).Success && sendTestMessage.IsSumCorrect;
    }

    public uint GetSmuVersion() => new GetSmuVersion(Smu).Execute().args[0];

    public double? GetBclk() => mmio.GetBclk();

    public bool SetBclk(double blck) => mmio.SetBclk(blck);

    public SMU.Status TransferTableToDram() => new TransferTableToDram(Smu).Execute().status;

    public uint GetTableVersion() => new GetTableVersion(Smu).Execute().args[0];

    public uint GetDramBaseAddress() => new GetDramAddress(Smu).Execute().args[0];

    public long GetDramBaseAddress64()
    {
        var cmdResult = new GetDramAddress(Smu).Execute();
        return (long)cmdResult.args[1] << 32 | cmdResult.args[0];
    }

    public bool GetLN2Mode() => new GetLN2Mode(Smu).Execute().args[0] == 1U;

    public SMU.Status SetPPTLimit(uint arg = 0)
    {
        return new SetSmuLimit(Smu).Execute(Smu.Rsmu.SMU_MSG_SetPPTLimit, arg).status;
    }

    public SMU.Status SetEDCVDDLimit(uint arg = 0)
    {
        return new SetSmuLimit(Smu).Execute(Smu.Rsmu.SMU_MSG_SetEDCVDDLimit, arg).status;
    }

    public SMU.Status SetEDCSOCLimit(uint arg = 0)
    {
        return new SetSmuLimit(Smu).Execute(Smu.Rsmu.SMU_MSG_SetEDCSOCLimit, arg).status;
    }

    public SMU.Status SetTDCVDDLimit(uint arg = 0)
    {
        return new SetSmuLimit(Smu).Execute(Smu.Rsmu.SMU_MSG_SetTDCVDDLimit, arg).status;
    }

    public SMU.Status SetTDCSOCLimit(uint arg = 0)
    {
        return new SetSmuLimit(Smu).Execute(Smu.Rsmu.SMU_MSG_SetTDCSOCLimit, arg).status;
    }

    public SMU.Status SetOverclockCpuVid(byte arg)
    {
        return new SetOverclockCpuVid(Smu).Execute(arg).status;
    }

    public SMU.Status EnableOcMode() => new SetOcMode(Smu).Execute(true).status;

    public SMU.Status DisableOcMode() => new SetOcMode(Smu).Execute(false).status;

    public SMU.Status SetPBOScalar(uint scalar)
    {
        return new SetPBOScalar(Smu).Execute(scalar).status;
    }

    public SMU.Status RefreshPowerTable()
    {
        return PowerTable?.Refresh() ?? SMU.Status.FAILED;
    }

    public int? GetPsmMarginSingleCore(uint coreMask)
    {
        var cmdResult = new GetPsmMarginSingleCore(Smu).Execute(coreMask);
        return !cmdResult.Success ? new int?() : (int)cmdResult.args[0];
    }

    public int? GetPsmMarginSingleCore(uint core, uint ccd, uint ccx)
    {
        return GetPsmMarginSingleCore(MakeCoreMask(core, ccd, ccx));
    }

    public bool SetPsmMarginAllCores(int margin)
    {
        return new SetPsmMarginAllCores(Smu).Execute(margin).Success;
    }

    public bool SetPsmMarginSingleCore(uint coreMask, int margin)
    {
        return new SetPsmMarginSingleCore(Smu).Execute(coreMask, margin).Success;
    }

    public bool SetPsmMarginSingleCore(uint core, uint ccd, uint ccx, int margin)
    {
        return SetPsmMarginSingleCore(MakeCoreMask(core, ccd, ccx), margin);
    }

    public bool SetFrequencyAllCore(uint frequency)
    {
        return new SetFrequencyAllCore(Smu).Execute(frequency).Success;
    }

    public bool SetFrequencySingleCore(uint coreMask, uint frequency)
    {
        return new SetFrequencySingleCore(Smu).Execute(coreMask, frequency).Success;
    }

    public bool SetFrequencySingleCore(uint core, uint ccd, uint ccx, uint frequency)
    {
        return SetFrequencySingleCore(MakeCoreMask(core, ccd, ccx), frequency);
    }

    private bool SetFrequencyMultipleCores(uint mask, uint frequency, int count)
    {
        for (uint newVal = 0; newVal < count; ++newVal)
        {
            mask = Utils.SetBits(mask, 20, 2, newVal);
            if (!SetFrequencySingleCore(mask, frequency))
            {
                return false;
            }
        }
        return true;
    }

    public bool SetFrequencyCCX(uint mask, uint frequency)
    {
        return SetFrequencyMultipleCores(mask, frequency, 8);
    }

    public bool SetFrequencyCCD(uint mask, uint frequency)
    {
        var flag = true;
        for (uint newVal = 0; newVal < SystemInfo!.CCXCount / SystemInfo.CCDCount; ++newVal)
        {
            mask = Utils.SetBits(mask, 24, 1, newVal);
            flag = SetFrequencyCCX(mask, frequency);
        }
        return flag;
    }
    [Obsolete("Obsolete")]
    public bool IsProchotEnabled() => ((int)ReadDword(366596U) & 1) == 1;
    [Obsolete("Obsolete")]
    public float? GetCpuTemperature()
    {
        uint data = 0;
        if (!ReadDwordEx(366592U, ref data))
        {
            return new float?();
        }

        var num1 = 0.0f;
        if (Info.cpuName.Contains("2700X"))
        {
            num1 = -10f;
        }
        else if (Info.cpuName.Contains("1600X") || Info.cpuName.Contains("1700X") || Info.cpuName.Contains("1800X"))
        {
            num1 = -20f;
        }
        else if (Info.cpuName.Contains("Threadripper 19") || Info.cpuName.Contains("Threadripper 29"))
        {
            num1 = -27f;
        }

        var num2 = (data >> 21) * 0.125f + num1;
        if (((int)data & 524288) != 0)
        {
            num2 -= 49f;
        }

        return num2;
    }
    [Obsolete("Obsolete")]
    public float? GetSingleCcdTemperature(uint ccd)
    {
        uint data = 0;
        if (!ReadDwordEx((uint)(366932 + (int)ccd * 4), ref data))
        {
            return new float?();
        }

        var num = (float)((data & 4095U) * 0.125 - 305.0);
        return num > 0.0 && num < 125.0 ? num : 0.0f;
    }
    [Obsolete("Obsolete")]
    private void Dispose(bool disposing)
    {
        if (disposedValue)
        {
            return;
        }

        if (disposing)
        {
            io.Dispose();
            Ring0.Close();
            Opcode.Close();
        }
        disposedValue = true;
    }
    [Obsolete("Obsolete")]
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
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
        public AOD aod;
    }
} 