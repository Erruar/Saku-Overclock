using Microsoft.Graphics.Display;
using Microsoft.UI;
using Windows.Foundation;

namespace Saku_Overclock.Helpers;

public static class HdrUtility
{
    private static DisplayInformation? _displayInfo;

    public static void RegisterHdrChange()
    {
        if (_displayInfo != null)
            return;

        var windowId = Win32Interop.GetWindowIdFromWindow(App.Hwnd);
        _displayInfo = DisplayInformation.CreateForWindowId(windowId);

        _displayInfo.AdvancedColorInfoChanged += OnAdvancedColorInfoChanged;
    }

    private static void OnAdvancedColorInfoChanged(DisplayInformation sender, object args)
    {
        DisplayInformationChanged?.Invoke(sender, args);
    }

    public static event TypedEventHandler<DisplayInformation, object>? DisplayInformationChanged;

    private static DisplayAdvancedColorInfo? ColorInfo =>
        _displayInfo?.GetAdvancedColorInfo();

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