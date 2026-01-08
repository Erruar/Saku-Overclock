using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Models;
using Saku_Overclock.SmuEngine;
using Saku_Overclock.Views.Windows.TaskDialog;
using ZenStates.Core;
using static ZenStates.Core.Cpu;

namespace Saku_Overclock.Services;
public class CpuService : ICpuService
{
    private readonly Cpu? _cpu;
    private readonly CodeName _codeName;
    private readonly bool _isBatteryUnavailable = true;
    private bool _unsupportedPlatformMessage;

    public bool IsAvailable
    {
        get;
    }

    public CpuService()
    {
        try
        {
            if (!PawnIo.IsInstalled)
            {
                var powerWindow = new TaskDialog();
                powerWindow.Activate();
            }
            else
            {
                _cpu = new Cpu();
                _codeName = _cpu.info.codeName;
            }
            

            GetSystemInfo.ReadDesignCapacity(out var notTrack);
            _isBatteryUnavailable = notTrack;
            IsAvailable = true;
        }
        catch
        {
            IsAvailable = false;
        }
    }

    /// <summary>
    ///  Возвращает тип платформы
    /// </summary>
    public bool? IsPlatformPc()
    {
        if (IsPlatformPcByCodename() == true)
        {
            if (_codeName is CodeName.RavenRidge or CodeName.Picasso or CodeName.Renoir or CodeName.Cezanne or CodeName.Phoenix or CodeName.Phoenix2)
            {
                if (_cpu?.info.packageType == PackageType.FPX)
                {
                    if (_isBatteryUnavailable)
                    {
                        if (_cpu.info.cpuName.Contains('G') ||
                            _cpu.info.cpuName.Contains("GE") ||
                            (_cpu.info.cpuName.Contains('X') && !_cpu.info.cpuName.Contains("HX")) ||
                            _cpu.info.cpuName.Contains('F') ||
                            (_cpu.info.cpuName.Contains("X3D") && !_cpu.info.cpuName.Contains("HX3D")) ||
                            _cpu.info.cpuName.Contains("XT")
                           )
                        {
                            return true;
                        }

                        return false;
                    }
                    return false;
                }

                return true;
            }
            return true;
        }
        return null; // Платформа не определена!
    }

    /// <summary>
    ///  Возвращает тип платформы по кодовому имени
    /// </summary>
    public bool? IsPlatformPcByCodename()
    {
        return _codeName switch
        {
            CodeName.BristolRidge or CodeName.SummitRidge or CodeName.PinnacleRidge => true,
            CodeName.RavenRidge or CodeName.Picasso or CodeName.Dali or CodeName.FireFlight => false, // Raven Ridge, Picasso может быть PC!
            CodeName.Matisse or CodeName.Vermeer => true,
            CodeName.Renoir or CodeName.Lucienne or CodeName.Cezanne => false, // Renoir, Cezanne может быть PC!
            CodeName.VanGogh => false,
            CodeName.KrackanPoint or CodeName.KrackanPoint2 => false,
            CodeName.Mendocino or CodeName.Rembrandt or CodeName.Phoenix or CodeName.Phoenix2 or CodeName.HawkPoint or CodeName.StrixPoint or CodeName.StrixHalo => false, // Phoenix может быть PC!
            CodeName.GraniteRidge or CodeName.Genoa or CodeName.Bergamo or CodeName.Raphael or CodeName.DragonRange => true,
            _ => null,// Устройство не определено
        };
    }

    public enum SmuStatus : byte
    {
        Ok = 1,
        Failed = byte.MaxValue,
        UnknownCmd = 254,
        CmdRejectedPrereq = 253,
        CmdRejectedBusy = 252,
        TimeoutMutexLock = 48,
        TimeoutMailboxReady = 49,
        TimeoutMailboxMsgWrite = 50,
        PciFailed = 51
    }

