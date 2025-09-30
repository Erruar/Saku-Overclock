using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Saku_Overclock.Helpers;

internal static class VisualTreeHelper
{
    public static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T t)
            {
                yield return t;
            }

            foreach (var childOfChild in FindVisualChildren<T>(child))
            {
                yield return childOfChild;
            }
        }
    }

    public static Grid? FindAdjacentGrid(StackPanel stackPanel)
    {
        var parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(stackPanel) as Panel;
        if (parent == null)
        {
            return null;
        }

        var index = parent.Children.IndexOf(stackPanel);
        return index < 0 || index >= parent.Children.Count - 1 ? null : parent.Children[index + 1] as Grid;
    }

    public static bool FindAjantedFontIcons(FontIcon fontIcon, List<string> glyphs)
    {
        foreach (var glyph in glyphs)
        {
            if (fontIcon.Glyph.Contains(glyph))
            {
                return true;
            }
        }

        return false;
    }

    public static void SetAllChildrenVisibility(FrameworkElement parent, Visibility visibility)
    {
        var stackPanels = FindVisualChildren<StackPanel>(parent);
        foreach (var stackPanel in stackPanels)
        {
            stackPanel.Visibility = visibility;
        }
    }
}