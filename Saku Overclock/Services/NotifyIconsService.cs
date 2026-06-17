using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using H.NotifyIcon;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Core.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Models;
using Saku_Overclock.Views;
using Icon = System.Drawing.Icon;

namespace Saku_Overclock.Services;

public class NotifyIconsService : INotifyIconsService
{
    private const string FolderPath = "Saku Overclock/Settings";
    private const string FileName = "NotifyIcons.json";

    private readonly string _applicationDataFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FolderPath);

    private readonly IFileService? _fileService;

    [JsonConstructor]
    private NotifyIconsService()
    {
    }

    public NotifyIconsService(IFileService fileService)
    {
        _fileService = fileService;
    }

    public List<NiIconsElements> Elements { get; set; } = [];

    public void LoadSettings()
    {
        try
        {
            Elements = _fileService?.Read<List<NiIconsElements>>(_applicationDataFolder, FileName) ?? [];
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex);
        }
    }

    public void SaveSettings()
    {
        _fileService?.Save(_applicationDataFolder, FileName, Elements);
    }


    public bool IsIconsCreated { get; set; }
    public bool IsIconsUpdated { get; set; }

    private readonly List<ИнформацияPage.MinMax> _niiconsMinMaxValues =
    [
        new(), new(), new(), new(), new(), new(), new(), new(), new(), new(), new(), new(), new(), new()
    ]; // Лист для хранения минимальных и максимальных значений Ni-Icons

    private readonly Dictionary<string, TaskbarIcon>
        _trayIcons = []; // Хранилище включенных в данный момент иконок Ni-Icons

    // Кеш для иконок чтобы не создавать заново каждый раз
    private readonly Dictionary<string, (Icon icon, IntPtr handle)> _iconCache = [];
    private readonly Lock _cacheLock = new();
    private readonly Lock _trayIconsLock = new(); // Отдельный объект для синхронизационной блокировки

    private readonly string _stapmText = "Settings_ni_Values_STAPM".GetLocalized();
    private readonly string _fastText = "Settings_ni_Values_Fast".GetLocalized();
    private readonly string _slowText = "Settings_ni_Values_Slow".GetLocalized();
    private readonly string _vrmEdcText = "Settings_ni_Values_VRMEDC".GetLocalized();
    private readonly string _cpuTempText = "Settings_ni_Values_CPUTEMP".GetLocalized();
    private readonly string _cpuUsageText = "Settings_ni_Values_CPUUsage".GetLocalized();
    private readonly string _cpuFreqText = "Settings_ni_Values_AVGCPUCLK".GetLocalized();
    private readonly string _cpuVoltText = "Settings_ni_Values_AVGCPUVOLT".GetLocalized();
    private readonly string _gfxFreqText = "Settings_ni_Values_GFXCLK".GetLocalized();
    private readonly string _gfxTempText = "Settings_ni_Values_GFXTEMP".GetLocalized();
    private readonly string _gfxVoltText = "Settings_ni_Values_GFXVOLT".GetLocalized();
    private readonly string _dGpuFreqText = "Settings_ni_Values_DgpuFreq".GetLocalized();
    private readonly string _dGpuTempText = "Settings_ni_Values_DgpuTemp".GetLocalized();
    private readonly string _ramUsageText = "Settings_ni_Values_RamUsage".GetLocalized();

    private readonly string _niCurrentValueText = "Settings_ni_Values_CurrentValue".GetLocalized();
    private readonly string _niMinvalueText = "Settings_ni_Values_MinValue".GetLocalized();
    private readonly string _niMaxvalueText = "Settings_ni_Values_MaxValue".GetLocalized();

    public void UpdateNotifyIcons(SensorsInformation sensorsInformation)
    {
        try
        {
            IsIconsUpdated = true;

            if (!IsIconsCreated) CreateNotifyIcons();

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
                sensorsInformation.ApuTempValue,
                sensorsInformation.ApuVoltage,
                sensorsInformation.NvidiaGpuFrequency,
                sensorsInformation.NvidiaGpuTemperature,
                sensorsInformation.RamUsagePercent
            };

            for (var i = 0; i < sensorValues.Length && i < _niiconsMinMaxValues.Count; i++)
                UpdateMinMaxValues(_niiconsMinMaxValues, i,
                    sensorValues[i]); // Вносит новые минимальные и максимальные значения в переменные

            // UI обновления только в UI потоке
            App.MainWindow.DispatcherQueue.TryEnqueue(() => UpdateAllIconTexts(sensorsInformation));
        }
        catch (Exception ex)
        {
            LogHelper.LogError($"Ошибка обновления TrayMon иконок: {ex}");
            IsIconsUpdated = false;
        }
    }

    private void UpdateAllIconTexts(SensorsInformation sensorsInformation)
    {
        try
        {
            // Группируем все обновления UI в один метод
            var iconUpdates = new[]
            {
                ("Settings_ni_Values_STAPM", sensorsInformation.CpuStapmValue, "W", _niiconsMinMaxValues[0],
                    _stapmText),
                ("Settings_ni_Values_Fast", sensorsInformation.CpuFastValue, "W", _niiconsMinMaxValues[1], _fastText),
                ("Settings_ni_Values_Slow", sensorsInformation.CpuSlowValue, "W", _niiconsMinMaxValues[2], _slowText),
                ("Settings_ni_Values_VRMEDC", sensorsInformation.VrmEdcValue, "A", _niiconsMinMaxValues[3],
                    _vrmEdcText),
                ("Settings_ni_Values_CPUTEMP", sensorsInformation.CpuTempValue, "C", _niiconsMinMaxValues[4],
                    _cpuTempText),
                ("Settings_ni_Values_CPUUsage", sensorsInformation.CpuUsage, "%", _niiconsMinMaxValues[5],
                    _cpuUsageText),
                ("Settings_ni_Values_AVGCPUCLK", sensorsInformation.CpuFrequency, "GHz", _niiconsMinMaxValues[6],
                    _cpuFreqText),
                ("Settings_ni_Values_AVGCPUVOLT", sensorsInformation.CpuVoltage, "V", _niiconsMinMaxValues[7],
                    _cpuVoltText),
                ("Settings_ni_Values_GFXCLK", sensorsInformation.ApuFrequency, "MHz", _niiconsMinMaxValues[8],
                    _gfxFreqText),
                ("Settings_ni_Values_GFXTEMP", sensorsInformation.ApuTempValue, "C", _niiconsMinMaxValues[9],
                    _gfxTempText),
                ("Settings_ni_Values_GFXVOLT", sensorsInformation.ApuVoltage, "V", _niiconsMinMaxValues[10],
                    _gfxVoltText),
                ("Settings_ni_Values_DgpuFreq", sensorsInformation.NvidiaGpuFrequency, "GHz", _niiconsMinMaxValues[11],
                    _dGpuFreqText),
                ("Settings_ni_Values_DgpuTemp", sensorsInformation.NvidiaGpuTemperature, "C", _niiconsMinMaxValues[12],
                    _dGpuTempText),
                ("Settings_ni_Values_RamUsage", sensorsInformation.RamUsagePercent, "%", _niiconsMinMaxValues[13],
                    _ramUsageText)
            };

            foreach (var (key, value, unit, minMax, textControl) in iconUpdates)
                UpdateNiIconText(key, value, unit, minMax, textControl);
        }
        catch (Exception ex)
        {
            LogHelper.LogError($"Ошибка обновления текстов иконок: {ex}");
        }
    }

    private static void
        UpdateMinMaxValues(List<ИнформацияPage.MinMax> minMaxValues, int index,
            double currentValue)
    {
        // Индекс не выходит за пределы списка.
        if (index >= 0 && index < minMaxValues.Count)
        {
            if (minMaxValues[index].Min == 0.0d) minMaxValues[index].Min = currentValue;
            if (index == 4 && currentValue > 150) currentValue = 150; // Фикс потенциально невозможной температуры

            minMaxValues[index].Max = Math.Max(minMaxValues[index].Max, currentValue);
            minMaxValues[index].Min = Math.Min(minMaxValues[index].Min, currentValue);
        }
        else
        {
            LogHelper.LogWarn(
                $"UpdateMinMaxValues: Попытка доступа по неверному индексу {index}. Размер списка: {minMaxValues.Count}");
        }
    }

    private void UpdateNiIconText(string key, double currentValue, string unit, ИнформацияPage.MinMax minMaxValue,
        string description) // Обновляет текущее значение показателей на трей иконках
    {
        // Ограничение и округление текущего, минимального и максимального значений
        var currentValueText = $"{currentValue:0.#}";
        var minValueText = $"{minMaxValue.Min:0.#}";
        var maxValueText = $"{minMaxValue.Max:0.#}";


        var tooltip = $"{description}" +
                      _niCurrentValueText + currentValueText + unit; // Сам тултип


        var extendedTooltip = _niMinvalueText + minValueText + unit +
                              _niMaxvalueText + maxValueText +
                              unit; // Расширенная часть тултипа (минимум и максимум)

        Change_Ni_Icons_Text(key, currentValueText, tooltip, extendedTooltip);
    }

    /// <summary> Внешний метод для обновления иконок после изменения их в настройках приложения </summary>
    public void UpdateTrayMonIcons()
    {
        if (IsIconsUpdated) DisposeAllNotifyIcons();

        CreateNotifyIcons();
    }

    public void DisposeAllNotifyIcons()
    {
        TaskbarIcon[] iconsToDispose;

        lock (_trayIconsLock)
        {
            iconsToDispose = [.. _trayIcons.Values];
            _trayIcons.Clear();
        }

        // Безопасно перебираем все иконки и вызываем Dispose для каждой из них
        foreach (var icon in iconsToDispose)
            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                if (!icon.IsDisposed)
                    try
                    {
                        icon.Dispose();
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogError($"Ошибка при Dispose иконки: {ex.Message}");
                    }
            });

        // Очищаем коллекцию иконок
        lock (_trayIconsLock)
        {
            _trayIcons.Clear();
        }
    }

    public void CreateNotifyIcons()
    {
        LoadSettings(); // Сначала загрузить конфиг со всеми настройками

        // Если нет элементов, не создаём иконки
        if (Elements.Count == 0) return;

        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                foreach (var element in Elements.Where(element => element.IsEnabled))
                {
                    if (!Guid.TryParse(element.Guid, out var parsedGuid) || parsedGuid == Guid.Empty)
                    {
                        parsedGuid = Guid.NewGuid();
                        element.Guid = parsedGuid.ToString();
                        SaveSettings();
                    }

                    // Проверяем есть ли уже TaskbarIcon с таким ID
                    TaskbarIcon? existingIcon;
                    lock (_trayIconsLock)
                    {
                        _trayIcons.TryGetValue(element.Name, out existingIcon);
                    }

                    // Если иконка уже есть - удаляем
                    if (existingIcon != null && !existingIcon.IsDisposed)
                        try
                        {
                            existingIcon.Icon?.Dispose(); // Освобождаем старую иконку
                            existingIcon.Dispose();
                        }
                        catch (Exception disposeEx)
                        {
                            LogHelper.LogError(
                                $"Ошибка при удалении существующей иконки {element.Name}: {disposeEx.Message}");
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
                        Icon = icon,
                        Id = parsedGuid, // Уникальный ID иконки ЕСЛИ ЕГО НЕТ - ПЕРЕЗАПИШЕТ ОСНОВНОЕ ТРЕЙ МЕНЮ ПРОГРАММЫ
                        ToolTipText = element.ContextMenuType != 0 ? element.Name : ""
                    };

                    try
                    {
                        notifyIcon.ForceCreate(false);
                    }
                    catch
                    {
                        element.Guid = Guid.NewGuid().ToString();
                        SaveSettings();

                        LogHelper.LogError(
                            "BackgroudDataUpdater Service: Невозможно создать TrayMon иконки. Перезапустите приложение.");

                        return;
                    }

                    lock (_trayIconsLock)
                    {
                        _trayIcons[element.Name] = notifyIcon;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"Критическая ошибка в CreateNotifyIcons: {ex.Message}");
            }
        });
        
        IsIconsCreated = true;
    }

    private Icon? GetOrCreateIcon(NiIconsElements? element)
    {
        if (element == null) return null;

        // Создаем ключ для кеша на основе параметров иконки
        var cacheKey =
            $"{element.Color}_{element.SecondColor}_{element.FontSize}_{element.IconShape}_{element.BgOpacity}_Text";

        lock (_cacheLock)
        {
            if (_iconCache.TryGetValue(cacheKey, out var cached)) return cached.icon; // Возвращаем из кеша
        }

        // Создаем новую иконку
        var newIcon = CreateIconFast(element);

        lock (_cacheLock)
        {
            // Добавляем в кеш (handle нужен для правильного освобождения)
            _iconCache[cacheKey] = (newIcon, newIcon.Handle);
        }

        return newIcon;
    }

    private static Icon CreateIconFast(NiIconsElements element)
    {
        using var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.Transparent);

        g.CompositingQuality = CompositingQuality.HighSpeed;

        var color = ColorTranslator.FromHtml("#" + element.Color);

        using var brush = new SolidBrush(Color.FromArgb((int)
            (element.BgOpacity * 255),
            color.R,
            color.G,
            color.B));

        g.FillRectangle(brush, 0, 0, 32, 32);

        using var font = new Font(new FontFamily("Arial"), 22, FontStyle.Regular, GraphicsUnit.Pixel);
        using var textBrush =
            new SolidBrush(GetContrastColor(element.Color, element.IsGradient ? element.SecondColor : null));

        g.DrawString("Saku", font, textBrush, new PointF(-13.4f, 2.3f));

        return Icon.FromHandle(bitmap.GetHicon());
    }

    /// <summary>Создаёт точную область скруглённого куба с учётом съедания пикселей GDI+</summary>
    private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        var factor = 0.99f; // Компенсирует "съедание" пикселей GDI+

        // Верхний левый угол
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);

        // Верхняя линия
        path.AddLine(rect.Left + radius, rect.Top, rect.Right - radius - factor, rect.Top);

        // Верхний правый угол
        path.AddArc(rect.Right - diameter - factor, rect.Top, diameter, diameter, 270, 90);

        // Правая линия
        path.AddLine(rect.Right, rect.Top + radius, rect.Right, rect.Bottom - radius - factor);

        // Нижний правый угол
        path.AddArc(rect.Right - diameter - factor, rect.Bottom - diameter - factor, diameter, diameter, 0, 90);

        // Нижняя линия
        path.AddLine(rect.Right - radius - factor, rect.Bottom, rect.Left + radius, rect.Bottom);

        // Нижний левый угол
        path.AddArc(rect.Left, rect.Bottom - diameter - factor, diameter, diameter, 90, 90);

        // Левая линия
        path.AddLine(rect.Left, rect.Bottom - radius - factor, rect.Left, rect.Top + radius);

        path.CloseFigure();
        return path;
    }

    private void Change_Ni_Icons_Text(string iconName, string? newText, string? tooltipText = null,
        string? advancedTooltip = null)
    {
        if (string.IsNullOrEmpty(iconName)) return;

        try
        {
            TaskbarIcon? notifyIcon;
            lock (_trayIconsLock)
            {
                _trayIcons.TryGetValue(iconName, out notifyIcon);
            }

            if (notifyIcon != null)
            {
                var element = Elements.FirstOrDefault(e => e.Name == iconName);
                if (element != null)
                {
                    // Сохраняем ссылку на старую иконку для правильного освобождения
                    var oldIcon = notifyIcon.Icon;

                    // Создаем новую (с кешированием)
                    var newIcon = UpdateIconText(newText, element.Color,
                        element.IsGradient ? element.SecondColor : string.Empty,
                        element.FontSize, element.IconShape, element.BgOpacity,
                        element.FontWeight == 1);

                    if (newIcon != null)
                    {
                        // Устанавливаем новую иконку
                        notifyIcon.Icon = newIcon;

                        // Только ПОСЛЕ установки новой иконки освобождаем старую
                        if (oldIcon != null)
                            try
                            {
                                var handle = oldIcon.Handle;
                                oldIcon.Dispose();
                                DestroyIcon(handle); // Освобождаем Handle после Dispose
                            }
                            catch (Exception disposeEx)
                            {
                                LogHelper.LogError($"Ошибка освобождения старой иконки: {disposeEx.Message}");
                            }

                        // Обновляем tooltip
                        if (tooltipText != null)
                            notifyIcon.ToolTipText = element.ContextMenuType == 2
                                ? $"{tooltipText}\n{advancedTooltip}"
                                : tooltipText;
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
        int iconShape, double opacity, bool useBold)
    {
        GraphicsPath? path = null;
        var hIcon = IntPtr.Zero;

        // Создаём новую иконку на основе существующей с новым текстом
        var bitmap = new Bitmap(32, 32);
        var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Цвет фона и кисть
        var bgColor = ColorTranslator.FromHtml("#" + newColor);
        Brush bgBrush = new SolidBrush(Color.FromArgb((int)(opacity * 255), bgColor));
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
                g.FillRectangle(bgBrush, 0, 0, 32, 32);
                break;
            case 1: // Скруглённый куб
                path = CreateRoundedRectanglePath(new Rectangle(0, 0, 32, 32), 7);
                g.FillPath(bgBrush, path);

                break;
            case 2: // Круг
                g.FillEllipse(bgBrush, 0, 0, 32, 32);
                break;
            default:
                g.FillRectangle(bgBrush, 0, 0, 32, 32);
                break;
        }

        // Определение позиции текста
        var textBrush = new SolidBrush(GetContrastColor(newColor, secondColor != string.Empty ? secondColor : null));
        var textPosition = GetTextPosition(newText, fontSize, out var fontSizeT, out var newTextT);
        var font = new Font(new FontFamily("Segoe UI"), fontSizeT * 2f, useBold ? FontStyle.Bold : FontStyle.Regular,
            GraphicsUnit.Pixel);

        // Рисуем текст
        g.DrawString(newTextT, font, textBrush, textPosition);

        // Создание иконки из Bitmap и освобождение ресурсов
        try
        {
            return Icon.FromHandle(bitmap.GetHicon());
        }
        catch (Exception ex)
        {
            LogHelper.LogError($"Ошибка создания иконки: {ex.Message}");

            // Освобождаем Handle в случае ошибки
            if (hIcon != IntPtr.Zero) DestroyIcon(hIcon);

            return null;
        }
        finally
        {
            // Освобождаем все ресурсы в правильном порядке
            path?.Dispose();
            font.Dispose();
            textBrush.Dispose();
            bgBrush.Dispose();
            g.Dispose();
            bitmap.Dispose();
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
        if (!newText!.Contains('.')) newText += ".0";

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
        if (!string.IsNullOrEmpty(color2)) brightness2 = GetBrightness(color2);

        // Определяем среднюю яркость
        var averageBrightness = brightness2 == null
            ? brightness1
            : (brightness1 + brightness2.Value) / 2;

        // Возвращаем цвет текста на основе средней яркости
        return averageBrightness < 128 ? Color.White : Color.Black;
    }
}