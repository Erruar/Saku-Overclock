using Saku_Overclock.Helpers;

namespace Saku_Overclock.JsonContainers;
public class Preset
{  
    public string Presetname = "Param_UnsignedPreset".GetLocalized();
    public string Presetdesc = "";
    public string Preseticon = "\uE718";
    
    // CPU configuration
    public bool Cpu1 = false;
    public bool Cpu2 = false;
    public bool Cpu3 = false;
    public bool Cpu4 = false;
    public bool Cpu5 = false;
    public bool Cpu6 = false;
    public bool Cpu7 = false;
    public double Cpu1Value = 100;
    public double Cpu2Value = 15;
    public double Cpu3Value = 25;
    public double Cpu4Value = 20;
    public double Cpu5Value = 5;
    public double Cpu6Value = 300;
    public double Cpu7Value = 100;
    
    // VRM configuration 
    public bool Vrm1 = false;
    public bool Vrm2 = false;
    public bool Vrm3 = false;
    public bool Vrm4 = false;
    public bool Vrm5 = false;
    public bool Vrm6 = false;
    public bool Vrm7 = false;
    public double Vrm1Value = 75.0;
    public double Vrm2Value = 75.0;
    public double Vrm3Value = 75.0;
    public double Vrm4Value = 75.0;
    public double Vrm5Value = 75.0;
    public double Vrm6Value = 75.0;
    public double Vrm7Value = 0.0;

    // iGPU and CPU subsystems configuration
    public bool Gpu1 = false;
    public bool Gpu2 = false;
    public bool Gpu3 = false;
    public bool Gpu4 = false;
    public bool Gpu5 = false;
    public bool Gpu6 = false;
    public bool Gpu7 = false;
    public bool Gpu8 = false;
    public bool Gpu9 = false;
    public bool Gpu10 = false;
    public bool Gpu11 = false;
    public bool Gpu12 = false;
    public bool Gpu16 = false;
    public double Gpu1Value = 1000.0;
    public double Gpu2Value = 3100.0;
    public double Gpu3Value = 1000.0;
    public double Gpu4Value = 3100.0;
    public double Gpu5Value = 1000.0;
    public double Gpu6Value = 3100.0;
    public double Gpu7Value = 1000.0;
    public double Gpu8Value = 3100.0;
    public double Gpu9Value = 800.0;
    public double Gpu10Value = 1200.0;
    public double Gpu11Value = 2500.0;
    public double Gpu12Value = 3500.0;
    public int Gpu16Value = 0;
    
    // Advanced options configuration 
    public bool Advncd1 = false;
    public bool Advncd3 = false;
    public bool Advncd4 = false;
    public bool Advncd5 = false;
    public bool Advncd6 = false;
    public bool Advncd7 = false;
    public bool Advncd8 = false;
    public bool Advncd9 = false;
    public bool Advncd10 = false;
    public bool Advncd11 = false;
    public bool Advncd12 = false;
    public bool Advncd13 = false;
    public bool Advncd14 = false;
    public bool Advncd15 = false;
    public double Advncd1Value = 1.0;
    public double Advncd3Value = 64.0;
    public double Advncd4Value = 12.0;
    public double Advncd5Value = 12.0;
    public double Advncd6Value = 50.0;
    public double Advncd7Value = 100.0;
    public double Advncd8Value = 25.0;
    public double Advncd9Value = 45.0;
    public double Advncd10Value = 2000.0;
    public double Advncd11Value = 2500.0;
    public double Advncd12Value = 1200.0;
    public int Advncd13Value = 0;
    public int Advncd14Value = 0;
    public double Advncd15Value = 1.0;

    // P-states configuration
    public double Did0 = 8;
    public double Did1 = 12;
    public double Did2 = 12;
    public double Fid0 = 100;
    public double Fid1 = 102;
    public double Fid2 = 98;
    public double Vid0 = 1218.0;
    public double Vid1 = 1105.0;
    public double Vid2 = 925.0;
    public bool EnablePstateEditor = false;
    public bool TurboBoost = true;
    public bool AutoPstate = false;
    public bool P0Ignorewarn = false;
    public bool IgnoreWarn = false;
    
    // Curve Optimizer configuration
    public bool Coall = false;
    public bool Cogfx = false;
    public double Coallvalue = 0.0;
    public double Cogfxvalue = 0.0;
    public bool Comode = false;
    public int Coprefmode = 0;
    public bool Coper0 = false;
    public bool Coper1 = false;
    public bool Coper2 = false;
    public bool Coper3 = false;
    public bool Coper4 = false;
    public bool Coper5 = false;
    public bool Coper6 = false;
    public bool Coper7 = false;
    public bool Coper8 = false;
    public bool Coper9 = false;
    public bool Coper10 = false;
    public bool Coper11 = false;
    public bool Coper12 = false;
    public bool Coper13 = false;
    public bool Coper14 = false;
    public bool Coper15 = false;
    public double Coper0Value = 0.0;
    public double Coper1Value = 0.0;
    public double Coper2Value = 0.0;
    public double Coper3Value = 0.0;
    public double Coper4Value = 0.0;
    public double Coper5Value = 0.0;
    public double Coper6Value = 0.0;
    public double Coper7Value = 0.0;
    public double Coper8Value = 0.0;
    public double Coper9Value = 0.0;
    public double Coper10Value = 0.0;
    public double Coper11Value = 0.0;
    public double Coper12Value = 0.0;
    public double Coper13Value = 0.0;
    public double Coper14Value = 0.0;
    public double Coper15Value = 0.0;
    
    // Send custom SMU commands configuration
    public bool SmuEnabled = false;
    
    // Override SMU Functions configuration
    public bool SmuFunctionsEnabl = false;
    public bool SmuFeatureCclk = true;
    public bool SmuFeatureData = true;
    public bool SmuFeaturePpt = true;
    public bool SmuFeatureTdc = true;
    public bool SmuFeatureThermal = true;
    public bool SmuFeaturePowerDown = true;
    public bool SmuFeatureProchot = true;
    public bool SmuFeatureStapm = true;
    public bool SmuFeatureCStates = true;
    public bool SmuFeatureGfxDutyCycle = true;
    public bool SmuFeatureAplusA = true;
}