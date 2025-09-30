using System.Buffers;
using System.Runtime.InteropServices;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.SMUEngine;
using ZenStates.Core;

namespace Saku_Overclock.Services;

public class ZenstatesCoreProvider : IDataProvider
{
    private float _currentCpuLoad;
    private int _globalCoreCounter = -1;
    private readonly Cpu _cpu = CpuSingleton.GetInstance();
    private uint _tableVersion;

    // Переиспользуемые массивы для предотвращения аллокаций
    private double[] _clkPerCoreCache = [];
    private double[] _voltPerCoreCache = [];
    private double[] _tempPerCoreCache = [];
    private double[] _powerPerCoreCache = [];

    // Кэш строк для предотвращения аллокаций
    private static class SensorNames
    {
        public const string CpuFrequencyStart = "CpuFrequencyStart";
        public const string CpuVoltageStart = "CpuVoltageStart";
        public const string CpuTemperatureStart = "CpuTemperatureStart";
        public const string CpuPowerStart = "CpuPowerStart";
    }

    /// <summary>
    ///     Реализация получения информации через Zenstates Core
    /// </summary>
    /// <returns>SensorsInformation</returns>
    public SensorsInformation GetDataAsync()
    {
        RefreshPowerTable();

        if (_globalCoreCounter == -1)
        {
            _globalCoreCounter = (int)_cpu.info.topology.physicalCores;
            InitializeCoreArrays();
        }

        _tableVersion = _cpu.smu.TableVersion;

        var (avgCoreClk, avgCoreVolt) = CalculateCoreMetrics();

        return new SensorsInformation
        {
            // powerTable Может быть нулевой на некотором оборудовании (ОЧЕНЬ редко)!
            CpuFamily = _cpu.info.codeName.ToString(), // Не стоит менять, работает везде
            CpuUsage = GetCoreLoad(), // Это попрошу оставить, работает везде, использует winAPI
            CpuStapmLimit = GetSensorValue("CpuStapmLimit", _tableVersion),
            CpuStapmValue = GetSensorValue("CpuStapmValue", _tableVersion),
            CpuFastLimit = GetSensorValue("CpuFastLimit", _tableVersion),
            CpuFastValue = GetSensorValue("CpuFastValue", _tableVersion),
            CpuSlowLimit = GetSensorValue("CpuSlowLimit", _tableVersion),
            CpuSlowValue = GetSensorValue("CpuSlowValue", _tableVersion),
            VrmTdcValue = GetVrmValue("VrmTdcValue", _tableVersion),
            VrmEdcValue = GetVrmValue("VrmEdcValue", _tableVersion),
            VrmTdcLimit = GetVrmLimit("VrmTdcLimit", _tableVersion),
            VrmEdcLimit = GetVrmLimit("VrmEdcLimit", _tableVersion),
            CpuTempValue = GetSensorValue("CpuTempValue", _tableVersion, _cpu.GetCpuTemperature() ?? 0f),
            CpuTempLimit = GetSensorValue("CpuTempLimit", _tableVersion, 100),
            MemFrequency = _cpu.powerTable?.MCLK ?? 0,
            FabricFrequency = _cpu.powerTable?.FCLK ?? 0,
            SocPower = GetSensorValue("SocPower", _tableVersion, (_cpu.powerTable?.VDDCR_SOC ?? 0) * 10),
            SocVoltage = _cpu.powerTable?.VDDCR_SOC ?? 0,
            CpuFrequency = avgCoreClk,
            CpuFrequencyPerCore = _clkPerCoreCache,
            CpuVoltage = avgCoreVolt,
            CpuVoltagePerCore = _voltPerCoreCache,
            CpuTemperaturePerCore = _tempPerCoreCache,
            CpuPowerPerCore = _powerPerCoreCache,

            // Параметры которые есть не на каждом процессоре
            ApuSlowLimit = GetSensorValue("ApuSlowLimit", _tableVersion),
            ApuSlowValue = GetSensorValue("ApuSlowValue", _tableVersion),
            VrmPsiValue = GetSensorValue("VrmPsiValue", _tableVersion),
            VrmPsiSocValue = GetSensorValue("VrmPsiSocValue", _tableVersion),
            SocTdcValue = GetSensorValue("SocTdcValue", _tableVersion),
            SocTdcLimit = GetSensorValue("SocTdcLimit", _tableVersion),
            SocEdcValue = GetSensorValue("SocEdcValue", _tableVersion),
            SocEdcLimit = GetSensorValue("SocEdcLimit", _tableVersion),
            ApuTempValue = GetSensorValue("ApuTempValue", _tableVersion),
            ApuTempLimit = GetSensorValue("ApuTempLimit", _tableVersion),
            DgpuTempValue = GetSensorValue("DgpuTempValue", _tableVersion),
            DgpuTempLimit = GetSensorValue("DgpuTempLimit", _tableVersion),
            CpuStapmTimeValue = GetSensorValue("CpuStapmTimeValue", _tableVersion),
            CpuSlowTimeValue = GetSensorValue("CpuSlowTimeValue", _tableVersion),
            ApuFrequency = GetSensorValue("ApuFrequency", _tableVersion),
            ApuTemperature = GetSensorValue("ApuTemperature", _tableVersion),
            ApuVoltage = GetSensorValue("ApuVoltage", _tableVersion)
        };
    }

