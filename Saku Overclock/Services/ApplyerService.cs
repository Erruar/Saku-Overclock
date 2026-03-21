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
            _selectedPreset = preset.PresetName;
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
            
            // TODO: FIX PREMADE PRESETS
            var preset = ocFinder.CreatePreset(presetType, OptimizationLevel.Basic);
                //(OptimizationLevel)settingsService.PremadeOptimizationLevel);
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

                                    _selectedPreset = presetManager.Presets[customIndex].PresetName;
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
            
            // TODO: FIX PREMADE PRESETS
            /*{ PremadeMaxActivated: true } => PresetType.Max,
            { PremadeSpeedActivated: true } => PresetType.Speed,
            { PremadeEcoActivated: true } => PresetType.Eco,
            { PremadeMinActivated: true } => PresetType.Min,*/
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
            if (preset.CpuSettings.CpuMaximumTemperature.IsEnabled)
            {
                adjline += " --tctl-temp=" + preset.CpuSettings.CpuMaximumTemperature.Value + (isBristol ? "000" : string.Empty);
            }

            if (preset.CpuSettings.CpuSustainedPowerLimit.IsEnabled)
            {
                var stapmBoostMillisecondsBristol = preset.CpuSettings.CpuBoostTimeSlow.Value * 1000 < 180000 ? preset.CpuSettings.CpuBoostTimeSlow.Value * 1000 : 180000;
                adjline += " --stapm-limit=" + preset.CpuSettings.CpuSustainedPowerLimit.Value + "000" +
                           (isBristol ? ",2," + stapmBoostMillisecondsBristol : string.Empty);
            }

            if (preset.CpuSettings.CpuActualPowerLimit.IsEnabled)
            {
                adjline += " --fast-limit=" + preset.CpuSettings.CpuActualPowerLimit.Value + "000";
            }

            if (preset.CpuSettings.CpuAveragePowerLimit.IsEnabled)
            {
                adjline += " --slow-limit=" + preset.CpuSettings.CpuAveragePowerLimit.Value + "000" +
                           (isBristol ? "," + preset.CpuSettings.CpuAveragePowerLimit.Value + "000,0" : string.Empty);
            }

            if (preset.CpuSettings.CpuBoostTimeSlow.IsEnabled)
            {
                adjline += " --stapm-time=" + preset.CpuSettings.CpuBoostTimeSlow.Value;
            }

            if (preset.CpuSettings.CpuBoostTimeFast.IsEnabled)
            {
                adjline += " --slow-time=" + preset.CpuSettings.CpuBoostTimeFast.Value;
            }

            // VRM settings
            if (preset.VrmSettings.VrmCpuEdcCurrentLimit.IsEnabled)
            {
                adjline += " --vrmmax-current=" + preset.VrmSettings.VrmCpuEdcCurrentLimit.Value + "000" +
                           (isBristol ? "," + preset.VrmSettings.VrmSocEdcCurrentLimit.Value + "000," + preset.VrmSettings.VrmSocEdcCurrentLimit.Value + "000" : string.Empty);
            }

            if (preset.VrmSettings.VrmCpuTdcCurrentLimit.IsEnabled)
            {
                adjline += " --vrm-current=" + preset.VrmSettings.VrmCpuTdcCurrentLimit.Value + "000" +
                           (isBristol ? "," + preset.VrmSettings.VrmSocEdcCurrentLimit.Value + "000," + preset.VrmSettings.VrmSocEdcCurrentLimit.Value + "000" : string.Empty);
            }

            if (preset.VrmSettings.VrmSocEdcCurrentLimit.IsEnabled && !isBristol)
            {
                adjline += " --vrmsocmax-current=" + preset.VrmSettings.VrmSocEdcCurrentLimit.Value + "000";
            }

            if (preset.VrmSettings.VrmSocTdcCurrentLimit.IsEnabled && !isBristol)
            {
                adjline += " --vrmsoc-current=" + preset.VrmSettings.VrmSocEdcCurrentLimit.Value + "000";
            }

            if (preset.VrmSettings.VrmPowerSaveVddCurrentLimit.IsEnabled && !isBristol)
            {
                adjline += " --psi0-current=" + preset.VrmSettings.VrmPowerSaveVddCurrentLimit.Value + "000" +
                           (isBristol ? "," + preset.VrmSettings.VrmPowerSaveSocCurrentLimit.Value + "000," + preset.VrmSettings.VrmPowerSaveSocCurrentLimit.Value + "000" : string.Empty);
            }

            if (preset.VrmSettings.VrmPowerSaveSocCurrentLimit.IsEnabled && !isBristol)
            {
                adjline += " --psi0soc-current=" + preset.VrmSettings.VrmPowerSaveSocCurrentLimit.Value + "000";
            }
            
            
            if (preset.VrmSettings.VrmPowerSaveCpuCurrentLimit.IsEnabled)
            {
                adjline += " --psi3cpu_current=" + preset.VrmSettings.VrmPowerSaveGpuCurrentLimit.Value + "000";
            }

            if (preset.VrmSettings.VrmPowerSaveGpuCurrentLimit.IsEnabled)
            {
                adjline += " --psi3gfx_current=" + preset.VrmSettings.VrmPowerSaveGpuCurrentLimit.Value + "000";
            }

            if (preset.VrmSettings.VrmCpuFrequencyRestoreTime.IsEnabled)
            {
                var prochotDeassertionTimeMillisecondsBristol = preset.VrmSettings.VrmCpuFrequencyRestoreTime.Value < 100 ? preset.VrmSettings.VrmCpuFrequencyRestoreTime.Value : 100;
                adjline += " --prochot-deassertion-ramp=" +
                           (isBristol ? prochotDeassertionTimeMillisecondsBristol : preset.VrmSettings.VrmCpuFrequencyRestoreTime.Value);
            }

            // GPU settings
            if (preset.SubsystemsSettings.MinimumSocFrequency.IsEnabled)
            {
                adjline += " --min-socclk-frequency=" + preset.SubsystemsSettings.MinimumSocFrequency.Value;
            }

            if (preset.SubsystemsSettings.MaximumSocFrequency.IsEnabled)
            {
                adjline += " --max-socclk-frequency=" + preset.SubsystemsSettings.MaximumSocFrequency.Value;
            }

            if (preset.SubsystemsSettings.MinimumFabricFrequency.IsEnabled)
            {
                adjline += " --min-fclk-frequency=" + preset.SubsystemsSettings.MinimumFabricFrequency.Value;
            }

            if (preset.SubsystemsSettings.MaximumFabricFrequency.IsEnabled)
            {
                adjline += " --max-fclk-frequency=" + preset.SubsystemsSettings.MaximumFabricFrequency.Value;
            }

            if (preset.SubsystemsSettings.MinimumVideoCodecFrequency.IsEnabled)
            {
                adjline += " --min-vcn=" + preset.SubsystemsSettings.MinimumVideoCodecFrequency.Value;
            }

            if (preset.SubsystemsSettings.MaximumVideoCodecFrequency.IsEnabled)
            {
                adjline += " --max-vcn=" + preset.SubsystemsSettings.MaximumVideoCodecFrequency.Value;
            }

            if (preset.SubsystemsSettings.MinimumDataLatchFrequency.IsEnabled)
            {
                adjline += " --min-lclk=" + preset.SubsystemsSettings.MinimumDataLatchFrequency.Value;
            }

            if (preset.SubsystemsSettings.MaximumDataLatchFrequency.IsEnabled)
            {
                adjline += " --max-lclk=" + preset.SubsystemsSettings.MaximumDataLatchFrequency.Value;
            }

            if (preset.SubsystemsSettings.MinimumIntegratedGraphicsFrequency.IsEnabled)
            {
                adjline += " --min-gfxclk=" + preset.SubsystemsSettings.MinimumIntegratedGraphicsFrequency.Value;
            }

            if (preset.SubsystemsSettings.MaximumIntegratedGraphicsFrequency.IsEnabled)
            {
                adjline += " --max-gfxclk=" + preset.SubsystemsSettings.MaximumIntegratedGraphicsFrequency.Value;
            }

            if (preset.CpuModesSettings.CpuFrequency04Fix.IsEnabled)
            {
                var fp6FeaturesSet = preset.CpuModesSettings.CpuFrequency04Fix.Value != 0 ? " --disable-feature=0,32" : " --enable-feature=0,32";
                var ryzen3000LineFix = preset.CpuModesSettings.CpuFrequency04Fix.Value != 0
                    ? " --setcpu-freqto-ramstate=0"
                    : " --stopcpu-freqto-ramstate=0";
                adjline += codenameGen switch
                {
                    CodenameGeneration.Fp6 or CodenameGeneration.Ff3 => fp6FeaturesSet,
                    CodenameGeneration.Fp7 or CodenameGeneration.Fp8 => preset.CpuModesSettings.CpuFrequency04Fix.Value != 0
                        ? " --disable-feature=0,16"
                        : " --enable-feature=0,16",
                    CodenameGeneration.Am5 => preset.CpuModesSettings.CpuFrequency04Fix.Value != 0
                        ? " --disable-feature=128"
                        : " --enable-feature=128",
                    _ => cpuService.IsRaven ? fp6FeaturesSet : ryzen3000LineFix,
                };
            }

            // Advanced CPU modes
            if (preset.CpuSettings.IntegratedGpuMaximumTemperature.IsEnabled)
            {
                adjline += " --apu-skin-temp=" + preset.CpuSettings.IntegratedGpuMaximumTemperature.Value * 256;
            }

            if (preset.CpuSettings.DiscreteGpuMaximumTemperature.IsEnabled)
            {
                adjline += " --dgpu-skin-temp=" + preset.CpuSettings.DiscreteGpuMaximumTemperature.Value * 256;
            }

            if (preset.CpuSettings.IntegratedGpuPowerLimit.IsEnabled)
            {
                adjline += " --apu-slow-limit=" + preset.CpuSettings.IntegratedGpuPowerLimit.Value + "000";
            }

            if (preset.CpuSettings.LaptopPowerLimit.IsEnabled)
            {
                var adjustPower = preset.CpuSettings.LaptopPowerLimit.Value + "000";
                adjline += " --skin-temp-limit=" + adjustPower;

                if (isStapmTuneRequired)
                {
                    adjline += " --stapm-limit=" + adjustPower;
                }
            }

            if (preset.FrequenciesSettings.IntegratedGraphicsFrequency.IsEnabled)
            {
                var val = 0x480000 | (int)preset.FrequenciesSettings.IntegratedGraphicsFrequency.Value; // Всегда на 1.1V
                adjline += codenameGen switch
                {
                    CodenameGeneration.Fp5 => " --set-gpuclockoverdrive-byvid=" + val,
                    _ => " --gfx-clk=" + preset.FrequenciesSettings.IntegratedGraphicsFrequency.Value,
                };
            }

            if (preset.FrequenciesSettings.CpuFrequency.IsEnabled)
            {
                adjline += " --oc-clk=" + preset.FrequenciesSettings.CpuFrequency.Value;
            }

            if (preset.FrequenciesSettings.CpuVoltage.IsEnabled)
            {
                adjline += " --oc-volt=" + Math.Round((1.55 - preset.FrequenciesSettings.CpuVoltage.Value / 1000) / 0.00625);
            }

            if (preset.CpuModesSettings.PreferredMode.IsEnabled)
            {
                adjline += preset.CpuModesSettings.PreferredMode.Value switch
                {
                    2 => " --power-saving=1",
                    _ => " --max-performance=1",
                };
            }

            if (preset.CpuModesSettings.OverclockMode.IsEnabled)
            {
                adjline += preset.CpuModesSettings.OverclockMode.Value switch
                {
                    1 => " --enable-oc=0 --enable-oc=16777216",
                    _ => " --disable-oc=0",
                };
            }

            if (preset.CpuModesSettings.PboScalar.IsEnabled)
            {
                adjline += " --pbo-scalar=" + preset.CpuModesSettings.PboScalar.Value * 100;
            }

            // CO All
            if (preset.CurveOptimizerOptions.CpuCurveOptimizerUndervoltingLevel.IsEnabled)
            {
                adjline += ProcessCoallSettings(preset.CurveOptimizerOptions.CpuCurveOptimizerUndervoltingLevel.Value);
            }

            // CO GFX
            if (preset.CurveOptimizerOptions.IntegratedGpuCurveOptimizerUndervoltingLevel.IsEnabled)
            {
                adjline += ProcessCoallSettings(preset.CurveOptimizerOptions.IntegratedGpuCurveOptimizerUndervoltingLevel.Value, true);
            }

            // CO Per Core
            if (preset.CurveOptimizerAdvancedOptions.CurveOptimizerPreferredMode.IsEnabled 
                && preset.CurveOptimizerAdvancedOptions.CurveOptimizerPreferredMode.Value != 0)
            {
                adjline += ProcessPerCoreCurveOptimizerSettings(preset, cpuService.IsDragonRange);
            }
        }

        // SMU Features
        if (preset.SmuFeaturesSettings.SmuFeaturesOverride)
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

    private string ProcessPerCoreCurveOptimizerSettings(Preset preset, bool isDragonRange)
    {
        var adjline = "";

        switch (preset.CurveOptimizerAdvancedOptions.CurveOptimizerPreferredMode.Value)
        {
            case 1 when isDragonRange:
                adjline += ProcessDragonRangePerCoreCurveOptimizer(preset);
                break;
            case 1:
                adjline += ProcessLaptopPerCoreCurveOptimizer(preset, cpuService.PhysicalCores);
                break;
            case 2:
                adjline += ProcessDesktopPerCoreCurveOptimizer(preset);
                break;
            case 3:
                ProcessIrusanovMethod(preset);
                break;
        }

        return adjline;
    }

    private static string ProcessDragonRangePerCoreCurveOptimizer(Preset preset)
    {
        var adjline = "";

        if (CheckCurveOptimizerLenghtAvailability(preset))
        {
            adjline += $" --set-coper={0 | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[0] & 0xFFFF)} ";
        }

        if (CheckCurveOptimizerLenghtAvailability(preset, 1))
        {
            adjline += $" --set-coper={1048576 | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[1] & 0xFFFF)} ";
        }

        if (CheckCurveOptimizerLenghtAvailability(preset, 2))
        {
            adjline += $" --set-coper={2097152 | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[2] & 0xFFFF)} ";
        }

        if (CheckCurveOptimizerLenghtAvailability(preset, 3))
        {
            adjline += $" --set-coper={3145728 | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[3] & 0xFFFF)} ";
        }

        if (CheckCurveOptimizerLenghtAvailability(preset, 4))
        {
            adjline += $" --set-coper={4194304 | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[4] & 0xFFFF)} ";
        }

        if (CheckCurveOptimizerLenghtAvailability(preset, 5))
        {
            adjline += $" --set-coper={5242880 | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[5] & 0xFFFF)} ";
        }

        if (CheckCurveOptimizerLenghtAvailability(preset, 6))
        {
            adjline += $" --set-coper={6291456 | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[6] & 0xFFFF)} ";
        }

        if (CheckCurveOptimizerLenghtAvailability(preset, 7))
        {
            adjline += $" --set-coper={7340032 | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[7] & 0xFFFF)} ";
        }

        if (CheckCurveOptimizerLenghtAvailability(preset, 8))
        {
            adjline +=
                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((0 % 8) & 15)) << 20) | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[8] & 0xFFFF)} ";
        }

        if (CheckCurveOptimizerLenghtAvailability(preset, 9))
        {
            adjline +=
                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((1 % 8) & 15)) << 20) | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[9] & 0xFFFF)} ";
        }

        if (CheckCurveOptimizerLenghtAvailability(preset, 10))
        {
            adjline +=
                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((2 % 8) & 15)) << 20) | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[10] & 0xFFFF)} ";
        }

        if (CheckCurveOptimizerLenghtAvailability(preset, 11))
        {
            adjline +=
                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((3 % 8) & 15)) << 20) | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[11] & 0xFFFF)} ";
        }

        if (CheckCurveOptimizerLenghtAvailability(preset, 12))
        {
            adjline +=
                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((4 % 8) & 15)) << 20) | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[12] & 0xFFFF)} ";
        }

        if (CheckCurveOptimizerLenghtAvailability(preset, 13))
        {
            adjline +=
                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((5 % 8) & 15)) << 20) | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[13] & 0xFFFF)} ";
        }

        if (CheckCurveOptimizerLenghtAvailability(preset, 14))
        {
            adjline +=
                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((6 % 8) & 15)) << 20) | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[14] & 0xFFFF)} ";
        }

        if (CheckCurveOptimizerLenghtAvailability(preset, 15))
        {
            adjline +=
                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((7 % 8) & 15)) << 20) | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[15] & 0xFFFF)} ";
        }

        return adjline;
    }

    private string ProcessLaptopPerCoreCurveOptimizer(Preset preset, uint cores)
    {
        var adjline = "";

        if (CheckCurveOptimizerLenghtAvailability(preset))
        {
            adjline += $" --set-coper={0 | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[0] & 0xFFFF)} ";
        }

        if (CheckCurveOptimizerLenghtAvailability(preset, 1))
        {
            adjline += $" --set-coper={(1 << 20) | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[1] & 0xFFFF)} ";
        }

        if (CheckCurveOptimizerLenghtAvailability(preset, 2))
        {
            adjline += $" --set-coper={(2 << 20) | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[2] & 0xFFFF)} ";
        }

        if (CheckCurveOptimizerLenghtAvailability(preset, 3))
        {
            adjline += $" --set-coper={(3 << 20) | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[3] & 0xFFFF)} ";
        }

        if (CheckCurveOptimizerLenghtAvailability(preset, 4))
        {
            adjline += $" --set-coper={(4 << 20) | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[4] & 0xFFFF)} ";
        }

        if (CheckCurveOptimizerLenghtAvailability(preset, 5))
        {
            adjline += $" --set-coper={(5 << 20) | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[5] & 0xFFFF)} ";
        }

        if (CheckCurveOptimizerLenghtAvailability(preset, 6))
        {
            adjline += $" --set-coper={(6 << 20) | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[6] & 0xFFFF)} ";
        }

        if (CheckCurveOptimizerLenghtAvailability(preset, 7))
        {
            adjline += $" --set-coper={(7 << 20) | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[7] & 0xFFFF)} ";
        }

        if (cores > 8)
        {
            if (CheckCurveOptimizerLenghtAvailability(preset, 8))
            {
                adjline += $" --set-coper={(0x100 << 20) | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[8] & 0xFFFF)} ";
            }

            if (CheckCurveOptimizerLenghtAvailability(preset, 9))
            {
                adjline += $" --set-coper={(0x101 << 20) | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[9] & 0xFFFF)} ";
            }

            if (CheckCurveOptimizerLenghtAvailability(preset, 10))
            {
                adjline += $" --set-coper={(0x102 << 20) | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[10] & 0xFFFF)} ";
            }

            if (CheckCurveOptimizerLenghtAvailability(preset, 11))
            {
                adjline += $" --set-coper={(0x103 << 20) | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[11] & 0xFFFF)} ";
            }

            if (CheckCurveOptimizerLenghtAvailability(preset, 12))
            {
                adjline += $" --set-coper={(0x104 << 20) | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[12] & 0xFFFF)} ";
            }

            if (CheckCurveOptimizerLenghtAvailability(preset, 13))
            {
                adjline += $" --set-coper={(0x105 << 20) | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[13] & 0xFFFF)} ";
            }

            if (CheckCurveOptimizerLenghtAvailability(preset, 14))
            {
                adjline += $" --set-coper={(0x106 << 20) | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[14] & 0xFFFF)} ";
            }

            if (CheckCurveOptimizerLenghtAvailability(preset, 15))
            {
                adjline += $" --set-coper={(0x107 << 20) | ((int)preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[15] & 0xFFFF)} ";
            }
        }

        return adjline;
    }

    private static bool CheckCurveOptimizerLenghtAvailability(Preset preset, int index = 0)
    {
        return preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.IsEnabled.Length > index &&
               preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value.Length > index &&
               preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.IsEnabled[index];
    }

    private static string ProcessDesktopPerCoreCurveOptimizer(Preset preset) => ProcessDragonRangePerCoreCurveOptimizer(preset);

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

        for (var i = 0; i < cpuService.PhysicalCores; i++)
        {
            var checkbox = i < 16 && 
                           i < preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.IsEnabled.Length && 
                           preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.IsEnabled[i];
            if (checkbox && i < preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value.Length)
            {
                var setVal = preset.CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[i];
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

        if (preset.SmuFeaturesSettings.CpuFrequencyScaling)
        {
            adjline += " --enable-feature=1";
        }
        else
        {
            adjline += " --disable-feature=1";
        }

        if (preset.SmuFeaturesSettings.SensorsDataCalculation)
        {
            adjline += " --enable-feature=4";
        }
        else
        {
            adjline += " --disable-feature=4";
        }

        if (preset.SmuFeaturesSettings.PowerLimits)
        {
            adjline += " --enable-feature=8";
        }
        else
        {
            adjline += " --disable-feature=8";
        }

        if (preset.SmuFeaturesSettings.SustainVrmTdcCurrent)
        {
            adjline += " --enable-feature=16";
        }
        else
        {
            adjline += " --disable-feature=16";
        }

        if (preset.SmuFeaturesSettings.TemperatureControl)
        {
            adjline += " --enable-feature=32";
        }
        else
        {
            adjline += " --disable-feature=32";
        }

        if (preset.SmuFeaturesSettings.DpmFrequencyPowerDown)
        {
            adjline += " --enable-feature=256";
        }
        else
        {
            adjline += " --disable-feature=256";
        }

        if (preset.SmuFeaturesSettings.ProchotSignal)
        {
            adjline += " --enable-feature=0,32";
        }
        else
        {
            adjline += " --disable-feature=0,32";
        }

        if (preset.SmuFeaturesSettings.SustainedPowerLimit)
        {
            adjline += " --enable-feature=0,128";
        }
        else
        {
            adjline += " --disable-feature=0,128";
        }

        if (preset.SmuFeaturesSettings.CStatesBoost)
        {
            adjline += " --enable-feature=0,256";
        }
        else
        {
            adjline += " --disable-feature=0,256";
        }

        if (preset.SmuFeaturesSettings.GraphicsDutyCycle)
        {
            adjline += " --enable-feature=0,512";
        }
        else
        {
            adjline += " --disable-feature=0,512";
        }

        if (preset.SmuFeaturesSettings.AplusAPowerMode)
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