using System.Buffers;
using System.Runtime.InteropServices;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.SmuEngine;

namespace Saku_Overclock.Services;

/// <summary>
/// Класс для расчёта метрик процессора: частота, напряжение, температура, мощность per-core
/// и общая загрузка процессора
/// </summary>
public class CoreMetricsCalculator
{
    private readonly ISensorReader _sensorReader;
    private readonly ISensorIndexResolver _indexResolver;
    private int _coreCount;

    // Кэш массивов для предотвращения аллокаций
    private double[] _clkPerCoreCache = [];
    private double[] _voltPerCoreCache = [];
    private double[] _tempPerCoreCache = [];
    private double[] _powerPerCoreCache = [];

    // Данные для расчёта загрузки CPU
    private float _currentCpuLoad;
    private long[] _idleTimes = [];
    private long[] _totalTimes = [];

    // Маппинг активных ядер к физическим индексам
    private readonly Dictionary<int, int[]> _activeCoreToPhysicalIndex = [];

    /// <summary>
    /// Проверка на определённые версии Windows, где время в простое необходимо считать отдельно
    /// </summary>
    private static readonly bool QueryIdleTimeSeparated =
        Environment.OSVersion.Version >= new Version(10, 0, 22621, 0) &&
        Environment.OSVersion.Version < new Version(10, 0, 26100, 0);

    private readonly bool _isRavenFamily;
    private readonly bool _isHawkPointFamily;

    public CoreMetricsCalculator(ISensorReader sensorReader, ISensorIndexResolver indexResolver)
    {
        try
        {
            var codeName = CpuSingleton.GetInstance().info.codeName;
            if (codeName is ZenStates.Core.Cpu.CodeName.RavenRidge
                or ZenStates.Core.Cpu.CodeName.Picasso
                or ZenStates.Core.Cpu.CodeName.Dali)
            {
                _isRavenFamily = true;
            }

            if (codeName == ZenStates.Core.Cpu.CodeName.HawkPoint)
            {
                _isHawkPointFamily = true;
            }
        }
        catch
        {
            LogHelper.LogError("[CoreMetricsCalculator]@ Failed to get Cpu instance");
        }

        _sensorReader = sensorReader;
        _indexResolver = indexResolver;
    }

    /// <summary>
    /// Инициализирует массивы для хранения per-core метрик
    /// </summary>
    public void Initialize(int coreCount)
    {
        _coreCount = coreCount;
        _clkPerCoreCache = new double[_isRavenFamily && _coreCount == 2 ? 4 : _coreCount];
        _voltPerCoreCache = new double[coreCount];
        _tempPerCoreCache = new double[coreCount];
        _powerPerCoreCache = new double[coreCount];
    }

    /// <summary>
    /// Рассчитывает все метрики процессора и возвращает средние значения
    /// </summary>
    public (double avgCoreClk, double avgCoreVolt, double[] clkPerCore, double[] voltPerCore,
        double[] tempPerCore, double[] powerPerCore) CalculateMetrics()
    {
        if (_coreCount == 0)
        {
            return (0, 0, [], [], [], []);
        }

        var tableVersion = _sensorReader.CurrentTableVersion;
        var startFreqIndex = _indexResolver.ResolveIndex(tableVersion, SensorId.CpuFrequencyStart);
        var startVoltIndex = _indexResolver.ResolveIndex(tableVersion, SensorId.CpuVoltageStart);
        var startTempIndex = _indexResolver.ResolveIndex(tableVersion, SensorId.CpuTemperatureStart);
        var startPowerIndex = _indexResolver.ResolveIndex(tableVersion, SensorId.CpuPowerStart);

        double sumClk = 0, sumVolt = 0;
        int validClk = 0, validVolt = 0;

        // Для Raven с 2 ядрами читаем 4 потока для частоты, но только 2 ядра для остального
        for (var core = 0; core < (_isRavenFamily && _coreCount == 2 ? 4 : _coreCount); core++)
        {
            // Частота - может быть больше массива для Raven
            if (core < _clkPerCoreCache.Length)
            {
                var clk = GetCoreMetric(startFreqIndex, core, 0.2, 8.0);
                if (clk > 0)
                {
                    _clkPerCoreCache[core] = Math.Round(clk, 3);
                    sumClk += _clkPerCoreCache[core];
                    validClk++;
                }
                else
                {
                    // Fallback на GetCoreMulti
                    var (success, fallbackClk) = _sensorReader.GetCoreMulti(core);
                    if (success)
                    {
                        _clkPerCoreCache[core] = Math.Round(fallbackClk, 3);
                        if (fallbackClk > 0.38)
                        {
                            sumClk += _clkPerCoreCache[core];
                            validClk++;
                        }
                    }
                }
            }

            // Напряжение
            if (core < _voltPerCoreCache.Length)
            {
                var volt = GetCoreMetric(startVoltIndex, core, 0.4, 2.0);
                if (volt > 0)
                {
                    _voltPerCoreCache[core] = Math.Round(volt, 4);
                    sumVolt += _voltPerCoreCache[core];
                    validVolt++;
                }
            }

            // Температура
            if (core < _tempPerCoreCache.Length)
            {
                var temp = GetCoreMetric(startTempIndex, core, -300, 150);
                if (temp > 0)
                {
                    _tempPerCoreCache[core] = Math.Round(temp, 2);
                }
            }

            // Мощность
            if (core < _powerPerCoreCache.Length)
            {
                var power = GetCoreMetric(startPowerIndex, core, 0.001, 1000);
                if (power > 0)
                {
                    _powerPerCoreCache[core] = Math.Round(power, 2);
                }
            }
        }

        var avgClk = validClk > 0 ? sumClk / validClk : 0;
        var avgVolt = validVolt > 0 ? sumVolt / validVolt : 0;

        return (avgClk, avgVolt, _clkPerCoreCache, _voltPerCoreCache, _tempPerCoreCache, _powerPerCoreCache);
    }

