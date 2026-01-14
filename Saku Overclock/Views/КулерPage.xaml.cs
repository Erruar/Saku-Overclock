using System.Diagnostics;
using Windows.Foundation.Metadata;
using Windows.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using Octokit;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Models;
using Saku_Overclock.ViewModels;
using Saku_Overclock.Wrappers;
using Application = Microsoft.UI.Xaml.Application;
using Exception = System.Exception;
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
    private DispatcherTimer? _fanUpdateTimer;
    private readonly IAppSettingsService _appSettings = App.GetService<IAppSettingsService>();
    private readonly IBackgroundDataUpdater _dataUpdater = App.GetService<IBackgroundDataUpdater>();
    private bool _selectedModeAsus;


    public КулерPage()
    {
        InitializeComponent();

        Loaded += Page_Loaded;
        Unloaded += Page_Unloaded;

        _fanUpdateTimer = new DispatcherTimer();
        _fanUpdateTimer.Tick += async (_, _) => await GetFanSpeedNfbc();
        _fanUpdateTimer.Interval = TimeSpan.FromMilliseconds(5000);
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
                ShowAlert("Cooler_AsusModeAvailabilityDesc".GetLocalized());
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

                    await SetFanSpeed();
                    if (CoolerFan1Manual.Value > 100)
                    {
                        UpdateFanSpeedsOnPage();
                        _appSettings.AsusModeFan1UserFanSpeedRpm = 110.0;
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

                    await SetFanSpeed();
                    if (CoolerFan1Manual.Value > 100)
                    {
                        UpdateFanSpeedsOnPage();
                        _appSettings.NbfcFan1UserFanSpeedRpm = 110.0;
                    }

                    _appSettings.SaveSettings();
                }
            }

            if (_appSettings.AsusCoolerServiceType == 1 || _appSettings.NbfcServiceType == 1 ||
                ServiceCombo.SelectedIndex == 1)
            {
                UpdateFanSpeedsOnPage();
            }
        }
    }

    private async void InstallNbfc_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await ShowNbfcDialogAsync();
        }
        catch (Exception ex)
        {
            await LogHelper.LogError(ex);
        }
    }

    private void ShowAlert(string text)
    {
        AlertGrid.Visibility = Visibility.Visible;
        AlertTextBlock.Text = text;
    }

    private void HideAlert()
    {
        AlertGrid.Visibility = Visibility.Collapsed;
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
                    using var response =
                        await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
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
    ///     Вспомогательный метод для запуска установщика
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
            FanInit();
            UpdateFanSpeedsOnPage();

            try
            {
                AsusWinIoWrapper
                    .Init_WinIo(); // Инициализация управления кулерами на ноутбуках Asus, если не загружен - Unavailable
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
                    var doubleFan =
                        i == 0 ? _appSettings.NbfcFan1UserFanSpeedRpm : _appSettings.NbfcFan2UserFanSpeedRpm;
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

            var success = await NotebookFanControlWrapper.OpenClientAsync();
            if (!success)
            {
                ShowAlert("Cooler_NbfcServiceUnavailable".GetLocalized());
            }
        }
        catch (Exception e1)
        {
            await LogHelper.TraceIt_TraceError(e1.ToString());
        }
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= Page_Loaded;
        Unloaded -= Page_Unloaded;
        _appSettings.NbfcAnswerSpeedFan1 = -1;
        _appSettings.NbfcAnswerSpeedFan2 = -1;
        _appSettings.SaveSettings();
        StopTempUpdate();
        StopAsusWinIoUpdate();
        GetFanSpeedsThroughNbfc(false);
        _tempUpdateTimer = null;
        _fanUpdateTimer = null;
        AsusWinIoWrapper.Cleanup_WinIo();
        NotebookFanControlWrapper.CloseClient();
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
            await NbfcApplyFanConfig();
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

    private async Task NbfcEnable()
    {
        if (_selectedModeAsus)
        {
            return;
        }

        switch (_appSettings.NbfcServiceType)
        {
            case 0:
                await NotebookFanControlWrapper.StopServiceAsync();
                break;
            case 2:
                await NotebookFanControlWrapper.StartServiceAsync();
                break;
            case 1:
                await NotebookFanControlWrapper.StartServiceAsync(true);
                break;
        }
    }

    private async Task SetFanSpeed(int fanNumber = 0)
    {
        try
        {
            if (_selectedModeAsus)
            {
                var speedValue = fanNumber == 0
                    ? _appSettings.AsusModeFan1UserFanSpeedRpm
                    : _appSettings.AsusModeFan2UserFanSpeedRpm;
                if (speedValue > 100)
                {
                    speedValue = 0; // Auto
                }

                SetOneFanSpeed(speedValue, fanNumber);
                return;
            }

            if (_appSettings.NbfcServiceType == 2)
            {
                var speedValue = fanNumber == 0
                    ? _appSettings.NbfcFan1UserFanSpeedRpm
                    : _appSettings.NbfcFan2UserFanSpeedRpm;
                if (speedValue > 100)
                {
                    speedValue = 101;
                }

                var success = await NotebookFanControlWrapper.SetFanSpeedAsync(speedValue, fanNumber);
                if (!success)
                {
                    ShowAlert("Cooler_NbfcServiceUnavailable".GetLocalized());
                }
            }
        }
        catch (Exception ex)
        {
            await LogHelper.TraceIt_TraceError(ex);
        }
    }

    private async Task NbfcApplyFanConfig()
    {
        if (_selectedModeAsus)
        {
            return;
        }

        await NotebookFanControlWrapper.ApplyConfigAsync(_appSettings.NbfcConfigXmlName);
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

    private async Task GetFanSpeedNfbc()
    {
        if (_selectedModeAsus || !_isPageLoaded ||
            ServiceCombo.SelectedIndex is not (1 or 2))
        {
            return;
        }

        // Запрашиваем скорость обоих вентиляторов
        _appSettings.NbfcAnswerSpeedFan1 = await NotebookFanControlWrapper.GetFanSpeedAsync(0);
        _appSettings.NbfcAnswerSpeedFan2 = await NotebookFanControlWrapper.GetFanSpeedAsync(1);
        _appSettings.SaveSettings();

        if (_appSettings.NbfcAnswerSpeedFan1 < 0)
        {
            ShowAlert("Cooler_NbfcServiceUnavailable".GetLocalized());
        }
        else
        {
            HideAlert();
        }
        UpdateFanSpeedsOnPage();
    }

    private void UpdateFanSpeedsOnPage()
    {
        CpuFanRpm.Text = _appSettings.NbfcAnswerSpeedFan1 < 0 ? "N/A" : $"{_appSettings.NbfcAnswerSpeedFan1}%";
        GpuFanRpm.Text = _appSettings.NbfcAnswerSpeedFan2 < 0 ? "N/A" : $"{_appSettings.NbfcAnswerSpeedFan2}%";
    }

    private async Task SuggestClickAsync()
    {
        if (_selectedModeAsus)
        {
            return;
        }

        try
        {
            var output = string.Join(Environment.NewLine,
                await NotebookFanControlWrapper.GetRecommendedConfigNamesAsync());
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

        _rpmUpdateTimer?.Stop();
        _rpmUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };

        _rpmUpdateTimer.Tick += (_, _) => _ = UpdateRpm();
    }

    private void StopAsusWinIoUpdate() => _rpmUpdateTimer?.Stop();

    private async Task UpdateRpm()
    {
        var fanSpeed1 = -1;
        var fanSpeed2 = -1;
        await Task.Run(() =>
        {
            var fanSpeeds = GetFanSpeeds();
            fanSpeed1 = fanSpeeds.Count > 0 ? fanSpeeds[0] : -1;
            fanSpeed2 = fanSpeeds.Count > 1 ? fanSpeeds[1] : -1;
        });

        CpuFanRpm.Text = fanSpeed1 == -1 ? "N/A" : $"{fanSpeed1}%";
        GpuFanRpm.Text = fanSpeed2 == -1 ? "N/A" : $"{fanSpeed2}%";
    }

    private void UpdateTemperatureAsync()
    {
        CpuFanTemp.Text = $"CPU Temp {_cpuTemp:F1}C";
        GpuFanTemp.Text = $"GPU Temp {(_gpuTemp == 0d ? "N/A" : $"{_gpuTemp:F1}C")}";
        TdpLimitSensorText.Text = $"{_cpuTdpLim:F1}W";
        TdpValueSensorText.Text = _cpuTdpVal.ToString("F1");
        CpuFreqSensorText.Text = _cpuFreq.ToString("F1");
        CpuCurrentSensorText.Text = _cpuCurrent.ToString("F1");
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


    private async void ServiceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (!_isPageLoaded)
            {
                return;
            }

            if (_selectedModeAsus)
            {
                _appSettings.AsusCoolerServiceType = ServiceCombo.SelectedIndex;
            }
            else
            {
                _appSettings.NbfcServiceType = ServiceCombo.SelectedIndex;
            }

            _appSettings.SaveSettings();

            await NbfcEnable();

            if (ServiceCombo.SelectedIndex == 1)
            {
                UpdateFanSpeedsOnPage();
            }
        }
        catch (Exception ex)
        {
            await LogHelper.LogError(ex);
        }
    }

    private async void Nbfc_Fan1Control_Click(object sender, RoutedEventArgs e)
    {
        try
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
                        await SetFanSpeed();
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
                        await SetFanSpeed();
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
                        await SetFanSpeed();
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
                        await SetFanSpeed();
                        break;

                    case "NbfcAutoToggle1":
                        NbfcTurboToggle1.IsChecked = false;
                        NbfcBalanceToggle1.IsChecked = false;
                        NbfcQuietToggle1.IsChecked = false;
                        CoolerFan2SliderGrid.Visibility = Visibility.Visible;

                        _appSettings.NbfcFan2UserFanSpeedRpm = CoolerFan2Manual.Value;
                        _appSettings.SaveSettings();
                        await SetFanSpeed(1);
                        break;
                    case "NbfcTurboToggle1":
                        NbfcAutoToggle1.IsChecked = false;
                        NbfcBalanceToggle1.IsChecked = false;
                        NbfcQuietToggle1.IsChecked = false;
                        CoolerFan2SliderGrid.Visibility = Visibility.Collapsed;

                        CoolerFan2Manual.Value = 90d;
                        _appSettings.NbfcFan2UserFanSpeedRpm = 90d;
                        _appSettings.SaveSettings();
                        await SetFanSpeed(1);
                        break;
                    case "NbfcBalanceToggle1":
                        NbfcAutoToggle1.IsChecked = false;
                        NbfcTurboToggle1.IsChecked = false;
                        NbfcQuietToggle1.IsChecked = false;
                        CoolerFan2SliderGrid.Visibility = Visibility.Collapsed;

                        CoolerFan2Manual.Value = 57d;
                        _appSettings.NbfcFan2UserFanSpeedRpm = 57d;
                        _appSettings.SaveSettings();
                        await SetFanSpeed(1);
                        break;
                    case "NbfcQuietToggle1":
                        NbfcBalanceToggle1.IsChecked = false;
                        NbfcTurboToggle1.IsChecked = false;
                        NbfcAutoToggle1.IsChecked = false;
                        CoolerFan2SliderGrid.Visibility = Visibility.Collapsed;

                        CoolerFan2Manual.Value = 37d;
                        _appSettings.NbfcFan2UserFanSpeedRpm = 37d;
                        _appSettings.SaveSettings();
                        await SetFanSpeed(1);
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
        catch (Exception ex)
        {
            await LogHelper.LogError(ex);
        }
    }

    private async void CurveCombo_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
    {
        try
        {
            if (!_isPageLoaded)
            {
                return;
            }

            CoolerCurveFanText.Text =
                CoolerCurveFanText.Text.Replace("Cooler_Curve_Fan1_Part".GetLocalized(), string.Empty);
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
                    _appSettings.NbfcFan1UserFanSpeedRpm = _appSettings.NbfcFan1UserFanSpeedRpm > 100
                        ? CoolerFan1Manual.Value
                        : _appSettings.NbfcFan1UserFanSpeedRpm;
                    _appSettings.NbfcFan2UserFanSpeedRpm = 110;
                    break;
                case 2: // Фиксированный только второй 
                    CoolerCurveFan1.Visibility = Visibility.Collapsed;
                    CoolerCurveFan2.Visibility = Visibility.Visible;
                    _appSettings.NbfcFan1UserFanSpeedRpm = 110;
                    _appSettings.NbfcFan2UserFanSpeedRpm = _appSettings.NbfcFan2UserFanSpeedRpm > 100
                        ? CoolerFan2Manual.Value
                        : _appSettings.NbfcFan2UserFanSpeedRpm;
                    break;
                case 3: // Оба фиксированные, но различные
                    CoolerCurveFanText.Text += "Cooler_Curve_Fan1_Part".GetLocalized();
                    CoolerCurveFan1.Visibility = Visibility.Visible;
                    CoolerCurveFan2.Visibility = Visibility.Visible; // Потому что оба будут управляться раздельно
                    _appSettings.NbfcFan1UserFanSpeedRpm = _appSettings.NbfcFan1UserFanSpeedRpm > 100
                        ? CoolerFan1Manual.Value
                        : _appSettings.NbfcFan1UserFanSpeedRpm;
                    _appSettings.NbfcFan2UserFanSpeedRpm = _appSettings.NbfcFan2UserFanSpeedRpm > 100
                        ? CoolerFan2Manual.Value
                        : _appSettings.NbfcFan2UserFanSpeedRpm;
                    break;
                case 4: // Оба фиксированные
                    CoolerCurveFan1.Visibility = Visibility.Visible;
                    CoolerCurveFan2.Visibility =
                        Visibility
                            .Collapsed; // Потому что оба будут управляться с одного места, с первого блока настроек
                    _appSettings.NbfcFan1UserFanSpeedRpm = _appSettings.NbfcFan1UserFanSpeedRpm > 100
                        ? CoolerFan1Manual.Value
                        : _appSettings.NbfcFan1UserFanSpeedRpm;
                    _appSettings.NbfcFan2UserFanSpeedRpm = _appSettings.NbfcFan2UserFanSpeedRpm > 100
                        ? CoolerFan2Manual.Value
                        : _appSettings.NbfcFan2UserFanSpeedRpm;
                    break;
            }

            _appSettings.SaveSettings();
            await SetFanSpeed();
            await SetFanSpeed(1);
        }
        catch (Exception ex)
        {
            await LogHelper.LogError(ex);
        }
    }

    private void ModeOptions_Button_Click(object sender, RoutedEventArgs e)
    {
        if (_unavailableFlag)
        {
            NbfcOptionsButton.IsChecked = true;
            AsusOptionsButton.IsChecked = false;
            HideAlert();
            _appSettings.IsNbfcModeEnabled = true;
            _appSettings.SaveSettings();
            StopAsusWinIoUpdate();
            return;
        }

        _appSettings.IsNbfcModeEnabled = !_appSettings.IsNbfcModeEnabled;
        _appSettings.SaveSettings();

        AsusOptionsButton.IsChecked = _appSettings.IsNbfcModeEnabled;
        NbfcOptionsButton.IsChecked = !_appSettings.IsNbfcModeEnabled;
        _selectedModeAsus = _appSettings.IsNbfcModeEnabled;
        if (_selectedModeAsus)
        {
            ShowAlert("Cooler_AsusModeAvailabilityDesc".GetLocalized());
            StartAsusWinIoUpdate();
            GetFanSpeedsThroughNbfc(false);
        }
        else
        {
            HideAlert();
            StopAsusWinIoUpdate();
            GetFanSpeedsThroughNbfc(true);
        }

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

            if (sender is Slider { Name: "CoolerFan2Manual" })
            {
                fanNumber = 1;
                fanValue = CoolerFan2Manual.Value;
            }

            if (ServiceCombo.SelectedIndex == 2)
            {
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

                _appSettings.SaveSettings();
                await SetFanSpeed(fanNumber);
            }

            if (ServiceCombo.SelectedIndex == 1)
            {
                UpdateFanSpeedsOnPage();
            }
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception);
        }
    }

    #region Asus WinIo Voids

    #region Set Fan Speed

    private static void SetOneFanSpeed(double percent, int fan = 0)
    {
        var value = (byte)(percent / 100.0f * 255);
        if (_fanCount == -1 && _unavailableFlag == false)
        {
            _fanCount = AsusWinIoWrapper.HealthyTable_FanCounts(); // Не обновлять лишний раз это значение
        }

        SetFanSpeed(value, (byte)fan);
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