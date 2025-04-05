using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using H.NotifyIcon;
using Microsoft.UI.Dispatching;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.JsonContainers;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.ViewModels;
using Saku_Overclock.Views;
using Icon = System.Drawing.Icon;

namespace Saku_Overclock.Services;

public partial class BackgroundDataUpdater(IDataProvider dataProvider) : IBackgroundDataUpdater
{
    private readonly IDataProvider? _dataProvider = dataProvider;
    private CancellationTokenSource? _cts;
    private Task? _updateTask;
    private readonly IRtssSettingsService
        RtssSettings = App.GetService<IRtssSettingsService>(); // Конфиг с настройками модуля RTSS
    private string? _rtssLine; // Строка для вывода в модуль RTSS
    private string? _cachedSelectedProfileReplacement; // Кешированная замена имени профиля
    private readonly string _cachedAppVersion = ГлавнаяViewModel.GetVersion(); // Кешированная версия приложения
    private bool _isIconsCreated;

    private bool _isIconsUpdated; // Флаги, служащие подтверждением уничтожения или обновления информации на каких-либо страницах приложения в реальном времени
    private bool _isRtssUpdated;

    private readonly List<ИнформацияPage.MinMax> _niiconsMinMaxValues =
    [
        new(), new(), new(), new(), new(), new(), new(), new(), new(), new(), new()
    ]; // Лист для хранения минимальных и максимальных значений Ni-Icons

    private readonly Dictionary<string, TaskbarIcon>
        _trayIcons = []; // Хранилище включенных в данный момент иконок Ni-Icons

    // ReSharper disable once InconsistentNaming
    private readonly IAppSettingsService
        AppSettings = App.GetService<IAppSettingsService>(); // Настройки приложения

    public event EventHandler<SensorsInformation>? DataUpdated;
    private NiIconsSettings _niicons = new(); // Конфиг с настройками Ni-Icons

    private bool _batteryCached;
    private string? _cachedBatteryName;
    private string? _cachedBatteryCapacity;
    private string? _cachedBatteryCycles;
    private string? _cachedBatteryHealth;
    private bool _cachedBatteryUnavailable;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            AppSettings.LoadSettings();
            if (AppSettings.NiIconsEnabled && !_isIconsCreated)
            {
                CreateNotifyIcons();
            }

