using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Models;
using Saku_Overclock.Services;
using Saku_Overclock.Styles;
using Saku_Overclock.ViewModels;
using Windows.Foundation.Metadata;
using Windows.UI.Text;
using Task = System.Threading.Tasks.Task;

namespace Saku_Overclock.Views;

public sealed partial class ПресетыPage
{
    private static readonly IAppSettingsService AppSettings = App.GetService<IAppSettingsService>();
    private static readonly IApplyerService Applyer = App.GetService<IApplyerService>();
    private static readonly IOcFinderService OcFinder = App.GetService<IOcFinderService>();
    private static readonly IPresetManagerService PresetManager = App.GetService<IPresetManagerService>();
    private bool _isLoaded; // Загружена ли корректно страница для применения изменений 
    private bool _presetChanging = true; // Ожидание окончательной смены пресета на другой. Активируется при смене пресета 
    private int _presetIndex; // Выбранный пресет
    private readonly IBackgroundDataUpdater _dataUpdater = App.GetService<IBackgroundDataUpdater>();

    private static readonly IAppNotificationService
        NotificationsService = App.GetService<IAppNotificationService>(); // Уведомления приложения

    private static readonly ICpuService Cpu = App.GetService<ICpuService>();
    private static bool? _isPlatformPc = false;
    private string _doubleClickApplyToken = string.Empty;

    public ПресетыPage()
    {
        InitializeComponent();

        PresetManager.LoadSettings();
        AppSettings.SaveSettings();

        _dataUpdater.DataUpdated += OnDataUpdated;

        Unloaded += ПресетыPage_Unloaded;
        Loaded += ПресетыPage_Loaded;
    }

    private void ПресетыPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _dataUpdater.DataUpdated -= OnDataUpdated;

