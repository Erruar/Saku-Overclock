using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Saku_Overclock.Contracts.Services;

public interface INavigationService
{
    /// <summary>
    ///     Событие навигации на страницу
    /// </summary>
    event NavigatedEventHandler Navigated;

    /// <summary>
    ///     Флаг возможности вернуться на прошлую страницу
    /// </summary>
    bool CanGoBack
    {
        get;
    }

    /// <summary>
    ///     Текущая страница
    /// </summary>
    Frame? Frame
    {
        get;
        set;
    }

    /// <summary>
    ///     Перейти на страницу
    /// </summary>
    /// <param name="pageKey">Имя ViewModel страницы</param>
    /// <param name="parameter">Параметр навигации</param>
    /// <param name="clearNavigation">Перейти без анимации</param>
    void NavigateTo(string pageKey, object? parameter = null, bool clearNavigation = false);

    /// <summary>
    ///     Перезагрузить страницу
    /// </summary>
    /// <param name="from">Имя ViewModel страницы</param>
    void ReloadPage(string from);

    /// <summary>
    ///     Вернуться назад
    /// </summary>
    /// <returns>true - Удалось, false - Не удалось</returns>
    bool GoBack();
}