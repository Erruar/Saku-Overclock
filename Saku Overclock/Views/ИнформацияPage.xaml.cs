﻿using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Management;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Windows.UI.Text;
using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.JsonContainers; 
using Saku_Overclock.SMUEngine;
using Saku_Overclock.ViewModels;
using ZenStates.Core;
using Brush = Microsoft.UI.Xaml.Media.Brush;
using FontFamily = System.Drawing.FontFamily;
using Icon = System.Drawing.Icon;
using LinearGradientBrush = System.Drawing.Drawing2D.LinearGradientBrush;

namespace Saku_Overclock.Views;

public sealed partial class ИнформацияPage
{
    private RTSSsettings _rtssset = new(); // Конфиг с настройками модуля RTSS
    private NiIconsSettings _niicons = new(); // Конфиг с настройками Ni-Icons
    private static readonly IAppSettingsService AppSettings = App.GetService<IAppSettingsService>();

    private readonly Dictionary<string, TaskbarIcon>
        _trayIcons = []; // Хранилище включенных в данный момент иконок Ni-Icons

    private class MinMax // Класс для хранения минимальных и максимальных значений Ni-Icons
    {
        public float Min;
        public float Max;
    }

    private class TotalBusyRam // Класс для хранения текущего использования ОЗУ и всего ОЗУ
    {
        public double BusyRam;
        public double TotalRam;
    }

    private readonly List<TotalBusyRam> _ramUsageHelper = []; // Лист текущего использования ОЗУ и всего ОЗУ

    private readonly List<MinMax> _niiconsMinMaxValues =
    [
        new(), new(), new(), new(), new(), new(), new(), new(), new(), new(), new()
    ]; // Лист для хранения минимальных и максимальных значений Ni-Icons

    private bool _loaded; // Страница загружена
    private bool _doNotTrackBattery;
    private string? _rtssLine; // Строка для вывода в модуль RTSS
    private string? _cachedSelectedProfileReplacement; // Кешированная замена имени профиля
    private readonly List<InfoPageCPUPoints> _cpuPointer = []; // Лист графика использования процессора
    private readonly List<InfoPageCPUPoints> _gpuPointer = []; // Лист графика частоты графического процессора
    private readonly List<InfoPageCPUPoints> _ramPointer = []; // Лист графика занятой ОЗУ
    private readonly List<InfoPageCPUPoints> _vrmPointer = []; // Лист графика тока VRM
    private readonly List<InfoPageCPUPoints> _batPointer = []; // Лист графика зарядки батареи
    private readonly List<InfoPageCPUPoints> _pstPointer = []; // Лист графика изменения P-State
    private readonly List<double> _psTatesList = [0, 0, 0]; // Лист с информацией о P-State

    private double
        _maxGfxClock =
            0.1; // Максимальная частота графического процессора, используется для графика частоты графического процессора

    private decimal _maxBatRate = 0.1m; // Максимальная мощность зарядки, используется для графика зарядки батареи
    private Brush? _transparentBrush; // Прозрачная кисть, используется для кнопок выбора баннера
    private Brush? _selectedBrush; // Кисть цвета выделенной кнопки, используется для кнопок выбора баннера

    private Brush?
        _selectedBorderBrush; // Кисть цвета границы выделенной кнопки, используется для кнопок выбора баннера

    private int _selectedGroup; // Текущий выбранный баннер, используется для кнопок выбора баннера
    private bool _isAppInTray; // Флаг приложения в трее, чтобы не обновлять значения и не тратить ресурсы ноутбука
    private IntPtr _ryzenAccess; // Указатель на библиотеку RyzenADJ
    private string _cpuName = "Unknown"; // Название процессора в системе
    private string _gpuName = "Unknown"; // Название графического процессора в системе
    private string _ramName = "Unknown"; // Название ОЗУ в системе
    private string? _batName = "Unknown"; // Название батареи в системе
    private int _numberOfCores; // Количество ядер
    private int _numberOfLogicalProcessors; // Количество потоков
    private DispatcherTimer? _dispatcherTimer; // Таймер для автообновления информации
    private Cpu? _cpu; // Инициализация ZenStates Core

    public ИнформацияPage()
    {
        _numberOfLogicalProcessors = 0;
        App.GetService<ИнформацияViewModel>();
        InitializeComponent();
        InitializeZenStates();
        RtssLoad();
        Loaded += ИнформацияPage_Loaded;
        Unloaded += ИнформацияPage_Unloaded;
    }

    #region JSON and Initialization

    #region JSON only voids

    //JSON форматирование 

