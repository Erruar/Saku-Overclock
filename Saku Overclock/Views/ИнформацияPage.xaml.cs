using System.Diagnostics;
using System.Management;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using Saku_Overclock.ViewModels;
namespace Saku_Overclock.Views;
#pragma warning disable CS4014 // Так как этот вызов не ожидается, выполнение существующего метода продолжается до тех пор, пока вызов не будет завершен
#pragma warning disable CS8600 // Преобразование литерала, допускающего значение NULL или возможного значения NULL в тип, не допускающий значение NULL.
#pragma warning disable IDE0059 // Ненужное присваивание значения
#pragma warning disable IDE0044 // Ненужное присваивание значения
#pragma warning disable CS8601 // Возможно, назначение-ссылка, допускающее значение NULL.
public sealed partial class ИнформацияPage : Page
{
    private Config config = new();
    public double refreshtime;
    public ИнформацияViewModel ViewModel
    {
        get;
    }
    public ИнформацияPage()
    {
        ViewModel = App.GetService<ИнформацияViewModel>();
        InitializeComponent();
        desc();
        ConfigLoad();
        InitInf();
        config.fanex = false;
        if (config.reapplyinfo == false) { Process(); }
        ConfigSave();
        // Инициализация таймера
        getCPUInfo();
    }

    public async void InitInf()
    {
        await Task.Delay(200);
        ConfigLoad();
        if (config.reapplyinfo == true) { Absc.IsChecked = true; } else { Absc.IsChecked = false; }
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

    private void desc()
    {
        if (richTextBox1.Text == "")
        {
            DescText.Opacity = 0;
        }
        else
        {
            DescText.Opacity = 1;
        }
    }
    private void xx_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        config.tempex = true;
        Absc.IsChecked = false;
        ConfigSave();
        Check_Info();
    }
    private async void Check_Info()
    {
        if (config.tempex == true)
        {
            Process();
            await Task.Delay(20);
            if (Absc.IsChecked == true) { try { await Task.Delay((int)refreshtime); } catch { refreshtime = 100; numberBox.Value = 100; await Task.Delay((int)refreshtime); } Check_Info(); }
        }
    }
    private void Process()
    {
        richTextBox1.Text = "";
        richTextBox2.Text = "";
        Process p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = @"ryzenadj.exe";
        p.StartInfo.Arguments = "-i";
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;
        ConfigLoad();
        if (config.tempex == true)
        {
            p.Start();
            config.tempex = false;
            StreamReader outputWriter = p.StandardOutput;
            var line = outputWriter.ReadLine();
            while (line != null)
            {
                if (line != "")
                {
                    richTextBox1.Text = richTextBox1.Text + "\n" + line;
                    if (line.Contains("CPU Family:") || line.Contains("SMU BIOS Interface") || line.Contains("Version:") || line.Contains("PM Table") || line.Contains("Name ") || line.Contains("|----") ) { }
                    else
                    {
                        if (line.Contains("STAPM VALUE"))
                        {
                            richTextBox2.Text = richTextBox2.Text + "\n" + line + "  - Лимит CPU (W), Текущее значение";
                            richTextBox2.Text = richTextBox2.Text.Replace("STAPM VALUE", "");
                        }
                        else
                        {
                            richTextBox2.Text = richTextBox2.Text + "\n" + line;
                        }
                        richTextBox2.Text = richTextBox2.Text.Replace("|", "");
                        richTextBox2.Text = richTextBox2.Text.Replace("STAPM LIMIT", "");
                        richTextBox2.Text = richTextBox2.Text.Replace("PPT LIMIT FAST ", "");
                        richTextBox2.Text = richTextBox2.Text.Replace("PPT LIMIT SLOW ", "");
                        richTextBox2.Text = richTextBox2.Text.Replace("StapmTimeConst  ", "");
                        richTextBox2.Text = richTextBox2.Text.Replace("SlowPPTTimeConst  ", "");
                        richTextBox2.Text = richTextBox2.Text.Replace("TDC LIMIT VDD ", "");
                        richTextBox2.Text = richTextBox2.Text.Replace("EDC LIMIT VDD  ", "");
                        richTextBox2.Text = richTextBox2.Text.Replace("TDC LIMIT SOC ", "");
                        richTextBox2.Text = richTextBox2.Text.Replace("EDC LIMIT SOC ", "");
                        richTextBox2.Text = richTextBox2.Text.Replace("THM LIMIT CORE ", "");
                        richTextBox2.Text = richTextBox2.Text.Replace("STT LIMIT APU ", "");
                        richTextBox2.Text = richTextBox2.Text.Replace("PPT LIMIT APU ", "");
                        richTextBox2.Text = richTextBox2.Text.Replace("STT LIMIT dGPU", "");
                        richTextBox2.Text = richTextBox2.Text.Replace("CCLK Boost SETPOINT", "");
                        richTextBox2.Text = richTextBox2.Text.Replace("CCLK BUSY VALUE", "");
                        richTextBox2.Text = richTextBox2.Text.Replace("  ", "");
                        richTextBox2.Text = richTextBox2.Text.Replace("stapm-limit", " - Лимит CPU (W), Установленное значение");
                        richTextBox2.Text = richTextBox2.Text.Replace("fast-limit", " - Реальный CPU (W), Установленное значение");
                        richTextBox2.Text = richTextBox2.Text.Replace("apu-slow-limit", " - Мощность APU (W), Установленное значение");
                        richTextBox2.Text = richTextBox2.Text.Replace("slow-limit", " - Средний CPU (W), Установленное значение");
                        richTextBox2.Text = richTextBox2.Text.Replace("stapm-time", " - Тик быстрого разгона (S)");
                        richTextBox2.Text = richTextBox2.Text.Replace("slow-time", " - Тик медленного разгона (S)");
                        richTextBox2.Text = richTextBox2.Text.Replace("vrm-current", " - Лимит по току VRM (A), Установленное значение");
                        richTextBox2.Text = richTextBox2.Text.Replace("vrmsoc-current", " - Лимит по току SoC (A), Установленное значение");
                        richTextBox2.Text = richTextBox2.Text.Replace("vrmmax-current", " - Максимальный ток VRM (A), Установленное значение");
                        richTextBox2.Text = richTextBox2.Text.Replace("vrmsocmax-current", " - Максимальный ток SoC (A), Установленное значение");
                        richTextBox2.Text = richTextBox2.Text.Replace("tctl-temp", " - Максимальная температура CPU (C)");
                        richTextBox2.Text = richTextBox2.Text.Replace("apu-skin-temp", " - Максимальная температура iGPU (C)");
                        richTextBox2.Text = richTextBox2.Text.Replace("dgpu-skin-temp", " - Максимальная температура dGPU (C)");
                        richTextBox2.Text = richTextBox2.Text.Replace("power-saving /", " - Процент начала троттлинга (% Загрузки CPU)");
                        richTextBox2.Text = richTextBox2.Text.Replace("max-performance", " - Текущая загрузка процессора (%)");
                    }
                    richTextBox1.Text = richTextBox1.Text.Replace("nan", "✕  ");
                    DescText.Opacity = 1;
                }
                line = outputWriter.ReadLine();
            }
            p.WaitForExit();
            line = null;
        }
        config.tempex = true;
    }
    private async void CheckBox_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await Task.Delay(20);
        if (Absc.IsChecked == true) { config.reapplyinfo = true; ConfigSave(); } else { config.reapplyinfo = false; ConfigSave(); }

