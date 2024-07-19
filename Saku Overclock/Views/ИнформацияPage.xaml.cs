using System.Management;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.ViewModels;
using ZenStates.Core; 

namespace Saku_Overclock.Views;
public sealed partial class ИнформацияPage : Page
{
    private Config config = new();
    public double refreshtime;
    private bool loaded = false;
    private static bool canUpdateOSDText = true;
    private static readonly Timer osdTimer = new(_ => canUpdateOSDText = true, null, 1000, 1000);
    private string? rtss_line;
    private readonly List<InfoPageCPUPoints> CPUPointer = [];
    private readonly List<InfoPageCPUPoints> GPUPointer = [];
    private readonly List<InfoPageCPUPoints> RAMPointer = [];
    private readonly List<InfoPageCPUPoints> VRMPointer = [];
    private readonly List<InfoPageCPUPoints> BATPointer = [];
    private readonly List<InfoPageCPUPoints> PSTPointer = [];
    private readonly List<double> PSTatesList = [0, 0, 0];
    private double MaxGFXClock = 0.0;
    private Microsoft.UI.Xaml.Media.Brush? TransparentBrush;
    private Microsoft.UI.Xaml.Media.Brush? SelectedBrush;
    private Microsoft.UI.Xaml.Media.Brush? SelectedBorderBrush;
    private int SelectedGroup = 0;
    private bool IsAppInTray = false;
    private IntPtr ryzenAccess;
    private string CPUName = "Unknown";
    private string GPUName = "Unknown";
    private string RAMName = "Unknown";
    private string BATName = "Unknown";
    private int numberOfCores = 0;
    private int numberOfLogicalProcessors = 0;
    private System.Windows.Threading.DispatcherTimer? dispatcherTimer;
    private readonly Cpu? cpu;
    public ИнформацияViewModel ViewModel
    {
        get;
    }
    public ИнформацияPage()
    {
        ViewModel = App.GetService<ИнформацияViewModel>();
        InitializeComponent();
        try
        {
            cpu ??= CpuSingleton.GetInstance();
        }
        catch
        {
            App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
        }
        ConfigLoad();
        config.NBFCFlagConsoleCheckSpeedRunning = false;
        config.FlagRyzenADJConsoleTemperatureCheckRunning = true;
        ConfigSave();
        Loaded += (s, a) =>
        {
            loaded = true;
            SelectedBrush = CPUBannerButton.Background;
            SelectedBorderBrush = CPUBannerButton.BorderBrush;
            TransparentBrush = GPUBannerButton.Background;
            GetCPUInfo();
            GetRAMInfo();
            ReadPstate();
            GetBATInfo();
        };
        Unloaded += ИнформацияPage_Unloaded;
    }

