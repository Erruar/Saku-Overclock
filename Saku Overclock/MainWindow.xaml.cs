using System.Diagnostics;
using Microsoft.UI.Xaml;
using Newtonsoft.Json;
using Saku_Overclock.Helpers;
using Windows.UI.ViewManagement;
using System.Windows.Forms;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.ViewModels;
namespace Saku_Overclock;
#pragma warning disable IDE0044 // Добавить модификатор только для чтения
#pragma warning disable CS8601 // Возможно, назначение-ссылка, допускающее значение NULL.
#pragma warning disable CS8622 // Допустимость значений NULL для ссылочных типов в типе параметра не соответствует целевому объекту делегирования (возможно, из-за атрибутов допустимости значений NULL).
public sealed partial class MainWindow : WindowEx
{
    private Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue;
    private UISettings settings;
    private Config config = new();
    private Devices devices = new();
    private Profile profile = new();
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
        ni.ContextMenuStrip.Items.Add("Tray_Saku_Overclock".GetLocalized(), bmp2, Menu_Show1);
        ni.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        ni.ContextMenuStrip.Items.Add("Tray_Presets".GetLocalized(), bmp3, Open_Preset);
        ni.ContextMenuStrip.Items.Add("Tray_Parameters".GetLocalized(), bmp4, Open_Param);
        ni.ContextMenuStrip.Items.Add("Tray_Inforfation".GetLocalized(), bmp5, Open_Info);
        ni.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        ni.ContextMenuStrip.Items.Add("Tray_Show".GetLocalized(), bmp, Menu_Show1);
        ni.ContextMenuStrip.Items.Add("Tray_Quit".GetLocalized(), bmp1, Menu_Exit1);
        ni.ContextMenuStrip.Items[0].Enabled = false;
        ni.Text = "Saku Overclock";
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
    void Menu_Exit1(object sender, EventArgs e)
    {
        System.Windows.Forms.NotifyIcon ni = new System.Windows.Forms.NotifyIcon();
        ni.Visible = false;
        Close();
    }
    public class Applyer
    {
        public bool execute = false;
        private Config config = new();
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
        public static void FanInfo()
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
                if (mc.config.fanex == false)
                {
                    mc.config.fanex = true;
                    ConfigSave();
                    timer.Tick += (sender, e) =>
                    {

                        // Запустите faninfo снова
                        Process();
                        
                    };
                    timer.Start();
                }
                else
                {
                    timer.Stop();
                    mc.config.fanex = false;
                    ConfigSave();
                }

            }
            else
            {
                Process();
            }
            void Process()
            {
                ConfigLoad();
                mc.config.fan1v = "";
                ConfigSave();
                Process p = new Process();
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.FileName = @"nbfc/nbfc.exe";
                p.StartInfo.Arguments = " status --fan 0";
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;

                p.Start();
                StreamReader outputWriter = p.StandardOutput;
                var line = outputWriter.ReadLine();
                while (line != null)
                {
                    if (line != "")
                    {
                        mc.config.fan1v += line;
                        ConfigSave();
                    }
                    line = outputWriter.ReadLine();
                }
                p.WaitForExit();
                line = null;
            }
        }

        /*public static void GetInfo()
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
            ConfigLoad();
            if (mc.config.fanex == true)
            {
                mc.config.fan1v = "";
                mc.config.fan2v = "";
                ConfigSave();
                Process p = new Process();
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.FileName = @"nbfc/nbfc.exe";
                p.StartInfo.Arguments = " status --fan 0";
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.Start();
                StreamReader outputWriter = p.StandardOutput;
                var line = outputWriter.ReadLine();
                while (line != null)
                {

                    if (line != "")
                    {
                        mc.config.fan1v += line;
                        ConfigSave();
                    }

                    line = outputWriter.ReadLine();
                }
                line = null;
                //p.WaitForExit();
                //fan 2
                Process p1 = new Process();
                p1.StartInfo.UseShellExecute = false;
                p1.StartInfo.FileName = @"nbfc/nbfc.exe";
                p1.StartInfo.Arguments = " status --fan 1";
                p1.StartInfo.CreateNoWindow = true;
                p1.StartInfo.RedirectStandardError = true;
                p1.StartInfo.RedirectStandardInput = true;
                p1.StartInfo.RedirectStandardOutput = true;

                p1.Start();
                StreamReader outputWriter1 = p1.StandardOutput;
                var line1 = outputWriter1.ReadLine();
                while (line1 != null)
                {

                    if (line1 != "")
                    {
                        mc.config.fan2v += line1;
                        ConfigSave();
                    }

                    line1 = outputWriter1.ReadLine();
                }
                line1 = null;
                //p1.WaitForExit();
            }
        }*/
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
                for (var j = 0; j < 5; j++)
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
        catch { }
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
        catch { }
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
        catch { }
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
#pragma warning restore IDE0044 // Добавить модификатор только для чтения
#pragma warning restore CS8601 // Возможно, назначение-ссылка, допускающее значение NULL.
#pragma warning restore CS8622 // Допустимость значений NULL для ссылочных типов в типе параметра не соответствует целевому объекту делегирования (возможно, из-за атрибутов допустимости значений NULL).