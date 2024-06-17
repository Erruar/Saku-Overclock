using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json;
using Saku_Overclock.Activation;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Core.Contracts.Services;
using Saku_Overclock.Core.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Models;
using Saku_Overclock.Notifications;
using Saku_Overclock.Services;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.ViewModels;
using Saku_Overclock.Views;
using Application = Microsoft.UI.Xaml.Application;
using UIElement = Microsoft.UI.Xaml.UIElement;
namespace Saku_Overclock;
// To learn more about WinUI 3, see https://docs.microsoft.com/windows/apps/winui/winui3/.
public partial class App : Application
{
    // The .NET Generic Host provides dependency injection, configuration, logging, and other services.
    // https://docs.microsoft.com/dotnet/core/extensions/generic-host
    // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
    // https://docs.microsoft.com/dotnet/core/extensions/configuration
    // https://docs.microsoft.com/dotnet/core/extensions/logging
    public IHost Host
    {
        get;
    } 
    public static T GetService<T>()
        where T : class
    {
        if ((Current as App)!.Host.Services.GetService(typeof(T)) is not T service)
        {
            throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");
        }

        return service;
    } 
    public static WindowEx MainWindow { get; } = new MainWindow(); 
    public static UIElement? AppTitlebar { get; set; }
    private Config config = new();
    public App()
    {
        InitializeComponent();
        Host = Microsoft.Extensions.Hosting.Host.
        CreateDefaultBuilder().
        UseContentRoot(AppContext.BaseDirectory).
        ConfigureServices((context, services) =>
        { 
            // Default Activation Handler
            services.AddTransient<ActivationHandler<LaunchActivatedEventArgs>, DefaultActivationHandler>();
            // Other Activation Handlers
            services.AddTransient<IActivationHandler, AppNotificationActivationHandler>();
            // Services
            services.AddSingleton<IAppNotificationService, AppNotificationService>();
            services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
            services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
            services.AddTransient<INavigationViewService, NavigationViewService>();
            services.AddSingleton<IActivationService, ActivationService>();
            services.AddSingleton<IPageService, PageService>();
            services.AddSingleton<INavigationService, NavigationService>();
            // Core Services
            services.AddSingleton<IFileService, FileService>();
            // Views and ViewModels
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<SettingsPage>();
            services.AddTransient<КулерViewModel>();
            services.AddTransient<КулерPage>();
            services.AddTransient<AdvancedКулерViewModel>();
            services.AddTransient<AdvancedКулерPage>();
            services.AddTransient<ИнформацияViewModel>();
            services.AddTransient<ИнформацияPage>();
            services.AddTransient<ПараметрыViewModel>();
            services.AddTransient<ПараметрыPage>(); 
            services.AddTransient<ПресетыViewModel>();
            services.AddTransient<ПресетыPage>();
            services.AddTransient<ГлавнаяViewModel>();
            services.AddTransient<ГлавнаяPage>();
            services.AddTransient<ShellPage>();
            services.AddTransient<ShellViewModel>();
            services.AddTransient<MainWindow.Applyer>();
            services.AddTransient<SendSMUCommand>();
            // Configuration
            services.Configure<LocalSettingsOptions>(context.Configuration.GetSection(nameof(LocalSettingsOptions)));
        }).
        Build();
        GetService<IAppNotificationService>().Initialize();
        UnhandledException += App_UnhandledException;
    }
    #region JSON and Initialization
    public void ConfigLoad()
    {
        config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json"))!;
    }
    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    { 
        MessageBox.Show(e.Message + "\nRerun application and contact developer", "Critical Error!", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1); 
    } 
    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);
        GetService<IAppNotificationService>().Show(string.Format("AppNotificationSamplePayload".GetLocalized(), AppContext.BaseDirectory));
        bool isFirstInstance; // Проверяем, запущен ли уже экземпляр программы
        var mutex = new Mutex(true, "MyProgramMutex", out isFirstInstance); 
        if (!isFirstInstance)
        { 
            MessageBox.Show("Текущий экземпляр будет завершен через 3 секунды...", "Другой экземпляр программы уже запущен.");
            Thread.Sleep(3000);
            App.MainWindow.Close();
            mutex.ReleaseMutex();
            return; 
        }  
        await GetService<IActivationService>().ActivateAsync(args);
    }
    #endregion
}
