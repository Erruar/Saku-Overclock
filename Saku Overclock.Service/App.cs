using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Saku_Overclock.Core.Contracts;
using Saku_Overclock.Core.Services;

namespace Saku_Overclock.Service;

public static class App
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "SakuOverclockService";
        });
        
        // Internal Services
        builder.Services.AddSingleton<IIpcSecurityService, IpcSecurityService>();
        
        // Core Services
        builder.Services.AddSingleton<IpcHub>();
        builder.Services.AddSingleton<IFileService, FileService>();
        builder.Services.AddSingleton<IAppSettingsService, AppSettingsService>();
        builder.Services.AddSingleton<IPresetManagerService, PresetManagerService>();
        builder.Services.AddSingleton<IPstateStrategy, Zen4PstateStrategy>();
        builder.Services.AddSingleton<IPstateStrategy, Zen5PstateStrategy>();
        builder.Services.AddSingleton<IPstateService, PstateService>();
        builder.Services.AddSingleton<ISensorIndexResolver, SensorIndexResolver>();
        builder.Services.AddSingleton<ISensorReader, SensorReader>();
        builder.Services.AddSingleton<CoreMetricsCalculator>();
        builder.Services.AddSingleton<IDataProvider, ZenstatesCoreProvider>();
        builder.Services.AddSingleton<ICpuService, CpuService>();
        builder.Services.AddSingleton<IOcFinderService, OcFinderService>();
        builder.Services.AddSingleton<IApplyerService, ApplyerService>();
        builder.Services.AddSingleton<IBackgroundDataUpdater, BackgroundDataUpdater>();
        builder.Services.AddSingleton<IPremadePresetManagementService, PremadePresetManagementService>();
        
        // Сначала регистрируем сервис активации/инициализации железного ядра
        builder.Services.AddHostedService<AppActivationWorker>();
        
        // Затем IPC воркер, чтобы пайпы открывались уже ПОСЛЕ того, как ядро инициализировано
        builder.Services.AddHostedService<IpcNamedPipeWorker>();
        
        await builder.Build().RunAsync();
    }
}