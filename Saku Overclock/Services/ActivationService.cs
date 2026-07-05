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
    IWindowStateManagerService windowStateManager,
    ITrayMenuService trayMenuService,
    IPresetManagerService presetManagerService)
    : IActivationService
{
    private UIElement? _shell;

    private readonly CancellationTokenSource _globalCts = new();

    public async Task ActivateAsync(object activationArgs)
    {
        // 1. Загрузка настроек приложения
        await appSettingsService.LoadSettingsAsync();
        
        // 2. Загрузка пресетов пользователя
        await presetManagerService.LoadSettingsAsync();
        
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
        // 3. Обновление данных
        backgroundDataUpdater.StartAsync(_globalCts.Token);

        // 4. Инициализация тем
        themeSelectorService.Initialize();

        // 5. Состояние окна и его скрытие в трей
        windowStateManager.Initialize();
    }

    /// <summary>
    ///     Загрузка тем и сервисов приложения
    /// </summary>
    private async Task StartupAsync()
    {
        // 1. Установка выбранной темы приложения
        themeSelectorService.SetRequestedThemeAsync();

        // 3. Трей иконка и меню
        trayMenuService.Initialize();

        // 4. Проверка наличия обновлений
        await updateCheckerService.CheckForUpdates();
    }
}