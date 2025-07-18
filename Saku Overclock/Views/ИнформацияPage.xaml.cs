using System.Management;
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
using Saku_Overclock.Services;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.ViewModels;
using ZenStates.Core;
using Brush = Microsoft.UI.Xaml.Media.Brush;
using Saku_Overclock.Wrappers;
using static ZenStates.Core.Cpu;

namespace Saku_Overclock.Views;

public sealed partial class ИнформацияPage
{
    // ReSharper disable once InconsistentNaming
    private readonly IAppSettingsService AppSettings = App.GetService<IAppSettingsService>(); // Настройки приложения
    private List<ManagementObject>? _cachedGpuList; // Кешированный лист GPU 

    public class MinMax // Класс для хранения минимальных и максимальных значений Ni-Icons
    {
        public double Min;
        public double Max;
    }

    private double _busyRam; // Текущее использование ОЗУ и всего ОЗУ
    private double _totalRam;
    private bool _loaded; // Страница загружена
    private bool _doNotTrackBattery; // Флаг не использования батареи 
    private readonly List<InfoPageCPUPoints> _cpuPointer = []; // Лист графика использования процессора
    private readonly List<InfoPageCPUPoints> _gpuPointer = []; // Лист графика частоты графического процессора
    private readonly List<InfoPageCPUPoints> _ramPointer = []; // Лист графика занятой ОЗУ
    private readonly List<InfoPageCPUPoints> _vrmPointer = []; // Лист графика тока VRM
    private readonly List<InfoPageCPUPoints> _batPointer = []; // Лист графика зарядки батареи
    private readonly List<InfoPageCPUPoints> _pstPointer = []; // Лист графика изменения P-State
    private readonly List<double> _psTatesList = [0, 0, 0]; // Лист с информацией о P-State
    private static readonly Point _maxedPoint = new(65, 54);
    private static readonly Point _startPoint = new(-2, 54);
    private static readonly Point _zeroPoint = new(0, 0);
    private static readonly CornerRadius _defaultCornerRadius = new(10);
    private double
        _maxGfxClock =
            0.1; // Максимальная частота графического процессора, используется для графика частоты графического процессора

    private double _maxBatRate = 0.1d; // Максимальная мощность зарядки, используется для графика зарядки батареи
    private Brush? _transparentBrush; // Прозрачная кисть, используется для кнопок выбора баннера
    private Brush? _selectedBrush; // Кисть цвета выделенной кнопки, используется для кнопок выбора баннера

    private Brush?
        _selectedBorderBrush; // Кисть цвета границы выделенной кнопки, используется для кнопок выбора баннера

    private int _selectedGroup; // Текущий выбранный баннер, используется для кнопок выбора баннера
    private bool _isAppInTray; // Флаг приложения в трее, чтобы не обновлять значения и не тратить ресурсы ноутбука
    private string _cpuName = "Unknown"; // Название процессора в системе
    private string _gpuName = "Unknown"; // Название графического процессора в системе
    private string _ramName = "Unknown"; // Название ОЗУ в системе
    private string? _batName = "Unknown"; // Название батареи в системе
    private int _numberOfCores; // Количество ядер
    private int _numberOfLogicalProcessors; // Количество потоков
    private DispatcherTimer? _dispatcherTimer; // Таймер для автообновления информации
    private readonly IBackgroundDataUpdater _dataUpdater; // Фоновое обновление информации
    private SensorsInformation? _sensorsInformation; // Информация с датчиков
    private Cpu? _cpu; // Инициализация ZenStates Core

    public ИнформацияPage()
    {
        _numberOfLogicalProcessors = 0;
        App.GetService<ИнформацияViewModel>();
        InitializeComponent();
        InitializeZenStates();
        _dataUpdater = App.BackgroundUpdater!;
        _dataUpdater.DataUpdated += OnDataUpdated;
        Loaded += ИнформацияPage_Loaded;
        Unloaded += ИнформацияPage_Unloaded;
    }

    #region Initialization

    #region Get-Info voids

    private void OnDataUpdated(object? sender, SensorsInformation info) => _sensorsInformation = info;

