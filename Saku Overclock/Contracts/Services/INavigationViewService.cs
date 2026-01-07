using Microsoft.UI.Xaml.Controls;

namespace Saku_Overclock.Contracts.Services;

public interface INavigationViewService
{
    /// <summary>
    ///     Элементы навигации
    /// </summary>
    IList<object>? MenuItems
    {
        get;
    }

    /// <summary>
    ///     Элемент настроек приложения
    /// </summary>
    object? SettingsItem
    {
        get;
    }

    /// <summary>
    ///     Инициализировать систему навигации страниц
    /// </summary>
    /// <param name="navigationView"></param>
    void Initialize(NavigationView navigationView);

    /// <summary>
    ///     Деинициализировать систему навигации страниц
    /// </summary>
    void UnregisterEvents();

    /// <summary>
    ///     Получить выбранную страницу
    /// </summary>
    /// <param name="pageType">Тип страницы</param>
    /// <returns>Выбранный элемент навигации</returns>
    NavigationViewItem? GetSelectedItem(Type pageType);
}