using System.ComponentModel;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
namespace Saku_Overclock.SMUEngine;

public class PowerTable : INotifyPropertyChanged
{
    private readonly IOModule io;
    private readonly SMU smu;
    private readonly ACPI_MMIO mmio;
    private readonly PTDef tableDef;
    public readonly uint DramBaseAddressLo;
    public readonly uint DramBaseAddressHi;
    public readonly uint DramBaseAddress;
    public readonly int TableSize;
    private const int NUM_ELEMENTS_TO_COMPARE = 20;
    private static readonly PowerTableDef PowerTables = new PowerTableDef
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
            3484,
            404,
            424,
            444,
            284,
            -1,
            -1,
            -1,
            -1,
            304
        },
        {
            6030083,
            3484,
            412,
            432,
            452,
            292,
            -1,
            -1,
            -1,
            -1,
            312
        },
        {
            1472,
            3484,
            412,
            432,
            452,
            292,
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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(PropertyChangedEventArgs eventArgs)
    {
        var propertyChanged = PropertyChanged;
        if (propertyChanged == null)
        {
            return;
        }

        propertyChanged(this, eventArgs);
    }

    private bool SetProperty<T>(ref T storage, T value, PropertyChangedEventArgs args)
    {
        if (Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(args);
        return true;
    }

    private PTDef GetDefByVersion(uint version)
    {
        return PowerTables.Find((Predicate<PTDef>)(x => x.tableVersion == version));
    }

    private PTDef GetDefaultTableDef(uint tableVersion, SMU.SmuType smutype)
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
                switch (tableVersion & 7U)
                {
                    case 0:
                        version = 512U;
                        break;
                    case 1:
                    case 2:
                    case 4:
                        version = 514U;
                        break;
                    default:
                        version = 515U;
                        break;
                }
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
        return GetDefByVersion(version);
    }

    private PTDef GetPowerTableDef(uint tableVersion, SMU.SmuType smutype)
    {
        var defByVersion = GetDefByVersion(tableVersion);
        return defByVersion.tableSize != 0 ? defByVersion : GetDefaultTableDef(tableVersion, smutype);
    }

    public PowerTable(SMU smuInstance, IOModule ioInstance, ACPI_MMIO mmio)
    {
        smu = smuInstance ?? throw new ArgumentNullException(nameof(smuInstance));
        io = ioInstance ?? throw new ArgumentNullException(nameof(ioInstance));
        this.mmio = mmio ?? throw new ArgumentNullException(nameof(mmio));
        var cmdResult = new GetDramAddress(smu).Execute();
        DramBaseAddressLo = DramBaseAddress = cmdResult.args[0];
        DramBaseAddressHi = cmdResult.args[1];
        if (DramBaseAddress == 0U)
        {
            throw new ApplicationException("Could not get DRAM base address.");
        }

        if (!Utils.Is64Bit)
        {
            new SetToolsDramAddress(smu).Execute(DramBaseAddress);
        }

        tableDef = GetPowerTableDef(smu.TableVersion, smu.SMU_TYPE);
        TableSize = tableDef.tableSize;
        Table = new float[TableSize / 4];
        _ = (int)Refresh();
    }

    private float GetDiscreteValue(float[] pt, int index)
    {
        return index > -1 && index < TableSize ? pt[index / 4] : 0.0f;
    }

    private void ParseTable(float[] pt)
    {
        if (pt == null)
        {
            return;
        }

        var num = 1f;
        var bclk = mmio.GetBclk();
        if (bclk.HasValue)
        {
            num = (float)bclk.Value / 100f;
        }

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
        var dst = new float[tableSize];
        if (Utils.Is64Bit)
        {
            var src = smu.SMU_TYPE >= SMU.SmuType.TYPE_CPU4 && smu.SMU_TYPE < SMU.SmuType.TYPE_CPU9 || smu.SMU_TYPE == SMU.SmuType.TYPE_APU2 ? io.ReadMemory(new IntPtr((long)DramBaseAddressHi << 32 | DramBaseAddressLo), tableSize * 4) : io.ReadMemory(new IntPtr(DramBaseAddressLo), tableSize * 4);
            if (src != null && src.Length != 0)
            {
                Buffer.BlockCopy(src, 0, dst, 0, src.Length);
            }
        }
        else
        {
            try
            {
                for (var index = 0; index < dst.Length; ++index)
                {
                    var dstOffset = index * 4;
                    _ = io.GetPhysLong((UIntPtr)(DramBaseAddress + (ulong)dstOffset), out var data) ? 1 : 0;
                    var bytes = BitConverter.GetBytes(data);
                    Buffer.BlockCopy(bytes, 0, dst, dstOffset, bytes.Length);
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
        var status = SMU.Status.FAILED;
        if (DramBaseAddress > 0U)
        {
            try
            {
                var numArray = ReadTableFromMemory(20);
                if (Utils.AllZero(numArray) || Utils.ArrayMembersEqual(Table, numArray, 20))
                {
                    status = new TransferTableToDram(smu).Execute().status;
                    if (status != SMU.Status.OK)
                    {
                        return status;
                    }
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

    public float ConfiguredClockSpeed
    {
        get; set;
    }

    public float MemRatio
    {
        get; set;
    }

    public float[] Table
    {
        get; private set;
    }

    public float FCLK
    {
        get => fclk;
        set => SetProperty(ref fclk, value, InternalEventArgsCache.FCLK);
    }

    public float MCLK
    {
        get => mclk;
        set => SetProperty(ref mclk, value, InternalEventArgsCache.MCLK);
    }

    public float UCLK
    {
        get => uclk;
        set => SetProperty(ref uclk, value, InternalEventArgsCache.UCLK);
    }

    public float VDDCR_SOC
    {
        get => vddcr_soc;
        set => SetProperty(ref vddcr_soc, value, InternalEventArgsCache.VDDCR_SOC);
    }

    public float CLDO_VDDP
    {
        get => cldo_vddp;
        set => SetProperty(ref cldo_vddp, value, InternalEventArgsCache.CLDO_VDDP);
    }

    public float CLDO_VDDG_IOD
    {
        get => cldo_vddg_iod;
        set => SetProperty(ref cldo_vddg_iod, value, InternalEventArgsCache.CLDO_VDDG_IOD);
    }

    public float CLDO_VDDG_CCD
    {
        get => cldo_vddg_ccd;
        set => SetProperty(ref cldo_vddg_ccd, value, InternalEventArgsCache.CLDO_VDDG_CCD);
    }

    public float VDD_MISC
    {
        get => vdd_misc;
        set => SetProperty(ref vdd_misc, value, InternalEventArgsCache.VDD_MISC);
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

    private class PowerTableDef : List<PTDef>
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
            Add(new PTDef
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