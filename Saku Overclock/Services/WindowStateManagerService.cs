using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI.ViewManagement;

namespace Saku_Overclock.Services;

public class WindowStateManagerService(
    IAppSettingsService settingsService,
    IBackgroundDataUpdater backgroundDataUpdater)
    : IWindowStateManagerService
{
    private DispatcherQueue? _dispatcherQueue;
    private UISettings? _settings;

    public void Initialize()
    {
        _dispatcherQueue = App.MainWindow.DispatcherQueue;
        _settings = new UISettings();
        _settings.ColorValuesChanged +=
            Settings_ColorValuesChanged;

        App.MainWindow.WindowStateChanged += MainWindow_WindowStateChanged;
        App.MainWindow.Activated += MainWindow_Activated;
        App.MainWindow.Closed += MainWindow_Closed;

        App.MainWindow.AppWindow.Closing += AppWindow_Closing;
        App.MainWindow.AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets/WindowIcon.ico"));
        App.MainWindow.Content = null;
        App.MainWindow.Title = "AppDisplayName".GetLocalized();
    }

    public void ToggleWindowVisibility()
    {
        if (App.MainWindow.Visible)
        {
            App.MainWindow.Hide();
        }
        else
        {
            ShowMainWindow();
        }
    }

    public void ShowMainWindow()
    {
        App.MainWindow.Show();
        App.MainWindow.BringToFront();
        App.MainWindow.WindowState = WindowState.Normal;
    }

    public (double, double) SetWindowTitleBarBounds(double scaleAdjustment)
    {
        try
        {
            const int titleIconActualWidth = 120;
            const int titleIconActualHeight = 48;
            const int ringerNotificationGridActualSize = 32;
            const int ringerNotificationPositionX = 956;
            const int ringerNotificationPositionY = 9;

            var rightInset = App.MainWindow.AppWindow.TitleBar.RightInset;
            var leftInset = App.MainWindow.AppWindow.TitleBar.LeftInset;
            if (rightInset < 0)
            {
                rightInset = 138;
            }

            if (leftInset < 0)
            {
                leftInset = 0;
            }

            var bounds = new Rect
            {
                Height = titleIconActualHeight,
                Width = titleIconActualWidth,
                X = titleIconActualHeight,
                Y = 0
            };

            var searchBoxRect = GetRect(bounds, scaleAdjustment);

            bounds = new Rect
            {
                Height = ringerNotificationGridActualSize,
                Width = ringerNotificationGridActualSize,
                X = ringerNotificationPositionX,
                Y = ringerNotificationPositionY
            };

            var ringerNotifRect = GetRect(bounds, scaleAdjustment);

            var rectArray = new[] { searchBoxRect, ringerNotifRect };

            var nonClientInputSrc = InputNonClientPointerSource.GetForWindowId(App.MainWindow.AppWindow.Id);
            nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, rectArray);

            if (rightInset < 0 || leftInset < 0)
            {
                throw new InvalidDataException("TitleBar tnset value incorrect");
            }

            return (rightInset / scaleAdjustment, 
                    leftInset / scaleAdjustment);
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex);
        }

        return (130, 0);
    }
    private static RectInt32 GetRect(Rect bounds, double scale)
    {
        return new RectInt32(
            (int)Math.Round(bounds.X * scale),
            (int)Math.Round(bounds.Y * scale),
            (int)Math.Round(bounds.Width * scale),
            (int)Math.Round(bounds.Height * scale)
        );
    }

    // this handles updating the caption button colors correctly when Windows system theme is changed
    // while the app is open
    private void Settings_ColorValuesChanged(UISettings sender, object args)
    {
        // This calls comes off-thread, hence we will need to dispatch it to current app's thread
        _dispatcherQueue?.TryEnqueue(TitleBarHelper.ApplySystemThemeToCaptionButtons);
    }

    // Тип скрытия в трей: 0 - выкл, 1 - при сворачивании приложения сразу в трей, 2 - при закрытии приложения сразу в трей
    private enum HidingType
    {
        MinimizeToTray = 1,
        CloseToTray = 2
    }

    // Тип автостарта: 0 - выкл, 1 - при запуске приложения сразу в трей, 2 - автостарт с системой, 3 - автостарт и трей
    private enum AutoStartType
    {
        HideToTray = 1,
        AutoStartWithHideToTray = 3
    }

    /// <summary>
    ///     Приложение активировано и загрузило UI
    /// </summary>
    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        // Отписаться от события, чтобы не вызвать его повторно
        App.MainWindow.Activated -= MainWindow_Activated;

        // TODO: Перенести пользователя на страницу первоначальной настройки приложения
        // при первом запуске программы (в работе)
        /*if (settingsService.AppFirstRun)
        {
            var navigationService = App.GetService<INavigationService>();
            navigationService.NavigateTo(typeof(ОбучениеViewModel).FullName!);
        }*/

        // Скрыть приложение при запуске, если это включено в настройках
        if (settingsService.AutostartType is
            (int)AutoStartType.HideToTray or (int)AutoStartType.AutoStartWithHideToTray)
        {
            App.MainWindow.Hide();
        }
    }


    /// <summary>
    ///     Сворачивает в трей приложение если включено скрытие по нажатии свернуть на окне
    /// </summary>
    private void MainWindow_WindowStateChanged(object? sender, WindowState e)
    {
        if (settingsService.HidingType == (int)HidingType.MinimizeToTray &&
            App.MainWindow.WindowState == WindowState.Minimized)
        {
            App.MainWindow.Hide();
        }
    }

    /// <summary>
    ///     Останавливает обновление значений сенсоров или отменяет закрытие (скрытие в трей)
    /// </summary>
    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (settingsService.HidingType == (int)HidingType.CloseToTray)
        {
            args.Cancel = true; // Отменяем закрытие
            App.MainWindow.Hide(); // Скрываем в трей
        }
        else
        {
            backgroundDataUpdater.Stop();
        }
    }

    /// <summary>
    ///     Удаляет трей иконку, закрывает все дополнительные окна
    /// </summary>
    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        Application.Current.Exit();
    }
}