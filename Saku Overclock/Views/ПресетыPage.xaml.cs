using System.Diagnostics;
using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.JsonContainers;
using Saku_Overclock.Services;
using Saku_Overclock.SmuEngine;
using Saku_Overclock.Styles;
using Saku_Overclock.ViewModels;
using Windows.Foundation.Metadata;
using Windows.UI.Text;
using static ZenStates.Core.Cpu;
using Task = System.Threading.Tasks.Task;

namespace Saku_Overclock.Views;

public sealed partial class ПресетыPage
{
    private static readonly IAppSettingsService AppSettings = App.GetService<IAppSettingsService>();
    private static readonly IApplyerService Applyer = App.GetService<IApplyerService>();
    private static readonly IOcFinderService OcFinder = App.GetService<IOcFinderService>();
    private static readonly IPresetManagerService PresetManager = App.GetService<IPresetManagerService>();
    private bool _isLoaded; // Загружена ли корректно страница для применения изменений 
    private bool _waitforload = true; // Ожидание окончательной смены пресета на другой. Активируется при смене пресета 
    private int _indexpreset; // Выбранный пресет
    private readonly IBackgroundDataUpdater? _dataUpdater;
    private CodeName? _codeName;

    private static readonly IAppNotificationService
        NotificationsService = App.GetService<IAppNotificationService>(); // Уведомления приложения

    private static readonly ISendSmuCommandService SendSmuCommand = App.GetService<ISendSmuCommandService>();
    private static bool? _isPlatformPc = false;
    private string _doubleClickApply = string.Empty;

    public ПресетыPage()
    {
        InitializeComponent();
        PresetManager.LoadSettings();
        AppSettings.SaveSettings();
        _dataUpdater = App.BackgroundUpdater!;
        _dataUpdater.DataUpdated += OnDataUpdated;
        Unloaded += (_, _) =>
        {
            _dataUpdater.DataUpdated -= OnDataUpdated;
        };
        Loaded += ПресетыPage_Loaded;
    }

    private void ПресетыPage_Loaded(object sender, RoutedEventArgs e)
    {
        _waitforload = false;
        SelectedPresetDescription.Text = "Preset_Min_Desc/Text".GetLocalized();

        try
        {
            _isPlatformPc = SendSmuCommand.IsPlatformPc();
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex);
        }

        _ = LoadPresets();

        // Загрузить остальные UI элементы, функции блока "Дополнительно"
        try
        {
            AutostartCom.SelectedIndex = AppSettings.AutostartType;
        }
        catch
        {
            AutostartCom.SelectedIndex = 0;
        }

        try
        {
            HideCom.SelectedIndex = AppSettings.HidingType;
        }
        catch
        {
            HideCom.SelectedIndex = 2;
        }

        ReapplyOptionsSetOnly(AppSettings.ReapplyOverclock ? ReapplyOptionsEnabled : ReapplyOptionsDisabled);
        AutoApplyOptionsSetOnly(AppSettings.ReapplyLatestSettingsOnAppLaunch
            ? AutoApplyOptionsEnabled
            : AutoApplyOptionsDisabled);
        TrayMonSetOnly(AppSettings.NiIconsEnabled ? TrayMonFeatEnabled : TrayMonFeatDisabled);
        RtssOverlaySetOnly(AppSettings.RtssMetricsEnabled ? RtssOverlayEnabled : RtssOverlayDisabled);
        StreamStabilizerSetOnly(
            AppSettings.StreamStabilizerEnabled ? StreamStabilizerSmart : StreamStabilizerDisabled);

        PremadeOptimizationLevel.SelectedIndex = AppSettings.PremadeOptimizationLevel;

