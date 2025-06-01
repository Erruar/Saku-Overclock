using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.JsonContainers;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.Styles;
using Saku_Overclock.ViewModels;
using Windows.Foundation.Metadata;
using static ZenStates.Core.Cpu;

namespace Saku_Overclock.Views;

public sealed partial class ПресетыPage
{
    private static readonly IAppSettingsService AppSettings = App.GetService<IAppSettingsService>();
    private bool _isLoaded; // Загружена ли корректно страница для применения изменений 
    private bool _waitforload = true; // Ожидание окончательной смены профиля на другой. Активируется при смене профиля 
    private static Profile[] _profile = new Profile[1]; // Всегда по умолчанию будет 1 профиль
    private int _indexprofile = 0; // Выбранный профиль
    private readonly IBackgroundDataUpdater? _dataUpdater;
    private CodeName? _codeName;
    private static readonly IAppNotificationService NotificationsService = App.GetService<IAppNotificationService>(); // Уведомления приложения
    private static readonly ISendSmuCommandService SendSmuCommand = App.GetService<ISendSmuCommandService>();
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
        LoadProfiles();

        // Загрузить остальные UI элементы, функции блока "Дополнительно"
        RtssOverlaySwitch.IsOn = AppSettings.RtssMetricsEnabled;
        TrayMonFeatSwitch.IsOn = AppSettings.NiIconsEnabled;
        StreamOptimizerSwitch.IsOn = AppSettings.StreamOptimizerEnabled;

        if (AppSettings.CurveOptimizerOverallEnabled)
        {
            switch (AppSettings.CurveOptimizerOverallLevel)
            {
                case 0:
                default:
                    CurveSetOnly(CurveOptions_Disabled);
                    break;
                case 1:
                    CurveSetOnly(CurveOptions_Light);
                    break;
                case 2:
                    CurveSetOnly(CurveOptions_Effective);
                    break;
            }
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
            AdvancedGpu_OptionsPanel_0.Visibility = Visibility.Collapsed;
            AdvancedGpu_OptionsPanel_1.Visibility = Visibility.Collapsed;
            AdvancedGpu_OptionsPanel_2.Visibility = Visibility.Collapsed;
            AdvancedGpu_OptionsPanel_3.Visibility = Visibility.Collapsed;
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
                            _profile[AppSettings.Preset].profilename == profile.profilename &&
                            _profile[AppSettings.Preset].profiledesc == profile.profiledesc &&
                            _profile[AppSettings.Preset].profileicon == profile.profileicon;


            var toggleButton = new ProfileItem
            {
                IsSelected = isChecked,
                IconGlyph = profile.profileicon == string.Empty ? "\uE718" : profile.profileicon,
                Text = profile.profilename,
                Description = profile.profiledesc != string.Empty ? profile.profiledesc : profile.profilename
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
            IsSelected = AppSettings.Preset == -1 && AppSettings.PremadeMaxActivated,
            IconGlyph = "\ue945",
            Text = "Preset_Speed_Name/Text".GetLocalized(), // Speed
            Description = "Preset_Speed_Desc/Text".GetLocalized()
        });
        ProfilesControl.Items.Add(new ProfileItem
        {
            IsSelected = AppSettings.Preset == -1 && AppSettings.PremadeMaxActivated,
            IconGlyph = "\uec49",
            Text = "Preset_Balance_Name/Text".GetLocalized(), // Balance
            Description = "Preset_Balance_Desc/Text".GetLocalized()
        });
        ProfilesControl.Items.Add(new ProfileItem
        {
            IsSelected = AppSettings.Preset == -1 && AppSettings.PremadeMaxActivated,
            IconGlyph = "\uec0a",
            Text = "Preset_Eco_Name/Text".GetLocalized(), // Eco
            Description = "Preset_Eco_Desc/Text".GetLocalized()
        });
        ProfilesControl.Items.Add(new ProfileItem
        {
            IsSelected = AppSettings.Preset == -1 && AppSettings.PremadeMaxActivated,
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
                    ProfileSettings_ChangeViewStackPanel.Visibility = Visibility.Collapsed;
                    ProfileSettings_StackPanel.Visibility = Visibility.Collapsed;
                    ProfileSettings_BeginnerView.Visibility = Visibility.Collapsed;
                    PremadeProfile_AffectsOn.Visibility = Visibility.Visible;
                    EditProfileButton.Visibility = Visibility.Collapsed;
                }
                else
                {
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

    private async Task MainInitAsync(int index)
    {
        _waitforload = true;

        ProfileLoad();
        try
        {
            if (_profile[index].cpu1value > c1v.Maximum)
            {
                c1v.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].cpu1value);
            }

            if (_profile[index].cpu2value > BaseTdp_Slider.Maximum)
            {
                BaseTdp_Slider.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].cpu2value);
            }

