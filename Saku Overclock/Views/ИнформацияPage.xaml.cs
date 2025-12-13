using System.Numerics;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.SmuEngine;
using Saku_Overclock.Wrappers;
using ZenStates.Core;
using Brush = Microsoft.UI.Xaml.Media.Brush;
using VisualTreeHelper = Saku_Overclock.Helpers.VisualTreeHelper;

namespace Saku_Overclock.Views;

public sealed partial class ИнформацияPage
{
    /// <summary>
    ///     Класс для хранения минимальных и максимальных значений TrayMon
    /// </summary>
    public class MinMax
    {
        public double Min;
        public double Max;
    }

    private readonly IAppSettingsService _appSettings = App.GetService<IAppSettingsService>(); // Настройки приложения
    private double _busyRam; // Текущее использование ОЗУ и всего ОЗУ
    private double _totalRam;
    private bool _loaded; // Страница загружена
    private bool _doNotTrackBattery; // Флаг не использования батареи 
    private bool _isBatteryInformationLoaded; // Флаг обновления информации о батарее
    private bool _isDiscreteGpuInformationLoaded; // Флаг обновления информации о дискретной видеокарте
    private readonly List<Point> _cpuPointer = []; // Лист графика использования процессора
    private readonly List<Point> _gpuPointer = []; // Лист графика частоты графического процессора
    private readonly List<Point> _ramPointer = []; // Лист графика занятой ОЗУ
    private readonly List<Point> _vrmPointer = []; // Лист графика тока VRM
    private readonly List<Point> _batPointer = []; // Лист графика зарядки батареи
    private readonly List<Point> _pstPointer = []; // Лист графика изменения P-State
    private readonly List<double> _pstatesList = [0, 0, 0]; // Лист с информацией о P-State
    private static readonly Point MaxedPoint = new(65, 54);
    private static readonly Point StartPoint = new(-2, 54);
    private static readonly Point ZeroPoint = new(0, 0);
    private static readonly CornerRadius DefaultCornerRadius = new(10);

    private double
        _maxGfxClock =
            0.1; // Максимальная частота графического процессора, используется для графика частоты графического процессора

    private double _maxBatRate = 0.1d; // Максимальная мощность зарядки, используется для графика зарядки батареи
    private Brush? _transparentBrush; // Прозрачная кисть, используется для кнопок выбора баннера
    private Brush? _selectedBrush; // Кисть цвета выделенной кнопки, используется для кнопок выбора баннера

    private Brush?
        _selectedBorderBrush; // Кисть цвета границы выделенной кнопки, используется для кнопок выбора баннера

    private int _selectedGroup; // Текущий выбранный баннер, используется для кнопок выбора баннера
    private string _cpuName = "Unknown"; // Название процессора в системе
    private string _ramName = "Unknown"; // Название ОЗУ в системе
    private string _batName = "Unknown"; // Название батареи в системе
    private int _numberOfCores; // Количество ядер
    private int _numberOfLogicalProcessors; // Количество потоков
    private DispatcherTimer? _dispatcherTimer; // Таймер для автообновления информации
    private readonly IBackgroundDataUpdater? _dataUpdater = App.BackgroundUpdater; // Фоновое обновление информации
    private SensorsInformation? _sensorsInformation; // Информация с датчиков
    private readonly Cpu? _cpu = CpuSingleton.GetInstance(); // Инициализация ZenStates Core

    private static readonly string BatFromWall = "InfoBatteryAC".GetLocalized(); // Устройство от сети
    private static readonly string MhzFreq = "InfoFreqBoundsMHZ".GetLocalized(); // Частота МГц
    private static readonly string GhzFreq = "infoAGHZ".GetLocalized(); // Частота ГГц
    private static readonly string Pstate = "InfoPSTState".GetLocalized(); // P-State
    private static readonly string PowerDisabled = "Info_PowerSumInfo_Disabled".GetLocalized(); // Отключен
    private bool _vrmTimingsDetected; // Флаг инициализации типа изменения таймингов VRM
    private double _prevSlow; // Динамическое изменение таймингов VRM
    private double _prevFast; // Динамическое изменение таймингов VRM
    private int _vrmTimingIteration; // Текущая итерация проверки таймингов
    private bool _isIntegratedGpuPresent; // Наличие встроенной видеокарты
    private static readonly string StaticTimingsText = "Info_VrmNoChangeTimings".GetLocalized(); // Статические тайминги

    private static readonly string
        TimingsNotFoundText = "Info_VrmNotFoundTimings".GetLocalized(); // Не удалось обнаружить тайминги

    private static readonly string
        HighChargeLevel = "InfoBatterySuggestion_HighChargeLevel/Text".GetLocalized(); // Высокий уровень заряда

    private static readonly string
        LowChargeLevel = "InfoBatterySuggestion_LowChargeLevel/Text".GetLocalized(); // Низкий уровень заряда

    private static readonly string
        NormalChargeLevel = "InfoBatterySuggestion_NormalChargeLevel/Text".GetLocalized(); // Низкий уровень заряда

    private static readonly string
        BatteryBadHealth = "InfoBatterySuggestion_BadHealth/Text".GetLocalized(); // Низкий уровень заряда

    private Button[] _allBannerButtons = [];
    private Button[] _allExpandButtons = [];

    // Константы для индексов выбора секции
    private const int SectionFrequency = 0;
    private const int SectionVoltage = 1;
    private const int SectionPower = 2;
    private const int SectionTemperature = 3;

    // Константы для групп отображения
    private const int GroupCpu = 0;
    private const int GroupGpu = 1;
    private const int GroupRam = 2;
    private const int GroupVrm = 3;
    private const int GroupBattery = 4;
    private const int GroupCpuPst = 5;

    private const int MaxCores = 16;
    private const double MaxValidFrequency = 7.0;
    private const double MaxValidVoltage = 1.7;

    public ИнформацияPage()
    {
        InitializeComponent();

        if (_dataUpdater != null)
        {
            _dataUpdater.DataUpdated += OnDataUpdated;
        }

        Loaded += ИнформацияPage_Loaded;
        Unloaded += ИнформацияPage_Unloaded;
    }

    #region Initialization

    #region Get-Info voids

    /// <summary>
    ///     Основной метод для получения и обновления значений сенсоров системы
    /// </summary>
    private void OnDataUpdated(object? sender, SensorsInformation info) => _sensorsInformation = info;

    /// <summary>
    ///     Вспомогательный метод для правильной установки цветов элементов
    /// </summary>
    private void SetThemeAccentTextForeground()
    {
        if (CpuFrequency.Foreground is SolidColorBrush brush)
        {
            if (brush.Color == Color.FromArgb(228, 0, 0, 0))
            {
                CpuUsageBannerPolygonText.Foreground = brush;
                CpuUsageBigBannerPolygonText.Foreground = brush;
                GpuUsageBannerPolygonText.Foreground = brush;
                GpuUsageBigBannerPolygonText.Foreground = brush;
                VrmUsageBannerPolygonText.Foreground = brush;
                VrmUsageBigBannerPolygonText.Foreground = brush;
                RamUsageBannerPolygonText.Foreground = brush;
                RamUsageBigBannerPolygonText.Foreground = brush;
                BatUsageBannerPolygonText.Foreground = brush;
                BatUsageBigBannerPolygonText.Foreground = brush;
                PstUsageBannerPolygonText.Foreground = brush;
                PstUsageBigBannerPolygonText.Foreground = brush;
            }
        }
    }

