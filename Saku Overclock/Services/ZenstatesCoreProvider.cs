using System.Runtime.InteropServices;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.SMUEngine;
using ZenStates.Core;

namespace Saku_Overclock.Services;

public class ZenstatesCoreProvider : IDataProvider
{
    private readonly List<double> _coreMultiplierCache = []; // Кэш множителей ядер
    private float _currentCpuLoad;
    private int _globalCoreCounter = -1;
    private readonly Cpu Cpu = CpuSingleton.GetInstance();
    private readonly Dictionary<int, int> _coreToTableIndexMap = []; // Маппинг: ядро -> индекс в таблице
    private float[]? _cachedTable;
    // Флаг для проверки готовности таблицы
    private bool _isInitialized;
    public async Task<SensorsInformation> GetDataAsync()
    {
        // Здесь реализация через Zenstates Core (с WMI, таблицами Power_Table и т.п.)
        RefreshPowerTable();
        await InitializeCoreIndexMapAsync((int)Cpu.info.topology.cores);

        await Task.Delay(50);
        var (avgCoreClk, clkPerClock) = CalculateCoreMetrics();
        return new SensorsInformation
        {
            CpuFamily = Cpu.info.codeName.ToString(),
            CpuStapmLimit = _cachedTable?[0] ?? 0,
            CpuStapmValue = _cachedTable?[1] ?? 0,
            CpuFastLimit = _cachedTable?[2] ?? 0,
            CpuFastValue = _cachedTable?[3] ?? 0,
            CpuSlowLimit = _cachedTable?[4] ?? 0,
            CpuSlowValue = _cachedTable?[5] ?? 0,
            VrmTdcValue = _cachedTable != null ? (_cachedTable[6] != 0 ? _cachedTable[7] : _cachedTable[9]) : 0,
            VrmEdcValue = _cachedTable != null ? (_cachedTable[6] != 0 ? _cachedTable[7] : _cachedTable[9]) : 0,
            VrmTdcLimit = _cachedTable != null ? (_cachedTable[6] != 0 ? _cachedTable[6] : _cachedTable[8]) : 0,
            VrmEdcLimit = _cachedTable != null ? (_cachedTable[6] != 0 ? _cachedTable[6] : _cachedTable[8]) : 0,
            CpuTempValue = _cachedTable?[11] ?? Cpu.GetCpuTemperature() ?? 0d,
            CpuTempLimit = _cachedTable?[10] ?? 100,
            CpuUsage = GetCoreLoad(),
            MemFrequency = Cpu.powerTable?.MCLK ?? 0,
            FabricFrequency = Cpu.powerTable?.FCLK ?? 0,
            SocPower = (Cpu.powerTable?.VDDCR_SOC ?? 0) * 10,
            SocVoltage = Cpu.powerTable?.VDDCR_SOC ?? 0,
            CpuFrequency = avgCoreClk,
            CpuFrequencyPerCore = clkPerClock,
        };
    }

    public float[]? GetPowerTable() => Cpu.powerTable?.Table;

    private void RefreshPowerTable()
    {
        try
        {

            Cpu.RefreshPowerTable();
            _cachedTable = Cpu.powerTable?.Table;

        }
        catch
        {
            //
        }
    }

    private (double avgCoreClk, double[] clkPerClock) CalculateCoreMetrics()
    {
        double sumCoreClk = 0;
        var validCoreCount = 0;
        List<double> clkPerClock = [];
        for (uint f = 0; f < _globalCoreCounter; f++)
        {
            var clk = Math.Round(GetCoreClock(f), 3);
            if (clk > 0) // Исключаем нули и -1
            {
                clkPerClock.Add(clk);
                sumCoreClk += clk;
                validCoreCount++;
            }
        }

        var avgCoreClk = validCoreCount > 0 ? sumCoreClk / validCoreCount : 0;

        return (avgCoreClk, clkPerClock.ToArray());
    }

    #region Get Information voids
    private async Task InitializeCoreIndexMapAsync(int coreCounter)
    {
        if (_isInitialized) { return; }

        _isInitialized = true;
        _globalCoreCounter = coreCounter; // Всего ядер в процессоре

        // Асинхронная загрузка WMI
        await Task.Run(() =>
        {
            // Получаем текущие частоты через CPU Multiplier для каждого ядра
            UpdateCoreMultiplierCache();

            // Строим маппинг ядер к индексам Power Table
            BuildCoreToTableMapping();
        }).ConfigureAwait(false); // Не в UI потоке
    }

