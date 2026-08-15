using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Saku_Overclock.Core.Contracts;
using Saku_Overclock.Core.Services;
using Saku_Overclock.Shared.Contracts;

namespace Saku_Overclock.Service;

public class AppActivationWorker(
    IAppSettingsService appSettings,
    IPresetManagerService presetManager,
    IPremadePresetManagementService premadePresetsService,
    ILocalThemeSettingsService localThemeSettingsService,
    INotifyIconsService notifyIconsService,
    IPowerMonSettingsService powerMonSettingsService,
    IRtssSettingsService rtssSettingsService,
    CoreIpcHandlers ipcHandlers,
    IPstateService powerStateService,
    IOcFinderService ocFinderService,
    IApplyerService applyerService,
    IRawSharedMemoryWriterService rawSharedMemoryWriterService,
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
        await appSettings.LoadSettingsAsync();
        
        // 2. Загрузка пользовательских пресетов
        presetManager.RegisterIpcHandlers();
        
        // 3. Загрузка тем приложения
        localThemeSettingsService.RegisterIpcHandlers();
        
        // 4. Загрузка настроек TrayMon
        notifyIconsService.RegisterIpcHandlers();
        
        // 5. Загрузка настроек PowerMon
        powerMonSettingsService.RegisterIpcHandlers();
        
        // 6. Загрузка настроек PowerMon
        rtssSettingsService.RegisterIpcHandlers();
        
        // 7. Загрузка методов получения информации
        ipcHandlers.RegisterIpcHandlers();
        
        // 8. Обновление данных
        rawSharedMemoryWriterService.RegisterIpcHandlers();
        
        // 9. Обновление данных
        backgroundDataUpdater.StartAsync(_globalCts.Token);
        
        // 10. Создание пресетов под конкретное железо
        ocFinderService.LazyInitTdp();
        
        // 11. Загрузка методов изменения Power States
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
    /// <param name="cancellationToken">Токен отмены</param>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // 1. Сохранение пользовательских настроек
        presetManager.SaveSettings();
        
        // 2. Остановка обновления данных
        backgroundDataUpdater.Stop();
        await Task.CompletedTask;
    }
}