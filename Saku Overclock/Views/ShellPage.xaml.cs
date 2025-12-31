using System.Diagnostics;
using Windows.Foundation;
using Windows.Graphics;
using Windows.System;
using Windows.UI.Text;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.JsonContainers;
using Saku_Overclock.ViewModels;
using Action = System.Action;
using Button = Microsoft.UI.Xaml.Controls.Button;
using Task = System.Threading.Tasks.Task;
using WindowActivatedEventArgs = Microsoft.UI.Xaml.WindowActivatedEventArgs;

// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable

namespace Saku_Overclock.Views;

public sealed partial class ShellPage
{
   
    private DispatcherTimer? _dispatcherTimer; // Таймер обновления уведомлений
    private bool _isNotificationPanelShow; // Флаг: Открыта ли панель уведомлений
    private int? _compareList; // Нет новых уведомлений - пока


    private static readonly IAppNotificationService
        NotificationsService = App.GetService<IAppNotificationService>(); // Уведомления

    private static readonly IKeyboardHotkeysService KeyboardHotkeys = App.GetService<IKeyboardHotkeysService>();
    private static readonly IUpdateCheckerService UpdateChecker = App.GetService<IUpdateCheckerService>();

    private static readonly IAppSettingsService
        AppSettings = App.GetService<IAppSettingsService>(); // Настройки приложения

    private static readonly IPresetManagerService
        PresetManager = App.GetService<IPresetManagerService>(); // Настройки приложения

    private Preset[]? _preset = []; // Класс с пресетами параметров разгона пользователя

    private AppWindow MAppWindow
    {
        get;
    }

    private bool _fixedTitleBar; // Флаг фиксированного тайтлбара 
    private bool _isTitleBarInitialized; // Флаг инициализации тайтлбара 
    private readonly IThemeSelectorService _themeSelectorService = App.GetService<IThemeSelectorService>();

    public ShellViewModel ViewModel // ViewModel, установка нужной модели для UI страницы
    {
        get;
    }

    public ShellPage(ShellViewModel viewModel)
    {
        MAppWindow = App.MainWindow.AppWindow; // AppWindow, нужен для тайтлбара приложения
        ViewModel = viewModel;
        InitializeComponent();
        ViewModel.NavigationService.Frame = NavigationFrame; // Выбранная пользователем страница
        ViewModel.NavigationViewService.Initialize(NavigationViewControl); // Инициализировать выбор страниц
        Loaded += OnLoaded;

        App.MainWindow.ExtendsContentIntoTitleBar = true;
        App.MainWindow.SetTitleBar(AppTitleBar);
        App.MainWindow.Activated += MainWindow_Activated; // Приложение активировалось, загрузить параметры TitleBar
    }

    #region Page Initialization and User Presets

    #region App TitleBar Initialization

    #region App loading and TitleBar

