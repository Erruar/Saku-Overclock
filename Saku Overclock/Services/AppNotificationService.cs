using System.Collections.Specialized;
using System.Diagnostics;
using System.Web;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppNotifications;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Core.Contracts.Services;
using Saku_Overclock.JsonContainers;
using Saku_Overclock.ViewModels;

namespace Saku_Overclock.Services;

public class AppNotificationService : IAppNotificationService
{
    private readonly INavigationService _navigationService;
    private readonly IFileService _fileService;

    private const string FolderPath = "Saku Overclock/Notifications";
    private const string FileName = "AppNotifications.json";

    private readonly string _localApplicationData =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private readonly string _applicationDataFolder;

    public AppNotificationService(IFileService fileService, INavigationService navigationService)
    {
        _navigationService = navigationService;
        _applicationDataFolder = Path.Combine(_localApplicationData, FolderPath);
        _fileService = fileService;
    }

    public List<Notify>? Notifies
    {
        get;
        set;
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

    private void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        // TODO: Handle notification invocations when your app is already running.

        // Обработка специальных параметров уведомления
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
                App.MainWindow.ShowMessageDialogAsync("Здесь вы сможете настроить ваш процессор как вам надо",
                    "Настройки применены!");
            });
        }

        if (ParseArguments(args.Argument)["action"] == "Message")
        {
            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                Process.Start(new ProcessStartInfo("https://github.com/Erruar/Saku-Overclock/issues/new/")
                    { UseShellExecute = true });
            });
        }
    }

    public void Show(string payload)
    {
        var appNotification = new AppNotification(payload);

        AppNotificationManager.Default.Show(appNotification);
        //return appNotification.Id != 0;
    }

    private NameValueCollection ParseArguments(string arguments) => HttpUtility.ParseQueryString(arguments);

    private void Unregister() => AppNotificationManager.Default.Unregister();

    private void LoadNotificationsSettings() =>
        Notifies = _fileService.Read<List<Notify>>(_applicationDataFolder, FileName);

    public void SaveNotificationsSettings() => _fileService.Save(_applicationDataFolder, FileName, Notifies);

    public void ShowNotification(string title, string message, InfoBarSeverity severity, bool save = false)
    {
        var notify = 
        new Notify
        {
            Title = title,
            Msg = message,
            Type = severity
        };

        if (save) 
        {
            Notifies ??= [];
            Notifies.Add(notify);
            SaveNotificationsSettings();
        }

        NotificationAdded?.Invoke(this, notify);
    }

    public event EventHandler<Notify>? NotificationAdded;
}