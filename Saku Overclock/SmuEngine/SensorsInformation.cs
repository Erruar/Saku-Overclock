namespace Saku_Overclock.SMUEngine;

public class SensorsInformation
{

    #region CPU/GPU/VRM Information
    public string? CpuFamily
    {
        get; set;
    }
    public double CpuStapmLimit
    {
        get; set;
    }
    public double CpuStapmValue
    {
        get; set;
    }
    public double CpuFastLimit
    {
        get; set;
    }
    public double CpuFastValue
    {
        get; set;
    }
    public double CpuSlowLimit
    {
        get; set;
    }
    public double CpuSlowValue
    {
        get; set;
    }
    public double ApuSlowLimit
    {
        get; set;
    }
    public double ApuSlowValue
    {
        get; set;
    }
    public double VrmTdcValue
    {
        get; set;
    }
    public double VrmTdcLimit
    {
        get; set;
    }
    public double VrmEdcValue
    {
        get; set;
    }
    public double VrmEdcLimit
    {
        get; set;
    }
    public double VrmPsiValue
    {
        get; set;
    }
    public double VrmPsiSocValue
    {
        get; set;
    }
    public double SocTdcValue
    {
        get; set;
    }
    public double SocTdcLimit
    {
        get; set;
    }
    public double SocEdcValue
    {
        get; set;
    }
    public double SocEdcLimit
    {
        get; set;
    }
    public double CpuTempValue
    {
        get; set;
    }
    public double CpuTempLimit
    {
        get; set;
    }
    public double ApuTempValue
    {
        get; set;
    }
    public double ApuTempLimit
    {
        get; set;
    }
    public double DgpuTempValue
    {
        get; set;
    }
    public double DgpuTempLimit
    {
        get; set;
    }
    public double CpuStapmTimeValue
    {
        get; set;
    }
    public double CpuSlowTimeValue
    {
        get; set;
    }
    public double CpuUsage
    {
        get; set;
    }
    public double[]? CpuFrequencyPerCore
    {
        get; set;
    }
    public double[]? CpuVoltagePerCore
    {
        get; set;
    }
    public double[]? CpuPowerPerCore
    {
        get; set;
    }
    public double[]? CpuTemperaturePerCore
    {
        get; set;
    }
    public double ApuFrequency
    {
        get; set;
    }
    public double ApuTemperature
    {
        get; set;
    }
    public double ApuVoltage
    {
        get; set;
    }
    public double MemFrequency
    {
        get; set;
    }
    public double FabricFrequency
    {
        get; set;
    }
    public double SocPower
    {
        get; set;
    }
    public double SocVoltage
    {
        get; set;
    }
    public double CpuFrequency
    {
        get; set;
    }
    public double CpuVoltage
    {
        get; set;
    }
    #endregion

    #region Battery Information
    public string? BatteryName
    {
        get; set;
    }
    public bool BatteryUnavailable
    {
        get; set;
    }
    public string? BatteryPercent
    {
        get; set;
    }
    public string? BatteryState
    {
        get; set;
    }
    public string? BatteryHealth
    {
        get; set;
    }
    public string? BatteryCycles
    {
        get; set;
    }
    public string? BatteryCapacity
    {
        get; set;
    }
    public double BatteryChargeRate
    {
        get; set;
    }
    public int BatteryLifeTime
    {
        get; set;
    }
    #endregion

    #region RAM Information
    public string? RamTotal
    {
        get; set;
    }
    public string? RamBusy
    {
        get; set;
    }
    public string? RamUsage
    {
        get; set;
    }
    public int RamUsagePercent
    {
        get; set;
    }

    #endregion
}
