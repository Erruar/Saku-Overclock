namespace Saku_Overclock.SmuEngine;

/// <summary>
/// Типы сенсоров, доступных для чтения из SMU таблицы.
/// </summary>
public enum SensorId
{
    // Лимиты и значения STAPM/Fast/Slow
    CpuStapmLimit,
    CpuStapmValue,
    CpuFastLimit,
    CpuFastValue,
    CpuSlowLimit,
    CpuSlowValue,
    ApuSlowLimit,
    ApuSlowValue,

    // VRM токи
    VrmTdcValue,
    VrmTdcLimit,
    VrmEdcValue,
    VrmEdcLimit,
    VrmPsiValue,
    VrmPsiSocValue,

    // SoC токи
    SocTdcValue,
    SocTdcLimit,
    SocEdcValue,
    SocEdcLimit,

    // Температуры
    CpuTempValue,
    CpuTempLimit,
    ApuTempValue,
    ApuTempLimit,
    DgpuTempValue,
    DgpuTempLimit,

    // Временные метрики
    CpuStapmTimeValue,
    CpuSlowTimeValue,

    // APU метрики
    ApuFrequency,
    ApuVoltage,

    // Частоты памяти и фабрики
    MemFrequency,
    FabricFrequency,

    // SoC мощность и напряжение
    SocPower,
    SocVoltage,

    // Стартовые индексы для массивов per-core данных
    CpuFrequencyStart,
    CpuVoltageStart,
    CpuTemperatureStart,
    CpuPowerStart
}