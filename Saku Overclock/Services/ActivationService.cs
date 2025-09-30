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
    IAppSettingsService appSettingsService)
    : IActivationService
{
    private UIElement? _shell;

    public async Task ActivateAsync(object activationArgs)
    {
        // Выполняется перед активацией
        await InitializeAsync();

        // Установаить контент для MainWindow
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
    ///     Оптимизация тем перед активацией приложения
    /// </summary>
    private async Task InitializeAsync()
    {
        await themeSelectorService.InitializeAsync().ConfigureAwait(false);
        await Task.CompletedTask;
    }

    /// <summary>
    ///     Загрузка тем и настроек приложения
    /// </summary>
    private async Task StartupAsync()
    {
        await themeSelectorService.SetRequestedThemeAsync();
        appSettingsService.LoadSettings();
        await Task.CompletedTask;
    }
}