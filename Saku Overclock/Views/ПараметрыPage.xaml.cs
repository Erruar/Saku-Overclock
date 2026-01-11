using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Text;
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
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation.Metadata;
using Windows.UI;
using static Saku_Overclock.Services.CpuService;
using VisualTreeHelper = Saku_Overclock.Helpers.VisualTreeHelper;

namespace Saku_Overclock.Views;

public sealed partial class ПараметрыPage
{
    private FontIcon? _smuSymbol1; // тоже самое что и SMUSymbol
    private readonly IAppNotificationService _notificationsService = App.GetService<IAppNotificationService>(); // Уведомления приложения
    private readonly ISendSmuCommandService _sendSmuCommand = App.GetService<ISendSmuCommandService>();
    private readonly ICustomSmuSettingsService _smuSettings = App.GetService<ICustomSmuSettingsService>();
    private readonly IKeyboardHotkeysService _hotkeysService = App.GetService<IKeyboardHotkeysService>();
    private readonly IApplyerService _applyer = App.GetService<IApplyerService>();
    private readonly IOcFinderService _ocFinder = App.GetService<IOcFinderService>();
    private readonly ICpuService _cpu = App.GetService<ICpuService>();
    private int _indexpreset; // Выбранный пресет
    private readonly IAppSettingsService _appSettings = App.GetService<IAppSettingsService>(); // Все настройки приложения
    private readonly IPresetManagerService _presetManager = App.GetService<IPresetManagerService>(); // Менеджер пресетов разгона
    private bool _isSearching; // Флаг выполнения поиска чтобы не сканировать адреса SMU 
    private readonly List<string> _searchItems = [];
    private readonly SmuAddressSet _testMailbox = new(0,0,0);
    private string
        _smuSymbol =
            "\uE8C8"; // Изначальный символ копирования, для секции Редактор параметров SMU. Используется для быстрых команд SMU

    private bool _isLoaded; // Загружена ли корректно страница для применения изменений
    private bool _waitforload = true; // Ожидание окончательной смены пресета на другой. Активируется при смене пресета
    private bool _commandReturnedValue; // Флаг если команда вернула значение
    private readonly bool _isPremadePresetApplied; // Флаг применённого готового пресета для его восстановления после покидания страницы Разгон

    private static readonly List<double> PstatesFid = [0, 0, 0];
    private static readonly List<double> PstatesDid = [0, 0, 0];
    private static readonly List<double> PstatesVid = [0, 0, 0];

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

        _indexpreset = _appSettings.Preset;

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

