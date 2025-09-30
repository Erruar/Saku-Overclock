using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Win32.TaskScheduler;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Services;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.Styles;
using Saku_Overclock.ViewModels;
using Windows.Foundation;
using Windows.Graphics;
using Windows.System;
using Windows.UI.Text;
using Saku_Overclock.JsonContainers;
using ZenStates.Core;
using Action = System.Action;
using Button = Microsoft.UI.Xaml.Controls.Button;
using Task = System.Threading.Tasks.Task;
using WindowActivatedEventArgs = Microsoft.UI.Xaml.WindowActivatedEventArgs;

// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable

namespace Saku_Overclock.Views;

public sealed partial class ShellPage
{
    private const int WhKeyboardLl = 13; // ID хука на клавиатуру
    private const int WmKeydown = 0x0100; // ID события нажатия клавиши
    private const int VkMenu = 0x12; // ID клавиши Alt
    private const int KeyPressed = 0x8000; // ID нажатой клавиши, а не события
    private static IntPtr _hookId = IntPtr.Zero; // ID хука, используется, например, для удаления
    private readonly LowLevelKeyboardProc _proc; // Коллбэк метод (вызывается при срабатывании хука)
    private DispatcherTimer? _dispatcherTimer; // Таймер обновления уведомлений
    private bool _isNotificationPanelShow; // Флаг: Открыта ли панель уведомлений
    private int? _compareList; // Нет новых уведомлений - пока

    private CancellationTokenSource? _applyDebounceCts;
    private readonly Lock _applyDebounceLock = new();
    private string _pendingProfileToApply = string.Empty; // Профиль, который нужно применить
    private bool _isCustomProfile = false; // Флаг типа профиля для применения
    private int _pendingCustomProfileIndex = -1; // Индекс кастомного профиля для применения

    // Состояние для отслеживания позиции при быстром переключении
    private int _virtualCustomProfileIndex = -1; // Виртуальная позиция в кастомных профилях
    private string _virtualPremadeProfile = string.Empty; // Виртуальная позиция в готовых профилях
    private bool _isVirtualStateActive = false; // Флаг активности виртуального состояния

    private static readonly IAppNotificationService
        NotificationsService = App.GetService<IAppNotificationService>(); // Класс с уведомлениями
    private static readonly ISendSmuCommandService SendSmuCommand = App.GetService<ISendSmuCommandService>();
    private static readonly IOcFinderService OcFinder = App.GetService<IOcFinderService>();

    private static readonly IAppSettingsService
        AppSettings = App.GetService<IAppSettingsService>(); // Настройки приложения

    private Profile[] _profile = new Profile[1]; // Класс с профилями параметров разгона пользователя

    private AppWindow MAppWindow
    {
        get;
    }

    private bool _fixedTitleBar; // Флаг фиксированного тайтлбара 
    private readonly IThemeSelectorService _themeSelectorService = App.GetService<IThemeSelectorService>();

    public static string SelectedProfile
    {
        get;
        private set;
    } = "Unknown";

    public ShellViewModel ViewModel // ViewModel, установка нужной модели для UI страницы
    {
        get;
    }

    public ShellPage(ShellViewModel viewModel)
    {
        if (AppSettings.HotkeysEnabled)
        {
            _proc = HookCallbackAsync;
            _hookId = SetHook(_proc); // Хук, который должен срабатывать
        }
        else
        {
            _proc = (_, _, _) => 0;
            _hookId = 0;
        }

        MAppWindow = App.MainWindow.AppWindow; // AppWindow, нужен для тайтлбара приложения
        ViewModel = viewModel; // ViewModel, установка нужной модели для UI страницы
        InitializeComponent(); // Запуск UI
        ViewModel.NavigationService.Frame = NavigationFrame; // Выбранная пользователем страница
        ViewModel.NavigationViewService.Initialize(NavigationViewControl); // Инициализировать выбор страниц

        // A custom title bar is required for full window theme and Mica support.
        // https://docs.microsoft.com/windows/apps/develop/title-bar?tabs=winui3#full-customization
        App.MainWindow.ExtendsContentIntoTitleBar = true;
        App.MainWindow.SetTitleBar(AppTitleBar);
        App.MainWindow.Activated += MainWindow_Activated; // Приложение активировалось, выставить первую страницу
        App.MainWindow.Closed += (_, _) =>
        {
            UnhookWindowsHookEx(_hookId); // Приложение закрылось - убить хуки
        };
    }

    #region JSON and Initialization

    #region App TitleBar Initialization

    #region App TitleBar

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        /*if (AppSettings.AppFirstRun) 
        {
            HideNavBar();
            Icon.Visibility = Visibility.Collapsed;
            RingerNotifGrid.Visibility = Visibility.Collapsed;
        }*/

