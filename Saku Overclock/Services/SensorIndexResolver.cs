using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Models;

namespace Saku_Overclock.Services;

public class SensorIndexResolver : ISensorIndexResolver
{
    private static bool _isLaptop;
    public SensorIndexResolver(ICpuService cpu)
    {
        _isLaptop = cpu.IsPlatformPcByCodename() == false;
    }

    public int ResolveIndex(int tableVersion, SensorId sensor)
    {
        return sensor switch
        {
            // Лимиты и значения
            SensorId.CpuStapmLimit => ResolveCpuStapmLimit(tableVersion),
            SensorId.CpuStapmValue => ResolveCpuStapmValue(tableVersion),
            SensorId.CpuFastLimit => ResolveCpuFastLimit(tableVersion),
            SensorId.CpuFastValue => ResolveCpuFastValue(tableVersion),
            SensorId.CpuSlowLimit => ResolveCpuSlowLimit(tableVersion),
            SensorId.CpuSlowValue => ResolveCpuSlowValue(tableVersion),
            SensorId.ApuSlowLimit => ResolveApuSlowLimit(tableVersion),
            SensorId.ApuSlowValue => ResolveApuSlowValue(tableVersion),

            // VRM
            SensorId.VrmTdcValue => ResolveVrmTdcValue(tableVersion),
            SensorId.VrmTdcLimit => ResolveVrmTdcLimit(tableVersion),
            SensorId.VrmEdcValue => ResolveVrmEdcValue(tableVersion),
            SensorId.VrmEdcLimit => ResolveVrmEdcLimit(tableVersion),
            SensorId.VrmPsiValue => ResolveVrmPsiValue(tableVersion),
            SensorId.VrmPsiSocValue => ResolveVrmPsiSocValue(tableVersion),

            // SoC
            SensorId.SocTdcValue => ResolveSocTdcValue(tableVersion),
            SensorId.SocTdcLimit => ResolveSocTdcLimit(tableVersion),
            SensorId.SocEdcValue => ResolveSocEdcValue(tableVersion),
            SensorId.SocEdcLimit => ResolveSocEdcLimit(tableVersion),

            // Температуры
            SensorId.CpuTempValue => ResolveCpuTempValue(tableVersion),
            SensorId.CpuTempLimit => ResolveCpuTempLimit(tableVersion),
            SensorId.ApuTempValue => ResolveApuTempValue(tableVersion),
            SensorId.ApuTempLimit => ResolveApuTempLimit(tableVersion),
            SensorId.DgpuTempValue => ResolveDgpuTempValue(tableVersion),
            SensorId.DgpuTempLimit => ResolveDgpuTempLimit(tableVersion),

            // Время
            SensorId.CpuStapmTimeValue => ResolveCpuStapmTimeValue(tableVersion),
            SensorId.CpuSlowTimeValue => ResolveCpuSlowTimeValue(tableVersion),

            // APU
            SensorId.ApuFrequency => ResolveApuFrequency(tableVersion),
            SensorId.ApuVoltage => ResolveApuVoltage(tableVersion),

            // SoC Power & Voltage
            SensorId.SocPower => ResolveSocPower(tableVersion),
            SensorId.SocVoltage => ResolveSocVoltage(tableVersion),

            // Per-core arrays
            SensorId.CpuFrequencyStart => ResolveCpuFrequencyStart(tableVersion),
            SensorId.CpuVoltageStart => ResolveCpuVoltageStart(tableVersion),
            SensorId.CpuTemperatureStart => ResolveCpuTemperatureStart(tableVersion),
            SensorId.CpuPowerStart => ResolveCpuPowerStart(tableVersion),

            _ => -1
        };
    }

    #region Limit/Value Resolvers

    private static int ResolveCpuStapmLimit(int ver)
    {
        if (_isLaptop)
        {
            return 0;
        }

        return ver switch
        {
            0x00540004 or    // Zen 4
            0x00540104 or    // Zen 4
            0x00540208 or
            0x00620105 or 
            0x00620205 => 0, // Zen 5
            _ => -1
        };
    }

    private static int ResolveCpuStapmValue(int ver)
    {
        if (_isLaptop)
        {
            return 1;
        }

        return ver switch
        {
            0x00540004 or     // Zen 4
            0x00540104 or     // Zen 4
            0x00540208 or
            0x00620105 or 
            0x00620205 => 1,  // Zen 5
            _ => -1
        };
    }

    private static int ResolveCpuFastLimit(int ver)
    {
        if (_isLaptop)
        {
            return 2;
        }

        return ver switch
        {
            0x00190001 or    // Zen
            0x00240803 or    // Zen 2
            0x00240903 or
            0x00380904 or    // Zen 3
            0x00380905 or
            0x00380804 or
            0x00380805 => 0,
            0x00540004 or    // Zen 4
            0x00540104 or    // Zen 4
            0x00540208 or
            0x00620105 or 
            0x00620205 => 2, // Zen 5
            _ => -1
        };
    }

    private static int ResolveCpuFastValue(int ver)
    {
        if (_isLaptop)
        {
            return 3;
        }

        return ver switch
        {
            0x00190001 => 22, // Zen
            0x00240803 or     // Zen 2
            0x00240903 or
            0x00380904 or     // Zen 3
            0x00380905 or
            0x00380804 or
            0x00380805 => 29,
            0x00540004 or     // Zen 4
            0x00540104 or
            0x00540208 or
            0x00620105 or 
            0x00620205 => 26, // Zen 5
            _ => -1
        };
    }

