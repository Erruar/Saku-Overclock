using System.Diagnostics;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using Octokit;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Services;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.ViewModels;
using Windows.Foundation.Metadata;
using Windows.UI.Text;
using Application = Microsoft.UI.Xaml.Application;
using FileMode = System.IO.FileMode;

namespace Saku_Overclock.Views;

public sealed partial class КулерPage
{
    private bool _isPageLoaded;
    private bool _isNbfcNotLoaded;
    private double _cpuTemp;
    private double _gpuTemp;
    private double _cpuTdpLim;
    private double _cpuTdpVal;
    private double _cpuFreq;
    private double _cpuCurrent;
    private DispatcherTimer? _tempUpdateTimer;
    private readonly DispatcherTimer? _fanUpdateTimer;
    private static readonly IAppSettingsService AppSettings = App.GetService<IAppSettingsService>();
    private readonly IBackgroundDataUpdater _dataUpdater;


    public КулерPage()
    {
        App.GetService<КулерViewModel>();
        InitializeComponent();
        FanInit();
        UpdatePageFanRpms();
        AppSettings.SaveSettings();

        Loaded += Page_Loaded;
        _fanUpdateTimer = new DispatcherTimer();
        _fanUpdateTimer.Tick += async (_, _) => await CheckFan();
        _fanUpdateTimer.Interval = TimeSpan.FromMilliseconds(6000);
        _dataUpdater = App.BackgroundUpdater!;
        _dataUpdater.DataUpdated += OnDataUpdated;
        Unloaded += Page_Unloaded;
    }

    #region Page Navigation and Window State

