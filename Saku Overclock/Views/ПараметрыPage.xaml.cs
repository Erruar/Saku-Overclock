using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Models;
using Saku_Overclock.Services;
using Saku_Overclock.ViewModels;
using Windows.Foundation.Metadata;
using static Saku_Overclock.Services.CpuService;
using VisualTreeHelper = Saku_Overclock.Helpers.VisualTreeHelper;

namespace Saku_Overclock.Views;

public sealed partial class ПараметрыPage
{
    private readonly IAppNotificationService _notificationsService = App.GetService<IAppNotificationService>(); // Уведомления приложения
    private readonly IKeyboardHotkeysService _hotkeysService = App.GetService<IKeyboardHotkeysService>();
    private readonly IApplyerService _applyer = App.GetService<IApplyerService>();
    private readonly IOcFinderService _ocFinder = App.GetService<IOcFinderService>();
    private readonly ICpuService _cpu = App.GetService<ICpuService>();
    private int _presetIndex; // Выбранный пресет
    private readonly IAppSettingsService _appSettings = App.GetService<IAppSettingsService>(); // Все настройки приложения
    private readonly IPresetManagerService _presetManager = App.GetService<IPresetManagerService>(); // Менеджер пресетов разгона
    private readonly List<string> _searchItems = [];

    private bool _isLoaded; // Загружена ли корректно страница для применения изменений
    private bool _presetChanging = true; // Ожидание окончательной смены пресета на другой. Активируется при смене пресета
    private bool _commandReturnedValue; // Флаг если команда вернула значение
    private readonly bool _isPremadePresetApplied; // Флаг применённого готового пресета для его восстановления после покидания страницы Разгон

    public static string ApplyInfo
    {
        get;
        set;
    } = "";

    public static bool SettingsApplied
    {
        get;
        set;
    }

    public ПараметрыPage()
    {
        InitializeComponent();

        _presetIndex = _appSettings.Preset;

        _hotkeysService.PresetChanged += PresetChanged;

        if (_appSettings.Preset == -1)
        {
            _isPremadePresetApplied = true;
        }

        Loaded += ПараметрыPage_Loaded;
        Unloaded += ПараметрыPage_Unloaded;
    }


    #region Initialization

    #region Page Load 