    private void RtssSave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                "SakuOverclock"));
            File.WriteAllText(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\rtssparam.json",
                JsonConvert.SerializeObject(_rtssset, Formatting.Indented));
        }
        catch
        {
            // ignored
        }
    }

    private void RtssLoad()
    {
        try
        {
            _rtssset = JsonConvert.DeserializeObject<RTSSsettings>(File.ReadAllText(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\rtssparam.json"))!;
            _rtssset.RTSS_Elements.RemoveRange(0, 9);
            //if (rtssset == null) { rtssset = new JsonContainers.RTSSsettings(); RtssSave(); }
        }
        catch
        {
            _rtssset = new RTSSsettings();
            RtssSave();
        }
    }

    private void NiSave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                "SakuOverclock"));
            File.WriteAllText(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\niicons.json",
                JsonConvert.SerializeObject(_niicons, Formatting.Indented));
        }
        catch
        {
            // ignored
        }
    }

    private void NiLoad()
    {
        try
        {
            _niicons = JsonConvert.DeserializeObject<NiIconsSettings>(File.ReadAllText(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\niicons.json"))!;
        }
        catch
        {
            _niicons = new NiIconsSettings();
            NiSave();
        }
    }

    #endregion

    #region Ni-Icons

    private void DisposeAllNotifyIcons()
    {
        // Перебираем все иконки и вызываем Dispose для каждой из них
        foreach (var icon in _trayIcons.Values)
        {
            icon.Dispose();
        }

        // Очищаем коллекцию иконок
        _trayIcons.Clear();
    }

    private void CreateNotifyIcons()
    {
        DisposeAllNotifyIcons(); // Уничтожаем старые иконки перед созданием новых

        NiLoad();
        // Если нет элементов, не создаём иконки
        if (_niicons.Elements.Count == 0)
        {
            return;
        }

        foreach (var element in _niicons.Elements)
        {
            if (!element.IsEnabled)
            {
                continue;
            }

            if (element.Guid == string.Empty)
            {
                element.Guid = Guid.NewGuid().ToString();
                NiSave();
            }

            // Создаём NotifyIcon
            var notifyIcon = new TaskbarIcon
            {
                // Генерация иконки
                Icon = CreateIconFromElement(element)!,
                Id = Guid.Parse(element
                    .Guid) // Уникальный ID иконки ЕСЛИ ЕГО НЕТ - ПЕРЕЗАПИШЕТ ОСНОВНОЕ ТРЕЙ МЕНЮ
            };
            notifyIcon.ForceCreate();
            if (element.ContextMenuType != 0)
            {
                notifyIcon.ToolTipText = element.Name;
            }

            _trayIcons[element.Name] = notifyIcon;
        }
    }

    private static Icon? CreateIconFromElement(NiIconsElements element)
    {
        // Создаём Grid виртуально и растрируем в Bitmap
        // Пример создания иконки будет зависеть от элемента:
        // 1. Создание формы (круг, квадрат, логотип и т.д.)
        // 2. Заливка цвета с заданной прозрачностью
        // 3. Наложение текста с указанным размером шрифта

        // Для простоты примера создадим пустую иконку
        var bitmap = new Bitmap(32, 32);
        var g = Graphics.FromImage(bitmap);
        // Задаём цвет фона и форму
        var bgColor = ColorTranslator.FromHtml("#" + element.Color);
        //System.Drawing.Brush bgBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb((int)(element.BgOpacity * 255), bgColor));
        var bgBrush = new LinearGradientBrush(
            new Rectangle(0, 0, 32, 32),
            Color.FromArgb((int)(element.BgOpacity * 255), bgColor),
            Color.FromArgb((int)(element.BgOpacity * 255), Color.Blue),
            LinearGradientMode.Horizontal);
        switch (element.IconShape)
        {
            case 0: // Куб
                g.FillRectangle(bgBrush, 0, 0, 32, 32);
                break;
            case 1: // Скруглённый куб
                var path = CreateRoundedRectanglePath(new Rectangle(0, 0, 32, 32), 7);
                g.FillPath(bgBrush, path!);
                break;
            case 2: // Круг
                g.FillEllipse(bgBrush, 0, 0, 32, 32);
                break;
            default:
                g.FillRectangle(bgBrush, 0, 0, 32, 32);
                break;
        }

        // Добавляем текст
        try
        {
            var font = new Font(new FontFamily("Arial"), element.FontSize * 2, System.Drawing.FontStyle.Regular,
                GraphicsUnit.Pixel);
            var textBrush = new SolidBrush(InvertColor(element.Color));
            // Центруем текст
            var textSize = g.MeasureString("Text", font);
            var textPosition = new PointF(
                (bitmap.Width - textSize.Width) / 2,
                (bitmap.Height - textSize.Height) / 2
            );
            // Рисуем текст
            g.DrawString("Text", font, textBrush, textPosition);
        }
        catch
        {
            // Игнорируем ошибки
        }

        try
        {
            return Icon.FromHandle(bitmap.GetHicon());
        }
        catch
        {
            return null;
        }
    }

    private static GraphicsPath? CreateRoundedRectanglePath(Rectangle rect, int radius)
    {
        // Проверка корректности значений
        if (radius <= 0 || rect.Width <= 0 || rect.Height <= 0)
        {
            return null;
        }

        try
        {
            var path = new GraphicsPath();
            var diameter = radius * 2;
            var size = new Size(diameter, diameter);
            var arc = new Rectangle(rect.Location, size);

            // Верхний левый угол
            path.AddArc(arc, 180, 90);

            // Верхний правый угол
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);

            // Нижний правый угол
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // Нижний левый угол
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }
        catch
        {
            return null;
        }
    }

    private void Change_Ni_Icons_Text(string iconName, string? newText, string? tooltipText = null,
        string? advancedTooltip = null)
    {
        try
        {
            if (_trayIcons.TryGetValue(iconName, out var notifyIcon))
            {
                foreach (var element in _niicons.Elements)
                {
                    if (element.Name == iconName)
                    {
                        // Изменяем текст на иконке (слой 2)
                        notifyIcon.Icon = UpdateIconText(newText, element.Color,
                            element.IsGradient ? element.SecondColor : string.Empty, element.FontSize,
                            element.IconShape, element.BgOpacity, notifyIcon.Icon);

                        // Обновляем TooltipText, если он задан
                        if (tooltipText != null && notifyIcon.ToolTipText != null)
                        {
                            notifyIcon.ToolTipText = element.ContextMenuType == 2
                                ? tooltipText + "\n" + advancedTooltip
                                : tooltipText;
                        }
                    }
                }
            }
        }
        catch
        {
            CreateNotifyIcons();
        }
    }

    private static Icon? UpdateIconText(string? newText, string newColor, string secondColor, int fontSize,
        int iconShape, double opacity, Icon? oldIcon = null)
    {
        // Уничтожаем старую иконку, если она существует
        if (oldIcon != null)
        {
            DestroyIcon(oldIcon.Handle); // Освобождение старой иконки
            oldIcon.Dispose(); // Освобождаем ресурсы иконки
        }

        // Создаём новую иконку на основе существующей с новым текстом
        var bitmap = new Bitmap(32, 32);
        var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Цвет фона и кисть
        var bgColor = ColorTranslator.FromHtml("#" + newColor);
        object bgBrush = new SolidBrush(Color.FromArgb((int)(opacity * 255), bgColor));
        if (secondColor != string.Empty)
        {
            var scColor = ColorTranslator.FromHtml("#" + secondColor);
            bgBrush = new LinearGradientBrush(
                new Rectangle(0, 0, 32, 32),
                Color.FromArgb((int)(opacity * 255), bgColor),
                Color.FromArgb((int)(opacity * 255), scColor),
                LinearGradientMode.Horizontal);
        }

        // Рисуем фон иконки в зависимости от формы
        switch (iconShape)
        {
            case 0: // Куб
                g.FillRectangle((System.Drawing.Brush)bgBrush, 0, 0, 32, 32);
                break;
            case 1: // Скруглённый куб
                var path = CreateRoundedRectanglePath(new Rectangle(0, 0, 32, 32), 7);
                if (path != null)
                {
                    g.FillPath((System.Drawing.Brush)bgBrush, path);
                }
                else
                {
                    g.FillRectangle((System.Drawing.Brush)bgBrush, 0, 0, 32, 32);
                }

                break;
            case 2: // Круг
                g.FillEllipse((System.Drawing.Brush)bgBrush, 0, 0, 32, 32);
                break;
            // Добавьте остальные фигуры и обработку ico при необходимости
            default:
                g.FillRectangle((System.Drawing.Brush)bgBrush, 0, 0, 32, 32);
                break;
        }

        // Определение позиции текста
        var font = new Font(new FontFamily("Segoe UI"), fontSize * 2f, System.Drawing.FontStyle.Bold,
            GraphicsUnit.Pixel);
        var textBrush = new SolidBrush(InvertColor(newColor));
        // Центрируем текст
        var textSize = g.MeasureString(newText, font);
        var textPosition = new PointF(
            (bitmap.Width - textSize.Width) / 10,
            (bitmap.Height - textSize.Height) / 2
        );
        // Рисуем текст
        g.DrawString(newText, font, textBrush, textPosition);
        // Создание иконки из Bitmap
        // Создание иконки из Bitmap и освобождение ресурсов
        try
        {
            return Icon.FromHandle(bitmap.GetHicon());
        }
        catch
        {
            return null;
        }
    }

    // Метод для освобождения ресурсов, используемый после GetHicon()
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    // Метод для вычисления позиции текста в зависимости от условий
    // ReSharper disable once UnusedMember.Local
    private static Point GetTextPosition(string? newText, int fontSize, out int newFontSize)
    {
        // По умолчанию позиция текста для трех символов
        var position = new Point(-5, 6);

        // Определение масштаба шрифта
        var scale = fontSize / 9.0f;

        if (newText != null)
        {
            // Если текст состоит из одного символа
            if (newText.Contains(',') && newText.Split(',')[0].Length == 1 && newText.Split(',')[1].Length >= 2)
            {
                position = new Point(-5, 6);
            }

            if (newText.Contains(',') && newText.Split(',')[0].Length == 2 || newText.Contains(',') &&
                newText.Split(',')[0].Length == 1 && newText.Split(',')[1].Length <= 1)
            {
                position = new Point(2, 6);
            }
            // Если текст состоит из четырёх символов
            else if (newText.Contains(',') && newText.Split(',')[0].Length == 4)
            {
                position = new Point(-6, 8);
                fontSize -= 2; // уменьшение размера шрифта на 2
            }
        }

        // Корректируем позицию текста на основе масштаба
        position.X = (int)Math.Floor(position.X * scale);
        position.Y = (int)Math.Floor(position.Y * scale);
        newFontSize = fontSize;
        return position;
    }

    private static Color InvertColor(string color)
    {
        var r = 0;
        var g = 0;
        var b = 0;
        if (!string.IsNullOrEmpty(color))
        {
            // Убираем символ #, если он присутствует
            var valuestring = color.TrimStart('#');
            // Парсим цветовые компоненты
            r = Convert.ToInt32(valuestring[..2], 16);
            g = Convert.ToInt32(valuestring.Substring(2, 2), 16);
            b = Convert.ToInt32(valuestring.Substring(4, 2), 16);
        }

        if (r + g + b > 325)
        {
            r = 0;
            b = 0;
            g = 0;
        }
        else
        {
            r = 255;
            b = 255;
            g = 255;
        }

        /*r = 255 - r;
        g = 255 - g;
        b = 255 - b;*/
        return Color.FromArgb(r, g, b);
    }

    #endregion

    #region Get-Info voids

    private void InitializeZenStates()
    {
        try
        {
            _cpu ??= CpuSingleton.GetInstance(); // Загрузить ZenStates Core
        }
        catch (Exception ex)
        {
            SendSmuCommand.TraceIt_TraceError(ex.ToString());
            App.GetService<IAppNotificationService>()
                .Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory)); // Вывести ошибку
        }
    }
    public static async Task<int> GetCpuCoresAsync()
    {
        return await Task.Run(() =>
        {
            var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Processor");
            try
            {
                foreach (var queryObj in searcher.Get().Cast<ManagementObject>())
                {
                    var numberOfCores = Convert.ToInt32(queryObj["NumberOfCores"]);
                    var numberOfLogicalProcessors = Convert.ToInt32(queryObj["NumberOfLogicalProcessors"]);
                    var l2Size = Convert.ToDouble(queryObj["L2CacheSize"]) / 1024;

                    return numberOfLogicalProcessors == numberOfCores
                        ? numberOfCores
                        : int.Parse(GetSystemInfo.GetBigLITTLE(numberOfCores, l2Size));
                }
            }
            catch
            {
                return 0; // Возвращаем 0 в случае ошибки
            }

            return 0; // Возвращаем 0, если данные не были найдены
        });
    }

    private async Task GetCpuInfo()
    {
        try
        {
            // Переменные для хранения данных
            string name = string.Empty, description = string.Empty, baseClock = string.Empty;
            double l3Size = 0;

            // Асинхронное выполнение WMI-запросов и первичных операций
            var cpuInfoTask = Task.Run(() =>
            {
                var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Processor");
                foreach (var queryObj in searcher.Get().Cast<ManagementObject>())
                {
                    name = queryObj["Name"]?.ToString() ?? "";
                    description = queryObj["Description"]?.ToString() ?? "";
                    _numberOfCores = Convert.ToInt32(queryObj["NumberOfCores"]);
                    _numberOfLogicalProcessors = Convert.ToInt32(queryObj["NumberOfLogicalProcessors"]);
                    _ = Convert.ToDouble(queryObj["L2CacheSize"]) / 1024;
                    l3Size = Convert.ToDouble(queryObj["L3CacheSize"]) / 1024;
                    baseClock = queryObj["MaxClockSpeed"]?.ToString() ?? "";
                }
            });

            if (_numberOfCores == 0 && _cpu != null)
            {
                _numberOfCores = (int)_cpu.info.topology.cores;
            }

            if (_numberOfLogicalProcessors == 0 && _cpu != null)
            {
                _numberOfLogicalProcessors = (int)_cpu.info.topology.logicalCores;
            }

            var gpuNameTask = Task.Run(() =>
            {
                var gpuName = GetSystemInfo.GetGPUName(0) ?? "";
                return gpuName.Contains("AMD") ? gpuName : GetSystemInfo.GetGPUName(1) ?? gpuName;
            });

            // Асинхронное выполнение других операций
            var instructionSetsTask = Task.Run(GetSystemInfo.InstructionSets);
            var l1CacheTask = Task.Run(() => CalculateCacheSizeAsync(GetSystemInfo.CacheLevel.Level1));
            var l2CacheTask = Task.Run(() => CalculateCacheSizeAsync(GetSystemInfo.CacheLevel.Level2));
            var codeNameTask = Task.Run(GetSystemInfo.Codename);

            // Ожидание выполнения всех задач
            await Task.WhenAll(cpuInfoTask, gpuNameTask, instructionSetsTask, l1CacheTask, l2CacheTask, codeNameTask);

            // Получение результатов
            var gpuName = gpuNameTask.Result;
            var instructionSets = instructionSetsTask.Result;
            var l1Cache = l1CacheTask.Result;
            var l2Cache = l2CacheTask.Result;
            var codeName = codeNameTask.Result;

            // Обновление UI в основном потоке
            await InfoCpuSectionGridBuilder();
            _cpuName = _cpu == null ? name : _cpu.info.cpuName;
            tbProcessor.Text = _cpuName;
            tbCaption.Text = description;

            if (!string.IsNullOrEmpty(codeName))
            {
                tbCodename.Text = codeName;
                tbCodename1.Visibility = Visibility.Collapsed;
                tbCode1.Visibility = Visibility.Collapsed;
            }
            else
            {
                try
                {
                    tbCodename1.Text = $"{_cpu?.info.codeName}";
                }
                catch
                {
                    tbCodename1.Visibility = Visibility.Collapsed;
                    tbCode1.Visibility = Visibility.Collapsed;
                }

                tbCodename.Visibility = Visibility.Collapsed;
                tbCode.Visibility = Visibility.Collapsed;
            }

            tbCores.Text = _numberOfLogicalProcessors == _numberOfCores
                ? _numberOfCores.ToString()
                : GetSystemInfo.GetBigLITTLE(_numberOfCores, l2Cache);
            _gpuName = gpuName;
            try
            {
                tbSMU.Text = _cpu?.systemInfo.GetSmuVersionString();
            }
            catch
            {
                tbSMU.Visibility = Visibility.Collapsed;
                infoSMU.Visibility = Visibility.Collapsed;
            }

            tbThreads.Text = _numberOfLogicalProcessors.ToString();
            tbL3Cache.Text = $"{l3Size:0.##} MB";
            tbL1Cache.Text = $"{l1Cache:0.##} MB";
            tbL2Cache.Text = $"{l2Cache:0.##} MB";
            tbBaseClock.Text = $"{baseClock} MHz";
            tbInstructions.Text = instructionSets;
        }
        catch (Exception ex)
        {
            Console.WriteLine("An error occurred: " + ex.Message);
        }
    }


    private static async Task<double> CalculateCacheSizeAsync(GetSystemInfo.CacheLevel level)
    {
        return await Task.Run(() =>
        {
            var sum = 0u;
            foreach (var number in GetSystemInfo.GetCacheSizes(level))
            {
                sum += number;
            }

            return sum / 1024.0;
        });
    }

    private async Task GetBatInfoAsync()
    {
        if (BATBannerButton.Visibility == Visibility.Collapsed)
        {
            return;
        }

        try
        {
            // Асинхронное выполнение операций
            var batteryInfoTask = Task.Run(() =>
            {
                var batteryPercent = GetSystemInfo.GetBatteryPercent() + "W";
                var batteryState = GetSystemInfo.GetBatteryStatus().ToString();
                var batteryHealth = $"{100 - GetSystemInfo.GetBatteryHealth() * 100:0.##}%";
                var batteryCycles = $"{GetSystemInfo.GetBatteryCycle()}";
                var fullChargeCapacity = GetSystemInfo.ReadFullChargeCapacity();
                var designCapacity = GetSystemInfo.ReadDesignCapacity(out var notTrack);
                var chargeRate = $"{GetSystemInfo.GetBatteryRate() / 1000:0.##}W";
                var batteryName = GetSystemInfo.GetBatteryName();

                return new
                {
                    batteryPercent,
                    batteryState,
                    batteryHealth,
                    batteryCycles,
                    fullChargeCapacity,
                    designCapacity,
                    chargeRate,
                    batteryName,
                    notTrack
                };
            });

            var batteryInfo = await batteryInfoTask;

            // Обновление UI
            tbBAT.Text = batteryInfo.batteryPercent;
            tbBATState.Text = batteryInfo.batteryState;
            tbBATHealth.Text = batteryInfo.batteryHealth;
            tbBATCycles.Text = batteryInfo.batteryCycles;
            tbBATCapacity.Text = $"{batteryInfo.fullChargeCapacity}mAh/{batteryInfo.designCapacity}mAh";
            tbBATChargeRate.Text = batteryInfo.chargeRate;
            _batName = batteryInfo.batteryName;

            if (batteryInfo.notTrack)
            {
                BATBannerButton.Visibility = Visibility.Collapsed;
                _doNotTrackBattery = true;
            }
        }
        catch
        {
            // При ошибке скрываем элементы и отмечаем, что батарея не отслеживается
            _doNotTrackBattery = true;
            if (BATBannerButton.Visibility != Visibility.Collapsed)
            {
                BATBannerButton.Visibility = Visibility.Collapsed;
            }
        }
    }

    private async Task GetRamInfo()
    {
        double capacity = 0;
        var speed = 0;
        var type = 0;
        var width = 0;
        var slots = 0;
        var producer = string.Empty;
        var model = string.Empty;

        try
        {
            var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_PhysicalMemory");
            await Task.Run(() =>
            {
                foreach (var queryObj in searcher.Get().Cast<ManagementObject>())
                {
                    if (producer == "")
                    {
                        producer = queryObj["Manufacturer"].ToString();
                    }
                    else if (!producer!.Contains(value: queryObj["Manufacturer"].ToString()!))
                    {
                        producer = $"{producer}/{queryObj["Manufacturer"]}";
                    }

                    if (model == "")
                    {
                        model = queryObj["PartNumber"].ToString();
                    }
                    else if (!model!.Contains(value: queryObj["PartNumber"].ToString()!))
                    {
                        model = $"{model}/{queryObj["PartNumber"]}";
                    }

                    capacity += Convert.ToDouble(queryObj["Capacity"]);
                    speed = Convert.ToInt32(queryObj["ConfiguredClockSpeed"]);
                    type = Convert.ToInt32(queryObj["SMBIOSMemoryType"]);
                    width += Convert.ToInt32(queryObj["DataWidth"]);
                    slots++;
                }
            });
            capacity = capacity / 1024 / 1024 / 1024;
            var ddrType = type switch
            {
                20 => "DDR",
                21 => "DDR2",
                24 => "DDR3",
                26 => "DDR4",
                30 => "LPDDR4",
                34 => "DDR5",
                35 => "LPDDR5",
                36 => "LPDDR5X",
                _ => $"Unknown ({type})",
            };
            _ramName = $"{capacity} GB {ddrType} @ {speed} MT/s";
            tbRAM.Text = speed + "MT/s";
            tbRAMProducer.Text = producer;
            tbRAMModel.Text = model.Replace(" ", null);
            tbWidth.Text = $"{width} bit";
            tbSlots.Text = $"{slots} * {width / slots} bit";
            tbTCL.Text = Utils.GetBits(_cpu!.ReadDword(0 | 0x50204), 0, 6) + "T";
            tbTRCDWR.Text = Utils.GetBits(_cpu!.ReadDword(0 | 0x50204), 24, 6) + "T";
            tbTRCDRD.Text = Utils.GetBits(_cpu!.ReadDword(0 | 0x50204), 16, 6) + "T";
            tbTRAS.Text = Utils.GetBits(_cpu!.ReadDword(0 | 0x50204), 8, 7) + "T";
            tbTRP.Text = Utils.GetBits(_cpu!.ReadDword(0 | 0x50208), 16, 6) + "T";
            tbTRC.Text = Utils.GetBits(_cpu!.ReadDword(0 | 0x50208), 0, 8) + "T";
        }
        catch (Exception ex)
        {
            SendSmuCommand.TraceIt_TraceError(ex.ToString());
        }
    }

    #endregion

    #region P-State voids

    private static void CalculatePstateDetails(uint eax,
        out uint cpuDfsId, out uint cpuFid)
    {
        cpuDfsId = eax >> 8 & 0x3F;
        cpuFid = eax & 0xFF;
    }

    private void ReadPstate()
    {
        try
        {
            for (var i = 0; i < 3; i++)
            {
                uint eax = 0, edx = 0;
                var pstateId = i;
                try
                {
                    if (_cpu?.ReadMsr(Convert.ToUInt32(Convert.ToInt64(0xC0010064) + pstateId), ref eax, ref edx) ==
                        false)
                    {
                        App.MainWindow.ShowMessageDialogAsync("Error while reading CPU Pstate", "Critical Error");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    SendSmuCommand.TraceIt_TraceError(ex.ToString());
                }

                CalculatePstateDetails(eax, out var cpuDfsId, out var cpuFid);
                var textBlock = (TextBlock)InfoPSTSectionMetrics.FindName($"tbPSTP{i}");
                if (cpuFid != 0)
                {
                    textBlock.Text =
                        $"FID: {Convert.ToString(cpuFid, 10)}/DID: {Convert.ToString(cpuDfsId, 10)}\n{cpuFid * 25 / (cpuDfsId * 12.5) / 10}" +
                        "infoAGHZ".GetLocalized();
                }
                else
                {
                    textBlock.Text = "Info_PowerSumInfo_DisabledPState".GetLocalized();
                }

                _psTatesList[i] = cpuFid * 25 / (cpuDfsId * 12.5) / 10;
            }
        }
        catch (Exception ex)
        {
            SendSmuCommand.TraceIt_TraceError(ex.ToString());
        }
    }

    #endregion

    #region Page-related voids

    private void Window_VisibilityChanged(object sender, WindowVisibilityChangedEventArgs args)
    {
        if (args.Visible)
        {
            _dispatcherTimer?.Start();
            _isAppInTray = false;
        }
        else
        {
            if (infoRTSSButton.IsChecked == false && AppSettings.NiIconsEnabled == false)
            {
                _dispatcherTimer?.Stop();
                _isAppInTray = true;
            }
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        StartInfoUpdate();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        StopInfoUpdate();
    }

    private async void ИнформацияPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _loaded = true;
            _selectedBrush = CPUBannerButton.Background;
            _selectedBorderBrush = CPUBannerButton.BorderBrush;
            _transparentBrush = GPUBannerButton.Background;
            await GetCpuInfo();
            await GetRamInfo();
            ReadPstate();
            await GetBatInfoAsync();
            if (CPUBannerButton.Shadow != new ThemeShadow())
            {
                CPUBannerButton.Shadow ??= new ThemeShadow();
                GPUBannerButton.Shadow = null;
                RAMBannerButton.Shadow = null;
                BATBannerButton.Shadow = null;
                PSTBannerButton.Shadow = null;
                VRMBannerButton.Shadow = null;
            }

            try
            { 
                infoRTSSButton.IsChecked = AppSettings.RTSSMetricsEnabled;
                infoNiIconsButton.IsChecked = AppSettings.NiIconsEnabled;
                if (AppSettings.NiIconsEnabled)
                {
                    CreateNotifyIcons();
                }
            }
            catch (Exception exception)
            {
                SendSmuCommand.TraceIt_TraceError(exception.ToString());
            }
        }
        catch (Exception exception)
        {
            SendSmuCommand.TraceIt_TraceError(exception.ToString());
        }
    }

    private void ИнформацияPage_Unloaded(object sender, RoutedEventArgs e)
    {
        try
        {
            DisposeAllNotifyIcons();
        }
        catch (Exception ex)
        {
            SendSmuCommand.TraceIt_TraceError(ex.ToString());
        }

        try
        {
            infoRTSSButton.IsChecked = false;
            _dispatcherTimer?.Stop();
            RtssHandler.ResetOsdText();
        }
        catch (Exception ex)
        {
            SendSmuCommand.TraceIt_TraceError(ex.ToString());
        }
    }

    #endregion

    #region Info Update voids

    private async void UpdateInfoAsync()
    {
        try
        {
            if (!_loaded)
            {
                return;
            }

            if (!_isAppInTray)
            {
                if (_selectedGroup != 0)
                {
                    infoCPUSectionComboBox.Visibility = Visibility.Collapsed;
                    InfoCPUComboBoxBorderSharedShadow_Element.Visibility = _ryzenAccess == 0x0 ? Visibility.Visible : Visibility.Collapsed;
                    if (_selectedGroup == 1)
                    {
                        //Показать свойства видеокарты

                        infoCPUSectionName.Text = "InfoGPUSectionName".GetLocalized();
                        InfoCPUSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoGPUSectionMetrics.Visibility = Visibility.Visible;
                        InfoRAMSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoVRMSectionMetrics.Visibility = Visibility.Collapsed;
                        infoRAMMAINSection.Visibility = Visibility.Collapsed;
                        infoCPUMAINSection.Visibility = Visibility.Collapsed;
                        InfoBATSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoPSTSectionMetrics.Visibility = Visibility.Collapsed;
                        tbProcessor.Text = _gpuName;
                    }

                    if (_selectedGroup == 2)
                    {
                        //Показать свойства ОЗУ

                        infoCPUSectionName.Text = "InfoRAMSectionName".GetLocalized();
                        InfoCPUSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoGPUSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoRAMSectionMetrics.Visibility = Visibility.Visible;
                        InfoVRMSectionMetrics.Visibility = Visibility.Collapsed;
                        tbProcessor.Text = _ramName;
                        infoRAMMAINSection.Visibility = Visibility.Visible;
                        infoCPUMAINSection.Visibility = Visibility.Collapsed;
                        InfoBATSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoPSTSectionMetrics.Visibility = Visibility.Collapsed;
                    }

                    if (_selectedGroup == 3)
                    {
                        //Зона VRM

                        infoCPUSectionName.Text = "VRM";
                        InfoCPUSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoGPUSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoRAMSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoVRMSectionMetrics.Visibility = Visibility.Visible;
                        tbProcessor.Text = _cpuName;
                        infoRAMMAINSection.Visibility = Visibility.Collapsed;
                        infoCPUMAINSection.Visibility = Visibility.Visible;
                        InfoBATSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoPSTSectionMetrics.Visibility = Visibility.Collapsed;
                    }

                    if (_selectedGroup == 4)
                    {
                        infoCPUSectionName.Text = "InfoBatteryName".GetLocalized();
                        InfoCPUSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoGPUSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoRAMSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoVRMSectionMetrics.Visibility = Visibility.Collapsed;
                        tbProcessor.Text = _batName;
                        infoRAMMAINSection.Visibility = Visibility.Collapsed;
                        infoCPUMAINSection.Visibility = Visibility.Visible;
                        InfoBATSectionMetrics.Visibility = Visibility.Visible;
                        InfoPSTSectionMetrics.Visibility = Visibility.Collapsed;
                    }

                    if (_selectedGroup == 5)
                    {
                        infoCPUSectionName.Text = "P-States";
                        InfoCPUSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoGPUSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoRAMSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoVRMSectionMetrics.Visibility = Visibility.Collapsed;
                        tbProcessor.Text = _cpuName;
                        infoRAMMAINSection.Visibility = Visibility.Collapsed;
                        infoCPUMAINSection.Visibility = Visibility.Visible;
                        InfoBATSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoPSTSectionMetrics.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    infoCPUMAINSection.Visibility = Visibility.Visible;
                    infoRAMMAINSection.Visibility = Visibility.Collapsed;
                    infoCPUSectionComboBox.Visibility = _ryzenAccess != 0x0 ? Visibility.Visible : Visibility.Collapsed;
                    InfoCPUComboBoxBorderSharedShadow_Element.Visibility = Visibility.Visible;
                    InfoCPUSectionMetrics.Visibility = Visibility.Visible;
                    InfoVRMSectionMetrics.Visibility = Visibility.Collapsed;
                    InfoGPUSectionMetrics.Visibility = Visibility.Collapsed;
                    InfoRAMSectionMetrics.Visibility = Visibility.Collapsed;
                    InfoBATSectionMetrics.Visibility = Visibility.Collapsed;
                    InfoPSTSectionMetrics.Visibility = Visibility.Collapsed;
                    //Скрыть лишние элементы 
                    infoCPUSectionName.Text = "InfoCPUSectionName".GetLocalized();
                    tbProcessor.Text = _cpuName;
                }

               

                if (_ryzenAccess == 0x0)
                {
                    if (_cpu != null)
                    {
                        await RyzenAdjWrapper.CpuFrequencyManager.RefreshPowerTable(_cpu);
                        if (infoCPUSectionGetInfoTypeComboBox.SelectedIndex == 1)
                        {
                            await RyzenAdjWrapper.CpuFrequencyManager.InitializeCoreIndexMapAsync(_numberOfCores);
                        }
                    } 

                    if (infoCPUSectionGetInfoTypeComboBox.Visibility == Visibility.Collapsed)
                    {
                        infoCPUSectionGetInfoTypeComboBox.Visibility = Visibility.Visible;
                    }

                    if (!Info_RyzenADJLoadError_InfoBar.IsOpen)
                    {
                        Info_RyzenADJLoadError_InfoBar.IsOpen = true;
                        infoRTSSButton.Visibility = Visibility.Collapsed;
                        infoNiIconsButton.Visibility = Visibility.Collapsed;
                    }

                    GPUBannerButton.Visibility = infoCPUSectionGetInfoTypeComboBox.SelectedIndex == 2
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                    InfoCPUSectionGetInfoSelectIndexesButton.Visibility =
                        infoCPUSectionGetInfoTypeComboBox.SelectedIndex == 2
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                }
                else
                {
                    RyzenAdjWrapper.Refresh_table(_ryzenAccess);
                }

                InfoACPUBannerPolygon.Points.Remove(new Windows.Foundation.Point(60, 49));
                InfoACPUBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(60, 49));
                InfoAGPUBannerPolygon.Points.Remove(new Windows.Foundation.Point(60, 49));
                InfoAGPUBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(60, 49));
                InfoARAMBannerPolygon.Points.Remove(new Windows.Foundation.Point(60, 49));
                InfoARAMBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(60, 49));
                InfoAVRMBannerPolygon.Points.Remove(new Windows.Foundation.Point(60, 49));
                InfoAVRMBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(60, 49));
                InfoABATBannerPolygon.Points.Remove(new Windows.Foundation.Point(60, 49));
                InfoABATBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(60, 49));
                InfoAPSTBannerPolygon.Points.Remove(new Windows.Foundation.Point(60, 49));
                InfoAPSTBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(60, 49));

                decimal batteryRate = 0;
                if (BATBannerButton.Visibility == Visibility.Visible && !_doNotTrackBattery)
                {
                    batteryRate = GetSystemInfo.GetBatteryRate() / 1000;
                }

                tbBATChargeRate.Text = $"{batteryRate}W";
                tbBAT.Text = GetSystemInfo.GetBatteryPercent() + "%";
                var batLifeTime = GetSystemInfo.GetBatteryLifeTime() + 0d;
                tbBATTime.Text = batLifeTime - -1.0f == 0.0f
                    ? "InfoBatteryAC".GetLocalized()
                    : $"{Math.Round(batLifeTime / 60 / 60, 0)}h {Math.Round((batLifeTime / 60 / 60 - Math.Round(batLifeTime / 60 / 60, 0)) * 60, 0)}m {Math.Round(((batLifeTime / 60 / 60 - (int)(batLifeTime / 60 / 60)) * 60 - Math.Round((batLifeTime / 60 / 60 - Math.Round(batLifeTime / 60 / 60, 0)) * 60, 0)) * 60, 0)}s"
                        .Replace('-', char.MinValue);
                InfoBATUsage.Text = tbBAT.Text + " " + tbBATChargeRate.Text + "\n" + tbBATTime.Text;
                infoIBATUsageBigBanner.Text = InfoBATUsage.Text;
                infoABATUsageBannerPolygonText.Text = tbBATChargeRate.Text;
                infoABATUsageBigBannerPolygonText.Text = tbBATChargeRate.Text;
                tbBATState.Text = GetSystemInfo.GetBatteryStatus().ToString();
                var currBatRate = batteryRate >= 0 ? batteryRate : -1 * batteryRate;
                var beforeMaxBatRate = _maxBatRate;
                if (_maxBatRate < currBatRate)
                {
                    _maxBatRate = currBatRate;
                }

                if (RyzenAdjWrapper.Get_stapm_limit(_ryzenAccess) == 0)
                {
                    tbStapmL.Text = "Info_PowerSumInfo_Disabled".GetLocalized();
                }
                else
                {
                    tbStapmL.Text = Math.Round(RyzenAdjWrapper.Get_stapm_value(_ryzenAccess), 3) + "W/" +
                                    Math.Round(RyzenAdjWrapper.Get_stapm_limit(_ryzenAccess), 3) + "W";
                }

                tbActualL.Text = Math.Round(RyzenAdjWrapper.Get_fast_value(_ryzenAccess), 3) + "W/" +
                                 Math.Round(RyzenAdjWrapper.Get_fast_limit(_ryzenAccess), 3) + "W";
                tbAclualPowerL.Text = tbActualL.Text;
                if (RyzenAdjWrapper.Get_slow_limit(_ryzenAccess) == 0)
                {
                    tbAVGL.Text = "Info_PowerSumInfo_Disabled".GetLocalized();
                }
                else
                {
                    tbAVGL.Text = Math.Round(RyzenAdjWrapper.Get_slow_value(_ryzenAccess), 3) + "W/" +
                                  Math.Round(RyzenAdjWrapper.Get_slow_limit(_ryzenAccess), 3) + "W";
                }

                tbFast.Text = Math.Round(RyzenAdjWrapper.Get_stapm_time(_ryzenAccess), 3) + "S";
                tbSlow.Text = Math.Round(RyzenAdjWrapper.Get_slow_time(_ryzenAccess), 3) + "S";

                tbAPUL.Text = Math.Round(RyzenAdjWrapper.Get_apu_slow_value(_ryzenAccess), 3) + "W/" +
                              Math.Round(RyzenAdjWrapper.Get_apu_slow_limit(_ryzenAccess), 3) + "W";

                tbVRMTDCL.Text = Math.Round(RyzenAdjWrapper.Get_vrm_current_value(_ryzenAccess), 3) + "A/" +
                                 Math.Round(RyzenAdjWrapper.Get_vrm_current(_ryzenAccess), 3) + "A";
                tbSOCTDCL.Text = Math.Round(RyzenAdjWrapper.Get_vrmsoc_current_value(_ryzenAccess), 3) + "A/" +
                                 Math.Round(RyzenAdjWrapper.Get_vrmsoc_current(_ryzenAccess), 3) + "A";
                tbVRMEDCL.Text = Math.Round(RyzenAdjWrapper.Get_vrmmax_current_value(_ryzenAccess), 3) + "A/" +
                                 Math.Round(RyzenAdjWrapper.Get_vrmmax_current(_ryzenAccess), 3) + "A";
                tbVRMEDCVRML.Text = tbVRMEDCL.Text;
                infoVRMUsageBanner.Text = Math.Round(RyzenAdjWrapper.Get_vrmmax_current_value(_ryzenAccess), 3) +
                                          "A\n" + Math.Round(RyzenAdjWrapper.Get_fast_value(_ryzenAccess), 3) + "W";
                infoIVRMUsageBigBanner.Text = infoVRMUsageBanner.Text;
                infoAVRMUsageBannerPolygonText.Text =
                    Math.Round(RyzenAdjWrapper.Get_vrmmax_current_value(_ryzenAccess), 3) + "A";
                infoAVRMUsageBigBannerPolygonText.Text = infoAVRMUsageBannerPolygonText.Text;
                tbSOCEDCL.Text = Math.Round(RyzenAdjWrapper.Get_vrmsocmax_current_value(_ryzenAccess), 3) + "A/" +
                                 Math.Round(RyzenAdjWrapper.Get_vrmsocmax_current(_ryzenAccess), 3) + "A";
                tbSOCVOLT.Text = Math.Round(RyzenAdjWrapper.Get_soc_volt(_ryzenAccess), 3) == 0
                    ? Math.Round(_cpu!.powerTable.VDDCR_SOC, 3) + "V"
                    : Math.Round(RyzenAdjWrapper.Get_soc_volt(_ryzenAccess), 3) + "V";
                tbSOCPOWER.Text = Math.Round(RyzenAdjWrapper.Get_soc_power(_ryzenAccess), 3) == 0
                    ? Math.Round(_cpu!.powerTable.VDDCR_SOC * 10, 3) + "W"
                    : Math.Round(RyzenAdjWrapper.Get_soc_power(_ryzenAccess), 3) + "W";
                tbMEMCLOCK.Text = Math.Round(RyzenAdjWrapper.Get_mem_clk(_ryzenAccess), 3) == 0
                    ? Math.Round(_cpu!.powerTable.MCLK, 3) + "InfoFreqBoundsMHZ".GetLocalized()
                    : Math.Round(RyzenAdjWrapper.Get_mem_clk(_ryzenAccess), 3) + "InfoFreqBoundsMHZ".GetLocalized();
                tbFabricClock.Text = Math.Round(RyzenAdjWrapper.Get_fclk(_ryzenAccess), 3) == 0
                    ? Math.Round(_cpu!.powerTable.FCLK, 3) + "InfoFreqBoundsMHZ".GetLocalized()
                    : Math.Round(RyzenAdjWrapper.Get_fclk(_ryzenAccess), 3) + "InfoFreqBoundsMHZ".GetLocalized();
                var coreClk = 0f;
                var endtrace = 0;
                var coreVolt = 0f;
                var endtraced = 0;
                var maxFreq = 0.0d;
                var currentPstate = 4;
                for (uint f = 0; f < 16; f++)
                {
                    var getCurrFreq = RyzenAdjWrapper.Get_core_clk(_ryzenAccess, f);
                    if (!float.IsNaN(getCurrFreq) && getCurrFreq > maxFreq)
                    {
                        maxFreq = getCurrFreq;
                    }

                    var currCore = infoCPUSectionComboBox.SelectedIndex switch
                    {
                        0 => getCurrFreq,
                        1 => RyzenAdjWrapper.Get_core_volt(_ryzenAccess, f),
                        2 => RyzenAdjWrapper.Get_core_power(_ryzenAccess, f),
                        3 => RyzenAdjWrapper.Get_core_temp(_ryzenAccess, f),
                        _ => getCurrFreq
                    };
                    if (!float.IsNaN(currCore))
                    {
                        if (!InfoMainCPUFreqGrid.IsLoaded)
                        {
                            return;
                        }

                        var currText = (TextBlock)InfoMainCPUFreqGrid.FindName($"FreqButtonText_{f}");
                        if (currText != null)
                        {
                            if (_selectedGroup == 0 || _selectedGroup == 5)
                            {
                                currText.Text = infoCPUSectionComboBox.SelectedIndex switch
                                {
                                    0 => Math.Round(currCore, 3) + " " + "infoAGHZ".GetLocalized(),
                                    1 => Math.Round(currCore, 3) + "V",
                                    2 => Math.Round(currCore, 3) + "W",
                                    3 => Math.Round(currCore, 3) + "C",
                                    _ => Math.Round(currCore, 3) + " " + "infoAGHZ".GetLocalized()
                                };
                            }
                            else
                            {
                                if (_selectedGroup == 1)
                                {
                                    currText.Text = GetSystemInfo.GetGPUName((int)f);
                                }

                                if (_selectedGroup == 2)
                                {
                                    var reject = 0;
                                    foreach (var element in tbRAMModel.Text.Split('/'))
                                    {
                                        if (reject == (int)f)
                                        {
                                            currText.Text = element;
                                        }

                                        reject++;
                                    }
                                }

                                if (_selectedGroup == 3)
                                {
                                    currText.Text = f switch
                                    {
                                        0 =>
                                            $"{Math.Round(RyzenAdjWrapper.Get_vrmmax_current_value(_ryzenAccess), 3)}A/{Math.Round(RyzenAdjWrapper.Get_vrmmax_current(_ryzenAccess), 3)}A",
                                        1 =>
                                            $"{Math.Round(RyzenAdjWrapper.Get_vrm_current_value(_ryzenAccess), 3)}A/{Math.Round(RyzenAdjWrapper.Get_vrm_current(_ryzenAccess), 3)}A",
                                        2 =>
                                            $"{Math.Round(RyzenAdjWrapper.Get_vrmsocmax_current_value(_ryzenAccess), 3)}A/{Math.Round(RyzenAdjWrapper.Get_vrmsocmax_current(_ryzenAccess), 3)}A",
                                        3 =>
                                            $"{Math.Round(RyzenAdjWrapper.Get_vrmsoc_current_value(_ryzenAccess), 3)}A/{Math.Round(RyzenAdjWrapper.Get_vrmsoc_current(_ryzenAccess), 3)}A",
                                        _ => "0A"
                                    };
                                }

                                if (_selectedGroup == 4)
                                {
                                    currText.Text = _batName;
                                }
                            }
                        }

                        if (f < _numberOfCores)
                        {
                            if (getCurrFreq - -1.0f > 0 && getCurrFreq != 0 && getCurrFreq < 7)
                            {
                                coreClk += getCurrFreq;
                                endtrace += 1;
                            }
                        }
                    }

                    var currVolt = RyzenAdjWrapper.Get_core_volt(_ryzenAccess, f);
                    if (!float.IsNaN(currVolt) && currVolt != 0 && currVolt - -1.0f > 0 && currVolt < 1.7)
                    {
                        coreVolt += currVolt;
                        endtraced += 1;
                    }
                }

                if (endtrace != 0)
                {
                    tbCPUFreq.Text = Math.Round(coreClk / endtrace, 3) + " " + "infoAGHZ".GetLocalized();

                    if (Math.Round(coreClk / endtrace, 3) >= _psTatesList[2])
                    {
                        tbPST.Text = "P2";
                        infoAPSTUsageBannerPolygonText.Text = "P2";
                        infoAPSTUsageBigBannerPolygonText.Text = "P2";
                        currentPstate = 1;
                    }
                    else
                    {
                        tbPST.Text = "C1";
                        infoAPSTUsageBannerPolygonText.Text = "C1";
                        infoAPSTUsageBigBannerPolygonText.Text = "C1";
                        currentPstate = 0;
                    }

                    if (Math.Round(coreClk / endtrace, 3) >= _psTatesList[1])
                    {
                        tbPST.Text = "P1";
                        infoAPSTUsageBannerPolygonText.Text = "P1";
                        infoAPSTUsageBigBannerPolygonText.Text = "P1";
                        currentPstate = 2;
                    }

                    if (Math.Round(coreClk / endtrace, 3) >= _psTatesList[0])
                    {
                        tbPST.Text = "P0";
                        infoAPSTUsageBannerPolygonText.Text = "P0";
                        infoAPSTUsageBigBannerPolygonText.Text = "P0";
                        currentPstate = 3;
                    }

                    InfoPSTUsage.Text = tbPST.Text + "InfoPSTState".GetLocalized();
                    infoIPSTUsageBigBanner.Text = InfoPSTUsage.Text;
                }
                else
                {
                    tbCPUFreq.Text = "? " + "infoAGHZ".GetLocalized();
                }

                if (endtraced != 0)
                {
                    tbCPUVolt.Text = Math.Round(coreVolt / endtraced, 3) + "V";
                }
                else
                {
                    tbCPUVoltDesc.Visibility = Visibility.Collapsed;
                    tbCPUVolt.Visibility = Visibility.Collapsed;
                    tbCPUVolt.Text = "?V";
                }

                tbPSTFREQ.Text = tbCPUFreq.Text;
                var gfxClk = Math.Round(RyzenAdjWrapper.Get_gfx_clk(_ryzenAccess) / 1000, 3);
                var gfxVolt = Math.Round(RyzenAdjWrapper.Get_gfx_volt(_ryzenAccess), 3);
                var gfxTemp = RyzenAdjWrapper.Get_gfx_temp(_ryzenAccess);
                var beforeMaxGfx = _maxGfxClock;
                if (_maxGfxClock < gfxClk)
                {
                    _maxGfxClock = gfxClk;
                }

                infoGPUUsageBanner.Text = gfxClk + " " + "infoAGHZ".GetLocalized() + "  " + Math.Round(gfxTemp, 0) +
                                          "C\n" + gfxVolt + "V";
                infoAGPUUsageBannerPolygonText.Text = gfxClk + "infoAGHZ".GetLocalized();
                tbGPUFreq.Text = infoAGPUUsageBannerPolygonText.Text;
                infoAGPUUsageBigBannerPolygonText.Text = infoAGPUUsageBannerPolygonText.Text;
                infoIGPUUsageBigBanner.Text = infoGPUUsageBanner.Text;
                tbGPUVolt.Text = gfxVolt + "V";
                var maxTemp = Math.Round(RyzenAdjWrapper.Get_tctl_temp(_ryzenAccess), 3);
                tbCPUMaxL.Text = Math.Round(RyzenAdjWrapper.Get_tctl_temp_value(_ryzenAccess), 3) + "C/" + maxTemp +
                                 "C";
                tbCPUMaxTempL.Text = tbCPUMaxL.Text;
                tbCPUMaxTempVRML.Text = tbCPUMaxL.Text;
                var apuTemp = Math.Round(RyzenAdjWrapper.Get_apu_skin_temp_value(_ryzenAccess), 3);
                var apuTempLimit = Math.Round(RyzenAdjWrapper.Get_apu_skin_temp_limit(_ryzenAccess), 3);
                tbAPUMaxL.Text = (!double.IsNaN(apuTemp) && apuTemp > 0 ? apuTemp : Math.Round(gfxTemp, 3)) + "C/" +
                                 (!double.IsNaN(apuTempLimit) && apuTempLimit > 0 ? apuTempLimit : maxTemp) + "C";
                tbDGPUMaxL.Text = Math.Round(RyzenAdjWrapper.Get_dgpu_skin_temp_value(_ryzenAccess), 3) + "C/" +
                                  Math.Round(RyzenAdjWrapper.Get_dgpu_skin_temp_limit(_ryzenAccess), 3) + "C";
                var coreCpuUsage = Math.Round(RyzenAdjWrapper.Get_cclk_busy_value(_ryzenAccess), 3);
                tbCPUUsage.Text = coreCpuUsage + "%";
                infoACPUUsageBannerPolygonText.Text = Math.Round(coreCpuUsage, 0) + "%";
                infoICPUUsageBanner.Text = Math.Round(coreCpuUsage, 0) + "%  " + tbCPUFreq.Text + "\n" +
                                           (tbCPUVolt.Text != "?V" ? tbCPUVolt.Text : string.Empty);
                infoACPUUsageBigBannerPolygonText.Text = tbCPUUsage.Text;
                infoICPUUsageBigBanner.Text = infoICPUUsageBanner.Text;

                //InfoACPUBanner График
                InfoACPUBannerPolygon.Points.Remove(new Windows.Foundation.Point(0, 0));
                _cpuPointer.Add(new InfoPageCPUPoints { X = 60, Y = 48 - (int)(coreCpuUsage * 0.48) });
                if (CPUFlyout.IsOpen)
                {
                    InfoACPUBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(0, 0));
                    InfoACPUBigBannerPolygon.Points.Add(new Windows.Foundation.Point(60,
                        48 - (int)(coreCpuUsage * 0.48)));
                }

                InfoACPUBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 48 - (int)(coreCpuUsage * 0.48)));
                foreach (var element in _cpuPointer.ToList())
                {
                    if (element.X < 0)
                    {
                        _cpuPointer.Remove(element);
                        if (CPUFlyout.IsOpen)
                        {
                            InfoACPUBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                        }

                        InfoACPUBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                    }
                    else
                    {
                        if (CPUFlyout.IsOpen)
                        {
                            InfoACPUBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                        }

                        InfoACPUBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                        element.X -= 1;
                        InfoACPUBannerPolygon.Points.Add(new Windows.Foundation.Point(element.X, element.Y));
                        if (CPUFlyout.IsOpen)
                        {
                            InfoACPUBigBannerPolygon.Points.Add(new Windows.Foundation.Point(element.X, element.Y));
                        }
                    }
                }

                if (CPUFlyout.IsOpen)
                {
                    InfoACPUBigBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 49));
                }

                InfoACPUBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 49));


                //InfoAGPUBanner График
                InfoAGPUBannerPolygon.Points.Remove(new Windows.Foundation.Point(0, 0));
                _gpuPointer.Add(new InfoPageCPUPoints { X = 60, Y = 48 - (int)(gfxClk / _maxGfxClock * 48) });
                InfoAGPUBannerPolygon.Points.Add(new Windows.Foundation.Point(60,
                    48 - (int)(gfxClk / _maxGfxClock * 48)));
                if (GPUFlyout.IsOpen)
                {
                    InfoAGPUBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(0, 0));
                    InfoAGPUBigBannerPolygon.Points.Add(new Windows.Foundation.Point(60,
                        48 - (int)(gfxClk / _maxGfxClock * 48)));
                }

                foreach (var element in _gpuPointer.ToList())
                {
                    if (element.X < 0)
                    {
                        _gpuPointer.Remove(element);
                        InfoAGPUBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                        if (GPUFlyout.IsOpen)
                        {
                            InfoAGPUBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                        }
                    }
                    else
                    {
                        InfoAGPUBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                        if (GPUFlyout.IsOpen)
                        {
                            InfoAGPUBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                        }

                        element.X -= 1;
                        element.Y = (int)(element.Y * beforeMaxGfx / _maxGfxClock);
                        if (GPUFlyout.IsOpen)
                        {
                            InfoAGPUBigBannerPolygon.Points.Add(new Windows.Foundation.Point(element.X, element.Y));
                        }

                        InfoAGPUBannerPolygon.Points.Add(new Windows.Foundation.Point(element.X, element.Y));
                    }
                }

                if (GPUFlyout.IsOpen)
                {
                    InfoAGPUBigBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 49));
                }

                InfoAGPUBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 49));

                //InfoAVRMBanner График
                InfoAVRMBannerPolygon.Points.Remove(new Windows.Foundation.Point(0, 0));
                _vrmPointer.Add(new InfoPageCPUPoints
                {
                    X = 60,
                    Y = 48 - (int)(RyzenAdjWrapper.Get_vrmmax_current_value(_ryzenAccess) /
                        RyzenAdjWrapper.Get_vrmmax_current(_ryzenAccess) * 48)
                });
                if (VRMFlyout.IsOpen)
                {
                    InfoAVRMBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(0, 0));
                    InfoAVRMBigBannerPolygon.Points.Add(new Windows.Foundation.Point(60,
                        48 - (int)(RyzenAdjWrapper.Get_vrmmax_current_value(_ryzenAccess) /
                            RyzenAdjWrapper.Get_vrmmax_current(_ryzenAccess) * 48)));
                }

                InfoAVRMBannerPolygon.Points.Add(new Windows.Foundation.Point(60,
                    48 - (int)(RyzenAdjWrapper.Get_vrmmax_current_value(_ryzenAccess) /
                        RyzenAdjWrapper.Get_vrmmax_current(_ryzenAccess) * 48)));
                foreach (var element in _vrmPointer.ToList())
                {
                    if (element.X < 0)
                    {
                        _vrmPointer.Remove(element);
                        InfoAVRMBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                        if (VRMFlyout.IsOpen)
                        {
                            InfoAVRMBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                        }
                    }
                    else
                    {
                        if (VRMFlyout.IsOpen)
                        {
                            InfoAVRMBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                        }

                        InfoAVRMBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                        element.X -= 1;
                        if (VRMFlyout.IsOpen)
                        {
                            InfoAVRMBigBannerPolygon.Points.Add(new Windows.Foundation.Point(element.X, element.Y));
                        }

                        InfoAVRMBannerPolygon.Points.Add(new Windows.Foundation.Point(element.X, element.Y));
                    }
                }

                InfoAVRMBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 49));
                if (VRMFlyout.IsOpen)
                {
                    InfoAVRMBigBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 49));
                }

                //InfoAPSTBanner График
                InfoAPSTBannerPolygon.Points.Remove(new Windows.Foundation.Point(0, 0));
                _pstPointer.Add(new InfoPageCPUPoints { X = 60, Y = 48 - currentPstate * 16 });
                InfoAPSTBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 48 - currentPstate * 16));
                if (PSTFlyout.IsOpen)
                {
                    InfoAPSTBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(0, 0));
                    InfoAPSTBigBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 48 - currentPstate * 16));
                }

                foreach (var element in _pstPointer.ToList())
                {
                    if (element.X < 0)
                    {
                        _pstPointer.Remove(element);
                        InfoAPSTBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                        if (PSTFlyout.IsOpen)
                        {
                            InfoAPSTBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                        }
                    }
                    else
                    {
                        InfoAPSTBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                        if (PSTFlyout.IsOpen)
                        {
                            InfoAPSTBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                        }

                        element.X -= 1;
                        InfoAPSTBannerPolygon.Points.Add(new Windows.Foundation.Point(element.X, element.Y));
                        if (PSTFlyout.IsOpen)
                        {
                            InfoAPSTBigBannerPolygon.Points.Add(new Windows.Foundation.Point(element.X, element.Y));
                        }
                    }
                }

                if (PSTFlyout.IsOpen)
                {
                    InfoAPSTBigBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 49));
                }

                InfoAPSTBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 49));

                //InfoABATBanner График
                InfoABATBannerPolygon.Points.Remove(new Windows.Foundation.Point(0, 0));
                _batPointer.Add(new InfoPageCPUPoints { X = 60, Y = 48 - (int)(currBatRate / _maxBatRate * 48) });
                InfoABATBannerPolygon.Points.Add(new Windows.Foundation.Point(60,
                    48 - (int)(currBatRate / _maxBatRate * 48)));
                if (BATFlyout.IsOpen)
                {
                    InfoABATBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(0, 0));
                    InfoABATBigBannerPolygon.Points.Add(new Windows.Foundation.Point(60,
                        48 - (int)(currBatRate / _maxBatRate * 48)));
                }

                foreach (var element in _batPointer.ToList())
                {
                    if (element.X < 0)
                    {
                        _batPointer.Remove(element);
                        InfoABATBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                        if (BATFlyout.IsOpen)
                        {
                            InfoABATBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                        }
                    }
                    else
                    {
                        InfoABATBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                        if (BATFlyout.IsOpen)
                        {
                            InfoABATBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                        }

                        element.X -= 1;
                        element.Y = (int)(element.Y * beforeMaxBatRate / _maxBatRate);
                        InfoABATBannerPolygon.Points.Add(new Windows.Foundation.Point(element.X, element.Y));
                        if (BATFlyout.IsOpen)
                        {
                            InfoABATBigBannerPolygon.Points.Add(new Windows.Foundation.Point(element.X, element.Y));
                        }
                    }
                }

                if (BATFlyout.IsOpen)
                {
                    InfoABATBigBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 49));
                }

                InfoABATBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 49));
                try
                {
                    if (_ramUsageHelper.Count != 0)
                    {
                        var busyRam = _ramUsageHelper[0].BusyRam;
                        var usageResult = _ramUsageHelper[0].TotalRam;
                        if (busyRam != 0 && usageResult != 0)
                        {
                            InfoARAMBannerPolygon.Points.Remove(new Windows.Foundation.Point(0, 0));
                            _ramPointer.Add(new InfoPageCPUPoints
                                { X = 60, Y = 48 - (int)(busyRam * 100 / usageResult * 0.48) });
                            InfoARAMBannerPolygon.Points.Add(new Windows.Foundation.Point(60,
                                48 - (int)(busyRam * 100 / usageResult * 0.48)));
                            if (RAMFlyout.IsOpen)
                            {
                                InfoARAMBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(0, 0));
                                InfoARAMBigBannerPolygon.Points.Add(new Windows.Foundation.Point(60,
                                    48 - (int)(busyRam * 100 / usageResult * 0.48)));
                            }
                        }
                    }

                    foreach (var element in _ramPointer.ToList())
                    {
                        if (element.X < 0)
                        {
                            _ramPointer.Remove(element);
                            InfoARAMBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                            if (RAMFlyout.IsOpen)
                            {
                                InfoARAMBigBannerPolygon.Points.Remove(
                                    new Windows.Foundation.Point(element.X, element.Y));
                            }
                        }
                        else
                        {
                            if (RAMFlyout.IsOpen)
                            {
                                InfoARAMBigBannerPolygon.Points.Remove(
                                    new Windows.Foundation.Point(element.X, element.Y));
                            }

                            InfoARAMBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                            element.X -= 1;
                            InfoARAMBannerPolygon.Points.Add(new Windows.Foundation.Point(element.X, element.Y));
                            if (RAMFlyout.IsOpen)
                            {
                                InfoARAMBigBannerPolygon.Points.Add(new Windows.Foundation.Point(element.X, element.Y));
                            }
                        }
                    }

                    if (RAMFlyout.IsOpen)
                    {
                        InfoARAMBigBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 49));
                    }

                    InfoARAMBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 49));
                }
                catch (Exception ex)
                {
                    SendSmuCommand.TraceIt_TraceError(ex.ToString());
                }

                //Раз в шесть секунд обновляет состояние памяти 
                var ramUsage = await Task.Run(() =>
                {
                    var ramMonitor =
                        new ManagementObjectSearcher(
                            "SELECT TotalVisibleMemorySize,FreePhysicalMemory FROM Win32_OperatingSystem");
                    foreach (var objram in ramMonitor.Get().Cast<ManagementObject>())
                    {
                        var totalRam = Convert.ToDouble(objram["TotalVisibleMemorySize"]);
                        var busyRam = totalRam - Convert.ToDouble(objram["FreePhysicalMemory"]);
                        var usage = Math.Round(busyRam * 100 / totalRam, 0) + "%";

                        return new { Usage = usage, BusyRam = busyRam, TotalRam = totalRam }; // Анонимный тип
                    }

                    return new { Usage = "Unknown", BusyRam = 0.0, TotalRam = 0.0 }; // Значения по умолчанию
                });
                dynamic ramUsageResult = ramUsage;
                InfoRAMUsage.Text = ramUsageResult.Usage + "\n" + Math.Round(ramUsageResult.BusyRam / 1024 / 1024, 2) +
                                    "GB/" + Math.Round(ramUsageResult.TotalRam / 1024 / 1024, 1) + "GB";
                infoARAMUsageBannerPolygonText.Text = ramUsageResult.Usage;
                infoARAMUsageBigBannerPolygonText.Text = infoARAMUsageBannerPolygonText.Text;
                infoIRAMUsageBigBanner.Text = InfoRAMUsage.Text;
                //InfoARAMBanner График
                _ramUsageHelper.Clear();
                _ramUsageHelper.Add(new TotalBusyRam
                    { BusyRam = ramUsageResult.BusyRam, TotalRam = ramUsageResult.TotalRam });
            }

            if (infoCPUSectionGetInfoTypeComboBox.SelectedIndex == 0 && _ryzenAccess == 0x0)
            {
                await RyzenAdjWrapper.CpuFrequencyManager.AsyncWmiGetCoreFreq(_numberOfCores);
            }

            if (AppSettings.RTSSMetricsEnabled || AppSettings.NiIconsEnabled)
            {
                var (avgCoreClk, avgCoreVolt, endClkString) = CalculateCoreMetrics();
                if (AppSettings.RTSSMetricsEnabled)
                {
                    var replacements = GetReplacements(avgCoreClk, avgCoreVolt);

                    if (_rtssset.AdvancedCodeEditor.Contains("$cpu_clock_cycle$") &&
                        _rtssset.AdvancedCodeEditor.Contains("$cpu_clock_cycle_end$"))
                    {
                        var prefix = _rtssset.AdvancedCodeEditor.Split("$cpu_clock_cycle$")[0];
                        var suffix = _rtssset.AdvancedCodeEditor.Split("$cpu_clock_cycle_end$")[1];

                        _rtssLine = ReplacePlaceholders(prefix, replacements)
                                    + endClkString
                                    + ReplacePlaceholders(suffix, replacements);
                    }
                    else
                    {
                        _rtssLine = ReplacePlaceholders(_rtssset.AdvancedCodeEditor, replacements);
                    }

                    RtssHandler.ChangeOsdText(_rtssLine);
                }
                if (AppSettings.NiIconsEnabled)
                {
                    // Обновляем минимальные и максимальные значения
                    UpdateMinMaxValues(_niiconsMinMaxValues, 0, RyzenAdjWrapper.Get_stapm_value(_ryzenAccess));
                    UpdateMinMaxValues(_niiconsMinMaxValues, 1, RyzenAdjWrapper.Get_fast_value(_ryzenAccess));
                    UpdateMinMaxValues(_niiconsMinMaxValues, 2, RyzenAdjWrapper.Get_slow_value(_ryzenAccess));
                    UpdateMinMaxValues(_niiconsMinMaxValues, 3, RyzenAdjWrapper.Get_vrmmax_current_value(_ryzenAccess));
                    UpdateMinMaxValues(_niiconsMinMaxValues, 4, RyzenAdjWrapper.Get_tctl_temp_value(_ryzenAccess));
                    UpdateMinMaxValues(_niiconsMinMaxValues, 5, RyzenAdjWrapper.Get_cclk_busy_value(_ryzenAccess));
                    UpdateMinMaxValues(_niiconsMinMaxValues, 6, ConvertFromTextToFloat(tbCPUFreq.Text.Replace("infoAGHZ".GetLocalized(), string.Empty)));
                    UpdateMinMaxValues(_niiconsMinMaxValues, 7, ConvertFromTextToFloat(tbCPUVolt.Text.Replace("V", string.Empty)));
                    UpdateMinMaxValues(_niiconsMinMaxValues, 8, RyzenAdjWrapper.Get_gfx_clk(_ryzenAccess));
                    UpdateMinMaxValues(_niiconsMinMaxValues, 9, RyzenAdjWrapper.Get_gfx_temp(_ryzenAccess));
                    UpdateMinMaxValues(_niiconsMinMaxValues, 10, RyzenAdjWrapper.Get_gfx_volt(_ryzenAccess));

                    // Обновляем текст иконок
                    UpdateNiIconText("Settings_ni_Values_STAPM", RyzenAdjWrapper.Get_stapm_value(_ryzenAccess), "W", _niiconsMinMaxValues[0], "Settings_ni_Values_STAPM".GetLocalized());
                    UpdateNiIconText("Settings_ni_Values_Fast", RyzenAdjWrapper.Get_fast_value(_ryzenAccess), "W", _niiconsMinMaxValues[1], "Settings_ni_Values_Fast".GetLocalized());
                    UpdateNiIconText("Settings_ni_Values_Slow", RyzenAdjWrapper.Get_slow_value(_ryzenAccess), "W", _niiconsMinMaxValues[2], "Settings_ni_Values_Slow".GetLocalized());
                    UpdateNiIconText("Settings_ni_Values_VRMEDC", RyzenAdjWrapper.Get_vrmmax_current_value(_ryzenAccess), "A", _niiconsMinMaxValues[3], "Settings_ni_Values_VRMEDC".GetLocalized());
                    UpdateNiIconText("Settings_ni_Values_CPUTEMP", RyzenAdjWrapper.Get_tctl_temp_value(_ryzenAccess), "C", _niiconsMinMaxValues[4], "Settings_ni_Values_CPUTEMP".GetLocalized());
                    UpdateNiIconText("Settings_ni_Values_CPUUsage", RyzenAdjWrapper.Get_cclk_busy_value(_ryzenAccess), "%", _niiconsMinMaxValues[5], "Settings_ni_Values_CPUUsage".GetLocalized());
                    UpdateNiIconText("Settings_ni_Values_AVGCPUCLK", ConvertFromTextToFloat(tbCPUFreq.Text.Replace("infoAGHZ".GetLocalized(), string.Empty)), "GHz", _niiconsMinMaxValues[6], "Settings_ni_Values_AVGCPUCLK".GetLocalized());
                    UpdateNiIconText("Settings_ni_Values_AVGCPUVOLT", ConvertFromTextToFloat(tbCPUVolt.Text.Replace("V", string.Empty)), "V", _niiconsMinMaxValues[7], "Settings_ni_Values_AVGCPUVOLT".GetLocalized());
                    UpdateNiIconText("Settings_ni_Values_GFXCLK", RyzenAdjWrapper.Get_gfx_clk(_ryzenAccess), "MHz", _niiconsMinMaxValues[8], "Settings_ni_Values_GFXCLK".GetLocalized());
                    UpdateNiIconText("Settings_ni_Values_GFXTEMP", RyzenAdjWrapper.Get_gfx_temp(_ryzenAccess), "C", _niiconsMinMaxValues[9], "Settings_ni_Values_GFXTEMP".GetLocalized());
                    UpdateNiIconText("Settings_ni_Values_GFXVOLT", RyzenAdjWrapper.Get_gfx_volt(_ryzenAccess), "V", _niiconsMinMaxValues[10], "Settings_ni_Values_GFXVOLT".GetLocalized());
                }
            }
        }
        catch (Exception ex)
        {
            SendSmuCommand.TraceIt_TraceError(ex.ToString());
        }
    }

    #region Update Ni-Icons information voids

    private static void UpdateMinMaxValues(List<MinMax> minMaxValues, int index, float currentValue) // Сохраняет минимальные и максимальные значение в словарь
    {
        if (minMaxValues[index].Min == 0.0f)
        {
            minMaxValues[index].Min = currentValue;
        }

        minMaxValues[index].Max = Math.Max(minMaxValues[index].Max, currentValue);
        minMaxValues[index].Min = Math.Min(minMaxValues[index].Min, currentValue);
    }
    
    private void UpdateNiIconText(string key, float currentValue, string unit, MinMax minMaxValue, string description) // Обновляет текущее значение показателей на трей иконках
    {
        // Ограничение и округление текущего, минимального и максимального значений
        var currentValueText = Math.Round(currentValue, 3).ToString(CultureInfo.InvariantCulture);
        var minValueText = Math.Round(minMaxValue.Min, 3).ToString(CultureInfo.InvariantCulture);
        var maxValueText = Math.Round(minMaxValue.Max, 3).ToString(CultureInfo.InvariantCulture);

        
        var tooltip = $"Saku Overclock© -\nTrayMon\n{description}" +
                      "Settings_ni_Values_CurrentValue".GetLocalized() + currentValueText + unit; // Сам тултип

        
        var extendedTooltip = "Settings_ni_Values_MinValue".GetLocalized() + minValueText + unit +
                                   "Settings_ni_Values_MaxValue".GetLocalized() + maxValueText + unit; // Расширенная часть тултипа (минимум и максимум)

        Change_Ni_Icons_Text(key, currentValueText, tooltip, extendedTooltip);
    }

    #endregion

    #region Update RTSS Line information voids

    private static string ReplacePlaceholders(string input, Dictionary<string, string> replacements) // Меняет заголовки в соответствии со словарём
    {
        return replacements.Aggregate(input, (current, replacement) => current.Replace(replacement.Key, replacement.Value));
    }

    private Dictionary<string, string> GetReplacements(double avgCoreClk, double avgCoreVolt) // Словарь с элементами, которые нужно заменить
    {
        // Если кэшированное значение отсутствует, вычислить его
        _cachedSelectedProfileReplacement ??= ShellPage.SelectedProfile
            .Replace('а', 'a').Replace('м', 'm')
            .Replace('и', 'i').Replace('н', 'n')
            .Replace('М', 'M').Replace('у', 'u')
            .Replace('Э', 'E').Replace('о', 'o')
            .Replace('Б', 'B').Replace('л', 'l')
            .Replace('с', 'c').Replace('С', 'C')
            .Replace('р', 'r').Replace('т', 't')
            .Replace('ь', ' ');

        return new Dictionary<string, string>
        {
            { 
                "$SelectedProfile$", 
                _cachedSelectedProfileReplacement 
            },
            {
                "$stapm_value$",
                Math.Round(RyzenAdjWrapper.Get_stapm_value(_ryzenAccess), 3).ToString(CultureInfo.InvariantCulture)
            },
            {
                "$stapm_limit$",
                Math.Round(RyzenAdjWrapper.Get_stapm_limit(_ryzenAccess), 3).ToString(CultureInfo.InvariantCulture)
            },
            {
                "$fast_value$",
                Math.Round(RyzenAdjWrapper.Get_fast_value(_ryzenAccess), 3).ToString(CultureInfo.InvariantCulture)
            },
            {
                "$fast_limit$",
                Math.Round(RyzenAdjWrapper.Get_fast_limit(_ryzenAccess), 3).ToString(CultureInfo.InvariantCulture)
            },
            {
                "$slow_value$",
                Math.Round(RyzenAdjWrapper.Get_slow_value(_ryzenAccess), 3).ToString(CultureInfo.InvariantCulture)
            },
            {
                "$slow_limit$",
                Math.Round(RyzenAdjWrapper.Get_slow_limit(_ryzenAccess), 3).ToString(CultureInfo.InvariantCulture)
            },
            {
                "$vrmedc_value$",
                Math.Round(RyzenAdjWrapper.Get_vrmmax_current_value(_ryzenAccess), 3)
                    .ToString(CultureInfo.InvariantCulture)
            },
            {
                "$vrmedc_max$",
                Math.Round(RyzenAdjWrapper.Get_vrmmax_current(_ryzenAccess), 3).ToString(CultureInfo.InvariantCulture)
            },
            {
                "$cpu_temp_value$",
                Math.Round(RyzenAdjWrapper.Get_tctl_temp_value(_ryzenAccess), 3).ToString(CultureInfo.InvariantCulture)
            },
            {
                "$cpu_temp_max$",
                Math.Round(RyzenAdjWrapper.Get_tctl_temp(_ryzenAccess), 3).ToString(CultureInfo.InvariantCulture)
            },
            {
                "$cpu_usage$",
                Math.Round(RyzenAdjWrapper.Get_cclk_busy_value(_ryzenAccess), 3).ToString(CultureInfo.InvariantCulture)
            },
            {
                "$gfx_clock$",
                Math.Round(RyzenAdjWrapper.Get_gfx_clk(_ryzenAccess), 3).ToString(CultureInfo.InvariantCulture)
            },
            {
                "$gfx_volt$",
                Math.Round(RyzenAdjWrapper.Get_gfx_volt(_ryzenAccess), 3).ToString(CultureInfo.InvariantCulture)
            },
            {
                "$gfx_temp$",
                Math.Round(RyzenAdjWrapper.Get_gfx_temp(_ryzenAccess), 3).ToString(CultureInfo.InvariantCulture)
            },
            {
                "$average_cpu_clock$", Math.Round(avgCoreClk / _numberOfCores, 3).ToString(CultureInfo.InvariantCulture)
            },
            {
                "$average_cpu_voltage$",
                Math.Round(avgCoreVolt / _numberOfCores, 3).ToString(CultureInfo.InvariantCulture)
            }
        };
    }
    
    private (double avgCoreClk, double avgCoreVolt, string endClkString) CalculateCoreMetrics() // Считает средние значения напряжения и частоты процессора
    {
        var avgCoreClk = 0d;
        var avgCoreVolt = 0d;
        var endClkString = string.Empty;
        var pattern = @"\$cpu_clock_cycle\$(.*?)\$cpu_clock_cycle_end\$";
        var match = Regex.Match(_rtssset.AdvancedCodeEditor, pattern);

        for (var f = 0u; f < _numberOfCores; f++)
        {
            if (f < 8)
            {
                var clk = Math.Round(RyzenAdjWrapper.Get_core_clk(_ryzenAccess, f), 3);
                var volt = Math.Round(RyzenAdjWrapper.Get_core_volt(_ryzenAccess, f), 3);
                avgCoreClk += clk;
                avgCoreVolt += volt;

                if (_rtssset.AdvancedCodeEditor == "")
                {
                    RtssLoad();
                }

                endClkString += f > 3
                    ? "<Br>        "
                    : "" + match.Groups[1].Value
                        .Replace("$currCore$", f.ToString())
                        .Replace("$cpu_core_clock$", clk.ToString(CultureInfo.InvariantCulture))
                        .Replace("$cpu_core_voltage$", volt.ToString(CultureInfo.InvariantCulture));
            }
        }

        return (avgCoreClk, avgCoreVolt, endClkString);
    }
    
    private static float ConvertFromTextToFloat(string input) // Конвертирует из текста в числа с плавающей запятой
    {
        try
        {
            // Попробуем преобразовать строку в float
            _ = float.TryParse(input, out var result);
            return result;
        }
        catch
        {
            return 0f;
        }
    }

    #endregion
    

    // Автообновление информации 
    private void StartInfoUpdate() // Начать обновление информации
    {
        try
        {
            InfoACPUBannerPolygon.Points.Clear();
            InfoACPUBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            InfoACPUBigBannerPolygon.Points.Clear();
            InfoACPUBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            InfoAGPUBannerPolygon.Points.Clear();
            InfoAGPUBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            InfoAGPUBigBannerPolygon.Points.Clear();
            InfoAGPUBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            InfoARAMBannerPolygon.Points.Clear();
            InfoARAMBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            InfoARAMBigBannerPolygon.Points.Clear();
            InfoARAMBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            InfoAVRMBannerPolygon.Points.Clear();
            InfoAVRMBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            InfoAVRMBigBannerPolygon.Points.Clear();
            InfoAVRMBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            InfoABATBannerPolygon.Points.Clear();
            InfoABATBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            InfoABATBigBannerPolygon.Points.Clear();
            InfoABATBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            InfoAPSTBannerPolygon.Points.Clear();
            InfoAPSTBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            InfoAPSTBigBannerPolygon.Points.Clear();
            InfoAPSTBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            _ryzenAccess = RyzenAdjWrapper.Init_ryzenadj();
            _ = RyzenAdjWrapper.Init_Table(_ryzenAccess);
            _dispatcherTimer = new DispatcherTimer();
            _dispatcherTimer.Tick += (_, _) => UpdateInfoAsync();
            _dispatcherTimer.Interval = TimeSpan.FromMilliseconds(300);
            App.MainWindow.VisibilityChanged += Window_VisibilityChanged;
            _dispatcherTimer.Start();
        }
        catch (Exception ex)
        {
            SendSmuCommand.TraceIt_TraceError(ex.ToString());
        }
    }

    // Метод, который будет вызываться при скрытии/переключении страницы
    private void StopInfoUpdate()
    {
        _dispatcherTimer?.Stop();
    }

    #endregion

    #region Information builders

    private async Task InfoCpuSectionGridBuilder()
    {
        try
        {
            // Очищаем сетку перед построением
            InfoMainCPUFreqGrid.RowDefinitions.Clear();
            InfoMainCPUFreqGrid.ColumnDefinitions.Clear();

            // Получаем список видеокарт с помощью WMI
            var predictedGpuCount = await Task.Run(() =>
            {
                return new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_VideoController")
                    .Get()
                    .Cast<ManagementObject>()
                    .Where(element =>
                    {
                        var name = element["Name"]?.ToString() ?? string.Empty;
                        // Исключаем виртуальные видеокарты (например, Parsec)
                        return !name.Contains("Parsec", StringComparison.OrdinalIgnoreCase) &&
                               !name.Contains("virtual", StringComparison.OrdinalIgnoreCase);
                    })
                    .ToList(); // Преобразуем в список для удобства работы
            });

            // Количество видеокарт
            var gpuCounter = predictedGpuCount.Count;

            // Сохраняем текущее значение _numberOfLogicalProcessors для восстановления позже
            var backupNumberLogical = _numberOfLogicalProcessors;

            // Определяем количество элементов (coreCounter) в зависимости от выбранной секции
            var coreCounter = _selectedGroup switch
            {
                // Секция процессора или PStates
                0 or 5 => _numberOfCores > 2
                    ? _numberOfCores // Если ядер больше 2, используем количество ядер
                    : infoCPUSectionComboBox.SelectedIndex == 0
                        ? _numberOfLogicalProcessors // Если выбрано отображение частоты, используем логические процессоры
                        : _numberOfCores, // Иначе используем количество ядер
                // Секция GFX
                1 => gpuCounter, // Используем количество видеокарт
                // Секция RAM
                2 => tbRAMModel.Text.Split('/').Length, // Используем количество плат ОЗУ
                // Секция 3
                3 => 4, // Фиксированное значение для секции 3
                // Другие секции
                _ => 1 // По умолчанию 1 элемент
            };

            // Если количество ядер больше 2, обновляем _numberOfLogicalProcessors
            if (_numberOfCores > 2)
            {
                _numberOfLogicalProcessors = coreCounter;
            }

            // Определяем количество строк и столбцов для сетки
            var rowCount = (_numberOfLogicalProcessors / 2 > 4 ? 4 : _numberOfLogicalProcessors / 2);
            if (_numberOfLogicalProcessors % 2 != 0 || _numberOfLogicalProcessors == 2)
            {
                rowCount++; // Добавляем дополнительную строку, если количество элементов нечётное или равно 2
            }

            // Добавляем строки и столбцы в сетку
            for (var i = 0; i < rowCount; i++)
            {
                InfoMainCPUFreqGrid.RowDefinitions.Add(new RowDefinition());
                InfoMainCPUFreqGrid.ColumnDefinitions.Add(new ColumnDefinition());
            }

            // Восстанавливаем значение _numberOfLogicalProcessors
            _numberOfLogicalProcessors = backupNumberLogical;

            // Заполняем сетку кнопками
            for (var j = 0; j < InfoMainCPUFreqGrid.RowDefinitions.Count; j++)
            {
                for (var f = 0; f < InfoMainCPUFreqGrid.ColumnDefinitions.Count; f++)
                {
                    // Если элементы закончились, выходим из метода
                    if (coreCounter <= 0)
                    {
                        return;
                    }

                    // Определяем текущее ядро (или элемент) в зависимости от секции
                    var currCore = _selectedGroup switch
                    {
                        // Секция процессора или PStates
                        0 or 5 => _numberOfCores > 2
                            ? _numberOfCores - coreCounter // Если ядер больше 2, используем оставшиеся ядра
                            : infoCPUSectionComboBox.SelectedIndex == 0
                                ? _numberOfLogicalProcessors -
                                  coreCounter // Если выбрано отображение частоты, используем логические процессоры
                                : _numberOfCores - coreCounter, // Иначе используем оставшиеся ядра
                        // Секция GFX
                        1 => gpuCounter - coreCounter, // Используем оставшиеся видеокарты
                        // Секция RAM
                        2 => tbRAMModel.Text.Split('/').Length - coreCounter, // Используем оставшиеся платы ОЗУ
                        // Секция 3
                        3 => 4 - coreCounter, // Фиксированное значение для секции 3
                        // Другие секции
                        _ => 0 // По умолчанию 0
                    };

                    // Создаём кнопку для текущего элемента
                    var elementButton = CreateElementButton(currCore, _selectedGroup, _numberOfCores, tbRAMModel.Text);

                    // Добавляем кнопку в сетку
                    Grid.SetRow(elementButton, j);
                    Grid.SetColumn(elementButton, f);
                    InfoMainCPUFreqGrid.Children.Add(elementButton);

                    // Уменьшаем счётчик элементов
                    coreCounter--;
                }
            }
        }
        catch (Exception e)
        {
            // Логируем ошибку, если что-то пошло не так
            SendSmuCommand.TraceIt_TraceError(e.ToString());
        }
    }
    private Grid CreateElementButton(int currCore, int selectedGroup, int numberOfCores, string ramModelText) // Создать кнопки отображающие текущие показатели
    {
        var elementButton = new Grid
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(3, 3, 3, 3),
            Children =
            {
                new Button
                {
                    Shadow = new ThemeShadow(),
                    Translation = new Vector3(0, 0, 20),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Content = new Grid
                    {
                        VerticalAlignment = VerticalAlignment.Stretch,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Children =
                        {
                            new TextBlock
                            {
                                VerticalAlignment = VerticalAlignment.Center,
                                HorizontalAlignment = HorizontalAlignment.Left,
                                Text = currCore.ToString(),
                                FontWeight = new FontWeight(200)
                            },
                            new TextBlock
                            {
                                Text = "0.00 Ghz",
                                Name = $"FreqButtonText_{currCore}",
                                VerticalAlignment = VerticalAlignment.Center,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                FontWeight = new FontWeight(800)
                            },
                            new TextBlock
                            {
                                FontSize = 13,
                                Margin = new Thickness(3, -2, 0, 0),
                                VerticalAlignment = VerticalAlignment.Center,
                                HorizontalAlignment = HorizontalAlignment.Right,
                                Text = selectedGroup switch
                                {
                                    // Секция процессора или PStates
                                    0 or 5 => currCore < numberOfCores
                                        ? "InfoCPUCore".GetLocalized() // Если это ядро, используем "Ядро"
                                        : "InfoCPUThread".GetLocalized(), // Иначе используем "Поток"
                                    // Секция GFX
                                    1 => "InfoGPUName".GetLocalized(), // Используем "Видеокарта"
                                    // Секция RAM
                                    2 => ramModelText.Contains('*')
                                        ? ramModelText.Split('*')[1]
                                            .Replace("bit", "") // Если есть информация о разрядности, используем её
                                        : "64", // По умолчанию 64 бита
                                    // Секция 3
                                    3 => currCore switch
                                    {
                                        0 => "VRM EDC",
                                        1 => "VRM TDC",
                                        2 => "SoC EDC",
                                        _ => "SoC TDC"
                                    },
                                    // Другие секции
                                    _ => "InfoBatteryName".GetLocalized() // По умолчанию "Батарея"
                                },
                                FontWeight = new FontWeight(200)
                            }
                        }
                    }
                }
            }
        };

        return elementButton;
    }
    
    #endregion

    #endregion

    #region Event Handlers

    private void InfoCPUSectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        //0 - частота, 1 - напряжение, 2 - мощность, 3 - температуры
        if (!_loaded)
        {
            return;
        }

        InfoMainCPUFreqGrid.Children.Clear();
        _ = InfoCpuSectionGridBuilder();
    }

    private void CPUBannerButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGroup != 0)
        {
            if (CPUBannerButton.Shadow != new ThemeShadow())
            {
                CPUBannerButton.Shadow ??= new ThemeShadow();
                GPUBannerButton.Shadow = null;
                RAMBannerButton.Shadow = null;
                BATBannerButton.Shadow = null;
                PSTBannerButton.Shadow = null;
                VRMBannerButton.Shadow = null;
            }

            _selectedGroup = 0;
            CPUBannerButton.Background = _selectedBrush;
            CPUBannerButton.BorderBrush = _selectedBorderBrush;
            GPUBannerButton.Background = _transparentBrush;
            GPUBannerButton.BorderBrush = _transparentBrush;
            RAMBannerButton.Background = _transparentBrush;
            RAMBannerButton.BorderBrush = _transparentBrush;
            VRMBannerButton.Background = _transparentBrush;
            VRMBannerButton.BorderBrush = _transparentBrush;
            BATBannerButton.Background = _transparentBrush;
            BATBannerButton.BorderBrush = _transparentBrush;
            PSTBannerButton.Background = _transparentBrush;
            PSTBannerButton.BorderBrush = _transparentBrush;
            InfoMainCPUFreqGrid.Children.Clear();
            _ = InfoCpuSectionGridBuilder();
        }
    }

    private void GPUBannerButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGroup != 1)
        {
            if (GPUBannerButton.Shadow != new ThemeShadow())
            {
                CPUBannerButton.Shadow = null;
                GPUBannerButton.Shadow ??= new ThemeShadow();
                RAMBannerButton.Shadow = null;
                BATBannerButton.Shadow = null;
                PSTBannerButton.Shadow = null;
                VRMBannerButton.Shadow = null;
            }

            _selectedGroup = 1;
            CPUBannerButton.Background = _transparentBrush;
            CPUBannerButton.BorderBrush = _transparentBrush;
            GPUBannerButton.Background = _selectedBrush;
            GPUBannerButton.BorderBrush = _selectedBorderBrush;
            RAMBannerButton.Background = _transparentBrush;
            RAMBannerButton.BorderBrush = _transparentBrush;
            VRMBannerButton.Background = _transparentBrush;
            VRMBannerButton.BorderBrush = _transparentBrush;
            BATBannerButton.Background = _transparentBrush;
            BATBannerButton.BorderBrush = _transparentBrush;
            PSTBannerButton.Background = _transparentBrush;
            PSTBannerButton.BorderBrush = _transparentBrush;
            InfoMainCPUFreqGrid.Children.Clear();
            _ = InfoCpuSectionGridBuilder();
        }
    }

    private void RAMBannerButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGroup != 2)
        {
            if (RAMBannerButton.Shadow != new ThemeShadow())
            {
                CPUBannerButton.Shadow = null;
                GPUBannerButton.Shadow = null;
                RAMBannerButton.Shadow ??= new ThemeShadow();
                BATBannerButton.Shadow = null;
                PSTBannerButton.Shadow = null;
                VRMBannerButton.Shadow = null;
            }

            _selectedGroup = 2;
            CPUBannerButton.Background = _transparentBrush;
            CPUBannerButton.BorderBrush = _transparentBrush;
            GPUBannerButton.Background = _transparentBrush;
            GPUBannerButton.BorderBrush = _transparentBrush;
            RAMBannerButton.Background = _selectedBrush;
            RAMBannerButton.BorderBrush = _selectedBorderBrush;
            VRMBannerButton.Background = _transparentBrush;
            VRMBannerButton.BorderBrush = _transparentBrush;
            BATBannerButton.Background = _transparentBrush;
            BATBannerButton.BorderBrush = _transparentBrush;
            PSTBannerButton.Background = _transparentBrush;
            PSTBannerButton.BorderBrush = _transparentBrush;
            InfoMainCPUFreqGrid.Children.Clear();
            _ = InfoCpuSectionGridBuilder();
        }
    }

    private void VRMBannerButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGroup != 3)
        {
            if (VRMBannerButton.Shadow != new ThemeShadow())
            {
                CPUBannerButton.Shadow = null;
                GPUBannerButton.Shadow = null;
                RAMBannerButton.Shadow = null;
                BATBannerButton.Shadow = null;
                PSTBannerButton.Shadow = null;
                VRMBannerButton.Shadow ??= new ThemeShadow();
            }

            _selectedGroup = 3;
            CPUBannerButton.Background = _transparentBrush;
            CPUBannerButton.BorderBrush = _transparentBrush;
            GPUBannerButton.Background = _transparentBrush;
            GPUBannerButton.BorderBrush = _transparentBrush;
            RAMBannerButton.Background = _transparentBrush;
            RAMBannerButton.BorderBrush = _transparentBrush;
            VRMBannerButton.Background = _selectedBrush;
            VRMBannerButton.BorderBrush = _selectedBorderBrush;
            BATBannerButton.Background = _transparentBrush;
            BATBannerButton.BorderBrush = _transparentBrush;
            PSTBannerButton.Background = _transparentBrush;
            PSTBannerButton.BorderBrush = _transparentBrush;
            InfoMainCPUFreqGrid.Children.Clear();
            _ = InfoCpuSectionGridBuilder();
        }
    }

    private void BATBannerButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGroup != 4)
        {
            if (BATBannerButton.Shadow != new ThemeShadow())
            {
                CPUBannerButton.Shadow = null;
                GPUBannerButton.Shadow = null;
                RAMBannerButton.Shadow = null;
                BATBannerButton.Shadow ??= new ThemeShadow();
                PSTBannerButton.Shadow = null;
                VRMBannerButton.Shadow = null;
            }

            _selectedGroup = 4;
            CPUBannerButton.Background = _transparentBrush;
            CPUBannerButton.BorderBrush = _transparentBrush;
            GPUBannerButton.Background = _transparentBrush;
            GPUBannerButton.BorderBrush = _transparentBrush;
            RAMBannerButton.Background = _transparentBrush;
            RAMBannerButton.BorderBrush = _transparentBrush;
            VRMBannerButton.Background = _transparentBrush;
            VRMBannerButton.BorderBrush = _transparentBrush;
            BATBannerButton.Background = _selectedBrush;
            BATBannerButton.BorderBrush = _selectedBorderBrush;
            PSTBannerButton.Background = _transparentBrush;
            PSTBannerButton.BorderBrush = _transparentBrush;
            InfoMainCPUFreqGrid.Children.Clear();
            _ = InfoCpuSectionGridBuilder();
        }
    }

    private void PSTBannerButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGroup != 5)
        {
            if (PSTBannerButton.Shadow != new ThemeShadow())
            {
                CPUBannerButton.Shadow = null;
                GPUBannerButton.Shadow = null;
                RAMBannerButton.Shadow = null;
                BATBannerButton.Shadow = null;
                PSTBannerButton.Shadow ??= new ThemeShadow();
                VRMBannerButton.Shadow = null;
            }

            _selectedGroup = 5;
            CPUBannerButton.Background = _transparentBrush;
            CPUBannerButton.BorderBrush = _transparentBrush;
            GPUBannerButton.Background = _transparentBrush;
            GPUBannerButton.BorderBrush = _transparentBrush;
            RAMBannerButton.Background = _transparentBrush;
            RAMBannerButton.BorderBrush = _transparentBrush;
            VRMBannerButton.Background = _transparentBrush;
            VRMBannerButton.BorderBrush = _transparentBrush;
            BATBannerButton.Background = _transparentBrush;
            BATBannerButton.BorderBrush = _transparentBrush;
            PSTBannerButton.Background = _selectedBrush;
            PSTBannerButton.BorderBrush = _selectedBorderBrush;
            InfoMainCPUFreqGrid.Children.Clear();
            if (infoCPUSectionComboBox.SelectedIndex != 0)
            {
                infoCPUSectionComboBox.SelectedIndex = 0;
            }
            else
            {
                _ = InfoCpuSectionGridBuilder();
            }
        }
    }

    private async void InfoRTSSButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (infoRTSSButton.IsChecked == false)
            {
                RtssHandler.ResetOsdText();
            }
            else
            {
                Info_RTSSTeacherTip.IsOpen = true;
                await Task.Delay(3000);
                Info_RTSSTeacherTip.IsOpen = false;
            }

            if (!_loaded)
            {
                return;
            }
 
            AppSettings.RTSSMetricsEnabled = infoRTSSButton.IsChecked == true;
            AppSettings.SaveSettings();
        }
        catch  
        {
            //
        }
    }

    private void InfoNiIconsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded)
        {
            return;
        }

        if (infoNiIconsButton.IsChecked == true)
        {
            CreateNotifyIcons();
        }
        else
        {
            DisposeAllNotifyIcons();
        }
 
        AppSettings.NiIconsEnabled = infoNiIconsButton.IsChecked == true;
        AppSettings.SaveSettings();
    }

    private void CPUFlyout_Opening(object sender, object e)
    {
        InfoACPUBigBannerPolygon.Points.Clear();
        InfoACPUBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
        if (CPUFlyout.IsOpen)
        {
            InfoACPUBigBannerPolygon.Points.Clear();
            InfoACPUBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            CPU_Expand_FontIcon.Glyph = "\uF0D7";
            CPU_Expand_Button.Visibility = Visibility.Visible;
        }
        else
        {
            CPU_Expand_Button.Visibility = Visibility.Collapsed;
            CPU_Expand_FontIcon.Glyph = "\uF0D8";
        }
    }

    private void CPUBannerButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (CPUBannerButton.IsPointerOver)
        {
            CPU_Expand_FontIcon.Glyph = "\uF0D8";
            CPU_Expand_Button.Visibility = Visibility.Visible;
            VRM_Expand_Button.Visibility = Visibility.Collapsed;
            GPU_Expand_Button.Visibility = Visibility.Collapsed;
            RAM_Expand_Button.Visibility = Visibility.Collapsed;
            BAT_Expand_Button.Visibility = Visibility.Collapsed;
            PST_Expand_Button.Visibility = Visibility.Collapsed;
        }
        else
        {
            if (CPUFlyout.IsOpen)
            {
                CPU_Expand_Button.Visibility = Visibility.Visible;
                CPU_Expand_FontIcon.Glyph = "\uF0D7";
            }
            else
            {
                CPU_Expand_Button.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void VRMFlyout_Opening(object sender, object e)
    {
        InfoAVRMBigBannerPolygon.Points.Clear();
        InfoAVRMBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
        if (VRMFlyout.IsOpen)
        {
            InfoAVRMBigBannerPolygon.Points.Clear();
            InfoAVRMBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            VRM_Expand_FontIcon.Glyph = "\uF0D7";
            VRM_Expand_Button.Visibility = Visibility.Visible;
        }
        else
        {
            VRM_Expand_Button.Visibility = Visibility.Collapsed;
            VRM_Expand_FontIcon.Glyph = "\uF0D8";
        }
    }

    private void VRMBannerButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (VRMBannerButton.IsPointerOver)
        {
            VRM_Expand_FontIcon.Glyph = "\uF0D8";
            CPU_Expand_Button.Visibility = Visibility.Collapsed;
            VRM_Expand_Button.Visibility = Visibility.Visible;
            GPU_Expand_Button.Visibility = Visibility.Collapsed;
            RAM_Expand_Button.Visibility = Visibility.Collapsed;
            BAT_Expand_Button.Visibility = Visibility.Collapsed;
            PST_Expand_Button.Visibility = Visibility.Collapsed;
        }
        else
        {
            if (VRMFlyout.IsOpen)
            {
                VRM_Expand_Button.Visibility = Visibility.Visible;
                VRM_Expand_FontIcon.Glyph = "\uF0D7";
            }
            else
            {
                VRM_Expand_Button.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void GPUFlyout_Opening(object sender, object e)
    {
        InfoAGPUBigBannerPolygon.Points.Clear();
        InfoAGPUBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
        if (GPUFlyout.IsOpen)
        {
            InfoAGPUBigBannerPolygon.Points.Clear();
            InfoAGPUBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            GPU_Expand_FontIcon.Glyph = "\uF0D7";
            GPU_Expand_Button.Visibility = Visibility.Visible;
        }
        else
        {
            GPU_Expand_Button.Visibility = Visibility.Collapsed;
            GPU_Expand_FontIcon.Glyph = "\uF0D8";
        }
    }

    private void GPUBannerButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (GPUBannerButton.IsPointerOver)
        {
            GPU_Expand_FontIcon.Glyph = "\uF0D8";
            CPU_Expand_Button.Visibility = Visibility.Collapsed;
            VRM_Expand_Button.Visibility = Visibility.Collapsed;
            GPU_Expand_Button.Visibility = Visibility.Visible;
            RAM_Expand_Button.Visibility = Visibility.Collapsed;
            BAT_Expand_Button.Visibility = Visibility.Collapsed;
            PST_Expand_Button.Visibility = Visibility.Collapsed;
        }
        else
        {
            if (GPUFlyout.IsOpen)
            {
                GPU_Expand_Button.Visibility = Visibility.Visible;
                GPU_Expand_FontIcon.Glyph = "\uF0D7";
            }
            else
            {
                GPU_Expand_Button.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void RAMFlyout_Opening(object sender, object e)
    {
        InfoARAMBigBannerPolygon.Points.Clear();
        InfoARAMBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
        if (RAMFlyout.IsOpen)
        {
            InfoARAMBigBannerPolygon.Points.Clear();
            InfoARAMBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            RAM_Expand_FontIcon.Glyph = "\uF0D7";
            RAM_Expand_Button.Visibility = Visibility.Visible;
        }
        else
        {
            RAM_Expand_Button.Visibility = Visibility.Collapsed;
            RAM_Expand_FontIcon.Glyph = "\uF0D8";
        }
    }

    private void RAMBannerButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (RAMBannerButton.IsPointerOver)
        {
            RAM_Expand_FontIcon.Glyph = "\uF0D8";
            CPU_Expand_Button.Visibility = Visibility.Collapsed;
            VRM_Expand_Button.Visibility = Visibility.Collapsed;
            GPU_Expand_Button.Visibility = Visibility.Collapsed;
            RAM_Expand_Button.Visibility = Visibility.Visible;
            BAT_Expand_Button.Visibility = Visibility.Collapsed;
            PST_Expand_Button.Visibility = Visibility.Collapsed;
        }
        else
        {
            if (RAMFlyout.IsOpen)
            {
                RAM_Expand_Button.Visibility = Visibility.Visible;
                RAM_Expand_FontIcon.Glyph = "\uF0D7";
            }
            else
            {
                RAM_Expand_Button.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void BATFlyout_Opening(object sender, object e)
    {
        InfoABATBigBannerPolygon.Points.Clear();
        InfoABATBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
        if (BATFlyout.IsOpen)
        {
            InfoABATBigBannerPolygon.Points.Clear();
            InfoABATBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            BAT_Expand_FontIcon.Glyph = "\uF0D7";
            BAT_Expand_Button.Visibility = Visibility.Visible;
        }
        else
        {
            BAT_Expand_Button.Visibility = Visibility.Collapsed;
            BAT_Expand_FontIcon.Glyph = "\uF0D8";
        }
    }

    private void BATBannerButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (BATBannerButton.IsPointerOver)
        {
            BAT_Expand_FontIcon.Glyph = "\uF0D8";
            CPU_Expand_Button.Visibility = Visibility.Collapsed;
            VRM_Expand_Button.Visibility = Visibility.Collapsed;
            GPU_Expand_Button.Visibility = Visibility.Collapsed;
            RAM_Expand_Button.Visibility = Visibility.Collapsed;
            BAT_Expand_Button.Visibility = Visibility.Visible;
            PST_Expand_Button.Visibility = Visibility.Collapsed;
        }
        else
        {
            if (BATFlyout.IsOpen)
            {
                BAT_Expand_Button.Visibility = Visibility.Visible;
                BAT_Expand_FontIcon.Glyph = "\uF0D7";
            }
            else
            {
                BAT_Expand_Button.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void PSTFlyout_Opening(object sender, object e)
    {
        InfoAPSTBigBannerPolygon.Points.Clear();
        InfoAPSTBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
        if (PSTFlyout.IsOpen)
        {
            InfoAPSTBigBannerPolygon.Points.Clear();
            InfoAPSTBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            PST_Expand_FontIcon.Glyph = "\uF0D7";
            PST_Expand_Button.Visibility = Visibility.Visible;
        }
        else
        {
            PST_Expand_Button.Visibility = Visibility.Collapsed;
            PST_Expand_FontIcon.Glyph = "\uF0D8";
        }
    }

    private void PSTBannerButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (PSTBannerButton.IsPointerOver)
        {
            PST_Expand_FontIcon.Glyph = "\uF0D8";
            CPU_Expand_Button.Visibility = Visibility.Collapsed;
            VRM_Expand_Button.Visibility = Visibility.Collapsed;
            GPU_Expand_Button.Visibility = Visibility.Collapsed;
            RAM_Expand_Button.Visibility = Visibility.Collapsed;
            BAT_Expand_Button.Visibility = Visibility.Collapsed;
            PST_Expand_Button.Visibility = Visibility.Visible;
        }
        else
        {
            if (PSTFlyout.IsOpen)
            {
                PST_Expand_Button.Visibility = Visibility.Visible;
                PST_Expand_FontIcon.Glyph = "\uF0D7";
            }
            else
            {
                PST_Expand_Button.Visibility = Visibility.Collapsed;
            }
        }
    }

    #endregion
}