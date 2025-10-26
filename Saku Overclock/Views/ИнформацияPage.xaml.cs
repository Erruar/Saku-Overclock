using System.Management;
using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.JsonContainers.Helpers;
using Saku_Overclock.Services;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.Wrappers;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;
using ZenStates.Core;
using static ZenStates.Core.DRAM.MemoryConfig;
using Brush = Microsoft.UI.Xaml.Media.Brush;

namespace Saku_Overclock.Views;

public sealed partial class ИнформацияPage
{
    /// <summary>
    ///  Класс для хранения минимальных и максимальных значений TrayMon
    /// </summary>
    public class MinMax
    {
        public double Min;
        public double Max;
    }

    private readonly IAppSettingsService AppSettings = App.GetService<IAppSettingsService>(); // Настройки приложения
    private double _busyRam; // Текущее использование ОЗУ и всего ОЗУ
    private double _totalRam;
    private bool _loaded; // Страница загружена
    private bool _doNotTrackBattery; // Флаг не использования батареи 
    private bool _isBatteryInformationLoaded; // Флаг обновления информации о батарее
    private readonly List<InfoPageCpuPoints> _cpuPointer = []; // Лист графика использования процессора
    private readonly List<InfoPageCpuPoints> _gpuPointer = []; // Лист графика частоты графического процессора
    private readonly List<InfoPageCpuPoints> _ramPointer = []; // Лист графика занятой ОЗУ
    private readonly List<InfoPageCpuPoints> _vrmPointer = []; // Лист графика тока VRM
    private readonly List<InfoPageCpuPoints> _batPointer = []; // Лист графика зарядки батареи
    private readonly List<InfoPageCpuPoints> _pstPointer = []; // Лист графика изменения P-State
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
    private string _gpuName = "Unknown"; // Название графического процессора в системе
    private string _ramName = "Unknown"; // Название ОЗУ в системе
    private string _batName = "Unknown"; // Название батареи в системе
    private int _numberOfCores; // Количество ядер
    private int _numberOfLogicalProcessors; // Количество потоков
    private DispatcherTimer? _dispatcherTimer; // Таймер для автообновления информации
    private readonly IBackgroundDataUpdater? _dataUpdater = App.BackgroundUpdater; // Фоновое обновление информации
    private SensorsInformation? _sensorsInformation; // Информация с датчиков
    private readonly Cpu? _cpu = CpuSingleton.GetInstance(); // Инициализация ZenStates Core

    private static readonly string _batFromWall = "InfoBatteryAC".GetLocalized(); // Устройство от сети
    private static readonly string _mhzFreq = "InfoFreqBoundsMHZ".GetLocalized(); // Частота МГц
    private static readonly string _ghzFreq = "infoAGHZ".GetLocalized(); // Частота ГГц
    private static readonly string _pstate = "InfoPSTState".GetLocalized(); // P-State
    private double _maxVoltage;
    private double _maxFrequency;

    private Button[] _allBannerButtons = [];
    private Button[] _allExpandButtons = [];

    // Константы для индексов выбора секции
    private const int SECTION_FREQUENCY = 0;
    private const int SECTION_VOLTAGE = 1;
    private const int SECTION_POWER = 2;
    private const int SECTION_TEMPERATURE = 3;

    // Константы для групп отображения
    private const int GROUP_CPU = 0;
    private const int GROUP_GPU = 1;
    private const int GROUP_RAM = 2;
    private const int GROUP_VRM = 3;
    private const int GROUP_BATTERY = 4;
    private const int GROUP_CPU_PST = 5;

    private const int MAX_CORES = 16;
    private const double MAX_VALID_FREQUENCY = 7.0;
    private const double MAX_VALID_VOLTAGE = 1.7;

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
    ///  Основной метод для получения и обновления значений сенсоров системы
    /// </summary>
    private void OnDataUpdated(object? sender, SensorsInformation info) => _sensorsInformation = info;

