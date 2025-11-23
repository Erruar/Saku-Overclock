using System.Diagnostics;
using System.Numerics;
using Windows.Foundation.Metadata;
using Windows.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.JsonContainers;
using Saku_Overclock.Services;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.Styles;
using Saku_Overclock.ViewModels;
using static ZenStates.Core.Cpu;
using Task = System.Threading.Tasks.Task;

namespace Saku_Overclock.Views;

public sealed partial class ПресетыPage
{
    private static readonly IAppSettingsService AppSettings = App.GetService<IAppSettingsService>();
    private static readonly IApplyerService Applyer = App.GetService<IApplyerService>();
    private static readonly IOcFinderService OcFinder = App.GetService<IOcFinderService>();
    private bool _isLoaded; // Загружена ли корректно страница для применения изменений 
    private bool _waitforload = true; // Ожидание окончательной смены профиля на другой. Активируется при смене профиля 
    private static Profile[] _profile = new Profile[1]; // Всегда по умолчанию будет 1 профиль
    private int _indexprofile; // Выбранный профиль
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
        SelectedProfileDescription.Text = "Preset_Min_Desc/Text".GetLocalized();

        try
        {
            _isPlatformPc = SendSmuCommand.IsPlatformPc();
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex);
        }

        _ = LoadProfiles();

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

        if (AppSettings.ProfilespageViewModeBeginner)
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

    private async Task LoadProfiles()
    {
        // Загрузить профили перед началом работы с ними
        ProfileLoad();

        // Очистить элементы ProfilesControl
        ProfilesControl.Items.Clear();

        // Пройтись по каждому профилю и добавить их в ProfilesControl
        foreach (var profile in _profile)
        {
            var isChecked = AppSettings.Preset != -1 &&
                            _profile[AppSettings.Preset].Profilename == profile.Profilename &&
                            _profile[AppSettings.Preset].Profiledesc == profile.Profiledesc &&
                            _profile[AppSettings.Preset].Profileicon == profile.Profileicon;


            var toggleButton = new ProfileItem
            {
                IsSelected = isChecked,
                IconGlyph = profile.Profileicon == string.Empty ? "\uE718" : profile.Profileicon,
                Text = profile.Profilename,
                Description = profile.Profiledesc != string.Empty ? profile.Profiledesc : profile.Profilename
            };
            ProfilesControl.Items.Add(toggleButton);
        }


        // Готовые Пресеты
        ProfilesControl.Items.Add(new ProfileItem
        {
            IsSelected = AppSettings is { Preset: -1, PremadeMaxActivated: true },
            IconGlyph = "\uEcad",
            Text = "Preset_Max_Name/Text".GetLocalized(), // Maximum
            Description = "Preset_Max_Desc/Text".GetLocalized()
        });
        ProfilesControl.Items.Add(new ProfileItem
        {
            IsSelected = AppSettings is { Preset: -1, PremadeSpeedActivated: true },
            IconGlyph = "\ue945",
            Text = "Preset_Speed_Name/Text".GetLocalized(), // Speed
            Description = "Preset_Speed_Desc/Text".GetLocalized()
        });
        ProfilesControl.Items.Add(new ProfileItem
        {
            IsSelected = AppSettings is { Preset: -1, PremadeBalanceActivated: true },
            IconGlyph = "\uec49",
            Text = "Preset_Balance_Name/Text".GetLocalized(), // Balance
            Description = "Preset_Balance_Desc/Text".GetLocalized()
        });
        ProfilesControl.Items.Add(new ProfileItem
        {
            IsSelected = AppSettings is { Preset: -1, PremadeEcoActivated: true },
            IconGlyph = "\uec0a",
            Text = "Preset_Eco_Name/Text".GetLocalized(), // Eco
            Description = "Preset_Eco_Desc/Text".GetLocalized()
        });
        ProfilesControl.Items.Add(new ProfileItem
        {
            IsSelected = AppSettings is { Preset: -1, PremadeMinActivated: true },
            IconGlyph = "\uebc0",
            Text = "Preset_Min_Name/Text".GetLocalized(), // Minimum
            Description = "Preset_Min_Desc/Text".GetLocalized()
        });

        // Workaround чтобы все элементы корректно загрузились в ProfilesControl
        ProfilesControl.UpdateView();

        foreach (var item in ProfilesControl.Items)
        {
            if (item.IsSelected)
            {
                SelectedProfileName.Text = item.Text;

                // Обход отсутствия описания, при помощи записывания имени пресета в описание. Чтобы не отображать два раза одну и ту же строку, описание пресета скрывается (так как его нет)
                SelectedProfileDescription.Text = item.Description != item.Text ? item.Description : string.Empty;
                if (item.Description == item.Text)
                {
                    SelectedProfileDescription.Visibility = Visibility.Collapsed;
                    EditCurrentButtonsStackPanel.Margin = new Thickness(0, 0, -13, -10);
                    EditCurrentButtonsStackPanel.VerticalAlignment = VerticalAlignment.Top;
                    SelectedProfileTextsStackPanel.VerticalAlignment = VerticalAlignment.Center;
                }
                else
                {
                    SelectedProfileDescription.Visibility = Visibility.Visible;
                    EditCurrentButtonsStackPanel.Margin = new Thickness(0, 17, -13, -10);
                    EditCurrentButtonsStackPanel.VerticalAlignment = VerticalAlignment.Center;
                    SelectedProfileTextsStackPanel.VerticalAlignment = VerticalAlignment.Top;
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
                    ProfileSettingsStackPanel.Visibility = Visibility.Collapsed;
                    ProfileSettingsBeginnerView.Visibility = Visibility.Visible;
                    ProfileSettingsBeginnerView.Margin = new Thickness(0, -5, 0, 0);
                    PremadeProfileAffectsOn.Visibility = Visibility.Visible;
                    ProfileSettingsBeginnerViewSettingsStackPanel.Visibility = Visibility.Collapsed;
                    EditProfileButton.Visibility = Visibility.Collapsed;
                    ProfileSettingsChangeViewStackPanel.Visibility = Visibility.Collapsed;

                    var optimizationLevel = PremadeOptimizationLevel.SelectedIndex switch
                    {
                        0 => OptimizationLevel.Basic,
                        1 => OptimizationLevel.Standard,
                        2 => OptimizationLevel.Deep,
                        _ => OptimizationLevel.Basic
                    };

                    PresetMetrics metrics;

                    if (item.Text == "Preset_Max_Name/Text".GetLocalized())
                    {
                        metrics = OcFinder.GetPresetMetrics(PresetType.Max, optimizationLevel);
                        await HelpWithShowPreset(PresetType.Max);
                    }
                    else if (item.Text == "Preset_Speed_Name/Text".GetLocalized())
                    {
                        metrics = OcFinder.GetPresetMetrics(PresetType.Performance, optimizationLevel);
                        await HelpWithShowPreset(PresetType.Performance);
                    }
                    else if (item.Text == "Preset_Balance_Name/Text".GetLocalized())
                    {
                        metrics = OcFinder.GetPresetMetrics(PresetType.Balance, optimizationLevel);
                        await HelpWithShowPreset(PresetType.Balance);
                    }
                    else if (item.Text == "Preset_Eco_Name/Text".GetLocalized())
                    {
                        metrics = OcFinder.GetPresetMetrics(PresetType.Eco, optimizationLevel);
                        await HelpWithShowPreset(PresetType.Eco);
                    }
                    else
                    {
                        metrics = OcFinder.GetPresetMetrics(PresetType.Min, optimizationLevel);
                        await HelpWithShowPreset(PresetType.Min);
                    }

                    PresetPerformanceBar.Value = 50 + metrics.PerformanceScore;
                    PresetEnergyEfficiencyBar.Value = 50 + metrics.EfficiencyScore;
                    PresetTemperaturesBar.Value = 50 + metrics.ThermalScore;

                    /*Preset_Options_Temp.Text = options.ThermalOptions;
                    Preset_Options_Power.Text = options.PowerOptions;
                    Preset_Options_Currents.Text = options.CurrentOptions;*/
                }
                else
                {
                    ProfileSettingsBeginnerViewSettingsStackPanel.Visibility = Visibility.Visible;
                    ProfileSettingsBeginnerView.Margin = new Thickness(0, 0, 0, 0);
                    if (AppSettings.ProfilespageViewModeBeginner)
                    {
                        BeginnerOptionsButton.IsChecked = true;
                        AdvancedOptionsButton.IsChecked = false;
                        ProfileSettingsStackPanel.Visibility = Visibility.Collapsed;
                        ProfileSettingsChangeViewStackPanel.Visibility = Visibility.Visible;
                        ProfileSettingsBeginnerView.Visibility = Visibility.Visible;
                        PremadeProfileAffectsOn.Visibility = Visibility.Collapsed;
                        EditProfileButton.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        BeginnerOptionsButton.IsChecked = false;
                        AdvancedOptionsButton.IsChecked = true;
                        ProfileSettingsStackPanel.Visibility = Visibility.Visible;
                        ProfileSettingsBeginnerView.Visibility = Visibility.Collapsed;
                        ProfileSettingsChangeViewStackPanel.Visibility = Visibility.Visible;
                        PremadeProfileAffectsOn.Visibility = Visibility.Collapsed;
                        EditProfileButton.Visibility = Visibility.Visible;
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
            case PresetType.Performance:
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

        ProfileLoad();
        try
        {
            if (_profile[index].Cpu1Value > C1V.Maximum)
            {
                C1V.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].Cpu1Value);
            }

            if (_profile[index].Cpu2Value > BaseTdpSlider.Maximum)
            {
                BaseTdpSlider.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].Cpu2Value);
            }

            if (_profile[index].Cpu2Value > C2V.Maximum)
            {
                C2V.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].Cpu2Value);
            }

            if (_profile[index].Cpu3Value > C3V.Maximum)
            {
                C3V.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].Cpu3Value);
            }

            if (_profile[index].Cpu4Value > C4V.Maximum)
            {
                C4V.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].Cpu4Value);
            }

            if (_profile[index].Cpu5Value > C5V.Maximum)
            {
                C5V.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].Cpu5Value);
            }

            if (_profile[index].Cpu6Value > C6V.Maximum)
            {
                C6V.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].Cpu6Value);
            }

            if (_profile[index].Vrm1Value > V1V.Maximum)
            {
                V1V.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].Vrm1Value);
            }

            if (_profile[index].Vrm2Value > V2V.Maximum)
            {
                V2V.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].Vrm2Value);
            }

            if (_profile[index].Vrm3Value > V3V.Maximum)
            {
                V3V.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].Vrm3Value);
            }

            if (_profile[index].Vrm4Value > V4V.Maximum)
            {
                V4V.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].Vrm4Value);
            }

            if (_profile[index].Gpu9Value > G9V.Maximum)
            {
                G9V.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].Gpu9Value);
            }

            if (_profile[index].Gpu10Value > G10V.Maximum)
            {
                G10V.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].Gpu10Value);
            }
        }
        catch (Exception ex)
        {
            await LogHelper.TraceIt_TraceError(ex);
        }

        try
        {
            // Заранее скомпилированная функция увеличения TDP, созданная специально для фирменной функции Smart TDP
            var fineTunedTdp = ПараметрыPage.FromValueToUpperFive(1.17335141 * _profile[index].Cpu2Value + 0.21631949);

            C2.IsChecked = _profile[index].Cpu2;
            C2V.Value = _profile[index].Cpu2Value;

            BaseTdpSlider.Value = _profile[index].Cpu2Value;
            if (_profile[index].Cpu2 && _profile[index].Cpu3 && _profile[index].Cpu4 &&
                (int)_profile[index].Cpu2Value == (int)_profile[index].Cpu4Value &&
                (int)_profile[index].Cpu3Value == fineTunedTdp)
            {
                SmartTdp.IsOn = true;
            }
            else
            {
                if (_isPlatformPc != false && SettingsViewModel.VersionId != 5) // Если устройство - не ноутбук
                {
                    // Так как на компьютерах невозможно выставить другие Power лимиты
                    if (!_profile[index].Cpu2 && !_profile[index].Cpu4 &&
                        (int)_profile[index].Cpu3Value == fineTunedTdp)
                    {
                        SmartTdp.IsOn = true;
                    }
                }
                else
                {
                    SmartTdp.IsOn = false;
                }
            }

            C1.IsChecked = _profile[index].Cpu1;
            C1V.Value = _profile[index].Cpu1Value;
            C3.IsChecked = _profile[index].Cpu3;
            C3V.Value = _profile[index].Cpu3Value;
            C4.IsChecked = _profile[index].Cpu4;
            C4V.Value = _profile[index].Cpu4Value;
            C5.IsChecked = _profile[index].Cpu5;
            C5V.Value = _profile[index].Cpu5Value;
            C6.IsChecked = _profile[index].Cpu6;
            C6V.Value = _profile[index].Cpu6Value;


            if (IsRavenFamily())
            {
                if (_profile[index].Gpu10 && _profile[index].Gpu9 && (int)_profile[index].Gpu10Value == 1200)
                {
                    if ((int)_profile[index].Gpu9Value == 800)
                    {
                        IntegratedGpuEnchantmentCombo.SelectedIndex = 1;
                    }

                    if ((int)_profile[index].Gpu9Value == 1000)
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
                if (_profile[index].Advncd10)
                {
                    if ((int)_profile[index].Advncd10Value == 1750)
                    {
                        IntegratedGpuEnchantmentCombo.SelectedIndex = 1;
                    }

                    if ((int)_profile[index].Advncd10Value == 2200)
                    {
                        IntegratedGpuEnchantmentCombo.SelectedIndex = 2;
                    }
                }
                else
                {
                    IntegratedGpuEnchantmentCombo.SelectedIndex = 0;
                }
            }

            if (!_profile[index].Cpu5 && !_profile[index].Cpu6)
            {
                TurboSetOnly(TurboLightModeToggle);
            }
            else
            {
                if ((_profile[index].Cpu5 && !_profile[index].Cpu6) || (!_profile[index].Cpu5 && _profile[index].Cpu6))
                {
                    TurboSetOnly(TurboLightModeToggle);
                }

                if (_profile[index].Cpu5 && _profile[index].Cpu6)
                {
                    if ((int)_profile[index].Cpu5Value == 400 && (int)_profile[index].Cpu6Value == 3)
                    {
                        TurboSetOnly(TurboBalanceModeToggle);
                    }
                    else if ((int)_profile[index].Cpu5Value == 5000 && (int)_profile[index].Cpu6Value == 1)
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

            if (_profile[index].Coall)
            {
                CurveOptimizerCustom.IsOn = true;
                CurveOptimizerLevelCustomSlider.Value = _profile[index].Coallvalue;
            }
            else
            {
                CurveOptimizerCustom.IsOn = false;
            }

            V1.IsChecked = _profile[index].Vrm1;
            V1V.Value = _profile[index].Vrm1Value;
            V2.IsChecked = _profile[index].Vrm2;
            V2V.Value = _profile[index].Vrm2Value;
            V3.IsChecked = _profile[index].Vrm3;
            V3V.Value = _profile[index].Vrm3Value;
            V4.IsChecked = _profile[index].Vrm4;
            V4V.Value = _profile[index].Vrm4Value;
            G9V.Value = _profile[index].Gpu9Value;
            G9.IsChecked = _profile[index].Gpu9;
            G10V.Value = _profile[index].Gpu10Value;
            G10.IsChecked = _profile[index].Gpu10;
        }
        catch
        {
            await LogHelper.LogError("Profile contains error. Creating new profile.");

            _profile = new Profile[1];
            _profile[0] = new Profile();
            ProfileSave();
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

            if (info.ApuTemperature == 0)
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
                GpuTempSensorText.Text = Math.Round(info.ApuTemperature) + "C";
            }

            CpuTempSensorText.Text = Math.Round(info.CpuTempValue) + (updateSmallSign ? "C" : string.Empty);
        });
    }

    #endregion

    #region JSON voids

    private static void ProfileSave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                "SakuOverclock"));
            File.WriteAllText(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\profile.json",
                JsonConvert.SerializeObject(_profile, Formatting.Indented));
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex);
        }
    }

    private static void ProfileLoad()
    {
        try
        {
            _profile = JsonConvert.DeserializeObject<Profile[]>(File.ReadAllText(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\profile.json"))!;
        }
        catch (Exception ex)
        {
            JsonRepair('p');
            LogHelper.TraceIt_TraceError(ex);
        }
    }

    private static void JsonRepair(char file)
    {
        switch (file)
        {
            case 'p':
                _profile = [];
                try
                {
                    Directory.CreateDirectory(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(
                        Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\profile.json",
                        JsonConvert.SerializeObject(_profile));
                }
                catch
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                                @"\SakuOverclock\profile.json");
                    Directory.CreateDirectory(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(
                        Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\profile.json",
                        JsonConvert.SerializeObject(_profile));
                }

                break;
        }
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
        if (AppSettings.ProfilespageViewModeBeginner && BeginnerOptionsButton.IsChecked == false)
        {
            BeginnerOptionsButton.IsChecked = true;
            return;
        }
        
        if (!AppSettings.ProfilespageViewModeBeginner && AdvancedOptionsButton.IsChecked == false)
        {
            AdvancedOptionsButton.IsChecked = true;
            return;
        }
        
        AppSettings.ProfilespageViewModeBeginner = !AppSettings.ProfilespageViewModeBeginner;
        AppSettings.SaveSettings();
        if (AppSettings.ProfilespageViewModeBeginner)
        {
            BeginnerOptionsButton.IsChecked = true;
            AdvancedOptionsButton.IsChecked = false;
            ProfileSettingsStackPanel.Visibility = Visibility.Collapsed;
            ProfileSettingsBeginnerView.Visibility = Visibility.Visible;
            PremadeProfileAffectsOn.Visibility = Visibility.Collapsed;
            EditProfileButton.Visibility = Visibility.Visible;
            ProfileSettingsChangeViewStackPanel.Visibility = Visibility.Visible;
        }
        else
        {
            BeginnerOptionsButton.IsChecked = false;
            AdvancedOptionsButton.IsChecked = true;
            ProfileSettingsStackPanel.Visibility = Visibility.Visible;
            ProfileSettingsBeginnerView.Visibility = Visibility.Collapsed;
            PremadeProfileAffectsOn.Visibility = Visibility.Collapsed;
            EditProfileButton.Visibility = Visibility.Visible;
            ProfileSettingsChangeViewStackPanel.Visibility = Visibility.Visible;
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

        ProfileLoad();
        var index = _indexprofile == -1 ? 0 : _indexprofile;

        // Заранее скомпилированная функция увеличения TDP, созданная специально для фирменной функции Smart TDP
        var fineTunedTdp = ПараметрыPage.FromValueToUpperFive(1.17335141 * BaseTdpSlider.Value + 0.21631949);

        C2.IsChecked = true;
        _profile[index].Cpu2 = true;
        C3.IsChecked = true;
        _profile[index].Cpu3 = true;
        C4.IsChecked = true;
        _profile[index].Cpu4 = true;

        if (fineTunedTdp > C3V.Maximum || BaseTdpSlider.Value > C2V.Maximum || BaseTdpSlider.Value > C4V.Maximum)
        {
            C2V.Maximum = ПараметрыPage.FromValueToUpperFive(BaseTdpSlider.Value);
            C3V.Maximum = ПараметрыPage.FromValueToUpperFive(fineTunedTdp);
            C4V.Maximum = ПараметрыPage.FromValueToUpperFive(BaseTdpSlider.Value);
        }

        C2V.Value = BaseTdpSlider.Value;
        _profile[index].Cpu2Value = BaseTdpSlider.Value;

        C4V.Value = BaseTdpSlider.Value;
        _profile[index].Cpu4Value = BaseTdpSlider.Value;

        if (SmartTdp.IsOn)
        {
            C3V.Value = fineTunedTdp;
            _profile[index].Cpu3Value = fineTunedTdp;
        }
        else
        {
            C3V.Value = BaseTdpSlider.Value;
            _profile[index].Cpu3Value = BaseTdpSlider.Value;
        }

        if (_isPlatformPc != false && SettingsViewModel.VersionId != 5) // Если устройство - не ноутбук
        {
            // Так как на компьютерах невозможно выставить другие Power лимиты
            C2.IsChecked = false; // Отключить STAPM
            _profile[index].Cpu2 = false;
            C4.IsChecked = false; // Отключить Slow лимит
            _profile[index].Cpu4 = false;
        }

        ProfileSave();
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

            ProfileLoad();

            if (toggle!.Name == "TurboLightModeToggle")
            {
                TurboSetOnly(TurboLightModeToggle);
                _profile[index].Cpu5 = false;
                _profile[index].Cpu6 = false;
                C5.IsChecked = false;
                C6.IsChecked = false;
            }
            else if (toggle.Name == "TurboBalanceModeToggle")
            {
                TurboSetOnly(TurboBalanceModeToggle);
                _profile[index].Cpu5 = true;
                _profile[index].Cpu6 = true;
                _profile[index].Cpu5Value = 400;
                _profile[index].Cpu6Value = 3;
                C5.IsChecked = true;
                C6.IsChecked = true;
                C5V.Value = 400;
                C6V.Value = 3;
            }
            else if (toggle.Name == "TurboHeavyModeToggle")
            {
                TurboSetOnly(TurboHeavyModeToggle);
                _profile[index].Cpu5 = true;
                _profile[index].Cpu6 = true;
                _profile[index].Cpu5Value = 5000;
                _profile[index].Cpu6Value = 1;
                C5.IsChecked = true;
                C6.IsChecked = true;
                C5V.Maximum = 5000;
                C5V.Value = 5000;
                C6V.Value = 1;
            }
        }

        ProfileSave();
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
                    _profile[index].Gpu10 = false;
                    _profile[index].Gpu9 = false;
                }
                else
                {
                    _profile[index].Advncd10 = false;
                }

                break;
            case 1:
                if (IsRavenFamily())
                {
                    _profile[index].Gpu10 = true;
                    _profile[index].Gpu10Value = 1200;
                    _profile[index].Gpu9 = true;
                    _profile[index].Gpu9Value = 800;
                }
                else
                {
                    _profile[index].Advncd10 = true;
                    _profile[index].Advncd10Value = 1750;
                }

                break;
            case 2:
                if (IsRavenFamily())
                {
                    _profile[index].Gpu10 = true;
                    _profile[index].Gpu10Value = 1200;
                    _profile[index].Gpu9 = true;
                    _profile[index].Gpu9Value = 1000;
                }
                else
                {
                    _profile[index].Advncd10 = true;
                    _profile[index].Advncd10Value = 2200;
                }

                break;
        }

        ProfileSave();
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

    #region Profile Management

    private async void AddProfileButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await OpenAddProfileDialogAsync();
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception);
        }
    }

    private async Task OpenAddProfileDialogAsync()
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
            PlaceholderText = "Param_Profile_New_Name_Add/PlaceholderText".GetLocalized(),
            CornerRadius = new CornerRadius(9),
            Width = 250,
            Shadow = (ThemeShadow)Resources["SharedShadow"],
            Translation = new Vector3(0, 0, 10)
        };

        var descBox = new TextBox
        {
            PlaceholderText = "Param_Profile_New_Desc_Add/PlaceholderText".GetLocalized(),
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
            Title = "Param_Profile_New_Name/Content".GetLocalized(),
            XamlRoot = XamlRoot,
            CloseButtonText = "CancelThis/Text".GetLocalized(),
            PrimaryButtonText = "Param_Profile_New_Name/Content".GetLocalized(),
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
            await LogHelper.Log($"Adding new profile: \"{presetName}\"");
            ProfileLoad();
            try
            {
                AppSettings.Preset += 1;
                _indexprofile += 1;
                _waitforload = true;
                if (_profile.Length == 0)
                {
                    _profile = new Profile[1];
                    _profile[0] = new Profile
                        { Profilename = presetName, Profiledesc = presetDesc, Profileicon = glyph };
                }
                else
                {
                    var profileList = new List<Profile>(_profile)
                    {
                        new()
                        {
                            Profilename = presetName,
                            Profiledesc = presetDesc,
                            Profileicon = glyph
                        }
                    };
                    _profile = [.. profileList];
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
        ProfileSave();
        await LoadProfiles();
    }

    private async void EditProfileButton_Click(string profileName, string profileDesc, string glyph)
    {
        try
        {
            await LogHelper.Log(
                $"Editing profile name: From \"{_profile[_indexprofile].Profilename}\" To \"{profileName}\"");
            if (profileName != "")
            {
                ProfileLoad();
                _profile[_indexprofile].Profilename = profileName;
                _profile[_indexprofile].Profiledesc = profileDesc;
                _profile[_indexprofile].Profileicon = glyph;
                ProfileSave();
                _waitforload = true;
                await LoadProfiles();
                _waitforload = false;
                NotificationsService.Notifies ??= [];
                NotificationsService.Notifies.Add(new Notify
                {
                    Title = "Edit_Target/Title".GetLocalized(),
                    Msg = "Edit_Target/Subtitle".GetLocalized() + " " + profileName,
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

    private async void DeleteProfileButton_Click()
    {
        try
        {
            await LogHelper.Log("Showing delete profile dialog");
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
                var indexprofile = AppSettings.Preset > -1 ? AppSettings.Preset : 0;

                await LogHelper.Log(
                    $"Showing delete profile dialog: deleting profile \"{_profile[indexprofile].Profilename}\"");

                ProfileLoad();

                _waitforload = true;

                var profileList = new List<Profile>(_profile);
                profileList.RemoveAt(indexprofile);
                _profile = [.. profileList];

                _waitforload = false;

                AppSettings.Preset = _profile.Length > 0 ? 0 : -1;
                _indexprofile = AppSettings.Preset;


                NotificationsService.Notifies ??= [];
                NotificationsService.Notifies.Add(new Notify
                {
                    Title = "DeleteSuccessTitle".GetLocalized(),
                    Msg = "DeleteSuccessDesc".GetLocalized(),
                    Type = InfoBarSeverity.Success
                });
                NotificationsService.SaveNotificationsSettings();

                ProfileSave();
                await LoadProfiles();
            }
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception);
        }
    }

    private async void EditProfileButton_Click_1(object sender, RoutedEventArgs e)
    {
        try
        {
            await OpenEditProfileDialog_Click(_profile[_indexprofile].Profilename, _profile[_indexprofile].Profiledesc,
                _profile[_indexprofile].Profileicon);
        }
        catch (Exception ex)
        {
            await LogHelper.LogWarn(ex);
        }
    }

    private async Task OpenEditProfileDialog_Click(string profileName, string profileDesc, string profileIcon)
    {
        // Создаём элементы интерфейса
        var glyph = new FontIcon { Glyph = profileIcon };
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
            Text = profileName,
            MaxLength = 31,
            PlaceholderText = "Param_Profile_New_Name_Add/PlaceholderText".GetLocalized(),
            CornerRadius = new CornerRadius(9),
            Width = 280,
            Shadow = (ThemeShadow)Resources["SharedShadow"],
            Translation = new Vector3(0, 0, 10)
        };

        var descBox = new TextBox
        {
            Text = profileDesc,
            PlaceholderText = "Param_Profile_New_Desc_Add/PlaceholderText".GetLocalized(),
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
            Title = "Param_Profile_Edit_Name/Content".GetLocalized(),
            XamlRoot = XamlRoot,
            PrimaryButtonText = "Param_Profile_Edit_Name/Content".GetLocalized(),
            SecondaryButtonText = "Param_Profile_Delete_Profile/Content".GetLocalized(),
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

            profileIcon = selectedGlyph.Glyph;
            glyph.Glyph = profileIcon;
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
            EditProfileButton_Click(nameBox.Text, descBox.Text, profileIcon);
        }
        else if (result == ContentDialogResult.Secondary)
        {
            DeleteProfileButton_Click();
        }
    }

    #endregion

    #endregion

    #endregion

    #region Profile Settings Events

    private async void ProfilesControl_SelectionChanged(object sender, SelectionChangedEventArgs? e)
    {
        try
        {
            var selectedItem = (sender as ProfileSelector)?.SelectedItem;

            if (selectedItem == null)
            {
                return;
            }

            // Корректное отображение описания, даже если оно маленькое (чтобы Grid изменил свой размер корректно и слова не обрывались)
            ProfileSettingsInfoRow.Height = new GridLength(0);
            ProfileSettingsInfoRow.Height = GridLength.Auto;

            SelectedProfileName.Text = selectedItem.Text;

            // Обход отсутствия описания, при помощи записывания имени пресета в описание. Чтобы не отображать два раза одну и ту же строку, описание пресета скрывается (так как его нет)
            SelectedProfileDescription.Text = selectedItem.Description != selectedItem.Text
                ? selectedItem.Description
                : string.Empty;

            if (e != null)
            {
                if (_doubleClickApply == SelectedProfileName.Text + SelectedProfileDescription.Text)
                {
                    ApplyButton_Click(null, null);
                }

                _doubleClickApply = SelectedProfileName.Text + SelectedProfileDescription.Text;
            }

            if (selectedItem.Description == selectedItem.Text)
            {
                SelectedProfileDescription.Visibility = Visibility.Collapsed;
                EditCurrentButtonsStackPanel.Margin = new Thickness(0, 0, -13, -10);
                EditCurrentButtonsStackPanel.VerticalAlignment = VerticalAlignment.Top;
                SelectedProfileTextsStackPanel.VerticalAlignment = VerticalAlignment.Center;
            }
            else
            {
                SelectedProfileDescription.Visibility = Visibility.Visible;
                EditCurrentButtonsStackPanel.Margin = new Thickness(0, 17, -13, -10);
                EditCurrentButtonsStackPanel.VerticalAlignment = VerticalAlignment.Center;
                SelectedProfileTextsStackPanel.VerticalAlignment = VerticalAlignment.Top;
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
                    preset = OcFinder.CreatePreset(PresetType.Performance, optimizationLevel);
                    await HelpWithShowPreset(PresetType.Performance);
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

                ProfileSettingsStackPanel.Visibility = Visibility.Collapsed;
                ProfileSettingsBeginnerView.Visibility = Visibility.Visible;
                ProfileSettingsBeginnerView.Margin = new Thickness(0, -5, 0, 0);
                PremadeProfileAffectsOn.Visibility = Visibility.Visible;
                ProfileSettingsBeginnerViewSettingsStackPanel.Visibility = Visibility.Collapsed;
                EditProfileButton.Visibility = Visibility.Collapsed;
                ProfileSettingsChangeViewStackPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                ProfileSettingsBeginnerViewSettingsStackPanel.Visibility = Visibility.Visible;
                ProfileSettingsBeginnerView.Margin = new Thickness(0, 0, 0, 0);

                if (AppSettings.ProfilespageViewModeBeginner)
                {
                    BeginnerOptionsButton.IsChecked = true;
                    AdvancedOptionsButton.IsChecked = false;
                    ProfileSettingsStackPanel.Visibility = Visibility.Collapsed;
                    ProfileSettingsBeginnerView.Visibility = Visibility.Visible;
                }
                else
                {
                    BeginnerOptionsButton.IsChecked = false;
                    AdvancedOptionsButton.IsChecked = true;
                    ProfileSettingsStackPanel.Visibility = Visibility.Visible;
                    ProfileSettingsBeginnerView.Visibility = Visibility.Collapsed;
                }

                PremadeProfileAffectsOn.Visibility = Visibility.Collapsed;
                EditProfileButton.Visibility = Visibility.Visible;
                ProfileSettingsChangeViewStackPanel.Visibility = Visibility.Visible;

                for (var i = 0; i < _profile.Length; i++)
                {
                    if ((_profile[i].Profiledesc == selectedItem.Description ||
                         _profile[i].Profilename == selectedItem.Description) &&
                        _profile[i].Profilename == selectedItem.Text &&
                        _profile[i].Profileicon == selectedItem.IconGlyph)
                    {
                        _indexprofile = i;
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
            var endMode = "Balance";
            ProfileItem? selectedItem = null;
            foreach (var item in ProfilesControl.Items)
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
                endMode = "Max";
            }
            else if (selectedItem.Text == "Preset_Speed_Name/Text".GetLocalized() &&
                     selectedItem.Description == "Preset_Speed_Desc/Text".GetLocalized())
            {
                endMode = "Speed";
            }
            else if (selectedItem.Text == "Preset_Balance_Name/Text".GetLocalized() &&
                     selectedItem.Description == "Preset_Balance_Desc/Text".GetLocalized())
            {
                endMode = "Balance";
            }
            else if (selectedItem.Text == "Preset_Eco_Name/Text".GetLocalized() &&
                     selectedItem.Description == "Preset_Eco_Desc/Text".GetLocalized())
            {
                endMode = "Eco";
            }
            else if (selectedItem.Text == "Preset_Min_Name/Text".GetLocalized() &&
                     selectedItem.Description == "Preset_Min_Desc/Text".GetLocalized())
            {
                endMode = "Min";
            }
            else
            {
                var name = selectedItem.Text;
                var desc = selectedItem.Description;
                var icon = selectedItem.IconGlyph;
                foreach (var profile in _profile)
                {
                    if (profile.Profilename == name &&
                        (profile.Profiledesc == desc || profile.Profilename == desc) &&
                        (profile.Profileicon == icon ||
                         profile.Profileicon == "\uE718"))
                    {
                        ПараметрыPage.ApplyInfo = string.Empty;
                        await Applyer.ApplyCustomPreset(profile, true);

                        NotificationsService.Notifies ??= [];
                        NotificationsService.Notifies.Add(new Notify
                        {
                            Title = "Profile_APPLIED",
                            Msg = "DEBUG MESSAGE",
                            Type = InfoBarSeverity.Informational
                        });
                        NotificationsService.SaveNotificationsSettings();


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

            ShellPage.SelectPremadePreset(endMode);

            var (_, _, _, settings, _) = ShellPage.PremadedPresets[endMode];

            AppSettings.RyzenAdjLine = settings;
            AppSettings.SaveSettings();

            await Applyer.ApplyWithoutAdjLine(true);

            NotificationsService.Notifies ??= [];
            NotificationsService.Notifies.Add(new Notify
            {
                Title = "Profile_APPLIED",
                Msg = "DEBUG MESSAGE",
                Type = InfoBarSeverity.Informational
            });
            NotificationsService.SaveNotificationsSettings();

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

        ProfilesControl_SelectionChanged(ProfilesControl, null);
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

        ProfileLoad();
        var index = _indexprofile == -1 ? 0 : _indexprofile;

        _profile[index].Coallvalue = CurveOptimizerLevelCustomSlider.Value;
        ProfileSave();
    }

    private void CurveOptimizerCustom_Toggled(object sender, RoutedEventArgs e)
    {
        if (_waitforload)
        {
            return;
        }

        ProfileLoad();
        var index = _indexprofile == -1 ? 0 : _indexprofile;

        _profile[index].Coall = CurveOptimizerCustom.IsOn;
        ProfileSave();
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

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Cpu1Value = C1V.Value;
            ProfileSave();
        }
    }

    //Лимит CPU (W)
    private void C2_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Cpu2Value = C2V.Value;
            ProfileSave();
        }
    }

    //Реальный CPU (W)
    private void C3_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Cpu3Value = C3V.Value;
            ProfileSave();
        }
    }

    //Средний CPU(W)
    private void C4_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Cpu4Value = C4V.Value;
            ProfileSave();
        }
    }

    //Тик быстрого разгона (S)
    private void C5_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Cpu5Value = C5V.Value;
            ProfileSave();
        }
    }

    //Тик медленного разгона (S)
    private void C6_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Cpu6Value = C6V.Value;
            ProfileSave();
        }
    }

    //Параметры VRM
    private void V1v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Vrm1Value = V1V.Value;
            ProfileSave();
        }
    }

    private void V2v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Vrm2Value = V2V.Value;
            ProfileSave();
        }
    }

    private void V3v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Vrm3Value = V3V.Value;
            ProfileSave();
        }
    }

    private void V4v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Vrm4Value = V4V.Value;
            ProfileSave();
        }
    }

    private void G9v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Gpu9Value = G9V.Value;
            ProfileSave();
        }
    }

    private void G10v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Gpu10Value = G10V.Value;
            ProfileSave();
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

        ProfileLoad();
        var check = C1.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Cpu1 = check;
            _profile[_indexprofile].Cpu1Value = C1V.Value;
            ProfileSave();
        }
    }

    //Лимит CPU (W)
    private void C2_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = C2.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Cpu2 = check;
            _profile[_indexprofile].Cpu2Value = C2V.Value;
            ProfileSave();
        }
    }

    //Реальный CPU (W)
    private void C3_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = C3.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Cpu3 = check;
            _profile[_indexprofile].Cpu3Value = C3V.Value;
            ProfileSave();
        }
    }

    //Средний CPU (W)
    private void C4_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = C4.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Cpu4 = check;
            _profile[_indexprofile].Cpu4Value = C4V.Value;
            ProfileSave();
        }
    }

    //Тик быстрого разгона (S)
    private void C5_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = C5.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Cpu5 = check;
            _profile[_indexprofile].Cpu5Value = C5V.Value;
            ProfileSave();
        }
    }

    //Тик медленного разгона (S)
    private void C6_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = C6.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Cpu6 = check;
            _profile[_indexprofile].Cpu6Value = C6V.Value;
            ProfileSave();
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

        ProfileLoad();
        var check = V1.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Vrm1 = check;
            _profile[_indexprofile].Vrm1Value = V1V.Value;
            ProfileSave();
        }
    }

    //Лимит по току VRM A
    private void V2_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = V2.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Vrm2 = check;
            _profile[_indexprofile].Vrm2Value = V2V.Value;
            ProfileSave();
        }
    }

    //Максимальный ток SOC A
    private void V3_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = V3.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Vrm3 = check;
            _profile[_indexprofile].Vrm3Value = V3V.Value;
            ProfileSave();
        }
    }

    //Лимит по току SOC A
    private void V4_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = V4.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Vrm4 = check;
            _profile[_indexprofile].Vrm4Value = V4V.Value;
            ProfileSave();
        }
    }

    //Минимальная частота iGpu
    private void G9_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = G9.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Gpu9 = check;
            _profile[_indexprofile].Gpu9Value = G9V.Value;
            ProfileSave();
        }
    }

    //Максимальная частота iGpu
    private void G10_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = G10.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Gpu10 = check;
            _profile[_indexprofile].Gpu10Value = G10V.Value;
            ProfileSave();
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