        if (AppSettings.StreamStabilizerEnabled)
        {
            StreamStabilizerModeCombo.SelectedIndex = AppSettings.StreamStabilizerType;
            StreamStabilizerTargetMhz.Value = AppSettings.StreamStabilizerMaxMHz;
            StreamStabilizerTargetPercent.Value = AppSettings.StreamStabilizerMaxPercentMHz;

            _isLoaded = true;
            StreamStabilizerModeCombo_SelectionChanged(null, null);
            _isLoaded = false;
        }

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
        };

        CurveOptimizerCustomGrid.Visibility =
            OcFinder.IsUndervoltingAvailable() ? Visibility.Visible : Visibility.Collapsed;
        if (CurveOptimizerCustomGrid.Visibility == Visibility.Collapsed)
        {
            CurveOptimizerCustom.IsOn = false;
        }

        if (AppSettings.PresetspageViewModeBeginner)
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

        var cpu = CpuSingleton.GetInstance();
        _codeName = cpu.info.codeName;
        if (_codeName != CodeName.RavenRidge && _codeName != CodeName.Dali && _codeName != CodeName.Picasso &&
            _codeName != CodeName.FireFlight && SettingsViewModel.VersionId != 5)
        {
            /*AdvancedGpuOptionsPanelGrid0.Visibility = Visibility.Collapsed;
            AdvancedGpuOptionsPanelGrid1.Visibility = Visibility.Collapsed;
            AdvancedGpuOptionsPanel0.Visibility = Visibility.Collapsed;
            AdvancedGpuOptionsPanel1.Height = new GridLength(0);
            AdvancedGpuOptionsPanel2.Visibility = Visibility.Collapsed;
            AdvancedGpuOptionsPanel3.Height = new GridLength(0);*/
        }
    }

    #region JSON and Initialization

    #region Initialization

    private async Task LoadPresets()
    {
        // Загрузить пресеты перед началом работы с ними
        PresetManager.LoadSettings();

        // Очистить элементы PresetsControl
        PresetsControl.Items.Clear();

        // Пройтись по каждому пресету и добавить их в PresetsControl
        foreach (var preset in PresetManager.Presets)
        {
            var isChecked = AppSettings.Preset != -1 &&
                            PresetManager.Presets[AppSettings.Preset].Presetname == preset.Presetname &&
                            PresetManager.Presets[AppSettings.Preset].Presetdesc == preset.Presetdesc &&
                            PresetManager.Presets[AppSettings.Preset].Preseticon == preset.Preseticon;


            var toggleButton = new PresetItem
            {
                IsSelected = isChecked,
                IconGlyph = preset.Preseticon == string.Empty ? "\uE718" : preset.Preseticon,
                Text = preset.Presetname,
                Description = preset.Presetdesc != string.Empty ? preset.Presetdesc : preset.Presetname
            };
            PresetsControl.Items.Add(toggleButton);
        }

        if (PresetManager.Presets.Length == 0 && AppSettings.Preset != -1)
        {
            AppSettings.Preset = -1;
            AppSettings.SaveSettings();
        }


        // Готовые Пресеты
        PresetsControl.Items.Add(new PresetItem
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
        });

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
                        await HelpWithShowPreset(PresetType.Max);
                    }
                    else if (item.Text == "Preset_Speed_Name/Text".GetLocalized())
                    {
                        presetConfiguration = OcFinder.CreatePreset(PresetType.Speed, optimizationLevel);
                        await HelpWithShowPreset(PresetType.Speed);
                    }
                    else if (item.Text == "Preset_Balance_Name/Text".GetLocalized())
                    {
                        presetConfiguration = OcFinder.CreatePreset(PresetType.Balance, optimizationLevel);
                        await HelpWithShowPreset(PresetType.Balance);
                    }
                    else if (item.Text == "Preset_Eco_Name/Text".GetLocalized())
                    {
                        presetConfiguration = OcFinder.CreatePreset(PresetType.Eco, optimizationLevel);
                        await HelpWithShowPreset(PresetType.Eco);
                    }
                    else
                    {
                        presetConfiguration = OcFinder.CreatePreset(PresetType.Min, optimizationLevel);
                        await HelpWithShowPreset(PresetType.Min);
                    }

                    PresetPerformanceBar.Value = 50 + presetConfiguration.Metrics.PerformanceScore;
                    PresetEnergyEfficiencyBar.Value = 50 + presetConfiguration.Metrics.EfficiencyScore;
                    PresetTemperaturesBar.Value = 50 + presetConfiguration.Metrics.ThermalScore;

                    PresetOptionsTemp.Text = presetConfiguration.Options.ThermalOptions;
                    PresetOptionsPower.Text = presetConfiguration.Options.PowerOptions;
                    PresetOptionsCurrents.Text = presetConfiguration.Options.CurrentOptions;


                    PresetOptionsCommandLine.Blocks.Clear();

                    var paragraph = new Paragraph();
                    foreach (var text in presetConfiguration.CommandString.Split("\n"))
                    {
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            var run = new Run { Text = text };
                            paragraph.Inlines.Add(run);
                        }
                    }

                    PresetOptionsCommandLine.Blocks.Add(paragraph);
                }
                else
                {
                    PresetSettingsBeginnerViewSettingsStackPanel.Visibility = Visibility.Visible;
                    PresetSettingsBeginnerView.Margin = new Thickness(0, 0, 0, 0);
                    if (AppSettings.PresetspageViewModeBeginner)
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
            await MainInitAsync(AppSettings.Preset);
        }
    }

    private async Task HelpWithShowPreset(PresetType type)
    {
        if (!_isLoaded)
        {
            await Task.Delay(700);
        }

        PresetHwGaming.Visibility = Visibility.Collapsed;
        PresetHwStreaming.Visibility = Visibility.Collapsed;
        PresetHwHardWorkload.Visibility = Visibility.Collapsed;
        PresetHwOfficeWorkload.Visibility = Visibility.Collapsed;
        PresetHwLightGaming.Visibility = Visibility.Collapsed;
        PresetHwVideos.Visibility = Visibility.Collapsed;
        PresetHwColumnHelper1.Width = GridLength.Auto;
        PresetHwColumnHelper2.Width = GridLength.Auto;
        switch (type)
        {
            case PresetType.Min:
                PresetHwOfficeWorkload.Visibility = Visibility.Visible;
                PresetHwVideos.Visibility = Visibility.Visible;
                PresetHwColumnHelper2.Width = new GridLength(0);
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
                PresetHwColumnHelper1.Width = new GridLength(0);
                PresetHwColumnHelper2.Width = new GridLength(0);
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
    }

    private async Task MainInitAsync(int index)
    {
        _waitforload = true;
        if (index > PresetManager.Presets.Length || index < 0)
        {
            _waitforload = false;
            return;
        }

        try
        {
            if (PresetManager.Presets[index].Cpu1Value > C1V.Maximum)
            {
                C1V.Maximum = ПараметрыPage.FromValueToUpperFive(PresetManager.Presets[index].Cpu1Value);
            }

            if (PresetManager.Presets[index].Cpu2Value > BaseTdpSlider.Maximum)
            {
                BaseTdpSlider.Maximum = ПараметрыPage.FromValueToUpperFive(PresetManager.Presets[index].Cpu2Value);
            }

            if (PresetManager.Presets[index].Cpu2Value > C2V.Maximum)
            {
                C2V.Maximum = ПараметрыPage.FromValueToUpperFive(PresetManager.Presets[index].Cpu2Value);
            }

            if (PresetManager.Presets[index].Cpu3Value > C3V.Maximum)
            {
                C3V.Maximum = ПараметрыPage.FromValueToUpperFive(PresetManager.Presets[index].Cpu3Value);
            }

            if (PresetManager.Presets[index].Cpu4Value > C4V.Maximum)
            {
                C4V.Maximum = ПараметрыPage.FromValueToUpperFive(PresetManager.Presets[index].Cpu4Value);
            }

            if (PresetManager.Presets[index].Cpu5Value > C5V.Maximum)
            {
                C5V.Maximum = ПараметрыPage.FromValueToUpperFive(PresetManager.Presets[index].Cpu5Value);
            }

            if (PresetManager.Presets[index].Cpu6Value > C6V.Maximum)
            {
                C6V.Maximum = ПараметрыPage.FromValueToUpperFive(PresetManager.Presets[index].Cpu6Value);
            }

            if (PresetManager.Presets[index].Vrm1Value > V1V.Maximum)
            {
                V1V.Maximum = ПараметрыPage.FromValueToUpperFive(PresetManager.Presets[index].Vrm1Value);
            }

            if (PresetManager.Presets[index].Vrm2Value > V2V.Maximum)
            {
                V2V.Maximum = ПараметрыPage.FromValueToUpperFive(PresetManager.Presets[index].Vrm2Value);
            }

            if (PresetManager.Presets[index].Vrm3Value > V3V.Maximum)
            {
                V3V.Maximum = ПараметрыPage.FromValueToUpperFive(PresetManager.Presets[index].Vrm3Value);
            }

            if (PresetManager.Presets[index].Vrm4Value > V4V.Maximum)
            {
                V4V.Maximum = ПараметрыPage.FromValueToUpperFive(PresetManager.Presets[index].Vrm4Value);
            }

            if (PresetManager.Presets[index].Gpu9Value > G9V.Maximum)
            {
                G9V.Maximum = ПараметрыPage.FromValueToUpperFive(PresetManager.Presets[index].Gpu9Value);
            }

            if (PresetManager.Presets[index].Gpu10Value > G10V.Maximum)
            {
                G10V.Maximum = ПараметрыPage.FromValueToUpperFive(PresetManager.Presets[index].Gpu10Value);
            }
        }
        catch (Exception ex)
        {
            await LogHelper.TraceIt_TraceError(ex);
        }

        try
        {
            // Заранее скомпилированная функция увеличения TDP, созданная специально для фирменной функции Smart TDP
            var fineTunedTdp = ПараметрыPage.FromValueToUpperFive(1.17335141 * PresetManager.Presets[index].Cpu2Value + 0.21631949);

            C2.IsChecked = PresetManager.Presets[index].Cpu2;
            C2V.Value = PresetManager.Presets[index].Cpu2Value;

            BaseTdpSlider.Value = PresetManager.Presets[index].Cpu2Value;
            if (PresetManager.Presets[index].Cpu2 && PresetManager.Presets[index].Cpu3 && PresetManager.Presets[index].Cpu4 &&
                (int)PresetManager.Presets[index].Cpu2Value == (int)PresetManager.Presets[index].Cpu4Value &&
                (int)PresetManager.Presets[index].Cpu3Value == fineTunedTdp)
            {
                SmartTdp.IsOn = true;
            }
            else
            {
                if (_isPlatformPc != false && SettingsViewModel.VersionId != 5) // Если устройство - не ноутбук
                {
                    // Так как на компьютерах невозможно выставить другие Power лимиты
                    if (!PresetManager.Presets[index].Cpu2 && !PresetManager.Presets[index].Cpu4 &&
                        (int)PresetManager.Presets[index].Cpu3Value == fineTunedTdp)
                    {
                        SmartTdp.IsOn = true;
                    }
                }
                else
                {
                    SmartTdp.IsOn = false;
                }
            }

            C1.IsChecked = PresetManager.Presets[index].Cpu1;
            C1V.Value = PresetManager.Presets[index].Cpu1Value;
            C3.IsChecked = PresetManager.Presets[index].Cpu3;
            C3V.Value = PresetManager.Presets[index].Cpu3Value;
            C4.IsChecked = PresetManager.Presets[index].Cpu4;
            C4V.Value = PresetManager.Presets[index].Cpu4Value;
            C5.IsChecked = PresetManager.Presets[index].Cpu5;
            C5V.Value = PresetManager.Presets[index].Cpu5Value;
            C6.IsChecked = PresetManager.Presets[index].Cpu6;
            C6V.Value = PresetManager.Presets[index].Cpu6Value;


            if (IsRavenFamily())
            {
                if (PresetManager.Presets[index].Gpu10 && PresetManager.Presets[index].Gpu9 && (int)PresetManager.Presets[index].Gpu10Value == 1200)
                {
                    if ((int)PresetManager.Presets[index].Gpu9Value == 800)
                    {
                        IntegratedGpuEnchantmentCombo.SelectedIndex = 1;
                    }

                    if ((int)PresetManager.Presets[index].Gpu9Value == 1000)
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
                if (PresetManager.Presets[index].Advncd10)
                {
                    if ((int)PresetManager.Presets[index].Advncd10Value == 1750)
                    {
                        IntegratedGpuEnchantmentCombo.SelectedIndex = 1;
                    }

                    if ((int)PresetManager.Presets[index].Advncd10Value == 2200)
                    {
                        IntegratedGpuEnchantmentCombo.SelectedIndex = 2;
                    }
                }
                else
                {
                    IntegratedGpuEnchantmentCombo.SelectedIndex = 0;
                }
            }

            if (!PresetManager.Presets[index].Cpu5 && !PresetManager.Presets[index].Cpu6)
            {
                TurboSetOnly(TurboLightModeToggle);
            }
            else
            {
                if ((PresetManager.Presets[index].Cpu5 && !PresetManager.Presets[index].Cpu6) || (!PresetManager.Presets[index].Cpu5 && PresetManager.Presets[index].Cpu6))
                {
                    TurboSetOnly(TurboLightModeToggle);
                }

                if (PresetManager.Presets[index].Cpu5 && PresetManager.Presets[index].Cpu6)
                {
                    if ((int)PresetManager.Presets[index].Cpu5Value == 400 && (int)PresetManager.Presets[index].Cpu6Value == 3)
                    {
                        TurboSetOnly(TurboBalanceModeToggle);
                    }
                    else if ((int)PresetManager.Presets[index].Cpu5Value == 5000 && (int)PresetManager.Presets[index].Cpu6Value == 1)
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

            if (PresetManager.Presets[index].Coall)
            {
                CurveOptimizerCustom.IsOn = true;
                CurveOptimizerLevelCustomSlider.Value = PresetManager.Presets[index].Coallvalue;
            }
            else
            {
                CurveOptimizerCustom.IsOn = false;
            }

            V1.IsChecked = PresetManager.Presets[index].Vrm1;
            V1V.Value = PresetManager.Presets[index].Vrm1Value;
            V2.IsChecked = PresetManager.Presets[index].Vrm2;
            V2V.Value = PresetManager.Presets[index].Vrm2Value;
            V3.IsChecked = PresetManager.Presets[index].Vrm3;
            V3V.Value = PresetManager.Presets[index].Vrm3Value;
            V4.IsChecked = PresetManager.Presets[index].Vrm4;
            V4V.Value = PresetManager.Presets[index].Vrm4Value;
            G9V.Value = PresetManager.Presets[index].Gpu9Value;
            G9.IsChecked = PresetManager.Presets[index].Gpu9;
            G10V.Value = PresetManager.Presets[index].Gpu10Value;
            G10.IsChecked = PresetManager.Presets[index].Gpu10;
        }
        catch
        {
            await LogHelper.LogError("Preset contains error. Creating new preset.");

            PresetManager.Presets = new Preset[1];
            PresetManager.Presets[0] = new Preset();
            PresetManager.SaveSettings();
        }

        _waitforload = false;
    }

    private void TurboSetOnly(ToggleButton button)
    {
        TurboLightModeToggle.IsChecked = false;
        TurboBalanceModeToggle.IsChecked = false;
        TurboHeavyModeToggle.IsChecked = false;
       
        button.IsChecked = true;
    }

    private void StreamStabilizerSetOnly(ToggleButton button)
    {
        StreamStabilizerDisabled.IsChecked = false;
        StreamStabilizerSmart.IsChecked = false;

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
        if (!_isLoaded || _waitforload)
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
        if (!_isLoaded || _waitforload)
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
        if (!_isLoaded || _waitforload)
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
        if (!_isLoaded || _waitforload)
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

    private void StreamStabilizerSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var toggle = sender as ToggleButton;
        if (StreamStabilizerDisabled.IsChecked == false
            && StreamStabilizerSmart.IsChecked == false)
        {
            toggle!.IsChecked = true;
        }
        else
        {
            if (toggle!.Name == "StreamStabilizerDisabled")
            {
                StreamStabilizerSetOnly(StreamStabilizerDisabled);
                AppSettings.StreamStabilizerEnabled = false;

                StreamStabilizerTargetMHzGrid.Visibility = Visibility.Collapsed;
                StreamStabilizerTargetPercentOfMHzGrid.Visibility = Visibility.Collapsed;

                SetPowerConfig("PROCTHROTTLEMIN 100");
                SetPowerConfig("PROCTHROTTLEMAX 100");
                SetPowerConfig("PROCFREQMAX 0");
                SavePowerConfig();
                SavePowerConfig();
            }
            else if (toggle.Name == "StreamStabilizerSmart")
            {
                StreamStabilizerSetOnly(StreamStabilizerSmart);
                AppSettings.StreamStabilizerEnabled = true;

                StreamStabilizerModeCombo_SelectionChanged(null, null);
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
        if (AutostartCom.SelectedIndex == 2 || AutostartCom.SelectedIndex == 3)
        {
            AutoStartHelper.SetStartupTask();
        }
        else
        {
            AutoStartHelper.RemoveStartupTask();
        }

        AppSettings.SaveSettings();
    }

    private void HideCom_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        AppSettings.HidingType = HideCom.SelectedIndex;
        AppSettings.SaveSettings();
    }

    private void AnimatedToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (AppSettings.PresetspageViewModeBeginner && BeginnerOptionsButton.IsChecked == false)
        {
            BeginnerOptionsButton.IsChecked = true;
            return;
        }
        
        if (!AppSettings.PresetspageViewModeBeginner && AdvancedOptionsButton.IsChecked == false)
        {
            AdvancedOptionsButton.IsChecked = true;
            return;
        }
        
        AppSettings.PresetspageViewModeBeginner = !AppSettings.PresetspageViewModeBeginner;
        AppSettings.SaveSettings();
        if (AppSettings.PresetspageViewModeBeginner)
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
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        ChangedBaseTdp_Value();
    }

    private void SmartTdp_Toggled(object sender, RoutedEventArgs e) => ChangedBaseTdp_Value();

    private void ChangedBaseTdp_Value()
    {
        if (_waitforload)
        {
            return;
        }

        var index = _indexpreset == -1 ? 0 : _indexpreset;

        // Заранее скомпилированная функция увеличения TDP, созданная специально для фирменной функции Smart TDP
        var fineTunedTdp = ПараметрыPage.FromValueToUpperFive(1.17335141 * BaseTdpSlider.Value + 0.21631949);

        C2.IsChecked = true;
        PresetManager.Presets[index].Cpu2 = true;
        C3.IsChecked = true;
        PresetManager.Presets[index].Cpu3 = true;
        C4.IsChecked = true;
        PresetManager.Presets[index].Cpu4 = true;

        if (fineTunedTdp > C3V.Maximum || BaseTdpSlider.Value > C2V.Maximum || BaseTdpSlider.Value > C4V.Maximum)
        {
            C2V.Maximum = ПараметрыPage.FromValueToUpperFive(BaseTdpSlider.Value);
            C3V.Maximum = ПараметрыPage.FromValueToUpperFive(fineTunedTdp);
            C4V.Maximum = ПараметрыPage.FromValueToUpperFive(BaseTdpSlider.Value);
        }

        C2V.Value = BaseTdpSlider.Value;
        PresetManager.Presets[index].Cpu2Value = BaseTdpSlider.Value;

        C4V.Value = BaseTdpSlider.Value;
        PresetManager.Presets[index].Cpu4Value = BaseTdpSlider.Value;

        if (SmartTdp.IsOn)
        {
            C3V.Value = fineTunedTdp;
            PresetManager.Presets[index].Cpu3Value = fineTunedTdp;
        }
        else
        {
            C3V.Value = BaseTdpSlider.Value;
            PresetManager.Presets[index].Cpu3Value = BaseTdpSlider.Value;
        }

        if (_isPlatformPc != false && SettingsViewModel.VersionId != 5) // Если устройство - не ноутбук
        {
            // Так как на компьютерах невозможно выставить другие Power лимиты
            C2.IsChecked = false; // Отключить STAPM
            PresetManager.Presets[index].Cpu2 = false;
            C4.IsChecked = false; // Отключить Slow лимит
            PresetManager.Presets[index].Cpu4 = false;
        }

        PresetManager.SaveSettings();
    }

    // Grid-помощник, который активирует переключатель когда пользователь нажал на область возле него
    private void SmartTdp_PointerPressed(object sender, object e) => SmartTdp.IsOn = !SmartTdp.IsOn;

    // Выбора режима усиления Турбобуста процессора: Авто, Умный, Сильный. Устанавливает параметры времени разгона процессора в зависимости от выбранной настройки
    private void Turbo_OtherModeToggle_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
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
            int index;
            if (AppSettings.Preset == -1)
            {
                return;
            }

            index = AppSettings.Preset;

            

            if (toggle!.Name == "TurboLightModeToggle")
            {
                TurboSetOnly(TurboLightModeToggle);
                PresetManager.Presets[index].Cpu5 = false;
                PresetManager.Presets[index].Cpu6 = false;
                C5.IsChecked = false;
                C6.IsChecked = false;
            }
            else if (toggle.Name == "TurboBalanceModeToggle")
            {
                TurboSetOnly(TurboBalanceModeToggle);
                PresetManager.Presets[index].Cpu5 = true;
                PresetManager.Presets[index].Cpu6 = true;
                PresetManager.Presets[index].Cpu5Value = 400;
                PresetManager.Presets[index].Cpu6Value = 3;
                C5.IsChecked = true;
                C6.IsChecked = true;
                C5V.Value = 400;
                C6V.Value = 3;
            }
            else if (toggle.Name == "TurboHeavyModeToggle")
            {
                TurboSetOnly(TurboHeavyModeToggle);
                PresetManager.Presets[index].Cpu5 = true;
                PresetManager.Presets[index].Cpu6 = true;
                PresetManager.Presets[index].Cpu5Value = 5000;
                PresetManager.Presets[index].Cpu6Value = 1;
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
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (AppSettings.Preset == -1)
        {
            return;
        }

        var index = AppSettings.Preset;
        if (_codeName == null)
        {
            return;
        }

        switch (IntegratedGpuEnchantmentCombo.SelectedIndex)
        {
            case 0:
                if (IsRavenFamily())
                {
                    PresetManager.Presets[index].Gpu10 = false;
                    PresetManager.Presets[index].Gpu9 = false;
                }
                else
                {
                    PresetManager.Presets[index].Advncd10 = false;
                }

                break;
            case 1:
                if (IsRavenFamily())
                {
                    PresetManager.Presets[index].Gpu10 = true;
                    PresetManager.Presets[index].Gpu10Value = 1200;
                    PresetManager.Presets[index].Gpu9 = true;
                    PresetManager.Presets[index].Gpu9Value = 800;
                }
                else
                {
                    PresetManager.Presets[index].Advncd10 = true;
                    PresetManager.Presets[index].Advncd10Value = 1750;
                }

                break;
            case 2:
                if (IsRavenFamily())
                {
                    PresetManager.Presets[index].Gpu10 = true;
                    PresetManager.Presets[index].Gpu10Value = 1200;
                    PresetManager.Presets[index].Gpu9 = true;
                    PresetManager.Presets[index].Gpu9Value = 1000;
                }
                else
                {
                    PresetManager.Presets[index].Advncd10 = true;
                    PresetManager.Presets[index].Advncd10Value = 2200;
                }

                break;
        }

        PresetManager.SaveSettings();
    }

    #region Function Helpers

    private bool IsRavenFamily() => _codeName == CodeName.RavenRidge ||
                                    _codeName == CodeName.Dali ||
                                    _codeName == CodeName.Picasso ||
                                    _codeName == CodeName.FireFlight;

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
                    _indexpreset += 1;
                }

                _waitforload = true;
                if (PresetManager.Presets.Length == 0)
                {
                    PresetManager.Presets = new Preset[1];
                    PresetManager.Presets[0] = new Preset
                        { Presetname = presetName, Presetdesc = presetDesc, Preseticon = glyph };
                }
                else
                {
                    var presetList = new List<Preset>(PresetManager.Presets)
                    {
                        new()
                        {
                            Presetname = presetName,
                            Presetdesc = presetDesc,
                            Preseticon = glyph
                        }
                    };
                    PresetManager.Presets = [.. presetList];
                }

                _waitforload = false;
                NotificationsService.Notifies ??= [];
                NotificationsService.Notifies.Add(new Notify
                {
                    Title = "SaveSuccessTitle".GetLocalized(),
                    Msg = "SaveSuccessDesc".GetLocalized() + " " + presetName,
                    Type = InfoBarSeverity.Success
                });
                NotificationsService.SaveNotificationsSettings();
            }
            catch
            {
                // Ignored
            }
        }
        else
        {
            NotificationsService.Notifies ??= [];
            NotificationsService.Notifies.Add(new Notify
            {
                Title = "Add_Target_Error/Title".GetLocalized(),
                Msg = "Add_Target_Error/Subtitle".GetLocalized(),
                Type = InfoBarSeverity.Error
            });
            NotificationsService.SaveNotificationsSettings();
        }

        AppSettings.SaveSettings();
        PresetManager.SaveSettings();
        await LoadPresets();
    }

    private async void EditPresetButton_Click(string presetName, string presetDesc, string glyph)
    {
        try
        {
            await LogHelper.Log(
                $"Editing preset name: From \"{PresetManager.Presets[_indexpreset].Presetname}\" To \"{presetName}\"");
            if (presetName != "")
            {
                
                PresetManager.Presets[_indexpreset].Presetname = presetName;
                PresetManager.Presets[_indexpreset].Presetdesc = presetDesc;
                PresetManager.Presets[_indexpreset].Preseticon = glyph;
                PresetManager.SaveSettings();
                _waitforload = true;
                await LoadPresets();
                _waitforload = false;
                NotificationsService.Notifies ??= [];
                NotificationsService.Notifies.Add(new Notify
                {
                    Title = "Edit_Target/Title".GetLocalized(),
                    Msg = "Edit_Target/Subtitle".GetLocalized() + " " + presetName,
                    Type = InfoBarSeverity.Success
                });
            }
            else
            {
                NotificationsService.Notifies ??= [];
                NotificationsService.Notifies.Add(new Notify
                {
                    Title = "Edit_Target_Error/Title".GetLocalized(),
                    Msg = "Edit_Target_Error/Subtitle".GetLocalized(),
                    Type = InfoBarSeverity.Error
                });
            }

            NotificationsService.SaveNotificationsSettings();
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception);
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
                    $"Showing delete preset dialog: deleting preset \"{PresetManager.Presets[indexpreset].Presetname}\"");

                

                _waitforload = true;

                var presetList = new List<Preset>(PresetManager.Presets);
                presetList.RemoveAt(indexpreset);
                PresetManager.Presets = [.. presetList];

                _waitforload = false;

                AppSettings.Preset = PresetManager.Presets.Length > 0 ? 0 : -1;
                _indexpreset = AppSettings.Preset;


                NotificationsService.Notifies ??= [];
                NotificationsService.Notifies.Add(new Notify
                {
                    Title = "DeleteSuccessTitle".GetLocalized(),
                    Msg = "DeleteSuccessDesc".GetLocalized(),
                    Type = InfoBarSeverity.Success
                });
                NotificationsService.SaveNotificationsSettings();

                PresetManager.SaveSettings();
                await LoadPresets();
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
            await OpenEditPresetDialog_Click(PresetManager.Presets[_indexpreset].Presetname, PresetManager.Presets[_indexpreset].Presetdesc,
                PresetManager.Presets[_indexpreset].Preseticon);
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

    private async void PresetsControl_SelectionChanged(object sender, SelectionChangedEventArgs? e)
    {
        try
        {
            var selectedItem = (sender as PresetSelector)?.SelectedItem;

            if (selectedItem == null)
            {
                return;
            }

            // Корректное отображение описания, даже если оно маленькое (чтобы Grid изменил свой размер корректно и слова не обрывались)
            PresetSettingsInfoRow.Height = new GridLength(0);
            PresetSettingsInfoRow.Height = GridLength.Auto;

            SelectedPresetName.Text = selectedItem.Text;

            // Обход отсутствия описания, при помощи записывания имени пресета в описание. Чтобы не отображать два раза одну и ту же строку, описание пресета скрывается (так как его нет)
            SelectedPresetDescription.Text = selectedItem.Description != selectedItem.Text
                ? selectedItem.Description
                : string.Empty;

            if (e != null)
            {
                if (_doubleClickApply == SelectedPresetName.Text + SelectedPresetDescription.Text)
                {
                    ApplyButton_Click(null, null);
                }

                _doubleClickApply = SelectedPresetName.Text + SelectedPresetDescription.Text;
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

            if ((selectedItem.Text == "Preset_Max_Name/Text".GetLocalized() &&
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
                    await HelpWithShowPreset(PresetType.Max);
                }
                else if (selectedItem.Text == "Preset_Speed_Name/Text".GetLocalized())
                {
                    preset = OcFinder.CreatePreset(PresetType.Speed, optimizationLevel);
                    await HelpWithShowPreset(PresetType.Speed);
                }
                else if (selectedItem.Text == "Preset_Balance_Name/Text".GetLocalized())
                {
                    preset = OcFinder.CreatePreset(PresetType.Balance, optimizationLevel);
                    await HelpWithShowPreset(PresetType.Balance);
                }
                else if (selectedItem.Text == "Preset_Eco_Name/Text".GetLocalized())
                {
                    preset = OcFinder.CreatePreset(PresetType.Eco, optimizationLevel);
                    await HelpWithShowPreset(PresetType.Eco);
                }
                else
                {
                    preset = OcFinder.CreatePreset(PresetType.Min, optimizationLevel);
                    await HelpWithShowPreset(PresetType.Min);
                }

                PresetPerformanceBar.Value = 50 + preset.Metrics.PerformanceScore;
                PresetEnergyEfficiencyBar.Value = 50 + preset.Metrics.EfficiencyScore;
                PresetTemperaturesBar.Value = 50 + preset.Metrics.ThermalScore;

                PresetOptionsTemp.Text = preset.Options.ThermalOptions;
                PresetOptionsPower.Text = preset.Options.PowerOptions;
                PresetOptionsCurrents.Text = preset.Options.CurrentOptions;

                PresetOptionsCommandLine.Blocks.Clear();

                var paragraph = new Paragraph();
                foreach (var text in preset.CommandString.Split("\n"))
                {
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        var run = new Run { Text = text };
                        paragraph.Inlines.Add(run);
                    }
                }

                PresetOptionsCommandLine.Blocks.Add(paragraph);

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

                if (AppSettings.PresetspageViewModeBeginner)
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

                for (var i = 0; i < PresetManager.Presets.Length; i++)
                {
                    if ((PresetManager.Presets[i].Presetdesc == selectedItem.Description ||
                         PresetManager.Presets[i].Presetname == selectedItem.Description) &&
                        PresetManager.Presets[i].Presetname == selectedItem.Text &&
                        PresetManager.Presets[i].Preseticon == selectedItem.IconGlyph)
                    {
                        _indexpreset = i;
                        AppSettings.Preset = i;
                        AppSettings.SaveSettings();
                        await MainInitAsync(i);
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await LogHelper.LogWarn(ex);
        }
    }

    private async void ApplyButton_Click(object? sender, RoutedEventArgs? e)
    {
        try
        {
            var endMode = PresetType.Balance;
            PresetItem? selectedItem = null;
            foreach (var item in PresetsControl.Items)
            {
                if (item.IsSelected)
                {
                    selectedItem = item;
                }
            }

            if (selectedItem == null)
            {
                return;
            }

            if (selectedItem.Text == "Preset_Max_Name/Text".GetLocalized() &&
                selectedItem.Description == "Preset_Max_Desc/Text".GetLocalized())
            {
                endMode = PresetType.Max;
            }
            else if (selectedItem.Text == "Preset_Speed_Name/Text".GetLocalized() &&
                     selectedItem.Description == "Preset_Speed_Desc/Text".GetLocalized())
            {
                endMode = PresetType.Speed;
            }
            else if (selectedItem.Text == "Preset_Balance_Name/Text".GetLocalized() &&
                     selectedItem.Description == "Preset_Balance_Desc/Text".GetLocalized())
            {
                endMode = PresetType.Balance;
            }
            else if (selectedItem.Text == "Preset_Eco_Name/Text".GetLocalized() &&
                     selectedItem.Description == "Preset_Eco_Desc/Text".GetLocalized())
            {
                endMode = PresetType.Eco;
            }
            else if (selectedItem.Text == "Preset_Min_Name/Text".GetLocalized() &&
                     selectedItem.Description == "Preset_Min_Desc/Text".GetLocalized())
            {
                endMode = PresetType.Min;
            }
            else
            {
                var name = selectedItem.Text;
                var desc = selectedItem.Description;
                var icon = selectedItem.IconGlyph;
                foreach (var preset in PresetManager.Presets)
                {
                    if (preset.Presetname == name &&
                        (preset.Presetdesc == desc || preset.Presetname == desc) &&
                        (preset.Preseticon == icon ||
                         preset.Preseticon == "\uE718"))
                    {
                        ПараметрыPage.ApplyInfo = string.Empty;
                        await Applyer.ApplyCustomPreset(preset, true);

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
                                    await LogHelper.Log("Apply_Success".GetLocalized());
                                    await Task.Delay(3000);
                                    ApplyTeach.IsOpen = false;
                                }

                                NotificationsService.Notifies ??= [];
                                NotificationsService.Notifies.Add(new Notify
                                {
                                    Title = ApplyTeach.Title,
                                    Msg = ApplyTeach.Subtitle + (applyInfo != string.Empty ? "DELETEUNAVAILABLE" : ""),
                                    Type = infoSet
                                });
                                NotificationsService.SaveNotificationsSettings();
                            }
                            catch (Exception ex)
                            {
                                await LogHelper.LogError(ex);
                            }
                        });
                        return;
                    }
                }
            }

            await Applyer.ApplyPremadePreset(endMode);

            ApplyTeach.Target = ApplyButton;
            ApplyTeach.Title = "Apply_Success".GetLocalized();
            ApplyTeach.Subtitle = "";
            ApplyTeach.IconSource = new SymbolIconSource { Symbol = Symbol.Accept };
            ApplyTeach.IsOpen = true;
            await LogHelper.Log("Apply_Success".GetLocalized());
            await Task.Delay(3000);
            ApplyTeach.IsOpen = false;
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

        AppSettings.PremadeOptimizationLevel = PremadeOptimizationLevel.SelectedIndex;
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

        AppSettings.PremadeCurveOptimizerOverrideLevel = (int)CurveOptimizerLevelSlider.Value;
        AppSettings.SaveSettings();
    }

    private void CurveOptimizerLevelCustom_Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_waitforload)
        {
            return;
        }

        
        var index = _indexpreset == -1 ? 0 : _indexpreset;

        PresetManager.Presets[index].Coallvalue = CurveOptimizerLevelCustomSlider.Value;
        PresetManager.SaveSettings();
    }

    private void CurveOptimizerCustom_Toggled(object sender, RoutedEventArgs e)
    {
        if (_waitforload)
        {
            return;
        }

        
        var index = _indexpreset == -1 ? 0 : _indexpreset;

        PresetManager.Presets[index].Coall = CurveOptimizerCustom.IsOn;
        PresetManager.SaveSettings();
    }

    private void StreamStabilizerModeCombo_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
    {
        if (!_isLoaded)
        {
            return;
        }

        AppSettings.StreamStabilizerType = StreamStabilizerModeCombo.SelectedIndex;

        switch (StreamStabilizerModeCombo.SelectedIndex)
        {
            case 1:
                StreamStabilizerTargetMHzGrid.Visibility = Visibility.Visible;
                StreamStabilizerTargetPercentOfMHzGrid.Visibility = Visibility.Collapsed;

                SetPowerConfig("PROCTHROTTLEMIN 100");
                SetPowerConfig("PROCTHROTTLEMAX 100");
                SetPowerConfig($"PROCFREQMAX {(int)StreamStabilizerTargetMhz.Value}");
                SavePowerConfig();

                AppSettings.StreamStabilizerMaxMHz = (int)StreamStabilizerTargetMhz.Value;

                break; // Target MHz
            case 2:
                StreamStabilizerTargetMHzGrid.Visibility = Visibility.Collapsed;
                StreamStabilizerTargetPercentOfMHzGrid.Visibility = Visibility.Visible;

                SetPowerConfig($"PROCTHROTTLEMIN {(int)StreamStabilizerTargetPercent.Value}");
                SetPowerConfig($"PROCTHROTTLEMAX {(int)StreamStabilizerTargetPercent.Value}");
                SetPowerConfig("PROCFREQMAX 0");
                SavePowerConfig();

                AppSettings.StreamStabilizerMaxPercentMHz = (int)StreamStabilizerTargetPercent.Value;

                break; // Target % of MHz
            default:
                StreamStabilizerTargetMHzGrid.Visibility = Visibility.Collapsed;
                StreamStabilizerTargetPercentOfMHzGrid.Visibility = Visibility.Collapsed;

                SetPowerConfig("PROCTHROTTLEMIN 99");
                SetPowerConfig("PROCTHROTTLEMAX 99");
                SetPowerConfig("PROCFREQMAX 0");
                SavePowerConfig();

                break; // Base lock
        }

        AppSettings.SaveSettings();
    }


    private void StreamStabilizerTargetMhz_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_isLoaded)
        {
            return;
        }

        SetPowerConfig("PROCTHROTTLEMIN 100");
        SetPowerConfig("PROCTHROTTLEMAX 100");
        SetPowerConfig($"PROCFREQMAX {(int)StreamStabilizerTargetMhz.Value}");
        SavePowerConfig();

        AppSettings.StreamStabilizerMaxMHz = (int)StreamStabilizerTargetMhz.Value;
        AppSettings.SaveSettings();
    }

    private void StreamStabilizerTargetPercent_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_isLoaded)
        {
            return;
        }

        SetPowerConfig($"PROCTHROTTLEMIN {(int)StreamStabilizerTargetPercent.Value}");
        SetPowerConfig($"PROCTHROTTLEMAX {(int)StreamStabilizerTargetPercent.Value}");
        SetPowerConfig("PROCFREQMAX 0");
        SavePowerConfig();

        AppSettings.StreamStabilizerMaxPercentMHz = (int)StreamStabilizerTargetPercent.Value;
        AppSettings.SaveSettings();
    }

    #region PowerConfig Helpers

    private static void SetPowerConfig(string arguments)
    {
        _ = Task.Run(() =>
        {
            using var process = new Process();
            process.StartInfo.FileName = "powercfg";
            process.StartInfo.Arguments = $"/SETACVALUEINDEX SCHEME_CURRENT SUB_PROCESSOR {arguments}";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.Verb = "runas";
            process.Start();
            process.WaitForExit();
        });
    }

    private static void SavePowerConfig()
    {
        _ = Task.Run(() =>
        {
            using var process = new Process();
            process.StartInfo.FileName = "powercfg";
            process.StartInfo.Arguments = "-S SCHEME_CURRENT";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.Verb = "runas";
            process.Start();
            process.WaitForExit();
        });
    }

    #endregion

    private void CurveOptimizerCustom_PointerPressed(object sender, PointerRoutedEventArgs e) =>
        CurveOptimizerCustom.IsOn = !CurveOptimizerCustom.IsOn;

    #region Advanced View Page Controllers

    #region Sliders

    //Параметры процессора, при изменении слайдеров
    //Максимальная температура CPU (C)
    private void C1_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        
        if (_indexpreset != -1)
        {
            PresetManager.Presets[_indexpreset].Cpu1Value = C1V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Лимит CPU (W)
    private void C2_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        
        if (_indexpreset != -1)
        {
            PresetManager.Presets[_indexpreset].Cpu2Value = C2V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Реальный CPU (W)
    private void C3_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        
        if (_indexpreset != -1)
        {
            PresetManager.Presets[_indexpreset].Cpu3Value = C3V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Средний CPU(W)
    private void C4_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        
        if (_indexpreset != -1)
        {
            PresetManager.Presets[_indexpreset].Cpu4Value = C4V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Тик быстрого разгона (S)
    private void C5_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        
        if (_indexpreset != -1)
        {
            PresetManager.Presets[_indexpreset].Cpu5Value = C5V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Тик медленного разгона (S)
    private void C6_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        
        if (_indexpreset != -1)
        {
            PresetManager.Presets[_indexpreset].Cpu6Value = C6V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Параметры VRM
    private void V1v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        
        if (_indexpreset != -1)
        {
            PresetManager.Presets[_indexpreset].Vrm1Value = V1V.Value;
            PresetManager.SaveSettings();
        }
    }

    private void V2v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        
        if (_indexpreset != -1)
        {
            PresetManager.Presets[_indexpreset].Vrm2Value = V2V.Value;
            PresetManager.SaveSettings();
        }
    }

    private void V3v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        
        if (_indexpreset != -1)
        {
            PresetManager.Presets[_indexpreset].Vrm3Value = V3V.Value;
            PresetManager.SaveSettings();
        }
    }

    private void V4v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        
        if (_indexpreset != -1)
        {
            PresetManager.Presets[_indexpreset].Vrm4Value = V4V.Value;
            PresetManager.SaveSettings();
        }
    }

    private void G9v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        
        if (_indexpreset != -1)
        {
            PresetManager.Presets[_indexpreset].Gpu9Value = G9V.Value;
            PresetManager.SaveSettings();
        }
    }

    private void G10v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        
        if (_indexpreset != -1)
        {
            PresetManager.Presets[_indexpreset].Gpu10Value = G10V.Value;
            PresetManager.SaveSettings();
        }
    }

    #endregion

    #region CheckBoxes

    //Параметры процессора
    //Максимальная температура CPU (C)
    private void C1_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        
        var check = C1.IsChecked == true;
        if (_indexpreset != -1)
        {
            PresetManager.Presets[_indexpreset].Cpu1 = check;
            PresetManager.Presets[_indexpreset].Cpu1Value = C1V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Лимит CPU (W)
    private void C2_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        
        var check = C2.IsChecked == true;
        if (_indexpreset != -1)
        {
            PresetManager.Presets[_indexpreset].Cpu2 = check;
            PresetManager.Presets[_indexpreset].Cpu2Value = C2V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Реальный CPU (W)
    private void C3_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        
        var check = C3.IsChecked == true;
        if (_indexpreset != -1)
        {
            PresetManager.Presets[_indexpreset].Cpu3 = check;
            PresetManager.Presets[_indexpreset].Cpu3Value = C3V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Средний CPU (W)
    private void C4_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        
        var check = C4.IsChecked == true;
        if (_indexpreset != -1)
        {
            PresetManager.Presets[_indexpreset].Cpu4 = check;
            PresetManager.Presets[_indexpreset].Cpu4Value = C4V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Тик быстрого разгона (S)
    private void C5_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        
        var check = C5.IsChecked == true;
        if (_indexpreset != -1)
        {
            PresetManager.Presets[_indexpreset].Cpu5 = check;
            PresetManager.Presets[_indexpreset].Cpu5Value = C5V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Тик медленного разгона (S)
    private void C6_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        
        var check = C6.IsChecked == true;
        if (_indexpreset != -1)
        {
            PresetManager.Presets[_indexpreset].Cpu6 = check;
            PresetManager.Presets[_indexpreset].Cpu6Value = C6V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Параметры VRM
    //Максимальный ток VRM A
    private void V1_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        
        var check = V1.IsChecked == true;
        if (_indexpreset != -1)
        {
            PresetManager.Presets[_indexpreset].Vrm1 = check;
            PresetManager.Presets[_indexpreset].Vrm1Value = V1V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Лимит по току VRM A
    private void V2_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        
        var check = V2.IsChecked == true;
        if (_indexpreset != -1)
        {
            PresetManager.Presets[_indexpreset].Vrm2 = check;
            PresetManager.Presets[_indexpreset].Vrm2Value = V2V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Максимальный ток SOC A
    private void V3_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        
        var check = V3.IsChecked == true;
        if (_indexpreset != -1)
        {
            PresetManager.Presets[_indexpreset].Vrm3 = check;
            PresetManager.Presets[_indexpreset].Vrm3Value = V3V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Лимит по току SOC A
    private void V4_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        
        var check = V4.IsChecked == true;
        if (_indexpreset != -1)
        {
            PresetManager.Presets[_indexpreset].Vrm4 = check;
            PresetManager.Presets[_indexpreset].Vrm4Value = V4V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Минимальная частота iGpu
    private void G9_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        
        var check = G9.IsChecked == true;
        if (_indexpreset != -1)
        {
            PresetManager.Presets[_indexpreset].Gpu9 = check;
            PresetManager.Presets[_indexpreset].Gpu9Value = G9V.Value;
            PresetManager.SaveSettings();
        }
    }

    //Максимальная частота iGpu
    private void G10_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        
        var check = G10.IsChecked == true;
        if (_indexpreset != -1)
        {
            PresetManager.Presets[_indexpreset].Gpu10 = check;
            PresetManager.Presets[_indexpreset].Gpu10Value = G10V.Value;
            PresetManager.SaveSettings();
        }
    }

    #endregion

    #region NumberBoxes

    private void TargetNumberBox_FocusEngaged(object sender, object args)
    {
        if (sender is NumberBox numberBox)
        {
            numberBox.SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden;
        }
    }

    private void TargetNumberBox_FocusDisengaged(object sender, object args)
    {
        if (sender is NumberBox numberBox)
        {
            numberBox.SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline;
        }
    }

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