    private void UpdateCoreMultiplierCache()
    {
        for (var core = 0; core < Cpu.info.topology.physicalCores; core++)
        {
            try
            {
                // Получаем множитель и конвертируем в ГГц
                var multiplier = (float)Cpu.GetCoreMulti(core) / 10f;
                if (multiplier > 0.38) // Частота больше минимальных 0.4 ГГц
                {
                    _coreMultiplierCache.Add(multiplier);
                }
            }
            catch (Exception ex)
            {
                _coreMultiplierCache.Add(0);
                LogHelper.LogWarn(ex.ToString());
            }
        }
    }
    private void BuildCoreToTableMapping()
    {
        _coreToTableIndexMap.Clear();

        if (_cachedTable == null || _coreMultiplierCache.Count == 0)
        {
            return;
        }

        // Получаем активные ядра (с частотой > 0.38)
        var activeCores = new List<int>();
        var disabledCores = new List<int>();
        for (var i = 0; i < Cpu.info.topology.physicalCores; i++)
        {
            var mapIndex = i < 8 ? 0 : 1;
            if ((~Cpu.info.topology.coreDisableMap[mapIndex] >> i % 8 & 1) != 0)

            {
                activeCores.Add(i);
            }
            else
            {
                disabledCores.Add(i);
            }
        }

        if (activeCores.Count == 0)
        {
            return;
        }

        // Ищем последовательности частот в таблице
        var frequencyGroups = FindFrequencyGroupsInTable(disabledCores);

        // Сопоставляем ядра с группами частот
        ValidateFrequencyGroups(frequencyGroups);
    }

    private List<(int startIndex, List<(int index, float frequency)> frequencies)> FindFrequencyGroupsInTable(List<int> disabledCores)
    {
        if (_cachedTable == null)
        {
            return [];
        }

        var groups = new List<(int startIndex, List<(int index, float frequency)> frequencies)>();

        // Определяем максимальное количество ядер в CCD (обычно 8)
        const int maxCoresPerCcd = 16;

        // Создаем шаблон позиций отключенных ядер относительно начала группы
        var disabledOffsets = disabledCores.OrderBy(x => x).ToList();

        for (int i = 0; i < _cachedTable.Length - maxCoresPerCcd; i++)
        {
            // Проверяем, может ли здесь начинаться группа частот ядер
            if (!IsValidFrequencyGroupStart(i, disabledOffsets, maxCoresPerCcd))
            {
                continue;
            }

            // Собираем группу частот
            var group = CollectFrequencyGroup(i, disabledOffsets, maxCoresPerCcd);

            if (group.Any() && IsValidFrequencyGroup(group))
            {
                groups.Add((i, group));

                // Логируем найденную группу
                var freqStr = string.Join(", ", group.Select(f => $"{f.index}:{f.frequency:F3}"));
                LogHelper.Log($"Found frequency group: start={i}, frequencies=[{freqStr}]");
            }
        }

        LogHelper.Log($"Total frequency groups found: {groups.Count}");
        return groups;
    }

    private bool IsValidFrequencyGroupStart(int startIndex, List<int> disabledOffsets, int maxCoresPerCcd)
    {
        // Проверяем, что в позициях отключенных ядер действительно нули
        foreach (var offset in disabledOffsets)
        {
            var checkIndex = startIndex + offset;
            if (checkIndex >= _cachedTable.Length || _cachedTable[checkIndex] != 0)
            {
                return false;
            }
        }

        // Проверяем, что остальные значения в пределах разумного диапазона частот
        for (int i = 0; i < maxCoresPerCcd && (startIndex + i) < _cachedTable.Length; i++)
        {
            // Пропускаем позиции отключенных ядер
            if (disabledOffsets.Contains(i))
            {
                continue;
            }

            var value = _cachedTable[startIndex + i];
            // Проверяем, что значение похоже на частоту процессора (0.5 - 6.0 ГГц)
            if (value != 0 && (value < 0.5f || value > 6.0f))
            {
                return false;
            }
        }

        return true;
    }

    private List<(int index, float frequency)> CollectFrequencyGroup(int startIndex, List<int> disabledOffsets, int maxCoresPerCcd)
    {
        var group = new List<(int index, float frequency)>();

        for (int i = 0; i < maxCoresPerCcd && (startIndex + i) < _cachedTable.Length; i++)
        {
            var currentIndex = startIndex + i;
            var frequency = _cachedTable[currentIndex];

            // Добавляем все значения, кроме нулей от отключенных ядер
            if (frequency != 0 || disabledOffsets.Contains(i))
            {
                group.Add((currentIndex, frequency));
            }
        }

        return group;
    }

