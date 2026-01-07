using Microsoft.UI.Xaml.Controls;
using Saku_Overclock.JsonContainers;

namespace Saku_Overclock.Contracts.Services;

public interface IAppNotificationService
{
    /// <summary>
    ///     Список уведомлений приложения
    /// </summary>
    List<Notify>? Notifies
    {
        get;
        set;
    }

    /// <summary>
    ///     Загружает предыдущие уведомления и регистрирует их
    /// </summary>
    void Initialize();

    /// <summary>
    ///     Отобразит уведомление в системе
    /// </summary>
    /// <param name="payload">Xml-строка для отображения уведомления</param>
    /// <returns>Результат выполнения</returns>
    void Show(string payload);

    /// <summary>
    ///     Сохранить уведомления
    /// </summary>
    void SaveNotificationsSettings();

    /// <summary>
    ///     Отобразить уведомление в приложении
    /// </summary>
    /// <param name="title">Заголовок</param>
    /// <param name="message">Описание</param>
    /// <param name="severity">Тип</param>
    /// <param name="save">Сохранить после перезапуска приложения</param>
    void ShowNotification(string title, string message, InfoBarSeverity severity, bool save = false);

    /// <summary>
    ///     Событие добавления уведомления
    /// </summary>
    event EventHandler<Notify> NotificationAdded;
}