    private void InitializeZenStates()
    {
        try
        {
            _cpu ??= CpuSingleton.GetInstance(); // Загрузить ZenStates Core
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex.ToString());
            App.GetService<IAppNotificationService>()
                .Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory)); // Вывести ошибку
        }
    }

    private void SetThemeAccentTextForeground()
    {
        if (tbCPUFreq.Foreground is SolidColorBrush brush)
        {
            if (brush.Color == Color.FromArgb(228, 0, 0, 0))
            {
                infoACPUUsageBannerPolygonText.Foreground = brush;
                infoACPUUsageBigBannerPolygonText.Foreground = brush;
                infoAGPUUsageBannerPolygonText.Foreground = brush;
                infoAGPUUsageBigBannerPolygonText.Foreground = brush;
                infoAVRMUsageBannerPolygonText.Foreground = brush;
                infoAVRMUsageBigBannerPolygonText.Foreground = brush;
                infoARAMUsageBannerPolygonText.Foreground = brush;
                infoARAMUsageBigBannerPolygonText.Foreground = brush;
                infoABATUsageBannerPolygonText.Foreground = brush;
                infoABATUsageBigBannerPolygonText.Foreground = brush;
                infoAPSTUsageBannerPolygonText.Foreground = brush;
                infoAPSTUsageBigBannerPolygonText.Foreground = brush;
            }
        }
    }

    public static async Task<int> GetCpuCoresAsync()
    {
        return await Task.Run(() =>
        {
            var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Processor");
            try
            {
                foreach (var queryObj in searcher.Get().Cast<ManagementObject>())
                {
                    var numberOfCores = Convert.ToInt32(queryObj["NumberOfCores"]);
                    var numberOfLogicalProcessors = Convert.ToInt32(queryObj["NumberOfLogicalProcessors"]);
                    var l2Size = Convert.ToDouble(queryObj["L2CacheSize"]) / 1024;

                    return numberOfLogicalProcessors == numberOfCores
                        ? numberOfCores
                        : int.Parse(GetSystemInfo.GetBigLITTLE(numberOfCores, l2Size));
                }
            }
            catch
            {
                return 0; // Возвращаем 0 в случае ошибки
            }

            return 0; // Возвращаем 0, если данные не были найдены
        });
    }

    private async Task GetCpuInfo()
    {
        try
        {
            // Переменные для хранения данных
            string name = string.Empty, description = string.Empty, baseClock = string.Empty;
            double l3Size = 0;

            // Асинхронное выполнение WMI-запросов и первичных операций
            var cpuInfoTask = Task.Run(() =>
            {
                var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Processor");
                foreach (var queryObj in searcher.Get().Cast<ManagementObject>())
                {
                    name = queryObj["Name"]?.ToString() ?? "";
                    description = queryObj["Description"]?.ToString() ?? "";
                    _numberOfCores = Convert.ToInt32(queryObj["NumberOfCores"]);
                    _numberOfLogicalProcessors = Convert.ToInt32(queryObj["NumberOfLogicalProcessors"]);
                    _ = Convert.ToDouble(queryObj["L2CacheSize"]) / 1024;
                    l3Size = Convert.ToDouble(queryObj["L3CacheSize"]) / 1024;
                    baseClock = queryObj["MaxClockSpeed"]?.ToString() ?? "";
                }
            });

            if (_numberOfCores == 0 && _cpu != null)
            {
                _numberOfCores = (int)_cpu.info.topology.cores;
            }

            if (_numberOfLogicalProcessors == 0 && _cpu != null)
            {
                _numberOfLogicalProcessors = (int)_cpu.info.topology.logicalCores;
            }

            // Обновление UI в основном потоке
            await InfoCpuSectionGridBuilder();

            _cpuName = _cpu == null ? name : _cpu.info.cpuName;
            tbProcessor.Text = _cpuName;

            var gpuNameTask = Task.Run(() =>
            {
                var gpuName = GetSystemInfo.GetGPUName(0) ?? "";
                return gpuName.Contains("AMD") ? gpuName : GetSystemInfo.GetGPUName(1) ?? gpuName;
            });

            // Асинхронное выполнение других операций
            var instructionSetsTask = Task.Run(GetSystemInfo.InstructionSets);
            var l1CacheTask = Task.Run(() => CalculateCacheSizeAsync(GetSystemInfo.CacheLevel.Level1));
            var l2CacheTask = Task.Run(() => CalculateCacheSizeAsync(GetSystemInfo.CacheLevel.Level2));
            var codeNameTask = Task.Run(GetSystemInfo.Codename);

            // Ожидание выполнения всех задач
            await Task.WhenAll(cpuInfoTask, gpuNameTask, instructionSetsTask, l1CacheTask, l2CacheTask, codeNameTask);

            // Получение результатов
            var gpuName = gpuNameTask.Result;
            var instructionSets = instructionSetsTask.Result;
            var l1Cache = l1CacheTask.Result;
            var l2Cache = l2CacheTask.Result;
            var codeName = codeNameTask.Result;
            tbCaption.Text = description;

            if (!string.IsNullOrEmpty(codeName))
            {
                tbCodename.Text = codeName;
            }
            else
            {
                try
                {
                    tbCodename.Text = $"{_cpu?.info.codeName}";
                }
                catch
                {
                    tbCodename.Visibility = Visibility.Collapsed;
                    tbCode.Visibility = Visibility.Collapsed;
                }

            }

            tbCores.Text = _numberOfLogicalProcessors == _numberOfCores
                ? _numberOfCores.ToString()
                : GetSystemInfo.GetBigLITTLE(_numberOfCores, l2Cache);
            _gpuName = gpuName;
            try
            {
                tbSMU.Text = _cpu?.systemInfo.GetSmuVersionString();
            }
            catch
            {
                tbSMU.Visibility = Visibility.Collapsed;
                infoSMU.Visibility = Visibility.Collapsed;
            }

            tbThreads.Text = _numberOfLogicalProcessors.ToString();
            tbL3Cache.Text = $"{l3Size:0.##} MB";
            tbL1Cache.Text = $"{l1Cache:0.##} MB";
            tbL2Cache.Text = $"{l2Cache:0.##} MB";
            tbBaseClock.Text = $"{baseClock} MHz";
            tbInstructions.Text = instructionSets;
        }
        catch (Exception ex)
        {
            Console.WriteLine("An error occurred: " + ex.Message);
        }
    }


    private static async Task<double> CalculateCacheSizeAsync(GetSystemInfo.CacheLevel level)
    {
        return await Task.Run(() =>
        {
            var sum = 0u;
            foreach (var number in GetSystemInfo.GetCacheSizes(level))
            {
                sum += number;
            }

            return sum / 1024.0;
        });
    }

    private async void GetBatInfoAsync()
    {
        try
        {
            if (BATBannerButton.Visibility == Visibility.Collapsed)
            {
                return;
            }

            if (_sensorsInformation == null)
            {
                await Task.Delay(100);
                GetBatInfoAsync();
                return;
            }

            if (_sensorsInformation!.BatteryUnavailable)
            {
                BATBannerButton.Visibility = Visibility.Collapsed;
                _doNotTrackBattery = true;
            }

            try
            {
                // Обновление UI
                tbBAT.Text = _sensorsInformation!.BatteryPercent;
                tbBATState.Text = _sensorsInformation!.BatteryState;
                tbBATHealth.Text = _sensorsInformation!.BatteryHealth;
                tbBATCycles.Text = _sensorsInformation!.BatteryCycles;
                tbBATCapacity.Text = _sensorsInformation!.BatteryCapacity;
                tbBATChargeRate.Text = _sensorsInformation!.BatteryChargeRate;
                _batName = _sensorsInformation!.BatteryName;
            }
            catch
            {
                // При ошибке скрываем элементы и отмечаем, что батарея некорректно отслеживается
                _doNotTrackBattery = true;
                if (BATBannerButton.Visibility != Visibility.Collapsed)
                {
                    BATBannerButton.Visibility = Visibility.Collapsed;
                }
            }
        }
        catch (Exception ex)
        {
            await LogHelper.LogError(ex.ToString());
        }
    }

    private async Task GetRamInfo()
    {
        double capacity = 0;
        var speed = 0;
        var type = 0;
        var width = 0;
        var slots = 0;
        var producer = string.Empty;
        var model = string.Empty;

        try
        {
            var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_PhysicalMemory");
            await Task.Run(() =>
            {
                foreach (var queryObj in searcher.Get().Cast<ManagementObject>())
                {
                    if (producer == "")
                    {
                        producer = queryObj["Manufacturer"].ToString();
                    }
                    else if (!producer!.Contains(queryObj["Manufacturer"].ToString()!))
                    {
                        producer = $"{producer}/{queryObj["Manufacturer"]}";
                    }

                    if (model == "")
                    {
                        model = queryObj["PartNumber"].ToString();
                    }
                    else if (!model!.Contains(queryObj["PartNumber"].ToString()!))
                    {
                        model = $"{model}/{queryObj["PartNumber"]}";
                    }

                    capacity += Convert.ToDouble(queryObj["Capacity"]);
                    speed = Convert.ToInt32(queryObj["ConfiguredClockSpeed"]);
                    type = Convert.ToInt32(queryObj["SMBIOSMemoryType"]);
                    width += Convert.ToInt32(queryObj["DataWidth"]);
                    slots++;
                }
            });
            capacity = capacity / 1024 / 1024 / 1024;
            var ddrType = type switch
            {
                20 => "DDR",
                21 => "DDR2",
                24 => "DDR3",
                26 => "DDR4",
                30 => "LPDDR4",
                34 => "DDR5",
                35 => "LPDDR5",
                36 => "LPDDR5X",
                _ => $"Unknown ({type})"
            };
            _ramName = $"{capacity} GB {ddrType} @ {speed} MT/s";
            tbRAM.Text = speed + "MT/s";
            tbRAMProducer.Text = producer;
            tbRAMModel.Text = model.Replace(" ", null);
            tbWidth.Text = $"{width} bit";
            tbSlots.Text = $"{slots} * {width / slots} bit";
            tbTCL.Text = Utils.GetBits(_cpu!.ReadDword(0 | 0x50204), 0, 6) + "T";
            tbTRCDWR.Text = Utils.GetBits(_cpu!.ReadDword(0 | 0x50204), 24, 6) + "T";
            tbTRCDRD.Text = Utils.GetBits(_cpu!.ReadDword(0 | 0x50204), 16, 6) + "T";
            tbTRAS.Text = Utils.GetBits(_cpu!.ReadDword(0 | 0x50204), 8, 7) + "T";
            tbTRP.Text = Utils.GetBits(_cpu!.ReadDword(0 | 0x50208), 16, 6) + "T";
            tbTRC.Text = Utils.GetBits(_cpu!.ReadDword(0 | 0x50208), 0, 8) + "T";
        }
        catch (Exception ex)
        {
            await LogHelper.TraceIt_TraceError(ex.ToString());
        }
    }

    #endregion

    #region P-State voids

    private static void CalculatePstateDetails(uint eax,
        out uint cpuDfsId, out uint cpuFid)
    {
        cpuDfsId = (eax >> 8) & 0x3F;
        cpuFid = eax & 0xFF;
    }

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
                    LogHelper.TraceIt_TraceError(ex.ToString());
                }

                CalculatePstateDetails(eax, out var cpuDfsId, out var cpuFid);
                var textBlock = (TextBlock)InfoPSTSectionMetrics.FindName($"tbPSTP{i}");
                if (cpuFid != 0)
                {
                    textBlock.Text =
                        $"FID: {Convert.ToString(cpuFid, 10)}/DID: {Convert.ToString(cpuDfsId, 10)}\n{cpuFid * 25 / (cpuDfsId * 12.5) / 10}" +
                        "infoAGHZ".GetLocalized();
                }
                else
                {
                    textBlock.Text = "Info_PowerSumInfo_DisabledPState".GetLocalized();
                }

                _psTatesList[i] = cpuFid * 25 / (cpuDfsId * 12.5) / 10;
            }
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex.ToString());
        }
    }

    #endregion

    #region Page-related voids

    private void Window_VisibilityChanged(object sender, WindowVisibilityChangedEventArgs args)
    {
        if (args.Visible)
        {
            _dispatcherTimer?.Start();
            _isAppInTray = false;
        }
        else
        {
            if (infoRTSSButton.IsChecked == false && AppSettings.NiIconsEnabled == false)
            {
                _dispatcherTimer?.Stop();
                _isAppInTray = true;
            }
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        StartInfoUpdate();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        StopInfoUpdate();
    }

    private async void ИнформацияPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _loaded = true;
            _selectedBrush = CPUBannerButton.Background;
            _selectedBorderBrush = CPUBannerButton.BorderBrush;
            _transparentBrush = GPUBannerButton.Background;
            SetThemeAccentTextForeground();
            GetBatInfoAsync();
            await GetCpuInfo();
            await GetRamInfo();
            ReadPstate();
            if (CPUBannerButton.Shadow != new ThemeShadow())
            {
                CPUBannerButton.Shadow ??= new ThemeShadow();
                GPUBannerButton.Shadow = null;
                RAMBannerButton.Shadow = null;
                BATBannerButton.Shadow = null;
                PSTBannerButton.Shadow = null;
                VRMBannerButton.Shadow = null;
            }

            try
            {
                infoRTSSButton.IsChecked = AppSettings.RtssMetricsEnabled;
                infoNiIconsButton.IsChecked = AppSettings.NiIconsEnabled;
            }
            catch (Exception exception)
            {
                await LogHelper.TraceIt_TraceError(exception.ToString());
            }
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception.ToString());
        }
    }

    private void ИнформацияPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _dataUpdater.DataUpdated -= OnDataUpdated;

        try
        {
            infoRTSSButton.IsChecked = false;
            _dispatcherTimer?.Stop();
            RtssHandler.ResetOsdText();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex.ToString());
        }
    }

    #endregion

    #region Info Update voids
    
    private void UpdateInfo()
    {
        try
        {
            if (!_loaded)
            {
                return;
            }

            if (!_isAppInTray)
            {
                if (_selectedGroup != 0)
                {
                    infoCPUSectionComboBox.Visibility = Visibility.Collapsed;
                    InfoCPUComboBoxBorderSharedShadow_Element.Visibility = Visibility.Collapsed;
                    switch (_selectedGroup)
                    {
                        case 1:
                            // Показать свойства видеокарты

                            infoCPUSectionName.Text = "InfoGPUSectionName".GetLocalized();
                            InfoCPUSectionMetrics.Visibility = Visibility.Collapsed;
                            InfoGPUSectionMetrics.Visibility = Visibility.Visible;
                            InfoRAMSectionMetrics.Visibility = Visibility.Collapsed;
                            InfoVRMSectionMetrics.Visibility = Visibility.Collapsed;
                            infoRAMMAINSection.Visibility = Visibility.Collapsed;
                            infoCPUMAINSection.Visibility = Visibility.Collapsed;
                            InfoBATSectionMetrics.Visibility = Visibility.Collapsed;
                            InfoPSTSectionMetrics.Visibility = Visibility.Collapsed;
                            tbProcessor.Text = _gpuName;
                            break;
                        case 2:
                            // Свойства ОЗУ

                            infoCPUSectionName.Text = "InfoRAMSectionName".GetLocalized();
                            InfoCPUSectionMetrics.Visibility = Visibility.Collapsed;
                            InfoGPUSectionMetrics.Visibility = Visibility.Collapsed;
                            InfoRAMSectionMetrics.Visibility = Visibility.Visible;
                            InfoVRMSectionMetrics.Visibility = Visibility.Collapsed;
                            tbProcessor.Text = _ramName;
                            infoRAMMAINSection.Visibility = Visibility.Visible;
                            infoCPUMAINSection.Visibility = Visibility.Collapsed;
                            InfoBATSectionMetrics.Visibility = Visibility.Collapsed;
                            InfoPSTSectionMetrics.Visibility = Visibility.Collapsed;
                            break;
                        case 3:
                            //Зона VRM 
                            infoCPUSectionName.Text = "VRM";
                            InfoCPUSectionMetrics.Visibility = Visibility.Collapsed;
                            InfoGPUSectionMetrics.Visibility = Visibility.Collapsed;
                            InfoRAMSectionMetrics.Visibility = Visibility.Collapsed;
                            InfoVRMSectionMetrics.Visibility = Visibility.Visible;
                            tbProcessor.Text = _cpuName;
                            infoRAMMAINSection.Visibility = Visibility.Collapsed;
                            infoCPUMAINSection.Visibility = Visibility.Visible;
                            InfoBATSectionMetrics.Visibility = Visibility.Collapsed;
                            InfoPSTSectionMetrics.Visibility = Visibility.Collapsed;
                            break;
                        case 4:
                            // Батарея
                            infoCPUSectionName.Text = "InfoBatteryName".GetLocalized();
                            InfoCPUSectionMetrics.Visibility = Visibility.Collapsed;
                            InfoGPUSectionMetrics.Visibility = Visibility.Collapsed;
                            InfoRAMSectionMetrics.Visibility = Visibility.Collapsed;
                            InfoVRMSectionMetrics.Visibility = Visibility.Collapsed;
                            tbProcessor.Text = _batName;
                            infoRAMMAINSection.Visibility = Visibility.Collapsed;
                            infoCPUMAINSection.Visibility = Visibility.Collapsed;
                            InfoBATSectionMetrics.Visibility = Visibility.Visible;
                            InfoPSTSectionMetrics.Visibility = Visibility.Collapsed;
                            break;
                        case 5:
                            // P-States
                            infoCPUSectionName.Text = "P-States";
                            InfoCPUSectionMetrics.Visibility = Visibility.Collapsed;
                            InfoGPUSectionMetrics.Visibility = Visibility.Collapsed;
                            InfoRAMSectionMetrics.Visibility = Visibility.Collapsed;
                            InfoVRMSectionMetrics.Visibility = Visibility.Collapsed;
                            tbProcessor.Text = _cpuName;
                            infoRAMMAINSection.Visibility = Visibility.Collapsed;
                            infoCPUMAINSection.Visibility = Visibility.Visible;
                            InfoBATSectionMetrics.Visibility = Visibility.Collapsed;
                            InfoPSTSectionMetrics.Visibility = Visibility.Visible;
                            break;
                    }
                }
                else
                {
                    infoCPUMAINSection.Visibility = Visibility.Visible;
                    infoRAMMAINSection.Visibility = Visibility.Collapsed;
                    infoCPUSectionComboBox.Visibility = !RyzenadjProvider.IsPhysicallyUnavailable
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                    InfoCPUComboBoxBorderSharedShadow_Element.Visibility = RyzenadjProvider.IsPhysicallyUnavailable
                        ? Visibility.Collapsed
                        : Visibility.Visible;
                    InfoCPUSectionMetrics.Visibility = Visibility.Visible;
                    InfoVRMSectionMetrics.Visibility = Visibility.Collapsed;
                    InfoGPUSectionMetrics.Visibility = Visibility.Collapsed;
                    InfoRAMSectionMetrics.Visibility = Visibility.Collapsed;
                    InfoBATSectionMetrics.Visibility = Visibility.Collapsed;
                    InfoPSTSectionMetrics.Visibility = Visibility.Collapsed;
                    //Скрыть лишние элементы 
                    infoCPUSectionName.Text = "InfoCPUSectionName".GetLocalized();
                    tbProcessor.Text = _cpuName;
                }


                if (RyzenadjProvider.IsPhysicallyUnavailable)
                {
                    GPUBannerButton.Visibility = Visibility.Collapsed;
                    InfoCPUSectionGetInfoSelectIndexesButton.Visibility = Visibility.Collapsed;
                }

                InfoACPUBannerPolygon.Points.Remove(_maxedPoint);
                InfoACPUBigBannerPolygon.Points.Remove(_maxedPoint);
                InfoAGPUBannerPolygon.Points.Remove(_maxedPoint);
                InfoAGPUBigBannerPolygon.Points.Remove(_maxedPoint);
                InfoARAMBannerPolygon.Points.Remove(_maxedPoint);
                InfoARAMBigBannerPolygon.Points.Remove(_maxedPoint);
                InfoAVRMBannerPolygon.Points.Remove(_maxedPoint);
                InfoAVRMBigBannerPolygon.Points.Remove(_maxedPoint);
                InfoABATBannerPolygon.Points.Remove(_maxedPoint);
                InfoABATBigBannerPolygon.Points.Remove(_maxedPoint);
                InfoAPSTBannerPolygon.Points.Remove(_maxedPoint);
                InfoAPSTBigBannerPolygon.Points.Remove(_maxedPoint);

                var currBatRate = 0d;
                var beforeMaxBatRate = 0;
                if (!_doNotTrackBattery)
                {
                    var batteryRate = 0d;
                    try
                    {
                        if (_sensorsInformation?.BatteryChargeRate?.Replace("W", string.Empty) != "")
                        {
                            batteryRate =
                                Convert.ToDouble(_sensorsInformation?.BatteryChargeRate?.Replace("W", string.Empty));
                        }
                        else
                        {
                            _doNotTrackBattery = true;
                        }
                    }
                    catch
                    {
                        _doNotTrackBattery = true;
                    }

                    tbBATChargeRate.Text = _sensorsInformation?.BatteryChargeRate;
                    tbBAT.Text = _sensorsInformation?.BatteryPercent;
                    var trueBatLifeTime = 0d;
                    if (_sensorsInformation?.BatteryLifeTime != null)
                    {
                        trueBatLifeTime = _sensorsInformation.BatteryLifeTime;
                    }

                    if (trueBatLifeTime < 0)
                    {
                        tbBATTime.Text = "InfoBatteryAC".GetLocalized(); // Считаем, что устройство питается от сети
                    }
                    else
                    {
                        var ts = TimeSpan.FromSeconds(trueBatLifeTime); // Преобразуем секунды в TimeSpan
                        var parts = new List<string>();
                        if ((int)ts.TotalHours > 0)
                        {
                            parts.Add($"{(int)ts.TotalHours}h"); // Добавляем часы, если они есть
                        }

                        if (ts.Minutes > 0)
                        {
                            parts.Add($"{ts.Minutes}m"); // Добавляем минуты, если они есть
                        }

                        if (ts.Seconds > 0 || parts.Count == 0)
                        {
                            parts.Add(
                                $"{ts.Seconds}s"); // Добавляем секунды – если других компонент нет, или если секунды ненулевые
                        }

                        tbBATTime.Text = string.Join(" ", parts);
                    }

                    InfoBATUsage.Text = tbBAT.Text + " " + tbBATChargeRate.Text + "\n" + tbBATTime.Text;
                    infoIBATUsageBigBanner.Text = InfoBATUsage.Text;
                    infoABATUsageBannerPolygonText.Text = tbBATChargeRate.Text;
                    infoABATUsageBigBannerPolygonText.Text = tbBATChargeRate.Text;
                    tbBATState.Text = _sensorsInformation?.BatteryState;
                    currBatRate = Math.Abs(batteryRate);
                    beforeMaxBatRate = (int)_maxBatRate;
                    if (currBatRate > _maxBatRate)
                    {
                        _maxBatRate = currBatRate;
                    }
                }

                if (_sensorsInformation == null)
                {
                    return;
                }

                if (_sensorsInformation.CpuStapmLimit == 0)
                {
                    tbStapmL.Text = "Info_PowerSumInfo_Disabled".GetLocalized();
                }
                else
                {
                    tbStapmL.Text = Math.Round(_sensorsInformation.CpuStapmValue, 3) + "W/" +
                                    (int)_sensorsInformation.CpuStapmLimit + "W";
                }

                tbActualL.Text = Math.Round(_sensorsInformation.CpuFastValue, 3) + "W/" +
                                 (int)_sensorsInformation.CpuFastLimit + "W";
                tbAclualPowerL.Text = tbActualL.Text;
                if (_sensorsInformation.CpuSlowLimit == 0)
                {
                    tbAVGL.Text = "Info_PowerSumInfo_Disabled".GetLocalized();
                }
                else
                {
                    tbAVGL.Text = Math.Round(_sensorsInformation.CpuSlowValue, 3) + "W/" +
                                  (int)_sensorsInformation.CpuSlowLimit + "W";
                }

                tbFast.Text = Math.Round(_sensorsInformation.CpuStapmTimeValue, 3) + "S";
                tbSlow.Text = Math.Round(_sensorsInformation.CpuSlowTimeValue, 3) + "S";

                tbAPUL.Text = Math.Round(_sensorsInformation.ApuSlowValue, 3) + "W/" +
                              (int)_sensorsInformation.ApuSlowLimit + "W";

                tbVRMTDCL.Text = Math.Round(_sensorsInformation.VrmTdcValue, 3) + "A/" +
                                 (int)_sensorsInformation.VrmTdcLimit + "A";
                tbSOCTDCL.Text = Math.Round(_sensorsInformation.SocTdcValue, 3) + "A/" +
                                 (int)_sensorsInformation.SocTdcLimit + "A";
                tbVRMEDCL.Text = Math.Round(_sensorsInformation.VrmEdcValue, 3) + "A/" +
                                 (int)_sensorsInformation.VrmEdcLimit + "A";
                tbVRMEDCVRML.Text = tbVRMEDCL.Text;
                infoVRMUsageBanner.Text = Math.Round(_sensorsInformation.VrmEdcValue, 3) +
                                          "A\n" + Math.Round(_sensorsInformation.CpuFastValue, 3) + "W";
                infoIVRMUsageBigBanner.Text = infoVRMUsageBanner.Text;
                infoAVRMUsageBannerPolygonText.Text =
                    Math.Round(_sensorsInformation.VrmEdcValue, 3) + "A";
                infoAVRMUsageBigBannerPolygonText.Text = infoAVRMUsageBannerPolygonText.Text;
                tbSOCEDCL.Text = Math.Round(_sensorsInformation.SocEdcValue, 3) + "A/" +
                                 (int)_sensorsInformation.SocEdcLimit + "A";
                tbSOCVOLT.Text = Math.Round(_sensorsInformation.SocVoltage, 3) == 0
                    ? Math.Round(_cpu!.powerTable.VDDCR_SOC, 3) + "V"
                    : Math.Round(_sensorsInformation.SocVoltage, 3) + "V";
                tbSOCPOWER.Text = Math.Round(_sensorsInformation.SocPower, 3) == 0
                    ? Math.Round(_cpu!.powerTable.VDDCR_SOC * 10, 3) + "W"
                    : Math.Round(_sensorsInformation.SocPower, 3) + "W";
                tbMEMCLOCK.Text = Math.Round(_sensorsInformation.MemFrequency, 3) == 0
                    ? Math.Round(_cpu!.powerTable.MCLK, 3) + "InfoFreqBoundsMHZ".GetLocalized()
                    : Math.Round(_sensorsInformation.MemFrequency, 3) + "InfoFreqBoundsMHZ".GetLocalized();
                tbFabricClock.Text = Math.Round(_sensorsInformation.FabricFrequency, 3) == 0
                    ? Math.Round(_cpu!.powerTable.FCLK, 3) + "InfoFreqBoundsMHZ".GetLocalized()
                    : Math.Round(_sensorsInformation.FabricFrequency, 3) + "InfoFreqBoundsMHZ".GetLocalized();
                var coreClk = 0d;
                var endtrace = 0;
                var coreVolt = 0d;
                var endtraced = 0;
                var maxFreq = 0.0d;
                var currentPstate = 4;
                for (uint f = 0; f < 16; f++)
                {
                    var getCurrFreq = _sensorsInformation.CpuFrequencyPerCore != null
                        ? _sensorsInformation.CpuFrequencyPerCore.Length > f
                            ? _sensorsInformation.CpuFrequencyPerCore[f]
                            : 0f
                        : 0f;
                    if (!double.IsNaN(getCurrFreq) && getCurrFreq > maxFreq)
                    {
                        maxFreq = getCurrFreq;
                    }

                    var currCore = infoCPUSectionComboBox.SelectedIndex switch
                    {
                        0 => getCurrFreq,
                        1 => _sensorsInformation.CpuVoltagePerCore != null
                            ? _sensorsInformation.CpuVoltagePerCore.Length > f
                                ? _sensorsInformation.CpuVoltagePerCore[f]
                                : 0f
                            : 0f,
                        2 => _sensorsInformation.CpuPowerPerCore != null
                            ? _sensorsInformation.CpuPowerPerCore.Length > f
                                ? _sensorsInformation.CpuPowerPerCore[f]
                                : 0f
                            : 0f,
                        3 => _sensorsInformation.CpuTemperaturePerCore != null
                            ? _sensorsInformation.CpuTemperaturePerCore.Length > f
                                ? _sensorsInformation.CpuTemperaturePerCore[f]
                                : 0f
                            : 0f,
                        _ => getCurrFreq
                    };
                    if (!double.IsNaN(currCore))
                    {
                        if (!InfoMainCPUFreqGrid.IsLoaded)
                        {
                            return;
                        }

                        var currText = (TextBlock)InfoMainCPUFreqGrid.FindName($"FreqButtonText_{f}");
                        if (currText != null)
                        {
                            if (_selectedGroup is 0 or 5)
                            {
                                currText.Text = infoCPUSectionComboBox.SelectedIndex switch
                                {
                                    0 => Math.Round(currCore, 3) + " " + "infoAGHZ".GetLocalized(),
                                    1 => Math.Round(currCore, 3) + "V",
                                    2 => Math.Round(currCore, 3) + "W",
                                    3 => Math.Round(currCore, 3) + "C",
                                    _ => Math.Round(currCore, 3) + " " + "infoAGHZ".GetLocalized()
                                };
                            }
                            else
                            {
                                switch (_selectedGroup)
                                {
                                    case 1:
                                        currText.Text = GetSystemInfo.GetGPUName((int)f);
                                        break;
                                    case 2:
                                    {
                                        var reject = 0;
                                        foreach (var element in tbRAMModel.Text.Split('/'))
                                        {
                                            if (reject == (int)f)
                                            {
                                                currText.Text = element;
                                            }

                                            reject++;
                                        }

                                        break;
                                    }
                                    case 3:
                                        currText.Text = f switch
                                        {
                                            0 =>
                                                $"{Math.Round(_sensorsInformation.VrmEdcValue, 3)}A/{Math.Round(_sensorsInformation.VrmEdcLimit, 3)}A",
                                            1 =>
                                                $"{Math.Round(_sensorsInformation.VrmTdcValue, 3)}A/{Math.Round(_sensorsInformation.VrmTdcLimit, 3)}A",
                                            2 =>
                                                $"{Math.Round(_sensorsInformation.SocEdcValue, 3)}A/{Math.Round(_sensorsInformation.SocEdcLimit, 3)}A",
                                            3 =>
                                                $"{Math.Round(_sensorsInformation.SocTdcValue, 3)}A/{Math.Round(_sensorsInformation.SocTdcLimit, 3)}A",
                                            _ => "0A"
                                        };
                                        break;
                                    case 4:
                                        currText.Text = _batName;
                                        break;
                                }
                            }
                        }

                        if (f < _numberOfCores)
                        {
                            if (getCurrFreq + 1.0f > 0 && getCurrFreq != 0 && getCurrFreq < 7)
                            {
                                coreClk += getCurrFreq;
                                endtrace += 1;
                            }
                        }
                    }

                    var currVolt = _sensorsInformation.CpuVoltagePerCore != null
                        ? _sensorsInformation.CpuVoltagePerCore.Length > f
                            ? _sensorsInformation.CpuVoltagePerCore[f]
                            : 0f
                        : 0f;
                    if (!double.IsNaN(currVolt) && currVolt != 0 && currVolt - -1.0f > 0 && currVolt < 1.7)
                    {
                        coreVolt += currVolt;
                        endtraced += 1;
                    }
                }

                if (endtrace != 0)
                {
                    tbCPUFreq.Text = Math.Round(coreClk / endtrace, 3) + " " + "infoAGHZ".GetLocalized();

                    if (Math.Round(coreClk / endtrace, 3) >= _psTatesList[2])
                    {
                        tbPST.Text = "P2";
                        infoAPSTUsageBannerPolygonText.Text = "P2";
                        infoAPSTUsageBigBannerPolygonText.Text = "P2";
                        currentPstate = 1;
                    }
                    else
                    {
                        tbPST.Text = "C1";
                        infoAPSTUsageBannerPolygonText.Text = "C1";
                        infoAPSTUsageBigBannerPolygonText.Text = "C1";
                        currentPstate = 0;
                    }

                    if (Math.Round(coreClk / endtrace, 3) >= _psTatesList[1])
                    {
                        tbPST.Text = "P1";
                        infoAPSTUsageBannerPolygonText.Text = "P1";
                        infoAPSTUsageBigBannerPolygonText.Text = "P1";
                        currentPstate = 2;
                    }

                    if (Math.Round(coreClk / endtrace, 3) >= _psTatesList[0])
                    {
                        tbPST.Text = "P0";
                        infoAPSTUsageBannerPolygonText.Text = "P0";
                        infoAPSTUsageBigBannerPolygonText.Text = "P0";
                        currentPstate = 3;
                    }

                    InfoPSTUsage.Text = tbPST.Text + "InfoPSTState".GetLocalized();
                    infoIPSTUsageBigBanner.Text = InfoPSTUsage.Text;
                }
                else
                {
                    tbCPUFreq.Text = "? " + "infoAGHZ".GetLocalized();
                }

                if (endtraced != 0)
                {
                    tbCPUVolt.Text = Math.Round(coreVolt / endtraced, 3) + "V";
                }
                else
                {
                    CpuVoltagePanel.Visibility = Visibility.Collapsed;
                    tbCPUVoltDesc.Visibility = Visibility.Collapsed;
                    tbCPUVolt.Visibility = Visibility.Collapsed;
                    tbCPUVolt.Text = "?V";
                }

                tbPSTFREQ.Text = tbCPUFreq.Text;
                var gfxClk = Math.Round(_sensorsInformation.ApuFrequency / 1000, 3);
                var gfxVolt = Math.Round(_sensorsInformation.ApuVoltage, 3);
                var gfxTemp = _sensorsInformation.ApuTemperature;
                var beforeMaxGfx = _maxGfxClock;
                if (_maxGfxClock < gfxClk)
                {
                    _maxGfxClock = gfxClk;
                }

                infoGPUUsageBanner.Text = gfxClk + " " + "infoAGHZ".GetLocalized() + "  " + Math.Round(gfxTemp, 0) +
                                          "C\n" + gfxVolt + "V";
                infoAGPUUsageBannerPolygonText.Text = gfxClk + "infoAGHZ".GetLocalized();
                tbGPUFreq.Text = infoAGPUUsageBannerPolygonText.Text;
                infoAGPUUsageBigBannerPolygonText.Text = infoAGPUUsageBannerPolygonText.Text;
                infoIGPUUsageBigBanner.Text = infoGPUUsageBanner.Text;
                tbGPUVolt.Text = gfxVolt + "V";
                var maxTemp = Math.Round(_sensorsInformation.CpuTempLimit, 3);
                tbCPUMaxL.Text = Math.Round(_sensorsInformation.CpuTempValue, 3) + "C/" + maxTemp +
                                 "C";
                tbCPUMaxTempL.Text = tbCPUMaxL.Text;
                tbCPUMaxTempVRML.Text = tbCPUMaxL.Text;
                var apuTemp = Math.Round(_sensorsInformation.ApuTempValue, 3);
                var apuTempLimit = Math.Round(_sensorsInformation.ApuTempLimit, 3);
                tbAPUMaxL.Text = (!double.IsNaN(apuTemp) && apuTemp > 0 ? apuTemp : Math.Round(gfxTemp, 3)) + "C/" +
                                 (!double.IsNaN(apuTempLimit) && apuTempLimit > 0 ? apuTempLimit : maxTemp) + "C";
                tbDGPUMaxL.Text = Math.Round(_sensorsInformation.DgpuTempValue, 3) + "C/" +
                                  Math.Round(_sensorsInformation.DgpuTempLimit, 3) + "C";
                var coreCpuUsage = Math.Round(_sensorsInformation.CpuUsage, 3);
                tbCPUUsage.Text = coreCpuUsage + "%";
                infoACPUUsageBannerPolygonText.Text = Math.Round(coreCpuUsage, 0) + "%";
                infoICPUUsageBanner.Text = Math.Round(coreCpuUsage, 0) + "%  " + tbCPUFreq.Text + "\n" +
                                           (tbCPUVolt.Text != "?V" ? tbCPUVolt.Text : string.Empty);
                infoACPUUsageBigBannerPolygonText.Text = tbCPUUsage.Text;
                infoICPUUsageBigBanner.Text = infoICPUUsageBanner.Text;

                //InfoACPUBanner График
                InfoACPUBannerPolygon.Points.Remove(_zeroPoint);
                _cpuPointer.Add(new InfoPageCPUPoints { X = 60, Y = 48 - (int)(coreCpuUsage * 0.48) });
                if (CPUFlyout.IsOpen)
                {
                    InfoACPUBigBannerPolygon.Points.Remove(_zeroPoint);
                    InfoACPUBigBannerPolygon.Points.Add(new Point(60,
                        48 - (int)(coreCpuUsage * 0.48)));
                }

                InfoACPUBannerPolygon.Points.Add(new Point(60, 48 - (int)(coreCpuUsage * 0.48)));
                foreach (var element in _cpuPointer.ToList())
                {
                    if (element.X < 0)
                    {
                        _cpuPointer.Remove(element);
                        if (CPUFlyout.IsOpen)
                        {
                            InfoACPUBigBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                        }

                        InfoACPUBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                    }
                    else
                    {
                        if (CPUFlyout.IsOpen)
                        {
                            InfoACPUBigBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                        }

                        InfoACPUBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                        element.X -= 1;
                        InfoACPUBannerPolygon.Points.Add(new Point(element.X, element.Y));
                        if (CPUFlyout.IsOpen)
                        {
                            InfoACPUBigBannerPolygon.Points.Add(new Point(element.X, element.Y));
                        }
                    }
                }

                if (CPUFlyout.IsOpen)
                {
                    InfoACPUBigBannerPolygon.Points.Add(_maxedPoint);
                }

                InfoACPUBannerPolygon.Points.Add(_maxedPoint);


                //InfoAGPUBanner График
                InfoAGPUBannerPolygon.Points.Remove(_zeroPoint);
                _gpuPointer.Add(new InfoPageCPUPoints { X = 60, Y = 48 - (int)(gfxClk / _maxGfxClock * 48) });
                InfoAGPUBannerPolygon.Points.Add(new Point(60,
                    48 - (int)(gfxClk / _maxGfxClock * 48)));
                if (GPUFlyout.IsOpen)
                {
                    InfoAGPUBigBannerPolygon.Points.Remove(_zeroPoint);
                    InfoAGPUBigBannerPolygon.Points.Add(new Point(60,
                        48 - (int)(gfxClk / _maxGfxClock * 48)));
                }

                foreach (var element in _gpuPointer.ToList())
                {
                    if (element.X < 0)
                    {
                        _gpuPointer.Remove(element);
                        InfoAGPUBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                        if (GPUFlyout.IsOpen)
                        {
                            InfoAGPUBigBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                        }
                    }
                    else
                    {
                        InfoAGPUBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                        if (GPUFlyout.IsOpen)
                        {
                            InfoAGPUBigBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                        }

                        element.X -= 1;
                        element.Y = (int)(element.Y * beforeMaxGfx / _maxGfxClock);
                        if (GPUFlyout.IsOpen)
                        {
                            InfoAGPUBigBannerPolygon.Points.Add(new Point(element.X, element.Y));
                        }

                        InfoAGPUBannerPolygon.Points.Add(new Point(element.X, element.Y));
                    }
                }

                if (GPUFlyout.IsOpen)
                {
                    InfoAGPUBigBannerPolygon.Points.Add(_maxedPoint);
                }

                InfoAGPUBannerPolygon.Points.Add(_maxedPoint);

                //InfoAVRMBanner График
                InfoAVRMBannerPolygon.Points.Remove(_zeroPoint);
                _vrmPointer.Add(new InfoPageCPUPoints
                {
                    X = 60,
                    Y = 48 - (int)(_sensorsInformation.VrmEdcValue /
                        _sensorsInformation.VrmEdcLimit * 48)
                });
                if (VRMFlyout.IsOpen)
                {
                    InfoAVRMBigBannerPolygon.Points.Remove(_zeroPoint);
                    InfoAVRMBigBannerPolygon.Points.Add(new Point(60,
                        48 - (int)(_sensorsInformation.VrmEdcValue /
                            _sensorsInformation.VrmEdcLimit * 48)));
                }

                InfoAVRMBannerPolygon.Points.Add(new Point(60,
                    48 - (int)(_sensorsInformation.VrmEdcValue /
                        _sensorsInformation.VrmEdcLimit * 48)));
                foreach (var element in _vrmPointer.ToList())
                {
                    if (element.X < 0)
                    {
                        _vrmPointer.Remove(element);
                        InfoAVRMBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                        if (VRMFlyout.IsOpen)
                        {
                            InfoAVRMBigBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                        }
                    }
                    else
                    {
                        if (VRMFlyout.IsOpen)
                        {
                            InfoAVRMBigBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                        }

                        InfoAVRMBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                        element.X -= 1;
                        if (VRMFlyout.IsOpen)
                        {
                            InfoAVRMBigBannerPolygon.Points.Add(new Point(element.X, element.Y));
                        }

                        InfoAVRMBannerPolygon.Points.Add(new Point(element.X, element.Y));
                    }
                }

                InfoAVRMBannerPolygon.Points.Add(_maxedPoint);
                if (VRMFlyout.IsOpen)
                {
                    InfoAVRMBigBannerPolygon.Points.Add(_maxedPoint);
                }

                //InfoAPSTBanner График
                InfoAPSTBannerPolygon.Points.Remove(_zeroPoint);
                _pstPointer.Add(new InfoPageCPUPoints { X = 60, Y = 48 - currentPstate * 16 });
                InfoAPSTBannerPolygon.Points.Add(new Point(60, 48 - currentPstate * 16));
                if (PSTFlyout.IsOpen)
                {
                    InfoAPSTBigBannerPolygon.Points.Remove(_zeroPoint);
                    InfoAPSTBigBannerPolygon.Points.Add(new Point(60, 48 - currentPstate * 16));
                }

                foreach (var element in _pstPointer.ToList())
                {
                    if (element.X < 0)
                    {
                        _pstPointer.Remove(element);
                        InfoAPSTBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                        if (PSTFlyout.IsOpen)
                        {
                            InfoAPSTBigBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                        }
                    }
                    else
                    {
                        InfoAPSTBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                        if (PSTFlyout.IsOpen)
                        {
                            InfoAPSTBigBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                        }

                        element.X -= 1;
                        InfoAPSTBannerPolygon.Points.Add(new Point(element.X, element.Y));
                        if (PSTFlyout.IsOpen)
                        {
                            InfoAPSTBigBannerPolygon.Points.Add(new Point(element.X, element.Y));
                        }
                    }
                }

                if (PSTFlyout.IsOpen)
                {
                    InfoAPSTBigBannerPolygon.Points.Add(_maxedPoint);
                }

                InfoAPSTBannerPolygon.Points.Add(_maxedPoint);

                //InfoABATBanner График
                InfoABATBannerPolygon.Points.Remove(_zeroPoint);
                _batPointer.Add(new InfoPageCPUPoints { X = 60, Y = 48 - (int)(Math.Abs(currBatRate) / _maxBatRate * 48) });
                InfoABATBannerPolygon.Points.Add(new Point(60,
                    48 - (int)(Math.Abs(currBatRate) / _maxBatRate * 48)));
                if (BATFlyout.IsOpen)
                {
                    InfoABATBigBannerPolygon.Points.Remove(_zeroPoint);
                    InfoABATBigBannerPolygon.Points.Add(new Point(60,
                        48 - (int)(Math.Abs(currBatRate) / _maxBatRate * 48)));
                }

                foreach (var element in _batPointer.ToList())
                { 
                    if (element.X < 0)
                    {
                        _batPointer.Remove(element);
                        InfoABATBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                        if (BATFlyout.IsOpen)
                        {
                            InfoABATBigBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                        }
                    }
                    else
                    {
                        InfoABATBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                        if (BATFlyout.IsOpen)
                        {
                            InfoABATBigBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                        }

                        element.X -= 1;
                        element.Y = (int)(element.Y * beforeMaxBatRate / _maxBatRate);
                        InfoABATBannerPolygon.Points.Add(new Point(element.X, element.Y));
                        if (BATFlyout.IsOpen)
                        {
                            InfoABATBigBannerPolygon.Points.Add(new Point(element.X, element.Y));
                        }
                    }
                }

                if (BATFlyout.IsOpen)
                {
                    InfoABATBigBannerPolygon.Points.Add(_maxedPoint);
                }

                InfoABATBannerPolygon.Points.Add(_maxedPoint);
                try
                {
                    var busyRam = _busyRam;
                    var usageResult = _totalRam;
                    if (busyRam != 0 && usageResult != 0)
                    {
                        InfoARAMBannerPolygon.Points.Remove(_zeroPoint);
                        _ramPointer.Add(new InfoPageCPUPoints
                        {
                            X = 60,
                            Y = 48 - (int)(busyRam * 100 / usageResult * 0.48)
                        });
                        InfoARAMBannerPolygon.Points.Add(new Point(60,
                            48 - (int)(busyRam * 100 / usageResult * 0.48)));
                        if (RAMFlyout.IsOpen)
                        {
                            InfoARAMBigBannerPolygon.Points.Remove(_zeroPoint);
                            InfoARAMBigBannerPolygon.Points.Add(new Point(60,
                                48 - (int)(busyRam * 100 / usageResult * 0.48)));
                        }
                    }

                    foreach (var element in _ramPointer.ToList())
                    {
                        if (element.X < 0)
                        {
                            _ramPointer.Remove(element);
                            InfoARAMBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                            if (RAMFlyout.IsOpen)
                            {
                                InfoARAMBigBannerPolygon.Points.Remove(
                                    new Point(element.X, element.Y));
                            }
                        }
                        else
                        {
                            if (RAMFlyout.IsOpen)
                            {
                                InfoARAMBigBannerPolygon.Points.Remove(
                                    new Point(element.X, element.Y));
                            }

                            InfoARAMBannerPolygon.Points.Remove(new Point(element.X, element.Y));
                            element.X -= 1;
                            InfoARAMBannerPolygon.Points.Add(new Point(element.X, element.Y));
                            if (RAMFlyout.IsOpen)
                            {
                                InfoARAMBigBannerPolygon.Points.Add(new Point(element.X, element.Y));
                            }
                        }
                    }

                    if (RAMFlyout.IsOpen)
                    {
                        InfoARAMBigBannerPolygon.Points.Add(_maxedPoint);
                    }

                    InfoARAMBannerPolygon.Points.Add(_maxedPoint);
                }
                catch (Exception ex)
                {
                    LogHelper.TraceIt_TraceError(ex.ToString());
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
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex.ToString());
        }
    }

    // Автообновление информации 
    private void StartInfoUpdate() // Начать обновление информации
    {
        try
        {
            InfoACPUBannerPolygon.Points.Clear();
            InfoACPUBannerPolygon.Points.Add(_startPoint);
            InfoACPUBigBannerPolygon.Points.Clear();
            InfoACPUBigBannerPolygon.Points.Add(_startPoint);
            InfoAGPUBannerPolygon.Points.Clear();
            InfoAGPUBannerPolygon.Points.Add(_startPoint);
            InfoAGPUBigBannerPolygon.Points.Clear();
            InfoAGPUBigBannerPolygon.Points.Add(_startPoint);
            InfoARAMBannerPolygon.Points.Clear();
            InfoARAMBannerPolygon.Points.Add(_startPoint);
            InfoARAMBigBannerPolygon.Points.Clear();
            InfoARAMBigBannerPolygon.Points.Add(_startPoint);
            InfoAVRMBannerPolygon.Points.Clear();
            InfoAVRMBannerPolygon.Points.Add(_startPoint);
            InfoAVRMBigBannerPolygon.Points.Clear();
            InfoAVRMBigBannerPolygon.Points.Add(_startPoint);
            InfoABATBannerPolygon.Points.Clear();
            InfoABATBannerPolygon.Points.Add(_startPoint);
            InfoABATBigBannerPolygon.Points.Clear();
            InfoABATBigBannerPolygon.Points.Add(_startPoint);
            InfoAPSTBannerPolygon.Points.Clear();
            InfoAPSTBannerPolygon.Points.Add(_startPoint);
            InfoAPSTBigBannerPolygon.Points.Clear();
            InfoAPSTBigBannerPolygon.Points.Add(_startPoint);
            _dispatcherTimer = new DispatcherTimer();
            _dispatcherTimer.Tick += (_, _) => UpdateInfo();
            _dispatcherTimer.Interval = TimeSpan.FromMilliseconds(300);
            App.MainWindow.VisibilityChanged += Window_VisibilityChanged;
            _dispatcherTimer.Start();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex.ToString());
        }
    }

    // Метод, который будет вызываться при скрытии/переключении страницы
    private void StopInfoUpdate() => _dispatcherTimer?.Stop();

    #endregion

    #region Information builders

    private async Task InfoCpuSectionGridBuilder()
    {
        try
        {
            // Очищаем сетку перед построением
            InfoMainCPUFreqGrid.RowDefinitions.Clear();
            InfoMainCPUFreqGrid.ColumnDefinitions.Clear();

            // Получаем список видеокарт с помощью WMI
            _cachedGpuList ??= await Task.Run(() =>
            {
                return new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_VideoController")
                    .Get()
                    .Cast<ManagementObject>()
                    .Where(element =>
                    {
                        var name = element["Name"]?.ToString() ?? string.Empty;
                        // Исключаем виртуальные видеокарты (например, Parsec)
                        return !name.Contains("Parsec", StringComparison.OrdinalIgnoreCase) &&
                               !name.Contains("virtual", StringComparison.OrdinalIgnoreCase);
                    })
                    .ToList(); // Преобразуем в список для удобства работы
            });

            // Количество видеокарт
            var gpuCounter = _cachedGpuList.Count;

            // Сохраняем текущее значение _numberOfLogicalProcessors для восстановления позже
            var backupNumberLogical = _numberOfLogicalProcessors;

            // Определяем количество элементов (coreCounter) в зависимости от выбранной секции
            var coreCounter = _selectedGroup switch
            {
                // Секция процессора или PStates
                0 or 5 => _numberOfCores > 2
                    ? _numberOfCores // Если ядер больше 2, используем количество ядер
                    : infoCPUSectionComboBox.SelectedIndex == 0
                        ? _numberOfLogicalProcessors // Если выбрано отображение частоты, используем логические процессоры
                        : _numberOfCores, // Иначе используем количество ядер
                // Секция GFX
                1 => gpuCounter, // Используем количество видеокарт
                // Секция RAM
                2 => tbRAMModel.Text.Split('/').Length, // Используем количество плат ОЗУ
                // Секция 3
                3 => 4, // Фиксированное значение для секции 3
                // Другие секции
                _ => 1 // По умолчанию 1 элемент
            };

            // Если количество ядер больше 2, обновляем _numberOfLogicalProcessors
            if (_numberOfCores > 2)
            {
                _numberOfLogicalProcessors = coreCounter;
            }

            // Определяем количество строк и столбцов для сетки
            var rowCount = _numberOfLogicalProcessors / 2 > 4 ? 4 : _numberOfLogicalProcessors / 2;
            if (_numberOfLogicalProcessors % 2 != 0 || _numberOfLogicalProcessors == 2)
            {
                rowCount++; // Добавляем дополнительную строку, если количество элементов нечётное или равно 2
            }

            // Добавляем строки и столбцы в сетку
            for (var i = 0; i < rowCount; i++)
            {
                InfoMainCPUFreqGrid.RowDefinitions.Add(new RowDefinition());
                InfoMainCPUFreqGrid.ColumnDefinitions.Add(new ColumnDefinition());
            }

            // Восстанавливаем значение _numberOfLogicalProcessors
            _numberOfLogicalProcessors = backupNumberLogical;

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
                        2 => tbRAMModel.Text.Split('/').Length - coreCounter, // Используем оставшиеся платы ОЗУ
                        // Секция 3
                        3 => 4 - coreCounter, // Фиксированное значение для секции 3
                        // Другие секции
                        _ => 0 // По умолчанию 0
                    };

                    // Создаём кнопку для текущего элемента
                    var elementButton = CreateElementButton(currCore, _selectedGroup, _numberOfCores, tbRAMModel.Text);

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
            await LogHelper.TraceIt_TraceError(e.ToString());
        }
    }

    private static Grid
        CreateElementButton(int currCore, int selectedGroup, int numberOfCores,
            string ramModelText) // Создать кнопки отображающие текущие показатели
    {
        var elementButton = new Grid
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(3, 3, 3, 3),
            Children =
            {
                new Button
                {
                    Shadow = new ThemeShadow(),
                    Translation = new Vector3(0, 0, 20),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    CornerRadius = _defaultCornerRadius,
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

    private void InfoCPUSectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        //0 - частота, 1 - напряжение, 2 - мощность, 3 - температуры
        if (!_loaded)
        {
            return;
        }

        InfoMainCPUFreqGrid.Children.Clear();
        _ = InfoCpuSectionGridBuilder();
    }

    private async void InfoRTSSButton_Click(object sender, RoutedEventArgs e)
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

            if (!_loaded)
            {
                return;
            }

            AppSettings.RtssMetricsEnabled = infoRTSSButton.IsChecked == true;
            AppSettings.SaveSettings();
        }
        catch
        {
            //
        }
    }

    private void InfoNiIconsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded)
        {
            return;
        }

        AppSettings.NiIconsEnabled = infoNiIconsButton.IsChecked == true;
        AppSettings.SaveSettings();
    }

    #region Banner Buttons Click Handlers

    private void HandleBannerButtonClick(Button clickedButton, int selectedGroup)
    {
        if (_selectedGroup != selectedGroup)
        {
            SetShadow(clickedButton);
            _selectedGroup = selectedGroup;
            UpdateButtonAppearance(clickedButton);
            InfoMainCPUFreqGrid.Children.Clear();
            if (selectedGroup == 5 && infoCPUSectionComboBox.SelectedIndex != 0)
            {
                infoCPUSectionComboBox.SelectedIndex = 0;
            }
            else
            {
                _ = InfoCpuSectionGridBuilder();
            }
        }
    }

    private void SetShadow(Button clickedButton)
    {
        var allButtons = new[]
            { CPUBannerButton, GPUBannerButton, RAMBannerButton, VRMBannerButton, BATBannerButton, PSTBannerButton };
        foreach (var button in allButtons)
        {
            button.Shadow = button == clickedButton ? new ThemeShadow() : null;
        }
    }

    private void UpdateButtonAppearance(Button clickedButton)
    {
        var allButtons = new[]
            { CPUBannerButton, GPUBannerButton, RAMBannerButton, VRMBannerButton, BATBannerButton, PSTBannerButton };
        foreach (var button in allButtons)
        {
            button.Background = button == clickedButton ? _selectedBrush : _transparentBrush;
            button.BorderBrush = button == clickedButton ? _selectedBorderBrush : _transparentBrush;
        }
    }

    // Обработчики событий для каждой кнопки
    private void CPUBannerButton_Click(object sender, RoutedEventArgs e) => HandleBannerButtonClick(CPUBannerButton, 0);
    private void GPUBannerButton_Click(object sender, RoutedEventArgs e) => HandleBannerButtonClick(GPUBannerButton, 1);
    private void RAMBannerButton_Click(object sender, RoutedEventArgs e) => HandleBannerButtonClick(RAMBannerButton, 2);
    private void VRMBannerButton_Click(object sender, RoutedEventArgs e) => HandleBannerButtonClick(VRMBannerButton, 3);
    private void BATBannerButton_Click(object sender, RoutedEventArgs e) => HandleBannerButtonClick(BATBannerButton, 4);
    private void PSTBannerButton_Click(object sender, RoutedEventArgs e) => HandleBannerButtonClick(PSTBannerButton, 5);

    #endregion

    #region Flyouts and Banner Buttons handlers

    private static void HandleFlyoutOpening(Flyout flyout, Polygon polygon, FontIcon expandIcon, Button expandButton)
    {
        polygon.Points.Clear();
        polygon.Points.Add(_startPoint);

        if (flyout.IsOpen)
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

    private void HandleBannerButtonPointerEntered(Button bannerButton, Flyout flyout, FontIcon expandIcon,
        Button expandButton)
    {
        if (bannerButton.IsPointerOver)
        {
            expandIcon.Glyph = "\uF0D8";
            expandButton.Visibility = Visibility.Visible;

            // Скрываем все остальные кнопки
            CollapseAllExpandButtons(expandButton);
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

    private void CollapseAllExpandButtons(Button exceptButton)
    {
        var allButtons = new[]
        {
            CPU_Expand_Button, VRM_Expand_Button, GPU_Expand_Button, RAM_Expand_Button, BAT_Expand_Button,
            PST_Expand_Button
        };
        foreach (var button in allButtons)
        {
            if (button != exceptButton)
            {
                button.Visibility = Visibility.Collapsed;
            }
        }
    }

    // Обработчики событий для каждого Flyout и BannerButton
    private void CPUFlyout_Opening(object sender, object e) => HandleFlyoutOpening(CPUFlyout, InfoACPUBigBannerPolygon,
        CPU_Expand_FontIcon, CPU_Expand_Button);

    private void CPUBannerButton_PointerEntered(object sender, PointerRoutedEventArgs e) =>
        HandleBannerButtonPointerEntered(CPUBannerButton, CPUFlyout, CPU_Expand_FontIcon, CPU_Expand_Button);

    private void VRMFlyout_Opening(object sender, object e) => HandleFlyoutOpening(VRMFlyout, InfoAVRMBigBannerPolygon,
        VRM_Expand_FontIcon, VRM_Expand_Button);

    private void VRMBannerButton_PointerEntered(object sender, PointerRoutedEventArgs e) =>
        HandleBannerButtonPointerEntered(VRMBannerButton, VRMFlyout, VRM_Expand_FontIcon, VRM_Expand_Button);

    private void GPUFlyout_Opening(object sender, object e) => HandleFlyoutOpening(GPUFlyout, InfoAGPUBigBannerPolygon,
        GPU_Expand_FontIcon, GPU_Expand_Button);

    private void GPUBannerButton_PointerEntered(object sender, PointerRoutedEventArgs e) =>
        HandleBannerButtonPointerEntered(GPUBannerButton, GPUFlyout, GPU_Expand_FontIcon, GPU_Expand_Button);

    private void RAMFlyout_Opening(object sender, object e) => HandleFlyoutOpening(RAMFlyout, InfoARAMBigBannerPolygon,
        RAM_Expand_FontIcon, RAM_Expand_Button);

    private void RAMBannerButton_PointerEntered(object sender, PointerRoutedEventArgs e) =>
        HandleBannerButtonPointerEntered(RAMBannerButton, RAMFlyout, RAM_Expand_FontIcon, RAM_Expand_Button);

    private void BATFlyout_Opening(object sender, object e) => HandleFlyoutOpening(BATFlyout, InfoABATBigBannerPolygon,
        BAT_Expand_FontIcon, BAT_Expand_Button);

    private void BATBannerButton_PointerEntered(object sender, PointerRoutedEventArgs e) =>
        HandleBannerButtonPointerEntered(BATBannerButton, BATFlyout, BAT_Expand_FontIcon, BAT_Expand_Button);

    private void PSTFlyout_Opening(object sender, object e) => HandleFlyoutOpening(PSTFlyout, InfoAPSTBigBannerPolygon,
        PST_Expand_FontIcon, PST_Expand_Button);

    private void PSTBannerButton_PointerEntered(object sender, PointerRoutedEventArgs e) =>
        HandleBannerButtonPointerEntered(PSTBannerButton, PSTFlyout, PST_Expand_FontIcon, PST_Expand_Button);

    #endregion

    #endregion
}