    private static int ResolveCpuSlowLimit(int ver)
    {
        if (_isLaptop)
        {
            return 4;
        }

        return ver switch
        {
            0x00190001 or    // Zen
            0x00240803 or    // Zen 2
            0x00240903 or
            0x00380904 or    // Zen 3
            0x00380905 or
            0x00380804 or
            0x00380805 => 0,
            0x00540004 or    // Zen 4
            0x00540104 or  
            0x00540208 or
            0x00620105 or 
            0x00620205 => 2, // Zen 5
            _ => -1
        };
    }

    private static int ResolveCpuSlowValue(int ver)
    {
        if (_isLaptop)
        {
            return 5;
        }

        return ver switch
        {
            0x00190001 or    // Zen
            0x00240803 or    // Zen 2
            0x00240903 or
            0x00380904 or    // Zen 3
            0x00380905 or
            0x00380804 or
            0x00380805 => 1,

            0x00540004 or    // Zen 4
            0x00540104 or 
            0x00540208 or
            0x00620105 or 
            0x00620205 => 3, // Zen 5
            _ => -1
        };
    }

    private static int ResolveApuSlowLimit(int ver)
    {
        return ver switch
        {
            // Raven and up → индекс 0
            0x001E0001 or
            0x001E0002 or
            0x001E0003 or
            0x001E0004 or
            0x001E0005 or
            0x001E000A or
            0x001E0101 => 0,

            // Renoir and up → индекс 6
            0x00370000 or
            0x00370001 or
            0x00370002 or
            0x00370003 or
            0x00370004 or
            0x00370005 or
            0x003F0000 or
            0x00400001 or
            0x00400002 or
            0x00400003 or
            0x00400004 or
            0x00400005 or
            0x00450004 or
            0x00450005 or
            0x004C0003 or
            0x004C0004 or
            0x004C0005 or
            0x004C0006 or
            0x004C0007 or
            0x004C0008 or
            0x004C0009 or
            0x0064020c => 6,

            0x00540004 or    // Zen 4
            0x00540104 or
            0x00540208 or
            0x00620105 or 
            0x00620205 => 2, // Zen 5
            _ => -1
        };
    }

    private static int ResolveApuSlowValue(int ver)
    {
        return ver switch
        {
            // Raven and up → индекс 153
            0x001E0001 or
            0x001E0002 or
            0x001E0003 or
            0x001E0004 or
            0x001E0005 or
            0x001E000A or
            0x001E0101 => 153,

            // Renoir and up → индекс 7
            0x00370000 or
            0x00370001 or
            0x00370002 or
            0x00370003 or
            0x00370004 or
            0x00370005 or
            0x003F0000 or
            0x00400001 or
            0x00400002 or
            0x00400003 or
            0x00400004 or
            0x00400005 or
            0x00450004 or
            0x00450005 or
            0x004C0003 or
            0x004C0004 or
            0x004C0005 or
            0x004C0006 or
            0x004C0007 or
            0x004C0008 or
            0x0064020c => 7,

            // Hawk Point -> индекс 43
            0x004C0009 => 43,

            0x00540004 or      // Zen 4
            0x00540104 => 99,
            0x00540208 => 100, // Dragon Range
            0x00620105 or 
            0x00620205 => 107, // Zen 5
            _ => -1
        };
    }

    #endregion

    #region VRM Resolvers

    private static int ResolveVrmTdcLimit(int ver)
    {
        return ver switch
        {
            // Zen поколение
            0x00190001 or    // Zen
            0x00240803 or    // Zen 2
            0x00240903 or
            0x00380904 or    // Zen 3
            0x00380905 or
            0x00380804 or
            0x00380805 => 2,

            // Raven and up → индекс 6
            0x001E0001 or
            0x001E0002 or
            0x001E0003 or
            0x001E0004 or
            0x001E0005 or
            0x001E000A or
            0x001E0101 => 6,

            // Renoir and up → индекс 8
            0x00370000 or
            0x00370001 or
            0x00370002 or
            0x00370003 or
            0x00370004 or
            0x00370005 or
            0x00400001 or
            0x00400002 or
            0x00400003 or
            0x00400004 or
            0x00400005 or
            0x00450004 or
            0x00450005 or
            0x004C0003 or
            0x004C0004 or
            0x004C0005 or
            0x004C0006 or
            0x004C0007 or
            0x004C0008 or
            0x004C0009 => 8,

            0x00540004 or    // Zen 4
            0x00540104 or  
            0x00540208 or
            0x00620105 or 
            0x00620205 => 8, // Zen 5

            _ => -1
        };
    }

