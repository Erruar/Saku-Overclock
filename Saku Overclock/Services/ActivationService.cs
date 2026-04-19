using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Saku_Overclock.Activation;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Views;

namespace Saku_Overclock.Services;

public class ActivationService(
    ActivationHandler<LaunchActivatedEventArgs> defaultHandler,
    IEnumerable<IActivationHandler> activationHandlers,
    IThemeSelectorService themeSelectorService,
    IAppSettingsService appSettingsService,
    IUpdateCheckerService updateCheckerService,
    IApplyerService applyerService,
    IWindowStateManagerService windowStateManager,
    ITrayMenuService trayMenuService,
    IPstateService pstateService,
    IBackgroundDataUpdater backgroundDataUpdater,
    IPresetManagerService presetManagerService,
    IOcFinderService ocFinderService,
    IPremadePresetManagementService premadePresetManagementService)
    : IActivationService
{
    private UIElement? _shell;

    private readonly CancellationTokenSource _globalCts = new();

    public async Task ActivateAsync(object activationArgs)
    {
        // Выполняется перед активацией
        Initialize();

        // Установить контент для MainWindow
        if (App.MainWindow.Content == null)
        {
            _shell = App.GetService<ShellPage>();
            App.MainWindow.Content = _shell ?? new Frame();
        }

        // Выполнить активацию
        await HandleActivationAsync(activationArgs);

        // Активировать MainWindow.
        App.MainWindow.Activate();

        // Задачи после активации
        await StartupAsync();
    }

    /// <summary>
    ///     Установка обработчика запуска приложения
    /// </summary>
    /// <param name="activationArgs"></param>
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
    ///     Действия перед активацией приложения
    /// </summary>
    private void Initialize()
    {
        // 1. Загрузка настроек приложения
        appSettingsService.LoadSettings();
        
        // 2. Загрузка пресетов пользователя
        presetManagerService.LoadSettings();

        // 3. Обновление данных
        backgroundDataUpdater.StartAsync(_globalCts.Token);

        // 4. Инициализация тем
        themeSelectorService.Initialize();

        // 5. Состояние окна и его скрытие в трей
        windowStateManager.Initialize();
        
        // 6. Инициализация TDP процессора
        ocFinderService.LazyInitTdp();
        
        // 7. Инициализация готовых пресетов
        premadePresetManagementService.Initialize();
    }

    /// <summary>
    ///     Загрузка тем и сервисов приложения
    /// </summary>
    private async Task StartupAsync()
    {
        // 1. Установка выбранной темы приложения
        themeSelectorService.SetRequestedThemeAsync();

        // 2. Восстановление предыдущих настроек разгона
        await applyerService.RestoreAppliedSettings();

        // 3. Установка стратегии работы с P-States
        pstateService.Initialize();

        // 4. Трей иконка и меню
        trayMenuService.Initialize();

        // 5. Проверка наличия обновлений
        await updateCheckerService.CheckForUpdates();
    }
}