    #region Get Information voids

    /// <summary>
    ///     Возвращает таблицу PowerTable целиком для дальнейшей обработки, например для методов OC Finder
    /// </summary>
    public float[]? GetPowerTable() => _cpu.powerTable?.Table;

    /// <summary>
    ///     Обновляет PowerTable
    /// </summary>
    private void RefreshPowerTable()
    {
        try
        {
            _cpu.RefreshPowerTable();
        }
        catch
        {
            //
        }
    }

    /// <summary>
    ///     Словарь маппинга разных версий таблицы PowerTable к определённым сенсорам доступным на этой версии таблицы.
    ///     Принимает: Table Version, Value: Словарь для (Index -> Sensor Name)
    /// </summary>
    private static readonly Dictionary<uint, List<(uint Offset, string Name)>> SupportedPmTableVersions = new()
    {
        // Zen 2
        {
            0x00240803, [
                (0, "CpuFastLimit"),
                (29, "CpuFastValue"),
                (0, "CpuSlowLimit"),
                (1, "CpuSlowValue"),
                (2, "VrmTdcLimit"),
                (3, "VrmTdcValue"),
                (4, "CpuTempLimit"),
                (5, "CpuTempValue"),
                (8, "VrmEdcLimit"),
                (9, "VrmEdcValue"),
                (25, "SocPower"),
                (45, "SocVoltage"),
                (42, "VrmPsiValue"),
                (46, "VrmPsiSocValue"),
                (46, "SocTdcValue"),
                (8, "SocTdcLimit"),
                (46, "SocEdcValue"),
                (8, "SocEdcLimit"),
                (147, "CpuPowerStart"),
                (163, "CpuVoltageStart"),
                (179, "CpuTemperatureStart"),
                (227, "CpuFrequencyStart")
            ]
        },
        {
            0x00240903, [
                (0, "CpuFastLimit"),
                (29, "CpuFastValue"),
                (0, "CpuSlowLimit"),
                (1, "CpuSlowValue"),
                (2, "VrmTdcLimit"),
                (3, "VrmTdcValue"),
                (4, "CpuTempLimit"),
                (5, "CpuTempValue"),
                (8, "VrmEdcLimit"),
                (9, "VrmEdcValue"),
                (25, "SocPower"),
                (45, "SocVoltage"),
                (41, "VrmPsiValue"),
                (46, "VrmPsiSocValue"),
                (46, "SocTdcValue"),
                (8, "SocTdcLimit"),
                (46, "SocEdcValue"),
                (8, "SocEdcLimit"),
                (147, "CpuPowerStart"),
                (155, "CpuVoltageStart"),
                (163, "CpuTemperatureStart"),
                (187, "CpuFrequencyStart")
            ]
        },
        // Zen 3
        {
            0x00380904, [
                (0, "CpuFastLimit"),
                (29, "CpuFastValue"),
                (0, "CpuSlowLimit"),
                (1, "CpuSlowValue"),
                (2, "VrmTdcLimit"),
                (3, "VrmTdcValue"),
                (4, "CpuTempLimit"),
                (5, "CpuTempValue"),
                (8, "VrmEdcLimit"),
                (9, "VrmEdcValue"),
                (25, "SocPower"),
                (45, "SocVoltage"),
                (42, "VrmPsiValue"),
                (46, "VrmPsiSocValue"),
                (46, "SocTdcValue"),
                (8, "SocTdcLimit"),
                (46, "SocEdcValue"),
                (8, "SocEdcLimit"),
                (169, "CpuPowerStart"),
                (177, "CpuVoltageStart"),
                (185, "CpuTemperatureStart"),
                (209, "CpuFrequencyStart")
            ]
        },
        {
            0x00380905, [
                (0, "CpuFastLimit"),
                (29, "CpuFastValue"),
                (0, "CpuSlowLimit"),
                (1, "CpuSlowValue"),
                (2, "VrmTdcLimit"),
                (3, "VrmTdcValue"),
                (4, "CpuTempLimit"),
                (5, "CpuTempValue"),
                (8, "VrmEdcLimit"),
                (9, "VrmEdcValue"),
                (25, "SocPower"),
                (45, "SocVoltage"),
                (42, "VrmPsiValue"),
                (46, "VrmPsiSocValue"),
                (46, "SocTdcValue"),
                (8, "SocTdcLimit"),
                (46, "SocEdcValue"),
                (8, "SocEdcLimit"),
                (172, "CpuPowerStart"),
                (180, "CpuVoltageStart"),
                (188, "CpuTemperatureStart"),
                (212, "CpuFrequencyStart")
            ]
        },
        {
            0x00380804, [
                (0, "CpuFastLimit"),
                (29, "CpuFastValue"),
                (0, "CpuSlowLimit"),
                (1, "CpuSlowValue"),
                (2, "VrmTdcLimit"),
                (3, "VrmTdcValue"),
                (4, "CpuTempLimit"),
                (5, "CpuTempValue"),
                (8, "VrmEdcLimit"),
                (9, "VrmEdcValue"),
                (25, "SocPower"),
                (45, "SocVoltage"),
                (42, "VrmPsiValue"),
                (46, "VrmPsiSocValue"),
                (46, "SocTdcValue"),
                (8, "SocTdcLimit"),
                (46, "SocEdcValue"),
                (8, "SocEdcLimit"),
                (169, "CpuPowerStart"),
                (185, "CpuVoltageStart"),
                (201, "CpuTemperatureStart"),
                (249, "CpuFrequencyStart")
            ]
        },
        {
            0x00380805, [
                (0, "CpuFastLimit"),
                (29, "CpuFastValue"),
                (0, "CpuSlowLimit"),
                (1, "CpuSlowValue"),
                (2, "VrmTdcLimit"),
                (3, "VrmTdcValue"),
                (4, "CpuTempLimit"),
                (5, "CpuTempValue"),
                (8, "VrmEdcLimit"),
                (9, "VrmEdcValue"),
                (25, "SocPower"),
                (45, "SocVoltage"),
                (42, "VrmPsiValue"),
                (46, "VrmPsiSocValue"),
                (46, "SocTdcValue"),
                (8, "SocTdcLimit"),
                (46, "SocEdcValue"),
                (8, "SocEdcLimit"),
                (172, "CpuPowerStart"),
                (188, "CpuVoltageStart"),
                (204, "CpuTemperatureStart"),
                (252, "CpuFrequencyStart")
            ]
        },
        // Zen 4
        {
            0x00540004, [
                (0, "CpuStapmLimit"),
                (1, "CpuStapmValue"),
                (2, "CpuFastLimit"),
                (26, "CpuFastValue"),
                (2, "CpuSlowLimit"),
                (3, "CpuSlowValue"),
                (8, "VrmTdcLimit"),
                (8, "VrmEdcLimit"),
                (48, "VrmTdcValue"),
                (49, "VrmEdcValue"),
                (10, "CpuTempLimit"),
                (11, "CpuTempValue"),
                (21, "SocPower"),
                (52, "SocVoltage"),
                (293, "CpuPowerStart"),
                (309, "CpuVoltageStart"),
                (325, "CpuTemperatureStart"),
                (341, "CpuFrequencyStart")
            ]
        }
    };

