namespace Saku_Overclock.Models;

public class PresetAdvancedCpuModesSettings
{
    public PresetOption<int> OverclockMode = new(false, 0);
    public PresetOption<int> PboScalar = new(false, 0);
    public PresetOption<int> PreferredMode = new(false, 0);
    public PresetOption<int> CpuFrequency04Fix = new(false, 0);
}