using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.JsonContainers;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.Styles;
using Saku_Overclock.ViewModels;
using Windows.Foundation.Metadata;
using static ZenStates.Core.Cpu;
using Task = System.Threading.Tasks.Task;

namespace Saku_Overclock.Views;

public sealed partial class ПресетыPage
{
    private static readonly IAppSettingsService AppSettings = App.GetService<IAppSettingsService>();
    private static readonly IApplyerService _applyer = App.GetService<IApplyerService>();
    private static readonly IOcFinderService OcFinder = App.GetService<IOcFinderService>();
    private bool _isLoaded; // Загружена ли корректно страница для применения изменений 
    private bool _waitforload = true; // Ожидание окончательной смены профиля на другой. Активируется при смене профиля 
    private static Profile[] _profile = new Profile[1]; // Всегда по умолчанию будет 1 профиль
    private int _indexprofile = 0; // Выбранный профиль
    private readonly IBackgroundDataUpdater? _dataUpdater;
    private CodeName? _codeName;
    private static readonly IAppNotificationService NotificationsService = App.GetService<IAppNotificationService>(); // Уведомления приложения
    private static readonly ISendSmuCommandService SendSmuCommand = App.GetService<ISendSmuCommandService>();
    private static bool? _isPlatformPC = false;
    private string _doubleClickApply = string.Empty;

    public ПресетыPage()
    {
        InitializeComponent();
        AppSettings.NbfcFlagConsoleCheckSpeedRunning = false;
        AppSettings.FlagRyzenAdjConsoleTemperatureCheckRunning = false;
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
        SelectedProfile_Description.Text = "Preset_Min_Desc/Text".GetLocalized();

        try
        {
            _isPlatformPC = SendSmuCommand.IsPlatformPc();
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex);
        }

        LoadProfiles();

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

        ReapplyOptionsSetOnly(AppSettings.ReapplyOverclock ? ReapplyOptions_Enabled : ReapplyOptions_Disabled);
        AutoApplyOptionsSetOnly(AppSettings.ReapplyLatestSettingsOnAppLaunch ? AutoApplyOptions_Enabled : AutoApplyOptions_Disabled);
        TrayMonSetOnly(AppSettings.NiIconsEnabled ? TrayMonFeat_Enabled : TrayMonFeat_Disabled);
        RtssOverlaySetOnly(AppSettings.RtssMetricsEnabled ? RtssOverlay_Enabled : RtssOverlay_Disabled);
        StreamStabilizerSetOnly(AppSettings.StreamStabilizerEnabled ? StreamStabilizer_Smart : StreamStabilizer_Disabled);

        Premade_OptimizationLevel.SelectedIndex = AppSettings.PremadeOptimizationLevel;
        StreamStabilizerModeCombo.SelectedIndex = AppSettings.StreamStabilizerType;
        StreamStabilizerTargetMhz.Value = AppSettings.StreamStabilizerMaxMHz;
        StreamStabilizerTargetPercent.Value = AppSettings.StreamStabilizerMaxPercentMHz;
        _isLoaded = true;
        StreamStabilizerModeCombo_SelectionChanged(null, null);
        _isLoaded = false;

        if (OcFinder.IsUndervoltingAvailable() && AppSettings.PremadeOptimizationLevel == 2)
        {
            Premade_PremadeCurveOptimizer_StackPanel.Visibility = Visibility.Visible;
        }
        else
        {
            Premade_PremadeCurveOptimizer_StackPanel.Visibility = Visibility.Collapsed;
        }
        CurveOptimizerLevel_Slider.Value = AppSettings.PremadeCurveOptimizerOverrideLevel;

        Premade_OptimizationLevelDesc.Text = Premade_OptimizationLevel.SelectedIndex switch
        {
            0 => "Preset_OptimizationLevel_Base_Desc".GetLocalized(),
            1 => "Preset_OptimizationLevel_Average_Desc".GetLocalized(),
            2 => "Preset_OptimizationLevel_Strong_Desc".GetLocalized(),
            _ => "Preset_OptimizationLevel_Base_Desc".GetLocalized()
        };

        CurveOptimizerCustom_Grid.Visibility = OcFinder.IsUndervoltingAvailable() ? Visibility.Visible : Visibility.Collapsed;
        if (CurveOptimizerCustom_Grid.Visibility == Visibility.Collapsed)
        {
            CurveOptimizerCustom.IsOn = false;
        }

        if (AppSettings.ProfilespageViewModeBeginner)
        {
            BeginnerOptions_Button.IsChecked = true;
            AdvancedOptions_Button.IsChecked = false;
        }
        else
        {
            BeginnerOptions_Button.IsChecked = false;
            AdvancedOptions_Button.IsChecked = true;
        }
        ToolTipService.SetToolTip(AdvancedOptions_Button, "Param_ProMode".GetLocalized());
        ToolTipService.SetToolTip(BeginnerOptions_Button, "Param_NewbieMode".GetLocalized());

        _isLoaded = true;

