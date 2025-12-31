using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;

namespace Saku_Overclock.Services;

public class SensorReader : ISensorReader
{
    private float[]? _table;
    private readonly ICpuService? Cpu;

    public int CurrentTableVersion
    {
        get; private set;
    }

    public SensorReader(ICpuService cpuService)
    {
        Cpu = cpuService;
        if (Cpu != null)
        {
            // Инициализируем версию таблицы
            UpdateTableVersion();
        }
    }

    public bool RefreshTable()
    {
        try
        {
            if (Cpu == null)
            {
                return false;
            }

            // Обновляем таблицу через ZenStates.Core
            Cpu.RefreshPowerTable();

            // Получаем обновлённую таблицу
            _table = Cpu.PowerTable;

            // Обновляем версию таблицы
            UpdateTableVersion();

            return _table != null;
        }
        catch (Exception ex)
        {
            LogHelper.LogError("RefreshTable failed: " + ex);
            return false;
        }
    }

    public (bool success, double value) ReadSensorByIndex(int index)
    {
        try
        {
            if (Cpu == null || _table == null)
            {
                return (false, 0);
            }

            if (index < 0 || index >= _table.Length)
            {
                return (false, 0);
            }

            var value = _table[index];
            return (true, value);
        }
        catch
        {
            return (false, 0);
        }
    }

    public enum SpecialValueType
    {
        Mclk,
        Fclk,
        VddcrSoc
    }

    /// <summary>
    /// Получает прямой доступ к полям powerTable для специальных значений (MCLK, FCLK, и т.д.)
    /// </summary>
    public (bool success, double value) ReadSpecialValue(SpecialValueType type)
    {
        try
        {
            if (Cpu == null || _table == null)
            {
                return (false, 0);
            }

            return type switch
            {
                SpecialValueType.Mclk => (true, Cpu.SocMemoryClock),
                SpecialValueType.Fclk => (true, Cpu.SocFabricClock),
                SpecialValueType.VddcrSoc => (true, Cpu.SocVoltage),
                _ => (false, 0)
            };
        }
        catch
        {
            return (false, 0);
        }
    }

    /// <summary>
    /// Получает температуру процессора напрямую через метод GetCpuTemperature
    /// </summary>
    public (bool success, double value) GetCpuTemperature()
    {
        try
        {
            if (Cpu == null)
            {
                return (false, 0);
            }

            var temp = Cpu.GetCpuTemperature();
            return temp.HasValue ? (true, temp.Value) : (false, 0);
        }
        catch
        {
            return (false, 0);
        }
    }

    /// <summary>
    /// Получает множитель ядра (для fallback частоты)
    /// </summary>
    public (bool success, double value) GetCoreMulti(int coreIndex)
    {
        try
        {
            if (Cpu == null)
            {
                return (false, 0);
            }

            var multi = Cpu.GetCoreMultiplier(coreIndex);
            return (true, multi);
        }
        catch
        {
            return (false, 0);
        }
    }

    /// <summary>
    /// Получает информацию о топологии процессора
    /// </summary>
    public int GetTotalCoresTopology()
    {
        if (Cpu == null)
        {
            return 0;
        }

        return (int)Cpu.Cores;
    }

    /// <summary>
    /// Получает кодовое имя процессора
    /// </summary>
    public string GetCodeName()
    {
        return Cpu?.CpuCodeName ?? "Unsupported";
    }

    /// <summary>
    /// Возвращает полную таблицу для специальных случаев (например, OC Finder)
    /// </summary>
    public float[]? GetFullTable()
    {
        return _table;
    }

    /// <summary>
    /// Обновляет версию таблицы с учётом особенностей разных поколений
    /// </summary>
    private void UpdateTableVersion()
    {
        if (Cpu == null)
        {
            CurrentTableVersion = 0;
            return;
        }

        var tableVersion = Cpu.PowerTableVersion;

        var codenameGen = Cpu.GetCodenameGeneration();

        // Zen fallback
        if (tableVersion == 0 && codenameGen == CpuService.CodenameGeneration.AM4_V1)
        {
            tableVersion = 0x00190001;
        }

        // Zen 2/Zen 3 override
        if (tableVersion is 0x00380705 or 0x00380605 or 0x00380505 or 0x00380005)
        {
            tableVersion = 0x00380805;
        }

        if (codenameGen == CpuService.CodenameGeneration.AM5)
        {
            var baseRevision = (tableVersion >> 16) & 0xFFFF;
            if (baseRevision == 0x54 
                && tableVersion != 0x00540004 
                && tableVersion != 0x00540104 
                && tableVersion != 0x00540208)
            {
                // Zen 4 override
                tableVersion = 0x00540004;
            }
            else if (baseRevision == 0x62 
                && tableVersion != 0x00620205 
                && tableVersion != 0x00620105)
            {
                // Zen 5 override
                tableVersion = 0x00620205;
            }
        }
        

        CurrentTableVersion = (int)tableVersion;
    }
}