    public SmuStatus SendSmuCommand(SmuAddressSet mailbox, uint command, ref uint[] arguments)
    {
        if (_cpu == null)
        {
            return SmuStatus.TimeoutMutexLock;
        }

        var normalizedMailbox = new Mailbox
        {
            SMU_ADDR_MSG = mailbox.MsgAddress,
            SMU_ADDR_RSP = mailbox.RspAddress,
            SMU_ADDR_ARG = mailbox.ArgAddress
        };

        return (SmuStatus)(byte)_cpu.smu.SendSmuCommand(normalizedMailbox, command, ref arguments);
    }

    public enum CodenameGeneration
    {
        Unknown,
        Fp4,
        Fp5,
        Fp6,
        Ff3,
        Fp7,
        Fp8,
        Am4V1,
        Am4V2,
        Am5,
    }

    public CodenameGeneration GetCodenameGeneration()
    {
        switch (_codeName)
        {
            case CodeName.BristolRidge:
                return CodenameGeneration.Fp4;
            case CodeName.SummitRidge:
            case CodeName.PinnacleRidge:
                return CodenameGeneration.Am4V1;
            case CodeName.RavenRidge:
            case CodeName.Picasso:
            case CodeName.Dali:
            case CodeName.FireFlight:
                return CodenameGeneration.Fp5;
            case CodeName.Matisse:
            case CodeName.Vermeer:
                return CodenameGeneration.Am4V2;
            case CodeName.Renoir:
            case CodeName.Lucienne:
            case CodeName.Cezanne:
                return CodenameGeneration.Fp6;
            case CodeName.VanGogh:
                return CodenameGeneration.Ff3;
            case CodeName.Mendocino:
            case CodeName.Rembrandt:
            case CodeName.Phoenix:
            case CodeName.Phoenix2:
            case CodeName.HawkPoint:
            case CodeName.KrackanPoint:
            case CodeName.KrackanPoint2:
                return CodenameGeneration.Fp7;
            case CodeName.StrixPoint:
            case CodeName.StrixHalo:
                return CodenameGeneration.Fp8;
            case CodeName.Raphael:
            case CodeName.GraniteRidge:
            case CodeName.Genoa:
            case CodeName.StormPeak:
            case CodeName.DragonRange:
            case CodeName.Bergamo:
                return CodenameGeneration.Am5;
            default:
                if (!_unsupportedPlatformMessage)
                {
                    _unsupportedPlatformMessage = true;
                    LogHelper.TraceIt_TraceError("UnsupportedPlatformMessage".GetLocalized());
                }

                break;
        }
        return CodenameGeneration.Unknown;
    }

    public bool IsRaven => _codeName == CodeName.RavenRidge;
    public bool IsDragonRange => _codeName == CodeName.DragonRange;
    public uint PhysicalCores => GetCpuPhysicalCores();

    private uint GetCpuPhysicalCores()
    {
        var processorCount = (uint)Environment.ProcessorCount;
        try
        {
            return _cpu?.info.topology.physicalCores ?? processorCount;
        }
        catch
        {
            return processorCount;
        }
    }
    public uint[] CoreDisableMap => GetCoreDisableMap();
    private uint[] GetCoreDisableMap()
    {
        try
        {
            return _cpu?.info.topology.coreDisableMap ?? [];
        }
        catch
        {
            return [];
        }
    }
    public uint Cores => GetCpuCores();
    private uint GetCpuCores()
    {
        var processorCount = (uint)Environment.ProcessorCount;
        try
        {
            return _cpu?.info.topology.cores ?? processorCount;
        }
        catch
        {
            return processorCount;
        }
    }