    private void ПараметрыPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _isLoaded = true;
            CollectSearchItems();
            SlidersInit();
            RecommendationsInit();
        }
        catch (Exception exception)
        {
            LogHelper.TraceIt_TraceError(exception);
        }
    }

    private void ПараметрыPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _hotkeysService.PresetChanged -= PresetChanged;

        Loaded -= ПараметрыPage_Loaded;
        Unloaded -= ПараметрыPage_Unloaded;
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);

        if (_isPremadePresetApplied && _isLoaded)
        {
            _appSettings.Preset = -1;
            _appSettings.SaveSettings();
        }
    }

    #endregion

    #region Initialization

    private void RecommendationsInit()
    {
        var data = _ocFinder.GetPerformanceRecommendationData();
        TempRecommend0.Text = data.TemperatureLimits[0];
        TempRecommend1.Text = data.TemperatureLimits[1];
        StapmRecommend0.Text = data.StapmLimits[0];
        StapmRecommend1.Text = data.StapmLimits[1];
        FastRecommend0.Text = data.FastLimits[0];
        FastRecommend1.Text = data.FastLimits[1];
        SlowRecommend0.Text = data.SlowLimits[0];
        SlowRecommend1.Text = data.SlowLimits[1];
        SttRecommend0.Text = data.StapmLimits[0];
        SttRecommend1.Text = data.StapmLimits[1];
        SlowTimeRecommend0.Text = data.SlowTime[0];
        SlowTimeRecommend1.Text = data.SlowTime[1];
        StapmTimeRecommend0.Text = data.StapmTime[0];
        StapmTimeRecommend1.Text = data.StapmTime[1];
        BdProchotTimeRecommend0.Text = data.ProchotRampTime[0];
        BdProchotTimeRecommend0.Text = data.ProchotRampTime[1];
    }
    private void SlidersInit()
    {
        if (!_isLoaded)
        {
            return;
        }

        _presetChanging = true;

        PresetCom.Items.Clear();
        PresetCom.Items.Add(new ComboBoxItem
        {
            Content = new TextBlock
            {
                Text = "Param_PremadeCombo/Content".GetLocalized(),
                Foreground = (Brush)Application.Current.Resources["AccentTextFillColorTertiaryBrush"]
            },
            IsEnabled = false
        });

        LoadPresetsToComboBox();

        if (_appSettings.Preset > _presetManager.Presets.Length)
        {
            _appSettings.Preset = 0;
            _appSettings.SaveSettings();
        }
        else
        {
            if (_appSettings.Preset == -1)
            {
                if (_presetManager.Presets.Length == 0)
                {
                    _presetManager.Presets = new Preset[1];
                    _presetManager.Presets[0] = new Preset();
                    PresetCom.Items.Add(_presetManager.Presets[0].PresetName);
                }


                _presetIndex = 0;
                PresetCom.SelectedIndex = 1;
                _appSettings.SaveSettings();
            }
            else
            {
                _presetIndex = _appSettings.Preset;
                if (PresetCom.Items.Count >= _presetIndex + 1)
                {
                    PresetCom.SelectedIndex = _presetIndex + 1;
                }
            }
        }

        MainInit(PresetCom.SelectedIndex - 1);

        _presetChanging = false;
    }

    private void LoadPresetsToComboBox()
    {
        foreach (var currPreset in _presetManager.Presets)
        {
            var presetName = currPreset.PresetName;
            if (currPreset.PresetName.Contains("Preset_"))
            {
                presetName = ГлавнаяPage.TryLocalize(presetName); 
            }
            PresetCom.Items.Add(presetName);
        }
    }

    //Убрать параметры для ноутбуков
    private void LaptopCpu_FP5_HideUnavailableParameters()
    {
        AdvLaptopAplusALimit.Visibility = Visibility.Collapsed;
        AdvLaptopAplusALimitDesc.Visibility = Visibility.Collapsed;
        AdvLaptopIGpuLimit.Visibility = Visibility.Collapsed;
        AdvLaptopIGpuLimitDesc.Visibility = Visibility.Collapsed;
        AdvLaptopIGpuTemp.Visibility = Visibility.Collapsed;
        AdvLaptopIGpuTempDesc.Visibility = Visibility.Collapsed;
        AdvLaptopDGpuTemp.Visibility = Visibility.Collapsed;
        AdvLaptopDGpuTempDesc.Visibility = Visibility.Collapsed;
        VrmLaptopsPsiCpu.Visibility = Visibility.Collapsed;
        VrmLaptopsPsiCpuDesc.Visibility = Visibility.Collapsed;
        VrmLaptopsPsiIGpu.Visibility = Visibility.Collapsed;
        VrmLaptopsPsiIGpuDesc.Visibility = Visibility.Collapsed;
    }

    private void DesktopCpu_AM4_HideUnavailableParameters()
    {
        LaptopsAvgWattage.Visibility = Visibility.Collapsed;
        LaptopsAvgWattageDesc.Visibility = Visibility.Collapsed;
        LaptopsFastSpeed.Visibility = Visibility.Collapsed;
        LaptopsFastSpeedDesc.Visibility = Visibility.Collapsed;
        LaptopsSlowSpeed.Visibility = Visibility.Collapsed;
        LaptopsSlowSpeedDesc.Visibility = Visibility.Collapsed;
        AdvLaptopIGpuLimit.Visibility = Visibility.Collapsed;
        AdvLaptopIGpuLimitDesc.Visibility = Visibility.Collapsed;
        AdvLaptopAplusALimit.Visibility = Visibility.Collapsed;
        AdvLaptopAplusALimitDesc.Visibility = Visibility.Collapsed;
        DesktopCpu_AM5_HideUnavailableParameters();
    }

    private void DesktopCpu_AM5_HideUnavailableParameters()
    {
        LaptopsStapmLimit.Visibility = Visibility.Collapsed;
        LaptopsStapmLimitDesc.Visibility = Visibility.Collapsed;
        VrmLaptopsProchotTime.Visibility = Visibility.Collapsed;
        VrmLaptopsProchotTimeDesc.Visibility = Visibility.Collapsed;
        VrmLaptopsPsiSoC.Visibility = Visibility.Collapsed;
        VrmLaptopsPsiSoCDesc.Visibility = Visibility.Collapsed;
        VrmLaptopsPsiVdd.Visibility = Visibility.Collapsed;
        VrmLaptopsPsiVddDesc.Visibility = Visibility.Collapsed;
        VrmLaptopsPsiCpu.Visibility = Visibility.Collapsed;
        VrmLaptopsPsiCpuDesc.Visibility = Visibility.Collapsed;
        VrmLaptopsPsiIGpu.Visibility = Visibility.Collapsed;
        VrmLaptopsPsiIGpuDesc.Visibility = Visibility.Collapsed;
        VrmLaptopsSoCLimit.Visibility = Visibility.Collapsed;
        VrmLaptopsSoCLimitDesc.Visibility = Visibility.Collapsed;
        VrmLaptopsSoCMax.Visibility = Visibility.Collapsed;
        VrmLaptopsSoCMaxDesc.Visibility = Visibility.Collapsed;
        AdvLaptopDGpuTemp.Visibility = Visibility.Collapsed;
        AdvLaptopDGpuTempDesc.Visibility = Visibility.Collapsed;
        AdvLaptopIGpuFreq.Visibility = Visibility.Collapsed;
        AdvLaptopIGpuFreqDesc.Visibility = Visibility.Collapsed;
        AdvLaptopIGpuTemp.Visibility = Visibility.Collapsed;
        AdvLaptopIGpuTempDesc.Visibility = Visibility.Collapsed;
        AdvLaptopPrefMode.Visibility = Visibility.Collapsed;
        AdvLaptopPrefModeDesc.Visibility = Visibility.Collapsed;
    }

    private void HideDisabledCurveOptimizedParameters(bool locks)
    {
        Ccd11.IsEnabled = locks;
        Ccd11V.IsEnabled = locks;
        Ccd12.IsEnabled = locks;
        Ccd12V.IsEnabled = locks;
        Ccd13.IsEnabled = locks;
        Ccd13V.IsEnabled = locks;
        Ccd14.IsEnabled = locks;
        Ccd14V.IsEnabled = locks;
        Ccd15.IsEnabled = locks;
        Ccd15V.IsEnabled = locks;
        Ccd16.IsEnabled = locks;
        Ccd16V.IsEnabled = locks;
        Ccd17.IsEnabled = locks;
        Ccd17V.IsEnabled = locks;
        Ccd18.IsEnabled = locks;
        Ccd18V.IsEnabled = locks;
        Ccd21.IsEnabled = locks;
        Ccd21V.IsEnabled = locks;
        Ccd22.IsEnabled = locks;
        Ccd22V.IsEnabled = locks;
        Ccd23.IsEnabled = locks;
        Ccd23V.IsEnabled = locks;
        Ccd24.IsEnabled = locks;
        Ccd24V.IsEnabled = locks;
        Ccd25.IsEnabled = locks;
        Ccd25V.IsEnabled = locks;
        Ccd26.IsEnabled = locks;
        Ccd26V.IsEnabled = locks;
        Ccd27.IsEnabled = locks;
        Ccd27V.IsEnabled = locks;
        Ccd28.IsEnabled = locks;
        Ccd28V.IsEnabled = locks;
    }

    private void MainInit(int index)
    {
        try
        {
            if (SettingsViewModel.VersionId != 5) // Если не дебаг. В дебаг версии отображаются все параметры
            {
                var codenameGen = _cpu.GetCodenameGeneration();
                /*                 F P 4    C P U                    */
                if (codenameGen == CodenameGeneration.Fp4)
                {
                    LaptopCpu_FP5_HideUnavailableParameters();

                    LaptopsAvgWattage.Visibility = Visibility.Collapsed;
                    LaptopsAvgWattageDesc.Visibility = Visibility.Collapsed;
                    LaptopsSlowSpeed.Visibility = Visibility.Collapsed;
                    LaptopsSlowSpeedDesc.Visibility = Visibility.Collapsed;
                    AdvLaptopFix04.Visibility = Visibility.Collapsed;
                    AdvLaptopFix04Desc.Visibility = Visibility.Collapsed;
                    AdvLaptopOcMode.Visibility = Visibility.Collapsed;
                    AdvLaptopOcModeDesc.Visibility = Visibility.Collapsed;
                    AdvLaptopPboScalar.Visibility = Visibility.Collapsed;
                    AdvLaptopPboScalarDesc.Visibility = Visibility.Collapsed;
                    AdvancedFreqOptionsGrid.Visibility = Visibility.Collapsed;
                    CoExpander.Visibility = Visibility.Collapsed;
                    Ccd1Expander.Visibility = Visibility.Collapsed;
                    Ccd2Expander.Visibility = Visibility.Collapsed;
                    ActionButtonMon.Visibility = Visibility.Collapsed;

                    ParamAdvParametersBlock.Text = "Param_ADV_DescriptionBristol".GetLocalized();

                    var elements = VisualTreeHelper.FindVisualChildren<TextBlock>(VrmOptionsGrid);
                    foreach (var element in elements)
                    {
                        element.Text = element.Text.Replace("SoC","NB");
                    }
                }
                /*                 F P 5    C P U                    */
                if (codenameGen == CodenameGeneration.Fp5)
                {
                    LaptopCpu_FP5_HideUnavailableParameters();
                }
                else
                {
                    IGpuSubsystems.Visibility = Visibility.Collapsed;
                }

                /*                 F P 6    C P U                    */
                if (codenameGen == CodenameGeneration.Fp6)
                {
                    LaptopsStapmLimit.Visibility = Visibility.Collapsed;
                    LaptopsStapmLimitDesc.Visibility = Visibility.Collapsed;
                    VrmLaptopsPsiCpu.Visibility = Visibility.Collapsed;
                    VrmLaptopsPsiCpuDesc.Visibility = Visibility.Collapsed;
                    VrmLaptopsPsiIGpu.Visibility = Visibility.Collapsed;
                    VrmLaptopsPsiIGpuDesc.Visibility = Visibility.Collapsed;
                }

                /*                 F F 3    C P U                    */
                if (codenameGen == CodenameGeneration.Ff3)
                {
                    LaptopsStapmLimit.Visibility = Visibility.Collapsed;
                    LaptopsStapmLimitDesc.Visibility = Visibility.Collapsed;
                    AdvLaptopDGpuTemp.Visibility = Visibility.Collapsed;
                    AdvLaptopDGpuTempDesc.Visibility = Visibility.Collapsed;
                    AdvLaptopOcMode.Visibility = Visibility.Collapsed;
                    AdvLaptopOcModeDesc.Visibility = Visibility.Collapsed;
                    AdvLaptopPboScalar.Visibility = Visibility.Collapsed;
                    AdvLaptopPboScalarDesc.Visibility = Visibility.Collapsed;
                    AdvLaptopCpuVolt.Visibility = Visibility.Collapsed;
                    AdvLaptopCpuVoltDesc.Visibility = Visibility.Collapsed;
                }

                /*     F T 6   F P 7   F P 8   F P 11    C P U       */
                if (codenameGen is CodenameGeneration.Fp7 or CodenameGeneration.Fp8)
                {
                    LaptopsStapmLimit.Visibility = Visibility.Collapsed;
                    LaptopsStapmLimitDesc.Visibility = Visibility.Collapsed;
                }

                /*              A M 4  v 1    C P U                  */
                if (codenameGen == CodenameGeneration.Am4V1)
                {
                    Ccd1Expander.Visibility = Visibility.Collapsed; //Убрать Оптимизатор кривой
                    Ccd2Expander.Visibility = Visibility.Collapsed;
                    CoExpander.Visibility = Visibility.Collapsed;
                    DesktopCpu_AM4_HideUnavailableParameters();
                }

                /*              A M 4  v 2    C P U                  */
                if (codenameGen == CodenameGeneration.Am4V2)
                {
                    DesktopCpu_AM4_HideUnavailableParameters();
                }
                /*                 A M 5    C P U                    */
                if (codenameGen == CodenameGeneration.Am5)
                {
                    DesktopCpu_AM5_HideUnavailableParameters();
                }


                /*if (codenameGen == CodenameGeneration.Unknown && false)
                {
                    MainScroll.Visibility = Visibility.Collapsed;
                    ActionButtonApply.Visibility = Visibility.Collapsed;
                    ActionButtonDelete.Visibility = Visibility.Collapsed;
                    ActionButtonMon.Visibility = Visibility.Collapsed;
                    ActionButtonSave.Visibility = Visibility.Collapsed;
                    ActionButtonShare.Visibility = Visibility.Collapsed;
                    EditPresetButton.Visibility = Visibility.Collapsed;
                    SuggestBox.Visibility = Visibility.Collapsed;
                    FiltersButton.Visibility = Visibility.Collapsed;
                    PresetsGrid.Visibility = Visibility.Collapsed;
                    ActionIncompatibleCpu.Visibility = Visibility.Visible;

                    return; // Остановить загрузку страницы
                }*/


                for (var i = 0; i < _cpu.PhysicalCores; i++)
                {
                    var mapIndex = i < 8 ? 0 : 1;
                    if (_cpu.CoreDisableMap.Length <= mapIndex)
                    {
                        break;
                    }
                    if ((~_cpu.CoreDisableMap[mapIndex] >> i % 8 & 1) == 0)
                    {
                        try
                        {
                            var checkbox = i < 8
                        ? (CheckBox)Ccd1Grid.FindName($"Ccd1{i + 1}")
                        : (CheckBox)Ccd2Grid.FindName($"Ccd2{i - 8}");
                            if (checkbox != null && checkbox.IsChecked == true)
                            {
                                var setVal = i < 8
                                    ? (Slider)Ccd1Grid.FindName($"Ccd1{i + 1}V")
                                    : (Slider)Ccd2Grid.FindName($"Ccd2{i - 8}V");
                                setVal.IsEnabled = false;
                                setVal.Opacity = 0.4;
                                checkbox.IsEnabled = false;
                                checkbox.IsChecked = false;
                            }
                            var setGrid1 = i < 8
                        ? (StackPanel)Ccd1Grid.FindName($"Ccd1Grid{i + 1}1")
                        : (StackPanel)Ccd2Grid.FindName($"Ccd2Grid{i - 8}1");
                            var setGrid2 = i < 8
                        ? (Grid)Ccd1Grid.FindName($"Ccd1Grid{i + 1}2")
                        : (Grid)Ccd2Grid.FindName($"Ccd2Grid{i - 8}2");
                            if (setGrid1 != null)
                            {
                                setGrid1.Visibility = Visibility.Collapsed;
                                setGrid1.Opacity = 0.4;
                            }
                            if (setGrid2 != null)
                            {
                                setGrid2.Visibility = Visibility.Collapsed;
                                setGrid2.Opacity = 0.4;
                            }
                        }
                        catch (Exception e)
                        {
                            LogHelper.TraceIt_TraceError(e);
                        }
                    }
                }

                if (Ccd1Grid11.Visibility == Visibility.Collapsed &&
                    Ccd1Grid21.Visibility == Visibility.Collapsed &&
                    Ccd1Grid31.Visibility == Visibility.Collapsed &&
                    Ccd1Grid41.Visibility == Visibility.Collapsed &&
                    Ccd1Grid51.Visibility == Visibility.Collapsed &&
                    Ccd1Grid61.Visibility == Visibility.Collapsed &&
                    Ccd1Grid71.Visibility == Visibility.Collapsed &&
                    Ccd1Grid81.Visibility == Visibility.Collapsed &&
                    Ccd2Grid01.Visibility == Visibility.Collapsed &&
                    Ccd2Grid11.Visibility == Visibility.Collapsed &&
                    Ccd2Grid21.Visibility == Visibility.Collapsed &&
                    Ccd2Grid31.Visibility == Visibility.Collapsed &&
                    Ccd2Grid41.Visibility == Visibility.Collapsed &&
                    Ccd2Grid51.Visibility == Visibility.Collapsed &&
                    Ccd2Grid61.Visibility == Visibility.Collapsed &&
                    Ccd2Grid71.Visibility == Visibility.Collapsed)
                {
                    LogHelper.LogWarn("Curve Optimizer Disabled cores detection incorrect on that CPU. Using standard disabled cores detection method.");
                    Ccd1Grid11.Visibility = Visibility.Visible;
                    Ccd1Grid21.Visibility = Visibility.Visible;
                    Ccd1Grid31.Visibility = Visibility.Visible;
                    Ccd1Grid41.Visibility = Visibility.Visible;
                    Ccd1Grid51.Visibility = Visibility.Visible;
                    Ccd1Grid61.Visibility = Visibility.Visible;
                    Ccd1Grid71.Visibility = Visibility.Visible;
                    Ccd1Grid81.Visibility = Visibility.Visible;
                    Ccd2Grid01.Visibility = Visibility.Visible;
                    Ccd2Grid11.Visibility = Visibility.Visible;
                    Ccd2Grid21.Visibility = Visibility.Visible;
                    Ccd2Grid31.Visibility = Visibility.Visible;
                    Ccd2Grid41.Visibility = Visibility.Visible;
                    Ccd2Grid51.Visibility = Visibility.Visible;
                    Ccd2Grid61.Visibility = Visibility.Visible;
                    Ccd2Grid71.Visibility = Visibility.Visible;

                    var cores = _cpu.Cores;
                    if (cores > 8)
                    {
                        if (cores <= 15)
                        {
                            Ccd2Grid72.Visibility = Visibility.Collapsed;
                            Ccd2Grid71.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 14)
                        {
                            Ccd2Grid62.Visibility = Visibility.Collapsed;
                            Ccd2Grid61.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 13)
                        {
                            Ccd2Grid52.Visibility = Visibility.Collapsed;
                            Ccd2Grid51.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 12)
                        {
                            Ccd2Grid42.Visibility = Visibility.Collapsed;
                            Ccd2Grid41.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 11)
                        {
                            Ccd2Grid32.Visibility = Visibility.Collapsed;
                            Ccd2Grid31.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 10)
                        {
                            Ccd2Grid22.Visibility = Visibility.Collapsed;
                            Ccd2Grid21.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 9)
                        {
                            Ccd2Grid12.Visibility = Visibility.Collapsed;
                            Ccd2Grid11.Visibility = Visibility.Collapsed;
                        }
                    }
                    else
                    {
                        CoCoresText.Text = CoCoresText.Text.Replace("7", $"{cores - 1}");
                        Ccd2Expander.Visibility = Visibility.Collapsed;
                        if (cores <= 7)
                        {
                            Ccd1Grid82.Visibility = Visibility.Collapsed;
                            Ccd1Grid81.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 6)
                        {
                            Ccd1Grid72.Visibility = Visibility.Collapsed;
                            Ccd1Grid71.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 5)
                        {
                            Ccd1Grid62.Visibility = Visibility.Collapsed;
                            Ccd1Grid61.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 4)
                        {
                            Ccd1Grid52.Visibility = Visibility.Collapsed;
                            Ccd1Grid51.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 3)
                        {
                            Ccd1Grid42.Visibility = Visibility.Collapsed;
                            Ccd1Grid41.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 2)
                        {
                            Ccd1Grid32.Visibility = Visibility.Collapsed;
                            Ccd1Grid31.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 1)
                        {
                            Ccd1Grid22.Visibility = Visibility.Collapsed;
                            Ccd1Grid21.Visibility = Visibility.Collapsed;
                        }

                        if (cores == 0)
                        {
                            Ccd1Expander.Visibility = Visibility.Collapsed;
                        }
                    }
                }
            }

            _presetChanging = true;
            if (_appSettings.Preset == -1 || index == -1 || _presetManager.Presets.Length == 0) // Создать новый пресет
            {
                _appSettings.Preset = 0;
                index = 0;

                if (_presetManager.Presets.Length == 0) 
                {
                    _presetManager.Presets = new Preset[1];
                    _presetManager.Presets[0] = new Preset();
                    _presetManager.SaveSettings();
                }
            }


            try
            {
                TrySetMaximum(_presetManager.Presets[index].CpuSettings.CpuMaximumTemperature.Value, C1V);
                TrySetMaximum(_presetManager.Presets[index].CpuSettings.CpuSustainedPowerLimit.Value, C2V);
                TrySetMaximum(_presetManager.Presets[index].CpuSettings.CpuActualPowerLimit.Value, C3V);
                TrySetMaximum(_presetManager.Presets[index].CpuSettings.CpuAveragePowerLimit.Value, C4V);
                TrySetMaximum(_presetManager.Presets[index].CpuSettings.CpuBoostTimeSlow.Value, C5V);
                TrySetMaximum(_presetManager.Presets[index].CpuSettings.CpuBoostTimeFast.Value, C6V);
                
                TrySetMaximum(_presetManager.Presets[index].VrmSettings.VrmCpuEdcCurrentLimit.Value, V1V);
                TrySetMaximum(_presetManager.Presets[index].VrmSettings.VrmCpuTdcCurrentLimit.Value, V2V);
                TrySetMaximum(_presetManager.Presets[index].VrmSettings.VrmSocEdcCurrentLimit.Value, V3V);
                TrySetMaximum(_presetManager.Presets[index].VrmSettings.VrmSocTdcCurrentLimit.Value, V4V);
                TrySetMaximum(_presetManager.Presets[index].VrmSettings.VrmPowerSaveVddCurrentLimit.Value, V5V);
                TrySetMaximum(_presetManager.Presets[index].VrmSettings.VrmPowerSaveSocCurrentLimit.Value, V6V);
                TrySetMaximum(_presetManager.Presets[index].VrmSettings.VrmCpuFrequencyRestoreTime.Value, V7V);
                
                TrySetMaximum(_presetManager.Presets[index].SubsystemsSettings.MinimumSocFrequency.Value, G1V);
                TrySetMaximum(_presetManager.Presets[index].SubsystemsSettings.MaximumSocFrequency.Value, G2V);
                TrySetMaximum(_presetManager.Presets[index].SubsystemsSettings.MinimumFabricFrequency.Value, G3V);
                TrySetMaximum(_presetManager.Presets[index].SubsystemsSettings.MaximumFabricFrequency.Value, G4V);
                TrySetMaximum(_presetManager.Presets[index].SubsystemsSettings.MinimumVideoCodecFrequency.Value, G5V);
                TrySetMaximum(_presetManager.Presets[index].SubsystemsSettings.MaximumVideoCodecFrequency.Value, G6V);
                TrySetMaximum(_presetManager.Presets[index].SubsystemsSettings.MinimumDataLatchFrequency.Value, G7V);
                TrySetMaximum(_presetManager.Presets[index].SubsystemsSettings.MaximumDataLatchFrequency.Value, G8V);
                TrySetMaximum(_presetManager.Presets[index].SubsystemsSettings.MinimumIntegratedGraphicsFrequency.Value, G9V);
                TrySetMaximum(_presetManager.Presets[index].SubsystemsSettings.MaximumIntegratedGraphicsFrequency.Value, G10V);
               
                TrySetMaximum(_presetManager.Presets[index].VrmSettings.VrmPowerSaveCpuCurrentLimit.Value, A4V);
                TrySetMaximum(_presetManager.Presets[index].VrmSettings.VrmPowerSaveGpuCurrentLimit.Value, A5V);
                TrySetMaximum(_presetManager.Presets[index].CpuSettings.IntegratedGpuMaximumTemperature.Value, A6V);
                TrySetMaximum(_presetManager.Presets[index].CpuSettings.DiscreteGpuMaximumTemperature.Value, A7V);
                TrySetMaximum(_presetManager.Presets[index].CpuSettings.IntegratedGpuPowerLimit.Value, A8V);
                TrySetMaximum(_presetManager.Presets[index].CpuSettings.LaptopPowerLimit.Value, A9V);
                
                TrySetMaximum(_presetManager.Presets[index].FrequenciesSettings.IntegratedGraphicsFrequency.Value, A10V);
                TrySetMaximum(_presetManager.Presets[index].FrequenciesSettings.CpuFrequency.Value, A11V);
                TrySetMaximum(_presetManager.Presets[index].FrequenciesSettings.CpuVoltage.Value, A12V);
            }
            catch (Exception ex)
            {
                LogHelper.TraceIt_TraceError(ex);
            }

            try
            {
                LoadUiOption(_presetManager.Presets[index].CpuSettings.CpuMaximumTemperature, C1, C1V);
                LoadUiOption(_presetManager.Presets[index].CpuSettings.CpuSustainedPowerLimit, C2, C2V);
                LoadUiOption(_presetManager.Presets[index].CpuSettings.CpuActualPowerLimit, C3, C3V);
                LoadUiOption(_presetManager.Presets[index].CpuSettings.CpuAveragePowerLimit, C4, C4V);
                LoadUiOption(_presetManager.Presets[index].CpuSettings.CpuBoostTimeSlow, C5, C5V);
                LoadUiOption(_presetManager.Presets[index].CpuSettings.CpuBoostTimeFast, C6, C6V);
                
                LoadUiOption(_presetManager.Presets[index].VrmSettings.VrmCpuEdcCurrentLimit, V1, V1V);
                LoadUiOption(_presetManager.Presets[index].VrmSettings.VrmCpuTdcCurrentLimit, V2, V2V);
                LoadUiOption(_presetManager.Presets[index].VrmSettings.VrmSocEdcCurrentLimit, V3, V3V);
                LoadUiOption(_presetManager.Presets[index].VrmSettings.VrmSocTdcCurrentLimit, V4, V4V);
                LoadUiOption(_presetManager.Presets[index].VrmSettings.VrmPowerSaveVddCurrentLimit, V5, V5V);
                LoadUiOption(_presetManager.Presets[index].VrmSettings.VrmPowerSaveSocCurrentLimit, V6, V6V);
                LoadUiOption(_presetManager.Presets[index].VrmSettings.VrmCpuFrequencyRestoreTime, V7, V7V);
                
                LoadUiOption(_presetManager.Presets[index].SubsystemsSettings.MinimumSocFrequency, G1, G1V);
                LoadUiOption(_presetManager.Presets[index].SubsystemsSettings.MaximumSocFrequency, G2, G2V);
                LoadUiOption(_presetManager.Presets[index].SubsystemsSettings.MinimumFabricFrequency, G3, G3V);
                LoadUiOption(_presetManager.Presets[index].SubsystemsSettings.MaximumFabricFrequency, G4, G4V);
                LoadUiOption(_presetManager.Presets[index].SubsystemsSettings.MinimumVideoCodecFrequency, G5, G5V);
                LoadUiOption(_presetManager.Presets[index].SubsystemsSettings.MaximumVideoCodecFrequency, G6, G6V);
                LoadUiOption(_presetManager.Presets[index].SubsystemsSettings.MinimumDataLatchFrequency, G7, G7V);
                LoadUiOption(_presetManager.Presets[index].SubsystemsSettings.MaximumDataLatchFrequency, G8, G8V);
                LoadUiOption(_presetManager.Presets[index].SubsystemsSettings.MinimumIntegratedGraphicsFrequency, G9, G9V);
                LoadUiOption(_presetManager.Presets[index].SubsystemsSettings.MaximumIntegratedGraphicsFrequency, G10, G10V);
                LoadUiOption(_presetManager.Presets[index].CpuModesSettings.CpuFrequency04Fix, G16, G16M);
                
                
                LoadUiOption(_presetManager.Presets[index].VrmSettings.VrmPowerSaveCpuCurrentLimit, A4, A4V);
                LoadUiOption(_presetManager.Presets[index].VrmSettings.VrmPowerSaveGpuCurrentLimit, A5, A5V);
                LoadUiOption(_presetManager.Presets[index].CpuSettings.IntegratedGpuMaximumTemperature, A6, A6V);
                LoadUiOption(_presetManager.Presets[index].CpuSettings.DiscreteGpuMaximumTemperature, A7, A7V);
                LoadUiOption(_presetManager.Presets[index].CpuSettings.IntegratedGpuPowerLimit, A8, A8V);
                LoadUiOption(_presetManager.Presets[index].CpuSettings.LaptopPowerLimit, A9, A9V);
                
                LoadUiOption(_presetManager.Presets[index].FrequenciesSettings.IntegratedGraphicsFrequency, A10, A10V);
                LoadUiOption(_presetManager.Presets[index].FrequenciesSettings.CpuFrequency, A11, A11V);
                LoadUiOption(_presetManager.Presets[index].FrequenciesSettings.CpuVoltage, A12, A12V);
                LoadUiOption(_presetManager.Presets[index].CpuModesSettings.PreferredMode, A13, A13M);
                LoadUiOption(_presetManager.Presets[index].CpuModesSettings.OverclockMode, A14, A14M);
                LoadUiOption(_presetManager.Presets[index].CpuModesSettings.PboScalar, A15, A15V);
                
                LoadUiOption(_presetManager.Presets[index].CurveOptimizerAdvancedOptions.CurveOptimizerPreferredMode, CcdCoModeSel, CcdCoMode);
                LoadUiOption(_presetManager.Presets[index].CurveOptimizerOptions.CpuCurveOptimizerUndervoltingLevel, O1, O1V);
                LoadUiOption(_presetManager.Presets[index].CurveOptimizerOptions.IntegratedGpuCurveOptimizerUndervoltingLevel, O2, O2V);
                
                LoadUiOption(_presetManager.Presets[index].CurveOptimizerAdvancedOptions.CurveOptimizerCores, 0, Ccd11, Ccd11V);
                LoadUiOption(_presetManager.Presets[index].CurveOptimizerAdvancedOptions.CurveOptimizerCores, 1, Ccd12, Ccd12V);
                LoadUiOption(_presetManager.Presets[index].CurveOptimizerAdvancedOptions.CurveOptimizerCores, 2, Ccd13, Ccd13V);
                LoadUiOption(_presetManager.Presets[index].CurveOptimizerAdvancedOptions.CurveOptimizerCores, 3, Ccd14, Ccd14V);
                LoadUiOption(_presetManager.Presets[index].CurveOptimizerAdvancedOptions.CurveOptimizerCores, 4, Ccd15, Ccd15V);
                LoadUiOption(_presetManager.Presets[index].CurveOptimizerAdvancedOptions.CurveOptimizerCores, 5, Ccd16, Ccd16V);
                LoadUiOption(_presetManager.Presets[index].CurveOptimizerAdvancedOptions.CurveOptimizerCores, 6, Ccd17, Ccd17V);
                LoadUiOption(_presetManager.Presets[index].CurveOptimizerAdvancedOptions.CurveOptimizerCores, 7, Ccd18, Ccd18V);
                LoadUiOption(_presetManager.Presets[index].CurveOptimizerAdvancedOptions.CurveOptimizerCores, 8, Ccd21, Ccd21V);
                LoadUiOption(_presetManager.Presets[index].CurveOptimizerAdvancedOptions.CurveOptimizerCores, 9, Ccd22, Ccd22V);
                LoadUiOption(_presetManager.Presets[index].CurveOptimizerAdvancedOptions.CurveOptimizerCores, 10, Ccd23, Ccd23V);
                LoadUiOption(_presetManager.Presets[index].CurveOptimizerAdvancedOptions.CurveOptimizerCores, 11, Ccd24, Ccd24V);
                LoadUiOption(_presetManager.Presets[index].CurveOptimizerAdvancedOptions.CurveOptimizerCores, 12, Ccd25, Ccd25V);
                LoadUiOption(_presetManager.Presets[index].CurveOptimizerAdvancedOptions.CurveOptimizerCores, 13, Ccd26, Ccd26V);
                LoadUiOption(_presetManager.Presets[index].CurveOptimizerAdvancedOptions.CurveOptimizerCores, 14, Ccd27, Ccd27V);
                LoadUiOption(_presetManager.Presets[index].CurveOptimizerAdvancedOptions.CurveOptimizerCores, 15, Ccd28, Ccd28V);
            }
            catch
            {
                LogHelper.LogError("Preset contains errors. Creating a new preset.");

                _presetManager.Presets = new Preset[1];
                _presetManager.Presets[0] = new Preset();
                _presetManager.SaveSettings();
            }

            _presetChanging = false;
        }
        catch (Exception e)
        {
            LogHelper.TraceIt_TraceError(e);
        }
    }

    private static void TrySetMaximum(double maximum, Slider slider)
    {
        if (maximum > slider.Maximum)
        {
            slider.Maximum = FromValueToUpperFive(maximum);
        }
    }
    
    private static void LoadUiOption(PresetOption<double> option, CheckBox check, Slider slider)
    {
        check.IsChecked = option.IsEnabled;
        slider.Value = option.Value;
    }
    
    private static void LoadUiOption(PresetLargeOption<double[]> option, int index, CheckBox check, Slider slider)
    {
        if (option.IsEnabled.Length <= index ||
            option.Value.Length <= index) return;
        check.IsChecked = option.IsEnabled[index];
        slider.Value = option.Value[index];
    }
    
    private static void LoadUiOption(PresetOption<int> option, CheckBox check, Slider slider)
    {
        check.IsChecked = option.IsEnabled;
        slider.Value = option.Value;
    }
    
    private static void LoadUiOption(PresetOption<int> option, CheckBox check, ComboBox comboBox)
    {
        check.IsChecked = option.IsEnabled;
        comboBox.SelectedIndex = option.Value;
    }

    #endregion

    #region Helpers

    public static int FromValueToUpperFive(double value) => (int)Math.Ceiling(value / 5) * 5;

    #endregion

    #region Suggestion Engine

    // Сбор элементов для отображения подсказок в поиске
    private void CollectSearchItems()
    {
        _searchItems.Clear();
        var expanders = VisualTreeHelper.FindVisualChildren<Expander>(MainScroll);
        foreach (var expander in expanders)
        {
            var stackPanels = VisualTreeHelper.FindVisualChildren<StackPanel>(expander);
            foreach (var stackPanel in stackPanels)
            {
                var textBlocks = VisualTreeHelper.FindVisualChildren<TextBlock>(stackPanel).Where(tb => tb.FontSize - 15 == 0);
                foreach (var textBlock in textBlocks)
                {
                    if (!string.IsNullOrWhiteSpace(textBlock.Text) &&
                        !_searchItems.Contains(textBlock.Text)
                        && !(textBlock.Text.Contains('') ||
                             textBlock.Text.Contains('') ||
                             textBlock.Text.Contains('') ||
                             textBlock.Text.Contains('') ||
                             textBlock.Text.Contains('') ||
                             textBlock.Text.Contains('')))
                    {
                        _searchItems.Add(textBlock.Text);
                    }
                }
            }
        }
    }

    private void ResetVisibility()
    {
        var expanders = VisualTreeHelper.FindVisualChildren<Expander>(MainScroll);
        foreach (var expander in expanders)
        {
            expander.IsExpanded = true;
            var stackPanels = VisualTreeHelper.FindVisualChildren<StackPanel>(expander);
            foreach (var stackPanel in stackPanels)
            {
                stackPanel.Visibility = Visibility.Visible;
                var adjacentGrid = VisualTreeHelper.FindAdjacentGrid(stackPanel);
                if (adjacentGrid != null)
                {
                    adjacentGrid.Visibility = Visibility.Visible;
                }
            }
        }
    }

    private void SuggestBox_OnTextChanged(AutoSuggestBox? sender, AutoSuggestBoxTextChangedEventArgs? args)
    {
        if (!_isLoaded) { return; }


        if (args?.Reason == AutoSuggestionBoxTextChangeReason.UserInput ||
            args?.Reason == AutoSuggestionBoxTextChangeReason.SuggestionChosen ||
            args == null)
        {
            var suitableItems = new List<TextBlock>();
            var splitText = SuggestBox.Text.ToLower().Split(" ");

            if (_searchItems.Count == 0) 
            { 
                CollectSearchItems(); 
            }

            foreach (var searchItem in _searchItems)
            {
                if (splitText.All(key => searchItem.Contains(key, StringComparison.CurrentCultureIgnoreCase)))
                {
                    var textBlock = new TextBlock 
                    { 
                        Text = searchItem, 
                        Margin = new Thickness(-10, 0, -10, 0), 
                        Foreground = ParamName.Foreground 
                    };

                    ToolTipService.SetToolTip(textBlock, searchItem);
                    suitableItems.Add(textBlock);
                }
            }

            if (suitableItems.Count == 0)
            {
                suitableItems.Add(new TextBlock { Text = "No results found", Foreground = ParamName.Foreground });
            }

            SuggestBox.ItemsSource = suitableItems;


            if (SuggestBox.Text == string.Empty)
            {
                FilterButtons_ResetButton_Click(null, null);
            }

            // Сбросить скрытое
            ResetVisibility();

            var expanders = VisualTreeHelper.FindVisualChildren<Expander>(MainScroll);
            foreach (var expander in expanders)
            {
                var stackPanels = VisualTreeHelper.FindVisualChildren<StackPanel>(expander);
                var arrayStackPanels = stackPanels as StackPanel[] ?? [.. stackPanels];
                var anyVisible = false;

                foreach (var stackPanel in arrayStackPanels)
                {
                    var textBlocks = VisualTreeHelper.FindVisualChildren<TextBlock>(stackPanel).Where(tb => tb.FontSize - 15 == 0);
                    var containsText = textBlocks.Any(tb => tb.Text.Contains(SuggestBox.Text.ToLower(), StringComparison.CurrentCultureIgnoreCase));

                    var containsControl = VisualTreeHelper.FindVisualChildren<CheckBox>(stackPanel).Any();

                    // Если текст и элементы управления найдены, делаем StackPanel видимой
                    if (containsText && containsControl)
                    {
                        stackPanel.Visibility = Visibility.Visible;
                        anyVisible = true;

                        // Второй проход: делаем видимыми все дочерние элементы
                        VisualTreeHelper.SetAllChildrenVisibility(stackPanel, Visibility.Visible);
                    }
                    else
                    {
                        stackPanel.Visibility = Visibility.Collapsed;
                    }

                    var adjacentGrid = VisualTreeHelper.FindAdjacentGrid(stackPanel);
                    if (adjacentGrid != null)
                    {
                        adjacentGrid.Visibility = stackPanel.Visibility;
                    }
                }
                foreach (var stackPanel1 in arrayStackPanels) // Второй проход
                {
                    if (stackPanel1.Visibility == Visibility.Visible)
                    {
                        VisualTreeHelper.SetAllChildrenVisibility(stackPanel1, Visibility.Visible);
                    }
                }

                // Скрыть Expander если нет видимых StackPanels
                if (!anyVisible)
                {
                    expander.IsExpanded = false;
                }
            }
        }
    }

    private void FilterButton_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) { return; }


        List<(string, ToggleButton)> buttons = [
            ("", FilterButtonsFreq),
            ("", FilterButtonsCurrent),
            ("", FilterButtonsPower),
            ("", FilterButtonsTemp),
            ("", FilterButtonsOther),
            ("", FilterButtonsTime),
            ("\uE7B3",FilterButtonsHide)];

        List<string> glyphs = [];

        foreach (var button in buttons) // Первый проход
        {
            if (button.Item2.IsChecked == true)
            {
                if (FiltersButton.Style != (Style)Application.Current.Resources["AccentButtonStyle"])
                {
                    FiltersButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
                }
                glyphs.Add(button.Item1);
            }
        }

        if (glyphs.Count == 0)
        {
            if (FiltersButton.Style != ActionButtonApply.Style)
            {
                FiltersButton.Style = ActionButtonApply.Style;
                FiltersButton.Translation = new System.Numerics.Vector3(0, 0, 20);
                FiltersButton.CornerRadius = new CornerRadius(14);
                FiltersButton.Shadow = SharedShadow;
            }

            foreach (var button in buttons) // Добавить все, так как мы не скрываем параметры
            {
                if (button.Item2 != FilterButtonsHide)
                {
                    glyphs.Add(button.Item1);
                }
            }
        }

        if (SuggestBox.Text == string.Empty)
        {
            ResetVisibility();
        }

        var expanders = VisualTreeHelper.FindVisualChildren<Expander>(MainScroll);
        foreach (var expander in expanders)
        {
            var stackPanels = VisualTreeHelper.FindVisualChildren<StackPanel>(expander);
            var arrayStackPanels = stackPanels as StackPanel[] ?? [.. stackPanels];
            var anyVisible = false;
            var savedArray = new List<int>();

            foreach (var stackPanel in arrayStackPanels)
            {
                var textBlocks = VisualTreeHelper.FindVisualChildren<FontIcon>(stackPanel).Where(tb => tb.FontSize - 15 == 0);
                var containsText = textBlocks.Any(tb => VisualTreeHelper.FindAjantedFontIcons(tb, glyphs));

                var containsControl = VisualTreeHelper.FindVisualChildren<CheckBox>(stackPanel).Any();

                // Если текст и элементы управления найдены, делаем StackPanel видимой
                if (containsText && containsControl)
                {
                    stackPanel.Visibility = Visibility.Visible;
                    anyVisible = true;
                    savedArray.Add(Grid.GetRow(stackPanel));

                    // Второй проход: делаем видимыми все дочерние элементы
                    VisualTreeHelper.SetAllChildrenVisibility(stackPanel, Visibility.Visible);
                }
                else
                {
                    stackPanel.Visibility = Visibility.Collapsed;
                }

                var adjacentGrid = VisualTreeHelper.FindAdjacentGrid(stackPanel);
                if (adjacentGrid != null)
                {
                    adjacentGrid.Visibility = stackPanel.Visibility;
                }
            }
            foreach (var secondStackPanel in arrayStackPanels) // Второй проход
            {
                if (secondStackPanel.Visibility == Visibility.Visible)
                {
                    VisualTreeHelper.SetAllChildrenVisibility(secondStackPanel, Visibility.Visible);
                }
            }

            // Текущий подход некорректный, делает видимыми все элементы Grid, включая те что в StackPanel, хотя их должен игнорировать
            var helperGrids = VisualTreeHelper.FindVisualChildren<Grid>(expander);
            var arrayHelperGrids = helperGrids as Grid[] ?? [.. helperGrids];
            foreach (var grid in arrayHelperGrids)
            {
                if (savedArray.Contains(Grid.GetRow(grid)) && Grid.GetColumn(grid) == 1 && grid.HorizontalAlignment == HorizontalAlignment.Center)
                {
                    grid.Visibility = Visibility.Visible;
                    VisualTreeHelper.SetAllChildrenVisibility(grid, Visibility.Visible);
                }
            }

            // Скрыть Expander если нет видимых StackPanels
            if (!anyVisible)
            {
                expander.IsExpanded = false;
            }
        }
    }

    private void FilterButtons_ResetButton_Click(object? sender, RoutedEventArgs? e)
    {

        FilterButtonsFreq.IsChecked = false;
        FilterButtonsCurrent.IsChecked = false;
        FilterButtonsPower.IsChecked = false;
        FilterButtonsTemp.IsChecked = false;
        FilterButtonsOther.IsChecked = false;
        FilterButtonsTime.IsChecked = false;
        FilterButtonsHide.IsChecked = false;

        if (SuggestBox.Text != string.Empty)
        {
            ResetVisibility();
            SuggestBox_OnTextChanged(null, null);
        }
    }

    #endregion

    #endregion

    #region SMU Related voids and Quick SMU Commands

    private async void Mon_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var newWindow = new Window.PowerMon.PowerWindow();
            var micaBackdrop = new MicaBackdrop
            {
                Kind = MicaKind.BaseAlt
            };
            newWindow.SystemBackdrop = micaBackdrop;
            newWindow.Activate();

        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception);
        }
    }

    #endregion

    #region Event Handlers and Custom Preset voids

    private void PresetChanged(object? sender, PresetManagerService.PresetId e)
    {
        _presetChanging = true;
        var index = e.PresetIndex;
        _appSettings.Preset = index;

        _presetIndex = index;
        PresetCom.SelectedIndex = index + 1;
        _presetChanging = false;
        MainInit(index);
    }

    private void PresetCOM_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (!_isLoaded || _presetChanging)
            {
                return;
            }

            if (PresetCom.SelectedIndex != -1)
            {
                _appSettings.Preset = PresetCom.SelectedIndex - 1;
                _appSettings.SaveSettings();
            }

            _presetIndex = PresetCom.SelectedIndex - 1;
            MainInit(PresetCom.SelectedIndex - 1);
        }
        catch (Exception exception)
        {
            LogHelper.TraceIt_TraceError(exception);
        }
    }

    private void SuggestBox_Loaded(object sender, RoutedEventArgs e)
    {
        var texts = VisualTreeHelper.FindVisualChildren<ScrollContentPresenter>(SuggestBox);
        foreach (var text in texts)
        {
            text.Margin = new Thickness(12, 10, 0, 0);
        }
        var contents = VisualTreeHelper.FindVisualChildren<ContentControl>(SuggestBox);
        foreach (var content in contents)
        {
            var presents = VisualTreeHelper.FindVisualChildren<ContentPresenter>(content);
            foreach (var present in presents)
            {
                var texts1 = VisualTreeHelper.FindVisualChildren<TextBlock>(present);
                foreach (var text in texts1)
                {
                    text.Margin = new Thickness(0, 5, 0, 0);
                }
            }
        }
    }

    //Параметры процессора
    //Максимальная температура CPU (C)
    private void C1_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = C1.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CpuSettings.CpuMaximumTemperature.IsEnabled = check;
            _presetManager.Presets[_presetIndex].CpuSettings.CpuMaximumTemperature.Value = C1V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Лимит CPU (W)
    private void C2_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = C2.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CpuSettings.CpuSustainedPowerLimit.IsEnabled = check;
            _presetManager.Presets[_presetIndex].CpuSettings.CpuSustainedPowerLimit.Value = C2V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Реальный CPU (W)
    private void C3_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = C3.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CpuSettings.CpuActualPowerLimit.IsEnabled = check;
            _presetManager.Presets[_presetIndex].CpuSettings.CpuActualPowerLimit.Value = C3V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Средний CPU (W)
    private void C4_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = C4.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CpuSettings.CpuAveragePowerLimit.IsEnabled = check;
            _presetManager.Presets[_presetIndex].CpuSettings.CpuAveragePowerLimit.Value = C4V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Тик быстрого разгона (S)
    private void C5_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = C5.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CpuSettings.CpuBoostTimeSlow.IsEnabled = check;
            _presetManager.Presets[_presetIndex].CpuSettings.CpuBoostTimeSlow.Value = C5V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Тик медленного разгона (S)
    private void C6_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = C6.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CpuSettings.CpuBoostTimeFast.IsEnabled = check;
            _presetManager.Presets[_presetIndex].CpuSettings.CpuBoostTimeFast.Value = C6V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Параметры VRM
    //Максимальный ток VRM A
    private void V1_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = V1.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].VrmSettings.VrmCpuEdcCurrentLimit.IsEnabled = check;
            _presetManager.Presets[_presetIndex].VrmSettings.VrmCpuEdcCurrentLimit.Value = V1V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Лимит по току VRM A
    private void V2_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = V2.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].VrmSettings.VrmCpuTdcCurrentLimit.IsEnabled = check;
            _presetManager.Presets[_presetIndex].VrmSettings.VrmCpuTdcCurrentLimit.Value = V2V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Максимальный ток SOC A
    private void V3_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = V3.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].VrmSettings.VrmSocEdcCurrentLimit.IsEnabled = check;
            _presetManager.Presets[_presetIndex].VrmSettings.VrmSocEdcCurrentLimit.Value = V3V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Лимит по току SOC A
    private void V4_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = V4.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].VrmSettings.VrmSocTdcCurrentLimit.IsEnabled = check;
            _presetManager.Presets[_presetIndex].VrmSettings.VrmSocTdcCurrentLimit.Value = V4V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Максимальный ток PCI VDD A
    private void V5_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = V5.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].VrmSettings.VrmPowerSaveVddCurrentLimit.IsEnabled = check;
            _presetManager.Presets[_presetIndex].VrmSettings.VrmPowerSaveVddCurrentLimit.Value = V5V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Максимальный ток PCI SOC A
    private void V6_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = V6.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].VrmSettings.VrmPowerSaveSocCurrentLimit.IsEnabled = check;
            _presetManager.Presets[_presetIndex].VrmSettings.VrmPowerSaveSocCurrentLimit.Value = V6V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Отключить троттлинг на время
    private void V7_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = V7.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].VrmSettings.VrmCpuFrequencyRestoreTime.IsEnabled = check;
            _presetManager.Presets[_presetIndex].VrmSettings.VrmCpuFrequencyRestoreTime.Value = V7V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Параметры графики
    //Минимальная частота SOC 
    private void G1_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = G1.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MinimumSocFrequency.IsEnabled = check;
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MinimumSocFrequency.Value = G1V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Максимальная частота SOC
    private void G2_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = G2.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MaximumSocFrequency.IsEnabled = check;
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MaximumSocFrequency.Value = G2V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Минимальная частота Infinity Fabric
    private void G3_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = G3.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MinimumFabricFrequency.IsEnabled = check;
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MinimumFabricFrequency.Value = G3V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Максимальная частота Infinity Fabric
    private void G4_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = G4.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MaximumFabricFrequency.IsEnabled = check;
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MaximumFabricFrequency.Value = G4V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Минимальная частота кодека VCE
    private void G5_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = G5.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MinimumVideoCodecFrequency.IsEnabled = check;
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MinimumVideoCodecFrequency.Value = G5V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Максимальная частота кодека VCE
    private void G6_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = G6.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MaximumVideoCodecFrequency.IsEnabled = check;
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MaximumVideoCodecFrequency.Value = G6V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Минимальная частота частота Data Latch
    private void G7_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = G7.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MinimumDataLatchFrequency.IsEnabled = check;
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MinimumDataLatchFrequency.Value = G7V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Максимальная частота Data Latch
    private void G8_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = G8.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MaximumDataLatchFrequency.IsEnabled = check;
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MaximumDataLatchFrequency.Value = G8V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Минимальная частота iGpu
    private void G9_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = G9.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MinimumIntegratedGraphicsFrequency.IsEnabled = check;
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MinimumIntegratedGraphicsFrequency.Value = G9V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Максимальная частота iGpu
    private void G10_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = G10.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MaximumIntegratedGraphicsFrequency.IsEnabled = check;
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MaximumIntegratedGraphicsFrequency.Value = G10V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Расширенные параметры

    private void A4_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = A4.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].VrmSettings.VrmPowerSaveCpuCurrentLimit.IsEnabled = check;
            _presetManager.Presets[_presetIndex].VrmSettings.VrmPowerSaveCpuCurrentLimit.Value = A4V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A5_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = A5.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].VrmSettings.VrmPowerSaveGpuCurrentLimit.IsEnabled = check;
            _presetManager.Presets[_presetIndex].VrmSettings.VrmPowerSaveGpuCurrentLimit.Value = A5V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A6_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = A6.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CpuSettings.IntegratedGpuMaximumTemperature.IsEnabled = check;
            _presetManager.Presets[_presetIndex].CpuSettings.IntegratedGpuMaximumTemperature.Value = A6V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A7_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = A7.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CpuSettings.DiscreteGpuMaximumTemperature.IsEnabled = check;
            _presetManager.Presets[_presetIndex].CpuSettings.DiscreteGpuMaximumTemperature.Value = A7V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A8_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = A8.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CpuSettings.IntegratedGpuPowerLimit.IsEnabled = check;
            _presetManager.Presets[_presetIndex].CpuSettings.IntegratedGpuPowerLimit.Value = A8V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A9_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = A9.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CpuSettings.LaptopPowerLimit.IsEnabled = check;
            _presetManager.Presets[_presetIndex].CpuSettings.LaptopPowerLimit.Value = A9V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A10_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = A10.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].FrequenciesSettings.IntegratedGraphicsFrequency.IsEnabled = check;
            _presetManager.Presets[_presetIndex].FrequenciesSettings.IntegratedGraphicsFrequency.Value = A10V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A11_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = A11.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].FrequenciesSettings.CpuFrequency.IsEnabled = check;
            _presetManager.Presets[_presetIndex].FrequenciesSettings.CpuFrequency.Value = A11V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A12_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = A12.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].FrequenciesSettings.CpuVoltage.IsEnabled = check;
            _presetManager.Presets[_presetIndex].FrequenciesSettings.CpuVoltage.Value = A12V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A13_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = A13.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CpuModesSettings.PreferredMode.IsEnabled = check;
            _presetManager.Presets[_presetIndex].CpuModesSettings.PreferredMode.Value = A13M.SelectedIndex;
            _presetManager.SaveSettings();
        }
    }

    // Оптимизатор кривой
    private void CCD2_8_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = Ccd28.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.IsEnabled[15] = check;
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[15] = Ccd28V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_7_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = Ccd27.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.IsEnabled[14] = check;
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[14] = Ccd27V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_6_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = Ccd26.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.IsEnabled[13] = check;
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[13] = Ccd26V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_5_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = Ccd25.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.IsEnabled[12] = check;
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[12] = Ccd25V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_4_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = Ccd24.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.IsEnabled[11] = check;
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[11] = Ccd24V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_3_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = Ccd23.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.IsEnabled[10] = check;
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[10] = Ccd23V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_2_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = Ccd22.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.IsEnabled[9] = check;
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[9] = Ccd22V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_1_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = Ccd21.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.IsEnabled[8] = check;
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[8] = Ccd21V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_8_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = Ccd18.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.IsEnabled[7] = check;
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[7] = Ccd18V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_7_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = Ccd17.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.IsEnabled[6] = check;
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[6] = Ccd17V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_6_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = Ccd16.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.IsEnabled[5] = check;
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[5] = Ccd16V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_5_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = Ccd15.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.IsEnabled[4] = check;
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[4] = Ccd15V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_4_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = Ccd14.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.IsEnabled[3] = check;
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[3] = Ccd14V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_3_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = Ccd13.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.IsEnabled[2] = check;
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[2] = Ccd13V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_2_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }
        
        var check = Ccd12.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.IsEnabled[1] = check;
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[1] = Ccd12V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_1_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = Ccd11.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.IsEnabled[0] = check;
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[0] = Ccd11V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void O1_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = O1.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerOptions.CpuCurveOptimizerUndervoltingLevel.IsEnabled = check;
            _presetManager.Presets[_presetIndex].CurveOptimizerOptions.CpuCurveOptimizerUndervoltingLevel.Value = O1V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void O2_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = O2.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerOptions.IntegratedGpuCurveOptimizerUndervoltingLevel.IsEnabled = check;
            _presetManager.Presets[_presetIndex].CurveOptimizerOptions.IntegratedGpuCurveOptimizerUndervoltingLevel.Value = O2V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD_CO_Mode_Sel_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (CcdCoMode.SelectedIndex > 0 && CcdCoModeSel.IsChecked == true)
        {
            HideDisabledCurveOptimizedParameters(true); //Оставить параметры изменения кривой
        }
        else
        {
            HideDisabledCurveOptimizedParameters(false); //Убрать параметры
        }

        var check = CcdCoModeSel.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerPreferredMode.IsEnabled = check;
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerPreferredMode.Value = CcdCoMode.SelectedIndex;
            _presetManager.SaveSettings();
        }
    }

    //Параметры процессора, при изменении слайдеров
    //Максимальная температура CPU (C)
    private void C1_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CpuSettings.CpuMaximumTemperature.Value = C1V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Лимит CPU (W)
    private void C2_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CpuSettings.CpuSustainedPowerLimit.Value = C2V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Реальный CPU (W)
    private void C3_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CpuSettings.CpuActualPowerLimit.Value = C3V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Средний CPU(W)
    private void C4_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CpuSettings.CpuAveragePowerLimit.Value = C4V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Тик быстрого разгона (S)
    private void C5_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CpuSettings.CpuBoostTimeSlow.Value = C5V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Тик медленного разгона (S)
    private void C6_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CpuSettings.CpuBoostTimeFast.Value = C6V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Параметры VRM
    private void V1v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].VrmSettings.VrmCpuEdcCurrentLimit.Value = V1V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void V2v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].VrmSettings.VrmCpuTdcCurrentLimit.Value = V2V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void V3v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].VrmSettings.VrmSocEdcCurrentLimit.Value = V3V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void V4v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].VrmSettings.VrmSocTdcCurrentLimit.Value = V4V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void V5v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].VrmSettings.VrmPowerSaveVddCurrentLimit.Value = V5V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void V6v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].VrmSettings.VrmPowerSaveSocCurrentLimit.Value = V6V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void V7v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].VrmSettings.VrmCpuFrequencyRestoreTime.Value = V7V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Параметры GPU
    private void G1v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MinimumSocFrequency.Value = G1V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void G2v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }
        
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MaximumSocFrequency.Value = G2V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void G3v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MinimumFabricFrequency.Value = G3V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void G4v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MaximumFabricFrequency.Value = G4V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void G5v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }
        
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MinimumVideoCodecFrequency.Value = G5V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void G6v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MaximumVideoCodecFrequency.Value = G6V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void G7v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MinimumDataLatchFrequency.Value = G7V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void G8v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MaximumDataLatchFrequency.Value = G8V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void G9v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MinimumIntegratedGraphicsFrequency.Value = G9V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void G10v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].SubsystemsSettings.MaximumIntegratedGraphicsFrequency.Value = G10V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Расширенные параметры

    private void A4v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].VrmSettings.VrmPowerSaveCpuCurrentLimit.Value = A4V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A5v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].VrmSettings.VrmPowerSaveGpuCurrentLimit.Value = A5V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A6v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CpuSettings.IntegratedGpuMaximumTemperature.Value = A6V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A7v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CpuSettings.DiscreteGpuMaximumTemperature.Value = A7V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A8v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CpuSettings.IntegratedGpuPowerLimit.Value = A8V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A9v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CpuSettings.LaptopPowerLimit.Value = A9V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A10v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].FrequenciesSettings.IntegratedGraphicsFrequency.Value = A10V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A11v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].FrequenciesSettings.CpuFrequency.Value = A11V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A12v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].FrequenciesSettings.CpuVoltage.Value = A12V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A13m_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CpuModesSettings.PreferredMode.Value = A13M.SelectedIndex;
            _presetManager.SaveSettings();
        }
    }

    private void G16_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = G16.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CpuModesSettings.CpuFrequency04Fix.IsEnabled = check;
            _presetManager.Presets[_presetIndex].CpuModesSettings.CpuFrequency04Fix.Value = G16M.SelectedIndex;
            _presetManager.SaveSettings();
        }
    }

    private void G16m_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CpuModesSettings.CpuFrequency04Fix.Value = G16M.SelectedIndex;
            _presetManager.SaveSettings();
        }
    }

    private void A14_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = A14.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CpuModesSettings.OverclockMode.IsEnabled = check;
            _presetManager.Presets[_presetIndex].CpuModesSettings.OverclockMode.Value = A14M.SelectedIndex;
            _presetManager.SaveSettings();
        }
    }

    private void A14m_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CpuModesSettings.OverclockMode.Value = A14M.SelectedIndex;
            _presetManager.SaveSettings();
        }
    }

    private void A15_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        var check = A15.IsChecked == true;
        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CpuModesSettings.PboScalar.IsEnabled = check;
            _presetManager.Presets[_presetIndex].CpuModesSettings.PboScalar.Value = (int)A15V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A15v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CpuModesSettings.PboScalar.Value = (int)A15V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Слайдеры из оптимизатора кривой 
    private void O1v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerOptions.CpuCurveOptimizerUndervoltingLevel.Value = O1V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void O2v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerOptions.IntegratedGpuCurveOptimizerUndervoltingLevel.Value = O2V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_1v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[0] = Ccd11V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_2v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[1] = Ccd12V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_3v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[2] = Ccd13V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_4v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[3] = Ccd14V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_5v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[4] = Ccd15V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_6v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[5] = Ccd16V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_7v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[6] = Ccd17V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_8v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[7] = Ccd18V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_1v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[8] = Ccd21V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_2v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[9] = Ccd22V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_3v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[10] = Ccd23V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_4v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[11] = Ccd24V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_5v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[12] = Ccd25V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_6v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[13] = Ccd26V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_7v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[14] = Ccd27V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_8v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerCores.Value[15] = Ccd28V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD_CO_Mode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging)
        {
            return;
        }

        if (CcdCoMode.SelectedIndex > 0 && CcdCoModeSel.IsChecked == true)
        {
            HideDisabledCurveOptimizedParameters(true); //Оставить параметры изменения кривой
        }
        else
        {
            HideDisabledCurveOptimizedParameters(false); //Убрать параметры
        }

        if (_presetIndex != -1)
        {
            _presetManager.Presets[_presetIndex].CurveOptimizerAdvancedOptions.CurveOptimizerPreferredMode.Value = CcdCoMode.SelectedIndex;
            _presetManager.SaveSettings();
        }
    }

    //Кнопка применить, итоговый выход
    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SettingsApplied = false;

            ApplyInfo = "";
            _appSettings.SaveSettings();
            await _applyer.ApplyPreset(_presetManager.Presets[_presetIndex], 
                true);

            var timerCounter = 0;
            while (!SettingsApplied)
            {
                await Task.Delay(50);

                timerCounter++;
                if (timerCounter == 140)
                {
                    break;
                }
            }

            var timer = 1000;
            if (ApplyInfo != string.Empty)
            {
                timer *= ApplyInfo.Split('\n').Length + 1;
            }

            
            ApplyTooltip.Title = "Apply_Success".GetLocalized();
            ApplyTooltip.Subtitle = "";
            
            ApplyTooltip.IconSource = new SymbolIconSource { Symbol = Symbol.Accept };
            ApplyTooltip.IsOpen = true;
            var infoSet = InfoBarSeverity.Success;
            if (ApplyInfo != string.Empty && !_commandReturnedValue)
            {
                await LogHelper.Log(ApplyInfo);
                ApplyTooltip.Title = "Apply_Warn".GetLocalized();
                ApplyTooltip.Subtitle = "Apply_Warn_Desc".GetLocalized() + ApplyInfo;
                ApplyTooltip.IconSource = new SymbolIconSource { Symbol = Symbol.ReportHacked };
                await Task.Delay(timer);
                ApplyTooltip.IsOpen = false;
                infoSet = InfoBarSeverity.Warning;
            }
            else
            {
                if (_commandReturnedValue)
                {
                    ApplyTooltip.Subtitle = ApplyInfo;
                }
                await Task.Delay(3000);
                ApplyTooltip.IsOpen = false;
            }

            _notificationsService.ShowNotification(ApplyTooltip.Title,
                ApplyTooltip.Subtitle.Replace("Param_DeveloperOptions_ResultSaved".GetLocalized(), string.Empty) + ((ApplyInfo != string.Empty && !_commandReturnedValue) ? "DELETEUNAVAILABLE" : ""),
                infoSet,
                true);
            _commandReturnedValue = false;
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception);
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(SavePresetN.Text))
            {
                ActionButtonSave.Flyout.Hide();
                _appSettings.Preset += 1;
                _presetIndex += 1;
                _presetChanging = true;
                PresetCom.Items.Add(SavePresetN.Text);
                PresetCom.SelectedItem = SavePresetN.Text;
                _presetManager.AddPreset(new Preset { PresetName = SavePresetN.Text });

                _presetChanging = false;
                _notificationsService.ShowNotification("SaveSuccessTitle".GetLocalized(),
                    "SaveSuccessDesc".GetLocalized() + " " + SavePresetN.Text,
                    InfoBarSeverity.Success);
            }
            else
            {
                _notificationsService.ShowNotification(AddTooltipError.Title,
                    AddTooltipError.Subtitle,
                    InfoBarSeverity.Error);
                AddTooltipError.IsOpen = true;
                await Task.Delay(3000);
                AddTooltipError.IsOpen = false;
            }

            _appSettings.SaveSettings();
            _presetManager.SaveSettings();
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception);
        }
    }

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            EditPresetButton.Flyout.Hide();
            if (EditPresetN.Text != "")
            {
                var backupIndex = PresetCom.SelectedIndex;
                if (PresetCom.SelectedIndex == 0 || _presetIndex + 1 == 0)
                {
                    UnsavedTooltip.IsOpen = true;
                    await Task.Delay(3000);
                    UnsavedTooltip.IsOpen = false;
                }
                else
                {
                    _presetManager.Presets[_presetIndex].PresetName = EditPresetN.Text;
                    _presetManager.SaveSettings();
                    _presetChanging = true;
                    PresetCom.Items.Clear();
                    PresetCom.Items.Add(new ComboBoxItem
                    {
                        Content = new TextBlock
                        {
                            Text = "Param_PremadeCombo/Content".GetLocalized(),
                            Foreground = (Brush)Application.Current.Resources["AccentTextFillColorTertiaryBrush"]
                        },
                        IsEnabled = false
                    });
                    LoadPresetsToComboBox();

                    PresetCom.SelectedIndex = 0;
                    _presetChanging = false;
                    PresetCom.SelectedIndex = backupIndex;
                    _notificationsService.ShowNotification(EditTooltip.Title,
                        EditTooltip.Subtitle + " " + SavePresetN.Text,
                        InfoBarSeverity.Success);
                    EditTooltip.IsOpen = true;
                    await Task.Delay(3000);
                    EditTooltip.IsOpen = false;
                }
            }
            else
            {
                _notificationsService.ShowNotification(EditTooltipError.Title,
                    EditTooltipError.Subtitle,
                    InfoBarSeverity.Error);
                EditTooltipError.IsOpen = true;
                await Task.Delay(3000);
                EditTooltipError.IsOpen = false;
            }
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception);
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        try
        {
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
                if (PresetCom.SelectedIndex == 0)
                {
                    _notificationsService.ShowNotification(DeleteTooltipError.Title,
                        DeleteTooltipError.Subtitle,
                        InfoBarSeverity.Error);
                    DeleteTooltipError.IsOpen = true;
                    await Task.Delay(3000);
                    DeleteTooltipError.IsOpen = false;
                }
                else
                {
                    _presetChanging = true;
                    PresetCom.Items.Remove(PresetCom.SelectedItem);
                    _presetManager.RemovePreset(_presetIndex);
                    _presetIndex = 0;
                    _presetChanging = false;

                    PresetCom.SelectedIndex = PresetCom.Items.Count - 1;
                    _notificationsService.ShowNotification("DeleteSuccessTitle".GetLocalized(),
                        "DeleteSuccessDesc".GetLocalized(),
                        InfoBarSeverity.Success);
                }

                _presetManager.SaveSettings();
            }
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception);
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
                    slider.Maximum = FromValueToUpperFive(sender.Value);
                }
            }
        }        
    }
    
    #endregion

}