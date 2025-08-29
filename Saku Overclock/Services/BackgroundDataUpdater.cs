using System.Buffers;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using H.NotifyIcon;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.JsonContainers;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.ViewModels;
using Saku_Overclock.Views;
using Saku_Overclock.Wrappers;
using static Saku_Overclock.Views.ИнформацияPage;
using Icon = System.Drawing.Icon;

namespace Saku_Overclock.Services;

public partial class BackgroundDataUpdater(IDataProvider dataProvider) : IBackgroundDataUpdater
{
    private readonly IDataProvider? _dataProvider = dataProvider;
    private CancellationTokenSource? _cts;
    private Task? _updateTask;

    private readonly IRtssSettingsService
        _rtssSettings = App.GetService<IRtssSettingsService>(); // Конфиг с настройками модуля RTSS

    private readonly string _cachedAppVersion = ГлавнаяViewModel.GetVersion(); // Кешированная версия приложения
    private bool _isIconsCreated;

    private bool
        _isIconsUpdated; // Флаги, служащие подтверждением уничтожения или обновления информации на каких-либо страницах приложения в реальном времени

    private bool _isRtssUpdated;

    private readonly List<MinMax> _niiconsMinMaxValues =
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

    // Кеш для иконок чтобы не создавать заново каждый раз
    private readonly Dictionary<string, (Icon icon, IntPtr handle)> _iconCache = [];
    private readonly Lock _cacheLock = new();

    private bool _batteryCached;
    private string? _cachedBatteryName;
    private string? _cachedBatteryCapacity;
    private string? _cachedBatteryCycles;
    private string? _cachedBatteryHealth;
    private bool _cachedBatteryUnavailable;

    private readonly string _stapmText = "Settings_ni_Values_STAPM".GetLocalized();
    private readonly string _fastText = "Settings_ni_Values_Fast".GetLocalized();
    private readonly string _slowText = "Settings_ni_Values_Slow".GetLocalized();
    private readonly string _vrmedcText = "Settings_ni_Values_VRMEDC".GetLocalized();
    private readonly string _cputempText = "Settings_ni_Values_CPUTEMP".GetLocalized();
    private readonly string _cpuusageText = "Settings_ni_Values_CPUUsage".GetLocalized();
    private readonly string _cpufreqText = "Settings_ni_Values_AVGCPUCLK".GetLocalized();
    private readonly string _cpuvoltText = "Settings_ni_Values_AVGCPUVOLT".GetLocalized();
    private readonly string _gfxfreqText = "Settings_ni_Values_GFXCLK".GetLocalized();
    private readonly string _gfxtempText = "Settings_ni_Values_GFXTEMP".GetLocalized();
    private readonly string _gfxvoltText = "Settings_ni_Values_GFXVOLT".GetLocalized();

    private readonly string _niCurrentvalueText = "Settings_ni_Values_CurrentValue".GetLocalized();
    private readonly string _niMinvalueText = "Settings_ni_Values_MinValue".GetLocalized();
    private readonly string _niMaxvalueText = "Settings_ni_Values_MaxValue".GetLocalized();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            AppSettings.LoadSettings(); // Перестраховка на случай если настройки к тому времени не были загружены

            if (AppSettings.NiIconsEnabled && !_isIconsCreated)
            {
                CreateNotifyIcons();
            }

