using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.JsonContainers;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.ViewModels;
using Windows.UI.Core;
using Windows.UI.Text;
using ZenStates.Core;
using VisualTreeHelper = Saku_Overclock.Helpers.VisualTreeHelper;

namespace Saku_Overclock.Views;

public sealed partial class ГлавнаяPage
{
    private readonly IBackgroundDataUpdater? _dataUpdater;

    private static readonly IAppSettingsService AppSettings = App.GetService<IAppSettingsService>();
    private static readonly IApplyerService _applyer = App.GetService<IApplyerService>();
    private static readonly IAppNotificationService NotificationsService = App.GetService<IAppNotificationService>(); // Уведомления приложения
    
    private static Profile[] _profile = new Profile[1]; // Всегда по умолчанию будет 1 профиль
    
    private readonly Cpu? _cpu;
    
    private int _currentMode; // Хранит режим, который выбрал пользователь, то, где стоял курсор при нажатии на блок, чтобы показать тултип с дополнительной информацией о просматриемом блоке
    private bool _waitForCheck;
    private int _tipVersion = 0;
    private bool _isAnimating = false;
    private string _doubleClickApplyPrev = string.Empty;
    private int _lastAppliedPreset = -2; // Начальное значение, которое точно не совпадёт
    private string _lastAppliedProfileName = string.Empty;
    private double _maxCpuFreq = 1d;
    private bool _isFirstLoad = true;
    private bool _isWindowVisible = true;

    private readonly string _batteryUnavailable = "Main_BatteryUnavailable".GetLocalized();
    private readonly string _ghzInfo = "infoAGHZ".GetLocalized();
    private readonly string _powerSumDisabled = "Info_PowerSumInfo_Disabled".GetLocalized();
    private readonly string _fromWall = "InfoBatteryAC".GetLocalized();

    public ГлавнаяPage()
    {
        App.GetService<ГлавнаяViewModel>();
        InitializeComponent();
        _dataUpdater = App.BackgroundUpdater!;
        _dataUpdater.DataUpdated += OnDataUpdated;

        try
        {
            _cpu = CpuSingleton.GetInstance();
        }
        catch (Exception e) 
        {
            LogHelper.LogError(e);
        }

        Unloaded += ГлавнаяPage_Unloaded;
        Loaded += ГлавнаяPage_Loaded;

        App.MainWindow.WindowStateChanged += OnVisibilityChanged;

    }

    private void ГлавнаяPage_Unloaded(object sender, RoutedEventArgs e)
    {
        // Отписка от всех событий для предотвращения утечек памяти
        if (_dataUpdater != null) 
        {
            _dataUpdater.DataUpdated -= OnDataUpdated;
        }

        App.MainWindow.WindowStateChanged -= OnVisibilityChanged;

        Unloaded -= ГлавнаяPage_Unloaded;
        Loaded -= ГлавнаяPage_Loaded;
    }
    private void OnVisibilityChanged(object? s, WindowState e)
    {
        _isWindowVisible = App.MainWindow.WindowState != WindowState.Minimized;
    }

    private void ГлавнаяPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            LoadProfiles();

            // Принудительная блокировка автомасштабирования
            Chart.YAxes.First().MinLimit = 0;
            Chart.YAxes.First().MaxLimit = 100;

            // Фикс неправильной темы
            var theme = ActualTheme == ElementTheme.Dark ||
               (ActualTheme == ElementTheme.Default &&
                Application.Current.RequestedTheme == ApplicationTheme.Dark)
                    ? LvcThemeKind.Dark
                    : LvcThemeKind.Light;

            LiveCharts.Configure(config =>
            {
                config.AddDefaultTheme(requestedTheme: theme);
            });

            if (_cpu == null) 
            {
                return; 
            }

