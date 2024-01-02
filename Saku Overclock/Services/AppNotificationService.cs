using System.Collections.Specialized;
using System.Web;

using Microsoft.Windows.AppNotifications;

using Saku_Overclock.Contracts.Services;
using Saku_Overclock.ViewModels;

namespace Saku_Overclock.Notifications;

public class AppNotificationService : IAppNotificationService
{
    private readonly INavigationService _navigationService;

    public AppNotificationService(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    ~AppNotificationService()
    {
        Unregister();
    }

    public void Initialize()
    {
        AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;

        AppNotificationManager.Default.Register();
    }

    public void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        // TODO: Handle notification invocations when your app is already running.

        // Navigate to a specific page based on the notification arguments.
        if (ParseArguments(args.Argument)["action"] == "Settings")
        {
            App.MainWindow.BringToFront();
            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                _navigationService.NavigateTo(typeof(ПресетыViewModel).FullName!);
                App.MainWindow.Show();
            });
            Task.Delay(2 * 1000).ContinueWith(_ =>
            {
                App.MainWindow.ShowMessageDialogAsync("Здесь вы сможете настроить ваш процессор как вам надо", "Настройки применены!");
            });
            
        }
    }

    public bool Show(string payload)
    {
        var appNotification = new AppNotification(payload);

        AppNotificationManager.Default.Show(appNotification);

        return appNotification.Id != 0;
    }

    public NameValueCollection ParseArguments(string arguments)
    {
        return HttpUtility.ParseQueryString(arguments);
    }

    public void Unregister()
    {
        AppNotificationManager.Default.Unregister();
    }
}
