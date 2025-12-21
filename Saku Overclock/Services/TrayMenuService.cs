using System.Diagnostics;
using H.NotifyIcon;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.ViewModels;
using Saku_Overclock.Views;
using Icon = System.Drawing.Icon;

namespace Saku_Overclock.Services;

public partial class TrayMenuService(
    IApplyerService applyerService,
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

        _minimalTrayIcon = new TaskbarIcon
        {
            ToolTipText = "Saku Overclock©\nContext menu is disabled",
            Icon = new Icon("Assets/WindowIcon.ico"),
            Id = Guid.NewGuid()
        };

        var toggleCommand = new XamlUICommand();
        toggleCommand.ExecuteRequested += (_, _) => windowStateManager.ToggleWindowVisibility();

        _minimalTrayIcon.LeftClickCommand = toggleCommand;
        _minimalTrayIcon.ForceCreate(false);
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
            { "Command6", () => NavigateAndShow(typeof(ИнформацияViewModel)) },
            { "Command7", () => NavigateAndShow(typeof(КулерViewModel)) },
            { "Command8", ApplyEcoModeAsync }
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

    private async void ApplyEcoModeAsync()
    {
        await applyerService.ApplyPremadePreset(PresetType.Eco);

        App.GetService<IAppNotificationService>()
            .Show(string.Format("AppNotificationEco".GetLocalized(), AppContext.BaseDirectory));
    }
}