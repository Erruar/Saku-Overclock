using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Saku_Overclock.Activation;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Shared.Contracts;
using Saku_Overclock.Views;

namespace Saku_Overclock.Services;

public class ActivationService(
    ActivationHandler<LaunchActivatedEventArgs> defaultHandler,
    IEnumerable<IActivationHandler> activationHandlers,
    IThemeSelectorService themeSelectorService,
    IAppSettingsService appSettingsService,
    IUpdateCheckerService updateCheckerService,
    INotifyIconsService notifyIconsService,
    IRtssSettingsService rtssSettingsService,
    IWindowStateManagerService windowStateManager,
    IBackgroundDataReceiver dataReceiver,
    CoreGatewayService coreGateway,
    ITrayMenuService trayMenuService,
    IPresetManagerService presetManagerService)
    : IActivationService
{
    private UIElement? _shell;
    public async Task ActivateAsync(object activationArgs)
    {
        // 1. Warm-up cpu info cache
        await coreGateway.WarmupAsync();
        
        // 2. Load app settings
        await appSettingsService.LoadSettingsAsync();
        
        // 3. Load user presets
        await presetManagerService.LoadSettingsAsync();
        
        // 4. Load Ni Icons settings
        notifyIconsService.LoadSettings();
        
        // 5. Load Rtss settings
        rtssSettingsService.LoadSettings();
        
        // Run before activation
        Initialize();

        // Set MainWindow context
        if (App.MainWindow.Content == null)
        {
            _shell = App.GetService<ShellPage>();
            App.MainWindow.Content = _shell ?? new Frame();
        }

        // Handle activation
        await HandleActivationAsync(activationArgs);

        // Activate MainWindow
        App.MainWindow.Activate();

        // Tasks after activation
        await StartupAsync();
    }

    /// <summary>
    ///     Activation handler
    /// </summary>
    /// <param name="activationArgs">Activation args</param>
    private async Task HandleActivationAsync(object activationArgs)
    {
        var activationHandler = activationHandlers.FirstOrDefault(h => h.CanHandle(activationArgs));

        if (activationHandler != null)
        {
            await activationHandler.HandleAsync(activationArgs);
        }

        if (defaultHandler.CanHandle(activationArgs))
        {
            await defaultHandler.HandleAsync(activationArgs);
        }
    }

    /// <summary>
    ///     Before activation
    /// </summary>
    private void Initialize()
    {
        dataReceiver.StartAsync(CancellationToken.None);
        
        // 4. Initializing themes
        themeSelectorService.Initialize();

        // 5. Window state and hiding to tray
        windowStateManager.Initialize();
    }

    /// <summary>
    ///     On startup behaviour
    /// </summary>
    private async Task StartupAsync()
    {
        // 1. Set required app theme
        themeSelectorService.SetRequestedThemeAsync();

        // 3. Tray icon and menu
        trayMenuService.Initialize();

        // 4. Update checking
        await updateCheckerService.CheckForUpdates();
    }
}