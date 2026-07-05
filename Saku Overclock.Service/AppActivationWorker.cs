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
    IHostApplicationLifetime lifetime,
    ILogger<AppActivationWorker> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        lifetime.ApplicationStarted.Register(() => 
        {
            // Логика, которая должна выполниться СРАЗУ ПОСЛЕ запуска всех сервисов
            Task.Run(async () => await OnApplicationStartedAsync(), cancellationToken);
        });

        appSettings.RegisterIpcHandlers();
        presetManager.RegisterIpcHandlers();
        ocFinderService.LazyInitTdp();
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
        presetManager.SaveSettings();
        await Task.CompletedTask;
    }
}