using System.ComponentModel;
using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.JsonContainers;
using Saku_Overclock.SMUEngine; 
using Saku_Overclock.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation.Metadata;
using Windows.UI;
using ZenStates.Core;
using Button = Microsoft.UI.Xaml.Controls.Button;
using CheckBox = Microsoft.UI.Xaml.Controls.CheckBox;
using ComboBox = Microsoft.UI.Xaml.Controls.ComboBox;
using Mailbox = ZenStates.Core.Mailbox;
using Process = System.Diagnostics.Process;
using TextBox = Microsoft.UI.Xaml.Controls.TextBox;

namespace Saku_Overclock.Views;

public sealed partial class ПараметрыPage
{
    private FontIcon? _smuSymbol1; // тоже самое что и SMUSymbol
    private List<SmuAddressSet>? _matches; // Совпадения адресов SMU 
    private static Smusettings _smusettings = new(); // Загрузка настроек быстрых команд SMU
    private static Profile[] _profile = new Profile[1]; // Всегда по умолчанию будет 1 профиль
    private static readonly IAppNotificationService NotificationsService = App.GetService<IAppNotificationService>(); // Уведомления приложения
    private int _indexprofile; // Выбранный профиль
    private static readonly IAppSettingsService AppSettings = App.GetService<IAppSettingsService>(); // Все настройки приложения
    private bool _isSearching; // Флаг, выполняется ли поиск, чтобы не сканировать адреса SMU
    private readonly List<string> _searchItems = [];
    private string
        _smuSymbol =
            "\uE8C8"; // Изначальный символ копирования, для секции Редактор параметров SMU. Используется для быстрых команд SMU

    private bool _isLoaded; // Загружена ли корректно страница для применения изменений
    private bool _relay; // Задержка между изменениями ComboBox в секции Состояния CPU
    private Cpu? _cpu; // Импорт Zen States core
    private SendSmuCommand? _cpusend; // Импорт отправителя команд SMU
    private bool _waitforload = true; // Ожидание окончательной смены профиля на другой. Активируется при смене профиля
    private string? _adjline; // Команды RyzenADJ для применения
    private readonly Mailbox _testMailbox = new(); // Новый адрес SMU

    private static string?
        _equalvid; // Преобразование из напряжения в его ID. Используется в секции Состояния CPU для указания напряжения PState

    private static readonly List<double> PstatesFid = [0, 0, 0];
    private static readonly List<double> PstatesDid = [0, 0, 0];
    private static readonly List<double> PstatesVid = [0, 0, 0];

    public static string ApplyInfo
    {
        get;
        set;
    } = "";

    public ПараметрыPage()
    {
        App.GetService<ПараметрыViewModel>();
        InitializeComponent();
        ProfileLoad();
        _indexprofile = AppSettings.Preset;
        AppSettings.NBFCFlagConsoleCheckSpeedRunning = false;
        AppSettings.FlagRyzenADJConsoleTemperatureCheckRunning = false;
        AppSettings.SaveSettings();
        try
        {
            _cpu ??= CpuSingleton.GetInstance();
            _cpusend ??= App.GetService<SendSmuCommand>();
        }
        catch (Exception ex)
        {
            TraceIt_TraceError(ex.ToString());
            App.GetService<IAppNotificationService>()
                .Show(string.Format("AppNotificationCrash_CPU".GetLocalized(), AppContext.BaseDirectory));
        }

        Loaded += ПараметрыPage_Loaded;
    }

    #region JSON and initialization

