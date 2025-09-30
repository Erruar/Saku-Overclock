using System.ComponentModel;
using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.JsonContainers;
using Saku_Overclock.JsonContainers.Helpers;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.SMUEngine.SmuMailBoxes;
using Saku_Overclock.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation.Metadata;
using Windows.UI;
using ZenStates.Core;
using Application = Microsoft.UI.Xaml.Application;
using Button = Microsoft.UI.Xaml.Controls.Button;
using CheckBox = Microsoft.UI.Xaml.Controls.CheckBox;
using ComboBox = Microsoft.UI.Xaml.Controls.ComboBox;
using Mailbox = ZenStates.Core.Mailbox;
using Process = System.Diagnostics.Process;
using TextBox = Microsoft.UI.Xaml.Controls.TextBox;
using VisualTreeHelper = Saku_Overclock.Helpers.VisualTreeHelper;

namespace Saku_Overclock.Views;

public sealed partial class ПараметрыPage
{
    private FontIcon? _smuSymbol1; // тоже самое что и SMUSymbol
    private List<SmuAddressSet>? _matches; // Совпадения адресов SMU 
    private static Smusettings _smusettings = new(); // Загрузка настроек быстрых команд SMU
    private static Profile[] _profile = new Profile[1]; // Всегда по умолчанию будет 1 профиль
    private static readonly IAppNotificationService NotificationsService = App.GetService<IAppNotificationService>(); // Уведомления приложения
    private static readonly ISendSmuCommandService SendSmuCommand = App.GetService<ISendSmuCommandService>();
    private static readonly IOcFinderService OcFinder = App.GetService<IOcFinderService>();
    private int _indexprofile; // Выбранный профиль
    private static readonly IAppSettingsService AppSettings = App.GetService<IAppSettingsService>(); // Все настройки приложения
    private bool _isSearching; // Флаг выполнения поиска чтобы не сканировать адреса SMU
    private readonly List<string> _searchItems = [];
    private bool _isStapmTuneRequired;
    private string
        _smuSymbol =
            "\uE8C8"; // Изначальный символ копирования, для секции Редактор параметров SMU. Используется для быстрых команд SMU

    private bool _isLoaded; // Загружена ли корректно страница для применения изменений
    private bool _relay; // Задержка между изменениями ComboBox в секции Состояния CPU
    private Cpu? _cpu; // Импорт Zen States core
    private bool _waitforload = true; // Ожидание окончательной смены профиля на другой. Активируется при смене профиля
    private string? _adjline; // Команды RyzenADJ для применения
    private readonly Mailbox _testMailbox = new(); // Новый адрес SMU
    private bool _commandReturnedValue; // Флаг если команда вернула значение
    private readonly bool _isPremadePresetApplied; // Флаг применённого готового пресета для его восстановления после покидания страницы Разгон

    private static string?
        _equalvid; // Преобразование из напряжения в его ID. Используется в секции Состояния CPU для указания напряжения P-State

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
        InitializeComponent();

        ProfileLoad();

        _indexprofile = AppSettings.Preset;
        AppSettings.NbfcFlagConsoleCheckSpeedRunning = false;
        AppSettings.FlagRyzenAdjConsoleTemperatureCheckRunning = false;
        AppSettings.SaveSettings();

        if (AppSettings.Preset == -1)
        {
            _isPremadePresetApplied = true;
        }

