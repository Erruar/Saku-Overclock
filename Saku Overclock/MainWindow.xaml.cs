using System.Diagnostics;
using H.NotifyIcon;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Services;
using Saku_Overclock.ViewModels;
using Saku_Overclock.Views;
using Windows.UI.ViewManagement;
using Saku_Overclock.JsonContainers;
using Icon = System.Drawing.Icon;

namespace Saku_Overclock;

public sealed partial class MainWindow
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly UISettings _settings;
    private static TaskbarIcon? _ni;
    private static TaskbarIcon? _niBackup;
    private static readonly IAppSettingsService SettingsService = App.GetService<IAppSettingsService>();
    private static readonly ISendSmuCommandService SendSmuCommand = App.GetService<ISendSmuCommandService>();
    private static readonly IOcFinderService OcFinder = App.GetService<IOcFinderService>();
    private bool _isActivated;

    public MainWindow()
    {
        InitializeComponent();
        WindowStateChanged += (_, _) =>
        {
            if (SettingsService.HidingType == 1 && WindowState == WindowState.Minimized)
            {
                this.Hide();
            }
        };
        Activated += (_, _) =>
        {
            if (SettingsService.AppFirstRun && !_isActivated)
            {
                _isActivated = true;
                /*var navigationService = App.GetService<INavigationService>();
                navigationService.NavigateTo(typeof(ОбучениеViewModel).FullName!);*/
            }
        };

        AppWindow.Closing += AppWindow_Closing;
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets/WindowIcon.ico"));
        Content = null;
        Title = "AppDisplayName".GetLocalized();
        var showHideWindowCommand = (XamlUICommand)Application.Current.Resources["ShowHideWindowCommand"];
        showHideWindowCommand.ExecuteRequested += ShowHideWindowCommand_ExecuteRequested;

        var exitApplicationCommand = (XamlUICommand)Application.Current.Resources["ExitApplicationCommand"];
        exitApplicationCommand.ExecuteRequested += ExitApplicationCommand_ExecuteRequested;

        var powerMonCommand = (XamlUICommand)Application.Current.Resources["Command2"];
        powerMonCommand.ExecuteRequested += PowerMonOpen_ExecuteRequested;

        var settingsCommand = (XamlUICommand)Application.Current.Resources["Command3"];
        settingsCommand.ExecuteRequested += SettingsOpen_ExecuteRequested;

        var appProfilesCommand = (XamlUICommand)Application.Current.Resources["Command4"];
        appProfilesCommand.ExecuteRequested += ProfilesOpen_ExecuteRequested;

        var overclockPageCommand = (XamlUICommand)Application.Current.Resources["Command5"];
        overclockPageCommand.ExecuteRequested += OverclockPageOpen_ExecuteRequested;

        var informationPageCommand = (XamlUICommand)Application.Current.Resources["Command6"];
        informationPageCommand.ExecuteRequested += InfoPageOpen_ExecuteRequested;

        var coolerTuneCommand = (XamlUICommand)Application.Current.Resources["Command7"];
        coolerTuneCommand.ExecuteRequested += CoolerPageOpen_ExecuteRequested;

        var ecoModeCommand = (XamlUICommand)Application.Current.Resources["Command8"];
        ecoModeCommand.ExecuteRequested += EcoMode_ExecuteRequested;

        var sakuLogoCommand = (XamlUICommand)Application.Current.Resources["Command1"];
        sakuLogoCommand.ExecuteRequested += GithubLink_ExecuteRequested;

        _ni = (TaskbarIcon)Application.Current.Resources["TrayIcon"];
        _ni.ForceCreate(enablesEfficiencyMode: App.EfficiencyModeAvailable);

        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _settings = new UISettings();
        _settings.ColorValuesChanged +=
            Settings_ColorValuesChanged;
        _ = Tray_Start();
        Closed += Dispose_Tray;
    }

    #region Colours

    // this handles updating the caption button colors correctly when Windows system theme is changed
    // while the app is open
    private void Settings_ColorValuesChanged(UISettings sender, object args)
    {
        // This calls comes off-thread, hence we will need to dispatch it to current app's thread
        _dispatcherQueue.TryEnqueue(TitleBarHelper.ApplySystemThemeToCaptionButtons);
    }

    #endregion

    #region Tray utils

    private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (SettingsService.HidingType == 2)
        {
            args.Cancel = true; // Отменяем закрытие
            this.Hide(); // Скрываем в трей
        }
        else
        {
            App.BackgroundUpdater?.Stop();
        }
    }

    public static void Set_ContextMenu_Tray()
    {
        _niBackup?.Dispose();

        if (_ni != null)
        {
            _ni.Visibility = Visibility.Visible;
            _ni.ForceCreate(enablesEfficiencyMode: App.EfficiencyModeAvailable);
        }
    }

    public static void Remove_ContextMenu_Tray()
    {
        if (_ni == null)
        {
            return;
        }

        _ni.Visibility = Visibility.Collapsed;
        _niBackup?.Dispose();
        _niBackup = new TaskbarIcon
        {
            ToolTipText = "Saku Overclock©\nContext menu is disabled",
            Icon = new Icon("Assets/WindowIcon.ico"),
            Id = Guid.NewGuid()
        };
        XamlUICommand xamlUiCommand = new();
        xamlUiCommand.ExecuteRequested += static (_, _) =>
        {
            if (App.MainWindow.Visible)
            {
                App.MainWindow.Hide();
            }
            else
            {
                App.MainWindow.Show();
                App.MainWindow.BringToFront();
                App.MainWindow.WindowState = WindowState.Normal;
            }
        };
        _niBackup.LeftClickCommand = xamlUiCommand;
        _niBackup.ForceCreate(enablesEfficiencyMode: App.EfficiencyModeAvailable);
    }

    private static void Dispose_Tray(object sender, WindowEventArgs args)
    {
        _ni?.Dispose();
        var workers = Process.GetProcessesByName("Saku Overclock");
        foreach (var worker in workers)
        {
            worker.Kill(); // Закрыть весь разгон, даже если открыт PowerMon или оверлей ProfileSwitcher
            worker.WaitForExit();
            worker.Dispose();
        }
    }

    private static void PowerMonOpen_ExecuteRequested(object? _, ExecuteRequestedEventArgs args)
    {
        try
        {
            var newWindow = new PowerWindow();
            var micaBackdrop = new MicaBackdrop
            {
                Kind = MicaKind.BaseAlt
            };
            newWindow.SystemBackdrop = micaBackdrop;
            newWindow.Activate();
        }
        catch (Exception ex)
        {
            throw new Exception(ex.ToString());
        }
    }

    private static void SettingsOpen_ExecuteRequested(object? _, ExecuteRequestedEventArgs args)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(SettingsViewModel).FullName!);
        App.MainWindow.Show();
        App.MainWindow.BringToFront();
        App.MainWindow.WindowState = WindowState.Normal;
    }

    private static void ProfilesOpen_ExecuteRequested(object? _, ExecuteRequestedEventArgs args)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ПресетыViewModel).FullName!);
        App.MainWindow.Show();
        App.MainWindow.BringToFront();
        App.MainWindow.WindowState = WindowState.Normal;
    }

    private static void OverclockPageOpen_ExecuteRequested(object? _, ExecuteRequestedEventArgs args)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ПараметрыViewModel).FullName!);
        App.MainWindow.Show();
        App.MainWindow.BringToFront();
        App.MainWindow.WindowState = WindowState.Normal;
    }

    private static void InfoPageOpen_ExecuteRequested(object? _, ExecuteRequestedEventArgs args)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ИнформацияViewModel).FullName!);
        App.MainWindow.Show();
        App.MainWindow.BringToFront();
        App.MainWindow.WindowState = WindowState.Normal;
    }

    private static void CoolerPageOpen_ExecuteRequested(object? _, ExecuteRequestedEventArgs args)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(КулерViewModel).FullName!);
        App.MainWindow.Show();
        App.MainWindow.BringToFront();
        App.MainWindow.WindowState = WindowState.Normal;
    }

    private void EcoMode_ExecuteRequested(object? sender, ExecuteRequestedEventArgs args)
    {
        SettingsService.Preset = -1;
        SettingsService.PremadeEcoActivated = true;
        SettingsService.PremadeBalanceActivated = false;
        SettingsService.PremadeMaxActivated = false;
        SettingsService.PremadeMinActivated = false;
        SettingsService.PremadeSpeedActivated = false;

        ShellPage.SelectPremadePreset("Eco");

        var (_, _, _, settings, _) = ShellPage.PremadedPresets["Eco"];

        SettingsService.RyzenAdjLine = settings;
        SettingsService.SaveSettings();

        _ = App.GetService<ApplyerService>().ApplyWithoutAdjLine(true);

        App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationEco".GetLocalized(),
                AppContext.BaseDirectory));
    }

    private static void GithubLink_ExecuteRequested(object? _, ExecuteRequestedEventArgs args) =>
        Process.Start(new ProcessStartInfo("https://github.com/Erruar/Saku-Overclock/") { UseShellExecute = true });

    private void ShowHideWindowCommand_ExecuteRequested(object? _, ExecuteRequestedEventArgs args)
    {
        if (App.MainWindow.Visible)
        {
            App.MainWindow.Hide();
        }
        else
        {
            App.MainWindow.Show();
            BringToFront();
            App.MainWindow.WindowState = WindowState.Normal;
        }
    }

    private static void ExitApplicationCommand_ExecuteRequested(object? _, ExecuteRequestedEventArgs args)
    {
        _ni?.Dispose();
        App.MainWindow.Close();
        Environment.Exit(0);
    }

    private async Task Tray_Start() // Запустить все команды после запуска приложения если включен Авто-применять Разгон
    {
        SettingsService.LoadSettings();

        OcFinder.LazyInitTdp();

        if (SettingsService.ReapplyLatestSettingsOnAppLaunch)
        {
            try
            {
                SendSmuCommand.ApplyQuickSmuCommand(true);
            }
            catch (Exception ex)
            {
                await LogHelper.LogError("[Mainwindow@Tray_Start]::ApplyQuickSmuCommand_FAILED - " + ex);
            }

            try
            {
                var profileFolderFile = Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                                    @"\SakuOverclock\profile.json";
                if (File.Exists(profileFolderFile))
                {
                    var profile = JsonConvert.DeserializeObject<Profile[]>(await File.ReadAllTextAsync(
                        Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                        @"\SakuOverclock\profile.json"));
                    if (profile == null)
                    {
                        var profiles = new Profile[1];
                        Directory.CreateDirectory(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                        await File.WriteAllTextAsync(
                            Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\profile.json",
                            JsonConvert.SerializeObject(profiles));
                        return;
                    }

                    var applyService = App.GetService<IApplyerService>();
                    if (SettingsService.Preset != -1)
                    {
                        if (SettingsService.Preset < profile.Length)
                        {
                            await applyService.ApplyCustomPreset(profile[SettingsService.Preset]);
                            if (profile[SettingsService.Preset].AutoPstate &&
                                profile[SettingsService.Preset].EnablePstateEditor)
                            {
                                ПараметрыPage.WritePstates();
                            }
                        }
                    }
                    else
                    {
                        var presetType = PresetType.Balance;

                        if (SettingsService.PremadeMaxActivated)
                        {
                            presetType = PresetType.Max;
                        }
                        else if (SettingsService.PremadeSpeedActivated)
                        {
                            presetType = PresetType.Performance;
                        }
                        else if (SettingsService.PremadeEcoActivated)
                        {
                            presetType = PresetType.Eco;
                        }
                        else if (SettingsService.PremadeMinActivated)
                        {
                            presetType = PresetType.Min;
                        }

                        await applyService.ApplyPremadePreset(presetType, (OptimizationLevel)SettingsService.PremadeOptimizationLevel);
                    }
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogError("[Mainwindow@Tray_Start]::Apply_Last_Settings_FAILED - " + ex);
            }
        }
        try
        {
            // Параллельный поток для выполнения задачи
            await Task.Run(async () =>
            {
                while (!_contentLoaded)
                {
                    await Task.Delay(50);
                }

                // После того как _contentLoaded стал true, выполняем условие
                if (SettingsService.AutostartType is 1 or 3)
                {
                    this.Hide();
                }
            });
            // Генерация строки с информацией о релизах
            await UpdateChecker.GenerateReleaseInfoString();
            // Вызов проверки обновлений
            await UpdateChecker.CheckForUpdates();
        }
        catch (Exception ex)
        {
            await LogHelper.LogError("[Mainwindow@Tray_Start]::App_Hide_And_UpdateChecker_FATAL - " + ex);
        }
    }

    #endregion

}