            Info_CpuName.Text = _cpu.systemInfo.CpuName;
            Info_CpuCores.Text = _cpu.info.topology.cores + "C/" +
                                 _cpu.info.topology.logicalCores + "T";
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex);
        }
    }


    #region JSON and Initialization

    private void LoadProfiles()
    {
        ProfileLoad();
        Preset_Custom.Children.Clear();
        foreach (var profile in _profile)
        {
            var isChecked = AppSettings.Preset != -1 &&
                            _profile[AppSettings.Preset].Profilename == profile.Profilename &&
                            _profile[AppSettings.Preset].Profiledesc == profile.Profiledesc &&
                            _profile[AppSettings.Preset].Profileicon == profile.Profileicon;

            var toggleButton = new ToggleButton
            {
                Margin = new Thickness(0, 7, 0, 0),
                CornerRadius = new CornerRadius(16),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Shadow = new ThemeShadow(),
                Translation = new System.Numerics.Vector3(0, 0, 20),
                IsChecked = isChecked,
                Content = new Grid
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(-5, -5, -5, -5),
                    Children =
                    {
                        new FontIcon
                        {
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Margin = new Thickness(7, 0, 0, 0),
                            Glyph = profile.Profileicon == string.Empty ? "\uE718" : profile.Profileicon
                        },
                        new StackPanel
                        {
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Orientation = Orientation.Vertical,
                            Margin = new Thickness(36, 5, 5, 5),
                            Children =
                            {
                                new TextBlock
                                {
                                    FontWeight = new FontWeight(700),
                                    Text = profile.Profilename
                                },
                                new TextBlock
                                {
                                    TextWrapping = TextWrapping.Wrap,
                                    Text = profile.Profiledesc,
                                    Visibility = profile.Profiledesc == string.Empty
                                        ? Visibility.Collapsed
                                        : Visibility.Visible
                                }
                            }
                        }
                    }
                }
            };
            toggleButton.Checked += ToggleButton_Checked;
            toggleButton.Unchecked += ToggleButton_Unchecked;
            toggleButton.Unloaded += (_, _) =>
            {
                toggleButton.Checked -= ToggleButton_Checked;
                toggleButton.Unchecked -= ToggleButton_Unchecked;
            };
            Preset_Custom.Children.Add(toggleButton);
        }

        if (AppSettings.Preset == -1)
        {
            Preset_Pivot.SelectedIndex = 1;
            Preset_Min.IsChecked = AppSettings.PremadeMinActivated;
            Preset_Eco.IsChecked = AppSettings.PremadeEcoActivated;
            Preset_Balance.IsChecked = AppSettings.PremadeBalanceActivated;
            Preset_Speed.IsChecked = AppSettings.PremadeSpeedActivated;
            Preset_Max.IsChecked = AppSettings.PremadeMaxActivated;
        }
    }

    #region JSON

    private static void ProfileLoad()
    {
        try
        {
            _profile = JsonConvert.DeserializeObject<Profile[]>(File.ReadAllText(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\profile.json"))!;
        }
        catch (Exception ex)
        {
            JsonRepair('p');
            LogHelper.LogWarn(ex.ToString());
        }
    }

    private static void JsonRepair(char file)
    {
        switch (file)
        {
            case 'p':
                _profile = [];
                try
                {
                    Directory.CreateDirectory(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(
                        Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\profile.json",
                        JsonConvert.SerializeObject(_profile));
                }
                catch
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                                @"\SakuOverclock\profile.json");
                    Directory.CreateDirectory(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(
                        Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\profile.json",
                        JsonConvert.SerializeObject(_profile));
                    App.MainWindow.Close();
                }

                break;
        }
    }

    #endregion

    #endregion

    #region Updater

    private void OnDataUpdated(object? sender, SensorsInformation info)
    {
        // Кэшируем максимальную частоту
        if (_maxCpuFreq < info.CpuFrequency)
        {
            _maxCpuFreq = info.CpuFrequency;
        }

        // Используем TryEnqueue с приоритетом Low для некритичных обновлений
        DispatcherQueue.TryEnqueue(() =>
        {
            // Не обновляем UI если окно скрыто/минимизировано
            if (!_isWindowVisible || !App.MainWindow.Visible)
            {
                return;
            }

            try
            {
                UpdateMainIndicators(info);
                UpdateAdditionalInfo(info);
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex);
            }
        });
    }

    private void UpdateMainIndicators(SensorsInformation info)
    {
        Indicators_Temp.Text = $"{Math.Round(info.CpuTempValue, 1):F1}C";
        Indicators_Busy.Text = $"{Math.Round(info.CpuUsage, 0):F0}%";
        Indicators_Freq.Text = $"{Math.Round(info.CpuFrequency, 1):F1} {_ghzInfo}";

        Indicators_Busy_Ring.Value = info.CpuUsage;
        Indicators_Freq_Ring.Value = info.CpuFrequency / _maxCpuFreq * 100;

        UpdateChartPointPosition((int)info.CpuTempValue);

        Indicators_Fast.Text = info.CpuFastValue == 0
            ? _powerSumDisabled
            : $"{Math.Round(info.CpuFastValue, 1):F1}W";

        Indicators_VrmEdc.Text = $"{Math.Round(info.VrmEdcValue, 1):F1}A";

        // Защита от деления на ноль
        Indicators_Fast_Ring.Value = info.CpuFastLimit > 0
            ? info.CpuFastValue / info.CpuFastLimit * 100
            : 0;
        Indicators_VrmEdc_Ring.Value = info.VrmEdcLimit > 0
            ? info.VrmEdcValue / info.VrmEdcLimit * 100
            : 0;

        UpdateBatteryInfo(info);
        UpdateRamInfo(info);
    }

    private void UpdateBatteryInfo(SensorsInformation info)
    {
        if (info.BatteryUnavailable)
        {
            if (Indicators_BatteryPercent.Text != "N/A")
            {
                Indicators_BatteryPercent.Text = "N/A";
                Indicators_BatteryPercent_Ring.Value = 0;
            }
        }
        else
        {
            Indicators_BatteryPercent.Text = info.BatteryPercent;

            // Безопасный парсинг процентов
            if (int.TryParse(info.BatteryPercent?.Replace("%", string.Empty), out var percent))
            {
                Indicators_BatteryPercent_Ring.Value = percent;
            }
        }
    }

    private void UpdateRamInfo(SensorsInformation info)
    {
        Indicators_Ram.Text = info.RamBusy;
        Indicators_Ram_Ring.Value = info.RamUsagePercent;
    }

    private void UpdateAdditionalInfo(SensorsInformation info)
    {
        var batteryTime = info.BatteryUnavailable ? string.Empty :
            (info.BatteryLifeTime < 0 ? _fromWall :
            GetSystemInfo.ConvertBatteryLifeTime(info.BatteryLifeTime));

        switch (_currentMode)
        {
            case 2:
                Main_AdditionalInfo1Name.Text = Indicators_Temp.Text;
                Main_AdditionalInfo2Name.Text = $"{Math.Round(100 - info.CpuTempValue, 1):F1}C";
                Main_AdditionalInfo3Name.Text = info.ApuTempValue == 0
                    ? _batteryUnavailable
                    : $"{Math.Round(info.ApuTempValue, 1):F1}C";
                break;

            case 3:
                Main_AdditionalInfo1Name.Text = Indicators_Busy.Text;
                break;

            case 4:
                Main_AdditionalInfo1Name.Text = Indicators_Freq.Text;
                Main_AdditionalInfo2Name.Text = $"{Math.Round(_maxCpuFreq, 1):F1} {_ghzInfo}";
                Main_AdditionalInfo3Name.Text = info.CpuVoltage == 0
                    ? _batteryUnavailable
                    : $"{Math.Round(info.CpuVoltage, 1):F1}V";
                break;

            case 5:
                Main_AdditionalInfo1Name.Text = $"{info.RamUsagePercent}%";
                Main_AdditionalInfo2Name.Text = Indicators_Ram.Text;
                Main_AdditionalInfo3Name.Text = info.RamTotal;
                break;

            case 6:
                Main_AdditionalInfo1Name.Text = Indicators_Fast.Text;
                Main_AdditionalInfo2Name.Text = info.CpuStapmValue == 0
                    ? _powerSumDisabled
                    : $"{Math.Round(info.CpuStapmValue, 1):F1}W";
                Main_AdditionalInfo3Name.Text = info.CpuSlowValue == 0
                    ? _powerSumDisabled
                    : $"{Math.Round(info.CpuSlowValue, 1):F1}W";
                break;

            case 7:
                Main_AdditionalInfo1Name.Text = info.VrmTdcValue == 0
                    ? _batteryUnavailable
                    : $"{Math.Round(info.VrmTdcValue, 1):F1}A";
                Main_AdditionalInfo2Name.Text = info.VrmEdcValue == 0
                    ? _batteryUnavailable
                    : $"{Math.Round(info.VrmEdcValue, 1):F1}A";
                Main_AdditionalInfo3Name.Text = info.SocEdcValue == 0
                    ? _batteryUnavailable
                    : $"{Math.Round(info.SocEdcValue, 1):F1}A";
                break;

            case 8:
                Main_AdditionalInfo1Name.Text = info.BatteryUnavailable ? _batteryUnavailable : info.BatteryHealth;
                Main_AdditionalInfo2Name.Text = info.BatteryUnavailable ? _batteryUnavailable : info.BatteryCycles;
                Main_AdditionalInfo3Name.Text = info.BatteryUnavailable ? _batteryUnavailable : batteryTime;
                break;
        }
    }

    public ObservableCollection<int> Values { get; set; } = [];

    public object Sync { get; } = new object();

    /// <summary>
    /// Обновление положения точки температуры на графике
    /// </summary>
    private void UpdateChartPointPosition(int temperature)
    {
        // Ограничиваем значение в диапазоне 0-100
        temperature = Math.Clamp(temperature, 0, 100);

        lock (Sync)
        {
            if (_isFirstLoad)
            {
                // При первой загрузке заполняем весь график одинаковыми значениями
                Values.Clear();
                for (var i = 0; i < 10; i++)
                {
                    Values.Add(temperature);
                }
                _isFirstLoad = false;
            }
            else
            {
                // Обычное обновление
                Values.Add(temperature);
                if (Values.Count > 10)
                {
                    Values.RemoveAt(0);
                }
            }
        }
    }

    #endregion

    #region Event Handlers
    private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var isCompact = e.NewSize.Height < 400;
        var (main, frequent) = isCompact ? CompactGridMargins : NormalGridMargins;


        if (MainGrid.Margin != main)
        {
            MainGrid.Margin = main;
        }

        if (FriquentlyUsedGrid.Margin != frequent)
        {
            FriquentlyUsedGrid.Margin = frequent;
        }

        DeviceInfoSign.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        FrequentlyUsedSign.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
    }
    private static readonly (Thickness Main, Thickness Frequent) CompactGridMargins =
    (
        new Thickness( 00, -20, 0, 0),
        new Thickness(-10,  8, 0, 3)
    );
    private static readonly (Thickness Main, Thickness Frequent) NormalGridMargins =
    (
        new Thickness( 00, 20, 0, 0),
        new Thickness(-10, 05, 0, 3)
    );

    private void PresetsPage_Click(object sender, RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ПресетыViewModel).FullName!);
    }

    private void OverclockPage_Click(object sender, RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ПараметрыViewModel).FullName!);
    }

    private void SettingsPage_Click(object sender, RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(SettingsViewModel).FullName!);
    }

    private void FaqLink_Click(object sender, RoutedEventArgs e) => Process.Start(
        new ProcessStartInfo("https://github.com/Erruar/Saku-Overclock/wiki/FAQ") { UseShellExecute = true });

    private void PivotProfiles_Loaded(object sender, RoutedEventArgs e)
    {
        var pivot = sender as Pivot;
        var headers = VisualTreeHelper.FindVisualChildren<ContentPresenter>(pivot!);
        foreach (var header in headers)
        {
            var contentPresenters = VisualTreeHelper.FindVisualChildren<PivotHeaderPanel>(header);
            foreach (var content in contentPresenters)
            {
                var headerItems = VisualTreeHelper.FindVisualChildren<PivotHeaderItem>(header);
                foreach (var item in headerItems)
                {
                    item.Visibility = Visibility.Collapsed;
                }
                content.Visibility = Visibility.Collapsed;
                content.Margin = new Thickness(0,-20,0,0); // Сместит контент на несколько пикселей
            }
        }
    }

    private void ToggleButton_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_waitForCheck)
        {
            return;
        }

        ((ToggleButton)sender).IsChecked = true;
    }

    private async void ToggleButton_Checked(object sender, RoutedEventArgs e)
    {
        try
        {
            _waitForCheck = true;
            var toggleButtons = VisualTreeHelper.FindVisualChildren<ToggleButton>(Preset_Pivot);
            foreach (var button in toggleButtons)
            {
                if (button != sender as ToggleButton)
                {
                    button.IsChecked = false;
                }
            }
            var name = string.Empty;
            var desc = string.Empty;
            var icon = string.Empty;
            var textBlocks = VisualTreeHelper.FindVisualChildren<TextBlock>((sender as ToggleButton)!);
            foreach (var block in textBlocks)
            {
                if (block.FontWeight == new FontWeight(700))
                {
                    name = block.Text;
                }
                else
                {
                    if (block.TextWrapping == TextWrapping.Wrap)
                    {
                        desc = block.Text;
                    }
                }
            }
            if (_doubleClickApplyPrev == name + desc)
            {
                Apply_Click(null, null);
            }
            _doubleClickApplyPrev = name + desc;

            await Task.Delay(20);
            _waitForCheck = false;
        }
        catch (Exception ex)
        {
            await LogHelper.LogError(ex);
        }
    } 

    private async void Apply_Click(object? sender, RoutedEventArgs? e)
    {
        // Анимация запускается всегда
        ButtonAnimationStoryboard.Begin();

        var toggleButtons = VisualTreeHelper.FindVisualChildren<ToggleButton>(Preset_Pivot);
        foreach (var button in toggleButtons)
        {
            if (button.IsChecked == true)
            {
                if (button.Tag != null && ((string)button.Tag).Contains("Preset_"))
                {
                    var presetValue = -1;
                    var endMode = "Balance";
                    switch (button.Tag)
                    {
                        case "Preset_Min":
                            endMode = "Min";
                            break;
                        case "Preset_Eco":
                            endMode = "Eco";
                            break;
                        case "Preset_Balance":
                            endMode = "Balance";
                            break;
                        case "Preset_Speed":
                            endMode = "Speed";
                            break;
                        case "Preset_Max":
                            endMode = "Max";
                            break;
                    }

                    // Проверяем, изменился ли пресет
                    if (_lastAppliedPreset != presetValue || _lastAppliedProfileName != endMode)
                    {
                        _lastAppliedPreset = presetValue;
                        _lastAppliedProfileName = endMode;

                        AppSettings.Preset = -1;
                        ShellPage.SelectPremadePreset(endMode);

                        var (_, _, _, settings, _) = ShellPage.PremadedPresets[endMode];

                        AppSettings.RyzenAdjLine = settings;
                        AppSettings.SaveSettings();

                        await _applyer.ApplyWithoutAdjLine(false);

                        NotificationsService.Notifies ??= [];
                        NotificationsService.Notifies.Add(new Notify
                        {
                            Title = "Profile_APPLIED",
                            Msg = "DEBUG MESSAGE",
                            Type = InfoBarSeverity.Informational
                        });
                        NotificationsService.SaveNotificationsSettings();

                        Apply_Teach.Target = ApplyButton;
                        Apply_Teach.Title = "Apply_Success".GetLocalized();
                        Apply_Teach.Subtitle = "";
                        Apply_Teach.IconSource = new SymbolIconSource { Symbol = Symbol.Accept };
                        Apply_Teach.IsOpen = true;
                        await LogHelper.Log("Apply_Success".GetLocalized());
                        await Task.Delay(3000);
                        Apply_Teach.IsOpen = false;
                    }
                }
                else
                {
                    var name = string.Empty;
                    var desc = string.Empty;
                    var icon = string.Empty;
                    var textBlocks = VisualTreeHelper.FindVisualChildren<TextBlock>(button);
                    foreach (var block in textBlocks)
                    {
                        if (block.FontWeight == new FontWeight(700))
                        {
                            name = block.Text;
                        }
                        else
                        {
                            if (block.TextWrapping == TextWrapping.Wrap)
                            {
                                desc = block.Text;
                            }
                        }
                    }

                    var glyphs = VisualTreeHelper.FindVisualChildren<FontIcon>(button);
                    foreach (var glyph in glyphs)
                    {
                        icon = glyph.Glyph;
                    }

                    for (var i = 0; i < _profile.Length; i++)
                    {
                        var profile = _profile[i];
                        if (profile.Profilename == name &&
                            profile.Profiledesc == desc &&
                            (profile.Profileicon == icon ||
                             profile.Profileicon == "\uE718"))
                        {
                            // Проверяем, изменился ли профиль
                            if (_lastAppliedPreset != i || _lastAppliedProfileName != name)
                            {
                                _lastAppliedPreset = i;
                                _lastAppliedProfileName = name;

                                AppSettings.Preset = i;
                                AppSettings.SaveSettings();

                                ПараметрыPage.ApplyInfo = string.Empty;
                                await _applyer.ApplyCustomPreset(profile, true);

                                NotificationsService.Notifies ??= [];
                                NotificationsService.Notifies.Add(new Notify
                                {
                                    Title = "Profile_APPLIED",
                                    Msg = "DEBUG MESSAGE",
                                    Type = InfoBarSeverity.Informational
                                });
                                NotificationsService.SaveNotificationsSettings();

                                await Task.Delay(1000);
                                var timer = 1000;
                                if (ПараметрыPage.ApplyInfo != string.Empty)
                                {
                                    timer *= ПараметрыPage.ApplyInfo.Split('\n').Length + 1;
                                }

                                Apply_Teach.Target = ApplyButton;
                                Apply_Teach.Title = "Apply_Success".GetLocalized();
                                Apply_Teach.Subtitle = "";
                                Apply_Teach.IconSource = new SymbolIconSource { Symbol = Symbol.Accept };
                                Apply_Teach.IsOpen = true;
                                var infoSet = InfoBarSeverity.Success;
                                if (ПараметрыPage.ApplyInfo != string.Empty)
                                {
                                    await LogHelper.Log(ПараметрыPage.ApplyInfo);
                                    Apply_Teach.Title = "Apply_Warn".GetLocalized();
                                    Apply_Teach.Subtitle = "Apply_Warn_Desc".GetLocalized() + ПараметрыPage.ApplyInfo;
                                    Apply_Teach.IconSource = new SymbolIconSource { Symbol = Symbol.ReportHacked };
                                    await Task.Delay(timer);
                                    Apply_Teach.IsOpen = false;
                                    infoSet = InfoBarSeverity.Warning;
                                }
                                else
                                {
                                    await LogHelper.Log("Apply_Success".GetLocalized());
                                    await Task.Delay(3000);
                                    Apply_Teach.IsOpen = false;
                                }

                                NotificationsService.Notifies ??= [];
                                NotificationsService.Notifies.Add(new Notify
                                {
                                    Title = Apply_Teach.Title,
                                    Msg = Apply_Teach.Subtitle + (ПараметрыPage.ApplyInfo != string.Empty ? "DELETEUNAVAILABLE" : ""),
                                    Type = infoSet
                                });
                                NotificationsService.SaveNotificationsSettings();
                            }
                            break;
                        }
                    }
                }
            }
        }
    }

    private void SwitchPivot_Click(object sender, RoutedEventArgs e) => 
        Preset_Pivot.SelectedIndex = Preset_Pivot.SelectedIndex == 1 ? 0 : 1;

    private void Preset_Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e) => 
        PresetsSign.Text = Preset_Pivot.SelectedIndex == 0 ? 
        "Main_OwnProfiles/Text".GetLocalized() : 
        "Main_PremadeProfiles/Text".GetLocalized();

    #region Mouse Events & Blocks behavior

    private async void SetPlacerTipAsync(int currMode)
    {
        try
        {
            var currentVersion = ++_tipVersion;
            _currentMode = currMode;

            // Ждём завершения предыдущей анимации
            while (_isAnimating)
            {
                await Task.Delay(50);
                if (currentVersion != _tipVersion) return;
            }

            if (Main_Teach.IsOpen)
            {
                _isAnimating = true;
                Main_Teach.IsOpen = false;
                await Task.Delay(300); // Время анимации закрытия
                _isAnimating = false;
            }

            if (currentVersion != _tipVersion) return;

            SetPlacerContent(currMode);

            if (currentVersion != _tipVersion) return;

            _isAnimating = true;
            Main_Teach.IsOpen = true;
            await Task.Delay(300); // Время анимации открытия
            _isAnimating = false;
        }
        catch (Exception ex)
        {
            await LogHelper.LogError(ex);
        }
    }

    private void SetPlacerContent(int currMode)
    {
        switch (currMode)
        {
            case 1:
                Main_Teach.Target = LogoPlacer;
                Main_AdditionalInfoDesc.Text = "Main_DeviceInfo1".GetLocalized();
                Main_AdditionalInfo1Desc.Text = "infoMModel/Text".GetLocalized() + ":";
                Main_AdditionalInfo2Desc.Text = "infoMProd/Text".GetLocalized() + ":";
                Main_AdditionalInfo3Desc.Text = "BIOS:";
                try
                {
                    if (_cpu != null)
                    {
                        Main_AdditionalInfo1Name.Text = _cpu.systemInfo.MbName;
                        Main_AdditionalInfo2Name.Text = _cpu.systemInfo.MbVendor;
                        Main_AdditionalInfo3Name.Text = _cpu.systemInfo.BiosVersion;
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.LogError(ex);
                }
                break;
            case 2:
                Main_Teach.Target = TemperaturePlacer;
                Main_AdditionalInfoDesc.Text = "Main_TemperatureSensors".GetLocalized();
                Main_AdditionalInfo1Desc.Text = "Main_CpuTemp1".GetLocalized();
                Main_AdditionalInfo2Desc.Text = "Main_CpuTJMaxDistance".GetLocalized();
                Main_AdditionalInfo3Desc.Text = "Main_GpuTemp".GetLocalized();
                break;
            case 3:
                Main_Teach.Target = UsabilityPlacer;
                Main_AdditionalInfoDesc.Text = "Main_CpuUsage".GetLocalized();
                Main_AdditionalInfo1Desc.Text = "Main_CpuUtilization".GetLocalized();
                Main_AdditionalInfo2Desc.Text = "Main_CpuCoreCount".GetLocalized();
                Main_AdditionalInfo3Desc.Text = "SMT:";
                try
                {
                    if (_cpu != null)
                    {
                        Main_AdditionalInfo2Name.Text = _cpu.info.topology.cores.ToString();
                        Main_AdditionalInfo3Name.Text = _cpu.systemInfo.SMT.ToString()
                            .Replace("True", "Cooler_Service_Enabled/Content".GetLocalized())
                            .Replace("False", "Cooler_Service_Disabled/Content".GetLocalized()); // Просто перевод, не для настроек кулера
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.LogError(ex);
                }
                break;
            case 4:
                Main_Teach.Target = FrequencyPlacer;
                Main_AdditionalInfoDesc.Text = "Main_CpuFrequency".GetLocalized();
                Main_AdditionalInfo1Desc.Text = "Main_AverageFrequency".GetLocalized();
                Main_AdditionalInfo2Desc.Text = "Main_MaxFrequency".GetLocalized();
                Main_AdditionalInfo3Desc.Text = "Main_AverageVoltage".GetLocalized();
                break;
            case 5:
                Main_Teach.Target = RamPlacer;
                Main_AdditionalInfoDesc.Text = "Info_RAM_text/Text".GetLocalized();
                Main_AdditionalInfo1Desc.Text = "Main_RamUtilization".GetLocalized();
                Main_AdditionalInfo2Desc.Text = "Main_RamUsage1".GetLocalized();
                Main_AdditionalInfo3Desc.Text = "Main_RamSize".GetLocalized();
                break;
            case 6:
                Main_Teach.Target = PowerPlacer;
                Main_AdditionalInfoDesc.Text = "Main_SystemPowers".GetLocalized();
                Main_AdditionalInfo1Desc.Text = "Param_CPU_c3/Text".GetLocalized() + ":"; // Реальная мощность 
                Main_AdditionalInfo2Desc.Text = "STAPM:";
                Main_AdditionalInfo3Desc.Text = "Param_CPU_c4/Text".GetLocalized() + ":"; // Средняя мощность
                break;
            case 7:
                Main_Teach.Target = VrmPlacer;
                Main_AdditionalInfoDesc.Text = "Main_VrmInfo".GetLocalized();
                Main_AdditionalInfo1Desc.Text = "VRM TDC:";
                Main_AdditionalInfo2Desc.Text = "VRM EDC:";
                Main_AdditionalInfo3Desc.Text = "SoC EDC:";
                break;
            case 8:
                Main_Teach.Target = BatteryPlacer;
                Main_AdditionalInfoDesc.Text = "Main_BatteryInfo".GetLocalized();
                Main_AdditionalInfo1Desc.Text = "infoABATWear/Text".GetLocalized() + ":";
                Main_AdditionalInfo2Desc.Text = "infoABATCycles/Text".GetLocalized() + ":";
                Main_AdditionalInfo3Desc.Text = "infoABATRemainTime/Text".GetLocalized() + ":";
                break;
        }
    }

    private void TooltipPlacer_PointerPressed(object sender, PointerRoutedEventArgs e) =>
        SetPlacerTipAsync(int.Parse((string)((FrameworkElement)sender).Tag));

    #endregion

    #endregion

    private bool _isExpanded;
    private void ExpandButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isExpanded)
        {
            CollapseStoryboard.Begin();
        }
        else
        {
            ExpandStoryboard.Begin();
        }

        _isExpanded = !_isExpanded;
    }
}