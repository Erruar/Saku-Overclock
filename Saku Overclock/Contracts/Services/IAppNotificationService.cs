using Saku_Overclock.Shared.Models;
using InfoBarSeverity = Microsoft.UI.Xaml.Controls.InfoBarSeverity;

namespace Saku_Overclock.Contracts.Services;

public interface IAppNotificationService
{
    /// <summary>
    ///     In-app notifications list
    /// </summary>
    List<Notify>? Notifies
    {
        get;
        set;
    }

    /// <summary>
    ///     Loading notifications from previous session
    /// </summary>
    void Initialize();

    /// <summary>
    ///     Display message in System UI
    /// </summary>
    /// <param name="payload">Xml-string for message</param>
    void Show(string payload);

    /// <summary>
    ///     Saving notifications
    /// </summary>
    void SaveNotificationsSettings();

    /// <summary>
    ///     Display message in client
    /// </summary>
    /// <param name="title">Title</param>
    /// <param name="message">Message</param>
    /// <param name="severity">Type</param>
    /// <param name="save">Save after app restart</param>
    void ShowNotification(string title, string message, InfoBarSeverity severity, bool save = false);

    /// <summary>
    ///     Notification added event
    /// </summary>
    event EventHandler<Notify> NotificationAdded;
}