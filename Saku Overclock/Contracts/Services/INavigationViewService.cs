using Microsoft.UI.Xaml.Controls;

namespace Saku_Overclock.Contracts.Services;

public interface INavigationViewService
{
    /// <summary>
    ///     Navigation elements
    /// </summary>
    IList<object>? MenuItems
    {
        get;
    }

    /// <summary>
    ///     Settings item
    /// </summary>
    object? SettingsItem
    {
        get;
    }

    /// <summary>
    ///     Initialize navigation system
    /// </summary>
    void Initialize(NavigationView navigationView);

    /// <summary>
    ///     Uninitialize navigation system
    /// </summary>
    void UnregisterEvents();

    /// <summary>
    ///     Get opened page
    /// </summary>
    /// <param name="pageType">Page type</param>
    /// <returns>Selected navigation element</returns>
    NavigationViewItem? GetSelectedItem(Type pageType);
}