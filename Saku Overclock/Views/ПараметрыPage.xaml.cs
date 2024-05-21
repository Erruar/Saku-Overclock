using System.ComponentModel;
using System.Windows.Forms;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Core.Helpers;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.ViewModels;
using Windows.Foundation.Metadata;
using Button = Microsoft.UI.Xaml.Controls.Button;
using CheckBox = Microsoft.UI.Xaml.Controls.CheckBox;
using ComboBox = Microsoft.UI.Xaml.Controls.ComboBox;
using Process = System.Diagnostics.Process;
using TextBox = Microsoft.UI.Xaml.Controls.TextBox;
using ZenStates.Core;
using System.Collections.ObjectModel;

namespace Saku_Overclock.Views;

public sealed partial class ПараметрыPage : Page
{
    public ПараметрыViewModel ViewModel
    {
        get;
    }
    private FontIcon? SMUSymbol1;
    private List<SmuAddressSet>? matches;
    private Config config = new();
    private Devices devices = new();
    private Smusettings smusettings = new();
    private Profile[] profile = new Profile[1];
    private JsonContainers.Notifications notify = new();
    private int indexprofile = 0;
    private string SMUSymbol = "\uE8C8";
    private bool isLoaded = false;
    private bool relay = false;
    private Cpu? cpu; //Import Zen States core
    private SendSMUCommand? cpusend;
    public bool turbobboost = true;
    private bool waitforload = true; 
    public string? adjline;
    private readonly ZenStates.Core.Mailbox testMailbox = new();
    public string? universalvid;
    public string? equalvid;
    
