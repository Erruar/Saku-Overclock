using System.Collections.Specialized;
using System.Diagnostics;
using System.Web;

using Microsoft.Windows.AppNotifications;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Core.Contracts.Services;
using Saku_Overclock.Core.Services;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.ViewModels;

namespace Saku_Overclock.Services;

public class AppNotificationService : IAppNotificationService
{
    private readonly INavigationService _navigationService;
    private readonly IFileService _fileService;

    private const string FolderPath = "Saku Overclock/Notifications";
    private const string FileName = "AppNotifications.json";

    private readonly string _localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private readonly string _applicationDataFolder;

    public AppNotificationService(IFileService fileService, INavigationService navigationService)
    {
        _navigationService = navigationService;
        _applicationDataFolder = Path.Combine(_localApplicationData, FolderPath);
        _fileService = fileService;
    }

    public List<Notify>? Notifies
    {
        get; set;
    } = [];

    ~AppNotificationService()
    {
        Unregister();
    }

    public void Initialize()
    {
        AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;

        AppNotificationManager.Default.Register();

        LoadNotificationsSettings();
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
            Task.Delay(2000).ContinueWith(_ =>
            {
                App.MainWindow.ShowMessageDialogAsync("Здесь вы сможете настроить ваш процессор как вам надо", "Настройки применены!");
            });

        }
        if (ParseArguments(args.Argument)["action"] == "Message")
        {
            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                Process.Start(new ProcessStartInfo("https://github.com/Erruar/Saku-Overclock/issues/new/") { UseShellExecute = true });
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

    // Загрузка настроек
    public void LoadNotificationsSettings()
    {
        Notifies = _fileService.Read<List<Notify>>(_applicationDataFolder, FileName) ?? []; 
    }

    // Сохранение настроек
    public void SaveNotificationsSettings()
    {
        _fileService.Save(_applicationDataFolder, FileName, Notifies);
    }
}
