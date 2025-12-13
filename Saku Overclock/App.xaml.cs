using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Saku_Overclock.Activation;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Core.Contracts.Services;
using Saku_Overclock.Core.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Models;
using Saku_Overclock.Services;
using Saku_Overclock.SmuEngine;
using Saku_Overclock.ViewModels;
using Saku_Overclock.Views;
using WinRT.Interop;
using UIElement = Microsoft.UI.Xaml.UIElement;

namespace Saku_Overclock;

// To learn more about WinUI 3, see https://docs.microsoft.com/windows/apps/winui/winui3/.
public partial class App
{
    // The .NET Generic Host provides dependency injection, configuration, logging, and other services.
    // https://docs.microsoft.com/dotnet/core/extensions/generic-host
    // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
    // https://docs.microsoft.com/dotnet/core/extensions/configuration
    // https://docs.microsoft.com/dotnet/core/extensions/logging
    private IHost? Host
    {
        get;
    }

    public static T GetService<T>()
        where T : class
    {
        try
        {
            if ((Current as App)!.Host!.Services.GetService(typeof(T)) is not T service)
            {
                throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");
            }

            return service;
        }
        catch (Exception ex)
        {
            HandleCriticalError(new InvalidOperationException(
                $"Unable to get service {typeof(T).Name}", ex));
            Current.Exit();
            throw;
        }
    }

    // Глобальный экземпляр фонового обновлятора, зарегистрированный как синглтон через DI.
    public static IBackgroundDataUpdater? BackgroundUpdater
    {
        get; private set;
    }
    private static readonly CancellationTokenSource GlobalCts = new();

    public static IntPtr Hwnd => WindowNative.GetWindowHandle(MainWindow);

    public static bool EfficiencyModeAvailable => Environment.OSVersion.Version.Build >= 22631;
    public static WindowEx MainWindow
    {
        get;
    } = new MainWindow();

    public static UIElement? AppTitlebar
    {
        get;
        set;
    }

    public App()
    {
        InitializeComponent();

        CheckForSecondInstance();

        try
        {
            Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder().UseContentRoot(AppContext.BaseDirectory)
            .ConfigureServices((context, services) =>
            {
                // Default Activation Handler
                services.AddTransient<ActivationHandler<LaunchActivatedEventArgs>, DefaultActivationHandler>();
                // Other Activation Handlers
                services.AddTransient<IActivationHandler, AppNotificationActivationHandler>();
                // Core Services
                services.AddSingleton<IFileService, FileService>();
                // Services
                services.AddSingleton<IAppNotificationService, AppNotificationService>();
                services.AddSingleton<ILocalThemeSettingsService, LocalThemeSettingsService>();
                services.AddSingleton<IAppSettingsService, AppSettingsService>();
                services.AddSingleton<IApplyerService, ApplyerService>();
                services.AddSingleton<IRtssSettingsService, RtssSettingsService>();
                services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
                services.AddTransient<INavigationViewService, NavigationViewService>();
                services.AddSingleton<IActivationService, ActivationService>();
                services.AddSingleton<IPageService, PageService>();
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<ZenstatesCoreProvider>();
                services.AddSingleton<ISendSmuCommandService, SendSmuCommandService>();
                services.AddSingleton<IOcFinderService, OcFinderService>();
                services.AddSingleton<IBackgroundDataUpdater, BackgroundDataUpdater>();
                services.AddSingleton<ISensorIndexResolver, SensorIndexResolver>();
                services.AddSingleton<ISensorReader, SensorReader>();
                services.AddSingleton<CoreMetricsCalculator>();
                services.AddSingleton<IDataProvider, ZenstatesCoreProvider>();
                services.AddSingleton<IBackgroundDataUpdater, BackgroundDataUpdater>();
                // Views and ViewModels
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<SettingsPage>();
                services.AddTransient<КулерViewModel>();
                services.AddTransient<КулерPage>();
                services.AddTransient<AdvancedКулерViewModel>();
                services.AddTransient<AdvancedКулерPage>();
                services.AddTransient<AsusКулерViewModel>();
                services.AddTransient<AsusКулерPage>();
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
                services.AddTransient<ОбновлениеPage>();
                services.AddTransient<ОбновлениеViewModel>();
                services.AddTransient<ОбучениеPage>();
                services.AddTransient<ОбучениеViewModel>();
                // Configuration
                services.Configure<LocalSettingsOptions>(
                    context.Configuration.GetSection(nameof(LocalSettingsOptions)));
            }).Build();
            GetService<IAppNotificationService>().Initialize();
        }
        catch (Exception ex)
        {
            HandleCriticalError(ex);
            Current.Exit();
        }

        UnhandledException += (_,e) => 
        {
            // Перехватываем исключения от LiveCharts (баг с PointerCapture)
            if (((e.Exception is NullReferenceException)
                && e.Exception.Source == "LiveChartsCore.SkiaSharpView.WinUI") ||
                ((e.Exception is ArgumentException)
                && e.Exception.Source == "WinRT.Runtime"))
            {
                e.Handled = true; // TODO: Удалить когда LiveCharts починят (issue #2035)
                return;
            }

            HandleCriticalError(e.Exception); 
        };
    }

