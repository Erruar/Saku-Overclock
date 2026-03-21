namespace Saku_Overclock.Models;

public class PresetVrmSettings
{
    public PresetOption<double> VrmCpuEdcCurrentLimit = new(false, 100);
    public PresetOption<double> VrmCpuTdcCurrentLimit = new(false, 80);
    public PresetOption<double> VrmSocEdcCurrentLimit = new(false, 30);
    public PresetOption<double> VrmSocTdcCurrentLimit = new(false, 20);
    public PresetOption<double> VrmPowerSaveVddCurrentLimit = new(false, 12);
    public PresetOption<double> VrmPowerSaveSocCurrentLimit = new(false, 5);
    public PresetOption<double> VrmPowerSaveCpuCurrentLimit = new(false, 12);
    public PresetOption<double> VrmPowerSaveGpuCurrentLimit = new(false, 12);
    public PresetOption<double> VrmCpuFrequencyRestoreTime = new(false, 20);
}