using System.Diagnostics;
using H.NotifyIcon;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.ViewModels;
using PowerWindow = Saku_Overclock.Views.Window.PowerMon.PowerWindow;

namespace Saku_Overclock.Services;

public partial class TrayMenuService(
    IWindowStateManagerService windowStateManager,
    INavigationService navigationService)
    : ITrayMenuService
{
    private TaskbarIcon? _mainTrayIcon;
    private TaskbarIcon? _minimalTrayIcon;

    public void Initialize()
    {
        try
        {
            _mainTrayIcon = (TaskbarIcon)Application.Current.Resources["TrayIcon"];
        }
        catch (Exception ex)
        {
            LogHelper.LogError($"[TrayIconService] Failed to initialize: {ex}");
        }

        RegisterCommands(CreateTrayCommands());
        EnsureTrayIconCreated();
    }

    public void RegisterCommands(ITrayCommandCollection commands)
    {
        foreach (var (commandName, action) in commands)
        {
            if (Application.Current.Resources[commandName] is XamlUICommand command)
            {
                command.ExecuteRequested += (_, _) => action();
            }
        }
    }

    public void EnsureTrayIconCreated()
    {
        try
        {
            _mainTrayIcon?.ForceCreate(false);
        }
        catch (Exception ex)
        {
            LogHelper.LogError($"[TrayIconService] Failed to create tray icon: {ex}");
        }
    }

    public void RestoreDefaultMenu()
    {
        _minimalTrayIcon?.Dispose();
        _minimalTrayIcon = null;

        if (_mainTrayIcon != null)
        {
            _mainTrayIcon.Visibility = Visibility.Visible;
            _mainTrayIcon.ForceCreate(false);
        }
    }

    public void SetMinimalMode()
    {
        if (_mainTrayIcon == null)
        {
            return;
        }

        _mainTrayIcon.Visibility = Visibility.Collapsed;
        _minimalTrayIcon?.Dispose();

        try
        {
            _minimalTrayIcon = new TaskbarIcon
            {
                ToolTipText = "Saku Overclock©\nContext menu is disabled",
                IconSource = new BitmapImage(new Uri("ms-appx:///Assets/WindowIcon.ico")),
                Id = Guid.NewGuid()
            };

            var toggleCommand = new XamlUICommand();
            toggleCommand.ExecuteRequested += (_, _) => windowStateManager.ToggleWindowVisibility();

            _minimalTrayIcon.LeftClickCommand = toggleCommand;
            _minimalTrayIcon.ForceCreate(false);
        }
        catch (Exception ex)
        {
            LogHelper.LogError($"[TrayIconService] Failed to set minimal mode: {ex}");
        }
    }

    public void Dispose()
    {
        _mainTrayIcon?.Dispose();
        _minimalTrayIcon?.Dispose();
        GC.SuppressFinalize(this);
    }

    private TrayCommandCollection CreateTrayCommands()
    {
        return new TrayCommandCollection
        {
            { "ShowHideWindowCommand", windowStateManager.ToggleWindowVisibility },
            { "ExitApplicationCommand", App.MainWindow.Close },
            { "Command1", OpenGitHub },
            { "Command2", OpenPowerMonitor },
            { "Command3", () => NavigateAndShow(typeof(SettingsViewModel)) },
            { "Command4", () => NavigateAndShow(typeof(ПресетыViewModel)) },
            { "Command5", () => NavigateAndShow(typeof(ПараметрыViewModel)) },
            { "Command6", () => NavigateAndShow(typeof(ИнформацияViewModel)) }
            //{ "Command7", () => NavigateAndShow(typeof(КулерViewModel)) }
        };
    }

    private void OpenGitHub() => Process.Start(new ProcessStartInfo("https://github.com/Erruar/Saku-Overclock/")
        { UseShellExecute = true });

    private void OpenPowerMonitor()
    {
        var powerWindow = new PowerWindow
        {
            SystemBackdrop = new MicaBackdrop
            {
                Kind = MicaKind.BaseAlt
            }
        };
        powerWindow.Activate();
    }

    private void NavigateAndShow(Type viewModelType)
    {
        navigationService.NavigateTo(viewModelType.FullName!);
        windowStateManager.ShowMainWindow();
    }
}