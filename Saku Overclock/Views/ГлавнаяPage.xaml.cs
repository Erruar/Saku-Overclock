using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;
using Windows.UI.Text;
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
using Saku_Overclock.SmuEngine;
using Saku_Overclock.ViewModels;
using ZenStates.Core;
using VisualTreeHelper = Saku_Overclock.Helpers.VisualTreeHelper;

namespace Saku_Overclock.Views;

public sealed partial class ГлавнаяPage
{
    private readonly IBackgroundDataUpdater? _dataUpdater; // Обновление данных сенсоров системы

    private static readonly IAppSettingsService
        AppSettings = App.GetService<IAppSettingsService>(); // Настройки приложения

    private static readonly IApplyerService Applyer = App.GetService<IApplyerService>(); // Применения пресетов

    private static readonly IAppNotificationService
        NotificationsService = App.GetService<IAppNotificationService>(); // Уведомления приложения

    private static Profile[] _profile = new Profile[1]; // Кастомные пресеты (всегда по умолчанию будет 1 профиль)

    private readonly Cpu?
        _cpu; // Ядро приложения, здесь используется для получения информации о названии процессора, ядрах, SMT и пр.

    private int
        _currentMode; // Хранит режим, который выбрал пользователь, то, где стоял курсор при нажатии на блок, чтобы показать тултип с дополнительной информацией о просматриемом блоке

    private bool
        _userSwitchPreset; // Временно выключает отмену выделения ToggleButton пресета, для его смены пользователем

    private int _tipVersion; // Версия TeachingTip, проверка открытия нужного TeachingTip, при быстром нажатии
    private bool _isAnimating; // Идёт ли сейчас анимация TeachingTip

    private string
        _doubleClickApplyPrev =
            string.Empty; // Проверка на имя прошлого пресета, если совпадает, то при двойном клике по пресету он применится

    private int
        _lastAppliedPreset = -2; // Начальное значение последнего применённого пресета, которое точно не совпадёт

    private string
        _lastAppliedProfileName =
            string.Empty; // Защита от переприменения пресета при многократном нажатии на кнопку применить

    private double
        _maxCpuFreq =
            1d; // Кешируемая максимальная частота процессора, используется в индикаторах, чтобы показать процент от максимальной частоты

    private bool _isTemperatureChartFirstLoad = true; // Показатель первой загрузки графика температуры
    private bool _isWindowVisible = true; // Показатель видимости окна
    private bool _isHelpButtonsExpanded; // Показатели отображения кнопок помощи возле блока "Не видите свой пресет?"

    private readonly string
        _batteryUnavailable = "Main_BatteryUnavailable".GetLocalized(); // Кешированные строки перевода

    private readonly string _ghzInfo = "infoAGHZ".GetLocalized();
    private readonly string _powerSumDisabled = "Info_PowerSumInfo_Disabled".GetLocalized();
    private readonly string _fromWall = "InfoBatteryAC".GetLocalized();

