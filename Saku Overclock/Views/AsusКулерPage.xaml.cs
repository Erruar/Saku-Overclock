using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.ViewModels;
using Newtonsoft.Json;
using Microsoft.VisualBasic.Devices;
using Microsoft.Win32;

namespace Saku_Overclock.Views;

public sealed partial class AsusКулерPage : Page
{
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
        isLoaded = true;
        ConfigLoad();
        Fanauto.IsChecked = config.AsusModeAutoUpdateInformation;
        UpdateSystemInformation();
        ry = RyzenADJWrapper.Init_ryzenadj();
        RyzenADJWrapper.Init_Table(ry);
        StartTempUpdate();
    }
    private void StartTempUpdate()
    {
        tempUpdateTimer = new System.Windows.Threading.DispatcherTimer();
        tempUpdateTimer.Tick += (sender, e) => UpdateTemperatureAsync();
        tempUpdateTimer.Interval = TimeSpan.FromMilliseconds(500);
        tempUpdateTimer.Start();
    }
    private Task UpdateTemperatureAsync()
    {
        ry = RyzenADJWrapper.Init_ryzenadj();
        RyzenADJWrapper.Init_Table(ry);
        _ = RyzenADJWrapper.refresh_table(ry);
        var avgCoreCLK = 0d;
        var avgCoreVolt = 0d;
        var countCLK = 0;
        var countVolt = 0;
        for (var f = 0u; f < ИнформацияPage.GetCPUCores(); f++)
        {
            if (f < 8)
            {
                var clk = Math.Round(RyzenADJWrapper.get_core_clk(ry, f), 3);
                var volt = Math.Round(RyzenADJWrapper.get_core_volt(ry, f), 3);
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
        CPUTemp.Text = Math.Round(RyzenADJWrapper.get_tctl_temp_value(ry), 3) + "℃";
        CPUFreq.Text = Math.Round(avgCoreCLK /countCLK, 3) + "GHz";
        CPUVolt.Text = Math.Round(avgCoreVolt /countVolt, 3) + "V";
        CPUFanRPM.Text = HealthyTable_FanCounts().ToString();
        return Task.CompletedTask;
    }
    private void StopTempUpdate()
    { 
        RyzenADJWrapper.Cleanup_ryzenadj(ry);
        tempUpdateTimer?.Stop();
    }
    private void UpdateSystemInformation()
    {
        LaptopName.Text = "Asus " + GetSystemInfo.Product?.Replace("ASUS","").Replace("Asus","").Replace("asus","").Replace('_',' ').Replace("  "," ");
        OSName.Text = GetSystemInfo.GetOSVersion() + " " + GetSystemInfo.GetWindowsEdition();
        BIOSVersion.Text = GetSystemInfo.GetBIOSVersion();
    }
    public void ConfigSave()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json", JsonConvert.SerializeObject(config));
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

    private void Fan1_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {

    }

    private void NbfcCoolerMode_Click(object sender, RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(КулерViewModel).FullName!);
    }

    private void Update_Click(object sender, RoutedEventArgs e)
    {

    }

    private void Fanauto_Checked(object sender, RoutedEventArgs e)
    {
        if (!isLoaded) { return; }
        ConfigLoad();
        config.AsusModeAutoUpdateInformation = Fanauto.IsChecked == true ? true : false;
        ConfigSave();
    }
    #endregion
    #region Asus WinIo Voids
    public static void SetFanSpeed(byte value, byte fanIndex = 0)
    {
        AsusWinIOWrapper.HealthyTable_SetFanIndex(fanIndex);
        AsusWinIOWrapper.HealthyTable_SetFanTestMode((char)(value > 0 ? 0x01 : 0x00));
        AsusWinIOWrapper.HealthyTable_SetFanPwmDuty(value);
    }

    public void SetFanSpeed(int percent, byte fanIndex = 0)
    {
        var value = (byte)(percent / 100.0f * 255);
        SetFanSpeed(value, fanIndex);
    }

    public async void SetFanSpeeds(byte value)
    {
        var fanCount = AsusWinIOWrapper.HealthyTable_FanCounts();
        for (byte fanIndex = 0; fanIndex < fanCount; fanIndex++)
        {
            SetFanSpeed(value, fanIndex);
            await Task.Delay(20);
        }
    }

    public void SetFanSpeeds(int percent)
    {
        var value = (byte)(percent / 100.0f * 255);
        SetFanSpeeds(value);
    }

    public static int GetFanSpeed(byte fanIndex = 0)
    {
        AsusWinIOWrapper.HealthyTable_SetFanIndex(fanIndex);
        var fanSpeed = AsusWinIOWrapper.HealthyTable_FanRPM();
        return fanSpeed;
    }

    public List<int> GetFanSpeeds()
    {
        var fanSpeeds = new List<int>();

        var fanCount = AsusWinIOWrapper.HealthyTable_FanCounts();
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

    public static ulong Thermal_Read_Cpu_Temperature()
    {
        return AsusWinIOWrapper.Thermal_Read_Cpu_Temperature();
    }
    #endregion
}