    private void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.CodeActivated ||
            args.WindowActivationState == WindowActivationState.PointerActivated)
        {
            _tempUpdateTimer?.Start();
            GetFanSpeedsThroughNbfc(true);
        }
        else
        {
            _tempUpdateTimer?.Stop();
            GetFanSpeedsThroughNbfc(false);
        }
    }

    private void Window_VisibilityChanged(object sender, WindowVisibilityChangedEventArgs args)
    {
        if (args.Visible)
        {
            _tempUpdateTimer?.Start();
            GetFanSpeedsThroughNbfc(true);
        }
        else
        {
            _tempUpdateTimer?.Stop();
            GetFanSpeedsThroughNbfc(false);
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        StartTempUpdate();
        GetFanSpeedsThroughNbfc(true);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        StopTempUpdate();
        GetFanSpeedsThroughNbfc(false);
    }

    private void AdvancedCooler_Click(object sender, RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(AdvancedКулерViewModel).FullName!);
    }

    #endregion

    #region Initialization

    private async void FanInit()
    {
        try
        {
            if (AppSettings.IsNBFCModeEnabled)
            {
                AsusOptions_Button.IsChecked = true;
                NbfcOptions_Button.IsChecked = false;
                ProfileSettings_StackPanel.Visibility = Visibility.Visible;
                ProfileSettings_BeginnerView.Visibility = Visibility.Collapsed;
                ProfileSettings_ChangeViewStackPanel.Visibility = Visibility.Visible;
            }
            else
            {
                AsusOptions_Button.IsChecked = false;
                NbfcOptions_Button.IsChecked = true;
                ProfileSettings_StackPanel.Visibility = Visibility.Collapsed;
                ProfileSettings_BeginnerView.Visibility = Visibility.Visible;
                ProfileSettings_ChangeViewStackPanel.Visibility = Visibility.Visible;
            }
            const string
                folderPath =
                    @"C:\Program Files (x86)\NoteBook FanControl\Configs"; // Получить папку, в которой хранятся файлы XML с конфигами
            var xmlFiles = Directory.GetFiles(folderPath, "*.xml");
            Selfan.Items.Clear();
            foreach (var xmlFile in xmlFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(xmlFile);
                if (fileName.Contains(".xml"))
                {
                    fileName = fileName.Replace(".xml", "");
                }

                var item = new ComboBoxItem
                {
                    Content = fileName,
                    Tag = xmlFile
                };
                Selfan.Items.Add(item);
                if (AppSettings.NBFCConfigXMLName == fileName)
                {
                    Selfan.SelectedItem = item;
                }
            }
        }
        catch
        {
            _isNbfcNotLoaded = true;
            if (!_isPageLoaded)
            {
                return;
            }

            await ShowNbfcDialogAsync();
        }
        finally
        {
            if (AppSettings.NBFCServiceType == 2 || ServiceCombo.SelectedIndex == 2)
            {
                Cooler_Fan1_Manual.Value = AppSettings.NBFCFan1UserFanSpeedRPM;
                Cooler_Fan2_Manual.Value = AppSettings.NBFCFan2UserFanSpeedRPM;

                NbfcFanSetSpeed();
                if (Cooler_Fan1_Manual.Value > 100)
                {
                    UpdatePageFanRpms();
                    AppSettings.NBFCFan1UserFanSpeedRPM = 110.0;
                }
                else
                {
                    CpuFanRpm.Text = Cooler_Fan1_Manual.Value.ToString(CultureInfo.InvariantCulture) + "%";
                    AppSettings.NBFCFlagConsoleCheckSpeedRunning = false;
                }

                AppSettings.SaveSettings();
            }

            if (AppSettings.NBFCServiceType == 1 || ServiceCombo.SelectedIndex == 1)
            {
                UpdatePageFanRpms();
            }
        }
    }

    // Метод для отображения диалога и загрузки NBFC
    private async Task ShowNbfcDialogAsync()
    {
        // Создаем элементы интерфейса, которые понадобятся в диалоге
        var downloadButton = new Button
        {
            Margin = new Thickness(0, 12, 0, 0),
            CornerRadius = new CornerRadius(15),
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new FontIcon { Glyph = "\uE74B" }, // Иконка загрузки
                    new TextBlock
                    {
                        Margin = new Thickness(10, 0, 0, 0), Text = "Cooler_DownloadNBFC_Title".GetLocalized(),
                        FontWeight = new FontWeight(700)
                    }
                }
            },
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var progressBar = new ProgressBar
        {
            IsIndeterminate = false,
            Opacity = 0.0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = "Cooler_DownloadNBFC_Desc".GetLocalized(),
                    Width = 300,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Left
                },
                downloadButton,
                progressBar
            }
        };

        var themerDialog = new ContentDialog
        {
            Title = "Warning".GetLocalized(),
            Content = stackPanel,
            CloseButtonText = "Cancel".GetLocalized(),
            PrimaryButtonText = "Next".GetLocalized(),
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false // Первоначально кнопка "Далее" неактивна
        };

        if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
        {
            themerDialog.XamlRoot = XamlRoot;
        }

        // Обработчик события нажатия на кнопку загрузки
        downloadButton.Click += async (_, _) =>
        {
            downloadButton.IsEnabled = false;
            progressBar.Opacity = 1.0;

            var client = new GitHubClient(new ProductHeaderValue("SakuOverclock"));
            var releases = await client.Repository.Release.GetAll("hirschmann", "nbfc");
            var latestRelease = releases[0];

            var downloadUrl = latestRelease.Assets.FirstOrDefault(a => a.Name.EndsWith(".exe"))?.BrowserDownloadUrl;
            if (downloadUrl != null)
            {
                var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 1;
                var downloadPath = Path.Combine(Path.GetTempPath(), "NBFC");

                var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write,
                    FileShare.None);
                var downloadStream = await response.Content.ReadAsStreamAsync();
                var buffer = new byte[8192];
                int bytesRead;
                long totalRead = 0;

                while ((bytesRead = await downloadStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalRead += bytesRead;
                    progressBar.Value = (double)totalRead / totalBytes * 100;
                }

                await Task.Delay(1000); // Задержка в 1 секунду
                // Убедиться, что файл полностью закрыт перед запуском
                if (File.Exists(downloadPath))
                {
                    label_8:
                    try
                    {
                        // Запуск загруженного установочного файла с правами администратора
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = downloadPath,
                            Verb = "runas" // Запуск от имени администратора
                        });
                    }
                    catch (Exception ex)
                    {
                        await App.MainWindow.ShowMessageDialogAsync(
                            "Cooler_DownloadNBFC_ErrorDesc".GetLocalized() + $": {ex.Message}", "Error".GetLocalized());
                        await Task.Delay(2000);
                        goto
                            label_8; // Повторить задачу открытия автообновления приложения, в случае если возникла ошибка доступа
                    }
                }

                downloadButton.Opacity = 0.0;
                progressBar.Opacity = 0.0;
                // Изменение текста диалога и активация кнопки "Далее"
                themerDialog.Content = new TextBlock
                {
                    Text = "Cooler_DownloadNBFC_AfterDesc".GetLocalized(),
                    TextAlignment = TextAlignment.Center
                };
                themerDialog.IsPrimaryButtonEnabled = true;
            }
        };
        var result = await themerDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            PageService.ReloadPage(typeof(КулерViewModel).FullName!); // Вызов метода перезагрузки страницы
        }
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            ServiceCombo.SelectedIndex = AppSettings.NBFCServiceType;
            
            CurveCombo.SelectedIndex =
                AppSettings is { NBFCFan1UserFanSpeedRPM: > 100, NBFCFan2UserFanSpeedRPM: > 100 } ? 0 :
                AppSettings is { NBFCFan1UserFanSpeedRPM: <= 100, NBFCFan2UserFanSpeedRPM: > 100 } ? 1 :
                AppSettings is { NBFCFan1UserFanSpeedRPM: > 100, NBFCFan2UserFanSpeedRPM: <= 100 } ? 2 :
                AppSettings is { NBFCFan1UserFanSpeedRPM: <= 100, NBFCFan2UserFanSpeedRPM: <= 100 } &&
                AppSettings.NBFCFan1UserFanSpeedRPM - AppSettings.NBFCFan2UserFanSpeedRPM > 0 ? 3 :
                AppSettings is { NBFCFan1UserFanSpeedRPM: <= 100, NBFCFan2UserFanSpeedRPM: <= 100 } &&
                AppSettings.NBFCFan1UserFanSpeedRPM - AppSettings.NBFCFan2UserFanSpeedRPM == 0 ? 4 : 0;
            
            Cooler_Fan1_Manual.Value = AppSettings.NBFCFan1UserFanSpeedRPM;
            Cooler_Fan2_Manual.Value = AppSettings.NBFCFan2UserFanSpeedRPM;
            
            for (var i = 0; i < 2; i++)
            {
                var doubleFan = i == 0 ? AppSettings.NBFCFan1UserFanSpeedRPM : AppSettings.NBFCFan2UserFanSpeedRPM;
                switch (doubleFan)
                {
                    case 90d:
                        SelectOnly(i == 0 ? Nbfc_TurboToggle : Nbfc_TurboToggle1);
                        break;
                    case 57d:
                        SelectOnly(i == 0 ? Nbfc_BalanceToggle : Nbfc_BalanceToggle1);
                        break;
                    case 37d:
                        SelectOnly(i == 0 ? Nbfc_QuietToggle : Nbfc_QuietToggle1);
                        break;
                    default:
                        SelectOnly(i == 0 ? Nbfc_AutoToggle : Nbfc_AutoToggle1);
                        (i == 0 ? Cooler_Fan1_SliderGrid : Cooler_Fan2_SliderGrid).Visibility = Visibility.Visible;
                        break;
                }
            }

            try
            {
                _isPageLoaded = true;
                CurveCombo_SelectionChanged(null, null);
                if (_isNbfcNotLoaded)
                {
                    await ShowNbfcDialogAsync();
                }
            }
            catch (Exception xException)
            {
                LogHelper.TraceIt_TraceError(xException.ToString());
            }
        }
        catch (Exception ex1)
        {
            LogHelper.TraceIt_TraceError(ex1.ToString());
        }
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e) => StopTempUpdate();

    #endregion

    #region Event Handlers 

    private void SelectOnly(ToggleButton name)
    {
        switch (name.Name)
        {
            case "Nbfc_QuietToggle":
                Nbfc_QuietToggle.IsChecked = true;
                Nbfc_BalanceToggle.IsChecked = false;
                Nbfc_TurboToggle.IsChecked = false;
                Nbfc_AutoToggle.IsChecked = false;
                break;
            case "Nbfc_BalanceToggle":
                Nbfc_QuietToggle.IsChecked = false;
                Nbfc_BalanceToggle.IsChecked = true;
                Nbfc_TurboToggle.IsChecked = false;
                Nbfc_AutoToggle.IsChecked = false;
                break;
            case "Nbfc_TurboToggle":
                Nbfc_QuietToggle.IsChecked = false;
                Nbfc_BalanceToggle.IsChecked = false;
                Nbfc_TurboToggle.IsChecked = true;
                Nbfc_AutoToggle.IsChecked = false;
                break;
            case "Nbfc_AutoToggle":
                Nbfc_QuietToggle.IsChecked = false;
                Nbfc_BalanceToggle.IsChecked = false;
                Nbfc_TurboToggle.IsChecked = false;
                Nbfc_AutoToggle.IsChecked = true;
                break;
            case "Nbfc_QuietToggle1":
                Nbfc_QuietToggle1.IsChecked = true;
                Nbfc_BalanceToggle1.IsChecked = false;
                Nbfc_TurboToggle1.IsChecked = false;
                Nbfc_AutoToggle1.IsChecked = false;
                break;
            case "Nbfc_BalanceToggle1":
                Nbfc_QuietToggle1.IsChecked = false;
                Nbfc_BalanceToggle1.IsChecked = true;
                Nbfc_TurboToggle1.IsChecked = false;
                Nbfc_AutoToggle1.IsChecked = false;
                break;
            case "Nbfc_TurboToggle1":
                Nbfc_QuietToggle1.IsChecked = false;
                Nbfc_BalanceToggle1.IsChecked = false;
                Nbfc_TurboToggle1.IsChecked = true;
                Nbfc_AutoToggle1.IsChecked = false;
                break;
            case "Nbfc_AutoToggle1":
                Nbfc_QuietToggle1.IsChecked = false;
                Nbfc_BalanceToggle1.IsChecked = false;
                Nbfc_TurboToggle1.IsChecked = false;
                Nbfc_AutoToggle1.IsChecked = true;
                break;
        }
    }


    private async void Selfan_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (!_isPageLoaded)
            {
                return;
            }

            await Task.Delay(200);
            AppSettings.NBFCConfigXMLName = (string)((ComboBoxItem)Selfan.SelectedItem).Content;
            AppSettings.SaveSettings();
            NbfcApplyFanConfig();
        }
        catch (Exception exception)
        {
            LogHelper.TraceIt_TraceError(exception.ToString());
        }
    }

    private async void Suggest_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await SuggestClickAsync();
        }
        catch (Exception exception)
        {
            LogHelper.TraceIt_TraceError(exception.ToString());
        }
    }

    #endregion

    #region NBFC Tasks

    private static void NbfcEnable()
    {
        var p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = "nbfc/nbfc.exe";
        p.StartInfo.Arguments = AppSettings.NBFCServiceType switch
        {
            0 => " stop",
            2 => " start --enabled",
            1 => " start --readonly",
            _ => p.StartInfo.Arguments
        };

        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;
        p.Start();
    }

    private static void NbfcFanSetSpeed(int fanNumber = 0)
    {
        try
        {
            var p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.FileName = "nbfc/nbfc.exe";

            if (AppSettings.NBFCServiceType == 2)
            {
                var speedValue = fanNumber == 0 ? AppSettings.NBFCFan1UserFanSpeedRPM : AppSettings.NBFCFan2UserFanSpeedRPM;

                p.StartInfo.Arguments = speedValue < 100 ? $" set --fan {fanNumber} --speed {speedValue}" : $" set --fan {fanNumber} --auto";
            }

            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.Start();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex.ToString());
        }
    }


    private static void NbfcApplyFanConfig()
    {
        const string quote = "\"";
        var p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = "nbfc/nbfc.exe";
        p.StartInfo.Arguments = " config --apply " + quote + AppSettings.NBFCConfigXMLName + quote;
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;
        p.Start();
    }

    private void GetFanSpeedsThroughNbfc(bool start)
    {
        if (start)
        {
            if (_fanUpdateTimer != null && !_fanUpdateTimer.IsEnabled)
            {
                _fanUpdateTimer.Start();
            }
        }
        else
        {
            if (_fanUpdateTimer != null && _fanUpdateTimer.IsEnabled)
            {
                _fanUpdateTimer.Stop();
            }
        }
    }

    private async Task CheckFan()
    {
        if (ServiceCombo.SelectedIndex is not (1 or 2))
        {
            return;
        }

        // Параллельно запрашиваем скорость обоих вентиляторов
        var fan1Task = GetFanSpeedAsync(0);
        var fan2Task = GetFanSpeedAsync(1);

        AppSettings.NBFCAnswerSpeedFan1 = await fan1Task;
        AppSettings.NBFCAnswerSpeedFan2 = await fan2Task;
        AppSettings.SaveSettings();

        UpdatePageFanRpms();
        return;

        static async Task<string> GetFanSpeedAsync(int fanNumber)
        {
            try
            {
                using var p = new Process();
                p.StartInfo = new ProcessStartInfo
                {
                    FileName = "nbfc/nbfc.exe",
                    Arguments = $"status --fan {fanNumber}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true
                };
                p.Start();

                while (await p.StandardOutput.ReadLineAsync() is { } line)
                {
                    if (line.Contains("Current fan speed", StringComparison.OrdinalIgnoreCase))
                    {
                        return line.Replace("Current fan speed", "")
                            .Replace(" ", "")
                            .Replace(":", "")
                            .Replace("\t", "")
                            .Trim();
                    }
                }

                await p.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                LogHelper.TraceIt_TraceError($"[Fan Control NBFC] Error (fan {fanNumber}): {ex.Message}");
            }

            return string.Empty;
        }
    } 

    private void UpdatePageFanRpms()
    {
        CpuFanRpm.Text = AppSettings.NBFCAnswerSpeedFan1 == string.Empty ? "N/A" : SpeedHelper(AppSettings.NBFCAnswerSpeedFan1) + "%";
        GpuFanRpm.Text = AppSettings.NBFCAnswerSpeedFan2 == string.Empty ? "N/A" : SpeedHelper(AppSettings.NBFCAnswerSpeedFan2) + "%";
    }

    private async Task SuggestClickAsync()
    {
        SuggestTip.Subtitle = "";
        var p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = "nbfc/nbfc.exe";
        p.StartInfo.Arguments = " config -r";
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;

        try
        {
            p.Start();
            var output = await p.StandardOutput.ReadToEndAsync();
            if (!string.IsNullOrEmpty(output))
            {
                SuggestTip.Subtitle = output;
            }
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex.ToString());
        }
        finally
        {
            await p.WaitForExitAsync();
            SuggestTip.IsOpen = true;
        }
    }


    private void StartTempUpdate()
    {
        _tempUpdateTimer = new DispatcherTimer();
        _tempUpdateTimer.Tick += (_, _) => UpdateTemperatureAsync();
        _tempUpdateTimer.Interval = TimeSpan.FromMilliseconds(500);
        App.MainWindow.Activated += Window_Activated; //Проверка фокуса на программе для экономии ресурсов
        App.MainWindow.VisibilityChanged +=
            Window_VisibilityChanged; //Проверка программу на трей меню для экономии ресурсов
        _tempUpdateTimer.Start();
    }

    private void UpdateTemperatureAsync()
    {
        CpuFanTemp.Text = "CPU Temp " + Math.Round(_cpuTemp, 1) + "C";
        GpuFanTemp.Text = "GPU Temp " + (_gpuTemp == 0d ? "N/A" : Math.Round(_gpuTemp, 1) + "C");
        TdpLimitSensor_Text.Text = Math.Round(_cpuTdpLim, 1) + "W";
        TdpValueSensor_Text.Text = Math.Round(_cpuTdpVal, 1).ToString(CultureInfo.InvariantCulture);
        CpuFreqSensor_Text.Text = Math.Round(_cpuFreq, 1).ToString(CultureInfo.InvariantCulture);
        CpuCurrentSensor_Text.Text = Math.Round(_cpuCurrent, 1).ToString(CultureInfo.InvariantCulture);
    }

    private void OnDataUpdated(object? sender, SensorsInformation info)
    {
        _cpuTemp = info.CpuTempValue;
        _gpuTemp = info.ApuTemperature;
        _cpuTdpLim = info.CpuFastLimit;
        _cpuTdpVal = info.CpuFastValue;
        _cpuFreq = info.CpuFrequency;
        _cpuCurrent = info.VrmEdcValue;
    }

    private void StopTempUpdate()
    {
        _dataUpdater.DataUpdated -= OnDataUpdated;
        _tempUpdateTimer?.Stop();
    }

    #endregion

    private static string SpeedHelper(string invar)
    {
        if (string.IsNullOrEmpty(invar))
        {
            return invar;
        }

        ReadOnlySpan<char> span = invar;

        var commaIndex = span.IndexOf(',');
        var dotIndex = span.IndexOf('.');

        var sepIndex = (commaIndex >= 0) ? commaIndex :
                       (dotIndex >= 0) ? dotIndex : -1;

        if (sepIndex >= 0 && sepIndex < span.Length - 1)
        {
            var length = Math.Min(span.Length, sepIndex + 2); // максимум 1 цифра после разделителя
            return span[..length].ToString();
        }

        return invar;
    }



    private void ServiceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isPageLoaded) { return; }
        AppSettings.NBFCServiceType = ServiceCombo.SelectedIndex;
        AppSettings.SaveSettings();
        NbfcEnable();
        if (ServiceCombo.SelectedIndex == 1)
        {
            UpdatePageFanRpms();
        }
    }

    private void Nbfc_Fan1Control_Click(object sender, RoutedEventArgs e)
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
            switch (toggleButton.Name)
            {
                case "Nbfc_AutoToggle":
                    Nbfc_TurboToggle.IsChecked = false;
                    Nbfc_BalanceToggle.IsChecked = false;
                    Nbfc_QuietToggle.IsChecked = false;
                    Cooler_Fan1_SliderGrid.Visibility = Visibility.Visible;

                    if (CurveCombo.SelectedIndex == 4) 
                    {
                        AppSettings.NBFCFan2UserFanSpeedRPM = Cooler_Fan1_Manual.Value;
                        Cooler_Fan2_Manual.Value = Cooler_Fan1_Manual.Value;
                    }
                    AppSettings.NBFCFan1UserFanSpeedRPM = Cooler_Fan1_Manual.Value;
                    AppSettings.SaveSettings();
                    NbfcFanSetSpeed();
                    break;
                case "Nbfc_TurboToggle":
                    Nbfc_AutoToggle.IsChecked = false;
                    Nbfc_BalanceToggle.IsChecked = false;
                    Nbfc_QuietToggle.IsChecked = false;
                    Cooler_Fan1_SliderGrid.Visibility = Visibility.Collapsed;

                    if (CurveCombo.SelectedIndex == 4) 
                    {
                        AppSettings.NBFCFan2UserFanSpeedRPM = 90d;
                        Cooler_Fan2_Manual.Value = 90d;
                    }
                    AppSettings.NBFCFan1UserFanSpeedRPM = 90d;
                    Cooler_Fan1_Manual.Value = 90d;
                    AppSettings.SaveSettings();
                    NbfcFanSetSpeed();
                    break;
                case "Nbfc_BalanceToggle":
                    Nbfc_AutoToggle.IsChecked = false;
                    Nbfc_TurboToggle.IsChecked = false;
                    Nbfc_QuietToggle.IsChecked = false;
                    Cooler_Fan1_SliderGrid.Visibility = Visibility.Collapsed;

                    if (CurveCombo.SelectedIndex == 4) 
                    {
                        Cooler_Fan2_Manual.Value = 57d;
                        AppSettings.NBFCFan2UserFanSpeedRPM = 57d;
                    }
                    Cooler_Fan1_Manual.Value = 57d;
                    AppSettings.NBFCFan1UserFanSpeedRPM = 57d;
                    AppSettings.SaveSettings();
                    NbfcFanSetSpeed();
                    break;
                case "Nbfc_QuietToggle":
                    Nbfc_AutoToggle.IsChecked = false;
                    Nbfc_TurboToggle.IsChecked = false;
                    Nbfc_BalanceToggle.IsChecked = false;
                    Cooler_Fan1_SliderGrid.Visibility = Visibility.Collapsed;

                    if (CurveCombo.SelectedIndex == 4) 
                    {
                        Cooler_Fan2_Manual.Value = 37d;
                        AppSettings.NBFCFan2UserFanSpeedRPM = 37d;
                    }
                    Cooler_Fan1_Manual.Value = 37d;
                    AppSettings.NBFCFan1UserFanSpeedRPM = 37d;
                    AppSettings.SaveSettings();
                    NbfcFanSetSpeed();
                    break;

                case "Nbfc_AutoToggle1":
                    Nbfc_TurboToggle1.IsChecked = false;
                    Nbfc_BalanceToggle1.IsChecked = false;
                    Nbfc_QuietToggle1.IsChecked = false;
                    Cooler_Fan2_SliderGrid.Visibility = Visibility.Visible;

                    AppSettings.NBFCFan2UserFanSpeedRPM = Cooler_Fan2_Manual.Value;
                    AppSettings.SaveSettings();
                    NbfcFanSetSpeed(1);
                    break;
                case "Nbfc_TurboToggle1":
                    Nbfc_AutoToggle1.IsChecked = false;
                    Nbfc_BalanceToggle1.IsChecked = false;
                    Nbfc_QuietToggle1.IsChecked = false;
                    Cooler_Fan2_SliderGrid.Visibility = Visibility.Collapsed;

                    Cooler_Fan2_Manual.Value = 90d;
                    AppSettings.NBFCFan2UserFanSpeedRPM = 90d;
                    AppSettings.SaveSettings();
                    NbfcFanSetSpeed(1);
                    break;
                case "Nbfc_BalanceToggle1":
                    Nbfc_AutoToggle1.IsChecked = false;
                    Nbfc_TurboToggle1.IsChecked = false;
                    Nbfc_QuietToggle1.IsChecked = false;
                    Cooler_Fan2_SliderGrid.Visibility = Visibility.Collapsed;

                    Cooler_Fan2_Manual.Value = 57d;
                    AppSettings.NBFCFan2UserFanSpeedRPM = 57d;
                    AppSettings.SaveSettings();
                    NbfcFanSetSpeed(1);
                    break;
                case "Nbfc_QuietToggle1":
                    Nbfc_BalanceToggle1.IsChecked = false;
                    Nbfc_TurboToggle1.IsChecked = false;
                    Nbfc_AutoToggle1.IsChecked = false;
                    Cooler_Fan2_SliderGrid.Visibility = Visibility.Collapsed;

                    Cooler_Fan2_Manual.Value = 37d;
                    AppSettings.NBFCFan2UserFanSpeedRPM = 37d;
                    AppSettings.SaveSettings();
                    NbfcFanSetSpeed(1);
                    break;
            }

            AppSettings.SaveSettings();
        }

        if ((Nbfc_QuietToggle.IsChecked == false && Nbfc_BalanceToggle.IsChecked == false &&
            Nbfc_TurboToggle.IsChecked == false && Nbfc_AutoToggle.IsChecked == false) ||
            (Nbfc_QuietToggle1.IsChecked == false && Nbfc_BalanceToggle1.IsChecked == false &&
            Nbfc_TurboToggle1.IsChecked == false && Nbfc_AutoToggle1.IsChecked == false))
        {
            toggleButton.IsChecked = true;
        }
    } 

    private void CurveCombo_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
    {
        if (!_isPageLoaded) { return; }
        Cooler_Curve_Fan_Text.Text = Cooler_Curve_Fan_Text.Text.Replace("Cooler_Curve_Fan1_Part".GetLocalized(), string.Empty);
        switch (CurveCombo.SelectedIndex)
        {
            case 0: // Оба авто
                Cooler_Curve_Fan1.Visibility = Visibility.Collapsed;
                Cooler_Curve_Fan2.Visibility = Visibility.Collapsed;
                AppSettings.NBFCFan1UserFanSpeedRPM = 110;
                AppSettings.NBFCFan2UserFanSpeedRPM = 110;
                break;
            case 1: // Фиксированный только первый 
                
                Cooler_Curve_Fan_Text.Text += "Cooler_Curve_Fan1_Part".GetLocalized();
                Cooler_Curve_Fan1.Visibility = Visibility.Visible;
                Cooler_Curve_Fan2.Visibility = Visibility.Collapsed;
                AppSettings.NBFCFan1UserFanSpeedRPM = AppSettings.NBFCFan1UserFanSpeedRPM > 100 ? 70 : AppSettings.NBFCFan1UserFanSpeedRPM;
                AppSettings.NBFCFan2UserFanSpeedRPM = 110;
                break;
            case 2: // Фиксированный только второй 
                Cooler_Curve_Fan1.Visibility = Visibility.Collapsed;
                Cooler_Curve_Fan2.Visibility = Visibility.Visible;
                AppSettings.NBFCFan1UserFanSpeedRPM = 110;
                AppSettings.NBFCFan2UserFanSpeedRPM = AppSettings.NBFCFan2UserFanSpeedRPM > 100 ? 70 : AppSettings.NBFCFan2UserFanSpeedRPM;
                break;
            case 3: // Оба фиксированные но различные
                Cooler_Curve_Fan_Text.Text += "Cooler_Curve_Fan1_Part".GetLocalized();
                Cooler_Curve_Fan1.Visibility = Visibility.Visible;
                Cooler_Curve_Fan2.Visibility = Visibility.Visible; // Потому что оба будут управляться с одного месте, с первого блока настроек
                AppSettings.NBFCFan1UserFanSpeedRPM = AppSettings.NBFCFan1UserFanSpeedRPM > 100 ? 70 : AppSettings.NBFCFan1UserFanSpeedRPM;
                AppSettings.NBFCFan2UserFanSpeedRPM = AppSettings.NBFCFan2UserFanSpeedRPM > 100 ? 50 : AppSettings.NBFCFan2UserFanSpeedRPM;
                break;
            case 4: // Оба фиксированные
                Cooler_Curve_Fan1.Visibility = Visibility.Visible;
                Cooler_Curve_Fan2.Visibility = Visibility.Collapsed; // Потому что оба будут управляться с одного месте, с первого блока настроек
                AppSettings.NBFCFan1UserFanSpeedRPM = AppSettings.NBFCFan1UserFanSpeedRPM > 100 ? 70 : AppSettings.NBFCFan1UserFanSpeedRPM;
                AppSettings.NBFCFan2UserFanSpeedRPM = AppSettings.NBFCFan2UserFanSpeedRPM > 100 ? 70 : AppSettings.NBFCFan1UserFanSpeedRPM;
                break;
        }
        AppSettings.SaveSettings();
        NbfcFanSetSpeed();
        NbfcFanSetSpeed(1);
    }

    private void ModeOptions_Button_Click(object sender, RoutedEventArgs e)
    {
        AppSettings.IsNBFCModeEnabled = !AppSettings.IsNBFCModeEnabled;
        AppSettings.SaveSettings();
        
        AsusOptions_Button.IsChecked = AppSettings.IsNBFCModeEnabled;
        NbfcOptions_Button.IsChecked = !AppSettings.IsNBFCModeEnabled;
        ProfileSettings_StackPanel.Visibility = AppSettings.IsNBFCModeEnabled ? Visibility.Visible : Visibility.Collapsed;
        ProfileSettings_BeginnerView.Visibility = AppSettings.IsNBFCModeEnabled ? Visibility.Collapsed : Visibility.Visible;


        ProfileSettings_ChangeViewStackPanel.Visibility = Visibility.Visible;
    }

    private async void Cooler_Fan_Manual_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        try
        {
            var fanNumber = 0;
            var fanValue = Cooler_Fan1_Manual.Value;
            var fanRpmTextBlock = CpuFanRpm;
            
            if ((sender as Slider)!.Name == "Cooler_Fan2_Manual")
            {
                fanNumber = 1;
                fanValue = Cooler_Fan2_Manual.Value;
                fanRpmTextBlock = GpuFanRpm;
            } 
            
            if (ServiceCombo.SelectedIndex == 2)
            {
                NbfcFanSetSpeed(fanNumber);
                
                await Task.Delay(200);

                var fanInvar = fanValue.ToString(CultureInfo.InvariantCulture);

                if (fanNumber == 0)
                {
                    AppSettings.NBFCFan1UserFanSpeedRPM = fanValue;

                    if (CurveCombo.SelectedIndex == 4)
                    {
                        AppSettings.NBFCFan2UserFanSpeedRPM = fanValue;
                        Cooler_Fan2_Manual.Value = fanValue;
                    }
                }
                else
                {
                    AppSettings.NBFCFan2UserFanSpeedRPM = fanValue;
                }

                fanRpmTextBlock.Text = fanInvar + "%";
                AppSettings.NBFCFlagConsoleCheckSpeedRunning = false;

                AppSettings.SaveSettings();
            }

            if (ServiceCombo.SelectedIndex == 1)
            {
                UpdatePageFanRpms();
            }
        }
        catch (Exception exception)
        {
            LogHelper.TraceIt_TraceError(exception.ToString());
        }
    }
}