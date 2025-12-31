using Microsoft.UI.Xaml;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Wrappers;
using WinRT.Interop;

namespace Saku_Overclock.Views.Windows.TaskDialog;

public sealed partial class TaskDialog : Window
{
    private readonly IAppNotificationService notificationService = App.GetService<IAppNotificationService>();
    public TaskDialog()
    {
        InitializeComponent();

        App.MainWindow.Hide();
        App.MainWindow.Activated += MainWindow_Activated;

        this.SetWindowSize(455, 400);
        this.CenterOnScreen();
        Content.CanDrag = false;
        ExtendsContentIntoTitleBar = true;
        this.SetIsAlwaysOnTop(true);
        this.SetIsResizable(false);
        this.ToggleWindowStyle(true, WindowStyle.SysMenu);
        var hwnd = WindowNative.GetWindowHandle(this);

        // ”станавливаем стиль окна как POPUP (убираем заголовок)
        WindowAttributesWrapper.SetWindowStyle(hwnd);
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args) => App.MainWindow.Hide();

    private void AgreeButton_Click(object sender, RoutedEventArgs e)
    {
        this.Hide();
        DriverHelper.InstallPawnIO();

        notificationService.Notifies = [];
        notificationService.SaveNotificationsSettings();

        Microsoft.Windows.AppLifecycle.AppInstance.Restart(string.Empty);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Environment.Exit(0);
    }
}
