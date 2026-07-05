using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Saku_Overclock.Core.Contracts;

namespace Saku_Overclock.Service;

public class AppActivationWorker(
    IAppSettingsService appSettings,
    IPresetManagerService presetManager,
    IPremadePresetManagementService premadePresetsService,
    IPstateService powerStateService,
    IOcFinderService ocFinderService,
    IApplyerService applyerService,
    IBackgroundDataUpdater backgroundDataUpdater,
    IHostApplicationLifetime lifetime,
    ILogger<AppActivationWorker> logger)
    : IHostedService
{
    private readonly CancellationTokenSource _globalCts = new();
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        lifetime.ApplicationStarted.Register(() => 
        {
            // Логика, которая должна выполниться СРАЗУ ПОСЛЕ запуска всех сервисов
            Task.Run(async () => await OnApplicationStartedAsync(), cancellationToken);
        });

        // 1. Загрузка настроек приложения
        appSettings.RegisterIpcHandlers();
        
        // 2. Загрузка пользовательских пресетов
        presetManager.RegisterIpcHandlers();
        
        // 3. Обновление данных
        backgroundDataUpdater.StartAsync(_globalCts.Token);
        
        // 4. Создание пресетов под конкретное железо
        ocFinderService.LazyInitTdp();
        
        // 5. Загрузка методов изменения Power States
        powerStateService.Initialize();
        
        await Task.CompletedTask;
    }

    /// <summary>
    ///     Пост-запуск сервиса
    /// </summary>
    private async Task OnApplicationStartedAsync()
    {
        try
        {
            // 1. Создание готовых пресетов (если не были созданы)
            premadePresetsService.Initialize();
            
            // 2. Восстановление предыдущих настроек разгона
            await applyerService.RestoreAppliedSettings();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Post-start Unhandled exception");
        }
    }

    /// <summary>
    ///     Логика при остановке сервиса
    /// </summary>
    /// <param name="cancellationToken"></param>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // 1. Сохранение пользовательских настроек
        presetManager.SaveSettings();
        
        // 2. Остановка обновления данных
        backgroundDataUpdater.Stop();
        await Task.CompletedTask;
    }
}