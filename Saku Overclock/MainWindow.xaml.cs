using System.Diagnostics;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.ViewModels;
using Saku_Overclock.Views;
using Windows.UI.ViewManagement;

namespace Saku_Overclock;

public sealed partial class MainWindow : WindowEx
{
    private readonly Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue;
    private readonly UISettings settings;
    private Config config = new();
    private static TaskbarIcon? ni;

    public MainWindow()
    {
        InitializeComponent();
        WindowStateChanged += (sender, e) =>
        {
            if (WindowState == WindowState.Minimized)
            {
                this.Hide();
            }
        };
        ConfigLoad();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets/WindowIcon.ico"));
        Content = null;
        Title = "AppDisplayName".GetLocalized();
        /* try
         {*/
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

        ni = (TaskbarIcon)Application.Current.Resources["TrayIcon"];
        ni.ForceCreate();
        /* }
         catch
         { 

         }*/
        dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        settings = new UISettings();
        settings.ColorValuesChanged += Settings_ColorValuesChanged; // cannot use FrameworkElement.ActualThemeChanged event   
        Tray_Start();
        Closed += Dispose_Tray;
    }
    #region Colours 
    // this handles updating the caption button colors correctly when indows system theme is changed
    // while the app is open
    private void Settings_ColorValuesChanged(UISettings sender, object args)
    {
        // This calls comes off-thread, hence we will need to dispatch it to current app's thread
        dispatcherQueue.TryEnqueue(TitleBarHelper.ApplySystemThemeToCaptionButtons);
    }
    #endregion
    #region Tray utils 
    public static void Set_ContextMenu_Tray()
    {
        ni = (TaskbarIcon)Application.Current.Resources["TrayIcon"];
    }
    public static void Remove_ContextMenu_Tray()
    { 
        if (ni == null) { return; }
        ni.ContextFlyout = null;
        ni.ToolTipText = "Saku Overclock©\nContext menu is disabled";
    }
    private void Dispose_Tray(object sender, WindowEventArgs args)
    {
        ni?.Dispose();
        var workers = Process.GetProcessesByName("Saku Overclock");
        foreach (var worker in workers)
        {
            worker.Kill(); // Закрыть весь разгон, даже если открыт PowerMon или оверлей ProfileSwitcher
            worker.WaitForExit();
            worker.Dispose();
        }
    }
    private void PowerMonOpen_ExecuteRequested(object? _, ExecuteRequestedEventArgs args)
    {
        try
        { 
            var newWindow = new PowerWindow(CpuSingleton.GetInstance());
            var micaBackdrop = new MicaBackdrop
            {
                Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt
            };
            newWindow.SystemBackdrop = micaBackdrop;
            newWindow.Activate();
        }
        catch (Exception ex) { throw new Exception(ex.ToString()); }
    } 
    private void SettingsOpen_ExecuteRequested(object? _, ExecuteRequestedEventArgs args)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(SettingsViewModel).FullName!);
        App.MainWindow.Show(); App.MainWindow.BringToFront(); App.MainWindow.WindowState = WindowState.Normal;
    }
    private void ProfilesOpen_ExecuteRequested(object? _, ExecuteRequestedEventArgs args)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ПресетыViewModel).FullName!);
        App.MainWindow.Show(); App.MainWindow.BringToFront(); App.MainWindow.WindowState = WindowState.Normal;
    }
    private void OverclockPageOpen_ExecuteRequested(object? _, ExecuteRequestedEventArgs args)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ПараметрыViewModel).FullName!);
        App.MainWindow.Show(); App.MainWindow.BringToFront(); App.MainWindow.WindowState = WindowState.Normal;
    }
    private void InfoPageOpen_ExecuteRequested(object? _, ExecuteRequestedEventArgs args)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ИнформацияViewModel).FullName!);
        App.MainWindow.Show(); App.MainWindow.BringToFront(); App.MainWindow.WindowState = WindowState.Normal;
    }
    private void CoolerPageOpen_ExecuteRequested(object? _, ExecuteRequestedEventArgs args)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(КулерViewModel).FullName!);
        App.MainWindow.Show(); App.MainWindow.BringToFront(); App.MainWindow.WindowState = WindowState.Normal;
    }
    private void EcoMode_ExecuteRequested(object? _, ExecuteRequestedEventArgs args)
    {
        ConfigLoad();
        config.PremadeEcoActivated = true;
        config.PremadeBalanceActivated = false;
        config.PremadeMaxActivated = false;
        config.PremadeMinActivated = false;
        config.PremadeSpeedActivated = false;
        ConfigSave();
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ПресетыViewModel).FullName!);
        App.MainWindow.Show(); App.MainWindow.BringToFront();
    }
    private void GithubLink_ExecuteRequested(object? _, ExecuteRequestedEventArgs args)
    { 
        Process.Start(new ProcessStartInfo("https://github.com/Erruar/Saku-Overclock/") { UseShellExecute = true });
    }
    private void ShowHideWindowCommand_ExecuteRequested(object? _, ExecuteRequestedEventArgs args)
    {
        if (App.MainWindow.Visible)
        {
            App.MainWindow.Hide();
        }
        else
        {
            App.MainWindow.Show(); BringToFront(); App.MainWindow.WindowState = WindowState.Normal;
        }
    }

    private void ExitApplicationCommand_ExecuteRequested(object? _, ExecuteRequestedEventArgs args)
    {
        ni?.Dispose();
        App.MainWindow?.Close(); 
        if (App.MainWindow == null)
        {
            Environment.Exit(0);
        }
    }
    private async void Tray_Start() // Запустить все команды после запуска приложения если включен Автоприменять Разгон
    {
        ConfigLoad();
        try
        {
            if (config.ReapplyLatestSettingsOnAppLaunch == true)
            {
                var cpu = App.GetService<ПараметрыPage>(); Applyer.Apply(config.RyzenADJline, false, config.ReapplyOverclock, config.ReapplyOverclockTimer);
                /*cpu.Play_Invernate_QuickSMU(1);*/
                var profile = JsonConvert.DeserializeObject<Profile[]>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json"))!;

                if (profile != null && profile[config.Preset] != null && profile[config.Preset].autoPstate == true && profile[config.Preset].enablePstateEditor == true)
                {
                    ПараметрыPage.WritePstates();
                }
            }
            if (config.AutostartType == 1 || config.AutostartType == 3) { await Task.Delay(700); this.Hide(); }
            // Генерация строки с информацией о релизах
            await UpdateChecker.GenerateReleaseInfoString();
            // Вызов проверки обновлений
            await UpdateChecker.CheckForUpdates();
        }
        catch
        {
            JsonRepair('c');
            JsonRepair('d');
        }
    }  
    #endregion
    #region Applyer class
    public class Applyer
    {
        public bool execute = false;
        private static Config config = new();
        private static SendSMUCommand? sendSMUCommand;
        public static readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(3 * 1000) };
        private static EventHandler<object>? tickHandler;
        public static void ApplyWithoutADJLine(bool saveinfo)
        {
            try
            {
                config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json"))!;
            }
            catch
            {
                config = new Config();
            }
            Apply(config.RyzenADJline, saveinfo, config.ReapplyOverclock, config.ReapplyOverclockTimer);
        }

        public static async void Apply(string RyzenADJline, bool saveinfo, bool ReapplyOverclock, double ReapplyOverclockTimer)
        {
            try { sendSMUCommand = App.GetService<SendSMUCommand>(); } catch { return; }
            if (ReapplyOverclock == true)
            {
                try
                {
                    timer.Interval = TimeSpan.FromMilliseconds(ReapplyOverclockTimer * 1000);
                    timer.Stop();
                }
                catch
                {
                    await App.MainWindow.ShowMessageDialogAsync("Время автообновления разгона некорректно и было исправлено на 3000 мс", "Критическая ошибка!");
                    ReapplyOverclockTimer = 3000;
                    timer.Interval = TimeSpan.FromMilliseconds(ReapplyOverclockTimer);
                }
                if (tickHandler != null)
                {
                    timer.Tick -= tickHandler;  // Удаляем старый обработчик
                }
                tickHandler = async (sender, e) =>
                {
                    if (ReapplyOverclock)
                    {
                        await Process(RyzenADJline, false); // Запустить SendSMUCommand снова, БЕЗ логирования, false
                        sendSMUCommand?.Play_Invernate_QuickSMU(1); // Запустить кастомные SMU команды пользователя, которые он добавил в автостарт
                    }
                };

                timer.Tick += tickHandler;  // Добавляем новый обработчик
                timer.Start();
            }
            else
            {
                timer.Stop();
            }
            await Process(RyzenADJline, saveinfo);
        }
        private static async Task Process(string ADJLine, bool saveinfo)
        {
            try
            {
                await Task.Run(() =>
                {
                    sendSMUCommand?.Translate(ADJLine, saveinfo);
                });
            }
            catch
            {

            }
        }
    }
    #endregion
    #region JSON Containers voids
    public void JsonRepair(char file)
    {
        if (file == 'c')
        {
            try
            {
                config = new Config();
            }
            catch
            {
                Close();
            }
            if (config != null)
            {
                try
                {
                    Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json", JsonConvert.SerializeObject(config, Formatting.Indented));
                }
                catch
                {
                    Close();
                }
            }
            else
            {
                try
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json");
                    Close();
                }
                catch
                {
                    Close();
                }
            }
        }
    }

    public void ConfigSave()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json", JsonConvert.SerializeObject(config, Formatting.Indented));
        }
        catch { }
    }

    public void ConfigLoad()
    {
        try
        {
            config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json"))!;
        }
        catch
        {
            JsonRepair('c');
        }
    }
    #endregion
}