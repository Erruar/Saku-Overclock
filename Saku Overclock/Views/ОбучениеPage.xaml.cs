using System.Diagnostics;
using System.Numerics;
using Windows.UI.Text;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.Win32;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Services;
using Saku_Overclock.Shared;
using Saku_Overclock.Shared.Models;
using Saku_Overclock.ViewModels;
using InfoBarSeverity = Microsoft.UI.Xaml.Controls.InfoBarSeverity;
using VisualTreeHelper = Saku_Overclock.Helpers.VisualTreeHelper;

namespace Saku_Overclock.Views;

public sealed partial class ОбучениеPage
{
    private static readonly IAppNotificationService NotificationsService = App.GetService<IAppNotificationService>(); // Уведомления
    private static readonly ITrayMenuService TrayMenuService = App.GetService<ITrayMenuService>(); // Управление треем
    private static readonly INotesWriterService NotesWriterService = App.GetService<INotesWriterService>(); // Управление треем
    private static readonly ILogger<ОбучениеPage> Logger = App.GetService<ILogger<ОбучениеPage>>(); // Логирование
    private static readonly CoreGatewayService CoreGatewayService = App.GetService<CoreGatewayService>(); // Состояние сервиса

    private bool _testFailed;
    
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
        LoadOcFinderTestPrompts();
        LoadDefaultPresets();
    }

    private void LoadOcFinderTestPrompts()
    {
        try
        {
            var name = GetSystemInfo.ReadCpuInformation().name.TrimEnd();
            OcFinderProcessorName.Text = name.Length > 35 ? name[..35] : name;
            if (CoreGatewayService.IsServiceUnavailable)
            {
                OcFinderServiceState.Text = "FirstLaunchServiceStopped/Text".GetLocalized();
                OcFinderServiceStateGlyph.Glyph = "\uEA39";
                
                OcFinderProcessorSupported.Text = "FirstLaunchProcessorUnknown/Text".GetLocalized();
                OcFinderProcessorStateGlyph.Glyph = "\uE9CE";
                _testFailed = true;
            }
            else
            {
                OcFinderServiceState.Text = "FirstLaunchServiceRunning/Text".GetLocalized();
                if (CoreGatewayService.IsAvailable)
                {
                    if (CoreGatewayService.GetCodenameGeneration() != CodenameGeneration.Unknown)
                    {
                        OcFinderProcessorSupported.Text = "FirstLaunchProcessorSupported/Text".GetLocalized();
                        OcFinderProcessorStateGlyph.Glyph = "\uEC61";
                    }
                }
                else
                {
                    OcFinderServiceState.Text = "FirstLaunchServiceError/Text".GetLocalized();
                    OcFinderServiceStateGlyph.Glyph = "\uEA39";
                    _testFailed = true;
                }
            }

            using var subKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO");
            if (subKey != null && TryParseVersion(subKey.GetValue("DisplayVersion"), out var parsed) && parsed != null)
                OcFinderDriverState.Text = "FirstLaunchDriverInstalled/Text".GetLocalized() + " " + parsed;
            else
            {
                OcFinderDriverState.Text = "FirstLaunchDriverNotInstalled/Text".GetLocalized();
                OcFinderDriverStateGlyph.Glyph = "\uEA39";
                _testFailed = true;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("LoadOcFinderTestPrompts failed {ex}", ex);
        }
    }
    
    private static bool TryParseVersion(object? val, out Version? version)
    {
        version = null;
        if (val != null)
        {
            try
            {
                version = new Version((val as string)!);
                return true;
            }
            catch (ArgumentException) { }
            catch (FormatException) { }
            catch (OverflowException) { }
        }
        return false;
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
        NotesWriterService.UpdateReleaseNotesBrushes(AccentTextBrush.Background,
                TextSecondaryBrush.Background,
                ControlStrongBrush.Background);
        var formattedText = NotesWriterService.FormatReleaseNotesAsRichText("LicenseText".GetLocalized());
        LicenseText.Children.Add(formattedText);
        showLicenseSection.Begin();
    }

    private async void RunIntroSequence()
    {
        try
        {
            // 1. ЗАПУСК LOTTIE (БОЛЬШОЕ ЛОГО)
            IAnimatedVisualSource2 newIntro = new AnimatedVisuals.SakuLogo();
            WelcomeLogoIntro.Source = newIntro;

            // "Прогрев" первого кадра (убирает моргание)
            await WelcomeLogoIntro.PlayAsync(0, 0.0001d, false);
            await Task.Delay(50); 

            // Играем основную часть 
            // Логотип сейчас огромный (500x500) по центру
            await WelcomeLogoIntro.PlayAsync(0, 350d / 373d, false);
            WelcomeLogoIntro.Pause();

            // Пауза "Наслаждения брендом"
            await Task.Delay(TimeSpan.FromSeconds(0.6));

            // 2. БЕЗОПАСНАЯ ПОДМЕНА (Lottie -> PNG)

            var hideStoryBoard = new Storyboard();

            var hideDuration = new Duration(TimeSpan.FromSeconds(1.1)); 
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

            hideStoryBoard.Children.Add(animHide);
            hideStoryBoard.Children.Add(animShow);

            hideStoryBoard.Begin();

            await Task.Delay(TimeSpan.FromSeconds(1.1));

            WelcomeLogoIntro.Source = null;

            // 3. АНИМАЦИЯ: "КИНЕТИЧЕСКИЙ РАЗЪЕЗД"

            var moveStoryboard = new Storyboard();

            var quintEase = new QuinticEase { EasingMode = EasingMode.EaseOut };
            var duration = new Duration(TimeSpan.FromSeconds(1.1));

            // 3.1. Логотип: Уменьшение
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

            // 3.2. Логотип: Сдвиг Влево
            var animMoveLogo = new DoubleAnimation
            {
                To = -160,
                Duration = duration,
                EasingFunction = quintEase
            };

            // 3.3. Текст: Сдвиг Вправо 
            // Текст "выезжает" из-за логотипа вправо
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

            // 3.4. Текст: Проявление
            // Появляется чуть позже начала движения
            var animFadeText = new DoubleAnimation
            {
                To = 1,
                BeginTime = TimeSpan.FromSeconds(0.15),
                Duration = new Duration(TimeSpan.FromSeconds(0.8))
            };

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

            moveStoryboard.Children.Add(animScaleX);
            moveStoryboard.Children.Add(animScaleY);
            moveStoryboard.Children.Add(animMoveLogo);
            moveStoryboard.Children.Add(animMoveText);
            moveStoryboard.Children.Add(animMoveGrid);
            moveStoryboard.Children.Add(animFadeText);

            moveStoryboard.Begin();

            await Task.Delay(3500);

            // 4. ФИНАЛ: УХОД НА ЛИЦЕНЗИЮ
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

            OpenLicenseSection();

            CenterContent.Visibility = Visibility.Collapsed;

        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Animation Error: {ex.Message}");
        }
    }

    private void LoadDefaultPresets()
    {
        PresetCustom.Children.Clear();
        var presets = new Preset[]
        {
            new() { PresetName = "Preset_SmartPreset".GetLocalized(), PresetIcon = "\uF4A5" },
            new() { PresetName = "Preset_PowerfulPreset".GetLocalized(), PresetIcon = "\uEC4A" },
        };
        for (var i = 0; i < presets.Length; i++)
        {
            var preset = presets[i];
            var isChecked = i == 0;

            var presetName = preset.PresetName;
            
            var toggleButton = new ToggleButton
            {
                Tag = i,
                Margin = new Thickness(0, 7, 0, 0),
                CornerRadius = new CornerRadius(16),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Shadow = new ThemeShadow(),
                Translation = new Vector3(0, 0, 20),
                IsChecked = isChecked,
                Content = new Grid
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(-5, -5, -4, -5),
                    Children =
                    {
                        new FontIcon
                        {
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Margin = new Thickness(7, 0, 0, 0),
                            Glyph = preset.PresetIcon == string.Empty ? "\uE718" : preset.PresetIcon
                        },
                        new TextBlock
                        {
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Margin = new Thickness(36, 5, 5, 5),
                            FontWeight = new FontWeight(500),
                            Text = presetName
                        }
                    }
                }
            };
            toggleButton.Checked += ToggleButton_Checked;
            toggleButton.Unchecked += ToggleButton_Unchecked;
            toggleButton.Unloaded += (_, _) =>
            {
                toggleButton.Checked -= ToggleButton_Checked;
                toggleButton.Unchecked -= ToggleButton_Unchecked;
            };
            PresetCustom.Children.Add(toggleButton);
        }
    }

    private bool _userSwitchPreset;
    private string
        _doubleClickApplyPrev =
            string.Empty;

    /// <summary>
    ///     Запрещает отмену выделения ToggleButton пресета
    /// </summary>
    private void ToggleButton_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_userSwitchPreset)
        {
            return;
        }

        ((ToggleButton)sender).IsChecked = true;
    }

    /// <summary>
    ///     Обработка выбора и активации пресета
    /// </summary>
    private async void ToggleButton_Checked(object sender, RoutedEventArgs e)
    {
        try
        {
            _userSwitchPreset = true;
            var toggleButtons = VisualTreeHelper.FindVisualChildren<ToggleButton>(PresetPivot);
            foreach (var button in toggleButtons)
            {
                if (button != sender as ToggleButton)
                {
                    button.IsChecked = false;
                }
            }

            var name = string.Empty;
            var desc = string.Empty;
            var toggle = sender as ToggleButton;
            var textBlocks = VisualTreeHelper.FindVisualChildren<TextBlock>(toggle!);
            foreach (var block in textBlocks)
            {
                if (block.FontWeight == new FontWeight(700))
                {
                    name = block.Text;
                }
                else
                {
                    if (block.TextWrapping == TextWrapping.Wrap)
                    {
                        desc = block.Text;
                    }
                }
            }

            if (_doubleClickApplyPrev == name + desc + toggle?.Tag)
            {
                ApplyButton_Click(null, null);
            }

            _doubleClickApplyPrev = name + desc + toggle?.Tag;
            if (toggle?.Tag is string)
            {
            }
            else
            {
            }

            await Task.Delay(20);
            _userSwitchPreset = false;
        }
        catch (Exception ex)
        {
            Logger.LogError("Preset switch toggle failed {ex}", ex);
        }
    }

    private async void AcceptButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_testFailed)
            {
                OcFinderContinueUsingRow.Height = new GridLength(0);
                OcFinderExit.RemoveFromParent();
                OcFinderContinue.Content = "FirstLaunchNextStage/Content".GetLocalized();
                OcFinderSystemReadyState.Text = "FirstLaunchOcFinderPreparingPresetsDesc/Text".GetLocalized();
            }
            await ChangeSection(LicenseSection, OcFinderScanSection);
        }
        catch (Exception ex) 
        {
            await LogHelper.LogError(ex);
        }
    }

    private static async Task ChangeSection(Grid from, Grid to)
    {

        to.Visibility = Visibility.Visible;
        var storyboard = new Storyboard();
        var fadeOut = new DoubleAnimation
        {
            To = 0,
            BeginTime = TimeSpan.FromSeconds(0),
            Duration = new Duration(TimeSpan.FromSeconds(0.8)),
            EnableDependentAnimation = true
        };
        var fadeIn = new DoubleAnimation
        {
            To = 1,
            BeginTime = TimeSpan.FromSeconds(0.9),
            Duration = new Duration(TimeSpan.FromSeconds(0.8)),
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(fadeIn, to);
        Storyboard.SetTargetProperty(fadeIn, "Opacity");

        Storyboard.SetTarget(fadeOut, from);
        Storyboard.SetTargetProperty(fadeOut, "Opacity");
        storyboard.Children.Add(fadeOut);
        storyboard.Children.Add(fadeIn);
        storyboard.Begin();

        await Task.Delay(TimeSpan.FromSeconds(1.7));

        from.Visibility = Visibility.Collapsed;
    }

    private void TrainingDone_Click(object sender, RoutedEventArgs e)
    {
        FirstRunManager.PassFirstRun();
        ShowNavbarAndControls();
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ГлавнаяViewModel).FullName!);
    }

    private async void ApplyButton_Click(object? sender, RoutedEventArgs? e)
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

    private void ExitLicenseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Application.Current.Exit();
    }

    private void ShowFullLicense_OnClick(object sender, RoutedEventArgs e)
    {
        FullLicenseText.Visibility = Visibility.Visible;
    }

    private void CloseFullLicense_OnClick(object sender, RoutedEventArgs e)
    {
        FullLicenseText.Visibility = Visibility.Collapsed;
    }

    private async void OcFinderContinue_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var failed = _testFailed;
            if ((sender as Button)!.Name == "OcFinderExit") failed = !failed;
            if (failed) Application.Current.Exit();
            else await ChangeSection(OcFinderScanSection, TrainingSection);
            
        }
        catch (Exception ex)
        {
            Logger.LogError("OcFinder Continue failed {ex}", ex);
        }
    }

    private void OcFinderAboutPreparing_OnClick(object sender, RoutedEventArgs e) => Process.Start(
        new ProcessStartInfo("https://github.com/Erruar/Saku-Overclock/wiki/Supported%E2%80%90Models") { UseShellExecute = true });

    private async void TrainingAgree_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await ChangeSection(TrainingSection, TrainingSectionThreeModes);
        }
        catch (Exception ex)
        {
            Logger.LogError("TrainingAgree failed {ex}", ex);
        }
    }

    private async void TrainingThreeModesBack_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await ChangeSection(TrainingSectionThreeModes, TrainingSection);
        }
        catch (Exception ex)
        {
            Logger.LogError("TrainingAgree failed {ex}", ex);
        }
    }

    private async void TrainingThreeModesNext_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await ChangeSection(TrainingSectionThreeModes, TrainingSectionSafeLimits);
        }
        catch (Exception ex)
        {
            Logger.LogError("TrainingAgree failed {ex}", ex);
        }
    }

    private async void TrainingSafeLimitsNext_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await ChangeSection(TrainingSectionSafeLimits, TrainingSectionPresetsApply);
        }
        catch (Exception ex)
        {
            Logger.LogError("TrainingAgree failed {ex}", ex);
        }
    }

    private async void TrainingSafeLimitsBack_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await ChangeSection(TrainingSectionSafeLimits, TrainingSectionThreeModes);
        }
        catch (Exception ex)
        {
            Logger.LogError("TrainingAgree failed {ex}", ex);
        }
    }

    private async void TrainingPresetsApplyBack_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await ChangeSection(TrainingSectionPresetsApply, TrainingSectionSafeLimits);
        }
        catch (Exception ex)
        {
            Logger.LogError("TrainingAgree failed {ex}", ex);
        }
    }
}