            if (_profile[index].cpu2value > c2v.Maximum)
            {
                c2v.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].cpu2value);
            }

            if (_profile[index].cpu3value > c3v.Maximum)
            {
                c3v.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].cpu3value);
            }

            if (_profile[index].cpu4value > c4v.Maximum)
            {
                c4v.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].cpu4value);
            }

            if (_profile[index].cpu5value > c5v.Maximum)
            {
                c5v.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].cpu5value);
            }

            if (_profile[index].cpu6value > c6v.Maximum)
            {
                c6v.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].cpu6value);
            }

            if (_profile[index].vrm1value > V1V.Maximum)
            {
                V1V.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].vrm1value);
            }

            if (_profile[index].vrm2value > V2V.Maximum)
            {
                V2V.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].vrm2value);
            }

            if (_profile[index].vrm3value > V3V.Maximum)
            {
                V3V.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].vrm3value);
            }

            if (_profile[index].vrm4value > V4V.Maximum)
            {
                V4V.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].vrm4value);
            }

            if (_profile[index].gpu9value > g9v.Maximum)
            {
                g9v.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].gpu9value);
            }

            if (_profile[index].gpu10value > g10v.Maximum)
            {
                g10v.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].gpu10value);
            }
        }
        catch (Exception ex)
        {
            await LogHelper.TraceIt_TraceError(ex.ToString());
        }

        try
        {
            // Заранее скомпилированная функция увеличения TDP, созданная специально для фирменной функции Smart TDP
            var fineTunedTdp = ПараметрыPage.FromValueToUpperFive(1.17335141 * _profile[index].cpu2value + 0.21631949);

            c2.IsChecked = _profile[index].cpu2;
            c2v.Value = _profile[index].cpu2value;

            BaseTdp_Slider.Value = _profile[index].cpu2value;
            if (_profile[index].cpu2 && _profile[index].cpu3 && _profile[index].cpu4 &&
                _profile[index].cpu2value == _profile[index].cpu4value && _profile[index].cpu3value == fineTunedTdp)
            {
                SmartTdp.IsOn = true;
            }
            else
            {
                if (SendSmuCommand.IsPlatformPC(CpuSingleton.GetInstance()) != false && SettingsViewModel.VersionId != 5) // Если устройство - не ноутбук
                {
                    // Так как на компьютерах невозможно выставить другие Power лимиты
                    if (!_profile[index].cpu2 && !_profile[index].cpu4 && _profile[index].cpu3value == fineTunedTdp)
                    {
                        SmartTdp.IsOn = true;
                    }
                }
                else
                {
                    SmartTdp.IsOn = false;
                }
            }

            c1.IsChecked = _profile[index].cpu1;
            c1v.Value = _profile[index].cpu1value;
            c3.IsChecked = _profile[index].cpu3;
            c3v.Value = _profile[index].cpu3value;
            c4.IsChecked = _profile[index].cpu4;
            c4v.Value = _profile[index].cpu4value;
            c5.IsChecked = _profile[index].cpu5;
            c5v.Value = _profile[index].cpu5value;
            c6.IsChecked = _profile[index].cpu6;
            c6v.Value = _profile[index].cpu6value;


            if (IsRavenFamily())
            {
                if (_profile[index].gpu10 == true && _profile[index].gpu9 == true && _profile[index].gpu10value == 1200)
                {
                    if (_profile[index].gpu9value == 800)
                    {
                        IgpuEnchancementCombo.SelectedIndex = 1;
                    }
                    if (_profile[index].gpu9value == 1000)
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
                if (_profile[index].advncd10 == true)
                {
                    if (_profile[index].advncd10value == 1750)
                    {
                        IgpuEnchancementCombo.SelectedIndex = 1;
                    }
                    if (_profile[index].advncd10value == 2200)
                    {
                        IgpuEnchancementCombo.SelectedIndex = 2;
                    }
                }
                else
                {
                    IgpuEnchancementCombo.SelectedIndex = 0;
                }
            }

            if (!_profile[index].cpu5 && !_profile[index].cpu6)
            {
                TurboSetOnly(Turbo_LightModeToggle);
            }
            else
            {
                if ((_profile[index].cpu5 && !_profile[index].cpu6) || (!_profile[index].cpu5 && _profile[index].cpu6))
                {
                    TurboSetOnly(Turbo_LightModeToggle);
                }
                if (_profile[index].cpu5 && _profile[index].cpu6)
                {
                    if (_profile[index].cpu5value == 400 && _profile[index].cpu6value == 3)
                    {
                        TurboSetOnly(Turbo_BalanceModeToggle);
                    }
                    else if (_profile[index].cpu5value == 5000 && _profile[index].cpu6value == 1)
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
            if (AppSettings.CurveOptimizerOverallEnabled)
            {
                switch (AppSettings.CurveOptimizerOverallLevel)
                {
                    case 0:
                    default:
                        CurveSetOnly(CurveOptions_Disabled);
                        break;
                    case 1:
                        CurveSetOnly(CurveOptions_Light);
                        break;
                    case 2:
                        CurveSetOnly(CurveOptions_Effective);
                        break;
                }
            }

            V1.IsChecked = _profile[index].vrm1;
            V1V.Value = _profile[index].vrm1value;
            V2.IsChecked = _profile[index].vrm2;
            V2V.Value = _profile[index].vrm2value;
            V3.IsChecked = _profile[index].vrm3;
            V3V.Value = _profile[index].vrm3value;
            V4.IsChecked = _profile[index].vrm4;
            V4V.Value = _profile[index].vrm4value;
            g9v.Value = _profile[index].gpu9value;
            g9.IsChecked = _profile[index].gpu9;
            g10v.Value = _profile[index].gpu10value;
            g10.IsChecked = _profile[index].gpu10;
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
    private void CurveSetOnly(ToggleButton button)
    {
        CurveOptions_Disabled.IsChecked = false;
        CurveOptions_Light.IsChecked = false;
        CurveOptions_Effective.IsChecked = false;

        switch (button.Name)
        {
            case "CurveOptions_Disabled":
                CurveOptions_Disabled.IsChecked = true;
                break;
            case "CurveOptions_Light":
                CurveOptions_Light.IsChecked = true;
                break;
            case "CurveOptions_Effective":
                CurveOptions_Effective.IsChecked = true;
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
            LogHelper.TraceIt_TraceError(ex.ToString());
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
            LogHelper.TraceIt_TraceError(ex.ToString());
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
    private void RtssOverlaySwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) { return; }
        AppSettings.RtssMetricsEnabled = RtssOverlaySwitch.IsOn;
        AppSettings.SaveSettings();
    }

    private void TrayMonFeatSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) { return; }
        AppSettings.NiIconsEnabled = TrayMonFeatSwitch.IsOn;
        AppSettings.SaveSettings();
    }

    private void StreamOptimizerSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) { return; }
        AppSettings.StreamOptimizerEnabled = StreamOptimizerSwitch.IsOn;
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
        BaseTdp_Text.Text = Math.Round(e.NewValue, 0).ToString() + "W";
        if (!_isLoaded || _waitforload) { return; }
        ChangedBaseTdp_Value();
    }

    private void SmartTdp_Toggled(object sender, RoutedEventArgs e)
    {
        ChangedBaseTdp_Value();
    }

    private void ChangedBaseTdp_Value()
    {
        if (_waitforload) { return; }
        ProfileLoad();
        var index = _indexprofile == -1 ? 0 : _indexprofile;

        // Заранее скомпилированная функция увеличения TDP, созданная специально для фирменной функции Smart TDP
        var fineTunedTdp = ПараметрыPage.FromValueToUpperFive(1.17335141 * BaseTdp_Slider.Value + 0.21631949);

        c2.IsChecked = true;
        _profile[index].cpu2 = true;
        c3.IsChecked = true;
        _profile[index].cpu3 = true;
        c4.IsChecked = true;
        _profile[index].cpu4 = true;

        if (fineTunedTdp > c3v.Maximum || BaseTdp_Slider.Value > c2v.Maximum || BaseTdp_Slider.Value > c4v.Maximum)
        {
            c2v.Maximum = ПараметрыPage.FromValueToUpperFive(BaseTdp_Slider.Value);
            c3v.Maximum = ПараметрыPage.FromValueToUpperFive(fineTunedTdp);
            c4v.Maximum = ПараметрыPage.FromValueToUpperFive(BaseTdp_Slider.Value);
        }
        c2v.Value = BaseTdp_Slider.Value;
        _profile[index].cpu2value = BaseTdp_Slider.Value;

        c4v.Value = BaseTdp_Slider.Value;
        _profile[index].cpu4value = BaseTdp_Slider.Value;

        if (SmartTdp.IsOn)
        {
            c3v.Value = fineTunedTdp;
            _profile[index].cpu3value = fineTunedTdp;
        }
        else
        {
            c3v.Value = BaseTdp_Slider.Value;
            _profile[index].cpu3value = BaseTdp_Slider.Value;
        }
        if (SendSmuCommand.IsPlatformPC(CpuSingleton.GetInstance()) != false && SettingsViewModel.VersionId != 5) // Если устройство - не ноутбук
        {
            // Так как на компьютерах невозможно выставить другие Power лимиты
            c2.IsChecked = false; // Отключить STAPM
            _profile[index].cpu2 = false;
            c4.IsChecked = false; // Отключить Slow лимит
            _profile[index].cpu4 = false;
        }
        ProfileSave();
    }

    // Grid-помощник, который активирует переключатель когда пользователь нажал на область возле него
    private void SmartTdp_PointerPressed(object sender, object e)
    {
        SmartTdp.IsOn = SmartTdp.IsOn == false;
    }

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
                _profile[index].cpu5 = false;
                _profile[index].cpu6 = false;
                c5.IsChecked = false;
                c6.IsChecked = false;
            }
            else if (toggle.Name == "Turbo_BalanceModeToggle")
            {
                TurboSetOnly(Turbo_BalanceModeToggle);
                _profile[index].cpu5 = true;
                _profile[index].cpu6 = true;
                _profile[index].cpu5value = 400;
                _profile[index].cpu6value = 3;
                c5.IsChecked = true;
                c6.IsChecked = true;
                c5v.Value = 400;
                c6v.Value = 3;
            }
            else if (toggle.Name == "Turbo_HeavyModeToggle")
            {
                TurboSetOnly(Turbo_HeavyModeToggle);
                _profile[index].cpu5 = true;
                _profile[index].cpu6 = true;
                _profile[index].cpu5value = 5000;
                _profile[index].cpu6value = 1;
                c5.IsChecked = true;
                c6.IsChecked = true;
                c5v.Maximum = 5000;
                c5v.Value = 5000;
                c6v.Value = 1;
            }
        }

        ProfileSave();
    }

    private void CurveOptions_Disabled_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload) { return; }

        var toggle = sender as ToggleButton;
        if (CurveOptions_Disabled.IsChecked == false
            && CurveOptions_Effective.IsChecked == false
            && CurveOptions_Light.IsChecked == false)
        {
            toggle!.IsChecked = true;
        }
        else
        {
            if (toggle!.Name == "CurveOptions_Disabled")
            {
                CurveSetOnly(CurveOptions_Disabled);
                AppSettings.CurveOptimizerOverallEnabled = false;
                AppSettings.CurveOptimizerOverallLevel = 0;

                MainWindow.Applyer.Apply(CurveOptimizerGenerateStringHelper(0), false, false, 0d);
                if (AppSettings.Preset > -1)
                {
                    ShellPage.MandarinSparseUnitProfile(_profile[AppSettings.Preset]);
                }
            }
            else if (toggle.Name == "CurveOptions_Light")
            {
                CurveSetOnly(CurveOptions_Light);
                AppSettings.CurveOptimizerOverallEnabled = true;
                AppSettings.CurveOptimizerOverallLevel = 1;
                MainWindow.Applyer.Apply(CurveOptimizerGenerateStringHelper(-15), false, false, 0d);
                if (AppSettings.Preset > -1)
                {
                    ShellPage.MandarinSparseUnitProfile(_profile[AppSettings.Preset]);
                }
            }
            else if (toggle.Name == "CurveOptions_Effective")
            {
                CurveSetOnly(CurveOptions_Effective);
                AppSettings.CurveOptimizerOverallEnabled = true;
                AppSettings.CurveOptimizerOverallLevel = 2;
                MainWindow.Applyer.Apply(CurveOptimizerGenerateStringHelper(-25), false, false, 0d);
                if (AppSettings.Preset > -1)
                {
                    ShellPage.MandarinSparseUnitProfile(_profile[AppSettings.Preset]);
                }
            }
        }

        AppSettings.SaveSettings();
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
                    _profile[index].gpu10 = false;
                    _profile[index].gpu9 = false;
                }
                else
                {
                    _profile[index].advncd10 = false;
                }
                break;
            case 1:
                if (IsRavenFamily())
                {
                    _profile[index].gpu10 = true;
                    _profile[index].gpu10value = 1200;
                    _profile[index].gpu9 = true;
                    _profile[index].gpu9value = 800;
                }
                else
                {
                    _profile[index].advncd10 = true;
                    _profile[index].advncd10value = 1750;
                }
                break;
            case 2:
                if (IsRavenFamily())
                {
                    _profile[index].gpu10 = true;
                    _profile[index].gpu10value = 1200;
                    _profile[index].gpu9 = true;
                    _profile[index].gpu9value = 1000;
                }
                else
                {
                    _profile[index].advncd10 = true;
                    _profile[index].advncd10value = 2200;
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

    private static string CurveOptimizerGenerateStringHelper(int value) => (value >= 0) ?
    $" --set-coall={value} " : $" --set-coall={Convert.ToUInt32(0x100000 - (uint)(-1 * value))} ";

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
                        _profile[0] = new Profile { profilename = SaveProfileN.Text, profiledesc = SaveProfileD.Text };
                    }
                    else
                    {
                        var profileList = new List<Profile>(_profile)
                        {
                            new()
                            {
                                profilename = SaveProfileN.Text,
                                profiledesc = SaveProfileD.Text
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
            await LogHelper.TraceIt_TraceError(exception.ToString());
        }
    }
    private async void EditProfileButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await LogHelper.Log($"Editing profile name: From \"{_profile[_indexprofile].profilename}\" To \"{EditProfileN.Text}\"");
            EditProfileButton.Flyout.Hide();
            if (EditProfileN.Text != "")
            {
                ProfileLoad();
                _profile[_indexprofile].profilename = EditProfileN.Text;
                _profile[_indexprofile].profiledesc = EditProfileD.Text;
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
            await LogHelper.TraceIt_TraceError(exception.ToString());
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
                await LogHelper.Log($"Showing delete profile dialog: deleting profile \"{_profile[indexprofile].profilename}\"");
                ProfileLoad();
                _waitforload = true;
                var profileList = new List<Profile>(_profile);
                profileList.RemoveAt(indexprofile);
                _profile = [.. profileList];
                _waitforload = false;
                AppSettings.Preset = 0;
                _indexprofile = 0;
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
            await LogHelper.TraceIt_TraceError(exception.ToString());
        }
    }
    private void EditProfileButton_Click_1(object sender, RoutedEventArgs e)
    {
        EditProfileN.Text = _profile[_indexprofile].profilename;
        EditProfileD.Text = _profile[_indexprofile].profiledesc;
    }

    #endregion

    #endregion

    #region Page Helpers Events

    private void TryAdvancedButton_Click(object sender, RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ПараметрыViewModel).FullName!);
    }

    #endregion

    #region OLD Methods
    private void Min_btn_Checked()
    {
        AppSettings.PremadeMinActivated = true;
        AppSettings.PremadeEcoActivated = false;
        AppSettings.PremadeBalanceActivated = false;
        AppSettings.PremadeSpeedActivated = false;
        AppSettings.PremadeMaxActivated = false;
        AppSettings.RyzenAdjLine =
            " --tctl-temp=60 " + //
            "--stapm-limit=9000 " + //
            "--fast-limit=9000 " + //
            "--stapm-time=900 " + //
            "--slow-limit=6000 " + //
            "--slow-time=900 " + //
            "--vrm-current=120000 " + //
            "--vrmmax-current=120000 " + //
            "--vrmsoc-current=120000 " + //
            "--vrmsocmax-current=120000 " + //
            "--vrmgfx-current=120000 " +
            "--prochot-deassertion-ramp=2 ";
        AppSettings.SaveSettings();
        MainWindow.Applyer.Apply(AppSettings.RyzenAdjLine, false, AppSettings.ReapplyOverclock,
            AppSettings.ReapplyOverclockTimer);
    }

    private void Eco_Checked()
    {
        AppSettings.PremadeMinActivated = false;
        AppSettings.PremadeEcoActivated = true;
        AppSettings.PremadeBalanceActivated = false;
        AppSettings.PremadeSpeedActivated = false;
        AppSettings.PremadeMaxActivated = false;
        AppSettings.RyzenAdjLine =
            " --tctl-temp=68 --stapm-limit=15000 " +
            " --fast-limit=18000 --stapm-time=500" +
            " --slow-limit=16000 --slow-time=500 " +
            "--vrm-current=120000 --vrmmax-current=120000 " +
            "--vrmsoc-current=120000 --vrmsocmax-current=120000" +
            " --vrmgfx-current=120000 --prochot-deassertion-ramp=2 ";
        AppSettings.SaveSettings();
        MainWindow.Applyer.Apply(AppSettings.RyzenAdjLine, false, AppSettings.ReapplyOverclock,
            AppSettings.ReapplyOverclockTimer);
    }

    private void Balance_Checked()
    {
        AppSettings.PremadeMinActivated = false;
        AppSettings.PremadeEcoActivated = false;
        AppSettings.PremadeBalanceActivated = true;
        AppSettings.PremadeSpeedActivated = false;
        AppSettings.PremadeMaxActivated = false;
        AppSettings.RyzenAdjLine =
            " --tctl-temp=75 --stapm-limit=17000 " +
            " --fast-limit=20000 --stapm-time=64 " +
            "--slow-limit=19000 --slow-time=128 " +
            "--vrm-current=120000 --vrmmax-current=120000" +
            " --vrmsoc-current=120000 --vrmsocmax-current=120000" +
            " --vrmgfx-current=120000 --prochot-deassertion-ramp=2";
        AppSettings.SaveSettings();
        MainWindow.Applyer.Apply(AppSettings.RyzenAdjLine, false, AppSettings.ReapplyOverclock,
            AppSettings.ReapplyOverclockTimer);
    }

    private void Speed_Checked()
    {
        AppSettings.PremadeMinActivated = false;
        AppSettings.PremadeEcoActivated = false;
        AppSettings.PremadeBalanceActivated = false;
        AppSettings.PremadeSpeedActivated = true;
        AppSettings.PremadeMaxActivated = false;
        AppSettings.RyzenAdjLine =
            " --tctl-temp=80 --stapm-limit=20000  --fast-limit=20000 --stapm-time=32 --slow-limit=20000 --slow-time=64 --vrm-current=120000 --vrmmax-current=120000 --vrmsoc-current=120000 --vrmsocmax-current=120000 --vrmgfx-current=120000 --prochot-deassertion-ramp=2";
        AppSettings.SaveSettings();
        MainWindow.Applyer.Apply(AppSettings.RyzenAdjLine, false, AppSettings.ReapplyOverclock,
            AppSettings.ReapplyOverclockTimer);
    }

    private void Max_btn_Checked()
    {
        AppSettings.PremadeMinActivated = false;
        AppSettings.PremadeEcoActivated = false;
        AppSettings.PremadeBalanceActivated = false;
        AppSettings.PremadeSpeedActivated = false;
        AppSettings.PremadeMaxActivated = true;
        AppSettings.RyzenAdjLine =
            " --tctl-temp=90 --stapm-limit=45000  --fast-limit=60000 --stapm-time=80 --slow-limit=60000 --slow-time=1 --vrm-current=120000 --vrmmax-current=120000 --vrmsoc-current=120000 --vrmsocmax-current=120000 --vrmgfx-current=120000 --prochot-deassertion-ramp=2";
        AppSettings.SaveSettings();
        MainWindow.Applyer.Apply(AppSettings.RyzenAdjLine, false, AppSettings.ReapplyOverclock,
            AppSettings.ReapplyOverclockTimer);
    }

    #endregion

    #endregion

    #region Profile Settings Events

    private async void ProfilesControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        //await App.MainWindow.ShowMessageDialogAsync((sender as Saku_Overclock.Styles.ProfileSelector).SelectedItem.Text, "Selected:");
        //(sender as Saku_Overclock.Styles.ProfileSelector).SelectedItem.Text
        var selectedItem = (sender as ProfileSelector)!.SelectedItem;
        if (selectedItem != null)
        {
            // Корректное отображение описания, даже если оно маленькое (чтобы Grid изменил свой размер корректно и слова не обрывались)
            ProfileSettings_InfoRow.Height = new GridLength(0);
            ProfileSettings_InfoRow.Height = GridLength.Auto;

            SelectedProfile_Name.Text = selectedItem.Text;

            // Обход отсутствия описания, при помощи записывания имени пресета в описание. Чтобы не отображать два раза одну и ту же строку, описание пресета скрывается (так как его нет)
            SelectedProfile_Description.Text = selectedItem.Description != selectedItem.Text ? selectedItem.Description : string.Empty;

            if (_doubleClickApply == SelectedProfile_Name.Text + SelectedProfile_Description.Text)
            {
                ApplyButton_Click(null,null);
            }
            _doubleClickApply = SelectedProfile_Name.Text + SelectedProfile_Description.Text;
            
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
                ProfileSettings_StackPanel.Visibility = Visibility.Collapsed;
                ProfileSettings_BeginnerView.Visibility = Visibility.Collapsed;
                PremadeProfile_AffectsOn.Visibility = Visibility.Visible;
                EditProfileButton.Visibility = Visibility.Collapsed;
                ProfileSettings_ChangeViewStackPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
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
                    if ((_profile[i].profiledesc == selectedItem.Description || _profile[i].profilename == selectedItem.Description) &&
                        _profile[i].profilename == selectedItem.Text &&
                        _profile[i].profileicon == selectedItem.IconGlyph)
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
                if (profile.profilename == name &&
                    (profile.profiledesc == desc || profile.profilename == desc) &&
                    (profile.profileicon == icon ||
                     profile.profileicon == "\uE718"))
                {
                    ПараметрыPage.ApplyInfo = string.Empty;
                    ShellPage.MandarinSparseUnitProfile(profile, true);

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

        ShellPage.NextPremadeProfile_Activate(endMode);

        var (_, _, _, settings, _) = ShellPage.PremadedProfiles[endMode];

        AppSettings.RyzenAdjLine = settings;
        AppSettings.SaveSettings();

        MainWindow.Applyer.ApplyWithoutAdjLine(false);

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
            _profile[_indexprofile].cpu1value = c1v.Value;
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
            _profile[_indexprofile].cpu2value = c2v.Value;
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
            _profile[_indexprofile].cpu3value = c3v.Value;
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
            _profile[_indexprofile].cpu4value = c4v.Value;
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
            _profile[_indexprofile].cpu5value = c5v.Value;
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
            _profile[_indexprofile].cpu6value = c6v.Value;
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
            _profile[_indexprofile].vrm1value = V1V.Value;
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
            _profile[_indexprofile].vrm2value = V2V.Value;
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
            _profile[_indexprofile].vrm3value = V3V.Value;
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
            _profile[_indexprofile].vrm4value = V4V.Value;
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
            _profile[_indexprofile].gpu9value = g9v.Value;
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
            _profile[_indexprofile].gpu10value = g10v.Value;
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
            _profile[_indexprofile].cpu1 = check;
            _profile[_indexprofile].cpu1value = c1v.Value;
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
            _profile[_indexprofile].cpu2 = check;
            _profile[_indexprofile].cpu2value = c2v.Value;
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
            _profile[_indexprofile].cpu3 = check;
            _profile[_indexprofile].cpu3value = c3v.Value;
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
            _profile[_indexprofile].cpu4 = check;
            _profile[_indexprofile].cpu4value = c4v.Value;
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
            _profile[_indexprofile].cpu5 = check;
            _profile[_indexprofile].cpu5value = c5v.Value;
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
            _profile[_indexprofile].cpu6 = check;
            _profile[_indexprofile].cpu6value = c6v.Value;
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
            _profile[_indexprofile].vrm1 = check;
            _profile[_indexprofile].vrm1value = V1V.Value;
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
            _profile[_indexprofile].vrm2 = check;
            _profile[_indexprofile].vrm2value = V2V.Value;
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
            _profile[_indexprofile].vrm3 = check;
            _profile[_indexprofile].vrm3value = V3V.Value;
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
            _profile[_indexprofile].vrm4 = check;
            _profile[_indexprofile].vrm4value = V4V.Value;
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
            _profile[_indexprofile].gpu9 = check;
            _profile[_indexprofile].gpu9value = g9v.Value;
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
            _profile[_indexprofile].gpu10 = check;
            _profile[_indexprofile].gpu10value = g10v.Value;
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
                    LogHelper.TraceIt_TraceError(ex.ToString());
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