using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Saku_Overclock.Helpers;

// Usage in XAML:
// <NavigationViewItem x:Uid="Shell_Main" Icon="Document" helpers:NavigationHelper.NavigateTo="AppName.ViewModels.MainViewModel" />
//
// Usage in code:
// NavigationHelper.SetNavigateTo(navigationViewItem, typeof(MainViewModel).FullName);

/// <summary>
///     Helper class to set the navigation target for a NavigationViewItem.
/// </summary>
public class NavigationHelper
{
    /// <summary>
    ///     Get navigation property
    /// </summary>
    public static string GetNavigateTo(NavigationViewItem item) => (string)item.GetValue(NavigateToProperty);

    /// <summary>
    ///     Set navigation property
    /// </summary>
    public static void SetNavigateTo(NavigationViewItem item, string value) => item.SetValue(NavigateToProperty, value);

    /// <summary>
    ///     Navigation property
    /// </summary>
    public static readonly DependencyProperty NavigateToProperty =
        DependencyProperty.RegisterAttached("NavigateTo", typeof(string), typeof(NavigationHelper),
            new PropertyMetadata(null));
}