    public SmuAddressSet Rsmu => new(_cpu?.smu.Rsmu?.SMU_ADDR_MSG ?? 0, _cpu?.smu.Rsmu?.SMU_ADDR_RSP ?? 0, _cpu?.smu.Rsmu?.SMU_ADDR_ARG ?? 0);
    public SmuAddressSet Mp1 => new(_cpu?.smu.Mp1Smu?.SMU_ADDR_MSG ?? 0, _cpu?.smu.Mp1Smu?.SMU_ADDR_RSP ?? 0, _cpu?.smu.Mp1Smu?.SMU_ADDR_ARG ?? 0);
    public SmuAddressSet Hsmp => new(_cpu?.smu.Hsmp?.SMU_ADDR_MSG ?? 0, _cpu?.smu.Hsmp?.SMU_ADDR_RSP ?? 0, _cpu?.smu.Hsmp?.SMU_ADDR_ARG ?? 0);

    public enum CpuFamily
    {
        Unsupported = 0,
        Family0Fh = 15,
        Family10H = 16,
        Family12H = 18,
        Family15H = 21,
        Family16H = 22,
        Family17H = 23,
        Family18H = 24,
        Family19H = 25,
        Family1Ah = 26
    }

    public CpuFamily Family => (CpuFamily?)_cpu?.info.family ?? CpuFamily.Unsupported;
    public bool ReadMsr(uint index, ref uint eax, ref uint edx) => _cpu?.ReadMsr(index, ref eax, ref edx) ?? false;
    public bool WriteMsr(uint msr, uint eax, uint edx) => _cpu?.WriteMsr(msr, eax, edx) ?? false;

    public string CpuName => GetSystemInfo.ReadCpuInformation().name;
    public bool Smt => _cpu?.systemInfo.SMT ?? true;

    public struct CommonMotherBoardInfo
    {
        public string? MotherBoardName;

        public string? MotherBoardVendor;

        public string? BiosVersion;
    }

    public CommonMotherBoardInfo MotherBoardInfo => new() 
    { 
        MotherBoardName = _cpu?.systemInfo.MbName, 
        MotherBoardVendor = _cpu?.systemInfo.MbVendor, 
        BiosVersion = _cpu?.systemInfo.BiosVersion 
    };

    public enum MemType
    {
        Unknown = -1,
        Ddr4,
        Ddr5,
        Lpddr5
    }

    public struct MemoryModule
    {
        public string PartNumber;

        public string Manufacturer;

        public string Capacity;
    }
    public struct MemoryTimings
    {
        public string Tcl;

        public string Trcdwr;

        public string Trcdrd;

        public string Tras;

        public string Trp;

        public string Trc;
    }

    public struct MemoryConfig
    {
        public MemType Type;

        public int TotalCapacity;

        public List<MemoryModule> Modules;

        public int MemorySpeed;

        public int FrequencyFromTimings;

        public MemoryTimings MemoryTimings;
    }

    public MemoryConfig GetMemoryConfig()
    {
        try
        {
            if (!IsAvailable || _cpu == null)
            {
                throw new Exception("Cpu isn't initialized");
            }
            var memoryConfig = _cpu.GetMemoryConfig();
            var modules = memoryConfig.Modules;
            var convertedModules = new List<MemoryModule>();
            foreach (var module in modules)
            {
                convertedModules.Add(new MemoryModule()
                {
                    Capacity = module.Capacity.ToString(),
                    Manufacturer = module.Manufacturer.ToString(),
                    PartNumber = module.PartNumber.ToString()
                });
            }

            var umcBase = GetUmcAddressValue(UmcAddress.UmcBase);
            var umcOffset1 = GetUmcAddressValue(UmcAddress.UmcOffset1);
            var umcOffset2 = GetUmcAddressValue(UmcAddress.UmcOffset2);

            var freqFromRatio = ((MemType)memoryConfig.Type == MemType.Ddr4 ?
                                 GetBits(umcBase, 0, 7) / 3 :
                                 GetBits(umcBase, 0, 16) / 100)
                                 * 200;


            var tcl = GetBits(umcOffset1, 0, 6);
            var trcdwr = GetBits(umcOffset1, 24, 6);
            var trcdrd = GetBits(umcOffset1, 16, 6);
            var tras = GetBits(umcOffset1, 8, 7);
            var trp = GetBits(umcOffset2, 16, 6);
            var trc = GetBits(umcOffset2, 0, 8);

            return new MemoryConfig
            {
                Type = (MemType)memoryConfig.Type,
                TotalCapacity = (int)(memoryConfig.TotalCapacity.SizeInBytes / 1073741824),
                Modules = convertedModules,
                MemorySpeed = (int)_cpu.powerTable.MCLK * 2,
                FrequencyFromTimings = (int)freqFromRatio,
                MemoryTimings = new MemoryTimings()
                {
                    Tcl = tcl + "T",
                    Trcdwr = trcdwr + "T",
                    Trcdrd = trcdrd + "T",
                    Tras = tras + "T",
                    Trp = trp + "T",
                    Trc = trc + "T"
                }
            };

        }
        catch
        {
            return new MemoryConfig
            {
                Type = MemType.Unknown,
                TotalCapacity = 0,
                Modules = [],
                MemorySpeed = 0,
                FrequencyFromTimings = 0,
                MemoryTimings = new MemoryTimings()
            };
        }
    }

