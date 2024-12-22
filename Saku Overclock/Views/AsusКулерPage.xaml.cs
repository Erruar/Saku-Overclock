using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives; 
using Saku_Overclock.Contracts.Services; 
using Saku_Overclock.SMUEngine;
using Saku_Overclock.ViewModels;

namespace Saku_Overclock.Views;

public sealed partial class AsusКулерPage
{
    private static int _fanCount = -1;
    private static bool _unavailableFlag;
    private static int _setFanIndex = -1;
    private static int _availableCpuCores; 
    private bool _isPageLoaded;
    private IntPtr _ry = IntPtr.Zero;
    private DispatcherTimer? _tempUpdateTimer;
    private static readonly IAppSettingsService SettingsService = App.GetService<IAppSettingsService>();

    public AsusКулерPage()
    {
        InitializeComponent();
        Loaded += AsusКулерPage_Loaded;
        Unloaded += AsusКулерPage_Unloaded;
    }

    #region Initialization

    private void AsusКулерPage_Unloaded(object sender, RoutedEventArgs e)
    {
        AsusWinIOWrapper.Cleanup_WinIo();
        _isPageLoaded = false;
        StopTempUpdate();
    }

    private void AsusКулерPage_Loaded(object sender, RoutedEventArgs e)
    {
        AsusWinIOWrapper.Init_WinIo(); 
        Fan1.Value = SettingsService.AsusModeFan1UserFanSpeedRPM;
        switch (SettingsService.AsusModeSelectedMode)
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
        }