    private static int ResolveVrmTdcValue(int ver)
    {
        return ver switch
        {
            // Zen поколение
            0x00190001 or     // Zen
            0x00240803 or     // Zen 2
            0x00240903 or
            0x00380904 or     // Zen 3
            0x00380905 or
            0x00380804 or
            0x00380805 => 3,

            // Raven and up → индекс 7
            0x001E0001 or
            0x001E0002 or
            0x001E0003 or
            0x001E0004 or
            0x001E0005 or
            0x001E000A or
            0x001E0101 => 7,

            // Renoir and up → индекс 9
            0x00370000 or
            0x00370001 or
            0x00370002 or
            0x00370003 or
            0x00370004 or
            0x00370005 or
            0x00400001 or
            0x00400002 or
            0x00400003 or
            0x00400004 or
            0x00400005 or
            0x00450004 or
            0x00450005 or
            0x004C0003 or
            0x004C0004 or
            0x004C0005 or
            0x004C0006 or
            0x004C0007 or
            0x004C0008 or
            0x004C0009 => 9,

            0x00540004 or    // Zen 4
            0x00540104 or    // Zen 4
            0x00540208 or
            0x00620105 or 
            0x00620205 => 9, // Zen 5

            _ => -1
        };
    }

    private static int ResolveVrmEdcLimit(int ver)
    {
        return ver switch
        {
            // Zen поколение
            0x00190001 => 2, // Zen
            0x00240803 or    // Zen 2
            0x00240903 or
            0x00380904 or    // Zen 3
            0x00380905 or
            0x00380804 or
            0x00380805 => 8,

            // Raven and up → индекс 10
            0x001E0001 or
            0x001E0002 or
            0x001E0003 or
            0x001E0004 or
            0x001E0005 or
            0x001E000A or
            0x001E0101 => 10,

            // Renoir and up → индекс 12
            0x00370000 or
            0x00370001 or
            0x00370002 or
            0x00370003 or
            0x00370004 or
            0x00370005 or
            0x00400001 or
            0x00400002 or
            0x00400003 or
            0x00400004 or
            0x00400005 or
            0x00450004 or
            0x00450005 or
            0x004C0003 or
            0x004C0004 or
            0x004C0005 or
            0x004C0006 or
            0x004C0007 or
            0x004C0008 or
            0x004C0009 => 12,

            0x00540004 or     // Zen 4
            0x00540104 => 61,
            0x00540208 => 62, // Dragon Range
            0x00620105 or     // Zen 5
            0x00620205 => 63,

            _ => -1
        };
    }

    private static int ResolveVrmEdcValue(int ver)
    {
        return ver switch
        {
            // Zen поколение
            0x00190001 => 25, // Zen
            0x00240803 or     // Zen 2
            0x00240903 or
            0x00380904 or     // Zen 3
            0x00380905 or
            0x00380804 or
            0x00380805 => 9,

            // Raven and up → индекс 11
            0x001E0001 or
            0x001E0002 or
            0x001E0003 or
            0x001E0004 or
            0x001E0005 or
            0x001E000A or
            0x001E0101 => 11,

            // Renoir and up → индекс 13
            0x00370000 or
            0x00370001 or
            0x00370002 or
            0x00370003 or
            0x00370004 or
            0x00370005 or
            0x00400001 or
            0x00400002 or
            0x00400003 or
            0x00400004 or
            0x00400005 or
            0x00450004 or
            0x00450005 or
            0x004C0003 or
            0x004C0004 or
            0x004C0005 or
            0x004C0006 or
            0x004C0007 or
            0x004C0008 or
            0x004C0009 => 13,

            0x00540004 or     // Zen 4
            0x00540104 => 63,
            0x00540208 => 64, // Dragon Range
            0x00620105 or     // Zen 5
            0x00620205 => 51, 

            _ => -1
        };
    }

    private static int ResolveVrmPsiValue(int ver)
    {
        return ver switch
        {
            // Zen поколение
            0x00240803 => 42, // Zen 2
            0x00240903 => 41,
            0x00380904 or     // Zen 3
            0x00380905 or
            0x00380804 or
            0x00380805 => 42,

            // Raven and up → индекс 16
            0x001E0001 or
            0x001E0002 or
            0x001E0003 or
            0x001E0004 or
            0x001E0005 or
            0x001E000A or
            0x001E0101 => 16,

            // Renoir and up → индекс 30, 38, 33
            0x00370000 or
            0x00370001 or
            0x00370002 or
            0x00370003 or
            0x00370004 or
            0x00370005 or
            0x00400001 or
            0x00400002 or
            0x00400003 or
            0x00400004 or
            0x00400005 => 30,
            0x00450004 or
            0x00450005 => 38, // Rembrandt
            0x004C0003 or
            0x004C0004 or
            0x004C0005 or
            0x004C0006 or
            0x004C0007 or
            0x004C0008 => 30,
            0x004C0009 => 33, // Hawk Point

            _ => -1
        };
    }

    private static int ResolveVrmPsiSocValue(int ver)
    {
        return ver switch
        {
            // Zen поколение
            0x00240803 or     // Zen 2
            0x00240903 or
            0x00380904 or     // Zen 3
            0x00380905 or
            0x00380804 or
            0x00380805 => 46,

            // Raven and up → индекс 18
            0x001E0001 or
            0x001E0002 or
            0x001E0003 or
            0x001E0004 or
            0x001E0005 or
            0x001E000A or
            0x001E0101 => 18,

            // Renoir and up → индекс 32, 40, 35
            0x00370000 or
            0x00370001 or
            0x00370002 or
            0x00370003 or
            0x00370004 or
            0x00370005 or
            0x00400001 or
            0x00400002 or
            0x00400003 or
            0x00400004 or
            0x00400005 => 32,
            0x00450004 or
            0x00450005 => 40, // Rembrandt
            0x004C0003 or
            0x004C0004 or
            0x004C0005 or
            0x004C0006 or
            0x004C0007 or
            0x004C0008 => 32, // Phoenix
            0x004C0009 => 35, // Hawk Point

            _ => -1
        };
    }