    /// <summary>
    /// Возвращает значение метрики для конкретного ядра с проверкой границ
    /// </summary>
    private double GetCoreMetric(int startIndex, int logicalCore, double minValid, double maxValid)
    {
        try
        {
            if (startIndex == -1)
            {
                return 0;
            }

            // Убедимся, что маппинг построен
            if (_activeCoreToPhysicalIndex.Count == 0 || !_activeCoreToPhysicalIndex.ContainsKey(startIndex))
            {
                BuildActiveCoreMapping(startIndex);
            }

            // Проверяем наличие ключа
            if (!_activeCoreToPhysicalIndex.TryGetValue(startIndex, out var values))
            {
                return 0;
            }

            // Проверяем доступность значения
            if (logicalCore < 0 || logicalCore >=
                (_isRavenFamily && _coreCount == 2 ? 4 : _coreCount)
                || values.Length <= logicalCore)
            {
                return 0;
            }

            var physicalTableIndex = values[logicalCore];

            // Читаем значение через SensorReader
            var (success, value) = _sensorReader.ReadSensorByIndex(physicalTableIndex);

            if (!success)
            {
                return 0;
            }

            // Проверка на валидность
            if (value >= minValid && value <= maxValid)
            {
                return value;
            }

            return 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Создаёт маппинг активных ядер к физическим индексам в таблице
    /// </summary>
    private void BuildActiveCoreMapping(int startIndex)
    {
        if (startIndex == -1)
        {
            _activeCoreToPhysicalIndex.Clear();
            return;
        }

        var table = _sensorReader.GetFullTable();
        if (table == null)
        {
            _activeCoreToPhysicalIndex.Clear();
            return;
        }

        var mapping = new List<int>();
        var coresCount = NormalizedCoreCount(startIndex);

        for (var physIndex = 0; physIndex < coresCount; physIndex++)
        {
            var tableIndex = startIndex + physIndex;
            if (tableIndex >= table.Length)
            {
                break;
            }

            if (table[tableIndex] != 0)
            {
                mapping.Add(tableIndex);
            }
        }

        _activeCoreToPhysicalIndex[startIndex] = [.. mapping];
    }

    /// <summary>
    /// Нормализует количество ядер до фактического количества в CCD/CCX.
    /// </summary>
    /// <param name="startIndex">Стартовый индекс последовательности слотов-индексов</param>
    /// <returns>Нормализованное количество ядер процессора</returns>
    private int NormalizedCoreCount(int startIndex)
    {
        // Определение ядёр для корректного прохода по PM Table.
        // В PM Table AMD могут присутствовать "отключенные" (fused-off) ядра,
        // но они всё равно занимают слоты-индексы. Чтобы не потерять реальные данные
        // и не уехать по смещению, мы рассчитываем coresCount с учетом фактической
        // структуры кристалла (CCD/CCX) и архитектурных исключений.

        var coresCount = _coreCount;

        // Специальный фикс для Hawk Point: при старте со смещения 105 читаем напряжение процессора.
        // Берём напряжение только из SVI2, так как индексы по ядрам возвращают некорректные значения
        // (баг в прошивке Smu)
        if (_isHawkPointFamily && startIndex == 105)
        {
            return 2;
        }

        // Raven Ridge и совместимые семейства: при старте со смещения 121 читаем частоту процессора.
        // 2-ядерные отображают последовательность ядра+потоки через нулевое значение (отключенные ядра),
        // поэтому учитываем fused структуру как 8 слотов-индексов.
        if (_coreCount == 2 && _isRavenFamily && startIndex == 121)
        {
            return 8;
        }

        // Общее правило для остальных Ryzen: младшие процессоры имеют "отключенные" (fused-off) ядра,
        // но PM Table сохраняет полный ряд значений для всего кристалла.
        // Поэтому нормализуем количество слотов до фактического количества в CCD/CCX.
        if (_coreCount > 8)
        {
            coresCount = 16;
        }
        else if (_coreCount > 4)
        {
            coresCount = 8;
        }
        else if (_coreCount > 1)
        {
            coresCount = 4;
        }

        return coresCount;
    }

    #region CPU Load Calculation

    /// <summary>
    /// Возвращает текущую загрузку процессора в процентах
    /// </summary>
    public float GetCoreLoad()
    {
        if (!GetTimes(out var newIdleTimes, out var newTotalTimes))
        {
            return _currentCpuLoad;
        }

        // Первый запуск - сохраняем данные
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
                return _currentCpuLoad;
            }
        }

        // Вычисляем загрузку
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

        // Обновляем времена
        _totalTimes = newTotalTimes;
        _idleTimes = newIdleTimes;
        _currentCpuLoad = result;

        return result;
    }

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
    ///     Возвращает значение загрузки процессора в производительном состоянии и в простое (для более новых систем)
    /// </summary>
    private static bool GetWindowsTimesFromIdleTimes(out long[] idle, out long[] total)
    {
        idle = [];
        total = [];

        var perfInfo = ArrayPool<CpuPerformanceInformation>.Shared.Rent(64);
        var perfSize = Marshal.SizeOf<CpuPerformanceInformation>();
        var idleInfo = ArrayPool<CpuIdleInformation>.Shared.Rent(64);
        var idleSize = Marshal.SizeOf<CpuIdleInformation>();

        if (NtQuerySystemInformation(
                CpuSysInformation.SystemProcessorIdleInformation,
                idleInfo, idleInfo.Length * idleSize, out var idleReturn) != 0)
        {
            return false;
        }

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

    #endregion

    #region NtDll Structures and Imports

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
}