    #region JSON and Initialization

    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            BackgroundUpdater = GetService<IBackgroundDataUpdater>();
            _ = BackgroundUpdater.StartAsync(GlobalCts.Token);
            base.OnLaunched(args);
            GetService<IAppNotificationService>().Show(string.Format("AppNotificationSamplePayload".GetLocalized(),
                AppContext.BaseDirectory));

            await GetService<IActivationService>().ActivateAsync(args);

            await Task.Delay(1500);
            await Task.Run(() =>
            {
                SetPriorityClass(Process.GetCurrentProcess().Handle, /*NORMAL_PRIORITY_CLASS*/0x20);
            });
        }
        catch (Exception e)
        {
            await LogHelper.LogError(e);
        }
    }

    /// <summary>
    ///    Обработка критической ошибки приложения
    /// </summary>
    private static void HandleCriticalError(Exception e)
    {
        var pathToExecutableFile = Assembly.GetExecutingAssembly().Location;
        var pathToProgramDirectory = Path.GetDirectoryName(pathToExecutableFile);
        var sakuLogo = Path.Combine(pathToProgramDirectory!, "WindowIcon.ico");
        Process.Start(new ProcessStartInfo
        {
            FileName = "CrashHandler.exe",
            Arguments = $"\"{e.Message} {e.StackTrace} {e.InnerException?.StackTrace}\" -theme dark -appName \"Saku Overclock\" -iconPath \"{sakuLogo}\"",
            Verb = "runas"
        });
    }

    /// <summary>
    ///    Проверка на отсутствие других запущенных экземпляров приложения
    /// </summary>
    private static void CheckForSecondInstance()
    {
        var currentProcess = Process.GetCurrentProcess();
        var anotherProcess = Process.GetProcesses()
            .FirstOrDefault(p => p.ProcessName == currentProcess.ProcessName && p.Id != currentProcess.Id);

        // Если открыто ещё одно окно приложения
        if (anotherProcess != null)
        {
            var hWnd = ActivationInvokeHandler.FindMainWindowHwnd(null, "Saku Overclock");
            ActivationInvokeHandler.BringToFrontWindow(anotherProcess.MainWindowHandle);
            ActivationInvokeHandler.ChangeAllWindowState(hWnd, 5);
            ActivationInvokeHandler.ChangeWindowState(hWnd, 5);
            ActivationInvokeHandler.SwitchToMainWindow(hWnd, true);
            Current.Exit();
        }
    }

    /// <summary>
    ///    Фикс запуска в "режиме эффективности" на Windows 11
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetPriorityClass(IntPtr hProcess, uint dwPriorityClass);

    #endregion
}