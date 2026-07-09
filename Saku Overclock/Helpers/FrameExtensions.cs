using Microsoft.UI.Xaml.Controls;

namespace Saku_Overclock.Helpers;

public static class FrameExtensions
{
    /// <summary>
    ///     Get page viewmodel
    /// </summary>
    /// <param name="frame">Page</param>
    /// <returns>Page viewmodel</returns>
    public static object? GetPageViewModel(this Frame frame)
    {
        if (frame.Content == null) return null;
        return GetViewModelReflective(frame.Content);
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2075", 
        Justification = "WinUI Pages in this app are preserved and their ViewModels are accessed safely.")]
    private static object? GetViewModelReflective(object page)
    {
        return page.GetType().GetProperty("ViewModel")?.GetValue(page);
    }
}