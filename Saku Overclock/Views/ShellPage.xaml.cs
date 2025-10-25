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
using Microsoft.UI.Xaml.Media.Imaging;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.JsonContainers;
using Saku_Overclock.Services;
using Saku_Overclock.ViewModels;
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
    private bool _isCustomProfile; // Флаг типа профиля для применения
    private int _pendingCustomProfileIndex = -1; // Индекс кастомного профиля для применения

    // Состояние для отслеживания позиции при быстром переключении
    private int _virtualCustomProfileIndex = -1; // Виртуальная позиция в кастомных профилях
    private string _virtualPremadeProfile = string.Empty; // Виртуальная позиция в готовых профилях
    private bool _isVirtualStateActive; // Флаг активности виртуального состояния

    private static readonly IAppNotificationService
        NotificationsService = App.GetService<IAppNotificationService>(); // Класс с уведомлениями

    private static readonly IOcFinderService OcFinder = App.GetService<IOcFinderService>();
    private static readonly IApplyerService Applyer = App.GetService<IApplyerService>();

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
        ViewModel = viewModel;
        InitializeComponent();
        ViewModel.NavigationService.Frame = NavigationFrame; // Выбранная пользователем страница
        ViewModel.NavigationViewService.Initialize(NavigationViewControl); // Инициализировать выбор страниц

        App.MainWindow.ExtendsContentIntoTitleBar = true;
        App.MainWindow.SetTitleBar(AppTitleBar);
        App.MainWindow.Activated += MainWindow_Activated; // Приложение активировалось, загрузить параметры TitleBar
        App.MainWindow.Closed += (_, _) =>
        {
            UnhookWindowsHookEx(_hookId); // Приложение закрылось - убить хуки
        };
    }

    #region Page Initialization and User Presets

    #region App TitleBar Initialization

    #region App loading and TitleBar

    /// <summary>
    ///     Загрузка приложения
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        /*if (AppSettings.AppFirstRun)
        {
            HideNavigationBar();
            Icon.Visibility = Visibility.Collapsed;
            RingerNotifGrid.Visibility = Visibility.Collapsed;
        }*/

        TitleBarHelper.UpdateTitleBar(RequestedTheme);
        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.Left, VirtualKeyModifiers.Menu));
        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.GoBack));

        StartInfoUpdate();
        LoadPresetsToViewModel();
        InitializeThemes();
        AutoStartHelper.AutoStartCheckAndFix();
    }

    /// <summary>
    ///     Обработчик активации окна
    /// </summary>
    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        App.AppTitlebar = VersionNumberIndicator;
        AppTitleBar.Loaded += AppTitleBar_Loaded;
        AppTitleBar.SizeChanged += AppTitleBar_SizeChanged;
    }

    /// <summary>
    ///     Помогает установить регион взаимодействия с программой (кликабельную кнопку уведомлений и лого)
    /// </summary>
    private void AppTitleBar_Loaded(object sender, RoutedEventArgs e) =>
        SetRegionsForCustomTitleBar(); //Установить регион взаимодействия

    /// <summary>
    ///     Помогает установить регион взаимодействия с программой (кликабельную кнопку уведомлений и лого)
    /// </summary>
    private void AppTitleBar_SizeChanged(object sender, SizeChangedEventArgs e) =>
        App.MainWindow.DispatcherQueue.TryEnqueue(SetRegionsForCustomTitleBar);

    #endregion

    #region Notifications

    #region Window Definitions

    /// <summary>
    ///     Активирует или выключает проверку уведомлений в приложении, если пользователь переключился на окно программы
    /// </summary>
    private void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.CodeActivated ||
            args.WindowActivationState == WindowActivationState.PointerActivated)
        {
            _dispatcherTimer?.Start(); // Снова запускает проверку наличия новых уведомлений
        }
        else
        {
            _dispatcherTimer?.Stop();
        }
    }

    /// <summary>
    ///     Активирует или выключает проверку уведомлений в приложении, если пользователь свернул или развернул окно программы
    /// </summary>
    private void Window_VisibilityChanged(object sender, WindowVisibilityChangedEventArgs args)
    {
        if (args.Visible)
        {
            _dispatcherTimer?.Start(); // Снова запускает проверку наличия новых уведомлений
        }
        else
        {
            _dispatcherTimer?.Stop();
        }
    }

    #endregion

    #region Info Update Timers

    /// <summary>
    ///     Начинает проверку наличия новых уведомлений
    /// </summary>
    private void StartInfoUpdate()
    {
        _dispatcherTimer = new DispatcherTimer();
        _dispatcherTimer.Tick += async (_, _) => await GetNotify();
        _dispatcherTimer.Interval = TimeSpan.FromMilliseconds(1000);
        App.MainWindow.VisibilityChanged += Window_VisibilityChanged;
        App.MainWindow.Activated += Window_Activated;
        _dispatcherTimer.Start();
    }

    #endregion

    #region Notification Update Voids

    /// <summary>
    ///     Главный метод проверки наличия новых уведомлений
    /// </summary>
    private Task GetNotify()
    {
        if (ViewModel.SelectedIndex != -1 && ViewModel.SelectedItem != string.Empty
                                          && SelectedProfile != ViewModel.SelectedItem)
        {
            SelectedProfile = ViewModel.SelectedItem ?? "Unknown";
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
                        InitializeThemes();
                        ClearAllNotification(NotificationPanelClearAllBtn, null); //Удалить все уведомления
                        return; //Удалить и не показывать 
                    case "UpdateNAVBAR":
                        HideNavigationBar();
                        Icon.Visibility = Visibility.Collapsed;
                        RingerNotificationGrid.Visibility = Visibility.Collapsed;
                        return; //Удалить и не показывать 
                    case "FirstLaunch":
                        HideNavigationBar();
                        Icon.Visibility = Visibility.Collapsed;
                        RingerNotificationGrid.Visibility = Visibility.Collapsed;
                        ClearAllNotification(NotificationPanelClearAllBtn, null); //Удалить все уведомления
                        return;
                    case "ExitFirstLaunch":
                        ShowNavigationBar();
                        Icon.Visibility = Visibility.Visible;
                        RingerNotificationGrid.Visibility = Visibility.Visible;
                        ClearAllNotification(NotificationPanelClearAllBtn, null); //Удалить все уведомления
                        return;
                    case "UPDATE_REQUIRED":
                        notify1.Title = "Shell_Update_App_Title".GetLocalized();
                        notify1.Msg = "Shell_Update_App_Message".GetLocalized() + " " +
                                      UpdateChecker.ParseVersion(UpdateChecker.GetNewVersion()?.TagName ??
                                                                 "Null-null-0.0.0.0");
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
                            HideNavigationBar();
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

                                navigationService.NavigateTo(
                                    navigationService.Frame!.GetPageViewModel() is not ГлавнаяViewModel
                                        ? typeof(ГлавнаяViewModel).FullName!
                                        : typeof(ПресетыViewModel).FullName!);

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
                LoadPresetsToViewModel();
            }

            _compareList = NotificationsService.Notifies.Count;
        });
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Скрывает навигационную панель
    /// </summary>
    private void HideNavigationBar()
    {
        NavigationViewControl.Margin = new Thickness(-49, -48, 0, 0);
        NavigationViewControl.IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed;
        NavigationViewControl.IsSettingsVisible = false;
        NavigationViewControl.IsPaneOpen = false;
        foreach (var element in NavigationViewControl.MenuItems)
        {
            ((NavigationViewItem)element).Visibility = Visibility.Collapsed;
        }

        ClearAllNotification(NotificationPanelClearAllBtn, null);
    }

    /// <summary>
    ///     Показывает навигационную панель после скрытия
    /// </summary>
    private void ShowNavigationBar()
    {
        NavigationViewControl.Margin = new Thickness(0, 0, 0, 0);
        NavigationViewControl.IsBackButtonVisible = NavigationViewBackButtonVisible.Visible;
        NavigationViewControl.IsSettingsVisible = true;
        foreach (var element in NavigationViewControl.MenuItems)
        {
            ((NavigationViewItem)element).Visibility = Visibility.Visible;
        }

        ClearAllNotification(NotificationPanelClearAllBtn, null);
    }

    #endregion

    #endregion

    #endregion

    #region User Presets

    /// <summary>
    ///     Загружает пресеты в ViewModel (включая готовые)
    /// </summary>
    private void LoadPresetsToViewModel()
    {
        ProfileLoad();

        var presetsCollection = _profile.Select(securedProfile => securedProfile.Profilename).ToList();

        presetsCollection.Add("PremadeSsAMin");
        presetsCollection.Add("PremadeSsAEco");
        presetsCollection.Add("PremadeSsABal");
        presetsCollection.Add("PremadeSsASpd");
        presetsCollection.Add("PremadeSsAMax");

        ViewModel.Presets = presetsCollection;

        if (AppSettings.Preset == -1)
        {
            if (AppSettings.PremadeMinActivated)
            {
                SelectRightPresetName("PremadeSsAMin");
            }

            if (AppSettings.PremadeEcoActivated)
            {
                SelectRightPresetName("PremadeSsAEco");
            }

            if (AppSettings.PremadeBalanceActivated)
            {
                SelectRightPresetName("PremadeSsABal");
            }

            if (AppSettings.PremadeSpeedActivated)
            {
                SelectRightPresetName("PremadeSsASpd");
            }

            if (AppSettings.PremadeMaxActivated)
            {
                SelectRightPresetName("PremadeSsAMax");
            }
        }
        else
        {
            ViewModel.SelectedIndex = AppSettings.Preset;

            if (_profile.Length > AppSettings.Preset)
            {
                ViewModel.SelectedItem = _profile[AppSettings.Preset].Profilename;
            }
            else
            {
                AppSettings.Preset = -1;
                AppSettings.SaveSettings();
            }
        }
    }

    /// <summary>
    ///     Помощник, выставляющий во ViewModel нужный пресет по имени
    /// </summary>
    private void SelectRightPresetName(string name)
    {
        foreach (var element in ViewModel.Presets.Where(element => element.Contains(name)))
        {
            ViewModel.SelectedItem = element;
            ViewModel.SelectedIndex = ViewModel.Presets.IndexOf(name);
            return;
        }
    }

    /// <summary>
    ///     Делает активным готовый пресет по короткому имени, но не применяет его
    /// </summary>
    public static void SelectPremadePreset(string nextProfile)
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

    /// <summary>
    ///     Вспомогательный словарь, показывающий какой готовый пресет следующий, на основе предыдущего
    /// </summary>
    private static Dictionary<string, string> NextPremadePreset => new()
    {
        { "Min", "Eco" },
        { "Eco", "Balance" },
        { "Balance", "Speed" },
        { "Speed", "Max" },
        { "Max", "Min" }
    };

    /// <summary>
    ///     Вспомогательный словарь, выдающий данные о готовом пресете по его короткому имени
    /// </summary>
    public static Dictionary<string, (string name, string desc, string icon, string settings, string comboName)>
        PremadedPresets => new()
    {
        {
            "Min",
            ("Shell_Preset_Min", "Preset_Min_OverlayDesc", "\uEBC0",
                OcFinder.CreatePreset(PresetType.Min,
                        AppSettings.PremadeOptimizationLevel == 0 ? OptimizationLevel.Standard :
                        AppSettings.PremadeOptimizationLevel == 1 ? OptimizationLevel.Standard : OptimizationLevel.Deep)
                    .CommandString,
                "PremadeSsAMin")
        },
        {
            "Eco",
            ("Shell_Preset_Eco", "Preset_Eco_OverlayDesc", "\uEC0A",
                OcFinder.CreatePreset(PresetType.Eco,
                        AppSettings.PremadeOptimizationLevel == 0 ? OptimizationLevel.Standard :
                        AppSettings.PremadeOptimizationLevel == 1 ? OptimizationLevel.Standard : OptimizationLevel.Deep)
                    .CommandString,
                "PremadeSsAEco")
        },
        {
            "Balance",
            ("Shell_Preset_Balance", "Preset_Balance_OverlayDesc", "\uEC49",
                OcFinder.CreatePreset(PresetType.Balance,
                        AppSettings.PremadeOptimizationLevel == 0 ? OptimizationLevel.Standard :
                        AppSettings.PremadeOptimizationLevel == 1 ? OptimizationLevel.Standard : OptimizationLevel.Deep)
                    .CommandString,
                "PremadeSsABal")
        },
        {
            "Speed",
            ("Shell_Preset_Speed", "Preset_Speed_OverlayDesc", "\uE945",
                OcFinder.CreatePreset(PresetType.Performance,
                        AppSettings.PremadeOptimizationLevel == 0 ? OptimizationLevel.Standard :
                        AppSettings.PremadeOptimizationLevel == 1 ? OptimizationLevel.Standard : OptimizationLevel.Deep)
                    .CommandString,
                "PremadeSsASpd")
        },
        {
            "Max",
            ("Shell_Preset_Max", "Preset_Max_OverlayDesc", "\uECAD",
                OcFinder.CreatePreset(PresetType.Max,
                        AppSettings.PremadeOptimizationLevel == 0 ? OptimizationLevel.Standard :
                        AppSettings.PremadeOptimizationLevel == 1 ? OptimizationLevel.Standard : OptimizationLevel.Deep)
                    .CommandString,
                "PremadeSsAMax")
        }
    };

    /// <summary>
    ///     Метод для получения следующего готового пресета, без применения настроек
    /// </summary>
    private (string profileName, string profileKey) GetNextPremadeProfile(out string icon, out string desc)
    {
        icon = "\uE783";
        desc = "";

        var profiles = new[] { "Min", "Eco", "Balance", "Speed", "Max" };

        string currentProfile;

        // Определяем текущую позицию
        if (_isVirtualStateActive && !string.IsNullOrEmpty(_virtualPremadeProfile))
        {
            currentProfile = _virtualPremadeProfile;
        }
        else
        {
            // Определяем реальную текущую позицию
            // Активен готовый пресет - ищем какой именно
            currentProfile = profiles.FirstOrDefault(p =>
                (bool)typeof(IAppSettingsService).GetProperty($"Premade{p}Activated")
                    ?.GetValue(AppSettings)!) ?? "Balance";

            _virtualPremadeProfile = currentProfile;
            _isVirtualStateActive = true;
        }

        // Получаем следующий пресет
        var nextProfile = NextPremadePreset[currentProfile];

        // Обновляем виртуальную позицию
        _virtualPremadeProfile = nextProfile;

        // Получаем данные пресета для отображения
        var (name, description, iconStr, _, _) = PremadedPresets[nextProfile];
        desc = description.GetLocalized();
        icon = iconStr;

        return (name.GetLocalized(), nextProfile);
    }

    /// <summary>
    ///     Метод для получения следующего кастомного пресета, без применения настроек
    /// </summary>
    private (string profileName, int profileIndex) GetNextCustomProfile(out string? icon, out string? desc)
    {
        icon = string.Empty;
        desc = string.Empty;

        try
        {
            ProfileLoad();

            if (_profile.Length == 0)
            {
                MandarinAddNotification("TraceIt_Error".GetLocalized(),
                    "No custom profiles available", InfoBarSeverity.Warning);
                return (string.Empty, -1);
            }

            int nextProfileIndex;

            // Определяем текущую позицию
            if (_isVirtualStateActive && _virtualCustomProfileIndex >= 0)
            {
                nextProfileIndex = (_virtualCustomProfileIndex + 1) % _profile.Length;
            }
            else
            {
                if (AppSettings.Preset == -1)
                {
                    // Сейчас активен готовый пресет - начинаем с первого кастомного
                    nextProfileIndex = 0;
                    _virtualCustomProfileIndex = -1; // Чтобы следующий был 0
                    _isVirtualStateActive = true;
                }
                else
                {
                    // Уже выбран кастомный пресет
                    nextProfileIndex = (AppSettings.Preset + 1) % _profile.Length;
                    _virtualCustomProfileIndex = AppSettings.Preset;
                    _isVirtualStateActive = true;
                }
            }

            // Обновляем виртуальную позицию
            _virtualCustomProfileIndex = nextProfileIndex;

            // Проверяем корректность индекса и данных пресета
            if (nextProfileIndex >= 0 && nextProfileIndex < _profile.Length &&
                !string.IsNullOrEmpty(_profile[nextProfileIndex].Profilename))
            {
                var profile = _profile[nextProfileIndex];
                icon = profile.Profileicon;
                desc = profile.Profiledesc;
                return (profile.Profilename, nextProfileIndex);
            }

            MandarinAddNotification("TraceIt_Error".GetLocalized(),
                $"Invalid profile index: {nextProfileIndex}", InfoBarSeverity.Error);
            return (string.Empty, -1);
        }
        catch (Exception ex)
        {
            MandarinAddNotification("TraceIt_Error".GetLocalized(),
                $"Error getting next custom profile: {ex.Message}", InfoBarSeverity.Error);
            return (string.Empty, -1);
        }
    }

    /// <summary>
    ///     Устанавливает готовую строку параметров разгона для применения готового пресета, но не применяет
    /// </summary>
    private void SetPremadePreset(string profileKey)
    {
        try
        {
            // Активируем пресет
            SelectPremadePreset(profileKey);
            AppSettings.Preset = -1; // Устанавливаем флаг готового пресета

            // Получаем данные пресета и обновляем настройки
            var (presetName, _, _, settings, comboName) = PremadedPresets[profileKey];

            AppSettings.RyzenAdjLine = settings;

            SelectRightPresetName(comboName);

            SelectedProfile = presetName.GetLocalized();
        }
        catch (Exception ex)
        {
            MandarinAddNotification("TraceIt_Error".GetLocalized(),
                $"Error applying premade profile: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    /// <summary>
    ///     Устанавливает и применяет готовую строку параметров разгона для применения кастомного пресета
    /// </summary>
    private void ApplyCustomProfile(int profileIndex)
    {
        try
        {
            ProfileLoad();

            if (profileIndex < 0 || profileIndex >= _profile.Length)
            {
                MandarinAddNotification("TraceIt_Error".GetLocalized(),
                    $"Invalid custom profile index: {profileIndex}", InfoBarSeverity.Error);
                return;
            }

            AppSettings.Preset = profileIndex;

            var profile = _profile[profileIndex];

            SelectRightPresetName(profile.Profilename);
            Applyer.ApplyCustomPreset(profile);

            SelectedProfile = profile.Profilename;

            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                var navigationService = App.GetService<INavigationService>();
                if (navigationService.Frame!.GetPageViewModel() is ПараметрыViewModel)
                {
                    navigationService.NavigateTo(typeof(ГлавнаяViewModel).FullName!, null, true);
                    navigationService.NavigateTo(typeof(ПараметрыViewModel).FullName!, null, true);
                }
            });

            AppSettings.SaveSettings();
        }
        catch (Exception ex)
        {
            MandarinAddNotification("TraceIt_Error".GetLocalized(),
                $"Error applying custom profile: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    #endregion

    #region Themes

    /// <summary>
    ///     Инициализирует активную тему приложения
    /// </summary>
    private void InitializeThemes()
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
            if (_themeSelectorService.Themes[AppSettings.ThemeType].ThemeCustomBg)
            {
                var themeBackground = _themeSelectorService.Themes[AppSettings.ThemeType].ThemeBackground;

                if (AppSettings.ThemeType > 2 &&
                    !string.IsNullOrEmpty(themeBackground) &&
                    (themeBackground.Contains("http") || themeBackground.Contains("appx") || File.Exists(themeBackground)))
                {
                    ThemeBackground.ImageSource = new BitmapImage(new Uri(themeBackground));
                }
            }
            else
            {
                ThemeBackground.ImageSource = null;
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

    #endregion

    #region JSON

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
        var next = CallNextHookEx(_hookId, nCode, wParam, lParam); // Передаем нажатие в следующее приложение

        if (nCode >= 0 && wParam == WmKeydown && GetAsyncKeyState(0x11) < 0 &&
            IsAltPressed()) // Проверяем следует ли перехватывать хук и событие нажатия на клавишу Control (0x11), Alt
        {
            _ = HandleKeyboardKeysCallback(Marshal.ReadInt32(lParam));
        }

        return next;
    }

    private async Task HandleKeyboardKeysCallback(int virtualkeyCode)
    {
        switch ((VirtualKey)virtualkeyCode)
        {
            // Переключить между своими пресетами
            case VirtualKey.W:
            {
                var (nextCustomProfile, nextCustomIndex) = GetNextCustomProfile(out var icon1, out var desc1);

                if (!string.IsNullOrEmpty(nextCustomProfile))
                {
                    // Сохраняем информацию о профиле для отложенного применения 
                    lock (_applyDebounceLock)
                    {
                        _pendingProfileToApply = nextCustomProfile;
                        _isCustomProfile = true;
                        _pendingCustomProfileIndex = nextCustomIndex;
                    }

                    await ProfileSwitcher.ProfileSwitcher.ShowOverlay(_themeSelectorService, AppSettings,
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
                var (nextPremadeProfile, nextPremadeKey) = GetNextPremadeProfile(out var icon, out var desc);

                if (!string.IsNullOrEmpty(nextPremadeProfile))
                {
                    // Сохраняем информацию о профиле для отложенного применения 
                    lock (_applyDebounceLock)
                    {
                        _pendingProfileToApply = nextPremadeKey;
                        _isCustomProfile = false;
                    }

                    await ProfileSwitcher.ProfileSwitcher.ShowOverlay(_themeSelectorService, AppSettings,
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
                    await ProfileSwitcher.ProfileSwitcher.ShowOverlay(_themeSelectorService, AppSettings,
                        "RTSS " + "Cooler_Service_Disabled/Content".GetLocalized(), "\uE7AC");

                    AppSettings.RtssMetricsEnabled = false;
                }
                else
                {
                    await ProfileSwitcher.ProfileSwitcher.ShowOverlay(_themeSelectorService, AppSettings,
                        "RTSS " + "Cooler_Service_Enabled/Content".GetLocalized(), "\uE7AC");

                    AppSettings.RtssMetricsEnabled = true;
                }

                AppSettings.SaveSettings();

                MandarinAddNotification("Shell_RTSSChanging".GetLocalized(),
                    "Shell_RTSSChanging_Success".GetLocalized(), InfoBarSeverity.Informational);
                break;
        }
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
                                    SetPremadePreset(profileToApply);
                                    Applyer.ApplyWithoutAdjLine(false);
                                }
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
            }, token);
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

    private void TitleIcon_PointerEntered(object? sender, PointerRoutedEventArgs? e)
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
        AppSettings.FixedTitleBar = _fixedTitleBar;
        AppSettings.SaveSettings();
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
        if (AppSettings.FixedTitleBar)
        {
            TitleIcon_PointerEntered(null, null);
            _fixedTitleBar = true;
        }
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