﻿using System.ComponentModel;
using System.Windows.Forms;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.ViewModels;
using Windows.Foundation.Metadata;
using Windows.UI;
using ZenStates.Core;
using Button = Microsoft.UI.Xaml.Controls.Button;
using CheckBox = Microsoft.UI.Xaml.Controls.CheckBox;
using ComboBox = Microsoft.UI.Xaml.Controls.ComboBox;
using Process = System.Diagnostics.Process;
using TextBox = Microsoft.UI.Xaml.Controls.TextBox;

namespace Saku_Overclock.Views;

public sealed partial class ПараметрыPage : Page
{
    public ПараметрыViewModel ViewModel
    {
        get;
    }
    private FontIcon? SMUSymbol1; // тоже самое что и SMUSymbol
    private List<SmuAddressSet>? matches; // Совпадения адресов SMU
    private static Config config = new(); // Основной конфиг приложения
    private static Smusettings smusettings = new(); // Загрузка настроек быстрых команд SMU
    private static Profile[] profile = new Profile[1]; // Всегда по умолчанию будет 1 профиль
    private static JsonContainers.Notifications notify = new(); // Уведомления приложения
    private int indexprofile = 0; // Выбранный профиль
    private string SMUSymbol = "\uE8C8"; // Изначальный символ копирования, для секции Редактор параметров SMU. Используется для быстрых команд SMU
    private bool isLoaded = false; // Загружена ли корректно страница для применения изменений
    private bool relay = false; // Задержка между изменениями ComboBox в секции Состояния CPU
    private Cpu? cpu; // Импорт Zen States core
    private SendSMUCommand? cpusend; // Импорт отправителя команд SMU
    public bool turboboost = true;
    private bool waitforload = true; // Ожидание окончательной смены профиля на другой. Активируется при смене профиля
    public string? adjline; // Команды RyzenADJ для применения
    private readonly ZenStates.Core.Mailbox testMailbox = new(); // Новый адрес SMU
    private static string? equalvid; // Преобразование из напряжения в его ID. Используется в секции Состояния CPU для указания напряжения PState
    private static readonly List<double> pstatesFID = [0, 0, 0];
    private static readonly List<double> pstatesDID = [0, 0, 0];
    private static readonly List<double> pstatesVID = [0, 0, 0];
    public static string ApplyInfo = ""; // Информация об ошибках после применения

