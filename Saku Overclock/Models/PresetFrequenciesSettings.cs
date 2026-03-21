namespace Saku_Overclock.Models;

public class PresetFrequenciesSettings
{
    public PresetOption<double> IntegratedGraphicsFrequency = new(false, 1800);
    public PresetOption<double> CpuFrequency = new(false, 2500);
    public PresetOption<double> CpuVoltage = new(false, 1200);
}