    /// <summary>
    ///     Получает значение сенсора по имени из PM таблицы
    /// </summary>
    private float GetSensorValue(string sensorName, uint tableVersion, float fallbackValue = 0f)
    {
        if (_cpu.powerTable?.Table == null)
        {
            return fallbackValue;
        }

        // Проверяем, поддерживается ли версия таблицы
        if (!SupportedPmTableVersions.TryGetValue(tableVersion, out var sensorMap))
        {
            LogHelper.LogWarn($"Unsupported PM table version: 0x{tableVersion:X8}");
            return fallbackValue;
        }

        // Ищем индекс сенсора по имени
        var sensorIndex = sensorMap.FirstOrDefault(kvp => kvp.Name == sensorName).Offset;

        // Проверяем границы массива
        if (sensorIndex >= _cpu.powerTable.Table.Length)
        {
            LogHelper.LogWarn($"Sensor index {sensorIndex} out of bounds for {sensorName}");
            return fallbackValue;
        }

        var value = _cpu.powerTable.Table[sensorIndex];
        return value != 0 ? value : fallbackValue;
    }

    /// <summary>
    ///     Получает значение VRM с поддержкой альтернативных индексов
    /// </summary>
    private float GetVrmValue(string baseSensorName, uint tableVersion)
    {
        if (_cpu.powerTable?.Table == null)
        {
            return 0f;
        }

        var primaryValue = GetSensorValue(baseSensorName, tableVersion);

        // Если основное значение нулевое, пробуем альтернативные индексы
        if (primaryValue == 0)
        {
            var altValue = GetSensorValue($"{baseSensorName}_Alt", tableVersion);
            if (altValue != 0)
            {
                return altValue;
            }

            altValue = GetSensorValue($"{baseSensorName}_Alt2", tableVersion);
            if (altValue != 0)
            {
                return altValue;
            }
        }

        return primaryValue;
    }

