using System.Buffers;
using System.Text.RegularExpressions;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Models;
using Saku_Overclock.ViewModels;
using Saku_Overclock.Wrappers;

namespace Saku_Overclock.Services;

public partial class RtssSettingsService(IFileService fileService) : IRtssSettingsService
{
    private const string FolderPath = "Saku Overclock/Settings";
    private const string FileName = "RtssSettings.json";

    private readonly string _applicationDataFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FolderPath);

    private readonly IFileService? _fileService = fileService;

    private RtssSettings RtssSettingsClass { get; set; } = new();

    public List<RtssElementsClass> RtssElements
    {
        get => RtssSettingsClass.RtssElements;
        set => RtssSettingsClass.RtssElements = value;
    }

    public bool IsAdvancedCodeEditorEnabled
    {
        get => RtssSettingsClass.IsAdvancedCodeEditorEnabled;
        set => RtssSettingsClass.IsAdvancedCodeEditorEnabled = value;
    }

    public string AdvancedCodeEditor
    {
        get => RtssSettingsClass.AdvancedCodeEditor;
        set => RtssSettingsClass.AdvancedCodeEditor = value;
    }

    // Загрузка настроек
    public void LoadSettings()
    {
        try
        {
            RtssSettingsClass =
                _fileService?.Read<RtssSettings>(_applicationDataFolder, FileName) ?? new RtssSettings();
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex);
        }
    }

    // Сохранение настроек
    public void SaveSettings()
    {
        _fileService?.Save(_applicationDataFolder, FileName, RtssSettingsClass);
    }


    public bool IsRtssUpdated { get; set; }

    public void UpdateRtssMetrics(SensorsInformation sensorsInformation, string? appliedPreset, int? coreCount)
    {
        try
        {
            _appliedPreset = appliedPreset ?? string.Empty;
            _coreCount = coreCount;

            IsRtssUpdated = true;

            if (string.IsNullOrEmpty(AdvancedCodeEditor))
            {
                LogHelper.LogWarn("Строка RTSS@AdvancedCodeEditor пустая");
                return;
            }

            ProcessAndSendRtssTemplate(AdvancedCodeEditor, sensorsInformation);
        }
        catch (Exception ex)
        {
            LogHelper.LogError($"Ошибка обновления RTSS метрик: {ex}");
            IsRtssUpdated = false;
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

    private int? _coreCount;

    private void ProcessComplexTemplate(string editorText, SensorsInformation sensorsInformation,
        int startIndex, int endIndex)
    {
        var estimatedLength = EstimateResultLength(editorText) +
                              (_coreCount ?? Environment.ProcessorCount * 50);

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

    private readonly string _cachedAppVersion = ГлавнаяViewModel.GetVersion(); // Кешированная версия приложения

    private int TryReplacePlaceholder(ReadOnlySpan<char> placeholder, Span<char> output,
        SensorsInformation sensorsInformation)
    {
        // Быстрая проверка по первым символам для оптимизации
        if (placeholder.Length < 3) return 0;

        return placeholder switch
        {
            "$AppVersion$" => WriteToSpan(_cachedAppVersion, output),
            "$SelectedPreset$" => WriteTransliteratedPreset(output),
            // Числовые значения с форматированием
            "$stapm_value$" => WriteFormattedDouble(sensorsInformation.CpuStapmValue, output),
            "$stapm_limit$" => WriteFormattedDouble(sensorsInformation.CpuStapmLimit, output),
            "$fast_value$" => WriteFormattedDouble(sensorsInformation.CpuFastValue, output),
            "$fast_limit$" => WriteFormattedDouble(sensorsInformation.CpuFastLimit, output),
            "$slow_value$" => WriteFormattedDouble(sensorsInformation.CpuSlowValue, output),
            "$slow_limit$" => WriteFormattedDouble(sensorsInformation.CpuSlowLimit, output),
            "$vrmedc_value$" => WriteFormattedDouble(sensorsInformation.VrmEdcValue, output),
            "$vrmedc_max$" => WriteFormattedDouble(sensorsInformation.VrmEdcLimit, output),
            "$cpu_temp_value$" => WriteFormattedDouble(sensorsInformation.CpuTempValue, output),
            "$cpu_temp_max$" => WriteFormattedDouble(sensorsInformation.CpuTempLimit, output),
            "$cpu_usage$" => WriteFormattedDouble(sensorsInformation.CpuUsage, output),
            "$gfx_clock$" => WriteFormattedDouble(sensorsInformation.ApuFrequency, output),
            "$gfx_volt$" => WriteFormattedDouble(sensorsInformation.ApuVoltage, output),
            "$gfx_temp$" => WriteFormattedDouble(sensorsInformation.ApuTempValue, output),
            "$average_cpu_clock$" => WriteFormattedDouble(sensorsInformation.CpuFrequency, output),
            "$average_cpu_voltage$" => WriteFormattedDouble(sensorsInformation.CpuVoltage, output),
            _ => 0
        };
    }

    private static int WriteToSpan(string text, Span<char> output)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        var span = text.AsSpan();
        span.CopyTo(output);
        return span.Length;
    }

    private static int WriteFormattedDouble(double value, Span<char> output)
    {
        return value.TryFormat(output, out var written, "0.###") ? written : 0;
    }

    private string _appliedPreset = string.Empty;

    private int WriteTransliteratedPreset(Span<char> output)
    {
        if (string.IsNullOrEmpty(_appliedPreset)) return 0;

        var written = 0;
        foreach (var c in _appliedPreset)
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

        return written;
    }

    private int CalculateCoreMetricsToSpan(Span<char> output, double[]? cpuFrequencyPerCore,
        double[]? cpuVoltagePerCore)
    {
        var template = GetCoreTemplate();
        if (string.IsNullOrEmpty(template)) return 0;

        var cores = _coreCount ?? Environment.ProcessorCount;
        var compactSizing = "<Br><S0>е" + (template.Contains("<S1>") ? "<S1>" : string.Empty);
        var outputPos = 0;

        // Начальный compactSizing для нормального отображения компактности
        outputPos += WriteToSpan(compactSizing, output[outputPos..]);

        for (uint f = 0; f < cores; f++)
        {
            if (f > 0 && f % 4 == 0) outputPos += WriteToSpan(compactSizing, output[outputPos..]);

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
        var match = ClockCycleRegex().Match(AdvancedCodeEditor);
        return match is { Success: true, Groups.Count: > 1 } ? match.Groups[1].Value : string.Empty;
    }

    private static int EstimateResultLength(string input)
    {
        return Math.Max(input.Length + input.Length / 2, 1024);
    }

    private static double GetSafeCoreValue(double[]? array, uint index)
    {
        return array != null && index < array.Length ? array[index] : 0f;
    }

    private static readonly Dictionary<char, string> TransliterationMap = new()
    {
        { 'а', "a" }, { 'б', "b" }, { 'в', "v" }, { 'г', "g" }, { 'д', "d" },
        { 'е', "e" }, { 'ё', "yo" }, { 'ж', "zh" }, { 'з', "z" }, { 'и', "i" },
        { 'й', "y" }, { 'к', "k" }, { 'л', "l" }, { 'м', "m" }, { 'н', "n" },
        { 'о', "o" }, { 'п', "p" }, { 'р', "r" }, { 'с', "s" }, { 'т', "t" },
        { 'у', "u" }, { 'ф', "f" }, { 'х', "h" }, { 'ц', "ts" }, { 'ч', "ch" },
        { 'ш', "sh" }, { 'щ', "sch" }, { 'ъ', "'" }, { 'ы', "i" }, { 'ь', "'" },
        { 'э', "e" }, { 'ю', "yu" }, { 'я', "ya" },

        // Прописные буквы
        { 'А', "A" }, { 'Б', "B" }, { 'В', "V" }, { 'Г', "G" }, { 'Д', "D" },
        { 'Е', "E" }, { 'Ё', "Yo" }, { 'Ж', "Zh" }, { 'З', "Z" }, { 'И', "I" },
        { 'Й', "Y" }, { 'К', "K" }, { 'Л', "L" }, { 'М', "M" }, { 'Н', "N" },
        { 'О', "O" }, { 'П', "P" }, { 'Р', "R" }, { 'С', "S" }, { 'Т', "T" },
        { 'У', "U" }, { 'Ф', "F" }, { 'Х', "H" }, { 'Ц', "Ts" }, { 'Ч', "Ch" },
        { 'Ш', "Sh" }, { 'Щ', "Sch" }, { 'Ъ', "'" }, { 'Ы', "I" }, { 'Ь', "'" },
        { 'Э', "E" }, { 'Ю', "Yu" }, { 'Я', "Ya" }
    };


    [GeneratedRegex(@"\$cpu_clock_cycle\$(.*?)\$cpu_clock_cycle_end\$")]
    private static partial Regex ClockCycleRegex();
}