            _rtssSettings.LoadSettings();
        }
        catch (Exception ex)
        {
            LogHelper.LogError($"BackgroundDataUpdater - Невозможно создать иконки TrayMon! {ex}");
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _updateTask = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    if (_dataProvider == null)
                    {
                        await LogHelper.LogError("DataProvider не инициализирован!");
                        return;
                    }

                    var info = await _dataProvider.GetDataAsync();
                    try
                    {
                        var (batteryName, batteryPercent, batteryState, batteryHealth, batteryCycles, batteryCapacity,
                            chargeRate, notTrack, batteryLifeTime) = await GetBatInfoAsync();

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
                    catch (Exception ex)
                    {
                        info.BatteryUnavailable = true;
                        await LogHelper.LogError($"Данные батареи не обновлены: {ex}");
                    }

                    (info.RamTotal, info.RamBusy, info.RamUsagePercent, info.RamUsage) = GetRamInfo();

                    DataUpdated?.Invoke(this, info);

                    UpdateTrayMonAndRtss(info);
                }
                catch (OperationCanceledException)
                {
                    // Это ожидаемое исключение при отмене задачи. Просто выходим из цикла.
                    break;
                }
                catch (Exception ex)
                {
                    await LogHelper.LogError($"Ошибка обновления данных: {ex}");
                }

                try
                {
                    await Task.Delay(300, _cts.Token);
                }
                catch (TaskCanceledException)
                {
                    break; // Выходим из цикла по запросу отмены
                }
                catch (OperationCanceledException)
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
            RtssHandler.ResetOsdText();
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
                // Получаем часто меняющиеся параметры
                var batteryPercent = GetSystemInfo.GetBatteryPercent() + "%";
                var batteryState = GetSystemInfo.GetBatteryStatus().ToString();
                var chargeRate = $"{GetSystemInfo.GetBatteryRate() / 1000:0.##}W";

                // Время работы батареи
                var batteryLifeTime = GetSystemInfo.GetBatteryLifeTime();

                // Переменные для кэшируемых значений
                string batteryName;
                string batteryCapacity;
                string batteryCycles;
                string batteryHealth;

                if (!_batteryCached)
                {
                    // Кешируем все необязательные данные
                    batteryHealth = $"{100 - GetSystemInfo.GetBatteryHealth() * 100:0.##}%";
                    batteryCycles = GetSystemInfo.GetBatteryCycle().ToString();

                    var fullChargeCapacity = GetSystemInfo.ReadFullChargeCapacity();
                    var designCapacity = GetSystemInfo.ReadDesignCapacity(out notTrack);
                    batteryCapacity = $"{fullChargeCapacity}mWh/{designCapacity}mWh";

                    batteryName = GetSystemInfo.GetBatteryName() ?? "Unknown";

                    _cachedBatteryName = batteryName;
                    _cachedBatteryHealth = batteryHealth;
                    _cachedBatteryCycles = batteryCycles;
                    _cachedBatteryCapacity = batteryCapacity;
                    _cachedBatteryUnavailable = notTrack;
                    _batteryCached = true;
                }
                else
                {
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
            // Батарея недоступна
            return (string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                true, 0);
        }
    }

    #endregion

    #region Update RAM information voids

    private static (
        string RamTotal,
        string RamBusy,
        int RamUsagePercent,
        string RamUsage
        ) GetRamInfo()
    {
        try
        {
            var memStatus = new MemoryInformation();
            if (!GlobalMemoryStatusEx(memStatus))
            {
                return ("Error", "Error", 0, "Error");
            }

            // Преобразуем из байтов в гигабайты
            var totalRamGb = memStatus.ullTotalPhys / 1073741824.0;
            var availRamGb = memStatus.ullAvailPhys / 1073741824.0;
            var busyRamGb = totalRamGb - availRamGb;

            return ($"{totalRamGb:F1}GB",
                    $"{busyRamGb:F1}GB",
                    (int)memStatus.dwMemoryLoad, 
                    $"{(int)memStatus.dwMemoryLoad}%\n{busyRamGb:F1}GB/{totalRamGb:F1}GB");
        }
        catch
        {
            return ("Error", "Error", 0, "Error");
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MemoryInformation
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public MemoryInformation()
        {
            dwLength = (uint)Marshal.SizeOf<MemoryInformation>();
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryInformation lpBuffer);

    #endregion

    #region Update Ni-Icons & RTSS information voids

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

    private void UpdateTrayMonAndRtss(SensorsInformation? sensorsInformation)
    {
        // Валидация входных данных
        if (sensorsInformation == null)
        {
            LogHelper.LogWarn("UpdateTrayMonAndRtss: SensorsInformation is null");
            return;
        }

        try
        {
            // Ранний выход если ничего не включено
            if (AppSettings is { RtssMetricsEnabled: false, NiIconsEnabled: false })
            {
                return;
            }

            // RTSS обновление
            if (AppSettings.RtssMetricsEnabled)
            {
                UpdateRtssMetrics(sensorsInformation);
            }
            // Сброс RTSS если был включен, но теперь выключен
            else if (_isRtssUpdated)
            {
                try
                {
                    RtssHandler.ResetOsdText();
                    _isRtssUpdated = false;
                }
                catch (Exception rtssResetEx)
                {
                    LogHelper.LogWarn($"Failed to reset RTSS: {rtssResetEx.Message}");
                }
            }

            // Notify Icons обновление
            if (AppSettings.NiIconsEnabled)
            {
                UpdateNotifyIcons(sensorsInformation);
            }
            // Очистка иконок если были включены, но теперь выключены
            else if (_isIconsUpdated)
            {
                try
                {
                    DisposeAllNotifyIcons();
                    _isIconsUpdated = false;
                    _isIconsCreated = false;
                }
                catch (Exception iconsDisposeEx)
                {
                    LogHelper.LogWarn($"Failed to dispose notify icons: {iconsDisposeEx.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogError($"Критическая ошибка в UpdateTrayMonAndRtss: {ex}");

            // Попытаться очистить ресурсы при критической ошибке
            TryCleanupResources();
        }
    }


    private void TryCleanupResources()
    {
        try
        {
            if (_isRtssUpdated)
            {
                RtssHandler.ResetOsdText();
                _isRtssUpdated = false;
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogWarn($"Не удалось очистить RTSS ресурсы: {ex.Message}");
        }

        try
        {
            if (_isIconsUpdated)
            {
                DisposeAllNotifyIcons();
                _isIconsUpdated = false;
                _isIconsCreated = false;
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogWarn($"Не удалось очистить иконки: {ex.Message}");
        }
    }

    #endregion

    #region Update RTSS Line information voids


    private void UpdateRtssMetrics(SensorsInformation sensorsInformation)
    {
        try
        {
            _isRtssUpdated = true;

            if (string.IsNullOrEmpty(_rtssSettings.AdvancedCodeEditor))
            {
                LogHelper.LogWarn("Строка RTSS@AdvancedCodeEditor пустая");
                return;
            }

            ProcessAndSendRtssTemplate(_rtssSettings.AdvancedCodeEditor, sensorsInformation);
        }
        catch (Exception ex)
        {
            LogHelper.LogError($"Ошибка обновления RTSS метрик: {ex}");
            _isRtssUpdated = false;
        }
    }

    private void ProcessAndSendRtssTemplate(string editorText, SensorsInformation sensorsInformation)
    {
        var startIndex = editorText.IndexOf("$cpu_clock_cycle$", StringComparison.Ordinal);
        var endIndex = editorText.IndexOf("$cpu_clock_cycle_end$", StringComparison.Ordinal);

        // Если теги отсутствуют или некорректны, обрабатываем простые плейсхолдеры
        if (startIndex == -1 || endIndex == -1 || endIndex <= startIndex + 17)
        {
            ProcessSimpleTemplate(editorText, sensorsInformation);
            return;
        }

        try
        {
            ProcessComplexTemplate(editorText, sensorsInformation, startIndex, endIndex);
        }
        catch (Exception ex)
        {
            LogHelper.LogWarn($"Ошибка обработки RTSS шаблона: {ex.Message}");
            ProcessSimpleTemplate(editorText, sensorsInformation);
        }
    }

    private void ProcessSimpleTemplate(string editorText, SensorsInformation sensorsInformation)
    {
        var estimatedLength = EstimateResultLength(editorText);
        var buffer = ArrayPool<char>.Shared.Rent(estimatedLength);

        try
        {
            var length = ReplaceAllPlaceholders(editorText.AsSpan(), buffer, sensorsInformation);
            RtssHandler.ChangeOsdTextSpan(buffer.AsSpan(0, length));
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    private void ProcessComplexTemplate(string editorText, SensorsInformation sensorsInformation,
        int startIndex, int endIndex)
    {
        var estimatedLength = EstimateResultLength(editorText) +
            (int)(CpuSingleton.GetInstance().info.topology.cores * 50); 

        var buffer = ArrayPool<char>.Shared.Rent(estimatedLength);

        try
        {
            var currentPos = 0;

            // Начало
            currentPos += ReplaceAllPlaceholders(
                editorText.AsSpan(0, startIndex),
                buffer.AsSpan(currentPos),
                sensorsInformation);

            // Середина - ядра процессора
            currentPos += CalculateCoreMetricsToSpan(
                buffer.AsSpan(currentPos),
                sensorsInformation.CpuFrequencyPerCore,
                sensorsInformation.CpuVoltagePerCore);

            // Конец
            currentPos += ReplaceAllPlaceholders(
                editorText.AsSpan(endIndex + 21),
                buffer.AsSpan(currentPos),
                sensorsInformation);

            RtssHandler.ChangeOsdTextSpan(buffer.AsSpan(0, currentPos));
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    private int ReplaceAllPlaceholders(ReadOnlySpan<char> input, Span<char> output,
        SensorsInformation sensorsInformation)
    {
        var current = input;
        var outputPos = 0;

        while (!current.IsEmpty)
        {
            var dollarIndex = current.IndexOf('$');
            if (dollarIndex == -1)
            {
                // Нет больше плейсхолдеров, копируем остаток
                current.CopyTo(output[outputPos..]);
                outputPos += current.Length;
                break;
            }

            // Копируем текст до плейсхолдера
            current[..dollarIndex].CopyTo(output[outputPos..]);
            outputPos += dollarIndex;

            var remaining = current[dollarIndex..];
            var endDollarIndex = remaining[1..].IndexOf('$');

            if (endDollarIndex == -1)
            {
                // Нет закрывающего $, копируем остаток
                remaining.CopyTo(output[outputPos..]);
                outputPos += remaining.Length;
                break;
            }

            var placeholder = remaining[..(endDollarIndex + 2)];
            var replacementLength = TryReplacePlaceholder(placeholder, output[outputPos..], sensorsInformation);

            if (replacementLength > 0)
            {
                outputPos += replacementLength;
                current = remaining[(endDollarIndex + 2)..];
            }
            else
            {
                // Плейсхолдер не найден, копируем как есть
                placeholder.CopyTo(output[outputPos..]);
                outputPos += placeholder.Length;
                current = remaining[(endDollarIndex + 2)..];
            }
        }

        return outputPos;
    }

    private int TryReplacePlaceholder(ReadOnlySpan<char> placeholder, Span<char> output,
        SensorsInformation sensorsInformation)
    {
        // Быстрая проверка по первым символам для оптимизации
        if (placeholder.Length < 3)
        {
            return 0;
        }

        if (placeholder.SequenceEqual("$AppVersion$"))
        {
            return WriteToSpan(_cachedAppVersion, output);
        }

        if (placeholder.SequenceEqual("$SelectedProfile$"))
        {
            return WriteTransliteratedProfile(output);
        }

        // Числовые значения с форматированием
        if (placeholder.SequenceEqual("$stapm_value$"))
        {
            return WriteFormattedDouble(sensorsInformation.CpuStapmValue, output);
        }

        if (placeholder.SequenceEqual("$stapm_limit$"))
        {
            return WriteFormattedDouble(sensorsInformation.CpuStapmLimit, output);
        }

        if (placeholder.SequenceEqual("$fast_value$"))
        {
            return WriteFormattedDouble(sensorsInformation.CpuFastValue, output);
        }

        if (placeholder.SequenceEqual("$fast_limit$"))
        {
            return WriteFormattedDouble(sensorsInformation.CpuFastLimit, output);
        }

        if (placeholder.SequenceEqual("$slow_value$"))
        {
            return WriteFormattedDouble(sensorsInformation.CpuSlowValue, output);
        }

        if (placeholder.SequenceEqual("$slow_limit$"))
        {
            return WriteFormattedDouble(sensorsInformation.CpuSlowLimit, output);
        }

        if (placeholder.SequenceEqual("$vrmedc_value$"))
        {
            return WriteFormattedDouble(sensorsInformation.VrmEdcValue, output);
        }

        if (placeholder.SequenceEqual("$vrmedc_max$"))
        {
            return WriteFormattedDouble(sensorsInformation.VrmEdcLimit, output);
        }

        if (placeholder.SequenceEqual("$cpu_temp_value$"))
        {
            return WriteFormattedDouble(sensorsInformation.CpuTempValue, output);
        }

        if (placeholder.SequenceEqual("$cpu_temp_max$"))
        {
            return WriteFormattedDouble(sensorsInformation.CpuTempLimit, output);
        }

        if (placeholder.SequenceEqual("$cpu_usage$"))
        {
            return WriteFormattedDouble(sensorsInformation.CpuUsage, output);
        }

        if (placeholder.SequenceEqual("$gfx_clock$"))
        {
            return WriteFormattedDouble(sensorsInformation.ApuFrequency, output);
        }

        if (placeholder.SequenceEqual("$gfx_volt$"))
        {
            return WriteFormattedDouble(sensorsInformation.ApuVoltage, output);
        }

        if (placeholder.SequenceEqual("$gfx_temp$"))
        {
            return WriteFormattedDouble(sensorsInformation.ApuTemperature, output);
        }

        if (placeholder.SequenceEqual("$average_cpu_clock$"))
        {
            return WriteFormattedDouble(sensorsInformation.CpuFrequency, output);
        }

        if (placeholder.SequenceEqual("$average_cpu_voltage$"))
        {
            return WriteFormattedDouble(sensorsInformation.CpuVoltage, output);
        }

        return 0;
    }

    private static int WriteToSpan(string text, Span<char> output)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var span = text.AsSpan();
        span.CopyTo(output);
        return span.Length;
    }

    private static int WriteFormattedDouble(double value, Span<char> output) => value.TryFormat(output, out var written, "0.###") ? written : 0;

    private static int WriteTransliteratedProfile(Span<char> output)
    {
        var profile = ShellPage.SelectedProfile;
        if (string.IsNullOrEmpty(profile))
        {
            return 0;
        }

        var written = 0;
        foreach (var c in profile)
        {
            if (TransliterationMap.TryGetValue(c, out var transliterated))
            {
                var span = transliterated.AsSpan();
                if (written + span.Length <= output.Length)
                {
                    span.CopyTo(output[written..]);
                    written += span.Length;
                }
            }
            else if (written < output.Length)
            {
                output[written++] = c;
            }
        }

        return written;
    }

    private int CalculateCoreMetricsToSpan(Span<char> output, double[]? cpuFrequencyPerCore,
        double[]? cpuVoltagePerCore)
    {
        var template = GetCoreTemplate();
        if (string.IsNullOrEmpty(template))
        {
            return 0;
        }

        var cores = CpuSingleton.GetInstance().info.topology.cores;
        var compactSizing = "<Br><S0>е" + (template.Contains("<S1>") ? "<S1>" : string.Empty);
        var outputPos = 0;

        // Начальный compactSizing для нормального отображения компактности
        outputPos += WriteToSpan(compactSizing, output[outputPos..]);

        for (uint f = 0; f < cores; f++)
        {
            if (f > 0 && f % 4 == 0)
            {
                outputPos += WriteToSpan(compactSizing, output[outputPos..]);
            }

            outputPos += ProcessCoreTemplate(template, f, cpuFrequencyPerCore, cpuVoltagePerCore,
                output[outputPos..]);
        }

        return outputPos;
    }

    private int ProcessCoreTemplate(string template, uint coreIndex, double[]? frequencies,
        double[]? voltages, Span<char> output)
    {
        var clk = GetSafeCoreValue(frequencies, coreIndex);
        var volt = GetSafeCoreValue(voltages, coreIndex);

        var templateSpan = template.AsSpan();
        var outputPos = 0;

        while (!templateSpan.IsEmpty)
        {
            var dollarIndex = templateSpan.IndexOf('$');
            if (dollarIndex == -1)
            {
                templateSpan.CopyTo(output[outputPos..]);
                outputPos += templateSpan.Length;
                break;
            }

            // Текст до плейсхолдера
            templateSpan[..dollarIndex].CopyTo(output[outputPos..]);
            outputPos += dollarIndex;

            var remaining = templateSpan[dollarIndex..];
            if (remaining.StartsWith("$currCore$"))
            {
                outputPos += coreIndex.TryFormat(output[outputPos..], out var written) ? written : 0;
                templateSpan = remaining[10..];
            }
            else if (remaining.StartsWith("$cpu_core_clock$"))
            {
                outputPos += clk.TryFormat(output[outputPos..], out var written, "F3") ? written : 0;
                templateSpan = remaining[16..];
            }
            else if (remaining.StartsWith("$cpu_core_voltage$"))
            {
                outputPos += volt.TryFormat(output[outputPos..], out var written, "G3") ? written : 0;
                templateSpan = remaining[18..];
            }
            else
            {
                // Неизвестный - копируем $
                output[outputPos++] = '$';
                templateSpan = remaining[1..];
            }
        }

        return outputPos;
    }

    private string GetCoreTemplate()
    {
        var match = ClockCycleRegex().Match(_rtssSettings.AdvancedCodeEditor);
        return match is { Success: true, Groups.Count: > 1 } ? match.Groups[1].Value : string.Empty;
    }

    private static int EstimateResultLength(string input) => Math.Max(input.Length + input.Length / 2, 1024);

    private static double GetSafeCoreValue(double[]? array, uint index)
    {
        return array != null && index < array.Length ? array[index] : 0f;
    }

    private static readonly Dictionary<char, string> TransliterationMap = new()
    {
        { 'а', "a"  }, { 'б', "b"   }, { 'в', "v"  }, { 'г', "g"  }, { 'д', "d"  },
        { 'е', "e"  }, { 'ё', "yo"  }, { 'ж', "zh" }, { 'з', "z"  }, { 'и', "i"  },
        { 'й', "y"  }, { 'к', "k"   }, { 'л', "l"  }, { 'м', "m"  }, { 'н', "n"  },
        { 'о', "o"  }, { 'п', "p"   }, { 'р', "r"  }, { 'с', "s"  }, { 'т', "t"  },
        { 'у', "u"  }, { 'ф', "f"   }, { 'х', "h"  }, { 'ц', "ts" }, { 'ч', "ch" },
        { 'ш', "sh" }, { 'щ', "sch" }, { 'ъ', "'"  }, { 'ы', "i"  }, { 'ь', "'"  },
        { 'э', "e"  }, { 'ю', "yu"  }, { 'я', "ya" },

        // Прописные буквы
        { 'А', "A"  }, { 'Б', "B"   }, { 'В', "V"  }, { 'Г', "G"  }, { 'Д', "D"  },
        { 'Е', "E"  }, { 'Ё', "Yo"  }, { 'Ж', "Zh" }, { 'З', "Z"  }, { 'И', "I"  },
        { 'Й', "Y"  }, { 'К', "K"   }, { 'Л', "L"  }, { 'М', "M"  }, { 'Н', "N"  },
        { 'О', "O"  }, { 'П', "P"   }, { 'Р', "R"  }, { 'С', "S"  }, { 'Т', "T"  },
        { 'У', "U"  }, { 'Ф', "F"   }, { 'Х', "H"  }, { 'Ц', "Ts" }, { 'Ч', "Ch" },
        { 'Ш', "Sh" }, { 'Щ', "Sch" }, { 'Ъ', "'"  }, { 'Ы', "I"  }, { 'Ь', "'"  },
        { 'Э', "E"  }, { 'Ю', "Yu"  }, { 'Я', "Ya" }
    };



    [GeneratedRegex(@"\$cpu_clock_cycle\$(.*?)\$cpu_clock_cycle_end\$")]
    private static partial Regex ClockCycleRegex();

    #endregion

    #region Update Ni-Icons voids

    private void UpdateNotifyIcons(SensorsInformation sensorsInformation)
    {
        try
        {
            _isIconsUpdated = true;

            if (!_isIconsCreated)
            {
                CreateNotifyIcons();
            }

            var sensorValues = new[]
            {
                sensorsInformation.CpuStapmValue,
                sensorsInformation.CpuFastValue,
                sensorsInformation.CpuSlowValue,
                sensorsInformation.VrmEdcValue,
                sensorsInformation.CpuTempValue,
                sensorsInformation.CpuUsage,
                sensorsInformation.CpuFrequency,
                sensorsInformation.CpuVoltage,
                sensorsInformation.ApuFrequency,
                sensorsInformation.ApuTemperature,
                sensorsInformation.ApuVoltage
            };

            for (var i = 0; i < sensorValues.Length && i < _niiconsMinMaxValues.Count; i++)
            {
                UpdateMinMaxValues(_niiconsMinMaxValues, i, sensorValues[i]);
            }

            // UI обновления только в UI потоке
            var queue = App.MainWindow.DispatcherQueue;
            if (queue != null)
            {
                queue.TryEnqueue(() => UpdateAllIconTexts(sensorsInformation));
            }
            else
            {
                LogHelper.LogWarn("DispatcherQueue недоступен для обновления иконок");
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogError($"Ошибка обновления notify icons: {ex}");
            _isIconsUpdated = false;
        }
    }

    private void UpdateAllIconTexts(SensorsInformation sensorsInformation)
    {
        try
        {
            // Группируем все обновления UI в один метод для минимизации overhead'а диспетчера
            var iconUpdates = new[]
            {
                ("Settings_ni_Values_STAPM", sensorsInformation.CpuStapmValue, "W", _niiconsMinMaxValues[0],
                    _stapmText),
                ("Settings_ni_Values_Fast", sensorsInformation.CpuFastValue, "W", _niiconsMinMaxValues[1], _fastText),
                ("Settings_ni_Values_Slow", sensorsInformation.CpuSlowValue, "W", _niiconsMinMaxValues[2], _slowText),
                ("Settings_ni_Values_VRMEDC", sensorsInformation.VrmEdcValue, "A", _niiconsMinMaxValues[3],
                    _vrmedcText),
                ("Settings_ni_Values_CPUTEMP", sensorsInformation.CpuTempValue, "C", _niiconsMinMaxValues[4],
                    _cputempText),
                ("Settings_ni_Values_CPUUsage", sensorsInformation.CpuUsage, "%", _niiconsMinMaxValues[5],
                    _cpuusageText),
                ("Settings_ni_Values_AVGCPUCLK", sensorsInformation.CpuFrequency, "GHz", _niiconsMinMaxValues[6],
                    _cpufreqText),
                ("Settings_ni_Values_AVGCPUVOLT", sensorsInformation.CpuVoltage, "V", _niiconsMinMaxValues[7],
                    _cpuvoltText),
                ("Settings_ni_Values_GFXCLK", sensorsInformation.ApuFrequency, "MHz", _niiconsMinMaxValues[8],
                    _gfxfreqText),
                ("Settings_ni_Values_GFXTEMP", sensorsInformation.ApuTemperature, "C", _niiconsMinMaxValues[9],
                    _gfxtempText),
                ("Settings_ni_Values_GFXVOLT", sensorsInformation.ApuVoltage, "V", _niiconsMinMaxValues[10],
                    _gfxvoltText)
            };

            foreach (var (key, value, unit, minMax, textControl) in iconUpdates)
            {
                UpdateNiIconText(key, value, unit, minMax, textControl);
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogError($"Ошибка обновления текстов иконок: {ex}");
        }
    }

    private static void
       UpdateMinMaxValues(List<MinMax> minMaxValues, int index,
           double currentValue)
    {
        // Проверяем, что индекс не выходит за пределы списка.
        if (index >= 0 && index < minMaxValues.Count)
        {
            if (minMaxValues[index].Min == 0.0d)
            {
                minMaxValues[index].Min = currentValue;
            }

            minMaxValues[index].Max = Math.Max(minMaxValues[index].Max, currentValue);
            minMaxValues[index].Min = Math.Min(minMaxValues[index].Min, currentValue);
        }
        else
        {
            LogHelper.LogWarn(
                $"UpdateMinMaxValues: Попытка доступа по неверному индексу {index}. Размер списка: {minMaxValues.Count}");
        }
    }

    private void UpdateNiIconText(string key, double currentValue, string unit, MinMax minMaxValue,
        string description) // Обновляет текущее значение показателей на трей иконках
    {
        // Ограничение и округление текущего, минимального и максимального значений
        var currentValueText = $"{currentValue:0.###}";
        var minValueText = $"{minMaxValue.Min:0.###}";
        var maxValueText = $"{minMaxValue.Max:0.###}";


        var tooltip = $"Saku Overclock© -\nTrayMon\n{description}" +
                      _niCurrentvalueText + currentValueText + unit; // Сам тултип


        var extendedTooltip = _niMinvalueText + minValueText + unit +
                              _niMaxvalueText + maxValueText +
                              unit; // Расширенная часть тултипа (минимум и максимум)

        Change_Ni_Icons_Text(key, currentValueText, tooltip, extendedTooltip);
    }

    /// <summary> Внешний метод для обновления иконок после изменения их в настройках приложения </summary>
    public void UpdateNotifyIcons()
    {
        if (_isIconsUpdated)
        {
            DisposeAllNotifyIcons();
        }

        CreateNotifyIcons();
    }

    private void DisposeAllNotifyIcons()
    {
        // Перебираем все иконки и вызываем Dispose для каждой из них
        foreach (var icon in _trayIcons.Values)
        {
            App.MainWindow.DispatcherQueue.TryEnqueue(icon.Dispose); // Действия с UI в UI потоке!
        }

        // Очищаем коллекцию иконок
        _trayIcons.Clear();
    }

    private void CreateNotifyIcons()
    {
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                NiLoad(); // Сначала загрузить конфиг со всеми настройками

                // Если нет элементов, не создаём иконки
                if (_niicons.Elements.Count == 0 || AppSettings.NiIconsEnabled == false)
                {
                    return;
                }

                foreach (var element in _niicons.Elements.Where(element => element.IsEnabled))
                {
                    if (string.IsNullOrWhiteSpace(element.Guid) || !Guid.TryParse(element.Guid, out var parsedGuid))
                    {
                        parsedGuid = Guid.NewGuid();
                        element.Guid = parsedGuid.ToString();
                        NiSave();
                    }

                    // Проверяем есть ли уже TaskbarIcon с таким ID
                    TaskbarIcon? existingIcon;
                    lock (_trayIcons)
                    {
                        _trayIcons.TryGetValue(element.Name, out existingIcon);
                    }

                    // Если иконка уже есть - удаляем
                    if (existingIcon != null)
                    {
                        try
                        {
                            existingIcon.Dispose(); // Это освободит и саму иконку внутри
                        }
                        catch (Exception disposeEx)
                        {
                            LogHelper.LogError(
                                $"Ошибка при dispose существующей иконки {element.Name}: {disposeEx.Message}");
                        }
                    }

                    var icon = GetOrCreateIcon(element);
                    if (icon == null)
                    {
                        LogHelper.LogError($"Не удалось создать иконку для {element.Name}");
                        continue;
                    }

                    // Создаём NotifyIcon
                    var notifyIcon = new TaskbarIcon
                    {
                        // Генерация иконки
                        Icon = icon,
                        Id = parsedGuid // Уникальный ID иконки ЕСЛИ ЕГО НЕТ - ПЕРЕЗАПИШЕТ ОСНОВНОЕ ТРЕЙ МЕНЮ ПРОГРАММЫ
                    };
                    try
                    {
                        notifyIcon.ForceCreate();
                    }
                    catch
                    {
                        element.Guid = Guid.NewGuid().ToString();
                        NiSave();

                        LogHelper.LogError(
                            "BackgroudDataUpdater Service: Невозможно создать TrayMon иконки. Перезапустите приложение.");

                        return;
                    }

                    if (element.ContextMenuType != 0)
                    {
                        notifyIcon.ToolTipText = element.Name;
                    }

                    _trayIcons[element.Name] = notifyIcon;
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"Критическая ошибка в CreateNotifyIcons: {ex.Message}");
            }

            _isIconsCreated = true;
        });
    }

    private Icon? GetOrCreateIcon(NiIconsElements? element)
    {
        if (element == null)
        {
            return null;
        }

        // Создаем ключ для кеша на основе параметров иконки
        var cacheKey = $"{element.Color}_{element.SecondColor}_{element.FontSize}_{element.IconShape}_{element.BgOpacity}_Text";

        lock (_cacheLock)
        {
            if (_iconCache.TryGetValue(cacheKey, out var cached))
            {
                return cached.icon; // Возвращаем из кеша
            }
        }

        // Создаем новую иконку
        var newIcon = CreateIconFromElement(element);

        if (newIcon != null)
        {
            lock (_cacheLock)
            {
                // Добавляем в кеш (handle нужен для правильного освобождения)
                _iconCache[cacheKey] = (newIcon, newIcon.Handle);
            }
        }

        return newIcon;
    }

    private static Icon? CreateIconFromElement(NiIconsElements? element)
    {
        // Создаётся виртуальный Grid и растрируется в Bitmap, затем обновляется иконка
        // Создание иконки:
        // 1. Создание формы (круг, квадрат, логотип и т.д.)
        // 2. Заливка цвета с заданной прозрачностью
        // 3. Наложение текста с указанным размером шрифта

        Bitmap? bitmap = null;
        Graphics? g = null;
        var hIcon = IntPtr.Zero;

        try
        {
            if (element == null)
            {
                return null;
            }

            // Для простоты примера создадим пустую иконку
            bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            g = Graphics.FromImage(bitmap);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            // Задаём цвет фона и форму
            var bgColor = ColorTranslator.FromHtml("#" + element.Color);
            Brush bgBrush;

            if (element.IsGradient && !string.IsNullOrEmpty(element.SecondColor))
            {
                var bgSecColor = ColorTranslator.FromHtml("#" + element.SecondColor);
                bgBrush = new LinearGradientBrush(
                    new Rectangle(0, 0, 32, 32),
                    Color.FromArgb((int)(element.BgOpacity * 255), bgColor),
                    Color.FromArgb((int)(element.BgOpacity * 255), bgSecColor),
                    LinearGradientMode.Horizontal);
            }
            else
            {
                bgBrush = new SolidBrush(Color.FromArgb((int)(element.BgOpacity * 255), bgColor));
            }

            switch (element.IconShape)
            {
                case 0: // Куб
                    g.FillRectangle(bgBrush, 0, 0, 32, 32);
                    break;
                case 1: // Скруглённый куб
                    var path = CreateRoundedRectanglePath(new Rectangle(0, 0, 32, 32), 7);
                    if (path != null)
                    {
                        g.FillPath(bgBrush, path);
                        path.Dispose();
                    }
                    else
                    {
                        g.FillRectangle(bgBrush, 0, 0, 32, 32);
                    }

                    break;
                case 2: // Круг
                    g.FillEllipse(bgBrush, 0, 0, 32, 32);
                    break;
                default:
                    g.FillRectangle(bgBrush, 0, 0, 32, 32);
                    break;
            }

            bgBrush.Dispose(); // Уничтожаем лишние кисти

            // Добавляем текст
            try
            {
                var font = new Font(new FontFamily("Arial"), element.FontSize * 2, FontStyle.Regular,
                    GraphicsUnit.Pixel);
                var textBrush =
                    new SolidBrush(GetContrastColor(element.Color, element.IsGradient ? element.SecondColor : null));

                const string sign = "Saku";
                // Центруем текст
                var textSize = g.MeasureString(sign, font);
                var textPosition = new PointF(
                    (bitmap.Width - textSize.Width) / 2,
                    (bitmap.Height - textSize.Height) / 2
                );
                // Рисуем текст
                g.DrawString(sign, font, textBrush, textPosition);

                font.Dispose();
                textBrush.Dispose();
            }
            catch (Exception textEx)
            {
                LogHelper.LogError($"Ошибка при добавлении текста: {textEx.Message}");
            }

            hIcon = bitmap.GetHicon();
            return Icon.FromHandle(hIcon);
        }
        catch (Exception ex)
        {
            LogHelper.LogError($"Ошибка при создании иконки: {ex.Message}");

            // Освобождаем handle если что-то пошло не так
            if (hIcon != IntPtr.Zero)
            {
                DestroyIcon(hIcon);
            }

            return null;
        }
        finally
        {
            g?.Dispose();
            bitmap?.Dispose();
        }
    }

    /// <summary>Создаёт точную область скруглённого куба с учётом съедания пикселей GDI+</summary>
    private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        var factor = 0.99f; // Компенсирует "съедание" пикселей GDI+

        // Верхний левый угол (без изменений)
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);

        // Верхняя линия (с компенсацией)
        path.AddLine(rect.Left + radius, rect.Top, rect.Right - radius - factor, rect.Top);

        // Верхний правый угол (с компенсацией)
        path.AddArc(rect.Right - diameter - factor, rect.Top, diameter, diameter, 270, 90);

        // Правая линия (с компенсацией)
        path.AddLine(rect.Right, rect.Top + radius, rect.Right, rect.Bottom - radius - factor);

        // Нижний правый угол (с компенсацией)
        path.AddArc(rect.Right - diameter - factor, rect.Bottom - diameter - factor, diameter, diameter, 0, 90);

        // Нижняя линия (с компенсацией)
        path.AddLine(rect.Right - radius - factor, rect.Bottom, rect.Left + radius, rect.Bottom);

        // Нижний левый угол (с компенсацией)
        path.AddArc(rect.Left, rect.Bottom - diameter - factor, diameter, diameter, 90, 90);

        // Левая линия (с компенсацией)
        path.AddLine(rect.Left, rect.Bottom - radius - factor, rect.Left, rect.Top + radius);

        path.CloseFigure();
        return path;
    }

    private void Change_Ni_Icons_Text(string iconName, string? newText, string? tooltipText = null,
        string? advancedTooltip = null)
    {
        if (string.IsNullOrEmpty(iconName))
        {
            return;
        }

        try
        {
            TaskbarIcon? notifyIcon;
            lock (_trayIcons)
            {
                _trayIcons.TryGetValue(iconName, out notifyIcon);
            }

            if (notifyIcon != null)
            {
                var element = _niicons.Elements.FirstOrDefault(e => e.Name == iconName);
                if (element != null)
                {
                    // Создаем новую (с кешированием)
                    var newIcon = UpdateIconText(newText, element.Color,
                        element.IsGradient ? element.SecondColor : string.Empty,
                        element.FontSize, element.IconShape, element.BgOpacity);

                    if (newIcon != null)
                    {
                        notifyIcon.Icon = newIcon;

                        // Обновляем tooltip
                        if (tooltipText != null)
                        {
                            notifyIcon.ToolTipText = element.ContextMenuType == 2
                                ? $"{tooltipText}\n{advancedTooltip}"
                                : tooltipText;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogError($"Ошибка в Change_Ni_Icons_Text: {ex.Message}");
            CreateNotifyIcons(); // Пересоздать иконки
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
                    var offset1 = (int)fontSize switch
                    {
                        14 => 3.3f,
                        13 => -5f,
                        12 => -1f,
                        11 => 2f,
                        _ => 0f
                    };
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

}