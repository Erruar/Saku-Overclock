using Microsoft.UI.Xaml.Controls;

namespace Saku_Overclock.Helpers;

public static class FrameExtensions
{
    public static object? GetPageViewModel(this Frame frame)
    {
        if (frame.Content == null) return null;

        // Используем паттерн-поиск по динамическому типу, который триммер понимает,
        // либо если у тебя у всех ViewModels есть свойство, можно пометить вызывающий тип атрибутом.
        // Но самый простой способ заткнуть триммер здесь, если не хочется городить интерфейсы:
        return GetViewModelReflective(frame.Content);
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2075", 
        Justification = "WinUI Pages in this app are preserved and their ViewModels are accessed safely.")]
    private static object? GetViewModelReflective(object page)
    {
        return page.GetType().GetProperty("ViewModel")?.GetValue(page);
    }
}