using System.Management;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.SMUEngine;
using ZenStates.Core;

namespace Saku_Overclock.Services;

public class ZenstatesCoreProvider : IDataProvider
{
    private List<double> _currentCpuClocks = [];
    private float _currentCpuLoad;
    private int _globalCoreCounter = -1;
    private readonly Cpu Cpu = CpuSingleton.GetInstance();
    private readonly Dictionary<int, int> CoreIndexMap = [];
    private int _cpuLoadIndex;
    private float[]? _cachedTable;
    // Флаг для проверки готовности таблицы
    private bool _isInitialized;
    private bool _isInitializing;
    public async Task<SensorsInformation> GetDataAsync()
    {
        // Здесь реализация через Zenstates Core (с WMI, таблицами FIRM и т.п.)
        await RefreshPowerTable();
        await InitializeCoreIndexMapAsync((int)Cpu.info.topology.cores);
        
        await Task.Delay(50);
        var (avgCoreClk, clkPerClock) = CalculateCoreMetrics();
        return new SensorsInformation
        {
            CpuFamily = Cpu.info.codeName.ToString(),
            CpuStapmLimit = GetStapmValue(),
            CpuStapmValue = GetStapmLimit(),
            CpuFastLimit = GetFastLimit(),
            CpuFastValue = GetFastValue(),
            CpuSlowLimit = GetSlowLimit(),
            CpuSlowValue = GetSlowValue(),
            VrmTdcValue = GetVrmedcValue(),
            VrmTdcLimit = GetVrmedcLimit(),
            VrmEdcValue = GetVrmedcValue(),
            VrmEdcLimit = GetVrmedcLimit(),
            CpuTempValue = GetTctlValue(),
            CpuTempLimit = GetTctlLimit(),
            CpuUsage = GetCoreLoad(),
            MemFrequency = Cpu.powerTable.MCLK,
            FabricFrequency = Cpu.powerTable.FCLK,
            SocPower = Cpu.powerTable.VDDCR_SOC * 10,
            SocVoltage = Cpu.powerTable.VDDCR_SOC,
            CpuFrequency = avgCoreClk,
            CpuFrequencyPerCore = clkPerClock,
        };
    }

    public float[]? GetPowerTable()
    {
        return Cpu.powerTable.Table;
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
        if (_isInitialized || _isInitializing) { return; }
        _isInitializing = true;
        CoreIndexMap.Clear();
        // Асинхронная загрузка WMI
        await Task.Run(() =>
        {
            if (_currentCpuClocks.Count == 0) { _currentCpuClocks = GetSystemInfo.GetCurrentClockSpeedsMHz(coreCounter); }
            if (_currentCpuLoad == 0) { _currentCpuLoad = (float)GetSystemInfo.GetCurrentUtilisation(); }
            if (coreCounter == 0)
            {
                if (_globalCoreCounter == -1 || _globalCoreCounter == 0)
                {
                    var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Processor");
                    foreach (var queryObj in searcher.Get().Cast<ManagementObject>())
                    {
                        coreCounter = Convert.ToInt32(queryObj["NumberOfCores"]);
                    }
                    _globalCoreCounter = coreCounter;
                }
                coreCounter = _globalCoreCounter;
            }
            else
            {
                _globalCoreCounter = coreCounter;
            }
            for (var core = 0; core < coreCounter; core++)
            {
                var index = FindIndexInPowerTable(_currentCpuClocks[core]);
                if (index >= 0)
                {
                    CoreIndexMap[core] = index;
                }
            }
            _cpuLoadIndex = FindIndexInPowerTable(_currentCpuLoad);
        }).ConfigureAwait(false);
        _isInitialized = true;
    }
    private async Task AsyncWmiGetCoreFreq(int coreCounter) /* Не используется ! */
    {
        await Task.Run(() =>
        {
            _currentCpuClocks = GetSystemInfo.GetCurrentClockSpeedsMHz(coreCounter); // Через WMI. Медленно. ОЧЕНЬ МЕДЛЕННО.
            _currentCpuLoad = (float)GetSystemInfo.GetCurrentUtilisation(); // Через WMI. Медленно. ОЧЕНЬ МЕДЛЕННО.
        });
    }

    private async Task RefreshPowerTable()
    {
        try
        {
            await Task.Run(() =>
            {
                Cpu.RefreshPowerTable();
                _cachedTable = Cpu.powerTable.Table;
            }).ConfigureAwait(false);
        }
        catch
        {
            //
        }
    }

    private int FindIndexInPowerTable(double clockSpeedMHz)
    {
        if (_cachedTable == null)
        {
            return -1;
        }
        for (var i = 0; i < _cachedTable.Length; i++)
        {
            if (Math.Abs(_cachedTable[i] - clockSpeedMHz) < 0.100 && _cachedTable[i] > 0.38) // Допустимая погрешность
            {
                return i;
            }
        }
        return -1;
    }
    private float GetCoreLoad()
    {
        if (!_isInitialized)
        {
            return _currentCpuLoad;
        }
        if (_cpuLoadIndex == 0)
        {
            return 0;
        }
        return (_cachedTable != null && _cachedTable.Length >= _cpuLoadIndex && _cpuLoadIndex >= 0) ? _cachedTable[_cpuLoadIndex] : 0;
    }
    private float GetStapmLimit() => _cachedTable != null ? _cachedTable[0] : 0;

    private float GetStapmValue() => _cachedTable != null ? _cachedTable[1] : 0;

    private float GetFastLimit() => _cachedTable != null ? _cachedTable[2] : 0;

    private float GetFastValue() => _cachedTable != null ? _cachedTable[3] : 0;

    private float GetSlowLimit() => _cachedTable != null ? _cachedTable[4] : 0;

    private float GetSlowValue() => _cachedTable != null ? _cachedTable[5] : 0;

    private float GetVrmedcLimit() => _cachedTable != null ? (_cachedTable[6] != 0 ? _cachedTable[6] : _cachedTable[8]) : 0;

    private float GetVrmedcValue() => _cachedTable != null ? (_cachedTable[6] != 0 ? _cachedTable[7] : _cachedTable[9]) : 0;

    private float GetTctlLimit() => _cachedTable != null ? _cachedTable[10] : 0;

    private float GetTctlValue() => _cachedTable != null ? _cachedTable[11] : 0;


    private float GetCoreClock(uint core)
    {
        return (float)Cpu.GetCoreMulti((int)core)/10;
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
    #endregion
}

