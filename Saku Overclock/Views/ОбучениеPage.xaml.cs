using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.ViewModels;
using Windows.Foundation.Metadata;

namespace Saku_Overclock.Views;

// ReSharper disable once RedundantExtendsListEntry
public sealed partial class ОбучениеPage : Page
{
    private static readonly IAppNotificationService NotificationsService = App.GetService<IAppNotificationService>(); // Уведомления
    private static readonly ITrayMenuService TrayMenuService = App.GetService<ITrayMenuService>(); // Управление треем
    private static readonly INotesWriterService NotesWriterService = App.GetService<INotesWriterService>(); // Управление треем
    private static readonly IAppSettingsService
        AppSettings = App.GetService<IAppSettingsService>(); // Настройки приложения

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
        RunIntroSequence();
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
                To = -100,
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


    public void AcceptButton_Click(object sender, RoutedEventArgs e)
    {
        if (LicenseAcceptButton.IsChecked == false)
        {
            AcceptErrTeachingTip.IsOpen = true;
            return;
        }
        TrainingSection.Visibility = Visibility.Visible;
        var storyboard = new Storyboard();
        var fadeOut = new DoubleAnimation
        {
            To = 0,
            BeginTime = TimeSpan.FromSeconds(0),
            Duration = new Duration(TimeSpan.FromSeconds(1.5)),
            EnableDependentAnimation = true
        };
        var fadeIn = new DoubleAnimation
        {
            To = 1,
            BeginTime = TimeSpan.FromSeconds(1.5),
            Duration = new Duration(TimeSpan.FromSeconds(1.5)),
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(fadeIn, TrainingSection);
        Storyboard.SetTargetProperty(fadeIn, "Opacity");

        Storyboard.SetTarget(fadeOut, LicenseSection);
        Storyboard.SetTargetProperty(fadeOut, "Opacity");
        storyboard.Children.Add(fadeOut);
        storyboard.Children.Add(fadeIn);
        storyboard.Begin();
        storyboard.Completed += (_, _) =>
        {
            LicenseSection.Visibility = Visibility.Collapsed;
            Pager.SelectedPageIndex = 2;
        };
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
}