        _isPageLoaded = true;
        UpdateSystemInformation();
        _ry = RyzenADJWrapper.Init_ryzenadj();
        RyzenADJWrapper.Init_Table(_ry);
        StartTempUpdate();
    }

    private void StartTempUpdate()
    {
        _tempUpdateTimer = new DispatcherTimer();
        _tempUpdateTimer.Tick += async (_, _) => await UpdateTemperatureAsync();
        _tempUpdateTimer.Interval = TimeSpan.FromMilliseconds(500);
        _tempUpdateTimer.Start();
    }

    private async Task UpdateTemperatureAsync()
    {
        var tempLine = string.Empty;
        await Task.Run(() =>
        {
            var fanSpeeds = GetFanSpeeds();
            tempLine = fanSpeeds.Count > 0 ? fanSpeeds[0].ToString() : "-.-";
        });
        _ry = RyzenADJWrapper.Init_ryzenadj();
        if (_ry == IntPtr.Zero)
        {
            CPUFanRPM.Text = tempLine;
            return;
        }

        RyzenADJWrapper.Init_Table(_ry);
        _ = RyzenADJWrapper.Refresh_table(_ry);
        var avgCoreClk = 0d;
        var avgCoreVolt = 0d;
        var countClk = 0;
        var countVolt = 0;
        if (_availableCpuCores == 0)
        {
            _availableCpuCores =
                await ИнформацияPage.GetCpuCoresAsync(); // Оптимизация приложения, лишний раз не обновлять это значение
        }

        for (var f = 0u; f < _availableCpuCores; f++)
        {
            if (f < 8)
            {
                var clk = Math.Round(RyzenADJWrapper.Get_core_clk(_ry, f), 3);
                var volt = Math.Round(RyzenADJWrapper.Get_core_volt(_ry, f), 3);
                if (clk != 0)
                {
                    avgCoreClk += clk;
                    countClk += 1;
                }

                if (volt != 0)
                {
                    avgCoreVolt += volt;
                    countVolt += 1;
                }
            }
        }

        if (countClk == 0)
        {
            countClk = 1;
        }

        if (countVolt == 0)
        {
            countVolt = 1;
        }

        UpdateValues(Math.Round(RyzenADJWrapper.Get_tctl_temp_value(_ry), 3) + "℃",
            Math.Round(avgCoreClk / countClk, 3) + "GHz", Math.Round(avgCoreVolt / countVolt, 3) + "V", tempLine);
    }

    private void
        UpdateValues(string temp, string freq, string volt, string rpm) // Обновление информации вне асинхронного метода
    {
        CPUTemp.Text = temp;
        CPUFreq.Text = freq;
        CPUVolt.Text = volt;
        CPUFanRPM.Text = rpm;
    }

    private void StopTempUpdate()
    {
        RyzenADJWrapper.Cleanup_ryzenadj(_ry);
        _tempUpdateTimer?.Stop();
    }

    private void UpdateSystemInformation()
    {
        var prod = GetSystemInfo.Product;
        LaptopName.Text =
            (prod?.Contains("Asus") == true || prod?.Contains("ASUS") == true || prod?.Contains("asus") == true)
                ? "Asus " + prod.Replace("ASUS", "").Replace("Asus", "").Replace("asus", "").Replace('_', ' ')
                    .Replace("  ", " ")
                : prod?.Replace('_', ' ').Replace("  ", " ");
        OSName.Text = GetSystemInfo.GetOSVersion() + " " + GetSystemInfo.GetWindowsEdition();
        BIOSVersion.Text = GetSystemInfo.GetBIOSVersion();
        _fanCount = HealthyTable_FanCounts();
        if (_fanCount == -1)
        {
            UnavailableLabel.IsOpen = true;
            _unavailableFlag = true;
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
        if (!_isPageLoaded)
        {
            return;
        }
 
        SettingsService.AsusModeSelectedMode = 0;
        SettingsService.AsusModeFan1UserFanSpeedRPM = Fan1.Value;
        SettingsService.SaveSettings();
        SetFanSpeeds((int)Fan1.Value);
    }

    private void AsusFans_ManualToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isPageLoaded)
        {
            return;
        }

        var toggleButton = sender as ToggleButton;
        if (toggleButton == null)
        {
            return;
        }

        if (toggleButton.IsChecked == true)
        {
            AsusWinIOWrapper.Init_WinIo();
            switch (toggleButton.Name)
            {
                case "AsusFans_ManualToggle": 
                    SettingsService.AsusModeSelectedMode = 0;
                    SettingsService.AsusModeFan1UserFanSpeedRPM = Fan1.Value; 
                    AsusFans_BalanceToggle.IsChecked = false;
                    AsusFans_TurboToggle.IsChecked = false;
                    AsusFans_QuietToggle.IsChecked = false;
                    SetFanSpeeds((int)Fan1.Value);
                    break;
                case "AsusFans_TurboToggle": 
                    SettingsService.AsusModeSelectedMode = 1; 
                    AsusFans_BalanceToggle.IsChecked = false;
                    AsusFans_ManualToggle.IsChecked = false;
                    AsusFans_QuietToggle.IsChecked = false;
                    SetFanSpeeds(90);
                    break;
                case "AsusFans_BalanceToggle": 
                    SettingsService.AsusModeSelectedMode = 2; 
                    AsusFans_ManualToggle.IsChecked = false;
                    AsusFans_TurboToggle.IsChecked = false;
                    AsusFans_QuietToggle.IsChecked = false;
                    SetFanSpeeds(57);
                    break;
                case "AsusFans_QuietToggle": 
                    SettingsService.AsusModeSelectedMode = 3; 
                    AsusFans_BalanceToggle.IsChecked = false;
                    AsusFans_TurboToggle.IsChecked = false;
                    AsusFans_ManualToggle.IsChecked = false;
                    SetFanSpeeds(37);
                    break;
            }
            SettingsService.SaveSettings();

        }

        if (AsusFans_BalanceToggle.IsChecked == false && AsusFans_ManualToggle.IsChecked == false &&
            AsusFans_QuietToggle.IsChecked == false && AsusFans_TurboToggle.IsChecked == false)
        { 
            SettingsService.AsusModeSelectedMode = -1;
            SettingsService.SaveSettings(); 
            SetFanSpeeds(0);
        }
    }

    #endregion

    #region Asus WinIo Voids

    private static void SetFanSpeed(byte value, byte fanIndex = 0)
    {
        AsusWinIOWrapper.HealthyTable_SetFanIndex(fanIndex);
        AsusWinIOWrapper.HealthyTable_SetFanTestMode((char)(value > 0 ? 0x01 : 0x00));
        AsusWinIOWrapper.HealthyTable_SetFanPwmDuty(value);
    }

    private static async void SetFanSpeeds(byte value)
    {
        try
        {
            if (_fanCount == -1 && _unavailableFlag == false)
            {
                _fanCount = AsusWinIOWrapper.HealthyTable_FanCounts(); // Не обновлять лишний раз это значение
            }

            for (byte fanIndex = 0; fanIndex < _fanCount; fanIndex++)
            {
                SetFanSpeed(value, fanIndex);
                await Task.Delay(20);
            }
        }
        catch (Exception e)
        {
            SendSmuCommand.TraceIt_TraceError(e.ToString());
        }
    }

    private static void SetFanSpeeds(int percent)
    {
        var value = (byte)(percent / 100.0f * 255);
        SetFanSpeeds(value);
    }

    private static int GetFanSpeed(byte fanIndex = 0)
    {
        if (_setFanIndex != fanIndex)
        {
            AsusWinIOWrapper.HealthyTable_SetFanIndex(fanIndex);
            _setFanIndex =
                fanIndex; // Лишний раз не использовать, после использования задать значение, которое было использовано
        }

        var fanSpeed = AsusWinIOWrapper.HealthyTable_FanRPM();
        return fanSpeed;
    }

    private static List<int> GetFanSpeeds()
    {
        var fanSpeeds = new List<int>();

        if (_fanCount == -1 && _unavailableFlag == false)
        {
            _fanCount = AsusWinIOWrapper.HealthyTable_FanCounts(); // Не обновлять лишний раз это значение
        }

        for (byte fanIndex = 0; fanIndex < _fanCount; fanIndex++)
        {
            var fanSpeed = GetFanSpeed(fanIndex);
            fanSpeeds.Add(fanSpeed);
        }

        return fanSpeeds;
    }

    private static int HealthyTable_FanCounts()
    {
        return AsusWinIOWrapper.HealthyTable_FanCounts();
    }

    #endregion
}