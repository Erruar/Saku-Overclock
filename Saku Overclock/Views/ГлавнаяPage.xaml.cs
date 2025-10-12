using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.JsonContainers;
using Saku_Overclock.Services;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.ViewModels;
using Windows.UI.Text;
using Brush = Microsoft.UI.Xaml.Media.Brush;
using Color = Windows.UI.Color;
using Image = Microsoft.UI.Xaml.Controls.Image;
using VisualTreeHelper = Saku_Overclock.Helpers.VisualTreeHelper;

namespace Saku_Overclock.Views;

public sealed partial class ГлавнаяPage
{
    private readonly IBackgroundDataUpdater? _dataUpdater;
    private double _maxCpuFreq = 1d;
    private static readonly IAppSettingsService AppSettings = App.GetService<IAppSettingsService>();
    private static readonly IAppNotificationService NotificationsService = App.GetService<IAppNotificationService>(); // Уведомления приложения
    private static Profile[] _profile = new Profile[1]; // Всегда по умолчанию будет 1 профиль
    private List<double>? _segmentLengths; // Хранит длины всех сегментов
    private double _totalLength; // Общая длина кривой
    private int _currentMode; // Хранит режим, который выбрал пользователь, то, где стоял курсор при нажатии на блок, чтобы показать тултип с дополнительной информацией о просматриемом блоке
    private bool _waitForTip;
    private bool _waitForCheck;
    private bool _waitingForCursorFlag;
    private string _doubleClickApplyPrev = string.Empty;

    public ГлавнаяPage()
    {
        App.GetService<ГлавнаяViewModel>();
        InitializeComponent();
        GetUpdates();
        CalculateSegmentLengths();
        _dataUpdater = App.BackgroundUpdater!;
        _dataUpdater.DataUpdated += OnDataUpdated;
        Unloaded += (_, _) =>
        {
            _dataUpdater.DataUpdated -= OnDataUpdated;
        };
        Loaded += ГлавнаяPage_Loaded;
    }