    private bool IsValidFrequencyGroup(List<(int index, float frequency)> group)
    {
        // Группа должна содержать хотя бы одно ненулевое значение
        if (!group.Any(g => g.frequency > 0))
        {
            return false;
        }

        // Проверяем, что все ненулевые значения находятся в разумном диапазоне частот
        var validFrequencies = group.Where(g => g.frequency > 0).Select(g => g.frequency).ToList();

        if (validFrequencies.Count == 0)
        {
            return false;
        }

        // Все частоты должны быть в пределах разумного диапазона
        if (validFrequencies.Any(freq => freq < 0.5f || freq > 6.0f))
        {
            return false;
        }

        // Проверяем, что частоты не слишком разные (разброс не более 2 ГГц между мин и макс)
        var minFreq = validFrequencies.Min();
        var maxFreq = validFrequencies.Max();

        if (maxFreq - minFreq > 2.0f)
        {
            return false;
        }

        return true;
    }

    private void ValidateFrequencyGroups(List<(int startIndex, List<(int index, float frequency)> frequencies)> frequencyGroups)
    {
        if (frequencyGroups.Count == 0 || _coreMultiplierCache == null)
        {
            return;
        }

        _coreToTableIndexMap.Clear();

        // Преобразуем множители в частоты и найдём среднюю частоту
        var averageFreq = GetAverage(_coreMultiplierCache);

        var usedTableIndices = new HashSet<int>();
        var usedCores = new HashSet<int>();

        // Проходим по каждой группе частот в таблице
        foreach (var (groupStartIndex, frequencies) in frequencyGroups)
        {
            // Фильтруем только ненулевые частоты из группы
            var validFrequencies = frequencies
                .Where(f => f.frequency > 0)
                .OrderByDescending(f => f.frequency)
                .ToList();

            if (validFrequencies.Count == 0)
            {
                continue;
            }

            var groupList = new List<double>(); 
            foreach (var (index, frequency) in validFrequencies) 
            {
                groupList.Add(frequency);
            }
            var averageGroupFreq = GetAverage(groupList);

            if (Math.Abs(averageFreq - averageGroupFreq) <= 0.5)
            {
                var paramCounter = 0;
                for (var i = 0; i < frequencies.Count; i++) 
                {
                    if (frequencies.Count == Cpu.info.topology.physicalCores && frequencies[i].frequency > 0)
                    {
                        _coreToTableIndexMap[paramCounter] = frequencies[i].index;
                        paramCounter++;
                    }
                }
            }


           /* // Пытаемся сопоставить ядра с частотами в текущей группе
            var groupMatches = new List<(int core, int tableIndex, float tableFreq)>();

            foreach (var (index, frequency) in validFrequencies)
            {
                if (usedTableIndices.Contains(index))
                {
                    continue;
                }

                // Ищем наиболее подходящее ядро для этой частоты
                var bestCoreMatch = coresByFrequency
                    .Where(c => !usedCores.Contains(c.Core))
                    .Select(c => new { c.Core, c.Frequency, Diff = Math.Abs(c.Frequency - frequency) })
                    .Where(x => x.Diff <= 0.3f) // Допуск 300 МГц
                    .OrderBy(x => x.Diff)
                    .FirstOrDefault();

                if (bestCoreMatch != null)
                {
                    groupMatches.Add((bestCoreMatch.Core, index, frequency));
                    usedCores.Add(bestCoreMatch.Core);
                    usedTableIndices.Add(index);
                }
            }

            // Добавляем найденные соответствия в маппинг
            foreach (var (core, tableIndex, _) in groupMatches)
            {
                _coreToTableIndexMap[core] = tableIndex;
            }

            // Если сопоставили все активные ядра, выходим
            if (usedCores.Count >= activeCores.Count)
            {
                break;
            }*/
        }

        // Логируем результат для отладки
        LogHelper.Log($"Core mapping result: {string.Join(", ", _coreToTableIndexMap.Select(kvp => $"Core{kvp.Key}→{kvp.Value}"))}");
    }

    private static float GetAverage(List<double> list, double lowerThan = 0.38, int divideBy = 1)
    {
        var coresAvgFrequency = 0d;
        var coresAvfCount = 0;
        foreach (var frequency in list)
        {
            if (frequency > lowerThan)
            {
                coresAvgFrequency += frequency / divideBy;
                coresAvfCount++;
            }
        }

        if (coresAvfCount == 0) { coresAvfCount = 1; }

        return (float)(coresAvgFrequency / coresAvfCount);
    }

