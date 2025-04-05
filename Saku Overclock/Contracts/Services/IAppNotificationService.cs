using System.Collections.Specialized;
using Saku_Overclock.SMUEngine;

namespace Saku_Overclock.Contracts.Services;

public interface IAppNotificationService
{
    List<Notify>? Notifies
    {
        get; set;
    }

    void Initialize();

    bool Show(string payload);

    NameValueCollection ParseArguments(string arguments);

    void Unregister();

    void LoadNotificationsSettings();

    void SaveNotificationsSettings();
}
