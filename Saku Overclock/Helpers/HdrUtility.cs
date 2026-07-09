using Microsoft.Graphics.Display;
using Microsoft.UI;
using Windows.Foundation;

namespace Saku_Overclock.Helpers;

public static class HdrUtility
{
    private static DisplayInformation? _displayInfo;

    /// <summary>
    ///     Register display HDR change
    /// </summary>
    public static void RegisterHdrChange()
    {
        if (_displayInfo != null)
            return;

        var windowId = Win32Interop.GetWindowIdFromWindow(App.Hwnd);
        _displayInfo = DisplayInformation.CreateForWindowId(windowId);

        _displayInfo.AdvancedColorInfoChanged += OnAdvancedColorInfoChanged;
    }

    /// <summary>
    ///     HDR state changed event
    /// </summary>
    private static void OnAdvancedColorInfoChanged(DisplayInformation sender, object args)
    {
        DisplayInformationChanged?.Invoke(sender, args);
    }

    public static event TypedEventHandler<DisplayInformation, object>? DisplayInformationChanged;

    /// <summary>
    ///     Color info (HDR or not)
    /// </summary>
    private static DisplayAdvancedColorInfo? ColorInfo =>
        _displayInfo?.GetAdvancedColorInfo();

    /// <summary>
    ///     Is HDR supported in system displays
    /// </summary>
    /// <returns>Supported</returns>
    public static bool IsHdrSupported()
    {
        if (_displayInfo == null)
        {
            RegisterHdrChange();
        }
        
        var info = ColorInfo;
        return info != null &&
               info.IsAdvancedColorKindAvailable(DisplayAdvancedColorKind.HighDynamicRange);
    }

    /// <summary>
    ///     Is HDR enabled on current display
    /// </summary>
    /// <returns>HDR Enabled</returns>
    public static bool IsHdrEnabled()
    {
        if (_displayInfo == null)
        {
            RegisterHdrChange();
        }
        
        var info = ColorInfo;
        return info != null &&
               info.CurrentAdvancedColorKind == DisplayAdvancedColorKind.HighDynamicRange;
    }
}