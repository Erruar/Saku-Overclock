using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.Views;
using ZenStates.Core;

namespace Saku_Overclock.Services;
public class OcFinderService : IOcFinderService
{
    private static readonly ISendSmuCommandService SendSmuCommand = App.GetService<ISendSmuCommandService>();
    private Cpu _cpu;
    public OcFinderService()
    {
        _cpu = CpuSingleton.GetInstance();
    }

    private bool _isInitialized = false;

    private string MinPreset = "";
    private string EcoPreset = "";
    private string BalancePreset = "";
    private string PerformancePreset = "";
    private string MaxPreset = "";

    private int TempLimitBalance = 80;
    private int TempLimitPerf = 90;

    private int StapmLimitBalance = 20;
    private int StapmLimitPerf = 30;

    private int FastLimitBalance = 25;
    private int FastLimitPerf = 35;

    private int SttLimitBalance = 50;
    private int SttLimitPerf = 70;

    private int SlowTimeBalance = 5;
    private int SlowTimePerf = 3;

    private int StapmTimeBalance = 300;
    private int StapmTimePerf = 200;

    private int BdProchotTimeBalance = 20;
    private int BdProchotTimePerf = 200;

    public void GeneratePremadeProfiles()
    {
        if (/*_isInitialized*/false) { return; }

        var x = 35d;
        var y = 65d;
        _cpu ??= CpuSingleton.GetInstance();

        var cpuPower = SendSmuCommand.ReturnCpuPowerLimit(_cpu);
        var platformPc = SendSmuCommand.IsPlatformPC(_cpu) == true;

        if (_cpu != null)
        { 
            if (platformPc)
            {
                y = cpuPower;
            }
            else
            {
                x = cpuPower;
            }
            if (cpuPower > 45 && !platformPc)
            {
                x = 45d;
            }
        }

        double stapm_val_min; 
        double stapm_val_eco; 
        double stapm_val_bal; 
        double stapm_val_per; 
        double stapm_val_max;

        if (!platformPc)
        {
            stapm_val_min =  0.0013 * x * x * x - 0.1228 * x * x + 3.5876 * x - 24.5430; // Min
            stapm_val_eco =  0.0006 * x * x * x - 0.0630 * x * x + 2.3241 * x - 10.8710; // Eco
            stapm_val_bal =  0.0000 * x * x * x - 0.0005 * x * x + 0.9628 * x + 2.92380; // Bal
            stapm_val_per = -0.0003 * x * x * x + 0.0292 * x * x + 0.2543 * x + 13.3532; // Perf
            stapm_val_max = -0.0009 * x * x * x + 0.0776 * x * x - 0.8828 * x + 28.8492; // Max

            if (stapm_val_min < 6) { stapm_val_min = 6; }
        }
        else
        {
            stapm_val_min =  0.00003451 * y * y * y - 0.011510 * y * y + 1.52400 * y - 25.52000; // Min
            stapm_val_eco =  0.00001711 * y * y * y - 0.006530 * y * y + 1.16600 * y - 8.417000;  // Eco
            stapm_val_bal = -0.00002563 * y * y * y + 0.005565 * y * y + 0.63570 * y + 7.069000; // Bal
            stapm_val_per = -0.00006658 * y * y * y + 0.021440 * y * y - 1.14200 * y + 81.64000; // Perf
            stapm_val_max = -0.00005653 * y * y * y + 0.021680 * y * y - 1.21900 * y + 98.47000; // Max
        }


        var fast_val_min = platformPc == false ? FromValueToUpper(1.17335141 * stapm_val_min + 0.21631949, 2) : (int)stapm_val_min;
        var fast_val_eco = platformPc == false ? FromValueToUpper(1.17335141 * stapm_val_eco + 0.21631949, 3) : (int)stapm_val_eco;
        var fast_val_bal = platformPc == false ? FromValueToUpper(1.17335141 * stapm_val_bal + 0.21631949, 3) : (int)stapm_val_bal;
        var fast_val_per = platformPc == false ? FromValueToUpper(1.17335141 * stapm_val_per + 0.21631949, 4) : (int)stapm_val_per;
        var fast_val_max = platformPc == false ? FromValueToUpper(1.17335141 * stapm_val_max + 0.21631949, 5) : (int)stapm_val_max; 

        StapmLimitBalance = (int)stapm_val_bal;
        StapmLimitPerf = (int)stapm_val_per;

        FastLimitBalance = fast_val_bal;
        FastLimitPerf = fast_val_per;

        SttLimitBalance = 2*(int)stapm_val_bal;
        SttLimitPerf = 2*(int)stapm_val_per;

        MinPreset = " --tctl-temp=60 " + //
            $"--stapm-limit={stapm_val_min * 1000} " + //
            $"--fast-limit={fast_val_min * 1000} " + //
            $"--stapm-time=900 " + //
            $"--slow-limit={stapm_val_min * 1000} " + //
            $"--slow-time=900 " + //
            $"--vrm-current=120000 " + //
            $"--vrmmax-current=120000 " + //
            $"--vrmsoc-current=120000 " + //
            $"--vrmsocmax-current=120000 " + //
            $"--prochot-deassertion-ramp=2 ";

        EcoPreset = " --tctl-temp=70 " + //
            $"--stapm-limit={stapm_val_eco * 1000} " + //
            $"--fast-limit={fast_val_eco * 1000} " + //
            $"--stapm-time=500 " + //
            $"--slow-limit={stapm_val_eco * 1000} " + //
            $"--slow-time=500 " + //
            $"--vrm-current=120000 " + //
            $"--vrmmax-current=120000 " + //
            $"--vrmsoc-current=120000 " + //
            $"--vrmsocmax-current=120000 " + //
            $"--prochot-deassertion-ramp=2 ";

        BalancePreset = " --tctl-temp=90 " + //
            $"--stapm-limit={stapm_val_bal * 1000} " + //
            $"--fast-limit={fast_val_bal * 1000} " + //
            $"--stapm-time=300 " + //
            $"--slow-limit={stapm_val_bal * 1000} " + //
            $"--slow-time=5 " + //
            $"--vrm-current=120000 " + //
            $"--vrmmax-current=120000 " + //
            $"--vrmsoc-current=120000 " + //
            $"--vrmsocmax-current=120000 " + //
            $"--prochot-deassertion-ramp=20 ";

        PerformancePreset = " --tctl-temp=90 " + //
            $"--stapm-limit={stapm_val_per * 1000} " + //
            $"--fast-limit={fast_val_per * 1000} " + //
            $"--stapm-time=200 " + //
            $"--slow-limit={stapm_val_per * 1000} " + //
            $"--slow-time=3 " + //
            $"--vrm-current=120000 " + //
            $"--vrmmax-current=120000 " + //
            $"--vrmsoc-current=120000 " + //
            $"--vrmsocmax-current=120000 " + //
            $"--prochot-deassertion-ramp=200 ";

        MaxPreset = " --tctl-temp=100 " + //
            $"--stapm-limit={stapm_val_max * 1000} " + //
            $"--fast-limit={fast_val_max * 1000} " + //
            $"--stapm-time=9000 " + //
            $"--slow-limit={stapm_val_max * 1000} " + //
            $"--slow-time=1 " + //
            $"--vrm-current=120000 " + //
            $"--vrmmax-current=120000 " + //
            $"--vrmsoc-current=120000 " + //
            $"--vrmsocmax-current=120000 " + //
            $"--prochot-deassertion-ramp=100 ";

        _isInitialized = true;
    }
    public string GetMinPreset() => MinPreset;
    public string GetEcoPreset() => EcoPreset;
    public string GetBalPreset() => BalancePreset;
    public string GetPerfPreset() => PerformancePreset;
    public string GetMaxPreset() => MaxPreset;
    public (int[], int[], int[], int[], int[], int[], int[]) GetPerformanceRecommendationData()
    {
        if (!_isInitialized) { GeneratePremadeProfiles(); }
        return new(
            [TempLimitBalance, TempLimitPerf], 
            [StapmLimitBalance, StapmLimitPerf], 
            [FastLimitBalance, FastLimitPerf], 
            [SttLimitBalance, SttLimitPerf], 
            [SlowTimeBalance, SlowTimePerf], 
            [StapmTimeBalance, StapmTimePerf], 
            [BdProchotTimeBalance, BdProchotTimePerf] 
            );
    }
    private static int FromValueToUpper(double value, int upper) => (int)Math.Ceiling(value / upper) * upper;
}
