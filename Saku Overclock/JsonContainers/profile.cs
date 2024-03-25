namespace Saku_Overclock;
#pragma warning disable CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.
internal class Profile
{  
    public string profilename = "Unsigned profile";  
    //CPU config 
    public bool cpu1;
    public bool cpu2;
    public bool cpu3;
    public bool cpu4;
    public bool cpu5;
    public bool cpu6;
    public double cpu1value;
    public double cpu2value;
    public double cpu3value;
    public double cpu4value;
    public double cpu5value;
    public double cpu6value;
    //VRM config 
    public bool vrm1;
    public bool vrm2;
    public bool vrm3;
    public bool vrm4;
    public bool vrm5;
    public bool vrm6;
    public bool vrm7;
    public double vrm1value;
    public double vrm2value;
    public double vrm3value;
    public double vrm4value;
    public double vrm5value;
    public double vrm6value;
    public double vrm7value;
    //GPU config  
    public bool gpu1;
    public bool gpu2;
    public bool gpu3;
    public bool gpu4;
    public bool gpu5;
    public bool gpu6;
    public bool gpu7;
    public bool gpu8;
    public bool gpu9;
    public bool gpu10;
    public double gpu1value;
    public double gpu2value;
    public double gpu3value;
    public double gpu4value;
    public double gpu5value;
    public double gpu6value;
    public double gpu7value;
    public double gpu8value;
    public double gpu9value;
    public double gpu10value;
    //ADVANCED config 
    public bool advncd1;
    public bool advncd2;
    public bool advncd3;
    public bool advncd4;
    public bool advncd5;
    public bool advncd6;
    public bool advncd7;
    public bool advncd8;
    public bool advncd9;
    public bool advncd10;
    public bool advncd11;
    public bool advncd12;
    public bool advncd13;
    public double advncd1value;
    public double advncd2value;
    public double advncd3value;
    public double advncd4value;
    public double advncd5value;
    public double advncd6value;
    public double advncd7value;
    public double advncd8value;
    public double advncd9value;
    public double advncd10value;
    public double advncd11value;
    public double advncd12value;
    public int advncd13value;
    //Pstates  
    public double did0;
    public double did1;
    public double did2;
    public double fid0;
    public double fid1;
    public double fid2;
    public double vid0;
    public double vid1;
    public double vid2;
    public bool enablePstateEditor;
    public bool turboBoost;
    public bool autoPstate;
    public bool p0Ignorewarn;
    public bool ignoreWarn;
    //SMU
    public bool smuEnabled;
}
#pragma warning restore CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.