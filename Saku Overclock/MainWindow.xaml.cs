using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.UI.Xaml;
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
    private Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue;
    private UISettings settings;
    private Config config = new();
    private Devices devices = new();
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
    public NotifyIcon ni = new();
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
            ni.ContextMenuStrip = new ContextMenuStrip(); //Трей меню
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
            ni.Text = "Saku Overclock";
        }
        catch
        {

        }
        dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        settings = new UISettings();
        settings.ColorValuesChanged += Settings_ColorValuesChanged; // cannot use FrameworkElement.ActualThemeChanged event   
        DeviceLoad();
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
    private void Dispose_Tray(object sender, WindowEventArgs args)
    {
        try { ni.Dispose(); } catch { }
    }
    private async void Tray_Start() // Запустить все команды после запуска приложения если включен Автоприменять Разгон
    {
        ConfigLoad();
        try
        {
            if (config.autooverclock == true) { var cpu = App.GetService<ПараметрыPage>(); Applyer.Apply(false); /*cpu.Play_Invernate_QuickSMU(1);*/ if (devices.autopstate == true && devices.enableps == true) { cpu.BtnPstateWrite_Click(); } }
            if (config.AutostartType == 1 || config.AutostartType == 3) { await Task.Delay(700); this.Hide(); }
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
        private Config config = new();
        private static SendSMUCommand? sendSMUCommand;
        private static readonly Applyer mc = App.GetService<Applyer>();

        public static async void Apply(bool saveinfo)
        {
            try { sendSMUCommand = App.GetService<SendSMUCommand>(); } catch { return; }
            var smu = App.GetService<ПараметрыPage>();
            void ConfigLoad()
            {
                mc.config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json"))!;
            }
            void ConfigSave()
            {
                Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json", JsonConvert.SerializeObject(mc.config, Formatting.Indented));
            }
            ConfigLoad();
            if (mc.config.reapplytime == true)
            {
                var timer = new DispatcherTimer();
                try
                {
                    timer.Interval = TimeSpan.FromMilliseconds(mc.config.reapplytimer * 1000);
                }
                catch
                {
                    await App.MainWindow.ShowMessageDialogAsync("Время автообновления разгона некорректно и было исправлено на 3000 мс", "Критическая ошибка!");
                    mc.config.reapplytimer = 3000;
                    timer.Interval = TimeSpan.FromMilliseconds(mc.config.reapplytimer);
                }
                if (mc.config.execute == false)
                {
                    mc.config.execute = true;
                    ConfigSave();
                    timer.Tick += async (sender, e) =>
                    {
                        if (mc.config.reapplytime == true)
                        {
                            await Process(false); // Запустить ryzenadj снова, БЕЗ логирования, false
                            sendSMUCommand.Play_Invernate_QuickSMU(1); //Запустить кастомные SMU команды пользователя, которые он добавил в автостарт
                        }
                    }; timer.Start();
                }
                else
                {
                    timer.Stop();
                    timer.Tick += async (sender, e) =>
                    {
                        if (mc.config.reapplytime == true)
                        {
                            await Process(false); // Запустить ryzenadj снова, БЕЗ логирования, false
                            sendSMUCommand.Play_Invernate_QuickSMU(1); //Запустить кастомные SMU команды пользователя, которые он добавил в автостарт
                        }
                    }; timer.Start();
                }
            }
            await Process(saveinfo);
        }
        private static async Task Process(bool saveinfo)
        {
            try
            {
                mc.config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json"))!;
                if (mc.config == null) { return; }
                await Task.Run(() =>
                {

                    sendSMUCommand?.Translate(mc.config.adjline, saveinfo);

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
        if (file == 'd')
        {
            try
            {
                for (var j = 0; j < 5; j++)
                {
                    devices = new Devices();
                }
            }
            catch
            {
                Close();
            }
            if (devices != null)
            {
                try
                {
                    Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\devices.json", JsonConvert.SerializeObject(devices, Formatting.Indented));
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
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\devices.json");
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

    public void DeviceSave()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\devices.json", JsonConvert.SerializeObject(devices, Formatting.Indented));
        }
        catch { }
    }

    public void DeviceLoad()
    {
        try
        {
            devices = JsonConvert.DeserializeObject<Devices>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\devices.json"))!;
        }
        catch
        {
            JsonRepair('d');
        }
    }
    #endregion
}