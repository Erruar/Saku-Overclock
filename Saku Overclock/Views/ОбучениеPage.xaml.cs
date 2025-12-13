using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Services;
using Saku_Overclock.ViewModels;
using Windows.Foundation.Metadata;
using Saku_Overclock.JsonContainers;

namespace Saku_Overclock.Views;

// ReSharper disable once RedundantExtendsListEntry
public sealed partial class ОбучениеPage : Page
{
    private static readonly IAppNotificationService NotificationsService = App.GetService<IAppNotificationService>(); // Класс с уведомлениями
    private static readonly IAppSettingsService
        AppSettings = App.GetService<IAppSettingsService>(); // Настройки приложения

    public ОбучениеPage()
    {
        InitializeComponent();
        RunIntroSequence();
    }

    public static void ShowNavbarAndControls()
    {
        NotificationsService.Notifies ??= [];
        NotificationsService.Notifies.Add(new Notify
        {
            Title = "ExitFirstLaunch",
            Msg = "DEBUG MESSAGE",
            Type = InfoBarSeverity.Informational
        });
        NotificationsService.SaveNotificationsSettings();
        MainWindow.Set_ContextMenu_Tray();
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

        var formattedText = UpdateChecker.FormatReleaseNotesAsRichText("LicenseText".GetLocalized());
        LicenseText.Children.Add(formattedText);
        showLicenseSection.Begin();
    }
    