    #endregion

    #region SoC Resolvers

    private static int ResolveSocTdcLimit(int ver)
    {
        return ver switch
        {
            // Zen поколение
            0x00190001 => 2, // Zen
            0x00240803 or    // Zen 2
            0x00240903 or
            0x00380904 or    // Zen 3
            0x00380905 or
            0x00380804 or
            0x00380805 => 8,

            // Raven and up → индекс 8
            0x001E0001 or
            0x001E0002 or
            0x001E0003 or
            0x001E0004 or
            0x001E0005 or
            0x001E000A or
            0x001E0101 => 8,

            // Renoir and up → индекс 10
            0x00370000 or
            0x00370001 or
            0x00370002 or
            0x00370003 or
            0x00370004 or
            0x00370005 or
            0x00400001 or
            0x00400002 or
            0x00400003 or
            0x00400004 or
            0x00400005 or
            0x00450004 or
            0x00450005 or
            0x004C0003 or
            0x004C0004 or
            0x004C0005 or
            0x004C0006 or
            0x004C0007 or
            0x004C0008 or
            0x004C0009 => 10,

            0x00540004 or    // Zen 4
            0x00540104 or 
            0x00540208 or
            0x00620105 or 
            0x00620205 => 8, // Zen 5

            _ => -1
        };
    }

    private static int ResolveSocTdcValue(int ver)
    {
        return ver switch
        {
            // Zen поколение
            0x00190001 => 27, // Zen
            0x00240803 or     // Zen 2
            0x00240903 or
            0x00380904 or     // Zen 3
            0x00380905 or
            0x00380804 or
            0x00380805 => 46,

            // Raven and up → индекс 9
            0x001E0001 or
            0x001E0002 or
            0x001E0003 or
            0x001E0004 or
            0x001E0005 or
            0x001E000A or
            0x001E0101 => 9,

            // Renoir and up → индекс 11
            0x00370000 or
            0x00370001 or
            0x00370002 or
            0x00370003 or
            0x00370004 or
            0x00370005 or
            0x00400001 or
            0x00400002 or
            0x00400003 or
            0x00400004 or
            0x00400005 or
            0x00450004 or
            0x00450005 or
            0x004C0003 or
            0x004C0004 or
            0x004C0005 or
            0x004C0006 or
            0x004C0007 or
            0x004C0008 or
            0x004C0009 => 11,

            0x00540004 or     // Zen 4
            0x00540104 => 53,
            0x00540208 => 54, // Dragon Range
            0x00620105 or 
            0x00620205 => 55, // Zen 5

            _ => -1
        };
    }

    private static int ResolveSocEdcLimit(int ver)
    {
        return ver switch
        {
            // Zen поколение
            0x00190001 => 2, // Zen
            0x00240803 or    // Zen 2
            0x00240903 or
            0x00380904 or    // Zen 3
            0x00380905 or
            0x00380804 or
            0x00380805 => 8,

            // Raven and up → индекс 13
            0x001E0001 or
            0x001E0002 or
            0x001E0003 or
            0x001E0004 or
            0x001E0005 or
            0x001E000A or
            0x001E0101 => 13,

            // Renoir and up → индекс 14
            0x00370000 or
            0x00370001 or
            0x00370002 or
            0x00370003 or
            0x00370004 or
            0x00370005 or
            0x00400001 or
            0x00400002 or
            0x00400003 or
            0x00400004 or
            0x00400005 or
            0x00450004 or
            0x00450005 or
            0x004C0003 or
            0x004C0004 or
            0x004C0005 or
            0x004C0006 or
            0x004C0007 or
            0x004C0008 or
            0x004C0009 => 14,

            0x00540004 or    // Zen 4
            0x00540104 or 
            0x00540208 or
            0x00620105 or
            0x00620205 => 8, // Zen 5

            _ => -1
        };
    }

    private static int ResolveSocEdcValue(int ver)
    {
        return ver switch
        {
            // Zen поколение
            0x00190001 => 28, // Zen
            0x00240803 or     // Zen 2
            0x00240903 or
            0x00380904 or     // Zen 3
            0x00380905 or
            0x00380804 or
            0x00380805 => 46,

            // Raven and up → индекс 14
            0x001E0001 or
            0x001E0002 or
            0x001E0003 or
            0x001E0004 or
            0x001E0005 or
            0x001E000A or
            0x001E0101 => 14,

            // Renoir and up → индекс 15, 39
            0x00370000 or
            0x00370001 or
            0x00370002 or
            0x00370003 or
            0x00370004 or
            0x00370005 or
            0x00400001 or
            0x00400002 or
            0x00400003 or
            0x00400004 => 15,
            0x00400005 or
            0x00450004 or
            0x00450005 => 39,
            0x004C0003 or
            0x004C0004 or
            0x004C0005 or
            0x004C0006 or
            0x004C0007 or
            0x004C0008 or
            0x004C0009 => 15,

            0x00540004 or     // Zen 4
            0x00540104 => 53, 
            0x00540208 => 54, // Dragon Range
            0x00620105 or 
            0x00620205 => 55, // Zen 5

            _ => -1
        };
    }

