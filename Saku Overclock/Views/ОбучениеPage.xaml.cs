using Accord.Math.Geometry;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.ViewModels;
using Windows.Foundation.Metadata;
namespace Saku_Overclock.Views;

public sealed partial class ОбучениеPage : Page
{
    private static readonly IAppNotificationService NotificationsService = App.GetService<IAppNotificationService>(); // Класс с уведомлениями

    public ОбучениеPage()
    {
        InitializeComponent();
        HideNavbarAndControls();
    }
    private static void HideNavbarAndControls()
    {
        NotificationsService.Notifies ??= [];
        NotificationsService.Notifies.Add(new()
        {
            Title = "FirstLaunch",
            Msg = "DEBUG MESSAGE",
            Type = InfoBarSeverity.Informational
        });
        NotificationsService.SaveNotificationsSettings();
        MainWindow.Remove_ContextMenu_Tray();
    }
    public static void ShowNavbarAndControls()
    {
        NotificationsService.Notifies ??= [];
        NotificationsService.Notifies.Add(new()
        {
            Title = "ExitFirstLaunch",
            Msg = "DEBUG MESSAGE",
            Type = InfoBarSeverity.Informational
        });
        NotificationsService.SaveNotificationsSettings();
        MainWindow.Set_ContextMenu_Tray();
    }
    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        // Create animations for both circles
        var storyboard = new Storyboard();

        // Scale animation for Circle1
        var scaleX = new DoubleAnimation
        {
            To = 8*700,
            Duration = new Duration(TimeSpan.FromSeconds(2.5)),
            EnableDependentAnimation = true
        };

        var scaleY = new DoubleAnimation
        {
            To = 8*700,
            Duration = new Duration(TimeSpan.FromSeconds(2.5)),
            EnableDependentAnimation = true
        };

        Storyboard.SetTarget(scaleX, Circle1);
        Storyboard.SetTargetProperty(scaleX, "Height");

        Storyboard.SetTarget(scaleY, Circle1);
        Storyboard.SetTargetProperty(scaleY, "Width");

        storyboard.Children.Add(scaleX);
        storyboard.Children.Add(scaleY);

        // Opacity animation
        var fadeOut = new DoubleAnimation
        {
            To = 0,
            BeginTime = TimeSpan.FromSeconds(2.5),
            Duration = new Duration(TimeSpan.FromSeconds(1)),
            EnableDependentAnimation = true
        };
        var fadeOut1 = new DoubleAnimation
        {
            To = 0,
            BeginTime = TimeSpan.FromSeconds(2.5),
            Duration = new Duration(TimeSpan.FromSeconds(1)),
            EnableDependentAnimation = true
        };
        var fadeOut2 = new DoubleAnimation
        {
            To = 0,
            BeginTime = TimeSpan.FromSeconds(2.5),
            Duration = new Duration(TimeSpan.FromSeconds(1)),
            EnableDependentAnimation = true
        };
        var fadeOut3 = new DoubleAnimation
        {
            To = 0,
            BeginTime = TimeSpan.FromSeconds(0),
            Duration = new Duration(TimeSpan.FromSeconds(1)),
            EnableDependentAnimation = true
        };

        // Rotation
        var rotateOut = new DoubleAnimation
        {
            To = -90,
            BeginTime = TimeSpan.FromSeconds(0),
            Duration = new Duration(TimeSpan.FromSeconds(1)),
            EnableDependentAnimation = true
        };

        Storyboard.SetTarget(fadeOut, Circle1);
        Storyboard.SetTargetProperty(fadeOut, "Opacity");

        Storyboard.SetTarget(fadeOut1, Circle2);
        Storyboard.SetTargetProperty(fadeOut1, "Opacity");

        Storyboard.SetTarget(fadeOut2, StartAnimation);
        Storyboard.SetTargetProperty(fadeOut2, "Opacity");

        Storyboard.SetTarget(fadeOut3, LogoAnimation);
        Storyboard.SetTargetProperty(fadeOut3, "Opacity");

        Storyboard.SetTarget(rotateOut, LogoAnimation);
        Storyboard.SetTargetProperty(rotateOut, "(UIElement.RenderTransform).(CompositeTransform.Rotation)");

        storyboard.Children.Add(fadeOut);
        storyboard.Children.Add(fadeOut1);
        storyboard.Children.Add(fadeOut2);
        storyboard.Children.Add(fadeOut3);
        storyboard.Children.Add(rotateOut);

        // Start the storyboard
        storyboard.Begin();
        storyboard.Completed += (_, _) => 
        {
            Pager.SelectedPageIndex = 1;
            Circle2.Visibility = Visibility.Collapsed;
            LicenseSection.Visibility = Visibility.Visible;
            var formattedText = ГлавнаяPage.ApplyMarkdownStyles("LicenseText".GetLocalized());
            foreach (var paragraph in formattedText)
            {
                LicenseText.Children.Add(paragraph);
            }
        };
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
            Title = "Пропустить обучение?",
            Content = "Вы не сможете вернуться к обучению в дальнейшем!",
            CloseButtonText = "Cancel".GetLocalized(),
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

    private async void AcceptTraining_Click(object sender, RoutedEventArgs e)
    {
        var SureDialog = new ContentDialog
        {
            Title = "Перед тем как начнём",
            Content = "Обучение будет происходить не на вашем оборудовании, а на интерфейсе программы. Можно включать/выключать, применять, трогать любые параметры и изучать программу. Никакого влияения на ваше оборудование не будет до конца обучения",
            CloseButtonText = "Всё-таки ещё подумаю",
            PrimaryButtonText = "Приступим!",
            DefaultButton = ContentDialogButton.Primary
        };
        // Use this code to associate the dialog to the appropriate AppWindow by setting
        // the dialog's XamlRoot to the same XamlRoot as an element that is already present in the AppWindow.
        if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
        {
            SureDialog.XamlRoot = XamlRoot;
        }
        var result = await SureDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ShowNavbarAndControls();
            var navigationService = App.GetService<INavigationService>();
            navigationService.NavigateTo(typeof(ГлавнаяViewModel).FullName!, null, true);
        }
    }
}