            RtssSettings.LoadSettings();
        }
        catch (Exception ex)
        {
            LogHelper.LogError($"BackgroundDataUpdater - UNABLE TO CREATE NOTIFY ICONS! {ex}");
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _updateTask = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var info = await _dataProvider!.GetDataAsync();
                    try
                    {
                        var batteryInfo = GetBatInfoAsync().Result;
                        var (batteryName, batteryPercent, batteryState, batteryHealth, batteryCycles, batteryCapacity,
                            chargeRate, notTrack, batteryLifeTime) = batteryInfo;
                        info.BatteryName = batteryName;
                        info.BatteryUnavailable = notTrack;
                        info.BatteryPercent = batteryPercent;
                        info.BatteryState = batteryState;
                        info.BatteryHealth = batteryHealth;
                        info.BatteryCycles = batteryCycles;
                        info.BatteryCapacity = batteryCapacity;
                        info.BatteryChargeRate = chargeRate;
                        info.BatteryLifeTime = batteryLifeTime;
                    }
                    catch
                    {
                        info.BatteryUnavailable = true;
                    }

                    try
                    {
                        var ramInfo = GetRamInfoAsync().Result;
                        var (totalRamGb, busyRamGb, usagePercent, usageString) = ramInfo;
                        info.RamTotal = totalRamGb;
                        info.RamBusy = busyRamGb;
                        info.RamUsagePercent = usagePercent;
                        info.RamUsage = usageString;
                    }
                    catch
                    {
                        // ignored
                    }

                    DataUpdated?.Invoke(this, info);
                    UpdateTrayMonAndRtss(info);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка обновления данных: {ex}");
                }

                try
                {
                    await Task.Delay(300, _cts.Token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }, _cts.Token);

        return _updateTask;
    }

    public void Stop()
    {
        if (_cts is { IsCancellationRequested: false })
        {
            _cts.Cancel();
            DisposeAllNotifyIcons();
        }
    }

    #region Update Battery information voids

    private async Task<(
        string BatteryName,
        string BatteryPercent,
        string BatteryState,
        string BatteryHealth,
        string BatteryCycles,
        string BatteryCapacity,
        string BatteryChargeRate,
        bool BatteryUnavailable,
        int BatteryLifeTime
        )> GetBatInfoAsync()
    {
        // Если данные о батарее помечены как недоступные, сразу возвращаем флаг и пустые строки
        if (_cachedBatteryUnavailable)
        {
            return (string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                true, 0);
        }

        try
        {
            bool notTrack;
            var batteryInfo = await Task.Run(() =>
            {
                // Получаем динамические (часто меняющиеся) параметры
                var batteryPercent = GetSystemInfo.GetBatteryPercent() + "%";
                var batteryState = GetSystemInfo.GetBatteryStatus().ToString();
                var chargeRate = $"{GetSystemInfo.GetBatteryRate() / 1000:0.##}W";

                // Получаем время работы батареи (не кэшируется)
                var batteryLifeTime = GetSystemInfo.GetBatteryLifeTime();

                // Переменные для кэшируемых значений
                string batteryName;
                string batteryCapacity;
                string batteryCycles;
                string batteryHealth;

                if (!_batteryCached)
                {
                    // Медленные операции – выполняются только при первом вызове
                    batteryHealth = $"{100 - GetSystemInfo.GetBatteryHealth() * 100:0.##}%";
                    batteryCycles = GetSystemInfo.GetBatteryCycle().ToString();

                    var fullChargeCapacity = GetSystemInfo.ReadFullChargeCapacity();
                    var designCapacity = GetSystemInfo.ReadDesignCapacity(out notTrack);
                    batteryCapacity = $"{fullChargeCapacity}mAh/{designCapacity}mAh";

                    batteryName = GetSystemInfo.GetBatteryName() ?? "Unknown";

                    // Сохраняем результаты для будущих вызовов
                    _cachedBatteryName = batteryName;
                    _cachedBatteryHealth = batteryHealth;
                    _cachedBatteryCycles = batteryCycles;
                    _cachedBatteryCapacity = batteryCapacity;
                    _cachedBatteryUnavailable = notTrack;
                    _batteryCached = true;
                }
                else
                {
                    // Используем ранее сохранённые данные
                    batteryName = _cachedBatteryName ?? "Unknown";
                    batteryHealth = _cachedBatteryHealth!;
                    batteryCycles = _cachedBatteryCycles!;
                    batteryCapacity = _cachedBatteryCapacity!;
                    notTrack = _cachedBatteryUnavailable;
                }

                return (batteryName, batteryPercent, batteryState, batteryHealth, batteryCycles, batteryCapacity,
                    chargeRate, notTrack, batteryLifeTime);
            });

            return batteryInfo;
        }
        catch
        {
            // В случае ошибки возвращаем пустые строки и флаг недоступности true
            return (string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                true, 0);
        }
    }

    #endregion

    #region Update RAM information voids

    private async Task<(
        string RamTotal,
        string RamBusy,
        int RamUsagePercent,
        string RamUsage
        )> GetRamInfoAsync()
    {
        try
        {
            var ramInfo = await Task.Run(() =>
            {
                var ramMonitor = new ManagementObjectSearcher(
                    "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
                foreach (var objram in ramMonitor.Get().Cast<ManagementObject>())
                {
                    var totalRam = Convert.ToDouble(objram["TotalVisibleMemorySize"]);
                    var busyRam = totalRam - Convert.ToDouble(objram["FreePhysicalMemory"]);
                    var usagePercent = (int)Math.Round(busyRam * 100 / totalRam, 0);
                    var totalRamGb = Math.Round(totalRam / 1024 / 1024, 1) + "GB"; // Преобразуем в GB
                    var busyRamGb = Math.Round(busyRam / 1024 / 1024, 2) + "GB";
                    var usageString = $"{usagePercent}%\n{busyRamGb}/{totalRamGb}";

                    return (totalRamGb, busyRamGb, usagePercent, usageString);
                }

                return ("Unknown", "Unknown", 0, "Unknown"); // Значения по умолчанию в случае ошибки
            });

            return ramInfo;
        }
        catch
        {
            return ("Error", "Error", 0, "Error");
        }
    }

    #endregion

    #region Update Ni-Icons information voids

    #region JSON

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

    private void UpdateTrayMonAndRtss(SensorsInformation sensorsInformation)
    {
        try
        {
            if (AppSettings.RTSSMetricsEnabled || AppSettings.NiIconsEnabled)
            {
                var (avgCoreClk, avgCoreVolt, endClkString) = CalculateCoreMetrics(sensorsInformation);
                if (AppSettings.RTSSMetricsEnabled)
                {
                    _isRtssUpdated = true;
                    var replacements = GetReplacements(avgCoreClk, avgCoreVolt, sensorsInformation);

                    if (RtssSettings.AdvancedCodeEditor.Contains("$cpu_clock_cycle$") &&
                        RtssSettings.AdvancedCodeEditor.Contains("$cpu_clock_cycle_end$"))
                    {
                        var prefix = RtssSettings.AdvancedCodeEditor.Split("$cpu_clock_cycle$")[0];
                        var suffix = RtssSettings.AdvancedCodeEditor.Split("$cpu_clock_cycle_end$")[1];

                        _rtssLine = ReplacePlaceholders(prefix, replacements)
                                    + endClkString
                                    + ReplacePlaceholders(suffix, replacements);
                    }
                    else
                    {
                        _rtssLine = ReplacePlaceholders(RtssSettings.AdvancedCodeEditor, replacements);
                    }

                    RtssHandler.ChangeOsdText(_rtssLine);
                }

                if (AppSettings.NiIconsEnabled)
                {
                    _isIconsUpdated = true;
                    if (!_isIconsCreated)
                    {
                        CreateNotifyIcons();
                    }

                    // Обновляем минимальные и максимальные значения
                    UpdateMinMaxValues(_niiconsMinMaxValues, 0, sensorsInformation.CpuStapmValue);
                    UpdateMinMaxValues(_niiconsMinMaxValues, 1, sensorsInformation.CpuFastValue);
                    UpdateMinMaxValues(_niiconsMinMaxValues, 2, sensorsInformation.CpuSlowValue);
                    UpdateMinMaxValues(_niiconsMinMaxValues, 3, sensorsInformation.VrmEdcValue);
                    UpdateMinMaxValues(_niiconsMinMaxValues, 4, sensorsInformation.CpuTempValue);
                    UpdateMinMaxValues(_niiconsMinMaxValues, 5, sensorsInformation.CpuUsage);
                    UpdateMinMaxValues(_niiconsMinMaxValues, 6, sensorsInformation.CpuFrequency);
                    UpdateMinMaxValues(_niiconsMinMaxValues, 7, sensorsInformation.CpuVoltage);
                    UpdateMinMaxValues(_niiconsMinMaxValues, 8, sensorsInformation.ApuFrequency);
                    UpdateMinMaxValues(_niiconsMinMaxValues, 9, sensorsInformation.ApuTemperature);
                    UpdateMinMaxValues(_niiconsMinMaxValues, 10, sensorsInformation.ApuVoltage);
                    DispatcherQueue.GetForCurrentThread();
                    App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                    {
                        // Обновляем текст иконок
                        UpdateNiIconText("Settings_ni_Values_STAPM", sensorsInformation.CpuStapmValue, "W",
                            _niiconsMinMaxValues[0], "Settings_ni_Values_STAPM".GetLocalized());
                        UpdateNiIconText("Settings_ni_Values_Fast", sensorsInformation.CpuFastValue, "W",
                            _niiconsMinMaxValues[1], "Settings_ni_Values_Fast".GetLocalized());
                        UpdateNiIconText("Settings_ni_Values_Slow", sensorsInformation.CpuSlowValue, "W",
                            _niiconsMinMaxValues[2], "Settings_ni_Values_Slow".GetLocalized());
                        UpdateNiIconText("Settings_ni_Values_VRMEDC", sensorsInformation.VrmEdcValue, "A",
                            _niiconsMinMaxValues[3], "Settings_ni_Values_VRMEDC".GetLocalized());
                        UpdateNiIconText("Settings_ni_Values_CPUTEMP", sensorsInformation.CpuTempValue, "C",
                            _niiconsMinMaxValues[4], "Settings_ni_Values_CPUTEMP".GetLocalized());
                        UpdateNiIconText("Settings_ni_Values_CPUUsage", sensorsInformation.CpuUsage, "%",
                            _niiconsMinMaxValues[5], "Settings_ni_Values_CPUUsage".GetLocalized());
                        UpdateNiIconText("Settings_ni_Values_AVGCPUCLK", sensorsInformation.CpuFrequency, "GHz",
                            _niiconsMinMaxValues[6], "Settings_ni_Values_AVGCPUCLK".GetLocalized());
                        UpdateNiIconText("Settings_ni_Values_AVGCPUVOLT", sensorsInformation.CpuVoltage, "V",
                            _niiconsMinMaxValues[7], "Settings_ni_Values_AVGCPUVOLT".GetLocalized());
                        UpdateNiIconText("Settings_ni_Values_GFXCLK", sensorsInformation.ApuFrequency, "MHz",
                            _niiconsMinMaxValues[8], "Settings_ni_Values_GFXCLK".GetLocalized());
                        UpdateNiIconText("Settings_ni_Values_GFXTEMP", sensorsInformation.ApuTemperature, "C",
                            _niiconsMinMaxValues[9], "Settings_ni_Values_GFXTEMP".GetLocalized());
                        UpdateNiIconText("Settings_ni_Values_GFXVOLT", sensorsInformation.ApuVoltage, "V",
                            _niiconsMinMaxValues[10], "Settings_ni_Values_GFXVOLT".GetLocalized());
                    });
                }
            }

            if (_isRtssUpdated && !AppSettings.RTSSMetricsEnabled) { RtssHandler.ResetOsdText(); _isRtssUpdated = false; }
            if (_isIconsUpdated && !AppSettings.NiIconsEnabled) { DisposeAllNotifyIcons(); _isIconsUpdated = false; _isIconsCreated = false; }
        }
        catch (Exception ex)
        {
            LogHelper.LogError($"Unable to update RTSS or TrayMon\u00a9: {ex}");
        }
    }

    private static void
        UpdateMinMaxValues(List<ИнформацияPage.MinMax> minMaxValues, int index,
            double currentValue) // Сохраняет минимальные и максимальные значение в словарь
    {
        if (minMaxValues[index].Min == 0.0d)
        {
            minMaxValues[index].Min = currentValue;
        }

        minMaxValues[index].Max = Math.Max(minMaxValues[index].Max, currentValue);
        minMaxValues[index].Min = Math.Min(minMaxValues[index].Min, currentValue);
    }

    private void UpdateNiIconText(string key, double currentValue, string unit, ИнформацияPage.MinMax minMaxValue,
        string description) // Обновляет текущее значение показателей на трей иконках
    {
        // Ограничение и округление текущего, минимального и максимального значений
        var currentValueText = Math.Round(currentValue, 3).ToString(CultureInfo.InvariantCulture);
        var minValueText = Math.Round(minMaxValue.Min, 3).ToString(CultureInfo.InvariantCulture);
        var maxValueText = Math.Round(minMaxValue.Max, 3).ToString(CultureInfo.InvariantCulture);


        var tooltip = $"Saku Overclock© -\nTrayMon\n{description}" +
                      "Settings_ni_Values_CurrentValue".GetLocalized() + currentValueText + unit; // Сам тултип


        var extendedTooltip = "Settings_ni_Values_MinValue".GetLocalized() + minValueText + unit +
                              "Settings_ni_Values_MaxValue".GetLocalized() + maxValueText +
                              unit; // Расширенная часть тултипа (минимум и максимум)

        Change_Ni_Icons_Text(key, currentValueText, tooltip, extendedTooltip);
    }

    #endregion

    #region Update RTSS Line information voids

    private static string
        ReplacePlaceholders(string input,
            Dictionary<string, string> replacements) // Меняет заголовки в соответствии со словарём
        =>
            replacements.Aggregate(input,
                (current, replacement) => current.Replace(replacement.Key, replacement.Value));

    private Dictionary<string, string> GetReplacements(double avgCoreClk, double avgCoreVolt,
        SensorsInformation sensorsInformation) // Словарь с элементами, которые нужно заменить
    {
        var profileName = ShellPage.SelectedProfile
            .Replace('а', 'a').Replace('м', 'm')
            .Replace('и', 'i').Replace('н', 'n')
            .Replace('М', 'M').Replace('у', 'u')
            .Replace('Э', 'E').Replace('о', 'o')
            .Replace('Б', 'B').Replace('л', 'l')
            .Replace('с', 'c').Replace('С', 'C')
            .Replace('р', 'r').Replace('т', 't')
            .Replace('ь', ' ');
        // Если кэшированное значение отсутствует, вычислить его
        _cachedSelectedProfileReplacement = _cachedSelectedProfileReplacement != profileName
            ? profileName
            : _cachedSelectedProfileReplacement;

        return new Dictionary<string, string>
        {
            {
                "$AppVersion$",
                _cachedAppVersion
            },
            {
                "$SelectedProfile$",
                _cachedSelectedProfileReplacement
            },
            {
                "$stapm_value$",
                Math.Round(sensorsInformation.CpuStapmValue, 3).ToString(CultureInfo.InvariantCulture)
            },
            {
                "$stapm_limit$",
                Math.Round(sensorsInformation.CpuStapmLimit, 3).ToString(CultureInfo.InvariantCulture)
            },
            {
                "$fast_value$",
                Math.Round(sensorsInformation.CpuFastValue, 3).ToString(CultureInfo.InvariantCulture)
            },
            {
                "$fast_limit$",
                Math.Round(sensorsInformation.CpuFastLimit, 3).ToString(CultureInfo.InvariantCulture)
            },
            {
                "$slow_value$",
                Math.Round(sensorsInformation.CpuSlowValue, 3).ToString(CultureInfo.InvariantCulture)
            },
            {
                "$slow_limit$",
                Math.Round(sensorsInformation.CpuSlowLimit, 3).ToString(CultureInfo.InvariantCulture)
            },
            {
                "$vrmedc_value$",
                Math.Round(sensorsInformation.VrmEdcValue, 3)
                    .ToString(CultureInfo.InvariantCulture)
            },
            {
                "$vrmedc_max$",
                Math.Round(sensorsInformation.VrmEdcLimit, 3).ToString(CultureInfo.InvariantCulture)
            },
            {
                "$cpu_temp_value$",
                Math.Round(sensorsInformation.CpuTempValue, 3).ToString(CultureInfo.InvariantCulture)
            },
            {
                "$cpu_temp_max$",
                Math.Round(sensorsInformation.CpuTempLimit, 3).ToString(CultureInfo.InvariantCulture)
            },
            {
                "$cpu_usage$",
                Math.Round(sensorsInformation.CpuUsage, 3).ToString(CultureInfo.InvariantCulture)
            },
            {
                "$gfx_clock$",
                Math.Round(sensorsInformation.ApuFrequency, 3).ToString(CultureInfo.InvariantCulture)
            },
            {
                "$gfx_volt$",
                Math.Round(sensorsInformation.ApuVoltage, 3).ToString(CultureInfo.InvariantCulture)
            },
            {
                "$gfx_temp$",
                Math.Round(sensorsInformation.ApuTemperature, 3).ToString(CultureInfo.InvariantCulture)
            },
            {
                "$average_cpu_clock$", Math.Round(avgCoreClk, 3).ToString(CultureInfo.InvariantCulture)
            },
            {
                "$average_cpu_voltage$",
                Math.Round(avgCoreVolt, 3).ToString(CultureInfo.InvariantCulture)
            }
        };
    }

    private (double avgCoreClk, double avgCoreVolt, string endClkString) CalculateCoreMetrics(
        SensorsInformation sensorsInformation)
    {
        double sumCoreClk = 0;
        double sumCoreVolt = 0;
        var validCoreCount = 0;
        var endClkString = string.Empty;

        if (string.IsNullOrEmpty(RtssSettings.AdvancedCodeEditor))
        {
            RtssSettings.LoadSettings();
        }

        var match = ClockCycleRegex().Match(RtssSettings.AdvancedCodeEditor);

        for (uint f = 0; f < CpuSingleton.GetInstance().info.topology.cores; f++)
        {
            if (f >= 8)
            {
                break;
            }

            var clk = Math.Round(
                sensorsInformation.CpuFrequencyPerCore != null
                    ? sensorsInformation.CpuFrequencyPerCore.Length > f ? sensorsInformation.CpuFrequencyPerCore[f] : 0f
                    : 0f, 3);
            var volt = Math.Round(
                sensorsInformation.CpuVoltagePerCore != null
                    ? sensorsInformation.CpuVoltagePerCore.Length > f ? sensorsInformation.CpuVoltagePerCore[f] : 0f
                    : 0f, 3);

            if (clk > 0 && volt > 0) // Исключаем нули и -1
            {
                sumCoreClk += clk;
                sumCoreVolt += volt;
                validCoreCount++;
            }

            if (match.Success)
            {
                endClkString += (f > 3 ? "<Br>        " : "") +
                                match.Groups[1].Value
                                    .Replace("$currCore$", f.ToString())
                                    .Replace("$cpu_core_clock$", clk.ToString(CultureInfo.InvariantCulture))
                                    .Replace("$cpu_core_voltage$", volt.ToString(CultureInfo.InvariantCulture));
            }
        }

        var avgCoreClk = validCoreCount > 0 ? sumCoreClk / validCoreCount : 0;
        var avgCoreVolt = validCoreCount > 0 ? sumCoreVolt / validCoreCount : 0;

        return (avgCoreClk, avgCoreVolt, endClkString);
    }

    [GeneratedRegex(@"\$cpu_clock_cycle\$(.*?)\$cpu_clock_cycle_end\$")]
    private static partial Regex ClockCycleRegex();

    #region Ni-Icons

    private void DisposeAllNotifyIcons()
    {
        // Перебираем все иконки и вызываем Dispose для каждой из них
        foreach (var icon in _trayIcons.Values)
        {
            App.MainWindow.DispatcherQueue.TryEnqueue(icon.Dispose);
        }

        // Очищаем коллекцию иконок
        _trayIcons.Clear();
    }

    private void CreateNotifyIcons()
    {
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            //DisposeAllNotifyIcons(); // Уничтожаем старые иконки перед созданием новых

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
        });
        _isIconsCreated = true;
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
        var bgSecColor = ColorTranslator.FromHtml("#" + element.SecondColor);
        var bgBrush = new LinearGradientBrush(
            new Rectangle(0, 0, 32, 32),
            Color.FromArgb((int)(element.BgOpacity * 255), bgColor),
            Color.FromArgb((int)(element.BgOpacity * 255), bgSecColor),
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
            var font = new Font(new FontFamily("Arial"), element.FontSize * 2, FontStyle.Regular,
                GraphicsUnit.Pixel);
            var textBrush =
                new SolidBrush(GetContrastColor(element.Color, element.IsGradient ? element.SecondColor : null));
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
                g.FillRectangle((Brush)bgBrush, 0, 0, 32, 32);
                break;
            case 1: // Скруглённый куб
                var path = CreateRoundedRectanglePath(new Rectangle(0, 0, 32, 32), 7);
                if (path != null)
                {
                    g.FillPath((Brush)bgBrush, path);
                }
                else
                {
                    g.FillRectangle((Brush)bgBrush, 0, 0, 32, 32);
                }

                break;
            case 2: // Круг
                g.FillEllipse((Brush)bgBrush, 0, 0, 32, 32);
                break;
            // Добавьте остальные фигуры и обработку ico при необходимости
            default:
                g.FillRectangle((Brush)bgBrush, 0, 0, 32, 32);
                break;
        }

        // Определение позиции текста

        var textBrush = new SolidBrush(GetContrastColor(newColor, secondColor != string.Empty ? secondColor : null));
        var textPosition = GetTextPosition(newText, fontSize, out var fontSizeT, out var newTextT);
        var font = new Font(new FontFamily("Segoe UI"), fontSizeT * 2f, FontStyle.Bold,
            GraphicsUnit.Pixel);
        // Рисуем текст
        g.DrawString(newTextT, font, textBrush, textPosition);
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

    ///<summary> Метод для освобождения ресурсов, используемый после GetHicon() </summary>
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>
    ///     Получить позицию текста и доработанный текст, на основе предугадывания позиции и готовых функций на основе
    ///     датасета всех возможных вариантов размера шрифта
    /// </summary>
    private static PointF GetTextPosition(string? newText, float fontSize, out float newFontSize,
        out string? newFixedText)
    {
        var yPosition =
            -1.475f * fontSize +
            16.2f; // Готовая "скомпилированная" функция, основанная на массиве данных, собранные на всех возможных размерах шрифта
        newFixedText = newText;
        var xPos = 20f;
        if (!newText!.Contains('.'))
        {
            newText += ".0";
        }

        if (!string.IsNullOrEmpty(newText) && newText.Contains('.'))
        {
            var parts = newText.Split('.');
            var wholePartLength = parts[0].Length;
            switch
                (wholePartLength) // TrayMon© - Разработка от Erruar, поэтому вам не стоит разбираться в том, как она работает. Все значения были скомпилированы в функции при помощи NumPy
            {
                case 1:
                    var offset1 = (int)fontSize == 14 ? 3.3f : (int)fontSize == 13 ? -3.3f : 0f;
                    xPos = -0.0715488215f * fontSize * fontSize * fontSize
                        + 2.83311688f * fontSize * fontSize
                        - 35.2581049f * fontSize + 135.071284f
                                                 + offset1;
                    newFixedText = fontSize > 13 ? parts[0] : newText;
                    break;
                case 2:
                    var offset2 = (int)fontSize == 10 ? 2.17329f : (int)fontSize == 9 ? -2.17329f : 0f;
                    xPos = 0.0614478114f * fontSize * fontSize * fontSize
                           - 2.48160173f * fontSize * fontSize
                           + 31.8379028f * fontSize - 132.756133f
                           + offset2;
                    newFixedText = fontSize > 9 ? parts[0] : newText;
                    break;
                case 3:
                    fontSize = fontSize > 12 ? 12 : fontSize;
                    xPos = 0.33333333f * fontSize * fontSize * fontSize
                        - 10.07142857f * fontSize * fontSize
                        + 98.5952381f * fontSize - 316.8f;
                    yPosition = -1.475f * fontSize + 16.2f;
                    break;
                case > 3:
                    fontSize = fontSize > 12 ? 12 : fontSize - 2;
                    xPos = 0.00378787879f * fontSize * fontSize * fontSize
                        - 0.00487012987f * fontSize * fontSize
                        - 2.32251082f * fontSize + 14.982684f;
                    yPosition = -1.475f * fontSize + 16.2f;
                    break;
                default:
                    xPos = 0f;
                    break;
            }
        }

        newFontSize = fontSize;
        var position = new PointF(xPos, yPosition);
        return position;
    }

    /// <summary> Функция для определения яркости цвета</summary>
    private static double GetBrightness(string color)
    {
        var valuestring = color.TrimStart('#');
        var r = Convert.ToInt32(valuestring[..2], 16);
        var g = Convert.ToInt32(valuestring.Substring(2, 2), 16);
        var b = Convert.ToInt32(valuestring.Substring(4, 2), 16);
        return 0.299 * r + 0.587 * g + 0.114 * b;
    }

    /// <summary> Функция для определения контрастного цвета текста по фону текста</summary>
    private static Color GetContrastColor(string color1, string? color2 = null)
    {
        var brightness1 = GetBrightness(color1);

        double? brightness2 = null;
        if (!string.IsNullOrEmpty(color2))
        {
            brightness2 = GetBrightness(color2);
        }

        // Определяем среднюю яркость
        var averageBrightness = brightness2 == null
            ? brightness1
            : (brightness1 + brightness2.Value) / 2;

        // Возвращаем цвет текста на основе средней яркости
        return averageBrightness < 128 ? Color.White : Color.Black;
    }

    #endregion

    #endregion
}