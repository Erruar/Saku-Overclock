using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.Graphics;
using Windows.System;
using Windows.UI.Text;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers; 
using Saku_Overclock.SMUEngine;
using Saku_Overclock.ViewModels;
using Button = Microsoft.UI.Xaml.Controls.Button;
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
    private bool _loaded = true; // Запустился ли UI поток приложения
    private bool _isNotificationPanelShow; // Флаг: Открыта ли панель уведомлений
    private int? _compareList; // Нет новых уведомлений - пока
    private JsonContainers.Notifications _notify = new(); // Класс с уведомлениями
    private static readonly IAppSettingsService SettingsService = App.GetService<IAppSettingsService>();
    private Profile[] _profile = new Profile[1]; // Класс с профилями параметров разгона пользователя

    private AppWindow MAppWindow
    {
        get;
    }

    private bool _fixedTitleBar; // Флаг фиксированного тайтлбара
    private Themer _themer = new(); // Класс с темами приложения

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
        
        if (SettingsService.HotkeysEnabled)
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

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        TitleBarHelper.UpdateTitleBar(RequestedTheme);
        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.Left, VirtualKeyModifiers.Menu));
        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.GoBack));
        _loaded = true;
        StartInfoUpdate();
        GetProfileInit();
    }

    #region App TitleBar Initialization

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        App.AppTitlebar = VersionNumberIndicator;
        AppTitleBar.Loaded += AppTitleBar_Loaded;
        AppTitleBar.SizeChanged += AppTitleBar_SizeChanged;
        Theme_Loader(); //Загрузить тему
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

    #region User Profiles

    private void GetProfileInit()
    {
        if (!SettingsService.OldTitleBar)
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
            foreach (var profile in _profile)
            {
                var comboBoxItem = new ComboBoxItem
                {
                    Content = profile.profilename,
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
            if (SettingsService.Preset == -1)
            {
                if (SettingsService.PremadeMinActivated)
                {
                    SelectRightPremadedProfileName("PremadeSsAMin");
                }

                if (SettingsService.PremadeEcoActivated)
                {
                    SelectRightPremadedProfileName("PremadeSsAEco");
                }

                if (SettingsService.PremadeBalanceActivated)
                {
                    SelectRightPremadedProfileName("PremadeSsABal");
                }

                if (SettingsService.PremadeSpeedActivated)
                {
                    SelectRightPremadedProfileName("PremadeSsASpd");
                }

                if (SettingsService.PremadeMaxActivated)
                {
                    SelectRightPremadedProfileName("PremadeSsAMax");
                }
            }
            else
            {
                ViewModel.SelectedIndex = SettingsService.Preset + 1;
                ProfileSetComboBox.SelectedIndex = SettingsService.Preset + 1;
            }

            if (SettingsService.ReapplyLatestSettingsOnAppLaunch)
            {
                ProfileSetButton.IsEnabled = false;
            }
        }
    }

    private void SelectRightPremadedProfileName(string names)
    {
        foreach (var box in ProfileSetComboBox.Items)
        {
            var combobox = box as ComboBoxItem;
            if (combobox?.Name.Contains(names) == true)
            {
                ProfileSetComboBox.SelectedItem = combobox;
                return;
            }
        }
    }

    private string NextPremadeProfile_Switch()
    {
        var nextProfile = string.Empty;
        if (SettingsService.Preset != -1) // У нас был готовый пресет
        {
            if (SettingsService.PremadeMinActivated)
            {
                SettingsService.Preset = -1;
                nextProfile = "Shell_Preset_Min".GetLocalized();
                SettingsService.RyzenADJline =
                    " --tctl-temp=60 --stapm-limit=9000 --fast-limit=9000 --stapm-time=900 --slow-limit=6000 --slow-time=900 --vrm-current=120000 --vrmmax-current=120000 --vrmsoc-current=120000 --vrmsocmax-current=120000 --vrmgfx-current=120000 --prochot-deassertion-ramp=2 ";
                foreach (var element in ProfileSetComboBox.Items)
                {
                    if (element != null && (element as ComboBoxItem)!.Name == "PremadeSsAMin")
                    {
                        ProfileSetComboBox.SelectedItem = (element as ComboBoxItem)!;
                        ProfileSetButton.IsEnabled = false;
                    }
                }
            }
            else if (SettingsService.PremadeEcoActivated)
            {
                SettingsService.Preset = -1;
                nextProfile = "Shell_Preset_Eco".GetLocalized();
                SettingsService.RyzenADJline =
                    " --tctl-temp=68 --stapm-limit=15000  --fast-limit=18000 --stapm-time=500 --slow-limit=16000 --slow-time=500 --vrm-current=120000 --vrmmax-current=120000 --vrmsoc-current=120000 --vrmsocmax-current=120000 --vrmgfx-current=120000 --prochot-deassertion-ramp=2 ";
                foreach (var element in ProfileSetComboBox.Items)
                {
                    if (element != null && (element as ComboBoxItem)!.Name == "PremadeSsAEco")
                    {
                        ProfileSetComboBox.SelectedItem = (element as ComboBoxItem)!;
                        ProfileSetButton.IsEnabled = false;
                    }
                }
            }
            else if (SettingsService.PremadeBalanceActivated)
            {
                SettingsService.Preset = -1;
                nextProfile = "Shell_Preset_Balance".GetLocalized();
                SettingsService.RyzenADJline =
                    " --tctl-temp=75 --stapm-limit=17000  --fast-limit=20000 --stapm-time=64 --slow-limit=19000 --slow-time=128 --vrm-current=120000 --vrmmax-current=120000 --vrmsoc-current=120000 --vrmsocmax-current=120000 --vrmgfx-current=120000 --prochot-deassertion-ramp=2 ";
                foreach (var element in ProfileSetComboBox.Items)
                {
                    if (element != null && (element as ComboBoxItem)!.Name == "PremadeSsABal")
                    {
                        ProfileSetComboBox.SelectedItem = (element as ComboBoxItem)!;
                        ProfileSetButton.IsEnabled = false;
                    }
                }
            }
            else if (SettingsService.PremadeSpeedActivated)
            {
                SettingsService.Preset = -1;
                nextProfile = "Shell_Preset_Speed".GetLocalized();
                SettingsService.RyzenADJline =
                    " --tctl-temp=80 --stapm-limit=20000  --fast-limit=20000 --stapm-time=32 --slow-limit=20000 --slow-time=64 --vrm-current=120000 --vrmmax-current=120000 --vrmsoc-current=120000 --vrmsocmax-current=120000 --vrmgfx-current=120000 --prochot-deassertion-ramp=2 ";
                foreach (var element in ProfileSetComboBox.Items)
                {
                    if (element != null && (element as ComboBoxItem)!.Name == "PremadeSsASpd")
                    {
                        ProfileSetComboBox.SelectedItem = (element as ComboBoxItem)!;
                        ProfileSetButton.IsEnabled = false;
                    }
                }
            }
            else if (SettingsService.PremadeMaxActivated)
            {
                SettingsService.Preset = -1;
                nextProfile = "Shell_Preset_Max".GetLocalized();
                SettingsService.RyzenADJline =
                    " --tctl-temp=90 --stapm-limit=45000  --fast-limit=60000 --stapm-time=80 --slow-limit=60000 --slow-time=1 --vrm-current=120000 --vrmmax-current=120000 --vrmsoc-current=120000 --vrmsocmax-current=120000 --vrmgfx-current=120000 --prochot-deassertion-ramp=2 ";
                foreach (var element in ProfileSetComboBox.Items)
                {
                    if (element != null && (element as ComboBoxItem)!.Name == "PremadeSsAMax")
                    {
                        ProfileSetComboBox.SelectedItem = (element as ComboBoxItem)!;
                        ProfileSetButton.IsEnabled = false;
                    }
                }
            }
        }
        else // У нас уже был выставлен какой-то профиль
        {
            if (SettingsService.PremadeMinActivated)
            {
                SettingsService.Preset = -1;
                nextProfile = "Shell_Preset_Eco".GetLocalized();
                SettingsService.PremadeMinActivated = false;
                SettingsService.PremadeEcoActivated = true;
                SettingsService.PremadeBalanceActivated = false;
                SettingsService.PremadeSpeedActivated = false;
                SettingsService.PremadeMaxActivated = false;
                SettingsService.RyzenADJline =
                    " --tctl-temp=68 --stapm-limit=15000  --fast-limit=18000 --stapm-time=500 --slow-limit=16000 --slow-time=500 --vrm-current=120000 --vrmmax-current=120000 --vrmsoc-current=120000 --vrmsocmax-current=120000 --vrmgfx-current=120000 --prochot-deassertion-ramp=2 ";
                foreach (var element in ProfileSetComboBox.Items)
                {
                    if (element != null && (element as ComboBoxItem)!.Name == "PremadeSsAEco")
                    {
                        ProfileSetComboBox.SelectedItem = (element as ComboBoxItem)!;
                        ProfileSetButton.IsEnabled = false;
                    }
                }
            } // Эко
            else if (SettingsService.PremadeEcoActivated)
            {
                SettingsService.Preset = -1;
                nextProfile = "Shell_Preset_Balance".GetLocalized();
                SettingsService.PremadeMinActivated = false;
                SettingsService.PremadeEcoActivated = false;
                SettingsService.PremadeBalanceActivated = true;
                SettingsService.PremadeSpeedActivated = false;
                SettingsService.PremadeMaxActivated = false;
                SettingsService.RyzenADJline =
                    " --tctl-temp=75 --stapm-limit=17000  --fast-limit=20000 --stapm-time=64 --slow-limit=19000 --slow-time=128 --vrm-current=120000 --vrmmax-current=120000 --vrmsoc-current=120000 --vrmsocmax-current=120000 --vrmgfx-current=120000 --prochot-deassertion-ramp=2 ";
                foreach (var element in ProfileSetComboBox.Items)
                {
                    if (element != null && (element as ComboBoxItem)!.Name == "PremadeSsABal")
                    {
                        ProfileSetComboBox.SelectedItem = (element as ComboBoxItem)!;
                        ProfileSetButton.IsEnabled = false;
                    }
                }
            } // Баланс
            else if (SettingsService.PremadeBalanceActivated)
            {
                SettingsService.Preset = -1;
                nextProfile = "Shell_Preset_Speed".GetLocalized();
                SettingsService.PremadeMinActivated = false;
                SettingsService.PremadeEcoActivated = false;
                SettingsService.PremadeBalanceActivated = false;
                SettingsService.PremadeSpeedActivated = true;
                SettingsService.PremadeMaxActivated = false;
                SettingsService.RyzenADJline =
                    " --tctl-temp=80 --stapm-limit=20000  --fast-limit=20000 --stapm-time=32 --slow-limit=20000 --slow-time=64 --vrm-current=120000 --vrmmax-current=120000 --vrmsoc-current=120000 --vrmsocmax-current=120000 --vrmgfx-current=120000 --prochot-deassertion-ramp=2 ";
                foreach (var element in ProfileSetComboBox.Items)
                {
                    if (element != null && (element as ComboBoxItem)!.Name == "PremadeSsASpd")
                    {
                        ProfileSetComboBox.SelectedItem = (element as ComboBoxItem)!;
                        ProfileSetButton.IsEnabled = false;
                    }
                }
            } // Скорость
            else if (SettingsService.PremadeSpeedActivated)
            {
                SettingsService.Preset = -1;
                nextProfile = "Shell_Preset_Max".GetLocalized();
                SettingsService.PremadeMinActivated = false;
                SettingsService.PremadeEcoActivated = false;
                SettingsService.PremadeBalanceActivated = false;
                SettingsService.PremadeSpeedActivated = false;
                SettingsService.PremadeMaxActivated = true;
                SettingsService.RyzenADJline =
                    " --tctl-temp=90 --stapm-limit=45000  --fast-limit=60000 --stapm-time=80 --slow-limit=60000 --slow-time=1 --vrm-current=120000 --vrmmax-current=120000 --vrmsoc-current=120000 --vrmsocmax-current=120000 --vrmgfx-current=120000 --prochot-deassertion-ramp=2 ";
                foreach (var element in ProfileSetComboBox.Items)
                {
                    if (element != null && (element as ComboBoxItem)!.Name == "PremadeSsAMax")
                    {
                        ProfileSetComboBox.SelectedItem = (element as ComboBoxItem)!;
                        ProfileSetButton.IsEnabled = false;
                    }
                }
            } // Максимум
            else if (SettingsService.PremadeMaxActivated)
            {
                SettingsService.Preset = -1;
                nextProfile = "Shell_Preset_Min".GetLocalized();
                SettingsService.PremadeMinActivated = true;
                SettingsService.PremadeEcoActivated = false;
                SettingsService.PremadeBalanceActivated = false;
                SettingsService.PremadeSpeedActivated = false;
                SettingsService.PremadeMaxActivated = false;
                SettingsService.RyzenADJline =
                    " --tctl-temp=60 --stapm-limit=9000 --fast-limit=9000 --stapm-time=900 --slow-limit=6000 --slow-time=900 --vrm-current=120000 --vrmmax-current=120000 --vrmsoc-current=120000 --vrmsocmax-current=120000 --vrmgfx-current=120000 --prochot-deassertion-ramp=2 ";
                foreach (var element in ProfileSetComboBox.Items)
                {
                    if (element != null && (element as ComboBoxItem)!.Name == "PremadeSsAMin")
                    {
                        ProfileSetComboBox.SelectedItem = (element as ComboBoxItem)!;
                        ProfileSetButton.IsEnabled = false;
                    }
                }
            } // Минимум
        }

        SettingsService.SaveSettings();
        SelectedProfile = nextProfile;
        return nextProfile;
    }

    private string NextCustomProfile_Switch()
    {
        var nextProfile = string.Empty;
        ProfileLoad(); 
        if (SettingsService.Preset == -1) // У нас был готовый пресет
        {
            if (_profile.Length > 0 &&
                _profile[0].profilename !=
                string.Empty) // Проверка именно на НОЛЬ, а не на пустую строку, так как профиль может загрузиться некорректно
            {
                SettingsService.Preset = 0;
                try
                {
                    foreach (var element in ProfileSetComboBox.Items)
                    {
                        if (element != null && element as ComboBoxItem != null)
                        {
                            var selectedName = (element as ComboBoxItem)!.Content.ToString();
                            if (selectedName != null && selectedName == _profile[0].profilename)
                            {
                                ProfileSetComboBox.SelectedItem = element as ComboBoxItem;
                                ProfileSetButton.IsEnabled = false;
                                nextProfile = selectedName;
                            }
                        }
                        else
                        {
                            MandarinAddNotification("TraceIt_Error".GetLocalized(),
                                $"Unable to select the profile {(element as ComboBoxItem)!.Content}",
                                InfoBarSeverity.Error);
                        }
                    }

                    MandarinSparseUnit();
                }
                catch
                {
                    MandarinAddNotification("TraceIt_Error".GetLocalized(),
                        $"Unable to select the profile {_profile[0].profilename}", InfoBarSeverity.Error);
                }
            }
        }
        else // У нас уже был выставлен какой-то профиль
        {
            var nextProfileIndex = _profile.Length - 1 >= SettingsService.Preset + 1 ? SettingsService.Preset + 1 : 0;
            if (_profile.Length > nextProfileIndex &&
                _profile[nextProfileIndex].profilename !=
                string.Empty) // Проверка именно на НОЛЬ, а не на пустую строку, так как профиль может загрузиться некорректно
            {
                SettingsService.Preset = nextProfileIndex;
                try
                {
                    foreach (var element in ProfileSetComboBox.Items)
                    {
                        if (element != null && element as ComboBoxItem != null)
                        {
                            var selectedName = (element as ComboBoxItem)!.Content.ToString();
                            if (selectedName != null && selectedName == _profile[nextProfileIndex].profilename)
                            {
                                ProfileSetComboBox.SelectedItem = element as ComboBoxItem;
                                ProfileSetButton.IsEnabled = false;
                                nextProfile = selectedName;
                            }
                        }
                        else
                        {
                            MandarinAddNotification("TraceIt_Error".GetLocalized(),
                                $"Unable to select the profile {(element as ComboBoxItem)!.Content}",
                                InfoBarSeverity.Error);
                        }
                    }

                    MandarinSparseUnit();
                }
                catch
                {
                    MandarinAddNotification("TraceIt_Error".GetLocalized(),
                        $"Unable to select the profile {_profile[0].profilename}", InfoBarSeverity.Error);
                }
            }
            else
            {
                MandarinAddNotification("TraceIt_Error".GetLocalized(), nextProfileIndex.ToString(),
                    InfoBarSeverity.Error);
            }
        }

        SettingsService.SaveSettings(); 
        SelectedProfile = nextProfile;
        return nextProfile;
    }

    private void MandarinSparseUnit()
    {
        int indexRequired;
        var element = ProfileSetComboBox.SelectedItem as ComboBoxItem; 
        //Required index
        if (!element!.Name.Contains("PremadeSsA"))
        {
            indexRequired = ProfileSetComboBox.SelectedIndex - 1;
            SettingsService.Preset = ProfileSetComboBox.SelectedIndex - 1;
            SettingsService.SaveSettings(); 
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
            if (element.Name.Contains("Min"))
            {
                SettingsService.PremadeMinActivated = true;
                SettingsService.PremadeEcoActivated = false;
                SettingsService.PremadeBalanceActivated = false;
                SettingsService.PremadeSpeedActivated = false;
                SettingsService.PremadeMaxActivated = false;
                SettingsService.Preset = -1;
                SettingsService.RyzenADJline =
                    " --tctl-temp=60 --stapm-limit=9000 --fast-limit=9000 --stapm-time=64 --slow-limit=6000 --slow-time=128 --vrm-current=180000 --vrmmax-current=180000 --vrmsoc-current=180000 --vrmsocmax-current=180000 --vrmgfx-current=180000 --prochot-deassertion-ramp=2";
                MainWindow.Applyer.Apply(SettingsService.RyzenADJline, false, SettingsService.ReapplyOverclock,
                    SettingsService.ReapplyOverclockTimer);
            }

            if (element.Name.Contains("Eco"))
            {
                SettingsService.PremadeMinActivated = false;
                SettingsService.PremadeEcoActivated = true;
                SettingsService.PremadeBalanceActivated = false;
                SettingsService.PremadeSpeedActivated = false;
                SettingsService.PremadeMaxActivated = false;
                SettingsService.Preset = -1;
                SettingsService.RyzenADJline =
                    " --tctl-temp=68 --stapm-limit=15000  --fast-limit=18000 --stapm-time=64 --slow-limit=16000 --slow-time=128 --vrm-current=180000 --vrmmax-current=180000 --vrmsoc-current=180000 --vrmsocmax-current=180000 --vrmgfx-current=180000 --prochot-deassertion-ramp=2";
                MainWindow.Applyer.Apply(SettingsService.RyzenADJline, false, SettingsService.ReapplyOverclock,
                    SettingsService.ReapplyOverclockTimer);
            }

            if (element.Name.Contains("Bal"))
            {
                SettingsService.PremadeMinActivated = false;
                SettingsService.PremadeEcoActivated = false;
                SettingsService.PremadeBalanceActivated = true;
                SettingsService.PremadeSpeedActivated = false;
                SettingsService.PremadeMaxActivated = false;
                SettingsService.Preset = -1;
                SettingsService.RyzenADJline =
                    " --tctl-temp=75 --stapm-limit=18000  --fast-limit=20000 --stapm-time=64 --slow-limit=19000 --slow-time=128 --vrm-current=180000 --vrmmax-current=180000 --vrmsoc-current=180000 --vrmsocmax-current=180000 --vrmgfx-current=180000 --prochot-deassertion-ramp=2";
                MainWindow.Applyer.Apply(SettingsService.RyzenADJline, false, SettingsService.ReapplyOverclock,
                    SettingsService.ReapplyOverclockTimer);
            }

            if (element.Name.Contains("Spd"))
            {
                SettingsService.PremadeMinActivated = false;
                SettingsService.PremadeEcoActivated = false;
                SettingsService.PremadeBalanceActivated = false;
                SettingsService.PremadeSpeedActivated = true;
                SettingsService.PremadeMaxActivated = false;
                SettingsService.Preset = -1;
                SettingsService.RyzenADJline =
                    " --tctl-temp=80 --stapm-limit=20000  --fast-limit=20000 --stapm-time=64 --slow-limit=20000 --slow-time=128 --vrm-current=180000 --vrmmax-current=180000 --vrmsoc-current=180000 --vrmsocmax-current=180000 --vrmgfx-current=180000 --prochot-deassertion-ramp=2";
                MainWindow.Applyer.Apply(SettingsService.RyzenADJline, false, SettingsService.ReapplyOverclock,
                    SettingsService.ReapplyOverclockTimer);
            }

            if (element.Name.Contains("Max"))
            {
                SettingsService.PremadeMinActivated = false;
                SettingsService.PremadeEcoActivated = false;
                SettingsService.PremadeBalanceActivated = false;
                SettingsService.PremadeSpeedActivated = false;
                SettingsService.PremadeMaxActivated = true;
                SettingsService.Preset = -1;
                SettingsService.RyzenADJline =
                    " --tctl-temp=90 --stapm-limit=45000  --fast-limit=60000 --stapm-time=64 --slow-limit=60000 --slow-time=128 --vrm-current=180000 --vrmmax-current=180000 --vrmsoc-current=180000 --vrmsocmax-current=180000 --vrmgfx-current=180000 --prochot-deassertion-ramp=2";
                MainWindow.Applyer.Apply(SettingsService.RyzenADJline, false, SettingsService.ReapplyOverclock,
                    SettingsService.ReapplyOverclockTimer);
            }

            SettingsService.SaveSettings(); 
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
            return;
        }

        ProfileLoad();
        var adjline = "";
        if (_profile[indexRequired].cpu1)
        {
            adjline += " --tctl-temp=" + _profile[indexRequired].cpu1value;
        }

        if (_profile[indexRequired].cpu2)
        {
            adjline += " --stapm-limit=" + _profile[indexRequired].cpu2value + "000";
        }

        if (_profile[indexRequired].cpu3)
        {
            adjline += " --fast-limit=" + _profile[indexRequired].cpu3value + "000";
        }

        if (_profile[indexRequired].cpu4)
        {
            adjline += " --slow-limit=" + _profile[indexRequired].cpu4value + "000";
        }

        if (_profile[indexRequired].cpu5)
        {
            adjline += " --stapm-time=" + _profile[indexRequired].cpu5value;
        }

        if (_profile[indexRequired].cpu6)
        {
            adjline += " --slow-time=" + _profile[indexRequired].cpu6value;
        }

        if (_profile[indexRequired].cpu7)
        {
            adjline += " --cHTC-temp=" + _profile[indexRequired].cpu7value;
        }

        //vrm
        if (_profile[indexRequired].vrm1)
        {
            adjline += " --vrmmax-current=" + _profile[indexRequired].vrm1value + "000";
        }

        if (_profile[indexRequired].vrm2)
        {
            adjline += " --vrm-current=" + _profile[indexRequired].vrm2value + "000";
        }

        if (_profile[indexRequired].vrm3)
        {
            adjline += " --vrmsocmax-current=" + _profile[indexRequired].vrm3value + "000";
        }

        if (_profile[indexRequired].vrm4)
        {
            adjline += " --vrmsoc-current=" + _profile[indexRequired].vrm4value + "000";
        }

        if (_profile[indexRequired].vrm5)
        {
            adjline += " --psi0-current=" + _profile[indexRequired].vrm5value + "000";
        }

        if (_profile[indexRequired].vrm6)
        {
            adjline += " --psi0soc-current=" + _profile[indexRequired].vrm6value + "000";
        }

        if (_profile[indexRequired].vrm7)
        {
            adjline += " --prochot-deassertion-ramp=" + _profile[indexRequired].vrm7value;
        }

        if (_profile[indexRequired].vrm8)
        {
            adjline += " --oc-volt-scalar=" + _profile[indexRequired].vrm8value;
        }

        if (_profile[indexRequired].vrm9)
        {
            adjline += " --oc-volt-modular=" + _profile[indexRequired].vrm9value;
        }

        if (_profile[indexRequired].vrm10)
        {
            adjline += " --oc-volt-variable=" + _profile[indexRequired].vrm10value;
        }

        //gpu
        if (_profile[indexRequired].gpu1)
        {
            adjline += " --min-socclk-frequency=" + _profile[indexRequired].gpu1value;
        }

        if (_profile[indexRequired].gpu2)
        {
            adjline += " --max-socclk-frequency=" + _profile[indexRequired].gpu2value;
        }

        if (_profile[indexRequired].gpu3)
        {
            adjline += " --min-fclk-frequency=" + _profile[indexRequired].gpu3value;
        }

        if (_profile[indexRequired].gpu4)
        {
            adjline += " --max-fclk-frequency=" + _profile[indexRequired].gpu4value;
        }

        if (_profile[indexRequired].gpu5)
        {
            adjline += " --min-vcn=" + _profile[indexRequired].gpu5value;
        }

        if (_profile[indexRequired].gpu6)
        {
            adjline += " --max-vcn=" + _profile[indexRequired].gpu6value;
        }

        if (_profile[indexRequired].gpu7)
        {
            adjline += " --min-lclk=" + _profile[indexRequired].gpu7value;
        }

        if (_profile[indexRequired].gpu8)
        {
            adjline += " --max-lclk=" + _profile[indexRequired].gpu8value;
        }

        if (_profile[indexRequired].gpu9)
        {
            adjline += " --min-gfxclk=" + _profile[indexRequired].gpu9value;
        }

        if (_profile[indexRequired].gpu10)
        {
            adjline += " --max-gfxclk=" + _profile[indexRequired].gpu10value;
        }

        if (_profile[indexRequired].gpu11)
        {
            adjline += " --min-cpuclk=" + _profile[indexRequired].gpu11value;
        }

        if (_profile[indexRequired].gpu12)
        {
            adjline += " --max-cpuclk=" + _profile[indexRequired].gpu12value;
        }

        if (_profile[indexRequired].gpu13)
        {
            adjline += " --setgpu-arerture-low=" + _profile[indexRequired].gpu13value;
        }

        if (_profile[indexRequired].gpu14)
        {
            adjline += " --setgpu-arerture-high=" + _profile[indexRequired].gpu14value;
        }

        if (_profile[indexRequired].gpu15)
        {
            if (_profile[indexRequired].gpu15value != 0)
            {
                adjline += " --start-gpu-link=" + (_profile[indexRequired].gpu15value - 1);
            }
            else
            {
                adjline += " --stop-gpu-link=0";
            }
        }

        if (_profile[indexRequired].gpu16)
        {
            if (_profile[indexRequired].gpu16value != 0)
            {
                adjline += " --setcpu-freqto-ramstate=" + (_profile[indexRequired].gpu16value - 1);
            }
            else
            {
                adjline += " --stopcpu-freqto-ramstate=0";
            }
        }

        //advanced
        if (_profile[indexRequired].advncd1)
        {
            adjline += " --vrmgfx-current=" + _profile[indexRequired].advncd1value + "000";
        }

        if (_profile[indexRequired].advncd2)
        {
            adjline += " --vrmcvip-current=" + _profile[indexRequired].advncd2value + "000";
        }

        if (_profile[indexRequired].advncd3)
        {
            adjline += " --vrmgfxmax_current=" + _profile[indexRequired].advncd3value + "000";
        }

        if (_profile[indexRequired].advncd4)
        {
            adjline += " --psi3cpu_current=" + _profile[indexRequired].advncd4value + "000";
        }

        if (_profile[indexRequired].advncd5)
        {
            adjline += " --psi3gfx_current=" + _profile[indexRequired].advncd5value + "000";
        }

        if (_profile[indexRequired].advncd6)
        {
            adjline += " --apu-skin-temp=" + _profile[indexRequired].advncd6value;
        }

        if (_profile[indexRequired].advncd7)
        {
            adjline += " --dgpu-skin-temp=" + _profile[indexRequired].advncd7value;
        }

        if (_profile[indexRequired].advncd8)
        {
            adjline += " --apu-slow-limit=" + _profile[indexRequired].advncd8value + "000";
        }

        if (_profile[indexRequired].advncd9)
        {
            adjline += " --skin-temp-limit=" + _profile[indexRequired].advncd9value + "000";
        }

        if (_profile[indexRequired].advncd10)
        {
            adjline += " --gfx-clk=" + _profile[indexRequired].advncd10value;
        }

        if (_profile[indexRequired].advncd11)
        {
            adjline += " --oc-clk=" + _profile[indexRequired].advncd11value;
        }

        if (_profile[indexRequired].advncd12)
        {
            adjline += " --oc-volt=" + Math.Round((1.55 - _profile[indexRequired].advncd12value / 1000) / 0.00625);
        }


        if (_profile[indexRequired].advncd13)
        {
            if (_profile[indexRequired].advncd13value == 1)
            {
                adjline += " --max-performance=1";
            }

            if (_profile[indexRequired].advncd13value == 2)
            {
                adjline += " --power-saving=1";
            }
        }

        if (_profile[indexRequired].advncd14)
        {
            if (_profile[indexRequired].advncd14value == 0)
            {
                adjline += " --disable-oc=1";
            }

            if (_profile[indexRequired].advncd14value == 1)
            {
                adjline += " --enable-oc=1";
            }
        }

        if (_profile[indexRequired].advncd15)
        {
            adjline += " --pbo-scalar=" + _profile[indexRequired].advncd15value * 100;
        }

        SettingsService.RyzenADJline = adjline + " ";
        SettingsService.SaveSettings(); 
        MainWindow.Applyer.Apply(SettingsService.RyzenADJline, false, SettingsService.ReapplyOverclock,
            SettingsService.ReapplyOverclockTimer); //false - logging disabled 
        /*   if (profile[indexRequired].enablePstateEditor) { cpu.BtnPstateWrite_Click(); }*/
    }

    #endregion

    #region Notifications

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

    private void StartInfoUpdate()
    {
        _dispatcherTimer = new DispatcherTimer();
        _dispatcherTimer.Tick += async (_, _) => await GetNotify();
        _dispatcherTimer.Interval = TimeSpan.FromMilliseconds(1000);
        App.MainWindow.VisibilityChanged += Window_VisibilityChanged;
        App.MainWindow.Activated += Window_Activated;
        _dispatcherTimer.Start();
    }

    private void StopInfoUpdate()
    {
        _dispatcherTimer?.Stop();
    }

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

    private Task GetNotify()
    {
        if (SelectedProfile != ((ComboBoxItem)ProfileSetComboBox.SelectedItem).Content.ToString() &&
            !ProfileSetButton.IsEnabled)
        {
            SelectedProfile = ((ComboBoxItem)ProfileSetComboBox.SelectedItem).Content.ToString()!;
        }

        if (_isNotificationPanelShow)
        {
            return Task.CompletedTask;
        }

        NotifyLoad();
        if (_notify.Notifies == null)
        {
            return Task.CompletedTask;
        }

        DispatcherQueue?.TryEnqueue(() =>
        {
            var contains = false;
            if (_compareList == _notify.Notifies.Count && NotificationContainer.Children.Count != 0)
            {
                return;
            } //нет новых уведомлений - пока

            ClearAllNotification(null, null);
            var index = 0;
            foreach (var notify1 in _notify.Notifies!)
            {
                Grid? subcontent = null;
                switch (notify1.Title)
                {
                    //Если уведомление о изменении темы
                    case "Theme applied!":
                        Theme_Loader();
                        ClearAllNotification(NotificationPanelClearAllBtn, null); //Удалить всё
                        return; //Удалить и не показывать
                    case "UpdateNAVBAR":
                    {
                        NavigationViewControl.Margin = new Thickness(-40, 0, 0, 0);
                        NavigationViewControl.IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed;
                        NavigationViewControl.IsSettingsVisible = false;
                        NavigationViewControl.IsPaneOpen = false;
                        foreach (var element in NavigationViewControl.MenuItems)
                        {
                            ((NavigationViewItem)element).Visibility = Visibility.Collapsed;
                        }

                        ClearAllNotification(NotificationPanelClearAllBtn, null); //Удалить всё
                        return; //Удалить и не показывать
                    }
                }

                if (notify1.Msg.Contains("DELETEUNAVAILABLE"))
                {
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
                                { UseShellExecute = true });
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
                        var string1 = but2.Tag.ToString(); // some content
                        var stringFrom = string1?.Split('\"');
                        if (stringFrom != null)
                        {
                            ProfileLoad();
                            var commandActions = new Dictionary<string, Action>
                            {
                                {
                                    "Param_SMU_Func_Text/Text".GetLocalized(),
                                    () => _profile[SettingsService.Preset].smuFunctionsEnabl = false
                                },
                                { "Param_CPU_c2/Text".GetLocalized(), () => _profile[SettingsService.Preset].cpu2 = false },
                                { "Param_VRM_v2/Text".GetLocalized(), () => _profile[SettingsService.Preset].vrm2 = false },
                                { "Param_VRM_v1/Text".GetLocalized(), () => _profile[SettingsService.Preset].vrm1 = false },
                                { "Param_CPU_c1/Text".GetLocalized(), () => _profile[SettingsService.Preset].cpu1 = false },
                                {
                                    "Param_ADV_a15/Text".GetLocalized(), () => _profile[SettingsService.Preset].advncd15 = false
                                },
                                {
                                    "Param_ADV_a11/Text".GetLocalized(), () => _profile[SettingsService.Preset].advncd11 = false
                                },
                                {
                                    "Param_ADV_a12/Text".GetLocalized(), () => _profile[SettingsService.Preset].advncd12 = false
                                },
                                { "Param_CO_O1/Text".GetLocalized(), () => _profile[SettingsService.Preset].coall = false },
                                { "Param_CO_O2/Text".GetLocalized(), () => _profile[SettingsService.Preset].cogfx = false },
                                {
                                    "Param_CCD1_CO_Section/Text".GetLocalized(),
                                    () => _profile[SettingsService.Preset].coprefmode = 0
                                },
                                {
                                    "Param_ADV_a14_E/Content".GetLocalized(),
                                    () => _profile[SettingsService.Preset].advncd14 = false
                                },
                                { "Param_CPU_c5/Text".GetLocalized(), () => _profile[SettingsService.Preset].cpu5 = false },
                                { "Param_CPU_c3/Text".GetLocalized(), () => _profile[SettingsService.Preset].cpu3 = false },
                                { "Param_CPU_c4/Text".GetLocalized(), () => _profile[SettingsService.Preset].cpu4 = false },
                                { "Param_CPU_c6/Text".GetLocalized(), () => _profile[SettingsService.Preset].cpu6 = false },
                                { "Param_CPU_c7/Text".GetLocalized(), () => _profile[SettingsService.Preset].cpu7 = false },
                                { "Param_ADV_a6/Text".GetLocalized(), () => _profile[SettingsService.Preset].advncd6 = false },
                                { "Param_VRM_v4/Text".GetLocalized(), () => _profile[SettingsService.Preset].vrm4 = false },
                                { "Param_VRM_v3/Text".GetLocalized(), () => _profile[SettingsService.Preset].vrm3 = false },
                                { "Param_ADV_a2/Text".GetLocalized(), () => _profile[SettingsService.Preset].advncd2 = false },
                                { "Param_ADV_a1/Text".GetLocalized(), () => _profile[SettingsService.Preset].advncd1 = false },
                                { "Param_ADV_a3/Text".GetLocalized(), () => _profile[SettingsService.Preset].advncd3 = false },
                                { "Param_VRM_v7/Text".GetLocalized(), () => _profile[SettingsService.Preset].vrm7 = false },
                                { "Param_ADV_a4/Text".GetLocalized(), () => _profile[SettingsService.Preset].advncd4 = false },
                                { "Param_ADV_a5/Text".GetLocalized(), () => _profile[SettingsService.Preset].advncd5 = false },
                                {
                                    "Param_ADV_a10/Text".GetLocalized(), () => _profile[SettingsService.Preset].advncd10 = false
                                },
                                {
                                    "Param_ADV_a13_E/Content".GetLocalized(),
                                    () => _profile[SettingsService.Preset].advncd13 = false
                                },
                                {
                                    "Param_ADV_a13_U/Content".GetLocalized(),
                                    () => _profile[SettingsService.Preset].advncd13 = false
                                },
                                { "Param_ADV_a8/Text".GetLocalized(), () => _profile[SettingsService.Preset].advncd8 = false },
                                { "Param_ADV_a7/Text".GetLocalized(), () => _profile[SettingsService.Preset].advncd7 = false },
                                { "Param_VRM_v5/Text".GetLocalized(), () => _profile[SettingsService.Preset].vrm5 = false },
                                { "Param_VRM_v6/Text".GetLocalized(), () => _profile[SettingsService.Preset].vrm6 = false },
                                { "Param_ADV_a9/Text".GetLocalized(), () => _profile[SettingsService.Preset].advncd9 = false },
                                { "Param_GPU_g12/Text".GetLocalized(), () => _profile[SettingsService.Preset].gpu12 = false },
                                { "Param_GPU_g11/Text".GetLocalized(), () => _profile[SettingsService.Preset].gpu11 = false },
                                { "Param_GPU_g10/Text".GetLocalized(), () => _profile[SettingsService.Preset].gpu10 = false },
                                { "Param_GPU_g9/Text".GetLocalized(), () => _profile[SettingsService.Preset].gpu9 = false },
                                { "Param_GPU_g2/Text".GetLocalized(), () => _profile[SettingsService.Preset].gpu2 = false },
                                { "Param_GPU_g1/Text".GetLocalized(), () => _profile[SettingsService.Preset].gpu1 = false },
                                { "Param_GPU_g4/Text".GetLocalized(), () => _profile[SettingsService.Preset].gpu4 = false },
                                { "Param_GPU_g3/Text".GetLocalized(), () => _profile[SettingsService.Preset].gpu3 = false },
                                { "Param_GPU_g6/Text".GetLocalized(), () => _profile[SettingsService.Preset].gpu6 = false },
                                { "Param_GPU_g5/Text".GetLocalized(), () => _profile[SettingsService.Preset].gpu5 = false },
                                { "Param_GPU_g8/Text".GetLocalized(), () => _profile[SettingsService.Preset].gpu8 = false },
                                { "Param_GPU_g7/Text".GetLocalized(), () => _profile[SettingsService.Preset].gpu7 = false },
                                { "Param_VRM_v8/Text".GetLocalized(), () => _profile[SettingsService.Preset].vrm8 = false },
                                { "Param_GPU_g13/Text".GetLocalized(), () => _profile[SettingsService.Preset].vrm9 = false },
                                { "Param_GPU_g14/Text".GetLocalized(), () => _profile[SettingsService.Preset].vrm9 = false },
                                { "Param_GPU_g15/Text".GetLocalized(), () => _profile[SettingsService.Preset].gpu15 = false },
                                { "Param_GPU_g16/Text".GetLocalized(), () => _profile[SettingsService.Preset].gpu16 = false },
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
                            ClearAllNotification(NotificationPanelClearAllBtn, null); //Удалить всё
                            await Task.Delay(2000);
                            but2.IsEnabled = true;
                            await sw.WriteLineAsync(@"//------OK------\\");
                            sw.Close();
                            if (navigationService.Frame!.GetPageViewModel() is not ПараметрыViewModel)
                            {
                                navigationService.NavigateTo(typeof(ПараметрыViewModel).FullName!, null, true);
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
                                                butLogs //,
                                                //but2
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
                    notify1.Msg = notify1.Msg.Replace("DELETEUNAVAILABLE", "");
                }

                MandarinAddNotification(notify1.Title, notify1.Msg, notify1.Type, notify1.isClosable, subcontent);
                if (notify1.Title.Contains("SaveSuccessTitle".GetLocalized()) ||
                    notify1.Title.Contains("DeleteSuccessTitle".GetLocalized()) ||
                    notify1.Title.Contains("Edit_TargetTitle".GetLocalized()))
                {
                    contains = true;
                }

                if (SettingsViewModel.VersionId != 5 &&
                    index > 8) //Если 9 уведомлений - очистить для оптимизации производительности
                {
                    index = 0; //Сброс счётчика циклов
                    ClearAllNotification(NotificationPanelClearAllBtn, null); //Удалить всё
                }

                index++;
            }

            if (contains)
            {
                GetProfileInit();
            } //Чтобы обновить всего раз, а не много раз, чтобы не сбить конфиг

            _compareList = _notify.Notifies.Count;
        });
        return Task.CompletedTask;
    }

    #endregion

    #endregion

    private void Theme_Loader()
    {
        ThemeLoad();
        try
        {
            ThemeOpacity.Opacity = _themer.Themes[SettingsService.ThemeType].ThemeOpacity;
            ThemeMaskOpacity.Opacity = _themer.Themes[SettingsService.ThemeType].ThemeMaskOpacity;
            var themeMobil = App.GetService<SettingsViewModel>();
            var themeLight = _themer.Themes[SettingsService.ThemeType].ThemeLight ? ElementTheme.Light : ElementTheme.Dark;
            themeMobil.SwitchThemeCommand.Execute(themeLight);
            if (SettingsService.ThemeType > 2)
            {
                ThemeBackground.ImageSource =
                    new BitmapImage(new Uri(_themer.Themes[SettingsService.ThemeType].ThemeBackground));
            }
        }
        catch
        {
            NotifyLoad();
            _notify.Notifies ??= [];
            try
            {
                _notify.Notifies.Add(new Notify
                {
                    Title =
                        "ThemeError".GetLocalized() + "\"" + $"{_themer.Themes[SettingsService.ThemeType].ThemeName}" + "\"",
                    Msg = "ThemeNotFoundBg".GetLocalized(), Type = InfoBarSeverity.Error
                });
            }
            catch
            {
                _notify.Notifies.Add(new Notify
                {
                    Title = "ThemeError".GetLocalized() + "\"" + ">> " + SettingsService.ThemeType + "\"",
                    Msg = "ThemeNotFoundBg".GetLocalized(), Type = InfoBarSeverity.Error
                });
            }

            NotifySave();
            SettingsService.ThemeType = 0;
            SettingsService.SaveSettings(); 
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
        try
        {
            _profile = JsonConvert.DeserializeObject<Profile[]>(File.ReadAllText(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\profile.json"))!;
        }
        catch
        {
            JsonRepair('p');
        }
    }

    private void NotifySave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                "SakuOverclock"));
            File.WriteAllText(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\notify.json",
                JsonConvert.SerializeObject(_notify, Formatting.Indented));
        }
        catch (Exception ex)
        {
            MandarinAddNotification("TraceIt_Error".GetLocalized(), ex.ToString(), InfoBarSeverity.Error);
        }
    }

    private async void NotifyLoad()
    {
        try
        {
            var success = false;
            var retryCount = 1;
            while (!success && retryCount < 3)
            {
                if (File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                                @"\SakuOverclock\notify.json"))
                {
                    try
                    {
                        _notify = JsonConvert.DeserializeObject<JsonContainers.Notifications>(
                            await File.ReadAllTextAsync(Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                                                        @"\SakuOverclock\notify.json"))!;
                        success = true;
                    }
                    catch
                    {
                        JsonRepair('n');
                    }
                }
                else
                {
                    JsonRepair('n');
                }

                if (!success)
                {
                    // Сделайте задержку перед следующей попыткой
                    await Task.Delay(30);
                    retryCount++;
                }
            }
        }
        catch (Exception ex)
        {
            MandarinAddNotification("TraceIt_Error".GetLocalized(), ex.ToString(), InfoBarSeverity.Error);
        }
    }

    private void ThemeLoad()
    {
        try
        {
            _themer = JsonConvert.DeserializeObject<Themer>(File.ReadAllText(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\theme.json"))!;
            if (_themer.Themes.Count > 8)
            {
                _themer.Themes.RemoveRange(0, 8);
            }

            if (_themer.Themes.Count == 0)
            {
                JsonRepair('t');
            }
        }
        catch
        {
            JsonRepair('t');
        }
    }

    private void JsonRepair(char file)
    {
        switch (file)
        {
            case 't':
            {
                _themer = new Themer();
                try
                {
                    Directory.CreateDirectory(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(
                        Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\theme.json",
                        JsonConvert.SerializeObject(_themer));
                }
                catch
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                                @"\SakuOverclock\theme.json");
                    Directory.CreateDirectory(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(
                        Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\theme.json",
                        JsonConvert.SerializeObject(_themer));
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(),
                        AppContext.BaseDirectory));
                }

                break;
            } 
            case 'p':
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
                    App.MainWindow.Close();
                }

                break;
            }
            case 'n':
            {
                _notify = new JsonContainers.Notifications();
                try
                {
                    Directory.CreateDirectory(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(
                        Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\notify.json",
                        JsonConvert.SerializeObject(_notify));
                }
                catch
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                                @"\SakuOverclock\notify.json");
                    Directory.CreateDirectory(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(
                        Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\notify.json",
                        JsonConvert.SerializeObject(_notify));
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(),
                        AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }

                break;
            }
        }
    }

    #endregion

    #region Keyboard Hooks

    private static IntPtr SetHook(LowLevelKeyboardProc proc) // Эту функцию можно не изменять
    {
        using var curProcess = Process.GetCurrentProcess(); // Получаем текущий процесс

        using var curModule = curProcess.MainModule!; // Получаем главный модуль процесса

        return SetWindowsHookEx(WhKeyboardLl, proc, // Вызываем WinAPI функцию
            GetModuleHandle(curModule.ModuleName), 0); // Получаем хэндл модуля
    }

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    private static bool IsAltPressed()
    {
        return (GetKeyState(VkMenu) & KeyPressed) != 0;
    }

    private delegate IntPtr
        LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam); // Callback делегат(для вызова callback метода)

    private nint HookCallbackAsync(int nCode, IntPtr wParam, IntPtr lParam) // Собственно сам callback метод
    {
        // Проверяем следует ли перехватывать хук (первая половина), и то, что произошло именно событие нажатия на клавишу (вторая половина)
        if (nCode >= 0 && wParam == WmKeydown)
        {
            var virtualkeyCode = Marshal.ReadInt32(lParam); // Получаем код клавиши из неуправляемой памяти
            if (GetAsyncKeyState(0x11) < 0 &&
                IsAltPressed()) //0x11 - Control, 0x4000 - Alt
            {
                switch ((VirtualKey)virtualkeyCode)
                {
                    // Переключить между своими пресетами
                    case VirtualKey.W:
                    {
                        //Создать уведомление
                        var nextCustomProfile = NextCustomProfile_Switch();
                        ProfileSwitcher.ProfileSwitcher.ShowOverlay(nextCustomProfile);
                        MandarinAddNotification("Shell_ProfileChanging".GetLocalized(),
                            "Shell_ProfileChanging_Custom".GetLocalized() + $"{nextCustomProfile}!",
                            InfoBarSeverity.Informational);
                        MainWindow.Applyer.ApplyWithoutAdjLine(false);
                        break;
                    }
                    // Переключить между готовыми пресетами
                    case VirtualKey.P:
                        var nextPremadeProfile = NextPremadeProfile_Switch();
                        ProfileSwitcher.ProfileSwitcher.ShowOverlay(nextPremadeProfile);
                        MandarinAddNotification("Shell_ProfileChanging".GetLocalized(),
                            "Shell_ProfileChanging_Premade".GetLocalized() + $"{nextPremadeProfile}!",
                            InfoBarSeverity.Informational);
                        MainWindow.Applyer.ApplyWithoutAdjLine(false);
                        break;
                    // Переключить состояние RTSS
                    case VirtualKey.R: 
                        if (SettingsService.RTSSMetricsEnabled)
                        {
                            var iconGrid = new Grid
                            {
                                Width = 100,
                                Height = 100,
                                Children =
                                {
                                    new FontIcon
                                    {
                                        Glyph = "\uE7AC",
                                        Opacity = 0.543d,
                                        FontSize = 40
                                    },
                                    new FontIcon
                                    {
                                        Glyph = "\uE711",
                                        Margin = new Thickness(4, 0, 0, 0),
                                        VerticalAlignment = VerticalAlignment.Center,
                                        HorizontalAlignment = HorizontalAlignment.Center,
                                        FontSize = 40
                                    }
                                }
                            };
                            ProfileSwitcher.ProfileSwitcher.ShowOverlay("RTSS " + "Cooler_Service_Disabled/Content".GetLocalized(), null, iconGrid);
                            var navigationService = App.GetService<INavigationService>();
                            navigationService.NavigateTo(typeof(ГлавнаяViewModel).FullName!, null, true);
                            SettingsService.RTSSMetricsEnabled = false;
                            SettingsService.SaveSettings(); 
                        }
                        else
                        {
                            var iconGrid = new Grid
                            {
                                Width = 100,
                                Height = 100,
                                Children =
                                {
                                    new FontIcon
                                    {
                                        Glyph = "\uE7AC",
                                        FontSize = 40
                                    }
                                }
                            };
                            ProfileSwitcher.ProfileSwitcher.ShowOverlay("RTSS " + "Cooler_Service_Enabled/Content".GetLocalized(), null, iconGrid);
                            SettingsService.RTSSMetricsEnabled = true;
                            SettingsService.SaveSettings(); 
                            var navigationService = App.GetService<INavigationService>();
                            navigationService.NavigateTo(typeof(ИнформацияViewModel).FullName!, null, true);
                        }

                        MandarinAddNotification("Shell_RTSSChanging".GetLocalized(),
                            "Shell_RTSSChanging_Success".GetLocalized(), InfoBarSeverity.Informational);
                        break;
                }
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam); // Передаем нажатие в следующее приложение
    }

    #region Hook DLL Imports

    // Импорт необходимых функций из WinApi
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

    private void Icon_Click(object sender, RoutedEventArgs e)
    {
        _fixedTitleBar = !_fixedTitleBar;
    }

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

        transform = ProfileSetup.TransformToVisual(null);
        bounds = transform.TransformBounds(new Rect(0, 0,
            ProfileSetup.ActualWidth,
            ProfileSetup.ActualHeight));
        var profileSetupRect = GetRect(bounds, scaleAdjustment);

        transform = RingerNotifGrid.TransformToVisual(null);
        bounds = transform.TransformBounds(new Rect(0, 0,
            RingerNotifGrid.ActualWidth,
            RingerNotifGrid.ActualHeight));
        var ringerNotifRect = GetRect(bounds, scaleAdjustment);

        var rectArray = new[] { searchBoxRect, profileSetupRect, ringerNotifRect };

        var nonClientInputSrc = InputNonClientPointerSource.GetForWindowId(MAppWindow.Id);
        nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, rectArray);
    }

    private static RectInt32 GetRect(Rect bounds, double scale)
    {
        return new RectInt32(
            _X: (int)Math.Round(bounds.X * scale),
            _Y: (int)Math.Round(bounds.Y * scale),
            _Width: (int)Math.Round(bounds.Width * scale),
            _Height: (int)Math.Round(bounds.Height * scale)
        );
    }

    #endregion

    #region Event Handlers

    private void ProfileSetButton_Click(object sender, RoutedEventArgs e)
    {
        MandarinSparseUnit();
        ProfileSetButton.IsEnabled = false;
    }

    private void ProfileSetButton_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        ActivateText.Text = ProfileSetButton.IsEnabled
            ? "Shell_Activate".GetLocalized()
            : "Shell_DeActivate".GetLocalized();
    }

    private void ProfileSetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded)
        {
            return;
        }

        SettingsService.SaveSettings(); 
        ProfileSetButton.IsEnabled = ProfileSetComboBox.SelectedIndex != SettingsService.Preset + 1;
    }

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
        var list = _notify.Notifies!;
        for (var i = 0; i < list.Count; i++)
        {
            var notify1 = list[i];
            if (sender.Title == notify1.Title && sender.Message == notify1.Msg && sender.Severity == notify1.Type)
            {
                NotifyLoad();
                _notify.Notifies?.RemoveAt(i);
                NotifySave();
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
                NotifyLoad();
                _notify.Notifies = [];
                NotifySave();
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
                    NotifChangelogTexts.Children.Clear();
                    await ГлавнаяPage.GenerateFormattedReleaseNotes(NotifChangelogTexts);
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

            notification.Name = msg + " " + DateTime.Now;
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