using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Saku_Overclock.Contracts.Services;

public interface INavigationService
{
    /// <summary>
    ///     Page navigation changed event
    /// </summary>
    event NavigatedEventHandler Navigated;

    /// <summary>
    ///     Ability return back flag
    /// </summary>
    bool CanGoBack
    {
        get;
    }

    /// <summary>
    ///     Current opened page
    /// </summary>
    Frame? Frame
    {
        get;
        set;
    }

    /// <summary>
    ///     Navigate to page
    /// </summary>
    /// <param name="pageKey">Page ViewModel name</param>
    /// <param name="parameter">Navigation option</param>
    /// <param name="clearNavigation">Navigate without animation</param>
    void NavigateTo(string pageKey, object? parameter = null, bool clearNavigation = false);

    /// <summary>
    ///     Reload opened page
    /// </summary>
    /// <param name="from">Page ViewModel name</param>
    void ReloadPage(string from);

    /// <summary>
    ///     Return back
    /// </summary>
    /// <returns>true - success, false - failed</returns>
    bool GoBack();
}