    /// <summary>
    ///     Получает лимит VRM с поддержкой альтернативных индексов
    /// </summary>
    private float GetVrmLimit(string baseSensorName, uint tableVersion)
    {
        if (_cpu.powerTable?.Table == null)
        {
            return 0f;
        }

        var primaryValue = GetSensorValue(baseSensorName, tableVersion);

        // Если основное значение нулевое, пробуем VrmEdcLimit как fallback
        if (primaryValue == 0)
        {
            return GetSensorValue($"{baseSensorName}_Alt", tableVersion);
        }

        return primaryValue;
    }

    /// <summary>
    ///     Первая инициализация безопасных массивов различных сенсоров процессора
    /// </summary>
    private void InitializeCoreArrays()
    {
        _clkPerCoreCache = new double[_globalCoreCounter];
        _voltPerCoreCache = new double[_globalCoreCounter];
        _tempPerCoreCache = new double[_globalCoreCounter];
        _powerPerCoreCache = new double[_globalCoreCounter];
    }

    /// <summary>
    ///     Обновление массивов различных сенсоров процессора
    /// </summary>
    private (double avgCoreClk, double avgCoreVolt) CalculateCoreMetrics()
    {
        var startFreqIndex = GetStartIndex(SensorNames.CpuFrequencyStart);
        var startVoltIndex = GetStartIndex(SensorNames.CpuVoltageStart);
        var startTempIndex = GetStartIndex(SensorNames.CpuTemperatureStart);
        var startPowerIndex = GetStartIndex(SensorNames.CpuPowerStart);

        double sumClk = 0, sumVolt = 0;
        int validClk = 0, validVolt = 0;

        for (var core = 0; core < _cpu.info.topology.cores; core++)
        {
            // Частота
            var clk = GetCoreMetric(startFreqIndex, core, 0.2, 8.0);
            if (clk > 0)
            {
                _clkPerCoreCache[core] = Math.Round(clk, 3);
                sumClk += _clkPerCoreCache[core];
                validClk++;
            }
            else
            {
                var fallbackClk = _cpu.GetCoreMulti(core) / 10; // Fallback на GetCoreMulti (Legacy)
                _clkPerCoreCache[core] = Math.Round(fallbackClk, 3);
                if (fallbackClk > 0.38)
                {
                    sumClk += _clkPerCoreCache[core];
                    validClk++;
                }
            }

            // Напряжение
            var volt = GetCoreMetric(startVoltIndex, core, 0.4, 2.0);
            if (volt > 0)
            {
                _voltPerCoreCache[core] = Math.Round(volt, 4);
                sumVolt += _voltPerCoreCache[core];
                validVolt++;
            }

            // Температура
            var temp = GetCoreMetric(startTempIndex, core, -300, 150);
            if (temp > 0)
            {
                _tempPerCoreCache[core] = Math.Round(temp, 2);
            }

            // Мощность
            var power = GetCoreMetric(startPowerIndex, core, 0.001, 1000);
            if (power > 0)
            {
                _powerPerCoreCache[core] = Math.Round(power, 2);
            }
        }

        var avgClk = validClk > 0 ? sumClk / validClk : 0;
        var avgVolt = validVolt > 0 ? sumVolt / validVolt : 0;

        return (avgClk, avgVolt);
    }