    private async void ПараметрыPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _isLoaded = true;
            CollectSearchItems();
            await SlidersInit();
            RecommendationsInit();
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception);
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
    private async Task SlidersInit()
    {
        if (!_isLoaded)
        {
            return;
        }

        _waitforload = true;

        PresetCom.Items.Clear();
        PresetCom.Items.Add(new ComboBoxItem
        {
            Content = new TextBlock
            {
                Text = "Param_Premaded".GetLocalized(),
                Foreground = (Brush)Application.Current.Resources["AccentTextFillColorTertiaryBrush"]
            },
            IsEnabled = false
        });

        foreach (var currPreset in _presetManager.Presets)
        {
            if (currPreset.Presetname != string.Empty)
            {
                PresetCom.Items.Add(currPreset.Presetname);
            }
        }

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
                    PresetCom.Items.Add(_presetManager.Presets[0].Presetname);
                }


                _indexpreset = 0;
                PresetCom.SelectedIndex = 1;
                _appSettings.SaveSettings();
            }
            else
            {
                _indexpreset = _appSettings.Preset;
                if (PresetCom.Items.Count >= _indexpreset + 1)
                {
                    PresetCom.SelectedIndex = _indexpreset + 1;
                }
            }
        }

        await MainInit(PresetCom.SelectedIndex - 1);

        _waitforload = false;
    }

    //Убрать параметры для ноутбуков
    private void LaptopCpu_FP5_HideUnavailableParameters()
    {
        AdvLaptopAplusALimit.Visibility = Visibility.Collapsed;
        AdvLaptopAplusALimitDesc.Visibility = Visibility.Collapsed;
        AdvLaptopIGpuLimit.Visibility = Visibility.Collapsed;
        AdvLaptopIGpuLimitDesc.Visibility = Visibility.Collapsed;
        LaptopsHtcTemp.Visibility = Visibility.Collapsed;
        LaptopsHtcTempDesc.Visibility = Visibility.Collapsed;
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

    private async Task MainInit(int index)
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
                    SmuFunctionsGrid.Visibility = Visibility.Collapsed;
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
                    LaptopsHtcTemp.Visibility = Visibility.Collapsed;
                    LaptopsHtcTempDesc.Visibility = Visibility.Collapsed;
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
                    LaptopsHtcTemp.Visibility = Visibility.Collapsed;
                    LaptopsHtcTempDesc.Visibility = Visibility.Collapsed;
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
                    LaptopsHtcTemp.Visibility = Visibility.Collapsed;
                    LaptopsHtcTempDesc.Visibility = Visibility.Collapsed;
                }

                /*              A M 4  v 1    C P U                  */
                if (codenameGen == CodenameGeneration.Am4V1)
                {
                    Ccd1Expander.Visibility = Visibility.Collapsed; //Убрать Оптимизатор кривой
                    Ccd2Expander.Visibility = Visibility.Collapsed;
                    CoExpander.Visibility = Visibility.Collapsed;
                    LaptopsHtcTemp.Visibility = Visibility.Collapsed;
                    LaptopsHtcTempDesc.Visibility = Visibility.Collapsed;
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


                if (codenameGen == CodenameGeneration.Unknown)
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
                }


                for (var i = 0; i < _cpu.PhysicalCores; i++)
                {
                    var mapIndex = i < 8 ? 0 : 1;
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
                            await LogHelper.TraceIt_TraceError(e);
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
                    await LogHelper.LogWarn("Curve Optimizer Disabled cores detection incorrect on that CPU. Using standart disabled cores detection method.");
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

            _waitforload = true;
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
                if (_presetManager.Presets[index].Cpu1Value > C1V.Maximum)
                {
                    C1V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Cpu1Value);
                }

                if (_presetManager.Presets[index].Cpu2Value > C2V.Maximum)
                {
                    C2V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Cpu2Value);
                }

                if (_presetManager.Presets[index].Cpu3Value > C3V.Maximum)
                {
                    C3V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Cpu3Value);
                }

                if (_presetManager.Presets[index].Cpu4Value > C4V.Maximum)
                {
                    C4V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Cpu4Value);
                }

                if (_presetManager.Presets[index].Cpu5Value > C5V.Maximum)
                {
                    C5V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Cpu5Value);
                }

                if (_presetManager.Presets[index].Cpu6Value > C6V.Maximum)
                {
                    C6V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Cpu6Value);
                }

                if (_presetManager.Presets[index].Cpu7Value > C7V.Maximum)
                {
                    C7V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Cpu7Value);
                }

                if (_presetManager.Presets[index].Vrm1Value > V1V.Maximum)
                {
                    V1V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Vrm1Value);
                }

                if (_presetManager.Presets[index].Vrm2Value > V2V.Maximum)
                {
                    V2V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Vrm2Value);
                }

                if (_presetManager.Presets[index].Vrm3Value > V3V.Maximum)
                {
                    V3V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Vrm3Value);
                }

                if (_presetManager.Presets[index].Vrm4Value > V4V.Maximum)
                {
                    V4V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Vrm4Value);
                }

                if (_presetManager.Presets[index].Vrm5Value > V5V.Maximum)
                {
                    V5V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Vrm5Value);
                }

                if (_presetManager.Presets[index].Vrm6Value > V6V.Maximum)
                {
                    V6V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Vrm6Value);
                }

                if (_presetManager.Presets[index].Vrm7Value > V7V.Maximum)
                {
                    V7V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Vrm7Value);
                }

                if (_presetManager.Presets[index].Gpu1Value > G1V.Maximum)
                {
                    G1V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Gpu1Value);
                }

                if (_presetManager.Presets[index].Gpu2Value > G2V.Maximum)
                {
                    G2V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Gpu2Value);
                }

                if (_presetManager.Presets[index].Gpu3Value > G3V.Maximum)
                {
                    G3V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Gpu3Value);
                }

                if (_presetManager.Presets[index].Gpu4Value > G4V.Maximum)
                {
                    G4V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Gpu4Value);
                }

                if (_presetManager.Presets[index].Gpu5Value > G5V.Maximum)
                {
                    G5V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Gpu5Value);
                }

                if (_presetManager.Presets[index].Gpu6Value > G6V.Maximum)
                {
                    G6V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Gpu6Value);
                }

                if (_presetManager.Presets[index].Gpu7Value > G7V.Maximum)
                {
                    G7V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Gpu7Value);
                }

                if (_presetManager.Presets[index].Gpu8Value > G8V.Maximum)
                {
                    G8V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Gpu8Value);
                }

                if (_presetManager.Presets[index].Gpu9Value > G9V.Maximum)
                {
                    G9V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Gpu9Value);
                }

                if (_presetManager.Presets[index].Gpu10Value > G10V.Maximum)
                {
                    G10V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Gpu10Value);
                }

                if (_presetManager.Presets[index].Advncd4Value > A4V.Maximum)
                {
                    A4V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Advncd4Value);
                }

                if (_presetManager.Presets[index].Advncd5Value > A5V.Maximum)
                {
                    A5V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Advncd5Value);
                }

                if (_presetManager.Presets[index].Advncd6Value > A6V.Maximum)
                {
                    A6V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Advncd6Value);
                }

                if (_presetManager.Presets[index].Advncd7Value > A7V.Maximum)
                {
                    A7V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Advncd7Value);
                }

                if (_presetManager.Presets[index].Advncd8Value > A8V.Maximum)
                {
                    A8V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Advncd8Value);
                }

                if (_presetManager.Presets[index].Advncd9Value > A9V.Maximum)
                {
                    A9V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Advncd9Value);
                }

                if (_presetManager.Presets[index].Advncd10Value > A10V.Maximum)
                {
                    A10V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Advncd10Value);
                }

                if (_presetManager.Presets[index].Advncd11Value > A11V.Maximum)
                {
                    A11V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Advncd11Value);
                }

                if (_presetManager.Presets[index].Advncd12Value > A12V.Maximum)
                {
                    A12V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Advncd12Value);
                }

                if (_presetManager.Presets[index].Advncd15Value > A15V.Maximum)
                {
                    A15V.Maximum = FromValueToUpperFive(_presetManager.Presets[index].Advncd15Value);
                }
            }
            catch (Exception ex)
            {
                await LogHelper.TraceIt_TraceError(ex);
            }

            try
            {
                C1.IsChecked = _presetManager.Presets[index].Cpu1;
                C1V.Value = _presetManager.Presets[index].Cpu1Value;
                C2.IsChecked = _presetManager.Presets[index].Cpu2;
                C2V.Value = _presetManager.Presets[index].Cpu2Value;
                C3.IsChecked = _presetManager.Presets[index].Cpu3;
                C3V.Value = _presetManager.Presets[index].Cpu3Value;
                C4.IsChecked = _presetManager.Presets[index].Cpu4;
                C4V.Value = _presetManager.Presets[index].Cpu4Value;
                C5.IsChecked = _presetManager.Presets[index].Cpu5;
                C5V.Value = _presetManager.Presets[index].Cpu5Value;
                C6.IsChecked = _presetManager.Presets[index].Cpu6;
                C6V.Value = _presetManager.Presets[index].Cpu6Value;
                C7.IsChecked = _presetManager.Presets[index].Cpu7;
                C7V.Value = _presetManager.Presets[index].Cpu7Value;
                V1.IsChecked = _presetManager.Presets[index].Vrm1;
                V1V.Value = _presetManager.Presets[index].Vrm1Value;
                V2.IsChecked = _presetManager.Presets[index].Vrm2;
                V2V.Value = _presetManager.Presets[index].Vrm2Value;
                V3.IsChecked = _presetManager.Presets[index].Vrm3;
                V3V.Value = _presetManager.Presets[index].Vrm3Value;
                V4.IsChecked = _presetManager.Presets[index].Vrm4;
                V4V.Value = _presetManager.Presets[index].Vrm4Value;
                V5.IsChecked = _presetManager.Presets[index].Vrm5;
                V5V.Value = _presetManager.Presets[index].Vrm5Value;
                V6.IsChecked = _presetManager.Presets[index].Vrm6;
                V6V.Value = _presetManager.Presets[index].Vrm6Value;
                V7.IsChecked = _presetManager.Presets[index].Vrm7;
                V7V.Value = _presetManager.Presets[index].Vrm7Value;
                G1.IsChecked = _presetManager.Presets[index].Gpu1;
                G1V.Value = _presetManager.Presets[index].Gpu1Value;
                G2.IsChecked = _presetManager.Presets[index].Gpu2;
                G2V.Value = _presetManager.Presets[index].Gpu2Value;
                G3.IsChecked = _presetManager.Presets[index].Gpu3;
                G3V.Value = _presetManager.Presets[index].Gpu3Value;
                G4.IsChecked = _presetManager.Presets[index].Gpu4;
                G4V.Value = _presetManager.Presets[index].Gpu4Value;
                G5.IsChecked = _presetManager.Presets[index].Gpu5;
                G5V.Value = _presetManager.Presets[index].Gpu5Value;
                G6.IsChecked = _presetManager.Presets[index].Gpu6;
                G6V.Value = _presetManager.Presets[index].Gpu6Value;
                G7.IsChecked = _presetManager.Presets[index].Gpu7;
                G7V.Value = _presetManager.Presets[index].Gpu7Value;
                G8V.Value = _presetManager.Presets[index].Gpu8Value;
                G8.IsChecked = _presetManager.Presets[index].Gpu8;
                G9V.Value = _presetManager.Presets[index].Gpu9Value;
                G9.IsChecked = _presetManager.Presets[index].Gpu9;
                G10V.Value = _presetManager.Presets[index].Gpu10Value;
                G10.IsChecked = _presetManager.Presets[index].Gpu10;
                G16.IsChecked = _presetManager.Presets[index].Gpu16;
                G16M.SelectedIndex = _presetManager.Presets[index].Gpu16Value;
                A4.IsChecked = _presetManager.Presets[index].Advncd4;
                A4V.Value = _presetManager.Presets[index].Advncd4Value;
                A5.IsChecked = _presetManager.Presets[index].Advncd5;
                A5V.Value = _presetManager.Presets[index].Advncd5Value;
                A6.IsChecked = _presetManager.Presets[index].Advncd6;
                A6V.Value = _presetManager.Presets[index].Advncd6Value;
                A7.IsChecked = _presetManager.Presets[index].Advncd7;
                A7V.Value = _presetManager.Presets[index].Advncd7Value;
                A8V.Value = _presetManager.Presets[index].Advncd8Value;
                A8.IsChecked = _presetManager.Presets[index].Advncd8;
                A9V.Value = _presetManager.Presets[index].Advncd9Value;
                A9.IsChecked = _presetManager.Presets[index].Advncd9;
                A10V.Value = _presetManager.Presets[index].Advncd10Value;
                A10.IsChecked = _presetManager.Presets[index].Advncd10;
                A11V.Value = _presetManager.Presets[index].Advncd11Value;
                A11.IsChecked = _presetManager.Presets[index].Advncd11;
                A12V.Value = _presetManager.Presets[index].Advncd12Value;
                A12.IsChecked = _presetManager.Presets[index].Advncd12;
                A13.IsChecked = _presetManager.Presets[index].Advncd13;
                A13M.SelectedIndex = _presetManager.Presets[index].Advncd13Value;
                A14.IsChecked = _presetManager.Presets[index].Advncd14;
                A14M.SelectedIndex = _presetManager.Presets[index].Advncd14Value;
                A15.IsChecked = _presetManager.Presets[index].Advncd15;
                A15V.Value = _presetManager.Presets[index].Advncd15Value;
                CcdCoModeSel.IsChecked = _presetManager.Presets[index].Comode;
                CcdCoMode.SelectedIndex = _presetManager.Presets[index].Coprefmode;
                O1.IsChecked = _presetManager.Presets[index].Coall;
                O1V.Value = _presetManager.Presets[index].Coallvalue;
                O2.IsChecked = _presetManager.Presets[index].Cogfx;
                O2V.Value = _presetManager.Presets[index].Cogfxvalue;
                Ccd11.IsChecked = _presetManager.Presets[index].Coper0;
                Ccd11V.Value = _presetManager.Presets[index].Coper0Value;
                Ccd12.IsChecked = _presetManager.Presets[index].Coper1;
                Ccd12V.Value = _presetManager.Presets[index].Coper1Value;
                Ccd13.IsChecked = _presetManager.Presets[index].Coper2;
                Ccd13V.Value = _presetManager.Presets[index].Coper2Value;
                Ccd14.IsChecked = _presetManager.Presets[index].Coper3;
                Ccd14V.Value = _presetManager.Presets[index].Coper3Value;
                Ccd15.IsChecked = _presetManager.Presets[index].Coper4;
                Ccd15V.Value = _presetManager.Presets[index].Coper4Value;
                Ccd16.IsChecked = _presetManager.Presets[index].Coper5;
                Ccd16V.Value = _presetManager.Presets[index].Coper5Value;
                Ccd17.IsChecked = _presetManager.Presets[index].Coper6;
                Ccd17V.Value = _presetManager.Presets[index].Coper6Value;
                Ccd18.IsChecked = _presetManager.Presets[index].Coper7;
                Ccd18V.Value = _presetManager.Presets[index].Coper7Value;
                Ccd21.IsChecked = _presetManager.Presets[index].Coper8;
                Ccd21V.Value = _presetManager.Presets[index].Coper8Value;
                Ccd22.IsChecked = _presetManager.Presets[index].Coper9;
                Ccd22V.Value = _presetManager.Presets[index].Coper9Value;
                Ccd23.IsChecked = _presetManager.Presets[index].Coper10;
                Ccd23V.Value = _presetManager.Presets[index].Coper10Value;
                Ccd24.IsChecked = _presetManager.Presets[index].Coper11;
                Ccd24V.Value = _presetManager.Presets[index].Coper11Value;
                Ccd25.IsChecked = _presetManager.Presets[index].Coper12;
                Ccd25V.Value = _presetManager.Presets[index].Coper12Value;
                Ccd26.IsChecked = _presetManager.Presets[index].Coper13;
                Ccd26V.Value = _presetManager.Presets[index].Coper13Value;
                Ccd27.IsChecked = _presetManager.Presets[index].Coper14;
                Ccd27V.Value = _presetManager.Presets[index].Coper14Value;
                Ccd28.IsChecked = _presetManager.Presets[index].Coper15;
                Ccd28V.Value = _presetManager.Presets[index].Coper15Value;
                EnableSmu.IsOn = _presetManager.Presets[index].SmuEnabled;
                SmuFuncEnableToggle.IsOn = _presetManager.Presets[index].SmuFunctionsEnabl;
                Bit0FeatureCclkController.IsOn = _presetManager.Presets[index].SmuFeatureCclk;
                Bit2FeatureDataCalculation.IsOn = _presetManager.Presets[index].SmuFeatureData;
                Bit3FeaturePpt.IsOn = _presetManager.Presets[index].SmuFeaturePpt;
                Bit4FeatureTdc.IsOn = _presetManager.Presets[index].SmuFeatureTdc;
                Bit5FeatureThermal.IsOn = _presetManager.Presets[index].SmuFeatureThermal;
                Bit8FeaturePllPowerDown.IsOn = _presetManager.Presets[index].SmuFeaturePowerDown;
                Bit37FeatureProchot.IsOn = _presetManager.Presets[index].SmuFeatureProchot;
                Bit39FeatureStapm.IsOn = _presetManager.Presets[index].SmuFeatureStapm;
                Bit40FeatureCoreCstates.IsOn = _presetManager.Presets[index].SmuFeatureCStates;
                Bit41FeatureGfxDutyCycle.IsOn = _presetManager.Presets[index].SmuFeatureGfxDutyCycle;
                Bit42FeatureAaMode.IsOn = _presetManager.Presets[index].SmuFeatureAplusA;
            }
            catch
            {
                await LogHelper.LogError("Preset contains errors. Creating a new preset.");

                _presetManager.Presets = new Preset[1];
                _presetManager.Presets[0] = new Preset();
                _presetManager.SaveSettings();
            }

            _waitforload = false;

            if (_smuSettings.Note != string.Empty)
            {
                SmuNotes.Document.SetText(TextSetOptions.FormatRtf, _smuSettings.Note.TrimEnd());
                ChangeRichEditBoxTextColor(SmuNotes, GetColorFromBrush(TextColor.Foreground));
            }

            try
            {
                Init_QuickSMU();
            }
            catch (Exception ex)
            {
                await LogHelper.TraceIt_TraceError("Loading user SMU settings failed: " + ex);
            }
        }
        catch (Exception e)
        {
            await LogHelper.TraceIt_TraceError(e);
        }
    }

    private void Init_QuickSMU()
    {
        if (_smuSettings.QuickSmuCommands == null)
        {
            return;
        }

        QuickSmu.Children.Clear();
        QuickSmu.RowDefinitions.Clear();
        for (var i = 0; i < _smuSettings.QuickSmuCommands.Count; i++)
        {
            var grid = new Grid
            {
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var rowDef = new RowDefinition
            {
                Height = GridLength.Auto
            };

            // Подготовка перед добавлением нового элемента
            QuickSmu.RowDefinitions.Add(rowDef);
            var rowIndex = QuickSmu.RowDefinitions.Count - 1;

            QuickSmu.Children.Add(grid); // Добавить в секцию грид быстрой команды
            Grid.SetRow(grid, rowIndex);

            var button = new Button // Основная кнопка быстрой команды. В ней всё содержимое
            {
                Height = 50,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                CornerRadius = new CornerRadius(13),
                Translation = new System.Numerics.Vector3(0, 0, 12),
                Shadow = SharedShadow
            };
            
            var innerGrid = new Grid // Grid внутри Button
            {
                Height = 50
            };
            
            var fontIcon = new FontIcon // Иконка у этой команды
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, -10, 0, 0),
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                HorizontalAlignment = HorizontalAlignment.Left,
                Glyph = _smuSettings.QuickSmuCommands[i].Symbol
            };
            
            innerGrid.Children.Add(fontIcon); // Иконка команды

            var textBlock1 = new TextBlock
            {
                Margin = string.IsNullOrWhiteSpace(_smuSettings.QuickSmuCommands[i].Description) ? new Thickness(35, 9, 0, 0) : new Thickness(35, 0.5, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
                Text = _smuSettings.QuickSmuCommands[i].Name,
                FontWeight = FontWeights.SemiBold
            };
            innerGrid.Children.Add(textBlock1); // Имя команды

            var textBlock2 = new TextBlock
            {
                Margin = new Thickness(35, 17.5, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
                Text = _smuSettings.QuickSmuCommands[i].Description,
                FontWeight = FontWeights.Light
            };
            innerGrid.Children.Add(textBlock2); // Подпись команды

            button.Content = innerGrid; // Внутренний Grid в Button

            var buttonsGrid = new Grid // Внешний Grid с кнопками
            {
                HorizontalAlignment = HorizontalAlignment.Right
            };

            // Создание и добавление кнопок во внешний Grid
            var playButton = new Button // "Применить"
            {
                Name = $"Play_{rowIndex}",
                HorizontalAlignment = HorizontalAlignment.Right,
                Width = 35,
                Height = 35,
                Margin = new Thickness(0, 0, 7, 0),
                Content = new SymbolIcon
                {
                    Symbol = Symbol.Play,
                    Margin = new Thickness(-5, 0, -5, 0),
                    HorizontalAlignment = HorizontalAlignment.Left
                },
                CornerRadius = new CornerRadius(10),
                Translation = new System.Numerics.Vector3(0, 0, 12),
                Shadow = SharedShadow
            };
            buttonsGrid.Children.Add(playButton);

            var editButton = new Button // "Изменить"
            {
                Name = $"Edit_{rowIndex}",
                HorizontalAlignment = HorizontalAlignment.Right,
                Width = 35,
                Height = 35,
                Margin = new Thickness(0, 0, 50, 0),
                Content = new SymbolIcon
                {
                    Symbol = Symbol.Edit,
                    Margin = new Thickness(-5, 0, -5, 0)
                },
                CornerRadius = new CornerRadius(10),
                Translation = new System.Numerics.Vector3(0, 0, 12),
                Shadow = SharedShadow
            };
            buttonsGrid.Children.Add(editButton);

            var rsmuButton = new Button // Кнопка отображающая текущий MailBox
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                Width = 86,
                Height = 35,
                Margin = new Thickness(0, 0, 93, 0),
                CornerRadius = new CornerRadius(10),
                Translation = new System.Numerics.Vector3(0, 0, 12),
                Shadow = SharedShadow,
                Content = new TextBlock
                {
                    Text = _smuSettings.MailBoxes![_smuSettings.QuickSmuCommands[i].MailIndex].Name
                }
            };
            buttonsGrid.Children.Add(rsmuButton);

            var cmdButton = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                Width = 86,
                Height = 35,
                Margin = new Thickness(0, 0, 187, 0),
                CornerRadius = new CornerRadius(10),
                Translation = new System.Numerics.Vector3(0, 0, 12),
                Shadow = SharedShadow,
                Content = new TextBlock
                {
                    Text = _smuSettings.QuickSmuCommands![i].Command + " / " + _smuSettings.QuickSmuCommands![i].Argument
                }
            };
            buttonsGrid.Children.Add(cmdButton);

            var autoButton = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                Width = 86,
                Height = 35,
                Margin = new Thickness(0, 0, 281, 0),
                CornerRadius = new CornerRadius(10),
                Translation = new System.Numerics.Vector3(0, 0, 12),
                Shadow = SharedShadow,
                Content = new TextBlock
                {
                    Text = _smuSettings.QuickSmuCommands![i].Startup ? "Autorun" : "Apply"
                }
            };
            
            if (_smuSettings.QuickSmuCommands![i].Startup || _smuSettings.QuickSmuCommands![i].ApplyWith)
            {
                buttonsGrid.Children.Add(autoButton);
            }

            // Добавление внешнего Grid в основной Grid
            grid.Children.Add(button);
            grid.Children.Add(buttonsGrid);

            // Назначение событий на нажатия кнопок
            editButton.Click += EditButton_Click;
            playButton.Click += PlayButton_Click;
        }
    }

    #endregion

    #region Helpers
    private static Color GetColorFromBrush(Brush brush)
    {
        if (brush is SolidColorBrush solidColorBrush)
        {
            return solidColorBrush.Color;
        }

        return Colors.White;
    }

    private static void ChangeRichEditBoxTextColor(RichEditBox richEditBox, Color color)
    {
        richEditBox.Document.ApplyDisplayUpdates();
        var documentRange = richEditBox.Document.GetRange(0, TextConstants.MaxUnitCount);
        documentRange.CharacterFormat.ForegroundColor = color;
        richEditBox.Document.ApplyDisplayUpdates();
    }

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
            _isSearching = true;
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

        _isSearching = true;

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
        _isSearching = false;
    }

    private void FilterButton_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) { return; }

        _isSearching = true;

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
        _isSearching = false;
    }

    private void FilterButtons_ResetButton_Click(object? sender, RoutedEventArgs? e)
    {
        _isSearching = true;

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

        _isSearching = false;
    }

    #endregion

    #endregion

    #region SMU Related voids and Quick SMU Commands

    private void PopulateMailboxesList(ItemCollection l)
    {
        l.Clear();
        l.Add(new MailboxListItem("RSMU", _cpu.Rsmu));
        l.Add(new MailboxListItem("MP1", _cpu.Mp1));
        l.Add(new MailboxListItem("HSMP", _cpu.Hsmp));
    }

    private void AddPopulatedSmuMailboxes()
    {
        try
        {
            PopulateMailboxesList(ComboBoxMailboxSelect.Items);
            if (ComboBoxMailboxSelect.Items.Count > 0)
            {
                ComboBoxMailboxSelect.SelectedIndex = 0;
            }
            else
            {
                ComboBoxMailboxSelect.SelectedIndex = -1;
            }

            QuickCommand.IsEnabled = true;
        }
        catch (Exception exception)
        {
            LogHelper.TraceIt_TraceError(exception);
        }
    }
    

    private void ResetSmuAddresses()
    {
        TextBoxCmdAddress.Text = $@"0x{Convert.ToString(_testMailbox.MsgAddress, 16).ToUpper()}";
        TextBoxRspAddress.Text = $@"0x{Convert.ToString(_testMailbox.RspAddress, 16).ToUpper()}";
        TextBoxArgAddress.Text = $@"0x{Convert.ToString(_testMailbox.ArgAddress, 16).ToUpper()}";
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        ApplySettings(1, int.Parse((sender as Button)!.Name.Replace("Play_", "")));
    }

    private async void EditButton_Click(object sender, RoutedEventArgs e)
    {
        await QuickDialog(1, int.Parse((sender as Button)!.Name.Replace("Edit_", "")));
    }

    //SMU КОМАНДЫ
    private void ApplySettings(int mode, int commandIndex)
    {
        _commandReturnedValue = false;

        try
        {
            uint[]? args;
            string[]? userArgs;
            uint addrMsg;
            uint addrRsp;
            uint addrArg;
            uint command;

            if (mode != 0)
            {
                args = SendSmuCommandService.MakeCmdArgs();
                userArgs = _smuSettings.QuickSmuCommands![commandIndex].Argument.Trim().Split(',');
                TryConvertToUint(_smuSettings.MailBoxes![_smuSettings.QuickSmuCommands![commandIndex].MailIndex].Cmd,
                    out addrMsg);
                TryConvertToUint(_smuSettings.MailBoxes![_smuSettings.QuickSmuCommands![commandIndex].MailIndex].Rsp,
                    out addrRsp);
                TryConvertToUint(_smuSettings.MailBoxes![_smuSettings.QuickSmuCommands![commandIndex].MailIndex].Arg,
                    out addrArg);
                TryConvertToUint(_smuSettings.QuickSmuCommands![commandIndex].Command, out command);
            }
            else
            {
                args = SendSmuCommandService.MakeCmdArgs();
                userArgs = TextBoxArg0.Text.Trim().Split(',');
                TryConvertToUint(TextBoxCmdAddress.Text, out addrMsg);
                TryConvertToUint(TextBoxRspAddress.Text, out addrRsp);
                TryConvertToUint(TextBoxArgAddress.Text, out addrArg);
                TryConvertToUint(TextBoxCmd.Text, out command);
            }

            _testMailbox.MsgAddress = addrMsg;
            _testMailbox.RspAddress = addrRsp;
            _testMailbox.ArgAddress = addrArg;

            for (var i = 0; i < userArgs.Length; i++)
            {
                if (i == args.Length)
                {
                    break;
                }

                TryConvertToUint(userArgs[i], out var temp);
                args[i] = temp;
            }

            var argsBefore = args.ToArray();

            Task.Run(async () =>
                await LogHelper.Log(
                    $"Sending SMU Command: {_smuSettings.QuickSmuCommands?[commandIndex].Command}\n" +
                    $"Args: {_smuSettings.QuickSmuCommands?[commandIndex].Argument}\n" +
                    $"Address MSG: {_testMailbox.MsgAddress}\n" +
                    $"Address RSP: {_testMailbox.RspAddress}\n" +
                    $"Address ARG: {_testMailbox.ArgAddress}"));

            var status = _cpu.SendSmuCommand(_testMailbox, command, ref args);
            if (status != SmuStatus.Ok)
            {
                ApplyInfo += "\n" + "SMUErrorText".GetLocalized() + ": " +
                             (TextBoxCmd.Text.Contains("0x") ? TextBoxCmd.Text : "0x" + TextBoxCmd.Text)
                             + "Param_SMU_Args_From".GetLocalized() + ComboBoxMailboxSelect.SelectedValue
                             + "Param_SMU_Args".GetLocalized() + (TextBoxArg0.Text.Contains("0x")
                                 ? TextBoxArg0.Text
                                 : "0x" + TextBoxArg0.Text);

                if (status == SmuStatus.CmdRejectedPrereq)
                {
                    ApplyInfo += "\n" + "SMUErrorRejected".GetLocalized();
                }
                else
                {
                    ApplyInfo += "\n" + "SMUErrorNoCMD".GetLocalized();
                }
            }

            if (args[0] != argsBefore[0] || args[1] != argsBefore[1] || args[2] != argsBefore[2])
            {
                _commandReturnedValue = true;
                ApplyInfo += "Param_DeveloperOptions_SmuCommand".GetLocalized() + $" {ComboBoxMailboxSelect.SelectedValue} 0x{command:X} " + "Param_DeveloperOptions_CommandResult".GetLocalized() + $"0x{args[0]:X}({args[0]}) 0x{args[1]:X}({args[1]}) 0x{args[2]:X}({args[2]})" + "Param_DeveloperOptions_ResultSaved".GetLocalized();
            }
            Task.Run(async () => await LogHelper.Log($"Get status: {status}"));
        }
        catch (Exception ex)
        {
            LogHelper.LogError($"Applying user SMU settings error: {ex.Message}");
            ApplyInfo += "\n" + "SMUErrorDesc".GetLocalized();
        }
    }

    private static void TryConvertToUint(string text, out uint address)
    {
        try
        {
            address = Convert.ToUInt32(text.Trim().ToLower(), 16);
        }
        catch
        {
            throw new ApplicationException("Invalid hexadecimal value.");
        }
    }
    private void SMUOptions_Expander_Expanding(Expander sender, ExpanderExpandingEventArgs args)
    {
        if (_isSearching) 
        { 
            return; 
        }

        AddPopulatedSmuMailboxes();
    }

    private void ComboBoxMailboxSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComboBoxMailboxSelect.SelectedItem is MailboxListItem item)
        {
            InitTestMailbox(item.MsgAddr, item.RspAddr, item.ArgAddr);
        }
    }

    private void InitTestMailbox(uint msgAddr, uint rspAddr, uint argAddr)
    {
        _testMailbox.MsgAddress = msgAddr;
        _testMailbox.RspAddress = rspAddr;
        _testMailbox.ArgAddress = argAddr;
        ResetSmuAddresses();
    }

    private async void Mon_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var newWindow = new PowerWindow();
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

    private void SMUEnabl_Click(object sender, RoutedEventArgs e)
    {
        EnableSmu.IsOn = !EnableSmu.IsOn;
        SmuEnabl();
    }

    private void EnableSMU_Toggled(object sender, RoutedEventArgs e) => SmuEnabl();

    private void SmuEnabl()
    {
        if (EnableSmu.IsOn)
        {
            _presetManager.Presets[_indexpreset].SmuEnabled = true;
            _presetManager.SaveSettings();
        }
        else
        {
            _presetManager.Presets[_indexpreset].SmuEnabled = false;
            _presetManager.SaveSettings();
        }
    }

    private async void CreateQuickCommandSMU_Click(object sender, RoutedEventArgs e) => await QuickDialog(0, 0);
    private async void CreateQuickCommandSMU1_Click(object sender, RoutedEventArgs e) => await RangeDialog();

    private async Task QuickDialog(int destination, int rowindex)
    {
        try
        {
            _smuSymbol1 = new FontIcon
            {
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                Glyph = _smuSymbol,
                Margin = new Thickness(-4, -2, -5, -5)
            };

            var symbolButton = new Button
            {
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 41, 0, 0),
                Width = 40,
                Height = 40,
                Content = new ContentControl
                {
                    Content = _smuSymbol1
                },
                CornerRadius = new CornerRadius(10),
                Translation = new System.Numerics.Vector3(0, 0, 12),
                Shadow = SharedShadow
            };

            var comboSelSmu = new ComboBox
            {
                Margin = new Thickness(55, 5, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
                CornerRadius = new CornerRadius(10),
                Translation = new System.Numerics.Vector3(0, 0, 12),
                Shadow = SharedShadow
            };

            var mainText = new TextBox
            {
                Margin = new Thickness(55, 45, 0, 0),
                PlaceholderText = "New_Name".GetLocalized(),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 305,
                CornerRadius = new CornerRadius(10),
                Translation = new System.Numerics.Vector3(0, 0, 12),
                Shadow = SharedShadow
            };

            var descText = new TextBox
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(55, 85, 0, 0),
                PlaceholderText = "Desc".GetLocalized(),
                Width = 305,
                CornerRadius = new CornerRadius(10),
                Translation = new System.Numerics.Vector3(0, 0, 12),
                Shadow = SharedShadow
            };

            var cmdText = new TextBox
            {
                PlaceholderText = "Command".GetLocalized(),
                Width = 176,
                CornerRadius = new CornerRadius(10),
                Translation = new System.Numerics.Vector3(0, 0, 12),
                Shadow = SharedShadow
            };

            var argText = new TextBox
            {
                PlaceholderText = "Arguments".GetLocalized(),
                Width = 179,
                CornerRadius = new CornerRadius(10),
                Translation = new System.Numerics.Vector3(0, 0, 12),
                Shadow = SharedShadow
            };

            var autoRun = new CheckBox
            {
                Margin = new Thickness(1, 185, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Content = "Param_Autorun".GetLocalized(),
                IsChecked = false
            };

            var applyWith = new CheckBox
            {
                Margin = new Thickness(1, 215, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Content = "Param_WithApply".GetLocalized(),
                IsChecked = false
            };

            try
            {
                foreach (var item in ComboBoxMailboxSelect.Items)
                {
                    comboSelSmu.Items.Add(item);
                }

                comboSelSmu.SelectedIndex = ComboBoxMailboxSelect.SelectedIndex;
                comboSelSmu.SelectionChanged += ComboSelSMU_SelectionChanged;
                symbolButton.Click += SymbolButton_Click;
                if (destination != 0 && _smuSettings.QuickSmuCommands != null && rowindex >= 0 && _smuSettings.QuickSmuCommands.Count > rowindex)
                {
                    var command = _smuSettings.QuickSmuCommands[rowindex];

                    _smuSymbol = command.Symbol;
                    _smuSymbol1.Glyph = command.Symbol;

                    comboSelSmu.SelectedIndex = command.MailIndex;
                    mainText.Text = command.Name;
                    descText.Text = command.Description;
                    cmdText.Text = command.Command;
                    argText.Text = command.Argument;
                    autoRun.IsChecked = command.Startup;
                    applyWith.IsChecked = command.ApplyWith;
                }
            }
            catch (Exception ex)
            {
                await LogHelper.TraceIt_TraceError(ex);
            }

            try
            {
                var newQuickCommand = new ContentDialog
                {
                    Title = "AdvancedCooler_DeleteAction".GetLocalized(),
                    Content = new Grid
                    {
                        Children =
                        {
                            comboSelSmu,
                            symbolButton,
                            mainText,
                            descText,
                            new StackPanel
                            {
                                HorizontalAlignment = HorizontalAlignment.Left,
                                VerticalAlignment = VerticalAlignment.Top,
                                Margin = new Thickness(0, 122, 0, 0),
                                Orientation = Orientation.Vertical,
                                Children =
                                {
                                    new TextBlock
                                    {
                                        Margin = new Thickness(2,0,0,0),
                                        Text = "Command".GetLocalized(),
                                        Padding = new Thickness(0,0,0,3)
                                    },
                                    cmdText
                                }
                            },
                            new StackPanel
                            {
                                HorizontalAlignment = HorizontalAlignment.Left,
                                VerticalAlignment = VerticalAlignment.Top,
                                Margin = new Thickness(180, 122, 0, 0),
                                Orientation = Orientation.Vertical,
                                Children =
                                {
                                    new TextBlock
                                    {
                                        Margin = new Thickness(2,0,0,0),
                                        Text = "Arguments".GetLocalized(),
                                        Padding = new Thickness(0,0,0,3)
                                    },
                                    argText
                                }
                            },
                            autoRun,
                            applyWith
                        }
                    },
                    PrimaryButtonText = "Save".GetLocalized(),
                    CloseButtonText = "CancelThis/Text".GetLocalized(),
                    DefaultButton = ContentDialogButton.Close
                };
                if (destination != 0)
                {
                    newQuickCommand.SecondaryButtonText = "Delete".GetLocalized();
                }

                if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
                {
                    newQuickCommand.XamlRoot = XamlRoot;
                }

                // Отобразить ContentDialog и обработать результат
                try
                {
                    var result = await newQuickCommand.ShowAsync();
                    // Создать ContentDialog 
                    if (result == ContentDialogResult.Primary)
                    {
                        if (_smuSettings == null)
                        {
                            return;
                        }

                        var saveIndex = comboSelSmu.SelectedIndex;
                        for (var i = 0; i < comboSelSmu.Items.Count; i++)
                        {
                            var adressName = false;
                            comboSelSmu.SelectedIndex = i;
                            if (_smuSettings!.MailBoxes == null)
                            {
                                _smuSettings.MailBoxes =
                                [
                                    new CustomMailBoxes
                                    {
                                        Name = comboSelSmu.SelectedItem.ToString()!,
                                        Cmd = TextBoxCmdAddress.Text,
                                        Rsp = TextBoxRspAddress.Text,
                                        Arg = TextBoxArgAddress.Text
                                    }
                                ];
                            }
                            else
                            {
                                for (var d = 0; d < _smuSettings?.MailBoxes?.Count; d++)
                                {
                                    if (_smuSettings.MailBoxes[d].Name != string.Empty &&
                                        _smuSettings.MailBoxes[d].Name == comboSelSmu.SelectedItem.ToString())
                                    {
                                        adressName = true;
                                        break;
                                    }
                                }

                                if (adressName == false)
                                {
                                    _smuSettings?.MailBoxes?.Add(new CustomMailBoxes
                                    {
                                        Name = comboSelSmu.SelectedItem.ToString()!,
                                        Cmd = TextBoxCmdAddress.Text,
                                        Rsp = TextBoxRspAddress.Text,
                                        Arg = TextBoxArgAddress.Text
                                    });
                                }
                            }
                        }

                        _smuSettings!.SaveSettings();

                        if (cmdText.Text != string.Empty && argText.Text != string.Empty)
                        {
                            var run = false;
                            var apply = false;
                            if (autoRun.IsChecked == true)
                            {
                                run = true;
                            }

                            if (applyWith.IsChecked == true)
                            {
                                apply = true;
                            }

                            if (destination == 0)
                            {
                                _smuSettings.QuickSmuCommands ??= [];
                                _smuSettings.QuickSmuCommands.Add(new QuickSmuCommands
                                {
                                    Name = mainText.Text,
                                    Description = descText.Text,
                                    Symbol = _smuSymbol,
                                    MailIndex = saveIndex,
                                    Startup = run,
                                    ApplyWith = apply,
                                    Command = cmdText.Text,
                                    Argument = argText.Text
                                });
                            }
                            else
                            {
                                _smuSettings.QuickSmuCommands![rowindex].Symbol = _smuSymbol;
                                _smuSettings.QuickSmuCommands![rowindex].Symbol = _smuSymbol1.Glyph;
                                _smuSettings.QuickSmuCommands![rowindex].MailIndex = saveIndex;
                                _smuSettings.QuickSmuCommands![rowindex].Name = mainText.Text;
                                _smuSettings.QuickSmuCommands![rowindex].Description = descText.Text;
                                _smuSettings.QuickSmuCommands![rowindex].Command = cmdText.Text;
                                _smuSettings.QuickSmuCommands![rowindex].Argument = argText.Text;
                                _smuSettings.QuickSmuCommands![rowindex].Startup = run;
                                _smuSettings.QuickSmuCommands![rowindex].ApplyWith = apply;
                            }
                        }

                        ComboBoxMailboxSelect.SelectedIndex = saveIndex;
                        _smuSettings?.SaveSettings();
                        Init_QuickSMU();
                        newQuickCommand?.Hide();
                        newQuickCommand = null;
                    }
                    else
                    {

                        if (result == ContentDialogResult.Secondary)
                        {
                            _smuSettings?.QuickSmuCommands?.RemoveAt(rowindex);
                            _smuSettings?.SaveSettings();
                            Init_QuickSMU();
                        }
                        else
                        {
                            newQuickCommand?.Hide();
                            newQuickCommand = null;
                        }
                    }
                }
                catch
                {
                    newQuickCommand?.Hide();
                }

                comboSelSmu.SelectionChanged -= ComboSelSMU_SelectionChanged;
                symbolButton.Click -= SymbolButton_Click;
                newQuickCommand = null;
            }
            catch (Exception ex)
            {
                await LogHelper.TraceIt_TraceError(ex);
            }
        }
        catch (Exception e)
        {
            await LogHelper.TraceIt_TraceError(e);
        }
    }

    private async Task RangeDialog()
    {
        try
        {
            var comboSelSmu = new ComboBox
            {
                Margin = new Thickness(0, 20, 0, 0),
                VerticalAlignment = VerticalAlignment.Top
            };
            var cmdStart = new TextBox
            {
                Margin = new Thickness(0, 60, 0, 0),
                PlaceholderText = "Command".GetLocalized(),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Height = 40,
                Width = 360
            };
            var argStart = new TextBox
            {
                Margin = new Thickness(0, 105, 0, 0),
                PlaceholderText = "Param_Start".GetLocalized(),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Height = 40,
                Width = 176
            };
            var argEnd = new TextBox
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(180, 105, 0, 0),
                PlaceholderText = "Param_EndW".GetLocalized(),
                Height = 40,
                Width = 179
            };
            var autoRun = new CheckBox
            {
                Margin = new Thickness(1, 155, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Content = "Logging".GetLocalized(),
                IsChecked = false
            };
            try
            {
                foreach (var item in ComboBoxMailboxSelect.Items)
                {
                    comboSelSmu.Items.Add(item);
                }

                comboSelSmu.SelectedIndex = ComboBoxMailboxSelect.SelectedIndex;
                comboSelSmu.SelectionChanged += ComboSelSMU_SelectionChanged;
            }
            catch (Exception ex)
            {
                await LogHelper.TraceIt_TraceError(ex);
            }

            try
            {
                var newQuickCommand = new ContentDialog
                {
                    Title = "AdvancedCooler_DeleteAction".GetLocalized(),
                    Content = new Grid
                    {
                        Children =
                        {
                            comboSelSmu,
                            cmdStart,
                            argStart,
                            argEnd,
                            autoRun
                        }
                    },
                    PrimaryButtonText = "Apply".GetLocalized(),
                    CloseButtonText = "CancelThis/Text".GetLocalized(),
                    DefaultButton = ContentDialogButton.Close
                };
                if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
                {
                    newQuickCommand.XamlRoot = XamlRoot;
                }

                newQuickCommand.Closed += (_, _) =>
                {
                    newQuickCommand = null;
                };
                // Отобразить ContentDialog и обработать результат
                try
                {
                    var result = await newQuickCommand.ShowAsync();
                    // Создать ContentDialog 
                    if (result == ContentDialogResult.Primary)
                    {
                        var saveIndex = comboSelSmu.SelectedIndex;
                        for (var i = 0; i < comboSelSmu.Items.Count; i++)
                        {
                            var adressName = false;
                            comboSelSmu.SelectedIndex = i;
                            if (_smuSettings.MailBoxes == null)
                            {
                                _smuSettings.MailBoxes = [];
                                _smuSettings.MailBoxes?.Add(new CustomMailBoxes
                                {
                                    Name = comboSelSmu.SelectedItem.ToString()!,
                                    Cmd = TextBoxCmdAddress.Text,
                                    Rsp = TextBoxRspAddress.Text,
                                    Arg = TextBoxArgAddress.Text
                                });
                            }
                            else
                            {
                                for (var d = 0; d < _smuSettings.MailBoxes?.Count; d++)
                                {
                                    if (_smuSettings.MailBoxes != null &&
                                        _smuSettings.MailBoxes[d].Name != string.Empty &&
                                        _smuSettings.MailBoxes[d].Name == comboSelSmu.SelectedItem.ToString())
                                    {
                                        adressName = true;
                                        break;
                                    }
                                }

                                if (adressName == false)
                                {
                                    _smuSettings.MailBoxes?.Add(new CustomMailBoxes
                                    {
                                        Name = comboSelSmu.SelectedItem.ToString()!,
                                        Cmd = TextBoxCmdAddress.Text,
                                        Rsp = TextBoxRspAddress.Text,
                                        Arg = TextBoxArgAddress.Text
                                    });
                                }
                            }
                        }

                        _smuSettings.SaveSettings();
                        var run = false;
                        if (cmdStart.Text != string.Empty && argStart.Text != string.Empty &&
                            argEnd.Text != string.Empty)
                        {
                            if (autoRun.IsChecked == true)
                            {
                                run = true;
                            }

                            _sendSmuCommand.RangeCompleted += CloseRangeStarted;

                            _sendSmuCommand.SendRange(cmdStart.Text, argStart.Text, argEnd.Text, saveIndex, run);
                            RangeStarted.IsOpen = true;
                            RangeStarted.Title = "SMURange".GetLocalized() + ". " + argStart.Text + "-" + argEnd.Text;

                        }

                        ComboBoxMailboxSelect.SelectedIndex = saveIndex;
                        _smuSettings.SaveSettings();
                        Init_QuickSMU();
                        newQuickCommand?.Hide();
                        newQuickCommand = null;
                    }
                    else
                    {
                        newQuickCommand?.Hide();
                        newQuickCommand = null;
                    }
                }
                catch
                {
                    newQuickCommand?.Hide();
                    newQuickCommand = null;
                }
            }
            catch (Exception ex)
            {
                await LogHelper.TraceIt_TraceError(ex);
            }
        }
        catch (Exception e)
        {
            await LogHelper.TraceIt_TraceError(e);
        }
    }
    
    private void CloseRangeStarted(object? sender, object? args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            RangeStarted.IsOpen = false;
        });

        
        _sendSmuCommand.RangeCompleted -= CloseRangeStarted;
    }

    private void SymbolButton_Click(object sender, RoutedEventArgs e) => SymbolFlyout.ShowAt(sender as Button);

    private void ComboSelSMU_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            var comboBox = sender as ComboBox;
            if (comboBox != null)
            {
                ComboBoxMailboxSelect.SelectedIndex = comboBox.SelectedIndex;
            }
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex);
        }
    }

    private void SymbolList_ItemClick(object sender, ItemClickEventArgs e)
    {
        var glypher = (FontIcon)e.ClickedItem;
        if (glypher != null)
        {
            _smuSymbol = glypher.Glyph;
            _smuSymbol1!.Glyph = glypher.Glyph;
        }
    }

    private void SMUNotes_TextChanged(object sender, RoutedEventArgs e)
    {
        var documentRange = SmuNotes.Document.GetRange(0, TextConstants.MaxUnitCount);
        documentRange.GetText(TextGetOptions.FormatRtf, out var content);
        _smuSettings.Note = content.TrimEnd();
        _smuSettings.SaveSettings();
    }

    private void ToHex_Click(object sender, RoutedEventArgs e)
    {
        // Преобразование выделенного текста в шестнадцатиричную систему
        if (TextBoxArg0.SelectedText != "")
        {
            try
            {
                var decimalValue = int.Parse(TextBoxArg0.SelectedText);
                var hexValue = decimalValue.ToString("X");
                TextBoxArg0.SelectedText = hexValue;
            }
            catch (Exception ex)
            {
                LogHelper.TraceIt_TraceError(ex);
            }
        }
        else
        {
            try
            {
                var decimalValue = int.Parse(TextBoxArg0.Text);
                var hexValue = decimalValue.ToString("X");
                TextBoxArg0.Text = hexValue;
            }
            catch (Exception ex)
            {
                LogHelper.TraceIt_TraceError(ex);
            }
        }
    }

    private void CopyThis_Click(object sender, RoutedEventArgs e)
    {
        if (TextBoxArg0.SelectedText != "")
        {
            // Скопировать текст в буфер обмена
            var dataPackage = new DataPackage
            {
                RequestedOperation = DataPackageOperation.Copy
            };
            dataPackage.SetText(TextBoxArg0.SelectedText);
            Clipboard.SetContent(dataPackage);
        }
        else
        {
            // Выделить весь текст
            TextBoxArg0.SelectAll();
            // Скопировать текст в буфер обмена
            var dataPackage = new DataPackage
            {
                RequestedOperation = DataPackageOperation.Copy
            };
            dataPackage.SetText(TextBoxArg0.Text);
            Clipboard.SetContent(dataPackage);
        }
    }

    private void CutThis_Click(object sender, RoutedEventArgs e)
    {
        if (TextBoxArg0.SelectedText != "")
        {
            // Скопировать текст в буфер обмена
            var dataPackage = new DataPackage
            {
                RequestedOperation = DataPackageOperation.Copy
            };
            dataPackage.SetText(TextBoxArg0.SelectedText);
            Clipboard.SetContent(dataPackage);
            // Обнулить текст
            TextBoxArg0.SelectedText = "";
        }
        else
        {
            // Выделить весь текст
            TextBoxArg0.SelectAll();
            // Скопировать текст в буфер обмена
            var dataPackage = new DataPackage
            {
                RequestedOperation = DataPackageOperation.Copy
            };
            dataPackage.SetText(TextBoxArg0.Text);
            Clipboard.SetContent(dataPackage);
            TextBoxArg0.Text = "";
        }
    }

    private void SelectAllThis_Click(object sender, RoutedEventArgs e)
    {
        // Выделить весь текст
        TextBoxArg0.SelectAll();
    }

    private void CancelRange_Click(object sender, RoutedEventArgs e)
    {
        _sendSmuCommand.CancelRange();
        CloseInfoRange();
    }

    private void CloseInfoRange() => RangeStarted.IsOpen = false;

    #endregion

    #region Event Handlers and Custom Preset voids

    private async void PresetChanged(object? sender, PresetManagerService.PresetId e)
    {
        if (e.PresetKey == "Custom")
        {
            _waitforload = true;
            var index = e.PresetIndex;
            _appSettings.Preset = index;

            _indexpreset = index;
            PresetCom.SelectedIndex = index + 1;
            _waitforload = false;
            await MainInit(index);
        }
    }

    private async void PresetCOM_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (!_isLoaded || _waitforload)
            {
                return;
            }

            if (PresetCom.SelectedIndex != -1)
            {
                _appSettings.Preset = PresetCom.SelectedIndex - 1;
                _appSettings.SaveSettings();
            }

            _indexpreset = PresetCom.SelectedIndex - 1;
            await MainInit(PresetCom.SelectedIndex - 1);
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception);
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
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = C1.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Cpu1 = check;
            _presetManager.Presets[_indexpreset].Cpu1Value = C1V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Лимит CPU (W)
    private void C2_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = C2.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Cpu2 = check;
            _presetManager.Presets[_indexpreset].Cpu2Value = C2V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Реальный CPU (W)
    private void C3_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = C3.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Cpu3 = check;
            _presetManager.Presets[_indexpreset].Cpu3Value = C3V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Средний CPU (W)
    private void C4_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = C4.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Cpu4 = check;
            _presetManager.Presets[_indexpreset].Cpu4Value = C4V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Тик быстрого разгона (S)
    private void C5_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = C5.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Cpu5 = check;
            _presetManager.Presets[_indexpreset].Cpu5Value = C5V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Тик медленного разгона (S)
    private void C6_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = C6.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Cpu6 = check;
            _presetManager.Presets[_indexpreset].Cpu6Value = C6V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Параметры VRM
    //Максимальный ток VRM A
    private void V1_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = V1.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Vrm1 = check;
            _presetManager.Presets[_indexpreset].Vrm1Value = V1V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Лимит по току VRM A
    private void V2_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = V2.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Vrm2 = check;
            _presetManager.Presets[_indexpreset].Vrm2Value = V2V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Максимальный ток SOC A
    private void V3_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = V3.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Vrm3 = check;
            _presetManager.Presets[_indexpreset].Vrm3Value = V3V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Лимит по току SOC A
    private void V4_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = V4.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Vrm4 = check;
            _presetManager.Presets[_indexpreset].Vrm4Value = V4V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Максимальный ток PCI VDD A
    private void V5_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = V5.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Vrm5 = check;
            _presetManager.Presets[_indexpreset].Vrm5Value = V5V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Максимальный ток PCI SOC A
    private void V6_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = V6.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Vrm6 = check;
            _presetManager.Presets[_indexpreset].Vrm6Value = V6V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Отключить троттлинг на время
    private void V7_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = V7.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Vrm7 = check;
            _presetManager.Presets[_indexpreset].Vrm7Value = V7V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Параметры графики
    //Минимальная частота SOC 
    private void G1_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = G1.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Gpu1 = check;
            _presetManager.Presets[_indexpreset].Gpu1Value = G1V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Максимальная частота SOC
    private void G2_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = G2.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Gpu2 = check;
            _presetManager.Presets[_indexpreset].Gpu2Value = G2V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Минимальная частота Infinity Fabric
    private void G3_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = G3.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Gpu3 = check;
            _presetManager.Presets[_indexpreset].Gpu3Value = G3V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Максимальная частота Infinity Fabric
    private void G4_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = G4.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Gpu4 = check;
            _presetManager.Presets[_indexpreset].Gpu4Value = G4V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Минимальная частота кодека VCE
    private void G5_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = G5.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Gpu5 = check;
            _presetManager.Presets[_indexpreset].Gpu5Value = G5V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Максимальная частота кодека VCE
    private void G6_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = G6.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Gpu6 = check;
            _presetManager.Presets[_indexpreset].Gpu6Value = G6V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Минимальная частота частота Data Latch
    private void G7_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = G7.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Gpu7 = check;
            _presetManager.Presets[_indexpreset].Gpu7Value = G7V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Максимальная частота Data Latch
    private void G8_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = G8.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Gpu8 = check;
            _presetManager.Presets[_indexpreset].Gpu8Value = G8V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Минимальная частота iGpu
    private void G9_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = G9.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Gpu9 = check;
            _presetManager.Presets[_indexpreset].Gpu9Value = G9V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Максимальная частота iGpu
    private void G10_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = G10.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Gpu10 = check;
            _presetManager.Presets[_indexpreset].Gpu10Value = G10V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Расширенные параметры

    private void A4_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = A4.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Advncd4 = check;
            _presetManager.Presets[_indexpreset].Advncd4Value = A4V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A5_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = A5.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Advncd5 = check;
            _presetManager.Presets[_indexpreset].Advncd5Value = A5V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A6_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = A6.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Advncd6 = check;
            _presetManager.Presets[_indexpreset].Advncd6Value = A6V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A7_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = A7.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Advncd7 = check;
            _presetManager.Presets[_indexpreset].Advncd7Value = A7V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A8_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = A8.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Advncd8 = check;
            _presetManager.Presets[_indexpreset].Advncd8Value = A8V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A9_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = A9.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Advncd9 = check;
            _presetManager.Presets[_indexpreset].Advncd9Value = A9V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A10_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = A10.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Advncd10 = check;
            _presetManager.Presets[_indexpreset].Advncd10Value = A10V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A11_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = A11.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Advncd11 = check;
            _presetManager.Presets[_indexpreset].Advncd11Value = A11V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A12_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = A12.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Advncd12 = check;
            _presetManager.Presets[_indexpreset].Advncd12Value = A12V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A13_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = A13.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Advncd13 = check;
            _presetManager.Presets[_indexpreset].Advncd1Value = A13M.SelectedIndex;
            _presetManager.SaveSettings();
        }
    }

    //Оптимизатор кривой
    private void CCD2_8_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = Ccd28.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper15 = check;
            _presetManager.Presets[_indexpreset].Coper15Value = Ccd28V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_7_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = Ccd27.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper14 = check;
            _presetManager.Presets[_indexpreset].Coper14Value = Ccd27V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_6_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = Ccd26.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper13 = check;
            _presetManager.Presets[_indexpreset].Coper13Value = Ccd26V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_5_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = Ccd25.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper12 = check;
            _presetManager.Presets[_indexpreset].Coper12Value = Ccd25V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_4_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = Ccd24.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper11 = check;
            _presetManager.Presets[_indexpreset].Coper11Value = Ccd24V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_3_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = Ccd23.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper10 = check;
            _presetManager.Presets[_indexpreset].Coper10Value = Ccd23V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_2_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = Ccd22.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper9 = check;
            _presetManager.Presets[_indexpreset].Coper9Value = Ccd22V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_1_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = Ccd21.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper8 = check;
            _presetManager.Presets[_indexpreset].Coper8Value = Ccd21V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_8_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = Ccd18.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper7 = check;
            _presetManager.Presets[_indexpreset].Coper7Value = Ccd18V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_7_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = Ccd17.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper6 = check;
            _presetManager.Presets[_indexpreset].Coper6Value = Ccd17V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_6_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = Ccd16.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper5 = check;
            _presetManager.Presets[_indexpreset].Coper5Value = Ccd16V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_5_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = Ccd15.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper4 = check;
            _presetManager.Presets[_indexpreset].Coper4Value = Ccd15V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_4_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = Ccd14.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper3 = check;
            _presetManager.Presets[_indexpreset].Coper3Value = Ccd14V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_3_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = Ccd13.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper2 = check;
            _presetManager.Presets[_indexpreset].Coper2Value = Ccd13V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_2_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }
        
        var check = Ccd12.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper1 = check;
            _presetManager.Presets[_indexpreset].Coper1Value = Ccd12V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_1_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = Ccd11.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper0 = check;
            _presetManager.Presets[_indexpreset].Coper0Value = Ccd11V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void O1_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = O1.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coall = check;
            _presetManager.Presets[_indexpreset].Coallvalue = O1V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void O2_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = O2.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Cogfx = check;
            _presetManager.Presets[_indexpreset].Cogfxvalue = O2V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD_CO_Mode_Sel_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
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
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Comode = check;
            _presetManager.Presets[_indexpreset].Coprefmode = CcdCoMode.SelectedIndex;
            _presetManager.SaveSettings();
        }
    }

    //Параметры процессора, при изменении слайдеров
    //Максимальная температура CPU (C)
    private void C1_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Cpu1Value = C1V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Лимит CPU (W)
    private void C2_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Cpu2Value = C2V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Реальный CPU (W)
    private void C3_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Cpu3Value = C3V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Средний CPU(W)
    private void C4_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Cpu4Value = C4V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Тик быстрого разгона (S)
    private void C5_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Cpu5Value = C5V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Тик медленного разгона (S)
    private void C6_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Cpu6Value = C6V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Параметры VRM
    private void V1v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Vrm1Value = V1V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void V2v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Vrm2Value = V2V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void V3v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Vrm3Value = V3V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void V4v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Vrm4Value = V4V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void V5v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Vrm5Value = V5V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void V6v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Vrm6Value = V6V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void V7v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Vrm7Value = V7V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Параметры GPU
    private void G1v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Gpu1Value = G1V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void G2v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }
        
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Gpu2Value = G2V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void G3v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Gpu3Value = G3V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void G4v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Gpu4Value = G4V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void G5v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }
        
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Gpu5Value = G5V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void G6v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Gpu6Value = G6V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void G7v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Gpu7Value = G7V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void G8v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Gpu8Value = G8V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void G9v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Gpu9Value = G9V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void G10v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Gpu10Value = G10V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Расширенные параметры

    private void A4v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Advncd4Value = A4V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A5v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Advncd5Value = A5V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A6v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Advncd6Value = A6V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A7v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Advncd7Value = A7V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A8v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Advncd8Value = A8V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A9v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Advncd9Value = A9V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A10v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Advncd10Value = A10V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A11v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Advncd11Value = A11V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A12v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Advncd12Value = A12V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A13m_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Advncd13Value = A13M.SelectedIndex;
            _presetManager.SaveSettings();
        }
    }

    //Новые
    private void C7_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = C7.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Cpu7 = check;
            _presetManager.Presets[_indexpreset].Cpu7Value = C7V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void C7_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Cpu7Value = C7V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void G16_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = G16.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Gpu16 = check;
            _presetManager.Presets[_indexpreset].Gpu16Value = G16M.SelectedIndex;
            _presetManager.SaveSettings();
        }
    }

    private void G16m_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Gpu16Value = G16M.SelectedIndex;
            _presetManager.SaveSettings();
        }
    }

    private void A14_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = A14.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Advncd14 = check;
            _presetManager.Presets[_indexpreset].Advncd14Value = A14M.SelectedIndex;
            _presetManager.SaveSettings();
        }
    }

    private void A14m_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Advncd14Value = A14M.SelectedIndex;
            _presetManager.SaveSettings();
        }
    }

    private void A15_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        var check = A15.IsChecked == true;
        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Advncd15 = check;
            _presetManager.Presets[_indexpreset].Advncd15Value = A15V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void A15v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Advncd15Value = A15V.Value;
            _presetManager.SaveSettings();
        }
    }

    //Слайдеры из оптимизатора кривой 
    private void O1v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coallvalue = O1V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void O2v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Cogfxvalue = O2V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_1v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper0Value = Ccd11V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_2v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper1Value = Ccd12V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_3v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper2Value = Ccd13V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_4v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper3Value = Ccd14V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_5v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper4Value = Ccd15V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_6v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper5Value = Ccd16V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_7v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper6Value = Ccd17V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD1_8v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper7Value = Ccd18V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_1v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper8Value = Ccd21V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_2v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper9Value = Ccd22V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_3v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper10Value = Ccd23V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_4v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper11Value = Ccd24V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_5v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper12Value = Ccd25V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_6v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper13Value = Ccd26V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_7v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper14Value = Ccd27V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD2_8v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
        {
            return;
        }

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coper15Value = Ccd28V.Value;
            _presetManager.SaveSettings();
        }
    }

    private void CCD_CO_Mode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded || _waitforload)
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

        if (_indexpreset != -1)
        {
            _presetManager.Presets[_indexpreset].Coprefmode = CcdCoMode.SelectedIndex;
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
            await _applyer.ApplyCustomPreset(_presetManager.Presets[_indexpreset], 
                true, DeveloperSettingsMode.Visibility == Visibility.Visible);
            /*if (EnablePstates.IsOn)
            {
                await BtnPstateWrite_Click();
            }*/

            if (TextBoxArg0 != null &&
                TextBoxArgAddress != null &&
                TextBoxCmd != null &&
                TextBoxCmdAddress != null &&
                TextBoxRspAddress != null &&
                EnableSmu.IsOn)
            {
                ApplySettings(0, 0);
            }

            var timerCounter = 0;
            while (SettingsApplied == false)
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

            if (SettingsViewModel.VersionId != 5) // Если версия не Debug Lanore
            {
                ApplyTooltip.Title = "Apply_Success".GetLocalized();
                ApplyTooltip.Subtitle = "";
            }
            else
#pragma warning disable CS0162 // Unreachable code detected
            // ReSharper disable once HeuristicUnreachableCode
            {
                ApplyTooltip.Title = "Apply_Success".GetLocalized();
                ApplyTooltip.Subtitle = "" + _appSettings.RyzenAdjLine;
            }
#pragma warning restore CS0162 // Unreachable code detected
            ApplyTooltip.IconSource = new SymbolIconSource { Symbol = Symbol.Accept };
            ApplyTooltip.IsOpen = true;
            var infoSet = InfoBarSeverity.Success;
            if (ApplyInfo != string.Empty && _commandReturnedValue == false)
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
            _sendSmuCommand.ApplyQuickSmuCommand(false);
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
                _indexpreset += 1;
                _waitforload = true;
                PresetCom.Items.Add(SavePresetN.Text);
                PresetCom.SelectedItem = SavePresetN.Text;
                _presetManager.AddPreset(new Preset { Presetname = SavePresetN.Text });

                _waitforload = false;
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
                if (PresetCom.SelectedIndex == 0 || _indexpreset + 1 == 0)
                {
                    UnsavedTooltip.IsOpen = true;
                    await Task.Delay(3000);
                    UnsavedTooltip.IsOpen = false;
                }
                else
                {
                    _presetManager.Presets[_indexpreset].Presetname = EditPresetN.Text;
                    _presetManager.SaveSettings();
                    _waitforload = true;
                    PresetCom.Items.Clear();
                    PresetCom.Items.Add(new ComboBoxItem
                    {
                        Content = new TextBlock
                        {
                            Text = "Param_Premaded".GetLocalized(),
                            Foreground = (Brush)Application.Current.Resources["AccentTextFillColorTertiaryBrush"]
                        },
                        IsEnabled = false
                    });
                    foreach (var currPreset in _presetManager.Presets)
                    {
                        if (currPreset.Presetname != string.Empty || currPreset.Presetname != "Unsigned preset")
                        {
                            PresetCom.Items.Add(currPreset.Presetname);
                        }
                    }

                    PresetCom.SelectedIndex = 0;
                    _waitforload = false;
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
                    _waitforload = true;
                    PresetCom.Items.Remove(PresetCom.SelectedItem);
                    _presetManager.RemovePreset(_indexpreset);
                    _indexpreset = 0;
                    _waitforload = false;

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

    private void SMU_Func_Click(object sender, RoutedEventArgs e) => Save_SMUFunctions(true);
    private void SMU_Func_Enabl_Toggled(object sender, RoutedEventArgs e) => Save_SMUFunctions(false);
    private void FEATURE_CCLK_CONTROLLER_Click(object sender, RoutedEventArgs e) => Save_SMUFeatureCCLK(true);
    private void Bit_0_FEATURE_CCLK_CONTROLLER_Toggled(object sender, RoutedEventArgs e) => Save_SMUFeatureCCLK(false);
    private void FEATURE_DATA_CALCULATION_Click(object sender, RoutedEventArgs e) => Save_SMUFeatureData(true);
    private void Bit_2_FEATURE_DATA_CALCULATION_Toggled(object sender, RoutedEventArgs e) => Save_SMUFeatureData(false);
    private void FEATURE_PPT_Click(object sender, RoutedEventArgs e) => Save_SMUFeaturePPT(true);
    private void Bit_3_FEATURE_PPT_Toggled(object sender, RoutedEventArgs e) => Save_SMUFeaturePPT(false);
    private void FEATURE_TDC_Click(object sender, RoutedEventArgs e) => Save_SMUFeatureTDC(true);
    private void Bit_4_FEATURE_TDC_Toggled(object sender, RoutedEventArgs e) => Save_SMUFeatureTDC(false);
    private void Bit_5_FEATURE_THERMAL_Toggled(object sender, RoutedEventArgs e) => Save_SMUFeatureThermal(false);
    private void FEATURE_THERMAL_Click(object sender, RoutedEventArgs e) => Save_SMUFeatureThermal(true);

    private void Bit_8_FEATURE_PLL_POWER_DOWN_Toggled(object sender, RoutedEventArgs e) =>
        Save_SMUFeaturePowerDown(false);

    private void FEATURE_PLL_POWER_DOWN_Click(object sender, RoutedEventArgs e) => Save_SMUFeaturePowerDown(true);
    private void FEATURE_PROCHOT_Click(object sender, RoutedEventArgs e) => Save_SMUFeatureProchot(true);
    private void Bit_37_FEATURE_PROCHOT_Toggled(object sender, RoutedEventArgs e) => Save_SMUFeatureProchot(false);
    private void FEATURE_STAPM_Click(object sender, RoutedEventArgs e) => Save_SMUFeatureSTAPM(true);
    private void Bit_39_FEATURE_STAPM_Toggled(object sender, RoutedEventArgs e) => Save_SMUFeatureSTAPM(false);
    private void FEATURE_CORE_CSTATES_Click(object sender, RoutedEventArgs e) => Save_SMUFeatureCStates(true);
    private void Bit_40_FEATURE_CORE_CSTATES_Toggled(object sender, RoutedEventArgs e) => Save_SMUFeatureCStates(false);
    private void FEATURE_GFX_DUTY_CYCLE_Click(object sender, RoutedEventArgs e) => Save_SMUFeatureGFXDutyCycle(true);

    private void Bit_41_FEATURE_GFX_DUTY_CYCLE_Toggled(object sender, RoutedEventArgs e) =>
        Save_SMUFeatureGFXDutyCycle(false);

    private void FEATURE_AA_MODE_Click(object sender, RoutedEventArgs e) => Save_SMUFeaturAplusA(true);
    private void Bit_42_FEATURE_AA_MODE_Toggled(object sender, RoutedEventArgs e) => Save_SMUFeaturAplusA(false);

    private void Save_SMUFunctions(bool isButton)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (isButton)
        {
            SmuFuncEnableToggle.IsOn = SmuFuncEnableToggle.IsOn != true;
        }

        try
        {
            _presetManager.Presets[PresetCom.SelectedIndex - 1].SmuFunctionsEnabl = SmuFuncEnableToggle.IsOn;
            _presetManager.SaveSettings();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex);
        }
    }

    private void Save_SMUFeatureCCLK(bool isButton)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (isButton)
        {
            Bit0FeatureCclkController.IsOn = Bit0FeatureCclkController.IsOn != true;
        }

        try
        {
            _presetManager.Presets[PresetCom.SelectedIndex - 1].SmuFeatureCclk = Bit0FeatureCclkController.IsOn;
            _presetManager.SaveSettings();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex);
        }
    }

    private void Save_SMUFeatureData(bool isButton)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (isButton)
        {
            Bit2FeatureDataCalculation.IsOn = Bit2FeatureDataCalculation.IsOn != true;
        }

        try
        {
            _presetManager.Presets[PresetCom.SelectedIndex - 1].SmuFeatureData = Bit2FeatureDataCalculation.IsOn;
            _presetManager.SaveSettings();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex);
        }
    }

    private void Save_SMUFeaturePPT(bool isButton)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (isButton)
        {
            Bit3FeaturePpt.IsOn = Bit3FeaturePpt.IsOn != true;
        }

        try
        {
            _presetManager.Presets[PresetCom.SelectedIndex - 1].SmuFeaturePpt = Bit3FeaturePpt.IsOn;
            _presetManager.SaveSettings();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex);
        }
    }

    private void Save_SMUFeatureTDC(bool isButton)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (isButton)
        {
            Bit4FeatureTdc.IsOn = Bit4FeatureTdc.IsOn != true;
        }

        try
        {
            _presetManager.Presets[PresetCom.SelectedIndex - 1].SmuFeatureTdc = Bit4FeatureTdc.IsOn;
            _presetManager.SaveSettings();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex);
        }
    }

    private void Save_SMUFeatureThermal(bool isButton)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (isButton)
        {
            Bit5FeatureThermal.IsOn = Bit5FeatureThermal.IsOn != true;
        }

        try
        {
            _presetManager.Presets[PresetCom.SelectedIndex - 1].SmuFeatureThermal = Bit5FeatureThermal.IsOn;
            _presetManager.SaveSettings();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex);
        }
    }

    private void Save_SMUFeaturePowerDown(bool isButton)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (isButton)
        {
            Bit8FeaturePllPowerDown.IsOn = Bit8FeaturePllPowerDown.IsOn != true;
        }

        try
        {
            _presetManager.Presets[PresetCom.SelectedIndex - 1].SmuFeaturePowerDown = Bit8FeaturePllPowerDown.IsOn;
            _presetManager.SaveSettings();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex);
        }
    }

    private void Save_SMUFeatureProchot(bool isButton)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (isButton)
        {
            Bit37FeatureProchot.IsOn = Bit37FeatureProchot.IsOn != true;
        }

        try
        {
            _presetManager.Presets[PresetCom.SelectedIndex - 1].SmuFeatureProchot = Bit37FeatureProchot.IsOn;
            _presetManager.SaveSettings();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex);
        }
    }

    private void Save_SMUFeatureSTAPM(bool isButton)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (isButton)
        {
            Bit39FeatureStapm.IsOn = Bit39FeatureStapm.IsOn != true;
        }

        try
        {
            _presetManager.Presets[PresetCom.SelectedIndex - 1].SmuFeatureStapm = Bit39FeatureStapm.IsOn;
            _presetManager.SaveSettings();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex);
        }
    }

    private void Save_SMUFeatureCStates(bool isButton)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (isButton)
        {
            Bit40FeatureCoreCstates.IsOn = Bit40FeatureCoreCstates.IsOn != true;
        }

        try
        {
            _presetManager.Presets[PresetCom.SelectedIndex - 1].SmuFeatureCStates = Bit40FeatureCoreCstates.IsOn;
            _presetManager.SaveSettings();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex);
        }
    }

    private void Save_SMUFeatureGFXDutyCycle(bool isButton)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (isButton)
        {
            Bit41FeatureGfxDutyCycle.IsOn = Bit41FeatureGfxDutyCycle.IsOn != true;
        }

        try
        {
            _presetManager.Presets[PresetCom.SelectedIndex - 1].SmuFeatureGfxDutyCycle = Bit41FeatureGfxDutyCycle.IsOn;
            _presetManager.SaveSettings();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex);
        }
    }

    private void Save_SMUFeaturAplusA(bool isButton)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (isButton)
        {
            Bit42FeatureAaMode.IsOn = Bit42FeatureAaMode.IsOn != true;
        }

        try
        {
            _presetManager.Presets[PresetCom.SelectedIndex - 1].SmuFeatureAplusA = Bit42FeatureAaMode.IsOn;
            _presetManager.SaveSettings();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex);
        }
    }

    //NumberBoxes
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
                    slider.Maximum = FromValueToUpperFive(sender.Value);
                }
            }
        }        
    }

    private void BackToNormalMode_Click(object sender, RoutedEventArgs e)
    {
        ToolTipService.SetToolTip(ActionButtonApply, "Param_Apply/ToolTipService/ToolTip".GetLocalized());
        DeveloperSettingsMode.Visibility = Visibility.Collapsed;
        DeveloperOptionsApply.Visibility = Visibility.Collapsed;
        NormalUserMode.Visibility = Visibility.Visible;
        ParamName.Text = "Param_Name/Text".GetLocalized();
        SuggestionsFilterStackPanel.Visibility = Visibility.Visible;
    }

    private void OpenDeveloperOptionsMode_Click(object sender, RoutedEventArgs e)
    {
        ToolTipService.SetToolTip(ActionButtonApply, "Param_Apply_DevOptions/ToolTipService/ToolTip".GetLocalized());
        NormalUserMode.Visibility = Visibility.Collapsed;
        DeveloperSettingsMode.Visibility = Visibility.Visible;
        DeveloperOptionsApply.Visibility = Visibility.Visible;
        ParamName.Text = "Param_DeveloperOptions_Name/Text".GetLocalized();
        SuggestionsFilterStackPanel.Visibility = Visibility.Collapsed;
    }

    #endregion

}