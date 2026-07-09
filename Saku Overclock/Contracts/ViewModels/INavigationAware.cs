namespace Saku_Overclock.Contracts.ViewModels;

public interface INavigationAware
{
    /// <summary>
    ///     On Navigated to page event
    /// </summary>
    /// <param name="parameter">Navigation parameter</param>
    void OnNavigatedTo(object parameter);

    /// <summary>
    ///     On Navigated from page event
    /// </summary>
    void OnNavigatedFrom();
}