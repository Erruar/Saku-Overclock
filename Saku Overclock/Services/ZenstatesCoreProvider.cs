using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.SmuEngine;

namespace Saku_Overclock.Services;

public class ZenstatesCoreProvider : IDataProvider
{
    private readonly ISensorReader _sensorReader;
    private readonly ISensorIndexResolver _indexResolver;
    private readonly CoreMetricsCalculator _metricsCalculator;

    private bool _isInitialized;
    private bool _unsupportedPmTableAlert;

    public ZenstatesCoreProvider(
        ISensorReader sensorReader,
        ISensorIndexResolver indexResolver,
        CoreMetricsCalculator metricsCalculator)
    {
        _sensorReader = sensorReader;
        _indexResolver = indexResolver;
        _metricsCalculator = metricsCalculator;
    }

    /// <summary>
    /// Реализация получения информации через Zenstates Core
    /// </summary>
    public void GetData(ref SensorsInformation sensorsInformation)
    {
        // Обновляем таблицу
        if (!_sensorReader.RefreshTable())
        {
            return;
        }

        // Инициализация при первом запуске
        if (!_isInitialized)
        {
            var totalCores = _sensorReader.GetTotalCoresTopology();
            if (totalCores > 0)
            {
                _metricsCalculator.Initialize(totalCores);
                _isInitialized = true;
            }
            else
            {
                return;
            }
        }

        var tableVersion = _sensorReader.CurrentTableVersion;

        // Проверяем поддержку версии таблицы
        if (!IsSupportedTableVersion(tableVersion))
        {
            if (!_unsupportedPmTableAlert)
            {
                LogHelper.LogWarn($"Unsupported PM table version: 0x{tableVersion:X8}");
                _unsupportedPmTableAlert = true;
            }
        }

        // Получаем метрики процессора
        var (avgCoreClk, avgCoreVolt, clkPerCore, voltPerCore, tempPerCore, powerPerCore) =
            _metricsCalculator.CalculateMetrics();

        // Базовая информация
        sensorsInformation.CpuFamily = _sensorReader.GetCodeName();
        sensorsInformation.CpuUsage = _metricsCalculator.GetCoreLoad();

        // Лимиты и значения STAPM/Fast/Slow
        sensorsInformation.CpuStapmLimit = GetSensorValue(SensorId.CpuStapmLimit);
        sensorsInformation.CpuStapmValue = GetSensorValue(SensorId.CpuStapmValue);
        sensorsInformation.CpuFastLimit = GetSensorValue(SensorId.CpuFastLimit);
        sensorsInformation.CpuFastValue = GetSensorValue(SensorId.CpuFastValue);
        sensorsInformation.CpuSlowLimit = GetSensorValue(SensorId.CpuSlowLimit);
        sensorsInformation.CpuSlowValue = GetSensorValue(SensorId.CpuSlowValue);

        // VRM токи
        sensorsInformation.VrmTdcValue = GetSensorValue(SensorId.VrmTdcValue);
        sensorsInformation.VrmTdcLimit = GetSensorValue(SensorId.VrmTdcLimit);
        sensorsInformation.VrmEdcValue = GetSensorValue(SensorId.VrmEdcValue);
        sensorsInformation.VrmEdcLimit = GetSensorValue(SensorId.VrmEdcLimit);

        // Температуры
        var (cpuTempSuccess, cpuTempDirect) = _sensorReader.GetCpuTemperature();
        sensorsInformation.CpuTempValue = GetSensorValue(SensorId.CpuTempValue, cpuTempSuccess ? (float)cpuTempDirect : 0f);
        sensorsInformation.CpuTempLimit = GetSensorValue(SensorId.CpuTempLimit, 100);

        // Частоты памяти и фабрики (специальные поля)
        var (mclkSuccess, mclkValue) = _sensorReader.ReadSpecialValue("MCLK");
        var (fclkSuccess, fclkValue) = _sensorReader.ReadSpecialValue("FCLK");
        sensorsInformation.MemFrequency = mclkSuccess ? mclkValue : 0;
        sensorsInformation.FabricFrequency = fclkSuccess ? fclkValue : 0;

        // SoC мощность и напряжение
        var (socVoltSuccess, socVolt) = _sensorReader.ReadSpecialValue("VDDCR_SOC");
        sensorsInformation.SocPower = GetSensorValue(SensorId.SocPower, socVoltSuccess ? (float)(socVolt * 10) : 0f);
        sensorsInformation.SocVoltage = socVoltSuccess ? socVolt : 0;

        // Частота и напряжение процессора
        sensorsInformation.CpuFrequency = avgCoreClk;
        sensorsInformation.CpuVoltage = avgCoreVolt;
        sensorsInformation.CpuFrequencyPerCore = clkPerCore;
        sensorsInformation.CpuVoltagePerCore = voltPerCore;
        sensorsInformation.CpuTemperaturePerCore = tempPerCore;
        sensorsInformation.CpuPowerPerCore = powerPerCore;

        // Дополнительные параметры (не на всех процессорах)
        sensorsInformation.ApuSlowLimit = GetSensorValue(SensorId.ApuSlowLimit);
        sensorsInformation.ApuSlowValue = GetSensorValue(SensorId.ApuSlowValue);
        sensorsInformation.VrmPsiValue = GetSensorValue(SensorId.VrmPsiValue);
        sensorsInformation.VrmPsiSocValue = GetSensorValue(SensorId.VrmPsiSocValue);
        sensorsInformation.SocTdcValue = GetSensorValue(SensorId.SocTdcValue);
        sensorsInformation.SocTdcLimit = GetSensorValue(SensorId.SocTdcLimit);
        sensorsInformation.SocEdcValue = GetSensorValue(SensorId.SocEdcValue);
        sensorsInformation.SocEdcLimit = GetSensorValue(SensorId.SocEdcLimit);
        sensorsInformation.ApuTempValue = GetSensorValue(SensorId.ApuTempValue);
        sensorsInformation.ApuTempLimit = GetSensorValue(SensorId.ApuTempLimit);
        sensorsInformation.DgpuTempValue = GetSensorValue(SensorId.DgpuTempValue);
        sensorsInformation.DgpuTempLimit = GetSensorValue(SensorId.DgpuTempLimit);
        sensorsInformation.CpuStapmTimeValue = GetSensorValue(SensorId.CpuStapmTimeValue);
        sensorsInformation.CpuSlowTimeValue = GetSensorValue(SensorId.CpuSlowTimeValue);
        sensorsInformation.ApuFrequency = GetSensorValue(SensorId.ApuFrequency);
        sensorsInformation.ApuVoltage = GetSensorValue(SensorId.ApuVoltage);
    }

    /// <summary>
    /// Возвращает таблицу PowerTable для дальнейшей обработки (например, для OC Finder)
    /// </summary>
    public float[]? GetPowerTable() => _sensorReader.GetFullTable();

    /// <summary>
    /// Получает значение сенсора по идентификатору
    /// </summary>
    private float GetSensorValue(SensorId sensorId, float fallbackValue = 0f)
    {
        var tableVersion = _sensorReader.CurrentTableVersion;
        var index = _indexResolver.ResolveIndex(tableVersion, sensorId);

        if (index == -1)
        {
            return fallbackValue;
        }

        var (success, value) = _sensorReader.ReadSensorByIndex(index);

        if (!success || value == 0)
        {
            return fallbackValue;
        }

        return (float)value;
    }

    /// <summary>
    /// Проверяет, поддерживается ли версия таблицы
    /// </summary>
    private static bool IsSupportedTableVersion(int tableVersion)
    {
        return tableVersion switch
        {
            0x001E0004 => true,
            0x00190001 => true,
            0x00240803 => true,
            0x00240903 => true,
            0x00380904 => true,
            0x00380905 => true,
            0x00380804 => true,
            0x00380805 => true,
            0x00540004 => true,
            0x00620205 => true,
            _ => false
        };
    }
}