        var cpu = CpuSingleton.GetInstance();
        _codeName = cpu.info.codeName;
        if (_codeName != CodeName.RavenRidge && _codeName != CodeName.Dali && _codeName != CodeName.Picasso && _codeName != CodeName.FireFlight && SettingsViewModel.VersionId != 5)
        {
            AdvancedGpu_OptionsPanelGrid_0.Visibility = Visibility.Collapsed;
            AdvancedGpu_OptionsPanelGrid_1.Visibility = Visibility.Collapsed;
            AdvancedGpu_OptionsPanel_0.Visibility = Visibility.Collapsed;
            AdvancedGpu_OptionsPanel_1.Height = new GridLength(0);
            AdvancedGpu_OptionsPanel_2.Visibility = Visibility.Collapsed;
            AdvancedGpu_OptionsPanel_3.Height = new GridLength(0);
        }

    }

    #region JSON and Initialization

    #region Initialization
    private async void LoadProfiles()
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
            IsSelected = AppSettings.Preset == -1 && AppSettings.PremadeMaxActivated,
            IconGlyph = "\uEcad",
            Text = "Preset_Max_Name/Text".GetLocalized(), // Maximum
            Description = "Preset_Max_Desc/Text".GetLocalized()
        });
        ProfilesControl.Items.Add(new ProfileItem
        {
            IsSelected = AppSettings.Preset == -1 && AppSettings.PremadeSpeedActivated,
            IconGlyph = "\ue945",
            Text = "Preset_Speed_Name/Text".GetLocalized(), // Speed
            Description = "Preset_Speed_Desc/Text".GetLocalized()
        });
        ProfilesControl.Items.Add(new ProfileItem
        {
            IsSelected = AppSettings.Preset == -1 && AppSettings.PremadeBalanceActivated,
            IconGlyph = "\uec49",
            Text = "Preset_Balance_Name/Text".GetLocalized(), // Balance
            Description = "Preset_Balance_Desc/Text".GetLocalized()
        });
        ProfilesControl.Items.Add(new ProfileItem
        {
            IsSelected = AppSettings.Preset == -1 && AppSettings.PremadeEcoActivated,
            IconGlyph = "\uec0a",
            Text = "Preset_Eco_Name/Text".GetLocalized(), // Eco
            Description = "Preset_Eco_Desc/Text".GetLocalized()
        });
        ProfilesControl.Items.Add(new ProfileItem
        {
            IsSelected = AppSettings.Preset == -1 && AppSettings.PremadeMinActivated,
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
                SelectedProfile_Name.Text = item.Text;

                // Обход отсутствия описания, при помощи записывания имени пресета в описание. Чтобы не отображать два раза одну и ту же строку, описание пресета скрывается (так как его нет)
                SelectedProfile_Description.Text = item.Description != item.Text ? item.Description : string.Empty;
                if (item.Description == item.Text)
                {
                    SelectedProfile_Description.Visibility = Visibility.Collapsed;
                    EditCurrent_ButtonsStackPanel.Margin = new Thickness(0, 0, -13, -10);
                    EditCurrent_ButtonsStackPanel.VerticalAlignment = VerticalAlignment.Top;
                    SelectedProfile_TextsStackPanel.VerticalAlignment = VerticalAlignment.Center;
                }
                else
                {
                    SelectedProfile_Description.Visibility = Visibility.Visible;
                    EditCurrent_ButtonsStackPanel.Margin = new Thickness(0, 17, -13, -10);
                    EditCurrent_ButtonsStackPanel.VerticalAlignment = VerticalAlignment.Center;
                    SelectedProfile_TextsStackPanel.VerticalAlignment = VerticalAlignment.Top;
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
                    ProfileSettings_StackPanel.Visibility = Visibility.Collapsed;
                    ProfileSettings_BeginnerView.Visibility = Visibility.Visible;
                    ProfileSettings_BeginnerView.Margin = new Thickness(0, -5, 0, 0);
                    PremadeProfile_AffectsOn.Visibility = Visibility.Visible;
                    ProfileSettings_BeginnerView_SettingsStackPanel.Visibility = Visibility.Collapsed;
                    EditProfileButton.Visibility = Visibility.Collapsed;
                    ProfileSettings_ChangeViewStackPanel.Visibility = Visibility.Collapsed;

                    var optimizationLevel = Premade_OptimizationLevel.SelectedIndex switch
                    {
                        0 => Services.OptimizationLevel.Basic,
                        1 => Services.OptimizationLevel.Standard,
                        2 => Services.OptimizationLevel.Deep,
                        _ => Services.OptimizationLevel.Basic
                    };

                    Services.PresetMetrics metrics;

                    if (item.Text == "Preset_Max_Name/Text".GetLocalized())
                    {
                        metrics = OcFinder.GetPresetMetrics(Services.PresetType.Max, optimizationLevel);
                        await HelpWithShowPreset(Services.PresetType.Max);
                    }
                    else if (item.Text == "Preset_Speed_Name/Text".GetLocalized())
                    {
                        metrics = OcFinder.GetPresetMetrics(Services.PresetType.Performance, optimizationLevel);
                        await HelpWithShowPreset(Services.PresetType.Performance);
                    }
                    else if (item.Text == "Preset_Balance_Name/Text".GetLocalized())
                    {
                        metrics = OcFinder.GetPresetMetrics(Services.PresetType.Balance, optimizationLevel);
                        await HelpWithShowPreset(Services.PresetType.Balance);
                    }
                    else if (item.Text == "Preset_Eco_Name/Text".GetLocalized())
                    {
                        metrics = OcFinder.GetPresetMetrics(Services.PresetType.Eco, optimizationLevel);
                        await HelpWithShowPreset(Services.PresetType.Eco);
                    }
                    else
                    {
                        metrics = OcFinder.GetPresetMetrics(Services.PresetType.Min, optimizationLevel);
                        await HelpWithShowPreset(Services.PresetType.Min);
                    }

                    Preset_PerformanceBar.Value = 50 + metrics.PerformanceScore;
                    Preset_EnergyEfficiencyBar.Value = 50 + metrics.EfficiencyScore;
                    Preset_TemperaturesBar.Value = 50 + metrics.ThermalScore;

                    /*Preset_Options_Temp.Text = options.ThermalOptions;
                    Preset_Options_Power.Text = options.PowerOptions;
                    Preset_Options_Currents.Text = options.CurrentOptions;*/
                }
                else
                {
                    ProfileSettings_BeginnerView_SettingsStackPanel.Visibility = Visibility.Visible;
                    ProfileSettings_BeginnerView.Margin = new Thickness(0, 0, 0, 0);
                    if (AppSettings.ProfilespageViewModeBeginner)
                    {
                        BeginnerOptions_Button.IsChecked = true;
                        AdvancedOptions_Button.IsChecked = false;
                        ProfileSettings_StackPanel.Visibility = Visibility.Collapsed;
                        ProfileSettings_ChangeViewStackPanel.Visibility = Visibility.Visible;
                        ProfileSettings_BeginnerView.Visibility = Visibility.Visible;
                        PremadeProfile_AffectsOn.Visibility = Visibility.Collapsed;
                        EditProfileButton.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        BeginnerOptions_Button.IsChecked = false;
                        AdvancedOptions_Button.IsChecked = true;
                        ProfileSettings_StackPanel.Visibility = Visibility.Visible;
                        ProfileSettings_BeginnerView.Visibility = Visibility.Collapsed;
                        ProfileSettings_ChangeViewStackPanel.Visibility = Visibility.Visible;
                        PremadeProfile_AffectsOn.Visibility = Visibility.Collapsed;
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

    private async Task HelpWithShowPreset(Services.PresetType type)
    {
        if (!_isLoaded) 
        {
            await Task.Delay(700);
        }

        Preset_HW_Gaming.Visibility = Visibility.Collapsed;
        Preset_HW_Striming.Visibility = Visibility.Collapsed;
        Preset_HW_HardWorkload.Visibility = Visibility.Collapsed;
        Preset_HW_OfficeWorkload.Visibility = Visibility.Collapsed;
        Preset_HW_LightGaming.Visibility = Visibility.Collapsed;
        Preset_HW_Videos.Visibility = Visibility.Collapsed;
        Preset_HW_ColumnHelper1.Width = GridLength.Auto;
        Preset_HW_ColumnHelper2.Width = GridLength.Auto;
        switch (type)
        {
            case Services.PresetType.Min:
                Preset_HW_OfficeWorkload.Visibility = Visibility.Visible;
                Preset_HW_Videos.Visibility = Visibility.Visible;
                Preset_HW_ColumnHelper2.Width = new GridLength(0);
                break;
            case Services.PresetType.Eco:
                Preset_HW_OfficeWorkload.Visibility = Visibility.Visible;
                Preset_HW_Videos.Visibility = Visibility.Visible;
                Preset_HW_LightGaming.Visibility = Visibility.Visible;
                break;
            case Services.PresetType.Balance:
                Preset_HW_Gaming.Visibility = Visibility.Visible;
                Preset_HW_OfficeWorkload.Visibility = Visibility.Visible;
                Preset_HW_Videos.Visibility = Visibility.Visible;
                Preset_HW_Striming.Visibility = Visibility.Visible;
                Preset_HW_ColumnHelper1.Width = new GridLength(0);
                Preset_HW_ColumnHelper2.Width = new GridLength(0);
                break;
            case Services.PresetType.Performance:
            case Services.PresetType.Max:
                Preset_HW_Gaming.Visibility = Visibility.Visible;
                Preset_HW_Striming.Visibility = Visibility.Visible;
                Preset_HW_OfficeWorkload.Visibility = Visibility.Visible;
                Preset_HW_Videos.Visibility = Visibility.Visible;
                Preset_HW_HardWorkload.Visibility = Visibility.Visible;
                break;
        }
    }

    private async Task MainInitAsync(int index)
    {
        _waitforload = true;

        ProfileLoad();
        try
        {
            if (_profile[index].Cpu1Value > c1v.Maximum)
            {
                c1v.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].Cpu1Value);
            }

            if (_profile[index].Cpu2Value > BaseTdp_Slider.Maximum)
            {
                BaseTdp_Slider.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].Cpu2Value);
            }

            if (_profile[index].Cpu2Value > c2v.Maximum)
            {
                c2v.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].Cpu2Value);
            }

            if (_profile[index].Cpu3Value > c3v.Maximum)
            {
                c3v.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].Cpu3Value);
            }

            if (_profile[index].Cpu4Value > c4v.Maximum)
            {
                c4v.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].Cpu4Value);
            }

            if (_profile[index].Cpu5Value > c5v.Maximum)
            {
                c5v.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].Cpu5Value);
            }

            if (_profile[index].Cpu6Value > c6v.Maximum)
            {
                c6v.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].Cpu6Value);
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

            if (_profile[index].Gpu9Value > g9v.Maximum)
            {
                g9v.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].Gpu9Value);
            }

            if (_profile[index].Gpu10Value > g10v.Maximum)
            {
                g10v.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].Gpu10Value);
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

            c2.IsChecked = _profile[index].Cpu2;
            c2v.Value = _profile[index].Cpu2Value;

            BaseTdp_Slider.Value = _profile[index].Cpu2Value;
            if (_profile[index].Cpu2 && _profile[index].Cpu3 && _profile[index].Cpu4 &&
                _profile[index].Cpu2Value == _profile[index].Cpu4Value && _profile[index].Cpu3Value == fineTunedTdp)
            {
                SmartTdp.IsOn = true;
            }
            else
            {
                if (_isPlatformPC != false && SettingsViewModel.VersionId != 5) // Если устройство - не ноутбук
                {
                    // Так как на компьютерах невозможно выставить другие Power лимиты
                    if (!_profile[index].Cpu2 && !_profile[index].Cpu4 && _profile[index].Cpu3Value == fineTunedTdp)
                    {
                        SmartTdp.IsOn = true;
                    }
                }
                else
                {
                    SmartTdp.IsOn = false;
                }
            }

            c1.IsChecked = _profile[index].Cpu1;
            c1v.Value = _profile[index].Cpu1Value;
            c3.IsChecked = _profile[index].Cpu3;
            c3v.Value = _profile[index].Cpu3Value;
            c4.IsChecked = _profile[index].Cpu4;
            c4v.Value = _profile[index].Cpu4Value;
            c5.IsChecked = _profile[index].Cpu5;
            c5v.Value = _profile[index].Cpu5Value;
            c6.IsChecked = _profile[index].Cpu6;
            c6v.Value = _profile[index].Cpu6Value;


            if (IsRavenFamily())
            {
                if (_profile[index].Gpu10 == true && _profile[index].Gpu9 == true && _profile[index].Gpu10Value == 1200)
                {
                    if (_profile[index].Gpu9Value == 800)
                    {
                        IgpuEnchancementCombo.SelectedIndex = 1;
                    }
                    if (_profile[index].Gpu9Value == 1000)
                    {
                        IgpuEnchancementCombo.SelectedIndex = 2;
                    }
                }
                else
                {
                    IgpuEnchancementCombo.SelectedIndex = 0;
                }
            }
            else
            {
                if (_profile[index].Advncd10 == true)
                {
                    if (_profile[index].Advncd10Value == 1750)
                    {
                        IgpuEnchancementCombo.SelectedIndex = 1;
                    }
                    if (_profile[index].Advncd10Value == 2200)
                    {
                        IgpuEnchancementCombo.SelectedIndex = 2;
                    }
                }
                else
                {
                    IgpuEnchancementCombo.SelectedIndex = 0;
                }
            }

            if (!_profile[index].Cpu5 && !_profile[index].Cpu6)
            {
                TurboSetOnly(Turbo_LightModeToggle);
            }
            else
            {
                if ((_profile[index].Cpu5 && !_profile[index].Cpu6) || (!_profile[index].Cpu5 && _profile[index].Cpu6))
                {
                    TurboSetOnly(Turbo_LightModeToggle);
                }
                if (_profile[index].Cpu5 && _profile[index].Cpu6)
                {
                    if (_profile[index].Cpu5Value == 400 && _profile[index].Cpu6Value == 3)
                    {
                        TurboSetOnly(Turbo_BalanceModeToggle);
                    }
                    else if (_profile[index].Cpu5Value == 5000 && _profile[index].Cpu6Value == 1)
                    {
                        TurboSetOnly(Turbo_HeavyModeToggle);
                    }
                    else
                    {
                        TurboSetOnly(Turbo_LightModeToggle);
                    }
                }
                else
                {
                    TurboSetOnly(Turbo_LightModeToggle);
                }
            }

            if (_profile[index].Coall)
            {
                CurveOptimizerCustom.IsOn = true;
                CurveOptimizerLevelCustom_Slider.Value = _profile[index].Coallvalue;
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
            g9v.Value = _profile[index].Gpu9Value;
            g9.IsChecked = _profile[index].Gpu9;
            g10v.Value = _profile[index].Gpu10Value;
            g10.IsChecked = _profile[index].Gpu10;
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
        Turbo_LightModeToggle.IsChecked = false;
        Turbo_BalanceModeToggle.IsChecked = false;
        Turbo_HeavyModeToggle.IsChecked = false;

        switch (button.Name)
        {
            case "Turbo_LightModeToggle":
                Turbo_LightModeToggle.IsChecked = true;
                break;
            case "Turbo_BalanceModeToggle":
                Turbo_BalanceModeToggle.IsChecked = true;
                break;
            case "Turbo_HeavyModeToggle":
                Turbo_HeavyModeToggle.IsChecked = true;
                break;
        }
    }

    private void StreamStabilizerSetOnly(ToggleButton button)
    {
        StreamStabilizer_Disabled.IsChecked = false;
        StreamStabilizer_Smart.IsChecked = false;

        switch (button.Name)
        {
            case "StreamStabilizer_Disabled":
                StreamStabilizer_Disabled.IsChecked = true;
                break;
            case "StreamStabilizer_Smart":
                StreamStabilizer_Smart.IsChecked = true;
                break;
        }
    }
    private void ReapplyOptionsSetOnly(ToggleButton button)
    {
        ReapplyOptions_Disabled.IsChecked = false;
        ReapplyOptions_Enabled.IsChecked = false;

        switch (button.Name)
        {
            case "ReapplyOptions_Disabled":
                ReapplyOptions_Disabled.IsChecked = true;
                break;
            case "ReapplyOptions_Enabled":
                ReapplyOptions_Enabled.IsChecked = true;
                break;
        }
    }
    private void RtssOverlaySetOnly(ToggleButton button)
    {
        RtssOverlay_Disabled.IsChecked = false;
        RtssOverlay_Enabled.IsChecked = false;

        switch (button.Name)
        {
            case "RtssOverlay_Disabled":
                RtssOverlay_Disabled.IsChecked = true;
                break;
            case "RtssOverlay_Enabled":
                RtssOverlay_Enabled.IsChecked = true;
                break;
        }
    }
    private void TrayMonSetOnly(ToggleButton button)
    {
        TrayMonFeat_Disabled.IsChecked = false;
        TrayMonFeat_Enabled.IsChecked = false;

        switch (button.Name)
        {
            case "TrayMonFeat_Disabled":
                TrayMonFeat_Disabled.IsChecked = true;
                break;
            case "TrayMonFeat_Enabled":
                TrayMonFeat_Enabled.IsChecked = true;
                break;
        }
    }
    private void AutoApplyOptionsSetOnly(ToggleButton button)
    {
        AutoApplyOptions_Disabled.IsChecked = false;
        AutoApplyOptions_Enabled.IsChecked = false;

        switch (button.Name)
        {
            case "AutoApplyOptions_Disabled":
                AutoApplyOptions_Disabled.IsChecked = true;
                break;
            case "AutoApplyOptions_Enabled":
                AutoApplyOptions_Enabled.IsChecked = true;
                break;
        }
    }

    private void OnDataUpdated(object? sender, SensorsInformation info)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            TdpLimitSensor_Text.Text = Math.Round(info.CpuFastLimit) + "W";
            TdpValueSensor_Text.Text = Math.Round(info.CpuFastValue).ToString();
            CpuFreqSensor_Text.Text = Math.Round(info.CpuFrequency, 1).ToString();

            var updateSmallSign = true;

            if (info.ApuTemperature == 0)
            {
                updateSmallSign = false;
                TempSensors_StackPanel.Margin = new Thickness(7, 0, 0, 5);
                TempSensors_StackPanel.VerticalAlignment = VerticalAlignment.Bottom;
                GpuTempSensor_StackPanel.Visibility = Visibility.Collapsed;
                CpuTempSensor_CaptionText.Visibility = Visibility.Collapsed;
                CpuTempSensor_BigCaptionText.Visibility = Visibility.Visible;
                CpuTempSensor_Text.FontSize = 38;
                CpuTempSensor_Text.Margin = new Thickness(4, -8, 0, 0);
                CpuTempSensor_Text.FontWeight = new Windows.UI.Text.FontWeight(700);
            }
            else
            {
                GpuTempSensor_Text.Text = Math.Round(info.ApuTemperature) + "C";
            }

            CpuTempSensor_Text.Text = Math.Round(info.CpuTempValue) + (updateSmallSign ? "C" : string.Empty);
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
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(),
                        AppContext.BaseDirectory));
                    App.MainWindow.Close();
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
        if (!_isLoaded || _waitforload) { return; }

        var toggle = sender as ToggleButton;
        if (ReapplyOptions_Disabled.IsChecked == false
            && ReapplyOptions_Enabled.IsChecked == false)
        {
            toggle!.IsChecked = true;
        }
        else
        {
            if (toggle!.Name == "ReapplyOptions_Disabled")
            {
                ReapplyOptionsSetOnly(ReapplyOptions_Disabled);
                AppSettings.ReapplyOverclock = false;
            }
            else if (toggle.Name == "ReapplyOptions_Enabled")
            {
                ReapplyOptionsSetOnly(ReapplyOptions_Enabled);
                AppSettings.ReapplyOverclock = true;
            }
        }

        AppSettings.SaveSettings();
    }
    private void RtssOverlaySwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload) { return; }

        var toggle = sender as ToggleButton;
        if (RtssOverlay_Disabled.IsChecked == false
            && RtssOverlay_Enabled.IsChecked == false)
        {
            toggle!.IsChecked = true;
        }
        else
        {
            if (toggle!.Name == "RtssOverlay_Disabled")
            {
                RtssOverlaySetOnly(RtssOverlay_Disabled);
                AppSettings.RtssMetricsEnabled = false;
            }
            else if (toggle.Name == "RtssOverlay_Enabled")
            {
                RtssOverlaySetOnly(RtssOverlay_Enabled);
                AppSettings.RtssMetricsEnabled = true;
            }
        }

        AppSettings.SaveSettings();
    }

    private void TrayMonFeatSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload) { return; }

        var toggle = sender as ToggleButton;
        if (TrayMonFeat_Disabled.IsChecked == false
            && TrayMonFeat_Enabled.IsChecked == false)
        {
            toggle!.IsChecked = true;
        }
        else
        {
            if (toggle!.Name == "TrayMonFeat_Disabled")
            {
                TrayMonSetOnly(TrayMonFeat_Disabled);
                AppSettings.NiIconsEnabled = false;
            }
            else if (toggle.Name == "TrayMonFeat_Enabled")
            {
                TrayMonSetOnly(TrayMonFeat_Enabled);
                AppSettings.NiIconsEnabled = true;
            }
        }

        AppSettings.SaveSettings();
    }

    private void AutoApplyOptions_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload) { return; }

        var toggle = sender as ToggleButton;
        if (AutoApplyOptions_Disabled.IsChecked == false
            && AutoApplyOptions_Enabled.IsChecked == false)
        {
            toggle!.IsChecked = true;
        }
        else
        {
            if (toggle!.Name == "AutoApplyOptions_Disabled")
            {
                AutoApplyOptionsSetOnly(AutoApplyOptions_Disabled);
                AppSettings.ReapplyLatestSettingsOnAppLaunch = false;
            }
            else if (toggle.Name == "AutoApplyOptions_Enabled")
            {
                AutoApplyOptionsSetOnly(AutoApplyOptions_Enabled);
                AppSettings.ReapplyLatestSettingsOnAppLaunch = true;
            }
        }

        AppSettings.SaveSettings();
    }

    private void StreamStabilizerSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload) { return; }

        var toggle = sender as ToggleButton;
        if (StreamStabilizer_Disabled.IsChecked == false
            && StreamStabilizer_Smart.IsChecked == false)
        {
            toggle!.IsChecked = true;
        }
        else
        {
            if (toggle!.Name == "StreamStabilizer_Disabled")
            {
                StreamStabilizerSetOnly(StreamStabilizer_Disabled);
                AppSettings.StreamStabilizerEnabled = false;

                StreamStabilizerTargetMHzGrid.Visibility = Visibility.Collapsed;
                StreamStabilizerTargetPercentOfMHzGrid.Visibility = Visibility.Collapsed;

                SetPowerConfig("PROCTHROTTLEMIN 100");
                SetPowerConfig("PROCTHROTTLEMAX 100");
                SetPowerConfig($"PROCFREQMAX 0");
                SavePowerConfig();
                SavePowerConfig();
            }
            else if (toggle.Name == "StreamStabilizer_Smart")
            {
                StreamStabilizerSetOnly(StreamStabilizer_Smart);
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
        AppSettings.ProfilespageViewModeBeginner = !AppSettings.ProfilespageViewModeBeginner;
        AppSettings.SaveSettings();
        if (AppSettings.ProfilespageViewModeBeginner)
        {
            BeginnerOptions_Button.IsChecked = true;
            AdvancedOptions_Button.IsChecked = false;
            ProfileSettings_StackPanel.Visibility = Visibility.Collapsed;
            ProfileSettings_BeginnerView.Visibility = Visibility.Visible;
            PremadeProfile_AffectsOn.Visibility = Visibility.Collapsed;
            EditProfileButton.Visibility = Visibility.Visible;
            ProfileSettings_ChangeViewStackPanel.Visibility = Visibility.Visible;
        }
        else
        {
            BeginnerOptions_Button.IsChecked = false;
            AdvancedOptions_Button.IsChecked = true;
            ProfileSettings_StackPanel.Visibility = Visibility.Visible;
            ProfileSettings_BeginnerView.Visibility = Visibility.Collapsed;
            PremadeProfile_AffectsOn.Visibility = Visibility.Collapsed;
            EditProfileButton.Visibility = Visibility.Visible;
            ProfileSettings_ChangeViewStackPanel.Visibility = Visibility.Visible;
        }
    }

    private void BaseTdp_Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload) { return; }
        ChangedBaseTdp_Value();
    }

    private void SmartTdp_Toggled(object sender, RoutedEventArgs e) => ChangedBaseTdp_Value();

    private void ChangedBaseTdp_Value()
    {
        if (_waitforload) { return; }
        ProfileLoad();
        var index = _indexprofile == -1 ? 0 : _indexprofile;

        // Заранее скомпилированная функция увеличения TDP, созданная специально для фирменной функции Smart TDP
        var fineTunedTdp = ПараметрыPage.FromValueToUpperFive(1.17335141 * BaseTdp_Slider.Value + 0.21631949);

        c2.IsChecked = true;
        _profile[index].Cpu2 = true;
        c3.IsChecked = true;
        _profile[index].Cpu3 = true;
        c4.IsChecked = true;
        _profile[index].Cpu4 = true;

        if (fineTunedTdp > c3v.Maximum || BaseTdp_Slider.Value > c2v.Maximum || BaseTdp_Slider.Value > c4v.Maximum)
        {
            c2v.Maximum = ПараметрыPage.FromValueToUpperFive(BaseTdp_Slider.Value);
            c3v.Maximum = ПараметрыPage.FromValueToUpperFive(fineTunedTdp);
            c4v.Maximum = ПараметрыPage.FromValueToUpperFive(BaseTdp_Slider.Value);
        }
        c2v.Value = BaseTdp_Slider.Value;
        _profile[index].Cpu2Value = BaseTdp_Slider.Value;

        c4v.Value = BaseTdp_Slider.Value;
        _profile[index].Cpu4Value = BaseTdp_Slider.Value;

        if (SmartTdp.IsOn)
        {
            c3v.Value = fineTunedTdp;
            _profile[index].Cpu3Value = fineTunedTdp;
        }
        else
        {
            c3v.Value = BaseTdp_Slider.Value;
            _profile[index].Cpu3Value = BaseTdp_Slider.Value;
        }
        if (_isPlatformPC != false && SettingsViewModel.VersionId != 5) // Если устройство - не ноутбук
        {
            // Так как на компьютерах невозможно выставить другие Power лимиты
            c2.IsChecked = false; // Отключить STAPM
            _profile[index].Cpu2 = false;
            c4.IsChecked = false; // Отключить Slow лимит
            _profile[index].Cpu4 = false;
        }
        ProfileSave();
    }

    // Grid-помощник, который активирует переключатель когда пользователь нажал на область возле него
    private void SmartTdp_PointerPressed(object sender, object e) => SmartTdp.IsOn = !SmartTdp.IsOn;

    // Выбора режима усиления Турбобуста процессора: Авто, Умный, Сильный. Устанавливает параметры времени разгона процессора в зависимости от выбранной настройки
    private void Turbo_OtherModeToggle_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload) { return; }

        var toggle = sender as ToggleButton;
        if (Turbo_LightModeToggle.IsChecked == false
            && Turbo_BalanceModeToggle.IsChecked == false
            && Turbo_HeavyModeToggle.IsChecked == false)
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
            else
            {
                index = AppSettings.Preset;
            }

            ProfileLoad();

            if (toggle!.Name == "Turbo_LightModeToggle")
            {
                TurboSetOnly(Turbo_LightModeToggle);
                _profile[index].Cpu5 = false;
                _profile[index].Cpu6 = false;
                c5.IsChecked = false;
                c6.IsChecked = false;
            }
            else if (toggle.Name == "Turbo_BalanceModeToggle")
            {
                TurboSetOnly(Turbo_BalanceModeToggle);
                _profile[index].Cpu5 = true;
                _profile[index].Cpu6 = true;
                _profile[index].Cpu5Value = 400;
                _profile[index].Cpu6Value = 3;
                c5.IsChecked = true;
                c6.IsChecked = true;
                c5v.Value = 400;
                c6v.Value = 3;
            }
            else if (toggle.Name == "Turbo_HeavyModeToggle")
            {
                TurboSetOnly(Turbo_HeavyModeToggle);
                _profile[index].Cpu5 = true;
                _profile[index].Cpu6 = true;
                _profile[index].Cpu5Value = 5000;
                _profile[index].Cpu6Value = 1;
                c5.IsChecked = true;
                c6.IsChecked = true;
                c5v.Maximum = 5000;
                c5v.Value = 5000;
                c6v.Value = 1;
            }
        }

        ProfileSave();
    }

    private void IgpuEnchancementCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload) { return; }
        int index;
        if (AppSettings.Preset == -1)
        {
            return;
        }
        else
        {
            index = AppSettings.Preset;
        }
        if (_codeName == null)
        {
            return;
        }
        switch (IgpuEnchancementCombo.SelectedIndex)
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
            if (SaveProfileN.Text != "")
            {
                await LogHelper.Log($"Adding new profile: \"{SaveProfileN.Text}\"");
                ProfileLoad();
                try
                {
                    AddProfileButton.Flyout.Hide();
                    AppSettings.Preset += 1;
                    _indexprofile += 1;
                    _waitforload = true;
                    if (_profile.Length == 0)
                    {
                        _profile = new Profile[1];
                        _profile[0] = new Profile { Profilename = SaveProfileN.Text, Profiledesc = SaveProfileD.Text };
                    }
                    else
                    {
                        var profileList = new List<Profile>(_profile)
                        {
                            new()
                            {
                                Profilename = SaveProfileN.Text,
                                Profiledesc = SaveProfileD.Text
                            }
                        };
                        _profile = [.. profileList];
                    }

                    _waitforload = false;
                    NotificationsService.Notifies ??= [];
                    NotificationsService.Notifies.Add(new Notify
                    {
                        Title = "SaveSuccessTitle".GetLocalized(),
                        Msg = "SaveSuccessDesc".GetLocalized() + " " + SaveProfileN.Text,
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
            LoadProfiles();
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception);
        }
    }
    private async void EditProfileButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await LogHelper.Log($"Editing profile name: From \"{_profile[_indexprofile].Profilename}\" To \"{EditProfileN.Text}\"");
            EditProfileButton.Flyout.Hide();
            if (EditProfileN.Text != "")
            {
                ProfileLoad();
                _profile[_indexprofile].Profilename = EditProfileN.Text;
                _profile[_indexprofile].Profiledesc = EditProfileD.Text;
                ProfileSave();
                _waitforload = true;
                LoadProfiles();
                _waitforload = false;
                NotificationsService.Notifies ??= [];
                NotificationsService.Notifies.Add(new Notify
                {
                    Title = "Edit_Target/Title".GetLocalized(),
                    Msg = "Edit_Target/Subtitle".GetLocalized() + " " + EditProfileN.Text,
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
    private async void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await LogHelper.Log("Showing delete profile dialog");
            var delDialog = new ContentDialog
            {
                Title = "Param_DelPreset_Text".GetLocalized(),
                Content = "Param_DelPreset_Desc".GetLocalized(),
                CloseButtonText = "Cancel".GetLocalized(),
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

                await LogHelper.Log($"Showing delete profile dialog: deleting profile \"{_profile[indexprofile].Profilename}\"");

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
                LoadProfiles();
            }
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception);
        }
    }
    private void EditProfileButton_Click_1(object sender, RoutedEventArgs e)
    {
        EditProfileN.Text = _profile[_indexprofile].Profilename;
        EditProfileD.Text = _profile[_indexprofile].Profiledesc;
    }

    #endregion

    #endregion

    #endregion

    #region Profile Settings Events

    private async void ProfilesControl_SelectionChanged(object sender, SelectionChangedEventArgs? e)
    {
        var selectedItem = (sender as ProfileSelector)!.SelectedItem;

        if (selectedItem != null)
        {
            // Корректное отображение описания, даже если оно маленькое (чтобы Grid изменил свой размер корректно и слова не обрывались)
            ProfileSettings_InfoRow.Height = new GridLength(0);
            ProfileSettings_InfoRow.Height = GridLength.Auto;

            SelectedProfile_Name.Text = selectedItem.Text;

            // Обход отсутствия описания, при помощи записывания имени пресета в описание. Чтобы не отображать два раза одну и ту же строку, описание пресета скрывается (так как его нет)
            SelectedProfile_Description.Text = selectedItem.Description != selectedItem.Text ? selectedItem.Description : string.Empty;

            if (e != null)
            {
                if (_doubleClickApply == SelectedProfile_Name.Text + SelectedProfile_Description.Text)
                {
                    ApplyButton_Click(null, null);
                }
                _doubleClickApply = SelectedProfile_Name.Text + SelectedProfile_Description.Text;
            }
            
            if (selectedItem.Description == selectedItem.Text)
            {
                SelectedProfile_Description.Visibility = Visibility.Collapsed;
                EditCurrent_ButtonsStackPanel.Margin = new Thickness(0, 0, -13, -10);
                EditCurrent_ButtonsStackPanel.VerticalAlignment = VerticalAlignment.Top;
                SelectedProfile_TextsStackPanel.VerticalAlignment = VerticalAlignment.Center;
            }
            else
            {
                SelectedProfile_Description.Visibility = Visibility.Visible;
                EditCurrent_ButtonsStackPanel.Margin = new Thickness(0, 17, -13, -10);
                EditCurrent_ButtonsStackPanel.VerticalAlignment = VerticalAlignment.Center;
                SelectedProfile_TextsStackPanel.VerticalAlignment = VerticalAlignment.Top;
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
                var optimizationLevel = Premade_OptimizationLevel.SelectedIndex switch
                {
                    0 => Services.OptimizationLevel.Basic,
                    1 => Services.OptimizationLevel.Standard,
                    2 => Services.OptimizationLevel.Deep,
                    _ => Services.OptimizationLevel.Basic
                };

                Services.PresetConfiguration preset;

                if (selectedItem.Text == "Preset_Max_Name/Text".GetLocalized())
                {
                    preset = OcFinder.CreatePreset(Services.PresetType.Max, optimizationLevel);
                    await HelpWithShowPreset(Services.PresetType.Max);
                }
                else if (selectedItem.Text == "Preset_Speed_Name/Text".GetLocalized())
                {
                    preset = OcFinder.CreatePreset(Services.PresetType.Performance, optimizationLevel);
                    await HelpWithShowPreset(Services.PresetType.Performance);
                }
                else if (selectedItem.Text == "Preset_Balance_Name/Text".GetLocalized())
                {
                    preset = OcFinder.CreatePreset(Services.PresetType.Balance, optimizationLevel);
                    await HelpWithShowPreset(Services.PresetType.Balance);
                }
                else if (selectedItem.Text == "Preset_Eco_Name/Text".GetLocalized())
                {
                    preset = OcFinder.CreatePreset(Services.PresetType.Eco, optimizationLevel);
                    await HelpWithShowPreset(Services.PresetType.Eco);
                }
                else
                {
                    preset = OcFinder.CreatePreset(Services.PresetType.Min, optimizationLevel);
                    await HelpWithShowPreset(Services.PresetType.Min);
                }

                Preset_PerformanceBar.Value = 50 + preset.Metrics.PerformanceScore;
                Preset_EnergyEfficiencyBar.Value = 50 + preset.Metrics.EfficiencyScore;
                Preset_TemperaturesBar.Value = 50 + preset.Metrics.ThermalScore;

                Preset_Options_Temp.Text = preset.Options.ThermalOptions;
                Preset_Options_Power.Text = preset.Options.PowerOptions;
                Preset_Options_Currents.Text = preset.Options.CurrentOptions;

                Preset_Options_CommandLine.Blocks.Clear();

                var paragraph = new Paragraph();
                foreach (var text in preset.CommandString.Split("\n"))
                {
                    if (!string.IsNullOrWhiteSpace(text)) 
                    {
                        var run = new Run() { Text = text };
                        paragraph.Inlines.Add(run);
                    }
                }

                Preset_Options_CommandLine.Blocks.Add(paragraph);

                ProfileSettings_StackPanel.Visibility = Visibility.Collapsed;
                ProfileSettings_BeginnerView.Visibility = Visibility.Visible;
                ProfileSettings_BeginnerView.Margin = new Thickness(0, -5, 0, 0);
                PremadeProfile_AffectsOn.Visibility = Visibility.Visible;
                ProfileSettings_BeginnerView_SettingsStackPanel.Visibility = Visibility.Collapsed;
                EditProfileButton.Visibility = Visibility.Collapsed;
                ProfileSettings_ChangeViewStackPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                ProfileSettings_BeginnerView_SettingsStackPanel.Visibility = Visibility.Visible;
                ProfileSettings_BeginnerView.Margin = new Thickness(0, 0, 0, 0);

                if (AppSettings.ProfilespageViewModeBeginner)
                {
                    BeginnerOptions_Button.IsChecked = true;
                    AdvancedOptions_Button.IsChecked = false;
                    ProfileSettings_StackPanel.Visibility = Visibility.Collapsed;
                    ProfileSettings_BeginnerView.Visibility = Visibility.Visible;
                    PremadeProfile_AffectsOn.Visibility = Visibility.Collapsed;
                    EditProfileButton.Visibility = Visibility.Visible;
                    ProfileSettings_ChangeViewStackPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    BeginnerOptions_Button.IsChecked = false;
                    AdvancedOptions_Button.IsChecked = true;
                    ProfileSettings_StackPanel.Visibility = Visibility.Visible;
                    ProfileSettings_BeginnerView.Visibility = Visibility.Collapsed;
                    PremadeProfile_AffectsOn.Visibility = Visibility.Collapsed;
                    EditProfileButton.Visibility = Visibility.Visible;
                    ProfileSettings_ChangeViewStackPanel.Visibility = Visibility.Visible;
                }

                for (var i = 0; i < _profile.Length; i++)
                {
                    if ((_profile[i].Profiledesc == selectedItem.Description || _profile[i].Profilename == selectedItem.Description) &&
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
    }

    private async void ApplyButton_Click(object? sender, RoutedEventArgs? e)
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
        if (selectedItem == null) { return; }
        if (selectedItem.Text == "Preset_Max_Name/Text".GetLocalized() &&
                selectedItem.Description == "Preset_Max_Desc/Text".GetLocalized())
        {
            endMode = "Max";
        }
        else
        if (selectedItem.Text == "Preset_Speed_Name/Text".GetLocalized() &&
                selectedItem.Description == "Preset_Speed_Desc/Text".GetLocalized())
        {
            endMode = "Speed";
        }
        else
        if (selectedItem.Text == "Preset_Balance_Name/Text".GetLocalized() &&
        selectedItem.Description == "Preset_Balance_Desc/Text".GetLocalized())
        {
            endMode = "Balance";
        }
        else
        if (selectedItem.Text == "Preset_Eco_Name/Text".GetLocalized() &&
        selectedItem.Description == "Preset_Eco_Desc/Text".GetLocalized())
        {
            endMode = "Eco";
        }
        else
        if (selectedItem.Text == "Preset_Min_Name/Text".GetLocalized() &&
        selectedItem.Description == "Preset_Min_Desc/Text".GetLocalized())
        {
            endMode = "Min";
        }
        else
        {
            var name = selectedItem.Text;
            var desc = selectedItem.Description;
            var icon = selectedItem.IconGlyph;
            for (var i = 0; i < _profile.Length; i++)
            {
                var profile = _profile[i];
                if (profile.Profilename == name &&
                    (profile.Profiledesc == desc || profile.Profilename == desc) &&
                    (profile.Profileicon == icon ||
                     profile.Profileicon == "\uE718"))
                {
                    ПараметрыPage.ApplyInfo = string.Empty;
                    await _applyer.ApplyCustomPreset(profile, true);

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
                    if (ПараметрыPage.ApplyInfo != string.Empty)
                    {
                        timer *= ПараметрыPage.ApplyInfo.Split('\n').Length + 1;
                    }
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        ApplyTeach.Target = ApplyButton;
                        ApplyTeach.Title = "Apply_Success".GetLocalized();
                        ApplyTeach.Subtitle = "Apply_Success_Desc".GetLocalized();
                        ApplyTeach.IconSource = new SymbolIconSource { Symbol = Symbol.Accept };
                        ApplyTeach.IsOpen = true;
                        var infoSet = InfoBarSeverity.Success;
                        if (ПараметрыPage.ApplyInfo != string.Empty)
                        {
                            await LogHelper.Log(ПараметрыPage.ApplyInfo);
                            ApplyTeach.Title = "Apply_Warn".GetLocalized();
                            ApplyTeach.Subtitle = "Apply_Warn_Desc".GetLocalized() + ПараметрыPage.ApplyInfo;
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
                            Msg = ApplyTeach.Subtitle + (ПараметрыPage.ApplyInfo != string.Empty ? "DELETEUNAVAILABLE" : ""),
                            Type = infoSet
                        });
                        NotificationsService.SaveNotificationsSettings();
                    });
                    return;
                }
            }
        }

        ShellPage.SelectPremadePreset(endMode);

        var (_, _, _, settings, _) = ShellPage.PremadedPresets[endMode];

        AppSettings.RyzenAdjLine = settings;
        AppSettings.SaveSettings();

        await _applyer.ApplyWithoutAdjLine(true);

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
        ApplyTeach.Subtitle = "Apply_Success_Desc".GetLocalized();
        ApplyTeach.IconSource = new SymbolIconSource { Symbol = Symbol.Accept };
        ApplyTeach.IsOpen = true;
        await LogHelper.Log("Apply_Success".GetLocalized());
        await Task.Delay(3000);
        ApplyTeach.IsOpen = false;
    }

    private void Premade_OptimizationLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded) { return; }

        AppSettings.PremadeOptimizationLevel = Premade_OptimizationLevel.SelectedIndex;
        AppSettings.SaveSettings();

        Premade_OptimizationLevelDesc.Text = Premade_OptimizationLevel.SelectedIndex switch
        {
            0 => "Preset_OptimizationLevel_Base_Desc".GetLocalized(),
            1 => "Preset_OptimizationLevel_Average_Desc".GetLocalized(),
            2 => "Preset_OptimizationLevel_Strong_Desc".GetLocalized(),
            _ => "Preset_OptimizationLevel_Base_Desc".GetLocalized()
        };

        if (OcFinder.IsUndervoltingAvailable() && Premade_OptimizationLevel.SelectedIndex == 2)
        {
            Premade_PremadeCurveOptimizer_StackPanel.Visibility = Visibility.Visible;
        }
        else
        {
            Premade_PremadeCurveOptimizer_StackPanel.Visibility = Visibility.Collapsed;
        }

        ProfilesControl_SelectionChanged(ProfilesControl, null);
    }

    private void CurveOptimizerLevel_Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded) { return; }

        AppSettings.PremadeCurveOptimizerOverrideLevel = (int)CurveOptimizerLevel_Slider.Value;
        AppSettings.SaveSettings();
    }

    private void CurveOptimizerLevelCustom_Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_waitforload) { return; }
        ProfileLoad();
        var index = _indexprofile == -1 ? 0 : _indexprofile;

        _profile[index].Coallvalue = CurveOptimizerLevelCustom_Slider.Value;
        ProfileSave();
    }

    private void CurveOptimizerCustom_Toggled(object sender, RoutedEventArgs e)
    {
        if (_waitforload) { return; }
        ProfileLoad();
        var index = _indexprofile == -1 ? 0 : _indexprofile;

        _profile[index].Coall = CurveOptimizerCustom.IsOn;
        ProfileSave();
    }

    private void StreamStabilizerModeCombo_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
    {
        if (!_isLoaded) { return; }

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

                break;// Target MHz
            case 2:
                StreamStabilizerTargetMHzGrid.Visibility = Visibility.Collapsed;
                StreamStabilizerTargetPercentOfMHzGrid.Visibility = Visibility.Visible;

                SetPowerConfig($"PROCTHROTTLEMIN {(int)StreamStabilizerTargetPercent.Value}");
                SetPowerConfig($"PROCTHROTTLEMAX {(int)StreamStabilizerTargetPercent.Value}");
                SetPowerConfig($"PROCFREQMAX 0");
                SavePowerConfig();

                AppSettings.StreamStabilizerMaxPercentMHz = (int)StreamStabilizerTargetPercent.Value;

                break;// Target % of MHz
            default:
                StreamStabilizerTargetMHzGrid.Visibility = Visibility.Collapsed;
                StreamStabilizerTargetPercentOfMHzGrid.Visibility = Visibility.Collapsed;

                SetPowerConfig("PROCTHROTTLEMIN 99");
                SetPowerConfig("PROCTHROTTLEMAX 99");
                SetPowerConfig($"PROCFREQMAX 0");
                SavePowerConfig();

                break;// Base lock
        }

        AppSettings.SaveSettings();
    }


    private void StreamStabilizerTargetMhz_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_isLoaded) { return; }

        SetPowerConfig("PROCTHROTTLEMIN 100");
        SetPowerConfig("PROCTHROTTLEMAX 100");
        SetPowerConfig($"PROCFREQMAX {(int)StreamStabilizerTargetMhz.Value}");
        SavePowerConfig();

        AppSettings.StreamStabilizerMaxMHz = (int)StreamStabilizerTargetMhz.Value;
        AppSettings.SaveSettings();
    }

    private void StreamStabilizerTargetPercent_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_isLoaded) { return; }

        SetPowerConfig($"PROCTHROTTLEMIN {(int)StreamStabilizerTargetPercent.Value}");
        SetPowerConfig($"PROCTHROTTLEMAX {(int)StreamStabilizerTargetPercent.Value}");
        SetPowerConfig($"PROCFREQMAX 0");
        SavePowerConfig();

        AppSettings.StreamStabilizerMaxPercentMHz = (int)StreamStabilizerTargetPercent.Value;
        AppSettings.SaveSettings();
    }

    #region PowerConfig Helpers

    public static void SetPowerConfig(string arguments)
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

    public static void SavePowerConfig()
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

    private void CurveOptimizerCustom_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) => CurveOptimizerCustom.IsOn = !CurveOptimizerCustom.IsOn;

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
            _profile[_indexprofile].Cpu1Value = c1v.Value;
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
            _profile[_indexprofile].Cpu2Value = c2v.Value;
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
            _profile[_indexprofile].Cpu3Value = c3v.Value;
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
            _profile[_indexprofile].Cpu4Value = c4v.Value;
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
            _profile[_indexprofile].Cpu5Value = c5v.Value;
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
            _profile[_indexprofile].Cpu6Value = c6v.Value;
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
            _profile[_indexprofile].Gpu9Value = g9v.Value;
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
            _profile[_indexprofile].Gpu10Value = g10v.Value;
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
        var check = c1.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Cpu1 = check;
            _profile[_indexprofile].Cpu1Value = c1v.Value;
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
        var check = c2.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Cpu2 = check;
            _profile[_indexprofile].Cpu2Value = c2v.Value;
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
        var check = c3.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Cpu3 = check;
            _profile[_indexprofile].Cpu3Value = c3v.Value;
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
        var check = c4.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Cpu4 = check;
            _profile[_indexprofile].Cpu4Value = c4v.Value;
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
        var check = c5.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Cpu5 = check;
            _profile[_indexprofile].Cpu5Value = c5v.Value;
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
        var check = c6.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Cpu6 = check;
            _profile[_indexprofile].Cpu6Value = c6v.Value;
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
        var check = g9.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Gpu9 = check;
            _profile[_indexprofile].Gpu9Value = g9v.Value;
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
        var check = g10.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Gpu10 = check;
            _profile[_indexprofile].Gpu10Value = g10v.Value;
            ProfileSave();
        }
    }
    #endregion

    #region NumberBoxes
    private void C2t_FocusEngaged(object sender, object args)
    {
        if (sender is NumberBox numberBox)
        {
            numberBox.SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden;
        }
    }

    private void C2t_FocusDisengaged(object sender, object args)
    {
        if (sender is NumberBox numberBox)
        {
            numberBox.SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline;
        }
    }

    private void C2t_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        {
            object slider;
            if (sender.Name.Contains('v'))
            {
                slider = FindName(sender.Name.Replace('t', 'V').Replace('v', 'V'));
            }
            else
            {
                try
                {
                    slider = FindName(sender.Name.Replace('t', 'v'));
                }
                catch (Exception ex)
                {
                    LogHelper.TraceIt_TraceError(ex);
                    return;
                }
            }

            if (slider is Slider slider1)
            {
                if (slider1.Maximum < sender.Value)
                {
                    slider1.Maximum = ПараметрыPage.FromValueToUpperFive(sender.Value);
                }
            }
        }
    }






    #endregion

    #endregion

    #endregion

}