    private void ГлавнаяPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            Info_CpuName.Text = CpuSingleton.GetInstance().systemInfo.CpuName;
            Info_CpuCores.Text = CpuSingleton.GetInstance().info.topology.cores + "C/" +
                                 CpuSingleton.GetInstance().info.topology.logicalCores + "T";
            LoadProfiles();
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
            LogHelper.TraceIt_TraceError(ex);
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
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(),
                        AppContext.BaseDirectory));
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
        if (_maxCpuFreq < info.CpuFrequency)
        {
            _maxCpuFreq = info.CpuFrequency;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                Indicators_Temp.Text = Math.Round(info.CpuTempValue, 1).ToString(CultureInfo.InvariantCulture) + "C";
                Indicators_Busy.Text = Math.Round(info.CpuUsage, 0).ToString(CultureInfo.InvariantCulture) + "%";
                Indicators_Freq.Text = Math.Round(info.CpuFrequency, 1).ToString(CultureInfo.InvariantCulture) + " " + "infoAGHZ".GetLocalized();
                Indicators_Busy_Ring.Value = info.CpuUsage;
                Indicators_Freq_Ring.Value = info.CpuFrequency / _maxCpuFreq * 100;
                UpdatePointPosition(info.CpuTempValue);
                Indicators_Fast.Text = info.CpuFastValue == 0
                    ? "Info_PowerSumInfo_Disabled".GetLocalized()
                    : Math.Round(info.CpuFastValue, 1).ToString(CultureInfo.InvariantCulture) + "W";
                Indicators_VrmEdc.Text = Math.Round(info.VrmEdcValue, 1).ToString(CultureInfo.InvariantCulture) + "A";

                Indicators_Fast_Ring.Value = info.CpuFastValue / info.CpuFastLimit * 100;
                Indicators_VrmEdc_Ring.Value = info.VrmEdcValue / info.VrmEdcLimit * 100;

                if (info.BatteryUnavailable)
                {
                    if (Indicators_BatteryPercent.Text != "N/A")
                    {
                        Indicators_BatteryPercent.Text = "N/A";
                    }

                    if (Indicators_BatteryPercent_Ring.Value != 0)
                    {
                        Indicators_BatteryPercent_Ring.Value = 0;
                    }
                }
                else
                {
                    Indicators_BatteryPercent.Text = info.BatteryPercent;
                    Indicators_BatteryPercent_Ring.Value =
                        Convert.ToInt32(info.BatteryPercent?.Replace("%", string.Empty));
                }

                Indicators_Ram.Text = info.RamBusy;
                Indicators_Ram_Ring.Value = info.RamUsagePercent;

                var trueBatLifeTime = info.BatteryLifeTime;
                string batteryTime;
                if (trueBatLifeTime < 0)
                {
                    batteryTime = "InfoBatteryAC".GetLocalized(); // Устройство питается от сети
                }
                else
                {
                    var ts = TimeSpan.FromSeconds(trueBatLifeTime); // Преобразуем секунды в TimeSpan
                    var parts = new List<string>();
                    if ((int)ts.TotalHours > 0)
                    {
                        parts.Add($"{(int)ts.TotalHours}h"); // Добавляем часы, если они есть
                    }

                    if (ts.Minutes > 0)
                    {
                        parts.Add($"{ts.Minutes}m"); // Добавляем минуты, если они есть
                    }

                    if (ts.Seconds > 0 || parts.Count == 0)
                    {
                        parts.Add(
                            $"{ts.Seconds}s"); // Добавляем секунды – если других частей нет, или если секунды ненулевые
                    }

                    batteryTime = string.Join(" ", parts);
                }

                switch (_currentMode)
                {
                    case 2:
                        Main_AdditionalInfo1Name.Text = Indicators_Temp.Text;
                        Main_AdditionalInfo2Name.Text =
                            Math.Round(100 - info.CpuTempValue, 1).ToString(CultureInfo.InvariantCulture) + "C";
                        Main_AdditionalInfo3Name.Text = info.ApuTempValue == 0
                            ? "Main_BatteryUnavailable".GetLocalized()
                            : Math.Round(info.ApuTempValue, 1).ToString(CultureInfo.InvariantCulture) + "C";
                        break;
                    case 3:
                        Main_AdditionalInfo1Name.Text = Indicators_Busy.Text;
                        break;
                    case 4:
                        Main_AdditionalInfo1Name.Text = Indicators_Freq.Text;
                        Main_AdditionalInfo2Name.Text =
                            Math.Round(_maxCpuFreq, 1).ToString(CultureInfo.InvariantCulture) + " " + "infoAGHZ".GetLocalized();
                        Main_AdditionalInfo3Name.Text = info.CpuVoltage == 0
                            ? "Main_BatteryUnavailable".GetLocalized()
                            : Math.Round(info.CpuVoltage, 1).ToString(CultureInfo.InvariantCulture) + "V";
                        break;
                    case 5:
                        Main_AdditionalInfo1Name.Text = info.RamUsagePercent + "%";
                        Main_AdditionalInfo2Name.Text = Indicators_Ram.Text;
                        Main_AdditionalInfo3Name.Text = info.RamTotal;
                        break;
                    case 6:
                        Main_AdditionalInfo1Name.Text = Indicators_Fast.Text;
                        Main_AdditionalInfo2Name.Text = info.CpuStapmValue == 0
                            ? "Info_PowerSumInfo_Disabled".GetLocalized()
                            : Math.Round(info.CpuStapmValue, 1).ToString(CultureInfo.InvariantCulture) + "W";
                        Main_AdditionalInfo3Name.Text = info.CpuSlowValue == 0
                            ? "Info_PowerSumInfo_Disabled".GetLocalized()
                            : Math.Round(info.CpuSlowValue, 1).ToString(CultureInfo.InvariantCulture) + "W";
                        break;
                    case 7:
                        Main_AdditionalInfo1Name.Text = info.VrmTdcValue == 0
                            ? "Main_BatteryUnavailable".GetLocalized()
                            : Math.Round(info.VrmTdcValue, 1).ToString(CultureInfo.InvariantCulture) + "A";
                        Main_AdditionalInfo2Name.Text = info.VrmEdcValue == 0
                            ? "Main_BatteryUnavailable".GetLocalized()
                            : Math.Round(info.VrmEdcValue, 1).ToString(CultureInfo.InvariantCulture) + "A";
                        Main_AdditionalInfo3Name.Text = info.SocEdcValue == 0
                            ? "Main_BatteryUnavailable".GetLocalized()
                            : Math.Round(info.SocEdcValue, 1).ToString(CultureInfo.InvariantCulture) + "A";
                        break;
                    case 8:
                        Main_AdditionalInfo1Name.Text = info.BatteryUnavailable ? "Main_BatteryUnavailable".GetLocalized() : info.BatteryHealth;
                        Main_AdditionalInfo2Name.Text = info.BatteryUnavailable ? "Main_BatteryUnavailable".GetLocalized() : info.BatteryCycles;
                        Main_AdditionalInfo3Name.Text = info.BatteryUnavailable ? "Main_BatteryUnavailable".GetLocalized() : batteryTime;
                        break;
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"{ex.Message}");
            }
        });
    }

    // Функция для вычисления точки на кубической кривой Безье
    private static PointF GetBezierPoint(PointF start, Point p1, Point p2, Point end, double t)
    {
        var x = Math.Pow(1 - t, 3) * start.X +
                3 * Math.Pow(1 - t, 2) * t * p1.X +
                3 * (1 - t) * Math.Pow(t, 2) * p2.X +
                Math.Pow(t, 3) * end.X;

        var y = Math.Pow(1 - t, 3) * start.Y +
                3 * Math.Pow(1 - t, 2) * t * p1.Y +
                3 * (1 - t) * Math.Pow(t, 2) * p2.Y +
                Math.Pow(t, 3) * end.Y;

        return new PointF((float)x, (float)y);
    }

    // Функция для линейной интерполяции между двумя точками
    private static PointF InterpolatePoint(Point start, Point end, double t)
    {
        var x = start.X + t * (end.X - start.X);
        var y = start.Y + t * (end.Y - start.Y);
        return new PointF((float)x, (float)y);
    }

    // Предварительный расчет длин сегментов
    private void CalculateSegmentLengths()
    {
        _segmentLengths = [];

        // Определяем контрольные точки кривой
        var startPoint = new Point(0, 50);
        var controlPoints = new List<(Point p1, Point p2, Point p3)>
        {
            (new Point(20, 30), new Point(50, 30), new Point(70, 40)),
            (new Point(90, 50), new Point(100, 50), new Point(120, 43)),
            (new Point(160, 13), new Point(160, 10), new Point(220, 5))
        };

        // Вычисляем длины Bezier-сегментов
        foreach (var (p1, p2, p3) in controlPoints)
        {
            var length = ApproximateBezierLength(startPoint, p1, p2, p3);
            _segmentLengths.Add(length);
            startPoint = p3; // Обновляем начальную точку для следующего сегмента
        }

        // Добавляем длину последнего Line-сегмента
        var endPoint = new Point(230, 5);
        var lineLength = Distance(startPoint, endPoint);
        _segmentLengths.Add(lineLength);

        // Вычисляем общую длину кривой
        _totalLength = _segmentLengths.Sum();
    }

    // Функция для приближенного вычисления длины кривой Безье
    private static double ApproximateBezierLength(PointF start, Point p1, Point p2, Point end)
    {
        const int steps = 100; // Количество шагов для аппроксимации
        double length = 0;
        var previousPoint = start;

        for (var i = 1; i <= steps; i++)
        {
            var t = i / (double)steps;
            var currentPoint = GetBezierPoint(start, p1, p2, end, t);
            length += Distance(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }

        return length;
    }

    // Функция для вычисления расстояния между двумя точками
    private static double Distance(PointF p1, PointF p2)
    {
        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    // Обновление положения точки
    private void UpdatePointPosition(double temperature)
    {
        if (temperature <= 0 || temperature > 97)
        {
            temperature = 97;
        }

        // Предполагаем, что температура изменяется от 0 до 100
        var fraction = temperature / 100.0;

        // Вычисляем расстояние, которое соответствует текущей температуре
        var targetLength = fraction * _totalLength;

        // Находим сегмент, на котором находится точка
        double accumulatedLength = 0;
        for (var i = 0; i < _segmentLengths!.Count; i++)
        {
            var segmentLength = _segmentLengths[i];
            if (accumulatedLength + segmentLength >= targetLength)
            {
                // Точка находится на этом сегменте
                var segmentFraction = (targetLength - accumulatedLength) / segmentLength;

                // Определяем тип сегмента и вычисляем точку
                PointF point;
                if (i < _segmentLengths.Count - 1)
                {
                    // Bezier-сегмент
                    var (p1, p2, p3) = GetControlPointsForSegment(i);
                    var startPoint = GetStartPointForSegment(i);
                    point = GetBezierPoint(startPoint, p1, p2, p3, segmentFraction);
                }
                else
                {
                    // Line-сегмент
                    var startPoint = GetStartPointForSegment(i);
                    var endPoint = new Point(230, 5);
                    point = InterpolatePoint(startPoint, endPoint, segmentFraction);
                }

                // Обновляем положение точки
                PointTransform.X = point.X - 7; // Центрируем точку по ширине
                PointTransform.Y = point.Y - 7; // Центрируем точку по высоте
                return;
            }

            accumulatedLength += segmentLength;
        }
    }

    // Вспомогательные функции для получения контрольных точек и начальной точки сегмента
    private static (Point p1, Point p2, Point p3) GetControlPointsForSegment(int index)
    {
        var controlPoints = new List<(Point p1, Point p2, Point p3)>
        {
            (new Point(20, 30), new Point(50, 30), new Point(70, 40)),
            (new Point(90, 50), new Point(100, 50), new Point(120, 43)),
            (new Point(160, 13), new Point(160, 10), new Point(220, 5))
        };
        return controlPoints[index];
    }

    private static Point GetStartPointForSegment(int index)
    {
        var points = new List<Point>
        {
            new(0, 50),
            new(70, 40),
            new(120, 43),
            new(220, 5)
        };
        return points[index];
    }

    private async void GetUpdates()
    {
        try
        {
            MainChangelogStackPanel.Children.Clear();
            if (string.IsNullOrEmpty(UpdateChecker.GitHubInfoString))
            {
                await UpdateChecker.GenerateReleaseInfoString();
            }

            await GenerateFormattedReleaseNotes(MainChangelogStackPanel);
        }
        catch (Exception e)
        {
            await LogHelper.TraceIt_TraceError(e);
        }
    }

    #endregion

    #region Event Handlers
    private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var isCompact = e.NewSize.Height < 400;
        var (main, device, segments, frequent) = isCompact ? CompactGridMargins : NormalGridMargins;

        if (MainGrid.Margin != main)
        {
            MainGrid.Margin = main;
        }

        if (DeviceInfoSign.Margin != device)
        {
            DeviceInfoSign.Margin = device;
        }

        if (SegmentsGrid.Margin != segments)
        {
            SegmentsGrid.Margin = segments;
        }

        if (FriquentlyUsedGrid.Margin != frequent)
        {
            FriquentlyUsedGrid.Margin = frequent;
        }
    }
    private static readonly (Thickness Main, Thickness Device, Thickness Segments, Thickness Frequent) CompactGridMargins =
    (
        new Thickness( 00, -3, 0, 0),
        new Thickness( 14,  2, 0, 0),
        new Thickness(-10,  2, 0, 0),
        new Thickness(-10,  2, 0, 3)
    );
    private static readonly (Thickness Main, Thickness Device, Thickness Segments, Thickness Frequent) NormalGridMargins =
    (
        new Thickness( 00, 20, 0, 0),
        new Thickness( 14, 16, 0, 0),
        new Thickness(-10, 08, 0, 0),
        new Thickness(-10, 05, 0, 3)
    );

    private void HyperLink_Click(object sender, RoutedEventArgs e)
    {
        var link = "https://github.com/Erruar/Saku-Overclock/wiki/FAQ";
        if (sender is Button { Tag: string str1 })
        {
            link = str1;
        }

        Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
    }

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

    private void MainGithubReadmeButton_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://github.com/Erruar/Saku-Overclock") { UseShellExecute = true });

    private void MainGithubIssuesButton_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(
            new ProcessStartInfo("https://github.com/Erruar/Saku-Overclock/issues") { UseShellExecute = true });
    }

    private void PivotProfiles_Loaded(object sender, RoutedEventArgs e)
    {
        var pivot = sender as Pivot;
        var headers = VisualTreeHelper.FindVisualChildren<ContentPresenter>(pivot!);
        foreach (var header in headers)
        {
            var contentPresenters = VisualTreeHelper.FindVisualChildren<PivotHeaderPanel>(header);
            foreach (var content in contentPresenters)
            {
                content.HorizontalAlignment = HorizontalAlignment.Center;
                content.Opacity = 0.8;
                content.Margin = new Thickness(0,-20,0,0);
            }
        }
    }

    private void ToggleButton_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_waitForCheck)
        {
            return;
        }

        (sender as ToggleButton)!.IsChecked = true;
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
        var toggleButtons = VisualTreeHelper.FindVisualChildren<ToggleButton>(Preset_Pivot);
        foreach (var button in toggleButtons)
        {
            if (button.IsChecked == true)
            {
                if (button.Tag != null && button.Tag.ToString()!.Contains("Preset_"))
                {
                    AppSettings.Preset = -1;
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

                    ShellPage.NextPremadeProfile_Activate(endMode);

                    var (_, _, _, settings, _) = ShellPage.PremadedProfiles[endMode];

                    AppSettings.RyzenAdjLine = settings;
                    AppSettings.SaveSettings();

                    MainWindow.Applyer.ApplyWithoutAdjLine(false);

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
                    Apply_Teach.Subtitle = "Apply_Success_Desc".GetLocalized();
                    Apply_Teach.IconSource = new SymbolIconSource { Symbol = Symbol.Accept };
                    Apply_Teach.IsOpen = true;
                    await LogHelper.Log("Apply_Success".GetLocalized());
                    await Task.Delay(3000);
                    Apply_Teach.IsOpen = false;
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
                            AppSettings.Preset = i;
                            AppSettings.SaveSettings();

                            ПараметрыPage.ApplyInfo = string.Empty; 
                            ShellPage.MandarinSparseUnitProfile(profile,true);

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
                            Apply_Teach.Subtitle = "Apply_Success_Desc".GetLocalized();
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
                            break;
                        }
                    }
                }
            }
        }
    }

    #region Mouse Events & Blocks behavior

    private async void SetPlacerTipAsync(int currMode)
    {
        try
        {
            _currentMode = currMode;
            if (Main_Teach.IsOpen)
            {
                RemovePlacerTip();
            }
            if (_waitForTip)
            {
                await Task.Delay(200);
                _waitForTip = false;
            }

            Main_Teach.IsOpen = false;
            _waitingForCursorFlag = true;
            if (!_waitingForCursorFlag || currMode != _currentMode)
            {
                return; // Если курсор ушел на другой элемент — отменяем отображение
            }


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
                        Main_AdditionalInfo1Name.Text = CpuSingleton.GetInstance().systemInfo.MbName;
                        Main_AdditionalInfo2Name.Text = CpuSingleton.GetInstance().systemInfo.MbVendor;
                        Main_AdditionalInfo3Name.Text = CpuSingleton.GetInstance().systemInfo.BiosVersion;
                    }
                    catch (Exception ex)
                    {
                        await LogHelper.LogError(ex);
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
                        Main_AdditionalInfo2Name.Text = CpuSingleton.GetInstance().info.topology.cores.ToString();
                        Main_AdditionalInfo3Name.Text = CpuSingleton.GetInstance().systemInfo.SMT.ToString()
                            .Replace("True", "Cooler_Service_Enabled/Content".GetLocalized()).Replace("False", "Cooler_Service_Disabled/Content".GetLocalized()); // Просто перевод, не для настроек кулера
                    }
                    catch (Exception ex)
                    {
                        await LogHelper.LogError(ex);
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


            Main_Teach.IsOpen = true;
        }
        catch (Exception ex)
        {
            await LogHelper.LogError(ex);
        }
    }

    private void RemovePlacerTip()
    {
        _waitingForCursorFlag = false;
        Main_Teach.IsOpen = false;
        _waitForTip = true;
    }
    private void LogoPlacer_PointerPressed(object sender, PointerRoutedEventArgs e) =>
        SetPlacerTipAsync(1);

    private void TemperaturePlacer_PointerPressed(object sender, PointerRoutedEventArgs e) =>
        SetPlacerTipAsync(2);

    private void UsabilityPlacer_PointerPressed(object sender, PointerRoutedEventArgs e) =>
        SetPlacerTipAsync(3);

    private void FrequencyPlacer_PointerPressed(object sender, PointerRoutedEventArgs e) =>
        SetPlacerTipAsync(4);

    private void RamPlacer_PointerPressed(object sender, PointerRoutedEventArgs e) =>
        SetPlacerTipAsync(5);

    private void PowerPlacer_PointerPressed(object sender, PointerRoutedEventArgs e) =>
        SetPlacerTipAsync(6);

    private void VrmPlacer_PointerPressed(object sender, PointerRoutedEventArgs e) =>
        SetPlacerTipAsync(7);

    private void BatteryPlacer_PointerPressed(object sender, PointerRoutedEventArgs e) =>
     SetPlacerTipAsync(8);

    #endregion

    #endregion

    #region NotesWriter

    public static async Task GenerateFormattedReleaseNotes(StackPanel stackPanel)
    {
        stackPanel.Children.Clear();
        if (string.IsNullOrEmpty(UpdateChecker.GitHubInfoString))
        {
            await UpdateChecker.GenerateReleaseInfoString();
        }

        var formattedText = FormatReleaseNotes(UpdateChecker.GitHubInfoString);
        foreach (var paragraph in formattedText)
        {
            stackPanel.Children.Add(paragraph);
        }
    }

    private static UIElement[] FormatReleaseNotes(string? releaseNotes)
    {
        // Удаление ненужных частей текста
        var cleanedNotes = CleanReleaseNotes(releaseNotes);
        // Применение стилей markdown
        var formattedElements = ApplyMarkdownStyles(cleanedNotes);
        return formattedElements;
    }

    private static string CleanReleaseNotes(string? releaseNotes)
    {
        var lines = releaseNotes?.Split([Environment.NewLine], StringSplitOptions.None);
        var cleanedLines = new List<string>();
        for (var i = 0; i < lines?.Length; i++)
        {
            var line = lines[i];
            if (line.StartsWith("Highlights:"))
            {
                cleanedLines.Add(line); // Добавляем строку Highlights: 
                i++;
                while (i < lines.Length)
                {
                    if (string.IsNullOrWhiteSpace(lines[i]) || char.IsDigit(lines[i][0]))
                    {
                        cleanedLines.Add(lines[i]);
                    }
                    else
                    {
                        break; // Удаляем всё после строки, которая не начинается с цифры или пустая
                    }

                    i++;
                }

                i--; // Вернемся на шаг назад, чтобы правильно обработать следующую строку
            }
            else
            {
                cleanedLines.Add(line);
            }
        }

        return string.Join(Environment.NewLine, cleanedLines);
    }

    public static UIElement[] ApplyMarkdownStyles(string cleanedNotes)
    {
        var lines = cleanedNotes.Split(["\r\n", "\n"], StringSplitOptions.None);
        var elements = new List<UIElement>();

        foreach (var line in lines)
        {
            var trimmedLine = line.TrimStart(); // Убираем пробелы в начале строки 

            if (trimmedLine.StartsWith("### "))
            {
                var text = trimmedLine[4..];
                var textBlock = new TextBlock
                {
                    Text = text,
                    FontWeight = new FontWeight(600),
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Width = double.NaN
                };
                elements.Add(textBlock);
            }
            else if (trimmedLine.StartsWith("## "))
            {
                var text = trimmedLine[3..];
                var textBlock = new TextBlock
                {
                    Text = text,
                    FontWeight = new FontWeight(700),
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Width = double.NaN
                };
                elements.Add(textBlock);
            }
            else if (trimmedLine.StartsWith("# "))
            {
                var text = trimmedLine[2..];
                var textBlock = new TextBlock
                {
                    Text = text,
                    FontWeight = new FontWeight(800),
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Width = double.NaN
                };
                elements.Add(textBlock);
            }
            else if (trimmedLine.StartsWith("> "))
            {
            }
            else if (trimmedLine.StartsWith("![image]("))
            {
                var text = trimmedLine.Replace("![image](", "").Replace(")", "");
                var spoilerText = new TextBlock
                {
                    Text = "+ Spoiler",
                    FontWeight = new FontWeight(500),
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                var spoilerImage = new Image
                {
                    Source = new BitmapImage(new Uri(text)),
                    Visibility = Visibility.Collapsed
                };
                var spoilerButton = new Button
                {
                    BorderBrush = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
                    Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Content = new StackPanel
                    {
                        Children =
                        {
                            spoilerText,
                            spoilerImage
                        }
                    }
                };
                spoilerButton.Click += (_, _) =>
                {
                    spoilerImage.Visibility = spoilerImage.Visibility == Visibility.Collapsed
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                    spoilerText.Text = spoilerText.Text.Contains('-') ? "+ Spoiler" : "- Spoiler";
                };
                elements.Add(spoilerButton);
            }
            else
            {
                var matches = UnmanagementWords().Matches(trimmedLine);
                var lastPos = 0;

                foreach (Match match in matches)
                {
                    if (match.Index > lastPos)
                    {
                        var beforeText = trimmedLine[lastPos..match.Index];
                        elements.Add(new TextBlock
                        {
                            Text = beforeText,
                            TextWrapping = TextWrapping.Wrap,
                            HorizontalAlignment = HorizontalAlignment.Stretch
                        });
                    }

                    var highlightedText = match.Groups[1].Value;
                    elements.Add(new TextBlock
                    {
                        Text = highlightedText,
                        FontWeight = new FontWeight(700),
                        Foreground = (Brush)Application.Current.Resources["AccentColor"],
                        TextWrapping = TextWrapping.Wrap,
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    });

                    lastPos = match.Index + match.Length;
                }

                if (lastPos < trimmedLine.Length)
                {
                    var remainingText = trimmedLine[lastPos..];
                    elements.Add(new TextBlock
                    {
                        Text = remainingText,
                        TextWrapping = TextWrapping.Wrap,
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    });
                }
            }
        }

        return [.. elements];
    }

    [GeneratedRegex(@"\*\*(.*?)\*\*")]
    private static partial Regex UnmanagementWords();

    #endregion

}