namespace Saku_Overclock.SMUEngine;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
public static class GetMaintainedSettings
{
    private static readonly Dictionary<Cpu.CodeName, SMU> settings = new Dictionary<Cpu.CodeName, SMU>
    {
        {
            Cpu.CodeName.BristolRidge,
            new BristolRidgeSettings()
        },
        {
            Cpu.CodeName.SummitRidge,
            new SummitRidgeSettings()
        },
        {
            Cpu.CodeName.Naples,
            new SummitRidgeSettings()
        },
        {
            Cpu.CodeName.Whitehaven,
            new SummitRidgeSettings()
        },
        {
            Cpu.CodeName.PinnacleRidge,
            new ZenPSettings()
        },
        {
            Cpu.CodeName.Colfax,
            new ColfaxSettings()
        },
        {
            Cpu.CodeName.Matisse,
            new Zen2Settings()
        },
        {
            Cpu.CodeName.CastlePeak,
            new Zen2Settings()
        },
        {
            Cpu.CodeName.Rome,
            new RomeSettings()
        },
        {
            Cpu.CodeName.Vermeer,
            new Zen3Settings()
        },
        {
            Cpu.CodeName.Chagall,
            new Zen3Settings()
        },
        {
            Cpu.CodeName.Milan,
            new Zen3Settings()
        },
        {
            Cpu.CodeName.Raphael,
            new Zen4Settings()
        },
        {
            Cpu.CodeName.Genoa,
            new Zen4Settings()
        },
        {
            Cpu.CodeName.StormPeak,
            new Zen4Settings()
        },
        {
            Cpu.CodeName.RavenRidge,
            new APUSettings0()
        },
        {
            Cpu.CodeName.FireFlight,
            new APUSettings0()
        },
        {
            Cpu.CodeName.Dali,
            new APUSettings0_Picasso()
        },
        {
            Cpu.CodeName.Picasso,
            new APUSettings0_Picasso()
        },
        {
            Cpu.CodeName.Renoir,
            new APUSettings1()
        },
        {
            Cpu.CodeName.Lucienne,
            new APUSettings1()
        },
        {
            Cpu.CodeName.Cezanne,
            new APUSettings1_Cezanne()
        },
        {
            Cpu.CodeName.VanGogh,
            new APUSettings1()
        },
        {
            Cpu.CodeName.Rembrandt,
            new APUSettings1_Rembrandt()
        },
        {
            Cpu.CodeName.Phoenix,
            new APUSettings1_Rembrandt()
        },
        {
            Cpu.CodeName.Mendocino,
            new APUSettings1_Rembrandt()
        },
        {
            Cpu.CodeName.Unsupported,
            new UnsupportedSettings()
        }
    };

    public static SMU GetByType(Cpu.CodeName type)
    {
        return !settings.TryGetValue(type, out var smu) ? new UnsupportedSettings() : smu;
    }
}