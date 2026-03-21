namespace Saku_Overclock.Models;

public class PresetSubsystemsSettings
{
    public PresetOption<double> MinimumSocFrequency = new(false, 200);
    public PresetOption<double> MaximumSocFrequency = new(false, 960);
    public PresetOption<double> MinimumFabricFrequency = new(false, 800);
    public PresetOption<double> MaximumFabricFrequency = new(false, 1200);
    public PresetOption<double> MinimumVideoCodecFrequency = new(false, 300);
    public PresetOption<double> MaximumVideoCodecFrequency = new(false, 1200);
    public PresetOption<double> MinimumDataLatchFrequency = new(false, 200);
    public PresetOption<double> MaximumDataLatchFrequency = new(false, 1200);
    public PresetOption<double> MinimumIntegratedGraphicsFrequency = new(false, 800);
    public PresetOption<double> MaximumIntegratedGraphicsFrequency = new(false, 1200);
}