    #endregion

    #region Temperature Resolvers

    private static int ResolveCpuTempLimit(int ver)
    {
        return ver switch
        {
            // Zen поколение
            0x00190001 or    // Zen
            0x00240803 or    // Zen 2
            0x00240903 or
            0x00380904 or    // Zen 3
            0x00380905 or
            0x00380804 or
            0x00380805 => 4,

            // Raven and up → индекс 22
            0x001E0001 or
            0x001E0002 or
            0x001E0003 or
            0x001E0004 or
            0x001E0005 or
            0x001E000A or
            0x001E0101 or
            0x0064020c => 22, // Strix Halo tested

            // Renoir and up → индекс 16
            0x00370000 or
            0x00370001 or
            0x00370002 or
            0x00370003 or
            0x00370004 or
            0x00370005 or
            0x003F0000 or
            0x00400001 or
            0x00400002 or
            0x00400003 or
            0x00400004 or
            0x00400005 or
            0x00450004 or
            0x00450005 or
            0x004C0003 or
            0x004C0004 or
            0x004C0005 or
            0x004C0006 or
            0x004C0007 or
            0x004C0008 or
            0x004C0009 => 16,

            0x00540004 or     // Zen 4
            0x00540104 or 
            0x00540208 or
            0x00620105 or 
            0x00620205 => 10, // Zen 5

            _ => -1
        };
    }

    private static int ResolveCpuTempValue(int ver)
    {
        return ver switch
        {
            // Zen поколение
            0x00190001 or    // Zen
            0x00240803 or    // Zen 2
            0x00240903 or
            0x00380904 or    // Zen 3
            0x00380905 or
            0x00380804 or
            0x00380805 => 5,

            // Raven and up → индекс 23
            0x001E0001 or
            0x001E0002 or
            0x001E0003 or
            0x001E0004 or
            0x001E0005 or
            0x001E000A or
            0x001E0101 or
            0x0064020c => 23, // Strix Halo tested

            // Renoir and up → индекс 17
            0x00370000 or
            0x00370001 or
            0x00370002 or
            0x00370003 or
            0x00370004 or
            0x00370005 or
            0x003F0000 or
            0x00400001 or
            0x00400002 or
            0x00400003 or
            0x00400004 or
            0x00400005 or
            0x00450004 or
            0x00450005 or
            0x004C0003 or
            0x004C0004 or
            0x004C0005 or
            0x004C0006 or
            0x004C0007 or
            0x004C0008 or
            0x004C0009 => 17,

            0x00540004 or     // Zen 4
            0x00540104 or
            0x00540208 or
            0x00620105 or 
            0x00620205 => 11, // Zen 5

            _ => -1
        };
    }

    private static int ResolveApuTempLimit(int ver)
    {
        return ver switch
        {
            // Raven and up → индекс 30
            0x001E0001 or
            0x001E0002 or
            0x001E0003 or
            0x001E0004 or
            0x001E0005 or
            0x001E000A or
            0x001E0101 => 30,

            // Renoir and up → индекс 22
            0x00370000 or
            0x00370001 or
            0x00370002 or
            0x00370003 or
            0x00370004 or
            0x00370005 or
            0x003F0000 or
            0x00400001 or
            0x00400002 or
            0x00400003 or
            0x00400004 or
            0x00400005 or
            0x00450004 or
            0x00450005 or
            0x004C0003 or
            0x004C0004 or
            0x004C0005 or
            0x004C0006 or
            0x004C0007 or
            0x004C0008 or
            0x004C0009 or
            0x0064020c => 22,

            0x00540004 or     // Zen 4
            0x00540104 or 
            0x00540208 or 
            0x00620105 or 
            0x00620205 => 10, // Zen 5
            _ => -1
        };
    }

    private static int ResolveApuTempValue(int ver)
    {
        return ver switch
        {
            // Raven and up → индекс 151
            0x001E0001 or
            0x001E0002 or
            0x001E0003 or
            0x001E0004 or
            0x001E0005 or
            0x001E000A or
            0x001E0101 => 151,

            /*// Renoir and up → индекс 23
            0x00370000 or
            0x00370001 or
            0x00370002 or
            0x00370003 or
            0x00370004 or
            0x00370005 or
            0x003F0000 or
            0x00400001 or
            0x00400002 or
            0x00400003 or
            0x00400004 or
            0x00400005 or
            0x00450004 or
            0x00450005 or
            0x004C0006 or
            0x004C0007 or
            0x004C0008 or
            0x004C0009 or
            0x0064020c => 23,*/

            // Renoir and up → индекс 363, 370, 224 и т.д
            0x00370000 or
            0x00370001 or
            0x00370002 or
            0x00370003 or
            0x00370004 => 363,
            0x00370005 => 370, // Renoir
            0x003F0000 => 224, // Van Gogh
            0x00400001 => 385,
            0x00400002 => 391,
            0x00400003 => 399,
            0x00400004 or
            0x00400005 => 400, // Cezanne
            0x00450004 or
            0x00450005 => 419, // Rembrandt
            0x004C0003 or
            0x004C0004 or
            0x004C0005 or
            0x004C0006 or
            0x004C0007 or
            0x004C0008 => 211, // Phoenix
            0x004C0009 => 214, // Hawk Point
            0x0064020c => 340, // Strix Halo tested

            0x00540004 or      // Zen 4
            0x00540104 => 98,
            0x00540208 => 99,  // Dragon Range
            0x00620105 or 
            0x00620205 => 106, // Zen 5

            _ => -1
        };
    }

