using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Saku_Overclock.Contracts.Services;
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
        builder.Services.AddSingleton<CoreIpcHandlers>();
        builder.Services.AddSingleton<IFileService, FileService>();
        builder.Services.AddSingleton<IAppSettingsService, AppSettingsService>();
        builder.Services.AddSingleton<IPresetManagerService, PresetManagerService>();
        builder.Services.AddSingleton<ILocalThemeSettingsService, LocalThemeSettingsService>();
        builder.Services.AddSingleton<INotifyIconsService, NotifyIconsService>();
        builder.Services.AddSingleton<IPowerMonSettingsService, PowerMonSettingsService>();
        builder.Services.AddSingleton<IRtssSettingsService, RtssSettingsService>();
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
        
        // Сервис активации/инициализации ядра
        builder.Services.AddHostedService<AppActivationWorker>();
        
        // IPC воркер
        builder.Services.AddHostedService<IpcNamedPipeWorker>();
        
        await builder.Build().RunAsync();
    }
}