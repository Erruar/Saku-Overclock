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
        Update();
        AppSettings.FlagRyzenADJConsoleTemperatureCheckRunning =
            true; //Автообновление информации о кулере включено! Это нужно для того, чтобы обновление информации не происходило нигде кроме страницы с оптимизацией кулера, так как контроллировать асинхронные методы бывает сложно
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
            _fanUpdateTimer?.Start();
        }
        else
        {
            _tempUpdateTimer?.Stop();
            _fanUpdateTimer?.Stop();
        }
    }

    private void Window_VisibilityChanged(object sender, WindowVisibilityChangedEventArgs args)
    {
        if (args.Visible)
        {
            _tempUpdateTimer?.Start();
            _fanUpdateTimer?.Start();
        }
        else
        {
            _tempUpdateTimer?.Stop();
            _fanUpdateTimer?.Stop();
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        StartTempUpdate();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        StopTempUpdate();
    }

    private void AdvancedCooler_Click(object sender, RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(AdvancedКулерViewModel).FullName!);
    }

    private void AsusCoolerMode_Click(object sender, RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(AsusКулерViewModel).FullName!);
    }

    #endregion

    #region Initialization

    private async void FanInit()
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
        try
        {
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
        try
        {
            if (AppSettings.NBFCServiceType == 2 || ServiceCombo.SelectedIndex == 2)
            {
                Cooler_Fan1_Manual.Value = AppSettings.NBFCFan1UserFanSpeedRPM;
                Cooler_Fan2_Manual.Value = AppSettings.NBFCFan2UserFanSpeedRPM;



                NbfcFan1SetSpeed();
                if (Cooler_Fan1_Manual.Value > 100)
                {
                    Update();
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
                Update();
            }
        }
        catch (Exception e)
        {
            SendSmuCommand.TraceIt_TraceError(e.ToString());
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
        ServiceCombo.SelectedIndex = AppSettings.NBFCServiceType;
        CurveCombo.SelectedIndex = AppSettings.NBFCFan1UserFanSpeedRPM > 100 && AppSettings.NBFCFan2UserFanSpeedRPM > 100 ? 0 :
            (AppSettings.NBFCFan1UserFanSpeedRPM <= 100 && AppSettings.NBFCFan2UserFanSpeedRPM > 100 ? 1 :
            (AppSettings.NBFCFan1UserFanSpeedRPM > 100 && AppSettings.NBFCFan2UserFanSpeedRPM <= 100 ? 2 :
            (AppSettings.NBFCFan1UserFanSpeedRPM <= 100 && AppSettings.NBFCFan2UserFanSpeedRPM <= 100 && AppSettings.NBFCFan1UserFanSpeedRPM != AppSettings.NBFCFan2UserFanSpeedRPM ? 3 :
            (AppSettings.NBFCFan1UserFanSpeedRPM <= 100 && AppSettings.NBFCFan2UserFanSpeedRPM <= 100 && AppSettings.NBFCFan1UserFanSpeedRPM == AppSettings.NBFCFan2UserFanSpeedRPM ? 4 : 0
            ))));
        Cooler_Fan1_Manual.Value = AppSettings.NBFCFan1UserFanSpeedRPM;
        Cooler_Fan2_Manual.Value = AppSettings.NBFCFan2UserFanSpeedRPM;
        for (var i = 0; i < 2; i++)
        {
            double doubleFan;
            if (i == 0)
            {
                doubleFan = AppSettings.NBFCFan1UserFanSpeedRPM;
            }
            else
            {
                doubleFan = AppSettings.NBFCFan2UserFanSpeedRPM;
            }
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
            SendSmuCommand.TraceIt_TraceError(xException.ToString());
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
            NbfcFanState();
        }
        catch (Exception exception)
        {
            SendSmuCommand.TraceIt_TraceError(exception.ToString());
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
            SendSmuCommand.TraceIt_TraceError(exception.ToString());
        }
    }

    #endregion

    #region NBFC Tasks

    private void NbfcEnable()
    {
        var p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = "nbfc/nbfc.exe";
        if (AppSettings.NBFCServiceType == 0)
        {
            p.StartInfo.Arguments = " stop";
        }

        if (AppSettings.NBFCServiceType == 2)
        {
            p.StartInfo.Arguments = " start --enabled";
        }

        if (AppSettings.NBFCServiceType == 1)
        {
            p.StartInfo.Arguments = " start --readonly";
        }

        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;
        p.Start();
    }

    private void NbfcFan1SetSpeed()
    {
        var p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = @"nbfc/nbfc.exe";
        if (AppSettings.NBFCServiceType == 2)
        {
            if (Cooler_Fan1_Manual.Value < 100)
            {
                p.StartInfo.Arguments = " set --fan 0 --speed " + Cooler_Fan1_Manual.Value;
            }
            else
            {
                p.StartInfo.Arguments = " set --fan 0 --auto";
            }
        }

        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;
        p.Start();
    }

    private void NbfcFan2SetSpeed()
    {
        var p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = @"nbfc/nbfc.exe";
        if (AppSettings.NBFCServiceType == 2)
        {
            if (Cooler_Fan2_Manual.Value < 100)
            {
                p.StartInfo.Arguments = " set --fan 1 --speed " + Cooler_Fan2_Manual.Value;
            }
            else
            {
                p.StartInfo.Arguments = " set --fan 1 --auto";
            }
        }

        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;
        p.Start();
    }

    private static void NbfcFanState()
    {
        const string quote = "\"";
        var p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = @"nbfc/nbfc.exe";
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
            _fanUpdateTimer?.Start();
        }
        else
        {
            _fanUpdateTimer?.Stop();
        }
    }

    private async Task CheckFan()
    {
        if (ServiceCombo.SelectedIndex == 2 || ServiceCombo.SelectedIndex == 1)
        {
            if (AppSettings.NBFCFlagConsoleCheckSpeedRunning)
            {
                AppSettings.NBFCAnswerSpeedFan1 = "";
                AppSettings.NBFCAnswerSpeedFan2 = "";
                var p = new Process();
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.FileName = @"nbfc/nbfc.exe";
                p.StartInfo.Arguments = " status --fan 0";
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                try
                {
                    p.Start();
                }
                catch (Exception ex)
                {
                    SendSmuCommand.TraceIt_TraceError("[Fan Control NBFC] Error " + ex.Message);
                }

                var outputWriter = p.StandardOutput;
                var line = await outputWriter.ReadLineAsync();
                while (line != null)
                {
                    if (line.Contains("Current fan speed"))
                    {
                        AppSettings.NBFCAnswerSpeedFan1 = line.Replace("Current fan speed", "").Replace(" ", "")
                            .Replace(":", "").Replace("\t", "");
                        AppSettings.SaveSettings();
                    }

                    line = await outputWriter.ReadLineAsync();
                }

                await p.WaitForExitAsync();
                var p1 = new Process(); //fan 2
                p1.StartInfo.UseShellExecute = false;
                p1.StartInfo.FileName = @"nbfc/nbfc.exe";
                p1.StartInfo.Arguments = " status --fan 1";
                p1.StartInfo.CreateNoWindow = true;
                p1.StartInfo.RedirectStandardError = true;
                p1.StartInfo.RedirectStandardInput = true;
                p1.StartInfo.RedirectStandardOutput = true;
                p1.Start();
                var outputWriter1 = p1.StandardOutput;
                var line1 = await outputWriter1.ReadLineAsync();
                while (line1 != null)
                {
                    if (line1.Contains("Current fan speed"))
                    {
                        AppSettings.NBFCAnswerSpeedFan2 = line1.Replace("Current fan speed", "").Replace(" ", "")
                            .Replace(":", "").Replace("\t", "");
                        AppSettings.SaveSettings();
                    }

                    line1 = await outputWriter1.ReadLineAsync();
                }

                await p1.WaitForExitAsync();
                Update();
            }
        }
    }

    private void Update()
    {
        if (AppSettings.NBFCAnswerSpeedFan1 == string.Empty)
        {
            return;
        }

        CpuFanRpm.Text = AppSettings.NBFCAnswerSpeedFan1 + "%";


        if (AppSettings.NBFCAnswerSpeedFan2 == string.Empty)
        {
            return;
        }

        GpuFanRpm.Text = AppSettings.NBFCAnswerSpeedFan2 + "%";
    }

    private async Task SuggestClickAsync()
    {
        SuggestTip.Subtitle = "";
        var p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = @"nbfc/nbfc.exe";
        p.StartInfo.Arguments = " config -r";
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;
        try
        {
            p.Start();
        }
        catch (Exception ex)
        {
            SendSmuCommand.TraceIt_TraceError(ex.ToString());
        }

        var outputWriter = p.StandardOutput;
        var line = await outputWriter.ReadLineAsync();
        while (line != null)
        {
            if (line != "")
            {
                SuggestTip.Subtitle += line + "\n";
            }

            line = await outputWriter.ReadLineAsync();
        }

        await p.WaitForExitAsync();
        SuggestTip.IsOpen = true;
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
        GpuFanTemp.Text = "GPU Temp " + Math.Round(_gpuTemp, 1) + "C";
        TdpLimitSensor_Text.Text = Math.Round(_cpuTdpLim, 1) + "W";
        TdpValueSensor_Text.Text = Math.Round(_cpuTdpVal, 1).ToString();
        CpuFreqSensor_Text.Text = Math.Round(_cpuFreq, 1).ToString();
        CpuCurrentSensor_Text.Text = Math.Round(_cpuCurrent, 1).ToString();
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

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {

    }

    private void ServiceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isPageLoaded) { return; }
        AppSettings.NBFCServiceType = ServiceCombo.SelectedIndex;
        AppSettings.SaveSettings();
        NbfcEnable();
        if (ServiceCombo.SelectedIndex == 1)
        {
            Update();
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
                    NbfcFan1SetSpeed();
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
                    NbfcFan1SetSpeed(); 
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
                    NbfcFan1SetSpeed();
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
                    NbfcFan1SetSpeed();
                    break;

                case "Nbfc_AutoToggle1":
                    Nbfc_TurboToggle1.IsChecked = false;
                    Nbfc_BalanceToggle1.IsChecked = false;
                    Nbfc_QuietToggle1.IsChecked = false;
                    Cooler_Fan2_SliderGrid.Visibility = Visibility.Visible;

                    AppSettings.NBFCFan2UserFanSpeedRPM = Cooler_Fan2_Manual.Value;
                    AppSettings.SaveSettings();
                    NbfcFan2SetSpeed();
                    break;
                case "Nbfc_TurboToggle1":
                    Nbfc_AutoToggle1.IsChecked = false;
                    Nbfc_BalanceToggle1.IsChecked = false;
                    Nbfc_QuietToggle1.IsChecked = false;
                    Cooler_Fan2_SliderGrid.Visibility = Visibility.Collapsed;

                    Cooler_Fan2_Manual.Value = 90d;
                    AppSettings.NBFCFan2UserFanSpeedRPM = 90d;
                    AppSettings.SaveSettings();
                    NbfcFan2SetSpeed();
                    break;
                case "Nbfc_BalanceToggle1":
                    Nbfc_AutoToggle1.IsChecked = false;
                    Nbfc_TurboToggle1.IsChecked = false;
                    Nbfc_QuietToggle1.IsChecked = false;
                    Cooler_Fan2_SliderGrid.Visibility = Visibility.Collapsed;

                    Cooler_Fan2_Manual.Value = 57d;
                    AppSettings.NBFCFan2UserFanSpeedRPM = 57d;
                    AppSettings.SaveSettings();
                    NbfcFan2SetSpeed();
                    break;
                case "Nbfc_QuietToggle1":
                    Nbfc_BalanceToggle1.IsChecked = false;
                    Nbfc_TurboToggle1.IsChecked = false;
                    Nbfc_AutoToggle1.IsChecked = false;
                    Cooler_Fan2_SliderGrid.Visibility = Visibility.Collapsed;

                    Cooler_Fan2_Manual.Value = 37d;
                    AppSettings.NBFCFan2UserFanSpeedRPM = 37d;
                    AppSettings.SaveSettings();
                    NbfcFan2SetSpeed();
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
        switch (CurveCombo.SelectedIndex)
        {
            case 0: // Оба авто
                Cooler_Curve_Fan1.Visibility = Visibility.Collapsed;
                Cooler_Curve_Fan2.Visibility = Visibility.Collapsed;
                AppSettings.NBFCFan1UserFanSpeedRPM = 110;
                AppSettings.NBFCFan2UserFanSpeedRPM = 110;
                break;
            case 1: // Фиксированный только первый 
                Cooler_Curve_Fan_Text.Text = Cooler_Curve_Fan_Text.Text.Replace("Cooler_Curve_Fan1_Part".GetLocalized(), string.Empty);
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
                Cooler_Curve_Fan_Text.Text = Cooler_Curve_Fan_Text.Text.Replace("Cooler_Curve_Fan1_Part".GetLocalized(), string.Empty);
                Cooler_Curve_Fan_Text.Text += "Cooler_Curve_Fan1_Part".GetLocalized();
                Cooler_Curve_Fan1.Visibility = Visibility.Visible;
                Cooler_Curve_Fan2.Visibility = Visibility.Visible; // Потому что оба будут управляться с одного месте, с первого блока настроек
                AppSettings.NBFCFan1UserFanSpeedRPM = AppSettings.NBFCFan1UserFanSpeedRPM > 100 ? 70 : AppSettings.NBFCFan1UserFanSpeedRPM;
                AppSettings.NBFCFan2UserFanSpeedRPM = AppSettings.NBFCFan2UserFanSpeedRPM > 100 ? 50 : AppSettings.NBFCFan2UserFanSpeedRPM;
                break;
            case 4: // Оба фиксированные
                Cooler_Curve_Fan_Text.Text = Cooler_Curve_Fan_Text.Text.Replace("Cooler_Curve_Fan1_Part".GetLocalized(), string.Empty);
                Cooler_Curve_Fan1.Visibility = Visibility.Visible;
                Cooler_Curve_Fan2.Visibility = Visibility.Collapsed; // Потому что оба будут управляться с одного месте, с первого блока настроек
                AppSettings.NBFCFan1UserFanSpeedRPM = AppSettings.NBFCFan1UserFanSpeedRPM > 100 ? 70 : AppSettings.NBFCFan1UserFanSpeedRPM;
                AppSettings.NBFCFan2UserFanSpeedRPM = AppSettings.NBFCFan2UserFanSpeedRPM > 100 ? 70 : AppSettings.NBFCFan1UserFanSpeedRPM;
                break;
        }
        AppSettings.SaveSettings();
    }

    private void ModeOptions_Button_Click(object sender, RoutedEventArgs e)
    {
        AppSettings.IsNBFCModeEnabled = !AppSettings.IsNBFCModeEnabled;
        AppSettings.SaveSettings();
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
    }

    private async void Cooler_Fan1_Manual_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        try
        {
            if (ServiceCombo.SelectedIndex == 2)
            {
                NbfcFan1SetSpeed();
                await Task.Delay(200);
                var fan1Invar = Cooler_Fan1_Manual.Value.ToString(CultureInfo.InvariantCulture);
                AppSettings.NBFCFan1UserFanSpeedRPM = Cooler_Fan1_Manual.Value;
                if (CurveCombo.SelectedIndex == 4)
                {
                    AppSettings.NBFCFan2UserFanSpeedRPM = Cooler_Fan1_Manual.Value;
                    Cooler_Fan2_Manual.Value = Cooler_Fan1_Manual.Value;
                }

                CpuFanRpm.Text = fan1Invar + "%";
                AppSettings.NBFCFlagConsoleCheckSpeedRunning = false;

                AppSettings.SaveSettings();
            }

            if (ServiceCombo.SelectedIndex == 1)
            {
                Update();
            }
        }
        catch (Exception exception)
        {
            SendSmuCommand.TraceIt_TraceError(exception.ToString());
        }
    }

    private async void Cooler_Fan2_Manual_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        try
        {
            if (ServiceCombo.SelectedIndex == 2)
            {
                NbfcFan2SetSpeed();
                await Task.Delay(200);
                var fan2Invar = Cooler_Fan2_Manual.Value.ToString(CultureInfo.InvariantCulture);
                AppSettings.NBFCFan2UserFanSpeedRPM = Cooler_Fan2_Manual.Value;

                GpuFanRpm.Text = fan2Invar + "%";
                AppSettings.NBFCFlagConsoleCheckSpeedRunning = false;

                AppSettings.SaveSettings();
            }

            if (ServiceCombo.SelectedIndex == 1)
            {
                Update();
            }
        }
        catch (Exception exception)
        {
            SendSmuCommand.TraceIt_TraceError(exception.ToString());
        }
    }

}