    private static int ResolveDgpuTempValue(int ver)
    {
        return ver switch
        {

            // Только для некоторых версий

            // Renoir and up → индекс 24
            0x00370000 or
            0x00370001 or
            0x00370002 or
            0x00370003 or
            0x00370004 or
            0x00370005 or
            0x003F0000 or
            0x00400001 or
            0x00400002 or
            0x00400003 or
            0x00400004 or
            0x00400005 or
            0x00450004 or
            0x00450005 or
            0x004C0003 or
            0x004C0004 or
            0x004C0005 or
            0x004C0006 or
            0x004C0007 or
            0x004C0008 or
            0x004C0009 or
            0x0064020c => 24, // StrixHalo tested

            _ => -1
        };
    }

    private static int ResolveDgpuTempLimit(int ver)
    {
        return ver switch
        {
            // Только для некоторых версий

            // Renoir and up → индекс
            0x00370000 or
            0x00370001 or
            0x00370002 or
            0x00370003 or
            0x00370004 or
            0x00370005 or
            0x003F0000 or
            0x00400001 or
            0x00400002 or
            0x00400003 or
            0x00400004 or
            0x00400005 or
            0x00450004 or
            0x00450005 or
            0x004C0003 or
            0x004C0004 or
            0x004C0005 or
            0x004C0006 or
            0x004C0007 or
            0x004C0008 or
            0x004C0009 or
            0x0064020c => 25,

            _ => -1
        };
    }

    #endregion

    #region Time Resolvers

    private static int ResolveCpuStapmTimeValue(int ver)
    {
        return ver switch
        {
            // Raven and up → индекс 345, 343, 376
            0x001E0001 or
            0x001E0002 => 345,
            0x001E0003 => 343,
            0x001E0004 or
            0x001E0005 or
            0x001E000A or
            0x001E0101 => 376,

            // Renoir and up → индекс 474, 534, 536 и т.д
            0x00370000 => 474,
            0x00370001 => 534,
            0x00370002 => 536,
            0x00370003 or
            0x00370004 => 544,
            0x00370005 => 551,
            0x00400001 => 569,
            0x00400002 => 575,
            0x00400003 => 584,
            0x00400004 or
            0x00400005 => 582, // Cezanne - Not Sure
            0x00450004 or
            0x00450005 => 672, // Rembrandt tested
            0x004C0003 or
            0x004C0004 or
            0x004C0005 or
            0x004C0006 or
            0x004C0007 or
            0x004C0008 => 605, // Phoenix tested
            0x004C0009 => 582, // Hawk Point tested

            0x00540004 or      // Zen 4
            0x00540104 or      // Incorrect
            0x00540208 or      // Incorrect
            0x00620105 or      // Incorrect
            0x00620205 => 547, // Zen 5 Incorrect

            _ => -1
        };
    }

    private static int ResolveCpuSlowTimeValue(int ver)
    {
        return ver switch
        {
            // Raven and up → индекс 346, 344, 377
            0x001E0001 or
            0x001E0002 => 346,
            0x001E0003 => 344,
            0x001E0004 or
            0x001E0005 or
            0x001E000A or
            0x001E0101 => 377,

            // Renoir and up → индекс 475, 535, 537 и т.д
            0x00370000 => 475,
            0x00370001 => 535,
            0x00370002 => 537,
            0x00370003 or
            0x00370004 => 545,
            0x00370005 => 552,
            0x00400001 => 570,
            0x00400002 => 576,
            0x00400003 => 585,
            0x00400004 or
            0x00400005 => 583,
            0x00450004 or
            0x00450005 => 673, // Rembrandt - Not Sure
            0x004C0003 or
            0x004C0004 or
            0x004C0005 or
            0x004C0006 or
            0x004C0007 or
            0x004C0008 => 606, // Phoenix tested
            0x004C0009 => 583, // Hawk Point tested

            0x00540004 or      // Zen 4
            0x00540104 or      // Incorrect
            0x00540208 or      // Incorrect
            0x00620105 or      // Incorrect
            0x00620205 => 548, // Zen 5 Incorrect

            _ => -1
        };
    }

    #endregion

    #region APU Resolvers

    private static int ResolveApuFrequency(int ver)
    {
        return ver switch
        {
            // Raven and up → индекс 155
            0x001E0001 or
            0x001E0002 or
            0x001E0003 or
            0x001E0004 or
            0x001E0005 or
            0x001E000A or
            0x001E0101 => 155,

            // Renoir and up → индекс 365, 372, 226 и т.д
            0x00370000 or
            0x00370001 or
            0x00370002 or
            0x00370003 or
            0x00370004 => 365,
            0x00370005 => 372,
            0x003F0000 => 226,
            0x00400001 => 387,
            0x00400002 => 393,
            0x00400003 => 401,
            0x00400004 or
            0x00400005 => 240,
            0x00450004 or
            0x00450005 => 421,
            0x004C0003 or
            0x004C0004 or
            0x004C0005 or
            0x004C0006 or
            0x004C0007 or
            0x004C0008 => 213,
            0x004C0009 => 216,
            0x0064020c => 342, // Strix Halo tested

            0x00540004 or      // Zen 4
            0x00540104 => 100,
            0x00540208 => 101, // Dragon Range
            0x00620105 or
            0x00620205 => 108, // Zen 5

            _ => -1
        };
    }

