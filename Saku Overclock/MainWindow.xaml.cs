using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.UI.Xaml;
using Newtonsoft.Json;
using Saku_Overclock.Activation;
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
    public enum DWMWINDOWATTRIBUTE
    {
        DWMWA_WINDOW_CORNER_PREFERENCE = 33
    }
    public enum DWM_WINDOW_CORNER_PREFERENCE
    {
        DWMWCP_DEFAULT = 0,
        DWMWCP_DONOTROUND = 1,
        DWMWCP_ROUND = 2,
        DWMWCP_ROUNDSMALL = 3
    }
    private static NotifyIcon ni = new();
    [LibraryImport("dwmapi.dll", SetLastError = true)]
    private static partial long DwmSetWindowAttribute(IntPtr hwnd,
                                                     DWMWINDOWATTRIBUTE attribute,
                                                     ref DWM_WINDOW_CORNER_PREFERENCE pvAttribute,
                                                     uint cbAttribute);
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
        try
        {
            ni.Icon = new System.Drawing.Icon(GetType(), "WindowIcon.ico");
            ni.Visible = true;
            ni.DoubleClick +=
                    delegate (object sender, EventArgs args)
                    {
                        this.Show();
                        WindowState = WindowState.Normal;
                    }
            !;
            ni.ContextMenuStrip = new ContextMenuStrip() { Size = new System.Drawing.Size(300, 300) }; //Трей меню
            var attribute = DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE; //Закруглить трей меню на Windows 11
            var preference = DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND; //Закруглить трей меню на Windows 11
            DwmSetWindowAttribute(ni.ContextMenuStrip.Handle, attribute, ref preference, sizeof(uint)); //Закруглить трей меню на Windows 11 
            ni.ContextMenuStrip.Items.Add("Tray_Saku_Overclock".GetLocalized(), new System.Drawing.Bitmap(GetType(), "WindowIcon.ico"), Menu_Show1!);
            ni.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            ni.ContextMenuStrip.Items.Add("Tray_Presets".GetLocalized(), new System.Drawing.Bitmap(GetType(), "preset.png"), Open_Preset!);
            ni.ContextMenuStrip.Items.Add("Tray_Parameters".GetLocalized(), new System.Drawing.Bitmap(GetType(), "param.png"), Open_Param!);
            ni.ContextMenuStrip.Items.Add("Tray_Inforfation".GetLocalized(), new System.Drawing.Bitmap(GetType(), "info.png"), Open_Info!);
            ni.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            ni.ContextMenuStrip.Items.Add("Tray_Show".GetLocalized(), new System.Drawing.Bitmap(GetType(), "show.png"), Menu_Show1!);
            ni.ContextMenuStrip.Items.Add("Tray_Quit".GetLocalized(), new System.Drawing.Bitmap(GetType(), "exit.png"), Menu_Exit1!);
            ni.ContextMenuStrip.Items[0].Enabled = false;
            ni.ContextMenuStrip.Opacity = 0.89;
            ni.ContextMenuStrip.ForeColor = System.Drawing.Color.Purple;
            ni.ContextMenuStrip.BackColor = System.Drawing.Color.White;
            ni.Text = "Saku Overclock©";

            var processId = Environment.ProcessId;
            // Разрешаем второй инстанции установить фокус
            ActivationInvokeHandler.AllowSetForegroundWindow(processId);
        }
        catch
        {

        }
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
    public static void Remove_ContextMenu_Tray()
    {
        ni.ContextMenuStrip?.Items.Clear();
        ni.Text = "Saku Overclock©\nContext menu is disabled";
    }
    private void Dispose_Tray(object sender, WindowEventArgs args)
    {
        ni.Dispose();
        var workers = Process.GetProcessesByName("Saku Overclock");
        foreach (var worker in workers)
        {
            worker.Kill(); // Закрыть весь разгон, даже если открыт PowerMon или оверлей ProfileSwitcher
            worker.WaitForExit();
            worker.Dispose();
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
    private void Open_Preset(object sender, EventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ПресетыViewModel).FullName!);
        this.Show(); BringToFront();
    }
    private void Open_Param(object sender, EventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ПараметрыViewModel).FullName!);
        this.Show(); BringToFront();
    }
    private void Open_Info(object sender, EventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ИнформацияViewModel).FullName!);
        this.Show(); BringToFront();
    }
    private void Menu_Show1(object sender, EventArgs e)
    {
        ni = new NotifyIcon
        {
            Visible = true
        };
        this.Show(); BringToFront();
    }
    private void Menu_Exit1(object sender, EventArgs e)
    {
        ni = new NotifyIcon
        {
            Visible = false
        }; Close();
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