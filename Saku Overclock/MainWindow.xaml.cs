using System.Diagnostics;
using Microsoft.UI.Xaml;
using Newtonsoft.Json;
using Saku_Overclock.Helpers;
using Windows.UI.ViewManagement;
using Microsoft.UI.Xaml.Media.Imaging;
using H.NotifyIcon;
using Hardcodet.Wpf.TaskbarNotification;
using System.Windows.Forms;
using Microsoft.UI.Xaml.Controls;
using System.Drawing;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.ViewModels;
using System.Runtime.InteropServices;
using Microsoft.Win32;
namespace Saku_Overclock;

public sealed partial class MainWindow : WindowEx
{
    private Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue;

    private UISettings settings;

    private Config config = new Config();

    private Devices devices = new Devices();

    private Profile profile = new Profile();

    public MainWindow()
    {
        InitializeComponent();
        this.WindowStateChanged += (sender, e) =>
        {
            if(this.WindowState == WindowState.Minimized)
            { 
                // Скройте окно
                this.Hide();
            }
        };
        ConfigLoad();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets/WindowIcon.ico"));
        Content = null;
        Title = "AppDisplayName".GetLocalized();
        NotifyIcon ni = new NotifyIcon();
        ni.Icon = new System.Drawing.Icon(GetType(),"WindowIcon.ico");
        ni.Visible = true;
        ni.DoubleClick +=
                delegate (object sender, EventArgs args)
                {
                    this.Show();
                    this.WindowState = WindowState.Normal;
                };
        ni.ContextMenuStrip = new ContextMenuStrip();

        
        System.Drawing.Image bmp = new System.Drawing.Bitmap(GetType(), "show.png");
        System.Drawing.Image bmp1 = new System.Drawing.Bitmap(GetType(), "exit.png");
        System.Drawing.Image bmp2 = new System.Drawing.Bitmap(GetType(), "WindowIcon.ico");
        System.Drawing.Image bmp3 = new System.Drawing.Bitmap(GetType(), "preset.png");
        System.Drawing.Image bmp4 = new System.Drawing.Bitmap(GetType(), "param.png");
        System.Drawing.Image bmp5 = new System.Drawing.Bitmap(GetType(), "info.png");
        ni.ContextMenuStrip.Items.Add("Saku Overclock", bmp2, Menu_Show1);
        ni.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        ni.ContextMenuStrip.Items.Add("Presets", bmp3, Open_Preset);
        ni.ContextMenuStrip.Items.Add("Parameters", bmp4, Open_Param);
        ni.ContextMenuStrip.Items.Add("Inforfation", bmp5, Open_Info);
        ni.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        ni.ContextMenuStrip.Items.Add("Show app", bmp, Menu_Show1);
        ni.ContextMenuStrip.Items.Add("Quit", bmp1, Menu_Exit1);
        ni.ContextMenuStrip.Items[0].Enabled = false;
        
        ni.Text = "Saku Overclock";   
        // Theme change code picked from https://github.com/microsoft/WinUI-Gallery/pull/1239
        dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        settings = new UISettings();
        settings.ColorValuesChanged += Settings_ColorValuesChanged; // cannot use FrameworkElement.ActualThemeChanged event
        DeviceLoad();
        ProfileLoad();
        Tray_Start();
    }
    private async void Tray_Start()
    {
        ConfigLoad();
        if (config.traystart == true)
        {
            await Task.Delay(700);
            this.Hide();
        }
    }
    void Open_Preset(object sender, EventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ПресетыViewModel).FullName!);
        this.Show();
        this.BringToFront();
    }
    void Open_Param(object sender, EventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ПараметрыViewModel).FullName!);
        this.Show();
        this.BringToFront();
    }
    void Open_Info(object sender, EventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ИнформацияViewModel).FullName!);
        this.Show();
        this.BringToFront();
    }
    void Menu_Show1(object sender, EventArgs e)
    {
        System.Windows.Forms.NotifyIcon ni = new System.Windows.Forms.NotifyIcon();
        ni.Visible = true;
        this.Show();
        this.BringToFront();
    }
    protected void Menu_Exit1(object sender, EventArgs e)
    {
        System.Windows.Forms.NotifyIcon ni = new System.Windows.Forms.NotifyIcon();
        ni.Visible = false;
        Close();
    }
    public class Applyer
    {
        public bool execute = false;

        private Config config = new Config();
        public static void Apply()
        {
            
            var mc = new Applyer();
            
            void ConfigLoad()
            {
                mc.config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json"));
            }
            void ConfigSave()
            {
                    Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json", JsonConvert.SerializeObject(mc.config));
            }

            ConfigLoad();
            

            if (mc.config.reapplytime == true)
            {
                DispatcherTimer timer = new DispatcherTimer();
                try
                {
                    ConfigLoad();
                    timer.Interval = TimeSpan.FromMilliseconds(mc.config.reapplytimer * 1000);
                }
                catch
                {
                    App.MainWindow.ShowMessageDialogAsync("Время автообновления разгона некорректно и было исправлено на 3000 мс", "Критическая ошибка!");
                    mc.config.reapplytimer = 3000;
                    ConfigLoad();
                    timer.Interval = TimeSpan.FromMilliseconds(mc.config.reapplytimer);
                }
                ConfigLoad();
                if (mc.config.execute == false)
                {
                    mc.config.execute = true;
                    ConfigSave();
                    timer.Tick += (sender, e) =>
                    {
                        if (mc.config.reapplytime == true)
                        {
                            // Запустите ryzenadj снова
                            Process();
                        }
                    };
                    timer.Start();
                }
                else
                {
                    timer.Stop();
                    mc.config.execute = false;
                    ConfigSave();
                }
                
            }
            else
            {
                Process();
            }
            void Process()
            {
                Process p = new Process();
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.FileName = @"ryzenadj.exe";
                ConfigLoad();
                p.StartInfo.Arguments = mc.config.adjline;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;

                p.Start();
                //App.MainWindow.ShowMessageDialogAsync("Вы успешно выставили свои настройки! \n" + mc.config.adjline, "Применение успешно!");
                
            }
        }
    }

    // this handles updating the caption button colors correctly when indows system theme is changed
    // while the app is open
    private void Settings_ColorValuesChanged(UISettings sender, object args)
    {
        // This calls comes off-thread, hence we will need to dispatch it to current app's thread
        dispatcherQueue.TryEnqueue(() =>
        {
            TitleBarHelper.ApplySystemThemeToCaptionButtons();
        });
    }

    //Json
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
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json", JsonConvert.SerializeObject(config));
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
                for (int j = 0; j < 5; j++)
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
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\devices.json", JsonConvert.SerializeObject(devices));
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
        if (file != 'p')
        {
            try
            {
                for (int j = 0; j < 5; j++)
                {
                    profile = new Profile();
                }
            }
            catch
            {
                Close();
            }
            if (profile != null)
            {
                try
                {
                    Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json", JsonConvert.SerializeObject(profile));
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
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json");
                    Close();
                }
                catch
                {
                    Close();
                }
            }
        }
        if (profile != null)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json", JsonConvert.SerializeObject(profile));
                return;
            }
            catch
            {
                Close();
                return;
            }
        }
        try
        {
            File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json");
            Close();
        }
        catch
        {
            Close();
        }
    }

    public void ConfigSave()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json", JsonConvert.SerializeObject(config));
        }
        catch (Exception ex)
        {

        }
    }

    public void ConfigLoad()
    {
        try
        {
            config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json"));
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
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\devices.json", JsonConvert.SerializeObject(devices));
        }
        catch (Exception ex)
        {

        }
    }

    public void DeviceLoad()
    {
        try
        {
            devices = JsonConvert.DeserializeObject<Devices>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\devices.json"));
        }
        catch
        {
            JsonRepair('d');
        }
    }

    public void ProfileSave()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json", JsonConvert.SerializeObject(profile));
        }
        catch (Exception ex)
        {

        }
    }

    public void ProfileLoad()
    {
        try
        {
            profile = JsonConvert.DeserializeObject<Profile>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json"));
        }
        catch
        {
            JsonRepair('p');
        }
    }

}