        TitleBarHelper.UpdateTitleBar(RequestedTheme);
        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.Left, VirtualKeyModifiers.Menu));
        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.GoBack));
        
        StartInfoUpdate();
        GetProfileInit();
        Theme_Loader();
        AutoStartChecker();
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        App.AppTitlebar = VersionNumberIndicator;
        AppTitleBar.Loaded += AppTitleBar_Loaded;
        AppTitleBar.SizeChanged += AppTitleBar_SizeChanged;
    }

    private void AppTitleBar_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            SetRegionsForCustomTitleBar(); //Установить регион взаимодействия
        }
        catch (Exception ex)
        {
            MandarinAddNotification("TraceIt_Error".GetLocalized(), ex.ToString(), InfoBarSeverity.Error);
        }
    }

    private void AppTitleBar_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        try
        {
            App.MainWindow.DispatcherQueue.TryEnqueue(SetRegionsForCustomTitleBar); //Установить регион взаимодействия
        }
        catch (Exception ex)
        {
            MandarinAddNotification("TraceIt_Error".GetLocalized(), ex.ToString(), InfoBarSeverity.Error);
        }
    }

    #endregion

    #region User Profiles

    private void GetProfileInit()
    {
        if (!AppSettings.OldTitleBar)
        {
            var itemz = new ObservableCollection<ComboBoxItem>();
            itemz.Clear();
            var userProfiles = new ComboBoxItem
            {
                Content = new TextBlock
                {
                    Text = "Shell_CustomProfiles".GetLocalized(),
                    Foreground = (Brush)Application.Current.Resources["AccentTextFillColorTertiaryBrush"]
                },
                IsEnabled = false
            };
            itemz.Add(userProfiles);

            ProfileLoad();

            if (_profile == null)
            {
                _profile = new Profile[1];
                _profile[0] = new Profile();
                ProfileSave();
            }

            foreach (var profile in _profile)
            {
                var securedProfile = profile;
                if (profile == null)
                {
                    securedProfile = new Profile();
                    if (_profile.Length > 0)
                    {
                        _profile[0] = securedProfile;
                        ProfileSave();
                    }
                    else
                    {
                        _profile = new Profile[1];
                        _profile[0] = securedProfile;
                        ProfileSave();
                    }
                }
                var comboBoxItem = new ComboBoxItem
                {
                    Content = securedProfile.Profilename,
                    IsEnabled = true
                };
                itemz.Add(comboBoxItem);
            }

            // Добавление второго элемента (с разделителем)
            var separator = new ComboBoxItem
            {
                IsEnabled = false,
                Content = new NavigationViewItemSeparator
                {
                    BorderThickness = new Thickness(1)
                }
            };
            itemz.Add(separator);
            var premadedProfiles = new ComboBoxItem
            {
                Content = new TextBlock
                {
                    Text = "Shell_PremadedProfiles".GetLocalized(),
                    Foreground = (Brush)Application.Current.Resources["AccentTextFillColorTertiaryBrush"]
                },
                IsEnabled = false
            };
            itemz.Add(premadedProfiles);
            itemz.Add(new ComboBoxItem { Content = "Shell_Preset_Min".GetLocalized(), Name = "PremadeSsAMin" });
            itemz.Add(new ComboBoxItem { Content = "Shell_Preset_Eco".GetLocalized(), Name = "PremadeSsAEco" });
            itemz.Add(new ComboBoxItem { Content = "Shell_Preset_Balance".GetLocalized(), Name = "PremadeSsABal" });
            itemz.Add(new ComboBoxItem { Content = "Shell_Preset_Speed".GetLocalized(), Name = "PremadeSsASpd" });
            itemz.Add(new ComboBoxItem { Content = "Shell_Preset_Max".GetLocalized(), Name = "PremadeSsAMax" });
            ViewModel.Items = itemz;
            if (AppSettings.Preset == -1)
            {
                if (AppSettings.PremadeMinActivated)
                {
                    SelectRightPremadedProfileName("PremadeSsAMin");
                }

                if (AppSettings.PremadeEcoActivated)
                {
                    SelectRightPremadedProfileName("PremadeSsAEco");
                }

                if (AppSettings.PremadeBalanceActivated)
                {
                    SelectRightPremadedProfileName("PremadeSsABal");
                }

                if (AppSettings.PremadeSpeedActivated)
                {
                    SelectRightPremadedProfileName("PremadeSsASpd");
                }

                if (AppSettings.PremadeMaxActivated)
                {
                    SelectRightPremadedProfileName("PremadeSsAMax");
                }
            }
            else
            {
                ViewModel.SelectedIndex = AppSettings.Preset + 1;
                foreach (var item in ViewModel.Items)
                {
                    if (_profile.Length > AppSettings.Preset && AppSettings.Preset != -1)
                    {
                        if (item.Content.ToString() == _profile[AppSettings.Preset].Profilename)
                        {
                            ViewModel.SelectedItem = item;
                        }
                    }
                    else
                    {
                        AppSettings.Preset = -1;
                        AppSettings.SaveSettings();
                    }
                    
                }
            }
        }
    }

    private void SelectRightPremadedProfileName(string names)
    {
        foreach (var box in ViewModel.Items)
        {
            if (box?.Name.Contains(names) == true)
            {
                ViewModel.SelectedItem = box;
                return;
            }
        }
    }

    public static void NextPremadeProfile_Activate(string nextProfile)
    {
        var profiles = new[] { "Min", "Eco", "Balance", "Speed", "Max" };
        foreach (var profile in profiles)
        {
            typeof(IAppSettingsService).GetProperty($"Premade{profile}Activated")
                ?.SetValue(AppSettings, profile == nextProfile);
        }
        AppSettings.Preset = -1;
        AppSettings.SaveSettings();
    }

    private static Dictionary<string, string> NextProfiles => new()
    {
        { "Min", "Eco" },
        { "Eco", "Balance" },
        { "Balance", "Speed" },
        { "Speed", "Max" },
        { "Max", "Min" }
    };

    public static Dictionary<string, (string name, string desc, string icon, string settings, string comboName)>
        PremadedProfiles => new()
    {
        {
            "Min",
            ("Shell_Preset_Min", "Preset_Min_OverlayDesc", "\uEBC0",
                OcFinder.CreatePreset(PresetType.Min, AppSettings.PremadeOptimizationLevel == 0 ? OptimizationLevel.Standard : (AppSettings.PremadeOptimizationLevel == 1 ? OptimizationLevel.Standard : OptimizationLevel.Deep)).CommandString,
                "PremadeSsAMin")
        },
        {
            "Eco",
            ("Shell_Preset_Eco", "Preset_Eco_OverlayDesc", "\uEC0A",
                OcFinder.CreatePreset(PresetType.Eco, AppSettings.PremadeOptimizationLevel == 0 ? OptimizationLevel.Standard : (AppSettings.PremadeOptimizationLevel == 1 ? OptimizationLevel.Standard : OptimizationLevel.Deep)).CommandString,
                "PremadeSsAEco")
        },
        {
            "Balance",
            ("Shell_Preset_Balance", "Preset_Balance_OverlayDesc", "\uEC49",
                OcFinder.CreatePreset(PresetType.Balance, AppSettings.PremadeOptimizationLevel == 0 ? OptimizationLevel.Standard :(AppSettings.PremadeOptimizationLevel == 1 ? OptimizationLevel.Standard : OptimizationLevel.Deep)).CommandString,
                "PremadeSsABal")
        },
        {
            "Speed",
            ("Shell_Preset_Speed", "Preset_Speed_OverlayDesc", "\uE945",
                OcFinder.CreatePreset(PresetType.Performance, AppSettings.PremadeOptimizationLevel == 0 ? OptimizationLevel.Standard :(AppSettings.PremadeOptimizationLevel == 1 ? OptimizationLevel.Standard : OptimizationLevel.Deep)).CommandString,
                "PremadeSsASpd")
        },
        {
            "Max",
            ("Shell_Preset_Max", "Preset_Max_OverlayDesc", "\uECAD",
                OcFinder.CreatePreset(PresetType.Max, AppSettings.PremadeOptimizationLevel == 0 ? OptimizationLevel.Standard :(AppSettings.PremadeOptimizationLevel == 1 ? OptimizationLevel.Standard : OptimizationLevel.Deep)).CommandString,
                "PremadeSsAMax")
        }
    };

    // Методы для получения следующего профиля БЕЗ применения настроек
    private (string profileName, string profileKey) GetNextPremadeProfile(out string icon, out string desc)
    {
        icon = "\uE783";
        desc = "Unable to read description";

        var profiles = new[] { "Min", "Eco", "Balance", "Speed", "Max" };

        string currentProfile;

        // Определяем текущую позицию
        if (_isVirtualStateActive && !string.IsNullOrEmpty(_virtualPremadeProfile))
        {
            // Используем виртуальную позицию при быстром переключении
            currentProfile = _virtualPremadeProfile;
        }
        else
        {
            // Определяем реальную текущую позицию
            if (AppSettings.Preset == -1)
            {
                // Активен готовый профиль - ищем какой именно
                currentProfile = profiles.FirstOrDefault(p =>
                                    (bool)typeof(IAppSettingsService).GetProperty($"Premade{p}Activated")
                                        ?.GetValue(AppSettings)!) ?? "Balance";

                // Инициализируем виртуальное состояние
                _virtualPremadeProfile = currentProfile;
                _isVirtualStateActive = true;
            }
            else
            {
                // Был активен кастомный профиль - начинаем с Balance
                currentProfile = "Balance";
                _virtualPremadeProfile = currentProfile;
                _isVirtualStateActive = true;
            }
        }

        // Получаем следующий профиль
        var nextProfile = NextProfiles[currentProfile];

        // Обновляем виртуальную позицию
        _virtualPremadeProfile = nextProfile;

        // Получаем данные профиля для отображения
        var (name, description, iconStr, _, _) = PremadedProfiles[nextProfile];
        desc = description.GetLocalized();
        icon = iconStr;

        return (name.GetLocalized(), nextProfile);
    }

    private (string profileName, int profileIndex) GetNextCustomProfile(out string? icon, out string? desc)
    {
        icon = string.Empty;
        desc = string.Empty;

        try
        {
            ProfileLoad();

            if (_profile == null || _profile.Length == 0)
            {
                MandarinAddNotification("TraceIt_Error".GetLocalized(),
                    "No custom profiles available", InfoBarSeverity.Warning);
                return (string.Empty, -1);
            }

            int nextProfileIndex;

            // Определяем текущую позицию
            if (_isVirtualStateActive && _virtualCustomProfileIndex >= 0)
            {
                // Используем виртуальную позицию при быстром переключении
                nextProfileIndex = (_virtualCustomProfileIndex + 1) % _profile.Length;
            }
            else
            {
                // Определяем реальную текущую позицию
                if (AppSettings.Preset == -1)
                {
                    // Сейчас активен готовый пресет - начинаем с первого кастомного
                    nextProfileIndex = 0;
                    _virtualCustomProfileIndex = -1; // Чтобы следующий был 0
                    _isVirtualStateActive = true;
                }
                else
                {
                    // Уже выбран кастомный профиль
                    nextProfileIndex = (AppSettings.Preset + 1) % _profile.Length;
                    _virtualCustomProfileIndex = AppSettings.Preset;
                    _isVirtualStateActive = true;
                }
            }

            // Обновляем виртуальную позицию
            _virtualCustomProfileIndex = nextProfileIndex;

            // Проверяем корректность индекса и данных профиля
            if (nextProfileIndex >= 0 && nextProfileIndex < _profile.Length &&
                !string.IsNullOrEmpty(_profile[nextProfileIndex].Profilename))
            {
                var profile = _profile[nextProfileIndex];
                icon = profile.Profileicon;
                desc = profile.Profiledesc;
                return (profile.Profilename, nextProfileIndex);
            }
            else
            {
                MandarinAddNotification("TraceIt_Error".GetLocalized(),
                    $"Invalid profile index: {nextProfileIndex}", InfoBarSeverity.Error);
                return (string.Empty, -1);
            }
        }
        catch (Exception ex)
        {
            MandarinAddNotification("TraceIt_Error".GetLocalized(),
                $"Error getting next custom profile: {ex.Message}", InfoBarSeverity.Error);
            return (string.Empty, -1);
        }
    }

    // Методы для применения профилей (вызываются только после задержки)
    private void ApplyPremadeProfile(string profileKey)
    {
        try
        {
            // Активируем профиль
            NextPremadeProfile_Activate(profileKey);
            AppSettings.Preset = -1; // Устанавливаем флаг готового профиля

            // Получаем данные профиля и обновляем настройки
            var (_, _, _, settings, comboName) = PremadedProfiles[profileKey];
            AppSettings.RyzenAdjLine = settings;

            // Обновляем UI
            foreach (var element in ViewModel.Items.OfType<ComboBoxItem>())
            {
                var selectedName = element.Content?.ToString();
                if (element.Name == comboName)
                {
                    SelectedProfile = selectedName!;
                    ViewModel.SelectedItem = element;
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            MandarinAddNotification("TraceIt_Error".GetLocalized(),
                $"Error applying premade profile: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void ApplyCustomProfile(int profileIndex)
    {
        try
        {
            if (_profile == null || profileIndex < 0 || profileIndex >= _profile.Length)
            {
                MandarinAddNotification("TraceIt_Error".GetLocalized(),
                    $"Invalid custom profile index: {profileIndex}", InfoBarSeverity.Error);
                return;
            }

            AppSettings.Preset = profileIndex;
            var profile = _profile[profileIndex];

            // Обновляем UI
            UpdateProfileComboBox(profile.Profilename);
            MandarinSparseUnit();
            SelectedProfile = profile.Profilename;

            AppSettings.SaveSettings();
        }
        catch (Exception ex)
        {
            MandarinAddNotification("TraceIt_Error".GetLocalized(),
                $"Error applying custom profile: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void UpdateProfileComboBox(string profileName)
    {
        try
        {
            foreach (var element in ViewModel.Items.OfType<ComboBoxItem>())
            {
                var selectedName = element.Content?.ToString();
                if (!string.IsNullOrEmpty(selectedName) && selectedName == profileName)
                {
                    SelectedProfile = selectedName;
                    ViewModel.SelectedItem = element;
                    return;
                }
            }

            MandarinAddNotification("TraceIt_Error".GetLocalized(),
                $"Profile '{profileName}' not found in ComboBox", InfoBarSeverity.Warning);
        }
        catch (Exception ex)
        {
            MandarinAddNotification("TraceIt_Error".GetLocalized(),
                $"Error updating ComboBox: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void MandarinSparseUnit()
    {
        var element = ViewModel.SelectedItem;
        //Required index
        if (!element!.Name.Contains("PremadeSsA"))
        {
            var indexRequired = ViewModel.SelectedIndex - 1;
            AppSettings.Preset = ViewModel.SelectedIndex - 1;
            AppSettings.SaveSettings();
            ProfileLoad();
            MandarinSparseUnitProfile(_profile[indexRequired]);
            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                var navigationService = App.GetService<INavigationService>();
                if (navigationService.Frame!.GetPageViewModel() is ПараметрыViewModel)
                {
                    navigationService.NavigateTo(typeof(ГлавнаяViewModel).FullName!, null, true);
                    navigationService.NavigateTo(typeof(ПараметрыViewModel).FullName!, null, true);
                }
            });
        }
        else
        {
            var optimizationLevel = AppSettings.PremadeOptimizationLevel == 0 ? OptimizationLevel.Standard : (AppSettings.PremadeOptimizationLevel == 1 ? OptimizationLevel.Standard : OptimizationLevel.Deep);
            if (element.Name.Contains("Min"))
            {
                AppSettings.PremadeMinActivated = true;
                AppSettings.PremadeEcoActivated = false;
                AppSettings.PremadeBalanceActivated = false;
                AppSettings.PremadeSpeedActivated = false;
                AppSettings.PremadeMaxActivated = false;
                AppSettings.Preset = -1;
                AppSettings.RyzenAdjLine = OcFinder.CreatePreset(PresetType.Min, optimizationLevel).CommandString;
                MainWindow.Applyer.Apply(AppSettings.RyzenAdjLine, false, AppSettings.ReapplyOverclock,
                    AppSettings.ReapplyOverclockTimer);
            }

            if (element.Name.Contains("Eco"))
            {
                AppSettings.PremadeMinActivated = false;
                AppSettings.PremadeEcoActivated = true;
                AppSettings.PremadeBalanceActivated = false;
                AppSettings.PremadeSpeedActivated = false;
                AppSettings.PremadeMaxActivated = false;
                AppSettings.Preset = -1;
                AppSettings.RyzenAdjLine = OcFinder.CreatePreset(PresetType.Eco, optimizationLevel).CommandString;
                MainWindow.Applyer.Apply(AppSettings.RyzenAdjLine, false, AppSettings.ReapplyOverclock,
                    AppSettings.ReapplyOverclockTimer);
            }

            if (element.Name.Contains("Bal"))
            {
                AppSettings.PremadeMinActivated = false;
                AppSettings.PremadeEcoActivated = false;
                AppSettings.PremadeBalanceActivated = true;
                AppSettings.PremadeSpeedActivated = false;
                AppSettings.PremadeMaxActivated = false;
                AppSettings.Preset = -1;
                AppSettings.RyzenAdjLine = OcFinder.CreatePreset(PresetType.Balance, optimizationLevel).CommandString;
                MainWindow.Applyer.Apply(AppSettings.RyzenAdjLine, false, AppSettings.ReapplyOverclock,
                    AppSettings.ReapplyOverclockTimer);
            }

            if (element.Name.Contains("Spd"))
            {
                AppSettings.PremadeMinActivated = false;
                AppSettings.PremadeEcoActivated = false;
                AppSettings.PremadeBalanceActivated = false;
                AppSettings.PremadeSpeedActivated = true;
                AppSettings.PremadeMaxActivated = false;
                AppSettings.Preset = -1;
                AppSettings.RyzenAdjLine = OcFinder.CreatePreset(PresetType.Performance, optimizationLevel).CommandString;
                MainWindow.Applyer.Apply(AppSettings.RyzenAdjLine, false, AppSettings.ReapplyOverclock,
                    AppSettings.ReapplyOverclockTimer);
            }

            if (element.Name.Contains("Max"))
            {
                AppSettings.PremadeMinActivated = false;
                AppSettings.PremadeEcoActivated = false;
                AppSettings.PremadeBalanceActivated = false;
                AppSettings.PremadeSpeedActivated = false;
                AppSettings.PremadeMaxActivated = true;
                AppSettings.Preset = -1;
                AppSettings.RyzenAdjLine = OcFinder.CreatePreset(PresetType.Max, optimizationLevel).CommandString;
                MainWindow.Applyer.Apply(AppSettings.RyzenAdjLine, false, AppSettings.ReapplyOverclock,
                    AppSettings.ReapplyOverclockTimer);
            }

            AppSettings.SaveSettings();
            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                var navigationService = App.GetService<INavigationService>();
                if (navigationService.Frame!.GetPageViewModel() is ПресетыViewModel)
                {
                    navigationService.NavigateTo(typeof(ГлавнаяViewModel).FullName!, null, true);
                    navigationService.NavigateTo(typeof(ПресетыViewModel).FullName!, null, true);
                }
                else if (navigationService.Frame!.GetPageViewModel() is ПараметрыViewModel)
                {
                    navigationService.NavigateTo(typeof(ГлавнаяViewModel).FullName!, null, true);
                    navigationService.NavigateTo(typeof(ПараметрыViewModel).FullName!, null, true);
                }
            });
        }
    }

    public static void MandarinSparseUnitProfile(Profile profile, bool saveInfo = false)
    {
        var isBristol = CpuSingleton.GetInstance()?.info.codeName == Cpu.CodeName.BristolRidge;

        var adjline = "";
        if (profile.Cpu1)
        {
            adjline += " --tctl-temp=" + profile.Cpu1Value + (isBristol ? "000" : string.Empty);
        }

        if (profile.Cpu2)
        {
            var stapmBoostMillisecondsBristol = profile.Cpu5Value * 1000 < 180000 ? profile.Cpu5Value * 1000 : 180000;
            adjline += " --stapm-limit=" + profile.Cpu2Value + "000" + (isBristol ? ",2," + stapmBoostMillisecondsBristol : string.Empty);
        }

        if (profile.Cpu3)
        {
            adjline += " --fast-limit=" + profile.Cpu3Value + "000";
        }

        if (profile.Cpu4)
        {
            adjline += " --slow-limit=" + profile.Cpu4Value + "000" + (isBristol ? "," + profile.Cpu4Value + "000,0" : string.Empty);
        }

        if (profile.Cpu5)
        {
            adjline += " --stapm-time=" + profile.Cpu5Value;
        }

        if (profile.Cpu6)
        {
            adjline += " --slow-time=" + profile.Cpu6Value;
        }

        if (profile.Cpu7)
        {
            adjline += " --cHTC-temp=" + profile.Cpu7Value;
        }

        //vrm
        if (profile.Vrm1)
        {
            adjline += " --vrmmax-current=" + profile.Vrm1Value + "000" + (isBristol ? "," + profile.Vrm3Value + "000," + profile.Vrm3Value + "000" : string.Empty);
        }

        if (profile.Vrm2)
        {
            adjline += " --vrm-current=" + profile.Vrm2Value + "000" + (isBristol ? "," + profile.Vrm4Value + "000," + profile.Vrm4Value + "000" : string.Empty);
        }

        if (profile.Vrm3 && !isBristol)
        {
            adjline += " --vrmsocmax-current=" + profile.Vrm3Value + "000";
        }

        if (profile.Vrm4 && !isBristol)
        {
            adjline += " --vrmsoc-current=" + profile.Vrm4Value + "000";
        }

        if (profile.Vrm5)
        {
            adjline += " --psi0-current=" + profile.Vrm5Value + "000" + (isBristol ? "," + profile.Vrm6Value + "000," + profile.Vrm6Value + "000" : string.Empty); ;
        }

        if (profile.Vrm6 && !isBristol)
        {
            adjline += " --psi0soc-current=" + profile.Vrm6Value + "000";
        }

        if (profile.Vrm7)
        {
            var prochotDeassertionTimeMillisecondsBristol = profile.Vrm7Value < 100 ? profile.Vrm7Value : 100;
            adjline += " --prochot-deassertion-ramp=" + (isBristol ? prochotDeassertionTimeMillisecondsBristol : profile.Vrm7Value);
        }


        //gpu
        if (profile.Gpu1)
        {
            adjline += " --min-socclk-frequency=" + profile.Gpu1Value;
        }

        if (profile.Gpu2)
        {
            adjline += " --max-socclk-frequency=" + profile.Gpu2Value;
        }

        if (profile.Gpu3)
        {
            adjline += " --min-fclk-frequency=" + profile.Gpu3Value;
        }

        if (profile.Gpu4)
        {
            adjline += " --max-fclk-frequency=" + profile.Gpu4Value;
        }

        if (profile.Gpu5)
        {
            adjline += " --min-vcn=" + profile.Gpu5Value;
        }

        if (profile.Gpu6)
        {
            adjline += " --max-vcn=" + profile.Gpu6Value;
        }

        if (profile.Gpu7)
        {
            adjline += " --min-lclk=" + profile.Gpu7Value;
        }

        if (profile.Gpu8)
        {
            adjline += " --max-lclk=" + profile.Gpu8Value;
        }

        if (profile.Gpu9)
        {
            adjline += " --min-gfxclk=" + profile.Gpu9Value;
        }

        if (profile.Gpu10)
        {
            adjline += " --max-gfxclk=" + profile.Gpu10Value;
        }

        if (profile.Gpu11)
        {
            adjline += " --min-cpuclk=" + profile.Gpu11Value;
        }

        if (profile.Gpu12)
        {
            adjline += " --max-cpuclk=" + profile.Gpu12Value;
        }

        if (profile.Gpu16)
        {
            if (profile.Gpu16Value != 0)
            {
                adjline += " --setcpu-freqto-ramstate=" + (profile.Gpu16Value - 1);
            }
            else
            {
                adjline += " --stopcpu-freqto-ramstate=0";
            }
        }

        //advanced
        if (profile.Advncd1)
        {
            adjline += " --vrmgfx-current=" + profile.Advncd1Value + "000";
        }

        if (profile.Advncd3)
        {
            adjline += " --vrmgfxmax_current=" + profile.Advncd3Value + "000";
        }

        if (profile.Advncd4)
        {
            adjline += " --psi3cpu_current=" + profile.Advncd4Value + "000";
        }

        if (profile.Advncd5)
        {
            adjline += " --psi3gfx_current=" + profile.Advncd5Value + "000";
        }

        if (profile.Advncd6)
        {
            adjline += " --apu-skin-temp=" + profile.Advncd6Value * 256;
        }

        if (profile.Advncd7)
        {
            adjline += " --dgpu-skin-temp=" + profile.Advncd7Value * 256;
        }

        if (profile.Advncd8)
        {
            adjline += " --apu-slow-limit=" + profile.Advncd8Value + "000";
        }

        if (profile.Advncd9)
        {
            adjline += " --skin-temp-limit=" + profile.Advncd9Value + "000";
        }

        if (profile.Advncd10)
        {
            adjline += " --gfx-clk=" + profile.Advncd10Value;
        }

        if (profile.Advncd11)
        {
            adjline += " --oc-clk=" + profile.Advncd11Value;
        }

        if (profile.Advncd12)
        {
            adjline += " --oc-volt=" + Math.Round((1.55 - profile.Advncd12Value / 1000) / 0.00625);
        }


        if (profile.Advncd13)
        {
            if (profile.Advncd13Value == 1)
            {
                adjline += " --max-performance=1";
            }

            if (profile.Advncd13Value == 2)
            {
                adjline += " --power-saving=1";
            }
        }

        if (profile.Advncd14)
        {
            switch (profile.Advncd14Value)
            {
                case 0:
                    adjline += " --disable-oc=1";
                    break;
                case 1:
                    adjline += " --enable-oc=1";
                    break;
            }
        }

        if (profile.Advncd15)
        {
            adjline += " --pbo-scalar=" + profile.Advncd15Value * 100;
        }

        if (profile.Coall)
        {
            if (profile.Coallvalue >= 0.0)
            {
                adjline += $" --set-coall={profile.Coallvalue} ";
            }
            else
            {
                adjline += $" --set-coall={Convert.ToUInt32(0x100000 - (uint)(-1 * (int)profile.Coallvalue))} ";
            }
        }

        var cpu = CpuSingleton.GetInstance();
        if (profile.Cogfx)
        {
            cpu.smu.Rsmu.SMU_MSG_SetDldoPsmMargin = SendSmuCommand.ReturnCoGfx(cpu.info.codeName, false);
            cpu.smu.Mp1Smu.SMU_MSG_SetDldoPsmMargin = SendSmuCommand.ReturnCoGfx(cpu.info.codeName, true);
            //Using Irusanov method
            for (var i = 0; i < cpu.info.topology.physicalCores; i++)
            {
                var mapIndex = i < 8 ? 0 : 1;
                if (((~cpu.info.topology.coreDisableMap[mapIndex] >> i) & 1) == 1)
                {
                    if (cpu.smu.Rsmu.SMU_MSG_SetDldoPsmMargin != 0U)
                    {
                        cpu.SetPsmMarginSingleCore(GetCoreMask(cpu, i), Convert.ToInt32(profile.Cogfxvalue));
                    }
                }
            }

            cpu.smu.Rsmu.SMU_MSG_SetDldoPsmMargin = SendSmuCommand.ReturnCoPer(cpu.info.codeName, false);
            cpu.smu.Mp1Smu.SMU_MSG_SetDldoPsmMargin = SendSmuCommand.ReturnCoPer(cpu.info.codeName, true);
        }

        if (profile.Comode && profile.Coprefmode != 0) // Если пользователь выбрал хотя-бы один режим и ...
        {
            switch (profile.Coprefmode)
            {
                // Если выбран режим ноутбук
                // Так как там как у компьютеров
                case 1 when cpu.info.codeName == Cpu.CodeName.DragonRange:
                    {
                        if (profile.Coper0)
                        {
                            adjline += $" --set-coper={0 | ((int)profile.Coper0Value & 0xFFFF)} ";
                        }

                        if (profile.Coper1)
                        {
                            adjline += $" --set-coper={1048576 | ((int)profile.Coper1Value & 0xFFFF)} ";
                        }

                        if (profile.Coper2)
                        {
                            adjline += $" --set-coper={2097152 | ((int)profile.Coper2Value & 0xFFFF)} ";
                        }

                        if (profile.Coper3)
                        {
                            adjline += $" --set-coper={3145728 | ((int)profile.Coper3Value & 0xFFFF)} ";
                        }

                        if (profile.Coper4)
                        {
                            adjline += $" --set-coper={4194304 | ((int)profile.Coper4Value & 0xFFFF)} ";
                        }

                        if (profile.Coper5)
                        {
                            adjline += $" --set-coper={5242880 | ((int)profile.Coper5Value & 0xFFFF)} ";
                        }

                        if (profile.Coper6)
                        {
                            adjline += $" --set-coper={6291456 | ((int)profile.Coper6Value & 0xFFFF)} ";
                        }

                        if (profile.Coper7)
                        {
                            adjline += $" --set-coper={7340032 | ((int)profile.Coper7Value & 0xFFFF)} ";
                        }

                        if (profile.Coper8)
                        {
                            adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((0 % 8) & 15)) << 20) | ((int)profile.Coper8Value & 0xFFFF)} ";
                        }

                        if (profile.Coper9)
                        {
                            adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((1 % 8) & 15)) << 20) | ((int)profile.Coper9Value & 0xFFFF)} ";
                        }

                        if (profile.Coper10)
                        {
                            adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((2 % 8) & 15)) << 20) | ((int)profile.Coper10Value & 0xFFFF)} ";
                        }

                        if (profile.Coper11)
                        {
                            adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((3 % 8) & 15)) << 20) | ((int)profile.Coper11Value & 0xFFFF)} ";
                        }

                        if (profile.Coper12)
                        {
                            adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((4 % 8) & 15)) << 20) | ((int)profile.Coper12Value & 0xFFFF)} ";
                        }

                        if (profile.Coper13)
                        {
                            adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((5 % 8) & 15)) << 20) | ((int)profile.Coper13Value & 0xFFFF)} ";
                        }

                        if (profile.Coper14)
                        {
                            adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((6 % 8) & 15)) << 20) | ((int)profile.Coper14Value & 0xFFFF)} ";
                        }

                        if (profile.Coper15)
                        {
                            adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((7 % 8) & 15)) << 20) | ((int)profile.Coper15Value & 0xFFFF)} ";
                        }

                        break;
                    }
                case 1:
                    {
                        if (profile.Coper0)
                        {
                            adjline += $" --set-coper={0 | ((int)profile.Coper0Value & 0xFFFF)} ";
                        }

                        if (profile.Coper1)
                        {
                            adjline += $" --set-coper={(1 << 20) | ((int)profile.Coper1Value & 0xFFFF)} ";
                        }

                        if (profile.Coper2)
                        {
                            adjline += $" --set-coper={(2 << 20) | ((int)profile.Coper2Value & 0xFFFF)} ";
                        }

                        if (profile.Coper3)
                        {
                            adjline += $" --set-coper={(3 << 20) | ((int)profile.Coper3Value & 0xFFFF)} ";
                        }

                        if (profile.Coper4)
                        {
                            adjline += $" --set-coper={(4 << 20) | ((int)profile.Coper4Value & 0xFFFF)} ";
                        }

                        if (profile.Coper5)
                        {
                            adjline += $" --set-coper={(5 << 20) | ((int)profile.Coper5Value & 0xFFFF)} ";
                        }

                        if (profile.Coper6)
                        {
                            adjline += $" --set-coper={(6 << 20) | ((int)profile.Coper6Value & 0xFFFF)} ";
                        }

                        if (profile.Coper7)
                        {
                            adjline += $" --set-coper={(7 << 20) | ((int)profile.Coper7Value & 0xFFFF)} ";
                        }

                        break;
                    }
                //Если выбран режим компьютер
                case 2:
                    {
                        if (profile.Coper0)
                        {
                            adjline += $" --set-coper={0 | ((int)profile.Coper0Value & 0xFFFF)} ";
                        }

                        if (profile.Coper1)
                        {
                            adjline += $" --set-coper={1048576 | ((int)profile.Coper1Value & 0xFFFF)} ";
                        }

                        if (profile.Coper2)
                        {
                            adjline += $" --set-coper={2097152 | ((int)profile.Coper2Value & 0xFFFF)} ";
                        }

                        if (profile.Coper3)
                        {
                            adjline += $" --set-coper={3145728 | ((int)profile.Coper3Value & 0xFFFF)} ";
                        }

                        if (profile.Coper4)
                        {
                            adjline += $" --set-coper={4194304 | ((int)profile.Coper4Value & 0xFFFF)} ";
                        }

                        if (profile.Coper5)
                        {
                            adjline += $" --set-coper={5242880 | ((int)profile.Coper5Value & 0xFFFF)} ";
                        }

                        if (profile.Coper6)
                        {
                            adjline += $" --set-coper={6291456 | ((int)profile.Coper6Value & 0xFFFF)} ";
                        }

                        if (profile.Coper7)
                        {
                            adjline += $" --set-coper={7340032 | ((int)profile.Coper7Value & 0xFFFF)} ";
                        }

                        if (profile.Coper8)
                        {
                            adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((0 % 8) & 15)) << 20) | ((int)profile.Coper8Value & 0xFFFF)} ";
                        }

                        if (profile.Coper9)
                        {
                            adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((1 % 8) & 15)) << 20) | ((int)profile.Coper9Value & 0xFFFF)} ";
                        }

                        if (profile.Coper10)
                        {
                            adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((2 % 8) & 15)) << 20) | ((int)profile.Coper10Value & 0xFFFF)} ";
                        }

                        if (profile.Coper11)
                        {
                            adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((3 % 8) & 15)) << 20) | ((int)profile.Coper11Value & 0xFFFF)} ";
                        }

                        if (profile.Coper12)
                        {
                            adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((4 % 8) & 15)) << 20) | ((int)profile.Coper12Value & 0xFFFF)} ";
                        }

                        if (profile.Coper13)
                        {
                            adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((5 % 8) & 15)) << 20) | ((int)profile.Coper13Value & 0xFFFF)} ";
                        }

                        if (profile.Coper14)
                        {
                            adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((6 % 8) & 15)) << 20) | ((int)profile.Coper14Value & 0xFFFF)} ";
                        }

                        if (profile.Coper15)
                        {
                            adjline +=
                                $" --set-coper={(((((1 << 4) | ((0 % 1) & 15)) << 4) | ((7 % 8) & 15)) << 20) | ((int)profile.Coper15Value & 0xFFFF)} ";
                        }

                        break;
                    }
                // Если выбран режим с использованием метода от Ирусанова, Irusanov, https://github.com/irusanov
                case 3:
                    {
                        cpu.smu.Rsmu.SMU_MSG_SetDldoPsmMargin = SendSmuCommand.ReturnCoPer(cpu.info.codeName, false);
                        cpu.smu.Mp1Smu.SMU_MSG_SetDldoPsmMargin = SendSmuCommand.ReturnCoPer(cpu.info.codeName, true);
                        var options = new Dictionary<int, double>
                    {
                        { 0, profile.Coper0Value },
                        { 1, profile.Coper1Value },
                        { 2, profile.Coper2Value },
                        { 3, profile.Coper3Value },
                        { 4, profile.Coper4Value },
                        { 5, profile.Coper5Value },
                        { 6, profile.Coper6Value },
                        { 7, profile.Coper7Value },
                        { 8, profile.Coper8Value },
                        { 9, profile.Coper9Value },
                        { 10, profile.Coper10Value },
                        { 11, profile.Coper11Value },
                        { 12, profile.Coper12Value },
                        { 13, profile.Coper13Value },
                        { 14, profile.Coper14Value },
                        { 15, profile.Coper15Value }
                    };
                        var checks = new Dictionary<int, bool>
                    {
                        { 0, profile.Coper0 },
                        { 1, profile.Coper1 },
                        { 2, profile.Coper2 },
                        { 3, profile.Coper3 },
                        { 4, profile.Coper4 },
                        { 5, profile.Coper5 },
                        { 6, profile.Coper6 },
                        { 7, profile.Coper7 },
                        { 8, profile.Coper8 },
                        { 9, profile.Coper9 },
                        { 10, profile.Coper10 },
                        { 11, profile.Coper11 },
                        { 12, profile.Coper12 },
                        { 13, profile.Coper13 },
                        { 14, profile.Coper14 },
                        { 15, profile.Coper15 }
                    };
                        for (var i = 0; i < cpu.info.topology.physicalCores; i++)
                        {
                            var checkbox = i < 16 && checks[i];
                            if (checkbox)
                            {
                                var setVal = options[i];
                                var mapIndex = i < 8 ? 0 : 1;
                                if (((~cpu.info.topology.coreDisableMap[mapIndex] >> i) & 1) == 1) // Если ядро включено
                                {
                                    if (cpu.smu.Rsmu.SMU_MSG_SetDldoPsmMargin != 0U) // Если команда существует
                                    {
                                        cpu.SetPsmMarginSingleCore(GetCoreMask(cpu, i), Convert.ToInt32(setVal));
                                    }
                                }
                            }
                        }

                        break;
                    }
            }
        }

        if (profile.SmuFunctionsEnabl)
        {
            if (profile.SmuFeatureCclk)
            {
                adjline += " --enable-feature=1";
            }
            else
            {
                adjline += " --disable-feature=1";
            }

            if (profile.SmuFeatureData)
            {
                adjline += " --enable-feature=4";
            }
            else
            {
                adjline += " --disable-feature=4";
            }

            if (profile.SmuFeaturePpt)
            {
                adjline += " --enable-feature=8";
            }
            else
            {
                adjline += " --disable-feature=8";
            }

            if (profile.SmuFeatureTdc)
            {
                adjline += " --enable-feature=16";
            }
            else
            {
                adjline += " --disable-feature=16";
            }

            if (profile.SmuFeatureThermal)
            {
                adjline += " --enable-feature=32";
            }
            else
            {
                adjline += " --disable-feature=32";
            }

            if (profile.SmuFeaturePowerDown)
            {
                adjline += " --enable-feature=256";
            }
            else
            {
                adjline += " --disable-feature=256";
            }

            if (profile.SmuFeatureProchot)
            {
                adjline += " --enable-feature=0,32";
            }
            else
            {
                adjline += " --disable-feature=0,32";
            }

            if (profile.SmuFeatureStapm)
            {
                adjline += " --enable-feature=0,128";
            }
            else
            {
                adjline += " --disable-feature=0,128";
            }

            if (profile.SmuFeatureCStates)
            {
                adjline += " --enable-feature=0,256";
            }
            else
            {
                adjline += " --disable-feature=0,256";
            }

            if (profile.SmuFeatureGfxDutyCycle)
            {
                adjline += " --enable-feature=0,512";
            }
            else
            {
                adjline += " --disable-feature=0,512";
            }

            if (profile.SmuFeatureAplusA)
            {
                adjline += " --enable-feature=0,1024";
            }
            else
            {
                adjline += " --disable-feature=0,1024";
            }
        }

        AppSettings.RyzenAdjLine = adjline + " ";
        AppSettings.SaveSettings();
        MainWindow.Applyer.Apply(AppSettings.RyzenAdjLine, saveInfo, AppSettings.ReapplyOverclock,
            AppSettings.ReapplyOverclockTimer);
    }

    private static uint GetCoreMask(Cpu cpu, int coreIndex)
    {
        Task.Run(async () => await LogHelper.Log("Getting Core Mask..."));
        var ccxInCcd = cpu.info.family >= Cpu.Family.FAMILY_19H ? 1U : 2U;
        var coresInCcx = 8 / ccxInCcd;

        var ccd = Convert.ToUInt32(coreIndex / 8);
        var ccx = Convert.ToUInt32(coreIndex / coresInCcx - ccxInCcd * ccd);
        var core = Convert.ToUInt32(coreIndex % coresInCcx);
        var coreMask = cpu.MakeCoreMask(core, ccd, ccx);
        Task.Run(async () =>
            await LogHelper.Log(
                $"Core Mask detected: {coreMask}\nCCD: {ccd}\nCCX: {ccx}\nCore: {core}\nCCX in Index: {ccxInCcd}"));
        return coreMask;
    }

    public static void AutoStartChecker()
    {
        var taskService = new TaskService();
        const string taskName = "Saku Overclock";
        var pathToExecutableFile = Assembly.GetExecutingAssembly().Location;
        var pathToProgramDirectory = Path.GetDirectoryName(pathToExecutableFile);
        var pathToStartupLnk = Path.Combine(pathToProgramDirectory!, "Saku Overclock.exe");

        if (AppSettings.AutostartType == 2 || AppSettings.AutostartType == 3)
        {
            // Проверяем существование задачи
            var existingTask = taskService.GetTask(taskName);
            if (existingTask != null)
            {
                // Проверяем корректность пути к исполняемому файлу
                if (existingTask.Definition.Actions[0] is ExecAction execAction && execAction.Path.Equals(pathToStartupLnk, StringComparison.OrdinalIgnoreCase))
                {
                    // Задача существует и путь корректен, ничего не делаем
                    return;
                }
                else
                {
                    // Путь некорректен, удаляем задачу
                    taskService.RootFolder.DeleteTask(taskName);
                }
            }

            // Создаём новую задачу
            var taskDefinition = taskService.NewTask();
            taskDefinition.RegistrationInfo.Description = "An awesome ryzen laptop overclock utility for those who want real performance! Autostart Saku Overclock application task";
            taskDefinition.RegistrationInfo.Author = "Sakura Serzhik";
            taskDefinition.RegistrationInfo.Version = new Version("1.0.0");
            taskDefinition.Principal.RunLevel = TaskRunLevel.Highest;
            taskDefinition.Triggers.Add(new LogonTrigger { Enabled = true });
            taskDefinition.Actions.Add(new ExecAction(pathToStartupLnk));

            taskService.RootFolder.RegisterTaskDefinition(taskName, taskDefinition);
        }
        else
        {
            // Если задача существует, удаляем её
            if (taskService.GetTask(taskName) != null)
            {
                taskService.RootFolder.DeleteTask(taskName);
            }
        }
    }

    #endregion

    #region Notifications

    #region Window Definitions

    private void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.CodeActivated ||
            args.WindowActivationState == WindowActivationState.PointerActivated)
        {
            // Окно активировано
            _dispatcherTimer?.Start();
        }
        else
        {
            // Окно не активировано
            _dispatcherTimer?.Stop();
        }
    }

    private void Window_VisibilityChanged(object sender, WindowVisibilityChangedEventArgs args)
    {
        if (args.Visible)
        {
            _dispatcherTimer?.Start();
        }
        else
        {
            _dispatcherTimer?.Stop();
        }
    }

    #endregion

    #region Info Update Timers
    private void StartInfoUpdate()
    {
        _dispatcherTimer = new DispatcherTimer();
        _dispatcherTimer.Tick += async (_, _) => await GetNotify();
        _dispatcherTimer.Interval = TimeSpan.FromMilliseconds(1000);
        App.MainWindow.VisibilityChanged += Window_VisibilityChanged;
        App.MainWindow.Activated += Window_Activated;
        _dispatcherTimer.Start();
    }

    private void StopInfoUpdate() => _dispatcherTimer?.Stop();

    #endregion

    #region Info Update Timer Stop When Page Navigated

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        StartInfoUpdate();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        StopInfoUpdate();
    }

    #endregion

    #region Notification Update Voids

    private Task GetNotify()
    {
        if (ViewModel.SelectedIndex != -1 && ((ComboBoxItem?)ViewModel.SelectedItem)?.Content != null)
        {
            if (SelectedProfile != ((ComboBoxItem?)ViewModel.SelectedItem)?.Content.ToString())
            {
                SelectedProfile = ViewModel.SelectedItem?.Content.ToString()!;
            }
        }

        if (_isNotificationPanelShow)
        {
            return Task.CompletedTask;
        }

        if (NotificationsService.Notifies == null)
        {
            return Task.CompletedTask;
        }

        DispatcherQueue?.TryEnqueue(() =>
        {
            var contains = false;
            if (_compareList == NotificationsService.Notifies.Count && NotificationContainer.Children.Count != 0)
            {
                return;
            } //нет новых уведомлений - пока

            ClearAllNotification(null, null);
            var index = 0;
            foreach (var notify1 in NotificationsService.Notifies!)
            {
                Grid? subcontent = null;
                switch (notify1.Title)
                {
                    //Если уведомление о изменении темы
                    case "Theme applied!":
                        Theme_Loader();
                        ClearAllNotification(NotificationPanelClearAllBtn, null); //Удалить все уведомления
                        return; //Удалить и не показывать 
                    case "UpdateNAVBAR":
                        HideNavBar();
                        Icon.Visibility = Visibility.Collapsed;
                        RingerNotificationGrid.Visibility = Visibility.Collapsed;
                        return; //Удалить и не показывать 
                    case "FirstLaunch":
                        HideNavBar();
                        Icon.Visibility = Visibility.Collapsed;
                        RingerNotificationGrid.Visibility = Visibility.Collapsed;
                        ClearAllNotification(NotificationPanelClearAllBtn, null); //Удалить все уведомления
                        return;
                    case "ExitFirstLaunch":
                        ShowNavBar();
                        Icon.Visibility = Visibility.Visible;
                        RingerNotificationGrid.Visibility = Visibility.Visible;
                        ClearAllNotification(NotificationPanelClearAllBtn, null); //Удалить все уведомления
                        return;
                    case "UPDATE_REQUIRED":
                        notify1.Title = "Shell_Update_App_Title".GetLocalized();
                        notify1.Msg = "Shell_Update_App_Message".GetLocalized() + " " +
                                      UpdateChecker.ParseVersion(UpdateChecker.GetNewVersion()?.TagName ?? "Null-null-0.0.0.0");
                        var updateButton = new Button
                        {
                            CornerRadius = new CornerRadius(15),
                            Content = new Grid
                            {
                                Children =
                                {
                                    new FontIcon
                                    {
                                        Glyph = "\uE777",
                                        HorizontalAlignment = HorizontalAlignment.Left
                                    },
                                    new TextBlock
                                    {
                                        Margin = new Thickness(30, 0, 0, 0),
                                        Text = "Shell_Update_App_Button".GetLocalized(),
                                        HorizontalAlignment = HorizontalAlignment.Center
                                    }
                                }
                            }
                        };
                        updateButton.Click += (_, _) =>
                        {
                            HideNavBar();
                            Icon.Visibility = Visibility.Collapsed;
                            RingerNotificationGrid.Visibility = Visibility.Collapsed;
                            var navigationService = App.GetService<INavigationService>();
                            navigationService.NavigateTo(typeof(ОбновлениеViewModel).FullName!, null, true);
                            ClearAllNotification(NotificationPanelClearAllBtn, null); //Удалить все уведомления 
                        };
                        subcontent = new Grid
                        {
                            Children = { updateButton }
                        };
                        break;
                    case "Profile_APPLIED":
                        contains = true;
                        ClearAllNotification(NotificationPanelClearAllBtn, null); //Удалить все уведомления
                        break;
                }

                if (notify1.Msg.Contains("DELETEUNAVAILABLE"))
                {
                    if (notify1.Type != InfoBarSeverity.Success)
                    {
                        contains = true;
                        var but1 = new Button
                        {
                            CornerRadius = new CornerRadius(15),
                            Content = new Grid
                            {
                                Children =
                            {
                                new FontIcon
                                {
                                    Glyph = "\uE71E",
                                    HorizontalAlignment = HorizontalAlignment.Left
                                },
                                new TextBlock
                                {
                                    Margin = new Thickness(30, 0, 0, 0),
                                    Text = "Shell_Notify_AboutErrors".GetLocalized(),
                                    HorizontalAlignment = HorizontalAlignment.Center
                                }
                            }
                            }
                        };
                        var but2 = new Button
                        {
                            Tag = notify1.Msg.Replace("DELETEUNAVAILABLE", ""),
                            CornerRadius = new CornerRadius(15),
                            Margin = new Thickness(10, 0, 0, 0),
                            Content = new Grid
                            {
                                Children =
                            {
                                new FontIcon
                                {
                                    Glyph = "\uE71C",
                                    HorizontalAlignment = HorizontalAlignment.Left
                                },
                                new TextBlock
                                {
                                    Margin = new Thickness(30, 0, 0, 0),
                                    Text = "Shell_Notify_DisableErrors".GetLocalized(),
                                    HorizontalAlignment = HorizontalAlignment.Center
                                }
                            }
                            }
                        };
                        but1.Click += (_, _) =>
                        {
                            Process.Start(
                                new ProcessStartInfo("https://github.com/Erruar/Saku-Overclock/wiki/FAQ#error-handling")
                                {
                                    UseShellExecute = true
                                });
                        };
                        but2.Click += async (_, _) =>
                        {
                            but2.IsEnabled = false;
                            var navigationService = App.GetService<INavigationService>();
                            if (navigationService.Frame!.GetPageViewModel() is ПараметрыViewModel)
                            {
                                navigationService.NavigateTo(typeof(ГлавнаяViewModel).FullName!, null, true);
                            }

                            MandarinAddNotification("Shell_Notify_TaskCompleting".GetLocalized(),
                                "Shell_Notify_TaskWait".GetLocalized(),
                                InfoBarSeverity.Informational,
                                true,
                                new Grid
                                {
                                    HorizontalAlignment = HorizontalAlignment.Left,
                                    VerticalAlignment = VerticalAlignment.Top,
                                    Children =
                                    {
                                    new ProgressRing
                                    {
                                        IsActive = true
                                    }
                                    }
                                });
                            var stringFrom = but2.Tag.ToString()?.Split('\"');
                            if (stringFrom != null)
                            {
                                ProfileLoad();
                                var commandActions = new Dictionary<string, Action>
                            {
                                {
                                    "Param_SMU_Func_Text/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].SmuFunctionsEnabl = false
                                },
                                {
                                    "Param_CPU_c2/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Cpu2 = false
                                },
                                {
                                    "Param_VRM_v2/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Vrm2 = false
                                },
                                {
                                    "Param_VRM_v1/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Vrm1 = false
                                },
                                {
                                    "Param_CPU_c1/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Cpu1 = false
                                },
                                {
                                    "Param_ADV_a15/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Advncd15 = false
                                },
                                {
                                    "Param_ADV_a11/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Advncd11 = false
                                },
                                {
                                    "Param_ADV_a12/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Advncd12 = false
                                },
                                {
                                    "Param_CO_O1/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Coall = false
                                },
                                {
                                    "Param_CO_O2/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Cogfx = false
                                },
                                {
                                    "Param_CCD1_CO_Section/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Coprefmode = 0
                                },
                                {
                                    "Param_ADV_a14_E/Content".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Advncd14 = false
                                },
                                {
                                    "Param_CPU_c5/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Cpu5 = false
                                },
                                {
                                    "Param_CPU_c3/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Cpu3 = false
                                },
                                {
                                    "Param_CPU_c4/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Cpu4 = false
                                },
                                {
                                    "Param_CPU_c6/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Cpu6 = false
                                },
                                {
                                    "Param_CPU_c7/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Cpu7 = false
                                },
                                {
                                    "Param_ADV_a6/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Advncd6 = false
                                },
                                {
                                    "Param_VRM_v4/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Vrm4 = false
                                },
                                {
                                    "Param_VRM_v3/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Vrm3 = false
                                },
                                {
                                    "Param_ADV_a1/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Advncd1 = false
                                },
                                {
                                    "Param_ADV_a3/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Advncd3 = false
                                },
                                {
                                    "Param_VRM_v7/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Vrm7 = false
                                },
                                {
                                    "Param_ADV_a4/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Advncd4 = false
                                },
                                {
                                    "Param_ADV_a5/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Advncd5 = false
                                },
                                {
                                    "Param_ADV_a10/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Advncd10 = false
                                },
                                {
                                    "Param_ADV_a13_E/Content".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Advncd13 = false
                                },
                                {
                                    "Param_ADV_a13_U/Content".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Advncd13 = false
                                },
                                {
                                    "Param_ADV_a8/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Advncd8 = false
                                },
                                {
                                    "Param_ADV_a7/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Advncd7 = false
                                },
                                {
                                    "Param_VRM_v5/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Vrm5 = false
                                },
                                {
                                    "Param_VRM_v6/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Vrm6 = false
                                },
                                {
                                    "Param_ADV_a9/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Advncd9 = false
                                },
                                {
                                    "Param_GPU_g12/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Gpu12 = false
                                },
                                {
                                    "Param_GPU_g11/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Gpu11 = false
                                },
                                {
                                    "Param_GPU_g10/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Gpu10 = false
                                },
                                {
                                    "Param_GPU_g9/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Gpu9 = false
                                },
                                {
                                    "Param_GPU_g2/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Gpu2 = false
                                },
                                {
                                    "Param_GPU_g1/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Gpu1 = false
                                },
                                {
                                    "Param_GPU_g4/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Gpu4 = false
                                },
                                {
                                    "Param_GPU_g3/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Gpu3 = false
                                },
                                {
                                    "Param_GPU_g6/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Gpu6 = false
                                },
                                {
                                    "Param_GPU_g5/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Gpu5 = false
                                },
                                {
                                    "Param_GPU_g8/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Gpu8 = false
                                },
                                {
                                    "Param_GPU_g7/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Gpu7 = false
                                },
                                {
                                    "Param_GPU_g15/Text".GetLocalized(),
                                    () =>
                                    {
                                    }
                                },
                                {
                                    "Param_GPU_g16/Text".GetLocalized(),
                                    () => _profile[AppSettings.Preset].Gpu16 = false
                                }
                            };
                                var loggingList = string.Empty;
                                var logFilePath = Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                                                  @"\SakuOverclock\FixedFailingCommandsLog.txt";
                                var sw = new StreamWriter(logFilePath, true);
                                if (!File.Exists(logFilePath))
                                {
                                    await sw.WriteLineAsync(@"//------Fixed Failing Commands Log------\\");
                                }

                                foreach (var currPos in stringFrom)
                                {
                                    if (commandActions.TryGetValue(currPos, out var value))
                                    {
                                        value.Invoke(); // Выполнение действия
                                        await sw.WriteLineAsync("\n" + currPos);
                                        loggingList += (loggingList == string.Empty ? "" : "\n") + currPos;
                                    }
                                }

                                ProfileSave();
                                ClearAllNotification(NotificationPanelClearAllBtn, null); //Удалить все уведомления
                                await Task.Delay(2000);
                                but2.IsEnabled = true;
                                await sw.WriteLineAsync(@"//------OK------\\");
                                sw.Close();

                                if (navigationService.Frame!.GetPageViewModel() is not ГлавнаяViewModel)
                                {
                                    navigationService.NavigateTo(typeof(ГлавнаяViewModel).FullName!, null, false);
                                }
                                else
                                {
                                    navigationService.NavigateTo(typeof(ПресетыViewModel).FullName!, null, false);
                                }

                                var butLogs = new Button
                                {
                                    CornerRadius = new CornerRadius(15),
                                    Content = new Grid
                                    {
                                        Children =
                                    {
                                        new FontIcon
                                        {
                                            Glyph = "\uE82D",
                                            HorizontalAlignment = HorizontalAlignment.Left
                                        },
                                        new TextBlock
                                        {
                                            Margin = new Thickness(30, 0, 0, 0),
                                            Text = "Shell_Notify_ErrorsShowLog".GetLocalized(),
                                            HorizontalAlignment = HorizontalAlignment.Center
                                        }
                                    }
                                    }
                                };
                                var butSavedLogs = new Button
                                {
                                    Margin = new Thickness(0, 20, 0, 0),
                                    CornerRadius = new CornerRadius(15),
                                    Content = new Grid
                                    {
                                        Children =
                                    {
                                        new FontIcon
                                        {
                                            Glyph = "\uE838",
                                            HorizontalAlignment = HorizontalAlignment.Left
                                        },
                                        new TextBlock
                                        {
                                            Margin = new Thickness(30, 0, 0, 0),
                                            Text = "Shell_Notify_ErrorsShowLogExplorer".GetLocalized(),
                                            HorizontalAlignment = HorizontalAlignment.Center
                                        }
                                    }
                                    }
                                };
                                butSavedLogs.Click += (_, _) =>
                                {
                                    if (File.Exists(logFilePath))
                                    {
                                        var filePath = Path.GetFullPath(logFilePath);
                                        Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                                    }
                                };
                                butLogs.Click += (_, _) =>
                                {
                                    App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                                    {
                                        var flyout1 = new Flyout
                                        {
                                            Content = new StackPanel
                                            {
                                                Orientation = Orientation.Vertical,
                                                Children =
                                            {
                                                new ScrollViewer
                                                {
                                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                                    VerticalAlignment = VerticalAlignment.Stretch,
                                                    Content = new StackPanel
                                                    {
                                                        Orientation = Orientation.Vertical,
                                                        Children =
                                                        {
                                                            new TextBlock
                                                            {
                                                                Text = "Shell_Notify_ErrorLog".GetLocalized(),
                                                                FontWeight = new FontWeight(600),
                                                                FontSize = 21
                                                            },
                                                            new TextBlock
                                                            {
                                                                HorizontalAlignment = HorizontalAlignment.Stretch,
                                                                Text = loggingList
                                                            }
                                                        }
                                                    }
                                                },
                                                butSavedLogs
                                            }
                                            }
                                        };
                                        flyout1.ShowAt(butLogs);
                                    });
                                };
                                MandarinAddNotification("Shell_Notify_ErrorsDisabled".GetLocalized(),
                                    "Shell_Notify_ErrorsDisabledDesc".GetLocalized(),
                                    InfoBarSeverity.Success,
                                    true,
                                    new Grid
                                    {
                                        Children =
                                        {
                                        new StackPanel
                                        {
                                            Orientation = Orientation.Horizontal,
                                            Children =
                                            {
                                                butLogs
                                            }
                                        }
                                        }
                                    });
                            }
                        };
                        subcontent = new Grid
                        {
                            Children =
                        {
                            new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                Children =
                                {
                                    but1,
                                    but2
                                }
                            }
                        }
                        };
                    }

                    notify1.Msg = notify1.Msg.Replace("DELETEUNAVAILABLE", "");
                }

                MandarinAddNotification(notify1.Title, notify1.Msg, notify1.Type, Notify.IsClosable, subcontent);
                if (notify1.Title.Contains("SaveSuccessTitle".GetLocalized()) ||
                    notify1.Title.Contains("DeleteSuccessTitle".GetLocalized()) ||
                    notify1.Title.Contains("Edit_TargetTitle".GetLocalized()))
                {
                    contains = true;
                }

                if (SettingsViewModel.VersionId != 5 &&
                    index > 8) //Если 9 уведомлений - очистить для оптимизации производительности
                {
                    ClearAllNotification(NotificationPanelClearAllBtn, null); //Удалить все уведомления
                    return;
                }

                index++;
            }

            if (contains)
            {
                GetProfileInit();
            }

            _compareList = NotificationsService.Notifies.Count;
        });
        return Task.CompletedTask;
    }

    private void HideNavBar() // Скрыть навигационную панель
    {
        NavigationViewControl.Margin = new Thickness(-49, -48, 0, 0);
        NavigationViewControl.IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed;
        NavigationViewControl.IsSettingsVisible = false;
        NavigationViewControl.IsPaneOpen = false;
        foreach (var element in NavigationViewControl.MenuItems)
        {
            ((NavigationViewItem)element).Visibility = Visibility.Collapsed;
        }

        ClearAllNotification(NotificationPanelClearAllBtn, null); //Удалить все уведомления
    }

    private void ShowNavBar() // Показать навигационную панель
    {
        NavigationViewControl.Margin = new Thickness(0, 0, 0, 0);
        NavigationViewControl.IsBackButtonVisible = NavigationViewBackButtonVisible.Visible;
        NavigationViewControl.IsSettingsVisible = true;
        foreach (var element in NavigationViewControl.MenuItems)
        {
            ((NavigationViewItem)element).Visibility = Visibility.Visible;
        }

        ClearAllNotification(NotificationPanelClearAllBtn, null); //Удалить все уведомления
    }

    #endregion

    #endregion

    #endregion

    #region JSON Initialization

    private void Theme_Loader()
    {
        try
        {
            var themeMobil = App.GetService<SettingsViewModel>();
            if (AppSettings.ThemeType == -1)
            {
                AppSettings.ThemeType = 0;
                AppSettings.SaveSettings();
            }

            var themeLight = _themeSelectorService.Themes[AppSettings.ThemeType].ThemeLight
                ? ElementTheme.Light
                : ElementTheme.Dark;
            themeMobil.SwitchThemeCommand.Execute(themeLight);
            var themeBackground = _themeSelectorService.Themes[AppSettings.ThemeType].ThemeBackground;

            if (AppSettings.ThemeType > 2 &&
                !string.IsNullOrEmpty(themeBackground) &&
                (themeBackground.Contains("http") || themeBackground.Contains("appx") || File.Exists(themeBackground)))
            {
                ThemeBackground.ImageSource = new BitmapImage(new Uri(themeBackground));
            }

            ThemeOpacity.Opacity = _themeSelectorService.Themes[AppSettings.ThemeType].ThemeOpacity;
            ThemeMaskOpacity.Opacity = _themeSelectorService.Themes[AppSettings.ThemeType].ThemeMaskOpacity;
            var backupWidth = TitleIcon.Width;
            TitleIcon.Width = 0;
            TitleIcon.Width = backupWidth;
        }
        catch
        {
            MandarinAddNotification(
                "ThemeError".GetLocalized() + "\"" + (AppSettings.ThemeType < _themeSelectorService.Themes.Count
                    ? _themeSelectorService.Themes[AppSettings.ThemeType].ThemeName
                    : $"Error in theme type = {AppSettings.ThemeType}") + "\"",
                "ThemeNotFoundBg".GetLocalized(),
                InfoBarSeverity.Error
            );
        }
    }

    private void ProfileSave()
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
            MandarinAddNotification("TraceIt_Error".GetLocalized(), ex.ToString(), InfoBarSeverity.Error);
        }
    }

    private void ProfileLoad()
    {
        var filePath = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\profile.json";
        if (File.Exists(filePath))
        {
            try
            {
                _profile = JsonConvert.DeserializeObject<Profile[]>(File.ReadAllText(filePath))!;
            }
            catch
            {
                JsonRepair();
            }
        }
        else
        {
            JsonRepair();
        }
    }

    private void JsonRepair()
    {

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
        }


    }

    #endregion

    #endregion

    #region Keyboard Hooks

    private static IntPtr SetHook(LowLevelKeyboardProc proc) => SetWindowsHookEx(WhKeyboardLl, proc,
            GetModuleHandle(Process.GetCurrentProcess().MainModule!.ModuleName), 0);

    private static bool IsAltPressed() => (GetKeyState(VkMenu) & KeyPressed) != 0;

    private delegate IntPtr
        LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private nint HookCallbackAsync(int nCode, IntPtr wParam, IntPtr lParam)
    {
        CallNextHookEx(_hookId, nCode, wParam, lParam); // Передаем нажатие в следующее приложение
        
        if (nCode >= 0 && wParam == WmKeydown && GetAsyncKeyState(0x11) < 0 &&
                IsAltPressed()) // Проверяем следует ли перехватывать хук и событие нажатия на клавишу Control (0x11), Alt
        {
            HandleKeyboardKeysCallback(Marshal.ReadInt32(lParam));
        }

        return 0x0;
    }

    private Task HandleKeyboardKeysCallback(int virtualkeyCode)
    {
        switch ((VirtualKey)virtualkeyCode)
        {
            // Переключить между своими пресетами
            case VirtualKey.W:
                {
                    (var nextCustomProfile, var nextCustomIndex) = GetNextCustomProfile(out var icon1, out var desc1);

                    if (!string.IsNullOrEmpty(nextCustomProfile))
                    {
                        // Сохраняем информацию о профиле для отложенного применения 
                        lock (_applyDebounceLock)
                        {
                            _pendingProfileToApply = nextCustomProfile;
                            _isCustomProfile = true;
                            _pendingCustomProfileIndex = nextCustomIndex;
                        }

                        ProfileSwitcher.ProfileSwitcher.ShowOverlay(_themeSelectorService, AppSettings,
                                nextCustomProfile, icon1, desc1);
                        ScheduleApplyProfile();

                        MandarinAddNotification("Shell_ProfileChanging".GetLocalized(),
                            "Shell_ProfileChanging_Custom".GetLocalized() + $"{nextCustomProfile}!",
                            InfoBarSeverity.Informational);
                    }
                    break;
                }
            // Переключить между готовыми пресетами
            case VirtualKey.P:
                (var nextPremadeProfile, var nextPremadeKey) = GetNextPremadeProfile(out var icon, out var desc);

                if (!string.IsNullOrEmpty(nextPremadeProfile))
                {
                    // Сохраняем информацию о профиле для отложенного применения 
                    lock (_applyDebounceLock)
                    {
                        _pendingProfileToApply = nextPremadeKey; 
                        _isCustomProfile = false;
                    }

                    ProfileSwitcher.ProfileSwitcher.ShowOverlay(_themeSelectorService, AppSettings,
                            nextPremadeProfile, icon, desc);

                    ScheduleApplyProfile();

                    MandarinAddNotification("Shell_ProfileChanging".GetLocalized(),
                        "Shell_ProfileChanging_Premade".GetLocalized() + $"{nextPremadeProfile}!",
                        InfoBarSeverity.Informational);
                }
                break;
            // Переключить состояние RTSS
            case VirtualKey.R:
                if (AppSettings.RtssMetricsEnabled)
                {

                    ProfileSwitcher.ProfileSwitcher.ShowOverlay(_themeSelectorService, AppSettings,
                        "RTSS " + "Cooler_Service_Disabled/Content".GetLocalized(), "\uE7AC");

                    AppSettings.RtssMetricsEnabled = false;
                }
                else
                {
                    ProfileSwitcher.ProfileSwitcher.ShowOverlay(_themeSelectorService, AppSettings,
                        "RTSS " + "Cooler_Service_Enabled/Content".GetLocalized(), "\uE7AC");

                    AppSettings.RtssMetricsEnabled = true;
                }

                AppSettings.SaveSettings();

                MandarinAddNotification("Shell_RTSSChanging".GetLocalized(),
                    "Shell_RTSSChanging_Success".GetLocalized(), InfoBarSeverity.Informational);
                break;
        }
        return Task.CompletedTask;
    }
    private void ScheduleApplyProfile()
    {
        lock (_applyDebounceLock)
        {
            // Отменяем предыдущий таймер
            _applyDebounceCts?.Cancel();
            _applyDebounceCts = new CancellationTokenSource();
            var token = _applyDebounceCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(1500), token);

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            // ТОЛЬКО ЗДЕСЬ обновляем настройки и применяем профиль
                            string profileToApply;
                            bool isCustom;
                            int customIndex;

                            lock (_applyDebounceLock)
                            {
                                profileToApply = _pendingProfileToApply;
                                isCustom = _isCustomProfile;
                                customIndex = _pendingCustomProfileIndex;

                                // Сбрасываем виртуальное состояние после применения
                                _isVirtualStateActive = false;
                                _virtualCustomProfileIndex = -1;
                                _virtualPremadeProfile = string.Empty;
                            }

                            if (!string.IsNullOrEmpty(profileToApply))
                            {
                                if (isCustom)
                                {
                                    ApplyCustomProfile(customIndex);
                                }
                                else
                                {
                                    ApplyPremadeProfile(profileToApply);
                                }

                                MainWindow.Applyer.ApplyWithoutAdjLine(false);
                            }
                        }
                        catch (Exception ex)
                        {
                            MandarinAddNotification("TraceIt_Error".GetLocalized(),
                                $"Error applying profile: {ex.Message}", InfoBarSeverity.Error);
                        }
                    });
                }
                catch (TaskCanceledException)
                {
                    // Таймер был отменен из-за нового нажатия
                }
                catch (Exception ex)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        MandarinAddNotification("TraceIt_Error".GetLocalized(),
                            $"Error in profile scheduling: {ex.Message}", InfoBarSeverity.Error);
                    });
                }
            });
        }
    }

    #region Hook DLL Imports

    // Импорт необходимых функций из WinApi
    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook,
        LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern short GetAsyncKeyState(int vKey);


    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);


    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
        IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    #endregion

    #endregion

    #region Based on Collapse Launcher TitleBar

    private void
        ToggleTitleIcon(
            bool hide) // Based on Collapse Launcher Titlebar, check out -> https://github.com/CollapseLauncher/Collapse
    {
        if (!hide)
        {
            IconTitle.Width = double.NaN;
            IconTitle.Opacity = 1d;
            IconImg.Opacity = 1d;
            return;
        }

        IconTitle.Width = 0.0;
        IconTitle.Opacity = 0d;
        IconImg.Opacity = 0.8d;
    }

    private void TitleIcon_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (!NavigationViewControl.IsPaneOpen && !_fixedTitleBar)
        {
            //показать
            var curMargin = Icon.Margin;
            curMargin.Left = -1;
            Icon.Margin = curMargin;
            ToggleTitleIcon(false);
        }
    }

    private void TitleIcon_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        //скрыть
        if (!NavigationViewControl.IsPaneOpen && !_fixedTitleBar)
        {
            var curMargin = Icon.Margin;
            curMargin.Left = 3;
            Icon.Margin = curMargin;
            ToggleTitleIcon(true);
        }
    }

    private void Icon_Click(object sender, RoutedEventArgs e) => _fixedTitleBar = !_fixedTitleBar;

    private void NavigationViewControl_DisplayModeChanged(NavigationView sender,
        NavigationViewDisplayModeChangedEventArgs args)
    {
        AppTitleBar.Margin = new Thickness
        {
            Left = sender.CompactPaneLength * (sender.DisplayMode == NavigationViewDisplayMode.Minimal ? 2 : 1),
            Top = AppTitleBar.Margin.Top,
            Right = AppTitleBar.Margin.Right,
            Bottom = AppTitleBar.Margin.Bottom
        };
    }

    private static KeyboardAccelerator BuildKeyboardAccelerator(VirtualKey key, VirtualKeyModifiers? modifiers = null)
    {
        var keyboardAccelerator = new KeyboardAccelerator { Key = key };

        if (modifiers.HasValue)
        {
            keyboardAccelerator.Modifiers = modifiers.Value;
        }

        keyboardAccelerator.Invoked += OnKeyboardAcceleratorInvoked;

        return keyboardAccelerator;
    }

    private static void OnKeyboardAcceleratorInvoked(KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        var navigationService = App.GetService<INavigationService>();

        var result = navigationService.GoBack();

        args.Handled = result;
    }

    private void NavigationViewControl_PaneOpened(NavigationView sender, object args)
    {
        IconColumn.Width = new GridLength(170, GridUnitType.Pixel);
        var curMargin = Icon.Margin;
        curMargin.Left = 50;
        Icon.Margin = curMargin;
        ToggleTitleIcon(false);
    }

    private void NavigationViewControl_PaneClosed(NavigationView sender, object args)
    {
        if (!NavigationViewControl.IsPaneOpen && !_fixedTitleBar)
        {
            var curMargin = Icon.Margin;
            curMargin.Left = 3;
            Icon.Margin = curMargin;
            ToggleTitleIcon(true);
            IconColumn.Width = new GridLength(120, GridUnitType.Pixel);
        }
    }

    private void SetRegionsForCustomTitleBar()
    {
        var scaleAdjustment =
            AppTitleBar.XamlRoot.RasterizationScale; // Specify the interactive regions of the title bar.
        RightPaddingColumn.Width = new GridLength(MAppWindow.TitleBar.RightInset / scaleAdjustment);
        LeftPaddingColumn.Width = new GridLength(MAppWindow.TitleBar.LeftInset / scaleAdjustment);

        var transform = TitleIcon.TransformToVisual(null);
        var bounds = transform.TransformBounds(new Rect(0, 0,
            TitleIcon.ActualWidth,
            TitleIcon.ActualHeight));
        var searchBoxRect = GetRect(bounds, scaleAdjustment);

        transform = RingerNotificationGrid.TransformToVisual(null);
        bounds = transform.TransformBounds(new Rect(0, 0,
            RingerNotificationGrid.ActualWidth,
            RingerNotificationGrid.ActualHeight));
        var ringerNotifRect = GetRect(bounds, scaleAdjustment);

        var rectArray = new[] { searchBoxRect, ringerNotifRect };

        var nonClientInputSrc = InputNonClientPointerSource.GetForWindowId(MAppWindow.Id);
        nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, rectArray);
    }

    private static RectInt32 GetRect(Rect bounds, double scale)
    {
        return new RectInt32(
            (int)Math.Round(bounds.X * scale),
            (int)Math.Round(bounds.Y * scale),
            (int)Math.Round(bounds.Width * scale),
            (int)Math.Round(bounds.Height * scale)
        );
    }

    #endregion

    #region Event Handlers


    private void ToggleNotificationPanelBtn_Click(object sender, RoutedEventArgs e)
    {
        _isNotificationPanelShow = ToggleNotificationPanelBtn.IsChecked ?? false;
        ShowHideNotificationPanel(true);
    }

    private void NotificationContainerBackground_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isNotificationPanelShow = false;
        ToggleNotificationPanelBtn.IsChecked = false;
        ShowHideNotificationPanel(true);
    }

    private void CloseThisClickHandler(InfoBar sender, object args)
    {
        var container = new Grid { Tag = sender.Name };
        sender.IsOpen = false;
        var list = NotificationsService.Notifies!;
        for (var i = 0; i < list.Count; i++)
        {
            var notify1 = list[i];
            if (sender.Title == notify1.Title && sender.Message == notify1.Msg && sender.Severity == notify1.Type)
            {
                NotificationsService.Notifies?.RemoveAt(i);
                NotificationsService.SaveNotificationsSettings();
                return;
            }
        }

        sender.Height = 0;
        sender.Margin = new Thickness(0, 0, 0, 0);
        NoNotificationIndicator.Opacity = NotificationContainer.Children.Count > 0 ? 0f : 1f;
        if (NewNotificationCountBadge.Value > 0)
        {
            NewNotificationCountBadge.Value--;
        }

        NoNotificationIndicator.Opacity = NotificationContainer.Children.Count > 0 ? 0f : 1f;
        NewNotificationCountBadge.Visibility =
            NewNotificationCountBadge.Value > 0 ? Visibility.Visible : Visibility.Collapsed;
        NotificationPanelClearAllGrid.Visibility =
            NotificationContainer.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        NotificationContainer.Children.Remove(container);
    }

    private async void ClearAllNotification(object? sender, RoutedEventArgs? args)
    {
        try
        {
            var button = sender as Button;
            if (button != null)
            {
                button.IsEnabled = false;
            }

            var stackIndex = 0;
            for (; stackIndex < NotificationContainer.Children.Count;)
            {
                if (NotificationContainer.Children[stackIndex] is not Grid container
                    || container.Children == null || container.Children.Count == 0
                    || container.Children[0] is not InfoBar { IsClosable: true } notifBar)
                {
                    ++stackIndex;
                    continue;
                }

                NotificationContainer.Children.RemoveAt(stackIndex);
                notifBar.IsOpen = false;
                await Task.Delay(100);
            }

            if (NotificationContainer.Children.Count == 0 && sender != null)
            {
                await Task.Delay(500);
                ToggleNotificationPanelBtn.IsChecked = false;
                _isNotificationPanelShow = false;
                ShowHideNotificationPanel(false);
            }

            if (button != null)
            {
                button.IsEnabled = true;
                NotificationsService.Notifies = [];
                NotificationsService.SaveNotificationsSettings();
            }
        }
        catch (Exception ex)
        {
            MandarinAddNotification("TraceIt_Error".GetLocalized(), ex.ToString(), InfoBarSeverity.Error);
        }
    }

    private async void Notif_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (NotificationsPivot.SelectedIndex == 0)
            {
                NotificationContainer.Visibility = Visibility.Visible;
                if (NotificationContainer.Children.Count == 0)
                {
                    NoNotificationIndicator.Visibility = Visibility.Visible;
                }
            }
            else
            {
                NotificationContainer.Visibility = Visibility.Collapsed;
                NoNotificationIndicator.Visibility = Visibility.Collapsed;
                if (UpdateChecker.GitHubInfoString == string.Empty)
                {
                    await UpdateChecker.GenerateReleaseInfoString();
                }

                if (UpdateChecker.GitHubInfoString != "**Failed to fetch info**")
                {
                    NotificationChangelogTexts.Children.Clear();
                    await ГлавнаяPage.GenerateFormattedReleaseNotes(NotificationChangelogTexts);
                }
            }
        }
        catch (Exception ex)
        {
            MandarinAddNotification("TraceIt_Error".GetLocalized(), ex.ToString(), InfoBarSeverity.Error);
        }
    }

    #endregion

    #region Based on Collapse Launcher Notification voids

    /*
    This code region belongs to the Collapse Launcher project. Its owners are neon-nyan, bagusnl.


    License:


    MIT License

Copyright (c) neon-nyan

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.


    This file contains code from:
    https://github.com/CollapseLauncher/Collapse/blob/main/CollapseLauncher/XAMLs/MainApp/MainPage.xaml.cs
    */
    private void ShowHideNotificationPanel(bool hider)
    {
        if (!hider)
        {
            return;
        }

        var lastMargin = NotificationPanel.Margin;
        lastMargin.Right = _isNotificationPanelShow ? 0 : NotificationPanel.ActualWidth * -1;
        NotificationPanel.Margin = lastMargin;
        NewNotificationCountBadge.Value = 0;
        NewNotificationCountBadge.Visibility = Visibility.Collapsed;
        ShowHideNotificationLostFocusBackground(_isNotificationPanelShow);
    }

    private async void ShowHideNotificationLostFocusBackground(bool show)
    {
        try
        {
            if (show)
            {
                NotificationLostFocusBackground.Visibility = Visibility.Visible;
                NotificationLostFocusBackground.Opacity = 0.3;
            }
            else
            {
                if (NotificationContainer.Children.Count > 7)
                {
                    ClearAllNotification(NotificationPanelClearAllBtn, null); //Удалить все, затем добавить один
                }

                NotificationLostFocusBackground.Opacity = 0;
                await Task.Delay(200);
                NotificationLostFocusBackground.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            MandarinAddNotification("TraceIt_Error".GetLocalized(), ex.ToString(), InfoBarSeverity.Error);
        }
    }

    private void MandarinAddNotification(string title, string msg, InfoBarSeverity type, bool isClosable = true,
        Grid? subcontent = null, TypedEventHandler<InfoBar, object>? closeClickHandler = null)
    {
        DispatcherQueue?.TryEnqueue(() =>
        {
            var otherContentContainer = new StackPanel { Margin = new Thickness(0, -4, 0, 8) };
            var notification = new InfoBar
            {
                Title = title,
                Message = msg,
                Severity = type,
                IsClosable = isClosable,
                IsIconVisible = true,
                Shadow = SharedShadow,
                IsOpen = true,
                Margin = new Thickness(0, 4, 4, 0),
                Width = 600,
                CornerRadius = new CornerRadius(8),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            if (subcontent != null)
            {
                otherContentContainer.Children.Add(subcontent);
                notification.Content = otherContentContainer;
            }

            notification.Name = msg;
            notification.CloseButtonClick += closeClickHandler;
            MandarinShowNotify(Name, notification);
            if (closeClickHandler == null)
            {
                notification.CloseButtonClick += CloseThisClickHandler;
            }
        });
    }

    private void MandarinShowNotify(string name, InfoBar notification)
    {
        var container = new Grid { Tag = name };
        notification.Loaded += (_, _) =>
        {
            NoNotificationIndicator.Opacity = NotificationContainer.Children.Count > 0 ? 0f : 1f;
            NewNotificationCountBadge.Visibility = Visibility.Visible;
            NewNotificationCountBadge.Value++;
            NotificationPanelClearAllGrid.Visibility =
                NotificationContainer.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        };

        notification.Closed += (s, _) =>
        {
            s.Height = 0;
            s.Margin = new Thickness(0, 0, 0, 0);
            NoNotificationIndicator.Opacity = NotificationContainer.Children.Count > 0 ? 0f : 1f;
            if (NewNotificationCountBadge.Value > 0)
            {
                NewNotificationCountBadge.Value--;
            }

            NoNotificationIndicator.Opacity = NotificationContainer.Children.Count > 0 ? 0f : 1f;
            NewNotificationCountBadge.Visibility =
                NewNotificationCountBadge.Value > 0 ? Visibility.Visible : Visibility.Collapsed;
            NotificationPanelClearAllGrid.Visibility =
                NotificationContainer.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            NotificationContainer.Children.Remove(container);
        };
        container.Children.Add(notification);
        NotificationContainer.Children.Add(container);
    }

    #endregion
}