    private static int ResolveApuVoltage(int ver)
    {
        return ver switch
        {
            // Raven and up → индекс 150
            0x001E0001 or
            0x001E0002 or
            0x001E0003 or
            0x001E0004 or
            0x001E0005 or
            0x001E000A or
            0x001E0101 => 150,

            // Renoir and up → индекс 362, 369, 223 и т.д
            0x00370000 or
            0x00370001 or
            0x00370002 or
            0x00370003 or
            0x00370004 => 362,
            0x00370005 => 369,
            0x003F0000 => 223,
            0x00400001 => 384,
            0x00400002 => 390,
            0x00400003 => 398,
            0x00400004 or
            0x00400005 => 399,
            0x00450004 or
            0x00450005 => 44,
            0x004C0003 or
            0x004C0004 or
            0x004C0005 or
            0x004C0006 or
            0x004C0007 or
            0x004C0008 => 210,
            0x004C0009 => 42,
            0x0064020c => 339, // Strix Halo tested


            0x00540004 or      // Zen 4
            0x00540104 => 97,   
            0x00540208 => 98,  // Dragon Range
            0x00620105 or
            0x00620205 => 105, // Zen 5

            _ => -1
        };
    }

    #endregion

    #region SoC Power & Voltage Resolvers

    private static int ResolveSocPower(int ver)
    {
        return ver switch
        {
            // Zen поколение
            0x00190001 => 19, // Zen
            0x00240803 or     // Zen 2
            0x00240903 or
            0x00380904 or     // Zen 3
            0x00380905 or
            0x00380804 or
            0x00380805 => 25,

            // Raven and up → индекс 67
            0x001E0001 or
            0x001E0002 or
            0x001E0003 or
            0x001E0004 or
            0x001E0005 or
            0x001E000A or
            0x001E0101 => 67,

            // Renoir and up → индекс 104
            0x00370000 or
            0x00370001 or
            0x00370002 or
            0x00370003 or
            0x00370004 or
            0x00370005 => 104,

            // Van Gogh → индекс 106
            0x003F0000 => 106,

            // Cezanne → индекс 105
            0x00400004 or
            0x00400005 => 105,

            // Rembrandt → индекс 116
            0x00450004 or
            0x00450005 => 116,

            // Phoenix → индекс 112
            0x004C0003 or
            0x004C0004 or
            0x004C0005 or
            0x004C0008 or
            0x004C0009 => 112, // Hawk Point

            0x00540004 or      // Zen 4
            0x00540104 or
            0x00540208 or      // Dragon Range
            0x00620105 or
            0x00620205 => 21,  // Zen 5

            _ => -1
        };
    }

    private static int ResolveSocVoltage(int ver)
    {
        return ver switch
        {
            // Zen поколение
            0x00190001 => 26, // Zen
            0x00240803 or     // Zen 2
            0x00240903 or
            0x00380904 or     // Zen 3
            0x00380905 or
            0x00380804 or
            0x00380805 => 45,

            // Raven and up → индекс 65
            0x001E0001 or
            0x001E0002 or
            0x001E0003 or
            0x001E0004 or
            0x001E0005 or
            0x001E000A or
            0x001E0101 => 65,

            // Renoir and up → индекс 102
            0x00370000 or
            0x00370001 or
            0x00370002 or
            0x00370003 or
            0x00370004 or
            0x00370005 => 102,

            // Van Gogh → индекс 104
            0x003F0000 => 104,

            // Cezanne → индекс 103
            0x00400004 or
            0x00400005 => 103,

            // Rembrandt → индекс 114
            0x00450004 or
            0x00450005 => 114,

            // Phoenix → индекс 110
            0x004C0003 or
            0x004C0004 or
            0x004C0005 or
            0x004C0008 or
            0x004C0009 => 110, // Hawk Point

            0x00540004 or
            0x00540104 => 52,  // Zen 4
            0x00540208 => 53,  // Dragon Range
            0x00620105 or
            0x00620205 => 54,  // Zen 5

            _ => -1
        };
    }

    #endregion

    #region Per-Core Array Start Resolvers