        try
        {
            _cpu ??= CpuSingleton.GetInstance();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex.ToString());
            App.GetService<IAppNotificationService>()
                .Show(string.Format("AppNotificationCrash_CPU".GetLocalized(), AppContext.BaseDirectory));
        }

        Loaded += ПараметрыPage_Loaded;
    }


    #region JSON and initialization

    #region Page Load 

    private void ПараметрыPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _isLoaded = true;

            ProfileLoad();
            CollectSearchItems();
            SlidersInit();
            RecommendationsInit();
        }
        catch (Exception exception)
        {
            LogHelper.TraceIt_TraceError(exception.ToString());
        }
    }
    
    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);

        if (_isPremadePresetApplied && _isLoaded)
        {
            AppSettings.Preset = -1;
            AppSettings.SaveSettings();
        }
    }

    #endregion

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
            LogHelper.TraceIt_TraceError(ex.ToString());
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
            LogHelper.TraceIt_TraceError(ex.ToString());
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
            LogHelper.TraceIt_TraceError(ex.ToString());
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

    private void RecommendationsInit()
    {
        var data = OcFinder.GetPerformanceRecommendationData();
        TempRecommend0.Text = $"{data.Item1[0]}С";
        TempRecommend1.Text = $"{data.Item1[1]}С";
        StapmRecommend0.Text = $"{data.Item2[0]}W";
        StapmRecommend1.Text = $"{data.Item2[1]}W";
        FastRecommend0.Text = $"{data.Item3[0]}W";
        FastRecommend1.Text = $"{data.Item3[1]}W";
        SlowRecommend0.Text = FastRecommend0.Text;
        SlowRecommend1.Text = FastRecommend1.Text;
        SttRecommend0.Text = $"{data.Item4[0]}W";
        SttRecommend1.Text = $"{data.Item4[1]}W";
        SlowTimeRecommend0.Text = $"{data.Item5[0]}S";
        SlowTimeRecommend1.Text = $"{data.Item5[1]}S";
        StapmTimeRecommend0.Text = $"{data.Item6[0]}S";
        StapmTimeRecommend1.Text = $"{data.Item6[1]}S";
        BdProchotTimeRecommend0.Text = $"{data.Item7[0]}mS";
        BdProchotTimeRecommend0.Text = $"{data.Item7[1]}mS";
    }
    private void SlidersInit()
    {
        LogHelper.Log("SakuOverclock SlidersInit");
        if (_isLoaded == false)
        {
            return;
        }

        _waitforload = true;

        ProfileLoad();

        ProfileCom.Items.Clear();
        ProfileCom.Items.Add(new ComboBoxItem
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
            if (currProfile.Profilename != string.Empty)
            {
                ProfileCom.Items.Add(currProfile.Profilename);
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
                if (_profile.Length == 0)
                {
                    _profile = new Profile[1];
                    _profile[0] = new Profile();
                    ProfileCom.Items.Add(_profile[0].Profilename);
                }


                _indexprofile = 0;
                ProfileCom.SelectedIndex = 1;
                AppSettings.SaveSettings();
            }
            else
            {
                _indexprofile = AppSettings.Preset;
                if (ProfileCom.Items.Count >= _indexprofile + 1)
                {
                    ProfileCom.SelectedIndex = _indexprofile + 1;
                }
            }
        }
        _waitforload = false;
    }

    //Убрать параметры для ноутбуков
    private void LaptopCpu_FP5_HideUnavailableParameters()
    {
        AdvLaptopAplusALimit.Visibility = Visibility.Collapsed;
        AdvLaptopAplusALimitDesc.Visibility = Visibility.Collapsed;
        AdvLaptopIGpuLimit.Visibility = Visibility.Collapsed;
        AdvLaptopIGpuLimitDesc.Visibility = Visibility.Collapsed;
        LaptopsHtcTemp.Visibility = Visibility.Collapsed;
        LaptopsHtcTempDesc.Visibility = Visibility.Collapsed;
        AdvLaptopIGpuTemp.Visibility = Visibility.Collapsed;
        AdvLaptopIGpuTempDesc.Visibility = Visibility.Collapsed;
        AdvLaptopDGpuTemp.Visibility = Visibility.Collapsed;
        AdvLaptopDGpuTempDesc.Visibility = Visibility.Collapsed;
        VrmLaptopsPsiCpu.Visibility = Visibility.Collapsed;
        VrmLaptopsPsiCpuDesc.Visibility = Visibility.Collapsed;
        VrmLaptopsPsiIGpu.Visibility = Visibility.Collapsed;
        VrmLaptopsPsiIGpuDesc.Visibility = Visibility.Collapsed;
    }

    private void DesktopCpu_AM4_HideUnavailableParameters()
    {
        LaptopsAvgWattage.Visibility = Visibility.Collapsed;
        LaptopsAvgWattageDesc.Visibility = Visibility.Collapsed;
        LaptopsFastSpeed.Visibility = Visibility.Collapsed;
        LaptopsFastSpeedDesc.Visibility = Visibility.Collapsed;
        LaptopsSlowSpeed.Visibility = Visibility.Collapsed;
        LaptopsSlowSpeedDesc.Visibility = Visibility.Collapsed;
        AdvLaptopIGpuLimit.Visibility = Visibility.Collapsed;
        AdvLaptopIGpuLimitDesc.Visibility = Visibility.Collapsed;
        AdvLaptopAplusALimit.Visibility = Visibility.Collapsed;
        AdvLaptopAplusALimitDesc.Visibility = Visibility.Collapsed;
        DesktopCpu_AM5_HideUnavailableParameters();
    }

    private void DesktopCpu_AM5_HideUnavailableParameters()
    {
        LaptopsStapmLimit.Visibility = Visibility.Collapsed;
        LaptopsStapmLimitDesc.Visibility = Visibility.Collapsed;
        VrmLaptopsProchotTime.Visibility = Visibility.Collapsed;
        VrmLaptopsProchotTimeDesc.Visibility = Visibility.Collapsed;
        VrmLaptopsPsiSoC.Visibility = Visibility.Collapsed;
        VrmLaptopsPsiSoCDesc.Visibility = Visibility.Collapsed;
        VrmLaptopsPsiVdd.Visibility = Visibility.Collapsed;
        VrmLaptopsPsiVddDesc.Visibility = Visibility.Collapsed;
        VrmLaptopsPsiCpu.Visibility = Visibility.Collapsed;
        VrmLaptopsPsiCpuDesc.Visibility = Visibility.Collapsed;
        VrmLaptopsPsiIGpu.Visibility = Visibility.Collapsed;
        VrmLaptopsPsiIGpuDesc.Visibility = Visibility.Collapsed;
        VrmLaptopsSoCLimit.Visibility = Visibility.Collapsed;
        VrmLaptopsSoCLimitDesc.Visibility = Visibility.Collapsed;
        VrmLaptopsSoCMax.Visibility = Visibility.Collapsed;
        VrmLaptopsSoCMaxDesc.Visibility = Visibility.Collapsed;
        AdvLaptopDGpuTemp.Visibility = Visibility.Collapsed;
        AdvLaptopDGpuTempDesc.Visibility = Visibility.Collapsed;
        AdvLaptopIGpuFreq.Visibility = Visibility.Collapsed;
        AdvLaptopIGpuFreqDesc.Visibility = Visibility.Collapsed;
        AdvLaptopIGpuTemp.Visibility = Visibility.Collapsed;
        AdvLaptopIGpuTempDesc.Visibility = Visibility.Collapsed;
        AdvLaptopPrefMode.Visibility = Visibility.Collapsed;
        AdvLaptopPrefModeDesc.Visibility = Visibility.Collapsed;
    }

    private void HideDisabledCurveOptimizedParameters(bool locks)
    {
        Ccd11.IsEnabled = locks;
        Ccd11V.IsEnabled = locks;
        Ccd12.IsEnabled = locks;
        Ccd12V.IsEnabled = locks;
        Ccd13.IsEnabled = locks;
        Ccd13V.IsEnabled = locks;
        Ccd14.IsEnabled = locks;
        Ccd14V.IsEnabled = locks;
        Ccd15.IsEnabled = locks;
        Ccd15V.IsEnabled = locks;
        Ccd16.IsEnabled = locks;
        Ccd16V.IsEnabled = locks;
        Ccd17.IsEnabled = locks;
        Ccd17V.IsEnabled = locks;
        Ccd18.IsEnabled = locks;
        Ccd18V.IsEnabled = locks;
        Ccd21.IsEnabled = locks;
        Ccd21V.IsEnabled = locks;
        Ccd22.IsEnabled = locks;
        Ccd22V.IsEnabled = locks;
        Ccd23.IsEnabled = locks;
        Ccd23V.IsEnabled = locks;
        Ccd24.IsEnabled = locks;
        Ccd24V.IsEnabled = locks;
        Ccd25.IsEnabled = locks;
        Ccd25V.IsEnabled = locks;
        Ccd26.IsEnabled = locks;
        Ccd26V.IsEnabled = locks;
        Ccd27.IsEnabled = locks;
        Ccd27V.IsEnabled = locks;
        Ccd28.IsEnabled = locks;
        Ccd28V.IsEnabled = locks;
    }

    private async void MainInit(int index)
    {
        try
        {
            if (SettingsViewModel.VersionId != 5) // Если не дебаг. В дебаг версии отображаются все параметры
            {
                /*                 F P 4    C P U                    */
                if (_cpu?.info.codeName == Cpu.CodeName.BristolRidge)
                {
                    LaptopCpu_FP5_HideUnavailableParameters();

                    LaptopsAvgWattage.Visibility = Visibility.Collapsed;
                    LaptopsAvgWattageDesc.Visibility = Visibility.Collapsed;
                    LaptopsSlowSpeed.Visibility = Visibility.Collapsed;
                    LaptopsSlowSpeedDesc.Visibility = Visibility.Collapsed;
                    AdvLaptopFix04.Visibility = Visibility.Collapsed;
                    AdvLaptopFix04Desc.Visibility = Visibility.Collapsed;
                    AdvLaptopOcMode.Visibility = Visibility.Collapsed;
                    AdvLaptopOcModeDesc.Visibility = Visibility.Collapsed;
                    AdvLaptopPboScalar.Visibility = Visibility.Collapsed;
                    AdvLaptopPboScalarDesc.Visibility = Visibility.Collapsed;
                    SmuFunctionsGrid.Visibility = Visibility.Collapsed;
                    CpuPowerStateOptionsGrid.Visibility = Visibility.Collapsed;
                    AdvancedFreqOptionsGrid.Visibility = Visibility.Collapsed;
                    CoExpander.Visibility = Visibility.Collapsed;
                    Ccd1Expander.Visibility = Visibility.Collapsed;
                    Ccd2Expander.Visibility = Visibility.Collapsed;
                    ActionButtonMon.Visibility = Visibility.Collapsed;

                    ParamAdvParametersBlock.Text = "Param_ADV_PdescBristol".GetLocalized();

                    var elements = VisualTreeHelper.FindVisualChildren<TextBlock>(VrmOptionsGrid);
                    foreach (var element in elements)
                    {
                        element.Text = element.Text.Replace("SoC","NB");
                    }
                }
                /*                 F P 5    C P U                    */
                if (_cpu?.info.codeName is Cpu.CodeName.RavenRidge 
                    or Cpu.CodeName.FireFlight 
                    or Cpu.CodeName.Dali 
                    or Cpu.CodeName.Picasso)
                {
                    LaptopCpu_FP5_HideUnavailableParameters();
                }
                else
                {
                    IGpuSubsystems.Visibility = Visibility.Collapsed;
                }

                /*                 F P 6    C P U                    */
                if (_cpu?.info.codeName is Cpu.CodeName.Renoir 
                    or Cpu.CodeName.Cezanne 
                    or Cpu.CodeName.Lucienne)
                {
                    LaptopsStapmLimit.Visibility = Visibility.Collapsed;
                    LaptopsStapmLimitDesc.Visibility = Visibility.Collapsed;
                    LaptopsHtcTemp.Visibility = Visibility.Collapsed;
                    LaptopsHtcTempDesc.Visibility = Visibility.Collapsed;
                    VrmLaptopsPsiCpu.Visibility = Visibility.Collapsed;
                    VrmLaptopsPsiCpuDesc.Visibility = Visibility.Collapsed;
                    VrmLaptopsPsiIGpu.Visibility = Visibility.Collapsed;
                    VrmLaptopsPsiIGpuDesc.Visibility = Visibility.Collapsed;

                    _isStapmTuneRequired = true;
                }

                /*                 F F 3    C P U                    */
                if (_cpu?.info.codeName == Cpu.CodeName.VanGogh)
                {
                    LaptopsStapmLimit.Visibility = Visibility.Collapsed;
                    LaptopsStapmLimitDesc.Visibility = Visibility.Collapsed;
                    AdvLaptopDGpuTemp.Visibility = Visibility.Collapsed;
                    AdvLaptopDGpuTempDesc.Visibility = Visibility.Collapsed;
                    LaptopsHtcTemp.Visibility = Visibility.Collapsed;
                    LaptopsHtcTempDesc.Visibility = Visibility.Collapsed;
                    AdvLaptopOcMode.Visibility = Visibility.Collapsed;
                    AdvLaptopOcModeDesc.Visibility = Visibility.Collapsed;
                    AdvLaptopPboScalar.Visibility = Visibility.Collapsed;
                    AdvLaptopPboScalarDesc.Visibility = Visibility.Collapsed;
                    AdvLaptopCpuVolt.Visibility = Visibility.Collapsed;
                    AdvLaptopCpuVoltDesc.Visibility = Visibility.Collapsed;

                    _isStapmTuneRequired = true;
                }

                /*     F T 6   F P 7   F P 8   F P 11    C P U       */
                if (_cpu?.info.codeName is Cpu.CodeName.Mendocino 
                    or Cpu.CodeName.Rembrandt 
                    or Cpu.CodeName.Phoenix 
                    or Cpu.CodeName.Phoenix2 
                    or Cpu.CodeName.HawkPoint 
                    or Cpu.CodeName.StrixPoint 
                    or Cpu.CodeName.StrixHalo 
                    or Cpu.CodeName.KrackanPoint 
                    or Cpu.CodeName.KrackanPoint2)
                {
                    LaptopsStapmLimit.Visibility = Visibility.Collapsed;
                    LaptopsStapmLimitDesc.Visibility = Visibility.Collapsed;
                    LaptopsHtcTemp.Visibility = Visibility.Collapsed;
                    LaptopsHtcTempDesc.Visibility = Visibility.Collapsed;

                    _isStapmTuneRequired = true;
                }

                /*              A M 4  v 1    C P U                  */
                if (_cpu?.info.codeName is Cpu.CodeName.PinnacleRidge 
                    or Cpu.CodeName.SummitRidge)
                {
                    Ccd1Expander.Visibility = Visibility.Collapsed; //Убрать Оптимизатор кривой
                    Ccd2Expander.Visibility = Visibility.Collapsed;
                    CoExpander.Visibility = Visibility.Collapsed;
                    DesktopCpu_AM4_HideUnavailableParameters();
                }

                /*              A M 4  v 2    C P U                  */
                if (_cpu?.info.codeName is Cpu.CodeName.Matisse 
                    or Cpu.CodeName.Vermeer)
                {
                    DesktopCpu_AM4_HideUnavailableParameters();
                }
                /*                 A M 5    C P U                    */
                if (_cpu?.info.codeName is Cpu.CodeName.Raphael 
                    or Cpu.CodeName.Genoa 
                    or Cpu.CodeName.GraniteRidge 
                    or Cpu.CodeName.StormPeak 
                    or Cpu.CodeName.DragonRange 
                    or Cpu.CodeName.Bergamo)
                {
                    DesktopCpu_AM5_HideUnavailableParameters();

                    _isStapmTuneRequired = true;
                }


                if (_cpu == null || _cpu?.info.codeName == Cpu.CodeName.Unsupported)
                {
                    MainScroll.Visibility = Visibility.Collapsed;
                    ActionButtonApply.Visibility = Visibility.Collapsed;
                    ActionButtonDelete.Visibility = Visibility.Collapsed;
                    ActionButtonMon.Visibility = Visibility.Collapsed;
                    ActionButtonSave.Visibility = Visibility.Collapsed;
                    ActionButtonShare.Visibility = Visibility.Collapsed;
                    EditProfileButton.Visibility = Visibility.Collapsed;
                    SuggestBox.Visibility = Visibility.Collapsed;
                    FiltersButton.Visibility = Visibility.Collapsed;
                    ProfilesGrid.Visibility = Visibility.Collapsed;
                    ActionIncompatibleProfile.IsOpen = false;
                    ActionIncompatibleCpu.Visibility = Visibility.Visible;

                    return; // Остановить загрузку страницы
                }

                uint cores = 0;

                if (_cpu != null)
                {
                    cores = _cpu.info.topology.physicalCores;
                }

                for (var i = 0; i < cores; i++)
                {
                    var mapIndex = i < 8 ? 0 : 1;
                    if ((~_cpu?.info.topology.coreDisableMap[mapIndex] >> i % 8 & 1) == 0)
                    {
                        try
                        {
                            var checkbox = i < 8
                        ? (CheckBox)Ccd1Grid.FindName($"Ccd1{i + 1}")
                        : (CheckBox)Ccd2Grid.FindName($"Ccd2{i - 8}");
                            if (checkbox != null && checkbox.IsChecked == true)
                            {
                                var setVal = i < 8
                                    ? (Slider)Ccd1Grid.FindName($"Ccd1{i + 1}V")
                                    : (Slider)Ccd2Grid.FindName($"Ccd2{i - 8}V");
                                setVal.IsEnabled = false;
                                setVal.Opacity = 0.4;
                                checkbox.IsEnabled = false;
                                checkbox.IsChecked = false;
                            }
                            var setGrid1 = i < 8
                        ? (StackPanel)Ccd1Grid.FindName($"Ccd1Grid{i + 1}1")
                        : (StackPanel)Ccd2Grid.FindName($"Ccd2Grid{i - 8}1");
                            var setGrid2 = i < 8
                        ? (Grid)Ccd1Grid.FindName($"Ccd1Grid{i + 1}2")
                        : (Grid)Ccd2Grid.FindName($"Ccd2Grid{i - 8}2");
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
                            await LogHelper.TraceIt_TraceError(e.ToString());
                        }
                    }
                }

                if (Ccd1Grid11.Visibility == Visibility.Collapsed &&
                    Ccd1Grid21.Visibility == Visibility.Collapsed &&
                    Ccd1Grid31.Visibility == Visibility.Collapsed &&
                    Ccd1Grid41.Visibility == Visibility.Collapsed &&
                    Ccd1Grid51.Visibility == Visibility.Collapsed &&
                    Ccd1Grid61.Visibility == Visibility.Collapsed &&
                    Ccd1Grid71.Visibility == Visibility.Collapsed &&
                    Ccd1Grid81.Visibility == Visibility.Collapsed &&
                    Ccd2Grid01.Visibility == Visibility.Collapsed &&
                    Ccd2Grid11.Visibility == Visibility.Collapsed &&
                    Ccd2Grid21.Visibility == Visibility.Collapsed &&
                    Ccd2Grid31.Visibility == Visibility.Collapsed &&
                    Ccd2Grid41.Visibility == Visibility.Collapsed &&
                    Ccd2Grid51.Visibility == Visibility.Collapsed &&
                    Ccd2Grid61.Visibility == Visibility.Collapsed &&
                    Ccd2Grid71.Visibility == Visibility.Collapsed)
                {
                    await LogHelper.LogWarn("Curve Optimizer Disabled cores detection incorrect on that CPU. Using standart disabled cores detection method.");
                    Ccd1Grid11.Visibility = Visibility.Visible;
                    Ccd1Grid21.Visibility = Visibility.Visible;
                    Ccd1Grid31.Visibility = Visibility.Visible;
                    Ccd1Grid41.Visibility = Visibility.Visible;
                    Ccd1Grid51.Visibility = Visibility.Visible;
                    Ccd1Grid61.Visibility = Visibility.Visible;
                    Ccd1Grid71.Visibility = Visibility.Visible;
                    Ccd1Grid81.Visibility = Visibility.Visible;
                    Ccd2Grid01.Visibility = Visibility.Visible;
                    Ccd2Grid11.Visibility = Visibility.Visible;
                    Ccd2Grid21.Visibility = Visibility.Visible;
                    Ccd2Grid31.Visibility = Visibility.Visible;
                    Ccd2Grid41.Visibility = Visibility.Visible;
                    Ccd2Grid51.Visibility = Visibility.Visible;
                    Ccd2Grid61.Visibility = Visibility.Visible;
                    Ccd2Grid71.Visibility = Visibility.Visible;

                    cores = _cpu?.info.topology.cores ?? 8;
                    if (cores > 8)
                    {
                        if (cores <= 15)
                        {
                            Ccd2Grid72.Visibility = Visibility.Collapsed;
                            Ccd2Grid71.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 14)
                        {
                            Ccd2Grid62.Visibility = Visibility.Collapsed;
                            Ccd2Grid61.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 13)
                        {
                            Ccd2Grid52.Visibility = Visibility.Collapsed;
                            Ccd2Grid51.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 12)
                        {
                            Ccd2Grid42.Visibility = Visibility.Collapsed;
                            Ccd2Grid41.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 11)
                        {
                            Ccd2Grid32.Visibility = Visibility.Collapsed;
                            Ccd2Grid31.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 10)
                        {
                            Ccd2Grid22.Visibility = Visibility.Collapsed;
                            Ccd2Grid21.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 9)
                        {
                            Ccd2Grid12.Visibility = Visibility.Collapsed;
                            Ccd2Grid11.Visibility = Visibility.Collapsed;
                        }
                    }
                    else
                    {
                        CoCoresText.Text = CoCoresText.Text.Replace("7", $"{cores - 1}");
                        Ccd2Expander.Visibility = Visibility.Collapsed;
                        if (cores <= 7)
                        {
                            Ccd1Grid82.Visibility = Visibility.Collapsed;
                            Ccd1Grid81.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 6)
                        {
                            Ccd1Grid72.Visibility = Visibility.Collapsed;
                            Ccd1Grid71.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 5)
                        {
                            Ccd1Grid62.Visibility = Visibility.Collapsed;
                            Ccd1Grid61.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 4)
                        {
                            Ccd1Grid52.Visibility = Visibility.Collapsed;
                            Ccd1Grid51.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 3)
                        {
                            Ccd1Grid42.Visibility = Visibility.Collapsed;
                            Ccd1Grid41.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 2)
                        {
                            Ccd1Grid32.Visibility = Visibility.Collapsed;
                            Ccd1Grid31.Visibility = Visibility.Collapsed;
                        }

                        if (cores <= 1)
                        {
                            Ccd1Grid22.Visibility = Visibility.Collapsed;
                            Ccd1Grid21.Visibility = Visibility.Collapsed;
                        }

                        if (cores == 0)
                        {
                            Ccd1Expander.Visibility = Visibility.Collapsed;
                        }
                    }
                }
            }

            ProfileLoad();

            _waitforload = true;
            if (AppSettings.Preset == -1 || index == -1 || _profile.Length == 0) // Создать новый пресет
            {
                AppSettings.Preset = 0;
                index = 0;

                if (_profile.Length == 0) 
                {
                    _profile = new Profile[1];
                    _profile[0] = new Profile();
                    ProfileSave();
                }
            }

            ActionIncompatibleProfile.IsOpen = false;


            try
            {
                if (_profile[index].Cpu1Value > C1V.Maximum)
                {
                    C1V.Maximum = FromValueToUpperFive(_profile[index].Cpu1Value);
                }

                if (_profile[index].Cpu2Value > C2V.Maximum)
                {
                    C2V.Maximum = FromValueToUpperFive(_profile[index].Cpu2Value);
                }

                if (_profile[index].Cpu3Value > C3V.Maximum)
                {
                    C3V.Maximum = FromValueToUpperFive(_profile[index].Cpu3Value);
                }

                if (_profile[index].Cpu4Value > C4V.Maximum)
                {
                    C4V.Maximum = FromValueToUpperFive(_profile[index].Cpu4Value);
                }

                if (_profile[index].Cpu5Value > C5V.Maximum)
                {
                    C5V.Maximum = FromValueToUpperFive(_profile[index].Cpu5Value);
                }

                if (_profile[index].Cpu6Value > C6V.Maximum)
                {
                    C6V.Maximum = FromValueToUpperFive(_profile[index].Cpu6Value);
                }

                if (_profile[index].Cpu7Value > C7V.Maximum)
                {
                    C7V.Maximum = FromValueToUpperFive(_profile[index].Cpu7Value);
                }

                if (_profile[index].Vrm1Value > V1V.Maximum)
                {
                    V1V.Maximum = FromValueToUpperFive(_profile[index].Vrm1Value);
                }

                if (_profile[index].Vrm2Value > V2V.Maximum)
                {
                    V2V.Maximum = FromValueToUpperFive(_profile[index].Vrm2Value);
                }

                if (_profile[index].Vrm3Value > V3V.Maximum)
                {
                    V3V.Maximum = FromValueToUpperFive(_profile[index].Vrm3Value);
                }

                if (_profile[index].Vrm4Value > V4V.Maximum)
                {
                    V4V.Maximum = FromValueToUpperFive(_profile[index].Vrm4Value);
                }

                if (_profile[index].Vrm5Value > V5V.Maximum)
                {
                    V5V.Maximum = FromValueToUpperFive(_profile[index].Vrm5Value);
                }

                if (_profile[index].Vrm6Value > V6V.Maximum)
                {
                    V6V.Maximum = FromValueToUpperFive(_profile[index].Vrm6Value);
                }

                if (_profile[index].Vrm7Value > V7V.Maximum)
                {
                    V7V.Maximum = FromValueToUpperFive(_profile[index].Vrm7Value);
                }

                if (_profile[index].Gpu1Value > G1V.Maximum)
                {
                    G1V.Maximum = FromValueToUpperFive(_profile[index].Gpu1Value);
                }

                if (_profile[index].Gpu2Value > G2V.Maximum)
                {
                    G2V.Maximum = FromValueToUpperFive(_profile[index].Gpu2Value);
                }

                if (_profile[index].Gpu3Value > G3V.Maximum)
                {
                    G3V.Maximum = FromValueToUpperFive(_profile[index].Gpu3Value);
                }

                if (_profile[index].Gpu4Value > G4V.Maximum)
                {
                    G4V.Maximum = FromValueToUpperFive(_profile[index].Gpu4Value);
                }

                if (_profile[index].Gpu5Value > G5V.Maximum)
                {
                    G5V.Maximum = FromValueToUpperFive(_profile[index].Gpu5Value);
                }

                if (_profile[index].Gpu6Value > G6V.Maximum)
                {
                    G6V.Maximum = FromValueToUpperFive(_profile[index].Gpu6Value);
                }

                if (_profile[index].Gpu7Value > G7V.Maximum)
                {
                    G7V.Maximum = FromValueToUpperFive(_profile[index].Gpu7Value);
                }

                if (_profile[index].Gpu8Value > G8V.Maximum)
                {
                    G8V.Maximum = FromValueToUpperFive(_profile[index].Gpu8Value);
                }

                if (_profile[index].Gpu9Value > G9V.Maximum)
                {
                    G9V.Maximum = FromValueToUpperFive(_profile[index].Gpu9Value);
                }

                if (_profile[index].Gpu10Value > G10V.Maximum)
                {
                    G10V.Maximum = FromValueToUpperFive(_profile[index].Gpu10Value);
                }

                if (_profile[index].Advncd4Value > A4V.Maximum)
                {
                    A4V.Maximum = FromValueToUpperFive(_profile[index].Advncd4Value);
                }

                if (_profile[index].Advncd5Value > A5V.Maximum)
                {
                    A5V.Maximum = FromValueToUpperFive(_profile[index].Advncd5Value);
                }

                if (_profile[index].Advncd6Value > A6V.Maximum)
                {
                    A6V.Maximum = FromValueToUpperFive(_profile[index].Advncd6Value);
                }

                if (_profile[index].Advncd7Value > A7V.Maximum)
                {
                    A7V.Maximum = FromValueToUpperFive(_profile[index].Advncd7Value);
                }

                if (_profile[index].Advncd8Value > A8V.Maximum)
                {
                    A8V.Maximum = FromValueToUpperFive(_profile[index].Advncd8Value);
                }

                if (_profile[index].Advncd9Value > A9V.Maximum)
                {
                    A9V.Maximum = FromValueToUpperFive(_profile[index].Advncd9Value);
                }

                if (_profile[index].Advncd10Value > A10V.Maximum)
                {
                    A10V.Maximum = FromValueToUpperFive(_profile[index].Advncd10Value);
                }

                if (_profile[index].Advncd11Value > A11V.Maximum)
                {
                    A11V.Maximum = FromValueToUpperFive(_profile[index].Advncd11Value);
                }

                if (_profile[index].Advncd12Value > A12V.Maximum)
                {
                    A12V.Maximum = FromValueToUpperFive(_profile[index].Advncd12Value);
                }

                if (_profile[index].Advncd15Value > A15V.Maximum)
                {
                    A15V.Maximum = FromValueToUpperFive(_profile[index].Advncd15Value);
                }
            }
            catch (Exception ex)
            {
                await LogHelper.TraceIt_TraceError(ex.ToString());
            }

            try
            {
                C1.IsChecked = _profile[index].Cpu1;
                C1V.Value = _profile[index].Cpu1Value;
                C2.IsChecked = _profile[index].Cpu2;
                C2V.Value = _profile[index].Cpu2Value;
                C3.IsChecked = _profile[index].Cpu3;
                C3V.Value = _profile[index].Cpu3Value;
                C4.IsChecked = _profile[index].Cpu4;
                C4V.Value = _profile[index].Cpu4Value;
                C5.IsChecked = _profile[index].Cpu5;
                C5V.Value = _profile[index].Cpu5Value;
                C6.IsChecked = _profile[index].Cpu6;
                C6V.Value = _profile[index].Cpu6Value;
                C7.IsChecked = _profile[index].Cpu7;
                C7V.Value = _profile[index].Cpu7Value;
                V1.IsChecked = _profile[index].Vrm1;
                V1V.Value = _profile[index].Vrm1Value;
                V2.IsChecked = _profile[index].Vrm2;
                V2V.Value = _profile[index].Vrm2Value;
                V3.IsChecked = _profile[index].Vrm3;
                V3V.Value = _profile[index].Vrm3Value;
                V4.IsChecked = _profile[index].Vrm4;
                V4V.Value = _profile[index].Vrm4Value;
                V5.IsChecked = _profile[index].Vrm5;
                V5V.Value = _profile[index].Vrm5Value;
                V6.IsChecked = _profile[index].Vrm6;
                V6V.Value = _profile[index].Vrm6Value;
                V7.IsChecked = _profile[index].Vrm7;
                V7V.Value = _profile[index].Vrm7Value;
                G1.IsChecked = _profile[index].Gpu1;
                G1V.Value = _profile[index].Gpu1Value;
                G2.IsChecked = _profile[index].Gpu2;
                G2V.Value = _profile[index].Gpu2Value;
                G3.IsChecked = _profile[index].Gpu3;
                G3V.Value = _profile[index].Gpu3Value;
                G4.IsChecked = _profile[index].Gpu4;
                G4V.Value = _profile[index].Gpu4Value;
                G5.IsChecked = _profile[index].Gpu5;
                G5V.Value = _profile[index].Gpu5Value;
                G6.IsChecked = _profile[index].Gpu6;
                G6V.Value = _profile[index].Gpu6Value;
                G7.IsChecked = _profile[index].Gpu7;
                G7V.Value = _profile[index].Gpu7Value;
                G8V.Value = _profile[index].Gpu8Value;
                G8.IsChecked = _profile[index].Gpu8;
                G9V.Value = _profile[index].Gpu9Value;
                G9.IsChecked = _profile[index].Gpu9;
                G10V.Value = _profile[index].Gpu10Value;
                G10.IsChecked = _profile[index].Gpu10;
                G16.IsChecked = _profile[index].Gpu16;
                G16M.SelectedIndex = _profile[index].Gpu16Value;
                A4.IsChecked = _profile[index].Advncd4;
                A4V.Value = _profile[index].Advncd4Value;
                A5.IsChecked = _profile[index].Advncd5;
                A5V.Value = _profile[index].Advncd5Value;
                A6.IsChecked = _profile[index].Advncd6;
                A6V.Value = _profile[index].Advncd6Value;
                A7.IsChecked = _profile[index].Advncd7;
                A7V.Value = _profile[index].Advncd7Value;
                A8V.Value = _profile[index].Advncd8Value;
                A8.IsChecked = _profile[index].Advncd8;
                A9V.Value = _profile[index].Advncd9Value;
                A9.IsChecked = _profile[index].Advncd9;
                A10V.Value = _profile[index].Advncd10Value;
                A10.IsChecked = _profile[index].Advncd10;
                A11V.Value = _profile[index].Advncd11Value;
                A11.IsChecked = _profile[index].Advncd11;
                A12V.Value = _profile[index].Advncd12Value;
                A12.IsChecked = _profile[index].Advncd12;
                A13.IsChecked = _profile[index].Advncd13;
                A13M.SelectedIndex = _profile[index].Advncd13Value;
                A14.IsChecked = _profile[index].Advncd14;
                A14M.SelectedIndex = _profile[index].Advncd14Value;
                A15.IsChecked = _profile[index].Advncd15;
                A15V.Value = _profile[index].Advncd15Value;
                CcdCoModeSel.IsChecked = _profile[index].Comode;
                CcdCoMode.SelectedIndex = _profile[index].Coprefmode;
                O1.IsChecked = _profile[index].Coall;
                O1V.Value = _profile[index].Coallvalue;
                O2.IsChecked = _profile[index].Cogfx;
                O2V.Value = _profile[index].Cogfxvalue;
                Ccd11.IsChecked = _profile[index].Coper0;
                Ccd11V.Value = _profile[index].Coper0Value;
                Ccd12.IsChecked = _profile[index].Coper1;
                Ccd12V.Value = _profile[index].Coper1Value;
                Ccd13.IsChecked = _profile[index].Coper2;
                Ccd13V.Value = _profile[index].Coper2Value;
                Ccd14.IsChecked = _profile[index].Coper3;
                Ccd14V.Value = _profile[index].Coper3Value;
                Ccd15.IsChecked = _profile[index].Coper4;
                Ccd15V.Value = _profile[index].Coper4Value;
                Ccd16.IsChecked = _profile[index].Coper5;
                Ccd16V.Value = _profile[index].Coper5Value;
                Ccd17.IsChecked = _profile[index].Coper6;
                Ccd17V.Value = _profile[index].Coper6Value;
                Ccd18.IsChecked = _profile[index].Coper7;
                Ccd18V.Value = _profile[index].Coper7Value;
                Ccd21.IsChecked = _profile[index].Coper8;
                Ccd21V.Value = _profile[index].Coper8Value;
                Ccd22.IsChecked = _profile[index].Coper9;
                Ccd22V.Value = _profile[index].Coper9Value;
                Ccd23.IsChecked = _profile[index].Coper10;
                Ccd23V.Value = _profile[index].Coper10Value;
                Ccd24.IsChecked = _profile[index].Coper11;
                Ccd24V.Value = _profile[index].Coper11Value;
                Ccd25.IsChecked = _profile[index].Coper12;
                Ccd25V.Value = _profile[index].Coper12Value;
                Ccd26.IsChecked = _profile[index].Coper13;
                Ccd26V.Value = _profile[index].Coper13Value;
                Ccd27.IsChecked = _profile[index].Coper14;
                Ccd27V.Value = _profile[index].Coper14Value;
                Ccd28.IsChecked = _profile[index].Coper15;
                Ccd28V.Value = _profile[index].Coper15Value;
                EnablePstates.IsOn = _profile[index].EnablePstateEditor;
                TurboBoostToggle.IsOn = _profile[index].TurboBoost;
                AutoApplyPstates.IsOn = _profile[index].AutoPstate;
                IgnoreWarn.IsOn = _profile[index].IgnoreWarn;
                WithoutP0State.IsOn = _profile[index].P0Ignorewarn;
                Did0.Value = _profile[index].Did0;
                Did1.Value = _profile[index].Did1;
                Did2.Value = _profile[index].Did2;
                Fid0.Value = _profile[index].Fid0;
                Fid1.Value = _profile[index].Fid1;
                Fid2.Value = _profile[index].Fid2;
                Vid0.Value = _profile[index].Vid0;
                Vid1.Value = _profile[index].Vid1;
                Vid2.Value = _profile[index].Vid2;
                EnableSmu.IsOn = _profile[index].SmuEnabled;
                SmuFuncEnableToggle.IsOn = _profile[index].SmuFunctionsEnabl;
                Bit0FeatureCclkController.IsOn = _profile[index].SmuFeatureCclk;
                Bit2FeatureDataCalculation.IsOn = _profile[index].SmuFeatureData;
                Bit3FeaturePpt.IsOn = _profile[index].SmuFeaturePpt;
                Bit4FeatureTdc.IsOn = _profile[index].SmuFeatureTdc;
                Bit5FeatureThermal.IsOn = _profile[index].SmuFeatureThermal;
                Bit8FeaturePllPowerDown.IsOn = _profile[index].SmuFeaturePowerDown;
                Bit37FeatureProchot.IsOn = _profile[index].SmuFeatureProchot;
                Bit39FeatureStapm.IsOn = _profile[index].SmuFeatureStapm;
                Bit40FeatureCoreCstates.IsOn = _profile[index].SmuFeatureCStates;
                Bit41FeatureGfxDutyCycle.IsOn = _profile[index].SmuFeatureGfxDutyCycle;
                Bit42FeatureAaMode.IsOn = _profile[index].SmuFeatureAplusA;
            }
            catch
            {
                await LogHelper.LogError("Profile contains errors. Creating a new profile.");

                _profile = new Profile[1];
                _profile[0] = new Profile();
                ProfileSave();
            }

            try
            {
                Mult0.SelectedIndex = (int)(Fid0.Value * 25 / (Did0.Value * 12.5)) - 4;
                P0Freq.Content = Fid0.Value * 25 / (Did0.Value * 12.5) * 100;
                Mult1.SelectedIndex = (int)(Fid1.Value * 25 / (Did1.Value * 12.5)) - 4;
                P1Freq.Content = Fid1.Value * 25 / (Did1.Value * 12.5) * 100;
                P2Freq.Content = Fid2.Value * 25 / (Did2.Value * 12.5) * 100;
                Mult2.SelectedIndex = (int)(Fid2.Value * 25 / (Did2.Value * 12.5)) - 4;
            }
            catch (Exception ex)
            {
                await LogHelper.TraceIt_TraceError("Loading P-States settings failed: " + ex);
            }

            _waitforload = false;

            SmuSettingsLoad();
            if (_smusettings.Note != string.Empty)
            {
                SmuNotes.Document.SetText(TextSetOptions.FormatRtf, _smusettings.Note.TrimEnd());
                ChangeRichEditBoxTextColor(SmuNotes, GetColorFromBrush(TextColor.Foreground));
            }

            try
            {
                Init_QuickSMU();
            }
            catch (Exception ex)
            {
                await LogHelper.TraceIt_TraceError("Loading user SMU settings failed: " + ex);
            }
        }
        catch (Exception e)
        {
            await LogHelper.TraceIt_TraceError(e.ToString());
        }
    }

    private void Init_QuickSMU()
    {
        SmuSettingsLoad();
        if (_smusettings.QuickSmuCommands == null)
        {
            return;
        }

        QuickSmu.Children.Clear();
        QuickSmu.RowDefinitions.Clear();
        for (var i = 0; i < _smusettings.QuickSmuCommands.Count; i++)
        {
            var grid = new Grid
            {
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var rowDef = new RowDefinition
            {
                Height = GridLength.Auto
            };

            // Подготовка перед добавлением нового элемента
            QuickSmu.RowDefinitions.Add(rowDef);
            var rowIndex = QuickSmu.RowDefinitions.Count - 1;

            QuickSmu.Children.Add(grid); // Добавить в секцию грид быстрой команды
            Grid.SetRow(grid, rowIndex);

            var button = new Button // Основная кнопка быстрой команды. В ней всё содержимое
            {
                Height = 50,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                CornerRadius = new CornerRadius(13),
                Translation = new System.Numerics.Vector3(0, 0, 12),
                Shadow = SharedShadow
            };
            
            var innerGrid = new Grid // Grid внутри Button
            {
                Height = 50
            };
            
            var fontIcon = new FontIcon // Иконка у этой команды
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, -10, 0, 0),
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                HorizontalAlignment = HorizontalAlignment.Left,
                Glyph = _smusettings.QuickSmuCommands[i].Symbol
            };
            
            innerGrid.Children.Add(fontIcon); // Иконка команды

            var textBlock1 = new TextBlock
            {
                Margin = string.IsNullOrWhiteSpace(_smusettings.QuickSmuCommands[i].Description) ? new Thickness(35, 9, 0, 0) : new Thickness(35, 0.5, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
                Text = _smusettings.QuickSmuCommands[i].Name,
                FontWeight = FontWeights.SemiBold
            };
            innerGrid.Children.Add(textBlock1); // Имя команды

            var textBlock2 = new TextBlock
            {
                Margin = new Thickness(35, 17.5, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
                Text = _smusettings.QuickSmuCommands[i].Description,
                FontWeight = FontWeights.Light
            };
            innerGrid.Children.Add(textBlock2); // Подпись команды

            button.Content = innerGrid; // Внутренний Grid в Button

            var buttonsGrid = new Grid // Внешний Grid с кнопками
            {
                HorizontalAlignment = HorizontalAlignment.Right
            };

            // Создание и добавление кнопок во внешний Grid
            var playButton = new Button // "Применить"
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
                },
                CornerRadius = new CornerRadius(10),
                Translation = new System.Numerics.Vector3(0, 0, 12),
                Shadow = SharedShadow
            };
            buttonsGrid.Children.Add(playButton);

            var editButton = new Button // "Изменить"
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
                },
                CornerRadius = new CornerRadius(10),
                Translation = new System.Numerics.Vector3(0, 0, 12),
                Shadow = SharedShadow
            };
            buttonsGrid.Children.Add(editButton);

            var rsmuButton = new Button // Кнопка отображающая текущий MailBox
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                Width = 86,
                Height = 35,
                Margin = new Thickness(0, 0, 93, 0),
                CornerRadius = new CornerRadius(10),
                Translation = new System.Numerics.Vector3(0, 0, 12),
                Shadow = SharedShadow,
                Content = new TextBlock
                {
                    Text = _smusettings.MailBoxes![_smusettings.QuickSmuCommands[i].MailIndex].Name
                }
            };
            buttonsGrid.Children.Add(rsmuButton);

            var cmdButton = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                Width = 86,
                Height = 35,
                Margin = new Thickness(0, 0, 187, 0),
                CornerRadius = new CornerRadius(10),
                Translation = new System.Numerics.Vector3(0, 0, 12),
                Shadow = SharedShadow,
                Content = new TextBlock
                {
                    Text = _smusettings.QuickSmuCommands![i].Command + " / " + _smusettings.QuickSmuCommands![i].Argument
                }
            };
            buttonsGrid.Children.Add(cmdButton);

            var autoButton = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                Width = 86,
                Height = 35,
                Margin = new Thickness(0, 0, 281, 0),
                CornerRadius = new CornerRadius(10),
                Translation = new System.Numerics.Vector3(0, 0, 12),
                Shadow = SharedShadow,
                Content = new TextBlock
                {
                    Text = _smusettings.QuickSmuCommands![i].Startup ? "Autorun" : "Apply"
                }
            };
            
            if (_smusettings.QuickSmuCommands![i].Startup || _smusettings.QuickSmuCommands![i].ApplyWith)
            {
                buttonsGrid.Children.Add(autoButton);
            }

            // Добавление внешнего Grid в основной Grid
            grid.Children.Add(button);
            grid.Children.Add(buttonsGrid);

            // Назначение событий на нажатия кнопок
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
        var ccxInCcd = _cpu?.info.family >= Cpu.Family.FAMILY_19H ? 1U : 2U;
        var coresInCcx = 8 / ccxInCcd;

        var ccd = Convert.ToUInt32(coreIndex / 8);
        var ccx = Convert.ToUInt32(coreIndex / coresInCcx - ccxInCcd * ccd);
        var core = Convert.ToUInt32(coreIndex % coresInCcx);
        return _cpu!.MakeCoreMask(core, ccd, ccx);
    }

    #endregion

    #region Suggestion Engine

    // Сбор элементов для отображения подсказок в поиске
    private void CollectSearchItems()
    {
        _searchItems.Clear();
        var expanders = VisualTreeHelper.FindVisualChildren<Expander>(MainScroll);
        foreach (var expander in expanders)
        {
            var stackPanels = VisualTreeHelper.FindVisualChildren<StackPanel>(expander);
            foreach (var stackPanel in stackPanels)
            {
                var textBlocks = VisualTreeHelper.FindVisualChildren<TextBlock>(stackPanel).Where(tb => tb.FontSize - 15 == 0);
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
        var expanders = VisualTreeHelper.FindVisualChildren<Expander>(MainScroll);
        foreach (var expander in expanders)
        {
            _isSearching = true;
            expander.IsExpanded = true;
            var stackPanels = VisualTreeHelper.FindVisualChildren<StackPanel>(expander);
            foreach (var stackPanel in stackPanels)
            {
                stackPanel.Visibility = Visibility.Visible;
                var adjacentGrid = VisualTreeHelper.FindAdjacentGrid(stackPanel);
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

            if (_searchItems.Count == 0) 
            { 
                CollectSearchItems(); 
            }

            foreach (var searchItem in _searchItems)
            {
                if (splitText.All(key => searchItem.Contains(key, StringComparison.CurrentCultureIgnoreCase)))
                {
                    var textBlock = new TextBlock 
                    { 
                        Text = searchItem, 
                        Margin = new Thickness(-10, 0, -10, 0), 
                        Foreground = ParamName.Foreground 
                    };

                    ToolTipService.SetToolTip(textBlock, searchItem);
                    suitableItems.Add(textBlock);
                }
            }

            if (suitableItems.Count == 0)
            {
                suitableItems.Add(new TextBlock { Text = "No results found", Foreground = ParamName.Foreground });
            }

            SuggestBox.ItemsSource = suitableItems;


            if (SuggestBox.Text == string.Empty)
            {
                FilterButtons_ResetButton_Click(null, null);
            }

            // Сбросить скрытое
            ResetVisibility();

            var expanders = VisualTreeHelper.FindVisualChildren<Expander>(MainScroll);
            foreach (var expander in expanders)
            {
                var stackPanels = VisualTreeHelper.FindVisualChildren<StackPanel>(expander);
                var arrayStackPanels = stackPanels as StackPanel[] ?? [.. stackPanels];
                var anyVisible = false;

                foreach (var stackPanel in arrayStackPanels)
                {
                    var textBlocks = VisualTreeHelper.FindVisualChildren<TextBlock>(stackPanel).Where(tb => tb.FontSize - 15 == 0);
                    var containsText = textBlocks.Any(tb => tb.Text.Contains(SuggestBox.Text.ToLower(), StringComparison.CurrentCultureIgnoreCase));

                    var containsControl = VisualTreeHelper.FindVisualChildren<CheckBox>(stackPanel).Any();

                    // Если текст и элементы управления найдены, делаем StackPanel видимой
                    if (containsText && containsControl)
                    {
                        stackPanel.Visibility = Visibility.Visible;
                        anyVisible = true;

                        // Второй проход: делаем видимыми все дочерние элементы
                        VisualTreeHelper.SetAllChildrenVisibility(stackPanel, Visibility.Visible);
                    }
                    else
                    {
                        stackPanel.Visibility = Visibility.Collapsed;
                    }

                    var adjacentGrid = VisualTreeHelper.FindAdjacentGrid(stackPanel);
                    if (adjacentGrid != null)
                    {
                        adjacentGrid.Visibility = stackPanel.Visibility;
                    }
                }
                foreach (var stackPanel1 in arrayStackPanels) // Второй проход
                {
                    if (stackPanel1.Visibility == Visibility.Visible)
                    {
                        VisualTreeHelper.SetAllChildrenVisibility(stackPanel1, Visibility.Visible);
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
            ("", FilterButtonsFreq),
            ("", FilterButtonsCurrent),
            ("", FilterButtonsPower),
            ("", FilterButtonsTemp),
            ("", FilterButtonsOther),
            ("", FilterButtonsTime),
            ("\uE7B3",FilterButtonsHide)];

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
            if (FiltersButton.Style != ActionButtonApply.Style)
            {
                FiltersButton.Style = ActionButtonApply.Style;
                FiltersButton.Translation = new System.Numerics.Vector3(0, 0, 20);
                FiltersButton.CornerRadius = new CornerRadius(14);
                FiltersButton.Shadow = SharedShadow;
            }

            foreach (var button in buttons) // Добавить все, так как мы не скрываем параметры
            {
                if (button.Item2 != FilterButtonsHide)
                {
                    glyphs.Add(button.Item1);
                }
            }
        }

        if (SuggestBox.Text == string.Empty)
        {
            ResetVisibility();
        }

        var expanders = VisualTreeHelper.FindVisualChildren<Expander>(MainScroll);
        foreach (var expander in expanders)
        {
            var stackPanels = VisualTreeHelper.FindVisualChildren<StackPanel>(expander);
            var arrayStackPanels = stackPanels as StackPanel[] ?? [.. stackPanels];
            var anyVisible = false;
            var savedArray = new List<int>();

            foreach (var stackPanel in arrayStackPanels)
            {
                var textBlocks = VisualTreeHelper.FindVisualChildren<FontIcon>(stackPanel).Where(tb => tb.FontSize - 15 == 0);
                var containsText = textBlocks.Any(tb => VisualTreeHelper.FindAjantedFontIcons(tb, glyphs));

                var containsControl = VisualTreeHelper.FindVisualChildren<CheckBox>(stackPanel).Any();

                // Если текст и элементы управления найдены, делаем StackPanel видимой
                if (containsText && containsControl)
                {
                    stackPanel.Visibility = Visibility.Visible;
                    anyVisible = true;
                    savedArray.Add(Grid.GetRow(stackPanel));

                    // Второй проход: делаем видимыми все дочерние элементы
                    VisualTreeHelper.SetAllChildrenVisibility(stackPanel, Visibility.Visible);
                }
                else
                {
                    stackPanel.Visibility = Visibility.Collapsed;
                }

                var adjacentGrid = VisualTreeHelper.FindAdjacentGrid(stackPanel);
                if (adjacentGrid != null)
                {
                    adjacentGrid.Visibility = stackPanel.Visibility;
                }
            }
            foreach (var secondStackPanel in arrayStackPanels) // Второй проход
            {
                if (secondStackPanel.Visibility == Visibility.Visible)
                {
                    VisualTreeHelper.SetAllChildrenVisibility(secondStackPanel, Visibility.Visible);
                }
            }

            // Текущий подход некорректный, делает видимыми все элементы Grid, включая те что в StackPanel, хотя их должен игнорировать
            var helperGrids = VisualTreeHelper.FindVisualChildren<Grid>(expander);
            var arrayHelperGrids = helperGrids as Grid[] ?? [.. helperGrids];
            foreach (var grid in arrayHelperGrids)
            {
                if (savedArray.Contains(Grid.GetRow(grid)) && Grid.GetColumn(grid) == 1 && grid.HorizontalAlignment == HorizontalAlignment.Center)
                {
                    grid.Visibility = Visibility.Visible;
                    VisualTreeHelper.SetAllChildrenVisibility(grid, Visibility.Visible);
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

        FilterButtonsFreq.IsChecked = false;
        FilterButtonsCurrent.IsChecked = false;
        FilterButtonsPower.IsChecked = false;
        FilterButtonsTemp.IsChecked = false;
        FilterButtonsOther.IsChecked = false;
        FilterButtonsTime.IsChecked = false;
        FilterButtonsHide.IsChecked = false;

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
            var backgroundWorker1 = new BackgroundWorker();
            backgroundWorker1.DoWork += task;
            backgroundWorker1.RunWorkerCompleted += completedHandler;
            backgroundWorker1.RunWorkerAsync();
        }
        catch
        {
            LogHelper.TraceIt_TraceError("Background Task Error");
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
        ComboBoxMailboxSelect.Items.Add(new MailboxListItem(label, addressSet));

    private async void SmuScan_WorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
        try
        {
            var index = ComboBoxMailboxSelect.SelectedIndex;
            PopulateMailboxesList(ComboBoxMailboxSelect.Items);
            for (var i = 0; i < _matches?.Count; i++)
            {
                AddMailboxToList($"Mailbox {i + 1}", _matches[i]);
            }

            if (index > ComboBoxMailboxSelect.Items.Count)
            {
                index = 0;
            }

            ComboBoxMailboxSelect.SelectedIndex = index;
            QuickCommand.IsEnabled = true;
            await Send_Message("SMUScanText".GetLocalized(), "SMUScanDesc".GetLocalized(), Symbol.Message);
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception.Message);
        }
    }

    private void BackgroundWorkerTrySettings_DoWork(object sender, DoWorkEventArgs e)
    {
        try
        {
            _cpu ??= new Cpu(CpuInitSettings.defaultSetttings);
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
            LogHelper.TraceIt_TraceError(ex.ToString());
        }
    }

    private void ScanSmuRange(uint start, uint end, uint step, uint offset)
    {
        _matches = [];
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
            }
            Console.WriteLine();
        }

        var possibleArgAddresses = new List<uint>();

        foreach (var pair in keyPairs)
        {
            Console.WriteLine($"Testing {pair.Key:X8}: {pair.Value:X8}");
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
        TextBoxCmdAddress.Text = $@"0x{Convert.ToString(_testMailbox.SMU_ADDR_MSG, 16).ToUpper()}";
        TextBoxRspAddress.Text = $@"0x{Convert.ToString(_testMailbox.SMU_ADDR_RSP, 16).ToUpper()}";
        TextBoxArgAddress.Text = $@"0x{Convert.ToString(_testMailbox.SMU_ADDR_ARG, 16).ToUpper()}";
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
        _commandReturnedValue = false;
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
                userArgs = TextBoxArg0.Text.Trim().Split(',');
                TryConvertToUint(TextBoxCmdAddress.Text, out addrMsg);
                TryConvertToUint(TextBoxRspAddress.Text, out addrRsp);
                TryConvertToUint(TextBoxArgAddress.Text, out addrArg);
                TryConvertToUint(TextBoxCmd.Text, out command);
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
            var argsBefore = args.ToArray();

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
                             (TextBoxCmd.Text.Contains("0x") ? TextBoxCmd.Text : "0x" + TextBoxCmd.Text)
                             + "Param_SMU_Args_From".GetLocalized() + ComboBoxMailboxSelect.SelectedValue
                             + "Param_SMU_Args".GetLocalized() + (TextBoxArg0.Text.Contains("0x")
                                 ? TextBoxArg0.Text
                                 : "0x" + TextBoxArg0.Text);
                if (status == SMU.Status.CMD_REJECTED_PREREQ)
                {
                    ApplyInfo += "\n" + "SMUErrorRejected".GetLocalized();
                }
                else
                {
                    ApplyInfo += "\n" + "SMUErrorNoCMD".GetLocalized();
                }
            }
            if (args[0] != argsBefore[0] || args[1] != argsBefore[1] || args[2] != argsBefore[2])
            {
                _commandReturnedValue = true;
                ApplyInfo += "Param_DeveloperOptions_SmuCommand".GetLocalized() + $" {ComboBoxMailboxSelect.SelectedValue} 0x{command:X} " + "Param_DeveloperOptions_CommandResult".GetLocalized() + $"0x{args[0]:X}({args[0]}) 0x{args[1]:X}({args[1]}) 0x{args[2]:X}({args[2]})" + "Param_DeveloperOptions_ResultSaved".GetLocalized();
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
        if (ComboBoxMailboxSelect.SelectedItem is MailboxListItem item)
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
            var newWindow = new PowerWindow();
            var micaBackdrop = new MicaBackdrop
            {
                Kind = MicaKind.BaseAlt
            };
            newWindow.SystemBackdrop = micaBackdrop;
            newWindow.Activate();

        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception.ToString());
        }
    }

    private void SMUEnabl_Click(object sender, RoutedEventArgs e)
    {
        EnableSmu.IsOn = !EnableSmu.IsOn;
        SmuEnabl();
    }

    private void EnableSMU_Toggled(object sender, RoutedEventArgs e) => SmuEnabl();

    private void SmuEnabl()
    {
        if (EnableSmu.IsOn)
        {
            _profile[_indexprofile].SmuEnabled = true;
            ProfileSave();
        }
        else
        {
            _profile[_indexprofile].SmuEnabled = false;
            ProfileSave();
        }
    }

    private void CreateQuickCommandSMU_Click(object sender, RoutedEventArgs e) => QuickDialog(0, 0);
    private void CreateQuickCommandSMU1_Click(object sender, RoutedEventArgs e) => RangeDialog();

    private async void QuickDialog(int destination, int rowindex)
    {
        try
        {
            _smuSymbol1 = new FontIcon
            {
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                Glyph = _smuSymbol,
                Margin = new Thickness(-4, -2, -5, -5)
            };
            var symbolButton = new Button
            {
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 41, 0, 0),
                Width = 40,
                Height = 40,
                Content = new ContentControl
                {
                    Content = _smuSymbol1
                },
                CornerRadius = new CornerRadius(10),
                Translation = new System.Numerics.Vector3(0, 0, 12),
                Shadow = SharedShadow
            };
            var comboSelSmu = new ComboBox
            {
                Margin = new Thickness(55, 5, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
                CornerRadius = new CornerRadius(10),
                Translation = new System.Numerics.Vector3(0, 0, 12),
                Shadow = SharedShadow
            };
            var mainText = new TextBox
            {
                Margin = new Thickness(55, 45, 0, 0),
                PlaceholderText = "New_Name".GetLocalized(),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 305,
                CornerRadius = new CornerRadius(10),
                Translation = new System.Numerics.Vector3(0, 0, 12),
                Shadow = SharedShadow
            };
            var descText = new TextBox
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(55, 85, 0, 0),
                PlaceholderText = "Desc".GetLocalized(),
                Width = 305,
                CornerRadius = new CornerRadius(10),
                Translation = new System.Numerics.Vector3(0, 0, 12),
                Shadow = SharedShadow
            };
            var cmdText = new TextBox
            {
                /* Margin = new Thickness(0, 152, 0, 0),*/
                PlaceholderText = "Command".GetLocalized(),
                /*HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top, */
                Width = 176,
                CornerRadius = new CornerRadius(10),
                Translation = new System.Numerics.Vector3(0, 0, 12),
                Shadow = SharedShadow
            };
            var argText = new TextBox
            {
                /*Margin = new Thickness(180, 152, 0, 0),*/
                PlaceholderText = "Arguments".GetLocalized(),
                Width = 179,
                CornerRadius = new CornerRadius(10),
                Translation = new System.Numerics.Vector3(0, 0, 12),
                Shadow = SharedShadow
            };
            var autoRun = new CheckBox
            {
                Margin = new Thickness(1, 185, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Content = "Param_Autorun".GetLocalized(),
                IsChecked = false
            };
            var applyWith = new CheckBox
            {
                Margin = new Thickness(1, 215, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Content = "Param_WithApply".GetLocalized(),
                IsChecked = false
            };
            try
            {
                foreach (var item in ComboBoxMailboxSelect.Items)
                {
                    comboSelSmu.Items.Add(item);
                }

                comboSelSmu.SelectedIndex = ComboBoxMailboxSelect.SelectedIndex;
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
                await LogHelper.TraceIt_TraceError(ex.ToString());
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
                            new StackPanel
                            {
                                HorizontalAlignment = HorizontalAlignment.Left,
                                VerticalAlignment = VerticalAlignment.Top,
                                Margin = new Thickness(0, 122, 0, 0),
                                Orientation = Orientation.Vertical,
                                Children =
                                {
                                    new TextBlock
                                    {
                                        Margin = new Thickness(2,0,0,0),
                                        Text = "Command".GetLocalized(),
                                        Padding = new Thickness(0,0,0,3)
                                    },
                                    cmdText
                                }
                            },
                            new StackPanel
                            {
                                HorizontalAlignment = HorizontalAlignment.Left,
                                VerticalAlignment = VerticalAlignment.Top,
                                Margin = new Thickness(180, 122, 0, 0),
                                Orientation = Orientation.Vertical,
                                Children =
                                {
                                    new TextBlock
                                    {
                                        Margin = new Thickness(2,0,0,0),
                                        Text = "Arguments".GetLocalized(),
                                        Padding = new Thickness(0,0,0,3)
                                    },
                                    argText
                                }
                            },
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
                                        Cmd = TextBoxCmdAddress.Text,
                                        Rsp = TextBoxRspAddress.Text,
                                        Arg = TextBoxArgAddress.Text
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
                                        Cmd = TextBoxCmdAddress.Text,
                                        Rsp = TextBoxRspAddress.Text,
                                        Arg = TextBoxArgAddress.Text
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

                        ComboBoxMailboxSelect.SelectedIndex = saveIndex;
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
                            _smusettings.QuickSmuCommands!.RemoveAt(rowindex);
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
            catch (Exception ex)
            {
                await LogHelper.TraceIt_TraceError(ex.ToString());
            }
        }
        catch (Exception e)
        {
            await LogHelper.TraceIt_TraceError(e.ToString());
        }
    }

    private async void RangeDialog()
    {
        try
        {
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
                foreach (var item in ComboBoxMailboxSelect.Items)
                {
                    comboSelSmu.Items.Add(item);
                }

                comboSelSmu.SelectedIndex = ComboBoxMailboxSelect.SelectedIndex;
                comboSelSmu.SelectionChanged += ComboSelSMU_SelectionChanged;
            }
            catch (Exception ex)
            {
                await LogHelper.TraceIt_TraceError(ex.ToString());
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
                                    Cmd = TextBoxCmdAddress.Text,
                                    Rsp = TextBoxRspAddress.Text,
                                    Arg = TextBoxArgAddress.Text
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
                                        Cmd = TextBoxCmdAddress.Text,
                                        Rsp = TextBoxRspAddress.Text,
                                        Arg = TextBoxArgAddress.Text
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

                            SendSmuCommand.RangeCompleted += CloseRangeStarted;

                            SendSmuCommand.SendRange(cmdStart.Text, argStart.Text, argEnd.Text, saveIndex, run);
                            RangeStarted.IsOpen = true;
                            RangeStarted.Title = "SMURange".GetLocalized() + ". " + argStart.Text + "-" + argEnd.Text;

                        }

                        ComboBoxMailboxSelect.SelectedIndex = saveIndex;
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
            catch (Exception ex)
            {
                await LogHelper.TraceIt_TraceError(ex.ToString());
            }
        }
        catch (Exception e)
        {
            await LogHelper.TraceIt_TraceError(e.ToString());
        }
    }
    private void CloseRangeStarted(object? sender, object? args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            RangeStarted.IsOpen = false;
        });

        
        SendSmuCommand.RangeCompleted -= CloseRangeStarted;
        
    }

    private void SymbolButton_Click(object sender, RoutedEventArgs e) => SymbolFlyout.ShowAt(sender as Button);

    private void ComboSelSMU_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            var comboBox = sender as ComboBox;
            if (comboBox != null)
            {
                ComboBoxMailboxSelect.SelectedIndex = comboBox.SelectedIndex;
            }
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex.ToString());
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
        var documentRange = SmuNotes.Document.GetRange(0, TextConstants.MaxUnitCount);
        documentRange.GetText(TextGetOptions.FormatRtf, out var content);
        _smusettings.Note = content.TrimEnd();
        SmuSettingsSave();
    }

    private void ToHex_Click(object sender, RoutedEventArgs e)
    {
        // Преобразование выделенного текста в шестнадцатиричную систему
        if (TextBoxArg0.SelectedText != "")
        {
            try
            {
                var decimalValue = int.Parse(TextBoxArg0.SelectedText);
                var hexValue = decimalValue.ToString("X");
                TextBoxArg0.SelectedText = hexValue;
            }
            catch (Exception ex)
            {
                LogHelper.TraceIt_TraceError(ex.ToString());
            }
        }
        else
        {
            try
            {
                var decimalValue = int.Parse(TextBoxArg0.Text);
                var hexValue = decimalValue.ToString("X");
                TextBoxArg0.Text = hexValue;
            }
            catch (Exception ex)
            {
                LogHelper.TraceIt_TraceError(ex.ToString());
            }
        }
    }

    private void CopyThis_Click(object sender, RoutedEventArgs e)
    {
        if (TextBoxArg0.SelectedText != "")
        {
            // Скопировать текст в буфер обмена
            var dataPackage = new DataPackage
            {
                RequestedOperation = DataPackageOperation.Copy
            };
            dataPackage.SetText(TextBoxArg0.SelectedText);
            Clipboard.SetContent(dataPackage);
        }
        else
        {
            // Выделить весь текст
            TextBoxArg0.SelectAll();
            // Скопировать текст в буфер обмена
            var dataPackage = new DataPackage
            {
                RequestedOperation = DataPackageOperation.Copy
            };
            dataPackage.SetText(TextBoxArg0.Text);
            Clipboard.SetContent(dataPackage);
        }
    }

    private void CutThis_Click(object sender, RoutedEventArgs e)
    {
        if (TextBoxArg0.SelectedText != "")
        {
            // Скопировать текст в буфер обмена
            var dataPackage = new DataPackage
            {
                RequestedOperation = DataPackageOperation.Copy
            };
            dataPackage.SetText(TextBoxArg0.SelectedText);
            Clipboard.SetContent(dataPackage);
            // Обнулить текст
            TextBoxArg0.SelectedText = "";
        }
        else
        {
            // Выделить весь текст
            TextBoxArg0.SelectAll();
            // Скопировать текст в буфер обмена
            var dataPackage = new DataPackage
            {
                RequestedOperation = DataPackageOperation.Copy
            };
            dataPackage.SetText(TextBoxArg0.Text);
            Clipboard.SetContent(dataPackage);
            TextBoxArg0.Text = "";
        }
    }

    private void SelectAllThis_Click(object sender, RoutedEventArgs e)
    {
        // Выделить весь текст
        TextBoxArg0.SelectAll();
    }

    private void CancelRange_Click(object sender, RoutedEventArgs e)
    {
        SendSmuCommand.CancelRange();
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

            if (ProfileCom.SelectedIndex != -1)
            {
                AppSettings.Preset = ProfileCom.SelectedIndex - 1;
                AppSettings.SaveSettings();
            }

            _indexprofile = ProfileCom.SelectedIndex - 1;
            MainInit(ProfileCom.SelectedIndex - 1);
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception.ToString());
        }
    }

    private void SuggestBox_Loaded(object sender, RoutedEventArgs e)
    {
        var texts = VisualTreeHelper.FindVisualChildren<ScrollContentPresenter>(SuggestBox);
        foreach (var text in texts)
        {
            text.Margin = new Thickness(12, 10, 0, 0);
        }
        var contents = VisualTreeHelper.FindVisualChildren<ContentControl>(SuggestBox);
        foreach (var content in contents)
        {
            var presents = VisualTreeHelper.FindVisualChildren<ContentPresenter>(content);
            foreach (var present in presents)
            {
                var texts1 = VisualTreeHelper.FindVisualChildren<TextBlock>(present);
                foreach (var text in texts1)
                {
                    text.Margin = new Thickness(0, 5, 0, 0);
                }
            }
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
        var check = C1.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Cpu1 = check;
            _profile[_indexprofile].Cpu1Value = C1V.Value;
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
        var check = C2.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Cpu2 = check;
            _profile[_indexprofile].Cpu2Value = C2V.Value;
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
        var check = C3.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Cpu3 = check;
            _profile[_indexprofile].Cpu3Value = C3V.Value;
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
        var check = C4.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Cpu4 = check;
            _profile[_indexprofile].Cpu4Value = C4V.Value;
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
        var check = C5.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Cpu5 = check;
            _profile[_indexprofile].Cpu5Value = C5V.Value;
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
        var check = C6.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Cpu6 = check;
            _profile[_indexprofile].Cpu6Value = C6V.Value;
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
            _profile[_indexprofile].Vrm1 = check;
            _profile[_indexprofile].Vrm1Value = V1V.Value;
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
            _profile[_indexprofile].Vrm2 = check;
            _profile[_indexprofile].Vrm2Value = V2V.Value;
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
            _profile[_indexprofile].Vrm3 = check;
            _profile[_indexprofile].Vrm3Value = V3V.Value;
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
            _profile[_indexprofile].Vrm4 = check;
            _profile[_indexprofile].Vrm4Value = V4V.Value;
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
            _profile[_indexprofile].Vrm5 = check;
            _profile[_indexprofile].Vrm5Value = V5V.Value;
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
            _profile[_indexprofile].Vrm6 = check;
            _profile[_indexprofile].Vrm6Value = V6V.Value;
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
            _profile[_indexprofile].Vrm7 = check;
            _profile[_indexprofile].Vrm7Value = V7V.Value;
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
        var check = G1.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Gpu1 = check;
            _profile[_indexprofile].Gpu1Value = G1V.Value;
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
        var check = G2.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Gpu2 = check;
            _profile[_indexprofile].Gpu2Value = G2V.Value;
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
        var check = G3.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Gpu3 = check;
            _profile[_indexprofile].Gpu3Value = G3V.Value;
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
        var check = G4.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Gpu4 = check;
            _profile[_indexprofile].Gpu4Value = G4V.Value;
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
        var check = G5.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Gpu5 = check;
            _profile[_indexprofile].Gpu5Value = G5V.Value;
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
        var check = G6.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Gpu6 = check;
            _profile[_indexprofile].Gpu6Value = G6V.Value;
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
        var check = G7.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Gpu7 = check;
            _profile[_indexprofile].Gpu7Value = G7V.Value;
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
        var check = G8.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Gpu8 = check;
            _profile[_indexprofile].Gpu8Value = G8V.Value;
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
        var check = G9.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Gpu9 = check;
            _profile[_indexprofile].Gpu9Value = G9V.Value;
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
        var check = G10.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Gpu10 = check;
            _profile[_indexprofile].Gpu10Value = G10V.Value;
            ProfileSave();
        }
    }

    //Расширенные параметры

    private void A4_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = A4.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Advncd4 = check;
            _profile[_indexprofile].Advncd4Value = A4V.Value;
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
        var check = A5.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Advncd5 = check;
            _profile[_indexprofile].Advncd5Value = A5V.Value;
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
        var check = A6.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Advncd6 = check;
            _profile[_indexprofile].Advncd6Value = A6V.Value;
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
        var check = A7.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Advncd7 = check;
            _profile[_indexprofile].Advncd7Value = A7V.Value;
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
        var check = A8.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Advncd8 = check;
            _profile[_indexprofile].Advncd8Value = A8V.Value;
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
        var check = A9.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Advncd9 = check;
            _profile[_indexprofile].Advncd9Value = A9V.Value;
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
        var check = A10.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Advncd10 = check;
            _profile[_indexprofile].Advncd10Value = A10V.Value;
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
        var check = A11.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Advncd11 = check;
            _profile[_indexprofile].Advncd11Value = A11V.Value;
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
        var check = A12.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Advncd12 = check;
            _profile[_indexprofile].Advncd12Value = A12V.Value;
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
        var check = A13.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Advncd13 = check;
            _profile[_indexprofile].Advncd1Value = A13M.SelectedIndex;
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
        var check = Ccd28.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Coper15 = check;
            _profile[_indexprofile].Coper15Value = Ccd28V.Value;
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
        var check = Ccd27.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Coper14 = check;
            _profile[_indexprofile].Coper14Value = Ccd27V.Value;
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
        var check = Ccd26.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Coper13 = check;
            _profile[_indexprofile].Coper13Value = Ccd26V.Value;
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
        var check = Ccd25.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Coper12 = check;
            _profile[_indexprofile].Coper12Value = Ccd25V.Value;
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
        var check = Ccd24.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Coper11 = check;
            _profile[_indexprofile].Coper11Value = Ccd24V.Value;
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
        var check = Ccd23.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Coper10 = check;
            _profile[_indexprofile].Coper10Value = Ccd23V.Value;
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
        var check = Ccd22.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Coper9 = check;
            _profile[_indexprofile].Coper9Value = Ccd22V.Value;
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
        var check = Ccd21.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Coper8 = check;
            _profile[_indexprofile].Coper8Value = Ccd21V.Value;
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
        var check = Ccd18.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Coper7 = check;
            _profile[_indexprofile].Coper7Value = Ccd18V.Value;
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
        var check = Ccd17.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Coper6 = check;
            _profile[_indexprofile].Coper6Value = Ccd17V.Value;
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
        var check = Ccd16.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Coper5 = check;
            _profile[_indexprofile].Coper5Value = Ccd16V.Value;
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
        var check = Ccd15.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Coper4 = check;
            _profile[_indexprofile].Coper4Value = Ccd15V.Value;
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
        var check = Ccd14.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Coper3 = check;
            _profile[_indexprofile].Coper3Value = Ccd14V.Value;
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
        var check = Ccd13.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Coper2 = check;
            _profile[_indexprofile].Coper2Value = Ccd13V.Value;
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
        var check = Ccd12.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Coper1 = check;
            _profile[_indexprofile].Coper1Value = Ccd12V.Value;
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
        var check = Ccd11.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Coper0 = check;
            _profile[_indexprofile].Coper0Value = Ccd11V.Value;
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
            _profile[_indexprofile].Coall = check;
            _profile[_indexprofile].Coallvalue = O1V.Value;
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
            _profile[_indexprofile].Cogfx = check;
            _profile[_indexprofile].Cogfxvalue = O2V.Value;
            ProfileSave();
        }
    }

    private void CCD_CO_Mode_Sel_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        if (CcdCoMode.SelectedIndex > 0 && CcdCoModeSel.IsChecked == true)
        {
            HideDisabledCurveOptimizedParameters(true); //Оставить параметры изменения кривой
        }
        else
        {
            HideDisabledCurveOptimizedParameters(false); //Убрать параметры
        }

        ProfileLoad();
        var check = CcdCoModeSel.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Comode = check;
            _profile[_indexprofile].Coprefmode = CcdCoMode.SelectedIndex;
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
            _profile[_indexprofile].Cpu1Value = C1V.Value;
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
            _profile[_indexprofile].Cpu2Value = C2V.Value;
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
            _profile[_indexprofile].Cpu3Value = C3V.Value;
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
            _profile[_indexprofile].Cpu4Value = C4V.Value;
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
            _profile[_indexprofile].Cpu5Value = C5V.Value;
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
            _profile[_indexprofile].Cpu6Value = C6V.Value;
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
            _profile[_indexprofile].Vrm1Value = V1V.Value;
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
            _profile[_indexprofile].Vrm2Value = V2V.Value;
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
            _profile[_indexprofile].Vrm3Value = V3V.Value;
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
            _profile[_indexprofile].Vrm4Value = V4V.Value;
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
            _profile[_indexprofile].Vrm5Value = V5V.Value;
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
            _profile[_indexprofile].Vrm6Value = V6V.Value;
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
            _profile[_indexprofile].Vrm7Value = V7V.Value;
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
            _profile[_indexprofile].Gpu1Value = G1V.Value;
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
            _profile[_indexprofile].Gpu2Value = G2V.Value;
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
            _profile[_indexprofile].Gpu3Value = G3V.Value;
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
            _profile[_indexprofile].Gpu4Value = G4V.Value;
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
            _profile[_indexprofile].Gpu5Value = G5V.Value;
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
            _profile[_indexprofile].Gpu6Value = G6V.Value;
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
            _profile[_indexprofile].Gpu7Value = G7V.Value;
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
            _profile[_indexprofile].Gpu8Value = G8V.Value;
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
            _profile[_indexprofile].Gpu9Value = G9V.Value;
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
            _profile[_indexprofile].Gpu10Value = G10V.Value;
            ProfileSave();
        }
    }

    //Расширенные параметры

    private void A4v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Advncd4Value = A4V.Value;
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
            _profile[_indexprofile].Advncd5Value = A5V.Value;
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
            _profile[_indexprofile].Advncd6Value = A6V.Value;
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
            _profile[_indexprofile].Advncd7Value = A7V.Value;
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
            _profile[_indexprofile].Advncd8Value = A8V.Value;
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
            _profile[_indexprofile].Advncd9Value = A9V.Value;
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
            _profile[_indexprofile].Advncd10Value = A10V.Value;
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
            _profile[_indexprofile].Advncd11Value = A11V.Value;
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
            _profile[_indexprofile].Advncd12Value = A12V.Value;
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
            _profile[_indexprofile].Advncd13Value = A13M.SelectedIndex;
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
        var check = C7.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Cpu7 = check;
            _profile[_indexprofile].Cpu7Value = C7V.Value;
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
            _profile[_indexprofile].Cpu7Value = C7V.Value;
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
        var check = G16.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Gpu16 = check;
            _profile[_indexprofile].Gpu16Value = G16M.SelectedIndex;
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
            _profile[_indexprofile].Gpu16Value = G16M.SelectedIndex;
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
        var check = A14.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Advncd14 = check;
            _profile[_indexprofile].Advncd14Value = A14M.SelectedIndex;
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
            _profile[_indexprofile].Advncd14Value = A14M.SelectedIndex;
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
        var check = A15.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].Advncd15 = check;
            _profile[_indexprofile].Advncd15Value = A15V.Value;
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
            _profile[_indexprofile].Advncd15Value = A15V.Value;
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
            _profile[_indexprofile].Coallvalue = O1V.Value;
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
            _profile[_indexprofile].Cogfxvalue = O2V.Value;
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
            _profile[_indexprofile].Coper0Value = Ccd11V.Value;
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
            _profile[_indexprofile].Coper1Value = Ccd12V.Value;
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
            _profile[_indexprofile].Coper2Value = Ccd13V.Value;
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
            _profile[_indexprofile].Coper3Value = Ccd14V.Value;
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
            _profile[_indexprofile].Coper4Value = Ccd15V.Value;
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
            _profile[_indexprofile].Coper5Value = Ccd16V.Value;
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
            _profile[_indexprofile].Coper6Value = Ccd17V.Value;
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
            _profile[_indexprofile].Coper7Value = Ccd18V.Value;
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
            _profile[_indexprofile].Coper8Value = Ccd21V.Value;
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
            _profile[_indexprofile].Coper9Value = Ccd22V.Value;
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
            _profile[_indexprofile].Coper10Value = Ccd23V.Value;
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
            _profile[_indexprofile].Coper11Value = Ccd24V.Value;
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
            _profile[_indexprofile].Coper12Value = Ccd25V.Value;
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
            _profile[_indexprofile].Coper13Value = Ccd26V.Value;
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
            _profile[_indexprofile].Coper14Value = Ccd27V.Value;
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
            _profile[_indexprofile].Coper15Value = Ccd28V.Value;
            ProfileSave();
        }
    }

    private void CCD_CO_Mode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        if (CcdCoMode.SelectedIndex > 0 && CcdCoModeSel.IsChecked == true)
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
            _profile[_indexprofile].Coprefmode = CcdCoMode.SelectedIndex;
            ProfileSave();
        }
    }

    //Кнопка применить, итоговый выход, Zen States-Core SMU Command
    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var isBristol = _cpu?.info.codeName == Cpu.CodeName.BristolRidge;

            if (NormalUserMode.Visibility == Visibility.Visible)
            {
                if (C1.IsChecked == true)
                {
                    _adjline += " --tctl-temp=" + C1V.Value + (isBristol ? "000" : string.Empty);
                }

                if (C2.IsChecked == true)
                {
                    var stapmBoostMillisecondsBristol = C5V.Value * 1000 < 180000 ? C5V.Value * 1000 : 180000;
                    _adjline += " --stapm-limit=" + C2V.Value + "000" + (isBristol ? ",2," + stapmBoostMillisecondsBristol : string.Empty);
                }

                if (C3.IsChecked == true)
                {
                    _adjline += " --fast-limit=" + C3V.Value + "000";
                }

                if (C4.IsChecked == true)
                {
                    _adjline += " --slow-limit=" + C4V.Value + "000" + (isBristol ? "," + C4V.Value + "000,0" : string.Empty);
                }

                if (C5.IsChecked == true)
                {
                    _adjline += " --stapm-time=" + C5V.Value;
                }

                if (C6.IsChecked == true)
                {
                    _adjline += " --slow-time=" + C6V.Value;
                }

                if (C7.IsChecked == true)
                {
                    _adjline += " --cHTC-temp=" + C7V.Value;
                }

                //vrm
                if (V1.IsChecked == true)
                {
                    _adjline += " --vrmmax-current=" + V1V.Value + "000" +  (isBristol ? "," + V3V.Value + "000," + V3V.Value + "000" : string.Empty);
                }

                if (V2.IsChecked == true)
                {
                    _adjline += " --vrm-current=" + V2V.Value + "000" + (isBristol ? "," + V4V.Value + "000," + V4V.Value + "000" : string.Empty);
                }

                if (V3.IsChecked == true && !isBristol)
                {
                    _adjline += " --vrmsocmax-current=" + V3V.Value + "000";
                }

                if (V4.IsChecked == true && !isBristol)
                {
                    _adjline += " --vrmsoc-current=" + V4V.Value + "000";
                }

                if (V5.IsChecked == true)
                {
                    _adjline += " --psi0-current=" + V5V.Value + "000" + (isBristol ? "," + V6V.Value + "000," + V6V.Value + "000" : string.Empty); 
                }

                if (V6.IsChecked == true && !isBristol)
                {
                    _adjline += " --psi0soc-current=" + V6V.Value + "000";
                }

                if (V7.IsChecked == true)
                {
                    var prochotDeassertionTimeMillisecondsBristol = V7V.Value < 100 ? V7V.Value : 100;
                    _adjline += " --prochot-deassertion-ramp=" + (isBristol ? prochotDeassertionTimeMillisecondsBristol : V7V.Value);
                }

                //gpu
                if (G1.IsChecked == true)
                {
                    _adjline += " --min-socclk-frequency=" + G1V.Value;
                }

                if (G2.IsChecked == true)
                {
                    _adjline += " --max-socclk-frequency=" + G2V.Value;
                }

                if (G3.IsChecked == true)
                {
                    _adjline += " --min-fclk-frequency=" + G3V.Value;
                }

                if (G4.IsChecked == true)
                {
                    _adjline += " --max-fclk-frequency=" + G4V.Value;
                }

                if (G5.IsChecked == true)
                {
                    _adjline += " --min-vcn=" + G5V.Value;
                }

                if (G6.IsChecked == true)
                {
                    _adjline += " --max-vcn=" + G6V.Value;
                }

                if (G7.IsChecked == true)
                {
                    _adjline += " --min-lclk=" + G7V.Value;
                }

                if (G8.IsChecked == true)
                {
                    _adjline += " --max-lclk=" + G8V.Value;
                }

                if (G9.IsChecked == true)
                {
                    _adjline += " --min-gfxclk=" + G9V.Value;
                }

                if (G10.IsChecked == true)
                {
                    _adjline += " --max-gfxclk=" + G10V.Value;
                }

                if (G16.IsChecked == true)
                {
                    _cpu ??= CpuSingleton.GetInstance();
                    _adjline += _cpu.info.codeName switch
                    {
                        Cpu.CodeName.RavenRidge or Cpu.CodeName.FireFlight or Cpu.CodeName.Cezanne or Cpu.CodeName.Renoir or Cpu.CodeName.Lucienne => G16M.SelectedIndex != 0 ? " --disable-feature=0,32" : " --enable-feature=0,32",
                        Cpu.CodeName.Rembrandt or Cpu.CodeName.Mendocino or Cpu.CodeName.Phoenix or Cpu.CodeName.Phoenix2 or Cpu.CodeName.HawkPoint or Cpu.CodeName.StrixPoint or Cpu.CodeName.StrixHalo or Cpu.CodeName.KrackanPoint => G16M.SelectedIndex != 0 ? " --disable-feature=0,16" : " --enable-feature=0,16",
                        Cpu.CodeName.Raphael or Cpu.CodeName.GraniteRidge or Cpu.CodeName.Genoa or Cpu.CodeName.StormPeak or Cpu.CodeName.DragonRange or Cpu.CodeName.Bergamo => G16M.SelectedIndex != 0 ? " --disable-feature=128" : " --enable-feature=128",
                        _ => G16M.SelectedIndex != 0 ? " --setcpu-freqto-ramstate=0" : " --stopcpu-freqto-ramstate=0",
                    };
                }

                //advanced

                if (A4.IsChecked == true)
                {
                    _adjline += " --psi3cpu_current=" + A4V.Value + "000";
                }

                if (A5.IsChecked == true)
                {
                    _adjline += " --psi3gfx_current=" + A5V.Value + "000";
                }

                if (A6.IsChecked == true)
                {
                    _adjline += " --apu-skin-temp=" + A6V.Value * 256;
                }

                if (A7.IsChecked == true)
                {
                    _adjline += " --dgpu-skin-temp=" + A7V.Value * 256;
                }

                if (A8.IsChecked == true)
                {
                    _adjline += " --apu-slow-limit=" + A8V.Value + "000";
                }

                if (A9.IsChecked == true)
                {
                    _adjline += " --skin-temp-limit=" + A9V.Value + "000";

                    if (_isStapmTuneRequired)
                    {
                        _adjline += " --stapm-limit=" + A9V.Value + "000";
                    }
                }

                if (A10.IsChecked == true)
                {
                    var val = 0x480000 | (int)A10V.Value; // Always at 1.1V
                    _cpu ??= CpuSingleton.GetInstance();
                    _adjline += _cpu.info.codeName switch
                    {
                        Cpu.CodeName.RavenRidge or Cpu.CodeName.FireFlight or Cpu.CodeName.Dali or Cpu.CodeName.Picasso => " --set-gpuclockoverdrive-byvid=" + val,
                        _ => " --gfx-clk=" + A10V.Value,
                    };
                }

                if (A11.IsChecked == true)
                {
                    _adjline += " --oc-clk=" + A11V.Value;
                }

                if (A12.IsChecked == true)
                {
                    _adjline += " --oc-volt=" + Math.Round((1.55 - A12V.Value / 1000) / 0.00625);
                }


                if (A13.IsChecked == true)
                {
                    _adjline += A13M.SelectedIndex switch
                    {
                        2 => " --power-saving=1",
                        _ => " --max-performance=1",
                    };
                }

                if (A14.IsChecked == true)
                {
                    _adjline += A14M.SelectedIndex switch
                    {
                        1 => " --enable-oc=1",
                        _ => " --disable-oc=1",
                    };
                }

                if (A15.IsChecked == true)
                {
                    _adjline += " --pbo-scalar=" + A15V.Value * 100;
                }

                if (O1.IsChecked == true)
                {
                    _adjline += (O1V.Value >= 0.0) ? $" --set-coall={O1V.Value} " : $" --set-coall={Convert.ToUInt32(0x100000 - (uint)(-1 * (int)O1V.Value))} ";
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
                                _cpu.SetPsmMarginSingleCore(GetCoreMask(i), Convert.ToInt32(O2V.Value));
                            }
                        }
                    }

                    _cpu!.smu.Rsmu.SMU_MSG_SetDldoPsmMargin = SendSmuCommand.ReturnCoPer(_cpu.info.codeName, false);
                    _cpu!.smu.Mp1Smu.SMU_MSG_SetDldoPsmMargin = SendSmuCommand.ReturnCoPer(_cpu.info.codeName, true);
                }

                if (CcdCoModeSel.IsChecked == true &&
                    CcdCoMode.SelectedIndex != 0) // Если пользователь выбрал хотя-бы один режим и ...
                {
                    if (CcdCoMode.SelectedIndex == 1) // Если выбран режим ноутбук
                    {
                        if (_cpu?.info.codeName == Cpu.CodeName.DragonRange) // Так как там как у компьютеров
                        {
                            if (Ccd11.IsChecked == true)
                            {
                                _adjline += $" --set-coper={0 | ((int)Ccd11V.Value & 0xFFFF)} ";
                            }

                            if (Ccd12.IsChecked == true)
                            {
                                _adjline += $" --set-coper={1048576 | ((int)Ccd12V.Value & 0xFFFF)} ";
                            }

                            if (Ccd13.IsChecked == true)
                            {
                                _adjline += $" --set-coper={2097152 | ((int)Ccd13V.Value & 0xFFFF)} ";
                            }

                            if (Ccd14.IsChecked == true)
                            {
                                _adjline += $" --set-coper={3145728 | ((int)Ccd14V.Value & 0xFFFF)} ";
                            }

                            if (Ccd15.IsChecked == true)
                            {
                                _adjline += $" --set-coper={4194304 | ((int)Ccd15V.Value & 0xFFFF)} ";
                            }

                            if (Ccd16.IsChecked == true)
                            {
                                _adjline += $" --set-coper={5242880 | ((int)Ccd16V.Value & 0xFFFF)} ";
                            }

                            if (Ccd17.IsChecked == true)
                            {
                                _adjline += $" --set-coper={6291456 | ((int)Ccd17V.Value & 0xFFFF)} ";
                            }

                            if (Ccd18.IsChecked == true)
                            {
                                _adjline += $" --set-coper={7340032 | ((int)Ccd18V.Value & 0xFFFF)} ";
                            }

                            if (Ccd21.IsChecked == true)
                            {
                                _adjline +=
                                    $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((0 % 8) & 15)) << 20) | ((int)Ccd21V.Value & 0xFFFF)} ";
                            }

                            if (Ccd22.IsChecked == true)
                            {
                                _adjline +=
                                    $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((1 % 8) & 15)) << 20) | ((int)Ccd22V.Value & 0xFFFF)} ";
                            }

                            if (Ccd23.IsChecked == true)
                            {
                                _adjline +=
                                    $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((2 % 8) & 15)) << 20) | ((int)Ccd23V.Value & 0xFFFF)} ";
                            }

                            if (Ccd24.IsChecked == true)
                            {
                                _adjline +=
                                    $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((3 % 8) & 15)) << 20) | ((int)Ccd24V.Value & 0xFFFF)} ";
                            }

                            if (Ccd25.IsChecked == true)
                            {
                                _adjline +=
                                    $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((4 % 8) & 15)) << 20) | ((int)Ccd25V.Value & 0xFFFF)} ";
                            }

                            if (Ccd26.IsChecked == true)
                            {
                                _adjline +=
                                    $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((5 % 8) & 15)) << 20) | ((int)Ccd26V.Value & 0xFFFF)} ";
                            }

                            if (Ccd27.IsChecked == true)
                            {
                                _adjline +=
                                    $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((6 % 8) & 15)) << 20) | ((int)Ccd27V.Value & 0xFFFF)} ";
                            }

                            if (Ccd28.IsChecked == true)
                            {
                                _adjline +=
                                    $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((7 % 8) & 15)) << 20) | ((int)Ccd28V.Value & 0xFFFF)} ";
                            }
                        }
                        else
                        {
                            if (Ccd11.IsChecked == true)
                            {
                                _adjline += $" --set-coper={0 | ((int)Ccd11V.Value & 0xFFFF)} ";
                            }

                            if (Ccd12.IsChecked == true)
                            {
                                _adjline += $" --set-coper={(1 << 20) | ((int)Ccd12V.Value & 0xFFFF)} ";
                            }

                            if (Ccd13.IsChecked == true)
                            {
                                _adjline += $" --set-coper={(2 << 20) | ((int)Ccd13V.Value & 0xFFFF)} ";
                            }

                            if (Ccd14.IsChecked == true)
                            {
                                _adjline += $" --set-coper={(3 << 20) | ((int)Ccd14V.Value & 0xFFFF)} ";
                            }

                            if (Ccd15.IsChecked == true)
                            {
                                _adjline += $" --set-coper={(4 << 20) | ((int)Ccd15V.Value & 0xFFFF)} ";
                            }

                            if (Ccd16.IsChecked == true)
                            {
                                _adjline += $" --set-coper={(5 << 20) | ((int)Ccd16V.Value & 0xFFFF)} ";
                            }

                            if (Ccd17.IsChecked == true)
                            {
                                _adjline += $" --set-coper={(6 << 20) | ((int)Ccd17V.Value & 0xFFFF)} ";
                            }

                            if (Ccd18.IsChecked == true)
                            {
                                _adjline += $" --set-coper={(7 << 20) | ((int)Ccd18V.Value & 0xFFFF)} ";
                            }
                        }
                    }
                    else if (CcdCoMode.SelectedIndex == 2) //Если выбран режим компьютер
                    {
                        if (Ccd11.IsChecked == true)
                        {
                            _adjline += $" --set-coper={0 | ((int)Ccd11V.Value & 0xFFFF)} ";
                        }

                        if (Ccd12.IsChecked == true)
                        {
                            _adjline += $" --set-coper={1048576 | ((int)Ccd12V.Value & 0xFFFF)} ";
                        }

                        if (Ccd13.IsChecked == true)
                        {
                            _adjline += $" --set-coper={2097152 | ((int)Ccd13V.Value & 0xFFFF)} ";
                        }

                        if (Ccd14.IsChecked == true)
                        {
                            _adjline += $" --set-coper={3145728 | ((int)Ccd14V.Value & 0xFFFF)} ";
                        }

                        if (Ccd15.IsChecked == true)
                        {
                            _adjline += $" --set-coper={4194304 | ((int)Ccd15V.Value & 0xFFFF)} ";
                        }

                        if (Ccd16.IsChecked == true)
                        {
                            _adjline += $" --set-coper={5242880 | ((int)Ccd16V.Value & 0xFFFF)} ";
                        }

                        if (Ccd17.IsChecked == true)
                        {
                            _adjline += $" --set-coper={6291456 | ((int)Ccd17V.Value & 0xFFFF)} ";
                        }

                        if (Ccd18.IsChecked == true)
                        {
                            _adjline += $" --set-coper={7340032 | ((int)Ccd18V.Value & 0xFFFF)} ";
                        }

                        if (Ccd21.IsChecked == true)
                        {
                            _adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((0 % 8) & 15)) << 20) | ((int)Ccd21V.Value & 0xFFFF)} ";
                        }

                        if (Ccd22.IsChecked == true)
                        {
                            _adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((1 % 8) & 15)) << 20) | ((int)Ccd22V.Value & 0xFFFF)} ";
                        }

                        if (Ccd23.IsChecked == true)
                        {
                            _adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((2 % 8) & 15)) << 20) | ((int)Ccd23V.Value & 0xFFFF)} ";
                        }

                        if (Ccd24.IsChecked == true)
                        {
                            _adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((3 % 8) & 15)) << 20) | ((int)Ccd24V.Value & 0xFFFF)} ";
                        }

                        if (Ccd25.IsChecked == true)
                        {
                            _adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((4 % 8) & 15)) << 20) | ((int)Ccd25V.Value & 0xFFFF)} ";
                        }

                        if (Ccd26.IsChecked == true)
                        {
                            _adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((5 % 8) & 15)) << 20) | ((int)Ccd26V.Value & 0xFFFF)} ";
                        }

                        if (Ccd27.IsChecked == true)
                        {
                            _adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((6 % 8) & 15)) << 20) | ((int)Ccd27V.Value & 0xFFFF)} ";
                        }

                        if (Ccd28.IsChecked == true)
                        {
                            _adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((7 % 8) & 15)) << 20) | ((int)Ccd28V.Value & 0xFFFF)} ";
                        }
                    }
                    else if
                        (CcdCoMode.SelectedIndex ==
                         3) // Если выбран режим с использованием метода от Ирусанова, Irusanov, https://github.com/irusanov
                    {
                        _cpu!.smu.Rsmu.SMU_MSG_SetDldoPsmMargin = SendSmuCommand.ReturnCoPer(_cpu.info.codeName, false);
                        _cpu!.smu.Mp1Smu.SMU_MSG_SetDldoPsmMargin = SendSmuCommand.ReturnCoPer(_cpu.info.codeName, true);
                        for (var i = 0; i < _cpu?.info.topology.physicalCores; i++)
                        {
                            var checkbox = i < 8
                                ? (CheckBox)Ccd1Grid.FindName($"Ccd1{i + 1}")
                                : (CheckBox)Ccd2Grid.FindName($"Ccd2{i - 7}");
                            if (checkbox != null && checkbox.IsChecked == true)
                            {
                                var setVal = i < 8
                                    ? (Slider)Ccd1Grid.FindName($"Ccd1{i + 1}V")
                                    : (Slider)Ccd2Grid.FindName($"Ccd2{i - 7}V");
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
            }


            if (SmuFuncEnableToggle.IsOn)
            {
                if (Bit0FeatureCclkController.IsOn)
                {
                    _adjline += " --enable-feature=1";
                }
                else
                {
                    _adjline += " --disable-feature=1";
                }

                if (Bit2FeatureDataCalculation.IsOn)
                {
                    _adjline += " --enable-feature=4";
                }
                else
                {
                    _adjline += " --disable-feature=4";
                }

                if (Bit3FeaturePpt.IsOn)
                {
                    _adjline += " --enable-feature=8";
                }
                else
                {
                    _adjline += " --disable-feature=8";
                }

                if (Bit4FeatureTdc.IsOn)
                {
                    _adjline += " --enable-feature=16";
                }
                else
                {
                    _adjline += " --disable-feature=16";
                }

                if (Bit5FeatureThermal.IsOn)
                {
                    _adjline += " --enable-feature=32";
                }
                else
                {
                    _adjline += " --disable-feature=32";
                }

                if (Bit8FeaturePllPowerDown.IsOn)
                {
                    _adjline += " --enable-feature=256";
                }
                else
                {
                    _adjline += " --disable-feature=256";
                }

                if (Bit37FeatureProchot.IsOn)
                {
                    _adjline += " --enable-feature=0,32";
                }
                else
                {
                    _adjline += " --disable-feature=0,32";
                }

                if (Bit39FeatureStapm.IsOn)
                {
                    _adjline += " --enable-feature=0,128";
                }
                else
                {
                    _adjline += " --disable-feature=0,128";
                }

                if (Bit40FeatureCoreCstates.IsOn)
                {
                    _adjline += " --enable-feature=0,256";
                }
                else
                {
                    _adjline += " --disable-feature=0,256";
                }

                if (Bit41FeatureGfxDutyCycle.IsOn)
                {
                    _adjline += " --enable-feature=0,512";
                }
                else
                {
                    _adjline += " --disable-feature=0,512";
                }

                if (Bit42FeatureAaMode.IsOn)
                {
                    _adjline += " --enable-feature=0,1024";
                }
                else
                {
                    _adjline += " --disable-feature=0,1024";
                }
            }

            AppSettings.RyzenAdjLine = _adjline + " ";
            _adjline = "";
            ApplyInfo = "";
            AppSettings.SaveSettings();
            SendSmuCommand.SetCpuCodename(_cpu!.info.codeName);
            MainWindow.Applyer.Apply(AppSettings.RyzenAdjLine, true, AppSettings.ReapplyOverclock,
                AppSettings.ReapplyOverclockTimer);
            if (EnablePstates.IsOn)
            {
                BtnPstateWrite_Click();
            }

            if (TextBoxArg0 != null &&
                TextBoxArgAddress != null &&
                TextBoxCmd != null &&
                TextBoxCmdAddress != null &&
                TextBoxRspAddress != null &&
                EnableSmu.IsOn)
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
                ApplyTooltip.Title = "Apply_Success".GetLocalized();
                ApplyTooltip.Subtitle = "Apply_Success_Desc".GetLocalized();
            }
            else
#pragma warning disable CS0162 // Unreachable code detected
            // ReSharper disable once HeuristicUnreachableCode
            {
                ApplyTooltip.Title = "Apply_Success".GetLocalized();
                ApplyTooltip.Subtitle = "Apply_Success_Desc".GetLocalized() + AppSettings.RyzenAdjLine;
            }
#pragma warning restore CS0162 // Unreachable code detected
            ApplyTooltip.IconSource = new SymbolIconSource { Symbol = Symbol.Accept };
            ApplyTooltip.IsOpen = true;
            var infoSet = InfoBarSeverity.Success;
            if (ApplyInfo != string.Empty && _commandReturnedValue == false)
            {
                await LogHelper.Log(ApplyInfo);
                ApplyTooltip.Title = "Apply_Warn".GetLocalized();
                ApplyTooltip.Subtitle = "Apply_Warn_Desc".GetLocalized() + ApplyInfo;
                ApplyTooltip.IconSource = new SymbolIconSource { Symbol = Symbol.ReportHacked };
                await Task.Delay(timer);
                ApplyTooltip.IsOpen = false;
                infoSet = InfoBarSeverity.Warning;
            }
            else
            {
                if (_commandReturnedValue)
                {
                    ApplyTooltip.Subtitle = ApplyInfo;
                }
                await Task.Delay(3000);
                ApplyTooltip.IsOpen = false;
            }

            NotificationsService.Notifies ??= [];
            NotificationsService.Notifies.Add(new Notify
            {
                Title = ApplyTooltip.Title,
                Msg = ApplyTooltip.Subtitle.Replace("Param_DeveloperOptions_ResultSaved".GetLocalized(), string.Empty) + ((ApplyInfo != string.Empty && !_commandReturnedValue) ? "DELETEUNAVAILABLE" : ""),
                Type = infoSet
            });
            NotificationsService.SaveNotificationsSettings();
            _commandReturnedValue = false;
            SendSmuCommand.Play_Invernate_QuickSMU(0);
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception.ToString());
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (SaveProfileN.Text != "")
            {
                ProfileLoad();
                try
                {
                    ActionButtonSave.Flyout.Hide();
                    AppSettings.Preset += 1;
                    _indexprofile += 1;
                    _waitforload = true;
                    ProfileCom.Items.Add(SaveProfileN.Text);
                    ProfileCom.SelectedItem = SaveProfileN.Text;
                    if (_profile.Length == 0)
                    {
                        _profile = new Profile[1];
                        _profile[0] = new Profile { Profilename = SaveProfileN.Text };
                    }
                    else
                    {
                        var profileList = new List<Profile>(_profile)
                        {
                            new()
                            {
                                Profilename = SaveProfileN.Text
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
                    AddTooltipMax.IsOpen = true;
                    await Task.Delay(3000);
                    AddTooltipMax.IsOpen = false;
                }
            }
            else
            {
                NotificationsService.Notifies ??= [];
                NotificationsService.Notifies.Add(new Notify
                {
                    Title = AddTooltipError.Title,
                    Msg = AddTooltipError.Subtitle,
                    Type = InfoBarSeverity.Error
                });
                NotificationsService.SaveNotificationsSettings();
                AddTooltipError.IsOpen = true;
                await Task.Delay(3000);
                AddTooltipError.IsOpen = false;
            }

            AppSettings.SaveSettings();
            ProfileSave();
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception.ToString());
        }
    }

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            EditProfileButton.Flyout.Hide();
            if (EditProfileN.Text != "")
            {
                var backupIndex = ProfileCom.SelectedIndex;
                if (ProfileCom.SelectedIndex == 0 || _indexprofile + 1 == 0)
                {
                    UnsavedTooltip.IsOpen = true;
                    await Task.Delay(3000);
                    UnsavedTooltip.IsOpen = false;
                }
                else
                {
                    ProfileLoad();
                    _profile[_indexprofile].Profilename = EditProfileN.Text;
                    ProfileSave();
                    _waitforload = true;
                    ProfileCom.Items.Clear();
                    ProfileCom.Items.Add(new ComboBoxItem
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
                        if (currProfile.Profilename != string.Empty || currProfile.Profilename != "Unsigned profile")
                        {
                            ProfileCom.Items.Add(currProfile.Profilename);
                        }
                    }

                    ProfileCom.SelectedIndex = 0;
                    _waitforload = false;
                    ProfileCom.SelectedIndex = backupIndex;
                    NotificationsService.Notifies ??= [];
                    NotificationsService.Notifies.Add(new Notify
                    {
                        Title = EditTooltip.Title,
                        Msg = EditTooltip.Subtitle + " " + SaveProfileN.Text,
                        Type = InfoBarSeverity.Success
                    });
                    EditTooltip.IsOpen = true;
                    await Task.Delay(3000);
                    EditTooltip.IsOpen = false;
                }
            }
            else
            {
                NotificationsService.Notifies ??= [];
                NotificationsService.Notifies.Add(new Notify
                {
                    Title = EditTooltipError.Title,
                    Msg = EditTooltipError.Subtitle,
                    Type = InfoBarSeverity.Error
                });
                EditTooltipError.IsOpen = true;
                await Task.Delay(3000);
                EditTooltipError.IsOpen = false;
            }
            NotificationsService.SaveNotificationsSettings();
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception.ToString());
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        try
        {
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
                if (ProfileCom.SelectedIndex == 0)
                {
                    NotificationsService.Notifies ??= [];
                    NotificationsService.Notifies.Add(new Notify
                    {
                        Title = DeleteTooltipError.Title,
                        Msg = DeleteTooltipError.Subtitle,
                        Type = InfoBarSeverity.Error
                    });
                    NotificationsService.SaveNotificationsSettings();
                    DeleteTooltipError.IsOpen = true;
                    await Task.Delay(3000);
                    DeleteTooltipError.IsOpen = false;
                }
                else
                {
                    ProfileLoad();
                    _waitforload = true;
                    ProfileCom.Items.Remove(ProfileCom.SelectedItem);
                    var profileList = new List<Profile>(_profile);
                    profileList.RemoveAt(_indexprofile);
                    _profile = [.. profileList];
                    _indexprofile = 0;
                    _waitforload = false;

                    ProfileCom.SelectedIndex = ProfileCom.Items.Count - 1;
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
            await LogHelper.TraceIt_TraceError(exception.ToString());
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
            SmuFuncEnableToggle.IsOn = SmuFuncEnableToggle.IsOn != true;
        }

        try
        {
            ProfileLoad();
            _profile[ProfileCom.SelectedIndex - 1].SmuFunctionsEnabl = SmuFuncEnableToggle.IsOn;
            ProfileSave();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex.ToString());
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
            Bit0FeatureCclkController.IsOn = Bit0FeatureCclkController.IsOn != true;
        }

        try
        {
            ProfileLoad();
            _profile[ProfileCom.SelectedIndex - 1].SmuFeatureCclk = Bit0FeatureCclkController.IsOn;
            ProfileSave();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex.ToString());
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
            Bit2FeatureDataCalculation.IsOn = Bit2FeatureDataCalculation.IsOn != true;
        }

        try
        {
            ProfileLoad();
            _profile[ProfileCom.SelectedIndex - 1].SmuFeatureData = Bit2FeatureDataCalculation.IsOn;
            ProfileSave();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex.ToString());
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
            Bit3FeaturePpt.IsOn = Bit3FeaturePpt.IsOn != true;
        }

        try
        {
            ProfileLoad();
            _profile[ProfileCom.SelectedIndex - 1].SmuFeaturePpt = Bit3FeaturePpt.IsOn;
            ProfileSave();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex.ToString());
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
            Bit4FeatureTdc.IsOn = Bit4FeatureTdc.IsOn != true;
        }

        try
        {
            ProfileLoad();
            _profile[ProfileCom.SelectedIndex - 1].SmuFeatureTdc = Bit4FeatureTdc.IsOn;
            ProfileSave();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex.ToString());
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
            Bit5FeatureThermal.IsOn = Bit5FeatureThermal.IsOn != true;
        }

        try
        {
            ProfileLoad();
            _profile[ProfileCom.SelectedIndex - 1].SmuFeatureThermal = Bit5FeatureThermal.IsOn;
            ProfileSave();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex.ToString());
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
            Bit8FeaturePllPowerDown.IsOn = Bit8FeaturePllPowerDown.IsOn != true;
        }

        try
        {
            ProfileLoad();
            _profile[ProfileCom.SelectedIndex - 1].SmuFeaturePowerDown = Bit8FeaturePllPowerDown.IsOn;
            ProfileSave();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex.ToString());
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
            Bit37FeatureProchot.IsOn = Bit37FeatureProchot.IsOn != true;
        }

        try
        {
            ProfileLoad();
            _profile[ProfileCom.SelectedIndex - 1].SmuFeatureProchot = Bit37FeatureProchot.IsOn;
            ProfileSave();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex.ToString());
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
            Bit39FeatureStapm.IsOn = Bit39FeatureStapm.IsOn != true;
        }

        try
        {
            ProfileLoad();
            _profile[ProfileCom.SelectedIndex - 1].SmuFeatureStapm = Bit39FeatureStapm.IsOn;
            ProfileSave();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex.ToString());
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
            Bit40FeatureCoreCstates.IsOn = Bit40FeatureCoreCstates.IsOn != true;
        }

        try
        {
            ProfileLoad();
            _profile[ProfileCom.SelectedIndex - 1].SmuFeatureCStates = Bit40FeatureCoreCstates.IsOn;
            ProfileSave();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex.ToString());
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
            Bit41FeatureGfxDutyCycle.IsOn = Bit41FeatureGfxDutyCycle.IsOn != true;
        }

        try
        {
            ProfileLoad();
            _profile[ProfileCom.SelectedIndex - 1].SmuFeatureGfxDutyCycle = Bit41FeatureGfxDutyCycle.IsOn;
            ProfileSave();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex.ToString());
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
            Bit42FeatureAaMode.IsOn = Bit42FeatureAaMode.IsOn != true;
        }

        try
        {
            ProfileLoad();
            _profile[ProfileCom.SelectedIndex - 1].SmuFeatureAplusA = Bit42FeatureAaMode.IsOn;
            ProfileSave();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex.ToString());
        }
    }

    //NumberBoxes
    private void TargetNumberBox_FocusEngaged(object sender, object args)
    {
        if (sender is NumberBox numberBox)
        {
            numberBox.SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden;
        }
    }

    private void TargetNumberBox_FocusDisengaged(object sender, object args)
    {
        if (sender is NumberBox numberBox)
        {
            numberBox.SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline;
        }
    }

    private void TargetNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        {
            
            var name = sender.Tag.ToString();
            
            if (name != null)
            {
                object sliderObject;

                try
                {
                    sliderObject = FindName(name);
                }
                catch (Exception ex)
                {
                    LogHelper.TraceIt_TraceError(ex.ToString());
                    return;
                }

                if (sliderObject is Slider slider)
                {
                    if (slider.Maximum < sender.Value)
                    {
                        slider.Maximum = FromValueToUpperFive(sender.Value);
                    }
                }
            }
        }
    }

    private void BackToNormalMode_Click(object sender, RoutedEventArgs e)
    {
        ToolTipService.SetToolTip(ActionButtonApply, "Param_Apply/ToolTipService/ToolTip".GetLocalized());
        DeveloperSettingsMode.Visibility = Visibility.Collapsed;
        DeveloperOptionsApply.Visibility = Visibility.Collapsed;
        NormalUserMode.Visibility = Visibility.Visible;
        ParamName.Text = "Param_Name/Text".GetLocalized();
        SuggestionsFilterStackPanel.Visibility = Visibility.Visible;
    }

    private void OpenDeveloperOptionsMode_Click(object sender, RoutedEventArgs e)
    {
        ToolTipService.SetToolTip(ActionButtonApply, "Param_Apply_DevOptions/ToolTipService/ToolTip".GetLocalized());
        NormalUserMode.Visibility = Visibility.Collapsed;
        DeveloperSettingsMode.Visibility = Visibility.Visible;
        DeveloperOptionsApply.Visibility = Visibility.Visible;
        ParamName.Text = "Param_DeveloperOptions_Name/Text".GetLocalized();
        SuggestionsFilterStackPanel.Visibility = Visibility.Collapsed;
    }
    #endregion

    #region PState Section related voids

    private async void BtnPstateWrite_Click()
    {
        try
        {
            await LogHelper.Log("P-States writing...");
            _profile[AppSettings.Preset].Did0 = Did0.Value;
            _profile[AppSettings.Preset].Did1 = Did1.Value;
            _profile[AppSettings.Preset].Did2 = Did2.Value;
            _profile[AppSettings.Preset].Fid0 = Fid0.Value;
            _profile[AppSettings.Preset].Fid1 = Fid1.Value;
            _profile[AppSettings.Preset].Fid2 = Fid2.Value;
            _profile[AppSettings.Preset].Vid0 = Vid0.Value;
            _profile[AppSettings.Preset].Vid1 = Vid1.Value;
            _profile[AppSettings.Preset].Vid2 = Vid2.Value;
            ProfileSave();
            if (_profile[AppSettings.Preset].AutoPstate)
            {
                if (WithoutP0State.IsOn)
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
                    if (WithoutP0State.IsOn)
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
                    if (WithoutP0State.IsOn)
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
                            await LogHelper.TraceIt_TraceError(ex.ToString());
                            WritePstatesWithoutP0();
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            await LogHelper.TraceIt_TraceError(e.ToString());
        }
    }

    public static void WritePstates()
    {
        try
        {
            var cpu = CpuSingleton.GetInstance();

            if (cpu.info.family < Cpu.Family.FAMILY_17H) 
            {
                return;
            }

            ProfileLoad();
            PstatesDid[0] = _profile[AppSettings.Preset].Did0;
            PstatesDid[1] = _profile[AppSettings.Preset].Did1;
            PstatesDid[2] = _profile[AppSettings.Preset].Did2;
            PstatesFid[0] = _profile[AppSettings.Preset].Fid0;
            PstatesFid[1] = _profile[AppSettings.Preset].Fid1;
            PstatesFid[2] = _profile[AppSettings.Preset].Fid2;
            PstatesVid[0] = _profile[AppSettings.Preset].Vid0;
            PstatesVid[1] = _profile[AppSettings.Preset].Vid1;
            PstatesVid[2] = _profile[AppSettings.Preset].Vid2;
            for (var p = 0; p < 3; p++)
            {
                if (PstatesFid[p] == 0 || PstatesDid[p] == 0 || PstatesVid[p] == 0)
                {
                    ReadPstate();
                    LogHelper.LogError("Corrupted P-States in config");
                }

                // Установка стандартных значений
                uint eax = 0, edx = 0;
                if (cpu?.ReadMsr(Convert.ToUInt32(Convert.ToInt64(0xC0010064) + p), ref eax, ref edx) == false)
                {
                    LogHelper.LogError("Error reading P-States");
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
            LogHelper.TraceIt_TraceError(ex.ToString());
        }
    }

    private void WritePstatesWithoutP0()
    {
        try
        {
            for (var p = 1; p < 3; p++)
            {
                if (string.IsNullOrEmpty(Did1.Text)
                    || string.IsNullOrEmpty(Fid1.Text)
                    || string.IsNullOrEmpty(Did2.Text)
                    || string.IsNullOrEmpty(Fid2.Text))
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
                        didtext = Did1.Text;
                        fidtext = Fid1.Text;
                        break;
                    case 2:
                        didtext = Did2.Text;
                        fidtext = Fid2.Text;
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
            LogHelper.TraceIt_TraceError(ex.ToString());
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
                    [.. Enumerable.Range(0, Environment.ProcessorCount)]);
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
            LogHelper.TraceIt_TraceError(ex.ToString());
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
                    LogHelper.TraceIt_TraceError(ex.ToString());
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
            LogHelper.TraceIt_TraceError(ex.ToString());
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
                    LogHelper.TraceIt_TraceError(ex.ToString());
                }

                CalculatePstateDetails(eax, out _, out _, out _, out var cpuDfsId, out var cpuFid);
                switch (i)
                {
                    case 0:
                        Did0.Text = Convert.ToString(cpuDfsId, 10);
                        Fid0.Text = Convert.ToString(cpuFid, 10);
                        P0Freq.Content = cpuFid * 25 / (cpuDfsId * 12.5) * 100;
                        var mult0V = (int)(cpuFid * 25 / (cpuDfsId * 12.5));
                        mult0V -= 4;
                        if (mult0V <= 0)
                        {
                            mult0V = 0;
                            LogHelper.LogError("Error reading CPU multiply");
                            App.MainWindow.ShowMessageDialogAsync("Error while reading CPU multiply", "Critical Error");
                        }

                        Mult0.SelectedIndex = mult0V;
                        break;
                    case 1:
                        Did1.Text = Convert.ToString(cpuDfsId, 10);
                        Fid1.Text = Convert.ToString(cpuFid, 10);
                        P1Freq.Content = cpuFid * 25 / (cpuDfsId * 12.5) * 100;
                        var mult1V = (int)(cpuFid * 25 / (cpuDfsId * 12.5));
                        mult1V -= 4;
                        if (mult1V <= 0)
                        {
                            mult1V = 0;
                            LogHelper.LogError("Error reading CPU multiply");
                            App.MainWindow.ShowMessageDialogAsync("Error while reading CPU multiply", "Critical Error");
                        }

                        Mult1.SelectedIndex = mult1V;
                        break;
                    case 2:
                        Did2.Text = Convert.ToString(cpuDfsId, 10);
                        Fid2.Text = Convert.ToString(cpuFid, 10);
                        P2Freq.Content = cpuFid * 25 / (cpuDfsId * 12.5) * 100;
                        var mult2V = (int)(cpuFid * 25 / (cpuDfsId * 12.5));
                        mult2V -= 4;
                        if (mult2V <= 0)
                        {
                            mult2V = 0;
                            LogHelper.LogError("Error reading CPU multiply");
                            App.MainWindow.ShowMessageDialogAsync("Error while reading CPU multiply", "Critical Error");
                        }

                        Mult2.SelectedIndex = mult2V;
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex.ToString());
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
        if (TurboBoostToggle.IsEnabled)
        {
            TurboBoostToggle.IsOn = !TurboBoostToggle.IsOn;
        }

        TurboBoost();
    }

    private void Autoapply_Click(object sender, RoutedEventArgs e)
    {
        AutoApplyPstates.IsOn = !AutoApplyPstates.IsOn;
        Autoapply();
    }

    private void WithoutP0_Click(object sender, RoutedEventArgs e)
    {
        WithoutP0State.IsOn = !WithoutP0State.IsOn;
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
            _profile[_indexprofile].EnablePstateEditor = EnablePstates.IsOn;

            ProfileSave();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex.ToString());
            _indexprofile = 0;
        }
    }

    private void TurboBoost()
    {
        SetCorePerformanceBoost(TurboBoostToggle.IsOn); //Турбобуст... 
        _profile[_indexprofile].TurboBoost = TurboBoostToggle.IsOn; //Сохранение

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
        if (AutoApplyPstates.IsOn)
        {
            _profile[_indexprofile].AutoPstate = true;
            ProfileSave();
        }
        else
        {
            _profile[_indexprofile].AutoPstate = false;
            ProfileSave();
        }
    }

    private void WithoutP0()
    {
        if (WithoutP0State.IsOn)
        {
            _profile[_indexprofile].P0Ignorewarn = true;
            ProfileSave();
        }
        else
        {
            _profile[_indexprofile].P0Ignorewarn = false;
            ProfileSave();
        }
    }

    private void IgnoreWarning()
    {
        if (IgnoreWarn.IsOn)
        {
            _profile[_indexprofile].IgnoreWarn = true;
            ProfileSave();
        }
        else
        {
            _profile[_indexprofile].IgnoreWarn = false;
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
                    var didValue = Did0.Value;
                    var fidValue = Fid0.Value;
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

                        P0Freq.Content = (mult0V + 4) * 100;
                        Mult0.SelectedIndex = (int)mult0V;
                    }
                    catch (Exception ex)
                    {
                        await LogHelper.TraceIt_TraceError(ex.ToString());
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
            await LogHelper.TraceIt_TraceError(e.ToString());
        }
    }

    private async void Mult_0_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (_waitforload == false)
            {
                await Task.Delay(20);
                var didValue = Did0.Value;
                if (Did0.Text != string.Empty)
                {
                    _waitforload = true;
                    var fidValue = (Mult0.SelectedIndex + 4) * didValue / 2;
                    _relay = true;
                    Fid0.Value = fidValue;
                    await Task.Delay(40);
                    Fid0.Value = fidValue;
                    P0Freq.Content = (Mult0.SelectedIndex + 4) * 100;
                    Save_ID0();
                    await Task.Delay(40);
                    _waitforload = false;
                }
            }
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception.ToString());
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
            var didValue = Did0.Value;
            var fidValue = Fid0.Value;
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

            P0Freq.Content = (mult0V + 4) * 100;
            try
            {
                Mult0.SelectedIndex = (int)mult0V;
            }
            catch (Exception ex)
            {
                await LogHelper.TraceIt_TraceError(ex.ToString());
            }

            Save_ID0();
        }
        catch (Exception e)
        {
            await LogHelper.TraceIt_TraceError(e.ToString());
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
                    var didValue = Did1.Value;
                    var fidValue = Fid1.Value;
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

                        P1Freq.Content = (mult1V + 4) * 100;
                        Mult1.SelectedIndex = (int)mult1V;
                    }
                    catch (Exception ex)
                    {
                        await LogHelper.TraceIt_TraceError(ex.ToString());
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
            await LogHelper.TraceIt_TraceError(e.ToString());
        }
    }

    private async void Mult_1_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (_waitforload == false)
            {
                await Task.Delay(20);
                var didValue = Did1.Value;
                if (Did1.Text != "" || Did1.Text != null)
                {
                    _waitforload = true;
                    var fidValue = (Mult1.SelectedIndex + 4) * didValue / 2;
                    _relay = true;
                    Fid1.Value = fidValue;
                    await Task.Delay(40);
                    Fid1.Value = fidValue;
                    P1Freq.Content = (Mult1.SelectedIndex + 4) * 100;
                    Save_ID1();
                    _waitforload = false;
                }
            }
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception.ToString());
        }
    }

    private async void DID_1_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        try
        {
            if (_waitforload == false)
            {
                await Task.Delay(20);
                var didValue = Did1.Value;
                var fidValue = Fid1.Value;
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

                P1Freq.Content = (mult1V + 4) * 100;
                try
                {
                    Mult1.SelectedIndex = (int)mult1V;
                }
                catch (Exception ex)
                {
                    await LogHelper.TraceIt_TraceError(ex.ToString());
                }

                Save_ID1();
            }
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception.ToString());
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
            var didValue = Did2.Value;
            if (Did2.Text != "" || Did2.Text != null)
            {
                _waitforload = true;
                var fidValue = (Mult2.SelectedIndex + 4) * didValue / 2;
                _relay = true;
                Fid2.Value = fidValue;
                await Task.Delay(40);
                Fid2.Value = fidValue;
                P2Freq.Content = (Mult2.SelectedIndex + 4) * 100;
                Save_ID2();
                _waitforload = false;
            }
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception.ToString());
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
                    var didValue = Did2.Value;
                    var fidValue = Fid2.Value;
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

                        P2Freq.Content = (mult2V + 4) * 100;
                        Mult2.SelectedIndex = (int)mult2V;
                    }
                    catch (Exception ex)
                    {
                        await LogHelper.TraceIt_TraceError(ex.ToString());
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
            await LogHelper.TraceIt_TraceError(exception.ToString());
        }
    }

    private async void DID_2_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        try
        {
            if (_waitforload == false)
            {
                await Task.Delay(40);
                var didValue = Did2.Value;
                var fidValue = Fid2.Value;
                var mult2V = fidValue / didValue * 2;
                mult2V -= 4;
                if (mult2V <= 0)
                {
                    mult2V = 0;
                }

                P2Freq.Content = (mult2V + 4) * 100;
                try
                {
                    Mult2.SelectedIndex = (int)mult2V;
                }
                catch (Exception ex)
                {
                    await LogHelper.TraceIt_TraceError(ex.ToString());
                }

                Save_ID2();
            }
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception.ToString());
        }
    }

    private void Save_ID0()
    {
        if (_waitforload == false)
        {
            _profile[_indexprofile].Did0 = Did0.Value;
            _profile[_indexprofile].Fid0 = Fid0.Value;
            _profile[_indexprofile].Vid0 = Vid0.Value;
            _profile[_indexprofile].Did1 = Did1.Value;
            _profile[_indexprofile].Fid1 = Fid1.Value;
            _profile[_indexprofile].Vid1 = Vid1.Value;
            _profile[_indexprofile].Did2 = Did2.Value;
            _profile[_indexprofile].Fid2 = Fid2.Value;
            _profile[_indexprofile].Vid2 = Vid2.Value;
            PstatesDid[0] = Did0.Value;
            PstatesFid[0] = Fid0.Value;
            PstatesVid[0] = Vid0.Value;
            ProfileSave();
        }
    }

    private void Save_ID1()
    {
        if (_waitforload == false)
        {
            _profile[_indexprofile].Did0 = Did0.Value;
            _profile[_indexprofile].Fid0 = Fid0.Value;
            _profile[_indexprofile].Vid0 = Vid0.Value;
            _profile[_indexprofile].Did1 = Did1.Value;
            _profile[_indexprofile].Fid1 = Fid1.Value;
            _profile[_indexprofile].Vid1 = Vid1.Value;
            _profile[_indexprofile].Did2 = Did2.Value;
            _profile[_indexprofile].Fid2 = Fid2.Value;
            _profile[_indexprofile].Vid2 = Vid2.Value;
            PstatesDid[1] = Did1.Value;
            PstatesFid[1] = Fid1.Value;
            PstatesVid[1] = Vid1.Value;
            ProfileSave();
        }
    }

    private void Save_ID2()
    {
        if (_waitforload == false)
        {
            _profile[_indexprofile].Did0 = Did0.Value;
            _profile[_indexprofile].Fid0 = Fid0.Value;
            _profile[_indexprofile].Vid0 = Vid0.Value;
            _profile[_indexprofile].Did1 = Did1.Value;
            _profile[_indexprofile].Fid1 = Fid1.Value;
            _profile[_indexprofile].Vid1 = Vid1.Value;
            _profile[_indexprofile].Did2 = Did2.Value;
            _profile[_indexprofile].Fid2 = Fid2.Value;
            _profile[_indexprofile].Vid2 = Vid2.Value;
            PstatesDid[2] = Did0.Value;
            PstatesFid[2] = Fid0.Value;
            PstatesVid[2] = Vid0.Value;
            ProfileSave();
        }
    }

    private void VID_0_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => Save_ID0();
    private void VID_1_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => Save_ID1();
    private void VID_2_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => Save_ID2();

    #endregion

}