    /// <summary>
    ///     Основной метод отображения характеристик встроенной видеокарты
    /// </summary>
    private void LoadIntegratedGpuInformation()
    {
        try
        {
            if (IntegratedGpuName.Text.Contains("AMD"))
            {
                var (memorySize, driverVersion) = GetSystemInfo.GetRegistryGpuDriverInformation(IntegratedGpuName.Text);
                IntegratedVramSize.Text = memorySize;
                IntegratedDriverVersion.Text = driverVersion;

                _isIntegratedGpuPresent = true;
                if (GpuBannerButton.Visibility == Visibility.Collapsed)
                {
                    GpuBannerButton.Visibility = Visibility.Visible;
                }
            }

            if (_cpu != null)
            {
                var memoryConfig = _cpu.GetMemoryConfig();
                IntegratedVramType.Text = memoryConfig.Type.ToString();
                IntegratedVramWidth.Text = memoryConfig.Modules.Count * 64 + " bit";
            }
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex);
        }
    }

    /// <summary>
    ///     Основной метод отображения характеристик дискретной видеокарты
    /// </summary>
    private void LoadDiscreteGpuInformation()
    {
        if (GpuBannerButton.Visibility == Visibility.Collapsed || _isDiscreteGpuInformationLoaded)
        {
            return;
        }

        if (_sensorsInformation != null)
        {
            DiscreteVramSize.Text = _sensorsInformation.NvidiaVramSize;
            DiscreteVramType.Text = _sensorsInformation.NvidiaVramType;
            DiscreteVramWidth.Text = _sensorsInformation.NvidiaVramWidth;
            DiscreteDriverVersion.Text = _sensorsInformation.NvidiaDriverVersion;

            _isDiscreteGpuInformationLoaded = true;
        }
    }

    /// <summary>
    ///     Основной метод отображения характеристик процессора
    /// </summary>
    private async Task LoadCpuInformation()
    {
        try
        {
            _numberOfLogicalProcessors = Environment.ProcessorCount;
            if (_cpu != null)
            {
                if (_numberOfCores == 0 || _numberOfLogicalProcessors == 0)
                {
                    _numberOfCores = (int)_cpu.info.topology.cores;
                    _numberOfLogicalProcessors = (int)_cpu.info.topology.logicalCores;
                }

                CpuCodename.Text = $"{_cpu.info.codeName}";
                SmuVersion.Text = _cpu.systemInfo?.GetSmuVersionString();
            }
            else
            {
                _numberOfCores = _numberOfLogicalProcessors / 2;
            }

            await InfoCpuSectionGridBuilder();

            var ((name, baseClock), integratedGpuName, discreteGpuName,
                    (l1Cache, l2Cache, l3Cache), instructionsSet, cpuCaption)
                = GetSystemInfo.GetCommonMetrics();

            CpuBaseClock.Text = $"{baseClock} MHz";
            ProcessorName.Text = _cpuName = name.TrimEnd();

            CpuCores.Text = _numberOfLogicalProcessors == _numberOfCores
                ? _numberOfCores.ToString()
                : GetSystemInfo.GetBigLITTLE(_numberOfCores);
            CpuThreads.Text = $"{_numberOfLogicalProcessors:0}";

            IntegratedGpuName.Text = integratedGpuName;
            DiscreteGpuName.Text = discreteGpuName;
            L1Cache.Text = $"{l1Cache:0.##} MB";
            L2Cache.Text = $"{l2Cache:0.##} MB";
            L3Cache.Text = $"{l3Cache:0.##} MB";
            Instructions.Text = instructionsSet;
            Caption.Text = cpuCaption;
        }
        catch (Exception ex)
        {
            await LogHelper.TraceIt_TraceError(ex);
        }
    }

    /// <summary>
    ///     Основной метод отображения характеристик батареи
    /// </summary>
    private void LoadBatteryInformation()
    {
        if (BatBannerButton.Visibility == Visibility.Collapsed || _isBatteryInformationLoaded)
        {
            return;
        }

        if (_sensorsInformation != null)
        {
            if (_sensorsInformation.BatteryUnavailable)
            {
                BatBannerButton.Visibility = Visibility.Collapsed;
                _doNotTrackBattery = true;
            }

            BatteryHealth.Text = _sensorsInformation.BatteryHealth;

            var batteryHealth = 100 - (double)GetSystemInfo.GetBatteryHealth() * 100;
            BatteryHealthBar.Value = batteryHealth;
            if (batteryHealth > 50 && BatteryConditionCaption.Text != BatteryBadHealth)
            {
                BatteryConditionCaption.Text = BatteryBadHealth;
            }

            BatteryCycles.Text = _sensorsInformation.BatteryCycles;
            BatteryCapacity.Text = _sensorsInformation.BatteryCapacity;
            _batName = _sensorsInformation.BatteryName ?? "Unknown";

            _isBatteryInformationLoaded = true;
        }
    }

    /// <summary>
    ///     Основной метод отображения характеристик оперативной памяти
    /// </summary>
    private async Task LoadRamInformation()
    {
        try
        {
            if (_cpu != null)
            {
                var (capacity, type, speed, producer, model, slots,
                        (tcl, trcdwr, trcdrd, tras, trp, trc))
                    = GetSystemInfo.GetMemoryInformation(
                        _cpu.GetMemoryConfig(), _cpu.powerTable?.MCLK,
                        _cpu.ReadDword(0x50200), _cpu.ReadDword(0x50204),
                        _cpu.ReadDword(0x50208));

                _ramName = $"{capacity} GB {type} @ {speed} MT/s";
                RamFrequency.Text = speed + "MT/s";
                RamProducer.Text = producer;
                RamModel.Text = model;
                RamSlots.Text = $"{slots} * 64 bit";
                Tcl.Text = tcl + "T";
                Trcdwr.Text = trcdwr + "T";
                Trcdrd.Text = trcdrd + "T";
                Tras.Text = tras + "T";
                Trp.Text = trp + "T";
                Trc.Text = trc + "T";
            }
        }
        catch (Exception ex)
        {
            await LogHelper.LogError(ex);
        }
    }

    #endregion

    #region P-State voids

    /// <summary>
    ///     Вспомогательный метод для получения Fid/Did
    /// </summary>
    private static void CalculatePstateDetails(uint eax,
        out uint cpuDfsId, out uint cpuFid)
    {
        cpuDfsId = (eax >> 8) & 0x3F;
        cpuFid = eax & 0xFF;
    }

    /// <summary>
    ///     Основной метод чтения P-States
    /// </summary>
    private void ReadPowerStates()
    {
        try
        {
            for (var i = 0; i < 3; i++)
            {
                uint eax = 0, edx = 0;
                var textBlock = i switch
                {
                    0 => PowerState0,
                    1 => PowerState1,
                    _ => PowerState2
                };
                var textBlockDesc = i switch
                {
                    0 => PowerState0Desc,
                    1 => PowerState1Desc,
                    _ => PowerState2Desc
                };

                try
                {
                    if (_cpu?.ReadMsr(Convert.ToUInt32(Convert.ToInt64(0xC0010064) + i), ref eax, ref edx) ==
                        false)
                    {
                        App.MainWindow.ShowMessageDialogAsync("Error while reading CPU Pstate", "Critical Error");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.TraceIt_TraceError(ex);
                }

                CalculatePstateDetails(eax, out var cpuDfsId, out var cpuFid);
                if (cpuFid != 0)
                {
                    textBlock.Text =
                        $"{cpuFid * 25 / (cpuDfsId * 12.5) / 10} {GhzFreq}";
                    textBlockDesc.Text = $"(FID {
                        Convert.ToString(cpuFid, 10)} / DID {
                            Convert.ToString(cpuDfsId, 10)})";
                }
                else
                {
                    textBlock.Text = "Info_PowerSumInfo_DisabledPState".GetLocalized();
                }

                _pstatesList[i] = cpuFid * 25 / (cpuDfsId * 12.5) / 10;
            }
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex);
        }
    }

    #endregion

    #region Page-related voids

    /// <summary>
    ///     Обработчик сворачивания окна
    /// </summary>
    private void Window_VisibilityChanged(object sender, WindowVisibilityChangedEventArgs args)
    {
        if (args.Visible)
        {
            _dispatcherTimer?.Start();
        }
        else
        {
            StopInfoUpdate();
        }
    }

    /// <summary>
    ///     Обработчик захода на страницу
    /// </summary>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        StartInfoUpdate();
    }

    /// <summary>
    ///     Обработчик выхода со страницы
    /// </summary>
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        StopInfoUpdate();
        App.MainWindow.VisibilityChanged -= Window_VisibilityChanged;
    }

    /// <summary>
    ///     Обработчик загрузки страницы
    /// </summary>
    private async void ИнформацияPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _loaded = true;

            _selectedBrush = CpuBannerButton.Background;
            _selectedBorderBrush = CpuBannerButton.BorderBrush;
            _transparentBrush = GpuBannerButton.Background;

            _allBannerButtons =
            [
                CpuBannerButton, GpuBannerButton,
                RamBannerButton, VrmBannerButton,
                BatBannerButton, PstBannerButton
            ];

            _allExpandButtons =
            [
                CpuExpandButton, VrmExpandButton,
                GpuExpandButton, RamExpandButton,
                BatExpandButton, PstExpandButton
            ];

            SetThemeAccentTextForeground();

            await LoadCpuInformation();
            LoadIntegratedGpuInformation();
            await LoadRamInformation();
            ReadPowerStates();

            if (CpuBannerButton.Shadow != new ThemeShadow())
            {
                CpuBannerButton.Shadow ??= new ThemeShadow();
                GpuBannerButton.Shadow = null;
                RamBannerButton.Shadow = null;
                BatBannerButton.Shadow = null;
                PstBannerButton.Shadow = null;
                VrmBannerButton.Shadow = null;
            }

            RtssButton.IsChecked = _appSettings.RtssMetricsEnabled;
            NiIconsButton.IsChecked = _appSettings.NiIconsEnabled;
        }
        catch (Exception ex)
        {
            await LogHelper.TraceIt_TraceError(ex);
        }
    }

    /// <summary>
    ///     Обработчик выгрузки страницы
    /// </summary>
    private void ИнформацияPage_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_dataUpdater != null)
        {
            _dataUpdater.DataUpdated -= OnDataUpdated;
        }

        Unloaded -= ИнформацияPage_Unloaded;
        App.MainWindow.VisibilityChanged -= Window_VisibilityChanged;
        StopInfoUpdate();
    }

    #endregion

    #region Info Update voids

    /// <summary>
    ///     Основной метод автообновления информации
    /// </summary>
    private void UpdateInfo()
    {
        try
        {
            if (!_loaded)
            {
                return;
            }

            // Сначала скрываем всё
            HideAllSections();

            if (_selectedGroup != 0)
            {
                CpuSectionComboBox.Visibility = _selectedGroup != 5 ? Visibility.Collapsed : Visibility.Visible;

                switch (_selectedGroup)
                {
                    case 1: // GPU
                        ConfigureSection(
                            "InfoGPUSectionName".GetLocalized(),
                            string.Empty,
                            gpuVisible: true
                        );
                        break;

                    case 2: // RAM
                        ConfigureSection(
                            "InfoRAMSectionName".GetLocalized(),
                            _ramName,
                            ramVisible: true,
                            ramMainVisible: true
                        );
                        break;

                    case 3: // VRM
                        ConfigureSection(
                            "VRM",
                            _cpuName,
                            vrmVisible: true,
                            cpuMainVisible: true
                        );
                        break;

                    case 4: // Battery
                        ConfigureSection(
                            "InfoBatteryName".GetLocalized(),
                            _batName,
                            batVisible: true
                        );
                        break;

                    case 5: // P-States
                        ConfigureSection(
                            "P-States",
                            _cpuName,
                            pstVisible: true,
                            cpuMainVisible: true
                        );
                        break;
                }
            }
            else
            {
                // CPU по умолчанию
                HideAllSections();

                PowersSection.Visibility = Visibility.Visible;

                CpuDetails.Visibility = Visibility.Visible;
                CpuCommonDetails.Visibility = Visibility.Visible;
                CpuSectionComboBox.Visibility = Visibility.Visible;

                CpuSectionName.Text = "InfoCPUSectionName".GetLocalized();
                ProcessorName.Text = _cpuName;
            }

            RemoveMaxPoints();

            if (_sensorsInformation == null)
            {
                return;
            }

            if (!_isIntegratedGpuPresent && !_sensorsInformation.IsNvidiaGpuAvailable)
            {
                GpuBannerButton.Visibility = Visibility.Collapsed;
            }

            LoadBatteryInformation();
            LoadDiscreteGpuInformation();

            var currBatRate = 0d;
            var previousMaxBatRate = 0;
            if (!_doNotTrackBattery)
            {
                BatteryChargeRate.Text = BatUsageBigBannerPolygonText.Text =
                    BatUsageBannerPolygonText.Text = $"{_sensorsInformation.BatteryChargeRate:0.##}W";
                var chargeRate = Math.Abs(_sensorsInformation.BatteryChargeRate);
                SetBarMaxValueHelper(BatteryChargeRateBar, chargeRate);

                BatteryPercent.Text = _sensorsInformation.BatteryPercent;
                var batteryPercent = (double)GetSystemInfo.GetBatteryPercent();
                BatteryPercentBar.Value = batteryPercent;
                if (batteryPercent > 80 && BatteryHighChargeLevel.Text != HighChargeLevel)
                {
                    BatteryHighChargeLevel.Text = HighChargeLevel;
                    BatteryHighChargeLevelDesc.Text = "80-85%";
                }
                else if (batteryPercent <= 20 && BatteryHighChargeLevel.Text != LowChargeLevel)
                {
                    BatteryHighChargeLevel.Text = LowChargeLevel;
                    BatteryHighChargeLevelDesc.Text = "20%";
                }
                else if (batteryPercent is > 20 and <= 80 && BatteryHighChargeLevel.Text != NormalChargeLevel)
                {
                    BatteryHighChargeLevel.Text = NormalChargeLevel;
                    BatteryHighChargeLevelDesc.Text = string.Empty;
                }

                BatteryTime.Text = _sensorsInformation.BatteryLifeTime < 0
                    ? BatFromWall
                    : GetSystemInfo.ConvertBatteryLifeTime(_sensorsInformation.BatteryLifeTime);
                BatteryUsageBigBanner.Text = BatteryUsage.Text =
                    BatteryPercent.Text + " " + BatteryChargeRate.Text + "\n" + BatteryTime.Text;

                currBatRate = Math.Abs(_sensorsInformation.BatteryChargeRate);
                previousMaxBatRate = (int)_maxBatRate;
                if (currBatRate > _maxBatRate)
                {
                    _maxBatRate = currBatRate;
                }
            }

            if (_sensorsInformation.CpuStapmLimit == 0)
            {
                StapmPowerLimit.Text = PowerDisabled;
                StapmLimitBar.ShowError = true;
                StapmLimitBar.IsIndeterminate = true;
            }
            else
            {
                StapmPowerLimit.Text =
                    $"{_sensorsInformation.CpuStapmValue:0.###}W/{_sensorsInformation.CpuStapmLimit:0}W";
                SetBarMaxValueHelper(StapmLimitBar, _sensorsInformation.CpuStapmValue, false,
                    _sensorsInformation.CpuStapmLimit);
            }

            ActualPowerLimitText.Text =
                $"{_sensorsInformation.CpuFastValue:0.###}W/{_sensorsInformation.CpuFastLimit:0}W";
            ActualLimitBar.Value = _sensorsInformation.CpuFastValue;
            SetBarMaxValueHelper(ActualLimitBar, _sensorsInformation.CpuFastValue, false,
                _sensorsInformation.CpuFastLimit);

            AveragePowerLimit.Text = _sensorsInformation.CpuSlowLimit == 0
                ? PowerDisabled
                : $"{_sensorsInformation.CpuSlowValue:0.###}W/{_sensorsInformation.CpuSlowLimit:0}W";
            SetBarMaxValueHelper(AverageLimitBar, _sensorsInformation.CpuSlowValue, false,
                _sensorsInformation.CpuSlowLimit);

            FastFrequencyRiseTime.Text = $"{_sensorsInformation.CpuSlowTimeValue:0.###}s";
            SlowFrequencyRiseTime.Text = $"{_sensorsInformation.CpuStapmTimeValue:0.###}s";

            UpdateVrmTimingsDisplay(_sensorsInformation.CpuSlowTimeValue, _sensorsInformation.CpuStapmTimeValue);

            IntegratedGpuPowerLimit.Text =
                $"{_sensorsInformation.ApuSlowValue:0.###}W/{_sensorsInformation.ApuSlowLimit:0}W";

            VrmTdcCurrent.Text = $"{_sensorsInformation.VrmTdcValue:0.###}A/{_sensorsInformation.VrmTdcLimit:0}A";
            SocTdcCurrent.Text = $"{_sensorsInformation.SocTdcValue:0.###}A/{_sensorsInformation.SocTdcLimit:0}A";
            VrmEdcCurrent.Text = VrmEdcCurrent1.Text =
                $"{_sensorsInformation.VrmEdcValue:0.###}A/{_sensorsInformation.VrmEdcLimit:0}A";
            SetBarMaxValueHelper(VrmEdcBar, _sensorsInformation.VrmEdcValue, true, _sensorsInformation.VrmEdcLimit);
            SetBarMaxValueHelper(VrmTdcBar, _sensorsInformation.VrmTdcValue, true, _sensorsInformation.VrmTdcLimit);

            VrmUsageBigBanner.Text = VrmUsageBanner.Text =
                $"{_sensorsInformation.VrmEdcValue:0.###}A\n{_sensorsInformation.CpuFastValue:0.###}W";
            VrmUsageBigBannerPolygonText.Text =
                VrmUsageBannerPolygonText.Text = $"{_sensorsInformation.VrmEdcValue:0.###}A";
            SocEdcCurrent.Text = $"{_sensorsInformation.SocEdcValue:0.###}A/{_sensorsInformation.SocEdcLimit:0}A";
            SocVoltage.Text = $"{_sensorsInformation.SocVoltage:0.###}V";
            SetBarMaxValueHelper(SocVoltageBar, _sensorsInformation.SocVoltage);
            SetBarMaxValueHelper(SocEdcBar, _sensorsInformation.SocEdcValue, true, _sensorsInformation.SocEdcLimit);
            SetBarMaxValueHelper(SocTdcBar, _sensorsInformation.SocTdcValue, true, _sensorsInformation.SocTdcLimit);

            SocPower.Text = $"{_sensorsInformation.SocPower:0.###}W";
            MemoryClock.Text = $"{_sensorsInformation.MemFrequency:0.###}{MhzFreq}";
            InfinityFabricClock.Text = $"{_sensorsInformation.FabricFrequency:0.###}{MhzFreq}";
            SetBarMaxValueHelper(SocPowerBar, _sensorsInformation.SocPower);
            SetBarMaxValueHelper(MemoryFrequencyBar, _sensorsInformation.MemFrequency);
            SetBarMaxValueHelper(InfinityFabricBar, _sensorsInformation.FabricFrequency);

            // Инициализация переменных для накопления данных
            var totalFrequency = 0d;
            var frequencyCount = 0;
            var totalVoltage = 0d;
            var voltageCount = 0;
            var maxFrequency = 0d;
            var currentPstate = 4;

            var selectedSection = CpuSectionComboBox.SelectedIndex;

            // Обработка данных по каждому ядру
            for (uint coreIndex = 0; coreIndex < MaxCores; coreIndex++)
            {
                // Получение частоты текущего ядра
                var coreFrequency = GetCoreValue(_sensorsInformation.CpuFrequencyPerCore, coreIndex);

                // Отслеживание максимальной частоты
                if (!double.IsNaN(coreFrequency) && coreFrequency > maxFrequency)
                {
                    maxFrequency = coreFrequency;
                }

                // Получение значения в зависимости от выбранной секции
                var currentValue = selectedSection switch
                {
                    SectionFrequency => coreFrequency,
                    SectionVoltage => GetCoreValue(_sensorsInformation.CpuVoltagePerCore, coreIndex),
                    SectionPower => GetCoreValue(_sensorsInformation.CpuPowerPerCore, coreIndex),
                    SectionTemperature => GetCoreValue(_sensorsInformation.CpuTemperaturePerCore, coreIndex),
                    _ => coreFrequency
                };

                // Обновление UI, если значение валидно
                if (!double.IsNaN(currentValue))
                {
                    UpdateButtonText(coreIndex, currentValue, selectedSection);

                    // Накопление данных для средних значений (только для реальных ядер)
                    if (coreIndex < _numberOfCores && IsValidFrequency(coreFrequency))
                    {
                        totalFrequency += coreFrequency;
                        frequencyCount++;
                    }
                }

                // Накопление данных по напряжению
                var coreVoltage = GetCoreValue(_sensorsInformation.CpuVoltagePerCore, coreIndex);
                if (IsValidVoltage(coreVoltage))
                {
                    totalVoltage += coreVoltage;
                    voltageCount++;
                }
            }

            // Обновление средней частоты и P-state
            if (frequencyCount > 0)
            {
                var avgFrequency = Math.Round(totalFrequency / frequencyCount, 3);
                CpuFrequency.Text = $"{avgFrequency} {GhzFreq}";
                SetBarMaxValueHelper(CpuFrequencyBar, avgFrequency);

                currentPstate = DeterminePState(avgFrequency);
                PowerStateBar.Value = currentPstate;
                UpdatePStateUi(currentPstate);
            }
            else
            {
                CpuFrequency.Text = $"? {GhzFreq}";
            }

            // Обновление среднего напряжения
            if (voltageCount > 0)
            {
                var avgVoltage = totalVoltage / voltageCount;
                CpuVoltage.Text = $"{avgVoltage:0.###}V";
                SetBarMaxValueHelper(CpuVoltageBar, avgVoltage);
            }
            else
            {
                HideVoltagePanel();
            }

            // Используем уже готовые значения с форматами
            var gfxClk = _sensorsInformation.ApuFrequency / 1000.0;
            var gfxVolt = _sensorsInformation.ApuVoltage;
            var gfxTemp = _sensorsInformation.ApuTempValue;

            // обновление максимума
            var beforeMaxGfxClk = _maxGfxClock;
            if (_maxGfxClock < gfxClk)
            {
                _maxGfxClock = gfxClk;
            }

            // GPU баннеры
            GpuUsageBanner.Text = GpuUsageBigBanner.Text =
                _sensorsInformation.IsNvidiaGpuAvailable
                    ? $"{_sensorsInformation.NvidiaGpuUsage:0.#}% {_sensorsInformation.NvidiaGpuFrequency:0.###} {GhzFreq}" +
                      $"\n{_sensorsInformation.NvidiaGpuTemperature:0.#}C"
                    : $"{gfxClk:0.###} {GhzFreq}  {gfxTemp:0}C\n{gfxVolt:0.###}V";

            GpuUsageBigBannerPolygonText.Text = GpuUsageBannerPolygonText.Text =
                _sensorsInformation.IsNvidiaGpuAvailable
                    ? $"{_sensorsInformation.NvidiaGpuUsage:0.#}%"
                    : $"{gfxClk:0.###} {GhzFreq}";

            GpuFrequency.Text = $"{gfxClk:0.###} {GhzFreq}";
            SetBarMaxValueHelper(IntegratedGpuFrequencyBar, gfxClk);
            SetBarMaxValueHelper(IntegratedGpuPowerLimitBar, _sensorsInformation.ApuSlowValue, false,
                _sensorsInformation.ApuSlowLimit);

            GpuVoltage.Text = $"{gfxVolt:0.###}V";
            SetBarMaxValueHelper(IntegratedGpuVoltageBar, gfxVolt);

            // CPU max temp
            var maxTemp = _sensorsInformation.CpuTempLimit;
            CpuMaxTempLimit.Text =
                $"{_sensorsInformation.CpuTempValue:0.###}C/{maxTemp:0.###}C";
            SetBarMaxValueHelper(CpuTemperatureBar, _sensorsInformation.CpuTempValue, false, maxTemp);

            // APU temp
            var apuTemp = _sensorsInformation.ApuTempValue;
            var apuTempLimit = _sensorsInformation.ApuTempLimit;
            var apuRealTemp = !double.IsNaN(apuTemp) && apuTemp > 0 ? apuTemp : gfxTemp;
            var apuRealTempLimit = !double.IsNaN(apuTempLimit) && apuTempLimit > 0 ? apuTempLimit : maxTemp;
            IntegratedGpuMaxTempLimit.Text =
                $"{apuRealTemp:0.###}C/" +
                $"{apuRealTempLimit:0.###}C";
            SetBarMaxValueHelper(IntegratedGpuTemperatureBar, apuRealTemp, false, apuRealTempLimit);

            DiscreteGpuFrequency.Text = $"{_sensorsInformation.NvidiaGpuFrequency:0.###} {GhzFreq}";
            DiscreteGpuTemperature.Text = $"{_sensorsInformation.NvidiaGpuTemperature:0.###}C";
            DiscreteGpuUsage.Text = $"{_sensorsInformation.NvidiaGpuUsage:0.###}%";
            DiscreteGpuVramFrequency.Text = $"{_sensorsInformation.NvidiaVramFrequency:0.###} {GhzFreq}";
            SetBarMaxValueHelper(DiscreteGpuFrequencyBar, _sensorsInformation.NvidiaGpuFrequency);
            SetBarMaxValueHelper(DiscreteGpuTemperatureBar, _sensorsInformation.NvidiaGpuTemperature);
            SetBarMaxValueHelper(DiscreteGpuUsageBar, _sensorsInformation.NvidiaGpuUsage);
            SetBarMaxValueHelper(DiscreteGpuVramFrequencyBar, _sensorsInformation.NvidiaVramFrequency);

            // CPU usage
            var cpuUsage = _sensorsInformation.CpuUsage;
            var cpuUsageText = $"{cpuUsage:0.#}%";
            CpuUsage.Text = CpuUsageBigBannerPolygonText.Text =
                CpuUsageBannerPolygonText.Text = cpuUsageText;
            CpuUsageBar.Value = cpuUsage;
            CpuUsageBanner.Text = CpuUsageBigBanner.Text =
                cpuUsageText + $"  {CpuFrequency.Text}\n{(CpuVoltage.Text != "?V" ? CpuVoltage.Text : string.Empty)}";


            //InfoACPUBanner График
            CpuBannerPolygon.Points.Remove(ZeroPoint);
            var newCpuPoint = new Point(60,
                48 - (int)(cpuUsage * 0.48));
            _cpuPointer.Add(newCpuPoint);
            if (CpuFlyout.IsOpen)
            {
                CpuBigBannerPolygon.Points.Remove(ZeroPoint);
                CpuBigBannerPolygon.Points.Add(newCpuPoint);
            }

            CpuBannerPolygon.Points.Add(newCpuPoint);
            for (var e = 0; e < _cpuPointer.Count; e++)
            {
                var element = _cpuPointer[e];
                if (element.X < 0)
                {
                    _cpuPointer.Remove(element);
                    if (CpuFlyout.IsOpen)
                    {
                        CpuBigBannerPolygon.Points.Remove(element);
                    }

                    CpuBannerPolygon.Points.Remove(element);
                }
                else
                {
                    if (CpuFlyout.IsOpen)
                    {
                        CpuBigBannerPolygon.Points.Remove(element);
                    }

                    CpuBannerPolygon.Points.Remove(element);
                    element.X -= 1;
                    CpuBannerPolygon.Points.Add(element);
                    if (CpuFlyout.IsOpen)
                    {
                        CpuBigBannerPolygon.Points.Add(element);
                    }

                    _cpuPointer[e] = element;
                }
            }

            if (CpuFlyout.IsOpen)
            {
                CpuBigBannerPolygon.Points.Add(MaxedPoint);
            }

            CpuBannerPolygon.Points.Add(MaxedPoint);


            //InfoAGPUBanner График
            GpuBannerPolygon.Points.Remove(ZeroPoint);
            var graphicsLoadFrequency = _sensorsInformation.IsNvidiaGpuAvailable
                ? _sensorsInformation.NvidiaGpuUsage / 100
                : gfxClk / _maxGfxClock;
            var newGpuPoint = new Point(60,
                48 - (int)(graphicsLoadFrequency * 48));
            _gpuPointer.Add(newGpuPoint);
            GpuBannerPolygon.Points.Add(newGpuPoint);
            if (GpuFlyout.IsOpen)
            {
                GpuBigBannerPolygon.Points.Remove(ZeroPoint);
                GpuBigBannerPolygon.Points.Add(newGpuPoint);
            }

            for (var e = 0; e < _gpuPointer.Count; e++)
            {
                var element = _gpuPointer[e];
                if (element.X < 0)
                {
                    _gpuPointer.Remove(element);
                    GpuBannerPolygon.Points.Remove(element);
                    if (GpuFlyout.IsOpen)
                    {
                        GpuBigBannerPolygon.Points.Remove(element);
                    }
                }
                else
                {
                    GpuBannerPolygon.Points.Remove(element);
                    if (GpuFlyout.IsOpen)
                    {
                        GpuBigBannerPolygon.Points.Remove(element);
                    }

                    var scalarFactor = _sensorsInformation.IsNvidiaGpuAvailable ? 1d : beforeMaxGfxClk / _maxGfxClock;
                    element.X -= 1;
                    element.Y = (int)(element.Y * scalarFactor);
                    if (GpuFlyout.IsOpen)
                    {
                        GpuBigBannerPolygon.Points.Add(element);
                    }

                    GpuBannerPolygon.Points.Add(element);
                    _gpuPointer[e] = element;
                }
            }

            if (GpuFlyout.IsOpen)
            {
                GpuBigBannerPolygon.Points.Add(MaxedPoint);
            }

            GpuBannerPolygon.Points.Add(MaxedPoint);

            //InfoAVRMBanner График
            VrmBannerPolygon.Points.Remove(ZeroPoint);
            var newVrmPoint = new Point(60,
                48 - (int)(_sensorsInformation.VrmEdcValue /
                    _sensorsInformation.VrmEdcLimit * 48));
            _vrmPointer.Add(newVrmPoint);
            VrmBannerPolygon.Points.Add(newVrmPoint);
            if (VrmFlyout.IsOpen)
            {
                VrmBigBannerPolygon.Points.Remove(ZeroPoint);
                VrmBigBannerPolygon.Points.Add(newVrmPoint);
            }

            for (var e = 0; e < _vrmPointer.Count; e++)
            {
                var element = _vrmPointer[e];
                if (element.X < 0)
                {
                    _vrmPointer.Remove(element);
                    VrmBannerPolygon.Points.Remove(element);
                    if (VrmFlyout.IsOpen)
                    {
                        VrmBigBannerPolygon.Points.Remove(element);
                    }
                }
                else
                {
                    if (VrmFlyout.IsOpen)
                    {
                        VrmBigBannerPolygon.Points.Remove(element);
                    }

                    VrmBannerPolygon.Points.Remove(element);
                    element.X -= 1;
                    if (VrmFlyout.IsOpen)
                    {
                        VrmBigBannerPolygon.Points.Add(element);
                    }

                    VrmBannerPolygon.Points.Add(element);
                    _vrmPointer[e] = element;
                }
            }

            VrmBannerPolygon.Points.Add(MaxedPoint);
            if (VrmFlyout.IsOpen)
            {
                VrmBigBannerPolygon.Points.Add(MaxedPoint);
            }

            //InfoAPSTBanner График
            PstBannerPolygon.Points.Remove(ZeroPoint);
            var newPowerStatePoint = new Point(60, 48 - currentPstate * 16);
            _pstPointer.Add(newPowerStatePoint);
            PstBannerPolygon.Points.Add(newPowerStatePoint);
            if (PstFlyout.IsOpen)
            {
                PstBigBannerPolygon.Points.Remove(ZeroPoint);
                PstBigBannerPolygon.Points.Add(newPowerStatePoint);
            }

            for (var e = 0; e < _pstPointer.Count; e++)
            {
                var element = _pstPointer[e];
                if (element.X < 0)
                {
                    _pstPointer.Remove(element);
                    PstBannerPolygon.Points.Remove(element);
                    if (PstFlyout.IsOpen)
                    {
                        PstBigBannerPolygon.Points.Remove(element);
                    }
                }
                else
                {
                    PstBannerPolygon.Points.Remove(element);
                    if (PstFlyout.IsOpen)
                    {
                        PstBigBannerPolygon.Points.Remove(element);
                    }

                    element.X -= 1;
                    PstBannerPolygon.Points.Add(element);
                    if (PstFlyout.IsOpen)
                    {
                        PstBigBannerPolygon.Points.Add(element);
                    }

                    _pstPointer[e] = element;
                }
            }

            if (PstFlyout.IsOpen)
            {
                PstBigBannerPolygon.Points.Add(MaxedPoint);
            }

            PstBannerPolygon.Points.Add(MaxedPoint);

            //InfoABATBanner График
            BatBannerPolygon.Points.Remove(ZeroPoint);
            var newBatteryPoint = new Point(60,
                48 - (int)(Math.Abs(currBatRate) / _maxBatRate * 48));
            _batPointer.Add(newBatteryPoint);
            BatBannerPolygon.Points.Add(newBatteryPoint);
            if (BatFlyout.IsOpen)
            {
                BatBigBannerPolygon.Points.Remove(ZeroPoint);
                BatBigBannerPolygon.Points.Add(newBatteryPoint);
            }

            for (var e = 0; e < _batPointer.Count; e++)
            {
                var element = _batPointer[e];
                if (element.X < 0)
                {
                    _batPointer.Remove(element);
                    BatBannerPolygon.Points.Remove(element);
                    if (BatFlyout.IsOpen)
                    {
                        BatBigBannerPolygon.Points.Remove(element);
                    }
                }
                else
                {
                    BatBannerPolygon.Points.Remove(element);
                    if (BatFlyout.IsOpen)
                    {
                        BatBigBannerPolygon.Points.Remove(element);
                    }

                    element.X -= 1;
                    element.Y = (int)(element.Y * previousMaxBatRate / _maxBatRate);
                    BatBannerPolygon.Points.Add(element);
                    if (BatFlyout.IsOpen)
                    {
                        BatBigBannerPolygon.Points.Add(element);
                    }

                    _batPointer[e] = element;
                }
            }

            if (BatFlyout.IsOpen)
            {
                BatBigBannerPolygon.Points.Add(MaxedPoint);
            }

            BatBannerPolygon.Points.Add(MaxedPoint);
            try
            {
                var busyRam = _busyRam;
                var usageResult = _totalRam;
                if (busyRam != 0 && usageResult != 0)
                {
                    RamBannerPolygon.Points.Remove(ZeroPoint);
                    var newRamPoint = new Point(60,
                        48 - (int)(busyRam * 100 / usageResult * 0.48));
                    _ramPointer.Add(newRamPoint);
                    RamBannerPolygon.Points.Add(newRamPoint);
                    if (RamFlyout.IsOpen)
                    {
                        RamBigBannerPolygon.Points.Remove(ZeroPoint);
                        RamBigBannerPolygon.Points.Add(newRamPoint);
                    }
                }

                for (var e = 0; e < _ramPointer.Count; e++)
                {
                    var element = _ramPointer[e];
                    if (element.X < 0)
                    {
                        _ramPointer.Remove(element);
                        RamBannerPolygon.Points.Remove(element);
                        if (RamFlyout.IsOpen)
                        {
                            RamBigBannerPolygon.Points.Remove(
                                element);
                        }
                    }
                    else
                    {
                        if (RamFlyout.IsOpen)
                        {
                            RamBigBannerPolygon.Points.Remove(
                                element);
                        }

                        RamBannerPolygon.Points.Remove(element);
                        element.X -= 1;
                        RamBannerPolygon.Points.Add(element);
                        if (RamFlyout.IsOpen)
                        {
                            RamBigBannerPolygon.Points.Add(element);
                        }

                        _ramPointer[e] = element;
                    }
                }

                if (RamFlyout.IsOpen)
                {
                    RamBigBannerPolygon.Points.Add(MaxedPoint);
                }

                RamBannerPolygon.Points.Add(MaxedPoint);
            }
            catch (Exception ex)
            {
                LogHelper.TraceIt_TraceError(ex);
            }

            RamUsage.Text = _sensorsInformation.RamUsage;
            RamUsageBannerPolygonText.Text = _sensorsInformation.RamUsagePercent + "%";
            RamUsageBigBannerPolygonText.Text = RamUsageBannerPolygonText.Text;
            RamUsageBigBanner.Text = RamUsage.Text;
            //InfoARAMBanner График
            try
            {
                _busyRam = Convert.ToDouble(_sensorsInformation?.RamBusy?.Replace("GB", string.Empty));
                _totalRam = Convert.ToDouble(_sensorsInformation?.RamTotal?.Replace("GB", string.Empty));
            }
            catch
            {
                _busyRam = 0;
                _totalRam = 0;
            }
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex);
        }
    }

    #region Update Helpers

    /// <summary>
    ///     Обновляет подпись для таймингов VRM
    /// </summary>
    private void UpdateVrmTimingsDisplay(double slow, double fast)
    {
        if (_vrmTimingsDetected || _vrmTimingIteration == 2)
        {
            return;
        }

        _vrmTimingIteration += 1;

        // Оба тайминга = 0, не обнаружены
        if (slow == 0 && fast == 0)
        {
            VrmTimingsSign.Text = TimingsNotFoundText;
            SlowTimePanel.Visibility = Visibility.Collapsed;
            FastTimePanel.Visibility = Visibility.Collapsed;
            _vrmTimingsDetected = true;
            return;
        }

        if (_prevSlow != 0 || _prevFast != 0)
        {
            if ((int)(slow * 1000) == (int)(_prevSlow * 1000)
                && (int)(fast * 1000) == (int)(_prevFast * 1000))
            {
                // Значения не менялись, статические
                VrmTimingsSign.Text = StaticTimingsText;
                _vrmTimingsDetected = true;
                return;
            }
        }

        _prevSlow = slow;
        _prevFast = fast;
    }

    /// <summary>
    ///     Помощник обновления текущего и максимального значения ProgressBar
    /// </summary>
    private static void SetBarMaxValueHelper(ProgressBar progressBar, double value, bool higher = true,
        double? maxValue = null)
    {
        progressBar.Value = value;

        var setMaxValue = maxValue ?? value;
        if (higher)
        {
            if (progressBar.Maximum < setMaxValue && !double.IsNaN(setMaxValue))
            {
                progressBar.Maximum = setMaxValue;
            }
        }
        else
        {
            if ((int)progressBar.Maximum != (int)setMaxValue && !double.IsNaN(setMaxValue))
            {
                progressBar.Maximum = setMaxValue;
            }
        }
    }

    /// <summary>
    ///     Безопасное получение значения из массива
    /// </summary>
    private static double GetCoreValue(double[]? array, uint index) =>
        array != null && array.Length > index ? array[index] : 0f;

    /// <summary>
    ///     Проверка валидности частоты
    /// </summary>
    private static bool IsValidFrequency(double frequency) => frequency is > 0 and < MaxValidFrequency;

    /// <summary>
    ///     Проверка валидности напряжения
    /// </summary>
    private static bool IsValidVoltage(double voltage) =>
        !double.IsNaN(voltage) && voltage is > 0 and < MaxValidVoltage;

    /// <summary>
    ///     Обновление отображаемого в интерфейсе значения для конкретного индекса
    /// </summary>
    private void UpdateButtonText(uint index, double value, int section)
    {
        var textBlock = (TextBlock)MainSensorsGrid.FindName($"FreqButtonText_{index}");
        if (textBlock == null)
        {
            return;
        }

        textBlock.Text = _selectedGroup is GroupCpu or GroupCpuPst
            ? FormatCoreValue(value, section)
            : GetGroupSpecificText(index);
    }

    /// <summary>
    ///     Форматирование значения в зависимости от секции
    /// </summary>
    private static string FormatCoreValue(double value, int section)
    {
        return section switch
        {
            SectionFrequency => $"{value:0.###} {GhzFreq}",
            SectionVoltage => $"{value:0.###}V",
            SectionPower => $"{value:0.###}W",
            SectionTemperature => $"{value:0.###}C",
            _ => $"{value:0.###} {GhzFreq}"
        };
    }


    /// <summary>
    ///     Получение текста для специфичных групп (GPU, RAM, VRM, Battery)
    /// </summary>
    private string GetGroupSpecificText(uint index)
    {
        return _selectedGroup switch
        {
            GroupGpu => GetGpuText(index),
            GroupRam => GetRamText(index),
            GroupVrm => GetVrmText(index),
            GroupBattery => _batName,
            _ => "Unknown"
        };
    }

    /// <summary>
    ///     Получение текста для GPU
    /// </summary>
    private static string GetGpuText(uint index)
    {
        var gpuNames = GetSystemInfo.GetGpuNames();
        return index < gpuNames.Count ? gpuNames[(int)index] : "Unknown";
    }

    /// <summary>
    ///     Получение текста для RAM
    /// </summary>
    private string GetRamText(uint index)
    {
        var ramModels = _cpu?.GetMemoryConfig().Modules;
        if (ramModels != null)
        {
            return index < ramModels.Count ? ramModels[(int)index].Capacity.ToString() : "Unknown";
        }

        return "Unknown";
    }

    /// <summary>
    ///     Получение текста для VRM
    /// </summary>
    private string GetVrmText(uint index)
    {
        return index switch
        {
            0 => FormatCurrentLimit(_sensorsInformation?.VrmEdcValue, _sensorsInformation?.VrmEdcLimit),
            1 => FormatCurrentLimit(_sensorsInformation?.VrmTdcValue, _sensorsInformation?.VrmTdcLimit),
            2 => FormatCurrentLimit(_sensorsInformation?.SocEdcValue, _sensorsInformation?.SocEdcLimit),
            3 => FormatCurrentLimit(_sensorsInformation?.SocTdcValue, _sensorsInformation?.SocTdcLimit),
            _ => "0A"
        };
    }

    /// <summary>
    ///     Форматирование значений тока VRM
    /// </summary>
    private static string FormatCurrentLimit(double? value, double? limit) => $"{value:0.###}A/{limit:0.###}A";

    // Определение текущего P-state на основе частоты
    private int DeterminePState(double frequency)
    {
        if (frequency >= _pstatesList[0])
        {
            return 3; // P0
        }

        if (frequency >= _pstatesList[1])
        {
            return 2; // P1
        }

        if (frequency >= _pstatesList[2])
        {
            return 1; // P2
        }

        return 0; // C1
    }

    /// <summary>
    ///     Обновление UI элементов P-state
    /// </summary>
    private void UpdatePStateUi(int pstate)
    {
        var pstateText = pstate switch
        {
            3 => "P0",
            2 => "P1",
            1 => "P2",
            _ => "C1"
        };

        PowerState.Text = PstUsageBannerPolygonText.Text = PstUsageBigBannerPolygonText.Text = pstateText;
        PstUsage.Text = PstUsageBigBanner.Text = pstateText + Pstate;
    }

    /// <summary>
    ///     Скрытие максимальных точек графика перед обновлением
    /// </summary>
    private void RemoveMaxPoints()
    {
        CpuBannerPolygon.Points.Remove(MaxedPoint);
        CpuBigBannerPolygon.Points.Remove(MaxedPoint);
        GpuBannerPolygon.Points.Remove(MaxedPoint);
        GpuBigBannerPolygon.Points.Remove(MaxedPoint);
        RamBannerPolygon.Points.Remove(MaxedPoint);
        RamBigBannerPolygon.Points.Remove(MaxedPoint);
        VrmBannerPolygon.Points.Remove(MaxedPoint);
        VrmBigBannerPolygon.Points.Remove(MaxedPoint);
        BatBannerPolygon.Points.Remove(MaxedPoint);
        BatBigBannerPolygon.Points.Remove(MaxedPoint);
        PstBannerPolygon.Points.Remove(MaxedPoint);
        PstBigBannerPolygon.Points.Remove(MaxedPoint);
    }

    /// <summary>
    ///     Скрытие панели напряжения
    /// </summary>
    private void HideVoltagePanel()
    {
        CpuVoltagePanel.Visibility = Visibility.Collapsed;
        CpuVoltage.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    ///     Вспомогательный метод для скрытия всех секций сенсоров
    /// </summary>
    private void HideAllSections()
    {
        CpuDetails.Visibility = Visibility.Collapsed;
        IntegratedGpuSectionMetrics.Visibility = Visibility.Collapsed;
        DiscreteGpuSectionMetrics.Visibility = Visibility.Collapsed;
        RamDetails.Visibility = Visibility.Collapsed;
        PowersSection.Visibility = Visibility.Collapsed;
        CurrentsSection.Visibility = Visibility.Collapsed;
        VrmDetails.Visibility = Visibility.Collapsed;
        BatteryStateSection.Visibility = Visibility.Collapsed;
        BatteryDetails.Visibility = Visibility.Collapsed;
        BatterySuggestionDetails.Visibility = Visibility.Collapsed;
        PowerStatesSection.Visibility = Visibility.Collapsed;
        RamFrequencyDetails.Visibility = Visibility.Collapsed;
        TimingsSection.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    ///     Вспомогательный метод для отображения или скрытия секций сенсоров
    /// </summary>
    private void ConfigureSection(
        string sectionName,
        string processorText,
        bool cpuVisible = false,
        bool gpuVisible = false,
        bool ramVisible = false,
        bool vrmVisible = false,
        bool batVisible = false,
        bool pstVisible = false,
        bool cpuMainVisible = false,
        bool ramMainVisible = false
    )
    {
        CpuSectionName.Text = sectionName;
        ProcessorName.Text = processorText;

        PowersSection.Visibility = cpuVisible ? Visibility.Visible : Visibility.Collapsed;
        CpuDetails.Visibility = cpuVisible ? Visibility.Visible : Visibility.Collapsed;
        IntegratedGpuSectionMetrics.Visibility =
            gpuVisible && _isIntegratedGpuPresent ? Visibility.Visible : Visibility.Collapsed;
        DiscreteGpuSectionMetrics.Visibility = gpuVisible && (_sensorsInformation?.IsNvidiaGpuAvailable ?? false)
            ? Visibility.Visible
            : Visibility.Collapsed;
        RamDetails.Visibility = ramVisible ? Visibility.Visible : Visibility.Collapsed;
        TimingsSection.Visibility = ramVisible ? Visibility.Visible : Visibility.Collapsed;
        CurrentsSection.Visibility = vrmVisible ? Visibility.Visible : Visibility.Collapsed;
        VrmDetails.Visibility = vrmVisible ? Visibility.Visible : Visibility.Collapsed;
        BatteryStateSection.Visibility = batVisible ? Visibility.Visible : Visibility.Collapsed;
        BatteryDetails.Visibility = batVisible ? Visibility.Visible : Visibility.Collapsed;
        BatterySuggestionDetails.Visibility = batVisible ? Visibility.Visible : Visibility.Collapsed;
        PowerStatesSection.Visibility = pstVisible ? Visibility.Visible : Visibility.Collapsed;
        CpuCommonDetails.Visibility = cpuMainVisible ? Visibility.Visible : Visibility.Collapsed;
        RamFrequencyDetails.Visibility = ramMainVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    ///     Начать автообновление информации
    /// </summary>
    private void StartInfoUpdate()
    {
        try
        {
            CpuBannerPolygon.Points.Clear();
            CpuBannerPolygon.Points.Add(StartPoint);
            CpuBigBannerPolygon.Points.Clear();
            CpuBigBannerPolygon.Points.Add(StartPoint);
            GpuBannerPolygon.Points.Clear();
            GpuBannerPolygon.Points.Add(StartPoint);
            GpuBigBannerPolygon.Points.Clear();
            GpuBigBannerPolygon.Points.Add(StartPoint);
            RamBannerPolygon.Points.Clear();
            RamBannerPolygon.Points.Add(StartPoint);
            RamBigBannerPolygon.Points.Clear();
            RamBigBannerPolygon.Points.Add(StartPoint);
            VrmBannerPolygon.Points.Clear();
            VrmBannerPolygon.Points.Add(StartPoint);
            VrmBigBannerPolygon.Points.Clear();
            VrmBigBannerPolygon.Points.Add(StartPoint);
            BatBannerPolygon.Points.Clear();
            BatBannerPolygon.Points.Add(StartPoint);
            BatBigBannerPolygon.Points.Clear();
            BatBigBannerPolygon.Points.Add(StartPoint);
            PstBannerPolygon.Points.Clear();
            PstBannerPolygon.Points.Add(StartPoint);
            PstBigBannerPolygon.Points.Clear();
            PstBigBannerPolygon.Points.Add(StartPoint);

            _dispatcherTimer = new DispatcherTimer();
            _dispatcherTimer.Tick += (_, _) => UpdateInfo();
            _dispatcherTimer.Interval = TimeSpan.FromMilliseconds(300);

            App.MainWindow.VisibilityChanged += Window_VisibilityChanged;
            _dispatcherTimer.Start();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex);
        }
    }

    /// <summary>
    ///     Остановка автообновления информации. Вызываеться при скрытии/переключении страницы
    /// </summary>
    private void StopInfoUpdate() => _dispatcherTimer?.Stop();

    #endregion

    #endregion

    #region Information builders

    /// <summary>
    ///     Основной метод для создания кнопок содержащих текущие показатели системы
    /// </summary>
    private async Task InfoCpuSectionGridBuilder()
    {
        try
        {
            MainSensorsGrid.RowDefinitions.Clear();
            MainSensorsGrid.ColumnDefinitions.Clear();

            var gpuCounter = GetSystemInfo.GetGpuNames().Count;

            var memoryCount = _cpu?.GetMemoryConfig().Modules.Count ?? 1;

            if (memoryCount <= 0)
            {
                memoryCount = 1;
            }

            // Определяем количество элементов (coreCounter) в зависимости от выбранной секции
            var coreCounter = _selectedGroup switch
            {
                // Секция процессора или P-States
                GroupCpu or GroupCpuPst => _numberOfCores > 2
                    ? _numberOfCores // Если ядер больше 2, используем количество ядер
                    : CpuSectionComboBox.SelectedIndex == 0
                        ? _numberOfLogicalProcessors // Если выбрано отображение частоты, используем логические процессоры
                        : _numberOfCores, // Иначе используем количество ядер
                // Секция GFX
                GroupGpu => gpuCounter, // Используем количество видеокарт
                // Секция RAM
                GroupRam => memoryCount, // Используем количество плат ОЗУ
                // Секция 3
                GroupVrm => 4, // Фиксированное значение для секции 3
                // Другие секции
                _ => 1 // По умолчанию 1 элемент
            };

            // Определяем количество строк и столбцов для сетки
            var rowCount = coreCounter / 2 > 4 ? 4 : coreCounter / 2;
            if (coreCounter % 2 != 0 || coreCounter == 2)
            {
                rowCount++; // Добавляем дополнительную строку, если количество элементов нечётное или равно 2
            }

            // Добавляем строки и столбцы в сетку
            for (var i = 0; i < rowCount; i++)
            {
                MainSensorsGrid.RowDefinitions.Add(new RowDefinition());
                MainSensorsGrid.ColumnDefinitions.Add(new ColumnDefinition());
            }

            // Заполняем сетку кнопками
            for (var j = 0; j < MainSensorsGrid.RowDefinitions.Count; j++)
            {
                for (var f = 0; f < MainSensorsGrid.ColumnDefinitions.Count; f++)
                {
                    // Если элементы закончились, выходим из метода
                    if (coreCounter <= 0)
                    {
                        return;
                    }

                    // Определяем текущее ядро (или элемент) в зависимости от секции
                    var currCore = _selectedGroup switch
                    {
                        // Секция процессора или PStates
                        GroupCpu or GroupCpuPst => _numberOfCores > 2
                            ? _numberOfCores - coreCounter // Если ядер больше 2, используем оставшиеся ядра
                            : CpuSectionComboBox.SelectedIndex == 0
                                ? _numberOfLogicalProcessors -
                                  coreCounter // Если выбрано отображение частоты, используем логические процессоры
                                : _numberOfCores - coreCounter, // Иначе используем оставшиеся ядра
                        // Секция GFX
                        GroupGpu => gpuCounter - coreCounter, // Используем оставшиеся видеокарты
                        // Секция RAM
                        GroupRam => memoryCount - coreCounter, // Используем оставшиеся платы ОЗУ
                        // Секция 3
                        GroupVrm => 4 - coreCounter, // Фиксированное значение для секции 3
                        // Другие секции
                        _ => 0 // По умолчанию 0
                    };

                    // Создаём кнопку для текущего элемента
                    var elementButton = CreateElementButton(currCore, _selectedGroup, _numberOfCores);

                    // Добавляем кнопку в сетку
                    Grid.SetRow(elementButton, j);
                    Grid.SetColumn(elementButton, f);
                    MainSensorsGrid.Children.Add(elementButton);

                    // Уменьшаем счётчик элементов
                    coreCounter--;
                }
            }
        }
        catch (Exception e)
        {
            await LogHelper.TraceIt_TraceError(e);
        }
    }

    /// <summary>
    ///     Создаёт кнопки отображающие текущие показатели
    /// </summary>
    private static Grid
        CreateElementButton(int currCore, int selectedGroup, int numberOfCores)
    {
        var elementButton = new Grid
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(3),
            Children =
            {
                new Button
                {
                    Shadow = new ThemeShadow(),
                    Translation = new Vector3(0, 0, 20),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    CornerRadius = DefaultCornerRadius,
                    Content = new Grid
                    {
                        VerticalAlignment = VerticalAlignment.Stretch,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Children =
                        {
                            new TextBlock
                            {
                                VerticalAlignment = VerticalAlignment.Center,
                                HorizontalAlignment = HorizontalAlignment.Left,
                                Text = currCore.ToString(),
                                FontWeight = new FontWeight(200)
                            },
                            new TextBlock
                            {
                                Text = "0.00 Ghz",
                                Name = $"FreqButtonText_{currCore}",
                                VerticalAlignment = VerticalAlignment.Center,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                FontWeight = new FontWeight(800)
                            },
                            new TextBlock
                            {
                                FontSize = 13,
                                Margin = new Thickness(3, -2, 0, 0),
                                VerticalAlignment = VerticalAlignment.Center,
                                HorizontalAlignment = HorizontalAlignment.Right,
                                Text = selectedGroup switch
                                {
                                    // Секция процессора или PStates
                                    GroupCpu or GroupCpuPst => currCore < numberOfCores
                                        ? "InfoCPUCore".GetLocalized() // Если это ядро, используем "Ядро"
                                        : "InfoCPUThread".GetLocalized(), // Иначе используем "Поток"
                                    // Секция GFX
                                    GroupGpu => "InfoGPUName".GetLocalized(), // Используем "Видеокарта"
                                    // Секция RAM
                                    GroupRam => "64", // По умолчанию 64 бита
                                    // Секция 3
                                    GroupVrm => currCore switch
                                    {
                                        0 => "VRM EDC",
                                        1 => "VRM TDC",
                                        2 => "SoC EDC",
                                        _ => "SoC TDC"
                                    },
                                    // Другие секции
                                    _ => "InfoBatteryName".GetLocalized() // По умолчанию "Батарея"
                                },
                                FontWeight = new FontWeight(200)
                            }
                        }
                    }
                }
            }
        };

        return elementButton;
    }

    #endregion

    #endregion

    #region Event Handlers

    /// <summary>
    ///     Обработчик переключения режима отображения информации о процессоре
    /// </summary>
    private void CpuSection_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded)
        {
            return;
        }

        try
        {
            MainSensorsGrid.Children.Clear();
            _ = InfoCpuSectionGridBuilder();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex);
        }
    }

    /// <summary>
    ///     Обработчик переключения режима RTSS
    /// </summary>
    private async void RtssButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (RtssButton.IsChecked == false)
            {
                RtssHandler.ResetOsdText();
            }
            else
            {
                RtssTeacherTip.IsOpen = true;
                await Task.Delay(3000);
                RtssTeacherTip.IsOpen = false;
            }

            _appSettings.RtssMetricsEnabled = RtssButton.IsChecked == true;
            _appSettings.SaveSettings();
        }
        catch (Exception ex)
        {
            await LogHelper.TraceIt_TraceError(ex);
        }
    }

    /// <summary>
    ///     Обработчик переключения режима TrayMon
    /// </summary>
    private void NiIconsButton_Click(object sender, RoutedEventArgs e) =>
        _appSettings.NiIconsEnabled = NiIconsButton.IsChecked == true;

    #region Flyouts and Banner Buttons handlers

    /// <summary>
    ///     Обработчик выбора отображения другой группы сенсоров
    /// </summary>
    private async void BannerButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button clickedButton && int.TryParse((string)clickedButton.Tag, out var selectedGroup))
            {
                if (_selectedGroup != selectedGroup)
                {
                    foreach (var button in _allBannerButtons)
                    {
                        button.Shadow = button == clickedButton ? new ThemeShadow() : null;
                        button.Background = button == clickedButton ? _selectedBrush : _transparentBrush;
                        button.BorderBrush = button == clickedButton ? _selectedBorderBrush : _transparentBrush;
                    }

                    _selectedGroup = selectedGroup;
                    MainSensorsGrid.Children.Clear();

                    if (selectedGroup == 5 && CpuSectionComboBox.SelectedIndex != 0)
                    {
                        CpuSectionComboBox.SelectedIndex = 0;
                    }
                    else
                    {
                        await InfoCpuSectionGridBuilder();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await LogHelper.TraceIt_TraceError(ex);
        }
    }

    /// <summary>
    ///     Обработчик отображения кнопки увеличения графика при наведении на баннер
    /// </summary>
    private void BannerButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Button bannerButton)
        {
            return;
        }

        // Находим ExpandButton внутри баннера
        var expandButton = VisualTreeHelper.FindVisualChildren<Button>(bannerButton).FirstOrDefault();
        if (expandButton is null)
        {
            return;
        }

        // Находим иконку и Flyout внутри ExpandButton
        var icon = VisualTreeHelper.FindVisualChildren<FontIcon>(expandButton).FirstOrDefault();
        var flyout = VisualTreeHelper.FindVisualChildren<Flyout>(expandButton).FirstOrDefault();

        var isFlyoutOpen = flyout?.IsOpen ?? false;

        // Показываем кнопку только если курсор над баннером или открыт Flyout
        expandButton.Visibility = bannerButton.IsPointerOver || isFlyoutOpen
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (icon is not null)
        {
            icon.Glyph = isFlyoutOpen ? "\uF0D7" : "\uF0D8";
        }

        // Скрываем все остальные кнопки расширения, кроме текущей
        foreach (var otherButton in _allExpandButtons)
        {
            if (otherButton == expandButton)
            {
                continue;
            }

            var otherFlyout = VisualTreeHelper.FindVisualChildren<Flyout>(otherButton).FirstOrDefault();
            var otherOpen = otherFlyout?.IsOpen ?? false;

            // Если Flyout у других не открыт, прячем их кнопки
            if (!otherOpen)
            {
                otherButton.Visibility = Visibility.Collapsed;
            }
        }
    }


    /// <summary>
    ///     Обработчик открытия Flyout для увеличения графика
    /// </summary>
    private void Flyout_Opening(object sender, object e)
    {
        foreach (var bannerButton in _allBannerButtons)
        {
            // Находим ExpandButton внутри баннера
            var expandButton = VisualTreeHelper.FindVisualChildren<Button>(bannerButton).FirstOrDefault();
            if (expandButton is null)
            {
                return;
            }

            // Находим иконку и Flyout внутри ExpandButton
            var icon = expandButton.Content as FontIcon;
            var flyout = sender as Flyout;
            if (flyout == null)
            {
                return;
            }

            var polygon = VisualTreeHelper.FindVisualChildren<Polygon>(flyout.Content).FirstOrDefault();

            if (icon == null || polygon == null)
            {
                return;
            }

            polygon.Points.Clear();
            polygon.Points.Add(StartPoint);

            if (flyout.IsOpen)
            {
                icon.Glyph = "\uF0D7";
                expandButton.Visibility = Visibility.Visible;
            }
            else
            {
                expandButton.Visibility = Visibility.Collapsed;
                icon.Glyph = "\uF0D8";
            }
        }
    }

    #endregion

    #endregion
}