    /// <summary>
    ///     Загрузка приложения
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
       /* if (AppSettings.AppFirstRun)
        {
            HideNavigationBar();
            Icon.Visibility = Visibility.Collapsed;
            RingerNotificationGrid.Visibility = Visibility.Collapsed;
        }*/
        TitleBarHelper.UpdateTitleBar(RequestedTheme);
        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.Left, VirtualKeyModifiers.Menu));
        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.GoBack));
        KeyboardHotkeys.Initialize();
        StartNotificationsUpdate();
        _ = UpdateThemes();
        AutoStartHelper.AutoStartCheckAndFix();
    }

    /// <summary>
    ///     Обработчик активации окна
    /// </summary>
    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        App.AppTitlebar = VersionNumberIndicator;
        AppTitleBar.Loaded += AppTitleBar_Loaded;
    }

    /// <summary>
    ///     Помогает установить регион взаимодействия с программой (кликабельную кнопку уведомлений и лого)
    /// </summary>
    private void AppTitleBar_Loaded(object sender, RoutedEventArgs e) =>
        SetRegionsForCustomTitleBar(); //Установить регион взаимодействия

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

    #region Notifications Update Timers

    /// <summary>
    ///     Начинает проверку наличия новых уведомлений
    /// </summary>
    private void StartNotificationsUpdate()
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
        if (_isNotificationPanelShow)
        {
            return Task.CompletedTask;
        }

        if (NotificationsService.Notifies == null)
        {
            return Task.CompletedTask;
        }

        _ = (DispatcherQueue?.TryEnqueue(() =>
        {
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
                        _ = UpdateThemes();
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
                                      UpdateChecker.ParseVersion();
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
                }

                if (notify1.Msg.Contains("DELETEUNAVAILABLE"))
                {
                    if (notify1.Type != InfoBarSeverity.Success)
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
                                _preset ??= new Preset[1];
                                var commandActions = new Dictionary<string, Action>
                                {
                                    {
                                        "Param_SMU_Func_Text/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].SmuFunctionsEnabl = false
                                    },
                                    {
                                        "Param_CPU_c2/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Cpu2 = false
                                    },
                                    {
                                        "Param_VRM_v2/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Vrm2 = false
                                    },
                                    {
                                        "Param_VRM_v1/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Vrm1 = false
                                    },
                                    {
                                        "Param_CPU_c1/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Cpu1 = false
                                    },
                                    {
                                        "Param_ADV_a15/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Advncd15 = false
                                    },
                                    {
                                        "Param_ADV_a11/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Advncd11 = false
                                    },
                                    {
                                        "Param_ADV_a12/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Advncd12 = false
                                    },
                                    {
                                        "Param_CO_O1/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Coall = false
                                    },
                                    {
                                        "Param_CO_O2/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Cogfx = false
                                    },
                                    {
                                        "Param_CCD1_CO_Section/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Coprefmode = 0
                                    },
                                    {
                                        "Param_ADV_a14_E/Content".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Advncd14 = false
                                    },
                                    {
                                        "Param_CPU_c5/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Cpu5 = false
                                    },
                                    {
                                        "Param_CPU_c3/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Cpu3 = false
                                    },
                                    {
                                        "Param_CPU_c4/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Cpu4 = false
                                    },
                                    {
                                        "Param_CPU_c6/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Cpu6 = false
                                    },
                                    {
                                        "Param_CPU_c7/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Cpu7 = false
                                    },
                                    {
                                        "Param_ADV_a6/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Advncd6 = false
                                    },
                                    {
                                        "Param_VRM_v4/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Vrm4 = false
                                    },
                                    {
                                        "Param_VRM_v3/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Vrm3 = false
                                    },
                                    {
                                        "Param_ADV_a1/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Advncd1 = false
                                    },
                                    {
                                        "Param_ADV_a3/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Advncd3 = false
                                    },
                                    {
                                        "Param_VRM_v7/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Vrm7 = false
                                    },
                                    {
                                        "Param_ADV_a4/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Advncd4 = false
                                    },
                                    {
                                        "Param_ADV_a5/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Advncd5 = false
                                    },
                                    {
                                        "Param_ADV_a10/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Advncd10 = false
                                    },
                                    {
                                        "Param_ADV_a13_E/Content".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Advncd13 = false
                                    },
                                    {
                                        "Param_ADV_a13_U/Content".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Advncd13 = false
                                    },
                                    {
                                        "Param_ADV_a8/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Advncd8 = false
                                    },
                                    {
                                        "Param_ADV_a7/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Advncd7 = false
                                    },
                                    {
                                        "Param_VRM_v5/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Vrm5 = false
                                    },
                                    {
                                        "Param_VRM_v6/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Vrm6 = false
                                    },
                                    {
                                        "Param_ADV_a9/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Advncd9 = false
                                    },
                                    {
                                        "Param_GPU_g12/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Gpu12 = false
                                    },
                                    {
                                        "Param_GPU_g11/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Gpu11 = false
                                    },
                                    {
                                        "Param_GPU_g10/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Gpu10 = false
                                    },
                                    {
                                        "Param_GPU_g9/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Gpu9 = false
                                    },
                                    {
                                        "Param_GPU_g2/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Gpu2 = false
                                    },
                                    {
                                        "Param_GPU_g1/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Gpu1 = false
                                    },
                                    {
                                        "Param_GPU_g4/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Gpu4 = false
                                    },
                                    {
                                        "Param_GPU_g3/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Gpu3 = false
                                    },
                                    {
                                        "Param_GPU_g6/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Gpu6 = false
                                    },
                                    {
                                        "Param_GPU_g5/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Gpu5 = false
                                    },
                                    {
                                        "Param_GPU_g8/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Gpu8 = false
                                    },
                                    {
                                        "Param_GPU_g7/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Gpu7 = false
                                    },
                                    {
                                        "Param_GPU_g15/Text".GetLocalized(),
                                        () =>
                                        {
                                        }
                                    },
                                    {
                                        "Param_GPU_g16/Text".GetLocalized(),
                                        () => _preset[AppSettings.Preset].Gpu16 = false
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

                                PresetManager.SaveSettings();

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

                if (index > 8) // Если 9 уведомлений - очистить для оптимизации производительности
                {
                    ClearAllNotification(NotificationPanelClearAllBtn, null); // Удалить все уведомления
                    return;
                }

                index++;
            }

            _compareList = NotificationsService.Notifies.Count;
        }));
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

    #region Themes

    /// <summary>
    ///     Инициализирует активную тему приложения
    /// </summary>
    private async Task UpdateThemes()
    {
        if (AppSettings.ThemeType < 0)
        {
            AppSettings.ThemeType = 0;
            AppSettings.SaveSettings();
        }

        var themeApplyResult = await _themeSelectorService.UpdateAppliedTheme(AppSettings.ThemeType);

        ThemeBackground.ImageSource = themeApplyResult.BackgroundImageSource;
        ThemeOpacity.Opacity = themeApplyResult.ThemeOpacity;
        ThemeMaskOpacity.Opacity = themeApplyResult.ThemeMaskOpacity;
    }

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

    /// <summary>
    ///     Устанавливает интерактивные области в TitleBar окна, меняет состояние лого
    /// </summary>
    private void SetRegionsForCustomTitleBar()
    {
        if (_isTitleBarInitialized)
        {
            return;
        }

        if (AppSettings.FixedTitleBar)
        {
            TitleIcon_PointerEntered(null, null);
            _fixedTitleBar = true;
        }

        var scaleAdjustment = AppTitleBar.XamlRoot.RasterizationScale;

        try
        {
            const int titleIconActualWidth = 120;
            const int titleIconActualHeight = 48;
            const int RingerNotificationGridActualSize = 32;
            const int RingerNotificationPositionX = 956;
            const int RingerNotificationPositionY = 9;

            var rightInset = MAppWindow.TitleBar.RightInset;
            var leftInset = MAppWindow.TitleBar.LeftInset;
            if (rightInset < 0)
            {
                rightInset = 138;
            }
            if (leftInset < 0)
            {
                leftInset = 0;
            }

            RightPaddingColumn.Width = new GridLength(rightInset / scaleAdjustment);
            LeftPaddingColumn.Width = new GridLength(leftInset / scaleAdjustment);

            var transform = TitleIcon.TransformToVisual(null);

            var bounds = new Rect()
            {
                Height = titleIconActualHeight,
                Width = titleIconActualWidth,
                X = titleIconActualHeight,
                Y = 0
            };

            var searchBoxRect = GetRect(bounds, scaleAdjustment);

            transform = RingerNotificationGrid.TransformToVisual(null);

            bounds = new Rect() 
            {
                Height = RingerNotificationGridActualSize,
                Width = RingerNotificationGridActualSize,
                X = RingerNotificationPositionX,
                Y = RingerNotificationPositionY
            };
            
            var ringerNotifRect = GetRect(bounds, scaleAdjustment);

            var rectArray = new[] { searchBoxRect, ringerNotifRect };

            var nonClientInputSrc = InputNonClientPointerSource.GetForWindowId(MAppWindow.Id);
            nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, rectArray);
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex);
        }

        _isTitleBarInitialized = true;
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

    private void ClearAllNotification(object? sender, RoutedEventArgs? args)
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
                Task.Delay(100);
            }

            if (NotificationContainer.Children.Count == 0 && sender != null)
            {
                Task.Delay(500);
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
            LogHelper.TraceIt_TraceError(ex);
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

    private void ShowHideNotificationLostFocusBackground(bool show)
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
                Task.Delay(200);
                NotificationLostFocusBackground.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex);
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