    public ГлавнаяPage()
    {
        App.GetService<ГлавнаяViewModel>();
        InitializeComponent();
        _dataUpdater = App.BackgroundUpdater;
        if (_dataUpdater != null)
        {
            _dataUpdater.DataUpdated += OnDataUpdated;
        }

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

    #region Page Initialization

    #region Page Related

    /// <summary>
    ///     Обработчик загрузки интерфейса приложения
    /// </summary>
    private void ГлавнаяPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            LoadProfilesToPivot();

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

            InfoCpuName.Text = _cpu.systemInfo.CpuName;
            InfoCpuCores.Text = _cpu.info.topology.cores + "C/" +
                                 _cpu.info.topology.logicalCores + "T";
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex);
        }
    }

    /// <summary>
    ///     Обработчик изменения состояния окна
    /// </summary>
    private void OnVisibilityChanged(object? s, WindowState e)
        => _isWindowVisible = App.MainWindow.WindowState != WindowState.Minimized;

    /// <summary>
    ///     Обработчик выгрузки страницы
    /// </summary>
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

    #endregion

    #region JSON and Initialization

    /// <summary>
    ///     Загружает все пресеты и выделяет активный пресет
    /// </summary>
    private void LoadProfilesToPivot()
    {
        ProfileLoad();
        PresetCustom.Children.Clear();
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
                Translation = new Vector3(0, 0, 20),
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
            PresetCustom.Children.Add(toggleButton);
        }

        if (AppSettings.Preset == -1)
        {
            PresetPivot.SelectedIndex = 1;
            PresetMin.IsChecked = AppSettings.PremadeMinActivated;
            PresetEco.IsChecked = AppSettings.PremadeEcoActivated;
            PresetBalance.IsChecked = AppSettings.PremadeBalanceActivated;
            PresetSpeed.IsChecked = AppSettings.PremadeSpeedActivated;
            PresetMax.IsChecked = AppSettings.PremadeMaxActivated;
        }
    }

    #region JSON

    /// <summary>
    ///     Загружает кастомные пресеты
    /// </summary>
    private static void ProfileLoad()
    {
        try
        {
            _profile = JsonConvert.DeserializeObject<Profile[]>(File.ReadAllText(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\profile.json"))!;
        }
        catch (Exception ex)
        {
            LogHelper.LogWarn(ex);
            ProfileJsonRepair();
        }
    }

    /// <summary>
    ///     Чинит файл кастомных пресетов (при необходимости)
    /// </summary>
    private static void ProfileJsonRepair()
    {
        _profile = new Profile[1];
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
        }
    }

    #endregion

    #endregion

    #endregion

    #region Updater

    /// <summary>
    ///     Обновляет показатели сенсоров системы в реальном времени
    /// </summary>
    private void OnDataUpdated(object? sender, SensorsInformation info)
    {
        // Кэшируем максимальную частоту
        if (_maxCpuFreq < info.CpuFrequency)
        {
            _maxCpuFreq = info.CpuFrequency;
        }

        // Обновляем UI только в UI потоке!
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

    /// <summary>
    ///     Обновляет состояние показателей системы
    /// </summary>
    private void UpdateMainIndicators(SensorsInformation info)
    {
        IndicatorsTemp.Text = $"{Math.Round(info.CpuTempValue, 1):F1}C";
        IndicatorsBusy.Text = $"{Math.Round(info.CpuUsage, 0):F0}%";
        IndicatorsFreq.Text = $"{Math.Round(info.CpuFrequency, 1):F1} {_ghzInfo}";

        IndicatorsBusyRing.Value = info.CpuUsage;
        IndicatorsFreqRing.Value = info.CpuFrequency / _maxCpuFreq * 100;

        UpdateTemperatureChartPointPosition((int)info.CpuTempValue);

        IndicatorsFast.Text = info.CpuFastValue == 0
            ? _powerSumDisabled
            : $"{Math.Round(info.CpuFastValue, 1):F1}W";

        IndicatorsVrmEdc.Text = $"{Math.Round(info.VrmEdcValue, 1):F1}A";

        // Защита от деления на ноль
        IndicatorsFastRing.Value = info.CpuFastLimit > 0
            ? info.CpuFastValue / info.CpuFastLimit * 100
            : 0;
        IndicatorsVrmEdcRing.Value = info.VrmEdcLimit > 0
            ? info.VrmEdcValue / info.VrmEdcLimit * 100
            : 0;

        IndicatorsRam.Text = info.RamBusy;
        IndicatorsRamRing.Value = info.RamUsagePercent;

        UpdateBatteryInfo(info);
    }

    /// <summary>
    ///     Обновляет состояние показателей батареи
    /// </summary>
    private void UpdateBatteryInfo(SensorsInformation info)
    {
        if (info.BatteryUnavailable)
        {
            if (IndicatorsBatteryPercent.Text != "N/A")
            {
                IndicatorsBatteryPercent.Text = "N/A";
                IndicatorsBatteryPercentRing.Value = 0;
            }
        }
        else
        {
            IndicatorsBatteryPercent.Text = info.BatteryPercent;
            if (info.BatteryState is > 5 and < 10 or 11 or > 1 and < 4)
            {
                if (IndicatorsBatteryPercentIcon.Glyph != "\uE83E")
                {
                    IndicatorsBatteryPercentIcon.Glyph = "\uE83E";
                }
            }
            else
            {
                if (IndicatorsBatteryPercentIcon.Glyph != "\uE83F")
                {
                    IndicatorsBatteryPercentIcon.Glyph = "\uE83F";
                }
            }
            // Безопасный парсинг процентов
            if (int.TryParse(info.BatteryPercent?.Replace("%", string.Empty), out var percent))
            {
                IndicatorsBatteryPercentRing.Value = percent;
            }
        }
    }

    /// <summary>
    ///     Обновляет значения сенсоров в открытом TeachingTip
    /// </summary>
    private void UpdateAdditionalInfo(SensorsInformation info)
    {
        switch (_currentMode)
        {
            case 2:
                MainAdditionalInfo1Name.Text = IndicatorsTemp.Text;
                MainAdditionalInfo2Name.Text = $"{Math.Round(100 - info.CpuTempValue, 1):F1}C";
                MainAdditionalInfo3Name.Text = info.ApuTempValue == 0
                    ? _batteryUnavailable
                    : $"{Math.Round(info.ApuTempValue, 1):F1}C";
                break;

            case 3:
                MainAdditionalInfo1Name.Text = IndicatorsBusy.Text;
                break;

            case 4:
                MainAdditionalInfo1Name.Text = IndicatorsFreq.Text;
                MainAdditionalInfo2Name.Text = $"{Math.Round(_maxCpuFreq, 1):F1} {_ghzInfo}";
                MainAdditionalInfo3Name.Text = info.CpuVoltage == 0
                    ? _batteryUnavailable
                    : $"{Math.Round(info.CpuVoltage, 1):F1}V";
                break;

            case 5:
                MainAdditionalInfo1Name.Text = $"{info.RamUsagePercent}%";
                MainAdditionalInfo2Name.Text = IndicatorsRam.Text;
                MainAdditionalInfo3Name.Text = info.RamTotal;
                break;

            case 6:
                MainAdditionalInfo1Name.Text = IndicatorsFast.Text;
                MainAdditionalInfo2Name.Text = info.CpuStapmValue == 0
                    ? _powerSumDisabled
                    : $"{Math.Round(info.CpuStapmValue, 1):F1}W";
                MainAdditionalInfo3Name.Text = info.CpuSlowValue == 0
                    ? _powerSumDisabled
                    : $"{Math.Round(info.CpuSlowValue, 1):F1}W";
                break;

            case 7:
                MainAdditionalInfo1Name.Text = info.VrmTdcValue == 0
                    ? _batteryUnavailable
                    : $"{Math.Round(info.VrmTdcValue, 1):F1}A";
                MainAdditionalInfo2Name.Text = info.VrmEdcValue == 0
                    ? _batteryUnavailable
                    : $"{Math.Round(info.VrmEdcValue, 1):F1}A";
                MainAdditionalInfo3Name.Text = info.SocEdcValue == 0
                    ? _batteryUnavailable
                    : $"{Math.Round(info.SocEdcValue, 1):F1}A";
                break;

            case 8:
                MainAdditionalInfo1Name.Text = info.BatteryUnavailable ? _batteryUnavailable : info.BatteryHealth;
                MainAdditionalInfo2Name.Text = info.BatteryUnavailable ? _batteryUnavailable : info.BatteryCycles;
                MainAdditionalInfo3Name.Text = info.BatteryUnavailable ? _batteryUnavailable :
                    info.BatteryLifeTime < 0 ? _fromWall :
                    GetSystemInfo.ConvertBatteryLifeTime(info.BatteryLifeTime);
                break;
        }
    }

    /// <summary>
    ///     Точки графика температуры
    /// </summary>
    public ObservableCollection<int> Values
    {
        get;
        set;
    } = [];

    /// <summary>
    ///     lock-обьект для обновления точек графика температуры
    /// </summary>
    public object Sync
    {
        get;
    } = new();

    /// <summary>
    ///     Обновление положения точки температуры на графике
    /// </summary>
    private void UpdateTemperatureChartPointPosition(int temperature)
    {
        // Ограничиваем значение в диапазоне 0-100
        temperature = Math.Clamp(temperature, 0, 100);

        lock (Sync)
        {
            if (_isTemperatureChartFirstLoad)
            {
                // При первой загрузке заполняем весь график одинаковыми значениями,
                // чтобы график не казался непрогруженным
                Values.Clear();
                for (var i = 0; i < 10; i++)
                {
                    Values.Add(temperature);
                }

                _isTemperatureChartFirstLoad = false;
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

    /// <summary>
    ///     Изменяет отображение контента при небольшом размере окна
    /// </summary>
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
        new Thickness(00, -20, 0, 0),
        new Thickness(-10, 8, 0, 3)
    );

    private static readonly (Thickness Main, Thickness Frequent) NormalGridMargins =
    (
        new Thickness(00, 20, 0, 0),
        new Thickness(-10, 05, 0, 3)
    );

    /// <summary>
    ///     Открывает страницу управления пресетами
    /// </summary>
    private void PresetsPage_Click(object sender, RoutedEventArgs e)
    {
        if (_isHelpButtonsExpanded)
        {
            var navigationService = App.GetService<INavigationService>();
            navigationService.NavigateTo(typeof(ПресетыViewModel).FullName!);
        }
    }

    /// <summary>
    ///     Открывает страницу разгон
    /// </summary>
    private void OverclockPage_Click(object sender, RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ПараметрыViewModel).FullName!);
    }

    /// <summary>
    ///     Открывает страницу настроек приложения
    /// </summary>
    private void SettingsPage_Click(object sender, RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(SettingsViewModel).FullName!);
    }

    /// <summary>
    ///     Переводит пользователя на страницу FAQ
    /// </summary>
    private void FaqLink_Click(object sender, RoutedEventArgs e) => Process.Start(
        new ProcessStartInfo("https://github.com/Erruar/Saku-Overclock/wiki/FAQ") { UseShellExecute = true });

    /// <summary>
    ///     Скрывает панель навигации Pivot и смезает её вверх
    /// </summary>
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
                content.Margin = new Thickness(0, -20, 0, 0);
            }
        }
    }

    /// <summary>
    ///     Запрещает отмену выделения ToggleButton пресета
    /// </summary>
    private void ToggleButton_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_userSwitchPreset)
        {
            return;
        }

        ((ToggleButton)sender).IsChecked = true;
    }

    /// <summary>
    ///     Обработка выбора и активации пресета
    /// </summary>
    private async void ToggleButton_Checked(object sender, RoutedEventArgs e)
    {
        try
        {
            _userSwitchPreset = true;
            var toggleButtons = VisualTreeHelper.FindVisualChildren<ToggleButton>(PresetPivot);
            foreach (var button in toggleButtons)
            {
                if (button != sender as ToggleButton)
                {
                    button.IsChecked = false;
                }
            }

            var name = string.Empty;
            var desc = string.Empty;
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
            _userSwitchPreset = false;
        }
        catch (Exception ex)
        {
            await LogHelper.LogError(ex);
        }
    }

    /// <summary>
    ///     Обработка применения пресета
    /// </summary>
    private async void Apply_Click(object? sender, RoutedEventArgs? e)
    {
        try
        {
            // Анимация запускается всегда
            ButtonAnimationStoryboard.Begin();

            var toggleButtons = VisualTreeHelper.FindVisualChildren<ToggleButton>(PresetPivot);
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

                            await Applyer.ApplyWithoutAdjLine(false);

                            NotificationsService.Notifies ??= [];
                            NotificationsService.Notifies.Add(new Notify
                            {
                                Title = "Profile_APPLIED",
                                Msg = "DEBUG MESSAGE",
                                Type = InfoBarSeverity.Informational
                            });
                            NotificationsService.SaveNotificationsSettings();

                            ApplyTeach.Target = ApplyButton;
                            ApplyTeach.Title = "Apply_Success".GetLocalized();
                            ApplyTeach.Subtitle = "";
                            ApplyTeach.IconSource = new SymbolIconSource { Symbol = Symbol.Accept };
                            ApplyTeach.IsOpen = true;
                            await LogHelper.Log("Apply_Success".GetLocalized());
                            await Task.Delay(3000);
                            ApplyTeach.IsOpen = false;
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
                                    await Applyer.ApplyCustomPreset(profile, true);

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
                                    var applyInfo = ПараметрыPage.ApplyInfo;
                                    if (applyInfo != string.Empty)
                                    {
                                        timer *= applyInfo.Split('\n').Length + 1;
                                    }

                                    ApplyTeach.Target = ApplyButton;
                                    ApplyTeach.Title = "Apply_Success".GetLocalized();
                                    ApplyTeach.Subtitle = "";
                                    ApplyTeach.IconSource = new SymbolIconSource { Symbol = Symbol.Accept };
                                    ApplyTeach.IsOpen = true;
                                    var infoSet = InfoBarSeverity.Success;
                                    if (applyInfo != string.Empty)
                                    {
                                        await LogHelper.Log(applyInfo);
                                        ApplyTeach.Title = "Apply_Warn".GetLocalized();
                                        ApplyTeach.Subtitle = "Apply_Warn_Desc".GetLocalized() + applyInfo;
                                        ApplyTeach.IconSource = new SymbolIconSource { Symbol = Symbol.ReportHacked };
                                        await Task.Delay(timer);
                                        ApplyTeach.IsOpen = false;
                                        infoSet = InfoBarSeverity.Warning;
                                    }
                                    else
                                    {
                                        await LogHelper.Log("Apply_Success".GetLocalized());
                                        await Task.Delay(3000);
                                        ApplyTeach.IsOpen = false;
                                    }

                                    NotificationsService.Notifies ??= [];
                                    NotificationsService.Notifies.Add(new Notify
                                    {
                                        Title = ApplyTeach.Title,
                                        Msg = ApplyTeach.Subtitle +
                                              (applyInfo != string.Empty ? "DELETEUNAVAILABLE" : ""),
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
        catch (Exception ex)
        {
            await LogHelper.LogError(ex);
        }
    }

    /// <summary>
    ///     Переключает режим отображения Pivot (готовые и свои пресеты)
    /// </summary>
    private void SwitchPivot_Click(object sender, RoutedEventArgs e)
    {
        if (!_isHelpButtonsExpanded && (sender as Button)?.Tag?.ToString() == "FromHelpButtons")
        {
            return;
        }
        
        PresetPivot.SelectedIndex = PresetPivot.SelectedIndex == 1 ? 0 : 1;
    }

    /// <summary>
    ///     Переключает название возле Pivot (готовые и свои пресеты)
    /// </summary>
    private void Preset_Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        PresetsSign.Text = PresetPivot.SelectedIndex == 0
            ? "Main_OwnProfiles/Text".GetLocalized()
            : "Main_PremadeProfiles/Text".GetLocalized();

    #region Mouse Events & Blocks behavior

    private int _previousMode = -1;
    
    /// <summary>
    ///     Открывает выбранный пользователем TeachingTip, с защитой от быстрых нажатий
    /// </summary>
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
                if (currentVersion != _tipVersion)
                {
                    return;
                }
            }

            if (MainTeach.IsOpen)
            {
                _isAnimating = true;
                MainTeach.IsOpen = false;
                await Task.Delay(300); // Время анимации закрытия
                _isAnimating = false;
            }

            if (currentVersion != _tipVersion)
            {
                return;
            }

            SetPlacerContent(currMode);

            if (currentVersion != _tipVersion || _previousMode == currMode)
            {
                _previousMode = -1;
                return;
            }

            _isAnimating = true;
            MainTeach.IsOpen = true;
            await Task.Delay(300); // Время анимации открытия
            _isAnimating = false;
            
            _previousMode = currMode;
        }
        catch (Exception ex)
        {
            await LogHelper.LogError(ex);
        }
    }

    /// <summary>
    ///     Обновляет описание контента в TeachingTip
    /// </summary>
    private void SetPlacerContent(int currMode)
    {
        switch (currMode)
        {
            case 1:
                MainTeach.Target = LogoPlacer;
                MainAdditionalInfoDesc.Text = "Main_DeviceInfo1".GetLocalized();
                MainAdditionalInfo1Desc.Text = "infoMModel/Text".GetLocalized() + ":";
                MainAdditionalInfo2Desc.Text = "infoMProd/Text".GetLocalized() + ":";
                MainAdditionalInfo3Desc.Text = "BIOS:";
                try
                {
                    if (_cpu != null)
                    {
                        MainAdditionalInfo1Name.Text = _cpu.systemInfo.MbName;
                        MainAdditionalInfo2Name.Text = _cpu.systemInfo.MbVendor;
                        MainAdditionalInfo3Name.Text = _cpu.systemInfo.BiosVersion;
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.LogError(ex);
                }

                break;
            case 2:
                MainTeach.Target = TemperaturePlacer;
                MainAdditionalInfoDesc.Text = "Main_TemperatureSensors".GetLocalized();
                MainAdditionalInfo1Desc.Text = "Main_CpuTemp1".GetLocalized();
                MainAdditionalInfo2Desc.Text = "Main_CpuTJMaxDistance".GetLocalized();
                MainAdditionalInfo3Desc.Text = "Main_GpuTemp".GetLocalized();
                break;
            case 3:
                MainTeach.Target = UsabilityPlacer;
                MainAdditionalInfoDesc.Text = "Main_CpuUsage".GetLocalized();
                MainAdditionalInfo1Desc.Text = "Main_CpuUtilization".GetLocalized();
                MainAdditionalInfo2Desc.Text = "Main_CpuCoreCount".GetLocalized();
                MainAdditionalInfo3Desc.Text = "SMT:";
                try
                {
                    if (_cpu != null)
                    {
                        MainAdditionalInfo2Name.Text = _cpu.info.topology.cores.ToString();
                        MainAdditionalInfo3Name.Text = _cpu.systemInfo.SMT.ToString()
                            .Replace("True", "Cooler_Service_Enabled/Content".GetLocalized())
                            .Replace("False",
                                "Cooler_Service_Disabled/Content"
                                    .GetLocalized()); // Просто перевод, не для настроек кулера
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.LogError(ex);
                }

                break;
            case 4:
                MainTeach.Target = FrequencyPlacer;
                MainAdditionalInfoDesc.Text = "Main_CpuFrequency".GetLocalized();
                MainAdditionalInfo1Desc.Text = "Main_AverageFrequency".GetLocalized();
                MainAdditionalInfo2Desc.Text = "Main_MaxFrequency".GetLocalized();
                MainAdditionalInfo3Desc.Text = "Main_AverageVoltage".GetLocalized();
                break;
            case 5:
                MainTeach.Target = RamPlacer;
                MainAdditionalInfoDesc.Text = "Info_RAM_text/Text".GetLocalized();
                MainAdditionalInfo1Desc.Text = "Main_RamUtilization".GetLocalized();
                MainAdditionalInfo2Desc.Text = "Main_RamUsage1".GetLocalized();
                MainAdditionalInfo3Desc.Text = "Main_RamSize".GetLocalized();
                break;
            case 6:
                MainTeach.Target = PowerPlacer;
                MainAdditionalInfoDesc.Text = "Main_SystemPowers".GetLocalized();
                MainAdditionalInfo1Desc.Text = "Param_CPU_c3/Text".GetLocalized() + ":"; // Реальная мощность 
                MainAdditionalInfo2Desc.Text = "STAPM:";
                MainAdditionalInfo3Desc.Text = "Param_CPU_c4/Text".GetLocalized() + ":"; // Средняя мощность
                break;
            case 7:
                MainTeach.Target = VrmPlacer;
                MainAdditionalInfoDesc.Text = "Main_VrmInfo".GetLocalized();
                MainAdditionalInfo1Desc.Text = "VRM TDC:";
                MainAdditionalInfo2Desc.Text = "VRM EDC:";
                MainAdditionalInfo3Desc.Text = "SoC EDC:";
                break;
            case 8:
                MainTeach.Target = BatteryPlacer;
                MainAdditionalInfoDesc.Text = "Main_BatteryInfo".GetLocalized();
                MainAdditionalInfo1Desc.Text = "infoABATWear/Text".GetLocalized() + ":";
                MainAdditionalInfo2Desc.Text = "infoABATCycles/Text".GetLocalized() + ":";
                MainAdditionalInfo3Desc.Text = "infoABATRemainTime/Text".GetLocalized() + ":";
                break;
        }
    }

    /// <summary>
    ///     Открывает нужный пользователю TeachingTip
    /// </summary>
    private void TooltipPlacer_PointerPressed(object sender, PointerRoutedEventArgs e) =>
        SetPlacerTipAsync(int.Parse((string)((FrameworkElement)sender).Tag));

    /// <summary>
    ///     Сворачивает/разворачивает кнопки помощи возле блока "Не видите свой пресет?"
    /// </summary>
    private async void ExpandButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isHelpButtonsExpanded)
            {
                CollapseStoryboard.Begin();
                await Task.Delay(500);
                ExpandGrid.Visibility = Visibility.Collapsed;
            }
            else
            {                
                ExpandGrid.Visibility = Visibility.Visible;
                ExpandStoryboard.Begin();
            }

            _isHelpButtonsExpanded = !_isHelpButtonsExpanded;
        }
        catch (Exception ex)
        {
            await LogHelper.LogError(ex);
        }
        
    }

    #endregion

    #endregion
}