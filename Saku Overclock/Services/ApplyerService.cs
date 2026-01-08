using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Models;
using static Saku_Overclock.Services.CpuService;
using static Saku_Overclock.Services.PresetManagerService;

namespace Saku_Overclock.Services;

public partial class ApplyerService(
    IAppSettingsService settingsService,
    ISendSmuCommandService sendSmuCommand,
    IOcFinderService ocFinder,
    IPresetManagerService presetManager,
    ICpuService cpuService)
    : IApplyerService
{
    private Timer? _timer; // Таймер для пере-применения настроек разгона
    private readonly Lock _timerLock = new(); // lock-объект для таймера

    private CancellationTokenSource?
        _applyDebounceCts; // Токен отмены для отмены применения пресета при помощи горячих клавиш

    private readonly Lock _applyDebounceLock = new(); // lock-объект для применения пресета при помощи горячих клавиш
    private string _pendingPresetToApply = string.Empty; // Пресет, который нужно применить
    private bool _isCustomPreset; // Флаг типа пресета для применения
    private int _pendingCustomPresetIndex = -1; // Индекс кастомного пресета для применения

    private string _selectedPreset = "Unknown"; // Применённый пресет

    public async Task ApplyCustomPreset(Preset preset, bool saveInfo = false, bool onlyDebugFunctions = false)
    {
        try
        {
            _selectedPreset = preset.Presetname;
            var adjline = ParseOverclockPreset(preset, onlyDebugFunctions);
            settingsService.RyzenAdjLine = adjline;
            settingsService.SaveSettings();
            await ApplySettings(saveInfo);
        }
        catch (Exception ex)
        {
            await LogHelper.LogError("[Applyer]::ApplyPreset_FAIL - " + ex);
        }
    }

    public async Task ApplyPremadePreset(PresetType presetType, bool presetSelected = true)
    {
        try
        {
            var preset = ocFinder.CreatePreset(presetType,
                (OptimizationLevel)settingsService.PremadeOptimizationLevel);
            settingsService.RyzenAdjLine = preset.CommandString;
            await ApplySettings(false);
        }
        catch (Exception ex)
        {
            await LogHelper.LogError("[Applyer]::ApplyPreset_FAIL - " + ex);
        }

        if (presetSelected)
        {
            var presetTypeString = presetType.ToString();
            presetManager.SelectPremadePreset(presetTypeString);
            _selectedPreset = ("Shell_Preset_" + presetTypeString).GetLocalized();
        }
    }

    public async Task
        AutoApplySettingsWithAppStart() // Запустить все команды после запуска приложения если включен Авто-применять Разгон
    {
        settingsService.LoadSettings();
        presetManager.LoadSettings();

        ocFinder.LazyInitTdp();

        if (settingsService.ReapplyLatestSettingsOnAppLaunch)
        {
            try
            {
                sendSmuCommand.ApplyQuickSmuCommand(true);
            }
            catch (Exception ex)
            {
                await LogHelper.LogError("[AutoApplySettingsWithAppStart]::ApplyQuickSmuCommand_FAILED - " + ex);
            }

            try
            {
                if (settingsService.Preset != -1)
                {
                    if (settingsService.Preset < presetManager.Presets.Length)
                    {
                        await ApplyCustomPreset(presetManager.Presets[settingsService.Preset]);
                        if (presetManager.Presets[settingsService.Preset].AutoPstate &&
                            presetManager.Presets[settingsService.Preset].EnablePstateEditor)
                        {
                            //ПараметрыPage.WritePstates();
                        }
                    }
                }
                else
                {
                    await ApplyPremadePreset(GetActivePresetType());
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogError("[AutoApplySettingsWithAppStart]::ApplyLastSettings_FAILED - " + ex);
            }
        }
    }

    public PresetId SwitchCustomPreset()
    {
        if (presetManager.Presets.Length == 0)
        {
            return SwitchPremadePreset();
        }

        var presetId = presetManager.GetNextCustomPreset();

        if (!string.IsNullOrEmpty(presetId.PresetName))
        {
            // Сохраняем информацию о пресете для отложенного применения 
            lock (_applyDebounceLock)
            {
                _pendingPresetToApply = presetId.PresetName;
                _isCustomPreset = true;
                _pendingCustomPresetIndex = presetId.PresetIndex;
            }

            ScheduleApplyPreset();
        }

        return presetId;
    }

    public PresetId SwitchPremadePreset()
    {
        var presetId = presetManager.GetNextPremadePreset();

        if (!string.IsNullOrEmpty(presetId.PresetName))
        {
            // Сохраняем информацию о пресете для отложенного применения 
            lock (_applyDebounceLock)
            {
                _pendingPresetToApply = presetId.PresetKey;
                _isCustomPreset = false;
            }

            ScheduleApplyPreset();
        }

        return presetId;
    }

    public string GetSelectedPresetName() => _selectedPreset;

    private void ScheduleApplyPreset()
    {
        lock (_applyDebounceLock)
        {
            // Отменяем предыдущий таймер
            _applyDebounceCts?.Cancel();
            _applyDebounceCts = new CancellationTokenSource();
            var token = _applyDebounceCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(1500), token);

                    try
                    {
                        // ТОЛЬКО ЗДЕСЬ обновляем настройки и применяем пресет
                        string presetToApply;
                        bool isCustom;
                        int customIndex;

                        lock (_applyDebounceLock)
                        {
                            presetToApply = _pendingPresetToApply;
                            isCustom = _isCustomPreset;
                            customIndex = _pendingCustomPresetIndex;

                            // Сбрасываем виртуальное состояние после применения
                            presetManager.ResetPresetStateAfterApply();
                        }

                        if (!string.IsNullOrEmpty(presetToApply))
                        {
                            if (isCustom)
                            {
                                if (customIndex < 0 || customIndex >= presetManager.Presets.Length)
                                {
                                    await LogHelper.TraceIt_TraceError($"Invalid custom preset index: {customIndex}");
                                }
                                else
                                {
                                    settingsService.Preset = customIndex;
                                    await ApplyCustomPreset(presetManager.Presets[customIndex]);

                                    _selectedPreset = presetManager.Presets[customIndex].Presetname;
                                }
                            }
                            else
                            {
                                var presetType = GetActivePresetType();
                                _selectedPreset = ("Shell_Preset_" + _pendingPresetToApply).GetLocalized();
                                await ApplyPremadePreset(presetType);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        await LogHelper.LogError($"Error applying preset: {ex.Message}");
                    }
                }
                catch (TaskCanceledException)
                {
                    // Таймер был отменен из-за нового нажатия
                }
                catch (Exception ex)
                {
                    await LogHelper.LogError($"Error in preset scheduling: {ex.Message}");
                }
            }, token);
        }
    }

    private PresetType GetActivePresetType()
    {
        return settingsService switch
        {
            { PremadeMaxActivated: true } => PresetType.Max,
            { PremadeSpeedActivated: true } => PresetType.Speed,
            { PremadeEcoActivated: true } => PresetType.Eco,
            { PremadeMinActivated: true } => PresetType.Min,
            _ => PresetType.Balance
        };
    }

    private async Task ApplySettings(bool saveinfo)
    {
        try
        {
            if (settingsService.ReapplyOverclock)
            {
                int intervalMs;
                try
                {
                    intervalMs = (int)(settingsService.ReapplyOverclockTimer * 1000);
                    if (intervalMs <= 0)
                    {
                        throw new ArgumentException("Интервал должен быть больше 0");
                    }
                }
                catch
                {
                    await LogHelper.TraceIt_TraceError(
                        "Время авто-обновления разгона некорректно и было исправлено на 3000 мс");
                    intervalMs = 3000;
                }

                lock (_timerLock)
                {
                    // Уничтожаем предыдущий таймер
                    _timer?.Dispose();

                    // Создаём новый таймер
                    _timer = new Timer(async void (_) =>
                    {
                        try
                        {
                            if (settingsService.ReapplyOverclock)
                            {
                                await Process(settingsService.RyzenAdjLine, false);
                                sendSmuCommand.ApplyQuickSmuCommand(true);
                            }
                        }
                        catch (Exception ex)
                        {
                            await LogHelper.LogError("[Applyer]::Overclock_Settings_Reapply_FAIL - " + ex);
                        }
                    }, null, intervalMs, intervalMs);
                }
            }
            else
            {
                lock (_timerLock)
                {
                    // Останавливаем и уничтожаем таймер
                    _timer?.Dispose();
                    _timer = null;
                }
            }

            await Process(settingsService.RyzenAdjLine, saveinfo);
        }
        catch (Exception ex)
        {
            await LogHelper.LogError("[Applyer]::Overclock_Settings_FirstApply_FAIL - " + ex);
        }
    }

    private async Task Process(string adjLine, bool saveinfo)
    {
        try
        {
            await Task.Run(() =>
            {
                sendSmuCommand.Translate(adjLine, saveinfo);
            });
        }
        catch (Exception ex)
        {
            await LogHelper.LogError("[Applyer]::Overclock_Settings_Apply_FAIL - " + ex);
        }
    }

    private string ParseOverclockPreset(Preset preset, bool onlyDebugFunctions)
    {
        var codenameGen = cpuService.GetCodenameGeneration();
        var isBristol = codenameGen == CodenameGeneration.Fp4;
        var isStapmTuneRequired = codenameGen is CodenameGeneration.Fp6
            or CodenameGeneration.Ff3 or CodenameGeneration.Fp7
            or CodenameGeneration.Fp8 or CodenameGeneration.Am5;
        var adjline = "";

        if (!onlyDebugFunctions)
        {
            // CPU settings
            if (preset.Cpu1)
            {
                adjline += " --tctl-temp=" + preset.Cpu1Value + (isBristol ? "000" : string.Empty);
            }

            if (preset.Cpu2)
            {
                var stapmBoostMillisecondsBristol = preset.Cpu5Value * 1000 < 180000 ? preset.Cpu5Value * 1000 : 180000;
                adjline += " --stapm-limit=" + preset.Cpu2Value + "000" +
                           (isBristol ? ",2," + stapmBoostMillisecondsBristol : string.Empty);
            }

            if (preset.Cpu3)
            {
                adjline += " --fast-limit=" + preset.Cpu3Value + "000";
            }

            if (preset.Cpu4)
            {
                adjline += " --slow-limit=" + preset.Cpu4Value + "000" +
                           (isBristol ? "," + preset.Cpu4Value + "000,0" : string.Empty);
            }

            if (preset.Cpu5)
            {
                adjline += " --stapm-time=" + preset.Cpu5Value;
            }

            if (preset.Cpu6)
            {
                adjline += " --slow-time=" + preset.Cpu6Value;
            }

            if (preset.Cpu7)
            {
                adjline += " --cHTC-temp=" + preset.Cpu7Value;
            }

            // VRM settings
            if (preset.Vrm1)
            {
                adjline += " --vrmmax-current=" + preset.Vrm1Value + "000" +
                           (isBristol ? "," + preset.Vrm3Value + "000," + preset.Vrm3Value + "000" : string.Empty);
            }

            if (preset.Vrm2)
            {
                adjline += " --vrm-current=" + preset.Vrm2Value + "000" +
                           (isBristol ? "," + preset.Vrm4Value + "000," + preset.Vrm4Value + "000" : string.Empty);
            }

            if (preset.Vrm3 && !isBristol)
            {
                adjline += " --vrmsocmax-current=" + preset.Vrm3Value + "000";
            }

            if (preset.Vrm4 && !isBristol)
            {
                adjline += " --vrmsoc-current=" + preset.Vrm4Value + "000";
            }

            if (preset.Vrm5)
            {
                adjline += " --psi0-current=" + preset.Vrm5Value + "000" +
                           (isBristol ? "," + preset.Vrm6Value + "000," + preset.Vrm6Value + "000" : string.Empty);
            }

            if (preset.Vrm6 && !isBristol)
            {
                adjline += " --psi0soc-current=" + preset.Vrm6Value + "000";
            }

            if (preset.Vrm7)
            {
                var prochotDeassertionTimeMillisecondsBristol = preset.Vrm7Value < 100 ? preset.Vrm7Value : 100;
                adjline += " --prochot-deassertion-ramp=" +
                           (isBristol ? prochotDeassertionTimeMillisecondsBristol : preset.Vrm7Value);
            }

            // GPU settings
            if (preset.Gpu1)
            {
                adjline += " --min-socclk-frequency=" + preset.Gpu1Value;
            }

            if (preset.Gpu2)
            {
                adjline += " --max-socclk-frequency=" + preset.Gpu2Value;
            }

            if (preset.Gpu3)
            {
                adjline += " --min-fclk-frequency=" + preset.Gpu3Value;
            }

            if (preset.Gpu4)
            {
                adjline += " --max-fclk-frequency=" + preset.Gpu4Value;
            }

            if (preset.Gpu5)
            {
                adjline += " --min-vcn=" + preset.Gpu5Value;
            }

            if (preset.Gpu6)
            {
                adjline += " --max-vcn=" + preset.Gpu6Value;
            }

            if (preset.Gpu7)
            {
                adjline += " --min-lclk=" + preset.Gpu7Value;
            }

            if (preset.Gpu8)
            {
                adjline += " --max-lclk=" + preset.Gpu8Value;
            }

            if (preset.Gpu9)
            {
                adjline += " --min-gfxclk=" + preset.Gpu9Value;
            }

            if (preset.Gpu10)
            {
                adjline += " --max-gfxclk=" + preset.Gpu10Value;
            }

            if (preset.Gpu16)
            {
                var fp6FeaturesSet = preset.Gpu16Value != 0 ? " --disable-feature=0,32" : " --enable-feature=0,32";
                var ryzen3000LineFix = preset.Gpu16Value != 0
                    ? " --setcpu-freqto-ramstate=0"
                    : " --stopcpu-freqto-ramstate=0";
                adjline += codenameGen switch
                {
                    CodenameGeneration.Fp6 or CodenameGeneration.Ff3 => fp6FeaturesSet,
                    CodenameGeneration.Fp7 or CodenameGeneration.Fp8 => preset.Gpu16Value != 0
                        ? " --disable-feature=0,16"
                        : " --enable-feature=0,16",
                    CodenameGeneration.Am5 => preset.Gpu16Value != 0
                        ? " --disable-feature=128"
                        : " --enable-feature=128",
                    _ => cpuService.IsRaven ? fp6FeaturesSet : ryzen3000LineFix,
                };
            }

            // Advanced settings

            if (preset.Advncd4)
            {
                adjline += " --psi3cpu_current=" + preset.Advncd4Value + "000";
            }

            if (preset.Advncd5)
            {
                adjline += " --psi3gfx_current=" + preset.Advncd5Value + "000";
            }

            if (preset.Advncd6)
            {
                adjline += " --apu-skin-temp=" + preset.Advncd6Value * 256;
            }

            if (preset.Advncd7)
            {
                adjline += " --dgpu-skin-temp=" + preset.Advncd7Value * 256;
            }

            if (preset.Advncd8)
            {
                adjline += " --apu-slow-limit=" + preset.Advncd8Value + "000";
            }

            if (preset.Advncd9)
            {
                adjline += " --skin-temp-limit=" + preset.Advncd9Value + "000";

                if (isStapmTuneRequired)
                {
                    adjline += " --stapm-limit=" + preset.Advncd9Value + "000";
                }
            }

            if (preset.Advncd10)
            {
                var val = 0x480000 | (int)preset.Advncd10Value; // Всегда на 1.1V
                adjline += codenameGen switch
                {
                    CodenameGeneration.Fp5 => " --set-gpuclockoverdrive-byvid=" + val,
                    _ => " --gfx-clk=" + preset.Advncd10Value,
                };
            }

            if (preset.Advncd11)
            {
                adjline += " --oc-clk=" + preset.Advncd11Value;
            }

            if (preset.Advncd12)
            {
                adjline += " --oc-volt=" + Math.Round((1.55 - preset.Advncd12Value / 1000) / 0.00625);
            }

            if (preset.Advncd13)
            {
                adjline += preset.Advncd13Value switch
                {
                    2 => " --power-saving=1",
                    _ => " --max-performance=1",
                };
            }

            if (preset.Advncd14)
            {
                adjline += preset.Advncd14Value switch
                {
                    1 => " --enable-oc=0 --enable-oc=16777216",
                    _ => " --disable-oc=0",
                };
            }

            if (preset.Advncd15)
            {
                adjline += " --pbo-scalar=" + preset.Advncd15Value * 100;
            }

            // CO All
            if (preset.Coall)
            {
                adjline += ProcessCoallSettings(preset.Coallvalue);
            }

            // CO GFX
            if (preset.Cogfx)
            {
                adjline += ProcessCoallSettings(preset.Cogfxvalue, true);
            }

            // CO Per Core
            if (preset.Comode && preset.Coprefmode != 0)
            {
                adjline += ProcessCoperSettings(preset, cpuService.IsDragonRange);
            }
        }

        // SMU Features
        if (preset.SmuFunctionsEnabl)
        {
            adjline += ProcessSmuFeatures(preset);
        }

        return adjline + " ";
    }

    private static string ProcessCoallSettings(double value, bool cogfx = false)
    {
        var adjline = "";
        var setString = cogfx ? "cogfx" : "coall";
        adjline += (value >= 0.0)
            ? $" --set-{setString}={value} "
            : $" --set-{setString}={Convert.ToUInt32(0x100000 - (uint)(-1 * (int)value))} ";
        return adjline;
    }

    private string ProcessCoperSettings(Preset preset, bool isDragonRange)
    {
        var adjline = "";

        switch (preset.Coprefmode)
        {
            case 1 when isDragonRange:
                adjline += ProcessDragonRangeCoper(preset);
                break;
            case 1:
                adjline += ProcessLaptopCoper(preset, cpuService.PhysicalCores);
                break;
            case 2:
                adjline += ProcessDesktopCoper(preset);
                break;
            case 3:
                ProcessIrusanovMethod(preset);
                break;
        }

        return adjline;
    }

    private static string ProcessDragonRangeCoper(Preset preset)
    {
        var adjline = "";

        if (preset.Coper0)
        {
            adjline += $" --set-coper={0 | ((int)preset.Coper0Value & 0xFFFF)} ";
        }

        if (preset.Coper1)
        {
            adjline += $" --set-coper={1048576 | ((int)preset.Coper1Value & 0xFFFF)} ";
        }

        if (preset.Coper2)
        {
            adjline += $" --set-coper={2097152 | ((int)preset.Coper2Value & 0xFFFF)} ";
        }

        if (preset.Coper3)
        {
            adjline += $" --set-coper={3145728 | ((int)preset.Coper3Value & 0xFFFF)} ";
        }

        if (preset.Coper4)
        {
            adjline += $" --set-coper={4194304 | ((int)preset.Coper4Value & 0xFFFF)} ";
        }

        if (preset.Coper5)
        {
            adjline += $" --set-coper={5242880 | ((int)preset.Coper5Value & 0xFFFF)} ";
        }

        if (preset.Coper6)
        {
            adjline += $" --set-coper={6291456 | ((int)preset.Coper6Value & 0xFFFF)} ";
        }

        if (preset.Coper7)
        {
            adjline += $" --set-coper={7340032 | ((int)preset.Coper7Value & 0xFFFF)} ";
        }

        if (preset.Coper8)
        {
            adjline +=
                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((0 % 8) & 15)) << 20) | ((int)preset.Coper8Value & 0xFFFF)} ";
        }

        if (preset.Coper9)
        {
            adjline +=
                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((1 % 8) & 15)) << 20) | ((int)preset.Coper9Value & 0xFFFF)} ";
        }

        if (preset.Coper10)
        {
            adjline +=
                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((2 % 8) & 15)) << 20) | ((int)preset.Coper10Value & 0xFFFF)} ";
        }

        if (preset.Coper11)
        {
            adjline +=
                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((3 % 8) & 15)) << 20) | ((int)preset.Coper11Value & 0xFFFF)} ";
        }

        if (preset.Coper12)
        {
            adjline +=
                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((4 % 8) & 15)) << 20) | ((int)preset.Coper12Value & 0xFFFF)} ";
        }

        if (preset.Coper13)
        {
            adjline +=
                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((5 % 8) & 15)) << 20) | ((int)preset.Coper13Value & 0xFFFF)} ";
        }

        if (preset.Coper14)
        {
            adjline +=
                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((6 % 8) & 15)) << 20) | ((int)preset.Coper14Value & 0xFFFF)} ";
        }

        if (preset.Coper15)
        {
            adjline +=
                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((7 % 8) & 15)) << 20) | ((int)preset.Coper15Value & 0xFFFF)} ";
        }

        return adjline;
    }

    private static string ProcessLaptopCoper(Preset preset, uint cores)
    {
        var adjline = "";

        if (preset.Coper0)
        {
            adjline += $" --set-coper={0 | ((int)preset.Coper0Value & 0xFFFF)} ";
        }

        if (preset.Coper1)
        {
            adjline += $" --set-coper={(1 << 20) | ((int)preset.Coper1Value & 0xFFFF)} ";
        }

        if (preset.Coper2)
        {
            adjline += $" --set-coper={(2 << 20) | ((int)preset.Coper2Value & 0xFFFF)} ";
        }

        if (preset.Coper3)
        {
            adjline += $" --set-coper={(3 << 20) | ((int)preset.Coper3Value & 0xFFFF)} ";
        }

        if (preset.Coper4)
        {
            adjline += $" --set-coper={(4 << 20) | ((int)preset.Coper4Value & 0xFFFF)} ";
        }

        if (preset.Coper5)
        {
            adjline += $" --set-coper={(5 << 20) | ((int)preset.Coper5Value & 0xFFFF)} ";
        }

        if (preset.Coper6)
        {
            adjline += $" --set-coper={(6 << 20) | ((int)preset.Coper6Value & 0xFFFF)} ";
        }

        if (preset.Coper7)
        {
            adjline += $" --set-coper={(7 << 20) | ((int)preset.Coper7Value & 0xFFFF)} ";
        }

        if (cores > 8)
        {
            if (preset.Coper8)
            {
                adjline += $" --set-coper={(0x100 << 20) | ((int)preset.Coper8Value & 0xFFFF)} ";
            }

            if (preset.Coper9)
            {
                adjline += $" --set-coper={(0x101 << 20) | ((int)preset.Coper9Value & 0xFFFF)} ";
            }

            if (preset.Coper10)
            {
                adjline += $" --set-coper={(0x102 << 20) | ((int)preset.Coper10Value & 0xFFFF)} ";
            }

            if (preset.Coper11)
            {
                adjline += $" --set-coper={(0x103 << 20) | ((int)preset.Coper11Value & 0xFFFF)} ";
            }

            if (preset.Coper12)
            {
                adjline += $" --set-coper={(0x104 << 20) | ((int)preset.Coper12Value & 0xFFFF)} ";
            }

            if (preset.Coper13)
            {
                adjline += $" --set-coper={(0x105 << 20) | ((int)preset.Coper13Value & 0xFFFF)} ";
            }

            if (preset.Coper14)
            {
                adjline += $" --set-coper={(0x106 << 20) | ((int)preset.Coper14Value & 0xFFFF)} ";
            }

            if (preset.Coper15)
            {
                adjline += $" --set-coper={(0x107 << 20) | ((int)preset.Coper15Value & 0xFFFF)} ";
            }
        }

        return adjline;
    }

    private static string ProcessDesktopCoper(Preset preset) => ProcessDragonRangeCoper(preset);

    private void ProcessIrusanovMethod(Preset preset)
    {
        if (cpuService.SmuCoperCommandRsmu == 0)
        {
            cpuService.SmuCoperCommandRsmu = sendSmuCommand.ReturnCoPer(false);
        }

        if (cpuService.SmuCoperCommandMp1 == 0)
        {
            cpuService.SmuCoperCommandMp1 = sendSmuCommand.ReturnCoPer();
        }

        var options = new Dictionary<int, double>
        {
            { 0, preset.Coper0Value }, { 1, preset.Coper1Value }, { 2, preset.Coper2Value }, { 3, preset.Coper3Value },
            { 4, preset.Coper4Value }, { 5, preset.Coper5Value }, { 6, preset.Coper6Value }, { 7, preset.Coper7Value },
            { 8, preset.Coper8Value }, { 9, preset.Coper9Value }, { 10, preset.Coper10Value },
            { 11, preset.Coper11Value },
            { 12, preset.Coper12Value }, { 13, preset.Coper13Value }, { 14, preset.Coper14Value },
            { 15, preset.Coper15Value }
        };

        var checks = new Dictionary<int, bool>
        {
            { 0, preset.Coper0 }, { 1, preset.Coper1 }, { 2, preset.Coper2 }, { 3, preset.Coper3 },
            { 4, preset.Coper4 }, { 5, preset.Coper5 }, { 6, preset.Coper6 }, { 7, preset.Coper7 },
            { 8, preset.Coper8 }, { 9, preset.Coper9 }, { 10, preset.Coper10 }, { 11, preset.Coper11 },
            { 12, preset.Coper12 }, { 13, preset.Coper13 }, { 14, preset.Coper14 }, { 15, preset.Coper15 }
        };

        for (var i = 0; i < cpuService.PhysicalCores; i++)
        {
            var checkbox = i < 16 && checks[i];
            if (checkbox)
            {
                var setVal = options[i];
                var mapIndex = i < 8 ? 0 : 1;
                if (((~cpuService.CoreDisableMap[mapIndex] >> i) & 1) == 1)
                {
                    if (cpuService.SmuCoperCommandRsmu != 0)
                    {
                        cpuService.SetCoperSingleCore(GetCoreMask(i), Convert.ToInt32(setVal));
                    }
                }
            }
        }
    }

    private static string ProcessSmuFeatures(Preset preset)
    {
        var adjline = "";

        if (preset.SmuFeatureCclk)
        {
            adjline += " --enable-feature=1";
        }
        else
        {
            adjline += " --disable-feature=1";
        }

        if (preset.SmuFeatureData)
        {
            adjline += " --enable-feature=4";
        }
        else
        {
            adjline += " --disable-feature=4";
        }

        if (preset.SmuFeaturePpt)
        {
            adjline += " --enable-feature=8";
        }
        else
        {
            adjline += " --disable-feature=8";
        }

        if (preset.SmuFeatureTdc)
        {
            adjline += " --enable-feature=16";
        }
        else
        {
            adjline += " --disable-feature=16";
        }

        if (preset.SmuFeatureThermal)
        {
            adjline += " --enable-feature=32";
        }
        else
        {
            adjline += " --disable-feature=32";
        }

        if (preset.SmuFeaturePowerDown)
        {
            adjline += " --enable-feature=256";
        }
        else
        {
            adjline += " --disable-feature=256";
        }

        if (preset.SmuFeatureProchot)
        {
            adjline += " --enable-feature=0,32";
        }
        else
        {
            adjline += " --disable-feature=0,32";
        }

        if (preset.SmuFeatureStapm)
        {
            adjline += " --enable-feature=0,128";
        }
        else
        {
            adjline += " --disable-feature=0,128";
        }

        if (preset.SmuFeatureCStates)
        {
            adjline += " --enable-feature=0,256";
        }
        else
        {
            adjline += " --disable-feature=0,256";
        }

        if (preset.SmuFeatureGfxDutyCycle)
        {
            adjline += " --enable-feature=0,512";
        }
        else
        {
            adjline += " --disable-feature=0,512";
        }

        if (preset.SmuFeatureAplusA)
        {
            adjline += " --enable-feature=0,1024";
        }
        else
        {
            adjline += " --disable-feature=0,1024";
        }

        return adjline;
    }

    private uint GetCoreMask(int coreIndex)
    {
        var ccxInCcd = cpuService.Family >= CpuFamily.Family19H ? 1U : 2U;
        var coresInCcx = 8 / ccxInCcd;

        var ccd = Convert.ToUInt32(coreIndex / 8);
        var ccx = Convert.ToUInt32(coreIndex / coresInCcx - ccxInCcd * ccd);
        var core = Convert.ToUInt32(coreIndex % coresInCcx);
        var coreMask = cpuService.MakeCoreMask(core, ccd, ccx);
        return coreMask;
    }

    public void Dispose()
    {
        lock (_timerLock)
        {
            _timer?.Dispose();
            _timer = null;
        }

        GC.SuppressFinalize(this);
    }
}