using System.Diagnostics;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using Octokit;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.SmuEngine;
using Saku_Overclock.ViewModels;
using Saku_Overclock.Wrappers;
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
    private DispatcherTimer? _rpmUpdateTimer;
    private static int _fanCount = -1;
    private static bool _unavailableFlag;
    private static int _setFanIndex = -1;
    private readonly DispatcherTimer? _fanUpdateTimer;
    private readonly IAppSettingsService _appSettings = App.GetService<IAppSettingsService>();
    private readonly IBackgroundDataUpdater _dataUpdater = App.GetService<IBackgroundDataUpdater>();
    private bool _selectedModeAsus;


    public КулерPage()
    {
        InitializeComponent();

        FanInit();
        UpdatePageFanRpms();

        _appSettings.SaveSettings();

        Loaded += Page_Loaded;
        Unloaded += Page_Unloaded;

        _fanUpdateTimer = new DispatcherTimer();
        _fanUpdateTimer.Tick += async (_, _) => await CheckFan();
        _fanUpdateTimer.Interval = TimeSpan.FromMilliseconds(6000);
        _dataUpdater.DataUpdated += OnDataUpdated;
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
            if (_appSettings.IsNbfcModeEnabled)
            {
                AsusOptionsButton.IsChecked = true;
                _selectedModeAsus = true;
                NbfcOptionsButton.IsChecked = false;
            }
            else
            {
                _selectedModeAsus = false;
                AsusOptionsButton.IsChecked = false;
                NbfcOptionsButton.IsChecked = true;
            }
            const string
                folderPath =
                    @"C:\Program Files (x86)\NoteBook FanControl\Configs"; // Получить папку, в которой хранятся файлы XML с конфигами
            var xmlFiles = Directory.GetFiles(folderPath, "*.xml");
            ConfigSelectorComboBox.Items.Clear();
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
                ConfigSelectorComboBox.Items.Add(item);
                if (_appSettings.NbfcConfigXmlName == fileName)
                {
                    ConfigSelectorComboBox.SelectedItem = item;
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

            if (_selectedModeAsus)
            {
                if (_appSettings.AsusCoolerServiceType == 2 || ServiceCombo.SelectedIndex == 2)
                {
                    CoolerFan1Manual.Value = _appSettings.AsusModeFan1UserFanSpeedRpm;
                    CoolerFan2Manual.Value = _appSettings.AsusModeFan2UserFanSpeedRpm;

                    NbfcFanSetSpeed();
                    if (CoolerFan1Manual.Value > 100)
                    {
                        UpdatePageFanRpms();
                        _appSettings.AsusModeFan1UserFanSpeedRpm = 110.0;
                    }
                    else
                    {
                        CpuFanRpm.Text = CoolerFan1Manual.Value.ToString(CultureInfo.InvariantCulture) + "%"; 
                    }

                    _appSettings.SaveSettings();
                }
            }
            else
            {
                if (_appSettings.NbfcServiceType == 2 || ServiceCombo.SelectedIndex == 2)
                {
                    CoolerFan1Manual.Value = _appSettings.NbfcFan1UserFanSpeedRpm;
                    CoolerFan2Manual.Value = _appSettings.NbfcFan2UserFanSpeedRpm;

                    NbfcFanSetSpeed();
                    if (CoolerFan1Manual.Value > 100)
                    {
                        UpdatePageFanRpms();
                        _appSettings.NbfcFan1UserFanSpeedRpm = 110.0;
                    }
                    else
                    {
                        CpuFanRpm.Text = CoolerFan1Manual.Value.ToString(CultureInfo.InvariantCulture) + "%";
                    }

                    _appSettings.SaveSettings();
                }
            }

            if (_appSettings.AsusCoolerServiceType == 1 || _appSettings.NbfcServiceType == 1 || ServiceCombo.SelectedIndex == 1)
            {
                UpdatePageFanRpms();
            }
        }
    }

    private async void InstallNbfc_Click(object sender, RoutedEventArgs e)
    {
        await ShowNbfcDialogAsync();
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

        var nbfcDialog = new ContentDialog
        {
            Title = "Warning".GetLocalized(),
            Content = stackPanel,
            CloseButtonText = "CancelThis/Text".GetLocalized(),
            PrimaryButtonText = "Next".GetLocalized(),
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false // Первоначально кнопка "Далее" неактивна
        };

        if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
        {
            nbfcDialog.XamlRoot = XamlRoot;
        }

        // Обработчик события нажатия на кнопку загрузки
        downloadButton.Click += async (_, _) =>
        {
            downloadButton.IsEnabled = false;
            progressBar.Opacity = 1.0;

            try
            {
                var client = new GitHubClient(new ProductHeaderValue("SakuOverclock"));
                var releases = await client.Repository.Release.GetAll("hirschmann", "nbfc");
                var latestRelease = releases[0];
                var release = latestRelease.Assets.FirstOrDefault(a => a.Name.EndsWith(".exe"));

                if (release == null)
                {
                    await LogHelper.TraceIt_TraceError(
                        "Cooler_DownloadNBFC_NoFileFound".GetLocalized());
                    return;
                }

                var downloadUrl = release.BrowserDownloadUrl;
                var downloadPath = Path.Combine(Path.GetTempPath(), release.Name); // Используем оригинальное имя файла

                // Скачивание файла с гарантированным освобождением ресурсов
                {
                    using var httpClient = new HttpClient();
                    using var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    var buffer = new byte[8192];
                    var totalRead = 0L;

                    await using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write,
                                     FileShare.None, 8192, true);
                    await using var downloadStream = await response.Content.ReadAsStreamAsync();
                    int bytesRead;
                    while ((bytesRead = await downloadStream.ReadAsync(buffer)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                        totalRead += bytesRead;

                        // Обновление прогресс-бара
                        if (totalBytes > 0)
                        {
                            progressBar.Value = (double)totalRead / totalBytes * 100;
                        }
                    }

                    await fileStream.FlushAsync();
                    // Гарантированное закрытие потоков
                }

                // Дополнительная задержка для полного освобождения файла системой
                await Task.Delay(500);

                // Запуск установщика с повторными попытками
                await LaunchNbfcInstallerWithRetry(downloadPath);

                // Обновление UI после успешной загрузки
                downloadButton.Opacity = 0.0;
                progressBar.Opacity = 0.0;

                nbfcDialog.Content = new TextBlock
                {
                    Text = "Cooler_DownloadNBFC_AfterDesc".GetLocalized(),
                    TextAlignment = TextAlignment.Center
                };
                nbfcDialog.IsPrimaryButtonEnabled = true;
            }
            catch (Exception ex)
            {
                await LogHelper.TraceIt_TraceError(
                    "Cooler_DownloadNBFC_ErrorDesc".GetLocalized() + $": {ex.Message}");

                // Восстановление UI при ошибке
                downloadButton.IsEnabled = true;
                progressBar.Opacity = 0.0;
            }
        };
        var result = await nbfcDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var navigation = App.GetService<INavigationService>();
            navigation.ReloadPage(typeof(КулерViewModel).FullName!); // Вызов метода перезагрузки страницы
        }
    }

    /// <summary>
    /// Вспомогательный метод для запуска установщика
    /// </summary>
    private static async Task LaunchNbfcInstallerWithRetry(string filePath)
    {
        const int maxRetries = 5;

        for (var retryCount = 0; retryCount < maxRetries; retryCount++)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException("Installer file not found", filePath);
                }

                // Проверка доступности файла
                await using (File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    // Файл доступен
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    Verb = "runas",
                    UseShellExecute = true
                });

                return;
            }
            catch (Exception ex)
            {
                if (retryCount >= maxRetries - 1)
                {
                    await LogHelper.LogError(
                        "Cooler_DownloadNBFC_ErrorDesc".GetLocalized() + $": {ex.Message}");
                    return;
                }

                await Task.Delay(2000);
            }
        }
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            try
            {
                AsusWinIoWrapper.Init_WinIo(); // Инит управления кулерами на ноутбуках Asus, если не загружен - Unavailable
                var fanCount = AsusWinIoWrapper.HealthyTable_FanCounts();
                if (fanCount == -1)
                {
                    _unavailableFlag = true;
                    AsusOptionsButton.IsEnabled = false;
                    AsusUnavailable.Visibility = Visibility.Visible;
                    ToolTipService.SetToolTip(AsusUnavailable, "Cooler_AsusModeUnsupportedSign".GetLocalized());
                }
                else
                {
                    StartAsusWinIoUpdate();
                }
            }
            catch (Exception exception)
            {
                _unavailableFlag = true;
                await LogHelper.TraceIt_TraceError(exception);
            }
            try
            {
                ServiceCombo.SelectedIndex = _appSettings.NbfcServiceType;

                CurveCombo.SelectedIndex =
                    _appSettings is { NbfcFan1UserFanSpeedRpm: > 100, NbfcFan2UserFanSpeedRpm: > 100 } ? 0 :
                    _appSettings is { NbfcFan1UserFanSpeedRpm: <= 100, NbfcFan2UserFanSpeedRpm: > 100 } ? 1 :
                    _appSettings is { NbfcFan1UserFanSpeedRpm: > 100, NbfcFan2UserFanSpeedRpm: <= 100 } ? 2 :
                    _appSettings is { NbfcFan1UserFanSpeedRpm: <= 100, NbfcFan2UserFanSpeedRpm: <= 100 } &&
                    _appSettings.NbfcFan1UserFanSpeedRpm - _appSettings.NbfcFan2UserFanSpeedRpm > 0 ? 3 :
                    _appSettings is { NbfcFan1UserFanSpeedRpm: <= 100, NbfcFan2UserFanSpeedRpm: <= 100 } &&
                    _appSettings.NbfcFan1UserFanSpeedRpm - _appSettings.NbfcFan2UserFanSpeedRpm == 0 ? 4 : 0;

                CoolerFan1Manual.Value = _appSettings.NbfcFan1UserFanSpeedRpm;
                CoolerFan2Manual.Value = _appSettings.NbfcFan2UserFanSpeedRpm;

                for (var i = 0; i < 2; i++)
                {
                    var doubleFan = i == 0 ? _appSettings.NbfcFan1UserFanSpeedRpm : _appSettings.NbfcFan2UserFanSpeedRpm;
                    switch (doubleFan)
                    {
                        case 90d:
                            SelectOnly(i == 0 ? NbfcTurboToggle : NbfcTurboToggle1);
                            break;
                        case 57d:
                            SelectOnly(i == 0 ? NbfcBalanceToggle : NbfcBalanceToggle1);
                            break;
                        case 37d:
                            SelectOnly(i == 0 ? NbfcQuietToggle : NbfcQuietToggle1);
                            break;
                        default:
                            SelectOnly(i == 0 ? NbfcAutoToggle : NbfcAutoToggle1);
                            (i == 0 ? CoolerFan1SliderGrid : CoolerFan2SliderGrid).Visibility = Visibility.Visible;
                            break;
                    }
                }

                try
                {
                    _isPageLoaded = true;
                    CurveCombo_SelectionChanged(null, null);
                    if (_isNbfcNotLoaded)
                    {
                        NbfcUnavailable.Visibility = Visibility.Visible;
                        CoolerManagementGrid.Visibility = Visibility.Collapsed;
                        CoolerManagementTypeGrid.Visibility = Visibility.Collapsed;
                        CoolerCurveFan1.Visibility = Visibility.Collapsed;
                        CoolerCurveFan2.Visibility = Visibility.Collapsed;

                        await ShowNbfcDialogAsync();
                    }
                }
                catch (Exception xException)
                {
                    await LogHelper.TraceIt_TraceError(xException.ToString());
                }
            }
            catch (Exception ex1)
            {
                await LogHelper.TraceIt_TraceError(ex1);
            }
        }
        catch (Exception e1)
        {
            await LogHelper.TraceIt_TraceError(e1.ToString());
        }
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        StopTempUpdate();
        StopAsusWinIoUpdate();
        AsusWinIoWrapper.Cleanup_WinIo();
        _isNbfcNotLoaded = false;
    }

    #endregion

    #region Event Handlers 

    private void EnterOneCoolerMode(bool enter = true)
    {
        CoolerFansGrid.ColumnDefinitions.Clear();
        CoolerFansGrid.ColumnDefinitions.Add(new ColumnDefinition());

        if (!enter)
        {
            CoolerFansGrid.ColumnDefinitions.Add(new ColumnDefinition());
        }

        CoolerFan2.Visibility = enter ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SelectOnly(ToggleButton name)
    {
        switch (name.Name)
        {
            case "NbfcQuietToggle":
                NbfcQuietToggle.IsChecked = true;
                NbfcBalanceToggle.IsChecked = false;
                NbfcTurboToggle.IsChecked = false;
                NbfcAutoToggle.IsChecked = false;
                break;
            case "NbfcBalanceToggle":
                NbfcQuietToggle.IsChecked = false;
                NbfcBalanceToggle.IsChecked = true;
                NbfcTurboToggle.IsChecked = false;
                NbfcAutoToggle.IsChecked = false;
                break;
            case "NbfcTurboToggle":
                NbfcQuietToggle.IsChecked = false;
                NbfcBalanceToggle.IsChecked = false;
                NbfcTurboToggle.IsChecked = true;
                NbfcAutoToggle.IsChecked = false;
                break;
            case "NbfcAutoToggle":
                NbfcQuietToggle.IsChecked = false;
                NbfcBalanceToggle.IsChecked = false;
                NbfcTurboToggle.IsChecked = false;
                NbfcAutoToggle.IsChecked = true;
                break;
            case "NbfcQuietToggle1":
                NbfcQuietToggle1.IsChecked = true;
                NbfcBalanceToggle1.IsChecked = false;
                NbfcTurboToggle1.IsChecked = false;
                NbfcAutoToggle1.IsChecked = false;
                break;
            case "NbfcBalanceToggle1":
                NbfcQuietToggle1.IsChecked = false;
                NbfcBalanceToggle1.IsChecked = true;
                NbfcTurboToggle1.IsChecked = false;
                NbfcAutoToggle1.IsChecked = false;
                break;
            case "NbfcTurboToggle1":
                NbfcQuietToggle1.IsChecked = false;
                NbfcBalanceToggle1.IsChecked = false;
                NbfcTurboToggle1.IsChecked = true;
                NbfcAutoToggle1.IsChecked = false;
                break;
            case "NbfcAutoToggle1":
                NbfcQuietToggle1.IsChecked = false;
                NbfcBalanceToggle1.IsChecked = false;
                NbfcTurboToggle1.IsChecked = false;
                NbfcAutoToggle1.IsChecked = true;
                break;
        }
    }


    private async void ConfigSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (!_isPageLoaded || _selectedModeAsus)
            {
                return;
            }

            await Task.Delay(200);
            _appSettings.NbfcConfigXmlName = (string)((ComboBoxItem)ConfigSelectorComboBox.SelectedItem).Content;
            _appSettings.SaveSettings();
            NbfcApplyFanConfig();
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception);
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
            await LogHelper.TraceIt_TraceError(exception);
        }
    }

    #endregion

    #region NBFC Tasks

    private void NbfcEnable()
    {
        if (_selectedModeAsus) { return; }
        var p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = "nbfc/nbfc.exe";
        p.StartInfo.Arguments = _appSettings.NbfcServiceType switch
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

    private void NbfcFanSetSpeed(int fanNumber = 0)
    {
        try
        {
            if (_selectedModeAsus) { return; }
            var p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.FileName = "nbfc/nbfc.exe";

            if (_appSettings.NbfcServiceType == 2)
            {
                var speedValue = fanNumber == 0 ? _appSettings.NbfcFan1UserFanSpeedRpm : _appSettings.NbfcFan2UserFanSpeedRpm;

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
            LogHelper.TraceIt_TraceError(ex);
        }
    }

    private void NbfcApplyFanConfig()
    {
        if (_selectedModeAsus) { return; }
        const string quote = "\"";
        var p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = "nbfc/nbfc.exe";
        p.StartInfo.Arguments = " config --apply " + quote + _appSettings.NbfcConfigXmlName + quote;
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;
        p.Start();
    }

    private void GetFanSpeedsThroughNbfc(bool start)
    {
        if (_selectedModeAsus)
        {
            if (_fanUpdateTimer != null && _fanUpdateTimer.IsEnabled)
            {
                _fanUpdateTimer.Stop();
            }
            return;
        }
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
        if (_selectedModeAsus) { return; }

        if (ServiceCombo.SelectedIndex is not (1 or 2))
        {
            return;
        }

        // Параллельно запрашиваем скорость обоих вентиляторов
        var fan1Task = GetFanSpeedAsync(0);
        var fan2Task = GetFanSpeedAsync(1);

        _appSettings.NbfcAnswerSpeedFan1 = await fan1Task;
        _appSettings.NbfcAnswerSpeedFan2 = await fan2Task;
        _appSettings.SaveSettings();

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
                await LogHelper.TraceIt_TraceError($"[Fan Control NBFC] Error (fan {fanNumber}): {ex.Message}");
            }

            return string.Empty;
        }
    }

    private void UpdatePageFanRpms()
    {
        CpuFanRpm.Text = _appSettings.NbfcAnswerSpeedFan1 == string.Empty ? "N/A" : SpeedHelper(_appSettings.NbfcAnswerSpeedFan1) + "%";
        GpuFanRpm.Text = _appSettings.NbfcAnswerSpeedFan2 == string.Empty ? "N/A" : SpeedHelper(_appSettings.NbfcAnswerSpeedFan2) + "%";
    }

    private async Task SuggestClickAsync()
    {
        if (_selectedModeAsus) { return; }

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
            await LogHelper.TraceIt_TraceError(ex);
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

    private void StartAsusWinIoUpdate()
    {
        if (_fanCount == 1)
        {
            EnterOneCoolerMode();
        }

        _rpmUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };

        _rpmUpdateTimer.Tick += (_, _) => _ = UpdateRpm();

    }

    private void StopAsusWinIoUpdate()
    {
        _rpmUpdateTimer?.Stop();
    }

    private async Task UpdateRpm()
    {
        var fanSpeed1 = string.Empty;
        var fanSpeed2 = string.Empty;
        await Task.Run(() =>
        {
            var fanSpeeds = GetFanSpeeds();
            fanSpeed1 = fanSpeeds.Count > 0 ? fanSpeeds[0].ToString() : string.Empty;
            fanSpeed2 = fanSpeeds.Count > 1 ? fanSpeeds[1].ToString() : string.Empty;
        });

        CpuFanRpm.Text = fanSpeed1 == string.Empty ? "N/A" : SpeedHelper(fanSpeed1) + "%";
        GpuFanRpm.Text = fanSpeed2 == string.Empty ? "N/A" : SpeedHelper(fanSpeed2) + "%";
    }

    private void UpdateTemperatureAsync()
    {
        CpuFanTemp.Text = "CPU Temp " + Math.Round(_cpuTemp, 1) + "C";
        GpuFanTemp.Text = "GPU Temp " + (_gpuTemp == 0d ? "N/A" : Math.Round(_gpuTemp, 1) + "C");
        TdpLimitSensorText.Text = Math.Round(_cpuTdpLim, 1) + "W";
        TdpValueSensorText.Text = Math.Round(_cpuTdpVal, 1).ToString(CultureInfo.InvariantCulture);
        CpuFreqSensorText.Text = Math.Round(_cpuFreq, 1).ToString(CultureInfo.InvariantCulture);
        CpuCurrentSensorText.Text = Math.Round(_cpuCurrent, 1).ToString(CultureInfo.InvariantCulture);
    }

    private void OnDataUpdated(object? sender, SensorsInformation info)
    {
        _cpuTemp = info.CpuTempValue;
        _gpuTemp = info.ApuTempValue;
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

        var sepIndex = commaIndex >= 0 ? commaIndex :
                       dotIndex >= 0 ? dotIndex : -1;

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
        if (_selectedModeAsus)
        {
            _appSettings.AsusCoolerServiceType = ServiceCombo.SelectedIndex;

        }
        else
        {
            _appSettings.NbfcServiceType = ServiceCombo.SelectedIndex;
        }

        _appSettings.SaveSettings();

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
                case "NbfcAutoToggle":
                    NbfcTurboToggle.IsChecked = false;
                    NbfcBalanceToggle.IsChecked = false;
                    NbfcQuietToggle.IsChecked = false;
                    CoolerFan1SliderGrid.Visibility = Visibility.Visible;

                    if (CurveCombo.SelectedIndex == 4)
                    {
                        _appSettings.NbfcFan2UserFanSpeedRpm = CoolerFan1Manual.Value;
                        CoolerFan2Manual.Value = CoolerFan1Manual.Value;
                    }
                    _appSettings.NbfcFan1UserFanSpeedRpm = CoolerFan1Manual.Value;
                    _appSettings.SaveSettings();
                    NbfcFanSetSpeed();
                    break;
                case "NbfcTurboToggle":
                    NbfcAutoToggle.IsChecked = false;
                    NbfcBalanceToggle.IsChecked = false;
                    NbfcQuietToggle.IsChecked = false;
                    CoolerFan1SliderGrid.Visibility = Visibility.Collapsed;

                    if (CurveCombo.SelectedIndex == 4)
                    {
                        _appSettings.NbfcFan2UserFanSpeedRpm = 90d;
                        CoolerFan2Manual.Value = 90d;
                    }
                    _appSettings.NbfcFan1UserFanSpeedRpm = 90d;
                    CoolerFan1Manual.Value = 90d;
                    _appSettings.SaveSettings();
                    NbfcFanSetSpeed();
                    break;
                case "NbfcBalanceToggle":
                    NbfcAutoToggle.IsChecked = false;
                    NbfcTurboToggle.IsChecked = false;
                    NbfcQuietToggle.IsChecked = false;
                    CoolerFan1SliderGrid.Visibility = Visibility.Collapsed;

                    if (CurveCombo.SelectedIndex == 4)
                    {
                        CoolerFan2Manual.Value = 57d;
                        _appSettings.NbfcFan2UserFanSpeedRpm = 57d;
                    }
                    CoolerFan1Manual.Value = 57d;
                    _appSettings.NbfcFan1UserFanSpeedRpm = 57d;
                    _appSettings.SaveSettings();
                    NbfcFanSetSpeed();
                    break;
                case "NbfcQuietToggle":
                    NbfcAutoToggle.IsChecked = false;
                    NbfcTurboToggle.IsChecked = false;
                    NbfcBalanceToggle.IsChecked = false;
                    CoolerFan1SliderGrid.Visibility = Visibility.Collapsed;

                    if (CurveCombo.SelectedIndex == 4)
                    {
                        CoolerFan2Manual.Value = 37d;
                        _appSettings.NbfcFan2UserFanSpeedRpm = 37d;
                    }
                    CoolerFan1Manual.Value = 37d;
                    _appSettings.NbfcFan1UserFanSpeedRpm = 37d;
                    _appSettings.SaveSettings();
                    NbfcFanSetSpeed();
                    break;

                case "NbfcAutoToggle1":
                    NbfcTurboToggle1.IsChecked = false;
                    NbfcBalanceToggle1.IsChecked = false;
                    NbfcQuietToggle1.IsChecked = false;
                    CoolerFan2SliderGrid.Visibility = Visibility.Visible;

                    _appSettings.NbfcFan2UserFanSpeedRpm = CoolerFan2Manual.Value;
                    _appSettings.SaveSettings();
                    NbfcFanSetSpeed(1);
                    break;
                case "NbfcTurboToggle1":
                    NbfcAutoToggle1.IsChecked = false;
                    NbfcBalanceToggle1.IsChecked = false;
                    NbfcQuietToggle1.IsChecked = false;
                    CoolerFan2SliderGrid.Visibility = Visibility.Collapsed;

                    CoolerFan2Manual.Value = 90d;
                    _appSettings.NbfcFan2UserFanSpeedRpm = 90d;
                    _appSettings.SaveSettings();
                    NbfcFanSetSpeed(1);
                    break;
                case "NbfcBalanceToggle1":
                    NbfcAutoToggle1.IsChecked = false;
                    NbfcTurboToggle1.IsChecked = false;
                    NbfcQuietToggle1.IsChecked = false;
                    CoolerFan2SliderGrid.Visibility = Visibility.Collapsed;

                    CoolerFan2Manual.Value = 57d;
                    _appSettings.NbfcFan2UserFanSpeedRpm = 57d;
                    _appSettings.SaveSettings();
                    NbfcFanSetSpeed(1);
                    break;
                case "NbfcQuietToggle1":
                    NbfcBalanceToggle1.IsChecked = false;
                    NbfcTurboToggle1.IsChecked = false;
                    NbfcAutoToggle1.IsChecked = false;
                    CoolerFan2SliderGrid.Visibility = Visibility.Collapsed;

                    CoolerFan2Manual.Value = 37d;
                    _appSettings.NbfcFan2UserFanSpeedRpm = 37d;
                    _appSettings.SaveSettings();
                    NbfcFanSetSpeed(1);
                    break;
            }

            _appSettings.SaveSettings();
        }

        if ((NbfcQuietToggle.IsChecked == false && NbfcBalanceToggle.IsChecked == false &&
            NbfcTurboToggle.IsChecked == false && NbfcAutoToggle.IsChecked == false) ||
            (NbfcQuietToggle1.IsChecked == false && NbfcBalanceToggle1.IsChecked == false &&
            NbfcTurboToggle1.IsChecked == false && NbfcAutoToggle1.IsChecked == false))
        {
            toggleButton.IsChecked = true;
        }
    }

    private void CurveCombo_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
    {
        if (!_isPageLoaded) { return; }
        CoolerCurveFanText.Text = CoolerCurveFanText.Text.Replace("Cooler_Curve_Fan1_Part".GetLocalized(), string.Empty);
        switch (CurveCombo.SelectedIndex)
        {
            case 0: // Оба авто
                CoolerCurveFan1.Visibility = Visibility.Collapsed;
                CoolerCurveFan2.Visibility = Visibility.Collapsed;
                _appSettings.NbfcFan1UserFanSpeedRpm = 110;
                _appSettings.NbfcFan2UserFanSpeedRpm = 110;
                break;
            case 1: // Фиксированный только первый 

                CoolerCurveFanText.Text += "Cooler_Curve_Fan1_Part".GetLocalized();
                CoolerCurveFan1.Visibility = Visibility.Visible;
                CoolerCurveFan2.Visibility = Visibility.Collapsed;
                _appSettings.NbfcFan1UserFanSpeedRpm = _appSettings.NbfcFan1UserFanSpeedRpm > 100 ? 70 : _appSettings.NbfcFan1UserFanSpeedRpm;
                _appSettings.NbfcFan2UserFanSpeedRpm = 110;
                break;
            case 2: // Фиксированный только второй 
                CoolerCurveFan1.Visibility = Visibility.Collapsed;
                CoolerCurveFan2.Visibility = Visibility.Visible;
                _appSettings.NbfcFan1UserFanSpeedRpm = 110;
                _appSettings.NbfcFan2UserFanSpeedRpm = _appSettings.NbfcFan2UserFanSpeedRpm > 100 ? 70 : _appSettings.NbfcFan2UserFanSpeedRpm;
                break;
            case 3: // Оба фиксированные но различные
                CoolerCurveFanText.Text += "Cooler_Curve_Fan1_Part".GetLocalized();
                CoolerCurveFan1.Visibility = Visibility.Visible;
                CoolerCurveFan2.Visibility = Visibility.Visible; // Потому что оба будут управляться с одного месте, с первого блока настроек
                _appSettings.NbfcFan1UserFanSpeedRpm = _appSettings.NbfcFan1UserFanSpeedRpm > 100 ? 70 : _appSettings.NbfcFan1UserFanSpeedRpm;
                _appSettings.NbfcFan2UserFanSpeedRpm = _appSettings.NbfcFan2UserFanSpeedRpm > 100 ? 50 : _appSettings.NbfcFan2UserFanSpeedRpm;
                break;
            case 4: // Оба фиксированные
                CoolerCurveFan1.Visibility = Visibility.Visible;
                CoolerCurveFan2.Visibility = Visibility.Collapsed; // Потому что оба будут управляться с одного месте, с первого блока настроек
                _appSettings.NbfcFan1UserFanSpeedRpm = _appSettings.NbfcFan1UserFanSpeedRpm > 100 ? 70 : _appSettings.NbfcFan1UserFanSpeedRpm;
                _appSettings.NbfcFan2UserFanSpeedRpm = _appSettings.NbfcFan2UserFanSpeedRpm > 100 ? 70 : _appSettings.NbfcFan1UserFanSpeedRpm;
                break;
        }
        _appSettings.SaveSettings();
        NbfcFanSetSpeed();
        NbfcFanSetSpeed(1);
    }

    private void ModeOptions_Button_Click(object sender, RoutedEventArgs e)
    {
        if (_unavailableFlag)
        {
            NbfcOptionsButton.IsChecked = true;
            AsusOptionsButton.IsChecked = false;
            _appSettings.IsNbfcModeEnabled = false;
            _appSettings.SaveSettings();
            return;
        }
        _appSettings.IsNbfcModeEnabled = !_appSettings.IsNbfcModeEnabled;
        _appSettings.SaveSettings();

        AsusOptionsButton.IsChecked = _appSettings.IsNbfcModeEnabled;
        NbfcOptionsButton.IsChecked = !_appSettings.IsNbfcModeEnabled;
        _selectedModeAsus = _appSettings.IsNbfcModeEnabled;

        CoolerConfig.Visibility = _selectedModeAsus ? Visibility.Collapsed : Visibility.Visible;
        if (NbfcUnavailable.Visibility == Visibility.Visible && _selectedModeAsus)
        {
            NbfcUnavailable.Visibility = Visibility.Collapsed;
            CoolerManagementGrid.Visibility = Visibility.Visible;
            CoolerManagementTypeGrid.Visibility = Visibility.Visible;
            CoolerCurveFan1.Visibility = Visibility.Visible;
            CoolerCurveFan2.Visibility = Visibility.Visible;
        }
        else
        {
            if (_isNbfcNotLoaded)
            {
                NbfcUnavailable.Visibility = Visibility.Visible;
                CoolerManagementGrid.Visibility = Visibility.Collapsed;
                CoolerManagementTypeGrid.Visibility = Visibility.Collapsed;
                CoolerCurveFan1.Visibility = Visibility.Collapsed;
                CoolerCurveFan2.Visibility = Visibility.Collapsed;
            }
        }

    }

    private async void Cooler_Fan_Manual_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        try
        {
            var fanNumber = 0;
            var fanValue = CoolerFan1Manual.Value;
            var fanRpmTextBlock = CpuFanRpm;

            if (sender is Slider { Name: "CoolerFan2Manual" })
            {
                fanNumber = 1;
                fanValue = CoolerFan2Manual.Value;
                fanRpmTextBlock = GpuFanRpm;
            }

            if (ServiceCombo.SelectedIndex == 2)
            {
                NbfcFanSetSpeed(fanNumber);

                await Task.Delay(200);

                var fanInvar = fanValue.ToString(CultureInfo.InvariantCulture);

                if (fanNumber == 0)
                {
                    if (_selectedModeAsus)
                    {
                        _appSettings.AsusModeFan1UserFanSpeedRpm = fanValue;
                    }
                    else
                    {
                        _appSettings.NbfcFan1UserFanSpeedRpm = fanValue;
                    }

                    if (CurveCombo.SelectedIndex == 4)
                    {
                        if (_selectedModeAsus)
                        {
                            _appSettings.AsusModeFan2UserFanSpeedRpm = fanValue;
                        }
                        else
                        {
                            _appSettings.NbfcFan2UserFanSpeedRpm = fanValue;
                        }
                        CoolerFan2Manual.Value = fanValue;
                    }
                }
                else
                {
                    if (_selectedModeAsus)
                    {
                        _appSettings.AsusModeFan2UserFanSpeedRpm = fanValue;
                    }
                    else
                    {
                        _appSettings.NbfcFan2UserFanSpeedRpm = fanValue;
                    }
                }

                fanRpmTextBlock.Text = fanInvar + "%";

                _appSettings.SaveSettings();
            }

            if (ServiceCombo.SelectedIndex == 1)
            {
                UpdatePageFanRpms();
            }
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception);
        }
    }

    #region Asus WinIo Voids

    #region Set Fan Speed

    private static void SetFanSpeeds(int percent)
    {
        var value = (byte)(percent / 100.0f * 255);
        SetFanSpeeds(value);
    }
    private static void SetOneFanSpeed(int percent, byte fan = 0)
    {
        var value = (byte)(percent / 100.0f * 255);
        if (_fanCount == -1 && _unavailableFlag == false)
        {
            _fanCount = AsusWinIoWrapper.HealthyTable_FanCounts(); // Не обновлять лишний раз это значение
        }
        SetFanSpeed(value, fan);
    }

    private static async void SetFanSpeeds(byte value)
    {
        try
        {
            if (_fanCount == -1 && _unavailableFlag == false)
            {
                _fanCount = AsusWinIoWrapper.HealthyTable_FanCounts(); // Не обновлять лишний раз это значение
            }

            for (byte fanIndex = 0; fanIndex < _fanCount; fanIndex++)
            {
                SetFanSpeed(value, fanIndex);
                await Task.Delay(20);
            }
        }
        catch (Exception e)
        {
            await LogHelper.TraceIt_TraceError(e);
        }
    }

    private static void SetFanSpeed(byte value, byte fanIndex = 0)
    {
        AsusWinIoWrapper.HealthyTable_SetFanIndex(fanIndex);
        AsusWinIoWrapper.HealthyTable_SetFanTestMode((char)(value > 0 ? 0x01 : 0x00));
        AsusWinIoWrapper.HealthyTable_SetFanPwmDuty(value);
    }

    #endregion

    #region Get Fan Speed

    private static List<int> GetFanSpeeds()
    {
        var fanSpeeds = new List<int>();

        if (_fanCount == -1 && _unavailableFlag == false)
        {
            _fanCount = AsusWinIoWrapper.HealthyTable_FanCounts(); // Не обновлять лишний раз это значение
            if (_fanCount == -1)
            {
                _unavailableFlag = true;
            }
        }

        for (byte fanIndex = 0; fanIndex < _fanCount; fanIndex++)
        {
            var fanSpeed = GetFanSpeed(fanIndex);
            fanSpeeds.Add(fanSpeed);
        }

        return fanSpeeds;
    }

    private static int GetFanSpeed(byte fanIndex = 0)
    {
        if (_setFanIndex != fanIndex)
        {
            AsusWinIoWrapper.HealthyTable_SetFanIndex(fanIndex);
            _setFanIndex =
                fanIndex; // Лишний раз не использовать, после использования задать значение, которое было использовано
        }

        var fanSpeed = AsusWinIoWrapper.HealthyTable_FanRPM();
        return fanSpeed;
    }

    #endregion

    #endregion
}