using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.SmuEngine;
using ZenStates.Core;

namespace Saku_Overclock.Services;

public class SensorReader : ISensorReader
{
    private readonly Cpu? _cpu;
    private float[]? _table;

    public int CurrentTableVersion
    {
        get; private set;
    }

    public SensorReader()
    {
        try
        {
            _cpu = CpuSingleton.GetInstance();

            if (_cpu != null)
            {
                // Инициализируем версию таблицы
                UpdateTableVersion();
            }
        }
        catch (Exception e)
        {
            LogHelper.LogError("SensorReader initialization failed: " + e);
        }
    }

    public bool RefreshTable()
    {
        try
        {
            if (_cpu == null)
            {
                return false;
            }

            // Обновляем таблицу через ZenStates.Core
            _cpu.RefreshPowerTable();

            // Получаем обновлённую таблицу
            _table = _cpu.powerTable?.Table;

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
            if (_table == null || _cpu?.powerTable?.Table == null)
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
            if (_cpu?.powerTable == null)
            {
                return (false, 0);
            }

            return type switch
            {
                SpecialValueType.Mclk => (true, _cpu.powerTable.MCLK),
                SpecialValueType.Fclk => (true, _cpu.powerTable.FCLK),
                SpecialValueType.VddcrSoc => (true, _cpu.powerTable.VDDCR_SOC),
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
            if (_cpu == null)
            {
                return (false, 0);
            }

            var temp = _cpu.GetCpuTemperature();
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
            if (_cpu == null)
            {
                return (false, 0);
            }

            var multi = _cpu.GetCoreMulti(coreIndex);
            return (true, multi / 10.0); // Конвертируем в GHz
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
        if (_cpu?.info.topology == null)
        {
            return 0;
        }

        return (int)_cpu.info.topology.cores;
    }

    /// <summary>
    /// Получает кодовое имя процессора
    /// </summary>
    public string GetCodeName()
    {
        return _cpu?.info.codeName.ToString() ?? "Unknown";
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
        if (_cpu == null)
        {
            CurrentTableVersion = 0;
            return;
        }

        var tableVersion = _cpu.smu.TableVersion;

        // Zen fallback
        if (tableVersion == 0 && _cpu.info.codeName is Cpu.CodeName.SummitRidge or Cpu.CodeName.PinnacleRidge)
        {
            tableVersion = 0x00190001;
        }

        // Zen 2/Zen 3 override
        if (tableVersion is 0x00380705 or 0x00380605 or 0x00380505 or 0x00380005)
        {
            tableVersion = 0x00380805;
        }

        tableVersion = _cpu.info.codeName switch
        {
            // Zen 4 override
            Cpu.CodeName.Raphael or Cpu.CodeName.DragonRange or Cpu.CodeName.StormPeak when
                tableVersion != 0x00540004 && tableVersion != 0x00540104 && tableVersion != 0x00540208 => 0x00540004,
            // Zen 5 override
            Cpu.CodeName.GraniteRidge or Cpu.CodeName.Genoa or Cpu.CodeName.Bergamo when tableVersion != 0x00620205 &&
                tableVersion != 0x00620105 => 0x00620205,
            _ => tableVersion
        };

        CurrentTableVersion = (int)tableVersion;
    }
}