    private float GetCoreClock(uint core)
    {
        var coreInt = (int)core;

        // Проверяем кэш маппинга к Power Table
        if (_coreToTableIndexMap.TryGetValue(coreInt, out var tableIndex) &&
            _cachedTable != null &&
            tableIndex < _cachedTable.Length)
        {
            var tableFreq = _cachedTable[tableIndex];
            if (tableFreq >= 0.38)
            {
                return tableFreq;
            }
        }

        // Последний fallback - прямое обращение к CPU Multiplier
        return (float)Cpu.GetCoreMulti((int)core) / 10; // В итоге приложение всегда падает сюда

        /*if (!_isInitialized)
        {
            return _currentCpuClocks.Count - 1 > (int)core ? (float)_currentCpuClocks[(int)core] : -1f;
        }
        if (!CoreIndexMap.TryGetValue((int)core, out var value))
        {
            return -1;
        }

        if (_cachedTable == null || value >= _cachedTable.Length)
        {
            LogHelper.TraceIt_TraceError("Cached table is invalid or out of range.");
            return -1;
        }
        if (_cachedTable[value] >= 7)
        {
            foreach (var el in _cachedTable)
            {
                if (el < 7)
                {
                    return el;
                }
            }
        }
        return _cachedTable[value];*/
    }

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
            var total = 1.0f - (totalIdle / count);
            result = (float)Math.Round(Math.Max(0.001f, Math.Min(1.0f, total)) * 100.0f, 2);
        }

        // ВАЖНО: Обновляем времена ВСЕГДА после успешного получения данных
        _totalTimes = newTotalTimes;
        _idleTimes = newIdleTimes;

        _currentCpuLoad = result;
        return result;
    }

    private static readonly bool _queryIdleTimeSeparated = Environment.OSVersion.Version >= new Version(10, 0, 22621, 0) && Environment.OSVersion.Version < new Version(10, 0, 26100, 0);

    private long[] _idleTimes = [];
    private long[] _totalTimes = [];

    private static bool GetTimes(out long[] idle, out long[] total)
    {
        idle = [];
        total = [];

        if (_queryIdleTimeSeparated)
        {
            return GetWindowsTimesFromIdleTimes(out idle, out total);
        }

        // Стандартный метод через SystemProcessorPerformanceInformation
        var perfInfo = new Cpu_Performance_Information[64];
        var perfSize = Marshal.SizeOf<Cpu_Performance_Information>();

        if (NtQuerySystemInformation(
            Cpu_Sys_Information.SystemProcessorPerformanceInformation,
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

        return true;
    }

    private static bool GetWindowsTimesFromIdleTimes(out long[] idle, out long[] total)
    {
        idle = [];
        total = [];

        var perfInfo = new Cpu_Performance_Information[64];
        var perfSize = Marshal.SizeOf<Cpu_Performance_Information>();
        var idleInfo = new Cpu_Idle_Information[64];
        var idleSize = Marshal.SizeOf<Cpu_Idle_Information>();

        // Получаем idle информацию
        if (NtQuerySystemInformation(
            Cpu_Sys_Information.SystemProcessorIdleInformation,
            idleInfo, idleInfo.Length * idleSize, out var idleReturn) != 0)
        {
            return false;
        }

        // Получаем performance информацию
        if (NtQuerySystemInformation(
            Cpu_Sys_Information.SystemProcessorPerformanceInformation,
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

        return true;
    }

    #region NtDll voids

    private const string DllName = "ntdll.dll";

    [StructLayout(LayoutKind.Sequential)]
    internal struct Cpu_Performance_Information
    {
        public long IdleTime;
        public long KernelTime;
        public long UserTime;
        public long DpcTime;
        public long InterruptTime;
        public uint InterruptCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Cpu_Idle_Information
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

    internal enum Cpu_Sys_Information
    {
        SystemProcessorPerformanceInformation = 8,
        SystemProcessorIdleInformation = 42
    }

    [DllImport(DllName)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int NtQuerySystemInformation(Cpu_Sys_Information SystemInformationClass, [Out] Cpu_Performance_Information[] SystemInformation, int SystemInformationLength, out int ReturnLength);

    [DllImport(DllName)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int NtQuerySystemInformation(Cpu_Sys_Information SystemInformationClass, [Out] Cpu_Idle_Information[] SystemInformation, int SystemInformationLength, out int ReturnLength);

    #endregion

    #endregion
}

