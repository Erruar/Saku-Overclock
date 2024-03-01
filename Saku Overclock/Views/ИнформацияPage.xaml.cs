using System.Diagnostics;
using System.Management;
using System.Windows.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json;
using Saku_Overclock.Services;
using Saku_Overclock.ViewModels;
using Windows.UI.Core;
namespace Saku_Overclock.Views;
#pragma warning disable CS8600 // Преобразование литерала, допускающего значение NULL или возможного значения NULL в тип, не допускающий значение NULL.
#pragma warning disable IDE0059 // Ненужное присваивание значения
#pragma warning disable IDE0044 // Ненужное присваивание значения
#pragma warning disable CS8601 // Возможно, назначение-ссылка, допускающее значение NULL.
#pragma warning disable CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.
public sealed partial class ИнформацияPage : Page
{
    private Config config = new();
    public double refreshtime;
    private System.Windows.Threading.DispatcherTimer dispatcherTimer;
    public ИнформацияViewModel ViewModel
    {
        get;
    }
    public ИнформацияPage()
    {
        ViewModel = App.GetService<ИнформацияViewModel>();
        InitializeComponent();
        ConfigLoad();
        config.fanex = false;
        config.tempex = true;
        ConfigSave();
        // Инициализация таймера
        getCPUInfo();
        getRAMInfo();
        ReadPstate();
    }
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
            config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json"));
        }
        catch
        {
            App.MainWindow.ShowMessageDialogAsync("Пресеты 3", "Критическая ошибка!");
        }
    }
    private async void getCPUInfo()
    {
        try
        {
            sdCPU.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            // CPU information using WMI
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Processor");

            string name = "";
            string description = "";
            string manufacturer = "";
            int numberOfCores = 0;
            int numberOfLogicalProcessors = 0;
            double l2Size = 0;
            double l3Size = 0;
            string baseClock = "";

            await Task.Run(() =>
            {
                foreach (ManagementObject queryObj in searcher.Get())
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
            string codeName = GetSystemInfo.Codename();
            if (codeName != "") tbCodename.Text = codeName;
            else
            {
                tbCodename.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                tbCode.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            }
            tbProducer.Text = manufacturer;
            if (numberOfLogicalProcessors == numberOfCores) tbCores.Text = numberOfCores.ToString();
            else tbCores.Text = GetSystemInfo.getBigLITTLE(numberOfCores, l2Size);
            tbThreads.Text = numberOfLogicalProcessors.ToString();
            tbL3Cache.Text = $"{l3Size.ToString("0.##")} MB";

            uint sum = 0;
            foreach (uint number in GetSystemInfo.GetCacheSizes(GetSystemInfo.CacheLevel.Level1)) sum += number;
            decimal total = sum;
            total /= 1024;
            tbL1Cache.Text = $"{total:0.##} MB";

            sum = 0;
            foreach (uint number in GetSystemInfo.GetCacheSizes(GetSystemInfo.CacheLevel.Level2)) sum += number;
            total = sum;
            total /= 1024;
            tbL2Cache.Text = $"{total:0.##} MB";

            tbBaseClock.Text = $"{baseClock} MHz";

            tbInstructions.Text = GetSystemInfo.InstructionSets();

            sdCPU.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            sdCPU.IsExpanded = false;
        }
        catch (ManagementException ex)
        {
            Console.WriteLine("An error occurred while querying for WMI data: " + ex.Message);
        }
    }

    private async void getRAMInfo()
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
            ManagementObjectSearcher searcher =
        new ManagementObjectSearcher("root\\CIMV2",
        "SELECT * FROM Win32_PhysicalMemory");
            await Task.Run(() =>
            {
                foreach (var queryObj in searcher.Get().Cast<ManagementObject>())
                {
                    if (producer == "") { producer = queryObj["Manufacturer"].ToString(); }
                    else if (!producer.Contains(queryObj["Manufacturer"].ToString())) { producer = $"{producer}/{queryObj["Manufacturer"]}"; }
                    if (model == "") { model = queryObj["PartNumber"].ToString(); }
                    else if (!model.Contains(queryObj["PartNumber"].ToString()))
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
            if (type == 20) DDRType = "DDR";
            else if (type == 21) DDRType = "DDR2";
            else if (type == 24) DDRType = "DDR3";
            else if (type == 26) DDRType = "DDR4";
            else if (type == 30) DDRType = "LPDDR4";
            else if (type == 34) DDRType = "DDR5";
            else if (type == 35) DDRType = "LPDDR5";
            else DDRType = $"Unknown ({type})";

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
    private void Pstate_Expanding(Microsoft.UI.Xaml.Controls.Expander sender, ExpanderExpandingEventArgs args)
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
        var DID_0S = "";
        var FID_0S = "";
        var VID_0S = "";
        var DID_1S = "";
        var FID_1S = "";
        var VID_1S = "";
        var DID_2S = "";
        var FID_2S = "";
        var VID_2S = "";
        var FREQ0 = "";
        var FREQ1 = "";
        var FREQ2 = "";
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
        StreamReader outputWriter = p.StandardOutput;
        var line = outputWriter.ReadLine();
        while (line != null)
        {
            if (line != "")
            {
                if (line.Contains("DID:"))
                {
                    DID_0S = line;
                    DID_0.Content = DID_0S.Replace("DID:", "").Replace(" ", "");
                }
                if (line.Contains("FID:"))
                {
                    FID_0S = line;
                    FID_0.Content = FID_0S.Replace("FID:", "").Replace(" ", "");
                }
                if (line.Contains("VCore (V):"))
                {
                    VID_0S = line;
                    VID_0.Content = VID_0S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "");
                    try
                    {
                        if (int.Parse(VID_0S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "")) >= 90000)
                        {
                            VID_0.Content = VID_0S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "").Remove(4);
                            if (int.Parse(VID_0S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "").Remove(4)) >= 9000)
                            {
                                VID_0.Content = VID_0S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "").Remove(3);
                            }
                        }
                        else
                        {
                            if (int.Parse(VID_0S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "")) >= 10000)
                            {
                                VID_0.Content = VID_0S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "").Remove(4);
                            }
                            else
                            {
                                if (int.Parse(VID_0S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "")) < 10) { VID_0.Content = VID_0S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "") + "000"; }
                                else
                                {
                                    if (int.Parse(VID_0S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "")) < 100) { VID_0.Content = VID_0S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "") + "0"; }
                                    else { VID_0.Content = VID_0S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "").Remove(3); }
                                }
                            }
                        }
                    }
                    catch { }
                }
                if (line.Contains("MHz"))
                {
                    FREQ0 = line;
                    P0_Freq.Content = FREQ0.Replace("Frequency (MHz):", "").Replace(" ", "");
                }
            }
            line = outputWriter.ReadLine();
        }
        p.WaitForExit();
        line = null;
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
        StreamReader outputWriter1 = p1.StandardOutput;
        var line1 = outputWriter1.ReadLine();
        while (line1 != null)
        {
            if (line1 != "")
            {
                if (line1.Contains("DID:"))
                {
                    DID_1S = line1;
                    DID_1.Content = DID_1S.Replace("DID:", "").Replace(" ", "");
                }
                if (line1.Contains("FID:"))
                {
                    FID_1S = line1;
                    FID_1.Content = FID_1S.Replace("FID:", "").Replace(" ", "");
                }
                if (line1.Contains("VCore (V):"))
                {
                    VID_1S = line1;
                    VID_1.Content = VID_1S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "");
                    try
                    {
                        if (int.Parse(VID_1S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "")) >= 90000)
                        {
                            VID_1.Content = VID_1S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "").Remove(4);
                            if (int.Parse(VID_1S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "").Remove(4)) >= 9000)
                            {
                                VID_1.Content = VID_1S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "").Remove(3);
                            }
                        }
                        else
                        {
                            if (int.Parse(VID_1S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "")) >= 10000)
                            {
                                VID_1.Content = VID_1S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "").Remove(4);
                            }
                            else
                            {
                                if (int.Parse(VID_1S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "")) < 10) { VID_1.Content = VID_1S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "") + "000"; }
                                else
                                {
                                    if (int.Parse(VID_1S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "")) < 100) { VID_1.Content = VID_1S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "") + "0"; }
                                    else { VID_1.Content = VID_1S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "").Remove(3); }
                                }
                            }
                        }
                    }
                    catch { }
                }
                if (line1.Contains("Frequency (MHz):"))
                {
                    FREQ1 = line1;
                    P1_Freq.Content = FREQ1.Replace("Frequency (MHz):", "").Replace(" ", "");
                }
            }
                line1 = outputWriter1.ReadLine();
        }
        p1.WaitForExit();
        line1 = null;
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
        StreamReader outputWriter2 = p2.StandardOutput;
        var line2 = outputWriter2.ReadLine();
        while (line2 != null)
        {
            if (line2 != "")
            {
                if (line2.Contains("DID:"))
                {
                    DID_2S = line2;
                    DID_2.Content = DID_2S.Replace("DID:", "").Replace(" ", "");
                }
                if (line2.Contains("FID:"))
                {
                    FID_2S = line2;
                    FID_2.Content = FID_2S.Replace("FID:", "").Replace(" ", "");
                }
                if (line2.Contains("VCore (V):"))
                {
                    VID_2S = line2;
                    VID_2.Content = VID_2S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "");
                    try
                    {
                        if (int.Parse(VID_2S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "")) >= 90000)
                        {
                            VID_2.Content = VID_2S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "").Remove(4);
                            if (int.Parse(VID_2S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "").Remove(4)) >= 9000)
                            {
                                VID_2.Content = VID_2S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "").Remove(3);
                            }
                        }
                        else
                        {
                            if (int.Parse(VID_2S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "")) >= 10000)
                            {
                                VID_2.Content = VID_2S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "").Remove(4);
                            }
                            else
                            {
                                if (int.Parse(VID_2S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "")) < 10) { VID_2.Content = VID_2S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "") + "000"; }
                                else
                                {
                                    if (int.Parse(VID_2S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "")) < 100) { VID_2.Content = VID_2S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "") + "0"; }
                                    else { VID_2.Content = VID_2S.Replace("VCore (V):", "").Replace(" ", "").Replace("0.", "").Replace(".", "").Remove(3); }

                                }
                            }
                        }
                    }
                    catch { }
                }
                if (line2.Contains("Frequency (MHz):"))
                {
                    FREQ2 = line2;
                    P2_Freq.Content = FREQ2.Replace("Frequency (MHz):", "").Replace(" ", "");
                }
            }
                line2 = outputWriter2.ReadLine();
        }
        p2.WaitForExit();
        line2 = null;
    }



    // Автообновление информации
    private void StartInfoUpdate()
    {
        dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
        dispatcherTimer.Tick += async (sender, e) => await UpdateInfoAsync();
        dispatcherTimer.Interval = TimeSpan.FromMilliseconds(300);
        // Подписка на событие потери фокуса
        //App.MainWindow.Activated += Window_Activated;

        // Подписка на событие изменения видимости
        App.MainWindow.VisibilityChanged += Window_VisibilityChanged;
        dispatcherTimer.Start();
    }

    /*private void Window_Activated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.CodeActivated || args.WindowActivationState == WindowActivationState.PointerActivated)
        {
            // Окно активировано
            if (dispatcherTimer != null)
            {
                dispatcherTimer.Start();
            }
        }
        else
        {
            // Окно не активировано
            if (dispatcherTimer != null)
            {
                dispatcherTimer.Stop();
            }
        }
    }*/

    private void Window_VisibilityChanged(object sender, WindowVisibilityChangedEventArgs args)
    {
        if (args.Visible)
        {
            // Окно видимо
            if (dispatcherTimer != null)
            {
                dispatcherTimer.Start();
            }
        }
        else
        {
            // Окно не видимо
            if (dispatcherTimer != null)
            {
                dispatcherTimer.Stop();
            }
        }
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

            try
            {
                p.Start();
            }
            catch
            {
                await App.MainWindow.ShowMessageDialogAsync("Unable to start info service. Error at line 651 of ИнформацияPage.xaml.cs in com.sakuoverclock.org", "Critical Error!");
            }
            var outputWriter = p.StandardOutput;
            var line = await outputWriter.ReadLineAsync();
            while (line != null)
            {
                if (line != "")
                {
                    if (line.Contains("STAPM LIMIT"))
                    {
                        tbStapmL.Text = line;
                        tbStapmL.Text = tbStapmL.Text.Replace("STAPM LIMIT", "").Replace("|", "").Replace(" ", "").Replace("stapm-limit", "") + " W";
                    }
                    if (line.Contains("STAPM VALUE"))
                    {
                        tbStapmC.Text = line;
                        tbStapmC.Text = tbStapmC.Text.Replace("STAPM VALUE", "").Replace("|", "").Replace(" ", "") + " W";
                    }
                    if (line.Contains("PPT LIMIT FAST"))
                    {
                        tbActualL.Text = line;
                        tbActualL.Text = tbActualL.Text.Replace("PPT LIMIT FAST", "").Replace("|", "").Replace(" ", "").Replace("fast-limit", "") + " W";
                    }
                    if (line.Contains("PPT VALUE FAST"))
                    {
                        tbActualC.Text = line;
                        tbActualC.Text = tbActualC.Text.Replace("PPT VALUE FAST", "").Replace("|", "").Replace(" ", "") + " W";
                    }
                    if (line.Contains("PPT LIMIT SLOW"))
                    {
                        tbAVGL.Text = line;
                        tbAVGL.Text = tbAVGL.Text.Replace("PPT LIMIT SLOW", "").Replace("|", "").Replace(" ", "").Replace("slow-limit", "") + " W";
                        infoPOWER.Maximum = int.Parse(tbAVGL.Text.Replace(" W", "").Remove(3).Replace(".", ""));
                    }
                    if (line.Contains("PPT VALUE SLOW"))
                    {
                        tbAVGC.Text = line;
                        tbAVGC.Text = tbAVGC.Text.Replace("PPT VALUE SLOW", "").Replace("|", "").Replace(" ", "") + " W";
                        if (int.Parse(tbAVGC.Text.Replace("PPT VALUE SLOW", "").Replace("|", "").Replace(" ", "").Replace(".", "").Replace("W", "").ToString()) < 10000)
                        {
                            infoPOWER.Value = int.Parse(tbAVGC.Text.Replace(" W", "").Remove(2).Replace(".", "").Replace("nan", "100"));
                            infoPOWERI.Text = tbAVGC.Text.Replace("PPT VALUE SLOW", "").Replace("|", "").Replace(" ", "").Remove(2).Replace(".", "") + " W".ToString();
                        }
                        else
                        {
                            infoPOWER.Value = int.Parse(tbAVGC.Text.Replace(" W", "").Remove(3).Replace(".", "").Replace("nan", "100"));
                            infoPOWERI.Text = tbAVGC.Text.Replace("PPT VALUE SLOW", "").Replace("|", "").Replace(" ", "").Remove(3).Replace(".", "") + " W".ToString();
                        }
                    }
                    if (line.Contains("StapmTimeConst"))
                    {
                        tbFast.Text = line;
                        tbFast.Text = tbFast.Text.Replace("StapmTimeConst", "").Replace("|", "").Replace(" ", "").Replace("stapm-time", "") + " S";
                    }
                    if (line.Contains("SlowPPTTimeConst"))
                    {
                        tbSlow.Text = line;
                        tbSlow.Text = tbSlow.Text.Replace("SlowPPTTimeConst", "").Replace("|", "").Replace(" ", "").Replace("slow-time", "") + " S";
                    }
                    if (line.Contains("PPT LIMIT APU"))
                    {
                        if (line.Contains("nan"))
                        {
                            tbAPULL.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                            tbAPULC.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                            tbAPUML.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                            tbAPUMC.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                            tbDGPUL.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                            tbDGPUC.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                            tbAPUL.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                            tbAPUC.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                            tbAPUMaxL.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                            tbAPUMaxC.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                            tbDGPUMaxL.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                            tbDGPUMaxC.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                        }
                        else
                        {
                            tbAPUL.Text = line;
                            tbAPUL.Text = tbAPUL.Text.Replace("PPT LIMIT APU", "").Replace("|", "").Replace(" ", "").Replace("apu-slow-limit", "") + " W";
                        }
                    }
                    if (line.Contains("PPT VALUE APU"))
                    {
                        tbAPUC.Text = line;
                        tbAPUC.Text = tbAPUC.Text.Replace("PPT VALUE APU", "").Replace("|", "").Replace(" ", "") + " W";
                    }
                    if (line.Contains("TDC LIMIT VDD"))
                    {
                        tbVRMTDCL.Text = line;
                        tbVRMTDCL.Text = tbVRMTDCL.Text.Replace("TDC LIMIT VDD", "").Replace("|", "").Replace(" ", "").Replace("vrm-current", "") + " A";
                    }
                    if (line.Contains("TDC VALUE VDD"))
                    {
                        tbVRMTDCC.Text = line;
                        tbVRMTDCC.Text = tbVRMTDCC.Text.Replace("TDC VALUE VDD", "").Replace("|", "").Replace(" ", "") + " A";
                    }
                    if (line.Contains("TDC LIMIT SOC"))
                    {
                        tbSOCTDCL.Text = line;
                        tbSOCTDCL.Text = tbSOCTDCL.Text.Replace("TDC LIMIT SOC", "").Replace("|", "").Replace(" ", "").Replace("vrmsoc-current", "") + " A";
                    }
                    if (line.Contains("TDC VALUE SOC"))
                    {
                        tbSOCTDCC.Text = line;
                        tbSOCTDCC.Text = tbSOCTDCC.Text.Replace("TDC VALUE SOC", "").Replace("|", "").Replace(" ", "") + " A";
                    }
                    if (line.Contains("EDC LIMIT VDD"))
                    {
                        tbVRMEDCL.Text = line;
                        tbVRMEDCL.Text = tbVRMEDCL.Text.Replace("EDC LIMIT VDD", "").Replace("|", "").Replace(" ", "").Replace("vrmmax-current", "") + " A";
                        infoVRM.Maximum = int.Parse(tbVRMEDCL.Text.Replace(" A", "").Remove(3).Replace(".", "").Replace("nan", "000"));
                    }
                    if (line.Contains("EDC VALUE VDD"))
                    {
                        tbVRMEDCC.Text = line;
                        tbVRMEDCC.Text = tbVRMEDCC.Text.Replace("EDC VALUE VDD", "").Replace("|", "").Replace(" ", "") + " A";
                        infoVRM.Value = int.Parse(tbVRMEDCC.Text.Replace(" A", "").Remove(3).Replace(".", "").Replace("nan", "000"));
                        infoVRMI.Text = tbVRMEDCC.Text.Replace("EDC VALUE VDD", "").Replace("|", "").Replace(" ", "").Remove(3).Replace(".", "") + " A".ToString();
                    }
                    if (line.Contains("EDC LIMIT SOC"))
                    {
                        tbSOCEDCL.Text = line;
                        tbSOCEDCL.Text = tbSOCEDCL.Text.Replace("EDC LIMIT SOC", "").Replace("|", "").Replace(" ", "").Replace("vrmsocmax-current", "") + " A";
                    }
                    if (line.Contains("EDC VALUE SOC"))
                    {
                        tbSOCEDCC.Text = line;
                        tbSOCEDCC.Text = tbSOCEDCC.Text.Replace("EDC VALUE SOC", "").Replace("|", "").Replace(" ", "") + " A";
                    }
                    if (line.Contains("THM LIMIT CORE"))
                    {
                        tbCPUMaxL.Text = line;
                        tbCPUMaxL.Text = tbCPUMaxL.Text.Replace("THM LIMIT CORE", "").Replace("|", "").Replace(" ", "").Replace("tctl-temp", "") + " C";
                        infoCPU.Maximum = int.Parse(tbCPUMaxL.Text.Replace(" C", "").Remove(3).Replace(".", "").Replace("nan", "100"));
                    }
                    if (line.Contains("THM VALUE CORE"))
                    {
                        tbCPUMaxC.Text = line;
                        tbCPUMaxC.Text = tbCPUMaxC.Text.Replace("THM VALUE CORE", "").Replace("|", "").Replace(" ", "") + " C";
                        infoCPU.Value = int.Parse(tbCPUMaxC.Text.Replace(" C", "").Remove(3).Replace(".", "").Replace("nan", "000"));
                        infoCPUI.Text = tbCPUMaxC.Text.Replace("THM VALUE CORE", "").Replace("|", "").Replace(" ", "").Remove(3).Replace(".", "") + "℃".ToString();
                    }
                    if (line.Contains("STT LIMIT APU"))
                    {
                        tbAPUMaxL.Text = line;
                        tbAPUMaxL.Text = tbAPUMaxL.Text.Replace("STT LIMIT APU", "").Replace("|", "").Replace(" ", "").Replace("apu-skin-temp", "") + " C";
                    }
                    if (line.Contains("STT VALUE APU"))
                    {
                        tbAPUMaxC.Text = line;
                        tbAPUMaxC.Text = tbAPUMaxC.Text.Replace("STT VALUE APU", "").Replace("|", "").Replace(" ", "") + " C";
                    }
                    if (line.Contains("STT LIMIT dGPU"))
                    {
                        tbDGPUMaxL.Text = line;
                        tbDGPUMaxL.Text = tbDGPUMaxL.Text.Replace("STT LIMIT dGPU", "").Replace("|", "").Replace(" ", "").Replace("dgpu-skin-temp", "") + " C";
                    }
                    if (line.Contains("STT VALUE dGPU"))
                    {
                        tbDGPUMaxC.Text = line;
                        tbDGPUMaxC.Text = tbDGPUMaxC.Text.Replace("STT VALUE dGPU", "").Replace("|", "").Replace(" ", "") + " C";
                    }

                    if (line.Contains("CCLK BUSY VALUE"))
                    {
                        tbCPUUsage.Text = line;
                        tbCPUUsage.Text = tbCPUUsage.Text.Replace("CCLK BUSY VALUE", "").Replace("|", "").Replace(" ", "").Replace("max-performance", "") + " %";
                    }
                }
                line = await outputWriter.ReadLineAsync();
            }

            p.WaitForExit();
            line = null;
        }
    }

    // Ваш метод, который будет вызываться при скрытии/переключении страницы
    private void StopInfoUpdate()
    {
        dispatcherTimer?.Stop();
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
}




#pragma warning restore IDE0059 // Ненужное присваивание значения
#pragma warning restore CS8600 // Преобразование литерала, допускающего значение NULL или возможного значения NULL в тип, не допускающий значение NULL.
#pragma warning restore CS4014 // Так как этот вызов не ожидается, выполнение существующего метода продолжается до тех пор, пока вызов не будет завершен
#pragma warning restore CS8601 // Возможно, назначение-ссылка, допускающее значение NULL.