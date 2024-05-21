using System.Diagnostics;
using System.Globalization;
using System.Management;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json;
using Saku_Overclock.ViewModels;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
namespace Saku_Overclock.Views;
public sealed partial class ИнформацияPage : Page
{
    private Config config = new();
    public double refreshtime;
    private System.Windows.Threading.DispatcherTimer? dispatcherTimer;
    private readonly ZenStates.Core.Cpu? cpu;
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
        config.fanex = false;
        config.tempex = true;
        ConfigSave();
        GetCPUInfo();
        GetRAMInfo();
        ReadPstate();
    }
    #region JSON and Initialization
    //JSON форматирование
    public void ConfigSave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
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
    private async void GetCPUInfo()
    {
        try
        {
            sdCPU.Visibility = Visibility.Collapsed;
            // CPU information using WMI
            var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Processor");

            var name = "";
            var description = "";
            var manufacturer = "";
            var numberOfCores = 0;
            var numberOfLogicalProcessors = 0;
            double l2Size = 0;
            double l3Size = 0;
            var baseClock = "";

            await Task.Run(() =>
            {
                foreach (var queryObj in searcher.Get().Cast<ManagementObject>())
                {
                    name = queryObj["Name"].ToString();
                    description = queryObj["Description"].ToString();
                    manufacturer = queryObj["Manufacturer"].ToString();
                    numberOfCores = Convert.ToInt32(queryObj["NumberOfCores"]);
                    numberOfLogicalProcessors = Convert.ToInt32(queryObj["NumberOfLogicalProcessors"]);
                    l2Size = Convert.ToDouble(queryObj["L2CacheSize"]) / 1024;
                    l3Size = Convert.ToDouble(queryObj["L3CacheSize"]) / 1024;
                    baseClock = queryObj["MaxClockSpeed"].ToString();
                }
            }); 
            tbProcessor.Text = name;
            tbCaption.Text = description;
            var codeName = GetSystemInfo.Codename();
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
            tbProducer.Text = manufacturer;
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
            sdCPU.Visibility = Visibility.Visible;
            sdCPU.IsExpanded = false;
        }
        catch (ManagementException ex)
        {
            Console.WriteLine("An error occurred while querying for WMI data: " + ex.Message);
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
                _ => $"Unknown ({type})",
            };
            tbRAM.Text = $"{capacity} GB {DDRType} @ {speed} MT/s";
            tbRAMProducer.Text = producer;
            tbRAMModel.Text = model.Replace(" ", null);
            tbWidth.Text = $"{width} bit";
            tbSlots.Text = $"{slots} * {width / slots} bit";
        }
        catch
        {

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
        var p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = @"ryzenps.exe";
        p.StartInfo.Arguments = "-p=0 --dry-run";
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;
        ConfigLoad();
        p.Start();
        var outputWriter = p.StandardOutput;
        var line = outputWriter.ReadLine();
        while (line != null)
        {
            if (line != "")
            {
                if (line.Contains("DID:")) { DID_0.Content = line.Replace("DID:", "").Replace(" ", ""); }
                if (line.Contains("FID:")) { FID_0.Content = line.Replace("FID:", "").Replace(" ", ""); }
                if (line.Contains("VCore (V):")) { try { VID_0.Content = double.Parse(line.Replace("VCore (V):", "").Replace(" ", ""), CultureInfo.InvariantCulture) * 1000; } catch { } }
                if (line.Contains("MHz")) { P0_Freq.Content = line.Replace("Frequency (MHz):", "").Replace(" ", ""); }
            } line = outputWriter.ReadLine();
        } p.WaitForExit();  
        var p1 = new Process();
        p1.StartInfo.UseShellExecute = false;
        p1.StartInfo.FileName = @"ryzenps.exe";
        p1.StartInfo.Arguments = "-p=1 --dry-run";
        p1.StartInfo.CreateNoWindow = true;
        p1.StartInfo.RedirectStandardError = true;
        p1.StartInfo.RedirectStandardInput = true;
        p1.StartInfo.RedirectStandardOutput = true;
        ConfigLoad();
        p1.Start();
        var outputWriter1 = p1.StandardOutput;
        var line1 = outputWriter1.ReadLine();
        while (line1 != null)
        {
            if (line1 != "")
            {
                if (line1.Contains("DID:")) { DID_1.Content = line1.Replace("DID:", "").Replace(" ", ""); }
                if (line1.Contains("FID:")) { FID_1.Content = line1.Replace("FID:", "").Replace(" ", ""); }
                if (line1.Contains("VCore (V):")) { try { VID_1.Content = double.Parse(line1.Replace("VCore (V):", "").Replace(" ", ""), CultureInfo.InvariantCulture) * 1000; } catch { } }
                if (line1.Contains("Frequency (MHz):")) { P1_Freq.Content = line1.Replace("Frequency (MHz):", "").Replace(" ", ""); }
            } line1 = outputWriter1.ReadLine();
        } p1.WaitForExit();  
        var p2 = new Process();
        p2.StartInfo.UseShellExecute = false;
        p2.StartInfo.FileName = @"ryzenps.exe";
        p2.StartInfo.Arguments = "-p=2 --dry-run";
        p2.StartInfo.CreateNoWindow = true;
        p2.StartInfo.RedirectStandardError = true;
        p2.StartInfo.RedirectStandardInput = true;
        p2.StartInfo.RedirectStandardOutput = true;
        ConfigLoad();
        p2.Start();
        var outputWriter2 = p2.StandardOutput;
        var line2 = outputWriter2.ReadLine();
        while (line2 != null)
        {
            if (line2 != "")
            {
                if (line2.Contains("DID:")) { DID_2.Content = line2.Replace("DID:", "").Replace(" ", ""); }
                if (line2.Contains("FID:")) { FID_2.Content = line2.Replace("FID:", "").Replace(" ", ""); }
                if (line2.Contains("VCore (V):")) { try { VID_2.Content = double.Parse(line2.Replace("VCore (V):", "").Replace(" ", ""), CultureInfo.InvariantCulture) * 1000; } catch { } }
                if (line2.Contains("Frequency (MHz):")) { P2_Freq.Content = line2.Replace("Frequency (MHz):", "").Replace(" ", ""); }
            } line2 = outputWriter2.ReadLine();
        } p2.WaitForExit();  
    }
    private void Window_VisibilityChanged(object sender, WindowVisibilityChangedEventArgs args)
    {
        if (args.Visible) { dispatcherTimer?.Start();  } else { dispatcherTimer?.Stop(); }
    } 
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e); StartInfoUpdate();
    } 
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    { 
        base.OnNavigatedFrom(e); StopInfoUpdate();
    }
    private async Task UpdateInfoAsync()
    {
        if (config.tempex)
        {
            var p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.FileName = @"ryzenadj.exe";
            p.StartInfo.Arguments = "-i";
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;
            try { p.Start(); } catch { await App.MainWindow.ShowMessageDialogAsync("Unable to start info service. Error at ИнформацияPage.xaml.cs in com.sakuoverclock.org", "Critical Error!"); }
            var outputWriter = p.StandardOutput;
            var line = await outputWriter.ReadLineAsync();
            while (line != null)
            {
                if (line != "")
                {
                    if (line.Contains("STAPM LIMIT"))
                    {
                        tbStapmL.Text = line.Replace("STAPM LIMIT", "").Replace("|", "").Replace(" ", "").Replace("stapm-limit", "") + " W";
                        infoPOWER.Maximum = double.Parse(tbStapmL.Text.Replace(" W", ""), CultureInfo.InvariantCulture);
                    }
                    if (line.Contains("STAPM VALUE"))
                    {
                        tbStapmC.Text = line.Replace("STAPM VALUE", "").Replace("|", "").Replace(" ", "") + " W";
                        infoPOWER.Value = double.Parse(line.Replace("STAPM VALUE", "").Replace("|", "").Replace("W", "").Replace(" ", "").Replace("nan", "100"), CultureInfo.InvariantCulture);
                        infoPOWERI.Text = $"{(int)infoPOWER.Value}{" W"}";
                    }
                    if (line.Contains("PPT LIMIT FAST"))
                    {
                        tbActualL.Text = line.Replace("PPT LIMIT FAST", "").Replace("|", "").Replace(" ", "").Replace("fast-limit", "") + " W";
                        infoPOWER1.Maximum = double.Parse(tbActualL.Text.Replace(" W", ""), CultureInfo.InvariantCulture);
                    }
                    if (line.Contains("PPT VALUE FAST"))
                    {
                        tbActualC.Text = line.Replace("PPT VALUE FAST", "").Replace("|", "").Replace(" ", "") + " W";
                        infoPOWER1.Value = double.Parse(line.Replace("PPT VALUE FAST", "").Replace("|", "").Replace("W", "").Replace(" ", "").Replace("nan", "100"), CultureInfo.InvariantCulture);
                        infoPOWERI1.Text = $"{(int)infoPOWER1.Value}{" W"}";
                    }
                    if (line.Contains("PPT LIMIT SLOW"))
                    {
                        tbAVGL.Text = line.Replace("PPT LIMIT SLOW", "").Replace("|", "").Replace(" ", "").Replace("slow-limit", "") + " W";
                        infoPOWER2.Maximum = double.Parse(tbAVGL.Text.Replace(" W", ""), CultureInfo.InvariantCulture);
                    }
                    if (line.Contains("PPT VALUE SLOW"))
                    {
                        tbAVGC.Text = line.Replace("PPT VALUE SLOW", "").Replace("|", "").Replace(" ", "") + " W";
                        infoPOWER2.Value = double.Parse(line.Replace("PPT VALUE SLOW", "").Replace("|", "").Replace("W", "").Replace(" ", "").Replace("nan", "100"), CultureInfo.InvariantCulture);
                        infoPOWERI2.Text = $"{(int)infoPOWER2.Value}{" W"}";
                    }
                    if (line.Contains("StapmTimeConst")) { tbFast.Text = line.Replace("StapmTimeConst", "").Replace("|", "").Replace(" ", "").Replace("stapm-time", "") + " S"; }
                    if (line.Contains("SlowPPTTimeConst")) { tbSlow.Text = line.Replace("SlowPPTTimeConst", "").Replace("|", "").Replace(" ", "").Replace("slow-time", "") + " S"; }
                    if (line.Contains("PPT LIMIT APU"))
                    {
                        if (line.Contains("nan"))
                        {
                            tbAPULL.Visibility = Visibility.Collapsed;
                            tbAPULC.Visibility = Visibility.Collapsed;
                            tbAPUML.Visibility = Visibility.Collapsed;
                            tbAPUMC.Visibility = Visibility.Collapsed;
                            tbDGPUL.Visibility = Visibility.Collapsed;
                            tbDGPUC.Visibility = Visibility.Collapsed;
                            tbAPUL.Visibility = Visibility.Collapsed;
                            tbAPUC.Visibility = Visibility.Collapsed;
                            tbAPUMaxL.Visibility = Visibility.Collapsed;
                            tbAPUMaxC.Visibility = Visibility.Collapsed;
                            tbDGPUMaxL.Visibility = Visibility.Collapsed;
                            tbDGPUMaxC.Visibility = Visibility.Collapsed;
                        }
                        else { tbAPUL.Text = line.Replace("PPT LIMIT APU", "").Replace("|", "").Replace(" ", "").Replace("apu-slow-limit", "") + " W"; }
                    }
                    if (line.Contains("PPT VALUE APU")) { tbAPUC.Text = line.Replace("PPT VALUE APU", "").Replace("|", "").Replace(" ", "") + " W"; }
                    if (line.Contains("TDC LIMIT VDD")) { tbVRMTDCL.Text = line.Replace("TDC LIMIT VDD", "").Replace("|", "").Replace(" ", "").Replace("vrm-current", "") + " A"; }
                    if (line.Contains("TDC VALUE VDD")) { tbVRMTDCC.Text = line.Replace("TDC VALUE VDD", "").Replace("|", "").Replace(" ", "") + " A"; }
                    if (line.Contains("TDC LIMIT SOC")) { tbSOCTDCL.Text = line.Replace("TDC LIMIT SOC", "").Replace("|", "").Replace(" ", "").Replace("vrmsoc-current", "") + " A"; }
                    if (line.Contains("TDC VALUE SOC")) { tbSOCTDCC.Text = line.Replace("TDC VALUE SOC", "").Replace("|", "").Replace(" ", "") + " A"; }
                    if (line.Contains("EDC LIMIT VDD")) { tbVRMEDCL.Text = line.Replace("EDC LIMIT VDD", "").Replace("|", "").Replace(" ", "").Replace("vrmmax-current", "") + " A"; infoVRM.Maximum = double.Parse(tbVRMEDCL.Text.Replace(" A", "").Replace("nan", "000"), CultureInfo.InvariantCulture); }
                    if (line.Contains("EDC VALUE VDD")) { tbVRMEDCC.Text = line.Replace("EDC VALUE VDD", "").Replace("|", "").Replace(" ", "") + " A"; infoVRM.Value = double.Parse(tbVRMEDCC.Text.Replace(" A", "").Replace("nan", "000"), CultureInfo.InvariantCulture); infoVRMI.Text = ((int)infoVRM.Value).ToString() + " A"; }
                    if (line.Contains("EDC LIMIT SOC")) { tbSOCEDCL.Text = line.Replace("EDC LIMIT SOC", "").Replace("|", "").Replace(" ", "").Replace("vrmsocmax-current", "") + " A"; }
                    if (line.Contains("EDC VALUE SOC")) { tbSOCEDCC.Text = line.Replace("EDC VALUE SOC", "").Replace("|", "").Replace(" ", "") + " A"; }
                    if (line.Contains("THM LIMIT CORE")) { tbCPUMaxL.Text = line.Replace("THM LIMIT CORE", "").Replace("|", "").Replace(" ", "").Replace("tctl-temp", "") + " C"; infoCPU.Maximum = double.Parse(tbCPUMaxL.Text.Replace(" C", "").Replace("nan", "100"), CultureInfo.InvariantCulture); }
                    if (line.Contains("THM VALUE CORE")) { tbCPUMaxC.Text = line.Replace("THM VALUE CORE", "").Replace("|", "").Replace(" ", "") + " C"; infoCPU.Value = double.Parse(tbCPUMaxC.Text.Replace(" C", "").Replace("nan", "000"), CultureInfo.InvariantCulture); infoCPUI.Text = ((int)infoCPU.Value).ToString() + " ℃"; }
                    if (line.Contains("STT LIMIT APU")) { tbAPUMaxL.Text = line.Replace("STT LIMIT APU", "").Replace("|", "").Replace(" ", "").Replace("apu-skin-temp", "") + " C"; }
                    if (line.Contains("STT VALUE APU")) { tbAPUMaxC.Text = line.Replace("STT VALUE APU", "").Replace("|", "").Replace(" ", "") + " C"; }
                    if (line.Contains("STT LIMIT dGPU")) { tbDGPUMaxL.Text = line.Replace("STT LIMIT dGPU", "").Replace("|", "").Replace(" ", "").Replace("dgpu-skin-temp", "") + " C"; }
                    if (line.Contains("STT VALUE dGPU")) { tbDGPUMaxC.Text = line.Replace("STT VALUE dGPU", "").Replace("|", "").Replace(" ", "") + " C"; }
                    if (line.Contains("CCLK BUSY VALUE")) { tbCPUUsage.Text = line.Replace("CCLK BUSY VALUE", "").Replace("|", "").Replace(" ", "").Replace("max-performance", "") + " %"; }
                }
                line = await outputWriter.ReadLineAsync();
            }
            p.WaitForExit();
        }
    }
    // Автообновление информации 
    private void StartInfoUpdate()
    {
        dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
        dispatcherTimer.Tick += async (sender, e) => await UpdateInfoAsync();
        dispatcherTimer.Interval = TimeSpan.FromMilliseconds(300);
        App.MainWindow.VisibilityChanged += Window_VisibilityChanged;
        dispatcherTimer.Start();
    }
    // Метод, который будет вызываться при скрытии/переключении страницы
    private void StopInfoUpdate()
    {
        dispatcherTimer?.Stop();
    }
    #endregion
}