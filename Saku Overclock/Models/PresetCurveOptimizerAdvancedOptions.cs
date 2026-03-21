namespace Saku_Overclock.Models;

public class PresetCurveOptimizerAdvancedOptions
{
    public PresetOption<int> CurveOptimizerPreferredMode = new(false, 0);
    public PresetLargeOption<double[]> CurveOptimizerCores = new(new bool[15], new  double[15]);
    
}