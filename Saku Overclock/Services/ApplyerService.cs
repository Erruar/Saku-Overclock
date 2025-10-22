using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.JsonContainers;
using Saku_Overclock.SMUEngine;
using ZenStates.Core;

namespace Saku_Overclock.Services;
public class ApplyerService : IApplyerService
{
    private readonly DispatcherTimer _timer;
    private EventHandler<object>? _tickHandler;
    private readonly IAppSettingsService _settingsService;
    private readonly ISendSmuCommandService _sendSmuCommand;
    private readonly IOcFinderService _ocFinder;

    public ApplyerService(
        IAppSettingsService settingsService,
        ISendSmuCommandService sendSmuCommand,
        IOcFinderService ocFinder)
    {
        _settingsService = settingsService;
        _sendSmuCommand = sendSmuCommand;
        _ocFinder = ocFinder;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(3 * 1000) };
    }

    public async Task ApplyWithoutAdjLine(bool saveinfo) =>
        await Apply(_settingsService.RyzenAdjLine, saveinfo,
            _settingsService.ReapplyOverclock, _settingsService.ReapplyOverclockTimer);

    public async Task Apply(string ryzenAdJline, bool saveinfo, bool reapplyOverclock,
        double reapplyOverclockTimer)
    {
        try
        {
            if (reapplyOverclock)
            {
                try
                {
                    _timer.Interval = TimeSpan.FromMilliseconds(reapplyOverclockTimer * 1000);
                    _timer.Stop();
                }
                catch
                {
                    await LogHelper.TraceIt_TraceError(
                        "Время автообновления разгона некорректно и было исправлено на 3000 мс");
                    reapplyOverclockTimer = 3000;
                    _timer.Interval = TimeSpan.FromMilliseconds(reapplyOverclockTimer);
                }

                if (_tickHandler != null)
                {
                    _timer.Tick -= _tickHandler;
                }

                _tickHandler = async (_, _) =>
                {
                    try
                    {
                        if (reapplyOverclock)
                        {
                            await Process(ryzenAdJline, false);
                            _sendSmuCommand?.ApplyQuickSmuCommand(true);
                        }
                    }
                    catch (Exception ex)
                    {
                        await LogHelper.LogError("[Applyer]::Overclock_Settings_Reapply_FAIL - " + ex.ToString());
                    }
                };

                _timer.Tick += _tickHandler;
                _timer.Start();
            }
            else
            {
                _timer.Stop();
            }

            await Process(ryzenAdJline, saveinfo);
        }
        catch (Exception ex)
        {
            await LogHelper.LogError("[Applyer]::Overclock_Settings_FirstApply_FAIL - " + ex.ToString());
        }
    }

    public async Task ApplyCustomPreset(Profile profile, bool saveInfo = false)
    {
        try
        {
            var adjline = ParseOverclockProfile(profile);
            _settingsService.RyzenAdjLine = adjline;
            _settingsService.SaveSettings();
            await Apply(adjline, saveInfo, _settingsService.ReapplyOverclock, _settingsService.ReapplyOverclockTimer);
        }
        catch (Exception ex)
        {
            await LogHelper.LogError("[Applyer]::ApplyProfile_FAIL - " + ex.ToString());
        }
    }

    public async Task ApplyPremadePreset(PresetType presetType, OptimizationLevel optimizationLevel)
    {
        try
        {
            var preset = _ocFinder.CreatePreset(presetType, optimizationLevel);
            _settingsService.RyzenAdjLine = preset.CommandString;
            await Apply(preset.CommandString, false, _settingsService.ReapplyOverclock, _settingsService.ReapplyOverclockTimer);
        }
        catch (Exception ex)
        {
            await LogHelper.LogError("[Applyer]::ApplyPreset_FAIL - " + ex.ToString());
        }
    }

    private async Task Process(string adjLine, bool saveinfo)
    {
        try
        {
            await Task.Run(() =>
            {
                _sendSmuCommand?.Translate(adjLine, saveinfo);
            });
        }
        catch (Exception ex)
        {
            await LogHelper.LogError("[Applyer]::Overclock_Settings_Apply_FAIL - " + ex.ToString());
        }
    }

    private string ParseOverclockProfile(Profile profile)
    {
        var cpu = CpuSingleton.GetInstance();
        var isBristol = cpu?.info.codeName == Cpu.CodeName.BristolRidge;
        var adjline = "";

        // CPU settings
        if (profile.Cpu1)
        {
            adjline += " --tctl-temp=" + profile.Cpu1Value + (isBristol ? "000" : string.Empty);
        }

        if (profile.Cpu2)
        {
            var stapmBoostMillisecondsBristol = profile.Cpu5Value * 1000 < 180000 ? profile.Cpu5Value * 1000 : 180000;
            adjline += " --stapm-limit=" + profile.Cpu2Value + "000" + (isBristol ? ",2," + stapmBoostMillisecondsBristol : string.Empty);
        }

        if (profile.Cpu3)
        {
            adjline += " --fast-limit=" + profile.Cpu3Value + "000";
        }

        if (profile.Cpu4)
        {
            adjline += " --slow-limit=" + profile.Cpu4Value + "000" + (isBristol ? "," + profile.Cpu4Value + "000,0" : string.Empty);
        }

        if (profile.Cpu5)
        {
            adjline += " --stapm-time=" + profile.Cpu5Value;
        }

        if (profile.Cpu6)
        {
            adjline += " --slow-time=" + profile.Cpu6Value;
        }

        if (profile.Cpu7)
        {
            adjline += " --cHTC-temp=" + profile.Cpu7Value;
        }

        // VRM settings
        if (profile.Vrm1)
        {
            adjline += " --vrmmax-current=" + profile.Vrm1Value + "000" + (isBristol ? "," + profile.Vrm3Value + "000," + profile.Vrm3Value + "000" : string.Empty);
        }

        if (profile.Vrm2)
        {
            adjline += " --vrm-current=" + profile.Vrm2Value + "000" + (isBristol ? "," + profile.Vrm4Value + "000," + profile.Vrm4Value + "000" : string.Empty);
        }

        if (profile.Vrm3 && !isBristol)
        {
            adjline += " --vrmsocmax-current=" + profile.Vrm3Value + "000";
        }

        if (profile.Vrm4 && !isBristol)
        {
            adjline += " --vrmsoc-current=" + profile.Vrm4Value + "000";
        }

        if (profile.Vrm5)
        {
            adjline += " --psi0-current=" + profile.Vrm5Value + "000" + (isBristol ? "," + profile.Vrm6Value + "000," + profile.Vrm6Value + "000" : string.Empty);
        }

        if (profile.Vrm6 && !isBristol)
        {
            adjline += " --psi0soc-current=" + profile.Vrm6Value + "000";
        }

        if (profile.Vrm7)
        {
            var prochotDeassertionTimeMillisecondsBristol = profile.Vrm7Value < 100 ? profile.Vrm7Value : 100;
            adjline += " --prochot-deassertion-ramp=" + (isBristol ? prochotDeassertionTimeMillisecondsBristol : profile.Vrm7Value);
        }

        // GPU settings
        if (profile.Gpu1)
        {
            adjline += " --min-socclk-frequency=" + profile.Gpu1Value;
        }

        if (profile.Gpu2)
        {
            adjline += " --max-socclk-frequency=" + profile.Gpu2Value;
        }

        if (profile.Gpu3)
        {
            adjline += " --min-fclk-frequency=" + profile.Gpu3Value;
        }

        if (profile.Gpu4)
        {
            adjline += " --max-fclk-frequency=" + profile.Gpu4Value;
        }

        if (profile.Gpu5)
        {
            adjline += " --min-vcn=" + profile.Gpu5Value;
        }

        if (profile.Gpu6)
        {
            adjline += " --max-vcn=" + profile.Gpu6Value;
        }

        if (profile.Gpu7)
        {
            adjline += " --min-lclk=" + profile.Gpu7Value;
        }

        if (profile.Gpu8)
        {
            adjline += " --max-lclk=" + profile.Gpu8Value;
        }

        if (profile.Gpu9)
        {
            adjline += " --min-gfxclk=" + profile.Gpu9Value;
        }

        if (profile.Gpu10)
        {
            adjline += " --max-gfxclk=" + profile.Gpu10Value;
        }

        if (profile.Gpu11)
        {
            adjline += " --min-cpuclk=" + profile.Gpu11Value;
        }

        if (profile.Gpu12)
        {
            adjline += " --max-cpuclk=" + profile.Gpu12Value;
        }

        if (profile.Gpu16)
        {
            if (profile.Gpu16Value != 0)
            {
                adjline += " --setcpu-freqto-ramstate=" + (profile.Gpu16Value - 1);
            }
            else
            {
                adjline += " --stopcpu-freqto-ramstate=0";
            }
        }

        // Advanced settings
        if (profile.Advncd1)
        {
            adjline += " --vrmgfx-current=" + profile.Advncd1Value + "000";
        }

        if (profile.Advncd3)
        {
            adjline += " --vrmgfxmax_current=" + profile.Advncd3Value + "000";
        }

        if (profile.Advncd4)
        {
            adjline += " --psi3cpu_current=" + profile.Advncd4Value + "000";
        }

        if (profile.Advncd5)
        {
            adjline += " --psi3gfx_current=" + profile.Advncd5Value + "000";
        }

        if (profile.Advncd6)
        {
            adjline += " --apu-skin-temp=" + profile.Advncd6Value * 256;
        }

        if (profile.Advncd7)
        {
            adjline += " --dgpu-skin-temp=" + profile.Advncd7Value * 256;
        }

        if (profile.Advncd8)
        {
            adjline += " --apu-slow-limit=" + profile.Advncd8Value + "000";
        }

        if (profile.Advncd9)
        {
            adjline += " --skin-temp-limit=" + profile.Advncd9Value + "000";
        }

        if (profile.Advncd10)
        {
            adjline += " --gfx-clk=" + profile.Advncd10Value;
        }

        if (profile.Advncd11)
        {
            adjline += " --oc-clk=" + profile.Advncd11Value;
        }

        if (profile.Advncd12)
        {
            adjline += " --oc-volt=" + Math.Round((1.55 - profile.Advncd12Value / 1000) / 0.00625);
        }

        if (profile.Advncd13)
        {
            if (profile.Advncd13Value == 1)
            {
                adjline += " --max-performance=1";
            }

            if (profile.Advncd13Value == 2)
            {
                adjline += " --power-saving=1";
            }
        }

        if (profile.Advncd14)
        {
            switch (profile.Advncd14Value)
            {
                case 0:
                    adjline += " --disable-oc=1";
                    break;
                case 1:
                    adjline += " --enable-oc=1";
                    break;
            }
        }

        if (profile.Advncd15)
        {
            adjline += " --pbo-scalar=" + profile.Advncd15Value * 100;
        }

        // CO All
        if (profile.Coall)
        {
            if (profile.Coallvalue >= 0.0)
            {
                adjline += $" --set-coall={profile.Coallvalue} ";
            }
            else
            {
                adjline += $" --set-coall={Convert.ToUInt32(0x100000 - (uint)(-1 * (int)profile.Coallvalue))} ";
            }
        }

        // CO GFX
        if (profile.Cogfx && cpu != null)
        {
            cpu.smu.Rsmu.SMU_MSG_SetDldoPsmMargin = _sendSmuCommand.ReturnCoGfx(false);
            cpu.smu.Mp1Smu.SMU_MSG_SetDldoPsmMargin = _sendSmuCommand.ReturnCoGfx(true);

            for (var i = 0; i < cpu.info.topology.physicalCores; i++)
            {
                var mapIndex = i < 8 ? 0 : 1;
                if (((~cpu.info.topology.coreDisableMap[mapIndex] >> i) & 1) == 1)
                {
                    if (cpu.smu.Rsmu.SMU_MSG_SetDldoPsmMargin != 0U)
                    {
                        cpu.SetPsmMarginSingleCore(GetCoreMask(cpu, i), Convert.ToInt32(profile.Cogfxvalue));
                    }
                }
            }

            cpu.smu.Rsmu.SMU_MSG_SetDldoPsmMargin = _sendSmuCommand.ReturnCoPer(false);
            cpu.smu.Mp1Smu.SMU_MSG_SetDldoPsmMargin = _sendSmuCommand.ReturnCoPer(true);
        }

        // CO Per Core
        if (profile.Comode && profile.Coprefmode != 0 && cpu != null)
        {
            adjline += ProcessCoperSettings(profile, cpu);
        }

        // SMU Features
        if (profile.SmuFunctionsEnabl)
        {
            adjline += ProcessSmuFeatures(profile);
        }

        return adjline + " ";
    }

    private string ProcessCoperSettings(Profile profile, Cpu cpu)
    {
        var adjline = "";

        switch (profile.Coprefmode)
        {
            case 1 when cpu.info.codeName == Cpu.CodeName.DragonRange:
                adjline += ProcessDragonRangeCoper(profile);
                break;
            case 1:
                adjline += ProcessLaptopCoper(profile);
                break;
            case 2:
                adjline += ProcessDesktopCoper(profile);
                break;
            case 3:
                ProcessIrusanovMethod(profile, cpu);
                break;
        }

        return adjline;
    }

    private static string ProcessDragonRangeCoper(Profile profile)
    {
        var adjline = "";

        if (profile.Coper0) adjline += $" --set-coper={0 | ((int)profile.Coper0Value & 0xFFFF)} ";
        if (profile.Coper1) adjline += $" --set-coper={1048576 | ((int)profile.Coper1Value & 0xFFFF)} ";
        if (profile.Coper2) adjline += $" --set-coper={2097152 | ((int)profile.Coper2Value & 0xFFFF)} ";
        if (profile.Coper3) adjline += $" --set-coper={3145728 | ((int)profile.Coper3Value & 0xFFFF)} ";
        if (profile.Coper4) adjline += $" --set-coper={4194304 | ((int)profile.Coper4Value & 0xFFFF)} ";
        if (profile.Coper5) adjline += $" --set-coper={5242880 | ((int)profile.Coper5Value & 0xFFFF)} ";
        if (profile.Coper6) adjline += $" --set-coper={6291456 | ((int)profile.Coper6Value & 0xFFFF)} ";
        if (profile.Coper7) adjline += $" --set-coper={7340032 | ((int)profile.Coper7Value & 0xFFFF)} ";
        if (profile.Coper8) adjline += $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((0 % 8) & 15)) << 20) | ((int)profile.Coper8Value & 0xFFFF)} ";
        if (profile.Coper9) adjline += $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((1 % 8) & 15)) << 20) | ((int)profile.Coper9Value & 0xFFFF)} ";
        if (profile.Coper10) adjline += $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((2 % 8) & 15)) << 20) | ((int)profile.Coper10Value & 0xFFFF)} ";
        if (profile.Coper11) adjline += $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((3 % 8) & 15)) << 20) | ((int)profile.Coper11Value & 0xFFFF)} ";
        if (profile.Coper12) adjline += $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((4 % 8) & 15)) << 20) | ((int)profile.Coper12Value & 0xFFFF)} ";
        if (profile.Coper13) adjline += $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((5 % 8) & 15)) << 20) | ((int)profile.Coper13Value & 0xFFFF)} ";
        if (profile.Coper14) adjline += $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((6 % 8) & 15)) << 20) | ((int)profile.Coper14Value & 0xFFFF)} ";
        if (profile.Coper15) adjline += $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((7 % 8) & 15)) << 20) | ((int)profile.Coper15Value & 0xFFFF)} ";

        return adjline;
    }

    private static string ProcessLaptopCoper(Profile profile)
    {
        var adjline = "";

        if (profile.Coper0) adjline += $" --set-coper={0 | ((int)profile.Coper0Value & 0xFFFF)} ";
        if (profile.Coper1) adjline += $" --set-coper={(1 << 20) | ((int)profile.Coper1Value & 0xFFFF)} ";
        if (profile.Coper2) adjline += $" --set-coper={(2 << 20) | ((int)profile.Coper2Value & 0xFFFF)} ";
        if (profile.Coper3) adjline += $" --set-coper={(3 << 20) | ((int)profile.Coper3Value & 0xFFFF)} ";
        if (profile.Coper4) adjline += $" --set-coper={(4 << 20) | ((int)profile.Coper4Value & 0xFFFF)} ";
        if (profile.Coper5) adjline += $" --set-coper={(5 << 20) | ((int)profile.Coper5Value & 0xFFFF)} ";
        if (profile.Coper6) adjline += $" --set-coper={(6 << 20) | ((int)profile.Coper6Value & 0xFFFF)} ";
        if (profile.Coper7) adjline += $" --set-coper={(7 << 20) | ((int)profile.Coper7Value & 0xFFFF)} ";

        return adjline;
    }

    private static string ProcessDesktopCoper(Profile profile) => ProcessDragonRangeCoper(profile);

    private void ProcessIrusanovMethod(Profile profile, Cpu cpu)
    {
        cpu.smu.Rsmu.SMU_MSG_SetDldoPsmMargin = _sendSmuCommand.ReturnCoPer(false);
        cpu.smu.Mp1Smu.SMU_MSG_SetDldoPsmMargin = _sendSmuCommand.ReturnCoPer(true);

        var options = new Dictionary<int, double>
        {
            { 0, profile.Coper0Value }, { 1, profile.Coper1Value }, { 2, profile.Coper2Value }, { 3, profile.Coper3Value },
            { 4, profile.Coper4Value }, { 5, profile.Coper5Value }, { 6, profile.Coper6Value }, { 7, profile.Coper7Value },
            { 8, profile.Coper8Value }, { 9, profile.Coper9Value }, { 10, profile.Coper10Value }, { 11, profile.Coper11Value },
            { 12, profile.Coper12Value }, { 13, profile.Coper13Value }, { 14, profile.Coper14Value }, { 15, profile.Coper15Value }
        };

        var checks = new Dictionary<int, bool>
        {
            { 0, profile.Coper0 }, { 1, profile.Coper1 }, { 2, profile.Coper2 }, { 3, profile.Coper3 },
            { 4, profile.Coper4 }, { 5, profile.Coper5 }, { 6, profile.Coper6 }, { 7, profile.Coper7 },
            { 8, profile.Coper8 }, { 9, profile.Coper9 }, { 10, profile.Coper10 }, { 11, profile.Coper11 },
            { 12, profile.Coper12 }, { 13, profile.Coper13 }, { 14, profile.Coper14 }, { 15, profile.Coper15 }
        };

        for (var i = 0; i < cpu.info.topology.physicalCores; i++)
        {
            var checkbox = i < 16 && checks[i];
            if (checkbox)
            {
                var setVal = options[i];
                var mapIndex = i < 8 ? 0 : 1;
                if (((~cpu.info.topology.coreDisableMap[mapIndex] >> i) & 1) == 1)
                {
                    if (cpu.smu.Rsmu.SMU_MSG_SetDldoPsmMargin != 0U)
                    {
                        cpu.SetPsmMarginSingleCore(GetCoreMask(cpu, i), Convert.ToInt32(setVal));
                    }
                }
            }
        }
    }

    private string ProcessSmuFeatures(Profile profile)
    {
        var adjline = "";

        if (profile.SmuFeatureCclk) adjline += " --enable-feature=1";
        else adjline += " --disable-feature=1";

        if (profile.SmuFeatureData) adjline += " --enable-feature=4";
        else adjline += " --disable-feature=4";

        if (profile.SmuFeaturePpt) adjline += " --enable-feature=8";
        else adjline += " --disable-feature=8";

        if (profile.SmuFeatureTdc) adjline += " --enable-feature=16";
        else adjline += " --disable-feature=16";

        if (profile.SmuFeatureThermal) adjline += " --enable-feature=32";
        else adjline += " --disable-feature=32";

        if (profile.SmuFeaturePowerDown) adjline += " --enable-feature=256";
        else adjline += " --disable-feature=256";

        if (profile.SmuFeatureProchot) adjline += " --enable-feature=0,32";
        else adjline += " --disable-feature=0,32";

        if (profile.SmuFeatureStapm) adjline += " --enable-feature=0,128";
        else adjline += " --disable-feature=0,128";

        if (profile.SmuFeatureCStates) adjline += " --enable-feature=0,256";
        else adjline += " --disable-feature=0,256";

        if (profile.SmuFeatureGfxDutyCycle) adjline += " --enable-feature=0,512";
        else adjline += " --disable-feature=0,512";

        if (profile.SmuFeatureAplusA) adjline += " --enable-feature=0,1024";
        else adjline += " --disable-feature=0,1024";

        return adjline;
    }

    private static uint GetCoreMask(Cpu cpu, int coreIndex)
    {
        var ccxInCcd = cpu.info.family >= Cpu.Family.FAMILY_19H ? 1U : 2U;
        var coresInCcx = 8 / ccxInCcd;

        var ccd = Convert.ToUInt32(coreIndex / 8);
        var ccx = Convert.ToUInt32(coreIndex / coresInCcx - ccxInCcd * ccd);
        var core = Convert.ToUInt32(coreIndex % coresInCcx);
        var coreMask = cpu.MakeCoreMask(core, ccd, ccx);
        return coreMask;
    }
}