        // Обновите Textblock и скрипт через заданный интервал времени
        try
        {
            refreshtime = numberBox.Value;
        }
        catch
        {
            App.MainWindow.ShowMessageDialogAsync("Время автообновления информации некорректно и было исправлено на 100 мс", "Критическая ошибка!");
            numberBox.Value = 100;
        }
        config.tempex = true;
        ConfigSave();
        Check_Info();
    }

    private void numberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        refreshtime = numberBox.Value;
        config.tempex = false;
        Absc.IsChecked = false;
        config.tempex = true;
        Absc.IsChecked = true;
    }
    //AI Gen
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
            //string codeName = GetSystemInfo.Codename();
            /*if (codeName != "") tbCodename.Text = codeName;
            else
            {
                tbCodename.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                tbCode.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            }
*/
            tbProducer.Text = manufacturer;
            if (numberOfLogicalProcessors == numberOfCores) tbCores.Text = numberOfCores.ToString();
            //else tbCores.Text = GetSystemInfo.getBigLITTLE(numberOfCores, l2Size);
            tbThreads.Text = numberOfLogicalProcessors.ToString();
            tbL3Cache.Text = $"{l3Size.ToString("0.##")} MB";

            uint sum = 0;
            //foreach (uint number in GetSystemInfo.GetCacheSizes(CacheLevel.Level1)) sum += number;
            decimal total = sum;
            total = total / 1024;
            tbL1Cache.Text = $"{total.ToString("0.##")} MB";

            sum = 0;
            //foreach (uint number in GetSystemInfo.GetCacheSizes(CacheLevel.Level2)) sum += number;
            total = sum;
            total = total / 1024;
            tbL2Cache.Text = $"{total.ToString("0.##")} MB";

            tbBaseClock.Text = $"{baseClock} MHz";

            //tbInstructions.Text = GetSystemInfo.InstructionSets();

            sdCPU.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        }
        catch (ManagementException ex)
        {
            Console.WriteLine("An error occurred while querying for WMI data: " + ex.Message);
        }
    }
}


#pragma warning restore IDE0059 // Ненужное присваивание значения
#pragma warning restore CS8600 // Преобразование литерала, допускающего значение NULL или возможного значения NULL в тип, не допускающий значение NULL.
#pragma warning restore CS4014 // Так как этот вызов не ожидается, выполнение существующего метода продолжается до тех пор, пока вызов не будет завершен
#pragma warning restore CS8601 // Возможно, назначение-ссылка, допускающее значение NULL.