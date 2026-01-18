using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Services;
using Saku_Overclock.ViewModels;
using Windows.Foundation.Metadata;
using VisualTreeHelper = Saku_Overclock.Helpers.VisualTreeHelper;

namespace Saku_Overclock.Views;

// ReSharper disable once RedundantExtendsListEntry
public sealed partial class ОбучениеPage : Page
{
    private static readonly IAppNotificationService NotificationsService = App.GetService<IAppNotificationService>(); // Уведомления
    private static readonly ITrayMenuService TrayMenuService = App.GetService<ITrayMenuService>(); // Управление треем
    private static readonly INotesWriterService NotesWriterService = App.GetService<INotesWriterService>(); // Управление треем
    private static readonly IThemeSelectorService ThemeSelectorService = App.GetService<IThemeSelectorService>(); // Темы приложения
    private static readonly IAppSettingsService
        AppSettings = App.GetService<IAppSettingsService>(); // Настройки приложения
    private bool _isLoaded;

    public ОбучениеPage()
    {
        InitializeComponent();
        TrayMenuService.SetMinimalMode();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        RunIntroSequence();
        LoadQuickSettings();
    }

    private void LoadQuickSettings()
    {
        try
        {
            AutoStartComboBox.SelectedIndex = AppSettings.AutostartType is > -1 and < 4 ? AppSettings.AutostartType : 0;
            AppHideTypeComboBox.SelectedIndex =
                AppHideTypeComboBox.SelectedIndex is > -1 and < 3 ? AppSettings.HidingType : 2;

            ApplyStart.IsOn = AppSettings.ReapplyLatestSettingsOnAppLaunch;
            AutoCheckUpdates.IsOn = AppSettings.CheckForUpdates;
            AutoReapply.IsOn = AppSettings.ReapplyOverclock;
            AutoReapplyNumberBox.Value = AppSettings.ReapplyOverclockTimer;
            AutoReapplyNumberBoxPanel.Visibility = AutoReapply.IsOn ? Visibility.Visible : Visibility.Collapsed;
            InitializeThemeSettings();
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex);
        }    
    }

    /// <summary>
    ///     Загружает параметры тем приложения
    /// </summary>
    private void InitializeThemeSettings()
    {
        ThemeComboBox.Items.Clear();

        try
        {
            // Проверяем, что есть темы для загрузки
            if (ThemeSelectorService.Themes.Count == 0)
            {
                return;
            }

            foreach (var theme in ThemeSelectorService.Themes)
            {
                try
                {
                    // Локализуем только темы с префиксом "Theme_"
                    var displayName = theme.ThemeName.Contains("Theme_")
                        ? theme.ThemeName.GetLocalized()
                        : theme.ThemeName;

                    ThemeComboBox.Items.Add(displayName);
                }
                catch
                {
                    ThemeComboBox.Items.Add(theme.ThemeName);
                }
            }

            // Проверяем индекс темы
            if (AppSettings.ThemeType < 0 || AppSettings.ThemeType >= ThemeSelectorService.Themes.Count)
            {
                // Сбрасываем на дефолтную тему
                AppSettings.ThemeType = 0;
                AppSettings.SaveSettings();
            }

            // Загружаем параметры выбранной темы
            var selectedTheme = ThemeSelectorService.Themes[AppSettings.ThemeType];

            // Устанавливаем выбранную тему
            ThemeComboBox.SelectedIndex = AppSettings.ThemeType;
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex);

            // Сбрасываем на безопасные значения
            AppSettings.ThemeType = 0;
            AppSettings.SaveSettings();
        }
    }

    /// <summary>
    ///     Применяет выбранную тему из ThemeComboBox
    /// </summary>
    private void ThemesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        AppSettings.ThemeType = ThemeComboBox.SelectedIndex > -1 ? ThemeComboBox.SelectedIndex : 0;
        AppSettings.SaveSettings();

        if (ThemeSelectorService.Themes.Count == 0)
        {
            ThemeSelectorService.SetThemeAsync(ElementTheme.Default); // Список пуст -> системная тема
            return;
        }

        if (AppSettings.ThemeType < 0 ||
            AppSettings.ThemeType >= ThemeSelectorService.Themes.Count) // Защита от некорректного индекса
        {
            AppSettings.ThemeType = 0;
            AppSettings.SaveSettings();
        }

        var selectedTheme = ThemeSelectorService.Themes[AppSettings.ThemeType];

        if (AppSettings.ThemeType == 0)
        {
            ThemeSelectorService.SetThemeAsync(ElementTheme.Default);
        }
        else
        {
            ThemeSelectorService.SetThemeAsync(selectedTheme.ThemeLight
                ? ElementTheme.Light
                : ElementTheme.Dark); // Переключение состояния темы, на светлую, тёмную
        }

        // Обновляем UI-элементы
        UpdateTheme();
    }

    /// <summary>
    ///     Обновляет тему приложения в реальном времени
    /// </summary>
    private static void UpdateTheme()
    {
        NotificationsService.ShowNotification("Theme applied!",
            "DEBUG MESSAGE",
            InfoBarSeverity.Success);
    }

    /// <summary>
    ///     Изменяет тип автозагрузки
    /// </summary>
    private void AutoStartComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        AppSettings.AutostartType = AutoStartComboBox.SelectedIndex;
        if (AutoStartComboBox.SelectedIndex is 2 or 3)
        {
            AutoStartHelper.SetStartupTask();
        }
        else
        {
            AutoStartHelper.RemoveStartupTask();
        }

        AppSettings.SaveSettings();
    }

    /// <summary>
    ///     Изменяет тип скрытия приложения в трей
    /// </summary>
    private void AppHideType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        AppSettings.HidingType = AppHideTypeComboBox.SelectedIndex;
        AppSettings.SaveSettings();
    }

    /// <summary>
    ///     Изменяет состояние переприменения последних применённых параметров разгона при запуске программы (включены,
    ///     выключены)
    /// </summary>
    private void ApplyOptionsOnStart_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        AppSettings.ReapplyLatestSettingsOnAppLaunch = ApplyStart.IsOn;

        AppSettings.SaveSettings();
    }

    /// <summary>
    ///     Изменяет состояние переприменение последних применённых параметров каждые несколько секунд (включено, выключено)
    /// </summary>
    private void AutoReapplyOptionsEverySeconds_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (AutoReapply.IsOn)
        {
            AutoReapplyNumberBoxPanel.Visibility = Visibility.Visible;
            AppSettings.ReapplyOverclock = true;
            AppSettings.ReapplyOverclockTimer = AutoReapplyNumberBox.Value;
        }
        else
        {
            AutoReapplyNumberBoxPanel.Visibility = Visibility.Collapsed;
            AppSettings.ReapplyOverclock = false;
            AppSettings.ReapplyOverclockTimer = 3;
        }

        AppSettings.SaveSettings();
    }

    /// <summary>
    ///     Изменяет состояние переприменение последних применённых параметров каждые несколько секунд (время переприменения)
    /// </summary>
    private void AutoReapplyOptionsEverySecondsNumberBox_ValueChanged(NumberBox sender,
        NumberBoxValueChangedEventArgs args)
    {
        try
        {
            if (!_isLoaded)
            {
                return;
            }

            AppSettings.ReapplyOverclock = true;
            AppSettings.ReapplyOverclockTimer = AutoReapplyNumberBox.Value;
            AppSettings.SaveSettings();
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex);
        }
    }

    /// <summary>
    ///     Изменяет состояние автоматической проверки наличия обновлений программы (включено, выключено)
    /// </summary>
    private void AutoCheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        AppSettings.CheckForUpdates = AutoCheckUpdates.IsOn;

        AppSettings.SaveSettings();
    }

    /// <summary>
    ///     Центрует текст в AutoReapplyNumberBox
    /// </summary>
    private void AutoReapplyOptionsEverySecondsNumberBox_Loaded(object sender, RoutedEventArgs e)
    {
        var texts = VisualTreeHelper.FindVisualChildren<ScrollContentPresenter>(AutoReapplyNumberBox);
        foreach (var text in texts)
        {
            text.Margin = new Thickness(12, 7, 0, 0);
        }

        var contents = VisualTreeHelper.FindVisualChildren<ContentControl>(AutoReapplyNumberBox);
        foreach (var content in contents)
        {
            var presents = VisualTreeHelper.FindVisualChildren<ContentPresenter>(content);
            foreach (var present in presents)
            {
                var texts1 = VisualTreeHelper.FindVisualChildren<TextBlock>(present);
                foreach (var text in texts1)
                {
                    text.Margin = new Thickness(0, 2, 0, 0);
                }
            }
        }
    }

    /// <summary>
    ///     Изменяет состояние AutoReapplyNumberBox
    /// </summary>
    private void AutoReapplyOptionsEverySeconds_FocusEngaged(object sender, object args) =>
        AutoReapplyNumberBox.SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden;

    /// <summary>
    ///     Изменяет состояние AutoReapplyNumberBox
    /// </summary>
    private void AutoReapplyOptionsEverySeconds_FocusDisengaged(object sender, object args) =>
        AutoReapplyNumberBox.SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline;

    /// <summary>
    ///     Изменяет состояние привязанных ToggleSwitch
    /// </summary>
    private void ToggleTheSwitchByTag(object sender, object e)
    {
        if (sender is FrameworkElement { Tag: string targetName })
        {
            // Ищем элемент по имени на текущей странице и меняем его состояние
            var targetToggle = FindName(targetName) as ToggleSwitch;
            if (targetToggle != null)
            {
                targetToggle.IsOn = !targetToggle.IsOn;
            }
        }
    }

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
                LogHelper.TraceIt_TraceError(ex);
                return;
            }

            if (sliderObject is Slider slider)
            {
                if (slider.Maximum < sender.Value)
                {
                    slider.Maximum = ПараметрыPage.FromValueToUpperFive(sender.Value);
                }
            }
        }
    }

    public static void ShowNavbarAndControls()
    {
        NotificationsService.ShowNotification("ExitFirstLaunch",
            "DEBUG MESSAGE",
            InfoBarSeverity.Informational);
        TrayMenuService.RestoreDefaultMenu();
    }
    
    private void OpenLicenseSection()
    {
        Pager.SelectedPageIndex = 0;
        Pager.Visibility = Visibility.Visible;

        LicenseSection.Opacity = 0;
        LicenseSection.Visibility = Visibility.Visible;

        var showLicenseSection = new Storyboard();
        {
            var fadeIn = new DoubleAnimation
            {
                To = 1,
                BeginTime = TimeSpan.FromSeconds(0.5),
                Duration = new Duration(TimeSpan.FromSeconds(1.0)),
                EnableDependentAnimation = true
            };

            Storyboard.SetTarget(fadeIn, LicenseSection);
            Storyboard.SetTargetProperty(fadeIn, "Opacity");

            showLicenseSection.Children.Add(fadeIn);
        }

        var formattedText = NotesWriterService.FormatReleaseNotesAsRichText("LicenseText".GetLocalized());
        LicenseText.Children.Add(formattedText);
        showLicenseSection.Begin();
    }

    private async void RunIntroSequence()
    {
        try
        {
            // ------------------------------------------------------------
            // 1. ЗАПУСК LOTTIE (БОЛЬШОЕ ЛОГО)
            // ------------------------------------------------------------
            // Загружаем анимацию
            IAnimatedVisualSource2 newIntro = new AnimatedVisuals.SakuLogo();
            WelcomeLogoIntro.Source = newIntro;

            // "Прогрев" первого кадра (опционально, убирает моргание)
            await WelcomeLogoIntro.PlayAsync(0, 0.0001d, false);
            await Task.Delay(50); // Даем потоку отрисовки вздохнуть

            // Играем основную часть (Рисование)
            // Логотип сейчас огромный (500x500) по центру
            await WelcomeLogoIntro.PlayAsync(0, 350d / 373d, false);
            WelcomeLogoIntro.Pause();

            // Пауза "Наслаждения брендом"
            await Task.Delay(TimeSpan.FromSeconds(0.6));

            // ------------------------------------------------------------
            // 2. БЕЗОПАСНАЯ ПОДМЕНА (Lottie -> PNG)
            // ------------------------------------------------------------
            // Мы делаем это ДО движения, чтобы не перегружать рендерер.

            var hideStoryBoard = new Storyboard();

            var hideDuration = new Duration(TimeSpan.FromSeconds(1.1)); // Чуть больше секунды
            var animHide = new DoubleAnimation
            {
                To = 0,
                Duration = hideDuration
            };

            var animShow = new DoubleAnimation
            {
                To = 1,
                Duration = hideDuration
            };

            Storyboard.SetTarget(animHide, WelcomeLogoIntro);
            Storyboard.SetTargetProperty(animHide, "Opacity");

            Storyboard.SetTarget(animShow, WelcomeLogoImage);
            Storyboard.SetTargetProperty(animShow, "Opacity");

            // Добавляем в сториборд
            hideStoryBoard.Children.Add(animHide);
            hideStoryBoard.Children.Add(animShow);

            hideStoryBoard.Begin();

            await Task.Delay(TimeSpan.FromSeconds(1.1));

            WelcomeLogoIntro.Source = null; // Выгружаем ресурсы (теперь безопасно)

            // ------------------------------------------------------------
            // 3. АНИМАЦИЯ: "КИНЕТИЧЕСКИЙ РАЗЪЕЗД"
            // ------------------------------------------------------------

            var moveStoryboard = new Storyboard();

            // QuinticEase EaseOut - "Apple Curve". 
            // Очень резкий старт и очень долгая, мягкая остановка.
            var quintEase = new QuinticEase { EasingMode = EasingMode.EaseOut };
            var duration = new Duration(TimeSpan.FromSeconds(1.1)); // Чуть больше секунды

            // === А. Логотип: Уменьшение ===
            // Уменьшаем с 1.0 (500px) до 0.4 (200px) - подбери scale по вкусу
            var animScaleX = new DoubleAnimation
            {
                To = 0.4,
                Duration = duration,
                EasingFunction = quintEase
            };
            var animScaleY = new DoubleAnimation
            {
                To = 0.4,
                Duration = duration,
                EasingFunction = quintEase
            };

            // === Б. Логотип: Сдвиг Влево ===
            // Сдвигаем влево, чтобы освободить место справа
            var animMoveLogo = new DoubleAnimation
            {
                To = -160,
                Duration = duration,
                EasingFunction = quintEase
            };

            // === В. Текст: Сдвиг Вправо (Навстречу) ===
            // Текст "выезжает" из-за логотипа вправо. 
            // Он был на -50, станет на 140 (справа от лого)
            var animMoveText = new DoubleAnimation
            {
                To = 130,
                Duration = duration,
                EasingFunction = quintEase
            };

            var animMoveGrid = new DoubleAnimation
            {
                To = -50,
                Duration = duration,
                EasingFunction = quintEase
            };

            // === Г. Текст: Проявление ===
            // Появляется чуть позже начала движения
            var animFadeText = new DoubleAnimation
            {
                To = 1,
                BeginTime = TimeSpan.FromSeconds(0.15),
                Duration = new Duration(TimeSpan.FromSeconds(0.8))
                // Тут Easing не обязателен, линейное появление смотрится чисто
            };

            // --- Привязки ---

            // Лого Scale
            Storyboard.SetTarget(animScaleX, LogoScale);
            Storyboard.SetTargetProperty(animScaleX, "ScaleX");
            Storyboard.SetTarget(animScaleY, LogoScale);
            Storyboard.SetTargetProperty(animScaleY, "ScaleY");

            // Лого Move
            Storyboard.SetTarget(animMoveLogo, LogoTranslate);
            Storyboard.SetTargetProperty(animMoveLogo, "X");

            // Текст Move
            Storyboard.SetTarget(animMoveText, TextTransform);
            Storyboard.SetTargetProperty(animMoveText, "X");

            Storyboard.SetTarget(animMoveGrid, GridTransform);
            Storyboard.SetTargetProperty(animMoveGrid, "X");

            // Текст Fade
            Storyboard.SetTarget(animFadeText, TextPanel);
            Storyboard.SetTargetProperty(animFadeText, "Opacity");

            // Добавляем в сториборд
            moveStoryboard.Children.Add(animScaleX);
            moveStoryboard.Children.Add(animScaleY);
            moveStoryboard.Children.Add(animMoveLogo);
            moveStoryboard.Children.Add(animMoveText);
            moveStoryboard.Children.Add(animMoveGrid);
            moveStoryboard.Children.Add(animFadeText);

            moveStoryboard.Begin();

            // Ждем чтения (3-4 секунды)
            await Task.Delay(3500);

            // ------------------------------------------------------------
            // 4. ФИНАЛ: УХОД НА ЛИЦЕНЗИЮ
            // ------------------------------------------------------------
            var exitStoryboard = new Storyboard();

            // Все плавно растворяется
            var fadeOutAll = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromSeconds(0.5)
            };

            Storyboard.SetTarget(fadeOutAll, CenterContent);
            Storyboard.SetTargetProperty(fadeOutAll, "Opacity");

            exitStoryboard.Children.Add(fadeOutAll);
            exitStoryboard.Begin();

            await Task.Delay(500);

            // Переход
            OpenLicenseSection();

            // Скрываем, чтобы не мешало кликам
            CenterContent.Visibility = Visibility.Collapsed;

        }
        catch (Exception ex)
        {
            // Логирование, если нужно. 
            // В продакшене лучше не игнорировать полностью, но для UI анимации - безопасно.
            System.Diagnostics.Debug.WriteLine($"Animation Error: {ex.Message}");
        }
    }


    public async void AcceptButton_Click(object sender, RoutedEventArgs e)
    {
        if (LicenseAcceptButton.IsChecked == false)
        {
            AcceptErrTeachingTip.IsOpen = true;
            return;
        }

        try
        {
            await ChangeSection(LicenseSection, QuickSettings);
        }
        catch (Exception ex) 
        {
            await LogHelper.LogError(ex);
        }
    }

    private async Task ChangeSection(Grid from, Grid to)
    {

        to.Visibility = Visibility.Visible;
        var storyboard = new Storyboard();
        var fadeOut = new DoubleAnimation
        {
            To = 0,
            BeginTime = TimeSpan.FromSeconds(0),
            Duration = new Duration(TimeSpan.FromSeconds(0.9)),
            EnableDependentAnimation = true
        };
        var fadeIn = new DoubleAnimation
        {
            To = 1,
            BeginTime = TimeSpan.FromSeconds(0.9),
            Duration = new Duration(TimeSpan.FromSeconds(0.9)),
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(fadeIn, to);
        Storyboard.SetTargetProperty(fadeIn, "Opacity");

        Storyboard.SetTarget(fadeOut, from);
        Storyboard.SetTargetProperty(fadeOut, "Opacity");
        storyboard.Children.Add(fadeOut);
        storyboard.Children.Add(fadeIn);
        storyboard.Begin();

        Pager.SelectedPageIndex += 1;
        await Task.Delay(TimeSpan.FromSeconds(1.8));

        from.Visibility = Visibility.Collapsed;
    }

    private async void DisagreeTraining_Click(object sender, RoutedEventArgs e)
    {
        var skipDialog = new ContentDialog
        {
            Title = "Пропустить диагностику?",
            Content = "Вы всегда сможете создать пресеты с OC Finder позже",
            CloseButtonText = "CancelThis/Text".GetLocalized(),
            PrimaryButtonText = "Да, пропустить",
            DefaultButton = ContentDialogButton.Close
        };
        // Use this code to associate the dialog to the appropriate AppWindow by setting
        // the dialog's XamlRoot to the same XamlRoot as an element that is already present in the AppWindow.
        if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
        {
            skipDialog.XamlRoot = XamlRoot;
        }
        var result = await skipDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ShowNavbarAndControls();
            var navigationService = App.GetService<INavigationService>();
            navigationService.NavigateTo(typeof(ГлавнаяViewModel).FullName!, null, true);
        }
    }

    private void AcceptTraining_Click(object sender, RoutedEventArgs e)
    {
        
            ShowNavbarAndControls();
            var navigationService = App.GetService<INavigationService>();
            navigationService.NavigateTo(typeof(ГлавнаяViewModel).FullName!, null, true);
    }

    private async void QuickSettingsDone_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await ChangeSection(QuickSettings, TrainingSection);
        }
        catch (Exception ex)
        {
            await LogHelper.LogError(ex);
        }
    }

    private void TrainingDone_Click(object sender, RoutedEventArgs e)
    {
        ShowNavbarAndControls();
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ГлавнаяViewModel).FullName!);
    }

    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ApplyTeach.IsOpen)
            {
                ApplyTeach.IsOpen = false;
                await Task.Delay(300);
            }
            ApplyTeach.Title = "Apply_Success".GetLocalized();
            ApplyTeach.Subtitle = "";
            ApplyTeach.IconSource = new SymbolIconSource { Symbol = Symbol.Accept };
            ApplyTeach.IsOpen = true;
            await Task.Delay(3000);
            ApplyTeach.IsOpen = false;
        }
        catch (Exception ex)
        {
            await LogHelper.LogError(ex);
        }
    }
}