    /// <summary>
    ///     Возвращает стартовый индекс для определённого индекса из словаря SupportedPmTableVersions
    /// </summary>
    private int GetStartIndex(string sensorName)
    {
        if (_cpu.powerTable?.Table == null)
        {
            return -1;
        }

        if (!SupportedPmTableVersions.TryGetValue(_tableVersion, out var sensorMap))
        {
            return -1;
        }

        foreach (var (offset, _) in from kvp in sensorMap
                 where kvp.Name == sensorName
                 select kvp)
        {
            return (int)offset;
        }

        return -1;
    }

    /// <summary>
    ///     Маппинг (Core -> Index) с учётом отключенных ядер процессора
    /// </summary>
    private readonly Dictionary<int, int[]> _activeCoreToPhysicalIndex = [];

    /// <summary>
    ///     Создаёт маппинг (Core -> Index) с учётом отключенных ядер процессора
    /// </summary>
    private void BuildActiveCoreMapping(int startIndex)
    {
        if (_cpu.powerTable?.Table == null || startIndex == -1)
        {
            _activeCoreToPhysicalIndex.Clear();
            return;
        }

        var mapping = new List<int>();
        for (var physIndex = 0; physIndex < _globalCoreCounter; physIndex++)
        {
            var tableIndex = startIndex + physIndex;
            if (tableIndex >= _cpu.powerTable.Table.Length)
            {
                break;
            }

            if (_cpu.powerTable.Table[tableIndex] != 0)
            {
                mapping.Add(tableIndex);
            }
        }

        _activeCoreToPhysicalIndex.Add(startIndex, [.. mapping]);
    }

    /// <summary>
    ///     Возвращает значение сенсора для определённого индекса и проверяет его на корректность
    /// </summary>
    private double GetCoreMetric(int startIndex, int logicalCore, double minValid, double maxValid)
    {
        // Убедимся, что маппинг построен
        if (_activeCoreToPhysicalIndex.Capacity == 0 ||
            !_activeCoreToPhysicalIndex.ContainsKey(startIndex))
        {
            BuildActiveCoreMapping(startIndex);
        }

        // Проверяем доступность значения
        if (logicalCore < 0 ||
            logicalCore > _globalCoreCounter ||
            _activeCoreToPhysicalIndex[startIndex].Length < logicalCore ||
            !_activeCoreToPhysicalIndex.TryGetValue(startIndex, out var values) ||
            values.Length < logicalCore)
        {
            return 0;
        }

        var physicalTableIndex = values[logicalCore];

        if (physicalTableIndex >= _cpu.powerTable!.Table.Length)
        {
            return 0;
        }

        var value = _cpu.powerTable.Table[physicalTableIndex];

        // Дополнительная проверка на валидность (на случай, если 0 — валидное значение)
        if (value >= minValid && value <= maxValid)
        {
            return value;
        }

        return 0;
    }

    /// <summary>
    ///     Возвращает значение загрузки процессора в процента, используя NtDll методы и нативную реализацию из диспетчера
    ///     задач Windows
    /// </summary>
    private float GetCoreLoad()
    {
        if (!GetTimes(out var newIdleTimes, out var newTotalTimes))
        {
            return _currentCpuLoad; // Возвращаем предыдущее значение
        }

        // Если это первый запуск - сохраняем данные и возвращаем 0
        if (_idleTimes.Length == 0 || _totalTimes.Length == 0)
        {
            _totalTimes = newTotalTimes;
            _idleTimes = newIdleTimes;
            return 0;
        }

        // Проверяем минимальную разность времени
        for (var i = 0; i < Math.Min(newTotalTimes.Length, _totalTimes.Length); i++)
        {
            if (newTotalTimes[i] - _totalTimes[i] < 100000)
            {
                return _currentCpuLoad; // Возвращаем предыдущее значение
            }
        }

        // Вычисляем общую загрузку
        float totalIdle = 0;
        var count = Math.Min(_idleTimes.Length, newIdleTimes.Length);

        for (var i = 0; i < count; i++)
        {
            var idle = (newIdleTimes[i] - _idleTimes[i]) / (float)(newTotalTimes[i] - _totalTimes[i]);
            idle = Math.Max(0.0f, Math.Min(1.0f, idle));
            totalIdle += idle;
        }

        float result = 0;
        if (count > 0)
        {
            var total = 1.0f - totalIdle / count;
            result = (float)Math.Round(Math.Max(0.001f, Math.Min(1.0f, total)) * 100.0f, 2);
        }

        // ВАЖНО: Обновляем времена ВСЕГДА после успешного получения данных
        _totalTimes = newTotalTimes;
        _idleTimes = newIdleTimes;

        _currentCpuLoad = result;
        return result;
    }

