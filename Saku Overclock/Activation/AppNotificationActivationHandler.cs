using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Saku_Overclock.Contracts.Services;

namespace Saku_Overclock.Activation;

public class AppNotificationActivationHandler(
    INavigationService navigationService,
    IAppNotificationService notificationService)
    : ActivationHandler<LaunchActivatedEventArgs>
{
    private readonly INavigationService _navigationService = navigationService;
    private readonly IAppNotificationService _notificationService = notificationService;

    protected override bool CanHandleInternal()
    {
        return AppInstance.GetCurrent().GetActivatedEventArgs()?.Kind == ExtendedActivationKind.AppNotification;
    }

    protected async override Task HandleInternalAsync(LaunchActivatedEventArgs args)
    {
        // TODO: Handle notification activations.

        //// // Access the AppNotificationActivatedEventArgs.
        //// var activatedEventArgs = (AppNotificationActivatedEventArgs)AppInstance.GetCurrent().GetActivatedEventArgs().Data;

        //// // Navigate to a specific page based on the notification arguments.
        //// if (_notificationService.ParseArguments(activatedEventArgs.Argument)["action"] == "Settings")
        //// {
        ////     // Queue navigation with low priority to allow the UI to initialize.
        ////     App.MainWindow.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        ////     {
        ////         _navigationService.NavigateTo(typeof(SettingsViewModel).FullName!);
        ////     });
        //// }

        App.MainWindow.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            App.MainWindow.ShowMessageDialogAsync("TODO: Handle notification activations.", "Notification Activation");
        });

        await Task.CompletedTask;
    }
}