    private static int ResolveCpuFrequencyStart(int ver)
    {
        return ver switch
        {
            // Zen поколение
            0x00190001 => 81,  // Zen
            0x00240803 => 227, // Zen 2
            0x00240903 => 187,
            0x00380904 => 209, // Zen 3
            0x00380905 => 212,
            0x00380804 => 249,
            0x00380805 => 252,

            // Raven and up → индекс 121
            0x001E0001 or
            0x001E0002 or
            0x001E0003 => 121,
            0x001E0004 => 120,
            0x001E0005 or
            0x001E000A or
            0x001E0101 => 121,

            // Renoir and up → индекс 232, 239, 162 и т.д
            0x00370000 or
            0x00370001 or
            0x00370002 or
            0x00370003 or
            0x00370004 => 232,
            0x00370005 => 239,
            0x003F0000 => 162,
            0x00400001 or
            0x00400002 or
            0x00400003 or
            0x00400004 or
            0x00400005 => 240,
            0x00450004 or
            0x00450005 => 259,
            0x004C0003 or
            0x004C0004 or
            0x004C0005 or
            0x004C0006 or
            0x004C0007 or
            0x004C0008 => 549,
            0x004C0009 => 553,
            0x0064020c => 788, // Strix Halo tested

            0x00540004 => 341, // Zen 4
            0x00540104 => 317,
            0x00540208 => 346, // Dragon Range
            0x00620105 => 325, // Zen 5
            0x00620205 => 349, 

            _ => -1
        };
    }

    private static int ResolveCpuVoltageStart(int ver)
    {
        return ver switch
        {
            // Zen поколение
            0x00190001 => 57,  // Zen
            0x00240803 => 163, // Zen 2
            0x00240903 => 155,
            0x00380904 => 177, // Zen 3
            0x00380905 => 180,
            0x00380804 => 185,
            0x00380805 => 188,

            // Raven and up → индекс 104
            0x001E0001 or
            0x001E0002 or
            0x001E0003 or
            0x001E0004 or
            0x001E0005 or
            0x001E000A or
            0x001E0101 => 104,

            // Renoir and up → индекс  и т.д
            0x00370000 or
            0x00370001 or
            0x00370002 or
            0x00370003 or
            0x00370004 => 200,
            0x00370005 => 207,
            0x003F0000 => 146,
            0x00400001 or
            0x00400002 or
            0x00400003 or
            0x00400004 or
            0x00400005 => 208,
            0x00450004 or
            0x00450005 => 227,
            0x004C0003 or 
            0x004C0004 or
            0x004C0005 or
            0x004C0006 or
            0x004C0007 or
            0x004C0008 => 517,
            0x004C0009 => 105, // Hawk Point → Use only two, from SVI2
            0x0064020c => 756, // Strix Halo tested

            0x00540004 => 309, // Zen 4
            0x00540104 => 301,
            0x00540208 => 314, // Dragon Range
            0x00620105 => 309, // Zen 5
            0x00620205 => 317,

            _ => -1
        };
    }

    private static int ResolveCpuTemperatureStart(int ver)
    {
        return ver switch
        {
            // Zen поколение
            0x00190001 => 65,  // Zen
            0x00240803 => 179, // Zen 2
            0x00240903 => 163,
            0x00380904 => 185, // Zen 3
            0x00380905 => 188,
            0x00380804 => 201,
            0x00380805 => 204,

            // Raven and up → индекс 108
            0x001E0001 or
            0x001E0002 or
            0x001E0003 or
            0x001E0004 or
            0x001E0005 or
            0x001E000A or
            0x001E0101 => 108,

            // Renoir and up → индекс 208, 215, 150 и т.д
            0x00370000 or
            0x00370001 or
            0x00370002 or
            0x00370003 or
            0x00370004 => 208,
            0x00370005 => 215,
            0x003F0000 => 150,
            0x00400001 or
            0x00400002 or
            0x00400003 or
            0x00400004 or
            0x00400005 => 216,
            0x00450004 or
            0x00450005 => 235,
            0x004C0003 or
            0x004C0004 or
            0x004C0005 or
            0x004C0006 or
            0x004C0007 or
            0x004C0008 => 525,
            0x004C0009 => 529,
            0x0064020c => 772, // Strix Halo tested

            0x00540004 => 325, // Zen 4
            0x00540104 => 309,
            0x00540208 => 330, // Dragon Range
            0x00620105 => 317, // Zen 5
            0x00620205 => 333,

            _ => -1
        };
    }

    private static int ResolveCpuPowerStart(int ver)
    {
        return ver switch
        {
            // Zen поколение
            0x00190001 => 41,  // Zen
            0x00240803 or      // Zen 2
            0x00240903 => 147,
            0x00380904 => 169, // Zen 3
            0x00380905 => 172,
            0x00380804 => 169,
            0x00380805 => 172,

            // Raven and up → индекс 96
            0x001E0001 or
            0x001E0002 or
            0x001E0003 or
            0x001E0004 or
            0x001E0005 or
            0x001E000A or
            0x001E0101 => 96,

            // Renoir and up → индекс 192, 199, 142 и т.д
            0x00370000 or
            0x00370001 or
            0x00370002 or
            0x00370003 or
            0x00370004 => 192,
            0x00370005 => 199,
            0x003F0000 => 142,
            0x00400001 or
            0x00400002 => 193,
            0x00400003 or
            0x00400004 or
            0x00400005 => 200,
            0x00450004 or
            0x00450005 => 219,
            0x004C0003 or
            0x004C0004 or
            0x004C0005 or
            0x004C0006 or
            0x004C0007 or
            0x004C0008 => 509,
            0x004C0009 => 513,
            0x0064020c => 740, // Strix Halo tested

            0x00540004 or      // Zen 4
            0x00540104 => 293,
            0x00540208 => 298, // Dragon Range
            0x00620105 or      // Zen 5
            0x00620205 => 301, 

            _ => -1
        };
    }

    #endregion
}