namespace Saku_Overclock.Models;

public class PresetSmuFeaturesSettings
{
    public bool SmuFeaturesOverride = false;
    public bool CpuFrequencyScaling = true;
    public bool SensorsDataCalculation = true;
    public bool PowerLimits = true;
    public bool SustainVrmTdcCurrent = true;
    public bool TemperatureControl = true;
    public bool DpmFrequencyPowerDown = true;
    public bool ProchotSignal = true;
    public bool SustainedPowerLimit = true;
    public bool CStatesBoost = true;
    public bool GraphicsDutyCycle = true;
    public bool AplusAPowerMode = true;
}