    private static uint GetBits(uint val, int offset, int n)
    {
        return (val >> offset) & (uint)(~(-1 << n));
    }

    public enum UmcAddress
    {
        UmcBase,
        UmcOffset1,
        UmcOffset2
    }

    private uint GetUmcAddressValue(UmcAddress type)
    {
        if (!IsAvailable || _cpu == null)
        {
            return 0;
        }

        return type switch
        {
            UmcAddress.UmcBase => _cpu.ReadDword(0x50200),
            UmcAddress.UmcOffset1 => _cpu.ReadDword(0x50204),
            UmcAddress.UmcOffset2 => _cpu.ReadDword(0x50208),
            _ => 0
        };
    }

    public bool Avx512AvailableByCodename => _codeName >= CodeName.Raphael;
    public string CpuCodeName => _codeName.ToString();
    public string SmuVersion => _cpu?.systemInfo.GetSmuVersionString() ?? "0.0.0";

    public uint MakeCoreMask(uint core = 0u, uint ccd = 0u, uint ccx = 0u) => _cpu?.MakeCoreMask(core, ccd, ccx) ?? 0;

    public uint SmuCoperCommandMp1
    {
        get => _cpu?.smu.Mp1Smu.SMU_MSG_SetDldoPsmMargin ?? 0;
        set 
        {
            if (_cpu != null)
            {
                _cpu.smu.Mp1Smu.SMU_MSG_SetDldoPsmMargin = value;
            }
        }
    }

    public uint SmuCoperCommandRsmu
    {
        get => _cpu?.smu.Rsmu.SMU_MSG_SetDldoPsmMargin ?? 0;
        set 
        {
            if (_cpu != null)
            {
                _cpu.smu.Rsmu.SMU_MSG_SetDldoPsmMargin = value;
            }
        }
    }

    public void SetCoperSingleCore(uint coreMask, int margin) => _cpu?.SetPsmMarginSingleCore(coreMask, margin);
    public void RefreshPowerTable() => _cpu?.RefreshPowerTable();

    public float[] PowerTable => _cpu?.powerTable?.Table ?? [];

    public uint PowerTableVersion => _cpu?.smu.TableVersion ?? 0;
    public float SocMemoryClock => _cpu?.powerTable?.MCLK ?? 0;
    public float SocFabricClock => _cpu?.powerTable?.FCLK ?? 0;
    public float SocVoltage => _cpu?.powerTable?.VDDCR_SOC ?? 0;

    public double GetCoreMultiplier(int core) => (_cpu?.GetCoreMulti(core) ?? 0) / 10.0; // Конвертируем в GHz
    public float? GetCpuTemperature() => _cpu?.GetCpuTemperature();
}