        Unloaded -= ПресетыPage_Unloaded;
        Loaded -= ПресетыPage_Loaded;
    }

    private void ПресетыPage_Loaded(object sender, RoutedEventArgs e)
    {
        _presetChanging = false;
        SelectedPresetDescription.Text = "Preset_Min_Desc/Text".GetLocalized();

        try
        {
            _isPlatformPc = Cpu.IsPlatformPc();
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex);
        }

        LoadPresets();

        // Загрузить остальные UI элементы, функции блока "Дополнительно"
        try
        {
            AutostartCom.SelectedIndex = AppSettings.AutostartType;
        }
        catch
        {
            AutostartCom.SelectedIndex = 0;
        }

        ReapplyOptionsSetOnly(AppSettings.ReapplyOverclock ? ReapplyOptionsEnabled : ReapplyOptionsDisabled);
        AutoApplyOptionsSetOnly(AppSettings.ReapplyLatestSettingsOnAppLaunch
            ? AutoApplyOptionsEnabled
            : AutoApplyOptionsDisabled);
        TrayMonSetOnly(AppSettings.NiIconsEnabled ? TrayMonFeatEnabled : TrayMonFeatDisabled);
        RtssOverlaySetOnly(AppSettings.RtssMetricsEnabled ? RtssOverlayEnabled : RtssOverlayDisabled);

        
        // TODO: FIX PREMADE PRESETS
        /*PremadeOptimizationLevel.SelectedIndex = AppSettings.PremadeOptimizationLevel;

        if (OcFinder.IsUndervoltingAvailable() && AppSettings.PremadeOptimizationLevel == 2)
        {
            PremadeCurveOptimizerStackPanel.Visibility = Visibility.Visible;
        }
        else
        {
            PremadeCurveOptimizerStackPanel.Visibility = Visibility.Collapsed;
        }

        CurveOptimizerLevelSlider.Value = AppSettings.PremadeCurveOptimizerOverrideLevel;

        PremadeOptimizationLevelDesc.Text = PremadeOptimizationLevel.SelectedIndex switch
        {
            0 => "Preset_OptimizationLevel_Base_Desc".GetLocalized(),
            1 => "Preset_OptimizationLevel_Average_Desc".GetLocalized(),
            2 => "Preset_OptimizationLevel_Strong_Desc".GetLocalized(),
            _ => "Preset_OptimizationLevel_Base_Desc".GetLocalized()
        };*/

        CurveOptimizerCustomGrid.Visibility =
            OcFinder.IsUndervoltingAvailable() ? Visibility.Visible : Visibility.Collapsed;
        if (CurveOptimizerCustomGrid.Visibility == Visibility.Collapsed)
        {
            CurveOptimizerCustom.IsOn = false;
        }

        if (AppSettings.PresetsPageViewModeBeginner)
        {
            BeginnerOptionsButton.IsChecked = true;
            AdvancedOptionsButton.IsChecked = false;
        }
        else
        {
            BeginnerOptionsButton.IsChecked = false;
            AdvancedOptionsButton.IsChecked = true;
        }

        ToolTipService.SetToolTip(AdvancedOptionsButton, "Param_ProMode".GetLocalized());
        ToolTipService.SetToolTip(BeginnerOptionsButton, "Param_NewbieMode".GetLocalized());

        _isLoaded = true;

        if (!IsRavenFamily() && SettingsViewModel.VersionId != 5)
        {
            AdvancedGpuOptionsPanelGrid0.Visibility = Visibility.Collapsed;
            AdvancedGpuOptionsPanelGrid1.Visibility = Visibility.Collapsed;
            AdvancedGpuOptionsPanel0.Visibility = Visibility.Collapsed;
            AdvancedGpuOptionsPanel1.Height = new GridLength(0);
            AdvancedGpuOptionsPanel2.Visibility = Visibility.Collapsed;
            AdvancedGpuOptionsPanel3.Height = new GridLength(0);
        }
    }

    #region JSON and Initialization

    #region Initialization

    private void LoadPresets()
    {
        // Загрузить пресеты перед началом работы с ними
        PresetManager.LoadSettings();

        // Очистить элементы PresetsControl
        PresetsControl.Items.Clear();

        // Пройтись по каждому пресету и добавить их в PresetsControl
        var isOneSelected = false;
        for (var i = 0; i < PresetManager.Presets.Length; i++)
        {
            var preset = PresetManager.Presets[i];
            var isChecked = AppSettings.Preset != -1 && AppSettings.Preset == i &&
                            PresetManager.Presets[AppSettings.Preset].PresetName == preset.PresetName &&
                            PresetManager.Presets[AppSettings.Preset].PresetDesc == preset.PresetDesc &&
                            PresetManager.Presets[AppSettings.Preset].PresetIcon == preset.PresetIcon;

            if (isChecked)
            {
                isOneSelected = true;
            }
            
            
            var presetName = preset.PresetName;
            var presetDesc = preset.PresetDesc;
            if (presetName.Contains("Preset_")) { presetName = ГлавнаяPage.TryLocalize(presetName); }
            if (presetDesc.Contains("Preset_")) { presetDesc = ГлавнаяPage.TryLocalize(presetDesc); }

            var toggleButton = new PresetItem
            {
                IsSelected = isChecked,
                IconGlyph = preset.PresetIcon == string.Empty ? "\uE718" : preset.PresetIcon,
                Text = presetName,
                Description = presetDesc != string.Empty ? presetDesc : presetName
            };
            PresetsControl.Items.Add(toggleButton);
        }

        if ((PresetManager.Presets.Length == 0 && AppSettings.Preset != -1) || !isOneSelected)
        {
            AppSettings.Preset = -1;
            AppSettings.SaveSettings();
        }

        
        // TODO: FIX PREMADE PRESETS
        // Готовые Пресеты
        /*PresetsControl.Items.Add(new PresetItem
        {
            IsSelected = AppSettings is { Preset: -1, PremadeMaxActivated: true },
            IconGlyph = "\uEcad",
            Text = "Preset_Max_Name/Text".GetLocalized(), // Maximum
            Description = "Preset_Max_Desc/Text".GetLocalized()
        });
        PresetsControl.Items.Add(new PresetItem
        {
            IsSelected = AppSettings is { Preset: -1, PremadeSpeedActivated: true },
            IconGlyph = "\ue945",
            Text = "Preset_Speed_Name/Text".GetLocalized(), // Speed
            Description = "Preset_Speed_Desc/Text".GetLocalized()
        });
        PresetsControl.Items.Add(new PresetItem
        {
            IsSelected = AppSettings is { Preset: -1, PremadeBalanceActivated: true },
            IconGlyph = "\uec49",
            Text = "Preset_Balance_Name/Text".GetLocalized(), // Balance
            Description = "Preset_Balance_Desc/Text".GetLocalized()
        });
        PresetsControl.Items.Add(new PresetItem
        {
            IsSelected = AppSettings is { Preset: -1, PremadeEcoActivated: true },
            IconGlyph = "\uec0a",
            Text = "Preset_Eco_Name/Text".GetLocalized(), // Eco
            Description = "Preset_Eco_Desc/Text".GetLocalized()
        });
        PresetsControl.Items.Add(new PresetItem
        {
            IsSelected = AppSettings is { Preset: -1, PremadeMinActivated: true },
            IconGlyph = "\uebc0",
            Text = "Preset_Min_Name/Text".GetLocalized(), // Minimum
            Description = "Preset_Min_Desc/Text".GetLocalized()
        });*/

        // Workaround чтобы все элементы корректно загрузились в PresetsControl
        PresetsControl.UpdateView();

        foreach (var item in PresetsControl.Items)
        {
            if (item.IsSelected)
            {
                SelectedPresetName.Text = item.Text;

                // Обход отсутствия описания, при помощи записывания имени пресета в описание. Чтобы не отображать два раза одну и ту же строку, описание пресета скрывается (так как его нет)
                SelectedPresetDescription.Text = item.Description != item.Text ? item.Description : string.Empty;
                if (item.Description == item.Text)
                {
                    SelectedPresetDescription.Visibility = Visibility.Collapsed;
                    EditCurrentButtonsStackPanel.Margin = new Thickness(0, 0, -13, -10);
                    EditCurrentButtonsStackPanel.VerticalAlignment = VerticalAlignment.Top;
                    SelectedPresetTextsStackPanel.VerticalAlignment = VerticalAlignment.Center;
                }
                else
                {
                    SelectedPresetDescription.Visibility = Visibility.Visible;
                    EditCurrentButtonsStackPanel.Margin = new Thickness(0, 17, -13, -10);
                    EditCurrentButtonsStackPanel.VerticalAlignment = VerticalAlignment.Center;
                    SelectedPresetTextsStackPanel.VerticalAlignment = VerticalAlignment.Top;
                }

                if ((item.Text == "Preset_Max_Name/Text".GetLocalized() &&
                     item.Description == "Preset_Max_Desc/Text".GetLocalized()) ||
                    (item.Text == "Preset_Speed_Name/Text".GetLocalized() &&
                     item.Description == "Preset_Speed_Desc/Text".GetLocalized()) ||
                    (item.Text == "Preset_Balance_Name/Text".GetLocalized() &&
                     item.Description == "Preset_Balance_Desc/Text".GetLocalized()) ||
                    (item.Text == "Preset_Eco_Name/Text".GetLocalized() &&
                     item.Description == "Preset_Eco_Desc/Text".GetLocalized()) ||
                    (item.Text == "Preset_Min_Name/Text".GetLocalized() &&
                     item.Description == "Preset_Min_Desc/Text".GetLocalized())
                   )
                {
                    PresetSettingsStackPanel.Visibility = Visibility.Collapsed;
                    PresetSettingsBeginnerView.Visibility = Visibility.Visible;
                    PresetSettingsBeginnerView.Margin = new Thickness(0, -5, 0, 0);
                    PremadePresetAffectsOn.Visibility = Visibility.Visible;
                    PresetSettingsBeginnerViewSettingsStackPanel.Visibility = Visibility.Collapsed;
                    EditPresetButton.Visibility = Visibility.Collapsed;
                    PresetSettingsChangeViewStackPanel.Visibility = Visibility.Collapsed;

                    var optimizationLevel = PremadeOptimizationLevel.SelectedIndex switch
                    {
                        0 => OptimizationLevel.Basic,
                        1 => OptimizationLevel.Standard,
                        2 => OptimizationLevel.Deep,
                        _ => OptimizationLevel.Basic
                    };

                    PresetConfiguration presetConfiguration;

                    if (item.Text == "Preset_Max_Name/Text".GetLocalized())
                    {
                        presetConfiguration = OcFinder.CreatePreset(PresetType.Max, optimizationLevel);
                        HelpWithShowPreset(PresetType.Max);
                    }
                    else if (item.Text == "Preset_Speed_Name/Text".GetLocalized())
                    {
                        presetConfiguration = OcFinder.CreatePreset(PresetType.Speed, optimizationLevel);
                        HelpWithShowPreset(PresetType.Speed);
                    }
                    else if (item.Text == "Preset_Balance_Name/Text".GetLocalized())
                    {
                        presetConfiguration = OcFinder.CreatePreset(PresetType.Balance, optimizationLevel);
                        HelpWithShowPreset(PresetType.Balance);
                    }
                    else if (item.Text == "Preset_Eco_Name/Text".GetLocalized())
                    {
                        presetConfiguration = OcFinder.CreatePreset(PresetType.Eco, optimizationLevel);
                        HelpWithShowPreset(PresetType.Eco);
                    }
                    else
                    {
                        presetConfiguration = OcFinder.CreatePreset(PresetType.Min, optimizationLevel);
                        HelpWithShowPreset(PresetType.Min);
                    }

                    PresetPerformanceBar.Value = 50 + presetConfiguration.Metrics.PerformanceScore;
                    PresetEnergyEfficiencyBar.Value = 50 + presetConfiguration.Metrics.EfficiencyScore;
                    PresetTemperaturesBar.Value = 50 + presetConfiguration.Metrics.ThermalScore;
                }
                else
                {
                    PresetSettingsBeginnerViewSettingsStackPanel.Visibility = Visibility.Visible;
                    PresetSettingsBeginnerView.Margin = new Thickness(0, 0, 0, 0);
                    if (AppSettings.PresetsPageViewModeBeginner)
                    {
                        BeginnerOptionsButton.IsChecked = true;
                        AdvancedOptionsButton.IsChecked = false;
                        PresetSettingsStackPanel.Visibility = Visibility.Collapsed;
                        PresetSettingsChangeViewStackPanel.Visibility = Visibility.Visible;
                        PresetSettingsBeginnerView.Visibility = Visibility.Visible;
                        PremadePresetAffectsOn.Visibility = Visibility.Collapsed;
                        EditPresetButton.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        BeginnerOptionsButton.IsChecked = false;
                        AdvancedOptionsButton.IsChecked = true;
                        PresetSettingsStackPanel.Visibility = Visibility.Visible;
                        PresetSettingsBeginnerView.Visibility = Visibility.Collapsed;
                        PresetSettingsChangeViewStackPanel.Visibility = Visibility.Visible;
                        PremadePresetAffectsOn.Visibility = Visibility.Collapsed;
                        EditPresetButton.Visibility = Visibility.Visible;
                    }
                }
            }
        }

        if (AppSettings.Preset != -1)
        {
            InitializeCustomPresetSettings(AppSettings.Preset);
        }
    }

    private void HelpWithShowPreset(PresetType type)
    {
        if (!_isLoaded)
        {
            return;
        }

        PresetHwGaming.Visibility = Visibility.Collapsed;
        PresetHwStreaming.Visibility = Visibility.Collapsed;
        PresetHwHardWorkload.Visibility = Visibility.Collapsed;
        PresetHwOfficeWorkload.Visibility = Visibility.Collapsed;
        PresetHwLightGaming.Visibility = Visibility.Collapsed;
        PresetHwVideos.Visibility = Visibility.Collapsed;
        var presetHwColumnHelper1 = GridLength.Auto;
        var presetHwColumnHelper2 = GridLength.Auto;
        switch (type)
        {
            case PresetType.Min:
                PresetHwOfficeWorkload.Visibility = Visibility.Visible;
                PresetHwVideos.Visibility = Visibility.Visible;
                presetHwColumnHelper2 = new GridLength(0);
                break;
            case PresetType.Eco:
                PresetHwOfficeWorkload.Visibility = Visibility.Visible;
                PresetHwVideos.Visibility = Visibility.Visible;
                PresetHwLightGaming.Visibility = Visibility.Visible;
                break;
            case PresetType.Balance:
                PresetHwGaming.Visibility = Visibility.Visible;
                PresetHwOfficeWorkload.Visibility = Visibility.Visible;
                PresetHwVideos.Visibility = Visibility.Visible;
                PresetHwStreaming.Visibility = Visibility.Visible;
                presetHwColumnHelper1 = new GridLength(0);
                presetHwColumnHelper2 = new GridLength(0);
                break;
            case PresetType.Speed:
            case PresetType.Max:
                PresetHwGaming.Visibility = Visibility.Visible;
                PresetHwStreaming.Visibility = Visibility.Visible;
                PresetHwOfficeWorkload.Visibility = Visibility.Visible;
                PresetHwVideos.Visibility = Visibility.Visible;
                PresetHwHardWorkload.Visibility = Visibility.Visible;
                break;
        }
        PresetHwColumnHelper1.Width = presetHwColumnHelper1;
        PresetHwColumnHelper2.Width = presetHwColumnHelper2;
    }

    private void InitializeCustomPresetSettings(int index)
    {
        _presetChanging = true;
        if (index > PresetManager.Presets.Length || index < 0)
        {
            _presetChanging = false;
            return;
        }

        try
        {
            TrySetMaximum(PresetManager.Presets[index].CpuSettings.CpuMaximumTemperature.Value, C1V);
            TrySetMaximum(PresetManager.Presets[index].CpuSettings.CpuSustainedPowerLimit.Value, C2V);
            TrySetMaximum(PresetManager.Presets[index].CpuSettings.CpuActualPowerLimit.Value, C3V);
            TrySetMaximum(PresetManager.Presets[index].CpuSettings.CpuAveragePowerLimit.Value, C4V);
            TrySetMaximum(PresetManager.Presets[index].CpuSettings.CpuBoostTimeSlow.Value, C5V);
            TrySetMaximum(PresetManager.Presets[index].CpuSettings.CpuBoostTimeFast.Value, C6V);

            TrySetMaximum(PresetManager.Presets[index].VrmSettings.VrmCpuEdcCurrentLimit.Value, V1V);
            TrySetMaximum(PresetManager.Presets[index].VrmSettings.VrmCpuTdcCurrentLimit.Value, V2V);
            TrySetMaximum(PresetManager.Presets[index].VrmSettings.VrmSocEdcCurrentLimit.Value, V3V);
            TrySetMaximum(PresetManager.Presets[index].VrmSettings.VrmSocTdcCurrentLimit.Value, V4V);

            TrySetMaximum(PresetManager.Presets[index].SubsystemsSettings.MinimumIntegratedGraphicsFrequency.Value, G9V);
            TrySetMaximum(PresetManager.Presets[index].SubsystemsSettings.MaximumIntegratedGraphicsFrequency.Value, G10V);
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex);
        }

        try
        {
            // Заранее скомпилированная функция увеличения TDP, созданная специально для фирменной функции Smart TDP
            var fineTunedTdp = ПараметрыPage.FromValueToUpperFive(1.17335141 * PresetManager.Presets[index].CpuSettings.CpuSustainedPowerLimit.Value + 0.21631949);

            LoadUiOption(PresetManager.Presets[index].CpuSettings.CpuSustainedPowerLimit, C2, C2V);

            BaseTdpSlider.Value = PresetManager.Presets[index].CpuSettings.CpuSustainedPowerLimit.Value;
            if (PresetManager.Presets[index].CpuSettings.CpuSustainedPowerLimit.IsEnabled && 
                PresetManager.Presets[index].CpuSettings.CpuActualPowerLimit.IsEnabled && 
                PresetManager.Presets[index].CpuSettings.CpuAveragePowerLimit.IsEnabled &&
                (int)PresetManager.Presets[index].CpuSettings.CpuSustainedPowerLimit.Value == 
                (int)PresetManager.Presets[index].CpuSettings.CpuAveragePowerLimit.Value &&
                (int)PresetManager.Presets[index].CpuSettings.CpuActualPowerLimit.Value == fineTunedTdp)
            {
                SmartTdp.IsOn = true;
            }
            else
            {
                if (_isPlatformPc != false && SettingsViewModel.VersionId != 5) // Если устройство - не ноутбук
                {
                    // Так как на компьютерах невозможно выставить другие Power лимиты
                    if (!PresetManager.Presets[index].CpuSettings.CpuSustainedPowerLimit.IsEnabled && 
                        !PresetManager.Presets[index].CpuSettings.CpuAveragePowerLimit.IsEnabled &&
                        (int)PresetManager.Presets[index].CpuSettings.CpuActualPowerLimit.Value == fineTunedTdp)
                    {
                        SmartTdp.IsOn = true;
                    }
                }
                else
                {
                    SmartTdp.IsOn = false;
                }
            }
            
            LoadUiOption(PresetManager.Presets[index].CpuSettings.CpuMaximumTemperature, C1, C1V);
            LoadUiOption(PresetManager.Presets[index].CpuSettings.CpuActualPowerLimit, C3, C3V);
            LoadUiOption(PresetManager.Presets[index].CpuSettings.CpuAveragePowerLimit, C4, C4V);
            LoadUiOption(PresetManager.Presets[index].CpuSettings.CpuBoostTimeSlow, C5, C5V);
            LoadUiOption(PresetManager.Presets[index].CpuSettings.CpuBoostTimeFast, C6, C6V);


            if (IsRavenFamily())
            {
                if (PresetManager.Presets[index].SubsystemsSettings.MaximumIntegratedGraphicsFrequency.IsEnabled && 
                    PresetManager.Presets[index].SubsystemsSettings.MinimumIntegratedGraphicsFrequency.IsEnabled && 
                    (int)PresetManager.Presets[index].SubsystemsSettings.MaximumIntegratedGraphicsFrequency.Value == 1200)
                {
                    if ((int)PresetManager.Presets[index].SubsystemsSettings.MinimumIntegratedGraphicsFrequency.Value == 800)
                    {
                        IntegratedGpuEnchantmentCombo.SelectedIndex = 1;
                    }

                    if ((int)PresetManager.Presets[index].SubsystemsSettings.MinimumIntegratedGraphicsFrequency.Value == 1000)
                    {
                        IntegratedGpuEnchantmentCombo.SelectedIndex = 2;
                    }
                }
                else
                {
                    IntegratedGpuEnchantmentCombo.SelectedIndex = 0;
                }
            }
            else
            {
                if (PresetManager.Presets[index].FrequenciesSettings.IntegratedGraphicsFrequency.IsEnabled)
                {
                    if ((int)PresetManager.Presets[index].FrequenciesSettings.IntegratedGraphicsFrequency.Value == 1750)
                    {
                        IntegratedGpuEnchantmentCombo.SelectedIndex = 1;
                    }

                    if ((int)PresetManager.Presets[index].FrequenciesSettings.IntegratedGraphicsFrequency.Value == 2200)
                    {
                        IntegratedGpuEnchantmentCombo.SelectedIndex = 2;
                    }
                }
                else
                {
                    IntegratedGpuEnchantmentCombo.SelectedIndex = 0;
                }
            }

            if (!PresetManager.Presets[index].CpuSettings.CpuBoostTimeSlow.IsEnabled && 
                !PresetManager.Presets[index].CpuSettings.CpuBoostTimeFast.IsEnabled)
            {
                TurboSetOnly(TurboLightModeToggle);
            }
            else
            {
                if ((PresetManager.Presets[index].CpuSettings.CpuBoostTimeSlow.IsEnabled && 
                     !PresetManager.Presets[index].CpuSettings.CpuBoostTimeFast.IsEnabled) || 
                    (!PresetManager.Presets[index].CpuSettings.CpuBoostTimeSlow.IsEnabled && 
                     PresetManager.Presets[index].CpuSettings.CpuBoostTimeFast.IsEnabled))
                {
                    TurboSetOnly(TurboLightModeToggle);
                }

                if (PresetManager.Presets[index].CpuSettings.CpuBoostTimeSlow.IsEnabled &&
                    PresetManager.Presets[index].CpuSettings.CpuBoostTimeFast.IsEnabled)
                {
                    if ((int)PresetManager.Presets[index].CpuSettings.CpuBoostTimeSlow.Value == 400 && 
                        (int)PresetManager.Presets[index].CpuSettings.CpuBoostTimeFast.Value == 3)
                    {
                        TurboSetOnly(TurboBalanceModeToggle);
                    }
                    else if ((int)PresetManager.Presets[index].CpuSettings.CpuBoostTimeSlow.Value == 5000 && 
                             (int)PresetManager.Presets[index].CpuSettings.CpuBoostTimeFast.Value == 1)
                    {
                        TurboSetOnly(TurboHeavyModeToggle);
                    }
                    else
                    {
                        TurboSetOnly(TurboLightModeToggle);
                    }
                }
                else
                {
                    TurboSetOnly(TurboLightModeToggle);
                }
            }

            if (PresetManager.Presets[index].CurveOptimizerOptions.CpuCurveOptimizerUndervoltingLevel.IsEnabled)
            {
                CurveOptimizerCustom.IsOn = true;
                CurveOptimizerLevelCustomSlider.Value = PresetManager.Presets[index].CurveOptimizerOptions
                    .CpuCurveOptimizerUndervoltingLevel.Value;
            }
            else
            {
                CurveOptimizerCustom.IsOn = false;
            }

            
            LoadUiOption(PresetManager.Presets[index].VrmSettings.VrmCpuEdcCurrentLimit, V1, V1V);
            LoadUiOption(PresetManager.Presets[index].VrmSettings.VrmCpuTdcCurrentLimit, V2, V2V);
            LoadUiOption(PresetManager.Presets[index].VrmSettings.VrmSocEdcCurrentLimit, V3, V3V);
            LoadUiOption(PresetManager.Presets[index].VrmSettings.VrmSocTdcCurrentLimit, V4, V4V);
            LoadUiOption(PresetManager.Presets[index].SubsystemsSettings.MinimumIntegratedGraphicsFrequency, G9, G9V);
            LoadUiOption(PresetManager.Presets[index].SubsystemsSettings.MaximumIntegratedGraphicsFrequency, G10, G10V);
        }
        catch
        {
            LogHelper.LogError("Preset contains error. Creating new preset.");

            PresetManager.Presets = new Preset[1];
            PresetManager.Presets[0] = new Preset();
            PresetManager.SaveSettings();
        }

        _presetChanging = false;
    }
    
    private static void TrySetMaximum(double maximum, Slider slider)
    {
        if (maximum > slider.Maximum)
        {
            slider.Maximum = ПараметрыPage.FromValueToUpperFive(maximum);
        }
    }
    
    private static void LoadUiOption(PresetOption<double> option, CheckBox check, Slider slider)
    {
        check.IsChecked = option.IsEnabled;
        slider.Value = option.Value;
    }

    private void TurboSetOnly(ToggleButton button)
    {
        TurboLightModeToggle.IsChecked = false;
        TurboBalanceModeToggle.IsChecked = false;
        TurboHeavyModeToggle.IsChecked = false;
       
        button.IsChecked = true;
    }

    private void ReapplyOptionsSetOnly(ToggleButton button)
    {
        ReapplyOptionsDisabled.IsChecked = false;
        ReapplyOptionsEnabled.IsChecked = false;
        
        button.IsChecked = true;
    }

    private void RtssOverlaySetOnly(ToggleButton button)
    {
        RtssOverlayDisabled.IsChecked = false;
        RtssOverlayEnabled.IsChecked = false;

        button.IsChecked = true;
    }

    private void TrayMonSetOnly(ToggleButton button)
    {
        TrayMonFeatDisabled.IsChecked = false;
        TrayMonFeatEnabled.IsChecked = false;

        button.IsChecked = true;
    }

    private void AutoApplyOptionsSetOnly(ToggleButton button)
    {
        AutoApplyOptionsDisabled.IsChecked = false;
        AutoApplyOptionsEnabled.IsChecked = false;

        button.IsChecked = true;
    }

    private void OnDataUpdated(object? sender, SensorsInformation info)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            TdpLimitSensorText.Text = $"{info.CpuFastLimit:F0}W";
            TdpValueSensorText.Text = $"{info.CpuFastValue:F0}";
            CpuFreqSensorText.Text = $"{info.CpuFrequency:F1}";

            var updateSmallSign = true;

            if (info.ApuTempValue == 0)
            {
                updateSmallSign = false;
                TempSensorsStackPanel.Margin = new Thickness(7, 0, 0, 5);
                TempSensorsStackPanel.VerticalAlignment = VerticalAlignment.Bottom;
                GpuTempSensorStackPanel.Visibility = Visibility.Collapsed;
                CpuTempSensorCaptionText.Visibility = Visibility.Collapsed;
                CpuTempSensorBigCaptionText.Visibility = Visibility.Visible;
                CpuTempSensorText.FontSize = 38;
                CpuTempSensorText.Margin = new Thickness(4, -8, 0, 0);
                CpuTempSensorText.FontWeight = new FontWeight(700);
            }
            else
            {
                GpuTempSensorText.Text = Math.Round(info.ApuTempValue) + "C";
            }

            CpuTempSensorText.Text = Math.Round(info.CpuTempValue) + (updateSmallSign ? "C" : string.Empty);
        });
    }

    #endregion

    #endregion

    #region Event Handlers

    #region Additional Functions

    private void ReapplyOptions_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var toggle = sender as ToggleButton;
        if (toggle == null)
        {
            return;
        }
        
        if (ReapplyOptionsDisabled.IsChecked == false
            && ReapplyOptionsEnabled.IsChecked == false)
        {
            toggle.IsChecked = true;
        }
        else
        {
            if (toggle.Name == "ReapplyOptionsDisabled")
            {
                ReapplyOptionsSetOnly(ReapplyOptionsDisabled);
                AppSettings.ReapplyOverclock = false;
            }
            else if (toggle.Name == "ReapplyOptionsEnabled")
            {
                ReapplyOptionsSetOnly(ReapplyOptionsEnabled);
                AppSettings.ReapplyOverclock = true;
            }
        }

        AppSettings.SaveSettings();
    }

    private void RtssOverlaySwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var toggle = sender as ToggleButton;
        if (RtssOverlayDisabled.IsChecked == false
            && RtssOverlayEnabled.IsChecked == false)
        {
            toggle!.IsChecked = true;
        }
        else
        {
            if (toggle!.Name == "RtssOverlayDisabled")
            {
                RtssOverlaySetOnly(RtssOverlayDisabled);
                AppSettings.RtssMetricsEnabled = false;
            }
            else if (toggle.Name == "RtssOverlayEnabled")
            {
                RtssOverlaySetOnly(RtssOverlayEnabled);
                AppSettings.RtssMetricsEnabled = true;
            }
        }

        AppSettings.SaveSettings();
    }

    private void TrayMonFeatSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var toggle = sender as ToggleButton;
        if (TrayMonFeatDisabled.IsChecked == false
            && TrayMonFeatEnabled.IsChecked == false)
        {
            toggle!.IsChecked = true;
        }
        else
        {
            if (toggle!.Name == "TrayMonFeatDisabled")
            {
                TrayMonSetOnly(TrayMonFeatDisabled);
                AppSettings.NiIconsEnabled = false;
            }
            else if (toggle.Name == "TrayMonFeatEnabled")
            {
                TrayMonSetOnly(TrayMonFeatEnabled);
                AppSettings.NiIconsEnabled = true;
            }
        }

        AppSettings.SaveSettings();
    }

    private void AutoApplyOptions_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var toggle = sender as ToggleButton;
        if (AutoApplyOptionsDisabled.IsChecked == false
            && AutoApplyOptionsEnabled.IsChecked == false)
        {
            toggle!.IsChecked = true;
        }
        else
        {
            if (toggle!.Name == "AutoApplyOptionsDisabled")
            {
                AutoApplyOptionsSetOnly(AutoApplyOptionsDisabled);
                AppSettings.ReapplyLatestSettingsOnAppLaunch = false;
            }
            else if (toggle.Name == "AutoApplyOptionsEnabled")
            {
                AutoApplyOptionsSetOnly(AutoApplyOptionsEnabled);
                AppSettings.ReapplyLatestSettingsOnAppLaunch = true;
            }
        }

        AppSettings.SaveSettings();
    }

    private void AutostartCom_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        AppSettings.AutostartType = AutostartCom.SelectedIndex;
        if (AutostartCom.SelectedIndex is 1 or 2)
        {
            AutoStartHelper.SetStartupTask();
        }
        else
        {
            AutoStartHelper.RemoveStartupTask();
        }

        AppSettings.SaveSettings();
    }

    private void AnimatedToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (AppSettings.PresetsPageViewModeBeginner && BeginnerOptionsButton.IsChecked == false)
        {
            BeginnerOptionsButton.IsChecked = true;
            return;
        }
        
        if (!AppSettings.PresetsPageViewModeBeginner && AdvancedOptionsButton.IsChecked == false)
        {
            AdvancedOptionsButton.IsChecked = true;
            return;
        }
        
        AppSettings.PresetsPageViewModeBeginner = !AppSettings.PresetsPageViewModeBeginner;
        AppSettings.SaveSettings();
        if (AppSettings.PresetsPageViewModeBeginner)
        {
            BeginnerOptionsButton.IsChecked = true;
            AdvancedOptionsButton.IsChecked = false;
            PresetSettingsStackPanel.Visibility = Visibility.Collapsed;
            PresetSettingsBeginnerView.Visibility = Visibility.Visible;
            PremadePresetAffectsOn.Visibility = Visibility.Collapsed;
            EditPresetButton.Visibility = Visibility.Visible;
            PresetSettingsChangeViewStackPanel.Visibility = Visibility.Visible;
        }
        else
        {
            BeginnerOptionsButton.IsChecked = false;
            AdvancedOptionsButton.IsChecked = true;
            PresetSettingsStackPanel.Visibility = Visibility.Visible;
            PresetSettingsBeginnerView.Visibility = Visibility.Collapsed;
            PremadePresetAffectsOn.Visibility = Visibility.Collapsed;
            EditPresetButton.Visibility = Visibility.Visible;
            PresetSettingsChangeViewStackPanel.Visibility = Visibility.Visible;
        }
    }

    private void BaseTdp_Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        ChangedBaseTdp_Value();
    }

    private void SmartTdp_Toggled(object sender, RoutedEventArgs e) => ChangedBaseTdp_Value();

    private void ChangedBaseTdp_Value()
    {
        if (_presetChanging)
        {
            return;
        }

        var index = _presetIndex == -1 ? 0 : _presetIndex;

        // Заранее скомпилированная функция увеличения TDP, созданная специально для фирменной функции Smart TDP
        var fineTunedTdp = ПараметрыPage.FromValueToUpperFive(1.17335141 * BaseTdpSlider.Value + 0.21631949);

        C2.IsChecked = true;
        PresetManager.Presets[index].CpuSettings.CpuSustainedPowerLimit.IsEnabled = true;
        C3.IsChecked = true;
        PresetManager.Presets[index].CpuSettings.CpuActualPowerLimit.IsEnabled = true;
        C4.IsChecked = true;
        PresetManager.Presets[index].CpuSettings.CpuAveragePowerLimit.IsEnabled = true;

        if (fineTunedTdp > C3V.Maximum || BaseTdpSlider.Value > C2V.Maximum || BaseTdpSlider.Value > C4V.Maximum)
        {
            C2V.Maximum = ПараметрыPage.FromValueToUpperFive(BaseTdpSlider.Value);
            C3V.Maximum = ПараметрыPage.FromValueToUpperFive(fineTunedTdp);
            C4V.Maximum = ПараметрыPage.FromValueToUpperFive(BaseTdpSlider.Value);
        }

        C2V.Value = BaseTdpSlider.Value;
        PresetManager.Presets[index].CpuSettings.CpuSustainedPowerLimit.Value = BaseTdpSlider.Value;

        C4V.Value = BaseTdpSlider.Value;
        PresetManager.Presets[index].CpuSettings.CpuAveragePowerLimit.Value = BaseTdpSlider.Value;

        if (SmartTdp.IsOn)
        {
            C3V.Value = fineTunedTdp;
            PresetManager.Presets[index].CpuSettings.CpuActualPowerLimit.Value = fineTunedTdp;
        }
        else
        {
            C3V.Value = BaseTdpSlider.Value;
            PresetManager.Presets[index].CpuSettings.CpuActualPowerLimit.Value = BaseTdpSlider.Value;
        }

        if (_isPlatformPc != false && SettingsViewModel.VersionId != 5) // Если устройство - не ноутбук
        {
            // Так как на компьютерах невозможно выставить другие Power лимиты
            C2.IsChecked = false; // Отключить STAPM
            PresetManager.Presets[index].CpuSettings.CpuSustainedPowerLimit.IsEnabled = false;
            C4.IsChecked = false; // Отключить Slow лимит
            PresetManager.Presets[index].CpuSettings.CpuAveragePowerLimit.IsEnabled = false;
        }

        PresetManager.SaveSettings();
    }

    // Grid-помощник, который активирует переключатель когда пользователь нажал на область возле него
    private void SmartTdp_PointerPressed(object sender, object e) => SmartTdp.IsOn = !SmartTdp.IsOn;

    // Выбора режима усиления Турбо-буста процессора: Авто, Умный, Сильный. Устанавливает параметры времени разгона процессора в зависимости от выбранной настройки
    private void Turbo_OtherModeToggle_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var toggle = sender as ToggleButton;
        if (TurboLightModeToggle.IsChecked == false
            && TurboBalanceModeToggle.IsChecked == false
            && TurboHeavyModeToggle.IsChecked == false)
        {
            toggle!.IsChecked = true;
        }
        else
        {
            if (AppSettings.Preset == -1)
            {
                return;
            }

            var index = AppSettings.Preset;

            

            if (toggle!.Name == "TurboLightModeToggle")
            {
                TurboSetOnly(TurboLightModeToggle);
                PresetManager.Presets[index].CpuSettings.CpuBoostTimeSlow.IsEnabled = false;
                PresetManager.Presets[index].CpuSettings.CpuBoostTimeFast.IsEnabled = false;
                C5.IsChecked = false;
                C6.IsChecked = false;
            }
            else if (toggle.Name == "TurboBalanceModeToggle")
            {
                TurboSetOnly(TurboBalanceModeToggle);
                PresetManager.Presets[index].CpuSettings.CpuBoostTimeSlow.IsEnabled = true;
                PresetManager.Presets[index].CpuSettings.CpuBoostTimeFast.IsEnabled = true;
                PresetManager.Presets[index].CpuSettings.CpuBoostTimeSlow.Value = 400;
                PresetManager.Presets[index].CpuSettings.CpuBoostTimeFast.Value = 3;
                C5.IsChecked = true;
                C6.IsChecked = true;
                C5V.Value = 400;
                C6V.Value = 3;
            }
            else if (toggle.Name == "TurboHeavyModeToggle")
            {
                TurboSetOnly(TurboHeavyModeToggle);
                PresetManager.Presets[index].CpuSettings.CpuBoostTimeSlow.IsEnabled = true;
                PresetManager.Presets[index].CpuSettings.CpuBoostTimeFast.IsEnabled = true;
                PresetManager.Presets[index].CpuSettings.CpuBoostTimeSlow.Value = 5000;
                PresetManager.Presets[index].CpuSettings.CpuBoostTimeFast.Value = 1;
                C5.IsChecked = true;
                C6.IsChecked = true;
                C5V.Maximum = 5000;
                C5V.Value = 5000;
                C6V.Value = 1;
            }
        }

        PresetManager.SaveSettings();
    }

    private void IntegratedGpuEnchantmentCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (AppSettings.Preset == -1)
        {
            return;
        }

        var index = AppSettings.Preset;

        switch (IntegratedGpuEnchantmentCombo.SelectedIndex)
        {
            case 0:
                if (IsRavenFamily())
                {
                    PresetManager.Presets[index].SubsystemsSettings.MaximumIntegratedGraphicsFrequency.IsEnabled = false;
                    PresetManager.Presets[index].SubsystemsSettings.MinimumIntegratedGraphicsFrequency.IsEnabled = false;
                }
                else
                {
                    PresetManager.Presets[index].FrequenciesSettings.IntegratedGraphicsFrequency.IsEnabled = false;
                }

                break;
            case 1:
                if (IsRavenFamily())
                {
                    PresetManager.Presets[index].SubsystemsSettings.MaximumIntegratedGraphicsFrequency.IsEnabled = true;
                    PresetManager.Presets[index].SubsystemsSettings.MaximumIntegratedGraphicsFrequency.Value = 1200;
                    PresetManager.Presets[index].SubsystemsSettings.MinimumIntegratedGraphicsFrequency.IsEnabled = true;
                    PresetManager.Presets[index].SubsystemsSettings.MinimumIntegratedGraphicsFrequency.Value = 800;
                }
                else
                {
                    PresetManager.Presets[index].FrequenciesSettings.IntegratedGraphicsFrequency.IsEnabled = true;
                    PresetManager.Presets[index].FrequenciesSettings.IntegratedGraphicsFrequency.Value = 1750;
                }

                break;
            case 2:
                if (IsRavenFamily())
                {
                    PresetManager.Presets[index].SubsystemsSettings.MaximumIntegratedGraphicsFrequency.IsEnabled = true;
                    PresetManager.Presets[index].SubsystemsSettings.MaximumIntegratedGraphicsFrequency.Value = 1200;
                    PresetManager.Presets[index].SubsystemsSettings.MinimumIntegratedGraphicsFrequency.IsEnabled = true;
                    PresetManager.Presets[index].SubsystemsSettings.MinimumIntegratedGraphicsFrequency.Value = 1000;
                }
                else
                {
                    PresetManager.Presets[index].FrequenciesSettings.IntegratedGraphicsFrequency.IsEnabled = true;
                    PresetManager.Presets[index].FrequenciesSettings.IntegratedGraphicsFrequency.Value = 2200;
                }

                break;
        }

        PresetManager.SaveSettings();
    }

    #region Function Helpers

    private bool IsRavenFamily() => Cpu.GetCodenameGeneration() == CpuService.CodenameGeneration.Fp5;

    private void TryAdvancedButton_Click(object sender, RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ПараметрыViewModel).FullName!);
    }

    #endregion

    #region Preset Management

    private async void AddPresetButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await OpenAddPresetDialogAsync();
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception);
        }
    }

    private async Task OpenAddPresetDialogAsync()
    {
        var selectedGlyph = "\uE718"; // Значок по умолчанию

        // Кнопка выбора иконки
        var glyphIcon = new FontIcon { Glyph = selectedGlyph };
        var iconButton = new Button
        {
            Height = 60,
            Width = 60,
            CornerRadius = new CornerRadius(16),
            Shadow = (ThemeShadow)Resources["SharedShadow"],
            Translation = new Vector3(0, 0, 14),
            Content = glyphIcon
        };

        // Поля ввода
        var nameBox = new TextBox
        {
            PlaceholderText = "Param_Preset_New_Name_Add/PlaceholderText".GetLocalized(),
            CornerRadius = new CornerRadius(9),
            Text = "Param_UnsignedPreset".GetLocalized(),
            Width = 250,
            Shadow = (ThemeShadow)Resources["SharedShadow"],
            Translation = new Vector3(0, 0, 10)
        };

        var descBox = new TextBox
        {
            PlaceholderText = "Param_Preset_New_Desc_Add/PlaceholderText".GetLocalized(),
            CornerRadius = new CornerRadius(9),
            Width = 250,
            Margin = new Thickness(0, 6, 0, 0),
            Shadow = (ThemeShadow)Resources["SharedShadow"],
            Translation = new Vector3(0, 0, 10)
        };

        // Стек с текстбоксами и кнопкой
        var fieldsPanel = new StackPanel
        {
            Margin = new Thickness(15, 0, 0, 0)
        };
        fieldsPanel.Children.Add(nameBox);
        fieldsPanel.Children.Add(descBox);

        // Основная горизонтальная панель
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 15
        };
        content.Children.Add(iconButton);
        content.Children.Add(fieldsPanel);

        // Создаём диалог
        var dialog = new ContentDialog
        {
            Title = "Param_Preset_New_Name/Content".GetLocalized(),
            XamlRoot = XamlRoot,
            CloseButtonText = "CancelThis/Text".GetLocalized(),
            PrimaryButtonText = "Param_Preset_New_Name/Content".GetLocalized(),
            Content = content,
            DefaultButton = ContentDialogButton.Close
        };

        // --- Обработчики ---

        // Обработчик выбора иконки — обновляем иконку при выборе сразу
        ItemClickEventHandler itemClickHandler = (_, e) =>
        {
            var clickedGlyph = (FontIcon)e.ClickedItem;
            if (clickedGlyph != null)
            {
                selectedGlyph = clickedGlyph.Glyph;
                glyphIcon.Glyph = selectedGlyph;
            }
        };

        RoutedEventHandler clickHandler = (_, _) =>
        {
            // Подписываемся на выбор иконки
            SelectionGrid.ItemClick -= itemClickHandler; // Избегаем дублирования
            SelectionGrid.ItemClick += itemClickHandler;

            SymbolFlyout.ShowAt(iconButton);
        };

        iconButton.Click += clickHandler;

        var result = await dialog.ShowAsync();

        // --- Очистка после диалога ---
        iconButton.Click -= clickHandler;
        SelectionGrid.ItemClick -= itemClickHandler;

        if (result == ContentDialogResult.Primary)
        {
            await AddPreset(nameBox.Text, descBox.Text, selectedGlyph);
        }
    }

    private async Task AddPreset(string presetName, string presetDesc, string glyph)
    {
        if (presetName != "")
        {
            await LogHelper.Log($"Adding new preset: \"{presetName}\"");
            
            try
            {
                if (PresetManager.Presets.Length != 0)
                {
                    AppSettings.Preset += 1;
                    _presetIndex += 1;
                }

                _presetChanging = true;
                if (PresetManager.Presets.Length == 0)
                {
                    PresetManager.Presets = new Preset[1];
                    PresetManager.Presets[0] = new Preset
                        { PresetName = presetName, PresetDesc = presetDesc, PresetIcon = glyph };
                }
                else
                {
                    var presetList = new List<Preset>(PresetManager.Presets)
                    {
                        new()
                        {
                            PresetName = presetName,
                            PresetDesc = presetDesc,
                            PresetIcon = glyph
                        }
                    };
                    PresetManager.Presets = [.. presetList];
                }

                _presetChanging = false;
                NotificationsService.ShowNotification("SaveSuccessTitle".GetLocalized(),
                    "SaveSuccessDesc".GetLocalized() + " " + presetName,
                    InfoBarSeverity.Success);
            }
            catch
            {
                // Ignored
            }
        }
        else
        {
            NotificationsService.ShowNotification("Add_Target_Error/Title".GetLocalized(),
                "Add_Target_Error/Subtitle".GetLocalized(),
                InfoBarSeverity.Error);
        }

        AppSettings.SaveSettings();
        PresetManager.SaveSettings();
        LoadPresets();
    }

    private void EditPresetButton_Click(string presetName, string presetDesc, string glyph)
    {
        try
        {
            LogHelper.Log(
                $"Editing preset name: From \"{PresetManager.Presets[_presetIndex].PresetName}\" To \"{presetName}\"");
            if (presetName != "")
            {
                
                PresetManager.Presets[_presetIndex].PresetName = presetName;
                PresetManager.Presets[_presetIndex].PresetDesc = presetDesc;
                PresetManager.Presets[_presetIndex].PresetIcon = glyph;
                PresetManager.SaveSettings();
                _presetChanging = true;
                LoadPresets();
                _presetChanging = false;
                NotificationsService.ShowNotification("Edit_Target/Title".GetLocalized(),
                    "Edit_Target/Subtitle".GetLocalized() + " " + presetName,
                    InfoBarSeverity.Success);
            }
            else
            {
                NotificationsService.ShowNotification("Edit_Target_Error/Title".GetLocalized(),
                    "Edit_Target_Error/Subtitle".GetLocalized(),
                    InfoBarSeverity.Error);
            }
        }
        catch (Exception exception)
        {
            LogHelper.TraceIt_TraceError(exception);
        }
    }

    private async void DeletePresetButton_Click()
    {
        try
        {
            await LogHelper.Log("Showing delete preset dialog");
            var delDialog = new ContentDialog
            {
                Title = "Param_DelPreset_Text".GetLocalized(),
                Content = "Param_DelPreset_Desc".GetLocalized(),
                CloseButtonText = "CancelThis/Text".GetLocalized(),
                PrimaryButtonText = "Delete".GetLocalized(),
                DefaultButton = ContentDialogButton.Close
            };
            // Use this code to associate the dialog to the appropriate AppWindow by setting
            // the dialog's XamlRoot to the same XamlRoot as an element that is already present in the AppWindow.
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
            {
                delDialog.XamlRoot = XamlRoot;
            }

            var result = await delDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var indexpreset = AppSettings.Preset > -1 ? AppSettings.Preset : 0;

                await LogHelper.Log(
                    $"Showing delete preset dialog: deleting preset \"{PresetManager.Presets[indexpreset].PresetName}\"");

                

                _presetChanging = true;

                var presetList = new List<Preset>(PresetManager.Presets);
                presetList.RemoveAt(indexpreset);
                PresetManager.Presets = [.. presetList];

                _presetChanging = false;

                AppSettings.Preset = PresetManager.Presets.Length > 0 ? 0 : -1;
                _presetIndex = AppSettings.Preset;

                NotificationsService.ShowNotification("DeleteSuccessTitle".GetLocalized(),
                    "DeleteSuccessDesc".GetLocalized(),
                    InfoBarSeverity.Success);

                PresetManager.SaveSettings();
                LoadPresets();
            }
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception);
        }
    }

    private async void EditPresetButton_Click_1(object sender, RoutedEventArgs e)
    {
        try
        {
            
            var presetName = PresetManager.Presets[_presetIndex].PresetName;
            var presetDesc = PresetManager.Presets[_presetIndex].PresetDesc;
            if (presetName.Contains("Preset_")) { presetName = ГлавнаяPage.TryLocalize(presetName); }
            if (presetDesc.Contains("Preset_")) { presetDesc = ГлавнаяPage.TryLocalize(presetDesc); }
            await OpenEditPresetDialog_Click(presetName, presetDesc,
                PresetManager.Presets[_presetIndex].PresetIcon);
        }
        catch (Exception ex)
        {
            await LogHelper.LogWarn(ex);
        }
    }

    private async Task OpenEditPresetDialog_Click(string presetName, string presetDesc, string presetIcon)
    {
        // Создаём элементы интерфейса
        var glyph = new FontIcon { Glyph = presetIcon };
        var iconButton = new Button
        {
            Height = 60,
            Width = 60,
            CornerRadius = new CornerRadius(16),
            Shadow = (ThemeShadow)Resources["SharedShadow"],
            Translation = new Vector3(0, 0, 14),
            Content = glyph
        };

        var nameBox = new TextBox
        {
            Text = presetName,
            MaxLength = 31,
            PlaceholderText = "Param_Preset_New_Name_Add/PlaceholderText".GetLocalized(),
            CornerRadius = new CornerRadius(9),
            Width = 280,
            Shadow = (ThemeShadow)Resources["SharedShadow"],
            Translation = new Vector3(0, 0, 10)
        };

        var descBox = new TextBox
        {
            Text = presetDesc,
            PlaceholderText = "Param_Preset_New_Desc_Add/PlaceholderText".GetLocalized(),
            CornerRadius = new CornerRadius(9),
            Width = 280,
            Margin = new Thickness(0, 6, 0, 0),
            Shadow = (ThemeShadow)Resources["SharedShadow"],
            Translation = new Vector3(0, 0, 10)
        };

        var fieldsPanel = new StackPanel();
        fieldsPanel.Children.Add(nameBox);
        fieldsPanel.Children.Add(descBox);

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 15
        };
        content.Children.Add(iconButton);
        content.Children.Add(fieldsPanel);

        var dialog = new ContentDialog
        {
            Title = "Param_Preset_Edit_Name/Content".GetLocalized(),
            XamlRoot = XamlRoot,
            PrimaryButtonText = "Param_Preset_Edit_Name/Content".GetLocalized(),
            SecondaryButtonText = "Param_Preset_Delete_Preset/Content".GetLocalized(),
            CloseButtonText = "CancelThis/Text".GetLocalized(),
            DefaultButton = ContentDialogButton.Close,
            Content = content
        };

        // --- Обработчики ---

        // Обработчик выбора иконки — обновляем сразу
        ItemClickEventHandler itemClickHandler = (_, e) =>
        {
            var selectedGlyph = (FontIcon)e.ClickedItem;
            if (selectedGlyph == null)
            {
                return;
            }

            presetIcon = selectedGlyph.Glyph;
            glyph.Glyph = presetIcon;
        };

        RoutedEventHandler clickHandler = (_, _) =>
        {
            // Подписываемся на выбор иконки
            SelectionGrid.ItemClick -= itemClickHandler; // Избегаем дублирования
            SelectionGrid.ItemClick += itemClickHandler;

            SymbolFlyout.ShowAt(iconButton);
        };

        iconButton.Click += clickHandler;

        // Показываем диалог
        var result = await dialog.ShowAsync();

        // --- Очистка после диалога ---
        iconButton.Click -= clickHandler;
        SelectionGrid.ItemClick -= itemClickHandler;

        // --- Логика результата ---
        if (result == ContentDialogResult.Primary)
        {
            EditPresetButton_Click(nameBox.Text, descBox.Text, presetIcon);
        }
        else if (result == ContentDialogResult.Secondary)
        {
            DeletePresetButton_Click();
        }
    }

    #endregion

    #endregion

    #endregion

    #region Preset Settings Events

    private void PresetsControl_SelectionChanged(object sender, SelectionChangedEventArgs? e)
    {
        try
        {
            var selectedItem = (sender as PresetSelector)?.SelectedItem;

            if (selectedItem == null)
            {
                return;
            }

            SelectedPresetName.Text = selectedItem.Text;

            // Обход отсутствия описания, при помощи записывания имени пресета в описание. Чтобы не отображать два раза одну и ту же строку, описание пресета скрывается (так как его нет)
            SelectedPresetDescription.Text = selectedItem.Description != selectedItem.Text
                ? selectedItem.Description
                : string.Empty;

            if (e != null)
            {
                if (_doubleClickApplyToken == SelectedPresetName.Text + SelectedPresetDescription.Text + PresetsControl.SelectedIndex)
                {
                    ApplyButton_Click(null, null);
                }

                _doubleClickApplyToken = SelectedPresetName.Text + SelectedPresetDescription.Text + PresetsControl.SelectedIndex;
            }

            if (selectedItem.Description == selectedItem.Text)
            {
                SelectedPresetDescription.Visibility = Visibility.Collapsed;
                EditCurrentButtonsStackPanel.Margin = new Thickness(0, 0, -13, -10);
                EditCurrentButtonsStackPanel.VerticalAlignment = VerticalAlignment.Top;
                SelectedPresetTextsStackPanel.VerticalAlignment = VerticalAlignment.Center;
            }
            else
            {
                SelectedPresetDescription.Visibility = Visibility.Visible;
                EditCurrentButtonsStackPanel.Margin = new Thickness(0, 17, -13, -10);
                EditCurrentButtonsStackPanel.VerticalAlignment = VerticalAlignment.Center;
                SelectedPresetTextsStackPanel.VerticalAlignment = VerticalAlignment.Top;
            }

            if (true|| (selectedItem.Text == "Preset_Max_Name/Text".GetLocalized() &&
                 selectedItem.Description == "Preset_Max_Desc/Text".GetLocalized()) ||
                (selectedItem.Text == "Preset_Speed_Name/Text".GetLocalized() &&
                 selectedItem.Description == "Preset_Speed_Desc/Text".GetLocalized()) ||
                (selectedItem.Text == "Preset_Balance_Name/Text".GetLocalized() &&
                 selectedItem.Description == "Preset_Balance_Desc/Text".GetLocalized()) ||
                (selectedItem.Text == "Preset_Eco_Name/Text".GetLocalized() &&
                 selectedItem.Description == "Preset_Eco_Desc/Text".GetLocalized()) ||
                (selectedItem.Text == "Preset_Min_Name/Text".GetLocalized() &&
                 selectedItem.Description == "Preset_Min_Desc/Text".GetLocalized())
               )
            {
                var optimizationLevel = PremadeOptimizationLevel.SelectedIndex switch
                {
                    0 => OptimizationLevel.Basic,
                    1 => OptimizationLevel.Standard,
                    2 => OptimizationLevel.Deep,
                    _ => OptimizationLevel.Basic
                };

                PresetConfiguration preset;

                if (selectedItem.Text == "Preset_Max_Name/Text".GetLocalized())
                {
                    preset = OcFinder.CreatePreset(PresetType.Max, optimizationLevel);
                    HelpWithShowPreset(PresetType.Max);
                }
                else if (selectedItem.Text == "Preset_Speed_Name/Text".GetLocalized())
                {
                    preset = OcFinder.CreatePreset(PresetType.Speed, optimizationLevel);
                    HelpWithShowPreset(PresetType.Speed);
                }
                else if (selectedItem.Text == "Preset_Balance_Name/Text".GetLocalized())
                {
                    preset = OcFinder.CreatePreset(PresetType.Balance, optimizationLevel);
                    HelpWithShowPreset(PresetType.Balance);
                }
                else if (selectedItem.Text == "Preset_Eco_Name/Text".GetLocalized())
                {
                    preset = OcFinder.CreatePreset(PresetType.Eco, optimizationLevel);
                    HelpWithShowPreset(PresetType.Eco);
                }
                else
                {
                    preset = OcFinder.CreatePreset(PresetType.Min, optimizationLevel);
                    HelpWithShowPreset(PresetType.Min);
                }

                PresetPerformanceBar.Value = 50 + preset.Metrics.PerformanceScore;
                PresetEnergyEfficiencyBar.Value = 50 + preset.Metrics.EfficiencyScore;
                PresetTemperaturesBar.Value = 50 + preset.Metrics.ThermalScore;

                PresetSettingsStackPanel.Visibility = Visibility.Collapsed;
                PresetSettingsBeginnerView.Visibility = Visibility.Visible;
                PresetSettingsBeginnerView.Margin = new Thickness(0, -5, 0, 0);
                PremadePresetAffectsOn.Visibility = Visibility.Visible;
                PresetSettingsBeginnerViewSettingsStackPanel.Visibility = Visibility.Collapsed;
                EditPresetButton.Visibility = Visibility.Collapsed;
                PresetSettingsChangeViewStackPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                PresetSettingsBeginnerViewSettingsStackPanel.Visibility = Visibility.Visible;
                PresetSettingsBeginnerView.Margin = new Thickness(0, 0, 0, 0);

                if (AppSettings.PresetsPageViewModeBeginner)
                {
                    BeginnerOptionsButton.IsChecked = true;
                    AdvancedOptionsButton.IsChecked = false;
                    PresetSettingsStackPanel.Visibility = Visibility.Collapsed;
                    PresetSettingsBeginnerView.Visibility = Visibility.Visible;
                }
                else
                {
                    BeginnerOptionsButton.IsChecked = false;
                    AdvancedOptionsButton.IsChecked = true;
                    PresetSettingsStackPanel.Visibility = Visibility.Visible;
                    PresetSettingsBeginnerView.Visibility = Visibility.Collapsed;
                }

                PremadePresetAffectsOn.Visibility = Visibility.Collapsed;
                EditPresetButton.Visibility = Visibility.Visible;
                PresetSettingsChangeViewStackPanel.Visibility = Visibility.Visible;

                var selectedIndex = PresetsControl.SelectedIndex;
                if (selectedIndex > -1 && selectedIndex < PresetManager.Presets.Length &&
                    selectedItem.Text == PresetManager.Presets[selectedIndex].PresetName)
                {
                    AppSettings.Preset = selectedIndex;
                }
                else
                {
                    for (var presetIndex = 0; presetIndex < PresetManager.Presets.Length; presetIndex++)
                    {
                        var preset = PresetManager.Presets[presetIndex];
                        var presetName = preset.PresetName;
                        var presetDesc = preset.PresetDesc;
                        if (presetName.Contains("Preset_")) { presetName = ГлавнаяPage.TryLocalize(presetName); }
                        if (presetDesc.Contains("Preset_")) { presetDesc = ГлавнаяPage.TryLocalize(presetDesc); }
                        if (presetName == selectedItem.Text && 
                            (presetDesc == selectedItem.Description || presetName == selectedItem.Description) &&
                            (preset.PresetIcon == selectedItem.IconGlyph ||
                             preset.PresetIcon == "\uE718"))
                        {
                            AppSettings.Preset = presetIndex;
                            break;
                        }
                    }
                }

                _presetIndex = AppSettings.Preset;
                AppSettings.SaveSettings();
                InitializeCustomPresetSettings(_presetIndex);
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogWarn(ex);
        }
    }

    private async void ApplyButton_Click(object? sender, RoutedEventArgs? e)
    {
        try
        {
            var selectedItem = PresetsControl.SelectedItem;
            var selectedIndex = PresetsControl.SelectedIndex;

            var name = selectedItem.Text;
            var desc = selectedItem.Description;
            var icon = selectedItem.IconGlyph;
            Preset? requiredPreset = null;
            if (selectedIndex > -1 && selectedIndex < PresetManager.Presets.Length &&
                selectedItem.Text == PresetManager.Presets[selectedIndex].PresetName)
            {
                requiredPreset = PresetManager.Presets[selectedIndex];
                AppSettings.Preset = selectedIndex;
            }
            else
            {
                for (var presetIndex = 0; presetIndex < PresetManager.Presets.Length; presetIndex++)
                {
                    var preset = PresetManager.Presets[presetIndex];
                    var presetName = preset.PresetName;
                    var presetDesc = preset.PresetDesc;
                    if (presetName.Contains("Preset_")) { presetName = ГлавнаяPage.TryLocalize(presetName); }
                    if (presetDesc.Contains("Preset_")) { presetDesc = ГлавнаяPage.TryLocalize(presetDesc); }
                    if (presetName == name &&
                        (presetDesc == desc || presetName == desc) &&
                        (preset.PresetIcon == icon ||
                         preset.PresetIcon == "\uE718"))
                    {
                        AppSettings.Preset = presetIndex;
                        requiredPreset = preset;
                        break;
                    }
                }
            }

            AppSettings.SaveSettings();

            ПараметрыPage.ApplyInfo = string.Empty;
            if (requiredPreset != null)
            {
                await Applyer.ApplyPreset(requiredPreset, true);
            }

            await Task.Delay(1000);
            var timer = 1000;
            var applyInfo = ПараметрыPage.ApplyInfo;
            if (applyInfo != string.Empty)
            {
                timer *= applyInfo.Split('\n').Length + 1;
            }

            DispatcherQueue.TryEnqueue(async void () =>
            {
                try
                {
                    ApplyTeach.Target = ApplyButton;
                    ApplyTeach.Title = "Apply_Success".GetLocalized();
                    ApplyTeach.Subtitle = "";
                    ApplyTeach.IconSource = new SymbolIconSource { Symbol = Symbol.Accept };
                    ApplyTeach.IsOpen = true;
                    var infoSet = InfoBarSeverity.Success;
                    if (applyInfo != string.Empty)
                    {
                        await LogHelper.Log(applyInfo);
                        ApplyTeach.Title = "Apply_Warn".GetLocalized();
                        ApplyTeach.Subtitle = "Apply_Warn_Desc".GetLocalized() + applyInfo;
                        ApplyTeach.IconSource = new SymbolIconSource { Symbol = Symbol.ReportHacked };
                        await Task.Delay(timer);
                        ApplyTeach.IsOpen = false;
                        infoSet = InfoBarSeverity.Warning;
                    }
                    else
                    {
                        await Task.Delay(3000);
                        ApplyTeach.IsOpen = false;
                    }

                    NotificationsService.ShowNotification(ApplyTeach.Title,
                        ApplyTeach.Subtitle + (applyInfo != string.Empty ? "DELETEUNAVAILABLE" : ""),
                        infoSet,
                        true);
                }
                catch (Exception ex)
                {
                    await LogHelper.LogError(ex);
                }
            });
        }
        catch (Exception ex)
        {
            await LogHelper.LogWarn(ex);
        }
    }

    private void Premade_OptimizationLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        
        // TODO: FIX PREMADE PRESETS
        //AppSettings.PremadeOptimizationLevel = PremadeOptimizationLevel.SelectedIndex;
        AppSettings.SaveSettings();

        PremadeOptimizationLevelDesc.Text = PremadeOptimizationLevel.SelectedIndex switch
        {
            0 => "Preset_OptimizationLevel_Base_Desc".GetLocalized(),
            1 => "Preset_OptimizationLevel_Average_Desc".GetLocalized(),
            2 => "Preset_OptimizationLevel_Strong_Desc".GetLocalized(),
            _ => "Preset_OptimizationLevel_Base_Desc".GetLocalized()
        };

        if (OcFinder.IsUndervoltingAvailable() && PremadeOptimizationLevel.SelectedIndex == 2)
        {
            PremadeCurveOptimizerStackPanel.Visibility = Visibility.Visible;
        }
        else
        {
            PremadeCurveOptimizerStackPanel.Visibility = Visibility.Collapsed;
        }

        PresetsControl_SelectionChanged(PresetsControl, null);
    }

    private void CurveOptimizerLevel_Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        // TODO: FIX PREMADE PRESETS
        //AppSettings.PremadeCurveOptimizerOverrideLevel = (int)CurveOptimizerLevelSlider.Value;
        AppSettings.SaveSettings();
    }

    private void CurveOptimizerLevelCustom_Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_presetChanging)
        {
            return;
        }

        
        var index = _presetIndex == -1 ? 0 : _presetIndex;

        PresetManager.Presets[index].CurveOptimizerOptions.CpuCurveOptimizerUndervoltingLevel.Value = CurveOptimizerLevelCustomSlider.Value;
        PresetManager.SaveSettings();
    }

    private void CurveOptimizerCustom_Toggled(object sender, RoutedEventArgs e)
    {
        if (_presetChanging)
        {
            return;
        }

        
        var index = _presetIndex == -1 ? 0 : _presetIndex;

        PresetManager.Presets[index].CurveOptimizerOptions.CpuCurveOptimizerUndervoltingLevel.IsEnabled = CurveOptimizerCustom.IsOn;
        PresetManager.SaveSettings();
    }

    private void CurveOptimizerCustom_PointerPressed(object sender, PointerRoutedEventArgs e) =>
        CurveOptimizerCustom.IsOn = !CurveOptimizerCustom.IsOn;

    #region Advanced View Page Controllers

    #region Sliders

    //Параметры процессора, при изменении слайдеров
    //Максимальная температура CPU (C)
    private void C1_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _presetChanging)
        {
            return;
        }

        
        if (_presetIndex != -1)
        {
            PresetManager.Presets[_presetIndex].CpuSettings.CpuMaximumTemperature.Value = C1V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Лимит CPU (W)
    private void C2_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _presetChanging)
        {
            return;
        }

        
        if (_presetIndex != -1)
        {
            PresetManager.Presets[_presetIndex].CpuSettings.CpuSustainedPowerLimit.Value = C2V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Реальный CPU (W)
    private void C3_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _presetChanging)
        {
            return;
        }

        
        if (_presetIndex != -1)
        {
            PresetManager.Presets[_presetIndex].CpuSettings.CpuActualPowerLimit.Value = C3V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Средний CPU(W)
    private void C4_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _presetChanging)
        {
            return;
        }

        
        if (_presetIndex != -1)
        {
            PresetManager.Presets[_presetIndex].CpuSettings.CpuAveragePowerLimit.Value = C4V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Тик быстрого разгона (S)
    private void C5_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _presetChanging)
        {
            return;
        }

        
        if (_presetIndex != -1)
        {
            PresetManager.Presets[_presetIndex].CpuSettings.CpuBoostTimeSlow.Value = C5V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Тик медленного разгона (S)
    private void C6_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _presetChanging)
        {
            return;
        }

        
        if (_presetIndex != -1)
        {
            PresetManager.Presets[_presetIndex].CpuSettings.CpuBoostTimeFast.Value = C6V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Параметры VRM
    private void V1v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _presetChanging)
        {
            return;
        }

        
        if (_presetIndex != -1)
        {
            PresetManager.Presets[_presetIndex].VrmSettings.VrmCpuEdcCurrentLimit.Value = V1V.Value;
            PresetManager.SaveSettings();
        }
    }

    private void V2v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _presetChanging)
        {
            return;
        }

        
        if (_presetIndex != -1)
        {
            PresetManager.Presets[_presetIndex].VrmSettings.VrmCpuTdcCurrentLimit.Value = V2V.Value;
            PresetManager.SaveSettings();
        }
    }

    private void V3v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _presetChanging)
        {
            return;
        }

        
        if (_presetIndex != -1)
        {
            PresetManager.Presets[_presetIndex].VrmSettings.VrmSocEdcCurrentLimit.Value = V3V.Value;
            PresetManager.SaveSettings();
        }
    }

    private void V4v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _presetChanging)
        {
            return;
        }

        
        if (_presetIndex != -1)
        {
            PresetManager.Presets[_presetIndex].VrmSettings.VrmSocTdcCurrentLimit.Value = V4V.Value;
            PresetManager.SaveSettings();
        }
    }

    private void G9v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _presetChanging)
        {
            return;
        }

        
        if (_presetIndex != -1)
        {
            PresetManager.Presets[_presetIndex].SubsystemsSettings.MinimumIntegratedGraphicsFrequency.Value = G9V.Value;
            PresetManager.SaveSettings();
        }
    }

    private void G10v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _presetChanging)
        {
            return;
        }

        
        if (_presetIndex != -1)
        {
            PresetManager.Presets[_presetIndex].SubsystemsSettings.MaximumIntegratedGraphicsFrequency.Value = G10V.Value;
            PresetManager.SaveSettings();
        }
    }

    #endregion

    #region CheckBoxes

    //Параметры процессора
    //Максимальная температура CPU (C)
    private void C1_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _presetChanging)
        {
            return;
        }

        
        var check = C1.IsChecked == true;
        if (_presetIndex != -1)
        {
            PresetManager.Presets[_presetIndex].CpuSettings.CpuMaximumTemperature.IsEnabled = check;
            PresetManager.Presets[_presetIndex].CpuSettings.CpuMaximumTemperature.Value = C1V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Лимит CPU (W)
    private void C2_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _presetChanging)
        {
            return;
        }

        
        var check = C2.IsChecked == true;
        if (_presetIndex != -1)
        {
            PresetManager.Presets[_presetIndex].CpuSettings.CpuSustainedPowerLimit.IsEnabled = check;
            PresetManager.Presets[_presetIndex].CpuSettings.CpuSustainedPowerLimit.Value = C2V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Реальный CPU (W)
    private void C3_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _presetChanging)
        {
            return;
        }

        
        var check = C3.IsChecked == true;
        if (_presetIndex != -1)
        {
            PresetManager.Presets[_presetIndex].CpuSettings.CpuActualPowerLimit.IsEnabled = check;
            PresetManager.Presets[_presetIndex].CpuSettings.CpuActualPowerLimit.Value = C3V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Средний CPU (W)
    private void C4_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _presetChanging)
        {
            return;
        }

        
        var check = C4.IsChecked == true;
        if (_presetIndex != -1)
        {
            PresetManager.Presets[_presetIndex].CpuSettings.CpuAveragePowerLimit.IsEnabled = check;
            PresetManager.Presets[_presetIndex].CpuSettings.CpuAveragePowerLimit.Value = C4V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Тик быстрого разгона (S)
    private void C5_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _presetChanging)
        {
            return;
        }

        
        var check = C5.IsChecked == true;
        if (_presetIndex != -1)
        {
            PresetManager.Presets[_presetIndex].CpuSettings.CpuBoostTimeSlow.IsEnabled = check;
            PresetManager.Presets[_presetIndex].CpuSettings.CpuBoostTimeSlow.Value = C5V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Тик медленного разгона (S)
    private void C6_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _presetChanging)
        {
            return;
        }

        
        var check = C6.IsChecked == true;
        if (_presetIndex != -1)
        {
            PresetManager.Presets[_presetIndex].CpuSettings.CpuBoostTimeFast.IsEnabled = check;
            PresetManager.Presets[_presetIndex].CpuSettings.CpuBoostTimeFast.Value = C6V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Параметры VRM
    //Максимальный ток VRM A
    private void V1_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _presetChanging)
        {
            return;
        }

        
        var check = V1.IsChecked == true;
        if (_presetIndex != -1)
        {
            PresetManager.Presets[_presetIndex].VrmSettings.VrmCpuEdcCurrentLimit.IsEnabled = check;
            PresetManager.Presets[_presetIndex].VrmSettings.VrmCpuEdcCurrentLimit.Value = V1V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Лимит по току VRM A
    private void V2_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _presetChanging)
        {
            return;
        }

        
        var check = V2.IsChecked == true;
        if (_presetIndex != -1)
        {
            PresetManager.Presets[_presetIndex].VrmSettings.VrmCpuTdcCurrentLimit.IsEnabled = check;
            PresetManager.Presets[_presetIndex].VrmSettings.VrmCpuTdcCurrentLimit.Value = V2V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Максимальный ток SOC A
    private void V3_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _presetChanging)
        {
            return;
        }

        
        var check = V3.IsChecked == true;
        if (_presetIndex != -1)
        {
            PresetManager.Presets[_presetIndex].VrmSettings.VrmSocEdcCurrentLimit.IsEnabled = check;
            PresetManager.Presets[_presetIndex].VrmSettings.VrmSocEdcCurrentLimit.Value = V3V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Лимит по току SOC A
    private void V4_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _presetChanging)
        {
            return;
        }

        
        var check = V4.IsChecked == true;
        if (_presetIndex != -1)
        {
            PresetManager.Presets[_presetIndex].VrmSettings.VrmSocTdcCurrentLimit.IsEnabled = check;
            PresetManager.Presets[_presetIndex].VrmSettings.VrmSocTdcCurrentLimit.Value = V4V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Минимальная частота iGpu
    private void G9_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _presetChanging)
        {
            return;
        }

        
        var check = G9.IsChecked == true;
        if (_presetIndex != -1)
        {
            PresetManager.Presets[_presetIndex].SubsystemsSettings.MinimumIntegratedGraphicsFrequency.IsEnabled = check;
            PresetManager.Presets[_presetIndex].SubsystemsSettings.MinimumIntegratedGraphicsFrequency.Value = G9V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Максимальная частота iGpu
    private void G10_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _presetChanging)
        {
            return;
        }

        
        var check = G10.IsChecked == true;
        if (_presetIndex != -1)
        {
            PresetManager.Presets[_presetIndex].SubsystemsSettings.MaximumIntegratedGraphicsFrequency.IsEnabled = check;
            PresetManager.Presets[_presetIndex].SubsystemsSettings.MaximumIntegratedGraphicsFrequency.Value = G10V.Value;
            PresetManager.SaveSettings();
        }
    }

    #endregion

    #region NumberBoxes

    private void TargetNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        var name = sender.Tag.ToString();
            
        if (name != null)
        {
            object sliderObject;

            try
            {
                sliderObject = FindName(name);
            }
            catch (Exception ex)
            {
                LogHelper.TraceIt_TraceError(ex);
                return;
            }

            if (sliderObject is Slider slider)
            {
                if (slider.Maximum < sender.Value)
                {
                    slider.Maximum = ПараметрыPage.FromValueToUpperFive(sender.Value);
                }
            }
        }        
    }

    #endregion

    #endregion

    #endregion
}