    #region JSON and Initialization
    //JSON форматирование
    public void ConfigSave()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json", JsonConvert.SerializeObject(config));
        }
        catch { }
    }
    public void ConfigLoad()
    {
        try
        {
            config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json"))!;
        }
        catch
        {
            App.MainWindow.ShowMessageDialogAsync("Пресеты 3", "Критическая ошибка!");
        }
    }
    public static int GetCPUCores()
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
            return 0;
        }
        return 0;
    }
    private async void GetCPUInfo()
    {
        try
        {
            // CPU information using WMI
            var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Processor");

            var name = "";
            var description = "";
            double l2Size = 0;
            double l3Size = 0;
            var baseClock = "";

            await Task.Run(() =>
            {
                foreach (var queryObj in searcher.Get().Cast<ManagementObject>())
                {
                    name = queryObj["Name"].ToString();
                    description = queryObj["Description"].ToString();
                    numberOfCores = Convert.ToInt32(queryObj["NumberOfCores"]);
                    numberOfLogicalProcessors = Convert.ToInt32(queryObj["NumberOfLogicalProcessors"]);
                    l2Size = Convert.ToDouble(queryObj["L2CacheSize"]) / 1024;
                    l3Size = Convert.ToDouble(queryObj["L3CacheSize"]) / 1024;
                    baseClock = queryObj["MaxClockSpeed"].ToString();
                }
                try
                {
                    if (GetSystemInfo.GetGPUName(0) != null)
                    {
                        GPUName = GetSystemInfo.GetGPUName(0)!.Contains("AMD") ? GetSystemInfo.GetGPUName(0)! : GetSystemInfo.GetGPUName(1)!;
                    }
                }
                catch (Exception ex) { SendSMUCommand.TraceIt_TraceError(ex.ToString()); }
            });
            InfoCPUSectionGridBuilder();
            tbProcessor.Text = name;
            CPUName = name;
            tbCaption.Text = description;
            var codeName = GetSystemInfo.Codename();
            //CODENAME OVERRIDE
            // codeName = "Renoir";
            if (codeName != "")
            {
                tbCodename.Text = codeName;
                tbCodename1.Visibility = Visibility.Collapsed;
                tbCode1.Visibility = Visibility.Collapsed;
            }
            else
            {
                try
                {
                    tbCodename1.Text = $"{cpu?.info.codeName}";
                }
                catch
                {
                    tbCodename1.Visibility = Visibility.Collapsed;
                    tbCode1.Visibility = Visibility.Collapsed;
                }
                tbCodename.Visibility = Visibility.Collapsed;
                tbCode.Visibility = Visibility.Collapsed;
            }
            try
            {
                tbSMU.Text = cpu?.systemInfo.GetSmuVersionString();
            }
            catch
            {
                tbSMU.Visibility = Visibility.Collapsed;
                infoSMU.Visibility = Visibility.Collapsed;
            }
            tbCores.Text = numberOfLogicalProcessors == numberOfCores ? numberOfCores.ToString() : GetSystemInfo.GetBigLITTLE(numberOfCores, l2Size);
            tbThreads.Text = numberOfLogicalProcessors.ToString();
            tbL3Cache.Text = $"{l3Size:0.##} MB";
            uint sum = 0;
            foreach (var number in GetSystemInfo.GetCacheSizes(GetSystemInfo.CacheLevel.Level1))
            {
                sum += number;
            }
            decimal total = sum;
            total /= 1024;
            tbL1Cache.Text = $"{total:0.##} MB";
            sum = 0;
            foreach (var number in GetSystemInfo.GetCacheSizes(GetSystemInfo.CacheLevel.Level2))
            {
                sum += number;
            }
            total = sum;
            total /= 1024;
            tbL2Cache.Text = $"{total:0.##} MB";
            tbBaseClock.Text = $"{baseClock} MHz";
            tbInstructions.Text = GetSystemInfo.InstructionSets();
        }
        catch (ManagementException ex)
        {
            Console.WriteLine("An error occurred while querying for WMI data: " + ex.Message);
        }
    }
    private void InfoCPUSectionGridBuilder()
    {
        InfoMainCPUFreqGrid.RowDefinitions.Clear();
        InfoMainCPUFreqGrid.ColumnDefinitions.Clear();
        /*numberOfCores = 8;
        numberOfLogicalProcessors = 16;*/
        var backupNumberLogical = numberOfLogicalProcessors;
        if (numberOfCores > 2)
        {
            numberOfLogicalProcessors = numberOfCores;
        }
        for (var i = 0; i < numberOfLogicalProcessors / 2; i++)
        {
            InfoMainCPUFreqGrid.RowDefinitions.Add(new RowDefinition());
            InfoMainCPUFreqGrid.ColumnDefinitions.Add(new ColumnDefinition());
        }
        if (numberOfLogicalProcessors % 2 != 0 || numberOfLogicalProcessors == 2)
        {
            InfoMainCPUFreqGrid.RowDefinitions.Add(new RowDefinition());
            InfoMainCPUFreqGrid.ColumnDefinitions.Add(new ColumnDefinition());
        }
        numberOfLogicalProcessors = backupNumberLogical;
        var coreCounter = SelectedGroup == 0 ?
            (numberOfCores > 2 ? numberOfCores :
            (infoCPUSectionComboBox.SelectedIndex == 0 ? numberOfLogicalProcessors
            : numberOfCores))
            : SelectedGroup == 1 ?
            new ManagementObjectSearcher("root\\CIMV2", $"SELECT * FROM Win32_VideoController").Get().Cast<ManagementObject>().Count()
            : (SelectedGroup == 2 ?
            tbRAMModel.Text.Split('/').Length
            : 4);
        for (var j = 0; j < InfoMainCPUFreqGrid.RowDefinitions.Count; j++)
        {
            for (var f = 0; f < InfoMainCPUFreqGrid.ColumnDefinitions.Count; f++)
            {
                if (coreCounter <= 0)
                {
                    return;
                }
                var currCore = SelectedGroup == 0 ?
                    (numberOfCores > 2 ?
                    numberOfCores - coreCounter
                    : infoCPUSectionComboBox.SelectedIndex == 0 ?
                    numberOfLogicalProcessors - coreCounter
                    : numberOfCores - coreCounter)
                    : SelectedGroup == 1 ?
                    new ManagementObjectSearcher("root\\CIMV2", $"SELECT * FROM Win32_VideoController").Get().Cast<ManagementObject>().Count() - coreCounter
                    : (SelectedGroup == 2 ?
                    tbRAMModel.Text.Split('/').Length - coreCounter
                    : 4 - coreCounter);
                var elementButton = new Grid()
                {
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(3, 3, 3, 3),
                    Children =
                        {

                            new Button()
                            {
                                HorizontalAlignment = HorizontalAlignment.Stretch,
                                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                                VerticalAlignment = VerticalAlignment.Stretch,
                                Content = new Grid()
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
                                            FontWeight = new Windows.UI.Text.FontWeight(200)
                                        },
                                        new TextBlock
                                        {
                                            Text = "0.00 Ghz",
                                            Name = $"FreqButtonText_{currCore}",
                                            VerticalAlignment = VerticalAlignment.Center,
                                            HorizontalAlignment = HorizontalAlignment.Center,
                                            FontWeight = new Windows.UI.Text.FontWeight(800)
                                        },
                                        new TextBlock
                                        {
                                            VerticalAlignment = VerticalAlignment.Center,
                                            HorizontalAlignment = HorizontalAlignment.Right,
                                            Text = SelectedGroup == 0 ? (currCore < numberOfCores ? "Core" : "Thread") : (SelectedGroup == 1 ? "GPU" :  (SelectedGroup == 2 ? tbSlots.Text.Split('*')[1].Replace("Bit","") : currCore == 0 ? "VRM EDC" : ( currCore == 1 ? "VRM TDC" : (currCore == 2 ? "SoC EDC" : "SoC TDC")) )),
                                            FontWeight = new Windows.UI.Text.FontWeight(200)
                                        }
                                    }
                                }

                            }
                        }
                };

                Grid.SetRow(elementButton, j); Grid.SetColumn(elementButton, f);
                InfoMainCPUFreqGrid.Children.Add(elementButton);
                coreCounter--;
            }
        }
    }
    private void GetBATInfo()
    {
        try
        {
            tbBAT.Text = GetSystemInfo.GetBatteryPercent().ToString() + "W";
            tbBATState.Text = GetSystemInfo.GetBatteryStatus().ToString();
            tbBATHealth.Text = $"{(GetSystemInfo.GetBatteryHealth() * 100):0.##}%";
            tbBATCycles.Text = $"{GetSystemInfo.GetBatteryCycle()}";
            tbBATCapacity.Text = $"{GetSystemInfo.ReadFullChargeCapacity()}mAh/{GetSystemInfo.ReadDesignCapacity()}mAh";
            tbBATChargeRate.Text = $"{(GetSystemInfo.GetBatteryRate() / 1000):0.##}W";
        }
        catch
        {
            if (BATBannerButton.Visibility != Visibility.Collapsed) { BATBannerButton.Visibility = Visibility.Collapsed; }
        }
    }
    private async void GetRAMInfo()
    {
        double capacity = 0;
        var speed = 0;
        var type = 0;
        var width = 0;
        var slots = 0;
        var producer = "";
        var model = "";

        try
        {
            var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_PhysicalMemory");
            await Task.Run(() =>
            {
                foreach (var queryObj in searcher.Get().Cast<ManagementObject>())
                {
                    if (producer == "") { producer = queryObj["Manufacturer"].ToString(); }
                    else if (!producer!.Contains(value: queryObj["Manufacturer"].ToString()!)) { producer = $"{producer}/{queryObj["Manufacturer"]}"; }
                    if (model == "") { model = queryObj["PartNumber"].ToString(); }
                    else if (!model!.Contains(value: queryObj["PartNumber"].ToString()!))
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
            var DDRType = "";
            DDRType = type switch
            {
                20 => "DDR",
                21 => "DDR2",
                24 => "DDR3",
                26 => "DDR4",
                30 => "LPDDR4",
                34 => "DDR5",
                35 => "LPDDR5",
                36 => "LPDDR5X",
                _ => $"Unknown ({type})",
            };
            RAMName = $"{capacity} GB {DDRType} @ {speed} MT/s";
            tbRAM.Text = speed + "MT/s";
            tbRAMProducer.Text = producer;
            tbRAMModel.Text = model.Replace(" ", null);
            tbWidth.Text = $"{width} bit";
            tbSlots.Text = $"{slots} * {width / slots} bit";
            tbTCL.Text = Utils.GetBits(cpu!.ReadDword(0 | 0x50204), 0, 6) + "T";
            tbTRCDWR.Text = Utils.GetBits(cpu!.ReadDword(0 | 0x50204), 24, 6) + "T";
            tbTRCDRD.Text = Utils.GetBits(cpu!.ReadDword(0 | 0x50204), 16, 6) + "T";
            tbTRAS.Text = Utils.GetBits(cpu!.ReadDword(0 | 0x50204), 8, 7) + "T";
            tbTRP.Text = Utils.GetBits(cpu!.ReadDword(0 | 0x50208), 16, 6) + "T";
            tbTRC.Text = Utils.GetBits(cpu!.ReadDword(0 | 0x50208), 0, 8) + "T";
        }
        catch (Exception ex)
        {
            SendSMUCommand.TraceIt_TraceError(ex.ToString());
        }
    }
    private void Pstate_Expanding(Expander sender, ExpanderExpandingEventArgs args)
    {
        ReadPstate();
    }
    public static void CalculatePstateDetails(uint eax, ref uint IddDiv, ref uint IddVal, ref uint CpuVid, ref uint CpuDfsId, ref uint CpuFid)
    {
        IddDiv = eax >> 30;
        IddVal = eax >> 22 & 0xFF;
        CpuVid = eax >> 14 & 0xFF;
        CpuDfsId = eax >> 8 & 0x3F;
        CpuFid = eax & 0xFF;
    }
    private void ReadPstate()
    {
        try
        {
            for (var i = 0; i < 3; i++)
            {
                uint eax = default, edx = default;
                var pstateId = i;
                try
                {
                    if (cpu?.ReadMsr(Convert.ToUInt32(Convert.ToInt64(0xC0010064) + pstateId), ref eax, ref edx) == false)
                    {
                        App.MainWindow.ShowMessageDialogAsync("Error while reading CPU Pstate", "Critical Error");
                        return;
                    }
                }
                catch (Exception ex) { SendSMUCommand.TraceIt_TraceError(ex.ToString()); }
                uint IddDiv = 0x0;
                uint IddVal = 0x0;
                uint CpuVid = 0x0;
                uint CpuDfsId = 0x0;
                uint CpuFid = 0x0;
                CalculatePstateDetails(eax, ref IddDiv, ref IddVal, ref CpuVid, ref CpuDfsId, ref CpuFid);
                var textBlock = (TextBlock)InfoPSTSectionMetrics.FindName($"tbPSTP{i}");
                textBlock.Text = $"FID: {Convert.ToString(CpuFid, 10)}/DID: {Convert.ToString(CpuDfsId, 10)}\n{CpuFid * 25 / (CpuDfsId * 12.5) / 10}" + "infoAGHZ".GetLocalized();
            }
        }
        catch (Exception ex) { SendSMUCommand.TraceIt_TraceError(ex.ToString()); }
    }
    private void Window_VisibilityChanged(object sender, WindowVisibilityChangedEventArgs args)
    {
        if (args.Visible)
        {
            dispatcherTimer?.Start();
            IsAppInTray = false;
        }
        else
        {
            if (infoRTSSButton.IsChecked == false)
            {
                dispatcherTimer?.Stop();
                IsAppInTray = true;
            }
        }
    }
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e); StartInfoUpdate();
    }
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e); StopInfoUpdate();
    }
    private void UpdateInfoAsync()
    {
        if (!loaded) { return; }
        if (config.FlagRyzenADJConsoleTemperatureCheckRunning)
        {
            if (!IsAppInTray)
            {
                if (SelectedGroup != 0)
                {
                    infoCPUSectionComboBox.Visibility = Visibility.Collapsed;
                    if (SelectedGroup == 1)
                    {
                        //Показать свойства видеокарты
                        infoCPUSectionName.Text = "InfoGPUSectionName".GetLocalized();
                        InfoCPUSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoGPUSectionMetrics.Visibility = Visibility.Visible;
                        InfoRAMSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoVRMSectionMetrics.Visibility = Visibility.Collapsed;
                        infoRAMMAINSection.Visibility = Visibility.Collapsed;
                        infoCPUMAINSection.Visibility = Visibility.Collapsed;
                        InfoBATSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoPSTSectionMetrics.Visibility = Visibility.Collapsed;
                        tbProcessor.Text = GPUName;
                    }
                    if (SelectedGroup == 2)
                    {
                        //Показать свойства ОЗУ
                        infoCPUSectionName.Text = "InfoRAMSectionName".GetLocalized();
                        InfoCPUSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoGPUSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoRAMSectionMetrics.Visibility = Visibility.Visible;
                        InfoVRMSectionMetrics.Visibility = Visibility.Collapsed;
                        tbProcessor.Text = RAMName;
                        infoRAMMAINSection.Visibility = Visibility.Visible;
                        infoCPUMAINSection.Visibility = Visibility.Collapsed;
                        InfoBATSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoPSTSectionMetrics.Visibility = Visibility.Collapsed;
                    }
                    if (SelectedGroup == 3)
                    {
                        //Зона VRM
                        infoCPUSectionName.Text = "VRM";
                        InfoCPUSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoGPUSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoRAMSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoVRMSectionMetrics.Visibility = Visibility.Visible;
                        tbProcessor.Text = CPUName;
                        infoRAMMAINSection.Visibility = Visibility.Collapsed;
                        infoCPUMAINSection.Visibility = Visibility.Visible;
                        InfoBATSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoPSTSectionMetrics.Visibility = Visibility.Collapsed;
                    }
                    if (SelectedGroup == 4)
                    {
                        infoCPUSectionName.Text = "Battery";
                        InfoCPUSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoGPUSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoRAMSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoVRMSectionMetrics.Visibility = Visibility.Collapsed;
                        tbProcessor.Text = CPUName;
                        infoRAMMAINSection.Visibility = Visibility.Collapsed;
                        infoCPUMAINSection.Visibility = Visibility.Visible;
                        InfoBATSectionMetrics.Visibility = Visibility.Visible;
                        InfoPSTSectionMetrics.Visibility = Visibility.Collapsed;
                    }
                    if (SelectedGroup == 5)
                    {
                        infoCPUSectionName.Text = "P-States";
                        InfoCPUSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoGPUSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoRAMSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoVRMSectionMetrics.Visibility = Visibility.Collapsed;
                        tbProcessor.Text = CPUName;
                        infoRAMMAINSection.Visibility = Visibility.Collapsed;
                        infoCPUMAINSection.Visibility = Visibility.Visible;
                        InfoBATSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoPSTSectionMetrics.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    infoCPUMAINSection.Visibility = Visibility.Visible;
                    infoRAMMAINSection.Visibility = Visibility.Collapsed;
                    infoCPUSectionComboBox.Visibility = Visibility.Visible;
                    InfoCPUSectionMetrics.Visibility = Visibility.Visible;
                    InfoVRMSectionMetrics.Visibility = Visibility.Collapsed;
                    InfoGPUSectionMetrics.Visibility = Visibility.Collapsed;
                    InfoRAMSectionMetrics.Visibility = Visibility.Collapsed;
                    InfoBATSectionMetrics.Visibility = Visibility.Collapsed;
                    InfoPSTSectionMetrics.Visibility = Visibility.Collapsed;
                    //Скрыть лишние элементы 
                    infoCPUSectionName.Text = "InfoCPUSectionName".GetLocalized();
                    tbProcessor.Text = CPUName;
                }
                InfoACPUBannerPolygon.Points.Remove(new Windows.Foundation.Point(60, 49));
                InfoAGPUBannerPolygon.Points.Remove(new Windows.Foundation.Point(60, 49));
                InfoARAMBannerPolygon.Points.Remove(new Windows.Foundation.Point(60, 49));
                InfoAVRMBannerPolygon.Points.Remove(new Windows.Foundation.Point(60, 49));


                _ = RyzenADJWrapper.refresh_table(ryzenAccess);
                tbBATChargeRate.Text = $"{(GetSystemInfo.GetBatteryRate() / 1000):0.##}W";
                tbBAT.Text = GetSystemInfo.GetBatteryPercent().ToString() + "%";
                tbBATState.Text = GetSystemInfo.GetBatteryStatus().ToString();
                tbStapmL.Text = Math.Round(RyzenADJWrapper.get_stapm_value(ryzenAccess), 3) + "W/" + Math.Round(RyzenADJWrapper.get_stapm_limit(ryzenAccess), 3) + "W";

                tbActualL.Text = Math.Round(RyzenADJWrapper.get_fast_value(ryzenAccess), 3) + "W/" + Math.Round(RyzenADJWrapper.get_fast_limit(ryzenAccess), 3) + "W";
                tbAclualPowerL.Text = tbActualL.Text;

                tbAVGL.Text = Math.Round(RyzenADJWrapper.get_slow_value(ryzenAccess), 3) + "W/" + Math.Round(RyzenADJWrapper.get_slow_limit(ryzenAccess), 3) + "W";

                tbFast.Text = Math.Round(RyzenADJWrapper.get_stapm_time(ryzenAccess), 3) + "S";
                tbSlow.Text = Math.Round(RyzenADJWrapper.get_slow_time(ryzenAccess), 3) + "S";

                tbAPUL.Text = Math.Round(RyzenADJWrapper.get_apu_slow_value(ryzenAccess), 3) + "W/" + Math.Round(RyzenADJWrapper.get_apu_slow_limit(ryzenAccess), 3) + "W";

                tbVRMTDCL.Text = Math.Round(RyzenADJWrapper.get_vrm_current_value(ryzenAccess), 3) + "A/" + Math.Round(RyzenADJWrapper.get_vrm_current(ryzenAccess), 3) + "A";
                tbSOCTDCL.Text = Math.Round(RyzenADJWrapper.get_vrmsoc_current_value(ryzenAccess), 3) + "A/" + Math.Round(RyzenADJWrapper.get_vrmsoc_current(ryzenAccess), 3) + "A";
                tbVRMEDCL.Text = Math.Round(RyzenADJWrapper.get_vrmmax_current_value(ryzenAccess), 3) + "A/" + Math.Round(RyzenADJWrapper.get_vrmmax_current(ryzenAccess), 3) + "A";
                tbVRMEDCVRML.Text = tbVRMEDCL.Text;
                infoVRMUsageBanner.Text = Math.Round(RyzenADJWrapper.get_vrmmax_current_value(ryzenAccess), 3) + "A\n" + Math.Round(RyzenADJWrapper.get_fast_value(ryzenAccess), 3) + "W";
                infoAVRMUsageBannerPolygonText.Text = Math.Round(RyzenADJWrapper.get_vrmmax_current_value(ryzenAccess), 3) + "A";
                tbSOCEDCL.Text = Math.Round(RyzenADJWrapper.get_vrmsocmax_current_value(ryzenAccess), 3) + "A/" + Math.Round(RyzenADJWrapper.get_vrmsocmax_current(ryzenAccess), 3) + "A";
                tbSOCVOLT.Text = Math.Round(RyzenADJWrapper.get_soc_volt(ryzenAccess), 3) + "V";
                tbSOCPOWER.Text = Math.Round(RyzenADJWrapper.get_soc_power(ryzenAccess), 3) + "W";
                tbMEMCLOCK.Text = Math.Round(RyzenADJWrapper.get_mem_clk(ryzenAccess), 3) + "MHz";
                tbFabricClock.Text = Math.Round(RyzenADJWrapper.get_fclk(ryzenAccess), 3) + "MHz";
                var core_Clk = 0f;
                var endtrace = 0;
                var core_Volt = 0f;
                var endtraced = 0;
                for (uint f = 0; f < 8; f++)
                {
                    var currCore = infoCPUSectionComboBox.SelectedIndex switch
                    {
                        0 => RyzenADJWrapper.get_core_clk(ryzenAccess, f),
                        1 => RyzenADJWrapper.get_core_volt(ryzenAccess, f),
                        2 => RyzenADJWrapper.get_core_power(ryzenAccess, f),
                        3 => RyzenADJWrapper.get_core_temp(ryzenAccess, f),
                        _ => RyzenADJWrapper.get_core_clk(ryzenAccess, f)
                    };
                    if (!float.IsNaN(currCore))
                    {
                        if (!InfoMainCPUFreqGrid.IsLoaded) { return; }
                        var currText = (TextBlock)InfoMainCPUFreqGrid.FindName($"FreqButtonText_{f}");
                        if (currText != null)
                        {
                            if (SelectedGroup == 0)
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
                                if (SelectedGroup == 1)
                                {
                                    foreach (var element in (new ManagementObjectSearcher("root\\CIMV2", $"SELECT * FROM Win32_VideoController").Get().Cast<ManagementObject>()))
                                    {
                                        currText.Text = GetSystemInfo.GetGPUName((int)f);
                                    }
                                }

                                if (SelectedGroup == 2)
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
                                }
                                if (SelectedGroup == 3)
                                {
                                    currText.Text = f == 0 ?
                                        $"{Math.Round(RyzenADJWrapper.get_vrmmax_current_value(ryzenAccess), 3)} A/{Math.Round(RyzenADJWrapper.get_vrmmax_current(ryzenAccess), 3)}A"
                                        : (f == 1 ? $"{Math.Round(RyzenADJWrapper.get_vrm_current_value(ryzenAccess), 3)} A/{Math.Round(RyzenADJWrapper.get_vrm_current(ryzenAccess), 3)}A"
                                        : (f == 2 ? $"{Math.Round(RyzenADJWrapper.get_vrmsocmax_current_value(ryzenAccess), 3)} A/{Math.Round(RyzenADJWrapper.get_vrmsocmax_current(ryzenAccess), 3)}A"
                                        : (f == 3 ? $"{Math.Round(RyzenADJWrapper.get_vrmsoc_current_value(ryzenAccess), 3)} A/{Math.Round(RyzenADJWrapper.get_vrmsoc_current(ryzenAccess), 3)}A" : $"{0}A")));
                                }
                            }
                        }
                        if (f < numberOfCores)
                        {
                            core_Clk += RyzenADJWrapper.get_core_clk(ryzenAccess, f);
                            endtrace += 1;
                        }
                    }
                    var currVolt = RyzenADJWrapper.get_core_volt(ryzenAccess, f);
                    if (!float.IsNaN(currVolt))
                    {
                        core_Volt += currVolt;
                        endtraced += 1;
                    }
                }
                if (endtrace != 0)
                {
                    tbCPUFreq.Text = Math.Round(core_Clk / endtrace, 3) + " " + "infoAGHZ".GetLocalized();
                }
                else
                {
                    tbCPUFreq.Text = "? " + "infoAGHZ".GetLocalized();
                }
                if (endtraced != 0)
                {
                    tbCPUVolt.Text = Math.Round(core_Volt / endtraced, 3) + "V";
                }
                else
                {
                    tbCPUVolt.Text = "?V";
                }
                tbPSTFREQ.Text = tbCPUFreq.Text;
                var gfxCLK = Math.Round(RyzenADJWrapper.get_gfx_clk(ryzenAccess) / 1000, 3);
                var gfxVolt = Math.Round(RyzenADJWrapper.get_gfx_volt(ryzenAccess), 3);
                var gfxTemp = RyzenADJWrapper.get_gfx_temp(ryzenAccess);
                var beforeMaxGFX = MaxGFXClock;
                if (MaxGFXClock < gfxCLK) { MaxGFXClock = gfxCLK; }
                infoGPUUsageBanner.Text = gfxCLK + " " + "infoAGHZ".GetLocalized() + "  " + Math.Round(gfxTemp, 0) + "C\n" + gfxVolt + "V";
                infoAGPUUsageBannerPolygonText.Text = gfxCLK + "infoAGHZ".GetLocalized(); tbGPUFreq.Text = infoAGPUUsageBannerPolygonText.Text;
                tbGPUVolt.Text = gfxVolt + "V";
                var maxTemp = Math.Round(RyzenADJWrapper.get_tctl_temp(ryzenAccess), 3);
                tbCPUMaxL.Text = Math.Round(RyzenADJWrapper.get_tctl_temp_value(ryzenAccess), 3) + "C/" + maxTemp + "C";
                tbCPUMaxTempL.Text = tbCPUMaxL.Text; tbCPUMaxTempVRML.Text = tbCPUMaxL.Text;
                var apuTemp = Math.Round(RyzenADJWrapper.get_apu_skin_temp_value(ryzenAccess), 3);
                var apuTempLimit = Math.Round(RyzenADJWrapper.get_apu_skin_temp_limit(ryzenAccess), 3);
                tbAPUMaxL.Text = (!double.IsNaN(apuTemp) && apuTemp > 0 ? apuTemp : Math.Round(gfxTemp, 3)) + "C/" + (!double.IsNaN(apuTempLimit) && apuTempLimit > 0 ? apuTempLimit : maxTemp) + "C";
                tbDGPUMaxL.Text = Math.Round(RyzenADJWrapper.get_dgpu_skin_temp_value(ryzenAccess), 3) + "C/" + Math.Round(RyzenADJWrapper.get_dgpu_skin_temp_limit(ryzenAccess), 3) + "C";
                var CoreCPUUsage = Math.Round(RyzenADJWrapper.get_cclk_busy_value(ryzenAccess), 3);
                tbCPUUsage.Text = CoreCPUUsage + "%"; infoACPUUsageBannerPolygonText.Text = Math.Round(CoreCPUUsage, 0) + "%";
                infoICPUUsageBanner.Text = Math.Round(CoreCPUUsage, 0) + "%  " + tbCPUFreq.Text + "\n" + tbCPUVolt.Text;

                //InfoACPUBanner График
                InfoACPUBannerPolygon.Points.Remove(new Windows.Foundation.Point(0, 0));
                CPUPointer.Add(new InfoPageCPUPoints() { X = 60, Y = 48 - (int)(CoreCPUUsage * 0.48) });
                InfoACPUBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 48 - (int)(CoreCPUUsage * 0.48)));
                foreach (var element in CPUPointer.ToList())
                {
                    if (element != null)
                    {
                        if (element.X < 0)
                        {
                            CPUPointer.Remove(element);
                            InfoACPUBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                        }
                        else
                        {
                            InfoACPUBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                            element.X -= 1;
                            InfoACPUBannerPolygon.Points.Add(new Windows.Foundation.Point(element.X, element.Y));
                        }
                    }
                }
                InfoACPUBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 49));

                //InfoAGPUBanner График
                InfoAGPUBannerPolygon.Points.Remove(new Windows.Foundation.Point(0, 0));
                GPUPointer.Add(new InfoPageCPUPoints() { X = 60, Y = 48 - (int)(gfxCLK / MaxGFXClock * 48) });
                InfoAGPUBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 48 - (int)(gfxCLK / MaxGFXClock * 48)));
                foreach (var element in GPUPointer.ToList())
                {
                    if (element != null)
                    {
                        if (element.X < 0)
                        {
                            GPUPointer.Remove(element);
                            InfoAGPUBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                        }
                        else
                        {
                            InfoAGPUBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                            element.X -= 1;
                            element.Y = (int)(element.Y * beforeMaxGFX / MaxGFXClock);
                            InfoAGPUBannerPolygon.Points.Add(new Windows.Foundation.Point(element.X, element.Y));
                        }
                    }
                }
                InfoAGPUBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 49));

                //InfoAVRMBanner График
                InfoAVRMBannerPolygon.Points.Remove(new Windows.Foundation.Point(0, 0));
                VRMPointer.Add(new InfoPageCPUPoints() { X = 60, Y = 48 - (int)(RyzenADJWrapper.get_vrmmax_current_value(ryzenAccess) / RyzenADJWrapper.get_vrmmax_current(ryzenAccess) * 48) });
                InfoAVRMBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 48 - (int)(RyzenADJWrapper.get_vrmmax_current_value(ryzenAccess) / RyzenADJWrapper.get_vrmmax_current(ryzenAccess) * 48)));
                foreach (var element in VRMPointer.ToList())
                {
                    if (element != null)
                    {
                        if (element.X < 0)
                        {
                            VRMPointer.Remove(element);
                            InfoAVRMBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                        }
                        else
                        {
                            InfoAVRMBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                            element.X -= 1;
                            InfoAVRMBannerPolygon.Points.Add(new Windows.Foundation.Point(element.X, element.Y));
                        }
                    }
                }
                InfoAVRMBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 49));


                var totalRam = 0d;
                var busyRam = 0d;
                //Раз в шесть секунд обновляет состояние памяти 
                var ramMonitor = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize,FreePhysicalMemory FROM Win32_OperatingSystem");
                foreach (var objram in ramMonitor.Get().Cast<ManagementObject>())
                {
                    totalRam = Convert.ToDouble(objram["TotalVisibleMemorySize"]);
                    busyRam = totalRam - Convert.ToDouble(objram["FreePhysicalMemory"]);
                    var RAMUsage = Math.Round(busyRam * 100 / totalRam, 0) + "%";
                    InfoRAMUsage.Text = RAMUsage + "\n" + Math.Round(busyRam / 1024 / 1024, 3) + "GB/" + Math.Round(totalRam / 1024 / 1024, 1) + "GB";
                    infoARAMUsageBannerPolygonText.Text = RAMUsage;
                }

                //InfoARAMBanner График
                try
                {
                    if (busyRam != 0 && totalRam != 0)
                    {
                        InfoARAMBannerPolygon.Points.Remove(new Windows.Foundation.Point(0, 0));
                        RAMPointer.Add(new InfoPageCPUPoints() { X = 60, Y = 48 - (int)(busyRam * 100 / totalRam * 0.48) });
                        InfoARAMBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 48 - (int)(busyRam * 100 / totalRam * 0.48)));
                    }
                    foreach (var element in RAMPointer.ToList())
                    {
                        if (element != null)
                        {
                            if (element.X < 0)
                            {
                                RAMPointer.Remove(element);
                                InfoARAMBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                            }
                            else
                            {
                                InfoARAMBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                                element.X -= 1;
                                InfoARAMBannerPolygon.Points.Add(new Windows.Foundation.Point(element.X, element.Y));
                            }
                        }
                    }
                    InfoARAMBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 49));
                }
                catch (Exception ex)
                {
                    SendSMUCommand.TraceIt_TraceError(ex.ToString());
                }
            }

            rtss_line = "<C0=FFA0A0><C1=A0FFA0><C2=FC89AC><C3=fa2363><S1=70><S2=-50><C0>Saku Overclock <C1>RC-4: <S0>" + ShellPage.SelectedProfile.Replace('а', 'a').Replace('м', 'm').Replace('и', 'i').Replace('н', 'n').Replace('М', 'M').Replace('у', 'u').Replace('Э', 'E').Replace('о', 'o').Replace('Б', 'B').Replace('л', 'l').Replace('с', 'c').Replace('С', 'C').Replace('р', 'r').Replace('т', 't').Replace('ь', ' ');
            rtss_line += "<S1><Br><C2>STAPM, Fast, Slow: " + "<C3><S0>" + Math.Round(RyzenADJWrapper.get_stapm_value(ryzenAccess), 3) + "<S2>W<S1>" + Math.Round(RyzenADJWrapper.get_stapm_limit(ryzenAccess), 3) + "W"
                + " <S0>" + Math.Round(RyzenADJWrapper.get_fast_value(ryzenAccess), 3) + "<S2>W<S1>" + Math.Round(RyzenADJWrapper.get_fast_limit(ryzenAccess), 3) + "W"
                + " <S0>" + Math.Round(RyzenADJWrapper.get_slow_value(ryzenAccess), 3) + "<S2>W<S1>" + Math.Round(RyzenADJWrapper.get_slow_limit(ryzenAccess), 3) + "W";
            rtss_line += "<Br><C2>EDC, Therm, CPU Usage: " + "<C3><S0>" + Math.Round(RyzenADJWrapper.get_vrmmax_current_value(ryzenAccess), 3) + "<S2>A<S1>" + Math.Round(RyzenADJWrapper.get_vrmmax_current(ryzenAccess), 3) + "A " + "<C3><S0>" + Math.Round(RyzenADJWrapper.get_tctl_temp_value(ryzenAccess), 3) + "<S2>C<S1>" + Math.Round(RyzenADJWrapper.get_tctl_temp(ryzenAccess), 3) + "C" + "<C3><S0> " + Math.Round(RyzenADJWrapper.get_cclk_busy_value(ryzenAccess), 3) + "<S2>%<S1>";
            var avgCoreCLK = 0d;
            var avgCoreVolt = 0d;
            var endCLKString = "<Br><S1><C2>Clocks: ";
            for (var f = 0u; f < numberOfCores; f++)
            {
                if (f < 8)
                {
                    var clk = Math.Round(RyzenADJWrapper.get_core_clk(ryzenAccess, f), 3);
                    var volt = Math.Round(RyzenADJWrapper.get_core_volt(ryzenAccess, f), 3);
                    avgCoreCLK += clk;
                    avgCoreVolt += volt;
                    endCLKString += f > 3 ? "<Br>        " : "" + "<S1><C2>" + f + ":<S0><C3> " + clk + "<S2>GHz<S1>" + volt + "V ";
                }
            }
            rtss_line += endCLKString + "<Br><C2>AVG Clock, Volt: " + "<C3><S0>" + Math.Round(avgCoreCLK / numberOfCores, 3) + "<S2>GHz<S1>" + Math.Round(avgCoreVolt / numberOfCores, 3) + "V";
            rtss_line += "<Br><C2>APU Clock, Volt, Temp: " + "<C3><S0>" + Math.Round(RyzenADJWrapper.get_gfx_clk(ryzenAccess), 3) + "<S2>MHz<S1>" + Math.Round(RyzenADJWrapper.get_gfx_volt(ryzenAccess), 3) + "V " + "<S0>" + Math.Round(RyzenADJWrapper.get_gfx_temp(ryzenAccess), 3) + "<S1>C";
            rtss_line += "<Br><C2>Framerate " + "<C3><S0>" + "%FRAMERATE% %FRAMETIME%";
            if (canUpdateOSDText && infoRTSSButton.IsChecked == true)
            {
                RTSSHandler.ChangeOSDText(rtss_line); canUpdateOSDText = false;
            }
        }
    }
    // Автообновление информации 
    private void StartInfoUpdate()
    {
        InfoACPUBannerPolygon.Points.Clear();
        InfoACPUBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
        InfoAGPUBannerPolygon.Points.Clear();
        InfoAGPUBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
        InfoARAMBannerPolygon.Points.Clear();
        InfoARAMBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
        InfoAVRMBannerPolygon.Points.Clear();
        InfoAVRMBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
        InfoABATBannerPolygon.Points.Clear();
        InfoABATBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
        InfoAPSTBannerPolygon.Points.Clear();
        InfoAPSTBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
        ryzenAccess = RyzenADJWrapper.Init_ryzenadj();
        _ = RyzenADJWrapper.init_table(ryzenAccess);
        dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
        dispatcherTimer.Tick += (sender, e) => UpdateInfoAsync();
        dispatcherTimer.Interval = TimeSpan.FromMilliseconds(300);
        App.MainWindow.VisibilityChanged += Window_VisibilityChanged;
        dispatcherTimer.Start();
    }
    // Метод, который будет вызываться при скрытии/переключении страницы
    private void StopInfoUpdate()
    {
        RyzenADJWrapper.Cleanup_ryzenadj(ryzenAccess);
        dispatcherTimer?.Stop();
    }
    private void ИнформацияPage_Unloaded(object sender, RoutedEventArgs e)
    {
        infoRTSSButton.IsChecked = false;
        dispatcherTimer?.Stop(); osdTimer.Dispose();
        RTSSHandler.ResetOSDText();
    }
    #endregion

    #region Event Handlers
    private void InfoCPUSectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        //0 - частота, 1 - напряжение, 2 - мощность, 3 - температуры
        if (!loaded) { return; }
        InfoMainCPUFreqGrid.Children.Clear();
        InfoCPUSectionGridBuilder();
    }

    private void CPUBannerButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedGroup = 0;
        CPUBannerButton.Background = SelectedBrush;
        CPUBannerButton.BorderBrush = SelectedBorderBrush;
        GPUBannerButton.Background = TransparentBrush;
        GPUBannerButton.BorderBrush = TransparentBrush;
        RAMBannerButton.Background = TransparentBrush;
        RAMBannerButton.BorderBrush = TransparentBrush;
        VRMBannerButton.Background = TransparentBrush;
        VRMBannerButton.BorderBrush = TransparentBrush;
        PSTBannerButton.Background = TransparentBrush;
        PSTBannerButton.BorderBrush = TransparentBrush;
        InfoMainCPUFreqGrid.Children.Clear();
        InfoCPUSectionGridBuilder();
    }

    private void GPUBannerButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedGroup = 1;
        CPUBannerButton.Background = TransparentBrush;
        CPUBannerButton.BorderBrush = TransparentBrush;
        GPUBannerButton.Background = SelectedBrush;
        GPUBannerButton.BorderBrush = SelectedBorderBrush;
        RAMBannerButton.Background = TransparentBrush;
        RAMBannerButton.BorderBrush = TransparentBrush;
        VRMBannerButton.Background = TransparentBrush;
        VRMBannerButton.BorderBrush = TransparentBrush;
        PSTBannerButton.Background = TransparentBrush;
        PSTBannerButton.BorderBrush = TransparentBrush;
        InfoMainCPUFreqGrid.Children.Clear();
        InfoCPUSectionGridBuilder();
    }

    private void RAMBannerButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedGroup = 2;
        CPUBannerButton.Background = TransparentBrush;
        CPUBannerButton.BorderBrush = TransparentBrush;
        GPUBannerButton.Background = TransparentBrush;
        GPUBannerButton.BorderBrush = TransparentBrush;
        RAMBannerButton.Background = SelectedBrush;
        RAMBannerButton.BorderBrush = SelectedBorderBrush;
        VRMBannerButton.Background = TransparentBrush;
        VRMBannerButton.BorderBrush = TransparentBrush;
        PSTBannerButton.Background = TransparentBrush;
        PSTBannerButton.BorderBrush = TransparentBrush;
        InfoMainCPUFreqGrid.Children.Clear();
        InfoCPUSectionGridBuilder();
    }
    private void VRMBannerButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedGroup = 3;
        CPUBannerButton.Background = TransparentBrush;
        CPUBannerButton.BorderBrush = TransparentBrush;
        GPUBannerButton.Background = TransparentBrush;
        GPUBannerButton.BorderBrush = TransparentBrush;
        RAMBannerButton.Background = TransparentBrush;
        RAMBannerButton.BorderBrush = TransparentBrush;
        VRMBannerButton.Background = SelectedBrush;
        VRMBannerButton.BorderBrush = SelectedBorderBrush;
        BATBannerButton.Background = TransparentBrush;
        BATBannerButton.BorderBrush = TransparentBrush;
        PSTBannerButton.Background = TransparentBrush;
        PSTBannerButton.BorderBrush = TransparentBrush;
        InfoMainCPUFreqGrid.Children.Clear();
        InfoCPUSectionGridBuilder();
    }
    private void BATBannerButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedGroup = 4;
        CPUBannerButton.Background = TransparentBrush;
        CPUBannerButton.BorderBrush = TransparentBrush;
        GPUBannerButton.Background = TransparentBrush;
        GPUBannerButton.BorderBrush = TransparentBrush;
        RAMBannerButton.Background = TransparentBrush;
        RAMBannerButton.BorderBrush = TransparentBrush;
        VRMBannerButton.Background = SelectedBrush;
        VRMBannerButton.BorderBrush = SelectedBorderBrush;
        BATBannerButton.Background = SelectedBrush;
        BATBannerButton.BorderBrush = SelectedBorderBrush;
        PSTBannerButton.Background = TransparentBrush;
        PSTBannerButton.BorderBrush = TransparentBrush;
    }
    private void PSTBannerButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedGroup = 5;
        CPUBannerButton.Background = TransparentBrush;
        CPUBannerButton.BorderBrush = TransparentBrush;
        GPUBannerButton.Background = TransparentBrush;
        GPUBannerButton.BorderBrush = TransparentBrush;
        RAMBannerButton.Background = TransparentBrush;
        RAMBannerButton.BorderBrush = TransparentBrush;
        VRMBannerButton.Background = SelectedBrush;
        VRMBannerButton.BorderBrush = SelectedBorderBrush;
        BATBannerButton.Background = TransparentBrush;
        BATBannerButton.BorderBrush = TransparentBrush;
        PSTBannerButton.Background = SelectedBrush;
        PSTBannerButton.BorderBrush = SelectedBorderBrush;
    }
    private void InfoRTSSButton_Click(object sender, RoutedEventArgs e)
    {
        if (infoRTSSButton.IsChecked == false)
        {
            RTSSHandler.ResetOSDText();
        }
    }
    #endregion
}