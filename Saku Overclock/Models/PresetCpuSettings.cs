namespace Saku_Overclock.Models;

public class PresetCpuSettings
{
    public PresetOption<double> CpuMaximumTemperature = new(false, 100);
    public PresetOption<double> IntegratedGpuMaximumTemperature = new(false, 100);
    public PresetOption<double> DiscreteGpuMaximumTemperature = new(false, 100);
    public PresetOption<double> LaptopPowerLimit = new(false, 55);
    public PresetOption<double> CpuSustainedPowerLimit = new(false, 15);
    public PresetOption<double> CpuActualPowerLimit = new(false, 25);
    public PresetOption<double> CpuAveragePowerLimit = new(false, 20);
    public PresetOption<double> IntegratedGpuPowerLimit = new(false, 25);
    public PresetOption<double> CpuBoostTimeSlow = new(false, 300);
    public PresetOption<double> CpuBoostTimeFast = new(false, 5);
}