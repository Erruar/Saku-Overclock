using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.ViewModels;

namespace Saku_Overclock.Views;

public sealed partial class AsusКулерPage : Page
{
    private static int fanCount = -1;
    private static bool unavailableFlag = false;
    private static int setFanIndex = -1;
    private static int availableCPUCores = 0;
    private Config config = new();
    public bool isLoaded = false;
    private IntPtr ry = IntPtr.Zero;
    private System.Windows.Threading.DispatcherTimer? tempUpdateTimer;

    public AsusКулерPage()
    {
        InitializeComponent();
        Loaded += AsusКулерPage_Loaded;
        Unloaded += AsusКулерPage_Unloaded;
    }

    #region JSON and Initialization
    private void AsusКулерPage_Unloaded(object sender, RoutedEventArgs e)
    {
        AsusWinIOWrapper.Cleanup_WinIo();
        isLoaded = false;
        StopTempUpdate();
    }
    private void AsusКулерPage_Loaded(object sender, RoutedEventArgs e)
    {
        AsusWinIOWrapper.Init_WinIo();
        ConfigLoad();
        Fan1.Value = config.AsusModeFan1UserFanSpeedRPM;
        switch (config.AsusModeSelectedMode)
        {
            case -1:
                AsusFans_ManualToggle.IsChecked = false;
                AsusFans_TurboToggle.IsChecked = false;
                AsusFans_BalanceToggle.IsChecked = false;
                AsusFans_QuietToggle.IsChecked = false;
                break;
            case 0:
                AsusFans_ManualToggle.IsChecked = true;
                AsusFans_TurboToggle.IsChecked = false;
                AsusFans_BalanceToggle.IsChecked = false;
                AsusFans_QuietToggle.IsChecked = false;
                break;
            case 1:
                AsusFans_ManualToggle.IsChecked = false;
                AsusFans_TurboToggle.IsChecked = true;
                AsusFans_BalanceToggle.IsChecked = false;
                AsusFans_QuietToggle.IsChecked = false;
                break;
            case 2:
                AsusFans_ManualToggle.IsChecked = false;
                AsusFans_TurboToggle.IsChecked = false;
                AsusFans_BalanceToggle.IsChecked = true;
                AsusFans_QuietToggle.IsChecked = false;
                break;
            case 3:
                AsusFans_ManualToggle.IsChecked = false;
                AsusFans_TurboToggle.IsChecked = false;
                AsusFans_BalanceToggle.IsChecked = false;
                AsusFans_QuietToggle.IsChecked = true;
                break;
        };
        isLoaded = true;
        UpdateSystemInformation();
        ry = RyzenADJWrapper.Init_ryzenadj();
        RyzenADJWrapper.Init_Table(ry);
        StartTempUpdate();
    }
    private void StartTempUpdate()
    {
        tempUpdateTimer = new System.Windows.Threading.DispatcherTimer();
        tempUpdateTimer.Tick += async (sender, e) => await UpdateTemperatureAsync();
        tempUpdateTimer.Interval = TimeSpan.FromMilliseconds(500);
        tempUpdateTimer.Start();
    }
    private async Task UpdateTemperatureAsync()
    {
        var tempLine = string.Empty;
        await Task.Run(() =>
        {
            var fanSpeeds = GetFanSpeeds();
            if (fanSpeeds.Count > 0)
            {
                tempLine = fanSpeeds[0].ToString();
            }
            else
            {
                tempLine = "-.-";
            }
        });
        ry = RyzenADJWrapper.Init_ryzenadj();
        RyzenADJWrapper.Init_Table(ry);
        _ = RyzenADJWrapper.Refresh_table(ry);
        var avgCoreCLK = 0d;
        var avgCoreVolt = 0d;
        var countCLK = 0;
        var countVolt = 0;
        if (availableCPUCores == 0)
        {
            availableCPUCores = ИнформацияPage.GetCPUCoresAsync().Result; // Оптимизация приложения, лишний раз не обновлять это значение
        }
        for (var f = 0u; f < availableCPUCores; f++)
        {
            if (f < 8)
            {
                var clk = Math.Round(RyzenADJWrapper.Get_core_clk(ry, f), 3);
                var volt = Math.Round(RyzenADJWrapper.Get_core_volt(ry, f), 3);
                if (clk != 0)
                {
                    avgCoreCLK += clk;
                    countCLK += 1;
                }
                if (volt != 0)
                {
                    avgCoreVolt += volt;
                    countVolt += 1;
                }
            }
        }
        if (countCLK == 0) { countCLK = 1; }
        if (countVolt == 0) { countVolt = 1; }
        UpdateValues(Math.Round(RyzenADJWrapper.Get_tctl_temp_value(ry), 3) + "℃", Math.Round(avgCoreCLK / countCLK, 3) + "GHz", Math.Round(avgCoreVolt / countVolt, 3) + "V", tempLine);
    }
    private void UpdateValues(string Temp, string Freq, string Volt, string RPM) // Обновление информации вне асинхронного метода
    {
        CPUTemp.Text = Temp;
        CPUFreq.Text = Freq;
        CPUVolt.Text = Volt;
        CPUFanRPM.Text = RPM;
    }
    private void StopTempUpdate()
    {
        RyzenADJWrapper.Cleanup_ryzenadj(ry);
        tempUpdateTimer?.Stop();
    }
    private void UpdateSystemInformation()
    {
        var prod = GetSystemInfo.Product;
        LaptopName.Text = (prod?.Contains("Asus") == true || prod?.Contains("ASUS") == true || prod?.Contains("asus") == true)  ? "Asus " + prod?.Replace("ASUS", "").Replace("Asus", "").Replace("asus", "").Replace('_', ' ').Replace("  ", " ") 
            : prod?.Replace('_', ' ').Replace("  ", " ");
        OSName.Text = GetSystemInfo.GetOSVersion() + " " + GetSystemInfo.GetWindowsEdition();
        BIOSVersion.Text = GetSystemInfo.GetBIOSVersion();
        fanCount = HealthyTable_FanCounts();
        if (fanCount == -1)
        {
            UnavailableLabel.IsOpen = true;
            unavailableFlag = true;
        }
    }
    public void ConfigSave()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json", JsonConvert.SerializeObject(config, Formatting.Indented));
        }
        catch
        {

        }
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
    #endregion
    #region Event Handlers 
    private void NbfcCoolerMode_Click(object sender, RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(КулерViewModel).FullName!); // Вернуться в обычный режим
    }

    private void Fan1_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!isLoaded) { return; }
        ConfigLoad();
        config.AsusModeSelectedMode = 0;
        config.AsusModeFan1UserFanSpeedRPM = Fan1.Value;
        ConfigSave();
        SetFanSpeeds((int)Fan1.Value);
    }

    private void AsusFans_ManualToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (!isLoaded) { return; }
        var toggleButton = sender as ToggleButton;
        if (toggleButton == null) { return; }
        if (toggleButton.IsChecked == true)
        {
            AsusWinIOWrapper.Init_WinIo();
            switch (toggleButton.Name)
            {
                case "AsusFans_ManualToggle":
                    ConfigLoad();
                    config.AsusModeSelectedMode = 0; 
                    config.AsusModeFan1UserFanSpeedRPM = Fan1.Value;
                    ConfigSave();
                    AsusFans_BalanceToggle.IsChecked = false;
                    AsusFans_TurboToggle.IsChecked = false;
                    AsusFans_QuietToggle.IsChecked = false;
                    SetFanSpeeds((int)Fan1.Value);
                    break;
                case "AsusFans_TurboToggle":
                    ConfigLoad();
                    config.AsusModeSelectedMode = 1;
                    ConfigSave();
                    AsusFans_BalanceToggle.IsChecked = false;
                    AsusFans_ManualToggle.IsChecked = false;
                    AsusFans_QuietToggle.IsChecked = false;
                    SetFanSpeeds(90);
                    break;
                case "AsusFans_BalanceToggle":
                    ConfigLoad();
                    config.AsusModeSelectedMode = 2;
                    ConfigSave();
                    AsusFans_ManualToggle.IsChecked = false;
                    AsusFans_TurboToggle.IsChecked = false;
                    AsusFans_QuietToggle.IsChecked = false;
                    SetFanSpeeds(57);
                    break;
                case "AsusFans_QuietToggle":
                    ConfigLoad();
                    config.AsusModeSelectedMode = 3;
                    ConfigSave();
                    AsusFans_BalanceToggle.IsChecked = false;
                    AsusFans_TurboToggle.IsChecked = false;
                    AsusFans_ManualToggle.IsChecked = false;
                    SetFanSpeeds(37);
                    break;
                default:
                    break; 
            };
        }
        if (AsusFans_BalanceToggle.IsChecked == false && AsusFans_ManualToggle.IsChecked == false &&
            AsusFans_QuietToggle.IsChecked == false && AsusFans_TurboToggle.IsChecked == false)
        {
            ConfigLoad();
            config.AsusModeSelectedMode = -1; 
            ConfigSave();
            SetFanSpeeds(0);
        }
    }
    #endregion
    #region Asus WinIo Voids
    public static void SetFanSpeed(byte value, byte fanIndex = 0)
    {
        AsusWinIOWrapper.HealthyTable_SetFanIndex(fanIndex);
        AsusWinIOWrapper.HealthyTable_SetFanTestMode((char)(value > 0 ? 0x01 : 0x00));
        AsusWinIOWrapper.HealthyTable_SetFanPwmDuty(value);
    }

    public static void SetFanSpeed(int percent, byte fanIndex = 0)
    {
        var value = (byte)(percent / 100.0f * 255);
        SetFanSpeed(value, fanIndex);
    }

    public static async void SetFanSpeeds(byte value)
    {
        if (fanCount == -1 && unavailableFlag == false)
        {
            fanCount = AsusWinIOWrapper.HealthyTable_FanCounts(); // Не обновлять лишний раз это значение
        }
        for (byte fanIndex = 0; fanIndex < fanCount; fanIndex++)
        {
            SetFanSpeed(value, fanIndex);
            await Task.Delay(20);
        }
    }

    public static void SetFanSpeeds(int percent)
    {
        var value = (byte)(percent / 100.0f * 255);
        SetFanSpeeds(value);
    }

    public static int GetFanSpeed(byte fanIndex = 0)
    {
        if (setFanIndex != fanIndex)
        {
            AsusWinIOWrapper.HealthyTable_SetFanIndex(fanIndex);
            setFanIndex = fanIndex; // Лишний раз не использовать, после использования задать значение, которое было использовано
        }
        var fanSpeed = AsusWinIOWrapper.HealthyTable_FanRPM();
        return fanSpeed;
    }

    public static List<int> GetFanSpeeds()
    {
        var fanSpeeds = new List<int>();

        if (fanCount == -1 && unavailableFlag == false)
        {
            fanCount = AsusWinIOWrapper.HealthyTable_FanCounts(); // Не обновлять лишний раз это значение
        }
        for (byte fanIndex = 0; fanIndex < fanCount; fanIndex++)
        {
            var fanSpeed = GetFanSpeed(fanIndex);
            fanSpeeds.Add(fanSpeed);
        }

        return fanSpeeds;
    }

    public static int HealthyTable_FanCounts()
    {
        return AsusWinIOWrapper.HealthyTable_FanCounts();
    } 
    #endregion
}
