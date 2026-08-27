using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Saku_Overclock.Helpers;

internal static class VisualTreeHelper
{
    /// <summary>
    ///     Find visual children on page tree
    /// </summary>
    /// <param name="parent">Parent element</param>
    /// <typeparam name="T">Type</typeparam>
    /// <returns>Framework elements collection</returns>
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

    /// <summary>
    ///     Find parent grid
    /// </summary>
    /// <param name="stackPanel">StackPanel to search in</param>
    /// <returns>Found Grid</returns>
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

    /// <summary>
    ///     Search for font icon in glyph collection
    /// </summary>
    /// <param name="fontIcon">Searching for font icon</param>
    /// <param name="glyphs">Glyph collection</param>
    /// <returns>Success</returns>
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

    /// <summary>
    ///     Set all elements visibility to Visibility state
    /// </summary>
    /// <param name="parent">Parent element</param>
    /// <param name="visibility">Visibility state</param>
    public static void SetAllChildrenVisibility(FrameworkElement parent, Visibility visibility)
    {
        var stackPanels = FindVisualChildren<StackPanel>(parent);
        foreach (var stackPanel in stackPanels)
        {
            stackPanel.Visibility = visibility;
        }
    }
    
    /// <summary>
    ///     Completely remove child element from control (Grid, StackPanel, Border, ContentControl etc.)
    /// </summary>
    public static bool RemoveFromParent(this FrameworkElement? element)
    {
        if (element == null) return false;

        var parent = element.Parent ?? Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(element);

        switch (parent)
        {
            // Grid, StackPanel, Canvas, RelativePanel, VariableSizedWrapGrid
            case Panel panel:
                return panel.Children.Remove(element);

            // Button, Frame, ScrollViewer, UserControl, Expander, NavigationViewItem
            case ContentControl contentControl when ReferenceEquals(contentControl.Content, element):
                contentControl.Content = null;
                return true;

            case Border border when ReferenceEquals(border.Child, element):
                border.Child = null;
                return true;

            case Viewbox viewbox when ReferenceEquals(viewbox.Child, element):
                viewbox.Child = null;
                return true;

            case ItemsControl itemsControl:
                return itemsControl.Items.Remove(element);

            default:
                return false;
        }
    }
}