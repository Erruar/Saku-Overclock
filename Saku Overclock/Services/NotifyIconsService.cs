using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using H.NotifyIcon;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Shared;
using Saku_Overclock.Shared.Models;
using Saku_Overclock.Views;
using Icon = System.Drawing.Icon;

namespace Saku_Overclock.Services;

public partial class NotifyIconsService(IpcConnectionService ipc)
    : SimpleIpcSettingsBase<List<NiIconsElements>>(ipc, "NotifyIcons", IpcJsonContext.Default.ListNiIconsElements, [])
        , INotifyIconsService
{
    public List<NiIconsElements> Elements
    {
        get => Get(s => s);
        set => Set(cache => { cache.Clear(); cache.AddRange(value); });
    }

    public void LoadSettings() => _ = LoadSettingsAsync();

    public bool IsIconsCreated { get; set; }
    public bool IsIconsUpdated { get; set; }

    private readonly List<ИнформацияPage.MinMax> _iconsMinMaxValues =
    [
        new(), new(), new(), new(), new(), new(), new(), new(), new(), new(), new(), new(), new(), new()
    ]; // Saving min-max TrayMon icons values

    private readonly Dictionary<string, TaskbarIcon>
        _trayIcons = []; // Enabled TrayMon icons

    // Icons cache
    private readonly Dictionary<string, (Icon icon, IntPtr handle)> _iconCache = [];
    private readonly Lock _cacheLock = new();
    private readonly Lock _trayIconsLock = new();

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

    // Need to rework
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

            for (var i = 0; i < sensorValues.Length && i < _iconsMinMaxValues.Count; i++)
                UpdateMinMaxValues(_iconsMinMaxValues, i,
                    sensorValues[i]); // Changing min-max

            // UI only in UI thread
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
            // Grouping UI work in one method
            var iconUpdates = new[]
            {
                ("Settings_ni_Values_STAPM", sensorsInformation.CpuStapmValue, "W", _iconsMinMaxValues[0],
                    _stapmText),
                ("Settings_ni_Values_Fast", sensorsInformation.CpuFastValue, "W", _iconsMinMaxValues[1], _fastText),
                ("Settings_ni_Values_Slow", sensorsInformation.CpuSlowValue, "W", _iconsMinMaxValues[2], _slowText),
                ("Settings_ni_Values_VRMEDC", sensorsInformation.VrmEdcValue, "A", _iconsMinMaxValues[3],
                    _vrmEdcText),
                ("Settings_ni_Values_CPUTEMP", sensorsInformation.CpuTempValue, "C", _iconsMinMaxValues[4],
                    _cpuTempText),
                ("Settings_ni_Values_CPUUsage", sensorsInformation.CpuUsage, "%", _iconsMinMaxValues[5],
                    _cpuUsageText),
                ("Settings_ni_Values_AVGCPUCLK", sensorsInformation.CpuFrequency, "GHz", _iconsMinMaxValues[6],
                    _cpuFreqText),
                ("Settings_ni_Values_AVGCPUVOLT", sensorsInformation.CpuVoltage, "V", _iconsMinMaxValues[7],
                    _cpuVoltText),
                ("Settings_ni_Values_GFXCLK", sensorsInformation.ApuFrequency, "MHz", _iconsMinMaxValues[8],
                    _gfxFreqText),
                ("Settings_ni_Values_GFXTEMP", sensorsInformation.ApuTempValue, "C", _iconsMinMaxValues[9],
                    _gfxTempText),
                ("Settings_ni_Values_GFXVOLT", sensorsInformation.ApuVoltage, "V", _iconsMinMaxValues[10],
                    _gfxVoltText),
                ("Settings_ni_Values_DgpuFreq", sensorsInformation.NvidiaGpuFrequency, "GHz", _iconsMinMaxValues[11],
                    _dGpuFreqText),
                ("Settings_ni_Values_DgpuTemp", sensorsInformation.NvidiaGpuTemperature, "C", _iconsMinMaxValues[12],
                    _dGpuTempText),
                ("Settings_ni_Values_RamUsage", sensorsInformation.RamUsagePercent, "%", _iconsMinMaxValues[13],
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
        if (index >= 0 && index < minMaxValues.Count)
        {
            if (minMaxValues[index].Min == 0.0d) minMaxValues[index].Min = currentValue;
            if (index == 4 && currentValue > 150) currentValue = 150; // Fix impossible temp (found on Ryzen 5 6600H)

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
        string description) // Updating current TrayMon icon text
    {
        // Rounding values
        var currentValueText = $"{currentValue:0.#}";
        var minValueText = $"{minMaxValue.Min:0.#}";
        var maxValueText = $"{minMaxValue.Max:0.#}";


        var tooltip = $"{description}" +
                      _niCurrentValueText + currentValueText + unit; // Tooltip


        var extendedTooltip = _niMinvalueText + minValueText + unit +
                              _niMaxvalueText + maxValueText +
                              unit; // Advanced tooltip (min-max values)

        Change_Ni_Icons_Text(key, currentValueText, tooltip, extendedTooltip);
    }

    /// <summary> Update icons from UI </summary>
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

        // Rebuild all icons
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

        // Clean icons collection
        lock (_trayIconsLock)
        {
            _trayIcons.Clear();
        }
    }

    public void CreateNotifyIcons()
    {
        LoadSettings(); // Load config

        // Return if nothing to show
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
                    }

                    // Check for Icon with same ID
                    TaskbarIcon? existingIcon;
                    lock (_trayIconsLock)
                    {
                        _trayIcons.TryGetValue(element.Name, out existingIcon);
                    }

                    // If exist - remove
                    if (existingIcon != null && !existingIcon.IsDisposed)
                        try
                        {
                            existingIcon.Icon?.Dispose(); // Remove old icon
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

                    // Crating TrayMon icons
                    var notifyIcon = new TaskbarIcon
                    {
                        Icon = icon,
                        Id = parsedGuid, // Unique icon ID (IF NOT EXIT - NEW ICON WILL OVERWRITE EXISTING MAIN APP TRAY ICON!)
                        ToolTipText = element.ContextMenuType != 0 ? element.Name : ""
                    };

                    try
                    {
                        notifyIcon.ForceCreate(false);
                    }
                    catch
                    {
                        element.Guid = Guid.NewGuid().ToString();

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

        // Creating cache for icon
        var cacheKey =
            $"{element.Color}_{element.SecondColor}_{element.FontSize}_{element.IconShape}_{element.BgOpacity}_Text";

        lock (_cacheLock)
        {
            if (_iconCache.TryGetValue(cacheKey, out var cached)) return cached.icon; // Get from cache
        }

        // Create new icon
        var newIcon = CreateIconFast(element);

        lock (_cacheLock)
        {
            // Add into cache
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

    /// <summary>Creating round cube</summary>
    private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        var factor = 0.99f; // Fix pixel eating in GDI+

        // Top left corner
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);

        // Top line
        path.AddLine(rect.Left + radius, rect.Top, rect.Right - radius - factor, rect.Top);

        // Top right corner
        path.AddArc(rect.Right - diameter - factor, rect.Top, diameter, diameter, 270, 90);

        // Right line
        path.AddLine(rect.Right, rect.Top + radius, rect.Right, rect.Bottom - radius - factor);

        // Bottom right corner
        path.AddArc(rect.Right - diameter - factor, rect.Bottom - diameter - factor, diameter, diameter, 0, 90);

        // Bottom line
        path.AddLine(rect.Right - radius - factor, rect.Bottom, rect.Left + radius, rect.Bottom);

        // Bottom left corner
        path.AddArc(rect.Left, rect.Bottom - diameter - factor, diameter, diameter, 90, 90);

        // Bottom line
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
                    // Save pointer to old icon
                    var oldIcon = notifyIcon.Icon;

                    // Create new icon
                    var newIcon = UpdateIconText(newText, element.Color,
                        element.IsGradient ? element.SecondColor : string.Empty,
                        element.FontSize, element.IconShape, element.BgOpacity,
                        element.FontWeight == 1);

                    if (newIcon != null)
                    {
                        // Set new icon
                        notifyIcon.Icon = newIcon;

                        // Remove old
                        if (oldIcon != null)
                            try
                            {
                                var handle = oldIcon.Handle;
                                oldIcon.Dispose();
                                DestroyIcon(handle);
                            }
                            catch (Exception disposeEx)
                            {
                                LogHelper.LogError($"Ошибка освобождения старой иконки: {disposeEx.Message}");
                            }

                        // Update tooltip
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
            CreateNotifyIcons(); // Re-create icons
        }
    }

    private static Icon? UpdateIconText(string? newText, string newColor, string secondColor, int fontSize,
        int iconShape, double opacity, bool useBold)
    {
        GraphicsPath? path = null;
        var hIcon = IntPtr.Zero;

        // Create new icon with existing text
        var bitmap = new Bitmap(32, 32);
        var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Background
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

        // Drawing shape
        switch (iconShape)
        {
            case 0: // Cube
                g.FillRectangle(bgBrush, 0, 0, 32, 32);
                break;
            case 1: // Round cube
                path = CreateRoundedRectanglePath(new Rectangle(0, 0, 32, 32), 7);
                g.FillPath(bgBrush, path);

                break;
            case 2: // Circle
                g.FillEllipse(bgBrush, 0, 0, 32, 32);
                break;
            default:
                g.FillRectangle(bgBrush, 0, 0, 32, 32);
                break;
        }

        // Update text position
        var textBrush = new SolidBrush(GetContrastColor(newColor, secondColor != string.Empty ? secondColor : null));
        var textPosition = GetTextPosition(newText, fontSize, out var fontSizeT, out var newTextT);
        var font = new Font(new FontFamily("Segoe UI"), fontSizeT * 2f, useBold ? FontStyle.Bold : FontStyle.Regular,
            GraphicsUnit.Pixel);

        // Draw text
        g.DrawString(newTextT, font, textBrush, textPosition);

        // Create icon from Bitmap and cleanup resources
        try
        {
            return Icon.FromHandle(bitmap.GetHicon());
        }
        catch (Exception ex)
        {
            LogHelper.LogError($"Ошибка создания иконки: {ex.Message}");

            // Cleanup Handle if error
            if (hIcon != IntPtr.Zero) DestroyIcon(hIcon);

            return null;
        }
        finally
        {
            // Cleanup Resources
            path?.Dispose();
            font.Dispose();
            textBrush.Dispose();
            bgBrush.Dispose();
            g.Dispose();
            bitmap.Dispose();
        }
    }

    ///<summary> Cleanup Resources method, after GetHicon() </summary>
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>
    ///     Get text position
    /// </summary>
    private static PointF GetTextPosition(string? newText, float fontSize, out float newFontSize,
        out string? newFixedText)
    {
        var yPosition =
            -1.475f * fontSize +
            16.2f; // Premade "compiled" function based on a dataset collected across all possible font sizes
        newFixedText = newText;
        var xPos = 20f;
        if (!newText!.Contains('.')) newText += ".0";

        if (!string.IsNullOrEmpty(newText) && newText.Contains('.'))
        {
            var parts = newText.Split('.');
            var wholePartLength = parts[0].Length;
            switch
                (wholePartLength) // TrayMon© - developed by Erruar, so you don't need to figure out how it works. All values were compiled into functions using NumPy
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

    /// <summary>Get color brightness</summary>
    private static double GetBrightness(string color)
    {
        var valuestring = color.TrimStart('#');
        var r = Convert.ToInt32(valuestring[..2], 16);
        var g = Convert.ToInt32(valuestring.Substring(2, 2), 16);
        var b = Convert.ToInt32(valuestring.Substring(4, 2), 16);
        return 0.299 * r + 0.587 * g + 0.114 * b;
    }

    /// <summary>Get contrast color for background</summary>
    private static Color GetContrastColor(string color1, string? color2 = null)
    {
        var brightness1 = GetBrightness(color1);

        double? brightness2 = null;
        if (!string.IsNullOrEmpty(color2)) brightness2 = GetBrightness(color2);

        // Get average color brightness
        var averageBrightness = brightness2 == null
            ? brightness1
            : (brightness1 + brightness2.Value) / 2;

        // Restoring the text color based on average brightness
        return averageBrightness < 128 ? Color.White : Color.Black;
    }
}