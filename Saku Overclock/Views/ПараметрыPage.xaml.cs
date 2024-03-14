using System.ComponentModel;
using System.Diagnostics;
using System.Management;
using System.Windows.Forms;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Services;
using Saku_Overclock.ViewModels;
using Windows.Foundation.Metadata;

namespace Saku_Overclock.Views;
#pragma warning disable CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.
#pragma warning restore CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.
#pragma warning disable CS8601 // Возможно, назначение-ссылка, допускающее значение NULL.
#pragma warning disable CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.

public sealed partial class ПараметрыPage : Microsoft.UI.Xaml.Controls.Page
{
    public ПараметрыViewModel ViewModel
    {
        get;
    }

    private List<SmuAddressSet> matches;
    private Config config = new();
    private Devices devices = new();
    private Profile[] profile = new Profile[1];
    private int indexprofile = 0;
    private readonly NUMAUtil _numaUtil;
    private bool isLoaded = false;
    private bool relay = false;
    private readonly Services.Cpu cpu;
    public bool turbobboost = true;
    private bool waitforload = true;
    private readonly string wmiAMDACPI = "AMD_ACPI";
    private readonly string wmiScope = "root\\wmi";
    private ManagementObject classInstance;
    private string instanceName;
    private ManagementBaseObject pack;
    private const string filename = "co_profile.txt";
    private const string profilesFolderName = "profiles";
    private const string defaultsPath = profilesFolderName + @"\" + filename;
    private readonly string[] args;
    private readonly bool isApplyProfile;
    public string adjline;
    public string ocmode;
    private readonly Services.Mailbox testMailbox = new();
    public string universalvid;
    public string equalvid;
    public ПараметрыPage()
    {
        ViewModel = App.GetService<ПараметрыViewModel>();
        InitializeComponent();
        OC_Detect();
        DeviceLoad();
        ConfigLoad();
        ProfileLoad();
        indexprofile = config.Preset;
        config.fanex = false;
        config.tempex = false;
        ConfigSave();
        try
        {
            _numaUtil = new NUMAUtil();
        }
        catch
        {
            App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
        }
        try
        {
            args = Environment.GetCommandLineArgs();
            foreach (var arg in args)
            {
                isApplyProfile |= arg.ToLower() == "--applyprofile";
            }
            cpu = new Services.Cpu();
            Services.Cpu.Cpu_Init();
        }
        catch
        {
            App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
        }
        Loaded += ПараметрыPage_Loaded;
    }

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
        l.Add(new MailboxListItem("RSMU", cpu.smu.Rsmu));
        l.Add(new MailboxListItem("MP1", cpu.smu.Mp1Smu));
        l.Add(new MailboxListItem("HSMP", cpu.smu.Hsmp));
    }
    private void AddMailboxToList(string label, SmuAddressSet addressSet)
    {
        comboBoxMailboxSelect.Items.Add(new MailboxListItem(label, addressSet));
    }
    private async void SmuScan_WorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
        var index = comboBoxMailboxSelect.SelectedIndex;
        PopulateMailboxesList(comboBoxMailboxSelect.Items);

        for (var i = 0; i < matches.Count; i++)
        {
            AddMailboxToList($"Mailbox {i + 1}", matches[i]);
        }

        if (index > comboBoxMailboxSelect.Items.Count)
        {
            index = 0;
        }
        comboBoxMailboxSelect.SelectedIndex = index;
        await Send_Message("SMUScanText".GetLocalized(), "SMUScanDesc".GetLocalized(), Symbol.Message);
    }

    [Obsolete]
    private void BackgroundWorkerTrySettings_DoWork(object sender, DoWorkEventArgs e)
    {
        try
        {
            switch (cpu.info.codeName)
            {
                case Cpu.CodeName.BristolRidge:
                    //ScanSmuRange(0x13000000, 0x13000F00, 4, 0x10);
                    break;
                case Cpu.CodeName.RavenRidge:
                case Cpu.CodeName.Picasso:
                case Cpu.CodeName.FireFlight:
                case Cpu.CodeName.Dali:
                case Cpu.CodeName.Renoir:
                    ScanSmuRange(0x03B10500, 0x03B10998, 8, 0x3C);
                    ScanSmuRange(0x03B10A00, 0x03B10AFF, 4, 0x60);
                    break;
                case Cpu.CodeName.PinnacleRidge:
                case Cpu.CodeName.SummitRidge:
                case Cpu.CodeName.Matisse:
                case Cpu.CodeName.Whitehaven:
                case Cpu.CodeName.Naples:
                case Cpu.CodeName.Colfax:
                case Cpu.CodeName.Vermeer:
                    //case Cpu.CodeName.Raphael:
                    ScanSmuRange(0x03B10500, 0x03B10998, 8, 0x3C);
                    ScanSmuRange(0x03B10500, 0x03B10AFF, 4, 0x4C);
                    break;
                case Cpu.CodeName.Raphael:
                    ScanSmuRange(0x03B10500, 0x03B10998, 8, 0x3C);
                    // ScanSmuRange(0x03B10500, 0x03B10AFF, 4, 0x4C);
                    break;
                case Cpu.CodeName.Rome:
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

    [Obsolete]
    private void ScanSmuRange(uint start, uint end, uint step, uint offset)
    {
        matches = new List<SmuAddressSet>();

        var temp = new List<KeyValuePair<uint, uint>>();

        while (start <= end)
        {
            var smuRspAddress = start + offset;

            if (cpu.ReadDword(start) != 0xFFFFFFFF)
            {
                // Send unknown command 0xFF to each pair of this start and possible response addresses
                if (cpu.WriteDwordEx(start, 0xFF))
                {
                    Thread.Sleep(10);

                    while (smuRspAddress <= end)
                    {
                        // Expect UNKNOWN_CMD status to be returned if the mailbox works
                        if (cpu.ReadDword(smuRspAddress) == 0xFE)
                        {
                            // Send Get_SMU_Version command
                            if (cpu.WriteDwordEx(start, 0x2))
                            {
                                Thread.Sleep(10);
                                if (cpu.ReadDword(smuRspAddress) == 0x1)
                                    temp.Add(new KeyValuePair<uint, uint>(start, smuRspAddress));
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
            for (var i = 0; i < temp.Count; i++)
            {
                Console.WriteLine($"{temp[i].Key:X8}: {temp[i].Value:X8}");
            }

            Console.WriteLine();
        }

        var possibleArgAddresses = new List<uint>();

        foreach (var pair in temp)
        {
            Console.WriteLine($"Testing {pair.Key:X8}: {pair.Value:X8}");

            if (TrySettings(pair.Key, pair.Value, 0xFFFFFFFF, 0x2, 0xFF) == SMU.Status.OK)
            {
                var smuArgAddress = pair.Value + 4;
                while (smuArgAddress <= end)
                {
                    if (cpu.ReadDword(smuArgAddress) == cpu.smu.Version)
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
                    if (TrySettings(pair.Key, pair.Value, address, 0x1, testArg) == SMU.Status.OK)
                        if (cpu.ReadDword(address) != testArg + 1)
                            retries = -1;
                }

                if (retries == 0)
                {
                    matches.Add(new SmuAddressSet(pair.Key, pair.Value, address));
                    break;
                }
            }
        }
    }

    [Obsolete]
    private SMU.Status TrySettings(uint msgAddr, uint rspAddr, uint argAddr, uint cmd, uint value)
    {
        var args = new uint[6];
        args[0] = value;

        testMailbox.SMU_ADDR_MSG = msgAddr;
        testMailbox.SMU_ADDR_RSP = rspAddr;
        testMailbox.SMU_ADDR_ARG = argAddr;

        return cpu.smu.SendSmuCommand(testMailbox, cmd, ref args);
    }
    private void ResetSmuAddresses()
    {
        textBoxCMDAddress.Text = $@"0x{Convert.ToString(testMailbox.SMU_ADDR_MSG, 16).ToUpper()}";
        textBoxRSPAddress.Text = $@"0x{Convert.ToString(testMailbox.SMU_ADDR_RSP, 16).ToUpper()}";
        textBoxARGAddress.Text = $@"0x{Convert.ToString(testMailbox.SMU_ADDR_ARG, 16).ToUpper()}";
    }
    private async void OC_Detect() // На неподдерживаемом оборудовании мгновенно отключит эти настройки
    {
        try
        {
            await Init_OC_Mode();
            if (ocmode == "set_enable_oc is not supported on this family")
            {
                OC_Advanced.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            }
        }
        catch
        {
            OC_Advanced.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            await App.MainWindow.ShowMessageDialogAsync("App can't detect Ryzen CPU model!", "Can't detect!");
        }
    }
    //JSON форматирование
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
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json", JsonConvert.SerializeObject(profile, Formatting.Indented));
        }
        catch { }
    }

    public void ProfileLoad()
    {
        try
        {

            profile = JsonConvert.DeserializeObject<Profile[]>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json"));
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
                    Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json", JsonConvert.SerializeObject(config));
                }
                catch
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json");
                    Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
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
                    Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
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
                    Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\devices.json", JsonConvert.SerializeObject(devices));
                }
                catch
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\devices.json");
                    Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
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
                    Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
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
                    Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json", JsonConvert.SerializeObject(profile));
                }
                catch
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json");
                    Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
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
                    Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
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
        waitforload = true;
        ConfigLoad();
        if (config.Preset == -1 || index == -1) //Load from unsaved
        {
            DeviceLoad();
            c1.IsChecked = devices.c1; c1v.Value = devices.c1v; c2.IsChecked = devices.c2; c1v.Value = devices.c2v; c3.IsChecked = devices.c3; c1v.Value = devices.c3v; c4.IsChecked = devices.c4; c1v.Value = devices.c4v; c5.IsChecked = devices.c5; c1v.Value = devices.c5v; c6.IsChecked = devices.c6; c1v.Value = devices.c6v;
            V1.IsChecked = devices.v1; V1V.Value = devices.v1v; V2.IsChecked = devices.v2; V2V.Value = devices.v2v; V3.IsChecked = devices.v3; V3V.Value = devices.v3v; V4.IsChecked = devices.v4; V4V.Value = devices.v4v; V5.IsChecked = devices.v5; V5V.Value = devices.v5v; V6.IsChecked = devices.v6; V6V.Value = devices.v6v; V7.IsChecked = devices.v7; V7V.Value = devices.v7v;
            g1.IsChecked = devices.g1; g1v.Value = devices.g1v; g2.IsChecked = devices.g2; g2v.Value = devices.g2v; g3.IsChecked = devices.g3; g3v.Value = devices.g3v; g4.IsChecked = devices.g4; g4v.Value = devices.g4v; g5.IsChecked = devices.g5; g5v.Value = devices.g5v; g6.IsChecked = devices.g6; g6v.Value = devices.g6v; g7.IsChecked = devices.g7; g7v.Value = devices.g7v; g8v.Value = devices.g8v; g8.IsChecked = devices.g8; g9v.Value = devices.g9v; g9.IsChecked = devices.g9; g10v.Value = devices.g10v; g10.IsChecked = devices.g10;
            a1.IsChecked = devices.a1; a1v.Value = devices.a1v; a2.IsChecked = devices.a2; a2v.Value = devices.a2v; a3.IsChecked = devices.a3; a3v.Value = devices.a3v; a4.IsChecked = devices.a4; a4v.Value = devices.a4v; a5.IsChecked = devices.a5; a5v.Value = devices.a5v; a6.IsChecked = devices.a6; a6v.Value = devices.a6v; a7.IsChecked = devices.a7; a7v.Value = devices.a7v; a8v.Value = devices.a8v; a8.IsChecked = devices.a8; a9v.Value = devices.a9v; a9.IsChecked = devices.a9; a10v.Value = devices.a10v; a11v.Value = devices.a11v; a11.IsChecked = devices.a11; a12v.Value = devices.a12v; a12.IsChecked = devices.a12; a13m.SelectedIndex = devices.a13v;
            EnablePstates.IsOn = devices.enableps; Turbo_boost.IsOn = devices.turboboost; Autoapply_1.IsOn = devices.autopstate; IgnoreWarn.IsOn = devices.ignorewarn; Without_P0.IsOn = devices.p0ignorewarn;
            DID_0.Value = devices.did0; DID_1.Value = devices.did1; DID_2.Value = devices.did2; FID_0.Value = devices.fid0; FID_1.Value = devices.fid1; FID_2.Value = devices.fid2; VID_0.Value = devices.vid0; VID_1.Value = devices.vid1; VID_2.Value = devices.vid2;
        }
        else
        {
            ProfileLoad();
            c1.IsChecked = profile[index].cpu1; c1v.Value = profile[index].cpu1value; c2.IsChecked = profile[index].cpu2; c2v.Value = profile[index].cpu2value; c3.IsChecked = profile[index].cpu3; c3v.Value = profile[index].cpu3value; c4.IsChecked = profile[index].cpu4; c4v.Value = profile[index].cpu4value; c5.IsChecked = profile[index].cpu5; c5v.Value = profile[index].cpu5value; c6.IsChecked = profile[index].cpu6; c6v.Value = profile[index].cpu6value;
            V1.IsChecked = profile[index].vrm1; V1V.Value = profile[index].vrm1value; V2.IsChecked = profile[index].vrm2; V2V.Value = profile[index].vrm2value; V3.IsChecked = profile[index].vrm3; V3V.Value = profile[index].vrm3value; V4.IsChecked = profile[index].vrm4; V4V.Value = profile[index].vrm4value; V5.IsChecked = profile[index].vrm5; V5V.Value = profile[index].vrm5value; V6.IsChecked = profile[index].vrm6; V6V.Value = profile[index].vrm6value; V7.IsChecked = profile[index].vrm7; V7V.Value = profile[index].vrm7value;
            g1.IsChecked = profile[index].gpu1; g1v.Value = profile[index].gpu1value; g2.IsChecked = profile[index].gpu2; g2v.Value = profile[index].gpu2value; g3.IsChecked = profile[index].gpu3; g3v.Value = profile[index].gpu3value; g4.IsChecked = profile[index].gpu4; g4v.Value = profile[index].gpu4value; g5.IsChecked = profile[index].gpu5; g5v.Value = profile[index].gpu5value; g6.IsChecked = profile[index].gpu6; g6v.Value = profile[index].gpu6value; g7.IsChecked = profile[index].gpu7; g7v.Value = profile[index].gpu7value; g8v.Value = profile[index].gpu8value; g8.IsChecked = profile[index].gpu8; g9v.Value = profile[index].gpu9value; g9.IsChecked = profile[index].gpu9; g10v.Value = profile[index].gpu10value; g10.IsChecked = profile[index].gpu10;
            a1.IsChecked = profile[index].advncd1; a1v.Value = profile[index].advncd1value; a2.IsChecked = profile[index].advncd2; a2v.Value = profile[index].advncd2value; a3.IsChecked = profile[index].advncd3; a3v.Value = profile[index].advncd3value; a4.IsChecked = profile[index].advncd4; a4v.Value = profile[index].advncd4value; a5.IsChecked = profile[index].advncd5; a5v.Value = profile[index].advncd5value; a6.IsChecked = profile[index].advncd6; a6v.Value = profile[index].advncd6value; a7.IsChecked = profile[index].advncd7; a7v.Value = profile[index].advncd7value; a8v.Value = profile[index].advncd8value; a8.IsChecked = profile[index].advncd8; a9v.Value = profile[index].advncd9value; a9.IsChecked = profile[index].advncd9; a10v.Value = profile[index].advncd10value; a11v.Value = profile[index].advncd11value; a11.IsChecked = profile[index].advncd11; a12v.Value = profile[index].advncd12value; a12.IsChecked = profile[index].advncd12; a13m.SelectedIndex = profile[index].advncd13value;
            EnablePstates.IsOn = profile[index].enablePstateEditor; Turbo_boost.IsOn = profile[index].turboBoost; Autoapply_1.IsOn = profile[index].autoPstate; IgnoreWarn.IsOn = profile[index].ignoreWarn; Without_P0.IsOn = profile[index].p0Ignorewarn;
            DID_0.Value = profile[index].did0; DID_1.Value = profile[index].did1; DID_2.Value = profile[index].did2; FID_0.Value = profile[index].fid0; FID_1.Value = profile[index].fid1; FID_2.Value = profile[index].fid2; VID_0.Value = profile[index].vid0; VID_1.Value = profile[index].vid1; VID_2.Value = profile[index].vid2;
        }
        waitforload = false;
    }
    private async void ProfileCOM_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        ConfigLoad();
        while (isLoaded == false || waitforload == true)
        {
            await Task.Delay(100);
        }
        if (ProfileCOM.SelectedIndex != -1) { config.Preset = ProfileCOM.SelectedIndex - 1; ConfigSave(); }
        indexprofile = ProfileCOM.SelectedIndex - 1;
        MainInit(ProfileCOM.SelectedIndex - 1);
    }
    //Параметры процессора
    //Максимальная температура CPU (C)
    private void C1_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (c1.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].cpu1 = check; profile[indexprofile].cpu1value = c1v.Value; ProfileSave(); }
        devices.c1 = check; devices.c1v = c1v.Value;
        DeviceSave();
    }
    //Лимит CPU (W)
    private void C2_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (c2.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].cpu2 = check; profile[indexprofile].cpu2value = c2v.Value; ProfileSave(); }
        devices.c2 = check; devices.c2v = c2v.Value;
        DeviceSave();
    }
    //Реальный CPU (W)
    private void C3_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (c3.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].cpu3 = check; profile[indexprofile].cpu3value = c3v.Value; ProfileSave(); }
        devices.c3 = check; devices.c3v = c3v.Value;
        DeviceSave();
    }
    //Средний CPU (W)
    private void C4_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (c4.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].cpu4 = check; profile[indexprofile].cpu4value = c4v.Value; ProfileSave(); }
        devices.c4 = check; devices.c4v = c4v.Value;
        DeviceSave();
    }
    //Тик быстрого разгона (S)
    private void C5_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (c5.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].cpu5 = check; profile[indexprofile].cpu5value = c5v.Value; ProfileSave(); }
        devices.c5 = check; devices.c5v = c5v.Value;
        DeviceSave();
    }
    //Тик медленного разгона (S)
    private void C6_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (c6.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].cpu6 = check; profile[indexprofile].cpu6value = c6v.Value; ProfileSave(); }
        devices.c6 = check; devices.c6v = c6v.Value;
        DeviceSave();
    }
    //Параметры VRM
    //Максимальный ток VRM A
    private void V1_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (V1.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].vrm1 = check; profile[indexprofile].vrm1value = V1V.Value; ProfileSave(); }
        devices.v1 = check; devices.v1v = V1V.Value;
        DeviceSave();
    }
    //Лимит по току VRM A
    private void V2_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (V2.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].vrm2 = check; profile[indexprofile].vrm2value = V2V.Value; ProfileSave(); }
        devices.v2 = check; devices.v2v = V2V.Value;
        DeviceSave();
    }
    //Максимальный ток SOC A
    private void V3_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (V3.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].vrm3 = check; profile[indexprofile].vrm3value = V3V.Value; ProfileSave(); }
        devices.v3 = check; devices.v3v = V3V.Value;
        DeviceSave();
    }
    //Лимит по току SOC A
    private void V4_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (V4.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].vrm4 = check; profile[indexprofile].vrm4value = V4V.Value; ProfileSave(); }
        devices.v4 = check; devices.v4v = V4V.Value;
        DeviceSave();
    }
    //Максимальный ток PCI VDD A
    private void V5_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (V5.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].vrm5 = check; profile[indexprofile].vrm5value = V5V.Value; ProfileSave(); }
        devices.v5 = check; devices.v5v = V5V.Value;
        DeviceSave();
    }
    //Максимальный ток PCI SOC A
    private void V6_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (V6.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].vrm6 = check; profile[indexprofile].vrm6value = V6V.Value; ProfileSave(); }
        devices.v6 = check; devices.v6v = V6V.Value;
        DeviceSave();
    }
    //Отключить троттлинг на время
    private void V7_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (V7.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].vrm7 = check; profile[indexprofile].vrm7value = V7V.Value; ProfileSave(); }
        devices.v7 = check; devices.v7v = V7V.Value;
        DeviceSave();
    }

    //Параметры графики
    //Минимальная частота SOC 
    private void G1_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (g1.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].gpu1 = check; profile[indexprofile].gpu1value = g1v.Value; ProfileSave(); }
        devices.g1 = check; devices.g1v = g1v.Value;
        DeviceSave();
    }
    //Максимальная частота SOC
    private void G2_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (g2.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].gpu2 = check; profile[indexprofile].gpu2value = g2v.Value; ProfileSave(); }
        devices.g2 = check; devices.g2v = g2v.Value;
        DeviceSave();
    }
    //Минимальная частота Infinity Fabric
    private void G3_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (g3.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].gpu3 = check; profile[indexprofile].gpu3value = g3v.Value; ProfileSave(); }
        devices.g3 = check; devices.g3v = g3v.Value;
        DeviceSave();
    }
    //Максимальная частота Infinity Fabric
    private void G4_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (g4.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].gpu4 = check; profile[indexprofile].gpu4value = g4v.Value; ProfileSave(); }
        devices.g4 = check; devices.g4v = g4v.Value;
        DeviceSave();
    }
    //Минимальная частота кодека VCE
    private void G5_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (g5.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].gpu5 = check; profile[indexprofile].gpu5value = g5v.Value; ProfileSave(); }
        devices.g5 = check; devices.g5v = g5v.Value;
        DeviceSave();
    }
    //Максимальная частота кодека VCE
    private void G6_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (g6.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].gpu6 = check; profile[indexprofile].gpu6value = g6v.Value; ProfileSave(); }
        devices.g6 = check; devices.g6v = g6v.Value;
        DeviceSave();
    }
    //Минимальная частота частота Data Latch
    private void G7_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (g7.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].gpu7 = check; profile[indexprofile].gpu7value = g7v.Value; ProfileSave(); }
        devices.g7 = check; devices.g7v = g7v.Value;
        DeviceSave();
    }
    //Максимальная частота Data Latch
    private void G8_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (g8.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].gpu8 = check; profile[indexprofile].gpu8value = g8v.Value; ProfileSave(); }
        devices.g8 = check; devices.g8v = g8v.Value;
        DeviceSave();
    }
    //Минимальная частота iGpu
    private void G9_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (g9.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].gpu9 = check; profile[indexprofile].gpu9value = g9v.Value; ProfileSave(); }
        devices.g9 = check; devices.g9v = g9v.Value;
        DeviceSave();
    }
    //Максимальная частота iGpu
    private void G10_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (g10.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].gpu10 = check; profile[indexprofile].gpu10value = g10v.Value; ProfileSave(); }
        devices.g10 = check; devices.g10v = g10v.Value;
        DeviceSave();
    }
    //Расширенные параметры
    private void A1_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (a1.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].advncd1 = check; profile[indexprofile].advncd1value = a1v.Value; ProfileSave(); }
        devices.a1 = check; devices.a1v = a1v.Value;
        DeviceSave();
    }
    private void A2_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (a2.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].advncd2 = check; profile[indexprofile].advncd2value = a2v.Value; ProfileSave(); }
        devices.a2 = check; devices.a2v = a2v.Value;
        DeviceSave();
    }
    private void A3_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (a3.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].advncd3 = check; profile[indexprofile].advncd3value = a3v.Value; ProfileSave(); }
        devices.a3 = check; devices.a3v = a3v.Value;
        DeviceSave();
    }
    private void A4_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (a4.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].advncd4 = check; profile[indexprofile].advncd4value = a4v.Value; ProfileSave(); }
        devices.a4 = check; devices.a4v = a4v.Value;
        DeviceSave();
    }
    private void A5_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (a5.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].advncd5 = check; profile[indexprofile].advncd5value = a5v.Value; ProfileSave(); }
        devices.a5 = check; devices.a5v = a5v.Value;
        DeviceSave();
    }
    private void A6_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (a6.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].advncd6 = check; profile[indexprofile].advncd6value = a6v.Value; ProfileSave(); }
        devices.a6 = check; devices.a6v = a6v.Value;
        DeviceSave();
    }
    private void A7_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (a7.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].advncd7 = check; profile[indexprofile].advncd7value = a7v.Value; ProfileSave(); }
        devices.a7 = check; devices.a7v = a7v.Value;
        DeviceSave();
    }
    private void A8_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (a8.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].advncd8 = check; profile[indexprofile].advncd8value = a8v.Value; ProfileSave(); }
        devices.a8 = check; devices.a8v = a8v.Value;
        DeviceSave();
    }
    private void A9_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (a9.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].advncd9 = check; profile[indexprofile].advncd9value = a9v.Value; ProfileSave(); }
        devices.a9 = check; devices.a9v = a9v.Value;
        DeviceSave();
    }
    private void A10_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (a10.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].advncd10 = check; profile[indexprofile].advncd10value = a10v.Value; ProfileSave(); }
        devices.a10 = check; devices.a10v = a10v.Value;
        DeviceSave();
    }
    private void A11_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (a11.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].advncd11 = check; profile[indexprofile].advncd11value = a11v.Value; ProfileSave(); }
        devices.a11 = check; devices.a11v = a11v.Value;
        DeviceSave();
    }
    private void A12_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (a12.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].advncd12 = check; profile[indexprofile].advncd12value = a12v.Value; ProfileSave(); }
        devices.a12 = check; devices.a12v = a12v.Value;
        DeviceSave();
    }
    private void A13_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        ProfileLoad(); DeviceLoad();
        var check = false;
        if (a13.IsChecked == true) { check = true; }
        if (indexprofile != -1) { profile[indexprofile].advncd13 = check; profile[indexprofile].advncd1value = a13m.SelectedIndex; ProfileSave(); }
        devices.a13 = check; devices.a13v = a13m.SelectedIndex;
        DeviceSave();
    }
    //Параметры процессора, при изменении слайдеров
    //Максимальная температура CPU (C)
    private void C1_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.c1v = c1v.Value;
        if (indexprofile != -1) { profile[indexprofile].cpu1value = c1v.Value; ProfileSave(); }
        DeviceSave();
    }
    //Лимит CPU (W)
    private void C2_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.c2v = c2v.Value;
        if (indexprofile != -1) { profile[indexprofile].cpu2value = c2v.Value; ProfileSave(); }
        DeviceSave();
    }
    //Реальный CPU (W)
    private void C3_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.c3v = c3v.Value;
        if (indexprofile != -1) { profile[indexprofile].cpu3value = c3v.Value; ProfileSave(); }
        DeviceSave();
    }
    //Средний CPU(W)
    private void C4_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.c4v = c4v.Value;
        if (indexprofile != -1) { profile[indexprofile].cpu4value = c4v.Value; ProfileSave(); }
        DeviceSave();
    }
    //Тик быстрого разгона (S)
    private void C5_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.c5v = c5v.Value;
        if (indexprofile != -1) { profile[indexprofile].cpu5value = c5v.Value; ProfileSave(); }
        DeviceSave();
    }
    //Тик медленного разгона (S)
    private void C6_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.c6v = c6v.Value;
        if (indexprofile != -1) { profile[indexprofile].cpu6value = c6v.Value; ProfileSave(); }
        DeviceSave();
    }
    //Параметры VRM
    private void V1v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.v1v = V1V.Value;
        if (indexprofile != -1) { profile[indexprofile].vrm1value = V1V.Value; ProfileSave(); }
        DeviceSave();
    }
    private void V2v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.v2v = V2V.Value;
        if (indexprofile != -1) { profile[indexprofile].vrm2value = V2V.Value; ProfileSave(); }
        DeviceSave();
    }
    private void V3v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.v3v = V3V.Value;
        if (indexprofile != -1) { profile[indexprofile].vrm3value = V3V.Value; ProfileSave(); }
        DeviceSave();
    }
    private void V4v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.v4v = V4V.Value;
        if (indexprofile != -1) { profile[indexprofile].vrm4value = V4V.Value; ProfileSave(); }
        DeviceSave();
    }
    private void V5v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.v5v = V5V.Value;
        if (indexprofile != -1) { profile[indexprofile].vrm5value = V5V.Value; ProfileSave(); }
        DeviceSave();
    }
    private void V6v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.v6v = V6V.Value;
        if (indexprofile != -1) { profile[indexprofile].vrm6value = V6V.Value; ProfileSave(); }
        DeviceSave();
    }
    private void V7v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.v7v = V7V.Value;
        if (indexprofile != -1) { profile[indexprofile].vrm7value = V7V.Value; ProfileSave(); }
        DeviceSave();
    }
    //Параметры GPU
    private void G1v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.g1v = g1v.Value;
        if (indexprofile != -1) { profile[indexprofile].gpu1value = g1v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void G2v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.g2v = g2v.Value;
        if (indexprofile != -1) { profile[indexprofile].gpu2value = g2v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void G3v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.g3v = g3v.Value;
        if (indexprofile != -1) { profile[indexprofile].gpu3value = g3v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void G4v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.g4v = g4v.Value;
        if (indexprofile != -1) { profile[indexprofile].gpu4value = g4v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void G5v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.g5v = g5v.Value;
        if (indexprofile != -1) { profile[indexprofile].gpu5value = g5v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void G6v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.g6v = g6v.Value;
        if (indexprofile != -1) { profile[indexprofile].gpu6value = g6v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void G7v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.g7v = g7v.Value;
        if (indexprofile != -1) { profile[indexprofile].gpu7value = g7v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void G8v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.g8v = g8v.Value;
        if (indexprofile != -1) { profile[indexprofile].gpu8value = g8v.Value; ProfileSave(); }
        DeviceSave();
    }
    private void G9v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
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
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.a1v = a1v.Value;
        if (indexprofile != -1) { profile[indexprofile].advncd1value = a1v.Value; ProfileSave(); }
        DeviceSave();
    }

    private void A2v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.a2v = a2v.Value;
        if (indexprofile != -1) { profile[indexprofile].advncd2value = a2v.Value; ProfileSave(); }
        DeviceSave();
    }

    private void A3v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.a3v = a3v.Value;
        if (indexprofile != -1) { profile[indexprofile].advncd3value = a3v.Value; ProfileSave(); }
        DeviceSave();
    }

    private void A4v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.a4v = a4v.Value;
        if (indexprofile != -1) { profile[indexprofile].advncd4value = a4v.Value; ProfileSave(); }
        DeviceSave();
    }

    private void A5v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.a5v = a5v.Value;
        if (indexprofile != -1) { profile[indexprofile].advncd5value = a5v.Value; ProfileSave(); }
        DeviceSave();
    }

    private void A6v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.a6v = a6v.Value;
        if (indexprofile != -1) { profile[indexprofile].advncd6value = a6v.Value; ProfileSave(); }
        DeviceSave();
    }

    private void A7v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.a7v = a7v.Value;
        if (indexprofile != -1) { profile[indexprofile].advncd7value = a7v.Value; ProfileSave(); }
        DeviceSave();
    }

    private void A8v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.a8v = a8v.Value;
        if (indexprofile != -1) { profile[indexprofile].advncd8value = a8v.Value; ProfileSave(); }
        DeviceSave();
    }

    private void A9v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.a9v = a9v.Value;
        if (indexprofile != -1) { profile[indexprofile].advncd9value = a9v.Value; ProfileSave(); }
        DeviceSave();
    }

    private void A10v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.a10v = a10v.Value;
        if (indexprofile != -1) { profile[indexprofile].advncd10value = a10v.Value; ProfileSave(); }
        DeviceSave();
    }

    private void A11v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.a11v = a11v.Value;
        if (indexprofile != -1) { profile[indexprofile].advncd11value = a11v.Value; ProfileSave(); }
        DeviceSave();
    }

    private void A12v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.a12v = a12v.Value;
        if (indexprofile != -1) { profile[indexprofile].advncd12value = a12v.Value; ProfileSave(); }
        DeviceSave();
    }

    private void A13m_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isLoaded == false || waitforload == true) { return; }
        DeviceLoad(); ProfileLoad();
        devices.a13v = a13m.SelectedIndex;
        if (indexprofile != -1) { profile[indexprofile].advncd13value = a13m.SelectedIndex; ProfileSave(); }
        DeviceSave();
    }

    //Кнопка применить, итоговый выход, Ryzen ADJ
    [Obsolete]
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
            adjline += " --max-gfxclk=" + g9v.Value;
        }

        if (g10.IsChecked == true)
        {
            adjline += " --min-socclk-frequency=" + g10v.Value;
        }
        if (ocmode != "set_enable_oc is not supported on this family")
        {
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
                    adjline += " --max-performance";
                }

                if (a13m.SelectedIndex == 2)
                {
                    adjline += " --power-saving";
                }
            }
        }

        config.adjline = adjline;
        adjline = "";
        ConfigSave();
        MainWindow.Applyer.Apply();
        if (EnablePstates.IsOn)
        {
            BtnPstateWrite_Click();
        }
        else
        {
            ReadPstate();
        }

        Apply_tooltip.IsOpen = true;
        await Task.Delay(3000);
        Apply_tooltip.IsOpen = false;
        if (textBoxARG0 != null && textBoxARGAddress != null && textBoxCMD != null && textBoxCMDAddress != null && textBoxRSPAddress != null) { ApplySettings(); }
    }

    private async Task Init_OC_Mode()
    {
        ocmode = "";
        var p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = @"ryzenadj.exe";
        p.StartInfo.Arguments = "--enable-oc";
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;
        p.Start();
        var outputWriter = p.StandardOutput;
        var line = outputWriter.ReadLine();
        if (line != "")
        {
            ocmode = line;
        }
        await p.WaitForExitAsync();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (SaveName.Text != "")
        {
            ConfigLoad();
            ProfileLoad();
            try
            {
                config.Preset += 1;
                indexprofile += 1;
                waitforload = true;
                ProfileCOM.Items.Add(SaveName.Text);
                ProfileCOM.SelectedItem = SaveName.Text;
                var profileList = new List<Profile>(profile)
                {
                    new()
                };
                profile = profileList.ToArray();
                waitforload = false;
                profile[indexprofile].profilename = SaveName.Text;
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
            Add_tooltip_Error.IsOpen = true;
            await Task.Delay(3000);
            Add_tooltip_Error.IsOpen = false;
        }
        ConfigSave();
        ProfileSave();
    }

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (SaveName.Text != "")
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
                profile[indexprofile].profilename = SaveName.Text;
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
                ProfileCOM.SelectedItem = SaveName.Text;

                Edit_tooltip.IsOpen = true;
                await Task.Delay(3000);
                Edit_tooltip.IsOpen = false;
            }
        }
        else
        {
            Edit_tooltip_Error.IsOpen = true;
            await Task.Delay(3000);
            Edit_tooltip_Error.IsOpen = false;
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        var DelDialog = new ContentDialog
        {
            Title = "Delete preset",
            Content = "Did you really want to delete this preset?",
            CloseButtonText = "Cancel",
            PrimaryButtonText = "Delete",
            DefaultButton = ContentDialogButton.Close
        };

        // Use this code to associate the dialog to the appropriate AppWindow by setting
        // the dialog's XamlRoot to the same XamlRoot as an element that is already present in the AppWindow.
        if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
        {
            DelDialog.XamlRoot = XamlRoot;
        }

        var result = await DelDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            if (ProfileCOM.SelectedIndex == 0)
            {
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
            }
            ProfileSave();
        }
    }

    [Obsolete]
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
                        Title = "Change Pstates?",
                        Content =
                            "Did you really want to change pstates? \nThis can crash your system immediately. \nChanging P0 state have MORE chances to crash your system",
                        CloseButtonText = "Cancel",
                        PrimaryButtonText = "Change",
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
                        Title = "Change Pstates?",
                        Content =
                            "Did you really want to change pstates? \nThis can crash your system immediately. \nChanging P0 state have MORE chances to crash your system",
                        CloseButtonText = "Cancel",
                        PrimaryButtonText = "Change",
                        SecondaryButtonText = "Without P0",
                        DefaultButton = ContentDialogButton.Close
                    };

                    // Use this code to associate the dialog to the appropriate AppWindow by setting
                    // the dialog's XamlRoot to the same XamlRoot as an element that is already present in the AppWindow.
                    if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
                    {
                        ApplyDialog.XamlRoot = XamlRoot;
                    } 
                    var result = await ApplyDialog.ShowAsync();
                    if (result == ContentDialogResult.Primary) { WritePstates(); } 
                    if (result == ContentDialogResult.Secondary) { WritePstatesWithoutP0(); }
                }
            }
        }
    } 
    [Obsolete]
    public void WritePstates()
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
            if (!cpu.ReadMsr(Convert.ToUInt32(Convert.ToInt64(0xC0010064) + pstateId), ref eax, ref edx))
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
            if (!cpu.WriteMsrWn(Convert.ToUInt32(Convert.ToInt64(0xC0010064) + pstateId), eax, edx)) { MessageBox.Show("Error writing PState! ID = " + pstateId); } 
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

    [Obsolete]
    public void WritePstatesWithoutP0()
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
            if (!cpu.ReadMsr(Convert.ToUInt32(Convert.ToInt64(0xC0010064) + pstateId), ref eax, ref edx))
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

            if (!cpu.WriteMsrWn(Convert.ToUInt32(Convert.ToInt64(0xC0010064) + pstateId), eax, edx))
            { MessageBox.Show("Error writing PState! ID = " + pstateId); }
        }

        ReadPstate();
    }

    public static void CalculatePstateDetails(uint eax, ref uint IddDiv, ref uint IddVal, ref uint CpuVid,
        ref uint CpuDfsId, ref uint CpuFid)
    {
        IddDiv = eax >> 30;
        IddVal = (eax >> 22) & 0xFF;
        CpuVid = (eax >> 14) & 0xFF;
        CpuDfsId = (eax >> 8) & 0x3F;
        CpuFid = eax & 0xFF;
    }

    // P0 fix C001_0015 HWCR[21]=1
    // Fixes timer issues when not using HPET
    [Obsolete]
    public bool ApplyTscWorkaround()
    {
        uint eax = 0, edx = 0; 
        if (cpu.ReadMsr(0xC0010015, ref eax, ref edx))
        {
            eax |= 0x200000;
            return cpu.WriteMsrWn(0xC0010015, eax, edx);
        } 
        MessageBox.Show("Error applying TSC fix!");
        return false;
    }

    [Obsolete]
    private bool WritePstateClick(int pstateId, uint eax, uint edx, int numanode = 0)
    {
        if (NUMAUtil.HighestNumaNode > 0)
        {
            NUMAUtil.SetThreadProcessorAffinity((ushort)(numanode + 1),
            Enumerable.Range(0, Environment.ProcessorCount).ToArray());
        } 
        if (!ApplyTscWorkaround())
        { return false; } 
        if (!cpu.WriteMsrWn(Convert.ToUInt32(Convert.ToInt64(0xC0010064) + pstateId), eax, edx)) { MessageBox.Show("Error writing PState! ID = " + pstateId); return false; } 
        return true;
    }

    private void ReadPstate()
    {
        for (var i = 0; i < 3; i++)
        {
            uint eax = default, edx = default;
            var pstateId = i;
            try
            {
                if (!cpu.ReadMsr(Convert.ToUInt32(Convert.ToInt64(0xC0010064) + pstateId), ref eax, ref edx))
                {
                    MessageBox.Show("Error reading PState! ID = " + pstateId);
                    return;
                }
            }
            catch
            {
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

    private string GetWmiInstanceName()
    {
        try
        {
            instanceName = WMI.GetInstanceName(wmiScope, wmiAMDACPI);
        }
        catch
        {
            // ignored
        }

        return instanceName;
    }

    private void PopulateWmiFunctions()
    {
        try
        {
            instanceName = GetWmiInstanceName();
            classInstance = new ManagementObject(wmiScope,
                $"{wmiAMDACPI}.InstanceName='{instanceName}'",
                null);

            // Get function names with their IDs
            string[] functionObjects = { "GetObjectID", "GetObjectID2" };
            var index = 1; 
            foreach (var functionObject in functionObjects)
            {
                try
                {
                    pack = WMI.InvokeMethod(classInstance, functionObject, "pack", null, 0);

                    if (pack != null)
                    {
                        var ID = (uint[])pack.GetPropertyValue("ID");
                        var IDString = (string[])pack.GetPropertyValue("IDString");
                        var Length = (byte)pack.GetPropertyValue("Length");

                        for (var i = 0; i < Length; ++i)
                        {
                            if (IDString[i] == "")
                            {
                                break;
                            }

                            var item = new WmiCmdListItem($"{IDString[i] + ": "}{ID[i]:X8}", ID[i],
                                !IDString[i].StartsWith("Get"));
                        }
                    }
                }
                catch
                {
                    // ignored
                } index++;
            }
        }
        catch
        {
            // ignored
        }
    } 
    //Pstates section
    private void Pstate_Expanding(Expander sender, ExpanderExpandingEventArgs args)
    {
        // ReadPstate();
    } 
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
            } catch { }
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
            } catch { }
            Save_ID1();
        }
    } 
    private async void Mult_2_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (waitforload == true) { return; }
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
    //Send Message
    private async Task Send_Message(string msg, string submsg, Symbol symbol)
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
    //SMU КОМАНДЫ
    [Obsolete]
    private async void ApplySettings()
    {
        try
        {
            var args = Services.Utils.MakeCmdArgs();
            var userArgs = textBoxARG0.Text.Trim().Split(',');

            TryConvertToUint(textBoxCMDAddress.Text, out var addrMsg);
            TryConvertToUint(textBoxRSPAddress.Text, out var addrRsp);
            TryConvertToUint(textBoxARGAddress.Text, out var addrArg);
            TryConvertToUint(textBoxCMD.Text, out var command);
            testMailbox.SMU_ADDR_MSG = addrMsg;
            testMailbox.SMU_ADDR_RSP = addrRsp;
            testMailbox.SMU_ADDR_ARG = addrArg;
            for (var i = 0; i < userArgs.Length; i++)
            {
                if (i == args.Length)
                {
                    break;
                }
                TryConvertToUint(userArgs[i], out var temp);
                args[i] = temp;
            }
            //App.MainWindow.ShowMessageDialogAsync("MSG Address:  0x" + Convert.ToString(testMailbox.SMU_ADDR_MSG, 16).ToUpper() + "\n" + "RSP Address:  0x" + Convert.ToString(testMailbox.SMU_ADDR_RSP, 16).ToUpper() + "\n" + "ARG0 Address: 0x" + Convert.ToString(testMailbox.SMU_ADDR_ARG, 16).ToUpper() + "\n" + "ARG0        : 0x" + Convert.ToString(args[0], 16).ToUpper() + " " + command.ToString(), "Adress");
            var status = cpu.smu.SendSmuCommand(testMailbox, command, ref args);
            //App.MainWindow.ShowMessageDialogAsync(testMailbox.SMU_ADDR_RSP + " " + testMailbox.SMU_ADDR_MSG + " " + testMailbox.SMU_ADDR_ARG + " " + command.ToString() + args[0].ToString(), "Set!");
            if (status == Services.SMU.Status.OK)
            {
                await Send_Message("SMUOKText".GetLocalized(), "SMUOKDesc".GetLocalized(), Symbol.Accept);
            }
            else
            {
                if (status == Services.SMU.Status.CMD_REJECTED_PREREQ)
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
    [Obsolete]
    private void DevEnv_Expanding(Expander sender, ExpanderExpandingEventArgs args)
    {
        RunBackgroundTask(BackgroundWorkerTrySettings_DoWork!, SmuScan_WorkerCompleted!);
    } 
    private void ComboBoxMailboxSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (comboBoxMailboxSelect.SelectedItem is MailboxListItem item) { InitTestMailbox(item.msgAddr, item.rspAddr, item.argAddr); }
        else { return; }
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
            var micaBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop
            {
                Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt
            };
            newWindow.SystemBackdrop = micaBackdrop;
            newWindow.Activate();
        }
    } 
#pragma warning restore CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.
#pragma warning restore CS8601 // Возможно, назначение-ссылка, допускающее значение NULL.
}
