using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.SmuEngine;

namespace Saku_Overclock.Services;

public class ZenstatesCoreProvider(
    ISensorReader sensorReader,
    ISensorIndexResolver indexResolver,
    CoreMetricsCalculator metricsCalculator)
    : IDataProvider
{
    private bool _isInitialized;
    private bool _unsupportedPmTableAlert;

    /// <summary>
    ///     Реализация получения информации через Zenstates Core
    /// </summary>
    public void GetData(ref SensorsInformation sensorsInformation)
    {
        // Обновляем таблицу
        if (!sensorReader.RefreshTable())
        {
            return;
        }

        // Инициализация при первом запуске
        if (!_isInitialized)
        {
            var totalCores = sensorReader.GetTotalCoresTopology();
            if (totalCores > 0)
            {
                metricsCalculator.Initialize(totalCores);
                _isInitialized = true;
            }
            else
            {
                return;
            }
        }

        var tableVersion = sensorReader.CurrentTableVersion;

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
            metricsCalculator.CalculateMetrics();

        // Базовая информация
        sensorsInformation.CpuFamily = sensorReader.GetCodeName();
        sensorsInformation.CpuUsage = metricsCalculator.GetCoreLoad();

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
        var (cpuTempSuccess, cpuTempDirect) = sensorReader.GetCpuTemperature();
        sensorsInformation.CpuTempValue =
            GetSensorValue(SensorId.CpuTempValue, cpuTempSuccess ? (float)cpuTempDirect : 0f);
        sensorsInformation.CpuTempLimit = GetSensorValue(SensorId.CpuTempLimit, 100);

        // Частоты памяти и фабрики (специальные поля)
        var (mclkSuccess, mclkValue) = sensorReader.ReadSpecialValue(SensorReader.SpecialValueType.Mclk);
        var (fclkSuccess, fclkValue) = sensorReader.ReadSpecialValue(SensorReader.SpecialValueType.Fclk);
        sensorsInformation.MemFrequency = mclkSuccess ? mclkValue : 0;
        sensorsInformation.FabricFrequency = fclkSuccess ? fclkValue : 0;


        var (socVoltSuccess, socVolt) = sensorReader.ReadSpecialValue(SensorReader.SpecialValueType.VddcrSoc);
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
    ///     Возвращает таблицу PowerTable для дальнейшей обработки (например, для OC Finder)
    /// </summary>
    public float[]? GetPowerTable() => sensorReader.GetFullTable();

    /// <summary>
    ///     Получает значение сенсора по идентификатору
    /// </summary>
    private float GetSensorValue(SensorId sensorId, float fallbackValue = 0f)
    {
        const int hawkPointPowerTableVerion = 0x004C0009;
        const int hawkPointApuVoltageAltIndex = 39;

        var tableVersion = sensorReader.CurrentTableVersion;
        var index = indexResolver.ResolveIndex(tableVersion, sensorId);

        if (index == -1)
        {
            return fallbackValue;
        }

        var (success, value) = sensorReader.ReadSensorByIndex(index);

        if (sensorId == SensorId.ApuVoltage && value == 0 &&
            sensorReader.CurrentTableVersion == hawkPointPowerTableVerion)
        {
            (success, value) = sensorReader.ReadSensorByIndex(hawkPointApuVoltageAltIndex);
        }

        if (!success || value == 0)
        {
            return fallbackValue;
        }

        return (float)value;
    }

    /// <summary>
    ///     Проверяет, поддерживается ли версия таблицы
    /// </summary>
    private static bool IsSupportedTableVersion(int tableVersion)
    {
        return tableVersion switch
        {
            0x001E0001 or // Raven, Dali, Picasso
                0x001E0002 or
                0x001E0003 or
                0x001E0004 or
                0x001E0005 or
                0x001E000A or
                0x001E0101 or // FireFlight
                0x00190001 or // Summit Ridge, Pinnacle Ridge
                0x00240803 or // Matisse
                0x00240903 or
                0x00370000 or // Renoir, Lucienne
                0x00370001 or
                0x00370002 or
                0x00370003 or
                0x00370004 or
                0x00370005 or
                0x00380005 or // Vermeer
                0x00380505 or
                0x00380605 or
                0x00380705 or
                0x00380804 or
                0x00380805 or
                0x00380904 or
                0x00380905 or
                0x003F0000 or // Van Gogh
                0x00400001 or // Cezanne
                0x00400002 or
                0x00400003 or
                0x00400004 or
                0x00400005 or
                0x00450004 or // Rembrandt, Mendocino
                0x00450005 or
                0x004C0003 or // Phoenix
                0x004C0004 or
                0x004C0005 or
                0x004C0006 or // Phoenix, Phoenix 2
                0x004C0007 or
                0x004C0008 or // Phoenix, Krackan Point
                0x004C0009 or // Hawk Point, Krackan Point 2
                0x00540004 or // Raphael 7900
                0x00540104 or // Raphael 7500, 7800
                0x00540208 or // Dragon Range
                0x00620105 or // Granite Ridge
                0x00620205 or
                0x0064020c => true, // Strix Halo
            _ => false
        };
    }
}