    public ПараметрыPage()
    {
        ViewModel = App.GetService<ПараметрыViewModel>();
        InitializeComponent();
        ConfigLoad();
        ProfileLoad();
        indexprofile = config.Preset;
        config.NBFCFlagConsoleCheckSpeedRunning = false;
        config.FlagRyzenADJConsoleTemperatureCheckRunning = false;
        ConfigSave();
        try
        {
            cpu ??= CpuSingleton.GetInstance();
            cpusend ??= App.GetService<SendSMUCommand>();
        }
        catch (Exception ex)
        {
            TraceIt_TraceError(ex.ToString());
            App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash_CPU".GetLocalized(), AppContext.BaseDirectory));
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
        catch (Exception ex)
        {
            TraceIt_TraceError(ex.ToString());
            try
            {
                ConfigLoad(); config.Preset = -1; ConfigSave(); indexprofile = -1;
                SlidersInit();
            }
            catch (Exception ex1)
            {
                TraceIt_TraceError(ex1.ToString());
                await Send_Message("Critical Error!", "Can't load profiles. Tell this to developer", Symbol.Bookmarks);
            }
        }
    }
    public static void ConfigSave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json", JsonConvert.SerializeObject(config, Formatting.Indented));
        }
        catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
    }
    public static void ConfigLoad()
    {
        try
        {
            config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json"))!;
        }
        catch (Exception ex)
        {
            TraceIt_TraceError(ex.ToString());
            JsonRepair('c');
        }
    }
    public static void NotifySave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json", JsonConvert.SerializeObject(notify, Formatting.Indented));
        }
        catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
    }
    public static async void NotifyLoad()
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
                catch (Exception ex) { TraceIt_TraceError(ex.ToString()); JsonRepair('n'); }
            }
            else { JsonRepair('n'); }
            if (!success)
            {
                await Task.Delay(30);
                retryCount++;
            }
        }
    }
    public static void SmuSettingsSave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\smusettings.json", JsonConvert.SerializeObject(smusettings, Formatting.Indented));
        }
        catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
    }
    public static void SmuSettingsLoad()
    {
        try
        {
            smusettings = JsonConvert.DeserializeObject<Smusettings>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\smusettings.json"))!;
        }
        catch (Exception ex) { JsonRepair('s'); TraceIt_TraceError(ex.ToString()); }
    }
    public static void ProfileSave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json", JsonConvert.SerializeObject(profile, Formatting.Indented));
        }
        catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
    }
    public static void ProfileLoad()
    {
        try
        {
            profile = JsonConvert.DeserializeObject<Profile[]>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json"))!;
        }
        catch (Exception ex) { JsonRepair('p'); TraceIt_TraceError(ex.ToString()); }
    }
    public static void JsonRepair(char file)
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
        ProfileCOM.Items.Add(new ComboBoxItem()
        {
            Content = new TextBlock
            {
                Text = "Param_Premaded".GetLocalized(),
                Foreground = (Microsoft.UI.Xaml.Media.Brush)App.Current.Resources["AccentTextFillColorTertiaryBrush"]
            },
            IsEnabled = false
        });
        if (profile == null) 
        {
            profile = new Profile[1];
            profile[0] = new Profile();
            ProfileSave();
        }
        else
        {
            for (var i = 0; i < profile.Length; i++)
            {
                if (profile[i] == null) 
                { 
                    profile[i] = new Profile();
                    ProfileSave();
                    NotifyLoad();
                    notify.Notifies ??= [];
                    notify.Notifies.Add(new Notify { Title = "SaveSuccessTitle".GetLocalized(), Msg = "SaveSuccessDesc".GetLocalized() + " " + SaveProfileN.Text, Type = InfoBarSeverity.Success });
                    NotifySave();
                }
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
        } 
        //Main INIT. It will be better soon! - Serzhik Saku, Erruar
        MainInit(indexprofile);
        waitforload = false;
    }
    private void DesktopCPU_Delete_UselessParameters()
    {
        Laptops_Avg_Wattage.Visibility = Visibility.Collapsed; //Убрать параметры для ноутбуков
        Laptops_Avg_Wattage_Desc.Visibility = Visibility.Collapsed;
        Laptops_Fast_Speed.Visibility = Visibility.Collapsed;
        Laptops_Fast_Speed_Desc.Visibility = Visibility.Collapsed;
        Laptops_HTC_Temp.Visibility = Visibility.Collapsed;
        Laptops_HTC_Temp_Desc.Visibility = Visibility.Collapsed;
        Laptops_Real_Wattage.Visibility = Visibility.Collapsed;
        Laptops_Real_Wattage_Desc.Visibility = Visibility.Collapsed;
        Laptops_slow_Speed.Visibility = Visibility.Collapsed;
        Laptops_slow_Speed_Desc.Visibility = Visibility.Collapsed;
        VRM_Laptops_Prochot_Time.Visibility = Visibility.Collapsed; //Убрать настройки VRM
        VRM_Laptops_Prochot_Time_Desc.Visibility = Visibility.Collapsed;
        VRM_Laptops_PSI_SoC.Visibility = Visibility.Collapsed;
        VRM_Laptops_PSI_SoC_Desc.Visibility = Visibility.Collapsed;
        VRM_Laptops_PSI_VDD.Visibility = Visibility.Collapsed;
        VRM_Laptops_PSI_VDD_Desc.Visibility = Visibility.Collapsed;
        VRM_Laptops_SoC_Limit.Visibility = Visibility.Collapsed;
        VRM_Laptops_SoC_Limit_Desc.Visibility = Visibility.Collapsed;
        VRM_Laptops_SoC_Max.Visibility = Visibility.Collapsed;
        VRM_Laptops_SoC_Max_Desc.Visibility = Visibility.Collapsed;
        ADV_Laptop_AplusA_Limit.Visibility = Visibility.Collapsed; //Убрать расширенный разгон
        ADV_Laptop_AplusA_Limit_Desc.Visibility = Visibility.Collapsed;
        ADV_Laptop_dGPU_Temp.Visibility = Visibility.Collapsed;
        ADV_Laptop_dGPU_Temp_Desc.Visibility = Visibility.Collapsed;
        ADV_Laptop_iGPU_Freq.Visibility = Visibility.Collapsed;
        ADV_Laptop_iGPU_Freq_Desc.Visibility = Visibility.Collapsed;
        ADV_Laptop_iGPU_Limit.Visibility = Visibility.Collapsed;
        ADV_Laptop_iGPU_Limit_Desc.Visibility = Visibility.Collapsed;
        ADV_Laptop_iGPU_Temp.Visibility = Visibility.Collapsed;
        ADV_Laptop_iGPU_Temp_Desc.Visibility = Visibility.Collapsed;
        ADV_Laptop_Pref_Mode.Visibility = Visibility.Collapsed;
        ADV_Laptop_Pref_Mode_Desc.Visibility = Visibility.Collapsed;
    }
    private void LockUselessParameters(bool locks)
    {
        CCD1_1.IsEnabled = locks; CCD1_1v.IsEnabled = locks;
        CCD1_2.IsEnabled = locks; CCD1_2v.IsEnabled = locks;
        CCD1_3.IsEnabled = locks; CCD1_3v.IsEnabled = locks;
        CCD1_4.IsEnabled = locks; CCD1_4v.IsEnabled = locks;
        CCD1_5.IsEnabled = locks; CCD1_5v.IsEnabled = locks;
        CCD1_6.IsEnabled = locks; CCD1_6v.IsEnabled = locks;
        CCD1_7.IsEnabled = locks; CCD1_7v.IsEnabled = locks;
        CCD1_8.IsEnabled = locks; CCD1_8v.IsEnabled = locks;
        CCD2_1.IsEnabled = locks; CCD2_1v.IsEnabled = locks;
        CCD2_2.IsEnabled = locks; CCD2_2v.IsEnabled = locks;
        CCD2_3.IsEnabled = locks; CCD2_3v.IsEnabled = locks;
        CCD2_4.IsEnabled = locks; CCD2_4v.IsEnabled = locks;
        CCD2_5.IsEnabled = locks; CCD2_5v.IsEnabled = locks;
        CCD2_6.IsEnabled = locks; CCD2_6v.IsEnabled = locks;
        CCD2_7.IsEnabled = locks; CCD2_7v.IsEnabled = locks;
        CCD2_8.IsEnabled = locks; CCD2_8v.IsEnabled = locks;
    }
    private void MainInit(int index)
    {
        if (SettingsViewModel.VersionId != 5) // Если не дебаг
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
            else
            {
                // Если перед нами вангог (стимдек и его подобные)
                VRM_Laptops_PSI_SoC.Visibility = Visibility.Collapsed;
                VRM_Laptops_PSI_SoC_Desc.Visibility = Visibility.Collapsed;
                VRM_Laptops_PSI_VDD.Visibility = Visibility.Collapsed;
                VRM_Laptops_PSI_VDD_Desc.Visibility = Visibility.Collapsed;
                ADV_Laptop_AplusA_Limit.Visibility = Visibility.Collapsed; // Убрать расширенный разгон
                ADV_Laptop_AplusA_Limit_Desc.Visibility = Visibility.Collapsed;
                ADV_Laptop_dGPU_Temp.Visibility = Visibility.Collapsed;
                ADV_Laptop_dGPU_Temp_Desc.Visibility = Visibility.Collapsed;
                ADV_Laptop_iGPU_Limit.Visibility = Visibility.Collapsed;
                ADV_Laptop_iGPU_Limit_Desc.Visibility = Visibility.Collapsed;
            }
            if (cpu?.info.codeName.ToString().Contains("Raven") == false && cpu?.info.codeName.ToString().Contains("Dali") == false && cpu?.info.codeName.ToString().Contains("Picasso") == false)
            {
                iGPU_Subsystems.Visibility = Visibility.Collapsed;
            }
            if (cpu?.info.codeName.ToString().Contains("Mendocino") == true || cpu?.info.codeName.ToString().Contains("Rembrandt") == true || cpu?.info.codeName.ToString().Contains("Phoenix") == true || cpu?.info.codeName.ToString().Contains("DragonRange") == true || cpu?.info.codeName.ToString().Contains("HawkPoint") == true)
            {
                VRM_Laptops_PSI_SoC.Visibility = Visibility.Collapsed;
                VRM_Laptops_PSI_SoC_Desc.Visibility = Visibility.Collapsed;
                VRM_Laptops_PSI_VDD.Visibility = Visibility.Collapsed;
                VRM_Laptops_PSI_VDD_Desc.Visibility = Visibility.Collapsed;
                ADV_Laptop_OCVOLT.Visibility = Visibility.Collapsed;
                ADV_Laptop_OCVOLT_Desc.Visibility = Visibility.Collapsed;
                ADV_Laptop_AplusA_Limit.Visibility = Visibility.Collapsed; //Убрать расширенный разгон
                ADV_Laptop_AplusA_Limit_Desc.Visibility = Visibility.Collapsed;
            }
            if (cpu?.info.codeName.ToString().Contains("Pinnacle") == true || cpu?.info.codeName.ToString().Contains("Summit") == true)
            {
                CCD1_Expander.Visibility = Visibility.Collapsed; //Убрать Оптимизатор кривой
                CCD2_Expander.Visibility = Visibility.Collapsed;
                CO_Expander.Visibility = Visibility.Collapsed;
                DesktopCPU_Delete_UselessParameters();
            }
            if (cpu?.info.codeName.ToString().Contains("Matisse") == true || cpu?.info.codeName.ToString().Contains("Vermeer") == true)
            {
                DesktopCPU_Delete_UselessParameters();
            }
            if (cpu == null || cpu?.info.codeName.ToString().Contains("Unsupported") == true)
            {
                MainScroll.IsEnabled = false;
                ActionButton_Apply.IsEnabled = false;
                ActionButton_Delete.IsEnabled = false;
                ActionButton_Mon.IsEnabled = false;
                ActionButton_Save.IsEnabled = false;
                ActionButton_Share.IsEnabled = false;
                EditProfileButton.IsEnabled = false;
                Action_IncompatibleProfile.IsOpen = false;
                Action_IncompatibleCPU.IsOpen = true;
            }
            var cores = ИнформацияPage.GetCPUCores();
            if (cores > 8)
            {
                if (cores <= 15) { CCD2_Grid7_2.Visibility = Visibility.Collapsed; CCD2_Grid7_1.Visibility = Visibility.Collapsed; }
                if (cores <= 14) { CCD2_Grid6_2.Visibility = Visibility.Collapsed; CCD2_Grid6_1.Visibility = Visibility.Collapsed; }
                if (cores <= 13) { CCD2_Grid5_2.Visibility = Visibility.Collapsed; CCD2_Grid5_1.Visibility = Visibility.Collapsed; }
                if (cores <= 12) { CCD2_Grid4_2.Visibility = Visibility.Collapsed; CCD2_Grid4_1.Visibility = Visibility.Collapsed; }
                if (cores <= 11) { CCD2_Grid3_2.Visibility = Visibility.Collapsed; CCD2_Grid3_1.Visibility = Visibility.Collapsed; }
                if (cores <= 10) { CCD2_Grid2_2.Visibility = Visibility.Collapsed; CCD2_Grid2_1.Visibility = Visibility.Collapsed; }
                if (cores <= 9) { CCD2_Grid1_2.Visibility = Visibility.Collapsed; CCD2_Grid1_1.Visibility = Visibility.Collapsed; }
            }
            else
            {
                CO_Cores_Text.Text = CO_Cores_Text.Text.Replace("7", $"{cores - 1}");
                CCD2_Expander.Visibility = Visibility.Collapsed;
                if (cores <= 7) { CCD1_Grid8_2.Visibility = Visibility.Collapsed; CCD1_Grid8_1.Visibility = Visibility.Collapsed; }
                if (cores <= 6) { CCD1_Grid7_2.Visibility = Visibility.Collapsed; CCD1_Grid7_1.Visibility = Visibility.Collapsed; }
                if (cores <= 5) { CCD1_Grid6_2.Visibility = Visibility.Collapsed; CCD1_Grid6_1.Visibility = Visibility.Collapsed; }
                if (cores <= 4) { CCD1_Grid5_2.Visibility = Visibility.Collapsed; CCD1_Grid5_1.Visibility = Visibility.Collapsed; }
                if (cores <= 3) { CCD1_Grid4_2.Visibility = Visibility.Collapsed; CCD1_Grid4_1.Visibility = Visibility.Collapsed; }
                if (cores <= 2) { CCD1_Grid3_2.Visibility = Visibility.Collapsed; CCD1_Grid3_1.Visibility = Visibility.Collapsed; }
                if (cores <= 1) { CCD1_Grid2_2.Visibility = Visibility.Collapsed; CCD1_Grid2_1.Visibility = Visibility.Collapsed; }
                if (cores == 0) { CCD1_Expander.Visibility = Visibility.Collapsed; }
            }
        }
        waitforload = true;
        ConfigLoad();
        if (config.Preset == -1 || index == -1) //Load from unsaved
        { 
            MainScroll.IsEnabled = false;
            ActionButton_Apply.IsEnabled = false;
            ActionButton_Delete.IsEnabled = false;
            ActionButton_Mon.IsEnabled = false;
            ActionButton_Save.IsEnabled = false;
            ActionButton_Share.IsEnabled = false;
            EditProfileButton.IsEnabled = false;
            Action_IncompatibleProfile.IsOpen = true;
            //Unknown
        }
        else
        {
            MainScroll.IsEnabled = true;
            ActionButton_Apply.IsEnabled = true;
            ActionButton_Delete.IsEnabled = true;
            ActionButton_Mon.IsEnabled = true;
            ActionButton_Save.IsEnabled = true;
            ActionButton_Share.IsEnabled = true;
            EditProfileButton.IsEnabled = true;
            Action_IncompatibleProfile.IsOpen = false;
            ProfileLoad();
            try
            {
                if (profile[index].cpu1value > c1v.Maximum) { c1v.Maximum = FromValueToUpperFive(profile[index].cpu1value); }
                if (profile[index].cpu2value > c2v.Maximum) { c2v.Maximum = FromValueToUpperFive(profile[index].cpu2value); }
                if (profile[index].cpu3value > c3v.Maximum) { c3v.Maximum = FromValueToUpperFive(profile[index].cpu3value); }
                if (profile[index].cpu4value > c4v.Maximum) { c4v.Maximum = FromValueToUpperFive(profile[index].cpu4value); }
                if (profile[index].cpu5value > c5v.Maximum) { c5v.Maximum = FromValueToUpperFive(profile[index].cpu5value); }
                if (profile[index].cpu6value > c6v.Maximum) { c6v.Maximum = FromValueToUpperFive(profile[index].cpu6value); }
                if (profile[index].cpu7value > c7v.Maximum) { c7v.Maximum = FromValueToUpperFive(profile[index].cpu7value); }

                if (profile[index].vrm1value > V1V.Maximum) { V1V.Maximum = FromValueToUpperFive(profile[index].vrm1value); }
                if (profile[index].vrm2value > V2V.Maximum) { V2V.Maximum = FromValueToUpperFive(profile[index].vrm2value); }
                if (profile[index].vrm3value > V3V.Maximum) { V3V.Maximum = FromValueToUpperFive(profile[index].vrm3value); }
                if (profile[index].vrm4value > V4V.Maximum) { V4V.Maximum = FromValueToUpperFive(profile[index].vrm4value); }
                if (profile[index].vrm5value > V5V.Maximum) { V5V.Maximum = FromValueToUpperFive(profile[index].vrm5value); }
                if (profile[index].vrm6value > V6V.Maximum) { V6V.Maximum = FromValueToUpperFive(profile[index].vrm6value); }
                if (profile[index].vrm7value > V7V.Maximum) { V7V.Maximum = FromValueToUpperFive(profile[index].vrm7value); }

                if (profile[index].gpu1value > g1v.Maximum) { g1v.Maximum = FromValueToUpperFive(profile[index].gpu1value); }
                if (profile[index].gpu2value > g2v.Maximum) { g2v.Maximum = FromValueToUpperFive(profile[index].gpu2value); }
                if (profile[index].gpu3value > g3v.Maximum) { g3v.Maximum = FromValueToUpperFive(profile[index].gpu3value); }
                if (profile[index].gpu4value > g4v.Maximum) { g4v.Maximum = FromValueToUpperFive(profile[index].gpu4value); }
                if (profile[index].gpu5value > g5v.Maximum) { g5v.Maximum = FromValueToUpperFive(profile[index].gpu5value); }
                if (profile[index].gpu6value > g6v.Maximum) { g6v.Maximum = FromValueToUpperFive(profile[index].gpu6value); }
                if (profile[index].gpu7value > g7v.Maximum) { g7v.Maximum = FromValueToUpperFive(profile[index].gpu7value); }
                if (profile[index].gpu8value > g8v.Maximum) { g8v.Maximum = FromValueToUpperFive(profile[index].gpu8value); }
                if (profile[index].gpu9value > g9v.Maximum) { g9v.Maximum = FromValueToUpperFive(profile[index].gpu9value); }
                if (profile[index].gpu10value > g10v.Maximum) { g10v.Maximum = FromValueToUpperFive(profile[index].gpu10value); }
                if (profile[index].gpu11value > g11v.Maximum) { g11v.Maximum = FromValueToUpperFive(profile[index].gpu11value); }
                if (profile[index].gpu12value > g12v.Maximum) { g12v.Maximum = FromValueToUpperFive(profile[index].gpu12value); }

                if (profile[index].advncd1value > a1v.Maximum) { a1v.Maximum = FromValueToUpperFive(profile[index].advncd1value); }
                if (profile[index].advncd2value > a2v.Maximum) { a2v.Maximum = FromValueToUpperFive(profile[index].advncd2value); }
                if (profile[index].advncd3value > a3v.Maximum) { a3v.Maximum = FromValueToUpperFive(profile[index].advncd3value); }
                if (profile[index].advncd4value > a4v.Maximum) { a4v.Maximum = FromValueToUpperFive(profile[index].advncd4value); }
                if (profile[index].advncd5value > a5v.Maximum) { a5v.Maximum = FromValueToUpperFive(profile[index].advncd5value); }
                if (profile[index].advncd6value > a6v.Maximum) { a6v.Maximum = FromValueToUpperFive(profile[index].advncd6value); }
                if (profile[index].advncd7value > a7v.Maximum) { a7v.Maximum = FromValueToUpperFive(profile[index].advncd7value); }
                if (profile[index].advncd8value > a8v.Maximum) { a8v.Maximum = FromValueToUpperFive(profile[index].advncd8value); }
                if (profile[index].advncd9value > a9v.Maximum) { a9v.Maximum = FromValueToUpperFive(profile[index].advncd9value); }
                if (profile[index].advncd10value > a10v.Maximum) { a10v.Maximum = FromValueToUpperFive(profile[index].advncd10value); }
                if (profile[index].advncd11value > a11v.Maximum) { a11v.Maximum = FromValueToUpperFive(profile[index].advncd11value); }
                if (profile[index].advncd12value > a12v.Maximum) { a12v.Maximum = FromValueToUpperFive(profile[index].advncd12value); }
                if (profile[index].advncd15value > a15v.Maximum) { a15v.Maximum = FromValueToUpperFive(profile[index].advncd15value); }

            }
            catch (Exception ex)
            {
                TraceIt_TraceError(ex.ToString());
            }
            try
            {
                c1.IsChecked = profile[index].cpu1; c1v.Value = profile[index].cpu1value; c2.IsChecked = profile[index].cpu2; c2v.Value = profile[index].cpu2value; c3.IsChecked = profile[index].cpu3; c3v.Value = profile[index].cpu3value; c4.IsChecked = profile[index].cpu4; c4v.Value = profile[index].cpu4value; c5.IsChecked = profile[index].cpu5; c5v.Value = profile[index].cpu5value; c6.IsChecked = profile[index].cpu6; c6v.Value = profile[index].cpu6value; c7.IsChecked = profile[index].cpu7; c7v.Value = profile[index].cpu7value;
                V1.IsChecked = profile[index].vrm1; V1V.Value = profile[index].vrm1value; V2.IsChecked = profile[index].vrm2; V2V.Value = profile[index].vrm2value; V3.IsChecked = profile[index].vrm3; V3V.Value = profile[index].vrm3value; V4.IsChecked = profile[index].vrm4; V4V.Value = profile[index].vrm4value; V5.IsChecked = profile[index].vrm5; V5V.Value = profile[index].vrm5value; V6.IsChecked = profile[index].vrm6; V6V.Value = profile[index].vrm6value; V7.IsChecked = profile[index].vrm7; V7V.Value = profile[index].vrm7value;
                g1.IsChecked = profile[index].gpu1; g1v.Value = profile[index].gpu1value; g2.IsChecked = profile[index].gpu2; g2v.Value = profile[index].gpu2value; g3.IsChecked = profile[index].gpu3; g3v.Value = profile[index].gpu3value; g4.IsChecked = profile[index].gpu4; g4v.Value = profile[index].gpu4value; g5.IsChecked = profile[index].gpu5; g5v.Value = profile[index].gpu5value; g6.IsChecked = profile[index].gpu6; g6v.Value = profile[index].gpu6value; g7.IsChecked = profile[index].gpu7; g7v.Value = profile[index].gpu7value; g8v.Value = profile[index].gpu8value; g8.IsChecked = profile[index].gpu8; g9v.Value = profile[index].gpu9value; g9.IsChecked = profile[index].gpu9; g10v.Value = profile[index].gpu10value; g10.IsChecked = profile[index].gpu10; g11.IsChecked = profile[index].gpu11; g11v.Value = profile[index].gpu11value; g12.IsChecked = profile[index].gpu12; g12v.Value = profile[index].gpu12value; g15.IsChecked = profile[index].gpu15; g15m.SelectedIndex = profile[index].gpu15value; g16.IsChecked = profile[index].gpu16; g16m.SelectedIndex = profile[index].gpu16value;
                a1.IsChecked = profile[index].advncd1; a1v.Value = profile[index].advncd1value; a2.IsChecked = profile[index].advncd2; a2v.Value = profile[index].advncd2value; a3.IsChecked = profile[index].advncd3; a3v.Value = profile[index].advncd3value; a4.IsChecked = profile[index].advncd4; a4v.Value = profile[index].advncd4value; a5.IsChecked = profile[index].advncd5; a5v.Value = profile[index].advncd5value; a6.IsChecked = profile[index].advncd6; a6v.Value = profile[index].advncd6value; a7.IsChecked = profile[index].advncd7; a7v.Value = profile[index].advncd7value; a8v.Value = profile[index].advncd8value; a8.IsChecked = profile[index].advncd8; a9v.Value = profile[index].advncd9value; a9.IsChecked = profile[index].advncd9; a10v.Value = profile[index].advncd10value; a11v.Value = profile[index].advncd11value; a11.IsChecked = profile[index].advncd11; a12v.Value = profile[index].advncd12value; a12.IsChecked = profile[index].advncd12; a13.IsChecked = profile[index].advncd13; a13m.SelectedIndex = profile[index].advncd13value; a14.IsChecked = profile[index].advncd14; a14m.SelectedIndex = profile[index].advncd14value; a15.IsChecked = profile[index].advncd15; a15v.Value = profile[index].advncd15value;
                CCD_CO_Mode_Sel.IsChecked = profile[index].comode; CCD_CO_Mode.SelectedIndex = profile[index].coprefmode;
                O1.IsChecked = profile[index].coall; O1v.Value = profile[index].coallvalue; O2.IsChecked = profile[index].cogfx; O2v.Value = profile[index].cogfxvalue; CCD1_1.IsChecked = profile[index].coper0; CCD1_1v.Value = profile[index].coper0value; CCD1_2.IsChecked = profile[index].coper1; CCD1_2v.Value = profile[index].coper1value; CCD1_3.IsChecked = profile[index].coper2; CCD1_3v.Value = profile[index].coper2value; CCD1_4.IsChecked = profile[index].coper3; CCD1_4v.Value = profile[index].coper3value; CCD1_5.IsChecked = profile[index].coper4; CCD1_5v.Value = profile[index].coper4value; CCD1_6.IsChecked = profile[index].coper5; CCD1_6v.Value = profile[index].coper5value; CCD1_7.IsChecked = profile[index].coper6; CCD1_7v.Value = profile[index].coper6value; CCD1_8.IsChecked = profile[index].coper7; CCD1_8v.Value = profile[index].coper7value;
                CCD2_1.IsChecked = profile[index].coper8; CCD2_1v.Value = profile[index].coper8value; CCD2_2.IsChecked = profile[index].coper9; CCD2_2v.Value = profile[index].coper9value; CCD2_3.IsChecked = profile[index].coper10; CCD2_3v.Value = profile[index].coper10value; CCD2_4.IsChecked = profile[index].coper11; CCD2_4v.Value = profile[index].coper11value; CCD2_5.IsChecked = profile[index].coper12; CCD2_5v.Value = profile[index].coper12value; CCD2_6.IsChecked = profile[index].coper13; CCD2_6v.Value = profile[index].coper13value; CCD2_7.IsChecked = profile[index].coper14; CCD2_7v.Value = profile[index].coper14value; CCD2_8.IsChecked = profile[index].coper15; CCD2_8v.Value = profile[index].coper15value;
                EnablePstates.IsOn = profile[index].enablePstateEditor; Turbo_boost.IsOn = profile[index].turboBoost; Autoapply_1.IsOn = profile[index].autoPstate; IgnoreWarn.IsOn = profile[index].ignoreWarn; Without_P0.IsOn = profile[index].p0Ignorewarn;
                DID_0.Value = profile[index].did0; DID_1.Value = profile[index].did1; DID_2.Value = profile[index].did2; FID_0.Value = profile[index].fid0; FID_1.Value = profile[index].fid1; FID_2.Value = profile[index].fid2; VID_0.Value = profile[index].vid0; VID_1.Value = profile[index].vid1; VID_2.Value = profile[index].vid2;
                EnableSMU.IsOn = profile[index].smuEnabled;
                SMU_Func_Enabl.IsOn = profile[index].smuFunctionsEnabl;
                Bit_0_FEATURE_CCLK_CONTROLLER.IsOn = profile[index].smuFeatureCCLK;
                Bit_2_FEATURE_DATA_CALCULATION.IsOn = profile[index].smuFeatureData;
                Bit_3_FEATURE_PPT.IsOn = profile[index].smuFeaturePPT;
                Bit_4_FEATURE_TDC.IsOn = profile[index].smuFeatureTDC;
                Bit_5_FEATURE_THERMAL.IsOn = profile[index].smuFeatureThermal;
                Bit_8_FEATURE_PLL_POWER_DOWN.IsOn = profile[index].smuFeaturePowerDown;
                Bit_37_FEATURE_PROCHOT.IsOn = profile[index].smuFeatureProchot;
                Bit_39_FEATURE_STAPM.IsOn = profile[index].smuFeatureSTAPM;
                Bit_40_FEATURE_CORE_CSTATES.IsOn = profile[index].smuFeatureCStates;
                Bit_41_FEATURE_GFX_DUTY_CYCLE.IsOn = profile[index].smuFeatureGfxDutyCycle;
                Bit_42_FEATURE_AA_MODE.IsOn = profile[index].smuFeatureAplusA;
            }
            catch
            {
                profile = new Profile[1];
                profile[0] = new Profile();
                ProfileSave();
            } 
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
        catch (Exception ex) { if (config.Preset != -1) { TraceIt_TraceError(ex.ToString()); } }
        waitforload = false;
        SmuSettingsLoad();
        if (smusettings.Note != string.Empty)
        {
            SMUNotes.Document.SetText(TextSetOptions.FormatRtf, smusettings.Note);
            ChangeRichEditBoxTextColor(SMUNotes, GetColorFromBrush(TextColor.Foreground));
        }
        try
        {
            Init_QuickSMU();
        }
        catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
    }
    private static Color GetColorFromBrush(Brush brush)
    {
        if (brush is SolidColorBrush solidColorBrush)
        {
            return solidColorBrush.Color;
        }
        else
        {
            throw new InvalidOperationException("Brush is not a SolidColorBrush");
        }
    }
    private static void ChangeRichEditBoxTextColor(RichEditBox richEditBox, Color color)
    {
        richEditBox.Document.ApplyDisplayUpdates();
        var documentRange = richEditBox.Document.GetRange(0, TextConstants.MaxUnitCount);
        documentRange.CharacterFormat.ForegroundColor = color;
        richEditBox.Document.ApplyDisplayUpdates();
    }
    private static int FromValueToUpperFive(double value) => (int)Math.Ceiling(value / 5) * 5;
    private uint GetCoreMask(int coreIndex)
    {
        var ccxInCcd = cpu?.info.family == Cpu.Family.FAMILY_19H ? 1U : 2U;
        var coresInCcx = 8 / ccxInCcd;

        var ccd = Convert.ToUInt32(coreIndex / 8);
        var ccx = Convert.ToUInt32(coreIndex / coresInCcx - ccxInCcd * ccd);
        var core = Convert.ToUInt32(coreIndex % coresInCcx);
        return cpu!.MakeCoreMask(core, ccd, ccx);
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
    private static void TraceIt_TraceError(string error) //Система TraceIt! позволит логгировать все ошибки
    {
        if (error != string.Empty)
        {
            NotifyLoad(); //Добавить уведомление
            notify.Notifies ??= [];
            notify.Notifies.Add(new Notify { Title = "TraceIt_Error".GetLocalized(), Msg = error, Type = InfoBarSeverity.Error });
            NotifySave();
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
        catch { }
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
        catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
    }
    private void ScanSmuRange(uint start, uint end, uint step, uint offset)
    {
        matches = [];

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
    private void ApplySettings(int mode, int CommandIndex)
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
            var errorStatus = string.Empty;
            if (status != SMU.Status.OK) 
            {
                ApplyInfo += "\n" + "SMUErrorText".GetLocalized() + ": " + (textBoxCMD.Text.Contains("0x") ? textBoxCMD.Text : "0x" + textBoxCMD.Text)
                    + "Param_SMU_Args_From".GetLocalized() + comboBoxMailboxSelect.SelectedValue
                    + "Param_SMU_Args".GetLocalized() + (textBoxARG0.Text.Contains("0x") ? textBoxARG0.Text : "0x" + textBoxARG0.Text);
                if (status == SMU.Status.CMD_REJECTED_PREREQ)
                {
                    ApplyInfo += "\n" + "SMUErrorRejected".GetLocalized();
                    //await Send_Message("SMUErrorText".GetLocalized(), "SMUErrorRejected".GetLocalized(), Symbol.Dislike);
                }
                else
                {
                    ApplyInfo += "\n" + "SMUErrorNoCMD".GetLocalized();
                    //await Send_Message("SMUErrorText".GetLocalized(), "SMUErrorNoCMD".GetLocalized(), Symbol.Filter);
                } 
            } 
        }
        catch
        {
            ApplyInfo += "\n" + "SMUErrorDesc".GetLocalized();
            //await Send_Message("SMUErrorText".GetLocalized(), "SMUErrorDesc".GetLocalized(), Symbol.Dislike);
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
        if (EnableSMU.IsOn) { profile[indexprofile].smuEnabled = true; ProfileSave(); }
        else { profile[indexprofile].smuEnabled = false; ProfileSave(); }
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
        catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
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
                            smusettings.MailBoxes = [];
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
                            smusettings.QuickSMUCommands ??= [];
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
        catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
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
        catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
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
                            smusettings.MailBoxes = [];
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
        catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
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
        catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
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
            catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
        }
        else
        {
            try
            {
                var decimalValue = int.Parse(textBoxARG0.Text);
                var hexValue = decimalValue.ToString("X");
                textBoxARG0.Text = hexValue;
            }
            catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
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
        ProfileLoad();
        var check = c1.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].cpu1 = check; profile[indexprofile].cpu1value = c1v.Value; ProfileSave(); }
    }
    //Лимит CPU (W)
    private void C2_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = c2.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].cpu2 = check; profile[indexprofile].cpu2value = c2v.Value; ProfileSave(); }
    }
    //Реальный CPU (W)
    private void C3_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = c3.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].cpu3 = check; profile[indexprofile].cpu3value = c3v.Value; ProfileSave(); }
    }
    //Средний CPU (W)
    private void C4_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = c4.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].cpu4 = check; profile[indexprofile].cpu4value = c4v.Value; ProfileSave(); }
    }
    //Тик быстрого разгона (S)
    private void C5_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = c5.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].cpu5 = check; profile[indexprofile].cpu5value = c5v.Value; ProfileSave(); }
    }
    //Тик медленного разгона (S)
    private void C6_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = c6.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].cpu6 = check; profile[indexprofile].cpu6value = c6v.Value; ProfileSave(); }
    }
    //Параметры VRM
    //Максимальный ток VRM A
    private void V1_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = V1.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].vrm1 = check; profile[indexprofile].vrm1value = V1V.Value; ProfileSave(); }
    }
    //Лимит по току VRM A
    private void V2_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = V2.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].vrm2 = check; profile[indexprofile].vrm2value = V2V.Value; ProfileSave(); }
    }
    //Максимальный ток SOC A
    private void V3_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = V3.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].vrm3 = check; profile[indexprofile].vrm3value = V3V.Value; ProfileSave(); }
    }
    //Лимит по току SOC A
    private void V4_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = V4.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].vrm4 = check; profile[indexprofile].vrm4value = V4V.Value; ProfileSave(); }
    }
    //Максимальный ток PCI VDD A
    private void V5_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = V5.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].vrm5 = check; profile[indexprofile].vrm5value = V5V.Value; ProfileSave(); }
    }
    //Максимальный ток PCI SOC A
    private void V6_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = V6.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].vrm6 = check; profile[indexprofile].vrm6value = V6V.Value; ProfileSave(); }
    }
    //Отключить троттлинг на время
    private void V7_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = V7.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].vrm7 = check; profile[indexprofile].vrm7value = V7V.Value; ProfileSave(); }
    }
    //Параметры графики
    //Минимальная частота SOC 
    private void G1_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = g1.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu1 = check; profile[indexprofile].gpu1value = g1v.Value; ProfileSave(); }
    }
    //Максимальная частота SOC
    private void G2_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = g2.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu2 = check; profile[indexprofile].gpu2value = g2v.Value; ProfileSave(); }
    }
    //Минимальная частота Infinity Fabric
    private void G3_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = g3.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu3 = check; profile[indexprofile].gpu3value = g3v.Value; ProfileSave(); }
    }
    //Максимальная частота Infinity Fabric
    private void G4_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = g4.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu4 = check; profile[indexprofile].gpu4value = g4v.Value; ProfileSave(); }
    }
    //Минимальная частота кодека VCE
    private void G5_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = g5.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu5 = check; profile[indexprofile].gpu5value = g5v.Value; ProfileSave(); }
    }
    //Максимальная частота кодека VCE
    private void G6_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = g6.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu6 = check; profile[indexprofile].gpu6value = g6v.Value; ProfileSave(); }
    }
    //Минимальная частота частота Data Latch
    private void G7_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = g7.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu7 = check; profile[indexprofile].gpu7value = g7v.Value; ProfileSave(); }
    }
    //Максимальная частота Data Latch
    private void G8_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = g8.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu8 = check; profile[indexprofile].gpu8value = g8v.Value; ProfileSave(); }
    }
    //Минимальная частота iGpu
    private void G9_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = g9.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu9 = check; profile[indexprofile].gpu9value = g9v.Value; ProfileSave(); }
    }
    //Максимальная частота iGpu
    private void G10_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = g10.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu10 = check; profile[indexprofile].gpu10value = g10v.Value; ProfileSave(); }
    }
    //Расширенные параметры
    private void A1_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = a1.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd1 = check; profile[indexprofile].advncd1value = a1v.Value; ProfileSave(); }
    }
    private void A2_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = a2.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd2 = check; profile[indexprofile].advncd2value = a2v.Value; ProfileSave(); }
    }
    private void A3_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = a3.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd3 = check; profile[indexprofile].advncd3value = a3v.Value; ProfileSave(); }
    }
    private void A4_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = a4.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd4 = check; profile[indexprofile].advncd4value = a4v.Value; ProfileSave(); }
    }
    private void A5_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = a5.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd5 = check; profile[indexprofile].advncd5value = a5v.Value; ProfileSave(); }
    }
    private void A6_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = a6.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd6 = check; profile[indexprofile].advncd6value = a6v.Value; ProfileSave(); }
    }
    private void A7_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = a7.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd7 = check; profile[indexprofile].advncd7value = a7v.Value; ProfileSave(); }
    }
    private void A8_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = a8.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd8 = check; profile[indexprofile].advncd8value = a8v.Value; ProfileSave(); }
    }
    private void A9_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = a9.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd9 = check; profile[indexprofile].advncd9value = a9v.Value; ProfileSave(); }
    }
    private void A10_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = a10.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd10 = check; profile[indexprofile].advncd10value = a10v.Value; ProfileSave(); }
    }
    private void A11_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = a11.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd11 = check; profile[indexprofile].advncd11value = a11v.Value; ProfileSave(); }
    }
    private void A12_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = a12.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd12 = check; profile[indexprofile].advncd12value = a12v.Value; ProfileSave(); }
    }
    private void A13_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = a13.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd13 = check; profile[indexprofile].advncd1value = a13m.SelectedIndex; ProfileSave(); }
    }
    //Оптимизатор кривой
    private void CCD2_8_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = CCD2_8.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].coper15 = check; profile[indexprofile].coper15value = CCD2_8v.Value; ProfileSave(); }
    }
    private void CCD2_7_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = CCD2_7.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].coper14 = check; profile[indexprofile].coper14value = CCD2_7v.Value; ProfileSave(); }
    }
    private void CCD2_6_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = CCD2_6.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].coper13 = check; profile[indexprofile].coper13value = CCD2_6v.Value; ProfileSave(); }
    }
    private void CCD2_5_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = CCD2_5.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].coper12 = check; profile[indexprofile].coper12value = CCD2_5v.Value; ProfileSave(); }
    }
    private void CCD2_4_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = CCD2_4.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].coper11 = check; profile[indexprofile].coper11value = CCD2_4v.Value; ProfileSave(); }
    }
    private void CCD2_3_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = CCD2_3.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].coper10 = check; profile[indexprofile].coper10value = CCD2_3v.Value; ProfileSave(); }
    }
    private void CCD2_2_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = CCD2_2.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].coper9 = check; profile[indexprofile].coper9value = CCD2_2v.Value; ProfileSave(); }
    }
    private void CCD2_1_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = CCD2_1.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].coper8 = check; profile[indexprofile].coper8value = CCD2_1v.Value; ProfileSave(); }
    }
    private void CCD1_8_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = CCD1_8.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].coper7 = check; profile[indexprofile].coper7value = CCD1_8v.Value; ProfileSave(); }
    }
    private void CCD1_7_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = CCD1_7.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].coper6 = check; profile[indexprofile].coper6value = CCD1_7v.Value; ProfileSave(); }
    }
    private void CCD1_6_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = CCD1_6.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].coper5 = check; profile[indexprofile].coper5value = CCD1_6v.Value; ProfileSave(); }
    }
    private void CCD1_5_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = CCD1_5.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].coper4 = check; profile[indexprofile].coper4value = CCD1_5v.Value; ProfileSave(); }
    }
    private void CCD1_4_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = CCD1_4.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].coper3 = check; profile[indexprofile].coper3value = CCD1_4v.Value; ProfileSave(); }
    }
    private void CCD1_3_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = CCD1_3.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].coper2 = check; profile[indexprofile].coper2value = CCD1_3v.Value; ProfileSave(); }
    }
    private void CCD1_2_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = CCD1_2.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].coper1 = check; profile[indexprofile].coper1value = CCD1_2v.Value; ProfileSave(); }
    }
    private void CCD1_1_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = CCD1_1.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].coper0 = check; profile[indexprofile].coper0value = CCD1_1v.Value; ProfileSave(); }
    }
    private void O1_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = O1.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].coall = check; profile[indexprofile].coallvalue = O1v.Value; ProfileSave(); }
    }
    private void O2_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = O2.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].cogfx = check; profile[indexprofile].cogfxvalue = O2v.Value; ProfileSave(); }
    }
    private void CCD_CO_Mode_Sel_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        if (CCD_CO_Mode.SelectedIndex > 0 && CCD_CO_Mode_Sel.IsChecked == true)
        {
            LockUselessParameters(true); //Оставить параметры изменения кривой
        }
        else
        {
            LockUselessParameters(false); //Убрать параметры
        }
        ProfileLoad();
        var check = CCD_CO_Mode_Sel.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].comode = check; profile[indexprofile].coprefmode = CCD_CO_Mode.SelectedIndex; ProfileSave(); }
    }

    //Параметры процессора, при изменении слайдеров
    //Максимальная температура CPU (C)
    private void C1_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].cpu1value = c1v.Value; ProfileSave(); }
    }
    //Лимит CPU (W)
    private void C2_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].cpu2value = c2v.Value; ProfileSave(); }
    }
    //Реальный CPU (W)
    private void C3_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].cpu3value = c3v.Value; ProfileSave(); }
    }
    //Средний CPU(W)
    private void C4_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].cpu4value = c4v.Value; ProfileSave(); }
    }
    //Тик быстрого разгона (S)
    private void C5_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].cpu5value = c5v.Value; ProfileSave(); }
    }
    //Тик медленного разгона (S)
    private void C6_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].cpu6value = c6v.Value; ProfileSave(); }
    }
    //Параметры VRM
    private void V1v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].vrm1value = V1V.Value; ProfileSave(); }
    }
    private void V2v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].vrm2value = V2V.Value; ProfileSave(); }
    }
    private void V3v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].vrm3value = V3V.Value; ProfileSave(); }
    }
    private void V4v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].vrm4value = V4V.Value; ProfileSave(); }
    }
    private void V5v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].vrm5value = V5V.Value; ProfileSave(); }
    }
    private void V6v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].vrm6value = V6V.Value; ProfileSave(); }
    }
    private void V7v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].vrm7value = V7V.Value; ProfileSave(); }
    }
    //Параметры GPU
    private void G1v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].gpu1value = g1v.Value; ProfileSave(); }
    }
    private void G2v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].gpu2value = g2v.Value; ProfileSave(); }
    }
    private void G3v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].gpu3value = g3v.Value; ProfileSave(); }
    }
    private void G4v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].gpu4value = g4v.Value; ProfileSave(); }
    }
    private void G5v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].gpu5value = g5v.Value; ProfileSave(); }
    }
    private void G6v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].gpu6value = g6v.Value; ProfileSave(); }
    }
    private void G7v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].gpu7value = g7v.Value; ProfileSave(); }
    }
    private void G8v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].gpu8value = g8v.Value; ProfileSave(); }
    }
    private void G9v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].gpu9value = g9v.Value; ProfileSave(); }
    }
    private void G10v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].gpu10value = g10v.Value; ProfileSave(); }
    }
    //Расширенные параметры
    private void A1v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].advncd1value = a1v.Value; ProfileSave(); }
    }
    private void A2v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].advncd2value = a2v.Value; ProfileSave(); }
    }
    private void A3v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].advncd3value = a3v.Value; ProfileSave(); }
    }
    private void A4v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].advncd4value = a4v.Value; ProfileSave(); }
    }
    private void A5v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].advncd5value = a5v.Value; ProfileSave(); }
    }
    private void A6v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].advncd6value = a6v.Value; ProfileSave(); }
    }
    private void A7v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].advncd7value = a7v.Value; ProfileSave(); }
    }
    private void A8v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].advncd8value = a8v.Value; ProfileSave(); }
    }
    private void A9v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].advncd9value = a9v.Value; ProfileSave(); }
    }
    private void A10v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].advncd10value = a10v.Value; ProfileSave(); }
    }
    private void A11v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].advncd11value = a11v.Value; ProfileSave(); }
    }
    private void A12v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].advncd12value = a12v.Value; ProfileSave(); }
    }
    private void A13m_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].advncd13value = a13m.SelectedIndex; ProfileSave(); }
    }
    //Новые
    private void C7_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = c7.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].cpu7 = check; profile[indexprofile].cpu7value = c7v.Value; ProfileSave(); }
    }
    private void C7_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].cpu7value = c7v.Value; ProfileSave(); }
    }
    private void G11_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = g11.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu11 = check; profile[indexprofile].gpu11value = g11v.Value; ProfileSave(); }
    }
    private void G11v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].gpu11value = g11v.Value; ProfileSave(); }
    }
    private void G12_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = g12.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu12 = check; profile[indexprofile].gpu12value = g12v.Value; ProfileSave(); }
    }
    private void G12v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].gpu12value = g12v.Value; ProfileSave(); }
    }

    private void G15_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = g15.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu15 = check; profile[indexprofile].gpu15value = g15m.SelectedIndex; ProfileSave(); }
    }
    private void G15m_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].gpu15value = g15m.SelectedIndex; ProfileSave(); }
    }
    private void G16_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = g16.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].gpu16 = check; profile[indexprofile].gpu16value = g16m.SelectedIndex; ProfileSave(); }
    }
    private void G16m_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].gpu16value = g16m.SelectedIndex; ProfileSave(); }
    }
    private void A14_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = a14.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd14 = check; profile[indexprofile].advncd14value = a14m.SelectedIndex; ProfileSave(); }
    }
    private void A14m_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].advncd14value = a14m.SelectedIndex; ProfileSave(); }
    }
    private void A15_Checked(object sender, RoutedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        var check = a15.IsChecked == true;
        if (indexprofile != -1) { profile[indexprofile].advncd15 = check; profile[indexprofile].advncd15value = a15v.Value; ProfileSave(); }
    }
    private void A15v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].advncd15value = a15v.Value; ProfileSave(); }
    }
    //Слайдеры из оптимизатора кривой 
    private void O1v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].coallvalue = O1v.Value; ProfileSave(); }
    }
    private void O2v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].cogfxvalue = O2v.Value; ProfileSave(); }
    }
    private void CCD1_1v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].coper0value = CCD1_1v.Value; ProfileSave(); }
    }
    private void CCD1_2v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].coper1value = CCD1_2v.Value; ProfileSave(); }
    }
    private void CCD1_3v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].coper2value = CCD1_3v.Value; ProfileSave(); }
    }
    private void CCD1_4v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].coper3value = CCD1_4v.Value; ProfileSave(); }
    }
    private void CCD1_5v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].coper4value = CCD1_5v.Value; ProfileSave(); }
    }
    private void CCD1_6v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].coper5value = CCD1_6v.Value; ProfileSave(); }
    }
    private void CCD1_7v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].coper6value = CCD1_7v.Value; ProfileSave(); }
    }
    private void CCD1_8v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].coper7value = CCD1_8v.Value; ProfileSave(); }
    }
    private void CCD2_1v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].coper8value = CCD2_1v.Value; ProfileSave(); }
    }
    private void CCD2_2v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].coper9value = CCD2_2v.Value; ProfileSave(); }
    }
    private void CCD2_3v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].coper10value = CCD2_3v.Value; ProfileSave(); }
    }
    private void CCD2_4v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].coper11value = CCD2_4v.Value; ProfileSave(); }
    }
    private void CCD2_5v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].coper12value = CCD2_5v.Value; ProfileSave(); }
    }
    private void CCD2_6v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].coper13value = CCD2_6v.Value; ProfileSave(); }
    }
    private void CCD2_7v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].coper14value = CCD2_7v.Value; ProfileSave(); }
    }
    private void CCD2_8v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].coper15value = CCD2_8v.Value; ProfileSave(); }
    }
    private void CCD_CO_Mode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isLoaded == false || waitforload) { return; }
        if (CCD_CO_Mode.SelectedIndex > 0 && CCD_CO_Mode_Sel.IsChecked == true)
        {
            LockUselessParameters(true); //Оставить параметры изменения кривой
        }
        else
        {
            LockUselessParameters(false); //Убрать параметры
        }
        ProfileLoad();
        if (indexprofile != -1) { profile[indexprofile].coprefmode = CCD_CO_Mode.SelectedIndex; ProfileSave(); }
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
        if (O1.IsChecked == true)
        {
            if (O1v.Value >= 0.0)
            {
                adjline += $" --set-coall={O1v.Value} ";
            }
            else
            {
                adjline += $" --set-coall={Convert.ToUInt32(0x100000 - (uint)(-1 * (int)O1v.Value))} ";
            }
        }
        if (O2.IsChecked == true)
        {
            cpu!.smu.Rsmu.SMU_MSG_SetDldoPsmMargin = SendSMUCommand.ReturnCoGFX(cpu.info.codeName);
            //Using Irusanov method
            for (var i = 0; i < cpu?.info.topology.physicalCores; i++)
            {
                if ((~cpu.info.topology.coreDisableMap.Length >> i & 1) == 1)
                {
                    if (cpu.smu.Rsmu.SMU_MSG_SetDldoPsmMargin != 0U)
                    {
                        cpu.SetPsmMarginSingleCore(GetCoreMask(i), Convert.ToInt32(O2v.Value));
                    }
                }
            }
            cpu!.smu.Rsmu.SMU_MSG_SetDldoPsmMargin = SendSMUCommand.ReturnCoPer(cpu.info.codeName);
        }
        if (CCD_CO_Mode_Sel.IsChecked == true && CCD_CO_Mode.SelectedIndex != 0)
        { //Если пользователь выбрал хотя-бы один режим и ...
            if (CCD_CO_Mode.SelectedIndex == 1) //Если выбран режим ноутбук
            {
                if (cpu?.info.codeName == Cpu.CodeName.DragonRange) //Так как там как у компьютеров
                {
                    if (CCD1_1.IsChecked == true) { adjline += $" --set-coper={((0 << 4 | 0 % 1 & 15) << 4 | 0 % 8 & 15) << 20 | ((int)CCD1_1v.Value & 0xFFFF)} "; }
                    if (CCD1_2.IsChecked == true) { adjline += $" --set-coper={((0 << 4 | 0 % 1 & 15) << 4 | 1 % 8 & 15) << 20 | ((int)CCD1_2v.Value & 0xFFFF)} "; }
                    if (CCD1_3.IsChecked == true) { adjline += $" --set-coper={((0 << 4 | 0 % 1 & 15) << 4 | 2 % 8 & 15) << 20 | ((int)CCD1_3v.Value & 0xFFFF)} "; }
                    if (CCD1_4.IsChecked == true) { adjline += $" --set-coper={((0 << 4 | 0 % 1 & 15) << 4 | 3 % 8 & 15) << 20 | ((int)CCD1_4v.Value & 0xFFFF)} "; }
                    if (CCD1_5.IsChecked == true) { adjline += $" --set-coper={((0 << 4 | 0 % 1 & 15) << 4 | 4 % 8 & 15) << 20 | ((int)CCD1_5v.Value & 0xFFFF)} "; }
                    if (CCD1_6.IsChecked == true) { adjline += $" --set-coper={((0 << 4 | 0 % 1 & 15) << 4 | 5 % 8 & 15) << 20 | ((int)CCD1_6v.Value & 0xFFFF)} "; }
                    if (CCD1_7.IsChecked == true) { adjline += $" --set-coper={((0 << 4 | 0 % 1 & 15) << 4 | 6 % 8 & 15) << 20 | ((int)CCD1_7v.Value & 0xFFFF)} "; }
                    if (CCD1_8.IsChecked == true) { adjline += $" --set-coper={((0 << 4 | 0 % 1 & 15) << 4 | 7 % 8 & 15) << 20 | ((int)CCD1_8v.Value & 0xFFFF)} "; }

                    if (CCD2_1.IsChecked == true) { adjline += $" --set-coper={((1 << 4 | 0 % 1 & 15) << 4 | 0 % 8 & 15) << 20 | ((int)CCD2_1v.Value & 0xFFFF)} "; }
                    if (CCD2_2.IsChecked == true) { adjline += $" --set-coper={((1 << 4 | 0 % 1 & 15) << 4 | 1 % 8 & 15) << 20 | ((int)CCD2_2v.Value & 0xFFFF)} "; }
                    if (CCD2_3.IsChecked == true) { adjline += $" --set-coper={((1 << 4 | 0 % 1 & 15) << 4 | 2 % 8 & 15) << 20 | ((int)CCD2_3v.Value & 0xFFFF)} "; }
                    if (CCD2_4.IsChecked == true) { adjline += $" --set-coper={((1 << 4 | 0 % 1 & 15) << 4 | 3 % 8 & 15) << 20 | ((int)CCD2_4v.Value & 0xFFFF)} "; }
                    if (CCD2_5.IsChecked == true) { adjline += $" --set-coper={((1 << 4 | 0 % 1 & 15) << 4 | 4 % 8 & 15) << 20 | ((int)CCD2_5v.Value & 0xFFFF)} "; }
                    if (CCD2_6.IsChecked == true) { adjline += $" --set-coper={((1 << 4 | 0 % 1 & 15) << 4 | 5 % 8 & 15) << 20 | ((int)CCD2_6v.Value & 0xFFFF)} "; }
                    if (CCD2_7.IsChecked == true) { adjline += $" --set-coper={((1 << 4 | 0 % 1 & 15) << 4 | 6 % 8 & 15) << 20 | ((int)CCD2_7v.Value & 0xFFFF)} "; }
                    if (CCD2_8.IsChecked == true) { adjline += $" --set-coper={((1 << 4 | 0 % 1 & 15) << 4 | 7 % 8 & 15) << 20 | ((int)CCD2_8v.Value & 0xFFFF)} "; }
                }
                else
                {
                    if (CCD1_1.IsChecked == true) { adjline += $" --set-coper={(0 << 20) | ((int)CCD1_1v.Value & 0xFFFF)} "; }
                    if (CCD1_2.IsChecked == true) { adjline += $" --set-coper={(1 << 20) | ((int)CCD1_2v.Value & 0xFFFF)} "; }
                    if (CCD1_3.IsChecked == true) { adjline += $" --set-coper={(2 << 20) | ((int)CCD1_3v.Value & 0xFFFF)} "; }
                    if (CCD1_4.IsChecked == true) { adjline += $" --set-coper={(3 << 20) | ((int)CCD1_4v.Value & 0xFFFF)} "; }
                    if (CCD1_5.IsChecked == true) { adjline += $" --set-coper={(4 << 20) | ((int)CCD1_5v.Value & 0xFFFF)} "; }
                    if (CCD1_6.IsChecked == true) { adjline += $" --set-coper={(5 << 20) | ((int)CCD1_6v.Value & 0xFFFF)} "; }
                    if (CCD1_7.IsChecked == true) { adjline += $" --set-coper={(6 << 20) | ((int)CCD1_7v.Value & 0xFFFF)} "; }
                    if (CCD1_8.IsChecked == true) { adjline += $" --set-coper={(7 << 20) | ((int)CCD1_8v.Value & 0xFFFF)} "; }
                }
            }
            else if (CCD_CO_Mode.SelectedIndex == 2) //Если выбран режим компьютер
            {
                if (CCD1_1.IsChecked == true) { adjline += $" --set-coper={((0 << 4 | 0 % 1 & 15) << 4 | 0 % 8 & 15) << 20 | ((int)CCD1_1v.Value & 0xFFFF)} "; }
                if (CCD1_2.IsChecked == true) { adjline += $" --set-coper={((0 << 4 | 0 % 1 & 15) << 4 | 1 % 8 & 15) << 20 | ((int)CCD1_2v.Value & 0xFFFF)} "; }
                if (CCD1_3.IsChecked == true) { adjline += $" --set-coper={((0 << 4 | 0 % 1 & 15) << 4 | 2 % 8 & 15) << 20 | ((int)CCD1_3v.Value & 0xFFFF)} "; }
                if (CCD1_4.IsChecked == true) { adjline += $" --set-coper={((0 << 4 | 0 % 1 & 15) << 4 | 3 % 8 & 15) << 20 | ((int)CCD1_4v.Value & 0xFFFF)} "; }
                if (CCD1_5.IsChecked == true) { adjline += $" --set-coper={((0 << 4 | 0 % 1 & 15) << 4 | 4 % 8 & 15) << 20 | ((int)CCD1_5v.Value & 0xFFFF)} "; }
                if (CCD1_6.IsChecked == true) { adjline += $" --set-coper={((0 << 4 | 0 % 1 & 15) << 4 | 5 % 8 & 15) << 20 | ((int)CCD1_6v.Value & 0xFFFF)} "; }
                if (CCD1_7.IsChecked == true) { adjline += $" --set-coper={((0 << 4 | 0 % 1 & 15) << 4 | 6 % 8 & 15) << 20 | ((int)CCD1_7v.Value & 0xFFFF)} "; }
                if (CCD1_8.IsChecked == true) { adjline += $" --set-coper={7340032 | ((int)CCD1_8v.Value! & 0xFFFF)} "; }

                if (CCD2_1.IsChecked == true) { adjline += $" --set-coper={((1 << 4 | 0 % 1 & 15) << 4 | 0 % 8 & 15) << 20 | ((int)CCD2_1v.Value & 0xFFFF)} "; }
                if (CCD2_2.IsChecked == true) { adjline += $" --set-coper={((1 << 4 | 0 % 1 & 15) << 4 | 1 % 8 & 15) << 20 | ((int)CCD2_2v.Value & 0xFFFF)} "; }
                if (CCD2_3.IsChecked == true) { adjline += $" --set-coper={((1 << 4 | 0 % 1 & 15) << 4 | 2 % 8 & 15) << 20 | ((int)CCD2_3v.Value & 0xFFFF)} "; }
                if (CCD2_4.IsChecked == true) { adjline += $" --set-coper={((1 << 4 | 0 % 1 & 15) << 4 | 3 % 8 & 15) << 20 | ((int)CCD2_4v.Value & 0xFFFF)} "; }
                if (CCD2_5.IsChecked == true) { adjline += $" --set-coper={((1 << 4 | 0 % 1 & 15) << 4 | 4 % 8 & 15) << 20 | ((int)CCD2_5v.Value & 0xFFFF)} "; }
                if (CCD2_6.IsChecked == true) { adjline += $" --set-coper={((1 << 4 | 0 % 1 & 15) << 4 | 5 % 8 & 15) << 20 | ((int)CCD2_6v.Value & 0xFFFF)} "; }
                if (CCD2_7.IsChecked == true) { adjline += $" --set-coper={((1 << 4 | 0 % 1 & 15) << 4 | 6 % 8 & 15) << 20 | ((int)CCD2_7v.Value & 0xFFFF)} "; }
                if (CCD2_8.IsChecked == true) { adjline += $" --set-coper={((1 << 4 | 0 % 1 & 15) << 4 | 7 % 8 & 15) << 20 | ((int)CCD2_8v.Value & 0xFFFF)} "; }
            }
            else if (CCD_CO_Mode.SelectedIndex == 3) //Если выбран режим с использованием метода от Ирусанова, Irusanov, https://github.com/irusanov
            {
                cpu!.smu.Rsmu.SMU_MSG_SetDldoPsmMargin = SendSMUCommand.ReturnCoPer(cpu.info.codeName);
                //Using Irusanov method
                for (var i = 0; i < cpu?.info.topology.physicalCores; i++)
                {
                    var checkbox = i < 8 ? (CheckBox)CCD1_Grid.FindName($"CCD1_{i}") : (CheckBox)CCD1_Grid.FindName($"CCD2_{i}");
                    if (checkbox != null && checkbox.IsChecked == true)
                    {
                        var setVal = i < 8 ? (Slider)CCD1_Grid.FindName($"CCD1_{i}v") : (Slider)CCD2_Grid.FindName($"CCD2_{i}v");
                        if ((~cpu.info.topology.coreDisableMap.Length >> i & 1) == 1)
                        {
                            if (cpu.smu.Rsmu.SMU_MSG_SetDldoPsmMargin != 0U)
                            {
                                cpu.SetPsmMarginSingleCore(GetCoreMask(i), Convert.ToInt32(setVal.Value));
                            }
                        }
                    }
                }
            }
        }
        if (SMU_Func_Enabl.IsOn)
        {
            if (Bit_0_FEATURE_CCLK_CONTROLLER.IsOn)
            {
                adjline += " --enable-feature=1";
            }
            else
            {
                adjline += " --disable-feature=1";
            }
            if (Bit_2_FEATURE_DATA_CALCULATION.IsOn)
            {
                adjline += " --enable-feature=4";
            }
            else
            {
                adjline += " --disable-feature=4";
            }
            if (Bit_3_FEATURE_PPT.IsOn)
            {
                adjline += " --enable-feature=8";
            }
            else
            {
                adjline += " --disable-feature=8";
            }
            if (Bit_4_FEATURE_TDC.IsOn)
            {
                adjline += " --enable-feature=16";
            }
            else
            {
                adjline += " --disable-feature=16";
            }
            if (Bit_5_FEATURE_THERMAL.IsOn)
            {
                adjline += " --enable-feature=32";
            }
            else
            {
                adjline += " --disable-feature=32";
            }
            if (Bit_8_FEATURE_PLL_POWER_DOWN.IsOn)
            {
                adjline += " --enable-feature=256";
            }
            else
            {
                adjline += " --disable-feature=256";
            }
            if (Bit_37_FEATURE_PROCHOT.IsOn)
            {
                adjline += " --enable-feature=0,32";
            }
            else
            {
                adjline += " --disable-feature=0,32";
            }
            if (Bit_39_FEATURE_STAPM.IsOn)
            {
                adjline += " --enable-feature=0,128";
            }
            else
            {
                adjline += " --disable-feature=0,128";
            }
            if (Bit_40_FEATURE_CORE_CSTATES.IsOn)
            {
                adjline += " --enable-feature=0,256";
            }
            else
            {
                adjline += " --disable-feature=0,256";
            }
            if (Bit_41_FEATURE_GFX_DUTY_CYCLE.IsOn)
            {
                adjline += " --enable-feature=0,512";
            }
            else
            {
                adjline += " --disable-feature=0,512";
            }
            if (Bit_42_FEATURE_AA_MODE.IsOn)
            {
                adjline += " --enable-feature=0,1024";
            }
            else
            {
                adjline += " --disable-feature=0,1024";
            }
        }
        ConfigLoad();
        config.RyzenADJline = adjline + " ";
        adjline = "";
        ApplyInfo = "";
        ConfigSave();
        SendSMUCommand.Codename = cpu!.info.codeName;
        MainWindow.Applyer.Apply(config.RyzenADJline, true, config.ReapplyOverclock, config.ReapplyOverclockTimer);
        if (EnablePstates.IsOn) { BtnPstateWrite_Click(); }
        if (textBoxARG0 != null &&
           textBoxARGAddress != null &&
           textBoxCMD != null &&
           textBoxCMDAddress != null &&
           textBoxRSPAddress != null &&
           EnableSMU.IsOn)
        {
            ApplySettings(0, 0);
        }
        await Task.Delay(1000);
        var timer = 1000;
        if (ApplyInfo != null)
        {
            timer *= ApplyInfo.Split('\n').Length + 1;
        }
        if (SettingsViewModel.VersionId != 5) // Если версия не Debug Lanore
        {
            Apply_tooltip.Title = "Apply_Success".GetLocalized(); Apply_tooltip.Subtitle = "Apply_Success_Desc".GetLocalized();
        }
        else
        {
#pragma warning disable CS0162 // Обнаружен недостижимый код
            Apply_tooltip.Title = "Apply_Success".GetLocalized(); Apply_tooltip.Subtitle = "Apply_Success_Desc".GetLocalized() + config.RyzenADJline;
#pragma warning restore CS0162 // Обнаружен недостижимый код
        }
        Apply_tooltip.IconSource = new SymbolIconSource { Symbol = Symbol.Accept };
        Apply_tooltip.IsOpen = true; 
        var infoSet = InfoBarSeverity.Success;
        if (ApplyInfo != string.Empty && 
            ApplyInfo != null) 
        { 
            Apply_tooltip.Title = "Apply_Warn".GetLocalized(); 
            Apply_tooltip.Subtitle = "Apply_Warn_Desc".GetLocalized() + ApplyInfo; 
            Apply_tooltip.IconSource = new SymbolIconSource { Symbol = Symbol.ReportHacked }; 
            await Task.Delay(timer); 
            Apply_tooltip.IsOpen = false; 
            infoSet = InfoBarSeverity.Warning; 
        }
        else 
        { 
            await Task.Delay(3000); 
            Apply_tooltip.IsOpen = false; 
        }
        NotifyLoad();
        notify.Notifies ??= [];
        notify.Notifies.Add(new Notify 
        { 
            Title = Apply_tooltip.Title, 
            Msg = Apply_tooltip.Subtitle, 
            Type = infoSet 
        });
        NotifySave(); 
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
                if (profile == null) 
                {
                    profile = new Profile[1];
                    profile[0] = new Profile() { profilename = SaveProfileN.Text };
                }
                else
                {
                    var profileList = new List<Profile>(profile)
                    {
                        new()
                        { 
                            profilename = SaveProfileN.Text
                        }
                    };
                    profile = [.. profileList];
                } 
                waitforload = false;
                NotifyLoad();
                notify.Notifies ??= [];
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
            notify.Notifies ??= [];
            notify.Notifies.Add(new Notify { Title = Add_tooltip_Error.Title, Msg = Add_tooltip_Error.Subtitle, Type = InfoBarSeverity.Error });
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
                ProfileCOM.Items.Add(new ComboBoxItem()
                {
                    Content = new TextBlock
                    {
                        Text = "Param_Premaded".GetLocalized(),
                        Foreground = (Microsoft.UI.Xaml.Media.Brush)App.Current.Resources["AccentTextFillColorTertiaryBrush"]
                    },
                    IsEnabled = false
                });
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
                notify.Notifies ??= [];
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
            notify.Notifies ??= [];
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
                notify.Notifies ??= [];
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
                profile = [.. profileList];
                indexprofile = 0;
                waitforload = false;
                ProfileCOM.SelectedIndex = 0;
                NotifyLoad();
                notify.Notifies ??= [];
                notify.Notifies.Add(new Notify { Title = "DeleteSuccessTitle".GetLocalized(), Msg = "DeleteSuccessDesc".GetLocalized(), Type = InfoBarSeverity.Success });
                NotifySave();
            }
            ProfileSave();
        }
    }
    private void SMU_Func_Click(object sender, RoutedEventArgs e) => Save_SMUFunctions(true);
    private void SMU_Func_Enabl_Toggled(object sender, RoutedEventArgs e) => Save_SMUFunctions(false);
    private void FEATURE_CCLK_CONTROLLER_Click(object sender, RoutedEventArgs e) => Save_SMUFeatureCCLK(true);
    private void Bit_0_FEATURE_CCLK_CONTROLLER_Toggled(object sender, RoutedEventArgs e) => Save_SMUFeatureCCLK(false);
    private void FEATURE_DATA_CALCULATION_Click(object sender, RoutedEventArgs e) => Save_SMUFeatureData(true);
    private void Bit_2_FEATURE_DATA_CALCULATION_Toggled(object sender, RoutedEventArgs e) => Save_SMUFeatureData(false);
    private void FEATURE_PPT_Click(object sender, RoutedEventArgs e) => Save_SMUFeaturePPT(true);
    private void Bit_3_FEATURE_PPT_Toggled(object sender, RoutedEventArgs e) => Save_SMUFeaturePPT(false);
    private void FEATURE_TDC_Click(object sender, RoutedEventArgs e) => Save_SMUFeatureTDC(true);
    private void Bit_4_FEATURE_TDC_Toggled(object sender, RoutedEventArgs e) => Save_SMUFeatureTDC(false);
    private void Bit_5_FEATURE_THERMAL_Toggled(object sender, RoutedEventArgs e) => Save_SMUFeatureThermal(false);
    private void FEATURE_THERMAL_Click(object sender, RoutedEventArgs e) => Save_SMUFeatureThermal(true);
    private void Bit_8_FEATURE_PLL_POWER_DOWN_Toggled(object sender, RoutedEventArgs e) => Save_SMUFeaturePowerDown(false);
    private void FEATURE_PLL_POWER_DOWN_Click(object sender, RoutedEventArgs e) => Save_SMUFeaturePowerDown(true);
    private void FEATURE_PROCHOT_Click(object sender, RoutedEventArgs e) => Save_SMUFeatureProchot(true);
    private void Bit_37_FEATURE_PROCHOT_Toggled(object sender, RoutedEventArgs e) => Save_SMUFeatureProchot(false);
    private void FEATURE_STAPM_Click(object sender, RoutedEventArgs e) => Save_SMUFeatureSTAPM(true);
    private void Bit_39_FEATURE_STAPM_Toggled(object sender, RoutedEventArgs e) => Save_SMUFeatureSTAPM(false);
    private void FEATURE_CORE_CSTATES_Click(object sender, RoutedEventArgs e) => Save_SMUFeatureCStates(true);
    private void Bit_40_FEATURE_CORE_CSTATES_Toggled(object sender, RoutedEventArgs e) => Save_SMUFeatureCStates(false);
    private void FEATURE_GFX_DUTY_CYCLE_Click(object sender, RoutedEventArgs e) => Save_SMUFeatureGFXDutyCycle(true);
    private void Bit_41_FEATURE_GFX_DUTY_CYCLE_Toggled(object sender, RoutedEventArgs e) => Save_SMUFeatureGFXDutyCycle(false);
    private void FEATURE_AA_MODE_Click(object sender, RoutedEventArgs e) => Save_SMUFeaturAplusA(true);
    private void Bit_42_FEATURE_AA_MODE_Toggled(object sender, RoutedEventArgs e) => Save_SMUFeaturAplusA(false);
    private void Save_SMUFunctions(bool isButton)
    {
        if (!isLoaded) { return; }
        if (isButton) { SMU_Func_Enabl.IsOn = SMU_Func_Enabl.IsOn != true; }
        try { ProfileLoad(); profile[ProfileCOM.SelectedIndex - 1].smuFunctionsEnabl = SMU_Func_Enabl.IsOn; ProfileSave(); } catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
    }
    private void Save_SMUFeatureCCLK(bool isButton)
    {
        if (!isLoaded) { return; }
        if (isButton) { Bit_0_FEATURE_CCLK_CONTROLLER.IsOn = Bit_0_FEATURE_CCLK_CONTROLLER.IsOn != true; }
        try { ProfileLoad(); profile[ProfileCOM.SelectedIndex - 1].smuFeatureCCLK = Bit_0_FEATURE_CCLK_CONTROLLER.IsOn; ProfileSave(); } catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
    }
    private void Save_SMUFeatureData(bool isButton)
    {
        if (!isLoaded) { return; }
        if (isButton) { Bit_2_FEATURE_DATA_CALCULATION.IsOn = Bit_2_FEATURE_DATA_CALCULATION.IsOn != true; }
        try { ProfileLoad(); profile[ProfileCOM.SelectedIndex - 1].smuFeatureData = Bit_2_FEATURE_DATA_CALCULATION.IsOn; ProfileSave(); } catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
    }
    private void Save_SMUFeaturePPT(bool isButton)
    {
        if (!isLoaded) { return; }
        if (isButton) { Bit_3_FEATURE_PPT.IsOn = Bit_3_FEATURE_PPT.IsOn != true; }
        try { ProfileLoad(); profile[ProfileCOM.SelectedIndex - 1].smuFeaturePPT = Bit_3_FEATURE_PPT.IsOn; ProfileSave(); } catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
    }
    private void Save_SMUFeatureTDC(bool isButton)
    {
        if (!isLoaded) { return; }
        if (isButton) { Bit_4_FEATURE_TDC.IsOn = Bit_4_FEATURE_TDC.IsOn != true; }
        try { ProfileLoad(); profile[ProfileCOM.SelectedIndex - 1].smuFeatureTDC = Bit_4_FEATURE_TDC.IsOn; ProfileSave(); } catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
    }
    private void Save_SMUFeatureThermal(bool isButton)
    {
        if (!isLoaded) { return; }
        if (isButton) { Bit_5_FEATURE_THERMAL.IsOn = Bit_5_FEATURE_THERMAL.IsOn != true; }
        try { ProfileLoad(); profile[ProfileCOM.SelectedIndex - 1].smuFeatureThermal = Bit_5_FEATURE_THERMAL.IsOn; ProfileSave(); } catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
    }
    private void Save_SMUFeaturePowerDown(bool isButton)
    {
        if (!isLoaded) { return; }
        if (isButton) { Bit_8_FEATURE_PLL_POWER_DOWN.IsOn = Bit_8_FEATURE_PLL_POWER_DOWN.IsOn != true; }
        try { ProfileLoad(); profile[ProfileCOM.SelectedIndex - 1].smuFeaturePowerDown = Bit_8_FEATURE_PLL_POWER_DOWN.IsOn; ProfileSave(); } catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
    }
    private void Save_SMUFeatureProchot(bool isButton)
    {
        if (!isLoaded) { return; }
        if (isButton) { Bit_37_FEATURE_PROCHOT.IsOn = Bit_37_FEATURE_PROCHOT.IsOn != true; }
        try { ProfileLoad(); profile[ProfileCOM.SelectedIndex - 1].smuFeatureProchot = Bit_37_FEATURE_PROCHOT.IsOn; ProfileSave(); } catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
    }
    private void Save_SMUFeatureSTAPM(bool isButton)
    {
        if (!isLoaded) { return; }
        if (isButton) { Bit_39_FEATURE_STAPM.IsOn = Bit_39_FEATURE_STAPM.IsOn != true; }
        try { ProfileLoad(); profile[ProfileCOM.SelectedIndex - 1].smuFeatureSTAPM = Bit_39_FEATURE_STAPM.IsOn; ProfileSave(); } catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
    }
    private void Save_SMUFeatureCStates(bool isButton)
    {
        if (!isLoaded) { return; }
        if (isButton) { Bit_40_FEATURE_CORE_CSTATES.IsOn = Bit_40_FEATURE_CORE_CSTATES.IsOn != true; }
        try { ProfileLoad(); profile[ProfileCOM.SelectedIndex - 1].smuFeatureCStates = Bit_40_FEATURE_CORE_CSTATES.IsOn; ProfileSave(); } catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
    }
    private void Save_SMUFeatureGFXDutyCycle(bool isButton)
    {
        if (!isLoaded) { return; }
        if (isButton) { Bit_41_FEATURE_GFX_DUTY_CYCLE.IsOn = Bit_41_FEATURE_GFX_DUTY_CYCLE.IsOn != true; }
        try { ProfileLoad(); profile[ProfileCOM.SelectedIndex - 1].smuFeatureGfxDutyCycle = Bit_41_FEATURE_GFX_DUTY_CYCLE.IsOn; ProfileSave(); } catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
    }
    private void Save_SMUFeaturAplusA(bool isButton)
    {
        if (!isLoaded) { return; }
        if (isButton) { Bit_42_FEATURE_AA_MODE.IsOn = Bit_42_FEATURE_AA_MODE.IsOn != true; }
        try { ProfileLoad(); profile[ProfileCOM.SelectedIndex - 1].smuFeatureAplusA = Bit_42_FEATURE_AA_MODE.IsOn; ProfileSave(); } catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
    }
    //NumberBoxes
    private void C2t_FocusEngaged(object sender, object args)
    {
        if (sender is NumberBox numberBox)
        {  
            numberBox.SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden;
        }
    }
    private void C2t_FocusDisengaged(object sender, object args)
    {
        if (sender is NumberBox numberBox)
        { 
            numberBox.SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline; 
        }
    }
    private void C2t_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (sender is NumberBox numberBox)
        {
            object slider;
            if (numberBox.Name.Contains('v'))
            {
                slider = FindName(numberBox.Name.Replace('t', 'V').Replace('v', 'V'));
            }
            else
            {
                try
                {
                    slider = FindName(numberBox.Name.Replace('t', 'v'));
                }
                catch (Exception ex)
                {
                    TraceIt_TraceError(ex.ToString());
                    return;
                }
            }
            if (slider is Slider slider1)
            {
                if (slider1.Maximum < numberBox.Value)
                {
                    slider1.Maximum = FromValueToUpperFive(numberBox.Value);
                }
            }
        }
    }
    #endregion
    #region PState Section related voids
    public async void BtnPstateWrite_Click()
    {
        ConfigLoad();
        profile[config.Preset].did0 = DID_0.Value;
        profile[config.Preset].did1 = DID_1.Value;
        profile[config.Preset].did2 = DID_2.Value;
        profile[config.Preset].fid0 = FID_0.Value;
        profile[config.Preset].fid1 = FID_1.Value;
        profile[config.Preset].fid2 = FID_2.Value;
        profile[config.Preset].vid0 = VID_0.Value;
        profile[config.Preset].vid1 = VID_1.Value;
        profile[config.Preset].vid2 = VID_2.Value; ProfileSave();
        if (profile[config.Preset].autoPstate)
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
                    catch (Exception ex)
                    {
                        TraceIt_TraceError(ex.ToString());
                        WritePstatesWithoutP0();
                    }
                }
            }
        }
    }
    public static void WritePstates()
    {
        try
        {
            var cpu = CpuSingleton.GetInstance();
            ConfigLoad(); ProfileLoad();
            pstatesDID[0] = profile[config.Preset].did0;
            pstatesDID[1] = profile[config.Preset].did1;
            pstatesDID[2] = profile[config.Preset].did2;
            pstatesFID[0] = profile[config.Preset].fid0;
            pstatesFID[1] = profile[config.Preset].fid1;
            pstatesFID[2] = profile[config.Preset].fid2;
            pstatesVID[0] = profile[config.Preset].vid0;
            pstatesVID[1] = profile[config.Preset].vid1;
            pstatesVID[2] = profile[config.Preset].vid2;
            for (var p = 0; p < 3; p++)
            {
                if (pstatesFID[p] == 0 || pstatesDID[p] == 0 || pstatesVID[p] == 0) 
                { 
                    ReadPstate(); MessageBox.Show("Corrupted Pstates in config","Critical Error!");
                }
                //Logic
                var pstateId = p;
                uint eax = default, edx = default;
                uint IddDiv = 0x0;
                uint IddVal = 0x0;
                uint CpuVid = 0x0;
                uint CpuDfsId = 0x0;
                uint CpuFid = 0x0;
                var Didtext = 12d;
                var Fidtext = 102d;
                var Vidtext = 56.0;
                if (cpu?.ReadMsr(Convert.ToUInt32(Convert.ToInt64(0xC0010064) + pstateId), ref eax, ref edx) == false)
                {
                    MessageBox.Show("Error reading PState! ID = " + pstateId);
                    return;
                }
                CalculatePstateDetails(eax, ref IddDiv, ref IddVal, ref CpuVid, ref CpuDfsId, ref CpuFid);
                Didtext = pstatesDID[p];
                Fidtext = pstatesFID[p];
                Vidtext = pstatesVID[p];
                eax = ((IddDiv & 0xFF) << 30) | ((IddVal & 0xFF) << 22) | ((CpuVid & 0xFF) << 14) |
                      (((uint)Math.Round(Didtext, 0) & 0xFF) << 8) | ((uint)Math.Round(Fidtext, 0) & 0xFF);
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
        catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
    }
    public void WritePstatesWithoutP0()
    {
        try
        {
            for (var p = 1; p < 3; p++)
            {
                if (string.IsNullOrEmpty(DID_1.Text) 
                    || string.IsNullOrEmpty(FID_1.Text) 
                    || string.IsNullOrEmpty(DID_2.Text) 
                    || string.IsNullOrEmpty(FID_2.Text)) 
                { 
                    ReadPstates(); 
                    ReadPstate(); 
                }
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
                      (((uint)Math.Round(double.Parse(Didtext), 0) & 0xFF) << 8) | ((uint)Math.Round(double.Parse(Fidtext), 0) & 0xFF);
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
        catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
    }
    public static void CalculatePstateDetails(uint eax, ref uint IddDiv, ref uint IddVal, ref uint CpuVid, ref uint CpuDfsId, ref uint CpuFid)
    {
        IddDiv = eax >> 30;
        IddVal = (eax >> 22) & 0xFF;
        CpuVid = (eax >> 14) & 0xFF;
        CpuDfsId = (eax >> 8) & 0x3F;
        CpuFid = eax & 0xFF;
    }
    public static bool ApplyTscWorkaround()
    { // P0 fix C001_0015 HWCR[21]=1
      // Fixes timer issues when not using HPET
        try
        {
            var cpu = CpuSingleton.GetInstance();
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
        catch
        {
            return false;
        }
    }
    private static bool WritePstateClick(int pstateId, uint eax, uint edx, int numanode = 0)
    {
        try
        {
            var cpu = CpuSingleton.GetInstance();
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
        catch (Exception ex) { TraceIt_TraceError(ex.ToString()); return false; }
    }
    private static void ReadPstate()
    {
        try
        {
            var cpu = CpuSingleton.GetInstance();
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
                catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
                uint IddDiv = 0x0;
                uint IddVal = 0x0;
                uint CpuVid = 0x0;
                uint CpuDfsId = 0x0;
                uint CpuFid = 0x0;
                CalculatePstateDetails(eax, ref IddDiv, ref IddVal, ref CpuVid, ref CpuDfsId, ref CpuFid);
                switch (i)
                {
                    case 0:
                        pstatesDID[0] = Convert.ToDouble(CpuDfsId);
                        pstatesFID[0] = Convert.ToDouble(CpuFid);
                        break;
                    case 1:
                        pstatesDID[1] = Convert.ToDouble(CpuDfsId);
                        pstatesFID[1] = Convert.ToDouble(CpuFid);
                        break;
                    case 2:
                        pstatesDID[2] = Convert.ToDouble(CpuDfsId);
                        pstatesFID[2] = Convert.ToDouble(CpuFid);
                        break;
                }
            }
        }
        catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
    }
    private void ReadPstates() // Прочитать и записать текущие Pstates
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
                catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
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
        catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
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
        try
        {
            if (EnablePstates.IsOn)
            {
                profile[indexprofile].enablePstateEditor = true;
                ProfileSave();
            }
            else
            {
                profile[indexprofile].enablePstateEditor = false;
                ProfileSave();
            }
        }
        catch (Exception ex) { TraceIt_TraceError(ex.ToString()); indexprofile = 0; }
    }
    private void TurboBoost()
    {
        SetCorePerformanceBoost(Turbo_boost.IsOn);  //Турбобуст... 
        if (Turbo_boost.IsOn) //Сохранение
        {
            turboboost = true;
            profile[indexprofile].turboBoost = true;
            ProfileSave();
        }
        else
        {
            turboboost = false;
            profile[indexprofile].turboBoost = false;
            ProfileSave();
        }
    } 
    public void SetCorePerformanceBoost(bool enable)
    {
        uint eax = 0x0;
        uint edx = 0x0; 
        // Чтение текущего состояния регистра MSR 0xC0010015
        cpu?.ReadMsr(0xC0010015, ref eax, ref edx);
        // Маска для 25-го бита (CpbDis)
        var mask = 1U << 25;
        if (enable)
        {
            // Устанавливаем 25-й бит в 0 (включаем Core Performance Boost)
            eax &= ~mask;
        }
        else
        {
            // Устанавливаем 25-й бит в 1 (выключаем Core Performance Boost)
            eax |= mask;
        }
        // Записываем обновленное значение обратно в MSR
        cpu?.WriteMsr(0xC0010015, eax, edx);
    }
    private void Autoapply()
    {
        if (Autoapply_1.IsOn)
        {
            profile[indexprofile].autoPstate = true;
            ProfileSave();
        }
        else
        {
            profile[indexprofile].autoPstate = false;
            ProfileSave();
        }
    }
    private void WithoutP0()
    {
        if (Without_P0.IsOn)
        {
            profile[indexprofile].p0Ignorewarn = true;
            ProfileSave();
        }
        else
        {
            profile[indexprofile].p0Ignorewarn = false;
            ProfileSave();
        }
    }
    private void IgnoreWarning()
    {
        if (IgnoreWarn.IsOn)
        {
            profile[indexprofile].ignoreWarn = true;
            ProfileSave();
        }
        else
        {
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
                catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
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
            P0_Freq.Content = (Mult_0_v + 4) * 100;
            try
            {
                Mult_0.SelectedIndex = (int)Mult_0_v;
            }
            catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
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
                catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
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
            double Mult_1_v;
            var Did_value = DID_1.Value;
            var Fid_value = FID_1.Value;
            Mult_1_v = Fid_value / Did_value * 2;
            if (Fid_value / Did_value % 2 == 5)
            {
                Mult_1_v -= 3;
            }
            else
            {
                Mult_1_v -= 4;
            }
            if (Mult_1_v <= 0)
            {
                Mult_1_v = 0;
            }
            P1_Freq.Content = (Mult_1_v + 4) * 100;
            try
            {
                Mult_1.SelectedIndex = (int)Mult_1_v;
            }
            catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
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
                catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
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
            try { Mult_2.SelectedIndex = (int)Mult_2_v; } catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
            Save_ID2();
        }
    }
    public void Save_ID0()
    {
        if (waitforload == false)
        {
            profile[indexprofile].did0 = DID_0.Value;
            profile[indexprofile].fid0 = FID_0.Value;
            profile[indexprofile].vid0 = VID_0.Value;
            profile[indexprofile].did1 = DID_1.Value;
            profile[indexprofile].fid1 = FID_1.Value;
            profile[indexprofile].vid1 = VID_1.Value;
            profile[indexprofile].did2 = DID_2.Value;
            profile[indexprofile].fid2 = FID_2.Value;
            profile[indexprofile].vid2 = VID_2.Value;
            pstatesDID[0] = DID_0.Value;
            pstatesFID[0] = FID_0.Value;
            pstatesVID[0] = VID_0.Value;
            ProfileSave();
        }
    }
    public void Save_ID1()
    {
        if (waitforload == false)
        {
            profile[indexprofile].did0 = DID_0.Value;
            profile[indexprofile].fid0 = FID_0.Value;
            profile[indexprofile].vid0 = VID_0.Value;
            profile[indexprofile].did1 = DID_1.Value;
            profile[indexprofile].fid1 = FID_1.Value;
            profile[indexprofile].vid1 = VID_1.Value;
            profile[indexprofile].did2 = DID_2.Value;
            profile[indexprofile].fid2 = FID_2.Value;
            profile[indexprofile].vid2 = VID_2.Value;
            pstatesDID[1] = DID_1.Value;
            pstatesFID[1] = FID_1.Value;
            pstatesVID[1] = VID_1.Value;
            ProfileSave();
        }
    }
    public void Save_ID2()
    {
        if (waitforload == false)
        {
            profile[indexprofile].did0 = DID_0.Value;
            profile[indexprofile].fid0 = FID_0.Value;
            profile[indexprofile].vid0 = VID_0.Value;
            profile[indexprofile].did1 = DID_1.Value;
            profile[indexprofile].fid1 = FID_1.Value;
            profile[indexprofile].vid1 = VID_1.Value;
            profile[indexprofile].did2 = DID_2.Value;
            profile[indexprofile].fid2 = FID_2.Value;
            profile[indexprofile].vid2 = VID_2.Value;
            pstatesDID[2] = DID_0.Value;
            pstatesFID[2] = FID_0.Value;
            pstatesVID[2] = VID_0.Value;
            ProfileSave();
        }
    }
    private void VID_0_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => Save_ID0();
    private void VID_1_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => Save_ID1();
    private void VID_2_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => Save_ID2();
    #endregion 
}