    private async void RunIntroSequence()
    {
        try
        {
            await Task.Delay(1000);

            var hideAnimationAndShowLogo = new Storyboard();
            {
                var fadeOut = new DoubleAnimation
                {
                    To = 0,
                    BeginTime = TimeSpan.FromSeconds(0),
                    Duration = new Duration(TimeSpan.FromSeconds(1.25)),
                    EnableDependentAnimation = true
                };
                var fadeIn = new DoubleAnimation
                {
                    To = 1,
                    BeginTime = TimeSpan.FromSeconds(1.25),
                    Duration = new Duration(TimeSpan.FromSeconds(1.25)),
                    EnableDependentAnimation = true
                };
                Storyboard.SetTarget(fadeIn, WelcomeLogoImage);
                Storyboard.SetTargetProperty(fadeIn, "Opacity");

                Storyboard.SetTarget(fadeOut, WelcomeLogoIntro);
                Storyboard.SetTargetProperty(fadeOut, "Opacity");
                hideAnimationAndShowLogo.Children.Add(fadeOut);
                hideAnimationAndShowLogo.Children.Add(fadeIn);

                hideAnimationAndShowLogo.Completed += (_, _) =>
                {
                    WelcomeLogoIntro.Stop();
                    WelcomeLogoIntro.Source = null;
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                };
            }


            var rotateImageLogoAndAppearText = new Storyboard();
            {
                var rotateKeyFrames = new DoubleAnimationUsingKeyFrames
                {
                    EnableDependentAnimation = true
                };

                rotateKeyFrames.KeyFrames.Add(new EasingDoubleKeyFrame
                {
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0)),
                    Value = -360
                });
                rotateKeyFrames.KeyFrames.Add(new EasingDoubleKeyFrame
                {
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.6)),
                    Value = -10
                });
                rotateKeyFrames.KeyFrames.Add(new EasingDoubleKeyFrame
                {
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.2)),
                    Value = 0
                });

                var rotateIn1 = new DoubleAnimation
                {
                    To = 0,
                    BeginTime = TimeSpan.FromSeconds(2.1),
                    Duration = new Duration(TimeSpan.FromSeconds(0.5)),
                    EnableDependentAnimation = true
                };

                var scaleX = new DoubleAnimation
                {
                    To = 200 / LogoAnimation.ActualHeight,
                    BeginTime = TimeSpan.FromSeconds(0),
                    Duration = new Duration(TimeSpan.FromSeconds(1.5)),
                    EnableDependentAnimation = true
                };
                var scaleY = new DoubleAnimation
                {
                    To = 200 / LogoAnimation.ActualHeight,
                    BeginTime = TimeSpan.FromSeconds(0),
                    Duration = new Duration(TimeSpan.FromSeconds(1.5)),
                    EnableDependentAnimation = true
                };
                var fadeIn = new DoubleAnimation
                {
                    To = 1,
                    BeginTime = TimeSpan.FromSeconds(1),
                    Duration = new Duration(TimeSpan.FromSeconds(0.5)),
                    EnableDependentAnimation = true
                };

                Storyboard.SetTarget(rotateKeyFrames, LogoImageRotateTransform);
                Storyboard.SetTargetProperty(rotateKeyFrames, "Angle");

                Storyboard.SetTarget(rotateIn1, LogoTextRotateTransform);
                Storyboard.SetTargetProperty(rotateIn1, "Angle");

                Storyboard.SetTarget(scaleX, LogoImageScaleTransform);
                Storyboard.SetTargetProperty(scaleX, "ScaleX");

                Storyboard.SetTarget(scaleY, LogoImageScaleTransform);
                Storyboard.SetTargetProperty(scaleY, "ScaleY");

                var logoTranslate = new DoubleAnimation
                {
                    To = -(LogoAnimation.ActualWidth * 0.4),
                    BeginTime = TimeSpan.FromSeconds(0),
                    Duration = new Duration(TimeSpan.FromSeconds(1.0)),
                    EnableDependentAnimation = true
                };
                Storyboard.SetTarget(logoTranslate, LogoImageTranslateTransform);
                Storyboard.SetTargetProperty(logoTranslate, "X");
                rotateImageLogoAndAppearText.Children.Add(logoTranslate);

                var logoTextTranslate = new DoubleAnimation
                {
                    From = 140,
                    To = -30,
                    BeginTime = TimeSpan.FromSeconds(1),
                    Duration = new Duration(TimeSpan.FromSeconds(0.7)),
                    EnableDependentAnimation = true
                };
                Storyboard.SetTarget(logoTextTranslate, LogoTextTranslateTransform);
                Storyboard.SetTargetProperty(logoTextTranslate, "Y");
                rotateImageLogoAndAppearText.Children.Add(logoTextTranslate);

                Storyboard.SetTarget(fadeIn, LogoText);
                Storyboard.SetTargetProperty(fadeIn, "Opacity");

                rotateImageLogoAndAppearText.Children.Add(rotateKeyFrames);
                rotateImageLogoAndAppearText.Children.Add(rotateIn1);
                rotateImageLogoAndAppearText.Children.Add(scaleX);
                rotateImageLogoAndAppearText.Children.Add(scaleY);
                rotateImageLogoAndAppearText.Children.Add(fadeIn);
            }

            var hideLogoTextAndAppearWelcome = new Storyboard();
            {
                var fadeOut2 = new DoubleAnimation
                {
                    To = 0,
                    BeginTime = TimeSpan.FromSeconds(0),
                    Duration = new Duration(TimeSpan.FromSeconds(0.5)),
                    EnableDependentAnimation = true
                };
                var fadeIn2 = new DoubleAnimation
                {
                    To = 1,
                    BeginTime = TimeSpan.FromSeconds(0.5),
                    Duration = new Duration(TimeSpan.FromSeconds(1.0)),
                    EnableDependentAnimation = true
                };
                var fadeIn3 = new DoubleAnimation
                {
                    To = 1,
                    BeginTime = TimeSpan.FromSeconds(1.5),
                    Duration = new Duration(TimeSpan.FromSeconds(0.5)),
                    EnableDependentAnimation = true
                };
                var fadeIn4 = new DoubleAnimation
                {
                    To = 1,
                    BeginTime = TimeSpan.FromSeconds(2.0),
                    Duration = new Duration(TimeSpan.FromSeconds(0.5)),
                    EnableDependentAnimation = true
                };

                Storyboard.SetTarget(fadeOut2, LogoText);
                Storyboard.SetTargetProperty(fadeOut2, "Opacity");

                Storyboard.SetTarget(fadeIn2, AfterAnimationPanel);
                Storyboard.SetTargetProperty(fadeIn2, "Opacity");

                Storyboard.SetTarget(fadeIn3, SakuText);
                Storyboard.SetTargetProperty(fadeIn3, "Opacity");

                Storyboard.SetTarget(fadeIn4, OverclockText);
                Storyboard.SetTargetProperty(fadeIn4, "Opacity");

                var logoTranslate1 = new DoubleAnimation
                {
                    To = -(LogoAnimation.ActualHeight * 0.2),
                    BeginTime = TimeSpan.FromSeconds(0),
                    Duration = new Duration(TimeSpan.FromSeconds(1.0)),
                    EnableDependentAnimation = true
                };
                var textTranslate = new DoubleAnimation
                {
                    To = 0,
                    BeginTime = TimeSpan.FromSeconds(1.5),
                    Duration = new Duration(TimeSpan.FromSeconds(0.5)),
                    EnableDependentAnimation = true
                };
                var textTranslate1 = new DoubleAnimation
                {
                    To = 0,
                    BeginTime = TimeSpan.FromSeconds(2.0),
                    Duration = new Duration(TimeSpan.FromSeconds(0.5)),
                    EnableDependentAnimation = true
                };
                Storyboard.SetTarget(logoTranslate1, LogoImageTranslateTransform);
                Storyboard.SetTargetProperty(logoTranslate1, "Y");

                Storyboard.SetTarget(textTranslate, SakuTextTranslateTransform);
                Storyboard.SetTargetProperty(textTranslate, "Y");

                Storyboard.SetTarget(textTranslate1, OverclockTextTranslateTransform);
                Storyboard.SetTargetProperty(textTranslate1, "Y");

                hideLogoTextAndAppearWelcome.Children.Add(logoTranslate1);
                hideLogoTextAndAppearWelcome.Children.Add(textTranslate);
                hideLogoTextAndAppearWelcome.Children.Add(textTranslate1);
                hideLogoTextAndAppearWelcome.Children.Add(fadeIn2);
                hideLogoTextAndAppearWelcome.Children.Add(fadeIn3);
                hideLogoTextAndAppearWelcome.Children.Add(fadeIn4);
                hideLogoTextAndAppearWelcome.Children.Add(fadeOut2);

            }

            var hideAllAndGoToLicense = new Storyboard();
            {
                var fadeOut3 = new DoubleAnimation
                {
                    To = 0,
                    BeginTime = TimeSpan.FromSeconds(0),
                    Duration = new Duration(TimeSpan.FromSeconds(1)),
                    EnableDependentAnimation = true
                };
                var fadeOut4 = new DoubleAnimation
                {
                    To = 0,
                    BeginTime = TimeSpan.FromSeconds(2),
                    Duration = new Duration(TimeSpan.FromSeconds(0.3)),
                    EnableDependentAnimation = true
                };
                var logoTranslate2 = new DoubleAnimation
                {
                    To = 0,
                    BeginTime = TimeSpan.FromSeconds(2),
                    Duration = new Duration(TimeSpan.FromSeconds(0.3)),
                    EnableDependentAnimation = true
                };

                Storyboard.SetTarget(fadeOut3, AfterAnimationPanel);
                Storyboard.SetTargetProperty(fadeOut3, "Opacity");

                Storyboard.SetTarget(fadeOut4, LogoAnimation);
                Storyboard.SetTargetProperty(fadeOut4, "Opacity");

                Storyboard.SetTarget(logoTranslate2, LogoImageTranslateTransform);
                Storyboard.SetTargetProperty(logoTranslate2, "Y");

                hideAllAndGoToLicense.Children.Add(fadeOut3);
                hideAllAndGoToLicense.Children.Add(fadeOut4);
                hideAllAndGoToLicense.Children.Add(logoTranslate2);
            }

            IAnimatedVisualSource2 newIntro = new AnimatedVisuals.SakuLogo();
            {
                WelcomeLogoIntro.Source = newIntro;
                WelcomeLogoIntro.AnimationOptimization = PlayerAnimationOptimization.Resources;

                await WelcomeLogoIntro.PlayAsync(0, 0.0001d, false);

                await Task.Delay(TimeSpan.FromSeconds(0.1));

                await WelcomeLogoIntro.PlayAsync(0, 350d / 373d, false);

                WelcomeLogoIntro.Pause();
                WelcomeLogoIntro.SetProgress(350d / 373d);

                await Task.Delay(TimeSpan.FromSeconds(0.5));
                hideAnimationAndShowLogo.Begin();

                await Task.Delay(TimeSpan.FromSeconds(3));
                rotateImageLogoAndAppearText.Begin();

                await Task.Delay(TimeSpan.FromSeconds(4.5));
                AfterAnimationPanel.Visibility = Visibility.Visible;
                hideLogoTextAndAppearWelcome.Begin();

                await Task.Delay(TimeSpan.FromSeconds(5.5));
                hideAllAndGoToLicense.Begin();

                await Task.Delay(TimeSpan.FromSeconds(4.5));
                OpenLicenseSection();

                WelcomeLogoIntro.Visibility = Visibility.Collapsed;
                LogoText.Visibility = Visibility.Collapsed;
                LogoAnimation.Visibility = Visibility.Collapsed;
                AfterAnimationPanel.Visibility = Visibility.Collapsed;
            }

        }
        catch
        {
            // Игнорим
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
        var SkipDialog = new ContentDialog
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
            SkipDialog.XamlRoot = XamlRoot;
        }
        var result = await SkipDialog.ShowAsync();
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