    /// <summary>
    ///     Проверка на определённые версии Windows где время в простое необходимо считать отдельно
    /// </summary>
    private static readonly bool QueryIdleTimeSeparated =
        Environment.OSVersion.Version >= new Version(10, 0, 22621, 0) &&
        Environment.OSVersion.Version < new Version(10, 0, 26100, 0);

    private long[] _idleTimes = [];
    private long[] _totalTimes = [];

    /// <summary>
    ///     Возвращает значение загрузки процессора всего и в простое
    /// </summary>
    private static bool GetTimes(out long[] idle, out long[] total)
    {
        idle = [];
        total = [];

        if (QueryIdleTimeSeparated)
        {
            return GetWindowsTimesFromIdleTimes(out idle, out total);
        }

        // Стандартный метод через SystemProcessorPerformanceInformation
        var perfInfo = ArrayPool<CpuPerformanceInformation>.Shared.Rent(64);
        var perfSize = Marshal.SizeOf<CpuPerformanceInformation>();

        if (NtQuerySystemInformation(
                CpuSysInformation.SystemProcessorPerformanceInformation,
                perfInfo, perfInfo.Length * perfSize, out var perfReturn) != 0)
        {
            return false;
        }

        var count = perfReturn / perfSize;
        idle = new long[count];
        total = new long[count];

        for (var i = 0; i < count; i++)
        {
            idle[i] = perfInfo[i].IdleTime;
            total[i] = perfInfo[i].KernelTime + perfInfo[i].UserTime;
        }

        ArrayPool<CpuPerformanceInformation>.Shared.Return(perfInfo);
        return true;
    }

    /// <summary>
    ///     Возвращает значение загрузки процессора в производительном состоянии и в простое (для более новыз систем)
    /// </summary>
    private static bool GetWindowsTimesFromIdleTimes(out long[] idle, out long[] total)
    {
        idle = [];
        total = [];

        var perfInfo = ArrayPool<CpuPerformanceInformation>.Shared.Rent(64);
        var perfSize = Marshal.SizeOf<CpuPerformanceInformation>();
        var idleInfo = ArrayPool<CpuIdleInformation>.Shared.Rent(64);
        var idleSize = Marshal.SizeOf<CpuIdleInformation>();

        // Получаем idle информацию
        if (NtQuerySystemInformation(
                CpuSysInformation.SystemProcessorIdleInformation,
                idleInfo, idleInfo.Length * idleSize, out var idleReturn) != 0)
        {
            return false;
        }

        // Получаем performance информацию
        if (NtQuerySystemInformation(
                CpuSysInformation.SystemProcessorPerformanceInformation,
                perfInfo, perfInfo.Length * perfSize, out var perfReturn) != 0)
        {
            return false;
        }

        var count = perfReturn / perfSize;
        if (count != idleReturn / idleSize)
        {
            return false;
        }

        idle = new long[count];
        total = new long[count];

        for (var i = 0; i < count; i++)
        {
            idle[i] = idleInfo[i].IdleTime;
            total[i] = perfInfo[i].KernelTime + perfInfo[i].UserTime;
        }

        ArrayPool<CpuPerformanceInformation>.Shared.Return(perfInfo);
        ArrayPool<CpuIdleInformation>.Shared.Return(idleInfo);
        return true;
    }

    #region NtDll voids

    private const string DllName = "ntdll.dll";

    [StructLayout(LayoutKind.Sequential)]
    private struct CpuPerformanceInformation
    {
        public long IdleTime;
        public long KernelTime;
        public long UserTime;
        public long DpcTime;
        public long InterruptTime;
        public uint InterruptCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CpuIdleInformation
    {
        public long IdleTime;
        public long C1Time;
        public long C2Time;
        public long C3Time;
        public uint C1Transitions;
        public uint C2Transitions;
        public uint C3Transitions;
        public uint Padding;
    }

    private enum CpuSysInformation
    {
        SystemProcessorPerformanceInformation = 8,
        SystemProcessorIdleInformation = 42
    }

    [DllImport(DllName)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int NtQuerySystemInformation(
        CpuSysInformation systemInformationClass,
        [Out] CpuPerformanceInformation[] systemInformation,
        int systemInformationLength,
        out int returnLength);

    [DllImport(DllName)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int NtQuerySystemInformation(
        CpuSysInformation systemInformationClass,
        [Out] CpuIdleInformation[] systemInformation,
        int systemInformationLength,
        out int returnLength);

    #endregion

    #endregion
}