    public ПараметрыPage()
    {
        ViewModel = App.GetService<ПараметрыViewModel>();
        InitializeComponent(); 
        DeviceLoad();
        ConfigLoad();
        ProfileLoad();
        indexprofile = config.Preset;
        config.fanex = false;
        config.tempex = false; 
        ConfigSave(); 
        try
        {
            cpu ??= CpuSingleton.GetInstance(); 
            cpusend ??= App.GetService<SendSMUCommand>(); 
        }
        catch
        {
            //App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
        }
        Loaded += ПараметрыPage_Loaded;
    }
    #region JSON and initialization
    private async void ПараметрыPage_Loaded(object sender, RoutedEventArgs e)
    {
        isLoaded = true;
        try
        {
            ProfileLoad();
            SlidersInit(); 
        }
        catch
        {
            try
            {
                ConfigLoad(); config.Preset = -1; ConfigSave(); indexprofile = -1;
                SlidersInit();
            }
            catch
            {
                await Send_Message("Critical Error!", "Can't load profiles. Tell this to developer", Symbol.Bookmarks);
            }
        }
    }
    public void ConfigSave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json", JsonConvert.SerializeObject(config, Formatting.Indented));
        }
        catch
        {
            // ignored
        }
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
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\devices.json", JsonConvert.SerializeObject(devices));
        }
        catch
        {
            // ignored
        }
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
    public void NotifySave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json", JsonConvert.SerializeObject(notify, Formatting.Indented));
        }
        catch
        {
            // ignored
        }
    }
    public async void NotifyLoad()
    {
        var success = false;
        var retryCount = 1;
        while (!success && retryCount < 3)
        {
            if (File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json"))
            {
                try
                {
                    notify = JsonConvert.DeserializeObject<JsonContainers.Notifications>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json"))!;
                    if (notify != null) { success = true; } else { JsonRepair('p'); }
                }
                catch { JsonRepair('n'); }
            }
            else { JsonRepair('n'); }
            if (!success)
            { 
                await Task.Delay(30);
                retryCount++;
            }
        }
    }
    public void SmuSettingsSave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\smusettings.json", JsonConvert.SerializeObject(smusettings, Formatting.Indented));
        }
        catch
        {
            // ignored
        }
    }
    public void SmuSettingsLoad()
    {
        try
        {
            smusettings = JsonConvert.DeserializeObject<Smusettings>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\smusettings.json"))!;
        }
        catch
        {
            JsonRepair('s');
        }
    }
    public void ProfileSave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json", JsonConvert.SerializeObject(profile, Formatting.Indented));
        }
        catch
        {
            // ignored
        }
    }
    public void ProfileLoad()
    {
        try
        {

            profile = JsonConvert.DeserializeObject<Profile[]>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json"))!;
        }
        catch
        {
            JsonRepair('p');
        }
    }
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
                App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                App.MainWindow.Close();
            }
            if (config != null)
            {
                try
                {
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json", JsonConvert.SerializeObject(config));
                }
                catch
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json");
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json", JsonConvert.SerializeObject(config));
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }
            }
            else
            {
                try
                {

                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json");
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json", JsonConvert.SerializeObject(config));
                    App.MainWindow.Close();
                }
                catch
                {
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                    App.MainWindow.Close();
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
                App.MainWindow.Close();
            }
            if (devices != null)
            {
                try
                {
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\devices.json", JsonConvert.SerializeObject(devices));
                }
                catch
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\devices.json");
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\devices.json", JsonConvert.SerializeObject(devices));
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }
            }
            else
            {
                try
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\devices.json");
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\devices.json", JsonConvert.SerializeObject(devices));
                    App.MainWindow.Close();
                }
                catch
                {
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }
            }
        }
        if (file == 's')
        {
            try
            {
                for (var j = 0; j < 5; j++)
                {
                    smusettings = new Smusettings();
                }
            }
            catch
            {
                App.MainWindow.Close();
            }
            if (smusettings != null)
            {
                try
                {
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\smusettings.json", JsonConvert.SerializeObject(smusettings, Formatting.Indented));
                }
                catch
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\smusettings.json");
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\smusettings.json", JsonConvert.SerializeObject(smusettings, Formatting.Indented));
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }
            }
            else
            {
                try
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\smusettings.json");
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\smusettings.json", JsonConvert.SerializeObject(smusettings, Formatting.Indented));
                    App.MainWindow.Close();
                }
                catch
                {
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }
            }
        }
        if (file == 'p')
        {
            try
            {
                for (var j = 0; j < 3; j++)
                {
                    profile[j] = new Profile();
                }
            }
            catch
            {
                App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                App.MainWindow.Close();
            }
            if (profile != null)
            {
                try
                {
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json", JsonConvert.SerializeObject(profile));
                }
                catch
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json");
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json", JsonConvert.SerializeObject(profile));
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }
            }
            else
            {
                try
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json");
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json", JsonConvert.SerializeObject(profile));
                    App.MainWindow.Close();
                }
                catch
                {
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }
            }
        }
        if (file == 'n')
        {
            try
            {
                notify = new JsonContainers.Notifications();
            }
            catch
            {
                App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                App.MainWindow.Close();
            }
            if (notify != null)
            {
                try
                {
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json", JsonConvert.SerializeObject(notify));
                }
                catch
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json");
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json", JsonConvert.SerializeObject(notify));
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }
            }
            else
            {
                try
                {

                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json");
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json", JsonConvert.SerializeObject(notify));
                    App.MainWindow.Close();
                }
                catch
                {
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }
            }
        }
    }
    public void SlidersInit()
    {
        //PLS don't beat me for this WEIRDEST initialization.
        //I know about that. If you can do better - do!
        //Open pull requests and create own with your code.
        //App still in BETA state. Make sense that I choosed "do it faster but poorly" instead of "do it slowly but better" at project start and now I'm fixing that situation
        if (isLoaded == false)
        {
            return;
        }
        waitforload = true;
        ProfileLoad();
        ConfigLoad();
        ProfileCOM.Items.Clear();
        ProfileCOM.Items.Add("Unsaved");
        for (var i = 0; i < profile.Length; i++)
        {
            if (profile[i].profilename != string.Empty)
            {
                ProfileCOM.Items.Add(profile[i].profilename);
            }
        }
        if (config.Preset > profile.Length) { config.Preset = 0; ConfigSave(); }
        else
        {
            if (config.Preset == -1)
            {
                indexprofile = 0;
                ProfileCOM.SelectedIndex = 0;
            }
            else
            {
                indexprofile = config.Preset;
                ProfileCOM.SelectedIndex = indexprofile + 1;
            }
        }
        //Main INIT. It will be better soon! - Serzhik Saku, Erruar
        MainInit(indexprofile);
        waitforload = false;
    }
    private void MainInit(int index)
    {
        if (cpu?.info.codeName.ToString().Contains("VanGogh") == false)
        {
            A1_main.Visibility = Visibility.Collapsed;
            A2_main.Visibility = Visibility.Collapsed;
            A3_main.Visibility = Visibility.Collapsed;
            A4_main.Visibility = Visibility.Collapsed;
            A5_main.Visibility = Visibility.Collapsed;
            A1_desc.Visibility = Visibility.Collapsed;
            A2_desc.Visibility = Visibility.Collapsed;
            A3_desc.Visibility = Visibility.Collapsed;
            A4_desc.Visibility = Visibility.Collapsed;
            A5_desc.Visibility = Visibility.Collapsed;
        }
        if (cpu?.info.codeName.ToString().Contains("Raven") == false && cpu?.info.codeName.ToString().Contains("Dali") == false && cpu?.info.codeName.ToString().Contains("Picasso") == false)
        {
            iGPU_Subsystems.Visibility = Visibility.Collapsed; Dunger_Zone.Visibility = Visibility.Collapsed; V8_Main.Visibility = Visibility.Collapsed; V8_Desc.Visibility = Visibility.Collapsed; V9_Main.Visibility = Visibility.Collapsed; V9_Desc.Visibility = Visibility.Collapsed; V10_Main.Visibility = Visibility.Collapsed; V10_Desc.Visibility = Visibility.Collapsed;
        }
        waitforload = true;
        ConfigLoad();
        if (config.Preset == -1 || index == -1) //Load from unsaved
        {
            DeviceLoad();
            c1.IsChecked = devices.c1; c1v.Value = devices.c1v; c2.IsChecked = devices.c2; c1v.Value = devices.c2v; c3.IsChecked = devices.c3; c1v.Value = devices.c3v; c4.IsChecked = devices.c4; c1v.Value = devices.c4v; c5.IsChecked = devices.c5; c1v.Value = devices.c5v; c6.IsChecked = devices.c6; c1v.Value = devices.c6v; c7.IsChecked = devices.c7; c7v.Value = devices.c7v;
            V1.IsChecked = devices.v1; V1V.Value = devices.v1v; V2.IsChecked = devices.v2; V2V.Value = devices.v2v; V3.IsChecked = devices.v3; V3V.Value = devices.v3v; V4.IsChecked = devices.v4; V4V.Value = devices.v4v; V5.IsChecked = devices.v5; V5V.Value = devices.v5v; V6.IsChecked = devices.v6; V6V.Value = devices.v6v; V7.IsChecked = devices.v7; V7V.Value = devices.v7v; V8.IsChecked = devices.v8; V8V.Value = devices.v8v; V9.IsChecked = devices.v9; V9V.Value = devices.v9v;
            g1.IsChecked = devices.g1; g1v.Value = devices.g1v; g2.IsChecked = devices.g2; g2v.Value = devices.g2v; g3.IsChecked = devices.g3; g3v.Value = devices.g3v; g4.IsChecked = devices.g4; g4v.Value = devices.g4v; g5.IsChecked = devices.g5; g5v.Value = devices.g5v; g6.IsChecked = devices.g6; g6v.Value = devices.g6v; g7.IsChecked = devices.g7; g7v.Value = devices.g7v; g8v.Value = devices.g8v; g8.IsChecked = devices.g8; g9v.Value = devices.g9v; g9.IsChecked = devices.g9; g10v.Value = devices.g10v; g10.IsChecked = devices.g10; g11v.Value = devices.g11v; g11.IsChecked = devices.g11; g12v.Value = devices.g12v; g12.IsChecked = devices.g12; g13v.Value = devices.g13v; g13.IsChecked = devices.g13; g14v.Value = devices.g14v; g14.IsChecked = devices.g14; g15m.SelectedIndex = devices.g15v; g15.IsChecked = devices.g15; g16m.SelectedIndex = devices.g16v; g16.IsChecked = devices.g16;
            a1.IsChecked = devices.a1; a1v.Value = devices.a1v; a2.IsChecked = devices.a2; a2v.Value = devices.a2v; a3.IsChecked = devices.a3; a3v.Value = devices.a3v; a4.IsChecked = devices.a4; a4v.Value = devices.a4v; a5.IsChecked = devices.a5; a5v.Value = devices.a5v; a6.IsChecked = devices.a6; a6v.Value = devices.a6v; a7.IsChecked = devices.a7; a7v.Value = devices.a7v; a8v.Value = devices.a8v; a8.IsChecked = devices.a8; a9v.Value = devices.a9v; a9.IsChecked = devices.a9; a10v.Value = devices.a10v; a11v.Value = devices.a11v; a11.IsChecked = devices.a11; a12v.Value = devices.a12v; a12.IsChecked = devices.a12; a13m.SelectedIndex = devices.a13v; a13.IsChecked = devices.a13; a14m.SelectedIndex = devices.a14v; a14.IsChecked = devices.a14; a15v.Value = devices.a15v; a15.IsChecked = devices.a15;
            EnablePstates.IsOn = devices.enableps; Turbo_boost.IsOn = devices.turboboost; Autoapply_1.IsOn = devices.autopstate; IgnoreWarn.IsOn = devices.ignorewarn; Without_P0.IsOn = devices.p0ignorewarn;
            DID_0.Value = devices.did0; DID_1.Value = devices.did1; DID_2.Value = devices.did2; FID_0.Value = devices.fid0; FID_1.Value = devices.fid1; FID_2.Value = devices.fid2; VID_0.Value = devices.vid0; VID_1.Value = devices.vid1; VID_2.Value = devices.vid2;
            EnableSMU.IsOn = devices.smuenabled;
        }
        else
        {
            ProfileLoad();
            c1.IsChecked = profile[index].cpu1; c1v.Value = profile[index].cpu1value; c2.IsChecked = profile[index].cpu2; c2v.Value = profile[index].cpu2value; c3.IsChecked = profile[index].cpu3; c3v.Value = profile[index].cpu3value; c4.IsChecked = profile[index].cpu4; c4v.Value = profile[index].cpu4value; c5.IsChecked = profile[index].cpu5; c5v.Value = profile[index].cpu5value; c6.IsChecked = profile[index].cpu6; c6v.Value = profile[index].cpu6value; c7.IsChecked = profile[index].cpu7; c7v.Value = profile[index].cpu7value;
            V1.IsChecked = profile[index].vrm1; V1V.Value = profile[index].vrm1value; V2.IsChecked = profile[index].vrm2; V2V.Value = profile[index].vrm2value; V3.IsChecked = profile[index].vrm3; V3V.Value = profile[index].vrm3value; V4.IsChecked = profile[index].vrm4; V4V.Value = profile[index].vrm4value; V5.IsChecked = profile[index].vrm5; V5V.Value = profile[index].vrm5value; V6.IsChecked = profile[index].vrm6; V6V.Value = profile[index].vrm6value; V7.IsChecked = profile[index].vrm7; V7V.Value = profile[index].vrm7value; V8.IsChecked = profile[index].vrm8; V8V.Value = profile[index].vrm8value; V9.IsChecked = profile[index].vrm9; V9V.Value = profile[index].vrm9value; V10.IsChecked = profile[index].vrm10; V10V.Value = profile[index].vrm10value;
            g1.IsChecked = profile[index].gpu1; g1v.Value = profile[index].gpu1value; g2.IsChecked = profile[index].gpu2; g2v.Value = profile[index].gpu2value; g3.IsChecked = profile[index].gpu3; g3v.Value = profile[index].gpu3value; g4.IsChecked = profile[index].gpu4; g4v.Value = profile[index].gpu4value; g5.IsChecked = profile[index].gpu5; g5v.Value = profile[index].gpu5value; g6.IsChecked = profile[index].gpu6; g6v.Value = profile[index].gpu6value; g7.IsChecked = profile[index].gpu7; g7v.Value = profile[index].gpu7value; g8v.Value = profile[index].gpu8value; g8.IsChecked = profile[index].gpu8; g9v.Value = profile[index].gpu9value; g9.IsChecked = profile[index].gpu9; g10v.Value = profile[index].gpu10value; g10.IsChecked = profile[index].gpu10; g11.IsChecked = profile[index].gpu11; g11v.Value = profile[index].gpu11value; g12.IsChecked = profile[index].gpu12; g12v.Value = profile[index].gpu12value; g13.IsChecked = profile[index].gpu13; g13v.Value = profile[index].gpu13value; g14.IsChecked = profile[index].gpu14; g14v.Value = profile[index].gpu14value; g15.IsChecked = profile[index].gpu15; g15m.SelectedIndex = profile[index].gpu15value; g16.IsChecked = profile[index].gpu16; g16m.SelectedIndex = profile[index].gpu16value;
            a1.IsChecked = profile[index].advncd1; a1v.Value = profile[index].advncd1value; a2.IsChecked = profile[index].advncd2; a2v.Value = profile[index].advncd2value; a3.IsChecked = profile[index].advncd3; a3v.Value = profile[index].advncd3value; a4.IsChecked = profile[index].advncd4; a4v.Value = profile[index].advncd4value; a5.IsChecked = profile[index].advncd5; a5v.Value = profile[index].advncd5value; a6.IsChecked = profile[index].advncd6; a6v.Value = profile[index].advncd6value; a7.IsChecked = profile[index].advncd7; a7v.Value = profile[index].advncd7value; a8v.Value = profile[index].advncd8value; a8.IsChecked = profile[index].advncd8; a9v.Value = profile[index].advncd9value; a9.IsChecked = profile[index].advncd9; a10v.Value = profile[index].advncd10value; a11v.Value = profile[index].advncd11value; a11.IsChecked = profile[index].advncd11; a12v.Value = profile[index].advncd12value; a12.IsChecked = profile[index].advncd12; a13.IsChecked = profile[index].advncd13; a13m.SelectedIndex = profile[index].advncd13value; a14.IsChecked = profile[index].advncd14; a14m.SelectedIndex = profile[index].advncd14value; a15.IsChecked = profile[index].advncd15; a15v.Value = profile[index].advncd15value;
            EnablePstates.IsOn = profile[index].enablePstateEditor; Turbo_boost.IsOn = profile[index].turboBoost; Autoapply_1.IsOn = profile[index].autoPstate; IgnoreWarn.IsOn = profile[index].ignoreWarn; Without_P0.IsOn = profile[index].p0Ignorewarn;
            DID_0.Value = profile[index].did0; DID_1.Value = profile[index].did1; DID_2.Value = profile[index].did2; FID_0.Value = profile[index].fid0; FID_1.Value = profile[index].fid1; FID_2.Value = profile[index].fid2; VID_0.Value = profile[index].vid0; VID_1.Value = profile[index].vid1; VID_2.Value = profile[index].vid2;
            EnableSMU.IsOn = profile[index].smuEnabled;
        }
        try
        {
            Mult_0.SelectedIndex = (int)(FID_0.Value * 25 / (DID_0.Value * 12.5)) - 4;
            P0_Freq.Content = FID_0.Value * 25 / (DID_0.Value * 12.5) * 100;
            Mult_1.SelectedIndex = (int)(FID_1.Value * 25 / (DID_1.Value * 12.5)) - 4;
            P1_Freq.Content = FID_1.Value * 25 / (DID_1.Value * 12.5) * 100;
            P2_Freq.Content = FID_2.Value * 25 / (DID_2.Value * 12.5) * 100;
            Mult_2.SelectedIndex = (int)(FID_2.Value * 25 / (DID_2.Value * 12.5)) - 4;
        }
        catch
        {
            //Ignored
        }
        waitforload = false;
        SmuSettingsLoad(); 
        if (smusettings.Note != string.Empty)
        {
            SMUNotes.Document.SetText(TextSetOptions.FormatRtf, smusettings.Note);
        }
        try
        {
            Init_QuickSMU();
        }
        catch
        {
            //Ignored
        }
    }
    private void Init_QuickSMU()
    {
        SmuSettingsLoad();
        if (smusettings.QuickSMUCommands == null)
        {
            return;
        }

        QuickSMU.Children.Clear();
        QuickSMU.RowDefinitions.Clear();
        for (var i = 0; i < smusettings?.QuickSMUCommands.Count; i++)
        {
            var grid = new Grid //Основной грид, куда всё добавляется
            {
                //grid.SetValue(Grid.RowProperty, 8);
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch
            };
            // Создание новой RowDefinition
            var rowDef = new RowDefinition
            {
                Height = GridLength.Auto // Указать необходимую высоту
            };
            // Добавление новой RowDefinition в SMU_MainSection
            QuickSMU.RowDefinitions.Add(rowDef);
            // Определение строки для размещения Grid
            var rowIndex = QuickSMU.RowDefinitions.Count - 1;
            // Размещение созданного Grid в SMU_MainSection
            QuickSMU.Children.Add(grid); //Добавить в программу грид быстрой команды
            Grid.SetRow(grid, rowIndex); //Задать дорожку для нового грида
            // Создание Button
            var button = new Button //Добавить основную кнопку быстрой команды. Именно в ней всё содержимое
            {
                Height = 50,
                HorizontalContentAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch
            };
            // Создание Grid внутри Button
            var innerGrid = new Grid
            {
                Height = 50
            };
            // Создание FontIcon она же иконка у этой команды
            var fontIcon = new FontIcon
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, -10, 0, 0),
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left,
                Glyph = smusettings.QuickSMUCommands[i].Symbol
            };
            // Добавление FontIcon в Grid
            innerGrid.Children.Add(fontIcon);
            // Создание TextBlock
            var textBlock1 = new TextBlock
            {
                Margin = new Thickness(35, 0.5, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
                Text = smusettings.QuickSMUCommands[i].Name,
                FontWeight = FontWeights.SemiBold
            };
            innerGrid.Children.Add(textBlock1);
            // Создание второго TextBlock
            var textBlock2 = new TextBlock
            {
                Margin = new Thickness(35, 17.5, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
                Text = smusettings.QuickSMUCommands[i].Description,
                FontWeight = FontWeights.Light
            };
            innerGrid.Children.Add(textBlock2);
            // Добавление внутреннего Grid в Button
            button.Content = innerGrid;
            // Создание внешнего Grid с кнопками
            var buttonsGrid = new Grid
            {
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Right
            };
            // Создание и добавление кнопок во внешний Grid
            var playButton = new Button //Кнопка применить
            {
                Name = $"Play_{rowIndex}",
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Right,
                Width = 35,
                Height = 35,
                Margin = new Thickness(0, 0, 7, 0),
                Content = new SymbolIcon()
                {
                    Symbol = Symbol.Play,
                    Margin = new Thickness(-5, 0, -5, 0),
                    HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left
                }
            };
            buttonsGrid.Children.Add(playButton);
            var editButton = new Button //Кнопка изменить
            {
                Name = $"Edit_{rowIndex}",
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Right,
                Width = 35,
                Height = 35,
                Margin = new Thickness(0, 0, 50, 0),
                Content = new SymbolIcon()
                {
                    Symbol = Symbol.Edit,
                    Margin = new Thickness(-5, 0, -5, 0)
                }
            };
            buttonsGrid.Children.Add(editButton);
            var rsmuButton = new Button
            {
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Right,
                Width = 86,
                Height = 35,
                Margin = new Thickness(0, 0, 93, 0)
            };
            var rsmuTextBlock = new TextBlock
            {
                Text = smusettings?.MailBoxes![smusettings.QuickSMUCommands[i].MailIndex].Name
            };
            rsmuButton.Content = rsmuTextBlock;
            buttonsGrid.Children.Add(rsmuButton);
            var cmdButton = new Button
            {
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Right,
                Width = 86,
                Height = 35,
                Margin = new Thickness(0, 0, 187, 0)
            };
            var cmdTextBlock = new TextBlock
            {
                Text = smusettings?.QuickSMUCommands![i].Command + " / " + smusettings?.QuickSMUCommands![i].Argument
            };
            cmdButton.Content = cmdTextBlock;
            buttonsGrid.Children.Add(cmdButton);
            var autoButton = new Button
            {
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Right,
                Width = 86,
                Height = 35,
                Margin = new Thickness(0, 0, 281, 0)
            };
            var autoTextBlock = new TextBlock
            {
                Text = "Apply"
            };
            if (smusettings?.QuickSMUCommands![i].Startup == true)
            {
                autoTextBlock.Text = "Autorun";
            }
            if (smusettings?.QuickSMUCommands![i].Startup == true || smusettings?.QuickSMUCommands![i].ApplyWith == true)
            {
                buttonsGrid.Children.Add(autoButton);
            }
            //
            autoButton.Content = autoTextBlock;
            // Добавление внешнего Grid в основной Grid
            grid.Children.Add(button);
            grid.Children.Add(buttonsGrid);
            editButton.Click += EditButton_Click;
            playButton.Click += PlayButton_Click;
        }
    } 
    #endregion
    #region SMU Related voids and Quick SMU Commands
    private static void RunBackgroundTask(DoWorkEventHandler task, RunWorkerCompletedEventHandler completedHandler)
    {
        try
        {
            var backgroundWorker1 = new BackgroundWorker();
            backgroundWorker1.DoWork += task;
            backgroundWorker1.RunWorkerCompleted += completedHandler;
            backgroundWorker1.RunWorkerAsync();
        }
        catch
        {
            //Ignored
        }
    }
    private void PopulateMailboxesList(ItemCollection l)
    {
        l.Clear();
        l.Add(new MailboxListItem("RSMU", cpu?.smu.Rsmu!));
        l.Add(new MailboxListItem("MP1", cpu?.smu.Mp1Smu!));
        l.Add(new MailboxListItem("HSMP", cpu?.smu.Hsmp!));
    }
    private void AddMailboxToList(string label, SmuAddressSet addressSet)
    {
        comboBoxMailboxSelect.Items.Add(new MailboxListItem(label, addressSet));
    }
    private async void SmuScan_WorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
        var index = comboBoxMailboxSelect.SelectedIndex;
        PopulateMailboxesList(comboBoxMailboxSelect.Items);
        //DONT TOUCH
        for (var i = 0; i < matches?.Count; i++)
        {
            AddMailboxToList($"Mailbox {i + 1}", matches[i]);
        }

        if (index > comboBoxMailboxSelect.Items.Count)
        {
            index = 0;
        }
        comboBoxMailboxSelect.SelectedIndex = index;
        QuickCommand.IsEnabled = true;
        QuickCommand2.IsEnabled = true;
        await Send_Message("SMUScanText".GetLocalized(), "SMUScanDesc".GetLocalized(), Symbol.Message);
    }
    private void BackgroundWorkerTrySettings_DoWork(object sender, DoWorkEventArgs e)
    {
      try
        {
            cpu ??= new Cpu(CpuInitSettings.defaultSetttings);
            switch (cpu.info.codeName)
            {
                case ZenStates.Core.Cpu.CodeName.BristolRidge:
                    //ScanSmuRange(0x13000000, 0x13000F00, 4, 0x10);
                    break;
                case ZenStates.Core.Cpu.CodeName.RavenRidge:
                case ZenStates.Core.Cpu.CodeName.Picasso:
                case ZenStates.Core.Cpu.CodeName.FireFlight:
                case ZenStates.Core.Cpu.CodeName.Dali:
                case ZenStates.Core.Cpu.CodeName.Renoir:
                    ScanSmuRange(0x03B10500, 0x03B10998, 8, 0x3C); 
                    ScanSmuRange(0x03B10A00, 0x03B10AFF, 4, 0x60);
                    break;
                case ZenStates.Core.Cpu.CodeName.PinnacleRidge:
                case ZenStates.Core.Cpu.CodeName.SummitRidge:
                case ZenStates.Core.Cpu.CodeName.Matisse:
                case ZenStates.Core.Cpu.CodeName.Whitehaven:
                case ZenStates.Core.Cpu.CodeName.Naples:
                case ZenStates.Core.Cpu.CodeName.Colfax:
                case ZenStates.Core.Cpu.CodeName.Vermeer:
                    //case Cpu.CodeName.Raphael:
                    ScanSmuRange(0x03B10500, 0x03B10998, 8, 0x3C);
                    ScanSmuRange(0x03B10500, 0x03B10AFF, 4, 0x4C);
                    break;
                case ZenStates.Core.Cpu.CodeName.Raphael:
                    ScanSmuRange(0x03B10500, 0x03B10998, 8, 0x3C);
                    // ScanSmuRange(0x03B10500, 0x03B10AFF, 4, 0x4C);
                    break;
                case ZenStates.Core.Cpu.CodeName.Rome:
                    ScanSmuRange(0x03B10500, 0x03B10AFF, 4, 0x4C);
                    break;
                default:
                    break;
            }
        }
        catch (ApplicationException)
        {
        }
    }
    private void ScanSmuRange(uint start, uint end, uint step, uint offset)
    {
        matches = new List<SmuAddressSet>();

        var temp = new List<KeyValuePair<uint, uint>>();

        while (start <= end)
        {
            var smuRspAddress = start + offset;

            if (cpu?.ReadDword(start) != 0xFFFFFFFF)
            {
                // Send unknown command 0xFF to each pair of this start and possible response addresses
                if (cpu?.WriteDwordEx(start, 0x120) == true) //CHANGED FROM 0xFF!!!!!!!!!!!!!!
                {
                    Thread.Sleep(10);

                    while (smuRspAddress <= end)
                    {
                        // Expect UNKNOWN_CMD status to be returned if the mailbox works
                        if (cpu?.ReadDword(smuRspAddress) == 0xFE)
                        {
                            // Send Get_SMU_Version command
                            if (cpu?.WriteDwordEx(start, 0x2) == true) //CHANGED FROM 0x2!!!!!!!!!!!!!!
                            {
                                Thread.Sleep(10);
                                if (cpu?.ReadDword(smuRspAddress) == 0x1)
                                {
                                    temp.Add(new KeyValuePair<uint, uint>(start, smuRspAddress));
                                }
                            }
                        }
                        smuRspAddress += step;
                    }
                }
            }
            start += step;
        }
        if (temp.Count > 0)
        {
            foreach (var t in temp)
            {
                Console.WriteLine($"{t.Key:X8}: {t.Value:X8}");
            }
        }
        var possibleArgAddresses = new List<uint>();
        foreach (var pair in temp)
        {
            Console.WriteLine($"Testing {pair.Key:X8}: {pair.Value:X8}");

            if (TrySettings(pair.Key, pair.Value, 0xFFFFFFFF, 0x2, 0xFF) == ZenStates.Core.SMU.Status.OK)
            {
                var smuArgAddress = pair.Value + 4;
                while (smuArgAddress <= end)
                {
                    if (cpu?.ReadDword(smuArgAddress) == cpu?.smu.Version) 
                    {
                        possibleArgAddresses.Add(smuArgAddress);
                    }
                    smuArgAddress += step;
                }
            }
            // Verify the arg address returns correct value (should be test argument + 1)
            foreach (var address in possibleArgAddresses)
            {
                var testArg = 0xFAFAFAFA;
                var retries = 3;
                while (retries > 0)
                {
                    testArg++;
                    retries--;

                    // Send test command
                    if (TrySettings(pair.Key, pair.Value, address, 0x1, testArg) == ZenStates.Core.SMU.Status.OK)
                    {
                        if (cpu?.ReadDword(address) != testArg + 1)
                        {
                            retries = -1;
                        }
                    }
                }
                if (retries == 0)
                {
                    matches.Add(new SmuAddressSet(pair.Key, pair.Value, address));
                    break;
                }
            }
        }
    } 
    private SMU.Status? TrySettings(uint msgAddr, uint rspAddr, uint argAddr, uint cmd, uint value)
    {
        var args = new uint[6];
        args[0] = value;

        testMailbox.SMU_ADDR_MSG = msgAddr;
        testMailbox.SMU_ADDR_RSP = rspAddr;
        testMailbox.SMU_ADDR_ARG = argAddr;

        return cpu?.smu.SendSmuCommand(testMailbox, cmd, ref args);
    }
    private void ResetSmuAddresses()
    {
        textBoxCMDAddress.Text = $@"0x{Convert.ToString(testMailbox.SMU_ADDR_MSG, 16).ToUpper()}";
        textBoxRSPAddress.Text = $@"0x{Convert.ToString(testMailbox.SMU_ADDR_RSP, 16).ToUpper()}";
        textBoxARGAddress.Text = $@"0x{Convert.ToString(testMailbox.SMU_ADDR_ARG, 16).ToUpper()}";
    }
    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        SmuSettingsLoad();
        ApplySettings(1, int.Parse(button!.Name.Replace("Play_", "")));
    }
    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        SmuSettingsLoad();
        QuickDialog(1, int.Parse(button!.Name.Replace("Edit_", "")));
    }
    //SMU КОМАНДЫ
    private async void ApplySettings(int mode, int CommandIndex)
    {
        try
        {
            uint[]? args;
            string[]? userArgs;
            uint addrMsg;
            uint addrRsp;
            uint addrArg;
            uint command;
            if (mode != 0)
            {
                SmuSettingsLoad();
                args = ZenStates.Core.Utils.MakeCmdArgs();
                userArgs = smusettings?.QuickSMUCommands![CommandIndex].Argument.Trim().Split(',');
                TryConvertToUint(smusettings?.MailBoxes![smusettings!.QuickSMUCommands![CommandIndex].MailIndex].CMD!, out addrMsg);
                TryConvertToUint(smusettings?.MailBoxes![smusettings!.QuickSMUCommands![CommandIndex].MailIndex].RSP!, out addrRsp);
                TryConvertToUint(smusettings?.MailBoxes![smusettings!.QuickSMUCommands![CommandIndex].MailIndex].ARG!, out addrArg);
                TryConvertToUint(smusettings?.QuickSMUCommands![CommandIndex].Command!, out command);
            }
            else
            {
                args = ZenStates.Core.Utils.MakeCmdArgs();
                userArgs = textBoxARG0.Text.Trim().Split(',');
                TryConvertToUint(textBoxCMDAddress.Text, out addrMsg);
                TryConvertToUint(textBoxRSPAddress.Text, out addrRsp);
                TryConvertToUint(textBoxARGAddress.Text, out addrArg);
                TryConvertToUint(textBoxCMD.Text, out command);

            }
            testMailbox.SMU_ADDR_MSG = addrMsg;
            testMailbox.SMU_ADDR_RSP = addrRsp;
            testMailbox.SMU_ADDR_ARG = addrArg;
            for (var i = 0; i < userArgs?.Length; i++)
            {
                if (i == args.Length)
                {
                    break;
                }
                TryConvertToUint(userArgs[i], out var temp);
                args[i] = temp;
            }
            var status = cpu?.smu.SendSmuCommand(testMailbox, command, ref args);
            if (status == SMU.Status.OK)
            {
                await Send_Message("SMUOKText".GetLocalized(), "SMUOKDesc".GetLocalized(), Symbol.Accept);
            }
            else
            {
                if (status == SMU.Status.CMD_REJECTED_PREREQ)
                {
                    await Send_Message("SMUErrorText".GetLocalized(), "SMUErrorRejected".GetLocalized(), Symbol.Dislike);
                }
                else
                {
                    await Send_Message("SMUErrorText".GetLocalized(), "SMUErrorNoCMD".GetLocalized(), Symbol.Filter);
                }
            }
        }
        catch
        {
            await Send_Message("SMUErrorText".GetLocalized(), "SMUErrorDesc".GetLocalized(), Symbol.Dislike);
        }
    }
    private static void TryConvertToUint(string text, out uint address)
    {
        try
        {
            address = Convert.ToUInt32(text.Trim().ToLower(), 16);
        }
        catch
        {
            throw new ApplicationException("Invalid hexadecimal value.");
        }
    }
    private void DevEnv_Expanding(Expander sender, ExpanderExpandingEventArgs args)
    {
        RunBackgroundTask(BackgroundWorkerTrySettings_DoWork!, SmuScan_WorkerCompleted!);
    }
    private void ComboBoxMailboxSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (comboBoxMailboxSelect.SelectedItem is MailboxListItem item) { InitTestMailbox(item.msgAddr, item.rspAddr, item.argAddr); }
    }
    private void InitTestMailbox(uint msgAddr, uint rspAddr, uint argAddr)
    {
        testMailbox.SMU_ADDR_MSG = msgAddr;
        testMailbox.SMU_ADDR_RSP = rspAddr;
        testMailbox.SMU_ADDR_ARG = argAddr;
        ResetSmuAddresses();
    }
    private async void Mon_Click(object sender, RoutedEventArgs e)
    {
        var MonDialog = new ContentDialog
        {
            Title = "PowerMonText".GetLocalized(),
            Content = "PowerMonDesc".GetLocalized(),
            CloseButtonText = "Cancel".GetLocalized(),
            PrimaryButtonText = "Open".GetLocalized(),
            DefaultButton = ContentDialogButton.Close
        };

        // Use this code to associate the dialog to the appropriate AppWindow by setting
        // the dialog's XamlRoot to the same XamlRoot as an element that is already present in the AppWindow.
        if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
        {
            MonDialog.XamlRoot = XamlRoot;
        }

        var result = await MonDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var newWindow = new PowerWindow(cpu);
            var micaBackdrop = new MicaBackdrop
            {
                Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt
            };
            newWindow.SystemBackdrop = micaBackdrop;
            newWindow.Activate();
        }
    }
    private void SMUEnabl_Click(object sender, RoutedEventArgs e)
    {
        if (EnableSMU.IsOn) { EnableSMU.IsOn = false; } else { EnableSMU.IsOn = true; }
        SMUEnabl();
    }
    private void EnableSMU_Toggled(object sender, RoutedEventArgs e) => SMUEnabl();
    private void SMUEnabl()
    {
        if (EnableSMU.IsOn) { devices.smuenabled = true; DeviceSave(); profile[indexprofile].smuEnabled = true; ProfileSave(); }
        else { devices.smuenabled = false; DeviceSave(); profile[indexprofile].smuEnabled = false; ProfileSave(); }
    }
    private void CreateQuickCommandSMU_Click(object sender, RoutedEventArgs e)
    {
        QuickDialog(0, 0);
    }
    private void CreateQuickCommandSMU1_Click(object sender, RoutedEventArgs e)
    {
        RangeDialog();
    }
    private async void QuickDialog(int destination, int rowindex)
    {
        SMUSymbol1 = new FontIcon
        {
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            Glyph = SMUSymbol,
            Margin = new Thickness(-4, -2, -5, -5),
        };
        var symbolButton = new Button
        {
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(320, 60, 0, 0),
            Width = 40,
            Height = 40,
            Content = new ContentControl
            {
                Content = SMUSymbol1
            }
        };
        var comboSelSMU = new ComboBox
        {
            Margin = new Thickness(0, 20, 0, 0),
            VerticalAlignment = VerticalAlignment.Top
        };
        var mainText = new TextBox
        {
            Margin = new Thickness(0, 60, 0, 0),
            PlaceholderText = "New_Name".GetLocalized(),
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Height = 39.5,
            Width = 315
        };
        var descText = new TextBox
        {
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 105.5, 0, 0),
            PlaceholderText = "Desc".GetLocalized(),
            Height = 40,
            Width = 360
        };
        var cmdText = new TextBox
        {
            Margin = new Thickness(0, 152, 0, 0),
            PlaceholderText = "Command".GetLocalized(),
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Height = 40,
            Width = 176
        };
        var argText = new TextBox
        {
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(180, 152, 0, 0),
            PlaceholderText = "Arguments".GetLocalized(),
            Height = 40,
            Width = 179
        };
        var autoRun = new CheckBox
        {
            Margin = new Thickness(1, 195, 0, 0),
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Content = "Param_Autorun".GetLocalized(),
            IsChecked = false
        };
        var applyWith = new CheckBox
        {
            Margin = new Thickness(1, 225, 0, 0),
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Content = "Param_WithApply".GetLocalized(),
            IsChecked = false
        };
        try
        {
            foreach (var item in comboBoxMailboxSelect.Items)
            {
                comboSelSMU.Items.Add(item);
            }
            comboSelSMU.SelectedIndex = comboBoxMailboxSelect.SelectedIndex;
            comboSelSMU.SelectionChanged += ComboSelSMU_SelectionChanged;
            symbolButton.Click += SymbolButton_Click;
            if (destination != 0)
            {
                SmuSettingsLoad();
                SMUSymbol = smusettings?.QuickSMUCommands![rowindex].Symbol!;
                SMUSymbol1.Glyph = smusettings?.QuickSMUCommands![rowindex].Symbol;
                comboSelSMU.SelectedIndex = smusettings!.QuickSMUCommands![rowindex].MailIndex;
                mainText.Text = smusettings?.QuickSMUCommands![rowindex].Name;
                descText.Text = smusettings?.QuickSMUCommands![rowindex].Description;
                cmdText.Text = smusettings?.QuickSMUCommands![rowindex].Command;
                argText.Text = smusettings?.QuickSMUCommands![rowindex].Argument;
                autoRun.IsChecked = smusettings?.QuickSMUCommands![rowindex].Startup;
                applyWith.IsChecked = smusettings?.QuickSMUCommands![rowindex].ApplyWith;
            }
        }
        catch { }
        try
        {
            var newQuickCommand = new ContentDialog
            {
                Title = "AdvancedCooler_Del_Action".GetLocalized(),
                Content = new Grid
                {
                    Children =
                    {
                        comboSelSMU,
                        symbolButton,
                        mainText,
                        descText,
                        cmdText,
                        argText,
                        autoRun,
                        applyWith
                    }
                },
                PrimaryButtonText = "Save".GetLocalized(),
                CloseButtonText = "Cancel".GetLocalized(),
                DefaultButton = ContentDialogButton.Close
            };
            if (destination != 0)
            {
                newQuickCommand.SecondaryButtonText = "Delete".GetLocalized();
            }
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
            {
                newQuickCommand.XamlRoot = XamlRoot;
            }
            newQuickCommand.Closed += (sender, args) =>
            {
                newQuickCommand?.Hide();
                newQuickCommand = null;
            };
            // Отобразить ContentDialog и обработать результат
            try
            {
                var saveIndex = 0;
                var result = await newQuickCommand.ShowAsync();
                // Создать ContentDialog 
                if (result == ContentDialogResult.Primary)
                {
                    SmuSettingsLoad();
                    saveIndex = comboSelSMU.SelectedIndex;
                    for (var i = 0; i < comboSelSMU.Items.Count; i++)
                    {
                        var adressName = false;
                        var adressIndex = 0;
                        comboSelSMU.SelectedIndex = i;
                        if (smusettings?.MailBoxes == null && smusettings != null)
                        {
                            smusettings.MailBoxes = new List<CustomMailBoxes>();
                            adressIndex = smusettings.MailBoxes.Count;
                            smusettings.MailBoxes.Add(new CustomMailBoxes
                            {
                                Name = comboSelSMU.SelectedItem.ToString()!,
                                CMD = textBoxCMDAddress.Text,
                                RSP = textBoxRSPAddress.Text,
                                ARG = textBoxARGAddress.Text
                            });
                        }
                        else
                        {
                            for (var d = 0; d < smusettings?.MailBoxes?.Count; d++)
                            {
                                if (smusettings.MailBoxes[d].Name != null && smusettings.MailBoxes[d].Name == comboSelSMU.SelectedItem.ToString())
                                {
                                    adressName = true;
                                    adressIndex = d;
                                    break;
                                }
                            }
                            if (adressName == false)
                            {
                                smusettings?.MailBoxes?.Add(new CustomMailBoxes
                                {
                                    Name = comboSelSMU.SelectedItem.ToString()!,
                                    CMD = textBoxCMDAddress.Text,
                                    RSP = textBoxRSPAddress.Text,
                                    ARG = textBoxARGAddress.Text
                                });
                            }
                        }
                    }
                    SmuSettingsSave();
                    if (cmdText.Text != string.Empty && argText.Text != string.Empty && smusettings != null)
                    {
                        var run = false;
                        var apply = false;
                        if (autoRun.IsChecked == true) { run = true; }
                        if (applyWith.IsChecked == true) { apply = true; }
                        if (destination == 0)
                        {
                            smusettings.QuickSMUCommands ??= new List<QuickSMUCommands>();
                            smusettings.QuickSMUCommands.Add(new QuickSMUCommands
                            {
                                Name = mainText.Text!,
                                Description = descText.Text!,
                                Symbol = SMUSymbol,
                                MailIndex = saveIndex,
                                Startup = run,
                                ApplyWith = apply,
                                Command = cmdText.Text!,
                                Argument = argText.Text!
                            });
                        }
                        else
                        {
                            smusettings.QuickSMUCommands![rowindex].Symbol = SMUSymbol;
                            smusettings.QuickSMUCommands![rowindex].Symbol = SMUSymbol1.Glyph!;
                            smusettings.QuickSMUCommands![rowindex].MailIndex = saveIndex;
                            smusettings.QuickSMUCommands![rowindex].Name = mainText.Text!;
                            smusettings.QuickSMUCommands![rowindex].Description = descText.Text!;
                            smusettings.QuickSMUCommands![rowindex].Command = cmdText.Text!;
                            smusettings.QuickSMUCommands![rowindex].Argument = argText.Text!;
                            smusettings.QuickSMUCommands![rowindex].Startup = run;
                            smusettings.QuickSMUCommands![rowindex].ApplyWith = apply;
                        }
                    }
                    comboBoxMailboxSelect.SelectedIndex = saveIndex;
                    SmuSettingsSave();
                    Init_QuickSMU();
                    newQuickCommand?.Hide();
                    newQuickCommand = null;
                }
                else
                {
                    if (result == ContentDialogResult.Secondary)
                    {
                        SmuSettingsLoad();
                        smusettings?.QuickSMUCommands!.RemoveAt(rowindex);
                        SmuSettingsSave();
                        Init_QuickSMU();
                    }
                    else
                    {
                        newQuickCommand?.Hide();
                        newQuickCommand = null;
                    }
                }
            }
            catch
            {
                newQuickCommand?.Hide();
                newQuickCommand = null;
            }

        }
        catch
        {
            // ignored
        }
    }
    private async void RangeDialog()
    {
        var comboSelSMU = new ComboBox
        {
            Margin = new Thickness(0, 20, 0, 0),
            VerticalAlignment = VerticalAlignment.Top
        };
        var cmdStart = new TextBox
        {
            Margin = new Thickness(0, 60, 0, 0),
            PlaceholderText = "Command".GetLocalized(),
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Height = 40,
            Width = 360
        };
        var argStart = new TextBox
        {
            Margin = new Thickness(0, 105, 0, 0),
            PlaceholderText = "Param_Start".GetLocalized(),
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Height = 40,
            Width = 176
        };
        var argEnd = new TextBox
        {
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(180, 105, 0, 0),
            PlaceholderText = "Param_EndW".GetLocalized(),
            Height = 40,
            Width = 179
        };
        var autoRun = new CheckBox
        {
            Margin = new Thickness(1, 155, 0, 0),
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Content = "Logging".GetLocalized(),
            IsChecked = false
        };
        try
        {
            foreach (var item in comboBoxMailboxSelect.Items)
            {
                comboSelSMU.Items.Add(item);
            }
            comboSelSMU.SelectedIndex = comboBoxMailboxSelect.SelectedIndex;
            comboSelSMU.SelectionChanged += ComboSelSMU_SelectionChanged;
        }
        catch { }
        try
        {
            var newQuickCommand = new ContentDialog
            {
                Title = "AdvancedCooler_Del_Action".GetLocalized(),
                Content = new Grid
                {
                    Children =
                    {
                        comboSelSMU,
                        cmdStart,
                        argStart,
                        argEnd,
                        autoRun
                    }
                },
                PrimaryButtonText = "Apply".GetLocalized(),
                CloseButtonText = "Cancel".GetLocalized(),
                DefaultButton = ContentDialogButton.Close
            };
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
            {
                newQuickCommand.XamlRoot = XamlRoot;
            }
            newQuickCommand.Closed += (sender, args) =>
            {
                newQuickCommand?.Hide();
                newQuickCommand = null;
            };
            // Отобразить ContentDialog и обработать результат
            try
            {
                var saveIndex = 0;
                var result = await newQuickCommand.ShowAsync();
                // Создать ContentDialog 
                if (result == ContentDialogResult.Primary)
                {
                    SmuSettingsLoad();
                    saveIndex = comboSelSMU.SelectedIndex;
                    for (var i = 0; i < comboSelSMU.Items.Count; i++)
                    {
                        var adressName = false;
                        var adressIndex = 0;
                        comboSelSMU.SelectedIndex = i;
                        if (smusettings.MailBoxes == null)
                        {
                            smusettings.MailBoxes = new List<CustomMailBoxes>();
                            adressIndex = smusettings.MailBoxes.Count;
                            smusettings.MailBoxes.Add(new CustomMailBoxes
                            {
                                Name = comboSelSMU.SelectedItem.ToString()!,
                                CMD = textBoxCMDAddress.Text,
                                RSP = textBoxRSPAddress.Text,
                                ARG = textBoxARGAddress.Text
                            });
                        }
                        else
                        {
                            for (var d = 0; d < smusettings.MailBoxes.Count; d++)
                            {
                                if (smusettings.MailBoxes[d].Name != null && smusettings.MailBoxes[d].Name == comboSelSMU.SelectedItem.ToString())
                                {
                                    adressName = true;
                                    adressIndex = d;
                                    break;
                                }
                            }
                            if (adressName == false)
                            {
                                smusettings.MailBoxes.Add(new CustomMailBoxes
                                {
                                    Name = comboSelSMU.SelectedItem.ToString()!,
                                    CMD = textBoxCMDAddress.Text,
                                    RSP = textBoxRSPAddress.Text,
                                    ARG = textBoxARGAddress.Text
                                });
                            }
                        }
                    }
                    SmuSettingsSave();
                    if (cmdStart.Text != string.Empty && argStart.Text != string.Empty && argEnd.Text != string.Empty)
                    {
                        var run = false;
                        if (autoRun.IsChecked == true) { run = true; }
                        // ConfigLoad(); config.RangeApplied = false; ConfigSave();
                        cpusend?.SendRange(cmdStart.Text, argStart.Text, argEnd.Text, saveIndex, run);
                        RangeStarted.IsOpen = true;
                        RangeStarted.Title = "SMURange".GetLocalized() + ". " + argStart.Text + "-" + argEnd.Text;
                    }
                    comboBoxMailboxSelect.SelectedIndex = saveIndex;
                    SmuSettingsSave();
                    Init_QuickSMU();
                    newQuickCommand?.Hide();
                    newQuickCommand = null;
                }
                else
                {
                    newQuickCommand?.Hide();
                    newQuickCommand = null;
                }
            }
            catch
            {
                newQuickCommand?.Hide();
                newQuickCommand = null;
            }
        }
        catch
        {
            // ignored
        }
    }
    private async void UnlockFeature()
    {
        var comboSelSMU = new ComboBox
        {
            Margin = new Thickness(0, 20, 0, 0),
            VerticalAlignment = VerticalAlignment.Top
        };
        var cmdStart = new TextBox
        {
            Margin = new Thickness(0, 60, 0, 0),
            PlaceholderText = "Command".GetLocalized(),
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Height = 40,
            Width = 360
        };
        var argStart = new TextBox
        {
            Margin = new Thickness(0, 105, 0, 0),
            PlaceholderText = "Feature ID",
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Height = 40,
            Width = 360
        };
        try
        {
            foreach (var item in comboBoxMailboxSelect.Items)
            {
                comboSelSMU.Items.Add(item);
            }
            comboSelSMU.SelectedIndex = comboBoxMailboxSelect.SelectedIndex;
            comboSelSMU.SelectionChanged += ComboSelSMU_SelectionChanged;
        }
        catch { }
        try
        {
            var newQuickCommand = new ContentDialog
            {
                Title = "AdvancedCooler_Del_Action".GetLocalized(),
                Content = new Grid
                {
                    Children =
                    {
                        comboSelSMU,
                        cmdStart,
                        argStart
                    }
                },
                PrimaryButtonText = "Apply".GetLocalized(),
                CloseButtonText = "Cancel".GetLocalized(),
                DefaultButton = ContentDialogButton.Close
            };
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
            {
                newQuickCommand.XamlRoot = XamlRoot;
            }
            newQuickCommand.Closed += (sender, args) =>
            {
                newQuickCommand?.Hide();
                newQuickCommand = null;
            };
            // Отобразить ContentDialog и обработать результат
            try
            {
                var saveIndex = 0;
                var result = await newQuickCommand.ShowAsync();
                // Создать ContentDialog 
                if (result == ContentDialogResult.Primary)
                {
                    SmuSettingsLoad();
                    saveIndex = comboSelSMU.SelectedIndex;
                    for (var i = 0; i < comboSelSMU.Items.Count; i++)
                    {
                        var adressName = false;
                        var adressIndex = 0;
                        comboSelSMU.SelectedIndex = i;
                        if (smusettings.MailBoxes == null)
                        {
                            smusettings.MailBoxes = new List<CustomMailBoxes>();
                            adressIndex = smusettings.MailBoxes.Count;
                            smusettings.MailBoxes.Add(new CustomMailBoxes
                            {
                                Name = comboSelSMU.SelectedItem.ToString()!,
                                CMD = textBoxCMDAddress.Text,
                                RSP = textBoxRSPAddress.Text,
                                ARG = textBoxARGAddress.Text
                            });
                        }
                        else
                        {
                            for (var d = 0; d < smusettings.MailBoxes.Count; d++)
                            {
                                if (smusettings.MailBoxes[d].Name != null && smusettings.MailBoxes[d].Name == comboSelSMU.SelectedItem.ToString())
                                {
                                    adressName = true;
                                    adressIndex = d;
                                    break;
                                }
                            }
                            if (adressName == false)
                            {
                                smusettings.MailBoxes.Add(new CustomMailBoxes
                                {
                                    Name = comboSelSMU.SelectedItem.ToString()!,
                                    CMD = textBoxCMDAddress.Text,
                                    RSP = textBoxRSPAddress.Text,
                                    ARG = textBoxARGAddress.Text
                                });
                            }
                        }
                    }
                    SmuSettingsSave();
                    comboBoxMailboxSelect.SelectedIndex = saveIndex;
                    comboSelSMU.SelectedIndex = saveIndex;
                    if (cmdStart.Text != string.Empty && argStart.Text != string.Empty && smusettings != null)
                    {
                        if (argStart.Text == "All")
                        {
                            var sdStatus = "";
                            for (var g = 1; 63 > g; g++)
                            {
                                var endString = "";
                                var value = 1 << g;
                                if (g > 31) { endString = "0,0x" + value.ToString("X"); }
                                else { endString = value.ToString("X"); }
                                uint[]? args;
                                var userArgs = endString.Trim().Split(',');
                                uint addrMsg;
                                uint addrRsp;
                                uint addrArg;
                                uint command;
                                args = Utils.MakeCmdArgs();
                                TryConvertToUint(smusettings.MailBoxes![comboSelSMU.SelectedIndex].CMD, out addrMsg);
                                TryConvertToUint(smusettings.MailBoxes![comboSelSMU.SelectedIndex].RSP, out addrRsp);
                                TryConvertToUint(smusettings.MailBoxes![comboSelSMU.SelectedIndex].ARG, out addrArg);
                                TryConvertToUint(cmdStart.Text, out command);
                                ZenStates.Core.Mailbox testMailbox2 = new()
                                {
                                    SMU_ADDR_MSG = addrMsg,
                                    SMU_ADDR_RSP = addrRsp,
                                    SMU_ADDR_ARG = addrArg
                                };
                                var someFeature = "";
                                for (var i = 0; i < userArgs.Length; i++)
                                {
                                    if (i == args.Length)
                                    {
                                        break;
                                    }
                                    someFeature += userArgs[i] + " ";
                                    TryConvertToUint(userArgs[i], out var temp);
                                    args[i] = temp;
                                }
                                try
                                {
                                    var status = cpu?.smu.SendSmuCommand(testMailbox2, command, ref args);
                                    sdStatus += someFeature + status + "\n";
                                    /* await Send_Message("Unlocked feature!","Command " + $"{command:X}"
                                     + " Args " + someFeature + status + " MailBox: " + smusettings.MailBoxes[saveIndex].Name 
                                     + "\n MSG: " + $"{testMailbox2.SMU_ADDR_MSG:X}" + "\n ARG: " + $"{testMailbox2.SMU_ADDR_ARG:X}"
                                     + "\n RSP: " + $"{testMailbox2.SMU_ADDR_RSP:X}", Symbol.Attach);*/
                                }
                                catch { }
                            }
                            await Send_Message("Unlocked 63 features!", "Check out! " + sdStatus, Symbol.Accept);
                        }
                        else
                        {
                            var endString = "";
                            var value = 1 << Convert.ToByte(argStart.Text);
                            if (int.Parse(argStart.Text) > 31) { endString = "0,0x" + value.ToString("X"); }
                            else { endString = value.ToString("X"); }
                            uint[]? args;
                            var userArgs = endString.Trim().Split(',');
                            uint addrMsg;
                            uint addrRsp;
                            uint addrArg;
                            uint command;
                            args = Utils.MakeCmdArgs();
                            TryConvertToUint(smusettings.MailBoxes![comboSelSMU.SelectedIndex].CMD, out addrMsg);
                            TryConvertToUint(smusettings.MailBoxes[comboSelSMU.SelectedIndex].RSP, out addrRsp);
                            TryConvertToUint(smusettings.MailBoxes[comboSelSMU.SelectedIndex].ARG, out addrArg);
                            TryConvertToUint(cmdStart.Text, out command);
                            ZenStates.Core.Mailbox testMailbox2 = new()
                            {
                                SMU_ADDR_MSG = addrMsg,
                                SMU_ADDR_RSP = addrRsp,
                                SMU_ADDR_ARG = addrArg
                            };
                            var someFeature = "";
                            for (var i = 0; i < userArgs.Length; i++)
                            {
                                if (i == args.Length)
                                {
                                    break;
                                }
                                someFeature += userArgs[i] + " ";
                                TryConvertToUint(userArgs[i], out var temp);
                                args[i] = temp;
                            }
                            try
                            {
                                var status = cpu?.smu.SendSmuCommand(testMailbox2, command, ref args);
                                /* await Send_Message("Unlocked feature!","Command " + $"{command:X}"
                                 + " Args " + someFeature + status + " MailBox: " + smusettings.MailBoxes[saveIndex].Name 
                                 + "\n MSG: " + $"{testMailbox2.SMU_ADDR_MSG:X}" + "\n ARG: " + $"{testMailbox2.SMU_ADDR_ARG:X}"
                                 + "\n RSP: " + $"{testMailbox2.SMU_ADDR_RSP:X}", Symbol.Attach);*/
                                await Send_Message("Unlocked feature!", " Args " + someFeature + status, Symbol.Attach);
                            }
                            catch { }
                        }

                    }
                    SmuSettingsSave();
                    newQuickCommand?.Hide();
                    newQuickCommand = null;
                }
                else
                {
                    newQuickCommand?.Hide();
                    newQuickCommand = null;
                }
            }
            catch
            {
                newQuickCommand?.Hide();
                newQuickCommand = null;
            }
        }
        catch
        {
            // ignored
        }
    }
    private void SymbolButton_Click(object sender, RoutedEventArgs e)
    {
        SymbolFlyout.ShowAt(sender as Button);
    }
    private void ComboSelSMU_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            var comboBox = sender as ComboBox;
            if (comboBox != null)
            {
                comboBoxMailboxSelect.SelectedIndex = comboBox.SelectedIndex;
            }
        }
        catch
        {
            // ignored
        }
    }
    private void SymbolList_ItemClick(object sender, ItemClickEventArgs e)
    {
        var glypher = (FontIcon)e.ClickedItem;
        if (glypher != null)
        {
            SMUSymbol = glypher.Glyph;
            SMUSymbol1!.Glyph = glypher.Glyph;
        }
    }
    private void SMUNotes_TextChanged(object sender, RoutedEventArgs e)
    {
        SmuSettingsLoad();
        var documentRange = SMUNotes.Document.GetRange(0, TextConstants.MaxUnitCount);
        string content;
        documentRange.GetText(TextGetOptions.FormatRtf, out content);
        smusettings.Note = content;
        SmuSettingsSave();
    }
    private void ToHex_Click(object sender, RoutedEventArgs e)
    {
        // Преобразование выделенного текста в шестнадцатиричную систему
        if (textBoxARG0.SelectedText != "")
        {
            try
            {
                var decimalValue = int.Parse(textBoxARG0.SelectedText);
                var hexValue = decimalValue.ToString("X");
                textBoxARG0.SelectedText = hexValue;
            }
            catch (FormatException)
            {
                // Отобразить сообщение об ошибке
            }
        }
        else
        {
            try
            {
                var decimalValue = int.Parse(textBoxARG0.Text);
                var hexValue = decimalValue.ToString("X");
                textBoxARG0.Text = hexValue;
            }
            catch (FormatException)
            {
                // Отобразить сообщение об ошибке
            }
        }
    }
    private void CopyThis_Click(object sender, RoutedEventArgs e)
    {
        if (textBoxARG0.SelectedText != "")
        {
            // Скопировать текст в буфер обмена
            Clipboard.SetText(textBoxARG0.SelectedText);
        }
        else
        {
            // Выделить весь текст
            textBoxARG0.SelectAll();
            // Скопировать текст в буфер обмена
            Clipboard.SetText(textBoxARG0.Text);
        }
    }
    private void CutThis_Click(object sender, RoutedEventArgs e)
    {
        if (textBoxARG0.SelectedText != "")
        {
            // Скопировать текст в буфер обмена
            Clipboard.SetText(textBoxARG0.SelectedText);
            textBoxARG0.SelectedText = "";
        }
        else
        {
            // Выделить весь текст
            textBoxARG0.SelectAll();
            // Скопировать текст в буфер обмена
            Clipboard.SetText(textBoxARG0.Text);
            textBoxARG0.Text = "";
        }
    }
    private void SelectAllThis_Click(object sender, RoutedEventArgs e)
    {
        // Выделить весь текст
        textBoxARG0.SelectAll();
    }
    private void CancelRange_Click(object sender, RoutedEventArgs e)
    {
        cpusend?.CancelRange(); CloseInfoRange();
    }
    public void CloseInfoRange()
    {
        RangeStarted.IsOpen = false;
    }
    private void UnlockFeature_Click(object sender, RoutedEventArgs e)
    {
        UnlockFeature();
    }
    //Send Message
    public async Task Send_Message(string msg, string submsg, Symbol symbol)
    {
        UniToolTip.IconSource = new SymbolIconSource()
        {
            Symbol = symbol
        };
        UniToolTip.Title = msg;
        UniToolTip.Subtitle = submsg;
        UniToolTip.IsOpen = true;
        await Task.Delay(3000);
        UniToolTip.IsOpen = false;
    }
    #endregion
    #region Event Handlers and Custom Profile voids 
    private async void ProfileCOM_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ConfigLoad();
        while (isLoaded == false || waitforload)
        {
            await Task.Delay(100);
        }
        if (ProfileCOM.SelectedIndex != -1) { config.Preset = ProfileCOM.SelectedIndex - 1; ConfigSave(); }
        indexprofile = ProfileCOM.SelectedIndex - 1;
        MainInit(ProfileCOM.SelectedIndex - 1);
    }
    //Параметры процессора
    //Максимальная температура CPU (C)
    private void C1_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = c1.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].cpu1 = check; profile[indexprofile].cpu1value = c1v.Value; ProfileSave(); }
        devices.c1 = check; devices.c1v = c1v.Value;
        DeviceSave();
    }
    //Лимит CPU (W)
    private void C2_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = c2.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].cpu2 = check; profile[indexprofile].cpu2value = c2v.Value; ProfileSave(); }
        devices.c2 = check; devices.c2v = c2v.Value;
        DeviceSave();
    }
    //Реальный CPU (W)
    private void C3_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = c3.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].cpu3 = check; profile[indexprofile].cpu3value = c3v.Value; ProfileSave(); }
        devices.c3 = check; devices.c3v = c3v.Value;
        DeviceSave();
    }
    //Средний CPU (W)
    private void C4_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = c4.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].cpu4 = check; profile[indexprofile].cpu4value = c4v.Value; ProfileSave(); }
        devices.c4 = check; devices.c4v = c4v.Value;
        DeviceSave();
    }
    //Тик быстрого разгона (S)
    private void C5_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = c5.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].cpu5 = check; profile[indexprofile].cpu5value = c5v.Value; ProfileSave(); }
        devices.c5 = check; devices.c5v = c5v.Value;
        DeviceSave();
    }
    //Тик медленного разгона (S)
    private void C6_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = c6.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].cpu6 = check; profile[indexprofile].cpu6value = c6v.Value; ProfileSave(); }
        devices.c6 = check; devices.c6v = c6v.Value;
        DeviceSave();
    }
    //Параметры VRM
    //Максимальный ток VRM A
    private void V1_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = V1.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].vrm1 = check; profile[indexprofile].vrm1value = V1V.Value; ProfileSave(); }
        devices.v1 = check; devices.v1v = V1V.Value;
        DeviceSave();
    }
    //Лимит по току VRM A
    private void V2_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = V2.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].vrm2 = check; profile[indexprofile].vrm2value = V2V.Value; ProfileSave(); }
        devices.v2 = check; devices.v2v = V2V.Value;
        DeviceSave();
    }
    //Максимальный ток SOC A
    private void V3_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = V3.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].vrm3 = check; profile[indexprofile].vrm3value = V3V.Value; ProfileSave(); }
        devices.v3 = check; devices.v3v = V3V.Value;
        DeviceSave();
    }
    //Лимит по току SOC A
    private void V4_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = V4.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].vrm4 = check; profile[indexprofile].vrm4value = V4V.Value; ProfileSave(); }
        devices.v4 = check; devices.v4v = V4V.Value;
        DeviceSave();
    }
    //Максимальный ток PCI VDD A
    private void V5_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = V5.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].vrm5 = check; profile[indexprofile].vrm5value = V5V.Value; ProfileSave(); }
        devices.v5 = check; devices.v5v = V5V.Value;
        DeviceSave();
    }
    //Максимальный ток PCI SOC A
    private void V6_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = V6.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].vrm6 = check; profile[indexprofile].vrm6value = V6V.Value; ProfileSave(); }
        devices.v6 = check; devices.v6v = V6V.Value;
        DeviceSave();
    }
    //Отключить троттлинг на время
    private void V7_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = V7.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].vrm7 = check; profile[indexprofile].vrm7value = V7V.Value; ProfileSave(); }
        devices.v7 = check; devices.v7v = V7V.Value;
        DeviceSave();
    }
    //Параметры графики
    //Минимальная частота SOC 
    private void G1_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = g1.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu1 = check; profile[indexprofile].gpu1value = g1v.Value; ProfileSave(); }
        devices.g1 = check; devices.g1v = g1v.Value;
        DeviceSave();
    }
    //Максимальная частота SOC
    private void G2_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = g2.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu2 = check; profile[indexprofile].gpu2value = g2v.Value; ProfileSave(); }
        devices.g2 = check; devices.g2v = g2v.Value;
        DeviceSave();
    }
    //Минимальная частота Infinity Fabric
    private void G3_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = g3.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu3 = check; profile[indexprofile].gpu3value = g3v.Value; ProfileSave(); }
        devices.g3 = check; devices.g3v = g3v.Value;
        DeviceSave();
    }
    //Максимальная частота Infinity Fabric
    private void G4_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = g4.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu4 = check; profile[indexprofile].gpu4value = g4v.Value; ProfileSave(); }
        devices.g4 = check; devices.g4v = g4v.Value;
        DeviceSave();
    }
    //Минимальная частота кодека VCE
    private void G5_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = g5.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu5 = check; profile[indexprofile].gpu5value = g5v.Value; ProfileSave(); }
        devices.g5 = check; devices.g5v = g5v.Value;
        DeviceSave();
    }
    //Максимальная частота кодека VCE
    private void G6_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = g6.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu6 = check; profile[indexprofile].gpu6value = g6v.Value; ProfileSave(); }
        devices.g6 = check; devices.g6v = g6v.Value;
        DeviceSave();
    }
    //Минимальная частота частота Data Latch
    private void G7_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = g7.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu7 = check; profile[indexprofile].gpu7value = g7v.Value; ProfileSave(); }
        devices.g7 = check; devices.g7v = g7v.Value;
        DeviceSave();
    }
    //Максимальная частота Data Latch
    private void G8_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = g8.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu8 = check; profile[indexprofile].gpu8value = g8v.Value; ProfileSave(); }
        devices.g8 = check; devices.g8v = g8v.Value;
        DeviceSave();
    }
    //Минимальная частота iGpu
    private void G9_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = g9.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu9 = check; profile[indexprofile].gpu9value = g9v.Value; ProfileSave(); }
        devices.g9 = check; devices.g9v = g9v.Value;
        DeviceSave();
    }
    //Максимальная частота iGpu
    private void G10_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = g10.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu10 = check; profile[indexprofile].gpu10value = g10v.Value; ProfileSave(); }
        devices.g10 = check; devices.g10v = g10v.Value;
        DeviceSave();
    }
    //Расширенные параметры
    private void A1_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = a1.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd1 = check; profile[indexprofile].advncd1value = a1v.Value; ProfileSave(); }
        devices.a1 = check; devices.a1v = a1v.Value;
        DeviceSave();
    }
    private void A2_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = a2.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd2 = check; profile[indexprofile].advncd2value = a2v.Value; ProfileSave(); }
        devices.a2 = check; devices.a2v = a2v.Value;
        DeviceSave();
    }
    private void A3_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = a3.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd3 = check; profile[indexprofile].advncd3value = a3v.Value; ProfileSave(); }
        devices.a3 = check; devices.a3v = a3v.Value;
        DeviceSave();
    }
    private void A4_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = a4.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd4 = check; profile[indexprofile].advncd4value = a4v.Value; ProfileSave(); }
        devices.a4 = check; devices.a4v = a4v.Value;
        DeviceSave();
    }
    private void A5_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = a5.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd5 = check; profile[indexprofile].advncd5value = a5v.Value; ProfileSave(); }
        devices.a5 = check; devices.a5v = a5v.Value;
        DeviceSave();
    }
    private void A6_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = a6.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd6 = check; profile[indexprofile].advncd6value = a6v.Value; ProfileSave(); }
        devices.a6 = check; devices.a6v = a6v.Value;
        DeviceSave();
    }
    private void A7_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = a7.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd7 = check; profile[indexprofile].advncd7value = a7v.Value; ProfileSave(); }
        devices.a7 = check; devices.a7v = a7v.Value;
        DeviceSave();
    }
    private void A8_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = a8.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd8 = check; profile[indexprofile].advncd8value = a8v.Value; ProfileSave(); }
        devices.a8 = check; devices.a8v = a8v.Value;
        DeviceSave();
    }
    private void A9_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = a9.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd9 = check; profile[indexprofile].advncd9value = a9v.Value; ProfileSave(); }
        devices.a9 = check; devices.a9v = a9v.Value;
        DeviceSave();
    }
    private void A10_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = a10.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd10 = check; profile[indexprofile].advncd10value = a10v.Value; ProfileSave(); }
        devices.a10 = check; devices.a10v = a10v.Value;
        DeviceSave();
    }
    private void A11_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = a11.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd11 = check; profile[indexprofile].advncd11value = a11v.Value; ProfileSave(); }
        devices.a11 = check; devices.a11v = a11v.Value;
        DeviceSave();
    }
    private void A12_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = a12.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd12 = check; profile[indexprofile].advncd12value = a12v.Value; ProfileSave(); }
        devices.a12 = check; devices.a12v = a12v.Value;
        DeviceSave();
    }
    private void A13_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = a13.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd13 = check; profile[indexprofile].advncd1value = a13m.SelectedIndex; ProfileSave(); }
        devices.a13 = check; devices.a13v = a13m.SelectedIndex;
        DeviceSave();
    }
    //Параметры процессора, при изменении слайдеров
    //Максимальная температура CPU (C)
    private void C1_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.c1v = c1v.Value;
        if (indexprofile != -1) { profile[indexprofile].cpu1value = c1v.Value; ProfileSave(); }
        DeviceSave();
    }
    //Лимит CPU (W)
    private void C2_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.c2v = c2v.Value;
        if (indexprofile != -1) { profile[indexprofile].cpu2value = c2v.Value; ProfileSave(); }
        DeviceSave();
    }
    //Реальный CPU (W)
    private void C3_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.c3v = c3v.Value;
        if (indexprofile != -1) { profile[indexprofile].cpu3value = c3v.Value; ProfileSave(); }
        DeviceSave();
    }
    //Средний CPU(W)
    private void C4_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.c4v = c4v.Value;
        if (indexprofile != -1) { profile[indexprofile].cpu4value = c4v.Value; ProfileSave(); }
        DeviceSave();
    }
    //Тик быстрого разгона (S)
    private void C5_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.c5v = c5v.Value;
        if (indexprofile != -1) { profile[indexprofile].cpu5value = c5v.Value; ProfileSave(); }
        DeviceSave();
    }
    //Тик медленного разгона (S)
    private void C6_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.c6v = c6v.Value;
        if (indexprofile != -1) { profile[indexprofile].cpu6value = c6v.Value; ProfileSave(); }
        DeviceSave();
    }
    //Параметры VRM
    private void V1v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.v1v = V1V.Value;
        if (indexprofile != -1) { profile[indexprofile].vrm1value = V1V.Value; ProfileSave(); }
        DeviceSave();
    }
    private void V2v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.v2v = V2V.Value;
        if (indexprofile != -1) { profile[indexprofile].vrm2value = V2V.Value; ProfileSave(); }
        DeviceSave();
    }
    private void V3v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.v3v = V3V.Value;
        if (indexprofile != -1) { profile[indexprofile].vrm3value = V3V.Value; ProfileSave(); }
        DeviceSave();
    }
    private void V4v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.v4v = V4V.Value;
        if (indexprofile != -1) { profile[indexprofile].vrm4value = V4V.Value; ProfileSave(); }
        DeviceSave();
    }
    private void V5v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.v5v = V5V.Value;
        if (indexprofile != -1) { profile[indexprofile].vrm5value = V5V.Value; ProfileSave(); }
        DeviceSave();
    }
    private void V6v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.v6v = V6V.Value;
        if (indexprofile != -1) { profile[indexprofile].vrm6value = V6V.Value; ProfileSave(); }
        DeviceSave();
    }
    private void V7v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.v7v = V7V.Value;
        if (indexprofile != -1) { profile[indexprofile].vrm7value = V7V.Value; ProfileSave(); }
        DeviceSave();
    }
    //Параметры GPU
    private void G1v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.g1v = g1v.Value;
        if (indexprofile != -1) { profile[indexprofile].gpu1value = g1v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void G2v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.g2v = g2v.Value;
        if (indexprofile != -1) { profile[indexprofile].gpu2value = g2v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void G3v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.g3v = g3v.Value;
        if (indexprofile != -1) { profile[indexprofile].gpu3value = g3v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void G4v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.g4v = g4v.Value;
        if (indexprofile != -1) { profile[indexprofile].gpu4value = g4v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void G5v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.g5v = g5v.Value;
        if (indexprofile != -1) { profile[indexprofile].gpu5value = g5v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void G6v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.g6v = g6v.Value;
        if (indexprofile != -1) { profile[indexprofile].gpu6value = g6v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void G7v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.g7v = g7v.Value;
        if (indexprofile != -1) { profile[indexprofile].gpu7value = g7v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void G8v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.g8v = g8v.Value;
        if (indexprofile != -1) { profile[indexprofile].gpu8value = g8v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void G9v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.g9v = g9v.Value;
        if (indexprofile != -1) { profile[indexprofile].gpu9value = g9v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void G10v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        DeviceLoad(); ProfileLoad();
        devices.g10v = g10v.Value;
        if (indexprofile != -1) { profile[indexprofile].gpu10value = g10v.Value; ProfileSave(); }
        DeviceSave();
    }
    //Расширенные параметры
    private void A1v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.a1v = a1v.Value;
        if (indexprofile != -1) { profile[indexprofile].advncd1value = a1v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void A2v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.a2v = a2v.Value;
        if (indexprofile != -1) { profile[indexprofile].advncd2value = a2v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void A3v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.a3v = a3v.Value;
        if (indexprofile != -1) { profile[indexprofile].advncd3value = a3v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void A4v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.a4v = a4v.Value;
        if (indexprofile != -1) { profile[indexprofile].advncd4value = a4v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void A5v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.a5v = a5v.Value;
        if (indexprofile != -1) { profile[indexprofile].advncd5value = a5v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void A6v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.a6v = a6v.Value;
        if (indexprofile != -1) { profile[indexprofile].advncd6value = a6v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void A7v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.a7v = a7v.Value;
        if (indexprofile != -1) { profile[indexprofile].advncd7value = a7v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void A8v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.a8v = a8v.Value;
        if (indexprofile != -1) { profile[indexprofile].advncd8value = a8v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void A9v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.a9v = a9v.Value;
        if (indexprofile != -1) { profile[indexprofile].advncd9value = a9v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void A10v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.a10v = a10v.Value;
        if (indexprofile != -1) { profile[indexprofile].advncd10value = a10v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void A11v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.a11v = a11v.Value;
        if (indexprofile != -1) { profile[indexprofile].advncd11value = a11v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void A12v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.a12v = a12v.Value;
        if (indexprofile != -1) { profile[indexprofile].advncd12value = a12v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void A13m_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.a13v = a13m.SelectedIndex;
        if (indexprofile != -1) { profile[indexprofile].advncd13value = a13m.SelectedIndex; ProfileSave(); }
        DeviceSave();
    }
    //Новые
    private void C7_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = c7.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].cpu7 = check; profile[indexprofile].cpu7value = c7v.Value; ProfileSave(); }
        devices.c7 = check; devices.c7v = c7v.Value;
        DeviceSave();
    }
    private void C7_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.c7v = c7v.Value;
        if (indexprofile != -1) { profile[indexprofile].cpu7value = c7v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void V8_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        if (V8.IsChecked == true) { App.MainWindow.ShowMessageDialogAsync("Param_Voltage_warn_heavy".GetLocalized(), "Param_Super_heavywarn".GetLocalized()); }
        ProfileLoad(); DeviceLoad();
        var check = V8.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].vrm8 = check; profile[indexprofile].vrm8value = V8V.Value; ProfileSave(); }
        devices.v8 = check; devices.v8v = V8V.Value;
        DeviceSave();
    }
    private void V8V_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.v8v = V8V.Value;
        if (indexprofile != -1) { profile[indexprofile].vrm8value = V8V.Value; ProfileSave(); }
        DeviceSave();
    }
    private void V9_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        if (V9.IsChecked == true) { App.MainWindow.ShowMessageDialogAsync("Param_Voltage_warn_heavy".GetLocalized(), "Param_Super_heavywarn".GetLocalized()); }
        ProfileLoad(); DeviceLoad();
        var check = V9.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].vrm9 = check; profile[indexprofile].vrm9value = V9V.Value; ProfileSave(); }
        devices.v9 = check; devices.v9v = V9V.Value;
        DeviceSave();
    }
    private void V9V_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.v9v = V9V.Value;
        if (indexprofile != -1) { profile[indexprofile].vrm9value = V9V.Value; ProfileSave(); }
        DeviceSave();
    }
    private void V10_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        if (V10.IsChecked == true) { App.MainWindow.ShowMessageDialogAsync("Param_Voltage_warn_heavy".GetLocalized(), "Param_Super_heavywarn".GetLocalized()); }
        ProfileLoad(); DeviceLoad();
        var check = V10.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].vrm10 = check; profile[indexprofile].vrm8value = V10V.Value; ProfileSave(); }
        devices.v10 = check; devices.v10v = V10V.Value;
        DeviceSave();
    }
    private void V10V_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.v10v = V10V.Value;
        if (indexprofile != -1) { profile[indexprofile].vrm10value = V10V.Value; ProfileSave(); }
        DeviceSave();
    }
    private void G11_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = g11.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu11 = check; profile[indexprofile].gpu11value = g11v.Value; ProfileSave(); }
        devices.g11 = check; devices.g11v = g11v.Value;
        DeviceSave();
    }
    private void G11v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.g11v = g11v.Value;
        if (indexprofile != -1) { profile[indexprofile].gpu11value = g11v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void G12_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = g12.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu12 = check; profile[indexprofile].gpu12value = g12v.Value; ProfileSave(); }
        devices.g12 = check; devices.g12v = g12v.Value;
        DeviceSave();
    }
    private void G12v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.g11v = g12v.Value;
        if (indexprofile != -1) { profile[indexprofile].gpu12value = g12v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void G13_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = g13.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu13 = check; profile[indexprofile].gpu13value = g13v.Value; ProfileSave(); }
        devices.g13 = check; devices.g13v = g13v.Value;
        DeviceSave();
    }
    private void G13v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.g13v = g13v.Value;
        if (indexprofile != -1) { profile[indexprofile].gpu13value = g13v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void G14_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = g14.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu14 = check; profile[indexprofile].gpu14value = g14v.Value; ProfileSave(); }
        devices.g14 = check; devices.g14v = g14v.Value;
        DeviceSave();
    }
    private void G14v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.g14v = g14v.Value;
        if (indexprofile != -1) { profile[indexprofile].gpu14value = g14v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void G15_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = g15.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu15 = check; profile[indexprofile].gpu15value = g15m.SelectedIndex; ProfileSave(); }
        devices.g15 = check; devices.g15v = g15m.SelectedIndex;
        DeviceSave();
    }
    private void G15m_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.g15v = g15m.SelectedIndex;
        if (indexprofile != -1) { profile[indexprofile].gpu15value = g15m.SelectedIndex; ProfileSave(); }
        DeviceSave();
    }
    private void G16_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = g16.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu16 = check; profile[indexprofile].gpu16value = g16m.SelectedIndex; ProfileSave(); }
        devices.g16 = check; devices.g16v = g16m.SelectedIndex;
        DeviceSave();
    }
    private void G16m_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.g16v = g16m.SelectedIndex;
        if (indexprofile != -1) { profile[indexprofile].gpu16value = g16m.SelectedIndex; ProfileSave(); }
        DeviceSave();
    }
    private void A14_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = a14.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd14 = check; profile[indexprofile].advncd14value = a14m.SelectedIndex; ProfileSave(); }
        devices.a14 = check; devices.a14v = a14m.SelectedIndex;
        DeviceSave();
    }
    private void A14m_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.a14v = a14m.SelectedIndex;
        if (indexprofile != -1) { profile[indexprofile].advncd14value = a14m.SelectedIndex; ProfileSave(); }
        DeviceSave();
    }
    private void A15_Checked(object sender, RoutedEventArgs e)
    { 
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad(); DeviceLoad();
        var check = a15.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd15 = check; profile[indexprofile].advncd15value = a15v.Value; ProfileSave(); }
        devices.a15 = check; devices.a15v = a15v.Value;
        DeviceSave();  
    }
    private void A15v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        DeviceLoad(); ProfileLoad();
        devices.a15v = a15v.Value;
        if (indexprofile != -1) { profile[indexprofile].advncd15value = a15v.Value; ProfileSave(); }
        DeviceSave();
    }
    //Кнопка применить, итоговый выход, Zen States-Core SMU Command
    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (c1.IsChecked == true)
        {
            adjline += " --tctl-temp=" + c1v.Value;
        }

        if (c2.IsChecked == true)
        {
            adjline += " --stapm-limit=" + c2v.Value + "000";
        }

        if (c3.IsChecked == true)
        {
            adjline += " --fast-limit=" + c3v.Value + "000";
        }

        if (c4.IsChecked == true)
        {
            adjline += " --slow-limit=" + c4v.Value + "000";
        }

        if (c5.IsChecked == true)
        {
            adjline += " --stapm-time=" + c5v.Value;
        }

        if (c6.IsChecked == true)
        {
            adjline += " --slow-time=" + c6v.Value;
        }
        if (c7.IsChecked == true)
        {
            adjline += " --cHTC-temp=" + c7v.Value;
        }

        //vrm
        if (V1.IsChecked == true)
        {
            adjline += " --vrmmax-current=" + V1V.Value + "000";
        }

        if (V2.IsChecked == true)
        {
            adjline += " --vrm-current=" + V2V.Value + "000";
        }

        if (V3.IsChecked == true)
        {
            adjline += " --vrmsocmax-current=" + V3V.Value + "000";
        }

        if (V4.IsChecked == true)
        {
            adjline += " --vrmsoc-current=" + V4V.Value + "000";
        }

        if (V5.IsChecked == true)
        {
            adjline += " --psi0-current=" + V5V.Value + "000";
        }

        if (V6.IsChecked == true)
        {
            adjline += " --psi0soc-current=" + V6V.Value + "000";
        }

        if (V7.IsChecked == true)
        {
            adjline += " --prochot-deassertion-ramp=" + V7V.Value;
        }
        if (V8.IsChecked == true)
        {
            adjline += " --oc-volt-scalar=" + V8V.Value;
        }
        if (V9.IsChecked == true)
        {
            adjline += " --oc-volt-modular=" + V9V.Value;
        }
        if (V10.IsChecked == true)
        {
            adjline += " --oc-volt-variable=" + V10V.Value;
        }

        //gpu
        if (g1.IsChecked == true)
        {
            adjline += " --min-socclk-frequency=" + g1v.Value;
        }

        if (g2.IsChecked == true)
        {
            adjline += " --max-socclk-frequency=" + g2v.Value;
        }

        if (g3.IsChecked == true)
        {
            adjline += " --min-fclk-frequency=" + g3v.Value;
        }

        if (g4.IsChecked == true)
        {
            adjline += " --max-fclk-frequency=" + g4v.Value;
        }

        if (g5.IsChecked == true)
        {
            adjline += " --min-vcn=" + g5v.Value;
        }

        if (g6.IsChecked == true)
        {
            adjline += " --max-vcn=" + g6v.Value;
        }

        if (g7.IsChecked == true)
        {
            adjline += " --min-lclk=" + g7v.Value;
        }

        if (g8.IsChecked == true)
        {
            adjline += " --max-lclk=" + g8v.Value;
        }

        if (g9.IsChecked == true)
        {
            adjline += " --min-gfxclk=" + g9v.Value;
        }

        if (g10.IsChecked == true)
        {
            adjline += " --max-gfxclk=" + g10v.Value;
        }
        if (g11.IsChecked == true)
        {
            adjline += " --min-cpuclk=" + g11v.Value;
        }
        if (g12.IsChecked == true)
        {
            adjline += " --max-cpuclk=" + g12v.Value;
        }
        if (g13.IsChecked == true)
        {
            adjline += " --setgpu-arerture-low=" + g13v.Value;
        }
        if (g14.IsChecked == true)
        {
            adjline += " --setgpu-arerture-high=" + g14v.Value;
        }
        if (g15.IsChecked == true)
        {
            if (g15m.SelectedIndex != 0) { adjline += " --start-gpu-link=" + (g15m.SelectedIndex - 1).ToString(); } 
            else { adjline += " --stop-gpu-link=0"; }
        }
        if (g16.IsChecked == true)
        {
            if (g16m.SelectedIndex != 0) { adjline += " --setcpu-freqto-ramstate=" + (g16m.SelectedIndex - 1).ToString(); }
            else { adjline += " --stopcpu-freqto-ramstate=0"; }
        }
        //advanced
        if (a1.IsChecked == true)
        {
            adjline += " --vrmgfx-current=" + a1v.Value + "000";
        }

        if (a2.IsChecked == true)
        {
            adjline += " --vrmcvip-current=" + a2v.Value + "000";
        }

        if (a3.IsChecked == true)
        {
            adjline += " --vrmgfxmax_current=" + a3v.Value + "000";
        }

        if (a4.IsChecked == true)
        {
            adjline += " --psi3cpu_current=" + a4v.Value + "000";
        }

        if (a5.IsChecked == true)
        {
            adjline += " --psi3gfx_current=" + a5v.Value + "000";
        }

        if (a6.IsChecked == true)
        {
            adjline += " --apu-skin-temp=" + a6v.Value;
        }

        if (a7.IsChecked == true)
        {
            adjline += " --dgpu-skin-temp=" + a7v.Value;
        }

        if (a8.IsChecked == true)
        {
            adjline += " --apu-slow-limit=" + a8v.Value + "000";
        }

        if (a9.IsChecked == true)
        {
            adjline += " --skin-temp-limit=" + a9v.Value + "000";
        }

        if (a10.IsChecked == true)
        {
            adjline += " --gfx-clk=" + a10v.Value;
        }

        if (a11.IsChecked == true)
        {
            adjline += " --oc-clk=" + a11v.Value;
        }

        if (a12.IsChecked == true)
        {
            adjline += " --oc-volt=" + Math.Round((1.55 - a12v.Value / 1000) / 0.00625);
        }
       

        if (a13.IsChecked == true)
        {
            if (a13m.SelectedIndex == 1)
            {
                adjline += " --max-performance=1";
            }

            if (a13m.SelectedIndex == 2)
            {
                adjline += " --power-saving=1";
            }
        }
        if (a14.IsChecked == true)
        {
            if (a14m.SelectedIndex == 0)
            {
                adjline += " --disable-oc=1";
            }

            if (a14m.SelectedIndex == 1)
            {
                adjline += " --enable-oc=1";
            }
        }
        if (a15.IsChecked == true)
        {
            adjline += " --pbo-scalar=" + a15v.Value * 100;
        }
        ConfigLoad();
        config.adjline = adjline + " ";
        config.ApplyInfo = "";
        adjline = "";
        ConfigSave(); 
        MainWindow.Applyer.Apply(true); 
        if (EnablePstates.IsOn) { BtnPstateWrite_Click(); }
        await Task.Delay(1000);
        ConfigLoad();
        var timer = 1000;
        timer *= config.ApplyInfo.Split('\n').Length + 1;
        Apply_tooltip.Title = "Apply_Success".GetLocalized(); Apply_tooltip.Subtitle = "Apply_Success_Desc".GetLocalized();
        Apply_tooltip.IconSource = new SymbolIconSource { Symbol = Symbol.Accept };
        Apply_tooltip.IsOpen = true; var infoSet = InfoBarSeverity.Success; 
        if (config.ApplyInfo != "") { Apply_tooltip.Title = "Apply_Warn".GetLocalized(); Apply_tooltip.Subtitle = "Apply_Warn_Desc".GetLocalized() + config.ApplyInfo; Apply_tooltip.IconSource = new SymbolIconSource { Symbol = Symbol.ReportHacked }; await Task.Delay(timer); Apply_tooltip.IsOpen = false; infoSet = InfoBarSeverity.Warning; }
        else { await Task.Delay(3000); Apply_tooltip.IsOpen = false; } 
        NotifyLoad();
        notify.Notifies ??= new List<Notify>();
        notify.Notifies.Add(new Notify { Title = Apply_tooltip.Title, Msg = Apply_tooltip.Subtitle, Type = infoSet });
        NotifySave(); 
        if (textBoxARG0 != null && textBoxARGAddress != null && textBoxCMD != null && textBoxCMDAddress != null && textBoxRSPAddress != null && EnableSMU.IsOn) { ApplySettings(0, 0); }
        cpusend ??= new SendSMUCommand();
        cpusend.Play_Invernate_QuickSMU(0);
    }
    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (SaveProfileN.Text != "")
        {
            ConfigLoad();
            ProfileLoad();
            try
            {
                config.Preset += 1;
                indexprofile += 1;
                waitforload = true;
                ProfileCOM.Items.Add(SaveProfileN.Text);
                ProfileCOM.SelectedItem = SaveProfileN.Text;
                var profileList = new List<Profile>(profile)
                {
                    new()
                };
                profile = profileList.ToArray();
                waitforload = false;
                profile[indexprofile].profilename = SaveProfileN.Text;
                NotifyLoad();
                notify.Notifies ??= new List<Notify>();
                notify.Notifies.Add(new Notify { Title = "SaveSuccessTitle".GetLocalized(), Msg = "SaveSuccessDesc".GetLocalized() + " " + SaveProfileN.Text, Type = InfoBarSeverity.Success });
                NotifySave();
            }
            catch
            {
                Add_tooltip_Max.IsOpen = true;
                await Task.Delay(3000);
                Add_tooltip_Max.IsOpen = false;
            }
        }
        else
        {
            NotifyLoad();
            notify.Notifies ??= new List<Notify>();
            notify.Notifies.Add(new Notify { Title = Add_tooltip_Error.Title, Msg = Add_tooltip_Error.Subtitle, Type = InfoBarSeverity.Error }) ;
            NotifySave();
            Add_tooltip_Error.IsOpen = true;
            await Task.Delay(3000);
            Add_tooltip_Error.IsOpen = false;
        }
        ConfigSave();
        ProfileSave();
    }
    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        EditProfileButton.Flyout.Hide();
        if (EditProfileN.Text != "")
        {
            if (ProfileCOM.SelectedIndex == 0 || indexprofile + 1 == 0)
            {
                Unsaved_tooltip.IsOpen = true;
                await Task.Delay(3000);
                Unsaved_tooltip.IsOpen = false;
            }
            else
            {
                ProfileLoad();
                profile[indexprofile].profilename = EditProfileN.Text;
                ProfileSave();
                waitforload = true;
                ProfileCOM.Items.Clear();
                ProfileCOM.Items.Add("Unsaved");
                for (var i = 0; i < profile.Length; i++)
                {
                    if (profile[i].profilename != string.Empty || profile[i].profilename != "Unsigned profile")
                    {
                        ProfileCOM.Items.Add(profile[i].profilename);
                    }
                }
                ProfileCOM.SelectedIndex = 0;
                waitforload = false;
                ProfileCOM.SelectedItem = EditProfileN.Text;
                NotifyLoad();
                notify.Notifies ??= new List<Notify>();
                notify.Notifies.Add(new Notify { Title = Edit_tooltip.Title, Msg = Edit_tooltip.Subtitle + " " + SaveProfileN.Text, Type = InfoBarSeverity.Success });
                NotifySave();
                Edit_tooltip.IsOpen = true;
                await Task.Delay(3000);
                Edit_tooltip.IsOpen = false;
            }
        }
        else
        {
            NotifyLoad();
            notify.Notifies ??= new List<Notify>();
            notify.Notifies.Add(new Notify { Title = Edit_tooltip_Error.Title, Msg = Edit_tooltip_Error.Subtitle, Type = InfoBarSeverity.Error });
            NotifySave();
            Edit_tooltip_Error.IsOpen = true;
            await Task.Delay(3000);
            Edit_tooltip_Error.IsOpen = false;
        }
    } 
    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        var DelDialog = new ContentDialog
        {
            Title = "Param_DelPreset_Text".GetLocalized(),
            Content = "Param_DelPreset_Desc".GetLocalized(),
            CloseButtonText = "Cancel".GetLocalized(),
            PrimaryButtonText = "Delete".GetLocalized(),
            DefaultButton = ContentDialogButton.Close
        };
        // Use this code to associate the dialog to the appropriate AppWindow by setting
        // the dialog's XamlRoot to the same XamlRoot as an element that is already present in the AppWindow.
        if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8)) { DelDialog.XamlRoot = XamlRoot; }
        var result = await DelDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            if (ProfileCOM.SelectedIndex == 0)
            {
                NotifyLoad();
                notify.Notifies ??= new List<Notify>();
                notify.Notifies.Add(new Notify { Title = Delete_tooltip_error.Title, Msg = Delete_tooltip_error.Subtitle, Type = InfoBarSeverity.Error });
                NotifySave();
                Delete_tooltip_error.IsOpen = true;
                await Task.Delay(3000);
                Delete_tooltip_error.IsOpen = false;
            }
            else
            {
                ProfileLoad();
                waitforload = true;
                ProfileCOM.Items.Remove(profile[indexprofile].profilename);
                var profileList = new List<Profile>(profile);
                profileList.RemoveAt(indexprofile);
                profile = profileList.ToArray();
                indexprofile = 0;
                waitforload = false;
                ProfileCOM.SelectedIndex = 0;
                NotifyLoad();
                notify.Notifies ??= new List<Notify>();
                notify.Notifies.Add(new Notify { Title = "DeleteSuccessTitle".GetLocalized(), Msg = "DeleteSuccessDesc".GetLocalized(), Type = InfoBarSeverity.Success });
                NotifySave();
            }
            ProfileSave(); 
        } 
    }
 
    #endregion
    #region PState Section related voids
    public async void BtnPstateWrite_Click()
    {
        DeviceLoad();
        if (devices.autopstate)
        {
            if (Without_P0.IsOn)
            {
                WritePstates();
            }
            else
            {
                WritePstatesWithoutP0();
            }
        }
        else
        {
            if (IgnoreWarn.IsOn)
            {
                if (Without_P0.IsOn)
                {
                    WritePstates();
                }
                else
                {
                    WritePstatesWithoutP0();
                }
            }
            else
            {
                if (Without_P0.IsOn)
                {
                    var WriteDialog = new ContentDialog
                    {
                        Title = "Param_ChPstates_Text".GetLocalized(),
                        Content = "Param_ChPstates_Desc".GetLocalized(),
                        CloseButtonText = "Cancel".GetLocalized(),
                        PrimaryButtonText = "Change".GetLocalized(),
                        DefaultButton = ContentDialogButton.Close
                    };
                    // Use this code to associate the dialog to the appropriate AppWindow by setting
                    // the dialog's XamlRoot to the same XamlRoot as an element that is already present in the AppWindow.
                    if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
                    {
                        WriteDialog.XamlRoot = XamlRoot;
                    }

                    var result1 = await WriteDialog.ShowAsync();
                    if (result1 == ContentDialogResult.Primary)
                    {
                        WritePstates();
                    }
                }
                else
                {
                    var ApplyDialog = new ContentDialog
                    {
                        Title = "Param_ChPstates_Text".GetLocalized(),
                        Content = "Param_ChPstates_Desc".GetLocalized(),
                        CloseButtonText = "Cancel".GetLocalized(),
                        PrimaryButtonText = "Change".GetLocalized(),
                        SecondaryButtonText = "Without_P0".GetLocalized(),
                        DefaultButton = ContentDialogButton.Close
                    };

                    // Use this code to associate the dialog to the appropriate AppWindow by setting
                    // the dialog's XamlRoot to the same XamlRoot as an element that is already present in the AppWindow.
                    if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
                    {
                        ApplyDialog.XamlRoot = XamlRoot;
                    }
                    try
                    {
                        var result = await ApplyDialog.ShowAsync();
                        if (result == ContentDialogResult.Primary) { WritePstates(); }
                        if (result == ContentDialogResult.Secondary) { WritePstatesWithoutP0(); }
                    }
                    catch
                    {
                        //Unable to set PStates
                        WritePstatesWithoutP0();
                    } 
                }
            }
        }
    }
    public void WritePstates()
    {
        try
        {
            if (devices.autopstate)
            {
                DID_0.Value = devices.did0;
                DID_1.Value = devices.did1;
                DID_2.Value = devices.did2;
                FID_0.Value = devices.fid0;
                FID_1.Value = devices.fid1;
                FID_2.Value = devices.fid2;
            }
            for (var p = 0; p < 3; p++)
            {
                if (string.IsNullOrEmpty(DID_0.Text) || string.IsNullOrEmpty(FID_0.Text) || string.IsNullOrEmpty(DID_1.Text) || string.IsNullOrEmpty(FID_1.Text) || string.IsNullOrEmpty(DID_2.Text) || string.IsNullOrEmpty(FID_2.Text)) { ReadPstate(); }
                //Logic
                var pstateId = p;
                uint eax = default, edx = default;
                uint IddDiv = 0x0;
                uint IddVal = 0x0;
                uint CpuVid = 0x0;
                uint CpuDfsId = 0x0;
                uint CpuFid = 0x0;
                var Didtext = "12";
                var Fidtext = "102";
                var Vidtext = 56.0;
                if (cpu?.ReadMsr(Convert.ToUInt32(Convert.ToInt64(0xC0010064) + pstateId), ref eax, ref edx) == false)
                {
                    MessageBox.Show("Error reading PState! ID = " + pstateId);
                    return;
                }
                CalculatePstateDetails(eax, ref IddDiv, ref IddVal, ref CpuVid, ref CpuDfsId, ref CpuFid);
                switch (p)
                {
                    case 0:
                        Didtext = DID_0.Text;
                        Fidtext = FID_0.Text;
                        Vidtext = VID_0.Value;
                        break;
                    case 1:
                        Didtext = DID_1.Text;
                        Fidtext = FID_1.Text;
                        Vidtext = VID_1.Value;
                        break;
                    case 2:
                        Didtext = DID_2.Text;
                        Fidtext = FID_2.Text;
                        Vidtext = VID_2.Value;
                        break;
                }
                eax = ((IddDiv & 0xFF) << 30) | ((IddVal & 0xFF) << 22) | ((CpuVid & 0xFF) << 14) |
                      ((uint.Parse(Didtext) & 0xFF) << 8) | (uint.Parse(Fidtext) & 0xFF);
                if (NUMAUtil.HighestNumaNode > 0)
                {
                    for (var i = 0; i <= 2; i++)
                    {
                        if (!WritePstateClick(pstateId, eax, edx, i))
                        {
                            return;
                        }
                    }
                }
                else
                {
                    if (!WritePstateClick(pstateId, eax, edx))
                    {
                        return;
                    }
                }
                if (!WritePstateClick(pstateId, eax, edx)) { return; }
                if (cpu?.WriteMsr(Convert.ToUInt32(Convert.ToInt64(0xC0010064) + pstateId), eax, edx) == false) { MessageBox.Show("Error writing PState! ID = " + pstateId); }
                //if (!cpu.WriteMsrWn(Convert.ToUInt32(Convert.ToInt64(0xC0010064) + pstateId), eax, edx)) { MessageBox.Show("Error writing PState! ID = " + pstateId); }
                equalvid = Math.Round((1.55 - Vidtext / 1000) / 0.00625).ToString();
                var f = new Process();
                f.StartInfo.UseShellExecute = false;
                f.StartInfo.FileName = @"ryzenps.exe";
                f.StartInfo.Arguments = "-p=" + p + " -v=" + equalvid;
                f.StartInfo.CreateNoWindow = true;
                f.StartInfo.RedirectStandardError = true;
                f.StartInfo.RedirectStandardInput = true;
                f.StartInfo.RedirectStandardOutput = true;
                f.Start();
                f.WaitForExit();
            }
            ReadPstate();
        }
        catch
        {
            // ignored
        }
    }
    public void WritePstatesWithoutP0()
    {
        try
        {
            for (var p = 1; p < 3; p++)
            {
                if (string.IsNullOrEmpty(DID_1.Text) || string.IsNullOrEmpty(FID_1.Text) || string.IsNullOrEmpty(DID_2.Text) || string.IsNullOrEmpty(FID_2.Text)) { ReadPstate(); }
                //Logic
                var pstateId = p;
                uint eax = default, edx = default;
                uint IddDiv = 0x0;
                uint IddVal = 0x0;
                uint CpuVid = 0x0;
                uint CpuDfsId = 0x0;
                uint CpuFid = 0x0;
                var Didtext = "12";
                var Fidtext = "102";
                if (cpu?.ReadMsr(Convert.ToUInt32(Convert.ToInt64(0xC0010064) + pstateId), ref eax, ref edx) == false)
                {
                    MessageBox.Show("Error reading PState! ID = " + pstateId);
                    return;
                }
                CalculatePstateDetails(eax, ref IddDiv, ref IddVal, ref CpuVid, ref CpuDfsId, ref CpuFid);
                switch (p)
                {
                    case 1:
                        Didtext = DID_1.Text;
                        Fidtext = FID_1.Text;
                        break;
                    case 2:
                        Didtext = DID_2.Text;
                        Fidtext = FID_2.Text;
                        break;
                }

                eax = ((IddDiv & 0xFF) << 30) | ((IddVal & 0xFF) << 22) | ((CpuVid & 0xFF) << 14) |
                      ((uint.Parse(Didtext) & 0xFF) << 8) | (uint.Parse(Fidtext) & 0xFF);
                if (NUMAUtil.HighestNumaNode > 0)
                {
                    for (var i = 0; i <= 2; i++)
                    {
                        if (!WritePstateClick(pstateId, eax, edx, i))
                        {
                            return;
                        }
                    }
                }
                else
                {
                    if (!WritePstateClick(pstateId, eax, edx))
                    {
                        return;
                    }
                }
                if (!WritePstateClick(pstateId, eax, edx))
                {
                    return;
                }
                if (cpu?.WriteMsr(Convert.ToUInt32(Convert.ToInt64(0xC0010064) + pstateId), eax, edx) == false)
                {
                    MessageBox.Show("Error writing PState! ID = " + pstateId);
                }
              /*  if (!cpu.WriteMsrWn(Convert.ToUInt32(Convert.ToInt64(0xC0010064) + pstateId), eax, edx))
                {
                    MessageBox.Show("Error writing PState! ID = " + pstateId);
                }*/
            }
            ReadPstate();
        }
        catch
        {
            // ignored
        }
    }
    public static void CalculatePstateDetails(uint eax, ref uint IddDiv, ref uint IddVal, ref uint CpuVid, ref uint CpuDfsId, ref uint CpuFid)
    {
        IddDiv = eax >> 30;
        IddVal = (eax >> 22) & 0xFF;
        CpuVid = (eax >> 14) & 0xFF;
        CpuDfsId = (eax >> 8) & 0x3F;
        CpuFid = eax & 0xFF;
    }  
    public bool ApplyTscWorkaround()
    { // P0 fix C001_0015 HWCR[21]=1
      // Fixes timer issues when not using HPET
        uint eax = 0, edx = 0;
        if (cpu?.ReadMsr(0xC0010015, ref eax, ref edx) == true)
        {
            eax |= 0x200000;
            return cpu.WriteMsr(0xC0010015, eax, edx);
           // return cpu.WriteMsrWn(0xC0010015, eax, edx);
        }
        MessageBox.Show("Error applying TSC fix!");
        return false;
    }
    private bool WritePstateClick(int pstateId, uint eax, uint edx, int numanode = 0)
    {
        try
        {
            if (NUMAUtil.HighestNumaNode > 0)
            {
                NUMAUtil.SetThreadProcessorAffinity((ushort)(numanode + 1),
                Enumerable.Range(0, Environment.ProcessorCount).ToArray());
            }
            if (!ApplyTscWorkaround()) { return false; }
            if (cpu?.WriteMsr(Convert.ToUInt32(Convert.ToInt64(0xC0010064) + pstateId), eax, edx) == false) { MessageBox.Show("Error writing PState! ID = " + pstateId); return false; }
          //  if (!cpu.WriteMsrWn(Convert.ToUInt32(Convert.ToInt64(0xC0010064) + pstateId), eax, edx)) { MessageBox.Show("Error writing PState! ID = " + pstateId); return false; }
            return true;
        }
        catch { return false; }
    }
    private void ReadPstate()
    {
        try
        {
            for (var i = 0; i < 3; i++)
            {
                uint eax = default, edx = default;
                var pstateId = i;
                try
                {
                    if (cpu?.ReadMsr(Convert.ToUInt32(Convert.ToInt64(0xC0010064) + pstateId), ref eax, ref edx) == false)
                    {
                        App.MainWindow.ShowMessageDialogAsync("Error while reading CPU Pstate", "Critical Error");
                        return;
                    }
                }
                catch
                {
                    // ignored
                } 
                uint IddDiv = 0x0;
                uint IddVal = 0x0;
                uint CpuVid = 0x0;
                uint CpuDfsId = 0x0;
                uint CpuFid = 0x0;
                CalculatePstateDetails(eax, ref IddDiv, ref IddVal, ref CpuVid, ref CpuDfsId, ref CpuFid);
                switch (i)
                {
                    case 0:
                        DID_0.Text = Convert.ToString(CpuDfsId, 10);
                        FID_0.Text = Convert.ToString(CpuFid, 10);
                        P0_Freq.Content = CpuFid * 25 / (CpuDfsId * 12.5) * 100;
                        int Mult_0_v;
                        Mult_0_v = (int)(CpuFid * 25 / (CpuDfsId * 12.5));
                        Mult_0_v -= 4;
                        if (Mult_0_v <= 0)
                        {
                            Mult_0_v = 0;
                            App.MainWindow.ShowMessageDialogAsync("Error while reading CPU multiply", "Critical Error");
                        }
                        Mult_0.SelectedIndex = Mult_0_v;
                        break;
                    case 1:
                        DID_1.Text = Convert.ToString(CpuDfsId, 10);
                        FID_1.Text = Convert.ToString(CpuFid, 10);
                        P1_Freq.Content = CpuFid * 25 / (CpuDfsId * 12.5) * 100;
                        int Mult_1_v;
                        Mult_1_v = (int)(CpuFid * 25 / (CpuDfsId * 12.5));
                        Mult_1_v -= 4;
                        if (Mult_1_v <= 0)
                        {
                            Mult_1_v = 0;
                            App.MainWindow.ShowMessageDialogAsync("Error while reading CPU multiply", "Critical Error");
                        }
                        Mult_1.SelectedIndex = Mult_1_v;
                        break;
                    case 2:
                        DID_2.Text = Convert.ToString(CpuDfsId, 10);
                        FID_2.Text = Convert.ToString(CpuFid, 10);
                        P2_Freq.Content = CpuFid * 25 / (CpuDfsId * 12.5) * 100;
                        int Mult_2_v;
                        Mult_2_v = (int)(CpuFid * 25 / (CpuDfsId * 12.5));
                        Mult_2_v -= 4;
                        if (Mult_2_v <= 0)
                        {
                            Mult_2_v = 0;
                            App.MainWindow.ShowMessageDialogAsync("Error while reading CPU multiply", "Critical Error");
                        }
                        Mult_2.SelectedIndex = Mult_2_v;
                        break;
                }
            }
        }
        catch
        {
            // ignored
        }
    } 
    //Pstates section 
    private void EnablePstates_Click(object sender, RoutedEventArgs e)
    {
        if (EnablePstates.IsOn) { EnablePstates.IsOn = false; } else { EnablePstates.IsOn = true; }
        EnablePstatess();
    }
    private void TurboBoost_Click(object sender, RoutedEventArgs e)
    {
        if (Turbo_boost.IsEnabled) { if (Turbo_boost.IsOn) { Turbo_boost.IsOn = false; } else { Turbo_boost.IsOn = true; } }
        TurboBoost();
    }
    private void Autoapply_Click(object sender, RoutedEventArgs e)
    {
        if (Autoapply_1.IsOn) { Autoapply_1.IsOn = false; } else { Autoapply_1.IsOn = true; }
        Autoapply();
    }
    private void WithoutP0_Click(object sender, RoutedEventArgs e)
    {
        if (Without_P0.IsOn) { Without_P0.IsOn = false; } else { Without_P0.IsOn = true; }
        WithoutP0();
    }
    private void IgnoreWarn_Click(object sender, RoutedEventArgs e)
    {
        if (IgnoreWarn.IsOn) { IgnoreWarn.IsOn = false; } else { IgnoreWarn.IsOn = true; }
        IgnoreWarning();
    }
    //Enable or disable pstate toggleswitches...
    private void EnablePstatess()
    {
        if (EnablePstates.IsOn)
        {
            devices.enableps = true;
            DeviceSave();
            profile[indexprofile].enablePstateEditor = true;
            ProfileSave();
        }
        else
        {
            devices.enableps = false;
            DeviceSave();
            profile[indexprofile].enablePstateEditor = false;
            ProfileSave();
        }
    }
    private void TurboBoost()
    {
        Turboo_Boost(); //Турбобуст... 
        if (Turbo_boost.IsOn) //Сохранение
        {
            turbobboost = true;
            devices.turboboost = true;
            DeviceSave();
            profile[indexprofile].turboBoost = true;
            ProfileSave();
        }
        else
        {
            turbobboost = false;
            devices.turboboost = false;
            DeviceSave();
            profile[indexprofile].turboBoost = false;
            ProfileSave();
        }
    }
    public void Turboo_Boost()
    {
        if (Turbo_boost.IsOn) { SetActive(); Enable(); } else { SetActive(); Disable(); }
        void Enable()
        {
            var p = new Process(); //AC
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.FileName = "powercfg.exe";
            p.StartInfo.Arguments =
                "/SETACVALUEINDEX 381b4222-f694-41f0-9685-ff5bb260df2e 54533251-82be-4824-96c1-47b60b740d00 be337238-0d82-4146-a960-4f3749d470c7 002";
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.Start();
            var p1 = new Process(); //DC
            p1.StartInfo.UseShellExecute = false;
            p1.StartInfo.FileName = "powercfg.exe";
            p1.StartInfo.Arguments =
                "/SETDCVALUEINDEX 381b4222-f694-41f0-9685-ff5bb260df2e 54533251-82be-4824-96c1-47b60b740d00 be337238-0d82-4146-a960-4f3749d470c7 002";
            p1.StartInfo.CreateNoWindow = true;
            p1.StartInfo.RedirectStandardError = true;
            p1.StartInfo.RedirectStandardInput = true;
            p1.StartInfo.RedirectStandardOutput = true;
            p1.Start();
        }
        void Disable()
        {
            var p = new Process(); //AC
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.FileName = "powercfg.exe";
            p.StartInfo.Arguments =
                "/SETACVALUEINDEX 381b4222-f694-41f0-9685-ff5bb260df2e 54533251-82be-4824-96c1-47b60b740d00 be337238-0d82-4146-a960-4f3749d470c7 000";
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.Start();
            var p1 = new Process(); //DC
            p1.StartInfo.UseShellExecute = false;
            p1.StartInfo.FileName = "powercfg.exe";
            p1.StartInfo.Arguments =
                "/SETDCVALUEINDEX 381b4222-f694-41f0-9685-ff5bb260df2e 54533251-82be-4824-96c1-47b60b740d00 be337238-0d82-4146-a960-4f3749d470c7 000";
            p1.StartInfo.CreateNoWindow = true;
            p1.StartInfo.RedirectStandardError = true;
            p1.StartInfo.RedirectStandardInput = true;
            p1.StartInfo.RedirectStandardOutput = true;
            p1.Start();
        }
        void SetActive()
        {
            var p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.FileName = "powercfg.exe";
            p.StartInfo.Arguments = "/s 381b4222-f694-41f0-9685-ff5bb260df2e";
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.Start();
        }
    }
    private void Autoapply()
    {
        if (Autoapply_1.IsOn)
        {
            devices.autopstate = true;
            DeviceSave();
            profile[indexprofile].autoPstate = true;
            ProfileSave();
        }
        else
        {
            devices.autopstate = false;
            DeviceSave();
            profile[indexprofile].autoPstate = false;
            ProfileSave();
        }
    }
    private void WithoutP0()
    {
        if (Without_P0.IsOn)
        {
            devices.p0ignorewarn = true;
            DeviceSave();
            profile[indexprofile].p0Ignorewarn = true;
            ProfileSave();
        }
        else
        {
            devices.p0ignorewarn = false;
            DeviceSave();
            profile[indexprofile].p0Ignorewarn = false;
            ProfileSave();
        }
    }
    private void IgnoreWarning()
    {
        if (IgnoreWarn.IsOn)
        {
            devices.ignorewarn = true;
            DeviceSave();
            profile[indexprofile].ignoreWarn = true;
            ProfileSave();
        }
        else
        {
            devices.ignorewarn = false;
            DeviceSave();
            profile[indexprofile].ignoreWarn = false;
            ProfileSave();
        }
    }
    //Toggleswitches pstate
    private void EnablePstates_Toggled(object sender, RoutedEventArgs e) => EnablePstatess();
    private void Without_P0_Toggled(object sender, RoutedEventArgs e) => WithoutP0();
    private void Autoapply_1_Toggled(object sender, RoutedEventArgs e) => Autoapply();
    private void Turbo_boost_Toggled(object sender, RoutedEventArgs e) => TurboBoost();
    private void Ignore_Toggled(object sender, RoutedEventArgs e) => IgnoreWarning();
    //Autochanging values
    private async void FID_0_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (waitforload == false)
        {
            if (relay == false)
            {
                await Task.Delay(20);
                double Mult_0_v;
                var Did_value = DID_0.Value;
                var Fid_value = FID_0.Value;
                try
                {
                    Mult_0_v = Fid_value / Did_value * 2;
                    if (Fid_value / Did_value % 2 == 5) { Mult_0_v -= 3; } else { Mult_0_v -= 4; }
                    if (Mult_0_v <= 0) { Mult_0_v = 0; }
                    P0_Freq.Content = (Mult_0_v + 4) * 100;
                    Mult_0.SelectedIndex = (int)Mult_0_v;
                }
                catch { }
            }
            else { relay = false; }
            Save_ID0();
        }
    }
    private async void Mult_0_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (waitforload == false)
        {
            await Task.Delay(20);
            double Fid_value;
            var Did_value = DID_0.Value;
            if (DID_0.Text != "" || DID_0.Text != null)
            {
                waitforload = true;
                Fid_value = (Mult_0.SelectedIndex + 4) * Did_value / 2;
                relay = true;
                FID_0.Value = Fid_value;
                await Task.Delay(40);
                FID_0.Value = Fid_value;
                P0_Freq.Content = (Mult_0.SelectedIndex + 4) * 100;
                Save_ID0();
                await Task.Delay(40);
                waitforload = false;
            }
        }
    }
    private async void DID_0_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (waitforload == false)
        {
            await Task.Delay(20);
            double Mult_0_v;
            var Did_value = DID_0.Value;
            var Fid_value = FID_0.Value;
            Mult_0_v = Fid_value / Did_value * 2;
            if (Fid_value / Did_value % 2 == 5)
            {
                Mult_0_v -= 3;
            }
            else
            {
                Mult_0_v -= 4;
            }
            if (Mult_0_v <= 0)
            {
                Mult_0_v = 0;
            }
            P2_Freq.Content = (Mult_0_v + 4) * 100;
            try
            {
                Mult_0.SelectedIndex = (int)Mult_0_v;
            }
            catch { }
            Save_ID0();
        }
    }
    private async void FID_1_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (waitforload == false)
        {
            if (relay == false)
            {
                await Task.Delay(20);
                double Mult_1_v;
                var Did_value = DID_1.Value;
                var Fid_value = FID_1.Value;
                try
                {
                    Mult_1_v = Fid_value / Did_value * 2;
                    if (Fid_value / Did_value % 2 == 5) { Mult_1_v -= 3; }
                    else { Mult_1_v -= 4; }
                    if (Mult_1_v <= 0) { Mult_1_v = 0; }
                    P1_Freq.Content = (Mult_1_v + 4) * 100;
                    Mult_1.SelectedIndex = (int)Mult_1_v;
                }
                catch { }
            }
            else
            {
                relay = false;
            }
            Save_ID1();
        }
    }
    private async void Mult_1_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (waitforload == false)
        {
            await Task.Delay(20);
            double Fid_value;
            var Did_value = DID_1.Value;
            if (DID_1.Text != "" || DID_1.Text != null)
            {
                waitforload = true;
                Fid_value = (Mult_1.SelectedIndex + 4) * Did_value / 2;
                relay = true;
                FID_1.Value = Fid_value;
                await Task.Delay(40);
                FID_1.Value = Fid_value;
                P1_Freq.Content = (Mult_1.SelectedIndex + 4) * 100;
                Save_ID1();
                waitforload = false;
            }
        }
    }
    private async void DID_1_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (waitforload == false)
        {
            await Task.Delay(20);
            double Mult_2_v;
            var Did_value = DID_1.Value;
            var Fid_value = FID_1.Value;
            Mult_2_v = Fid_value / Did_value * 2;
            if (Fid_value / Did_value % 2 == 5)
            {
                Mult_2_v -= 3;
            }
            else
            {
                Mult_2_v -= 4;
            }
            if (Mult_2_v <= 0)
            {
                Mult_2_v = 0;
            }
            P1_Freq.Content = (Mult_2_v + 4) * 100;
            try
            {
                Mult_1.SelectedIndex = (int)Mult_2_v;
            }
            catch { }
            Save_ID1();
        }
    }
    private async void Mult_2_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (waitforload) { return; }
        await Task.Delay(20);
        double Fid_value;
        var Did_value = DID_2.Value;
        if (DID_2.Text != "" || DID_2.Text != null)
        {
            waitforload = true;
            Fid_value = (Mult_2.SelectedIndex + 4) * Did_value / 2;
            relay = true;
            FID_2.Value = Fid_value;
            await Task.Delay(40);
            FID_2.Value = Fid_value;
            P2_Freq.Content = (Mult_2.SelectedIndex + 4) * 100;
            Save_ID2();
            waitforload = false;
        }
    }
    private async void FID_2_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (waitforload == false)
        {
            if (relay == false)
            {
                await Task.Delay(20);
                double Mult_2_v;
                var Did_value = DID_2.Value;
                var Fid_value = FID_2.Value;
                try
                {
                    Mult_2_v = Fid_value / Did_value * 2;
                    if (Fid_value / Did_value % 2 == 5) { Mult_2_v -= 3; } else { Mult_2_v -= 4; }
                    if (Mult_2_v <= 0) { Mult_2_v = 0; }
                    P2_Freq.Content = (Mult_2_v + 4) * 100;
                    Mult_2.SelectedIndex = (int)Mult_2_v;
                }
                catch { }
            }
            else { relay = false; }
            Save_ID2();
        }
    }
    private async void DID_2_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (waitforload == false)
        {
            await Task.Delay(40);
            double Mult_2_v; var Did_value = DID_2.Value; var Fid_value = FID_2.Value;
            Mult_2_v = Fid_value / Did_value * 2; Mult_2_v -= 4;
            if (Mult_2_v <= 0) { Mult_2_v = 0; }
            P2_Freq.Content = (Mult_2_v + 4) * 100;
            try { Mult_2.SelectedIndex = (int)Mult_2_v; } catch { }
            Save_ID2();
        }
    }
    public void Save_ID0()
    {
        if (waitforload == false)
        {
            devices.did0 = DID_0.Value;
            devices.fid0 = FID_0.Value;
            devices.vid0 = VID_0.Value;
            DeviceSave();
            profile[indexprofile].did0 = DID_0.Value;
            profile[indexprofile].fid0 = FID_0.Value;
            profile[indexprofile].vid0 = VID_0.Value;
            ProfileSave();
        }
    }
    public void Save_ID1()
    {
        if (waitforload == false)
        {
            devices.did1 = DID_1.Value;
            devices.fid1 = FID_1.Value;
            devices.vid1 = VID_1.Value;
            DeviceSave();
            profile[indexprofile].did1 = DID_1.Value;
            profile[indexprofile].fid1 = FID_1.Value;
            profile[indexprofile].vid1 = VID_1.Value;
            ProfileSave();
        }
    }
    public void Save_ID2()
    {
        if (waitforload == false)
        {
            devices.did2 = DID_2.Value;
            devices.fid2 = FID_2.Value;
            devices.vid2 = VID_2.Value;
            DeviceSave();
            profile[indexprofile].did2 = DID_2.Value;
            profile[indexprofile].fid2 = FID_2.Value;
            profile[indexprofile].vid2 = VID_2.Value;
            ProfileSave();
        }
    }
    private void VID_0_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => Save_ID0();
    private void VID_1_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => Save_ID1();
    private void VID_2_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => Save_ID2();
    #endregion 
}