    private async void ПараметрыPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await LogHelper.Log("Opened Overclock page. Start loading.");
            _isLoaded = true;
            try
            {
                ProfileLoad();
                CollectSearchItems();
                SlidersInit();
            }
            catch (Exception ex)
            {
                TraceIt_TraceError(ex.ToString());
                try
                {
                    AppSettings.Preset = -1;
                    AppSettings.SaveSettings();
                    _indexprofile = -1;
                    SlidersInit();
                }
                catch (Exception ex1)
                {
                    TraceIt_TraceError(ex1.ToString());
                    await Send_Message("Critical Error!", "Can't load profiles. Tell this to developer",
                        Symbol.Bookmarks);
                }
            }
        }
        catch (Exception exception)
        {
            TraceIt_TraceError(exception.ToString());
        }
    }

    #region JSON only voids

    private static void SmuSettingsSave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                "SakuOverclock"));
            File.WriteAllText(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\smusettings.json",
                JsonConvert.SerializeObject(_smusettings, Formatting.Indented));
        }
        catch (Exception ex)
        {
            TraceIt_TraceError(ex.ToString());
        }
    }

    private static void SmuSettingsLoad()
    {
        var filePath = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\smusettings.json";
        if (File.Exists(filePath))
        {
            try
            {
                _smusettings = JsonConvert.DeserializeObject<Smusettings>(File.ReadAllText(filePath))!;
            }
            catch
            {
                JsonRepair('s');
            }
        }
        else
        {
            JsonRepair('s');
        }
    }

    private static void ProfileSave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                "SakuOverclock"));
            File.WriteAllText(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\profile.json",
                JsonConvert.SerializeObject(_profile, Formatting.Indented));
        }
        catch (Exception ex)
        {
            TraceIt_TraceError(ex.ToString());
        }
    }

    private static void ProfileLoad()
    {
        try
        {
            _profile = JsonConvert.DeserializeObject<Profile[]>(File.ReadAllText(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\profile.json"))!;
        }
        catch (Exception ex)
        {
            JsonRepair('p');
            TraceIt_TraceError(ex.ToString());
        }
    }

    private static void JsonRepair(char file)
    {
        switch (file)
        {
            case 's':
                _smusettings = new Smusettings();
                try
                {
                    Directory.CreateDirectory(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(
                        Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                        @"\SakuOverclock\smusettings.json",
                        JsonConvert.SerializeObject(_smusettings, Formatting.Indented));
                }
                catch
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                                @"\SakuOverclock\smusettings.json");
                    Directory.CreateDirectory(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(
                        Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                        @"\SakuOverclock\smusettings.json",
                        JsonConvert.SerializeObject(_smusettings, Formatting.Indented));
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(),
                        AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }

                break;
            case 'p':
                _profile = [];
                try
                {
                    Directory.CreateDirectory(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(
                        Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\profile.json",
                        JsonConvert.SerializeObject(_profile));
                }
                catch
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                                @"\SakuOverclock\profile.json");
                    Directory.CreateDirectory(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(
                        Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\profile.json",
                        JsonConvert.SerializeObject(_profile));
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(),
                        AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }

                break;
        }
    }
    #endregion

    #region Initialization

    private void SlidersInit()
    {
        LogHelper.Log("SakuOverclock SlidersInit");
        if (_isLoaded == false)
        {
            return;
        }

        _waitforload = true;
        ProfileLoad();
        ProfileCOM.Items.Clear();
        ProfileCOM.Items.Add(new ComboBoxItem
        {
            Content = new TextBlock
            {
                Text = "Param_Premaded".GetLocalized(),
                Foreground = (Brush)Application.Current.Resources["AccentTextFillColorTertiaryBrush"]
            },
            IsEnabled = false
        });
        foreach (var currProfile in _profile)
        {
            if (currProfile.profilename != string.Empty)
            {
                ProfileCOM.Items.Add(currProfile.profilename);
            }
        } 

        if (AppSettings.Preset > _profile.Length)
        {
            AppSettings.Preset = 0;
            AppSettings.SaveSettings();
        }
        else
        {
            if (AppSettings.Preset == -1)
            {
                _indexprofile = 0;
                ProfileCOM.SelectedIndex = 0;
            }
            else
            {
                _indexprofile = AppSettings.Preset;
                if (ProfileCOM.Items.Count >= _indexprofile + 1)
                {
                    ProfileCOM.SelectedIndex = _indexprofile + 1;
                }
            }
        } 
        _waitforload = false;
    }

    private void DesktopCPU_HideUnavailableParameters()
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

    private void HideDisabledCurveOptimizedParameters(bool locks)
    {
        CCD1_1.IsEnabled = locks;
        CCD1_1v.IsEnabled = locks;
        CCD1_2.IsEnabled = locks;
        CCD1_2v.IsEnabled = locks;
        CCD1_3.IsEnabled = locks;
        CCD1_3v.IsEnabled = locks;
        CCD1_4.IsEnabled = locks;
        CCD1_4v.IsEnabled = locks;
        CCD1_5.IsEnabled = locks;
        CCD1_5v.IsEnabled = locks;
        CCD1_6.IsEnabled = locks;
        CCD1_6v.IsEnabled = locks;
        CCD1_7.IsEnabled = locks;
        CCD1_7v.IsEnabled = locks;
        CCD1_8.IsEnabled = locks;
        CCD1_8v.IsEnabled = locks;
        CCD2_1.IsEnabled = locks;
        CCD2_1v.IsEnabled = locks;
        CCD2_2.IsEnabled = locks;
        CCD2_2v.IsEnabled = locks;
        CCD2_3.IsEnabled = locks;
        CCD2_3v.IsEnabled = locks;
        CCD2_4.IsEnabled = locks;
        CCD2_4v.IsEnabled = locks;
        CCD2_5.IsEnabled = locks;
        CCD2_5v.IsEnabled = locks;
        CCD2_6.IsEnabled = locks;
        CCD2_6v.IsEnabled = locks;
        CCD2_7.IsEnabled = locks;
        CCD2_7v.IsEnabled = locks;
        CCD2_8.IsEnabled = locks;
        CCD2_8v.IsEnabled = locks;
    }

    private async void MainInit(int index)
    {
        try
        {
            await LogHelper.Log("MainInit started");
            await LogHelper.Log((_cpu?.info.codeName.ToString() ?? "Unknown") + " Codename");
            if (SettingsViewModel.VersionId != 5) // Если не дебаг. В дебаг версии отображаются все параметры
            {
                if (_cpu?.info.codeName.ToString().Contains("VanGogh") == false)
                {
                    A1_main.Visibility = Visibility.Collapsed;
                    A3_main.Visibility = Visibility.Collapsed;
                    A4_main.Visibility = Visibility.Collapsed;
                    A5_main.Visibility = Visibility.Collapsed;
                    A1_desc.Visibility = Visibility.Collapsed;
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

                if (_cpu?.info.codeName.ToString().Contains("Raven") == false &&
                    _cpu?.info.codeName.ToString().Contains("Dali") == false &&
                    _cpu?.info.codeName.ToString().Contains("Picasso") == false)
                {
                    iGPU_Subsystems.Visibility = Visibility.Collapsed;
                }

                if (_cpu?.info.codeName.ToString().Contains("Mendocino") == true ||
                    _cpu?.info.codeName.ToString().Contains("Rembrandt") == true ||
                    _cpu?.info.codeName.ToString().Contains("Phoenix") == true ||
                    _cpu?.info.codeName.ToString().Contains("DragonRange") == true ||
                    _cpu?.info.codeName.ToString().Contains("HawkPoint") == true)
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

                if (_cpu?.info.codeName.ToString().Contains("Pinnacle") == true ||
                    _cpu?.info.codeName.ToString().Contains("Summit") == true)
                {
                    CCD1_Expander.Visibility = Visibility.Collapsed; //Убрать Оптимизатор кривой
                    CCD2_Expander.Visibility = Visibility.Collapsed;
                    CO_Expander.Visibility = Visibility.Collapsed;
                    DesktopCPU_HideUnavailableParameters();
                }

                if (_cpu?.info.codeName.ToString().Contains("Matisse") == true ||
                    _cpu?.info.codeName.ToString().Contains("Vermeer") == true)
                {
                    DesktopCPU_HideUnavailableParameters();
                }

                uint cores = 0;

                if (_cpu == null || _cpu?.info.codeName.ToString().Contains("Unsupported") == true)
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
                    cores = (uint)await ИнформацияPage.GetCpuCoresAsync();
                }

                if (_cpu != null)
                {
                    cores = _cpu.info.topology.physicalCores;
                    await LogHelper.Log("CPU Cores: " + cores);
                }

                for (var i = 0; i < cores; i++)
                {
                    var mapIndex = i < 8 ? 0 : 1;
                    if ((~_cpu?.info.topology.coreDisableMap[mapIndex] >> i % 8 & 1) == 0)
                    {
                        try
                        {
                            var checkbox = i < 8
                        ? (CheckBox)CCD1_Grid.FindName($"CCD1_{i + 1}")
                        : (CheckBox)CCD2_Grid.FindName($"CCD2_{i - 8}");
                            if (checkbox != null && checkbox.IsChecked == true)
                            {
                                var setVal = i < 8
                                    ? (Slider)CCD1_Grid.FindName($"CCD1_{i + 1}v")
                                    : (Slider)CCD2_Grid.FindName($"CCD2_{i - 8}v");
                                setVal.IsEnabled = false;
                                setVal.Opacity = 0.4;
                                checkbox.IsEnabled = false;
                                checkbox.IsChecked = false;
                            }
                            var setGrid1 = i < 8
                        ? (StackPanel)CCD1_Grid.FindName($"CCD1_Grid{i + 1}_1")
                        : (StackPanel)CCD2_Grid.FindName($"CCD2_Grid{i - 8}_1");
                            var setGrid2 = i < 8
                        ? (Grid)CCD1_Grid.FindName($"CCD1_Grid{i + 1}_2")
                        : (Grid)CCD2_Grid.FindName($"CCD2_Grid{i - 8}_2");
                            if (setGrid1 != null)
                            {
                                setGrid1.Visibility = Visibility.Collapsed;
                                setGrid1.Opacity = 0.4;
                            }
                            if (setGrid2 != null)
                            {
                                setGrid2.Visibility = Visibility.Collapsed;
                                setGrid2.Opacity = 0.4;
                            }
                        }
                        catch (Exception e)
                        {
                            TraceIt_TraceError(e.ToString());
                        }
                    }
                }

                if (CCD1_Grid1_1.Visibility == Visibility.Collapsed &&
                    CCD1_Grid2_1.Visibility == Visibility.Collapsed &&
                    CCD1_Grid3_1.Visibility == Visibility.Collapsed &&
                    CCD1_Grid4_1.Visibility == Visibility.Collapsed &&
                    CCD1_Grid5_1.Visibility == Visibility.Collapsed &&
                    CCD1_Grid6_1.Visibility == Visibility.Collapsed &&
                    CCD1_Grid7_1.Visibility == Visibility.Collapsed &&
                    CCD1_Grid8_1.Visibility == Visibility.Collapsed &&
                    CCD2_Grid0_1.Visibility == Visibility.Collapsed &&
                    CCD2_Grid1_1.Visibility == Visibility.Collapsed &&
                    CCD2_Grid2_1.Visibility == Visibility.Collapsed &&
                    CCD2_Grid3_1.Visibility == Visibility.Collapsed &&
                    CCD2_Grid4_1.Visibility == Visibility.Collapsed &&
                    CCD2_Grid5_1.Visibility == Visibility.Collapsed &&
                    CCD2_Grid6_1.Visibility == Visibility.Collapsed &&
                    CCD2_Grid7_1.Visibility == Visibility.Collapsed)
                {
                    await LogHelper.LogWarn("Curve Optimizer Disabled cores detection incorrect on that CPU. Using standart disabled cores detection method.");
                    CCD1_Grid1_1.Visibility = Visibility.Visible;
                    CCD1_Grid2_1.Visibility = Visibility.Visible;
                    CCD1_Grid3_1.Visibility = Visibility.Visible;
                    CCD1_Grid4_1.Visibility = Visibility.Visible;
                    CCD1_Grid5_1.Visibility = Visibility.Visible;
                    CCD1_Grid6_1.Visibility = Visibility.Visible;
                    CCD1_Grid7_1.Visibility = Visibility.Visible;
                    CCD1_Grid8_1.Visibility = Visibility.Visible;
                    CCD2_Grid0_1.Visibility = Visibility.Visible;
                    CCD2_Grid1_1.Visibility = Visibility.Visible;
                    CCD2_Grid2_1.Visibility = Visibility.Visible;
                    CCD2_Grid3_1.Visibility = Visibility.Visible;
                    CCD2_Grid4_1.Visibility = Visibility.Visible;
                    CCD2_Grid5_1.Visibility = Visibility.Visible;
                    CCD2_Grid6_1.Visibility = Visibility.Visible;
                    CCD2_Grid7_1.Visibility = Visibility.Visible;
                    if (cores > 8)
                    {
                        if (cores <= 15)
                        {
                            CCD2_Grid7_2.Visibility = Visibility.Collapsed;
                            CCD2_Grid7_1.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 14)
                        {
                            CCD2_Grid6_2.Visibility = Visibility.Collapsed;
                            CCD2_Grid6_1.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 13)
                        {
                            CCD2_Grid5_2.Visibility = Visibility.Collapsed;
                            CCD2_Grid5_1.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 12)
                        {
                            CCD2_Grid4_2.Visibility = Visibility.Collapsed;
                            CCD2_Grid4_1.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 11)
                        {
                            CCD2_Grid3_2.Visibility = Visibility.Collapsed;
                            CCD2_Grid3_1.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 10)
                        {
                            CCD2_Grid2_2.Visibility = Visibility.Collapsed;
                            CCD2_Grid2_1.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 9)
                        {
                            CCD2_Grid1_2.Visibility = Visibility.Collapsed;
                            CCD2_Grid1_1.Visibility = Visibility.Collapsed;
                        }
                    }
                    else
                    {
                        CO_Cores_Text.Text = CO_Cores_Text.Text.Replace("7", $"{cores - 1}");
                        CCD2_Expander.Visibility = Visibility.Collapsed;
                        if (cores <= 7)
                        {
                            CCD1_Grid8_2.Visibility = Visibility.Collapsed;
                            CCD1_Grid8_1.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 6)
                        {
                            CCD1_Grid7_2.Visibility = Visibility.Collapsed;
                            CCD1_Grid7_1.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 5)
                        {
                            CCD1_Grid6_2.Visibility = Visibility.Collapsed;
                            CCD1_Grid6_1.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 4)
                        {
                            CCD1_Grid5_2.Visibility = Visibility.Collapsed;
                            CCD1_Grid5_1.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 3)
                        {
                            CCD1_Grid4_2.Visibility = Visibility.Collapsed;
                            CCD1_Grid4_1.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 2)
                        {
                            CCD1_Grid3_2.Visibility = Visibility.Collapsed;
                            CCD1_Grid3_1.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 1)
                        {
                            CCD1_Grid2_2.Visibility = Visibility.Collapsed;
                            CCD1_Grid2_1.Visibility = Visibility.Collapsed;
                        }

                        if (cores == 0)
                        {
                            CCD1_Expander.Visibility = Visibility.Collapsed;
                        }
                    }
                }
            }

            _waitforload = true;
            if (AppSettings.Preset == -1 || index == -1) //Load from unsaved
            {
                await LogHelper.LogWarn("Unable to find or set last saved profile. Setting unsaved profile.");

                MainScroll.IsEnabled = false;
                ActionButton_Apply.IsEnabled = false;
                ActionButton_Delete.IsEnabled = false;
                ActionButton_Mon.IsEnabled = false;
                ActionButton_Save.IsEnabled = true;
                ActionButton_Save.BorderBrush = new SolidColorBrush(Colors.Red);
                ActionButton_Save.BorderThickness = new Thickness(8);
                ActionButton_Share.IsEnabled = false;
                EditProfileButton.IsEnabled = false;
                Action_IncompatibleProfile.IsOpen = true;
                //Unknown
            }
            else
            {
                await LogHelper.Log($"Loading profile... {_profile[index].profilename}");
                ActionButton_Save.BorderBrush = ActionButton_Delete.BorderBrush;
                ActionButton_Save.BorderThickness = ActionButton_Delete.BorderThickness;
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
                    if (_profile[index].cpu1value > c1v.Maximum)
                    {
                        c1v.Maximum = FromValueToUpperFive(_profile[index].cpu1value);
                    }

                    if (_profile[index].cpu2value > c2v.Maximum)
                    {
                        c2v.Maximum = FromValueToUpperFive(_profile[index].cpu2value);
                    }

                    if (_profile[index].cpu3value > c3v.Maximum)
                    {
                        c3v.Maximum = FromValueToUpperFive(_profile[index].cpu3value);
                    }

                    if (_profile[index].cpu4value > c4v.Maximum)
                    {
                        c4v.Maximum = FromValueToUpperFive(_profile[index].cpu4value);
                    }

                    if (_profile[index].cpu5value > c5v.Maximum)
                    {
                        c5v.Maximum = FromValueToUpperFive(_profile[index].cpu5value);
                    }

                    if (_profile[index].cpu6value > c6v.Maximum)
                    {
                        c6v.Maximum = FromValueToUpperFive(_profile[index].cpu6value);
                    }

                    if (_profile[index].cpu7value > c7v.Maximum)
                    {
                        c7v.Maximum = FromValueToUpperFive(_profile[index].cpu7value);
                    }

                    if (_profile[index].vrm1value > V1V.Maximum)
                    {
                        V1V.Maximum = FromValueToUpperFive(_profile[index].vrm1value);
                    }

                    if (_profile[index].vrm2value > V2V.Maximum)
                    {
                        V2V.Maximum = FromValueToUpperFive(_profile[index].vrm2value);
                    }

                    if (_profile[index].vrm3value > V3V.Maximum)
                    {
                        V3V.Maximum = FromValueToUpperFive(_profile[index].vrm3value);
                    }

                    if (_profile[index].vrm4value > V4V.Maximum)
                    {
                        V4V.Maximum = FromValueToUpperFive(_profile[index].vrm4value);
                    }

                    if (_profile[index].vrm5value > V5V.Maximum)
                    {
                        V5V.Maximum = FromValueToUpperFive(_profile[index].vrm5value);
                    }

                    if (_profile[index].vrm6value > V6V.Maximum)
                    {
                        V6V.Maximum = FromValueToUpperFive(_profile[index].vrm6value);
                    }

                    if (_profile[index].vrm7value > V7V.Maximum)
                    {
                        V7V.Maximum = FromValueToUpperFive(_profile[index].vrm7value);
                    }

                    if (_profile[index].gpu1value > g1v.Maximum)
                    {
                        g1v.Maximum = FromValueToUpperFive(_profile[index].gpu1value);
                    }

                    if (_profile[index].gpu2value > g2v.Maximum)
                    {
                        g2v.Maximum = FromValueToUpperFive(_profile[index].gpu2value);
                    }

                    if (_profile[index].gpu3value > g3v.Maximum)
                    {
                        g3v.Maximum = FromValueToUpperFive(_profile[index].gpu3value);
                    }

                    if (_profile[index].gpu4value > g4v.Maximum)
                    {
                        g4v.Maximum = FromValueToUpperFive(_profile[index].gpu4value);
                    }

                    if (_profile[index].gpu5value > g5v.Maximum)
                    {
                        g5v.Maximum = FromValueToUpperFive(_profile[index].gpu5value);
                    }

                    if (_profile[index].gpu6value > g6v.Maximum)
                    {
                        g6v.Maximum = FromValueToUpperFive(_profile[index].gpu6value);
                    }

                    if (_profile[index].gpu7value > g7v.Maximum)
                    {
                        g7v.Maximum = FromValueToUpperFive(_profile[index].gpu7value);
                    }

                    if (_profile[index].gpu8value > g8v.Maximum)
                    {
                        g8v.Maximum = FromValueToUpperFive(_profile[index].gpu8value);
                    }

                    if (_profile[index].gpu9value > g9v.Maximum)
                    {
                        g9v.Maximum = FromValueToUpperFive(_profile[index].gpu9value);
                    }

                    if (_profile[index].gpu10value > g10v.Maximum)
                    {
                        g10v.Maximum = FromValueToUpperFive(_profile[index].gpu10value);
                    }

                    if (_profile[index].gpu11value > g11v.Maximum)
                    {
                        g11v.Maximum = FromValueToUpperFive(_profile[index].gpu11value);
                    }

                    if (_profile[index].gpu12value > g12v.Maximum)
                    {
                        g12v.Maximum = FromValueToUpperFive(_profile[index].gpu12value);
                    }

                    if (_profile[index].advncd1value > a1v.Maximum)
                    {
                        a1v.Maximum = FromValueToUpperFive(_profile[index].advncd1value);
                    } 

                    if (_profile[index].advncd3value > a3v.Maximum)
                    {
                        a3v.Maximum = FromValueToUpperFive(_profile[index].advncd3value);
                    }

                    if (_profile[index].advncd4value > a4v.Maximum)
                    {
                        a4v.Maximum = FromValueToUpperFive(_profile[index].advncd4value);
                    }

                    if (_profile[index].advncd5value > a5v.Maximum)
                    {
                        a5v.Maximum = FromValueToUpperFive(_profile[index].advncd5value);
                    }

                    if (_profile[index].advncd6value > a6v.Maximum)
                    {
                        a6v.Maximum = FromValueToUpperFive(_profile[index].advncd6value);
                    }

                    if (_profile[index].advncd7value > a7v.Maximum)
                    {
                        a7v.Maximum = FromValueToUpperFive(_profile[index].advncd7value);
                    }

                    if (_profile[index].advncd8value > a8v.Maximum)
                    {
                        a8v.Maximum = FromValueToUpperFive(_profile[index].advncd8value);
                    }

                    if (_profile[index].advncd9value > a9v.Maximum)
                    {
                        a9v.Maximum = FromValueToUpperFive(_profile[index].advncd9value);
                    }

                    if (_profile[index].advncd10value > a10v.Maximum)
                    {
                        a10v.Maximum = FromValueToUpperFive(_profile[index].advncd10value);
                    }

                    if (_profile[index].advncd11value > a11v.Maximum)
                    {
                        a11v.Maximum = FromValueToUpperFive(_profile[index].advncd11value);
                    }

                    if (_profile[index].advncd12value > a12v.Maximum)
                    {
                        a12v.Maximum = FromValueToUpperFive(_profile[index].advncd12value);
                    }

                    if (_profile[index].advncd15value > a15v.Maximum)
                    {
                        a15v.Maximum = FromValueToUpperFive(_profile[index].advncd15value);
                    }
                }
                catch (Exception ex)
                {
                    TraceIt_TraceError(ex.ToString());
                }

                try
                {
                    c1.IsChecked = _profile[index].cpu1;
                    c1v.Value = _profile[index].cpu1value;
                    c2.IsChecked = _profile[index].cpu2;
                    c2v.Value = _profile[index].cpu2value;
                    c3.IsChecked = _profile[index].cpu3;
                    c3v.Value = _profile[index].cpu3value;
                    c4.IsChecked = _profile[index].cpu4;
                    c4v.Value = _profile[index].cpu4value;
                    c5.IsChecked = _profile[index].cpu5;
                    c5v.Value = _profile[index].cpu5value;
                    c6.IsChecked = _profile[index].cpu6;
                    c6v.Value = _profile[index].cpu6value;
                    c7.IsChecked = _profile[index].cpu7;
                    c7v.Value = _profile[index].cpu7value;
                    V1.IsChecked = _profile[index].vrm1;
                    V1V.Value = _profile[index].vrm1value;
                    V2.IsChecked = _profile[index].vrm2;
                    V2V.Value = _profile[index].vrm2value;
                    V3.IsChecked = _profile[index].vrm3;
                    V3V.Value = _profile[index].vrm3value;
                    V4.IsChecked = _profile[index].vrm4;
                    V4V.Value = _profile[index].vrm4value;
                    V5.IsChecked = _profile[index].vrm5;
                    V5V.Value = _profile[index].vrm5value;
                    V6.IsChecked = _profile[index].vrm6;
                    V6V.Value = _profile[index].vrm6value;
                    V7.IsChecked = _profile[index].vrm7;
                    V7V.Value = _profile[index].vrm7value;
                    g1.IsChecked = _profile[index].gpu1;
                    g1v.Value = _profile[index].gpu1value;
                    g2.IsChecked = _profile[index].gpu2;
                    g2v.Value = _profile[index].gpu2value;
                    g3.IsChecked = _profile[index].gpu3;
                    g3v.Value = _profile[index].gpu3value;
                    g4.IsChecked = _profile[index].gpu4;
                    g4v.Value = _profile[index].gpu4value;
                    g5.IsChecked = _profile[index].gpu5;
                    g5v.Value = _profile[index].gpu5value;
                    g6.IsChecked = _profile[index].gpu6;
                    g6v.Value = _profile[index].gpu6value;
                    g7.IsChecked = _profile[index].gpu7;
                    g7v.Value = _profile[index].gpu7value;
                    g8v.Value = _profile[index].gpu8value;
                    g8.IsChecked = _profile[index].gpu8;
                    g9v.Value = _profile[index].gpu9value;
                    g9.IsChecked = _profile[index].gpu9;
                    g10v.Value = _profile[index].gpu10value;
                    g10.IsChecked = _profile[index].gpu10;
                    g11.IsChecked = _profile[index].gpu11;
                    g11v.Value = _profile[index].gpu11value;
                    g12.IsChecked = _profile[index].gpu12;
                    g12v.Value = _profile[index].gpu12value; 
                    g16.IsChecked = _profile[index].gpu16;
                    g16m.SelectedIndex = _profile[index].gpu16value;
                    a1.IsChecked = _profile[index].advncd1;
                    a1v.Value = _profile[index].advncd1value; 
                    a3.IsChecked = _profile[index].advncd3;
                    a3v.Value = _profile[index].advncd3value;
                    a4.IsChecked = _profile[index].advncd4;
                    a4v.Value = _profile[index].advncd4value;
                    a5.IsChecked = _profile[index].advncd5;
                    a5v.Value = _profile[index].advncd5value;
                    a6.IsChecked = _profile[index].advncd6;
                    a6v.Value = _profile[index].advncd6value;
                    a7.IsChecked = _profile[index].advncd7;
                    a7v.Value = _profile[index].advncd7value;
                    a8v.Value = _profile[index].advncd8value;
                    a8.IsChecked = _profile[index].advncd8;
                    a9v.Value = _profile[index].advncd9value;
                    a9.IsChecked = _profile[index].advncd9;
                    a10v.Value = _profile[index].advncd10value;
                    a11v.Value = _profile[index].advncd11value;
                    a11.IsChecked = _profile[index].advncd11;
                    a12v.Value = _profile[index].advncd12value;
                    a12.IsChecked = _profile[index].advncd12;
                    a13.IsChecked = _profile[index].advncd13;
                    a13m.SelectedIndex = _profile[index].advncd13value;
                    a14.IsChecked = _profile[index].advncd14;
                    a14m.SelectedIndex = _profile[index].advncd14value;
                    a15.IsChecked = _profile[index].advncd15;
                    a15v.Value = _profile[index].advncd15value;
                    CCD_CO_Mode_Sel.IsChecked = _profile[index].comode;
                    CCD_CO_Mode.SelectedIndex = _profile[index].coprefmode;
                    O1.IsChecked = _profile[index].coall;
                    O1v.Value = _profile[index].coallvalue;
                    O2.IsChecked = _profile[index].cogfx;
                    O2v.Value = _profile[index].cogfxvalue;
                    CCD1_1.IsChecked = _profile[index].coper0;
                    CCD1_1v.Value = _profile[index].coper0value;
                    CCD1_2.IsChecked = _profile[index].coper1;
                    CCD1_2v.Value = _profile[index].coper1value;
                    CCD1_3.IsChecked = _profile[index].coper2;
                    CCD1_3v.Value = _profile[index].coper2value;
                    CCD1_4.IsChecked = _profile[index].coper3;
                    CCD1_4v.Value = _profile[index].coper3value;
                    CCD1_5.IsChecked = _profile[index].coper4;
                    CCD1_5v.Value = _profile[index].coper4value;
                    CCD1_6.IsChecked = _profile[index].coper5;
                    CCD1_6v.Value = _profile[index].coper5value;
                    CCD1_7.IsChecked = _profile[index].coper6;
                    CCD1_7v.Value = _profile[index].coper6value;
                    CCD1_8.IsChecked = _profile[index].coper7;
                    CCD1_8v.Value = _profile[index].coper7value;
                    CCD2_1.IsChecked = _profile[index].coper8;
                    CCD2_1v.Value = _profile[index].coper8value;
                    CCD2_2.IsChecked = _profile[index].coper9;
                    CCD2_2v.Value = _profile[index].coper9value;
                    CCD2_3.IsChecked = _profile[index].coper10;
                    CCD2_3v.Value = _profile[index].coper10value;
                    CCD2_4.IsChecked = _profile[index].coper11;
                    CCD2_4v.Value = _profile[index].coper11value;
                    CCD2_5.IsChecked = _profile[index].coper12;
                    CCD2_5v.Value = _profile[index].coper12value;
                    CCD2_6.IsChecked = _profile[index].coper13;
                    CCD2_6v.Value = _profile[index].coper13value;
                    CCD2_7.IsChecked = _profile[index].coper14;
                    CCD2_7v.Value = _profile[index].coper14value;
                    CCD2_8.IsChecked = _profile[index].coper15;
                    CCD2_8v.Value = _profile[index].coper15value;
                    EnablePstates.IsOn = _profile[index].enablePstateEditor;
                    Turbo_boost.IsOn = _profile[index].turboBoost;
                    Autoapply_1.IsOn = _profile[index].autoPstate;
                    IgnoreWarn.IsOn = _profile[index].ignoreWarn;
                    Without_P0.IsOn = _profile[index].p0Ignorewarn;
                    DID_0.Value = _profile[index].did0;
                    DID_1.Value = _profile[index].did1;
                    DID_2.Value = _profile[index].did2;
                    FID_0.Value = _profile[index].fid0;
                    FID_1.Value = _profile[index].fid1;
                    FID_2.Value = _profile[index].fid2;
                    VID_0.Value = _profile[index].vid0;
                    VID_1.Value = _profile[index].vid1;
                    VID_2.Value = _profile[index].vid2;
                    EnableSMU.IsOn = _profile[index].smuEnabled;
                    SMU_Func_Enabl.IsOn = _profile[index].smuFunctionsEnabl;
                    Bit_0_FEATURE_CCLK_CONTROLLER.IsOn = _profile[index].smuFeatureCCLK;
                    Bit_2_FEATURE_DATA_CALCULATION.IsOn = _profile[index].smuFeatureData;
                    Bit_3_FEATURE_PPT.IsOn = _profile[index].smuFeaturePPT;
                    Bit_4_FEATURE_TDC.IsOn = _profile[index].smuFeatureTDC;
                    Bit_5_FEATURE_THERMAL.IsOn = _profile[index].smuFeatureThermal;
                    Bit_8_FEATURE_PLL_POWER_DOWN.IsOn = _profile[index].smuFeaturePowerDown;
                    Bit_37_FEATURE_PROCHOT.IsOn = _profile[index].smuFeatureProchot;
                    Bit_39_FEATURE_STAPM.IsOn = _profile[index].smuFeatureSTAPM;
                    Bit_40_FEATURE_CORE_CSTATES.IsOn = _profile[index].smuFeatureCStates;
                    Bit_41_FEATURE_GFX_DUTY_CYCLE.IsOn = _profile[index].smuFeatureGfxDutyCycle;
                    Bit_42_FEATURE_AA_MODE.IsOn = _profile[index].smuFeatureAplusA;
                }
                catch
                {
                    await LogHelper.LogError("Profile contains error. Creating new profile.");

                    _profile = new Profile[1];
                    _profile[0] = new Profile();
                    ProfileSave();
                }
            }

            try
            {
                await LogHelper.Log("Trying to load P-States settings.");
                Mult_0.SelectedIndex = (int)(FID_0.Value * 25 / (DID_0.Value * 12.5)) - 4;
                P0_Freq.Content = FID_0.Value * 25 / (DID_0.Value * 12.5) * 100;
                Mult_1.SelectedIndex = (int)(FID_1.Value * 25 / (DID_1.Value * 12.5)) - 4;
                P1_Freq.Content = FID_1.Value * 25 / (DID_1.Value * 12.5) * 100;
                P2_Freq.Content = FID_2.Value * 25 / (DID_2.Value * 12.5) * 100;
                Mult_2.SelectedIndex = (int)(FID_2.Value * 25 / (DID_2.Value * 12.5)) - 4;
            }
            catch (Exception ex)
            {
                if (AppSettings.Preset != -1)
                {
                    TraceIt_TraceError(ex.ToString());
                }
            }

            _waitforload = false;
            await LogHelper.Log("Loading user SMU settings.");
            SmuSettingsLoad();
            if (_smusettings.Note != string.Empty)
            {
                SMUNotes.Document.SetText(TextSetOptions.FormatRtf, _smusettings.Note.TrimEnd());
                ChangeRichEditBoxTextColor(SMUNotes, GetColorFromBrush(TextColor.Foreground));
            }

            try
            {
                Init_QuickSMU();
            }
            catch (Exception ex)
            {
                TraceIt_TraceError(ex.ToString());
            }
        }
        catch (Exception e)
        {
            TraceIt_TraceError(e.ToString());
        }
    }

    private void Init_QuickSMU()
    {
        Task.Run(async () => await LogHelper.Log("Initing Quick SMU options..."));
        SmuSettingsLoad();
        if (_smusettings.QuickSmuCommands == null)
        {
            Task.Run(async () => await LogHelper.Log("Quick SMU options not found... Exiting initialization."));
            return;
        }

        QuickSMU.Children.Clear();
        QuickSMU.RowDefinitions.Clear();
        for (var i = 0; i < _smusettings.QuickSmuCommands.Count; i++)
        {
            var grid = new Grid //Основной грид, куда всё добавляется
            {
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch
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
            QuickSMU.Children.Add(grid); // Добавить в программу грид быстрой команды
            Grid.SetRow(grid, rowIndex); // Задать дорожку для нового грида
            // Создание Button
            var button = new Button // Добавить основную кнопку быстрой команды. Именно в ней всё содержимое
            {
                Height = 50,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch
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
                HorizontalAlignment = HorizontalAlignment.Left,
                Glyph = _smusettings.QuickSmuCommands[i].Symbol
            };
            // Добавление FontIcon в Grid
            innerGrid.Children.Add(fontIcon);
            // Создание TextBlock
            var textBlock1 = new TextBlock
            {
                Margin = new Thickness(35, 0.5, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
                Text = _smusettings.QuickSmuCommands[i].Name,
                FontWeight = FontWeights.SemiBold
            };
            innerGrid.Children.Add(textBlock1);
            // Создание второго TextBlock
            var textBlock2 = new TextBlock
            {
                Margin = new Thickness(35, 17.5, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
                Text = _smusettings.QuickSmuCommands[i].Description,
                FontWeight = FontWeights.Light
            };
            innerGrid.Children.Add(textBlock2);
            // Добавление внутреннего Grid в Button
            button.Content = innerGrid;
            // Создание внешнего Grid с кнопками
            var buttonsGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Right
            };
            // Создание и добавление кнопок во внешний Grid
            var playButton = new Button //Кнопка применить
            {
                Name = $"Play_{rowIndex}",
                HorizontalAlignment = HorizontalAlignment.Right,
                Width = 35,
                Height = 35,
                Margin = new Thickness(0, 0, 7, 0),
                Content = new SymbolIcon
                {
                    Symbol = Symbol.Play,
                    Margin = new Thickness(-5, 0, -5, 0),
                    HorizontalAlignment = HorizontalAlignment.Left
                }
            };
            buttonsGrid.Children.Add(playButton);
            var editButton = new Button //Кнопка изменить
            {
                Name = $"Edit_{rowIndex}",
                HorizontalAlignment = HorizontalAlignment.Right,
                Width = 35,
                Height = 35,
                Margin = new Thickness(0, 0, 50, 0),
                Content = new SymbolIcon
                {
                    Symbol = Symbol.Edit,
                    Margin = new Thickness(-5, 0, -5, 0)
                }
            };
            buttonsGrid.Children.Add(editButton);
            var rsmuButton = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                Width = 86,
                Height = 35,
                Margin = new Thickness(0, 0, 93, 0)
            };
            var rsmuTextBlock = new TextBlock
            {
                Text = _smusettings.MailBoxes![_smusettings.QuickSmuCommands[i].MailIndex].Name
            };
            rsmuButton.Content = rsmuTextBlock;
            buttonsGrid.Children.Add(rsmuButton);
            var cmdButton = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                Width = 86,
                Height = 35,
                Margin = new Thickness(0, 0, 187, 0)
            };
            var cmdTextBlock = new TextBlock
            {
                Text = _smusettings.QuickSmuCommands![i].Command + " / " + _smusettings.QuickSmuCommands![i].Argument
            };
            cmdButton.Content = cmdTextBlock;
            buttonsGrid.Children.Add(cmdButton);
            var autoButton = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                Width = 86,
                Height = 35,
                Margin = new Thickness(0, 0, 281, 0)
            };
            var autoTextBlock = new TextBlock
            {
                Text = "Apply"
            };
            if (_smusettings.QuickSmuCommands![i].Startup)
            {
                autoTextBlock.Text = "Autorun";
            }

            if (_smusettings.QuickSmuCommands![i].Startup || _smusettings.QuickSmuCommands![i].ApplyWith)
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

    #region Helpers
    private static Color GetColorFromBrush(Brush brush)
    {
        if (brush is SolidColorBrush solidColorBrush)
        {
            return solidColorBrush.Color;
        }

        return Colors.White;
    }

    private static void ChangeRichEditBoxTextColor(RichEditBox richEditBox, Color color)
    {
        richEditBox.Document.ApplyDisplayUpdates();
        var documentRange = richEditBox.Document.GetRange(0, TextConstants.MaxUnitCount);
        documentRange.CharacterFormat.ForegroundColor = color;
        richEditBox.Document.ApplyDisplayUpdates();
    }

    public static int FromValueToUpperFive(double value) => (int)Math.Ceiling(value / 5) * 5;

    private uint GetCoreMask(int coreIndex)
    {
        Task.Run(async () => await LogHelper.Log("Getting Core Mask..."));
        var ccxInCcd = _cpu?.info.family >= Cpu.Family.FAMILY_19H ? 1U : 2U;
        var coresInCcx = 8 / ccxInCcd;

        var ccd = Convert.ToUInt32(coreIndex / 8);
        var ccx = Convert.ToUInt32(coreIndex / coresInCcx - ccxInCcd * ccd);
        var core = Convert.ToUInt32(coreIndex % coresInCcx);
        var coreMask = _cpu!.MakeCoreMask(core, ccd, ccx);
        Task.Run(async () => await LogHelper.Log($"Core Mask detected: {coreMask}\nCCD: {ccd}\nCCX: {ccx}\nCore: {core}\nCCX in Index: {ccxInCcd}"));
        return coreMask;
    }

    private static void TraceIt_TraceError(string error) // Система TraceIt! позволит логгировать все ошибки
    {
        if (error != string.Empty)
        {
            Task.Run(async () => await LogHelper.LogError(error));
            NotificationsService.Notifies ??= [];
            NotificationsService.Notifies.Add(new Notify
            {
                Title = "TraceIt_Error".GetLocalized(),
                Msg = error,
                Type = InfoBarSeverity.Error
            });
            NotificationsService.SaveNotificationsSettings();
        }
    }
    #endregion

    #region Suggestion Engine

    // Collecting Search Items
    private void CollectSearchItems()
    {
        _searchItems.Clear();
        var expanders = Helpers.VisualTreeHelper.FindVisualChildren<Expander>(MainScroll);
        foreach (var expander in expanders)
        {
            var stackPanels = Helpers.VisualTreeHelper.FindVisualChildren<StackPanel>(expander);
            foreach (var stackPanel in stackPanels)
            {
                var textBlocks = Helpers.VisualTreeHelper.FindVisualChildren<TextBlock>(stackPanel).Where(tb => tb.FontSize - 15 == 0);
                foreach (var textBlock in textBlocks)
                {
                    if (!string.IsNullOrWhiteSpace(textBlock.Text) &&
                        !_searchItems.Contains(textBlock.Text)
                        && !(textBlock.Text.Contains('') || 
                             textBlock.Text.Contains('') ||
                             textBlock.Text.Contains('') ||
                             textBlock.Text.Contains('') ||
                             textBlock.Text.Contains('') ||
                             textBlock.Text.Contains('')))
                    {
                        _searchItems.Add(textBlock.Text);
                    } 
                }
            }
        }
    } 

    private void ResetVisibility()
    {
        var expanders = Helpers.VisualTreeHelper.FindVisualChildren<Expander>(MainScroll);
        foreach (var expander in expanders)
        {
            _isSearching = true;
            expander.IsExpanded = true;
            var stackPanels = Helpers.VisualTreeHelper.FindVisualChildren<StackPanel>(expander);
            foreach (var stackPanel in stackPanels)
            {
                stackPanel.Visibility = Visibility.Visible;
                var adjacentGrid = Helpers.VisualTreeHelper.FindAdjacentGrid(stackPanel);
                if (adjacentGrid != null)
                {
                    adjacentGrid.Visibility = Visibility.Visible;
                }
            }
        }
    } 

    private void SuggestBox_OnTextChanged(AutoSuggestBox? sender, AutoSuggestBoxTextChangedEventArgs? args)
    {
        if (!_isLoaded) { return; }
        _isSearching = true;
        if (args?.Reason == AutoSuggestionBoxTextChangeReason.UserInput ||
            args?.Reason == AutoSuggestionBoxTextChangeReason.SuggestionChosen ||
            args == null)
        {
            var suitableItems = new List<TextBlock>();
            var splitText = SuggestBox.Text.ToLower().Split(" ");
            if (_searchItems.Count == 0) { CollectSearchItems(); }
            foreach (var searchItem in _searchItems)
            {
                var found = splitText.All(key => searchItem.Contains(key, StringComparison.CurrentCultureIgnoreCase));
                if (found)
                {
                    var textBlock = new TextBlock { Text = searchItem, Margin = new Thickness(-10,0,-10,0), Foreground = Param_Name.Foreground };
                    ToolTipService.SetToolTip(textBlock, searchItem);
                    suitableItems.Add(textBlock);
                }
            }
            if (suitableItems.Count == 0)
            {
                suitableItems.Add(new TextBlock { Text = "No results found", Foreground = Param_Name.Foreground });
            }
            SuggestBox.ItemsSource = suitableItems;


            var searchText = SuggestBox.Text.ToLower();
            if (SuggestBox.Text == string.Empty)
            {
                FilterButtons_ResetButton_Click(null, null);
            }
            // Сбросить скрытое
            ResetVisibility();

            var expanders = Helpers.VisualTreeHelper.FindVisualChildren<Expander>(MainScroll);
            foreach (var expander in expanders)
            {
                var stackPanels = Helpers.VisualTreeHelper.FindVisualChildren<StackPanel>(expander);
                var arrayStackPanels = stackPanels as StackPanel[] ?? stackPanels.ToArray(); 
                var anyVisible = false;

                foreach (var stackPanel in arrayStackPanels)
                {
                    var textBlocks = Helpers.VisualTreeHelper.FindVisualChildren<TextBlock>(stackPanel).Where(tb => tb.FontSize - 15 == 0);
                    var containsText = textBlocks.Any(tb => tb.Text.Contains(searchText, StringComparison.CurrentCultureIgnoreCase));

                    var containsControl = Helpers.VisualTreeHelper.FindVisualChildren<CheckBox>(stackPanel).Any();

                    // Если текст и элементы управления найдены, делаем StackPanel видимой
                    if (containsText && containsControl)
                    {
                        stackPanel.Visibility = Visibility.Visible;
                        anyVisible = true;

                        // Второй проход: делаем видимыми все дочерние элементы
                        Helpers.VisualTreeHelper.SetAllChildrenVisibility(stackPanel, Visibility.Visible);
                    }
                    else
                    {
                        stackPanel.Visibility = Visibility.Collapsed;
                    }

                    var adjacentGrid = Helpers.VisualTreeHelper.FindAdjacentGrid(stackPanel);
                    if (adjacentGrid != null)
                    {
                        adjacentGrid.Visibility = stackPanel.Visibility;
                    }
                }
                foreach (var stackPanel1 in arrayStackPanels) // Второй проход
                {
                    if (stackPanel1.Visibility == Visibility.Visible)
                    {
                        Helpers.VisualTreeHelper.SetAllChildrenVisibility(stackPanel1, Visibility.Visible);
                    }
                }

                // Скрыть Expander если нет видимых StackPanels
                if (!anyVisible)
                {
                    expander.IsExpanded = false;
                }
            }
        }
        _isSearching = false;
    }

    private void FilterButton_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) { return; }
        _isSearching = true;
        List<(string, ToggleButton)> buttons = [
            ("", FilterButtons_Freq),
            ("", FilterButtons_Current),
            ("", FilterButtons_Power),
            ("", FilterButtons_Temp),
            ("", FilterButtons_Other),
            ("", FilterButtons_Time),
            ("\uE7B3",FilterButtons_Hide)];
        List<string> glyphs = [];
        foreach (var button in buttons) // Первый проход
        {
            if (button.Item2.IsChecked == true)
            {
                if (FiltersButton.Style != (Style)Application.Current.Resources["AccentButtonStyle"])
                {
                    FiltersButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
                }
                glyphs.Add(button.Item1);
            }
        }
        if (glyphs.Count == 0)
        {
            if (FiltersButton.Style != ActionButton_Apply.Style)
            {
                FiltersButton.Style = ActionButton_Apply.Style;
                FiltersButton.Translation = new System.Numerics.Vector3(0, 0, 20);
                FiltersButton.CornerRadius = new CornerRadius(4);
                FiltersButton.Shadow = SharedShadow;
            }
            foreach (var button in buttons) // Добавить все, так как мы не скрываем параметры
            {
                if (button.Item2 != FilterButtons_Hide)
                {
                    glyphs.Add(button.Item1);
                }
            }
        }
        if (SuggestBox.Text == string.Empty)
        {
            ResetVisibility();
        }
        var expanders = Helpers.VisualTreeHelper.FindVisualChildren<Expander>(MainScroll);
        foreach (var expander in expanders)
        {
            var stackPanels = Helpers.VisualTreeHelper.FindVisualChildren<StackPanel>(expander);
            var arrayStackPanels = stackPanels as StackPanel[] ?? stackPanels.ToArray(); 
            var anyVisible = false;

            foreach (var stackPanel in arrayStackPanels)
            {
                var textBlocks = Helpers.VisualTreeHelper.FindVisualChildren<FontIcon>(stackPanel).Where(tb => tb.FontSize - 15 == 0);
                var containsText = textBlocks.Any(tb => Helpers.VisualTreeHelper.FindAjantedFontIcons(tb, glyphs));

                var containsControl = Helpers.VisualTreeHelper.FindVisualChildren<CheckBox>(stackPanel).Any();

                // Если текст и элементы управления найдены, делаем StackPanel видимой
                if (containsText && containsControl)
                {
                    stackPanel.Visibility = Visibility.Visible;
                    anyVisible = true;

                    // Второй проход: делаем видимыми все дочерние элементы
                    Helpers.VisualTreeHelper.SetAllChildrenVisibility(stackPanel, Visibility.Visible);
                }
                else
                {
                    stackPanel.Visibility = Visibility.Collapsed;
                }

                var adjacentGrid = Helpers.VisualTreeHelper.FindAdjacentGrid(stackPanel);
                if (adjacentGrid != null)
                {
                    adjacentGrid.Visibility = stackPanel.Visibility;
                }
            }
            foreach (var secondStackPanel in arrayStackPanels) // Второй проход
            {
                if (secondStackPanel.Visibility == Visibility.Visible)
                {
                    Helpers.VisualTreeHelper.SetAllChildrenVisibility(secondStackPanel, Visibility.Visible);
                }
            }

            // Скрыть Expander если нет видимых StackPanels
            if (!anyVisible)
            {
                expander.IsExpanded = false;
            }
        }
        _isSearching = false;
    }

    private void FilterButtons_ResetButton_Click(object? sender, RoutedEventArgs? e)
    {
        _isSearching = true;
        FilterButtons_Freq.IsChecked = false;
        FilterButtons_Current.IsChecked = false;
        FilterButtons_Power.IsChecked = false;
        FilterButtons_Temp.IsChecked = false;
        FilterButtons_Other.IsChecked = false;
        FilterButtons_Time.IsChecked = false;
        FilterButtons_Hide.IsChecked = false;
        if (SuggestBox.Text != string.Empty)
        {
            ResetVisibility();
            SuggestBox_OnTextChanged(null, null);
        }
        _isSearching = false;
    }

    #endregion

    #endregion

    #region SMU Related voids and Quick SMU Commands

    private static void RunBackgroundTask(DoWorkEventHandler task, RunWorkerCompletedEventHandler completedHandler)
    {
        try
        {
            Task.Run(async () => await LogHelper.Log("Starting background task"));
            var backgroundWorker1 = new BackgroundWorker();
            backgroundWorker1.DoWork += task;
            backgroundWorker1.RunWorkerCompleted += completedHandler;
            backgroundWorker1.RunWorkerAsync();
        }
        catch
        {
            TraceIt_TraceError("Background Task Error");
        }
    }

    private void PopulateMailboxesList(ItemCollection l)
    {
        l.Clear();
        l.Add(new MailboxListItem("RSMU", _cpu?.smu.Rsmu!));
        l.Add(new MailboxListItem("MP1", _cpu?.smu.Mp1Smu!));
        l.Add(new MailboxListItem("HSMP", _cpu?.smu.Hsmp!));
    }

    private void AddMailboxToList(string label, SmuAddressSet addressSet) =>
        comboBoxMailboxSelect.Items.Add(new MailboxListItem(label, addressSet));

    private async void SmuScan_WorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
        try
        {
            var index = comboBoxMailboxSelect.SelectedIndex;
            PopulateMailboxesList(comboBoxMailboxSelect.Items);
            for (var i = 0; i < _matches?.Count; i++)
            {
                AddMailboxToList($"Mailbox {i + 1}", _matches[i]);
            }

            if (index > comboBoxMailboxSelect.Items.Count)
            {
                index = 0;
            }

            comboBoxMailboxSelect.SelectedIndex = index;
            QuickCommand.IsEnabled = true;
            await LogHelper.Log("SMU Scan Completed");
            await Send_Message("SMUScanText".GetLocalized(), "SMUScanDesc".GetLocalized(), Symbol.Message);
        }
        catch (Exception exception)
        {
            TraceIt_TraceError(exception.Message);
        }
    }

    private void BackgroundWorkerTrySettings_DoWork(object sender, DoWorkEventArgs e)
    {
        try
        {
            _cpu ??= new Cpu(CpuInitSettings.defaultSetttings);
            Task.Run(async () => await LogHelper.Log("Starting scanning SMU addresses task"));
            switch (_cpu.info.codeName)
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
            }
        }
        catch (Exception ex)
        {
            TraceIt_TraceError(ex.ToString());
        }
    }

    private void ScanSmuRange(uint start, uint end, uint step, uint offset)
    {
        _matches = [];
        Task.Run(async () => await LogHelper.Log("Starting scanning SMU range task"));
        var keyPairs = new List<KeyValuePair<uint, uint>>();

        while (start <= end)
        {
            var smuRspAddress = start + offset;

            if (_cpu?.ReadDword(start) != 0xFFFFFFFF)
            {
                // Send unknown command 0xFF to each pair of this start and possible response addresses
                if (_cpu?.WriteDwordEx(start, 0xFF) == true)
                {
                    Thread.Sleep(10);

                    while (smuRspAddress <= end)
                    {
                        // Expect UNKNOWN_CMD status to be returned if the mailbox works
                        if (_cpu?.ReadDword(smuRspAddress) == 0xFE)
                        {
                            // Send Get_SMU_Version command
                            if (_cpu?.WriteDwordEx(start, 0x2) == true)
                            {
                                Thread.Sleep(10);
                                if (_cpu?.ReadDword(smuRspAddress) == 0x1)
                                {
                                    keyPairs.Add(new KeyValuePair<uint, uint>(start, smuRspAddress));
                                }
                            }
                        }

                        smuRspAddress += step;
                    }
                }
            }

            start += step;
        }

        if (keyPairs.Count > 0)
        {
            foreach (var keyPair in keyPairs)
            {
                Console.WriteLine($"{keyPair.Key:X8}: {keyPair.Value:X8}");
                Task.Run(async () => await LogHelper.Log($"Found keypair: {keyPair.Key:X8}: {keyPair.Value:X8}"));
            }
            Console.WriteLine();
        }

        var possibleArgAddresses = new List<uint>();

        foreach (var pair in keyPairs)
        {
            Console.WriteLine($"Testing {pair.Key:X8}: {pair.Value:X8}");
            Task.Run(async () => await LogHelper.Log($"Testing keypair: {pair.Key:X8}: {pair.Value:X8}"));
            if (TrySettings(pair.Key, pair.Value, 0xFFFFFFAF, 0x2, 0xFF) == SMU.Status.OK) //ЗДЕСЬ БЫЛО FFFFFFFF
            {
                var smuArgAddress = pair.Value + 4;
                while (smuArgAddress <= end)
                {
                    if (_cpu?.ReadDword(smuArgAddress) == _cpu?.smu.Version)
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
                    {
                        if (_cpu?.ReadDword(address) != testArg + 1)
                        {
                            retries = -1;
                        }
                    }
                }

                if (retries == 0)
                {
                    _matches.Add(new SmuAddressSet(pair.Key, pair.Value, address));
                    break;
                }
            }
        }
        Task.Run(async () => await LogHelper.Log("Scanning SMU range task completed."));
    }

    private SMU.Status? TrySettings(uint msgAddr, uint rspAddr, uint argAddr, uint cmd, uint value)
    {
        var args = new uint[6];
        args[0] = value;

        _testMailbox.SMU_ADDR_MSG = msgAddr;
        _testMailbox.SMU_ADDR_RSP = rspAddr;
        _testMailbox.SMU_ADDR_ARG = argAddr;

        return _cpu?.smu.SendSmuCommand(_testMailbox, cmd, ref args);
    }

    private void ResetSmuAddresses()
    {
        textBoxCMDAddress.Text = $@"0x{Convert.ToString(_testMailbox.SMU_ADDR_MSG, 16).ToUpper()}";
        textBoxRSPAddress.Text = $@"0x{Convert.ToString(_testMailbox.SMU_ADDR_RSP, 16).ToUpper()}";
        textBoxARGAddress.Text = $@"0x{Convert.ToString(_testMailbox.SMU_ADDR_ARG, 16).ToUpper()}";
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        SmuSettingsLoad();
        ApplySettings(1, int.Parse((sender as Button)!.Name.Replace("Play_", "")));
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        SmuSettingsLoad();
        QuickDialog(1, int.Parse((sender as Button)!.Name.Replace("Edit_", "")));
    }

    //SMU КОМАНДЫ
    private void ApplySettings(int mode, int commandIndex)
    {
        try
        {
            Task.Run(async () => await LogHelper.Log("Applying user SMU settings task started."));
            uint[]? args;
            string[]? userArgs;
            uint addrMsg;
            uint addrRsp;
            uint addrArg;
            uint command;
            if (mode != 0)
            {
                SmuSettingsLoad();
                args = Utils.MakeCmdArgs();
                userArgs = _smusettings.QuickSmuCommands![commandIndex].Argument.Trim().Split(',');
                TryConvertToUint(_smusettings.MailBoxes![_smusettings.QuickSmuCommands![commandIndex].MailIndex].Cmd,
                    out addrMsg);
                TryConvertToUint(_smusettings.MailBoxes![_smusettings.QuickSmuCommands![commandIndex].MailIndex].Rsp,
                    out addrRsp);
                TryConvertToUint(_smusettings.MailBoxes![_smusettings.QuickSmuCommands![commandIndex].MailIndex].Arg,
                    out addrArg);
                TryConvertToUint(_smusettings.QuickSmuCommands![commandIndex].Command, out command);
            }
            else
            {
                args = Utils.MakeCmdArgs();
                userArgs = textBoxARG0.Text.Trim().Split(',');
                TryConvertToUint(textBoxCMDAddress.Text, out addrMsg);
                TryConvertToUint(textBoxRSPAddress.Text, out addrRsp);
                TryConvertToUint(textBoxARGAddress.Text, out addrArg);
                TryConvertToUint(textBoxCMD.Text, out command);
            }

            _testMailbox.SMU_ADDR_MSG = addrMsg;
            _testMailbox.SMU_ADDR_RSP = addrRsp;
            _testMailbox.SMU_ADDR_ARG = addrArg;
            for (var i = 0; i < userArgs.Length; i++)
            {
                if (i == args.Length)
                {
                    break;
                }

                TryConvertToUint(userArgs[i], out var temp);
                args[i] = temp;
            }

            Task.Run(async () =>
                await LogHelper.Log(
                    $"Sending SMU Command: {_smusettings.QuickSmuCommands?[commandIndex].Command}\n" +
                    $"Args: {_smusettings.QuickSmuCommands?[commandIndex].Argument}\n" +
                    $"Address MSG: {_testMailbox.SMU_ADDR_MSG}\n" +
                    $"Address RSP: {_testMailbox.SMU_ADDR_RSP}\n" +
                    $"Address ARG: {_testMailbox.SMU_ADDR_ARG}"));
            var status = _cpu?.smu.SendSmuCommand(_testMailbox, command, ref args);
            if (status != SMU.Status.OK)
            {
                ApplyInfo += "\n" + "SMUErrorText".GetLocalized() + ": " +
                             (textBoxCMD.Text.Contains("0x") ? textBoxCMD.Text : "0x" + textBoxCMD.Text)
                             + "Param_SMU_Args_From".GetLocalized() + comboBoxMailboxSelect.SelectedValue
                             + "Param_SMU_Args".GetLocalized() + (textBoxARG0.Text.Contains("0x")
                                 ? textBoxARG0.Text
                                 : "0x" + textBoxARG0.Text);
                if (status == SMU.Status.CMD_REJECTED_PREREQ)
                {
                    ApplyInfo += "\n" + "SMUErrorRejected".GetLocalized();
                }
                else
                {
                    ApplyInfo += "\n" + "SMUErrorNoCMD".GetLocalized();
                }
            }
            Task.Run(async () => await LogHelper.Log($"Get status: {status}"));
        }
        catch (Exception ex)
        {
            Task.Run(async () => await LogHelper.LogError($"Applying user SMU settings error: {ex.Message}"));
            ApplyInfo += "\n" + "SMUErrorDesc".GetLocalized();
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
    private void SMUOptions_Expander_Expanding(Expander sender, ExpanderExpandingEventArgs args)
    {
        if (_isSearching) { return; }
        RunBackgroundTask(BackgroundWorkerTrySettings_DoWork!, SmuScan_WorkerCompleted!);
    }

    private void ComboBoxMailboxSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (comboBoxMailboxSelect.SelectedItem is MailboxListItem item)
        {
            InitTestMailbox(item.MsgAddr, item.RspAddr, item.ArgAddr);
        }
    }

    private void InitTestMailbox(uint msgAddr, uint rspAddr, uint argAddr)
    {
        _testMailbox.SMU_ADDR_MSG = msgAddr;
        _testMailbox.SMU_ADDR_RSP = rspAddr;
        _testMailbox.SMU_ADDR_ARG = argAddr;
        ResetSmuAddresses();
    }

    private async void Mon_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await LogHelper.Log("Saku PowerMon Clicked to open. Showing dialog");
            var monDialog = new ContentDialog
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
                monDialog.XamlRoot = XamlRoot;
            }

            var result = await monDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await LogHelper.Log("Saku PowerMon starting...");
                var newWindow = new PowerWindow(_cpu);
                var micaBackdrop = new MicaBackdrop
                {
                    Kind = MicaKind.BaseAlt
                };
                newWindow.SystemBackdrop = micaBackdrop;
                newWindow.Activate();
            }
            else
            {
                await LogHelper.Log("Saku PowerMon dialog closed");
            }
        }
        catch (Exception exception)
        {
            TraceIt_TraceError(exception.ToString());
        }
    }

    private void SMUEnabl_Click(object sender, RoutedEventArgs e)
    {
        EnableSMU.IsOn = !EnableSMU.IsOn;
        SmuEnabl();
    }

    private void EnableSMU_Toggled(object sender, RoutedEventArgs e) => SmuEnabl();

    private void SmuEnabl()
    {
        if (EnableSMU.IsOn)
        {
            _profile[_indexprofile].smuEnabled = true;
            ProfileSave();
        }
        else
        {
            _profile[_indexprofile].smuEnabled = false;
            ProfileSave();
        }
    }

    private void CreateQuickCommandSMU_Click(object sender, RoutedEventArgs e) => QuickDialog(0, 0);
    private void CreateQuickCommandSMU1_Click(object sender, RoutedEventArgs e) => RangeDialog();

    private async void QuickDialog(int destination, int rowindex)
    {
        try
        {
            await LogHelper.Log("Showing Quick SMU Commands dialog");
            _smuSymbol1 = new FontIcon
            {
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                Glyph = _smuSymbol,
                Margin = new Thickness(-4, -2, -5, -5)
            };
            var symbolButton = new Button
            {
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(320, 60, 0, 0),
                Width = 40,
                Height = 40,
                Content = new ContentControl
                {
                    Content = _smuSymbol1
                }
            };
            var comboSelSmu = new ComboBox
            {
                Margin = new Thickness(0, 20, 0, 0),
                VerticalAlignment = VerticalAlignment.Top
            };
            var mainText = new TextBox
            {
                Margin = new Thickness(0, 60, 0, 0),
                PlaceholderText = "New_Name".GetLocalized(),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Height = 39.5,
                Width = 315
            };
            var descText = new TextBox
            {
                HorizontalAlignment = HorizontalAlignment.Left,
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
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Height = 40,
                Width = 176
            };
            var argText = new TextBox
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(180, 152, 0, 0),
                PlaceholderText = "Arguments".GetLocalized(),
                Height = 40,
                Width = 179
            };
            var autoRun = new CheckBox
            {
                Margin = new Thickness(1, 195, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Content = "Param_Autorun".GetLocalized(),
                IsChecked = false
            };
            var applyWith = new CheckBox
            {
                Margin = new Thickness(1, 225, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Content = "Param_WithApply".GetLocalized(),
                IsChecked = false
            };
            try
            {
                foreach (var item in comboBoxMailboxSelect.Items)
                {
                    comboSelSmu.Items.Add(item);
                }

                comboSelSmu.SelectedIndex = comboBoxMailboxSelect.SelectedIndex;
                comboSelSmu.SelectionChanged += ComboSelSMU_SelectionChanged;
                symbolButton.Click += SymbolButton_Click;
                if (destination != 0)
                {
                    SmuSettingsLoad();
                    _smuSymbol = _smusettings.QuickSmuCommands![rowindex].Symbol;
                    _smuSymbol1.Glyph = _smusettings.QuickSmuCommands![rowindex].Symbol;
                    comboSelSmu.SelectedIndex = _smusettings.QuickSmuCommands![rowindex].MailIndex;
                    mainText.Text = _smusettings.QuickSmuCommands![rowindex].Name;
                    descText.Text = _smusettings.QuickSmuCommands![rowindex].Description;
                    cmdText.Text = _smusettings.QuickSmuCommands![rowindex].Command;
                    argText.Text = _smusettings.QuickSmuCommands![rowindex].Argument;
                    autoRun.IsChecked = _smusettings.QuickSmuCommands![rowindex].Startup;
                    applyWith.IsChecked = _smusettings.QuickSmuCommands![rowindex].ApplyWith;
                }
            }
            catch (Exception ex)
            {
                TraceIt_TraceError(ex.ToString());
            }

            try
            {
                var newQuickCommand = new ContentDialog
                {
                    Title = "AdvancedCooler_Del_Action".GetLocalized(),
                    Content = new Grid
                    {
                        Children =
                        {
                            comboSelSmu,
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

                newQuickCommand.Closed += (_, _) =>
                {
                    newQuickCommand = null;
                };
                // Отобразить ContentDialog и обработать результат
                try
                {
                    var result = await newQuickCommand.ShowAsync();
                    // Создать ContentDialog 
                    if (result == ContentDialogResult.Primary)
                    {
                        await LogHelper.Log("Adding new Quick SMU Command");
                        SmuSettingsLoad();
                        var saveIndex = comboSelSmu.SelectedIndex;
                        for (var i = 0; i < comboSelSmu.Items.Count; i++)
                        {
                            var adressName = false;
                            comboSelSmu.SelectedIndex = i;
                            if (_smusettings?.MailBoxes == null && _smusettings != null)
                            {
                                _smusettings.MailBoxes =
                                [
                                    new CustomMailBoxes
                                    {
                                        Name = comboSelSmu.SelectedItem.ToString()!,
                                        Cmd = textBoxCMDAddress.Text,
                                        Rsp = textBoxRSPAddress.Text,
                                        Arg = textBoxARGAddress.Text
                                    }
                                ];
                            }
                            else
                            {
                                for (var d = 0; d < _smusettings?.MailBoxes?.Count; d++)
                                {
                                    if (_smusettings.MailBoxes[d].Name != string.Empty &&
                                        _smusettings.MailBoxes[d].Name == comboSelSmu.SelectedItem.ToString())
                                    {
                                        adressName = true;
                                        break;
                                    }
                                }

                                if (adressName == false)
                                {
                                    _smusettings?.MailBoxes?.Add(new CustomMailBoxes
                                    {
                                        Name = comboSelSmu.SelectedItem.ToString()!,
                                        Cmd = textBoxCMDAddress.Text,
                                        Rsp = textBoxRSPAddress.Text,
                                        Arg = textBoxARGAddress.Text
                                    });
                                }
                            }
                        }

                        SmuSettingsSave();
                        if (cmdText.Text != string.Empty && argText.Text != string.Empty && _smusettings != null)
                        {
                            var run = false;
                            var apply = false;
                            if (autoRun.IsChecked == true)
                            {
                                run = true;
                            }

                            if (applyWith.IsChecked == true)
                            {
                                apply = true;
                            }

                            if (destination == 0)
                            {
                                _smusettings.QuickSmuCommands ??= [];
                                _smusettings.QuickSmuCommands.Add(new QuickSmuCommands
                                {
                                    Name = mainText.Text,
                                    Description = descText.Text,
                                    Symbol = _smuSymbol,
                                    MailIndex = saveIndex,
                                    Startup = run,
                                    ApplyWith = apply,
                                    Command = cmdText.Text,
                                    Argument = argText.Text
                                });
                            }
                            else
                            {
                                _smusettings.QuickSmuCommands![rowindex].Symbol = _smuSymbol;
                                _smusettings.QuickSmuCommands![rowindex].Symbol = _smuSymbol1.Glyph;
                                _smusettings.QuickSmuCommands![rowindex].MailIndex = saveIndex;
                                _smusettings.QuickSmuCommands![rowindex].Name = mainText.Text;
                                _smusettings.QuickSmuCommands![rowindex].Description = descText.Text;
                                _smusettings.QuickSmuCommands![rowindex].Command = cmdText.Text;
                                _smusettings.QuickSmuCommands![rowindex].Argument = argText.Text;
                                _smusettings.QuickSmuCommands![rowindex].Startup = run;
                                _smusettings.QuickSmuCommands![rowindex].ApplyWith = apply;
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
                            await LogHelper.Log("Removing Quick SMU Command");
                            SmuSettingsLoad();
                            _smusettings.QuickSmuCommands!.RemoveAt(rowindex);
                            SmuSettingsSave();
                            Init_QuickSMU();
                        }
                        else
                        {
                            await LogHelper.Log("Exiting Quick SMU Commands dialog");
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
            catch (Exception ex)
            {
                TraceIt_TraceError(ex.ToString());
            }
        }
        catch (Exception e)
        {
            TraceIt_TraceError(e.ToString());
        }
    }

    private async void RangeDialog()
    {
        try
        {
            await LogHelper.Log("SMU Apply Range Dialog opened");
            var comboSelSmu = new ComboBox
            {
                Margin = new Thickness(0, 20, 0, 0),
                VerticalAlignment = VerticalAlignment.Top
            };
            var cmdStart = new TextBox
            {
                Margin = new Thickness(0, 60, 0, 0),
                PlaceholderText = "Command".GetLocalized(),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Height = 40,
                Width = 360
            };
            var argStart = new TextBox
            {
                Margin = new Thickness(0, 105, 0, 0),
                PlaceholderText = "Param_Start".GetLocalized(),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Height = 40,
                Width = 176
            };
            var argEnd = new TextBox
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(180, 105, 0, 0),
                PlaceholderText = "Param_EndW".GetLocalized(),
                Height = 40,
                Width = 179
            };
            var autoRun = new CheckBox
            {
                Margin = new Thickness(1, 155, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Content = "Logging".GetLocalized(),
                IsChecked = false
            };
            try
            {
                foreach (var item in comboBoxMailboxSelect.Items)
                {
                    comboSelSmu.Items.Add(item);
                }

                comboSelSmu.SelectedIndex = comboBoxMailboxSelect.SelectedIndex;
                comboSelSmu.SelectionChanged += ComboSelSMU_SelectionChanged;
            }
            catch (Exception ex)
            {
                TraceIt_TraceError(ex.ToString());
            }

            try
            {
                var newQuickCommand = new ContentDialog
                {
                    Title = "AdvancedCooler_Del_Action".GetLocalized(),
                    Content = new Grid
                    {
                        Children =
                        {
                            comboSelSmu,
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

                newQuickCommand.Closed += (_, _) =>
                {
                    newQuickCommand = null;
                };
                // Отобразить ContentDialog и обработать результат
                try
                {
                    var result = await newQuickCommand.ShowAsync();
                    // Создать ContentDialog 
                    if (result == ContentDialogResult.Primary)
                    {
                        await LogHelper.Log("SMU Apply Range: applying range... Log will be saved to another file");
                        SmuSettingsLoad();
                        var saveIndex = comboSelSmu.SelectedIndex;
                        for (var i = 0; i < comboSelSmu.Items.Count; i++)
                        {
                            var adressName = false;
                            comboSelSmu.SelectedIndex = i;
                            if (_smusettings.MailBoxes == null)
                            {
                                _smusettings.MailBoxes = [];
                                _smusettings.MailBoxes?.Add(new CustomMailBoxes
                                {
                                    Name = comboSelSmu.SelectedItem.ToString()!,
                                    Cmd = textBoxCMDAddress.Text,
                                    Rsp = textBoxRSPAddress.Text,
                                    Arg = textBoxARGAddress.Text
                                });
                            }
                            else
                            {
                                for (var d = 0; d < _smusettings.MailBoxes?.Count; d++)
                                {
                                    if (_smusettings.MailBoxes != null &&
                                        _smusettings.MailBoxes[d].Name != string.Empty &&
                                        _smusettings.MailBoxes[d].Name == comboSelSmu.SelectedItem.ToString())
                                    {
                                        adressName = true;
                                        break;
                                    }
                                }

                                if (adressName == false)
                                {
                                    _smusettings.MailBoxes?.Add(new CustomMailBoxes
                                    {
                                        Name = comboSelSmu.SelectedItem.ToString()!,
                                        Cmd = textBoxCMDAddress.Text,
                                        Rsp = textBoxRSPAddress.Text,
                                        Arg = textBoxARGAddress.Text
                                    });
                                }
                            }
                        }

                        SmuSettingsSave();
                        var run = false;
                        if (cmdStart.Text != string.Empty && argStart.Text != string.Empty &&
                            argEnd.Text != string.Empty)
                        {
                            if (autoRun.IsChecked == true)
                            {
                                run = true;
                            }

                            _cpusend?.SendRange(cmdStart.Text, argStart.Text, argEnd.Text, saveIndex, run);
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
                        await LogHelper.Log("SMU Apply Range Dialog closed");
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
            catch (Exception ex)
            {
                TraceIt_TraceError(ex.ToString());
            }
        }
        catch (Exception e)
        {
            TraceIt_TraceError(e.ToString());
        }
    }

    private void SymbolButton_Click(object sender, RoutedEventArgs e) => SymbolFlyout.ShowAt(sender as Button);

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
        catch (Exception ex)
        {
            TraceIt_TraceError(ex.ToString());
        }
    }

    private void SymbolList_ItemClick(object sender, ItemClickEventArgs e)
    {
        var glypher = (FontIcon)e.ClickedItem;
        if (glypher != null)
        {
            _smuSymbol = glypher.Glyph;
            _smuSymbol1!.Glyph = glypher.Glyph;
        }
    }

    private void SMUNotes_TextChanged(object sender, RoutedEventArgs e)
    {
        SmuSettingsLoad();
        var documentRange = SMUNotes.Document.GetRange(0, TextConstants.MaxUnitCount);
        documentRange.GetText(TextGetOptions.FormatRtf, out var content);
        _smusettings.Note = content.TrimEnd();
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
            catch (Exception ex)
            {
                TraceIt_TraceError(ex.ToString());
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
            catch (Exception ex)
            {
                TraceIt_TraceError(ex.ToString());
            }
        }
    }

    private void CopyThis_Click(object sender, RoutedEventArgs e)
    {
        if (textBoxARG0.SelectedText != "")
        {
            // Скопировать текст в буфер обмена
            var dataPackage = new DataPackage
            {
                RequestedOperation = DataPackageOperation.Copy
            };
            dataPackage.SetText(textBoxARG0.SelectedText);
            Clipboard.SetContent(dataPackage);
        }
        else
        {
            // Выделить весь текст
            textBoxARG0.SelectAll();
            // Скопировать текст в буфер обмена
            var dataPackage = new DataPackage
            {
                RequestedOperation = DataPackageOperation.Copy
            };
            dataPackage.SetText(textBoxARG0.Text);
            Clipboard.SetContent(dataPackage);
        }
    }

    private void CutThis_Click(object sender, RoutedEventArgs e)
    {
        if (textBoxARG0.SelectedText != "")
        {
            // Скопировать текст в буфер обмена
            var dataPackage = new DataPackage
            {
                RequestedOperation = DataPackageOperation.Copy
            };
            dataPackage.SetText(textBoxARG0.SelectedText);
            Clipboard.SetContent(dataPackage);
            // Обнулить текст
            textBoxARG0.SelectedText = "";
        }
        else
        {
            // Выделить весь текст
            textBoxARG0.SelectAll();
            // Скопировать текст в буфер обмена
            var dataPackage = new DataPackage
            {
                RequestedOperation = DataPackageOperation.Copy
            };
            dataPackage.SetText(textBoxARG0.Text);
            Clipboard.SetContent(dataPackage);
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
        _cpusend?.CancelRange();
        CloseInfoRange();
    }

    private void CloseInfoRange() => RangeStarted.IsOpen = false;

    //Send Message
    private async Task Send_Message(string msg, string submsg, Symbol symbol)
    {
        UniToolTip.IconSource = new SymbolIconSource
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
        try
        {
            while (_isLoaded == false || _waitforload)
            {
                await Task.Delay(100);
            }

            if (ProfileCOM.SelectedIndex != -1)
            {
                AppSettings.Preset = ProfileCOM.SelectedIndex - 1;
                AppSettings.SaveSettings();
            }

            _indexprofile = ProfileCOM.SelectedIndex - 1;
            MainInit(ProfileCOM.SelectedIndex - 1);
        }
        catch (Exception exception)
        {
            TraceIt_TraceError(exception.ToString());
        }
    }

    //Параметры процессора
    //Максимальная температура CPU (C)
    private void C1_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = c1.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].cpu1 = check;
            _profile[_indexprofile].cpu1value = c1v.Value;
            ProfileSave();
        }
    }

    //Лимит CPU (W)
    private void C2_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = c2.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].cpu2 = check;
            _profile[_indexprofile].cpu2value = c2v.Value;
            ProfileSave();
        }
    }

    //Реальный CPU (W)
    private void C3_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = c3.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].cpu3 = check;
            _profile[_indexprofile].cpu3value = c3v.Value;
            ProfileSave();
        }
    }

    //Средний CPU (W)
    private void C4_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = c4.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].cpu4 = check;
            _profile[_indexprofile].cpu4value = c4v.Value;
            ProfileSave();
        }
    }

    //Тик быстрого разгона (S)
    private void C5_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = c5.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].cpu5 = check;
            _profile[_indexprofile].cpu5value = c5v.Value;
            ProfileSave();
        }
    }

    //Тик медленного разгона (S)
    private void C6_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = c6.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].cpu6 = check;
            _profile[_indexprofile].cpu6value = c6v.Value;
            ProfileSave();
        }
    }

    //Параметры VRM
    //Максимальный ток VRM A
    private void V1_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = V1.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].vrm1 = check;
            _profile[_indexprofile].vrm1value = V1V.Value;
            ProfileSave();
        }
    }

    //Лимит по току VRM A
    private void V2_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = V2.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].vrm2 = check;
            _profile[_indexprofile].vrm2value = V2V.Value;
            ProfileSave();
        }
    }

    //Максимальный ток SOC A
    private void V3_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = V3.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].vrm3 = check;
            _profile[_indexprofile].vrm3value = V3V.Value;
            ProfileSave();
        }
    }

    //Лимит по току SOC A
    private void V4_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = V4.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].vrm4 = check;
            _profile[_indexprofile].vrm4value = V4V.Value;
            ProfileSave();
        }
    }

    //Максимальный ток PCI VDD A
    private void V5_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = V5.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].vrm5 = check;
            _profile[_indexprofile].vrm5value = V5V.Value;
            ProfileSave();
        }
    }

    //Максимальный ток PCI SOC A
    private void V6_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = V6.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].vrm6 = check;
            _profile[_indexprofile].vrm6value = V6V.Value;
            ProfileSave();
        }
    }

    //Отключить троттлинг на время
    private void V7_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = V7.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].vrm7 = check;
            _profile[_indexprofile].vrm7value = V7V.Value;
            ProfileSave();
        }
    }

    //Параметры графики
    //Минимальная частота SOC 
    private void G1_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = g1.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu1 = check;
            _profile[_indexprofile].gpu1value = g1v.Value;
            ProfileSave();
        }
    }

    //Максимальная частота SOC
    private void G2_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = g2.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu2 = check;
            _profile[_indexprofile].gpu2value = g2v.Value;
            ProfileSave();
        }
    }

    //Минимальная частота Infinity Fabric
    private void G3_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = g3.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu3 = check;
            _profile[_indexprofile].gpu3value = g3v.Value;
            ProfileSave();
        }
    }

    //Максимальная частота Infinity Fabric
    private void G4_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = g4.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu4 = check;
            _profile[_indexprofile].gpu4value = g4v.Value;
            ProfileSave();
        }
    }

    //Минимальная частота кодека VCE
    private void G5_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = g5.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu5 = check;
            _profile[_indexprofile].gpu5value = g5v.Value;
            ProfileSave();
        }
    }

    //Максимальная частота кодека VCE
    private void G6_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = g6.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu6 = check;
            _profile[_indexprofile].gpu6value = g6v.Value;
            ProfileSave();
        }
    }

    //Минимальная частота частота Data Latch
    private void G7_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = g7.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu7 = check;
            _profile[_indexprofile].gpu7value = g7v.Value;
            ProfileSave();
        }
    }

    //Максимальная частота Data Latch
    private void G8_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = g8.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu8 = check;
            _profile[_indexprofile].gpu8value = g8v.Value;
            ProfileSave();
        }
    }

    //Минимальная частота iGpu
    private void G9_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = g9.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu9 = check;
            _profile[_indexprofile].gpu9value = g9v.Value;
            ProfileSave();
        }
    }

    //Максимальная частота iGpu
    private void G10_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = g10.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu10 = check;
            _profile[_indexprofile].gpu10value = g10v.Value;
            ProfileSave();
        }
    }

    //Расширенные параметры
    private void A1_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = a1.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].advncd1 = check;
            _profile[_indexprofile].advncd1value = a1v.Value;
            ProfileSave();
        }
    }


    private void A3_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = a3.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].advncd3 = check;
            _profile[_indexprofile].advncd3value = a3v.Value;
            ProfileSave();
        }
    }

    private void A4_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = a4.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].advncd4 = check;
            _profile[_indexprofile].advncd4value = a4v.Value;
            ProfileSave();
        }
    }

    private void A5_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = a5.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].advncd5 = check;
            _profile[_indexprofile].advncd5value = a5v.Value;
            ProfileSave();
        }
    }

    private void A6_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = a6.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].advncd6 = check;
            _profile[_indexprofile].advncd6value = a6v.Value;
            ProfileSave();
        }
    }

    private void A7_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = a7.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].advncd7 = check;
            _profile[_indexprofile].advncd7value = a7v.Value;
            ProfileSave();
        }
    }

    private void A8_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = a8.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].advncd8 = check;
            _profile[_indexprofile].advncd8value = a8v.Value;
            ProfileSave();
        }
    }

    private void A9_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = a9.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].advncd9 = check;
            _profile[_indexprofile].advncd9value = a9v.Value;
            ProfileSave();
        }
    }

    private void A10_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = a10.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].advncd10 = check;
            _profile[_indexprofile].advncd10value = a10v.Value;
            ProfileSave();
        }
    }

    private void A11_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = a11.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].advncd11 = check;
            _profile[_indexprofile].advncd11value = a11v.Value;
            ProfileSave();
        }
    }

    private void A12_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = a12.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].advncd12 = check;
            _profile[_indexprofile].advncd12value = a12v.Value;
            ProfileSave();
        }
    }

    private void A13_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = a13.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].advncd13 = check;
            _profile[_indexprofile].advncd1value = a13m.SelectedIndex;
            ProfileSave();
        }
    }

    //Оптимизатор кривой
    private void CCD2_8_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = CCD2_8.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper15 = check;
            _profile[_indexprofile].coper15value = CCD2_8v.Value;
            ProfileSave();
        }
    }

    private void CCD2_7_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = CCD2_7.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper14 = check;
            _profile[_indexprofile].coper14value = CCD2_7v.Value;
            ProfileSave();
        }
    }

    private void CCD2_6_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = CCD2_6.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper13 = check;
            _profile[_indexprofile].coper13value = CCD2_6v.Value;
            ProfileSave();
        }
    }

    private void CCD2_5_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = CCD2_5.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper12 = check;
            _profile[_indexprofile].coper12value = CCD2_5v.Value;
            ProfileSave();
        }
    }

    private void CCD2_4_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = CCD2_4.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper11 = check;
            _profile[_indexprofile].coper11value = CCD2_4v.Value;
            ProfileSave();
        }
    }

    private void CCD2_3_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = CCD2_3.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper10 = check;
            _profile[_indexprofile].coper10value = CCD2_3v.Value;
            ProfileSave();
        }
    }

    private void CCD2_2_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = CCD2_2.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper9 = check;
            _profile[_indexprofile].coper9value = CCD2_2v.Value;
            ProfileSave();
        }
    }

    private void CCD2_1_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = CCD2_1.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper8 = check;
            _profile[_indexprofile].coper8value = CCD2_1v.Value;
            ProfileSave();
        }
    }

    private void CCD1_8_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = CCD1_8.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper7 = check;
            _profile[_indexprofile].coper7value = CCD1_8v.Value;
            ProfileSave();
        }
    }

    private void CCD1_7_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = CCD1_7.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper6 = check;
            _profile[_indexprofile].coper6value = CCD1_7v.Value;
            ProfileSave();
        }
    }

    private void CCD1_6_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = CCD1_6.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper5 = check;
            _profile[_indexprofile].coper5value = CCD1_6v.Value;
            ProfileSave();
        }
    }

    private void CCD1_5_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = CCD1_5.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper4 = check;
            _profile[_indexprofile].coper4value = CCD1_5v.Value;
            ProfileSave();
        }
    }

    private void CCD1_4_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = CCD1_4.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper3 = check;
            _profile[_indexprofile].coper3value = CCD1_4v.Value;
            ProfileSave();
        }
    }

    private void CCD1_3_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = CCD1_3.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper2 = check;
            _profile[_indexprofile].coper2value = CCD1_3v.Value;
            ProfileSave();
        }
    }

    private void CCD1_2_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = CCD1_2.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper1 = check;
            _profile[_indexprofile].coper1value = CCD1_2v.Value;
            ProfileSave();
        }
    }

    private void CCD1_1_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = CCD1_1.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper0 = check;
            _profile[_indexprofile].coper0value = CCD1_1v.Value;
            ProfileSave();
        }
    }

    private void O1_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = O1.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coall = check;
            _profile[_indexprofile].coallvalue = O1v.Value;
            ProfileSave();
        }
    }

    private void O2_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = O2.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].cogfx = check;
            _profile[_indexprofile].cogfxvalue = O2v.Value;
            ProfileSave();
        }
    }

    private void CCD_CO_Mode_Sel_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        if (CCD_CO_Mode.SelectedIndex > 0 && CCD_CO_Mode_Sel.IsChecked == true)
        {
            HideDisabledCurveOptimizedParameters(true); //Оставить параметры изменения кривой
        }
        else
        {
            HideDisabledCurveOptimizedParameters(false); //Убрать параметры
        }

        ProfileLoad();
        var check = CCD_CO_Mode_Sel.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].comode = check;
            _profile[_indexprofile].coprefmode = CCD_CO_Mode.SelectedIndex;
            ProfileSave();
        }
    }

    //Параметры процессора, при изменении слайдеров
    //Максимальная температура CPU (C)
    private void C1_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].cpu1value = c1v.Value;
            ProfileSave();
        }
    }

    //Лимит CPU (W)
    private void C2_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].cpu2value = c2v.Value;
            ProfileSave();
        }
    }

    //Реальный CPU (W)
    private void C3_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].cpu3value = c3v.Value;
            ProfileSave();
        }
    }

    //Средний CPU(W)
    private void C4_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].cpu4value = c4v.Value;
            ProfileSave();
        }
    }

    //Тик быстрого разгона (S)
    private void C5_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].cpu5value = c5v.Value;
            ProfileSave();
        }
    }

    //Тик медленного разгона (S)
    private void C6_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].cpu6value = c6v.Value;
            ProfileSave();
        }
    }

    //Параметры VRM
    private void V1v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].vrm1value = V1V.Value;
            ProfileSave();
        }
    }

    private void V2v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].vrm2value = V2V.Value;
            ProfileSave();
        }
    }

    private void V3v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].vrm3value = V3V.Value;
            ProfileSave();
        }
    }

    private void V4v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].vrm4value = V4V.Value;
            ProfileSave();
        }
    }

    private void V5v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].vrm5value = V5V.Value;
            ProfileSave();
        }
    }

    private void V6v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].vrm6value = V6V.Value;
            ProfileSave();
        }
    }

    private void V7v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].vrm7value = V7V.Value;
            ProfileSave();
        }
    }

    //Параметры GPU
    private void G1v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu1value = g1v.Value;
            ProfileSave();
        }
    }

    private void G2v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu2value = g2v.Value;
            ProfileSave();
        }
    }

    private void G3v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu3value = g3v.Value;
            ProfileSave();
        }
    }

    private void G4v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu4value = g4v.Value;
            ProfileSave();
        }
    }

    private void G5v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu5value = g5v.Value;
            ProfileSave();
        }
    }

    private void G6v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu6value = g6v.Value;
            ProfileSave();
        }
    }

    private void G7v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu7value = g7v.Value;
            ProfileSave();
        }
    }

    private void G8v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu8value = g8v.Value;
            ProfileSave();
        }
    }

    private void G9v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu9value = g9v.Value;
            ProfileSave();
        }
    }

    private void G10v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu10value = g10v.Value;
            ProfileSave();
        }
    }

    //Расширенные параметры
    private void A1v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].advncd1value = a1v.Value;
            ProfileSave();
        }
    }

    private void A3v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].advncd3value = a3v.Value;
            ProfileSave();
        }
    }

    private void A4v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].advncd4value = a4v.Value;
            ProfileSave();
        }
    }

    private void A5v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].advncd5value = a5v.Value;
            ProfileSave();
        }
    }

    private void A6v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].advncd6value = a6v.Value;
            ProfileSave();
        }
    }

    private void A7v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].advncd7value = a7v.Value;
            ProfileSave();
        }
    }

    private void A8v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].advncd8value = a8v.Value;
            ProfileSave();
        }
    }

    private void A9v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].advncd9value = a9v.Value;
            ProfileSave();
        }
    }

    private void A10v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].advncd10value = a10v.Value;
            ProfileSave();
        }
    }

    private void A11v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].advncd11value = a11v.Value;
            ProfileSave();
        }
    }

    private void A12v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].advncd12value = a12v.Value;
            ProfileSave();
        }
    }

    private void A13m_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].advncd13value = a13m.SelectedIndex;
            ProfileSave();
        }
    }

    //Новые
    private void C7_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = c7.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].cpu7 = check;
            _profile[_indexprofile].cpu7value = c7v.Value;
            ProfileSave();
        }
    }

    private void C7_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].cpu7value = c7v.Value;
            ProfileSave();
        }
    }

    private void G11_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = g11.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu11 = check;
            _profile[_indexprofile].gpu11value = g11v.Value;
            ProfileSave();
        }
    }

    private void G11v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu11value = g11v.Value;
            ProfileSave();
        }
    }

    private void G12_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = g12.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu12 = check;
            _profile[_indexprofile].gpu12value = g12v.Value;
            ProfileSave();
        }
    }

    private void G12v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu12value = g12v.Value;
            ProfileSave();
        }
    }

    private void G16_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = g16.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu16 = check;
            _profile[_indexprofile].gpu16value = g16m.SelectedIndex;
            ProfileSave();
        }
    }

    private void G16m_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu16value = g16m.SelectedIndex;
            ProfileSave();
        }
    }

    private void A14_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = a14.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].advncd14 = check;
            _profile[_indexprofile].advncd14value = a14m.SelectedIndex;
            ProfileSave();
        }
    }

    private void A14m_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].advncd14value = a14m.SelectedIndex;
            ProfileSave();
        }
    }

    private void A15_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = a15.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].advncd15 = check;
            _profile[_indexprofile].advncd15value = a15v.Value;
            ProfileSave();
        }
    }

    private void A15v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].advncd15value = a15v.Value;
            ProfileSave();
        }
    }

    //Слайдеры из оптимизатора кривой 
    private void O1v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coallvalue = O1v.Value;
            ProfileSave();
        }
    }

    private void O2v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].cogfxvalue = O2v.Value;
            ProfileSave();
        }
    }

    private void CCD1_1v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper0value = CCD1_1v.Value;
            ProfileSave();
        }
    }

    private void CCD1_2v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper1value = CCD1_2v.Value;
            ProfileSave();
        }
    }

    private void CCD1_3v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper2value = CCD1_3v.Value;
            ProfileSave();
        }
    }

    private void CCD1_4v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper3value = CCD1_4v.Value;
            ProfileSave();
        }
    }

    private void CCD1_5v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper4value = CCD1_5v.Value;
            ProfileSave();
        }
    }

    private void CCD1_6v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper5value = CCD1_6v.Value;
            ProfileSave();
        }
    }

    private void CCD1_7v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper6value = CCD1_7v.Value;
            ProfileSave();
        }
    }

    private void CCD1_8v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper7value = CCD1_8v.Value;
            ProfileSave();
        }
    }

    private void CCD2_1v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper8value = CCD2_1v.Value;
            ProfileSave();
        }
    }

    private void CCD2_2v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper9value = CCD2_2v.Value;
            ProfileSave();
        }
    }

    private void CCD2_3v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper10value = CCD2_3v.Value;
            ProfileSave();
        }
    }

    private void CCD2_4v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper11value = CCD2_4v.Value;
            ProfileSave();
        }
    }

    private void CCD2_5v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper12value = CCD2_5v.Value;
            ProfileSave();
        }
    }

    private void CCD2_6v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper13value = CCD2_6v.Value;
            ProfileSave();
        }
    }

    private void CCD2_7v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper14value = CCD2_7v.Value;
            ProfileSave();
        }
    }

    private void CCD2_8v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coper15value = CCD2_8v.Value;
            ProfileSave();
        }
    }

    private void CCD_CO_Mode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        if (CCD_CO_Mode.SelectedIndex > 0 && CCD_CO_Mode_Sel.IsChecked == true)
        {
            HideDisabledCurveOptimizedParameters(true); //Оставить параметры изменения кривой
        }
        else
        {
            HideDisabledCurveOptimizedParameters(false); //Убрать параметры
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].coprefmode = CCD_CO_Mode.SelectedIndex;
            ProfileSave();
        }
    }

    //Кнопка применить, итоговый выход, Zen States-Core SMU Command
    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await LogHelper.Log("Applying user settings.");
            if (c1.IsChecked == true)
            {
                _adjline += " --tctl-temp=" + c1v.Value;
            }

            if (c2.IsChecked == true)
            {
                _adjline += " --stapm-limit=" + c2v.Value + "000";
            }

            if (c3.IsChecked == true)
            {
                _adjline += " --fast-limit=" + c3v.Value + "000";
            }

            if (c4.IsChecked == true)
            {
                _adjline += " --slow-limit=" + c4v.Value + "000";
            }

            if (c5.IsChecked == true)
            {
                _adjline += " --stapm-time=" + c5v.Value;
            }

            if (c6.IsChecked == true)
            {
                _adjline += " --slow-time=" + c6v.Value;
            }

            if (c7.IsChecked == true)
            {
                _adjline += " --cHTC-temp=" + c7v.Value;
            }

            //vrm
            if (V1.IsChecked == true)
            {
                _adjline += " --vrmmax-current=" + V1V.Value + "000";
            }

            if (V2.IsChecked == true)
            {
                _adjline += " --vrm-current=" + V2V.Value + "000";
            }

            if (V3.IsChecked == true)
            {
                _adjline += " --vrmsocmax-current=" + V3V.Value + "000";
            }

            if (V4.IsChecked == true)
            {
                _adjline += " --vrmsoc-current=" + V4V.Value + "000";
            }

            if (V5.IsChecked == true)
            {
                _adjline += " --psi0-current=" + V5V.Value + "000";
            }

            if (V6.IsChecked == true)
            {
                _adjline += " --psi0soc-current=" + V6V.Value + "000";
            }

            if (V7.IsChecked == true)
            {
                _adjline += " --prochot-deassertion-ramp=" + V7V.Value;
            }

            //gpu
            if (g1.IsChecked == true)
            {
                _adjline += " --min-socclk-frequency=" + g1v.Value;
            }

            if (g2.IsChecked == true)
            {
                _adjline += " --max-socclk-frequency=" + g2v.Value;
            }

            if (g3.IsChecked == true)
            {
                _adjline += " --min-fclk-frequency=" + g3v.Value;
            }

            if (g4.IsChecked == true)
            {
                _adjline += " --max-fclk-frequency=" + g4v.Value;
            }

            if (g5.IsChecked == true)
            {
                _adjline += " --min-vcn=" + g5v.Value;
            }

            if (g6.IsChecked == true)
            {
                _adjline += " --max-vcn=" + g6v.Value;
            }

            if (g7.IsChecked == true)
            {
                _adjline += " --min-lclk=" + g7v.Value;
            }

            if (g8.IsChecked == true)
            {
                _adjline += " --max-lclk=" + g8v.Value;
            }

            if (g9.IsChecked == true)
            {
                _adjline += " --min-gfxclk=" + g9v.Value;
            }

            if (g10.IsChecked == true)
            {
                _adjline += " --max-gfxclk=" + g10v.Value;
            }

            if (g11.IsChecked == true)
            {
                _adjline += " --min-cpuclk=" + g11v.Value;
            }

            if (g12.IsChecked == true)
            {
                _adjline += " --max-cpuclk=" + g12v.Value;
            }


            if (g16.IsChecked == true)
            {
                if (g16m.SelectedIndex != 0)
                {
                    _adjline += " --setcpu-freqto-ramstate=" + (g16m.SelectedIndex - 1);
                }
                else
                {
                    _adjline += " --stopcpu-freqto-ramstate=0";
                }
            }

            //advanced
            if (a1.IsChecked == true)
            {
                _adjline += " --vrmgfx-current=" + a1v.Value + "000";
            }


            if (a3.IsChecked == true)
            {
                _adjline += " --vrmgfxmax_current=" + a3v.Value + "000";
            }

            if (a4.IsChecked == true)
            {
                _adjline += " --psi3cpu_current=" + a4v.Value + "000";
            }

            if (a5.IsChecked == true)
            {
                _adjline += " --psi3gfx_current=" + a5v.Value + "000";
            }

            if (a6.IsChecked == true)
            {
                _adjline += " --apu-skin-temp=" + a6v.Value;
            }

            if (a7.IsChecked == true)
            {
                _adjline += " --dgpu-skin-temp=" + a7v.Value;
            }

            if (a8.IsChecked == true)
            {
                _adjline += " --apu-slow-limit=" + a8v.Value + "000";
            }

            if (a9.IsChecked == true)
            {
                _adjline += " --skin-temp-limit=" + a9v.Value + "000";
            }

            if (a10.IsChecked == true)
            {
                _adjline += " --gfx-clk=" + a10v.Value;
            }

            if (a11.IsChecked == true)
            {
                _adjline += " --oc-clk=" + a11v.Value;
            }

            if (a12.IsChecked == true)
            {
                _adjline += " --oc-volt=" + Math.Round((1.55 - a12v.Value / 1000) / 0.00625);
            }


            if (a13.IsChecked == true)
            {
                switch (a13m.SelectedIndex)
                {
                    case 1:
                        _adjline += " --max-performance=1";
                        break;
                    case 2:
                        _adjline += " --power-saving=1";
                        break;
                }
            }

            if (a14.IsChecked == true)
            {
                switch (a14m.SelectedIndex)
                {
                    case 0:
                        _adjline += " --disable-oc=1";
                        break;
                    case 1:
                        _adjline += " --enable-oc=1";
                        break;
                }
            }

            if (a15.IsChecked == true)
            {
                _adjline += " --pbo-scalar=" + a15v.Value * 100;
            }

            if (O1.IsChecked == true)
            {
                if (O1v.Value >= 0.0)
                {
                    _adjline += $" --set-coall={O1v.Value} ";
                }
                else
                {
                    _adjline += $" --set-coall={Convert.ToUInt32(0x100000 - (uint)(-1 * (int)O1v.Value))} ";
                }
            }

            if (O2.IsChecked == true)
            {
                _cpu!.smu.Rsmu.SMU_MSG_SetDldoPsmMargin = SendSmuCommand.ReturnCoGfx(_cpu.info.codeName, false);
                _cpu!.smu.Mp1Smu.SMU_MSG_SetDldoPsmMargin = SendSmuCommand.ReturnCoGfx(_cpu.info.codeName, true);
                //Using Irusanov method
                for (var i = 0; i < _cpu?.info.topology.physicalCores; i++)
                {
                    var mapIndex = i < 8 ? 0 : 1;
                    if (((~_cpu.info.topology.coreDisableMap[mapIndex] >> i) & 1) == 1)
                    {
                        if (_cpu.smu.Rsmu.SMU_MSG_SetDldoPsmMargin != 0U)
                        {
                            _cpu.SetPsmMarginSingleCore(GetCoreMask(i), Convert.ToInt32(O2v.Value));
                        }
                    }
                }

                _cpu!.smu.Rsmu.SMU_MSG_SetDldoPsmMargin = SendSmuCommand.ReturnCoPer(_cpu.info.codeName, false);
                _cpu!.smu.Mp1Smu.SMU_MSG_SetDldoPsmMargin = SendSmuCommand.ReturnCoPer(_cpu.info.codeName, true);
            }

            if (CCD_CO_Mode_Sel.IsChecked == true &&
                CCD_CO_Mode.SelectedIndex != 0) // Если пользователь выбрал хотя-бы один режим и ...
            {
                if (CCD_CO_Mode.SelectedIndex == 1) // Если выбран режим ноутбук
                {
                    if (_cpu?.info.codeName == Cpu.CodeName.DragonRange) // Так как там как у компьютеров
                    {
                        if (CCD1_1.IsChecked == true)
                        {
                            _adjline += $" --set-coper={0 | ((int)CCD1_1v.Value & 0xFFFF)} ";
                        }

                        if (CCD1_2.IsChecked == true)
                        {
                            _adjline += $" --set-coper={1048576 | ((int)CCD1_2v.Value & 0xFFFF)} ";
                        }

                        if (CCD1_3.IsChecked == true)
                        {
                            _adjline += $" --set-coper={2097152 | ((int)CCD1_3v.Value & 0xFFFF)} ";
                        }

                        if (CCD1_4.IsChecked == true)
                        {
                            _adjline += $" --set-coper={3145728 | ((int)CCD1_4v.Value & 0xFFFF)} ";
                        }

                        if (CCD1_5.IsChecked == true)
                        {
                            _adjline += $" --set-coper={4194304 | ((int)CCD1_5v.Value & 0xFFFF)} ";
                        }

                        if (CCD1_6.IsChecked == true)
                        {
                            _adjline += $" --set-coper={5242880 | ((int)CCD1_6v.Value & 0xFFFF)} ";
                        }

                        if (CCD1_7.IsChecked == true)
                        {
                            _adjline += $" --set-coper={6291456 | ((int)CCD1_7v.Value & 0xFFFF)} ";
                        }

                        if (CCD1_8.IsChecked == true)
                        {
                            _adjline += $" --set-coper={7340032 | ((int)CCD1_8v.Value & 0xFFFF)} ";
                        }

                        if (CCD2_1.IsChecked == true)
                        {
                            _adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((0 % 8) & 15)) << 20) | ((int)CCD2_1v.Value & 0xFFFF)} ";
                        }

                        if (CCD2_2.IsChecked == true)
                        {
                            _adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((1 % 8) & 15)) << 20) | ((int)CCD2_2v.Value & 0xFFFF)} ";
                        }

                        if (CCD2_3.IsChecked == true)
                        {
                            _adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((2 % 8) & 15)) << 20) | ((int)CCD2_3v.Value & 0xFFFF)} ";
                        }

                        if (CCD2_4.IsChecked == true)
                        {
                            _adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((3 % 8) & 15)) << 20) | ((int)CCD2_4v.Value & 0xFFFF)} ";
                        }

                        if (CCD2_5.IsChecked == true)
                        {
                            _adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((4 % 8) & 15)) << 20) | ((int)CCD2_5v.Value & 0xFFFF)} ";
                        }

                        if (CCD2_6.IsChecked == true)
                        {
                            _adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((5 % 8) & 15)) << 20) | ((int)CCD2_6v.Value & 0xFFFF)} ";
                        }

                        if (CCD2_7.IsChecked == true)
                        {
                            _adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((6 % 8) & 15)) << 20) | ((int)CCD2_7v.Value & 0xFFFF)} ";
                        }

                        if (CCD2_8.IsChecked == true)
                        {
                            _adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((7 % 8) & 15)) << 20) | ((int)CCD2_8v.Value & 0xFFFF)} ";
                        }
                    }
                    else
                    {
                        if (CCD1_1.IsChecked == true)
                        {
                            _adjline += $" --set-coper={0 | ((int)CCD1_1v.Value & 0xFFFF)} ";
                        }

                        if (CCD1_2.IsChecked == true)
                        {
                            _adjline += $" --set-coper={(1 << 20) | ((int)CCD1_2v.Value & 0xFFFF)} ";
                        }

                        if (CCD1_3.IsChecked == true)
                        {
                            _adjline += $" --set-coper={(2 << 20) | ((int)CCD1_3v.Value & 0xFFFF)} ";
                        }

                        if (CCD1_4.IsChecked == true)
                        {
                            _adjline += $" --set-coper={(3 << 20) | ((int)CCD1_4v.Value & 0xFFFF)} ";
                        }

                        if (CCD1_5.IsChecked == true)
                        {
                            _adjline += $" --set-coper={(4 << 20) | ((int)CCD1_5v.Value & 0xFFFF)} ";
                        }

                        if (CCD1_6.IsChecked == true)
                        {
                            _adjline += $" --set-coper={(5 << 20) | ((int)CCD1_6v.Value & 0xFFFF)} ";
                        }

                        if (CCD1_7.IsChecked == true)
                        {
                            _adjline += $" --set-coper={(6 << 20) | ((int)CCD1_7v.Value & 0xFFFF)} ";
                        }

                        if (CCD1_8.IsChecked == true)
                        {
                            _adjline += $" --set-coper={(7 << 20) | ((int)CCD1_8v.Value & 0xFFFF)} ";
                        }
                    }
                }
                else if (CCD_CO_Mode.SelectedIndex == 2) //Если выбран режим компьютер
                {
                    if (CCD1_1.IsChecked == true)
                    {
                        _adjline += $" --set-coper={0 | ((int)CCD1_1v.Value & 0xFFFF)} ";
                    }

                    if (CCD1_2.IsChecked == true)
                    {
                        _adjline += $" --set-coper={1048576 | ((int)CCD1_2v.Value & 0xFFFF)} ";
                    }

                    if (CCD1_3.IsChecked == true)
                    {
                        _adjline += $" --set-coper={2097152 | ((int)CCD1_3v.Value & 0xFFFF)} ";
                    }

                    if (CCD1_4.IsChecked == true)
                    {
                        _adjline += $" --set-coper={3145728 | ((int)CCD1_4v.Value & 0xFFFF)} ";
                    }

                    if (CCD1_5.IsChecked == true)
                    {
                        _adjline += $" --set-coper={4194304 | ((int)CCD1_5v.Value & 0xFFFF)} ";
                    }

                    if (CCD1_6.IsChecked == true)
                    {
                        _adjline += $" --set-coper={5242880 | ((int)CCD1_6v.Value & 0xFFFF)} ";
                    }

                    if (CCD1_7.IsChecked == true)
                    {
                        _adjline += $" --set-coper={6291456 | ((int)CCD1_7v.Value & 0xFFFF)} ";
                    }

                    if (CCD1_8.IsChecked == true)
                    {
                        _adjline += $" --set-coper={7340032 | ((int)CCD1_8v.Value & 0xFFFF)} ";
                    }

                    if (CCD2_1.IsChecked == true)
                    {
                        _adjline +=
                            $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((0 % 8) & 15)) << 20) | ((int)CCD2_1v.Value & 0xFFFF)} ";
                    }

                    if (CCD2_2.IsChecked == true)
                    {
                        _adjline +=
                            $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((1 % 8) & 15)) << 20) | ((int)CCD2_2v.Value & 0xFFFF)} ";
                    }

                    if (CCD2_3.IsChecked == true)
                    {
                        _adjline +=
                            $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((2 % 8) & 15)) << 20) | ((int)CCD2_3v.Value & 0xFFFF)} ";
                    }

                    if (CCD2_4.IsChecked == true)
                    {
                        _adjline +=
                            $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((3 % 8) & 15)) << 20) | ((int)CCD2_4v.Value & 0xFFFF)} ";
                    }

                    if (CCD2_5.IsChecked == true)
                    {
                        _adjline +=
                            $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((4 % 8) & 15)) << 20) | ((int)CCD2_5v.Value & 0xFFFF)} ";
                    }

                    if (CCD2_6.IsChecked == true)
                    {
                        _adjline +=
                            $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((5 % 8) & 15)) << 20) | ((int)CCD2_6v.Value & 0xFFFF)} ";
                    }

                    if (CCD2_7.IsChecked == true)
                    {
                        _adjline +=
                            $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((6 % 8) & 15)) << 20) | ((int)CCD2_7v.Value & 0xFFFF)} ";
                    }

                    if (CCD2_8.IsChecked == true)
                    {
                        _adjline +=
                            $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((7 % 8) & 15)) << 20) | ((int)CCD2_8v.Value & 0xFFFF)} ";
                    }
                }
                else if
                    (CCD_CO_Mode.SelectedIndex ==
                     3) // Если выбран режим с использованием метода от Ирусанова, Irusanov, https://github.com/irusanov
                {
                    _cpu!.smu.Rsmu.SMU_MSG_SetDldoPsmMargin = SendSmuCommand.ReturnCoPer(_cpu.info.codeName, false);
                    _cpu!.smu.Mp1Smu.SMU_MSG_SetDldoPsmMargin = SendSmuCommand.ReturnCoPer(_cpu.info.codeName, true);
                    for (var i = 0; i < _cpu?.info.topology.physicalCores; i++)
                    {
                        var checkbox = i < 8
                            ? (CheckBox)CCD1_Grid.FindName($"CCD1_{i + 1}")
                            : (CheckBox)CCD2_Grid.FindName($"CCD2_{i - 7}");
                        if (checkbox != null && checkbox.IsChecked == true)
                        {
                            var setVal = i < 8
                                ? (Slider)CCD1_Grid.FindName($"CCD1_{i + 1}v")
                                : (Slider)CCD2_Grid.FindName($"CCD2_{i - 7}v");
                            var mapIndex = i < 8 ? 0 : 1;
                            if (((~_cpu.info.topology.coreDisableMap[mapIndex] >> i) & 1) == 1) // Если ядро включено
                            {
                                if (_cpu.smu.Rsmu.SMU_MSG_SetDldoPsmMargin != 0U) // Если команда существует
                                {
                                    _cpu.SetPsmMarginSingleCore(GetCoreMask(i), Convert.ToInt32(setVal.Value));
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
                    _adjline += " --enable-feature=1";
                }
                else
                {
                    _adjline += " --disable-feature=1";
                }

                if (Bit_2_FEATURE_DATA_CALCULATION.IsOn)
                {
                    _adjline += " --enable-feature=4";
                }
                else
                {
                    _adjline += " --disable-feature=4";
                }

                if (Bit_3_FEATURE_PPT.IsOn)
                {
                    _adjline += " --enable-feature=8";
                }
                else
                {
                    _adjline += " --disable-feature=8";
                }

                if (Bit_4_FEATURE_TDC.IsOn)
                {
                    _adjline += " --enable-feature=16";
                }
                else
                {
                    _adjline += " --disable-feature=16";
                }

                if (Bit_5_FEATURE_THERMAL.IsOn)
                {
                    _adjline += " --enable-feature=32";
                }
                else
                {
                    _adjline += " --disable-feature=32";
                }

                if (Bit_8_FEATURE_PLL_POWER_DOWN.IsOn)
                {
                    _adjline += " --enable-feature=256";
                }
                else
                {
                    _adjline += " --disable-feature=256";
                }

                if (Bit_37_FEATURE_PROCHOT.IsOn)
                {
                    _adjline += " --enable-feature=0,32";
                }
                else
                {
                    _adjline += " --disable-feature=0,32";
                }

                if (Bit_39_FEATURE_STAPM.IsOn)
                {
                    _adjline += " --enable-feature=0,128";
                }
                else
                {
                    _adjline += " --disable-feature=0,128";
                }

                if (Bit_40_FEATURE_CORE_CSTATES.IsOn)
                {
                    _adjline += " --enable-feature=0,256";
                }
                else
                {
                    _adjline += " --disable-feature=0,256";
                }

                if (Bit_41_FEATURE_GFX_DUTY_CYCLE.IsOn)
                {
                    _adjline += " --enable-feature=0,512";
                }
                else
                {
                    _adjline += " --disable-feature=0,512";
                }

                if (Bit_42_FEATURE_AA_MODE.IsOn)
                {
                    _adjline += " --enable-feature=0,1024";
                }
                else
                {
                    _adjline += " --disable-feature=0,1024";
                }
            }

            AppSettings.RyzenADJline = _adjline + " ";
            _adjline = "";
            ApplyInfo = "";
            AppSettings.SaveSettings();
            SendSmuCommand.Codename = _cpu!.info.codeName;
            await LogHelper.Log($"Sending commandline: {_adjline}");
            MainWindow.Applyer.Apply(AppSettings.RyzenADJline, true, AppSettings.ReapplyOverclock,
                AppSettings.ReapplyOverclockTimer);
            if (EnablePstates.IsOn)
            {
                BtnPstateWrite_Click();
            }

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
            if (ApplyInfo != string.Empty)
            {
                timer *= ApplyInfo.Split('\n').Length + 1;
            }

            if (SettingsViewModel.VersionId != 5) // Если версия не Debug Lanore
            {
                Apply_tooltip.Title = "Apply_Success".GetLocalized();
                Apply_tooltip.Subtitle = "Apply_Success_Desc".GetLocalized();
            }
            else
#pragma warning disable CS0162 // Unreachable code detected
            // ReSharper disable once HeuristicUnreachableCode
            {
                Apply_tooltip.Title = "Apply_Success".GetLocalized();
                Apply_tooltip.Subtitle = "Apply_Success_Desc".GetLocalized() + AppSettings.RyzenADJline;
            }
#pragma warning restore CS0162 // Unreachable code detected
            Apply_tooltip.IconSource = new SymbolIconSource { Symbol = Symbol.Accept };
            Apply_tooltip.IsOpen = true;
            var infoSet = InfoBarSeverity.Success;
            if (ApplyInfo != string.Empty)
            {
                await LogHelper.Log(ApplyInfo);
                Apply_tooltip.Title = "Apply_Warn".GetLocalized();
                Apply_tooltip.Subtitle = "Apply_Warn_Desc".GetLocalized() + ApplyInfo;
                Apply_tooltip.IconSource = new SymbolIconSource { Symbol = Symbol.ReportHacked };
                await Task.Delay(timer);
                Apply_tooltip.IsOpen = false;
                infoSet = InfoBarSeverity.Warning;
            }
            else
            {
                await LogHelper.Log("Apply_Success".GetLocalized());
                await Task.Delay(3000);
                Apply_tooltip.IsOpen = false;
            }

            NotificationsService.Notifies ??= [];
            NotificationsService.Notifies.Add(new Notify
            {
                Title = Apply_tooltip.Title,
                Msg = Apply_tooltip.Subtitle + (ApplyInfo != string.Empty ? "DELETEUNAVAILABLE" : ""),
                Type = infoSet
            });
            NotificationsService.SaveNotificationsSettings();
            _cpusend ??= new SendSmuCommand();
            _cpusend.Play_Invernate_QuickSMU(0);
        }
        catch (Exception exception)
        {
            TraceIt_TraceError(exception.ToString());
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (SaveProfileN.Text != "")
            {
                await LogHelper.Log($"Adding new profile: \"{SaveProfileN.Text}\"");
                ProfileLoad();
                try
                {
                    ActionButton_Save.Flyout.Hide();
                    AppSettings.Preset += 1;
                    _indexprofile += 1;
                    _waitforload = true;
                    ProfileCOM.Items.Add(SaveProfileN.Text);
                    ProfileCOM.SelectedItem = SaveProfileN.Text;
                    if (_profile.Length == 0)
                    {
                        _profile = new Profile[1];
                        _profile[0] = new Profile { profilename = SaveProfileN.Text };
                    }
                    else
                    {
                        var profileList = new List<Profile>(_profile)
                        {
                            new()
                            {
                                profilename = SaveProfileN.Text
                            }
                        };
                        _profile = [.. profileList];
                    }

                    _waitforload = false;
                    NotificationsService.Notifies ??= [];
                    NotificationsService.Notifies.Add(new Notify
                    {
                        Title = "SaveSuccessTitle".GetLocalized(),
                        Msg = "SaveSuccessDesc".GetLocalized() + " " + SaveProfileN.Text,
                        Type = InfoBarSeverity.Success
                    });
                    NotificationsService.SaveNotificationsSettings();
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
                NotificationsService.Notifies ??= [];
                NotificationsService.Notifies.Add(new Notify
                {
                    Title = Add_tooltip_Error.Title,
                    Msg = Add_tooltip_Error.Subtitle,
                    Type = InfoBarSeverity.Error
                });
                NotificationsService.SaveNotificationsSettings();
                Add_tooltip_Error.IsOpen = true;
                await Task.Delay(3000);
                Add_tooltip_Error.IsOpen = false;
            }

            AppSettings.SaveSettings();
            ProfileSave();
        }
        catch (Exception exception)
        {
            TraceIt_TraceError(exception.ToString());
        }
    }

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await LogHelper.Log($"Editing profile name: From \"{_profile[_indexprofile].profilename}\" To \"{EditProfileN.Text}\"");
            EditProfileButton.Flyout.Hide();
            if (EditProfileN.Text != "")
            {
                var backupIndex = ProfileCOM.SelectedIndex;
                if (ProfileCOM.SelectedIndex == 0 || _indexprofile + 1 == 0)
                {
                    Unsaved_tooltip.IsOpen = true;
                    await Task.Delay(3000);
                    Unsaved_tooltip.IsOpen = false;
                }
                else
                {
                    ProfileLoad();
                    _profile[_indexprofile].profilename = EditProfileN.Text;
                    ProfileSave();
                    _waitforload = true;
                    ProfileCOM.Items.Clear();
                    ProfileCOM.Items.Add(new ComboBoxItem
                    {
                        Content = new TextBlock
                        {
                            Text = "Param_Premaded".GetLocalized(),
                            Foreground = (Brush)Application.Current.Resources["AccentTextFillColorTertiaryBrush"]
                        },
                        IsEnabled = false
                    });
                    foreach (var currProfile in _profile)
                    {
                        if (currProfile.profilename != string.Empty || currProfile.profilename != "Unsigned profile")
                        {
                            ProfileCOM.Items.Add(currProfile.profilename/*new ComboBoxItem
                            {
                                Content = new StackPanel
                                {
                                    Orientation = Orientation.Horizontal,
                                    Children = 
                                    { 
                                        new TextBlock
                                        {
                                            Text = currProfile.profilename
                                        },
                                        new TextBlock
                                        {
                                            Text = " " + currProfile.cpu2value.ToString(CultureInfo.InvariantCulture) + "W",
                                            Foreground = new SolidColorBrush(new Color { A = 255, B = 154, G = 143, R = 178})
                                        },
                                        new TextBlock
                                        {
                                            Text = "-" 
                                        },
                                        new TextBlock
                                        {
                                            Text = currProfile.cpu3value.ToString(CultureInfo.InvariantCulture) + "W",
                                            Foreground = new SolidColorBrush(new Color { A = 255, B = 26, G = 112, R = 194})
                                        },
                                        new TextBlock
                                        {
                                            Text = "-" 
                                        },
                                        new TextBlock
                                        {
                                            Text = currProfile.vrm1value.ToString(CultureInfo.InvariantCulture) + "A",
                                            Foreground = new SolidColorBrush(new Color { A = 255, B = 26, G = 23, R = 162})
                                        }
                                    },
                                } 
                            }*/);
                        }
                    }

                    ProfileCOM.SelectedIndex = 0;
                    _waitforload = false;
                    ProfileCOM.SelectedIndex = backupIndex;
                    NotificationsService.Notifies ??= [];
                    NotificationsService.Notifies.Add(new Notify
                    {
                        Title = Edit_tooltip.Title,
                        Msg = Edit_tooltip.Subtitle + " " + SaveProfileN.Text,
                        Type = InfoBarSeverity.Success
                    });
                    Edit_tooltip.IsOpen = true;
                    await Task.Delay(3000);
                    Edit_tooltip.IsOpen = false;
                }
            }
            else
            {
                NotificationsService.Notifies ??= [];
                NotificationsService.Notifies.Add(new Notify
                {
                    Title = Edit_tooltip_Error.Title,
                    Msg = Edit_tooltip_Error.Subtitle,
                    Type = InfoBarSeverity.Error
                });
                Edit_tooltip_Error.IsOpen = true;
                await Task.Delay(3000);
                Edit_tooltip_Error.IsOpen = false;
            }
            NotificationsService.SaveNotificationsSettings();
        }
        catch (Exception exception)
        {
            TraceIt_TraceError(exception.ToString());
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await LogHelper.Log("Showing delete profile dialog");
            var delDialog = new ContentDialog
            {
                Title = "Param_DelPreset_Text".GetLocalized(),
                Content = "Param_DelPreset_Desc".GetLocalized(),
                CloseButtonText = "Cancel".GetLocalized(),
                PrimaryButtonText = "Delete".GetLocalized(),
                DefaultButton = ContentDialogButton.Close
            };
            // Use this code to associate the dialog to the appropriate AppWindow by setting
            // the dialog's XamlRoot to the same XamlRoot as an element that is already present in the AppWindow.
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
            {
                delDialog.XamlRoot = XamlRoot;
            }

            var result = await delDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await LogHelper.Log($"Showing delete profile dialog: deleting profile \"{_profile[_indexprofile].profilename}\"");
                if (ProfileCOM.SelectedIndex == 0)
                {
                    NotificationsService.Notifies ??= [];
                    NotificationsService.Notifies.Add(new Notify
                    {
                        Title = Delete_tooltip_error.Title,
                        Msg = Delete_tooltip_error.Subtitle,
                        Type = InfoBarSeverity.Error
                    });
                    NotificationsService.SaveNotificationsSettings();
                    Delete_tooltip_error.IsOpen = true;
                    await Task.Delay(3000);
                    Delete_tooltip_error.IsOpen = false;
                }
                else
                {
                    ProfileLoad();
                    _waitforload = true;
                    ProfileCOM.Items.Remove(ProfileCOM.SelectedItem);
                    var profileList = new List<Profile>(_profile);
                    profileList.RemoveAt(_indexprofile);
                    _profile = [.. profileList];
                    _indexprofile = 0;
                    _waitforload = false;

                    ProfileCOM.SelectedIndex = ProfileCOM.Items.Count - 1;
                    NotificationsService.Notifies ??= [];
                    NotificationsService.Notifies.Add(new Notify
                    {
                        Title = "DeleteSuccessTitle".GetLocalized(),
                        Msg = "DeleteSuccessDesc".GetLocalized(),
                        Type = InfoBarSeverity.Success
                    });
                    NotificationsService.SaveNotificationsSettings();
                }

                ProfileSave();
            }
        }
        catch (Exception exception)
        {
            TraceIt_TraceError(exception.ToString());
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

    private void Bit_8_FEATURE_PLL_POWER_DOWN_Toggled(object sender, RoutedEventArgs e) =>
        Save_SMUFeaturePowerDown(false);

    private void FEATURE_PLL_POWER_DOWN_Click(object sender, RoutedEventArgs e) => Save_SMUFeaturePowerDown(true);
    private void FEATURE_PROCHOT_Click(object sender, RoutedEventArgs e) => Save_SMUFeatureProchot(true);
    private void Bit_37_FEATURE_PROCHOT_Toggled(object sender, RoutedEventArgs e) => Save_SMUFeatureProchot(false);
    private void FEATURE_STAPM_Click(object sender, RoutedEventArgs e) => Save_SMUFeatureSTAPM(true);
    private void Bit_39_FEATURE_STAPM_Toggled(object sender, RoutedEventArgs e) => Save_SMUFeatureSTAPM(false);
    private void FEATURE_CORE_CSTATES_Click(object sender, RoutedEventArgs e) => Save_SMUFeatureCStates(true);
    private void Bit_40_FEATURE_CORE_CSTATES_Toggled(object sender, RoutedEventArgs e) => Save_SMUFeatureCStates(false);
    private void FEATURE_GFX_DUTY_CYCLE_Click(object sender, RoutedEventArgs e) => Save_SMUFeatureGFXDutyCycle(true);

    private void Bit_41_FEATURE_GFX_DUTY_CYCLE_Toggled(object sender, RoutedEventArgs e) =>
        Save_SMUFeatureGFXDutyCycle(false);

    private void FEATURE_AA_MODE_Click(object sender, RoutedEventArgs e) => Save_SMUFeaturAplusA(true);
    private void Bit_42_FEATURE_AA_MODE_Toggled(object sender, RoutedEventArgs e) => Save_SMUFeaturAplusA(false);

    private void Save_SMUFunctions(bool isButton)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (isButton)
        {
            SMU_Func_Enabl.IsOn = SMU_Func_Enabl.IsOn != true;
        }

        try
        {
            ProfileLoad();
            _profile[ProfileCOM.SelectedIndex - 1].smuFunctionsEnabl = SMU_Func_Enabl.IsOn;
            ProfileSave();
        }
        catch (Exception ex)
        {
            TraceIt_TraceError(ex.ToString());
        }
    }

    private void Save_SMUFeatureCCLK(bool isButton)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (isButton)
        {
            Bit_0_FEATURE_CCLK_CONTROLLER.IsOn = Bit_0_FEATURE_CCLK_CONTROLLER.IsOn != true;
        }

        try
        {
            ProfileLoad();
            _profile[ProfileCOM.SelectedIndex - 1].smuFeatureCCLK = Bit_0_FEATURE_CCLK_CONTROLLER.IsOn;
            ProfileSave();
        }
        catch (Exception ex)
        {
            TraceIt_TraceError(ex.ToString());
        }
    }

    private void Save_SMUFeatureData(bool isButton)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (isButton)
        {
            Bit_2_FEATURE_DATA_CALCULATION.IsOn = Bit_2_FEATURE_DATA_CALCULATION.IsOn != true;
        }

        try
        {
            ProfileLoad();
            _profile[ProfileCOM.SelectedIndex - 1].smuFeatureData = Bit_2_FEATURE_DATA_CALCULATION.IsOn;
            ProfileSave();
        }
        catch (Exception ex)
        {
            TraceIt_TraceError(ex.ToString());
        }
    }

    private void Save_SMUFeaturePPT(bool isButton)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (isButton)
        {
            Bit_3_FEATURE_PPT.IsOn = Bit_3_FEATURE_PPT.IsOn != true;
        }

        try
        {
            ProfileLoad();
            _profile[ProfileCOM.SelectedIndex - 1].smuFeaturePPT = Bit_3_FEATURE_PPT.IsOn;
            ProfileSave();
        }
        catch (Exception ex)
        {
            TraceIt_TraceError(ex.ToString());
        }
    }

    private void Save_SMUFeatureTDC(bool isButton)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (isButton)
        {
            Bit_4_FEATURE_TDC.IsOn = Bit_4_FEATURE_TDC.IsOn != true;
        }

        try
        {
            ProfileLoad();
            _profile[ProfileCOM.SelectedIndex - 1].smuFeatureTDC = Bit_4_FEATURE_TDC.IsOn;
            ProfileSave();
        }
        catch (Exception ex)
        {
            TraceIt_TraceError(ex.ToString());
        }
    }

    private void Save_SMUFeatureThermal(bool isButton)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (isButton)
        {
            Bit_5_FEATURE_THERMAL.IsOn = Bit_5_FEATURE_THERMAL.IsOn != true;
        }

        try
        {
            ProfileLoad();
            _profile[ProfileCOM.SelectedIndex - 1].smuFeatureThermal = Bit_5_FEATURE_THERMAL.IsOn;
            ProfileSave();
        }
        catch (Exception ex)
        {
            TraceIt_TraceError(ex.ToString());
        }
    }

    private void Save_SMUFeaturePowerDown(bool isButton)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (isButton)
        {
            Bit_8_FEATURE_PLL_POWER_DOWN.IsOn = Bit_8_FEATURE_PLL_POWER_DOWN.IsOn != true;
        }

        try
        {
            ProfileLoad();
            _profile[ProfileCOM.SelectedIndex - 1].smuFeaturePowerDown = Bit_8_FEATURE_PLL_POWER_DOWN.IsOn;
            ProfileSave();
        }
        catch (Exception ex)
        {
            TraceIt_TraceError(ex.ToString());
        }
    }

    private void Save_SMUFeatureProchot(bool isButton)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (isButton)
        {
            Bit_37_FEATURE_PROCHOT.IsOn = Bit_37_FEATURE_PROCHOT.IsOn != true;
        }

        try
        {
            ProfileLoad();
            _profile[ProfileCOM.SelectedIndex - 1].smuFeatureProchot = Bit_37_FEATURE_PROCHOT.IsOn;
            ProfileSave();
        }
        catch (Exception ex)
        {
            TraceIt_TraceError(ex.ToString());
        }
    }

    private void Save_SMUFeatureSTAPM(bool isButton)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (isButton)
        {
            Bit_39_FEATURE_STAPM.IsOn = Bit_39_FEATURE_STAPM.IsOn != true;
        }

        try
        {
            ProfileLoad();
            _profile[ProfileCOM.SelectedIndex - 1].smuFeatureSTAPM = Bit_39_FEATURE_STAPM.IsOn;
            ProfileSave();
        }
        catch (Exception ex)
        {
            TraceIt_TraceError(ex.ToString());
        }
    }

    private void Save_SMUFeatureCStates(bool isButton)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (isButton)
        {
            Bit_40_FEATURE_CORE_CSTATES.IsOn = Bit_40_FEATURE_CORE_CSTATES.IsOn != true;
        }

        try
        {
            ProfileLoad();
            _profile[ProfileCOM.SelectedIndex - 1].smuFeatureCStates = Bit_40_FEATURE_CORE_CSTATES.IsOn;
            ProfileSave();
        }
        catch (Exception ex)
        {
            TraceIt_TraceError(ex.ToString());
        }
    }

    private void Save_SMUFeatureGFXDutyCycle(bool isButton)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (isButton)
        {
            Bit_41_FEATURE_GFX_DUTY_CYCLE.IsOn = Bit_41_FEATURE_GFX_DUTY_CYCLE.IsOn != true;
        }

        try
        {
            ProfileLoad();
            _profile[ProfileCOM.SelectedIndex - 1].smuFeatureGfxDutyCycle = Bit_41_FEATURE_GFX_DUTY_CYCLE.IsOn;
            ProfileSave();
        }
        catch (Exception ex)
        {
            TraceIt_TraceError(ex.ToString());
        }
    }

    private void Save_SMUFeaturAplusA(bool isButton)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (isButton)
        {
            Bit_42_FEATURE_AA_MODE.IsOn = Bit_42_FEATURE_AA_MODE.IsOn != true;
        }

        try
        {
            ProfileLoad();
            _profile[ProfileCOM.SelectedIndex - 1].smuFeatureAplusA = Bit_42_FEATURE_AA_MODE.IsOn;
            ProfileSave();
        }
        catch (Exception ex)
        {
            TraceIt_TraceError(ex.ToString());
        }
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
        {
            object slider;
            if (sender.Name.Contains('v'))
            {
                slider = FindName(sender.Name.Replace('t', 'V').Replace('v', 'V'));
            }
            else
            {
                try
                {
                    slider = FindName(sender.Name.Replace('t', 'v'));
                }
                catch (Exception ex)
                {
                    TraceIt_TraceError(ex.ToString());
                    return;
                }
            }

            if (slider is Slider slider1)
            {
                if (slider1.Maximum < sender.Value)
                {
                    slider1.Maximum = FromValueToUpperFive(sender.Value);
                }
            }
        }
    }
    #endregion

    #region PState Section related voids

    private async void BtnPstateWrite_Click()
    {
        try
        {
            await LogHelper.Log("P-States writing...");
            _profile[AppSettings.Preset].did0 = DID_0.Value;
            _profile[AppSettings.Preset].did1 = DID_1.Value;
            _profile[AppSettings.Preset].did2 = DID_2.Value;
            _profile[AppSettings.Preset].fid0 = FID_0.Value;
            _profile[AppSettings.Preset].fid1 = FID_1.Value;
            _profile[AppSettings.Preset].fid2 = FID_2.Value;
            _profile[AppSettings.Preset].vid0 = VID_0.Value;
            _profile[AppSettings.Preset].vid1 = VID_1.Value;
            _profile[AppSettings.Preset].vid2 = VID_2.Value;
            ProfileSave();
            if (_profile[AppSettings.Preset].autoPstate)
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
                        var writeDialog = new ContentDialog
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
                            writeDialog.XamlRoot = XamlRoot;
                        }

                        var result1 = await writeDialog.ShowAsync();
                        if (result1 == ContentDialogResult.Primary)
                        {
                            WritePstates();
                        }
                    }
                    else
                    {
                        var applyDialog = new ContentDialog
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
                            applyDialog.XamlRoot = XamlRoot;
                        }

                        try
                        {
                            var result = await applyDialog.ShowAsync();
                            if (result == ContentDialogResult.Primary)
                            {
                                WritePstates();
                            }

                            if (result == ContentDialogResult.Secondary)
                            {
                                WritePstatesWithoutP0();
                            }
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
        catch (Exception e)
        {
            TraceIt_TraceError(e.ToString());
        }
    }

    public static void WritePstates()
    {
        try
        {
            var cpu = CpuSingleton.GetInstance();
            ProfileLoad();
            PstatesDid[0] = _profile[AppSettings.Preset].did0;
            PstatesDid[1] = _profile[AppSettings.Preset].did1;
            PstatesDid[2] = _profile[AppSettings.Preset].did2;
            PstatesFid[0] = _profile[AppSettings.Preset].fid0;
            PstatesFid[1] = _profile[AppSettings.Preset].fid1;
            PstatesFid[2] = _profile[AppSettings.Preset].fid2;
            PstatesVid[0] = _profile[AppSettings.Preset].vid0;
            PstatesVid[1] = _profile[AppSettings.Preset].vid1;
            PstatesVid[2] = _profile[AppSettings.Preset].vid2;
            for (var p = 0; p < 3; p++)
            {
                if (PstatesFid[p] == 0 || PstatesDid[p] == 0 || PstatesVid[p] == 0)
                {
                    ReadPstate();
                    LogHelper.LogError("Corrupted P-States in config");
                    App.GetService<IAppNotificationService>().Show(
                        "<toast launch=\"action=ToastClick\">\r\n  <visual>\r\n    <binding template=\"ToastGeneric\">\r\n      <text>Critical app error</text>\r\n      <text>Corrupted P-States in config</text>\r\n      <image placement=\"appLogoOverride\" hint-crop=\"circle\" src=\"Assets/WindowIcon.ico\"/>\r\n    </binding>\r\n  </visual>\r\n  <actions>\r\n    <action content=\"Restart\" arguments=\"action=Settings\"/>\r\n    <action content=\"Support\" arguments=\"action=Message\"/>\r\n  </actions>\r\n</toast>");
                }

                // Установка стандартных значений
                uint eax = 0, edx = 0;
                if (cpu?.ReadMsr(Convert.ToUInt32(Convert.ToInt64(0xC0010064) + p), ref eax, ref edx) == false)
                {
                    LogHelper.LogError("Error reading P-States");
                    App.GetService<IAppNotificationService>().Show(
                        $"<toast launch=\"action=ToastClick\">\r\n  <visual>\r\n    <binding template=\"ToastGeneric\">\r\n      <text>Critical app error</text>\r\n      <text>Error reading P-State! ID = {p}</text>\r\n      <image placement=\"appLogoOverride\" hint-crop=\"circle\" src=\"Assets/WindowIcon.ico\"/>\r\n    </binding>\r\n  </visual>\r\n  <actions>\r\n    <action content=\"Restart\" arguments=\"action=Settings\"/>\r\n    <action content=\"Support\" arguments=\"action=Message\"/>\r\n  </actions>\r\n</toast>");
                    return;
                }

                CalculatePstateDetails(eax, out var iddDiv, out var iddVal, out var cpuVid, out _, out _);
                var didtext = PstatesDid[p];
                var fidtext = PstatesFid[p];
                var vidtext = PstatesVid[p];
                eax = ((iddDiv & 0xFF) << 30) | ((iddVal & 0xFF) << 22) | ((cpuVid & 0xFF) << 14) |
                      (((uint)Math.Round(didtext, 0) & 0xFF) << 8) | ((uint)Math.Round(fidtext, 0) & 0xFF);
                if (NumaUtil.HighestNumaNode > 0)
                {
                    for (var i = 0; i <= 2; i++)
                    {
                        if (!WritePstateClick(p, eax, edx, i))
                        {
                            return;
                        }
                    }
                }
                else
                {
                    if (!WritePstateClick(p, eax, edx))
                    {
                        return;
                    }
                }

                if (!WritePstateClick(p, eax, edx))
                {
                    return;
                }

                if (cpu?.WriteMsr(Convert.ToUInt32(Convert.ToInt64(0xC0010064) + p), eax, edx) == false)
                {
                    LogHelper.LogError($"Error writing P-State: {p}");
                    App.GetService<IAppNotificationService>().Show(
                        $"<toast launch=\"action=ToastClick\">\r\n  <visual>\r\n    <binding template=\"ToastGeneric\">\r\n      <text>Critical app error</text>\r\n      <text>Error writing PState! ID = {p}</text>\r\n      <image placement=\"appLogoOverride\" hint-crop=\"circle\" src=\"{0}Assets/WindowIcon.ico\"/>\r\n    </binding>\r\n  </visual>\r\n  <actions>\r\n    <action content=\"Restart\" arguments=\"action=Settings\"/>\r\n    <action content=\"Support\" arguments=\"action=Message\"/>\r\n  </actions>\r\n</toast>");
                }

                _equalvid = Math.Round((1.55 - vidtext / 1000) / 0.00625).ToString(CultureInfo.InvariantCulture);
                var f = new Process();
                f.StartInfo.UseShellExecute = false;
                f.StartInfo.FileName = "ryzenps.exe";
                f.StartInfo.Arguments = "-p=" + p + " -v=" + _equalvid;
                f.StartInfo.CreateNoWindow = true;
                f.StartInfo.RedirectStandardError = true;
                f.StartInfo.RedirectStandardInput = true;
                f.StartInfo.RedirectStandardOutput = true;
                f.Start();
                f.WaitForExit();
            }

            ReadPstate();
        }
        catch (Exception ex)
        {
            TraceIt_TraceError(ex.ToString());
        }
    }

    private void WritePstatesWithoutP0()
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
                uint eax = 0, edx = 0;
                var didtext = "12";
                var fidtext = "102";
                if (_cpu?.ReadMsr(Convert.ToUInt32(Convert.ToInt64(0xC0010064) + p), ref eax, ref edx) == false)
                {
                    LogHelper.LogError("Error reading P-States");
                    App.GetService<IAppNotificationService>().Show(
                        $"<toast launch=\"action=ToastClick\">\r\n  <visual>\r\n    <binding template=\"ToastGeneric\">\r\n      <text>Critical app error</text>\r\n      <text>Error reading PState! ID = {p}</text>\r\n      <image placement=\"appLogoOverride\" hint-crop=\"circle\" src=\"{0}Assets/WindowIcon.ico\"/>\r\n    </binding>\r\n  </visual>\r\n  <actions>\r\n    <action content=\"Restart\" arguments=\"action=Settings\"/>\r\n    <action content=\"Support\" arguments=\"action=Message\"/>\r\n  </actions>\r\n</toast>");
                    return;
                }

                CalculatePstateDetails(eax, out var iddDiv, out var iddVal, out var cpuVid, out _, out _);
                switch (p)
                {
                    case 1:
                        didtext = DID_1.Text;
                        fidtext = FID_1.Text;
                        break;
                    case 2:
                        didtext = DID_2.Text;
                        fidtext = FID_2.Text;
                        break;
                }

                eax = ((iddDiv & 0xFF) << 30) | ((iddVal & 0xFF) << 22) | ((cpuVid & 0xFF) << 14) |
                      (((uint)Math.Round(double.Parse(didtext), 0) & 0xFF) << 8) |
                      ((uint)Math.Round(double.Parse(fidtext), 0) & 0xFF);
                if (NumaUtil.HighestNumaNode > 0)
                {
                    for (var i = 0; i <= 2; i++)
                    {
                        if (!WritePstateClick(p, eax, edx, i))
                        {
                            return;
                        }
                    }
                }
                else
                {
                    if (!WritePstateClick(p, eax, edx))
                    {
                        return;
                    }
                }

                if (!WritePstateClick(p, eax, edx))
                {
                    return;
                }

                if (_cpu?.WriteMsr(Convert.ToUInt32(Convert.ToInt64(0xC0010064) + p), eax, edx) == false)
                {
                    LogHelper.LogError($"Error writing P-State: {p}");
                    App.GetService<IAppNotificationService>().Show(
                        $"<toast launch=\"action=ToastClick\">\r\n  <visual>\r\n    <binding template=\"ToastGeneric\">\r\n      <text>Critical app error</text>\r\n      <text>Error writing PState! ID = {p}</text>\r\n      <image placement=\"appLogoOverride\" hint-crop=\"circle\" src=\"{0}Assets/WindowIcon.ico\"/>\r\n    </binding>\r\n  </visual>\r\n  <actions>\r\n    <action content=\"Restart\" arguments=\"action=Settings\"/>\r\n    <action content=\"Support\" arguments=\"action=Message\"/>\r\n  </actions>\r\n</toast>");
                }
            }

            ReadPstate();
        }
        catch (Exception ex)
        {
            TraceIt_TraceError(ex.ToString());
        }
    }

    private static void CalculatePstateDetails(uint eax, out uint iddDiv, out uint iddVal, out uint cpuVid,
        out uint cpuDfsId, out uint cpuFid)
    {
        iddDiv = eax >> 30;
        iddVal = (eax >> 22) & 0xFF;
        cpuVid = (eax >> 14) & 0xFF;
        cpuDfsId = (eax >> 8) & 0x3F;
        cpuFid = eax & 0xFF;
    }

    private static bool ApplyTscWorkaround()
    {
        // P0 fix C001_0015 HWCR[21]=1
        // Fixes timer issues when not using HPET
        try
        {
            var cpu = CpuSingleton.GetInstance();
            uint eax = 0, edx = 0;
            if (cpu.ReadMsr(0xC0010015, ref eax, ref edx))
            {
                eax |= 0x200000;
                return cpu.WriteMsr(0xC0010015, eax, edx);
            }
            LogHelper.LogError("Error applying TSC workaround");
            App.GetService<IAppNotificationService>().Show(
                "<toast launch=\"action=ToastClick\">\r\n  <visual>\r\n    <binding template=\"ToastGeneric\">\r\n      <text>Critical app error</text>\r\n      <text>Error applying TSC CPU P-States fix</text>\r\n      <image placement=\"appLogoOverride\" hint-crop=\"circle\" src=\"Assets/WindowIcon.ico\"/>\r\n    </binding>\r\n  </visual>\r\n  <actions>\r\n    <action content=\"Restart\" arguments=\"action=Settings\"/>\r\n    <action content=\"Support\" arguments=\"action=Message\"/>\r\n  </actions>\r\n</toast>");
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
            if (NumaUtil.HighestNumaNode > 0)
            {
                NumaUtil.SetThreadProcessorAffinity((ushort)(numanode + 1),
                    Enumerable.Range(0, Environment.ProcessorCount).ToArray());
            }

            if (!ApplyTscWorkaround())
            {
                return false;
            }

            if (cpu.WriteMsr(Convert.ToUInt32(Convert.ToInt64(0xC0010064) + pstateId), eax, edx) == false)
            {
                LogHelper.LogError($"Error writing P-State: {pstateId}");
                App.GetService<IAppNotificationService>().Show(
                    $"<toast launch=\"action=ToastClick\">\r\n  <visual>\r\n    <binding template=\"ToastGeneric\">\r\n      <text>Critical app error</text>\r\n      <text>Error writing PState! ID = {pstateId}</text>\r\n      <image placement=\"appLogoOverride\" hint-crop=\"circle\" src=\"Assets/WindowIcon.ico\"/>\r\n    </binding>\r\n  </visual>\r\n  <actions>\r\n    <action content=\"Restart\" arguments=\"action=Settings\"/>\r\n    <action content=\"Support\" arguments=\"action=Message\"/>\r\n  </actions>\r\n</toast>");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            TraceIt_TraceError(ex.ToString());
            return false;
        }
    }

    private static void ReadPstate()
    {
        try
        {
            var cpu = CpuSingleton.GetInstance();
            for (var i = 0; i < 3; i++)
            {
                uint eax = 0, edx = 0;
                try
                {
                    if (cpu?.ReadMsr(Convert.ToUInt32(Convert.ToInt64(0xC0010064) + i), ref eax, ref edx) == false)
                    {
                        LogHelper.LogError("Error reading P-States");

                        App.MainWindow.ShowMessageDialogAsync("Error while reading CPU Pstate", "Critical Error");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    TraceIt_TraceError(ex.ToString());
                }

                CalculatePstateDetails(eax, out _, out _, out _, out var cpuDfsId, out var cpuFid);
                switch (i)
                {
                    case 0:
                        PstatesDid[0] = Convert.ToDouble(cpuDfsId);
                        PstatesFid[0] = Convert.ToDouble(cpuFid);
                        break;
                    case 1:
                        PstatesDid[1] = Convert.ToDouble(cpuDfsId);
                        PstatesFid[1] = Convert.ToDouble(cpuFid);
                        break;
                    case 2:
                        PstatesDid[2] = Convert.ToDouble(cpuDfsId);
                        PstatesFid[2] = Convert.ToDouble(cpuFid);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            TraceIt_TraceError(ex.ToString());
        }
    }

    private void ReadPstates() // Прочитать и записать текущие Pstates
    {
        try
        {
            for (var i = 0; i < 3; i++)
            {
                uint eax = 0, edx = 0;
                try
                {
                    if (_cpu?.ReadMsr(Convert.ToUInt32(Convert.ToInt64(0xC0010064) + i), ref eax, ref edx) == false)
                    {
                        LogHelper.LogError("Error reading P-States");
                        App.MainWindow.ShowMessageDialogAsync("Error while reading CPU Pstate", "Critical Error");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    TraceIt_TraceError(ex.ToString());
                }

                CalculatePstateDetails(eax, out _, out _, out _, out var cpuDfsId, out var cpuFid);
                switch (i)
                {
                    case 0:
                        DID_0.Text = Convert.ToString(cpuDfsId, 10);
                        FID_0.Text = Convert.ToString(cpuFid, 10);
                        P0_Freq.Content = cpuFid * 25 / (cpuDfsId * 12.5) * 100;
                        var mult0V = (int)(cpuFid * 25 / (cpuDfsId * 12.5));
                        mult0V -= 4;
                        if (mult0V <= 0)
                        {
                            mult0V = 0;
                            LogHelper.LogError("Error reading CPU multiply");
                            App.MainWindow.ShowMessageDialogAsync("Error while reading CPU multiply", "Critical Error");
                        }

                        Mult_0.SelectedIndex = mult0V;
                        break;
                    case 1:
                        DID_1.Text = Convert.ToString(cpuDfsId, 10);
                        FID_1.Text = Convert.ToString(cpuFid, 10);
                        P1_Freq.Content = cpuFid * 25 / (cpuDfsId * 12.5) * 100;
                        var mult1V = (int)(cpuFid * 25 / (cpuDfsId * 12.5));
                        mult1V -= 4;
                        if (mult1V <= 0)
                        {
                            mult1V = 0;
                            LogHelper.LogError("Error reading CPU multiply");
                            App.MainWindow.ShowMessageDialogAsync("Error while reading CPU multiply", "Critical Error");
                        }

                        Mult_1.SelectedIndex = mult1V;
                        break;
                    case 2:
                        DID_2.Text = Convert.ToString(cpuDfsId, 10);
                        FID_2.Text = Convert.ToString(cpuFid, 10);
                        P2_Freq.Content = cpuFid * 25 / (cpuDfsId * 12.5) * 100;
                        var mult2V = (int)(cpuFid * 25 / (cpuDfsId * 12.5));
                        mult2V -= 4;
                        if (mult2V <= 0)
                        {
                            mult2V = 0;
                            LogHelper.LogError("Error reading CPU multiply");
                            App.MainWindow.ShowMessageDialogAsync("Error while reading CPU multiply", "Critical Error");
                        }

                        Mult_2.SelectedIndex = mult2V;
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            TraceIt_TraceError(ex.ToString());
        }
    }

    //Pstates section 
    private void EnablePstates_Click(object sender, RoutedEventArgs e)
    {
        EnablePstates.IsOn = !EnablePstates.IsOn;
        EnablePstatess();
    }

    private void TurboBoost_Click(object sender, RoutedEventArgs e)
    {
        if (Turbo_boost.IsEnabled)
        {
            Turbo_boost.IsOn = !Turbo_boost.IsOn;
        }

        TurboBoost();
    }

    private void Autoapply_Click(object sender, RoutedEventArgs e)
    {
        Autoapply_1.IsOn = !Autoapply_1.IsOn;
        Autoapply();
    }

    private void WithoutP0_Click(object sender, RoutedEventArgs e)
    {
        Without_P0.IsOn = !Without_P0.IsOn;
        WithoutP0();
    }

    private void IgnoreWarn_Click(object sender, RoutedEventArgs e)
    {
        IgnoreWarn.IsOn = !IgnoreWarn.IsOn;
        IgnoreWarning();
    }

    //Enable or disable pstate toggleswitches...
    private void EnablePstatess()
    {
        try
        {
            _profile[_indexprofile].enablePstateEditor = EnablePstates.IsOn;

            ProfileSave();
        }
        catch (Exception ex)
        {
            TraceIt_TraceError(ex.ToString());
            _indexprofile = 0;
        }
    }

    private void TurboBoost()
    {
        SetCorePerformanceBoost(Turbo_boost.IsOn); //Турбобуст... 
        if (Turbo_boost.IsOn) //Сохранение
        {
            _profile[_indexprofile].turboBoost = true;
        }
        else
        {
            _profile[_indexprofile].turboBoost = false;
        }

        ProfileSave();
    }

    private void SetCorePerformanceBoost(bool enable)
    {
        uint eax = 0x0;
        uint edx = 0x0;
        const uint mask = 33554432U;

        // Чтение текущего состояния регистра MSR 0xC0010015
        _cpu?.ReadMsr(0xC0010015, ref eax, ref edx);
        // Маска для 25-го бита (CpbDis)
        if (enable)
        {
            LogHelper.Log("Settings Core Performance Boost: Enabling");
            // Устанавливаем 25-й бит в 0 (включаем Core Performance Boost)
            eax &= ~mask;
        }
        else
        {
            LogHelper.Log("Settings Core Performance Boost: Disabling");
            // Устанавливаем 25-й бит в 1 (выключаем Core Performance Boost)
            eax |= mask;
        }

        // Записываем обновленное значение обратно в MSR
        _cpu?.WriteMsr(0xC0010015, eax, edx);
    }

    private void Autoapply()
    {
        if (Autoapply_1.IsOn)
        {
            _profile[_indexprofile].autoPstate = true;
            ProfileSave();
        }
        else
        {
            _profile[_indexprofile].autoPstate = false;
            ProfileSave();
        }
    }

    private void WithoutP0()
    {
        if (Without_P0.IsOn)
        {
            _profile[_indexprofile].p0Ignorewarn = true;
            ProfileSave();
        }
        else
        {
            _profile[_indexprofile].p0Ignorewarn = false;
            ProfileSave();
        }
    }

    private void IgnoreWarning()
    {
        if (IgnoreWarn.IsOn)
        {
            _profile[_indexprofile].ignoreWarn = true;
            ProfileSave();
        }
        else
        {
            _profile[_indexprofile].ignoreWarn = false;
            ProfileSave();
        }
    }

    //Toggleswitches pstate
    private void EnablePstates_Toggled(object sender, RoutedEventArgs e) => EnablePstatess();
    private void Without_P0_Toggled(object sender, RoutedEventArgs e) => WithoutP0();
    private void Autoapply_1_Toggled(object sender, RoutedEventArgs e) => Autoapply();
    private void Turbo_boost_Toggled(object sender, RoutedEventArgs e) => TurboBoost();

    private void Ignore_Toggled(object sender, RoutedEventArgs e) => IgnoreWarning();

    // Автоизменение значений
    private async void FID_0_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        try
        {
            if (_waitforload == false)
            {
                if (_relay == false)
                {
                    await Task.Delay(20);
                    var didValue = DID_0.Value;
                    var fidValue = FID_0.Value;
                    try
                    {
                        var mult0V = fidValue / didValue * 2;
                        if (fidValue / didValue % 2 - 5 == 0.0d)
                        {
                            mult0V -= 3;
                        }
                        else
                        {
                            mult0V -= 4;
                        }

                        if (mult0V <= 0)
                        {
                            mult0V = 0;
                        }

                        P0_Freq.Content = (mult0V + 4) * 100;
                        Mult_0.SelectedIndex = (int)mult0V;
                    }
                    catch (Exception ex)
                    {
                        TraceIt_TraceError(ex.ToString());
                    }
                }
                else
                {
                    _relay = false;
                }

                Save_ID0();
            }
        }
        catch (Exception e)
        {
            TraceIt_TraceError(e.ToString());
        }
    }

    private async void Mult_0_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (_waitforload == false)
            {
                await Task.Delay(20);
                var didValue = DID_0.Value;
                if (DID_0.Text != string.Empty)
                {
                    _waitforload = true;
                    var fidValue = (Mult_0.SelectedIndex + 4) * didValue / 2;
                    _relay = true;
                    FID_0.Value = fidValue;
                    await Task.Delay(40);
                    FID_0.Value = fidValue;
                    P0_Freq.Content = (Mult_0.SelectedIndex + 4) * 100;
                    Save_ID0();
                    await Task.Delay(40);
                    _waitforload = false;
                }
            }
        }
        catch (Exception exception)
        {
            TraceIt_TraceError(exception.ToString());
        }
    }

    private async void DID_0_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        try
        {
            if (_waitforload)
            {
                return;
            }

            await Task.Delay(20);
            var didValue = DID_0.Value;
            var fidValue = FID_0.Value;
            var mult0V = fidValue / didValue * 2;
            if (fidValue / didValue % 2 - 5 == 0.0d)
            {
                mult0V -= 3;
            }
            else
            {
                mult0V -= 4;
            }

            if (mult0V <= 0)
            {
                mult0V = 0;
            }

            P0_Freq.Content = (mult0V + 4) * 100;
            try
            {
                Mult_0.SelectedIndex = (int)mult0V;
            }
            catch (Exception ex)
            {
                TraceIt_TraceError(ex.ToString());
            }

            Save_ID0();
        }
        catch (Exception e)
        {
            TraceIt_TraceError(e.ToString());
        }
    }

    private async void FID_1_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        try
        {
            if (_waitforload == false)
            {
                if (_relay == false)
                {
                    await Task.Delay(20);
                    var didValue = DID_1.Value;
                    var fidValue = FID_1.Value;
                    try
                    {
                        var mult1V = fidValue / didValue * 2;
                        if (fidValue / didValue % 2 - 5 == 0.0d)
                        {
                            mult1V -= 3;
                        }
                        else
                        {
                            mult1V -= 4;
                        }

                        if (mult1V <= 0)
                        {
                            mult1V = 0;
                        }

                        P1_Freq.Content = (mult1V + 4) * 100;
                        Mult_1.SelectedIndex = (int)mult1V;
                    }
                    catch (Exception ex)
                    {
                        TraceIt_TraceError(ex.ToString());
                    }
                }
                else
                {
                    _relay = false;
                }

                Save_ID1();
            }
        }
        catch (Exception e)
        {
            TraceIt_TraceError(e.ToString());
        }
    }

    private async void Mult_1_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (_waitforload == false)
            {
                await Task.Delay(20);
                var didValue = DID_1.Value;
                if (DID_1.Text != "" || DID_1.Text != null)
                {
                    _waitforload = true;
                    var fidValue = (Mult_1.SelectedIndex + 4) * didValue / 2;
                    _relay = true;
                    FID_1.Value = fidValue;
                    await Task.Delay(40);
                    FID_1.Value = fidValue;
                    P1_Freq.Content = (Mult_1.SelectedIndex + 4) * 100;
                    Save_ID1();
                    _waitforload = false;
                }
            }
        }
        catch (Exception exception)
        {
            TraceIt_TraceError(exception.ToString());
        }
    }

    private async void DID_1_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        try
        {
            if (_waitforload == false)
            {
                await Task.Delay(20);
                var didValue = DID_1.Value;
                var fidValue = FID_1.Value;
                var mult1V = fidValue / didValue * 2;
                if (fidValue / didValue % 2 - 5 == 0.0f)
                {
                    mult1V -= 3;
                }
                else
                {
                    mult1V -= 4;
                }

                if (mult1V <= 0)
                {
                    mult1V = 0;
                }

                P1_Freq.Content = (mult1V + 4) * 100;
                try
                {
                    Mult_1.SelectedIndex = (int)mult1V;
                }
                catch (Exception ex)
                {
                    TraceIt_TraceError(ex.ToString());
                }

                Save_ID1();
            }
        }
        catch (Exception exception)
        {
            TraceIt_TraceError(exception.ToString());
        }
    }

    private async void Mult_2_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (_waitforload)
            {
                return;
            }

            await Task.Delay(20);
            var didValue = DID_2.Value;
            if (DID_2.Text != "" || DID_2.Text != null)
            {
                _waitforload = true;
                var fidValue = (Mult_2.SelectedIndex + 4) * didValue / 2;
                _relay = true;
                FID_2.Value = fidValue;
                await Task.Delay(40);
                FID_2.Value = fidValue;
                P2_Freq.Content = (Mult_2.SelectedIndex + 4) * 100;
                Save_ID2();
                _waitforload = false;
            }
        }
        catch (Exception exception)
        {
            TraceIt_TraceError(exception.ToString());
        }
    }

    private async void FID_2_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        try
        {
            if (_waitforload == false)
            {
                if (_relay == false)
                {
                    await Task.Delay(20);
                    var didValue = DID_2.Value;
                    var fidValue = FID_2.Value;
                    try
                    {
                        var mult2V = fidValue / didValue * 2;
                        if (fidValue / didValue % 2 - 5 == 0.0d)
                        {
                            mult2V -= 3;
                        }
                        else
                        {
                            mult2V -= 4;
                        }

                        if (mult2V <= 0)
                        {
                            mult2V = 0;
                        }

                        P2_Freq.Content = (mult2V + 4) * 100;
                        Mult_2.SelectedIndex = (int)mult2V;
                    }
                    catch (Exception ex)
                    {
                        TraceIt_TraceError(ex.ToString());
                    }
                }
                else
                {
                    _relay = false;
                }

                Save_ID2();
            }
        }
        catch (Exception exception)
        {
            TraceIt_TraceError(exception.ToString());
        }
    }

    private async void DID_2_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        try
        {
            if (_waitforload == false)
            {
                await Task.Delay(40);
                var didValue = DID_2.Value;
                var fidValue = FID_2.Value;
                var mult2V = fidValue / didValue * 2;
                mult2V -= 4;
                if (mult2V <= 0)
                {
                    mult2V = 0;
                }

                P2_Freq.Content = (mult2V + 4) * 100;
                try
                {
                    Mult_2.SelectedIndex = (int)mult2V;
                }
                catch (Exception ex)
                {
                    TraceIt_TraceError(ex.ToString());
                }

                Save_ID2();
            }
        }
        catch (Exception exception)
        {
            TraceIt_TraceError(exception.ToString());
        }
    }

    private void Save_ID0()
    {
        if (_waitforload == false)
        {
            _profile[_indexprofile].did0 = DID_0.Value;
            _profile[_indexprofile].fid0 = FID_0.Value;
            _profile[_indexprofile].vid0 = VID_0.Value;
            _profile[_indexprofile].did1 = DID_1.Value;
            _profile[_indexprofile].fid1 = FID_1.Value;
            _profile[_indexprofile].vid1 = VID_1.Value;
            _profile[_indexprofile].did2 = DID_2.Value;
            _profile[_indexprofile].fid2 = FID_2.Value;
            _profile[_indexprofile].vid2 = VID_2.Value;
            PstatesDid[0] = DID_0.Value;
            PstatesFid[0] = FID_0.Value;
            PstatesVid[0] = VID_0.Value;
            ProfileSave();
        }
    }

    private void Save_ID1()
    {
        if (_waitforload == false)
        {
            _profile[_indexprofile].did0 = DID_0.Value;
            _profile[_indexprofile].fid0 = FID_0.Value;
            _profile[_indexprofile].vid0 = VID_0.Value;
            _profile[_indexprofile].did1 = DID_1.Value;
            _profile[_indexprofile].fid1 = FID_1.Value;
            _profile[_indexprofile].vid1 = VID_1.Value;
            _profile[_indexprofile].did2 = DID_2.Value;
            _profile[_indexprofile].fid2 = FID_2.Value;
            _profile[_indexprofile].vid2 = VID_2.Value;
            PstatesDid[1] = DID_1.Value;
            PstatesFid[1] = FID_1.Value;
            PstatesVid[1] = VID_1.Value;
            ProfileSave();
        }
    }

    private void Save_ID2()
    {
        if (_waitforload == false)
        {
            _profile[_indexprofile].did0 = DID_0.Value;
            _profile[_indexprofile].fid0 = FID_0.Value;
            _profile[_indexprofile].vid0 = VID_0.Value;
            _profile[_indexprofile].did1 = DID_1.Value;
            _profile[_indexprofile].fid1 = FID_1.Value;
            _profile[_indexprofile].vid1 = VID_1.Value;
            _profile[_indexprofile].did2 = DID_2.Value;
            _profile[_indexprofile].fid2 = FID_2.Value;
            _profile[_indexprofile].vid2 = VID_2.Value;
            PstatesDid[2] = DID_0.Value;
            PstatesFid[2] = FID_0.Value;
            PstatesVid[2] = VID_0.Value;
            ProfileSave();
        }
    }

    private void VID_0_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => Save_ID0();
    private void VID_1_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => Save_ID1();
    private void VID_2_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => Save_ID2();

    #endregion 
}