    /// <summary>
    ///  Вспомогательный метод для правильной установки цветов элементов
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
                infoAVRMUsageBannerPolygonText.Foreground = brush;
                infoAVRMUsageBigBannerPolygonText.Foreground = brush;
                infoARAMUsageBannerPolygonText.Foreground = brush;
                infoARAMUsageBigBannerPolygonText.Foreground = brush;
                infoABATUsageBannerPolygonText.Foreground = brush;
                infoABATUsageBigBannerPolygonText.Foreground = brush;
                PstUsageBannerPolygonText.Foreground = brush;
                PstUsageBigBannerPolygonText.Foreground = brush;
            }
        }
    }

    /// <summary>
    ///  Основной метод отображения характеристик процессора
    /// </summary>
    private async Task LoadCpuInformation()
    {
        try
        {
            var (name, baseclock) = GetSystemInfo.ReadCpuInformation();
            tbBaseClock.Text = $"{baseclock} MHz";
            tbThreads.Text = Environment.ProcessorCount.ToString();

            _cpuName = _cpu == null ? name : _cpu.info.cpuName;

            if (_cpu != null && (_numberOfCores == 0 || _numberOfLogicalProcessors == 0))
            {
                _numberOfCores = (int)_cpu.info.topology.cores;
                _numberOfLogicalProcessors = (int)_cpu.info.topology.logicalCores;
            }

            try
            {
                tbCodename.Text = $"{_cpu?.info.codeName}";
            }
            catch
            {
                tbCodename.Visibility = Visibility.Collapsed;
                tbCode.Visibility = Visibility.Collapsed;
            }

            try
            {
                tbSMU.Text = _cpu?.systemInfo.GetSmuVersionString();
            }
            catch
            {
                tbSMU.Visibility = Visibility.Collapsed;
                infoSMU.Visibility = Visibility.Collapsed;
            }

            await InfoCpuSectionGridBuilder();

            tbProcessor.Text = _cpuName;
            tbCores.Text = _numberOfLogicalProcessors == _numberOfCores
                ? _numberOfCores.ToString()
                : GetSystemInfo.GetBigLITTLE(_numberOfCores);

            var gpus = GetSystemInfo.GetGpuNames();
            var gpuName = gpus.Count == 0 ? "Unknown" :
                          gpus.Count == 1 ? gpus[0] :
                          gpus[0].Contains("AMD", StringComparison.OrdinalIgnoreCase) ? gpus[0] : gpus[1];

            var l1Cache = GetSystemInfo.CalculateCacheSize(GetSystemInfo.CacheLevel.Level1);
            var l2Cache = GetSystemInfo.CalculateCacheSize(GetSystemInfo.CacheLevel.Level2);

            _gpuName = gpuName;

            tbThreads.Text = _numberOfLogicalProcessors.ToString();
        }
        catch (Exception ex)
        {
            await LogHelper.TraceIt_TraceError(ex);
        }
    }

    /// <summary>
    ///  Основной метод отображения характеристик батареи
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

            tbBATHealth.Text = _sensorsInformation.BatteryHealth;
            tbBATCycles.Text = _sensorsInformation.BatteryCycles;
            tbBATCapacity.Text = _sensorsInformation.BatteryCapacity;
            _batName = _sensorsInformation.BatteryName ?? "Unknown";

            _isBatteryInformationLoaded = true;
        }
    }

    /// <summary>
    ///  Основной метод отображения характеристик оперативной памяти
    /// </summary>
    private async Task LoadRamInformation()
    {
        try
        {
            if (_cpu != null)
            {
                var capacity = _cpu.GetMemoryConfig().TotalCapacity.SizeInBytes / 1073741824;

                var speed = _cpu.powerTable?.MCLK * 2 ?? 0;
                var umcBase = _cpu.ReadDword(0x50200);
                var freqFromRatio = (_cpu.GetMemoryConfig().Type == MemType.DDR4 ?
                                     Utils.GetBits(umcBase, 0, 7) / 3 :
                                     Utils.GetBits(umcBase, 0, 16) / 100)
                                     * 200;

                if (speed == 0 || freqFromRatio > speed)
                {
                    speed = freqFromRatio;
                }

                var type = _cpu.GetMemoryConfig().Type;
                var modules = _cpu.GetMemoryConfig().Modules;

                var width = modules.Count * 64;
                var slots = modules.Count; 

                var producer = (modules == null || modules.Count == 0)
                    ? "Unknown"
                    : string.Join("/", modules
                        .Select(m => m?.Manufacturer ?? "Unknown")
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct());

                var model = (modules == null || modules.Count == 0)
                    ? "Unknown"
                    : string.Join("/", modules
                        .Select(m => m?.PartNumber ?? "Unknown")
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct());

                var tcl = Utils.GetBits(_cpu.ReadDword(0x50204), 0, 6);
                var trcdwr = Utils.GetBits(_cpu.ReadDword(0x50204), 24, 6);
                var trcdrd = Utils.GetBits(_cpu.ReadDword(0x50204), 16, 6);
                var tras = Utils.GetBits(_cpu.ReadDword(0x50204), 8, 7);
                var trp = Utils.GetBits(_cpu.ReadDword(0x50208), 16, 6);
                var trc = Utils.GetBits(_cpu.ReadDword(0x50208), 0, 8);

                _ramName = $"{capacity} GB {type} @ {speed} MT/s";
                tbRAM.Text = speed + "MT/s";
                tbRAMProducer.Text = producer;
                RamModel.Text = model.Replace(" ", "");
                tbWidth.Text = $"{width} bit";
                tbSlots.Text = $"{slots} * {width / slots} bit";
                tbTCL.Text = tcl + "T";
                tbTRCDWR.Text = trcdwr + "T";
                tbTRCDRD.Text = trcdrd + "T";
                tbTRAS.Text = tras + "T";
                tbTRP.Text = trp + "T";
                tbTRC.Text = trc + "T";
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
    ///  Вспомогательный метод для получения Fid/Did
    /// </summary>
    private static void CalculatePstateDetails(uint eax,
        out uint cpuDfsId, out uint cpuFid)
    {
        cpuDfsId = (eax >> 8) & 0x3F;
        cpuFid = eax & 0xFF;
    }

    /// <summary>
    ///  Основной метод чтения P-States
    /// </summary>
    private void ReadPstate()
    {
        try
        {
            for (var i = 0; i < 3; i++)
            {
                uint eax = 0, edx = 0;
                var pstateId = i;
                try
                {
                    if (_cpu?.ReadMsr(Convert.ToUInt32(Convert.ToInt64(0xC0010064) + pstateId), ref eax, ref edx) ==
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
                var textBlock = (TextBlock)InfoPSTSectionMetrics.FindName($"tbPSTP{i}");
                if (cpuFid != 0)
                {
                    textBlock.Text =
                        $"FID: {Convert.ToString(cpuFid, 10)}/DID: {Convert.ToString(cpuDfsId, 10)}\n{cpuFid * 25 / (cpuDfsId * 12.5) / 10}" +
                        _ghzFreq;
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
    ///  Обработчик сворачивания окна
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
    ///  Обработчик захода на страницу
    /// </summary>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        StartInfoUpdate();
    }

    /// <summary>
    ///  Обработчик выхода со страницы
    /// </summary>
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        StopInfoUpdate();
        App.MainWindow.VisibilityChanged -= Window_VisibilityChanged;
    }

    /// <summary>
    ///  Обработчик загрузки страницы
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
            await LoadRamInformation();
            ReadPstate();

            if (CpuBannerButton.Shadow != new ThemeShadow())
            {
                CpuBannerButton.Shadow ??= new ThemeShadow();
                GpuBannerButton.Shadow = null;
                RamBannerButton.Shadow = null;
                BatBannerButton.Shadow = null;
                PstBannerButton.Shadow = null;
                VrmBannerButton.Shadow = null;
            }

            infoRTSSButton.IsChecked = AppSettings.RtssMetricsEnabled;
            infoNiIconsButton.IsChecked = AppSettings.NiIconsEnabled;
        }
        catch (Exception ex)
        {
            await LogHelper.TraceIt_TraceError(ex);
        }
    }

    /// <summary>
    ///  Обработчик выгрузки страницы
    /// </summary>
    private void ИнформацияPage_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_dataUpdater != null)
        {
            _dataUpdater.DataUpdated -= OnDataUpdated;
        }

        StopInfoUpdate();
    }

    #endregion

    #region Info Update voids

    /// <summary>
    ///  Основной метод автообновления информации
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
                infoCPUSectionComboBox.Visibility = _selectedGroup != 5 ? Visibility.Collapsed : Visibility.Visible;

                switch (_selectedGroup)
                {
                    case 1: // GPU
                        ConfigureSection(
                            "InfoGPUSectionName".GetLocalized(),
                            _gpuName,
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

                infoCPUMAINSection.Visibility = Visibility.Visible;
                InfoCPUSectionMetrics.Visibility = Visibility.Visible;
                infoCPUSectionComboBox.Visibility = Visibility.Visible;

                infoCPUSectionName.Text = "InfoCPUSectionName".GetLocalized();
                tbProcessor.Text = _cpuName;
            }


            if (RyzenadjProvider.IsPhysicallyUnavailable)
            {
                GpuBannerButton.Visibility = Visibility.Collapsed;
            }

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

            if (_sensorsInformation == null)
            {
                return;
            }

            LoadBatteryInformation();

            var currBatRate = 0d;
            var previousMaxBatRate = 0;
            if (!_doNotTrackBattery)
            {
                tbBATChargeRate.Text = infoABATUsageBigBannerPolygonText.Text = infoABATUsageBannerPolygonText.Text = $"{_sensorsInformation.BatteryChargeRate:0.##}W";
                tbBAT.Text = _sensorsInformation.BatteryPercent;
                tbBATTime.Text = _sensorsInformation.BatteryLifeTime < 0 ? 
                    _batFromWall : 
                    GetSystemInfo.ConvertBatteryLifeTime(_sensorsInformation.BatteryLifeTime);
                infoIBATUsageBigBanner.Text = InfoBATUsage.Text = tbBAT.Text + " " + tbBATChargeRate.Text + "\n" + tbBATTime.Text;
                tbBATState.Text = _sensorsInformation.BatteryState;
                
                currBatRate = Math.Abs(_sensorsInformation.BatteryChargeRate);
                previousMaxBatRate = (int)_maxBatRate;
                if (currBatRate > _maxBatRate)
                {
                    _maxBatRate = currBatRate;
                }
            }

            if (_sensorsInformation.CpuStapmLimit == 0)
            {
                tbStapmL.Text = "Info_PowerSumInfo_Disabled".GetLocalized();
                StapmLimitBar.ShowError = true;
                StapmLimitBar.IsIndeterminate = true;
            }
            else
            {
                tbStapmL.Text = $"{_sensorsInformation.CpuStapmValue:0.###}W/{_sensorsInformation.CpuStapmLimit:0}W";
                StapmLimitBar.Value = _sensorsInformation.CpuStapmValue;
                StapmLimitBar.Maximum = _sensorsInformation.CpuStapmLimit;
            }

            tbAclualPowerL.Text = tbActualL.Text = $"{_sensorsInformation.CpuFastValue:0.###}W/{_sensorsInformation.CpuFastLimit:0}W";
            ActualLimitBar.Value = _sensorsInformation.CpuFastValue;
            ActualLimitBar.Maximum = _sensorsInformation.CpuFastLimit;

            tbAVGL.Text = _sensorsInformation.CpuSlowLimit == 0 ? 
                "Info_PowerSumInfo_Disabled".GetLocalized() : 
                $"{_sensorsInformation.CpuSlowValue:0.###}W/{_sensorsInformation.CpuSlowLimit:0}W";
            AverageLimitBar.Value = _sensorsInformation.CpuSlowValue;
            AverageLimitBar.Maximum = _sensorsInformation.CpuSlowLimit;

            tbFast.Text = $"{_sensorsInformation.CpuStapmTimeValue:0.###}S";
            tbSlow.Text = $"{_sensorsInformation.CpuSlowTimeValue:0.###}S";

            tbAPUL.Text = $"{_sensorsInformation.ApuSlowValue:0.###}W/{_sensorsInformation.ApuSlowLimit:0}W";

            tbVRMTDCL.Text = $"{_sensorsInformation.VrmTdcValue:0.###}A/{_sensorsInformation.VrmTdcLimit:0}A";
            tbSOCTDCL.Text = $"{_sensorsInformation.SocTdcValue:0.###}A/{_sensorsInformation.SocTdcLimit:0}A";
            tbVRMEDCVRML.Text = tbVRMEDCL.Text = $"{_sensorsInformation.VrmEdcValue:0.###}A/{_sensorsInformation.VrmEdcLimit:0}A";
            VrmEdcBar.Value = _sensorsInformation.VrmEdcValue;
            VrmEdcBar.Maximum = _sensorsInformation.VrmEdcLimit;

            infoIVRMUsageBigBanner.Text = infoVRMUsageBanner.Text = $"{_sensorsInformation.VrmEdcValue:0.###}A\n{_sensorsInformation.CpuFastValue:0.###}W";
            infoAVRMUsageBigBannerPolygonText.Text = infoAVRMUsageBannerPolygonText.Text = $"{_sensorsInformation.VrmEdcValue:0.###}A";
            tbSOCEDCL.Text = $"{_sensorsInformation.SocEdcValue:0.###}A/{_sensorsInformation.SocEdcLimit:0}A";
            tbSOCVOLT.Text = _sensorsInformation.SocVoltage == 0 ? 
                $"{_cpu!.powerTable?.VDDCR_SOC ?? 0:0.###}V" : 
                $"{_sensorsInformation.SocVoltage:0.###}V";

            tbSOCPOWER.Text = _sensorsInformation.SocPower == 0 ? 
                $"{(_cpu!.powerTable?.VDDCR_SOC ?? 0) * 10:0.###}W" : 
                $"{_sensorsInformation.SocPower:0.###}W";

            tbMEMCLOCK.Text = _sensorsInformation.MemFrequency == 0 ? 
                $"{_cpu!.powerTable?.MCLK ?? 0:0.###}{_mhzFreq}" : 
                $"{_sensorsInformation.MemFrequency:0.###}{_mhzFreq}";

            tbFabricClock.Text = _sensorsInformation.FabricFrequency == 0 ? 
                $"{_cpu!.powerTable?.FCLK ?? 0:0.###}{_mhzFreq}" : 
                $"{_sensorsInformation.FabricFrequency:0.###}{_mhzFreq}";

            // Инициализация переменных для накопления данных
            var totalFrequency = 0d;
            var frequencyCount = 0;
            var totalVoltage = 0d;
            var voltageCount = 0;
            var maxFrequency = 0d;
            var currentPstate = 4;

            var selectedSection = infoCPUSectionComboBox.SelectedIndex;

            // Обработка данных по каждому ядру
            for (uint coreIndex = 0; coreIndex < MAX_CORES; coreIndex++)
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
                    SECTION_FREQUENCY => coreFrequency,
                    SECTION_VOLTAGE => GetCoreValue(_sensorsInformation.CpuVoltagePerCore, coreIndex),
                    SECTION_POWER => GetCoreValue(_sensorsInformation.CpuPowerPerCore, coreIndex),
                    SECTION_TEMPERATURE => GetCoreValue(_sensorsInformation.CpuTemperaturePerCore, coreIndex),
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
                CpuFrequency.Text = $"{avgFrequency} {_ghzFreq}";
                if (avgFrequency > _maxFrequency)
                {
                    _maxFrequency = avgFrequency;
                    CpuFrequencyBar.Maximum = _maxFrequency;
                }
                CpuFrequencyBar.Value = avgFrequency;

                currentPstate = DeterminePState(avgFrequency);
                UpdatePStateUI(currentPstate);
            }
            else
            {
                CpuFrequency.Text = $"? {_ghzFreq}";
            }

            // Обновление среднего напряжения
            if (voltageCount > 0)
            {
                var avgVoltage = totalVoltage / voltageCount;
                CpuVoltage.Text = $"{avgVoltage:0.###}V";
                if (avgVoltage > _maxVoltage)
                {
                    _maxVoltage = avgVoltage;
                    CpuVoltageBar.Maximum = _maxVoltage;
                }
                CpuVoltageBar.Value = avgVoltage;
            }
            else
            {
                HideVoltagePanel();
            }


            PstFrequency.Text = CpuFrequency.Text;
            // Используем уже готовые значения с форматами
            var gfxClk = _sensorsInformation.ApuFrequency / 1000.0;
            var gfxVolt = _sensorsInformation.ApuVoltage;
            var gfxTemp = _sensorsInformation.ApuTemperature;

            // обновление максимума
            var beforeMaxGfxClk = _maxGfxClock;
            if (_maxGfxClock < gfxClk)
            {
                _maxGfxClock = gfxClk;
            }

            // GPU баннеры
            GpuUsageBanner.Text = GpuUsageBigBanner.Text =
                $"{gfxClk:0.###} {_ghzFreq}  {gfxTemp:0}C\n{gfxVolt:0.###}V";

            GpuFrequency.Text = GpuUsageBigBannerPolygonText.Text = GpuUsageBannerPolygonText.Text =
                $"{gfxClk:0.###}{_ghzFreq}";

            GpuVoltage.Text = $"{gfxVolt:0.###}V";

            // CPU max temp
            var maxTemp = _sensorsInformation.CpuTempLimit;
            CpuMaxTemp.Text = CpuMaxTempLimit.Text = CpuMaxTempLimit_Vrm.Text =
                $"{_sensorsInformation.CpuTempValue:0.###}C/{maxTemp:0.###}C";
            CpuTemperatureBar.Value = _sensorsInformation.CpuTempValue;
            CpuTemperatureBar.Maximum = maxTemp;

            // APU temp
            var apuTemp = _sensorsInformation.ApuTempValue;
            var apuTempLimit = _sensorsInformation.ApuTempLimit;
            IgpuMaxTempLimit.Text =
                $"{(!double.IsNaN(apuTemp) && apuTemp > 0 ? apuTemp : gfxTemp):0.###}C/" +
                $"{(!double.IsNaN(apuTempLimit) && apuTempLimit > 0 ? apuTempLimit : maxTemp):0.###}C";

            // dGPU temp
            DgpuMaxTempLimit.Text =
                $"{_sensorsInformation.DgpuTempValue:0.###}C/{_sensorsInformation.DgpuTempLimit:0.###}C";

            // CPU usage
            var coreCpuUsage = _sensorsInformation.CpuUsage;
            CpuUsage.Text = CpuUsageBigBannerPolygonText.Text = $"{coreCpuUsage:0.###}%";
            CpuUsageBar.Value = coreCpuUsage;
            CpuUsageBannerPolygonText.Text = $"{coreCpuUsage:0}%";
            CpuUsageBanner.Text = CpuUsageBigBanner.Text =
                $"{coreCpuUsage:0}%  {CpuFrequency.Text}\n{(CpuVoltage.Text != "?V" ? CpuVoltage.Text : string.Empty)}";


            //InfoACPUBanner График
            CpuBannerPolygon.Points.Remove(ZeroPoint);
            _cpuPointer.Add(new InfoPageCpuPoints { X = 60, Y = 48 - (int)(coreCpuUsage * 0.48) });
            if (CpuFlyout.IsOpen)
            {
                CpuBigBannerPolygon.Points.Remove(ZeroPoint);
                CpuBigBannerPolygon.Points.Add(new Point(60,
                    48 - (int)(coreCpuUsage * 0.48)));
            }

            CpuBannerPolygon.Points.Add(new Point(60, 48 - (int)(coreCpuUsage * 0.48)));
            foreach (var element in _cpuPointer.ToList())
            {
                if (element.X < 0)
                {
                    _cpuPointer.Remove(element);
                    if (CpuFlyout.IsOpen)
                    {
                        CpuBigBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                    }

                    CpuBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                }
                else
                {
                    if (CpuFlyout.IsOpen)
                    {
                        CpuBigBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                    }

                    CpuBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                    element.X -= 1;
                    CpuBannerPolygon.Points.Add(new Point(element.X, element.Y));
                    if (CpuFlyout.IsOpen)
                    {
                        CpuBigBannerPolygon.Points.Add(new Point(element.X, element.Y));
                    }
                }
            }

            if (CpuFlyout.IsOpen)
            {
                CpuBigBannerPolygon.Points.Add(MaxedPoint);
            }

            CpuBannerPolygon.Points.Add(MaxedPoint);


            //InfoAGPUBanner График
            GpuBannerPolygon.Points.Remove(ZeroPoint);
            _gpuPointer.Add(new InfoPageCpuPoints { X = 60, Y = 48 - (int)(gfxClk / _maxGfxClock * 48) });
            GpuBannerPolygon.Points.Add(new Point(60,
                48 - (int)(gfxClk / _maxGfxClock * 48)));
            if (GpuFlyout.IsOpen)
            {
                GpuBigBannerPolygon.Points.Remove(ZeroPoint);
                GpuBigBannerPolygon.Points.Add(new Point(60,
                    48 - (int)(gfxClk / _maxGfxClock * 48)));
            }

            foreach (var element in _gpuPointer.ToList())
            {
                if (element.X < 0)
                {
                    _gpuPointer.Remove(element);
                    GpuBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                    if (GpuFlyout.IsOpen)
                    {
                        GpuBigBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                    }
                }
                else
                {
                    GpuBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                    if (GpuFlyout.IsOpen)
                    {
                        GpuBigBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                    }

                    element.X -= 1;
                    element.Y = (int)(element.Y * beforeMaxGfxClk / _maxGfxClock);
                    if (GpuFlyout.IsOpen)
                    {
                        GpuBigBannerPolygon.Points.Add(new Point(element.X, element.Y));
                    }

                    GpuBannerPolygon.Points.Add(new Point(element.X, element.Y));
                }
            }

            if (GpuFlyout.IsOpen)
            {
                GpuBigBannerPolygon.Points.Add(MaxedPoint);
            }

            GpuBannerPolygon.Points.Add(MaxedPoint);

            //InfoAVRMBanner График
            VrmBannerPolygon.Points.Remove(ZeroPoint);
            _vrmPointer.Add(new InfoPageCpuPoints
            {
                X = 60,
                Y = 48 - (int)(_sensorsInformation.VrmEdcValue /
                    _sensorsInformation.VrmEdcLimit * 48)
            });
            if (VrmFlyout.IsOpen)
            {
                VrmBigBannerPolygon.Points.Remove(ZeroPoint);
                VrmBigBannerPolygon.Points.Add(new Point(60,
                    48 - (int)(_sensorsInformation.VrmEdcValue /
                        _sensorsInformation.VrmEdcLimit * 48)));
            }

            VrmBannerPolygon.Points.Add(new Point(60,
                48 - (int)(_sensorsInformation.VrmEdcValue /
                    _sensorsInformation.VrmEdcLimit * 48)));
            foreach (var element in _vrmPointer.ToList())
            {
                if (element.X < 0)
                {
                    _vrmPointer.Remove(element);
                    VrmBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                    if (VrmFlyout.IsOpen)
                    {
                        VrmBigBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                    }
                }
                else
                {
                    if (VrmFlyout.IsOpen)
                    {
                        VrmBigBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                    }

                    VrmBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                    element.X -= 1;
                    if (VrmFlyout.IsOpen)
                    {
                        VrmBigBannerPolygon.Points.Add(new Point(element.X, element.Y));
                    }

                    VrmBannerPolygon.Points.Add(new Point(element.X, element.Y));
                }
            }

            VrmBannerPolygon.Points.Add(MaxedPoint);
            if (VrmFlyout.IsOpen)
            {
                VrmBigBannerPolygon.Points.Add(MaxedPoint);
            }

            //InfoAPSTBanner График
            PstBannerPolygon.Points.Remove(ZeroPoint);
            _pstPointer.Add(new InfoPageCpuPoints { X = 60, Y = 48 - currentPstate * 16 });
            PstBannerPolygon.Points.Add(new Point(60, 48 - currentPstate * 16));
            if (PstFlyout.IsOpen)
            {
                PstBigBannerPolygon.Points.Remove(ZeroPoint);
                PstBigBannerPolygon.Points.Add(new Point(60, 48 - currentPstate * 16));
            }

            foreach (var element in _pstPointer.ToList())
            {
                if (element.X < 0)
                {
                    _pstPointer.Remove(element);
                    PstBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                    if (PstFlyout.IsOpen)
                    {
                        PstBigBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                    }
                }
                else
                {
                    PstBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                    if (PstFlyout.IsOpen)
                    {
                        PstBigBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                    }

                    element.X -= 1;
                    PstBannerPolygon.Points.Add(new Point(element.X, element.Y));
                    if (PstFlyout.IsOpen)
                    {
                        PstBigBannerPolygon.Points.Add(new Point(element.X, element.Y));
                    }
                }
            }

            if (PstFlyout.IsOpen)
            {
                PstBigBannerPolygon.Points.Add(MaxedPoint);
            }

            PstBannerPolygon.Points.Add(MaxedPoint);

            //InfoABATBanner График
            BatBannerPolygon.Points.Remove(ZeroPoint);
            _batPointer.Add(new InfoPageCpuPoints { X = 60, Y = 48 - (int)(Math.Abs(currBatRate) / _maxBatRate * 48) });
            BatBannerPolygon.Points.Add(new Point(60,
                48 - (int)(Math.Abs(currBatRate) / _maxBatRate * 48)));
            if (BatFlyout.IsOpen)
            {
                BatBigBannerPolygon.Points.Remove(ZeroPoint);
                BatBigBannerPolygon.Points.Add(new Point(60,
                    48 - (int)(Math.Abs(currBatRate) / _maxBatRate * 48)));
            }

            foreach (var element in _batPointer.ToList())
            {
                if (element.X < 0)
                {
                    _batPointer.Remove(element);
                    BatBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                    if (BatFlyout.IsOpen)
                    {
                        BatBigBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                    }
                }
                else
                {
                    BatBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                    if (BatFlyout.IsOpen)
                    {
                        BatBigBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                    }

                    element.X -= 1;
                    element.Y = (int)(element.Y * previousMaxBatRate / _maxBatRate);
                    BatBannerPolygon.Points.Add(new Point(element.X, element.Y));
                    if (BatFlyout.IsOpen)
                    {
                        BatBigBannerPolygon.Points.Add(new Point(element.X, element.Y));
                    }
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
                    _ramPointer.Add(new InfoPageCpuPoints
                    {
                        X = 60,
                        Y = 48 - (int)(busyRam * 100 / usageResult * 0.48)
                    });
                    RamBannerPolygon.Points.Add(new Point(60,
                        48 - (int)(busyRam * 100 / usageResult * 0.48)));
                    if (RamFlyout.IsOpen)
                    {
                        RamBigBannerPolygon.Points.Remove(ZeroPoint);
                        RamBigBannerPolygon.Points.Add(new Point(60,
                            48 - (int)(busyRam * 100 / usageResult * 0.48)));
                    }
                }

                foreach (var element in _ramPointer.ToList())
                {
                    if (element.X < 0)
                    {
                        _ramPointer.Remove(element);
                        RamBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                        if (RamFlyout.IsOpen)
                        {
                            RamBigBannerPolygon.Points.Remove(
                                new Point(element.X, element.Y));
                        }
                    }
                    else
                    {
                        if (RamFlyout.IsOpen)
                        {
                            RamBigBannerPolygon.Points.Remove(
                                new Point(element.X, element.Y));
                        }

                        RamBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                        element.X -= 1;
                        RamBannerPolygon.Points.Add(new Point(element.X, element.Y));
                        if (RamFlyout.IsOpen)
                        {
                            RamBigBannerPolygon.Points.Add(new Point(element.X, element.Y));
                        }
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

            InfoRAMUsage.Text = _sensorsInformation.RamUsage;
            infoARAMUsageBannerPolygonText.Text = _sensorsInformation.RamUsagePercent + "%";
            infoARAMUsageBigBannerPolygonText.Text = infoARAMUsageBannerPolygonText.Text;
            infoIRAMUsageBigBanner.Text = InfoRAMUsage.Text;
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
    ///  Безопасное получение значения из массива
    /// </summary>
    private static double GetCoreValue(double[]? array, uint index) => array != null && array.Length > index ? array[index] : 0f;

    /// <summary>
    ///  Проверка валидности частоты
    /// </summary>
    private static bool IsValidFrequency(double frequency) => frequency > 0 && frequency < MAX_VALID_FREQUENCY;

    /// <summary>
    ///  Проверка валидности напряжения
    /// </summary>
    private static bool IsValidVoltage(double voltage) => !double.IsNaN(voltage) && voltage > 0 && voltage < MAX_VALID_VOLTAGE;

    /// <summary>
    ///  Обновление отображаемого в интерфейсе значения для конкретного индекса
    /// </summary>
    private void UpdateButtonText(uint index, double value, int section)
    {
        var textBlock = (TextBlock)InfoMainCPUFreqGrid.FindName($"FreqButtonText_{index}");
        if (textBlock == null)
        {
            return;
        }

        if (_selectedGroup is GROUP_CPU or GROUP_CPU_PST)
        {
            textBlock.Text = FormatCoreValue(value, section);
        }
        else
        {
            textBlock.Text = GetGroupSpecificText(index);
        }
    }

    /// <summary>
    ///  Форматирование значения в зависимости от секции
    /// </summary>
    private static string FormatCoreValue(double value, int section)
    {
        return section switch
        {
            SECTION_FREQUENCY => $"{value:0.###} {_ghzFreq}",
            SECTION_VOLTAGE => $"{value:0.###}V",
            SECTION_POWER => $"{value:0.###}W",
            SECTION_TEMPERATURE => $"{value:0.###}C",
            _ => $"{value:0.###} {_ghzFreq}"
        };
    }


    /// <summary>
    ///  Получение текста для специфичных групп (GPU, RAM, VRM, Battery)
    /// </summary>
    private string GetGroupSpecificText(uint index)
    {
        return _selectedGroup switch
        {
            GROUP_GPU => GetGpuText(index),
            GROUP_RAM => GetRamText(index),
            GROUP_VRM => GetVrmText(index),
            GROUP_BATTERY => _batName,
            _ => "Unknown"
        };
    }

    /// <summary>
    ///  Получение текста для GPU
    /// </summary>
    private static string GetGpuText(uint index)
    {
        var gpuNames = GetSystemInfo.GetGpuNames();
        return index < gpuNames.Count ? gpuNames[(int)index] : "Unknown";
    }

    /// <summary>
    ///  Получение текста для RAM
    /// </summary>
    private string GetRamText(uint index)
    {
        var ramModels = RamModel.Text.Split('/');
        return index < ramModels.Length ? ramModels[index] : "Unknown";
    }

    /// <summary>
    ///  Получение текста для VRM
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
    ///  Форматирование значений тока VRM
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
    ///  Обновление UI элементов P-state
    /// </summary>
    private void UpdatePStateUI(int pstate)
    {
        var pstateText = pstate switch
        {
            3 => "P0",
            2 => "P1",
            1 => "P2",
            _ => "C1"
        };

        PowerState.Text = PstUsageBannerPolygonText.Text = PstUsageBigBannerPolygonText.Text = pstateText;
        PstUsage.Text = PstUsageBigBanner.Text = pstateText + _pstate;
    }

    /// <summary>
    ///  Скрытие панели напряжения
    /// </summary>
    private void HideVoltagePanel()
    {
        CpuVoltagePanel.Visibility = Visibility.Collapsed;
        CpuVoltage.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    ///  Вспомогательный метод для скрытия всех секций сенсоров
    /// </summary>
    private void HideAllSections()
    {
        InfoCPUSectionMetrics.Visibility = Visibility.Collapsed;
        InfoGPUSectionMetrics.Visibility = Visibility.Collapsed;
        InfoRAMSectionMetrics.Visibility = Visibility.Collapsed;
        InfoVRMSectionMetrics.Visibility = Visibility.Collapsed;
        InfoBATSectionMetrics.Visibility = Visibility.Collapsed;
        InfoPSTSectionMetrics.Visibility = Visibility.Collapsed;
        infoRAMMAINSection.Visibility = Visibility.Collapsed;
        infoCPUMAINSection.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    ///  Вспомогательный метод для отображения или скрытия секций сенсоров
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
        infoCPUSectionName.Text = sectionName;
        tbProcessor.Text = processorText;

        InfoCPUSectionMetrics.Visibility = cpuVisible ? Visibility.Visible : Visibility.Collapsed;
        InfoGPUSectionMetrics.Visibility = gpuVisible ? Visibility.Visible : Visibility.Collapsed;
        InfoRAMSectionMetrics.Visibility = ramVisible ? Visibility.Visible : Visibility.Collapsed;
        InfoVRMSectionMetrics.Visibility = vrmVisible ? Visibility.Visible : Visibility.Collapsed;
        InfoBATSectionMetrics.Visibility = batVisible ? Visibility.Visible : Visibility.Collapsed;
        InfoPSTSectionMetrics.Visibility = pstVisible ? Visibility.Visible : Visibility.Collapsed;
        infoCPUMAINSection.Visibility = cpuMainVisible ? Visibility.Visible : Visibility.Collapsed;
        infoRAMMAINSection.Visibility = ramMainVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    ///  Начать автообновление информации 
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
    ///  Остановка автообновления информации. Вызываеться при скрытии/переключении страницы
    /// </summary>
    private void StopInfoUpdate() => _dispatcherTimer?.Stop();

    #endregion

    #endregion

    #region Information builders

    /// <summary>
    ///  Основной метод для создания кнопок содержащих текущие показатели системы
    /// </summary>
    private async Task InfoCpuSectionGridBuilder()
    {
        try
        {
            InfoMainCPUFreqGrid.RowDefinitions.Clear();
            InfoMainCPUFreqGrid.ColumnDefinitions.Clear();

            // Количество видеокарт
            var gpuCounter = GetSystemInfo.GetGpuNames().Count;

            var threads = _numberOfLogicalProcessors;

            // Определяем количество элементов (coreCounter) в зависимости от выбранной секции
            var coreCounter = _selectedGroup switch
            {
                // Секция процессора или P-States
                0 or 5 => _numberOfCores > 2
                    ? _numberOfCores // Если ядер больше 2, используем количество ядер
                    : infoCPUSectionComboBox.SelectedIndex == 0
                        ? threads // Если выбрано отображение частоты, используем логические процессоры
                        : _numberOfCores, // Иначе используем количество ядер
                // Секция GFX
                1 => gpuCounter, // Используем количество видеокарт
                // Секция RAM
                2 => RamModel.Text.Split('/').Length, // Используем количество плат ОЗУ
                // Секция 3
                3 => 4, // Фиксированное значение для секции 3
                // Другие секции
                _ => 1 // По умолчанию 1 элемент
            };

            // Если количество ядер больше 2, обновляем threads
            if (_numberOfCores > 2)
            {
                threads = coreCounter;
            }

            // Определяем количество строк и столбцов для сетки
            var rowCount = threads / 2 > 4 ? 4 : threads / 2;
            if (threads % 2 != 0 || threads == 2)
            {
                rowCount++; // Добавляем дополнительную строку, если количество элементов нечётное или равно 2
            }

            // Добавляем строки и столбцы в сетку
            for (var i = 0; i < rowCount; i++)
            {
                InfoMainCPUFreqGrid.RowDefinitions.Add(new RowDefinition());
                InfoMainCPUFreqGrid.ColumnDefinitions.Add(new ColumnDefinition());
            }

            // Заполняем сетку кнопками
            for (var j = 0; j < InfoMainCPUFreqGrid.RowDefinitions.Count; j++)
            {
                for (var f = 0; f < InfoMainCPUFreqGrid.ColumnDefinitions.Count; f++)
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
                        0 or 5 => _numberOfCores > 2
                            ? _numberOfCores - coreCounter // Если ядер больше 2, используем оставшиеся ядра
                            : infoCPUSectionComboBox.SelectedIndex == 0
                                ? _numberOfLogicalProcessors -
                                  coreCounter // Если выбрано отображение частоты, используем логические процессоры
                                : _numberOfCores - coreCounter, // Иначе используем оставшиеся ядра
                        // Секция GFX
                        1 => gpuCounter - coreCounter, // Используем оставшиеся видеокарты
                        // Секция RAM
                        2 => RamModel.Text.Split('/').Length - coreCounter, // Используем оставшиеся платы ОЗУ
                        // Секция 3
                        3 => 4 - coreCounter, // Фиксированное значение для секции 3
                        // Другие секции
                        _ => 0 // По умолчанию 0
                    };

                    // Создаём кнопку для текущего элемента
                    var elementButton = CreateElementButton(currCore, _selectedGroup, _numberOfCores, RamModel.Text);

                    // Добавляем кнопку в сетку
                    Grid.SetRow(elementButton, j);
                    Grid.SetColumn(elementButton, f);
                    InfoMainCPUFreqGrid.Children.Add(elementButton);

                    // Уменьшаем счётчик элементов
                    coreCounter--;
                }
            }
        }
        catch (Exception e)
        {
            // Логируем ошибку, если что-то пошло не так
            await LogHelper.TraceIt_TraceError(e);
        }
    }

    /// <summary> 
    ///  Создаёт кнопки отображающие текущие показатели 
    /// </summary>
    private static Grid
        CreateElementButton(int currCore, int selectedGroup, int numberOfCores,
            string ramModelText)
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
                                    0 or 5 => currCore < numberOfCores
                                        ? "InfoCPUCore".GetLocalized() // Если это ядро, используем "Ядро"
                                        : "InfoCPUThread".GetLocalized(), // Иначе используем "Поток"
                                    // Секция GFX
                                    1 => "InfoGPUName".GetLocalized(), // Используем "Видеокарта"
                                    // Секция RAM
                                    2 => ramModelText.Contains('*')
                                        ? ramModelText.Split('*')[1]
                                            .Replace("bit", "") // Если есть информация о разрядности, используем её
                                        : "64", // По умолчанию 64 бита
                                    // Секция 3
                                    3 => currCore switch
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
    ///  Обработчик переключения режима отображения информации о процессоре
    /// </summary>
    private async void CpuSection_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded)
        {
            return;
        }

        try
        {
            InfoMainCPUFreqGrid.Children.Clear();
            await InfoCpuSectionGridBuilder();
        }
        catch (Exception ex)
        {
            await LogHelper.TraceIt_TraceError(ex);
        }
    }

    /// <summary>
    ///  Обработчик переключения режима RTSS
    /// </summary>
    private async void RtssButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (infoRTSSButton.IsChecked == false)
            {
                RtssHandler.ResetOsdText();
            }
            else
            {
                Info_RTSSTeacherTip.IsOpen = true;
                await Task.Delay(3000);
                Info_RTSSTeacherTip.IsOpen = false;
            }

            AppSettings.RtssMetricsEnabled = infoRTSSButton.IsChecked == true;
            AppSettings.SaveSettings();
        }
        catch (Exception ex)
        {
            await LogHelper.TraceIt_TraceError(ex);
        }
    }

    /// <summary>
    ///  Обработчик переключения режима TrayMon
    /// </summary>
    private void NiIconsButton_Click(object sender, RoutedEventArgs e) => AppSettings.NiIconsEnabled = infoNiIconsButton.IsChecked == true;

    #region Flyouts and Banner Buttons handlers

    /// <summary>
    ///  Обработчик выбора отображения другой группы сенсоров
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
                    InfoMainCPUFreqGrid.Children.Clear();

                    if (selectedGroup == 5 && infoCPUSectionComboBox.SelectedIndex != 0)
                    {
                        infoCPUSectionComboBox.SelectedIndex = 0;
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
    ///  Обработчик отображения кнопки увеличения графика
    /// </summary>
    private void BannerButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button bannerButton)
        {
            var prefix = bannerButton.Name.Replace("BannerButton", "");

            var flyout = (Flyout)FindName(prefix + "Flyout");
            var expandIcon = (FontIcon)FindName(prefix + "ExpandFontIcon");
            var expandButton = (Button)FindName(prefix + "ExpandButton");

            if (bannerButton.IsPointerOver)
            {
                expandIcon.Glyph = "\uF0D8";
                expandButton.Visibility = Visibility.Visible;

                foreach (var button in _allExpandButtons)
                {
                    if (button != expandButton)
                    {
                        button.Visibility = Visibility.Collapsed;
                    }
                }
            }
            else
            {
                if (flyout.IsOpen)
                {
                    expandButton.Visibility = Visibility.Visible;
                    expandIcon.Glyph = "\uF0D7";
                }
                else
                {
                    expandButton.Visibility = Visibility.Collapsed;
                }
            }
        }
    }

    /// <summary>
    ///  Обработчик открытия Flyout для увеличения графика
    /// </summary>
    private void Flyout_Opening(object sender, object e)
    {
        if (sender is FlyoutBase flyout && flyout.Target is FrameworkElement target)
        {
            var prefix = target.Name.Replace("ExpandButton", "");

            var polygon = (Polygon)FindName(prefix + "BigBannerPolygon");
            var expandIcon = (FontIcon)FindName(prefix + "ExpandFontIcon");
            var expandButton = (Button)FindName(prefix + "ExpandButton");

            polygon.Points.Clear();
            polygon.Points.Add(StartPoint);

            if (((Flyout)flyout).IsOpen)
            {
                expandIcon.Glyph = "\uF0D7";
                expandButton.Visibility = Visibility.Visible;
            }
            else
            {
                expandButton.Visibility = Visibility.Collapsed;
                expandIcon.Glyph = "\uF0D8";
            }
        }
    }

    #endregion

    #endregion
}