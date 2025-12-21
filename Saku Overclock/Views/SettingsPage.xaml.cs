using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Foundation.Metadata;
using Windows.UI;
using Windows.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.JsonContainers;
using Saku_Overclock.Styles;
using Saku_Overclock.ViewModels;
using Saku_Overclock.Wrappers;
using static System.Environment;
using TextGetOptions = Microsoft.UI.Text.TextGetOptions;
using TextSetOptions = Microsoft.UI.Text.TextSetOptions;
using VisualTreeHelper = Saku_Overclock.Helpers.VisualTreeHelper;

namespace Saku_Overclock.Views;

public sealed partial class SettingsPage
{
    /// <summary>
    ///     VievModel для отображения версии программы и типа билда
    /// </summary>
    public SettingsViewModel ViewModel
    {
        get;
    }

    private static readonly IAppSettingsService
        AppSettings = App.GetService<IAppSettingsService>(); // Настройки приложения

    private readonly IThemeSelectorService
        _themeSelectorService = App.GetService<IThemeSelectorService>(); // Темы приложения

    private static readonly IAppNotificationService
        NotificationsService = App.GetService<IAppNotificationService>(); // Уведомления

    private static readonly ISendSmuCommandService
        SendSmuCommand =
            App.GetService<ISendSmuCommandService>(); // SendSmuService для определения состояния безопасного применения параметров разгона

    private static readonly IRtssSettingsService
        RtssSettings = App.GetService<IRtssSettingsService>(); // Настройки RTSS

    private NiIconsSettings _niicons = new(); // TrayMon иконки
    private bool _isLoaded; // Флаг загрузки страницы

    public SettingsPage()
    {
        ViewModel = App.GetService<SettingsViewModel>();
        InitializeComponent();
        InitializePage();
        Loaded += LoadedApp;
    }

    #region Initialization

    /// <summary>
    ///     Приложение загружено - разрешить изменения UI
    /// </summary>
    private void LoadedApp(object sender, RoutedEventArgs e) => _isLoaded = true;

    /// <summary>
    ///     Главный метод инициализации страницы
    /// </summary>
    private void InitializePage()
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
            SafeReapply.IsOn = AppSettings.ReapplySafeOverclock;
            ThemeType.Visibility = AppSettings.ThemeType > 7 ? Visibility.Visible : Visibility.Collapsed;
            ThemeCustomBg.Visibility = AppSettings.ThemeType > 7 ? Visibility.Visible : Visibility.Collapsed;
            RtssSettingsEnable.IsOn = AppSettings.RtssMetricsEnabled;
            RtssAdvancedCodeEditor.IsOn = RtssSettings.IsAdvancedCodeEditorEnabled;
            EnableKeybindingsSetting.IsOn = AppSettings.HotkeysEnabled;

            InitializeRtss();
            InitializeThemeSettings();
            InitializeTrayMonIcons();
        }
        catch (Exception e)
        {
            LogHelper.TraceIt_TraceError(e);
        }
    }

    /// <summary>
    ///     Загружает настройки Rtss
    /// </summary>
    private void InitializeRtss()
    {
        var rtssVisibility = RtssSettingsEnable.IsOn ? Visibility.Visible : Visibility.Collapsed;
        SettingsRtssEnableName.Visibility = rtssVisibility;
        RtssGridView.Visibility = rtssVisibility;
        RtssAdvancedCodeEditor.Visibility = rtssVisibility;
        RtssAdvancedCodeEditorEditBox.Visibility =
            RtssAdvancedCodeEditor.IsOn ? rtssVisibility : Visibility.Collapsed;
        RtssAdvancedCodeEditorGrid.CornerRadius = RtssAdvancedCodeEditor.IsOn
            ? new CornerRadius(15, 15, 0, 0)
            : new CornerRadius(15);

        LoadAndFormatAdvancedCodeEditor(RtssSettings.AdvancedCodeEditor);

        // Проход по элементам RTSS_Elements
        LoadRtssElements();
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
            if (_themeSelectorService.Themes.Count == 0)
            {
                return;
            }

            foreach (var theme in _themeSelectorService.Themes)
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
            if (AppSettings.ThemeType < 0 || AppSettings.ThemeType >= _themeSelectorService.Themes.Count)
            {
                // Сбрасываем на дефолтную тему
                AppSettings.ThemeType = 0;
                AppSettings.SaveSettings();
            }

            // Загружаем параметры выбранной темы
            var selectedTheme = _themeSelectorService.Themes[AppSettings.ThemeType];
            ThemeOpacity.Value = selectedTheme.ThemeOpacity;
            ThemeMaskOpacity.Value = selectedTheme.ThemeMaskOpacity;
            AdvancedThemeOptions.IsOn = selectedTheme.ThemeCustom;
            ThemeCustomBg.IsOn = selectedTheme.ThemeCustomBg;

            UpdateThemeCustomInterface();

            // Устанавливаем выбранную тему
            ThemeComboBox.SelectedIndex = AppSettings.ThemeType;

            // Настройка типа темы - тёмный, светлый, показываем только для тем пользователя (больше 7)
            ThemeType.Visibility = AppSettings.ThemeType > 7
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex);

            // Сбрасываем на безопасные значения
            AppSettings.ThemeType = 0;
            AppSettings.SaveSettings();
        }
    }

    #endregion

    #region JSON

    /// <summary>
    ///     Сохранение настроек TrayMon
    /// </summary>
    private void NiSave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(GetFolderPath(SpecialFolder.Personal),
                "SakuOverclock"));
            File.WriteAllText(
                GetFolderPath(SpecialFolder.Personal) + @"\SakuOverclock\niicons.json",
                JsonConvert.SerializeObject(_niicons, Formatting.Indented));
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex);
        }
    }

    /// <summary>
    ///     Загрузка настроек TrayMon
    /// </summary>
    private void NiLoad()
    {
        try
        {
            _niicons = JsonConvert.DeserializeObject<NiIconsSettings>(File.ReadAllText(
                GetFolderPath(SpecialFolder.Personal) + @"\SakuOverclock\niicons.json"))!;
        }
        catch
        {
            _niicons = new NiIconsSettings();
            NiSave();
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    ///     Вспомогательный метод для преобразования HEX в Windows.UI.Color
    /// </summary>
    private static Color ParseColor(string hex)
    {
        if (hex.Length == 6)
        {
            return Color.FromArgb(255,
                byte.Parse(hex[..2], NumberStyles.HexNumber),
                byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber),
                byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber));
        }

        return Color.FromArgb(255, 255, 255, 255); // если цвет неизвестен
    }

    /// <summary>
    ///     Вспомогательный метод для загрузки своей строки в Rtss AdvancedCodeEditor
    /// </summary>
    private void LoadAndFormatAdvancedCodeEditor(string advancedCode)
    {
        if (string.IsNullOrEmpty(advancedCode))
        {
            return;
        }

        RtssAdvancedCodeEditorEditBox.Document.SetText(TextSetOptions.None,
            advancedCode.Replace("<Br>", "\n").TrimEnd());
    }

    /// <summary>
    ///     Открывает ссылку на дискорд сообщество
    /// </summary>
    private void Discord_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://discord.com/invite/yVsKxqAaa7") { UseShellExecute = true });

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

    /// <summary>
    ///     Отображает Flyout возле элемента на который нажал пользователь
    /// </summary>
    private void ShowTrayMonIconColorPickerFlyout_PointerPressed(object sender, PointerRoutedEventArgs e) =>
        SettingsNiColorPicker.Flyout.ShowAt(SettingsNiColorPicker);

    /// <summary>
    ///     Обновляет отображение расширенных параметров темы
    /// </summary>
    private void UpdateThemeCustomInterface()
    {
        if (!AdvancedThemeOptions.IsOn)
        {
            ThemeOpacity.Visibility = Visibility.Collapsed;
            ThemeMaskOpacity.Visibility = Visibility.Collapsed;
            ThemeMaskOpacity.Visibility = Visibility.Collapsed;
            ThemeCustomBg.Visibility = Visibility.Collapsed;
            ThemeType.Visibility = Visibility.Collapsed;
            ThemeBgButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            ThemeOpacity.Visibility = Visibility.Visible;
            ThemeMaskOpacity.Visibility = Visibility.Visible;
            ThemeMaskOpacity.Visibility = Visibility.Visible;
            ThemeCustomBg.Visibility = AppSettings.ThemeType > 7 ? Visibility.Visible : Visibility.Collapsed;
            ThemeType.Visibility = Visibility.Visible;
            ThemeBgButton.Visibility = Visibility.Visible;
            ThemeBgButton.Visibility = ThemeCustomBg.IsOn ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>
    ///     Обновляет тему приложения в реальном времени
    /// </summary>
    private static void UpdateTheme()
    {
        NotificationsService.Notifies ??= [];
        NotificationsService.Notifies.Add(new Notify
        {
            Title = "Theme applied!",
            Msg = "DEBUG MESSAGE. YOU SHOULDN'T SEE THIS",
            Type = InfoBarSeverity.Success
        });
        NotificationsService.SaveNotificationsSettings();
    }

    /// <summary>
    ///     Проверяет корректность и доступность выбранной темы
    /// </summary>
    private bool CheckThemeType() => _isLoaded
                                     && AppSettings.ThemeType > -1
                                     && AppSettings.ThemeType < _themeSelectorService.Themes.Count;

    #endregion

    #region Event Handlers

    /// <summary>
    ///     Изменяет состояние горячих клавиш (включены, выключены)
    /// </summary>
    private void EnableKeybinds_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        SettingsKeybindingsTooltip.IsOpen = true;
        AppSettings.HotkeysEnabled = EnableKeybindingsSetting.IsOn;
        AppSettings.SaveSettings();
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
    ///     Изменяет состояние применения только безопасных параметров разгона (включено, выключено)
    /// </summary>
    private void SafeReapply_Toggled(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_isLoaded)
            {
                return;
            }

            AppSettings.ReapplySafeOverclock = SafeReapply.IsOn;
            SendSmuCommand.GetSetSafeReapply(SafeReapply.IsOn);
            AppSettings.SaveSettings();
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex);
        }
    }

    #endregion

    #region Theme Section

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

        if (_themeSelectorService.Themes.Count == 0)
        {
            _themeSelectorService.SetThemeAsync(ElementTheme.Default); // Список пуст -> системная тема
            return;
        }

        if (AppSettings.ThemeType < 0 ||
            AppSettings.ThemeType >= _themeSelectorService.Themes.Count) // Защита от некорректного индекса
        {
            AppSettings.ThemeType = 0;
            AppSettings.SaveSettings();
        }

        var selectedTheme = _themeSelectorService.Themes[AppSettings.ThemeType];

        if (AppSettings.ThemeType == 0)
        {
            _themeSelectorService.SetThemeAsync(ElementTheme.Default);
        }
        else
        {
            _themeSelectorService.SetThemeAsync(selectedTheme.ThemeLight
                ? ElementTheme.Light
                : ElementTheme.Dark); // Переключение состояния темы, на светлую, тёмную
        }

        // Обновляем UI-элементы
        var customThemeVisibility = AppSettings.ThemeType > 7 ? Visibility.Visible : Visibility.Collapsed;
        AdvancedThemeOptions.IsOn = selectedTheme.ThemeCustom;
        ThemeOpacity.Value = selectedTheme.ThemeOpacity;
        ThemeMaskOpacity.Value = selectedTheme.ThemeMaskOpacity;
        ThemeCustomBg.Visibility = customThemeVisibility;
        ThemeCustomBg.IsOn = selectedTheme.ThemeCustomBg;
        ThemeType.Visibility = customThemeVisibility;
        ThemeType.IsOn = selectedTheme.ThemeLight;
        ThemeBgButton.Visibility = selectedTheme.ThemeCustomBg ? Visibility.Visible : Visibility.Collapsed;

        UpdateThemeCustomInterface();
        UpdateTheme();
    }

    /// <summary>
    ///     Переключает доступность расширенных параметров темы
    /// </summary>
    private void AdvancedThemeOptions_Toggled(object sender, RoutedEventArgs e)
    {
        if (CheckThemeType())
        {
            _themeSelectorService.Themes[AppSettings.ThemeType].ThemeCustom = AdvancedThemeOptions.IsOn;
            _themeSelectorService.SaveThemeInSettings();
            UpdateThemeCustomInterface();
        }
    }

    /// <summary>
    ///     Изменяет состояние темы с тёмной на светлую и наоборот
    /// </summary>
    private void ThemeType_Toggled(object sender, RoutedEventArgs e)
    {
        if (CheckThemeType())
        {
            _themeSelectorService.Themes[AppSettings.ThemeType].ThemeLight = ThemeType.IsOn;
            _themeSelectorService.SaveThemeInSettings();
            UpdateTheme();
        }
    }

    /// <summary>
    ///     Изменяет интенсивность цвета
    /// </summary>
    private void ThemeColorIntensity_ValueChanged(object sender, object args)
    {
        if (CheckThemeType())
        {
            _themeSelectorService.Themes[AppSettings.ThemeType].ThemeOpacity = ThemeOpacity.Value;
            _themeSelectorService.SaveThemeInSettings();
            UpdateTheme();
        }
    }

    /// <summary>
    ///     Изменяет прозрачность маски
    /// </summary>
    private void ThemeBackgroundMaskOpacity_ValueChanged(object sender, object args)
    {
        if (CheckThemeType())
        {
            _themeSelectorService.Themes[AppSettings.ThemeType].ThemeMaskOpacity = ThemeMaskOpacity.Value;
            _themeSelectorService.SaveThemeInSettings();
            UpdateTheme();
        }
    }

    /// <summary>
    ///     Изменяет состояние своего фона
    /// </summary>
    private void ThemeCustomBackground_Toggled(object sender, RoutedEventArgs e)
    {
        if (CheckThemeType())
        {
            _themeSelectorService.Themes[AppSettings.ThemeType].ThemeCustomBg = ThemeCustomBg.IsOn;
            _themeSelectorService.SaveThemeInSettings();
            ThemeBgButton.Visibility = ThemeCustomBg.IsOn ? Visibility.Visible : Visibility.Collapsed;
            UpdateTheme();
        }
    }

    /// <summary>
    ///     Открывает диалог выбора фона для темы
    /// </summary>
    private async void OpenSelectThemeBackgroundDialog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var endStringPath = "";
            var fromFileWhy = new TextBlock
            {
                MaxWidth = 300,
                Text = "ThemeBgFromFileWhy".GetLocalized(),
                TextWrapping = TextWrapping.WrapWholeWords,
                FontWeight = new FontWeight(300)
            };
            var fromFilePickedFile = new TextBlock
            {
                MaxWidth = 300,
                Visibility = Visibility.Collapsed,
                Text = "ThemeUnknownNewFile".GetLocalized(),
                TextWrapping = TextWrapping.WrapWholeWords,
                FontWeight = new FontWeight(300)
            };
            var fromFile = new Button
            {
                Height = 90,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                CornerRadius = new CornerRadius(16),
                Translation = new Vector3(0, 0, 12),
                Shadow = SharedShadow,
                Content = new Grid
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Children =
                    {
                        new Image
                        {
                            Margin = new Thickness(0, 0, 0, 0),
                            HorizontalAlignment = HorizontalAlignment.Left,
                            VerticalAlignment = VerticalAlignment.Center,
                            Source = new BitmapImage(new Uri("ms-appx:///Assets/ThemeBg/folder.png"))
                        },
                        new StackPanel
                        {
                            MinWidth = 300,
                            Orientation = Orientation.Vertical,
                            Margin = new Thickness(108, 0, 0, 0),
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            VerticalAlignment = VerticalAlignment.Top,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = "ThemeBgFromFile".GetLocalized(),
                                    FontWeight = new FontWeight(600)
                                },
                                fromFileWhy,
                                fromFilePickedFile
                            }
                        }
                    }
                }
            };
            var orText = new TextBlock
            {
                Margin = new Thickness(0, 5, 0, 0),
                Text = "ThemeBgOr".GetLocalized(),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var gifText = new TextBlock
            {
                Margin = new Thickness(0, 5, 0, 0),
                Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"],
                Text = "ThemeBgGifWarn".GetLocalized(),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var fromLinkWhy = new TextBlock
            {
                MaxWidth = 300,
                HorizontalAlignment = HorizontalAlignment.Left,
                Text = "ThemeBgFromURLWhy".GetLocalized(),
                TextWrapping = TextWrapping.WrapWholeWords,
                FontWeight = new FontWeight(300)
            };
            var fromLinkTextBox = new TextBox
            {
                MaxWidth = 300,
                Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PlaceholderText = "https://...."
            };
            var fromLink = new Button
            {
                Margin = new Thickness(0, 5, 0, 0),
                Height = 90,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                CornerRadius = new CornerRadius(16),
                Translation = new Vector3(0, 0, 12),
                Shadow = SharedShadow,
                Content = new Grid
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Children =
                    {
                        new Image
                        {
                            HorizontalAlignment = HorizontalAlignment.Left,
                            VerticalAlignment = VerticalAlignment.Center,
                            Source = new BitmapImage(new Uri("ms-appx:///Assets/ThemeBg/link.png"))
                        },
                        new StackPanel
                        {
                            MinWidth = 300,
                            Orientation = Orientation.Vertical,
                            Margin = new Thickness(108, 0, 0, 0),
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            VerticalAlignment = VerticalAlignment.Top,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = "ThemeBgFromURL".GetLocalized(),
                                    FontWeight = new FontWeight(600)
                                },
                                fromLinkWhy,
                                fromLinkTextBox
                            }
                        }
                    }
                }
            };
            // Открыть диалог с изменением 
            var bgDialog = new ContentDialog
            {
                Title = "ThemeBgDialog".GetLocalized(),
                Content = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Children =
                    {
                        fromFile,
                        orText,
                        fromLink,
                        gifText
                    }
                },
                CloseButtonText = "CancelThis/Text".GetLocalized(),
                PrimaryButtonText = "ThemeSelect".GetLocalized(),
                DefaultButton = ContentDialogButton.Close
            };
            fromFile.Click += (_, _) =>
            {
                // Сброс отображаемого текста (если используется для уведомлений)
                fromFilePickedFile.Text = "";

                var ofn = new OpenFileName();

                ofn.structSize = Marshal.SizeOf(ofn);

                ofn.filter = ".png\0*.png\0.jpeg\0*.jpeg\0.jpg\0*.jpg\0.gif\0*.gif\0";

                ofn.file = new string(new char[256]);
                ofn.maxFile = ofn.file.Length;

                ofn.fileTitle = new string(new char[64]);
                ofn.maxFileTitle = ofn.fileTitle.Length;

                ofn.initialDir = Path.GetFullPath(SpecialFolder.MyPictures.ToString());
                ofn.title = "Saku Overclock: Open image for theme background...";
                ofn.defExt = "png";

                // Вызываем диалог выбора файла
                if (OpenFileDialog.GetOpenFileNameApi(ofn))
                {
                    // Удаляем завершающие нулевые символы и получаем путь к выбранному файлу
                    var selectedFile = ofn.file.TrimEnd('\0');
                    fromFilePickedFile.Text = "ThemePickedFile".GetLocalized() + selectedFile;
                    endStringPath = selectedFile;
                }
                else
                {
                    // Если диалог не открылся, можно получить код ошибки для диагностики:
                    var error = Marshal.GetLastWin32Error();
                    fromFilePickedFile.Text = "ThemeOpCancel".GetLocalized() + " (Error: " + error + ")";
                }
            };

            fromLink.Click += (_, _) =>
            {
                if (fromLinkTextBox.Visibility == Visibility.Collapsed)
                {
                    fromLinkWhy.Visibility = Visibility.Collapsed;
                    fromLinkTextBox.Visibility = Visibility.Visible;
                }
                else
                {
                    fromLinkWhy.Visibility = Visibility.Visible;
                    fromLinkTextBox.Visibility = Visibility.Collapsed;
                }
            };
            fromLinkTextBox.TextChanged += (_, _) =>
            {
                endStringPath = fromLinkTextBox.Text;
            };
            // Use this code to associate the dialog to the appropriate AppWindow by setting
            // the dialog's XamlRoot to the same XamlRoot as an element that is already present in the AppWindow.
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
            {
                bgDialog.XamlRoot = XamlRoot;
            }

            var result = await bgDialog.ShowAsync();
            if (result == ContentDialogResult.Primary && endStringPath != "")
            {
                var backupIndex = ThemeComboBox.SelectedIndex;
                _themeSelectorService.Themes[backupIndex].ThemeBackground = endStringPath;
                _themeSelectorService.SaveThemeInSettings();
                UpdateTheme();
            }
        }
        catch (Exception ex)
        {
            await LogHelper.TraceIt_TraceError(ex);
        }
    }

    /// <summary>
    ///     Открывает диалог менеджера тем
    /// </summary>
    private async void OpenThemeManagerDialog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Отрыть редактор тем  
            var themeLoaderPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            var themerDialog = new ContentDialog
            {
                Title = "ThemeManagerTitle".GetLocalized(),
                Content = new ScrollViewer
                {
                    MaxHeight = 300,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Content = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Children =
                        {
                            themeLoaderPanel
                        }
                    }
                },
                CloseButtonText = "ThemeDone".GetLocalized(),
                DefaultButton = ContentDialogButton.Close
            };
            try
            {
                if (_themeSelectorService.Themes.Count != 0)
                {
                    for (var element = 8; element < _themeSelectorService.Themes.Count; element++)
                    {
                        var baseThemeName = _themeSelectorService.Themes[element].ThemeName;
                        Uri? baseThemeUri;
                        if (_themeSelectorService.Themes[element].ThemeBackground != "")
                        {
                            try
                            {
                                baseThemeUri = new Uri(_themeSelectorService.Themes[element].ThemeBackground);
                            }
                            catch
                            {
                                baseThemeUri = null;
                            }
                        }
                        else
                        {
                            baseThemeUri = null;
                        }

                        var sureDelete =
                            new Button
                            {
                                CornerRadius = new CornerRadius(15),
                                Content = "Delete".GetLocalized()
                            };
                        var buttonDelete = new Button
                        {
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            CornerRadius = new CornerRadius(15, 0, 0, 15),
                            Content = new FontIcon
                            {
                                Glyph = "\uE74D"
                            },
                            Flyout = new Flyout
                            {
                                Content = sureDelete
                            }
                        };
                        var textBoxThemeName = new TextBox
                        {
                            MaxLength = 13,
                            CornerRadius = new CornerRadius(15, 0, 0, 15),
                            Width = 300,
                            PlaceholderText = "ThemeNewName".GetLocalized(),
                            Text = baseThemeName
                        };
                        var newNameThemeSetButton = new Button
                        {
                            CornerRadius = new CornerRadius(0, 15, 15, 0),
                            Content = new FontIcon
                            {
                                Glyph = "\uEC61"
                            }
                        };
                        var editFlyout = new Flyout
                        {
                            Content = new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                Children =
                                {
                                    textBoxThemeName,
                                    newNameThemeSetButton
                                }
                            }
                        };
                        var buttonEdit = new Button
                        {
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            CornerRadius = new CornerRadius(0, 15, 15, 0),
                            Content = new FontIcon
                            {
                                Glyph = "\uE8AC"
                            },
                            Flyout = editFlyout
                        };
                        var themeNameText = new TextBlock
                        {
                            MaxWidth = 330,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            TextWrapping = TextWrapping.Wrap,
                            Text = baseThemeName,
                            FontWeight = new FontWeight(800)
                        };
                        var buttonsPanel = new StackPanel
                        {
                            Visibility = Visibility.Collapsed,
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Margin = new Thickness(0, 0, 4, 0),
                            Children =
                            {
                                buttonDelete,
                                buttonEdit
                            }
                        };
                        var eachButton = new Button
                        {
                            CornerRadius = new CornerRadius(17),
                            Width = 430,
                            Content = new Grid
                            {
                                MinHeight = 40,
                                Margin = new Thickness(-15, -5, -15, -6),
                                CornerRadius = new CornerRadius(4),
                                VerticalAlignment = VerticalAlignment.Stretch,
                                HorizontalAlignment = HorizontalAlignment.Stretch,
                                Children =
                                {
                                    new Border
                                    {
                                        MinHeight = 40,
                                        CornerRadius = new CornerRadius(17),
                                        Width = 430,
                                        Opacity = _themeSelectorService.Themes[element].ThemeOpacity,
                                        VerticalAlignment = VerticalAlignment.Stretch,
                                        HorizontalAlignment = HorizontalAlignment.Stretch,
                                        Background = new ImageBrush
                                        {
                                            ImageSource = new BitmapImage
                                            {
                                                UriSource = baseThemeUri
                                            }
                                        }
                                    },
                                    new Grid
                                    {
                                        MinHeight = 40,
                                        VerticalAlignment = VerticalAlignment.Stretch,
                                        HorizontalAlignment = HorizontalAlignment.Stretch,
                                        Background =
                                            (Brush)Application.Current.Resources["BackgroundImageMaskAcrylicBrush"],
                                        Opacity = _themeSelectorService.Themes[element].ThemeMaskOpacity
                                    },
                                    new Grid
                                    {
                                        HorizontalAlignment = HorizontalAlignment.Stretch,
                                        Children =
                                        {
                                            themeNameText,
                                            buttonsPanel
                                        }
                                    }
                                }
                            }
                        };
                        sureDelete.Name = element.ToString();
                        if (element > 8)
                        {
                            eachButton.Margin = new Thickness(0, 10, 0, 0);
                        }

                        newNameThemeSetButton.Click += (_, _) =>
                        {
                            if (textBoxThemeName.Text != "" || textBoxThemeName.Text != baseThemeName)
                            {
                                _themeSelectorService.Themes[int.Parse(sureDelete.Name)].ThemeName =
                                    textBoxThemeName.Text;
                                themeNameText.Text = textBoxThemeName.Text;
                                editFlyout.Hide();
                                _themeSelectorService.SaveThemeInSettings();
                                InitializePage();
                            }
                        };
                        eachButton.PointerEntered += (_, _) =>
                        {
                            themeNameText.Margin = new Thickness(-90, 0, 0, 0);
                            buttonsPanel.Visibility = Visibility.Visible;
                        };
                        eachButton.PointerExited += (_, _) =>
                        {
                            themeNameText.Margin = new Thickness(0);
                            buttonsPanel.Visibility = Visibility.Collapsed;
                        };
                        sureDelete.Click += (_, _) =>
                        {
                            try
                            {
                                _themeSelectorService.Themes.RemoveAt(int.Parse(sureDelete.Name));
                                _themeSelectorService.SaveThemeInSettings();
                                AppSettings.ThemeType = 0;
                                AppSettings.SaveSettings();
                                InitializePage();
                                themeLoaderPanel.Children.Remove(eachButton);
                            }
                            catch
                            {
                                themeLoaderPanel.Children.Remove(eachButton);
                            }
                        };
                        themeLoaderPanel.Children.Add(eachButton);
                    }
                }

                var newTheme = new Button
                {
                    CornerRadius = new CornerRadius(15),
                    Width = 430,
                    Style = (Style)Application.Current.Resources["AccentButtonStyle"],
                    Content = new TextBlock
                    {
                        FontWeight = new FontWeight(700),
                        Text = "ThemeNewName".GetLocalized()
                    }
                };
                if (themeLoaderPanel.Children.Count > 0)
                {
                    newTheme.Margin = new Thickness(0, 10, 0, 0);
                }

                themeLoaderPanel.Children.Add(newTheme);
                // Добавить новую тему
                newTheme.Click += (_, _) =>
                {
                    var textBoxThemeName = new TextBox
                    {
                        MaxLength = 13,
                        CornerRadius = new CornerRadius(15, 0, 0, 15),
                        Width = 300,
                        PlaceholderText = "ThemeNewName".GetLocalized()
                    };
                    var newNameThemeSetButton = new Button
                    {
                        CornerRadius = new CornerRadius(0, 15, 15, 0),
                        Content = new FontIcon
                        {
                            Glyph = "\uEC61"
                        }
                    };
                    newTheme.Flyout = new Flyout
                    {
                        Content = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Children =
                            {
                                textBoxThemeName,
                                newNameThemeSetButton
                            }
                        }
                    };
                    newNameThemeSetButton.Click += (_, _) =>
                    {
                        if (textBoxThemeName.Text != "")
                        {
                            try
                            {
                                _themeSelectorService.Themes.Add(new ThemeClass { ThemeName = textBoxThemeName.Text });
                                newTheme.Flyout.Hide();
                                themerDialog.Hide();
                                _themeSelectorService.SaveThemeInSettings();
                                InitializePage();
                            }
                            catch (Exception ex)
                            {
                                LogHelper.LogError(ex);
                            }
                        }
                    };
                };
            }
            catch
            {
                throw new Exception("Themer:Error#1 Cant load themes to edit");
            }


            // Use this code to associate the dialog to the appropriate AppWindow by setting
            // the dialog's XamlRoot to the same XamlRoot as an element that is already present in the AppWindow.
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
            {
                themerDialog.XamlRoot = XamlRoot;
            }

            await themerDialog.ShowAsync();
            UpdateTheme();
        }
        catch (Exception ex)
        {
            await LogHelper.LogError(ex);
        }
    }

    #endregion

    #region TrayMon Section

    /// <summary>
    ///     Инициализирует параметры TrayMon
    /// </summary>
    private void InitializeTrayMonIcons()
    {
        NiLoad();
        try
        {
            TrayMonIconsEnabled.IsOn = AppSettings.NiIconsEnabled;
            UpdateIconsGridCornerRadius();

            LoadTrayMonIconElements();

            if (TrayMonIconsEnabled.IsOn)
            {
                ShowTrayMonIconControls();
            }
            else
            {
                HideTrayMonIconControls();
            }

            // Финальное обновление корнеров (дублируется в оригинале)
            UpdateIconsGridCornerRadius();
        }
        catch
        {
            AppSettings.NiIconsType = -1; // Нет сохранённых
            AppSettings.SaveSettings();
        }
    }

    /// <summary>
    ///     Загружает элементы иконок в комбобокс и настраивает UI
    /// </summary>
    private void LoadTrayMonIconElements()
    {
        NiIconComboboxElements.Items.Clear();

        if (_niicons.Elements.Count == 0)
        {
            return;
        }

        // Заполняем комбобокс
        foreach (var trayIcon in _niicons.Elements)
        {
            try
            {
                NiIconComboboxElements.Items.Add(trayIcon.Name.GetLocalized());
            }
            catch
            {
                NiIconComboboxElements.Items.Add(trayIcon.Name);
            }
        }

        NiIconComboboxElements.SelectedIndex = AppSettings.NiIconsType;

        // Показываем элементы если есть выбранный индекс
        if (NiIconComboboxElements.SelectedIndex >= 0)
        {
            NiIconStackPanel.Visibility = Visibility.Visible;
            SettingsNiContextMenu.Visibility = Visibility.Visible;
            IsTrayMonIconShowing.Visibility = Visibility.Visible;
        }

        // Загружаем настройки выбранного элемента
        LoadSelectedTrayMonIconSettings();
    }

    /// <summary>
    ///     Загружает настройки выбранной иконки
    /// </summary>
    private void LoadSelectedTrayMonIconSettings()
    {
        if (AppSettings.NiIconsType < 0 || AppSettings.NiIconsType >= _niicons.Elements.Count)
        {
            return;
        }

        var selectedIcon = _niicons.Elements[AppSettings.NiIconsType];

        IsTrayMonIconShowing.IsOn = selectedIcon.IsEnabled;
        UpdateEnabledElementCornerRadius();

        if (!selectedIcon.IsEnabled)
        {
            NiIconStackPanel.Visibility = Visibility.Collapsed;
            SettingsNiContextMenu.Visibility = Visibility.Collapsed;
        }

        NiIconCombobox.SelectedIndex = selectedIcon.ContextMenuType;
        NiIconsColorPickerColorPicker.Color = ParseColor(selectedIcon.Color);
        SettingsNiGradientToggle.IsOn = selectedIcon.IsGradient;
        NiIconShapeCombobox.SelectedIndex = selectedIcon.IconShape;
        SettingsNiFontsize.Value = selectedIcon.FontSize;
        SettingsNiOpacity.Value = selectedIcon.BgOpacity;
    }

    /// <summary>
    ///     Показывает элементы управления иконками
    /// </summary>
    private void ShowTrayMonIconControls()
    {
        SettingsNiIconsElement.Visibility = Visibility.Visible;
        SettingsNiAddElement.Visibility = Visibility.Visible;
        IsTrayMonIconShowing.Visibility = Visibility.Visible;
        NiIconComboboxElements.Visibility = Visibility.Visible;

        // Показываем детальные настройки только если элемент выбран и включен
        var showDetailedSettings = NiIconComboboxElements.SelectedIndex >= 0 && IsTrayMonIconShowing.IsOn;

        NiIconStackPanel.Visibility = showDetailedSettings ? Visibility.Visible : Visibility.Collapsed;
        SettingsNiContextMenu.Visibility = showDetailedSettings ? Visibility.Visible : Visibility.Collapsed;

        if (!showDetailedSettings)
        {
            UpdateEnabledElementCornerRadius();
        }
    }

    /// <summary>
    ///     Скрывает все элементы управления иконками
    /// </summary>
    private void HideTrayMonIconControls()
    {
        NiIconComboboxElements.Visibility = Visibility.Collapsed;
        SettingsNiIconsElement.Visibility = Visibility.Collapsed;
        NiIconStackPanel.Visibility = Visibility.Collapsed;
        SettingsNiContextMenu.Visibility = Visibility.Collapsed;
        SettingsNiAddElement.Visibility = Visibility.Collapsed;
        IsTrayMonIconShowing.Visibility = Visibility.Collapsed;

        UpdateEnabledElementCornerRadius();
    }

    /// <summary>
    ///     Обновляет радиус углов для главной Grid иконок
    /// </summary>
    private void UpdateIconsGridCornerRadius()
    {
        SettingsNiIconsGrid.CornerRadius = TrayMonIconsEnabled.IsOn
            ? new CornerRadius(15, 15, 0, 0)
            : new CornerRadius(15);
    }

    /// <summary>
    ///     Обновляет радиус углов для элемента EnabledElement
    /// </summary>
    private void UpdateEnabledElementCornerRadius()
    {
        SettingsNiEnabledElementGrid.CornerRadius = IsTrayMonIconShowing.IsOn
            ? new CornerRadius(0)
            : new CornerRadius(0, 0, 15, 15);
    }

    /// <summary>
    ///     Основной метод изменения состояния иконок TrayMon
    /// </summary>
    private void TrayMonIconsEnabled_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        AppSettings.NiIconsEnabled = TrayMonIconsEnabled.IsOn;
        AppSettings.SaveSettings();
        if (TrayMonIconsEnabled.IsOn)
        {
            NiIconComboboxElements.Visibility = Visibility.Visible;
            SettingsNiIconsElement.Visibility = Visibility.Visible;
            SettingsNiAddElement.Visibility = Visibility.Visible;
            IsTrayMonIconShowing.Visibility = Visibility.Visible;
            if (NiIconComboboxElements.SelectedIndex >= 0 && IsTrayMonIconShowing.IsOn)
            {
                NiIconStackPanel.Visibility = Visibility.Visible;
                SettingsNiContextMenu.Visibility = Visibility.Visible;
            }
            else
            {
                NiIconStackPanel.Visibility = Visibility.Collapsed;
                SettingsNiContextMenu.Visibility = Visibility.Collapsed;
                SettingsNiEnabledElementGrid.CornerRadius = IsTrayMonIconShowing.IsOn
                    ? new CornerRadius(0)
                    : new CornerRadius(0, 0, 15, 15);
            }
        }
        else
        {
            NiIconComboboxElements.Visibility = Visibility.Collapsed;
            SettingsNiIconsElement.Visibility = Visibility.Collapsed;
            NiIconStackPanel.Visibility = Visibility.Collapsed;
            SettingsNiContextMenu.Visibility = Visibility.Collapsed;
            SettingsNiAddElement.Visibility = Visibility.Collapsed;
            IsTrayMonIconShowing.Visibility = Visibility.Collapsed;
            SettingsNiEnabledElementGrid.CornerRadius = IsTrayMonIconShowing.IsOn
                ? new CornerRadius(0)
                : new CornerRadius(0, 0, 15, 15);
        }

        SettingsNiIconsGrid.CornerRadius =
            TrayMonIconsEnabled.IsOn ? new CornerRadius(15, 15, 0, 0) : new CornerRadius(15);

        App.BackgroundUpdater?.UpdateNotifyIcons();
    }

    /// <summary>
    ///     Диалог добавления иконки TrayMon
    /// </summary>
    private async void AddTrayMonIcon_Click(object sender, object e)
    {
        try
        {
            var niLoaderPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            var niAddIconDialog = new ContentDialog
            {
                Title = "Settings_ni_icon_dialog".GetLocalized(),
                Content = new ScrollViewer
                {
                    MaxHeight = 300,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Content = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Children =
                        {
                            niLoaderPanel
                        }
                    }
                },
                CloseButtonText = "ThemeDone".GetLocalized(),
                DefaultButton = ContentDialogButton.Close
            };
            NiLoad();
            try
            {
                if (_niicons.Elements.Count != 0)
                {
                    for (var element = 8; element < _niicons.Elements.Count; element++)
                    {
                        var baseNiName = _niicons.Elements[element].Name;
                        Color baseNiBackground; // Белый 
                        if (_niicons.Elements[element].Color != "")
                        {
                            try
                            {
                                baseNiBackground = ParseColor(_niicons.Elements[element].Color);
                            }
                            catch
                            {
                                baseNiBackground = Color.FromArgb(255, 255, 255, 255);
                            }
                        }
                        else
                        {
                            baseNiBackground = Color.FromArgb(255, 255, 255, 255);
                        }

                        var sureDelete =
                            new Button
                            {
                                CornerRadius = new CornerRadius(15),
                                Content = "Delete".GetLocalized()
                            };
                        var buttonDelete = new Button
                        {
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            CornerRadius = new CornerRadius(15, 15, 15, 15),
                            Content = new FontIcon
                            {
                                Glyph = "\uE74D"
                            },
                            Flyout = new Flyout
                            {
                                Content = sureDelete
                            }
                        };
                        var niElementName = new TextBlock
                        {
                            MaxWidth = 330,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            TextWrapping = TextWrapping.Wrap,
                            Text = baseNiName,
                            FontWeight = new FontWeight(800)
                        };
                        var buttonsPanel = new StackPanel
                        {
                            Visibility = Visibility.Collapsed,
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Margin = new Thickness(0, 0, 4, 0),
                            Children =
                            {
                                buttonDelete
                            }
                        };
                        var eachButton = new Button
                        {
                            CornerRadius = new CornerRadius(17),
                            Width = 430,
                            Content = new Grid
                            {
                                MinHeight = 40,
                                Margin = new Thickness(-15, -5, -15, -6),
                                CornerRadius = new CornerRadius(4),
                                VerticalAlignment = VerticalAlignment.Stretch,
                                HorizontalAlignment = HorizontalAlignment.Stretch,
                                Children =
                                {
                                    new Border
                                    {
                                        MinHeight = 40,
                                        CornerRadius = new CornerRadius(17),
                                        Width = 430,
                                        Opacity = _niicons.Elements[element].BgOpacity,
                                        VerticalAlignment = VerticalAlignment.Stretch,
                                        HorizontalAlignment = HorizontalAlignment.Stretch,
                                        Background = new SolidColorBrush
                                        {
                                            Color = baseNiBackground
                                        }
                                    },
                                    new Grid
                                    {
                                        HorizontalAlignment = HorizontalAlignment.Stretch,
                                        Children =
                                        {
                                            niElementName,
                                            buttonsPanel
                                        }
                                    }
                                }
                            }
                        };
                        sureDelete.Name = element.ToString();
                        if (element > 8)
                        {
                            eachButton.Margin = new Thickness(0, 10, 0, 0);
                        }

                        eachButton.PointerEntered += (_, _) =>
                        {
                            niElementName.Margin = new Thickness(-90, 0, 0, 0);
                            buttonsPanel.Visibility = Visibility.Visible;
                        };
                        eachButton.PointerExited += (_, _) =>
                        {
                            niElementName.Margin = new Thickness(0);
                            buttonsPanel.Visibility = Visibility.Collapsed;
                        };
                        sureDelete.Click += (_, _) =>
                        {
                            try
                            {
                                _niicons.Elements.RemoveAt(int.Parse(sureDelete.Name));
                                NiSave();
                                niLoaderPanel.Children.Remove(eachButton);
                            }
                            catch
                            {
                                niLoaderPanel.Children.Remove(eachButton);
                            }
                        };
                        niLoaderPanel.Children.Add(eachButton);
                    }
                }

                var newNiIcon = new Button // Добавить новый элемент
                {
                    CornerRadius = new CornerRadius(15),
                    Width = 430,
                    Style = (Style)Application.Current.Resources["AccentButtonStyle"],
                    Content = new TextBlock
                    {
                        FontWeight = new FontWeight(700),
                        Text = "ThemeNewName".GetLocalized()
                    }
                };
                if (niLoaderPanel.Children.Count > 0)
                {
                    newNiIcon.Margin = new Thickness(0, 10, 0, 0);
                }

                niLoaderPanel.Children.Add(newNiIcon);
                // Добавить новую тему
                newNiIcon.Click += (_, _) =>
                {
                    var niIconSelectedComboBox = new ComboBox
                    {
                        CornerRadius = new CornerRadius(15, 0, 0, 15),
                        Width = 300
                    };
                    if (!NiIconComboboxElements.Items.Contains("Settings_ni_Values_STAPM".GetLocalized()))
                    {
                        niIconSelectedComboBox.Items.Add(new ComboBoxItem
                        {
                            Content = "Settings_ni_Values_STAPM".GetLocalized(),
                            Name = "Settings_ni_Values_STAPM"
                        });
                    }

                    if (!NiIconComboboxElements.Items.Contains("Settings_ni_Values_Fast".GetLocalized()))
                    {
                        niIconSelectedComboBox.Items.Add(new ComboBoxItem
                        {
                            Content = "Settings_ni_Values_Fast".GetLocalized(),
                            Name = "Settings_ni_Values_Fast"
                        });
                    }

                    if (!NiIconComboboxElements.Items.Contains("Settings_ni_Values_Slow".GetLocalized()))
                    {
                        niIconSelectedComboBox.Items.Add(new ComboBoxItem
                        {
                            Content = "Settings_ni_Values_Slow".GetLocalized(),
                            Name = "Settings_ni_Values_Slow"
                        });
                    }

                    if (!NiIconComboboxElements.Items.Contains("Settings_ni_Values_VRMEDC".GetLocalized()))
                    {
                        niIconSelectedComboBox.Items.Add(new ComboBoxItem
                        {
                            Content = "Settings_ni_Values_VRMEDC".GetLocalized(),
                            Name = "Settings_ni_Values_VRMEDC"
                        });
                    }

                    if (!NiIconComboboxElements.Items.Contains("Settings_ni_Values_CPUTEMP".GetLocalized()))
                    {
                        niIconSelectedComboBox.Items.Add(new ComboBoxItem
                        {
                            Content = "Settings_ni_Values_CPUTEMP".GetLocalized(),
                            Name = "Settings_ni_Values_CPUTEMP"
                        });
                    }

                    if (!NiIconComboboxElements.Items.Contains("Settings_ni_Values_CPUUsage".GetLocalized()))
                    {
                        niIconSelectedComboBox.Items.Add(new ComboBoxItem
                        {
                            Content = "Settings_ni_Values_CPUUsage".GetLocalized(),
                            Name = "Settings_ni_Values_CPUUsage"
                        });
                    }

                    if (!NiIconComboboxElements.Items.Contains("Settings_ni_Values_AVGCPUCLK".GetLocalized()))
                    {
                        niIconSelectedComboBox.Items.Add(new ComboBoxItem
                        {
                            Content = "Settings_ni_Values_AVGCPUCLK".GetLocalized(),
                            Name = "Settings_ni_Values_AVGCPUCLK"
                        });
                    }

                    if (!NiIconComboboxElements.Items.Contains("Settings_ni_Values_AVGCPUVOLT".GetLocalized()))
                    {
                        niIconSelectedComboBox.Items.Add(new ComboBoxItem
                        {
                            Content = "Settings_ni_Values_AVGCPUVOLT".GetLocalized(),
                            Name = "Settings_ni_Values_AVGCPUVOLT"
                        });
                    }

                    if (!NiIconComboboxElements.Items.Contains("Settings_ni_Values_GFXCLK".GetLocalized()))
                    {
                        niIconSelectedComboBox.Items.Add(new ComboBoxItem
                        {
                            Content = "Settings_ni_Values_GFXCLK".GetLocalized(),
                            Name = "Settings_ni_Values_GFXCLK"
                        });
                    }

                    if (!NiIconComboboxElements.Items.Contains("Settings_ni_Values_GFXTEMP".GetLocalized()))
                    {
                        niIconSelectedComboBox.Items.Add(new ComboBoxItem
                        {
                            Content = "Settings_ni_Values_GFXTEMP".GetLocalized(),
                            Name = "Settings_ni_Values_GFXTEMP"
                        });
                    }

                    if (!NiIconComboboxElements.Items.Contains("Settings_ni_Values_GFXVOLT".GetLocalized()))
                    {
                        niIconSelectedComboBox.Items.Add(new ComboBoxItem
                        {
                            Content = "Settings_ni_Values_GFXVOLT".GetLocalized(),
                            Name = "Settings_ni_Values_GFXVOLT"
                        });
                    }

                    if (niIconSelectedComboBox.Items.Count >= 1)
                    {
                        niIconSelectedComboBox.SelectedIndex = 0;
                    }

                    var niIconAddButtonSuccess = new Button
                    {
                        CornerRadius = new CornerRadius(0, 15, 15, 0),
                        Content = new FontIcon
                        {
                            Glyph = "\uEC61"
                        }
                    };
                    newNiIcon.Flyout = new Flyout
                    {
                        Content = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Children =
                            {
                                niIconSelectedComboBox,
                                niIconAddButtonSuccess
                            }
                        }
                    };
                    niIconAddButtonSuccess.Click += (_, _) =>
                    {
                        if (niIconSelectedComboBox.SelectedIndex != -1)
                        {
                            try
                            {
                                _niicons.Elements.Add(new NiIconsElements
                                {
                                    Name = ((ComboBoxItem)niIconSelectedComboBox.SelectedItem).Name!
                                });
                                newNiIcon.Flyout.Hide();
                                niAddIconDialog.Hide();
                                NiSave();
                                InitializeTrayMonIcons();
                            }
                            catch (Exception ex)
                            {
                                LogHelper.LogError(ex);
                            }
                        }
                    };
                };
            }
            catch
            {
                throw new Exception("NiIcons:Error#1 Cant load themes to edit");
            }


            // Use this code to associate the dialog to the appropriate AppWindow by setting
            // the dialog's XamlRoot to the same XamlRoot as an element that is already present in the AppWindow.
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
            {
                niAddIconDialog.XamlRoot = XamlRoot;
            }

            await niAddIconDialog.ShowAsync();

            App.BackgroundUpdater?.UpdateNotifyIcons();
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception);
        }
    }

    /// <summary>
    ///     Изменение выбранного TrayMon элемента для настройки
    /// </summary>
    private void TrayMonIconElements_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
    {
        if (!_isLoaded)
        {
            return;
        }

        AppSettings.NiIconsType = NiIconComboboxElements.SelectedIndex;
        AppSettings.SaveSettings();
        NiLoad();
        if (_niicons.Elements.Count != 0 && AppSettings.NiIconsType != -1)
        {
            if (NiIconComboboxElements.SelectedIndex >= 0)
            {
                NiIconStackPanel.Visibility = Visibility.Visible;
                SettingsNiContextMenu.Visibility = Visibility.Visible;
                IsTrayMonIconShowing.Visibility = Visibility.Visible;
            }

            IsTrayMonIconShowing.IsOn = _niicons.Elements[AppSettings.NiIconsType].IsEnabled;
            SettingsNiEnabledElementGrid.CornerRadius = IsTrayMonIconShowing.IsOn
                ? new CornerRadius(0)
                : new CornerRadius(0, 0, 15, 15);

            if (!_niicons.Elements[AppSettings.NiIconsType].IsEnabled)
            {
                NiIconStackPanel.Visibility = Visibility.Collapsed;
                SettingsNiContextMenu.Visibility = Visibility.Collapsed;
            }

            NiIconCombobox.SelectedIndex = _niicons.Elements[AppSettings.NiIconsType].ContextMenuType;
            NiIconsColorPickerColorPicker.Color =
                ParseColor(_niicons.Elements[AppSettings.NiIconsType].Color);
            SettingsNiGradientToggle.IsOn = _niicons.Elements[AppSettings.NiIconsType].IsGradient;
            NiIconShapeCombobox.SelectedIndex = _niicons.Elements[AppSettings.NiIconsType].IconShape;
            SettingsNiFontsize.Value = _niicons.Elements[AppSettings.NiIconsType].FontSize;
            SettingsNiOpacity.Value = _niicons.Elements[AppSettings.NiIconsType].BgOpacity;
        }
    }

    /// <summary>
    ///     Изменение выбранного типа отображения контекстного меню TrayMon иконки (стандартное, расширенное)
    /// </summary>
    private void NiIconContextMenuType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        NiLoad();
        _niicons.Elements[AppSettings.NiIconsType].ContextMenuType = NiIconCombobox.SelectedIndex;
        NiSave();

        App.BackgroundUpdater?.UpdateNotifyIcons();
    }

    /// <summary>
    ///     Изменение состояния отображения иконки (отображать в трее или нет)
    /// </summary>
    private void IsTrayMonIconShowing_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        NiLoad();
        _niicons.Elements[AppSettings.NiIconsType].IsEnabled = IsTrayMonIconShowing.IsOn;
        NiSave();
        if (NiIconComboboxElements.SelectedIndex >= 0 && IsTrayMonIconShowing.IsOn)
        {
            NiIconStackPanel.Visibility = Visibility.Visible;
            SettingsNiContextMenu.Visibility = Visibility.Visible;
        }
        else
        {
            NiIconStackPanel.Visibility = Visibility.Collapsed;
            SettingsNiContextMenu.Visibility = Visibility.Collapsed;
        }

        SettingsNiEnabledElementGrid.CornerRadius =
            IsTrayMonIconShowing.IsOn ? new CornerRadius(0) : new CornerRadius(0, 0, 15, 15);

        App.BackgroundUpdater?.UpdateNotifyIcons();
    }

    /// <summary>
    ///     Изменение размера шрифта на иконке
    /// </summary>
    private void ChangeTrayMonIconFontSize_ValueChanged(object sender, object args)
    {
        if (!_isLoaded)
        {
            return;
        }

        NiLoad();
        _niicons.Elements[AppSettings.NiIconsType].FontSize =
            Convert.ToInt32(SettingsNiFontsize.Value);
        NiSave();

        App.BackgroundUpdater?.UpdateNotifyIcons();
    }

    /// <summary>
    ///     Изменение интенсивности цвета фона иконки
    /// </summary>
    private void ChangeTrayMonIconColorIntensity_ValueChanged(object sender, object args)
    {
        if (!_isLoaded)
        {
            return;
        }

        NiLoad();
        _niicons.Elements[AppSettings.NiIconsType].BgOpacity = SettingsNiOpacity.Value;
        NiSave();

        App.BackgroundUpdater?.UpdateNotifyIcons();
    }

    /// <summary>
    ///     Изменение цвета иконки
    /// </summary>
    private void ChangeTrayMonIconColor_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (!_isLoaded)
        {
            return;
        }

        NiLoad();
        if (SettingsNiGradientColorSwitcher.IsChecked == false)
        {
            _niicons.Elements[AppSettings.NiIconsType].Color =
                $"{NiIconsColorPickerColorPicker.Color.R:X2}{NiIconsColorPickerColorPicker.Color.G:X2}{NiIconsColorPickerColorPicker.Color.B:X2}";
        }
        else if (SettingsNiGradientColorSwitcher.IsChecked == true)
        {
            _niicons.Elements[AppSettings.NiIconsType].SecondColor =
                $"{NiIconsColorPickerColorPicker.Color.R:X2}{NiIconsColorPickerColorPicker.Color.G:X2}{NiIconsColorPickerColorPicker.Color.B:X2}";
        }

        NiSave();

        App.BackgroundUpdater?.UpdateNotifyIcons();
    }

    /// <summary>
    ///     Изменение типа фона иконки (обычная заливка, градиент)
    /// </summary>
    private void EnableTrayMonIconGradientMode_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        NiLoad();
        _niicons.Elements[AppSettings.NiIconsType].IsGradient = true;
        NiSave();

        App.BackgroundUpdater?.UpdateNotifyIcons();
    }

    /// <summary>
    ///     Изменение цвета фона иконки (обычная заливка, градиент)
    /// </summary>
    private void UpdateTrayMonIconGradientColor_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        var button = sender as ToggleButton;
        if (button != null)
        {
            if (button.IsChecked == true)
            {
                button.Content = "Settings_ni_TrayMonGradientColorSwitch/Content".GetLocalized() + "2";
                NiIconsColorPickerColorPicker.Color =
                    ParseColor(_niicons.Elements[AppSettings.NiIconsType].SecondColor);
            }
            else
            {
                button.Content = "Settings_ni_TrayMonGradientColorSwitch/Content".GetLocalized() + "1";
                NiIconsColorPickerColorPicker.Color =
                    ParseColor(_niicons.Elements[AppSettings.NiIconsType].Color);
            }
        }

        App.BackgroundUpdater?.UpdateNotifyIcons();
    }

    /// <summary>
    ///     Изменение формы иконки (квадрат, скруглённый квадрат, круг)
    /// </summary>
    private void ChangeTrayMonIconShape_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        NiLoad();
        _niicons.Elements[AppSettings.NiIconsType].IconShape = NiIconShapeCombobox.SelectedIndex;
        NiSave();

        App.BackgroundUpdater?.UpdateNotifyIcons();
    }

    /// <summary>
    ///     Удаляет выбранную иконку TrayMon
    /// </summary>
    private void DeleteTrayMonIcon_Click(object sender, object e)
    {
        if (!_isLoaded)
        {
            return;
        }

        try
        {
            NiLoad();
            _niicons.Elements.RemoveAt(AppSettings.NiIconsType);
            NiSave();
            AppSettings.NiIconsType = -1;
            AppSettings.SaveSettings();
            InitializeTrayMonIcons();

            App.BackgroundUpdater?.UpdateNotifyIcons();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex);
        }
    }

    /// <summary>
    ///     Восстанавливает параметры TrayMon по умолчанию
    /// </summary>
    private void TrayMonResetDefaults_Click(object sender, object e)
    {
        if (!_isLoaded)
        {
            return;
        }

        NiLoad();
        _niicons.Elements[AppSettings.NiIconsType].IsEnabled = true;
        _niicons.Elements[AppSettings.NiIconsType].ContextMenuType = 1;
        _niicons.Elements[AppSettings.NiIconsType].Color = "FF6ACF";
        _niicons.Elements[AppSettings.NiIconsType].IconShape = 0;
        _niicons.Elements[AppSettings.NiIconsType].FontSize = 9;
        _niicons.Elements[AppSettings.NiIconsType].BgOpacity = 0.5d;
        NiSave();
        TrayMonIconElements_SelectionChanged(null, null);

        App.BackgroundUpdater?.UpdateNotifyIcons();
    }

    #endregion

    #region Rtss Section

    /// <summary>
    ///     Загружает состояние каждого элемента настроек Rtss в интерфейс
    /// </summary>
    private void LoadRtssElements()
    {
        for (var i = 0; i < RtssSettings.RtssElements.Count; i++)
        {
            // Получаем элементы в зависимости от текущего значения i
            var toggleButton = RtssMainColorCompactToggle;
            var checkBox = RtssMainColorCheckbox;
            TextBox? textBox = null;
            var colorPicker = RtssMainColorColorPicker;

            switch (i)
            {
                case 0:
                    toggleButton = RtssMainColorCompactToggle;
                    checkBox = RtssMainColorCheckbox;
                    textBox = null; // Здесь TextBox нет
                    colorPicker = RtssMainColorColorPicker;
                    break;
                case 1:
                    toggleButton = RtssAllCompactToggle;
                    checkBox = RtssSecondColorCheckbox;
                    textBox = null; // Здесь TextBox нет
                    colorPicker = RtssSecondColorColorPicker;
                    break;
                case 2:
                    toggleButton = RtssSakuPresetCompactToggle;
                    checkBox = RtssSakuOverclockPresetCheckbox;
                    textBox = RtssSakuOverclockPresetTextBox;
                    colorPicker = RtssSakuOverclockPresetColorPicker;
                    break;
                case 3:
                    toggleButton = RtssStapmFastSlowCompactToggle;
                    checkBox = RtssStapmFastSlowCheckbox;
                    textBox = RtssStapmFastSlowTextBox;
                    colorPicker = RtssStapmFastSlowColorPicker;
                    break;
                case 4:
                    toggleButton = RtssEdcThermUsageCompactToggle;
                    checkBox = RtssEdcThermUsageCheckbox;
                    textBox = RtssEdcThermUsageTextBox;
                    colorPicker = RtssEdcThermUsageColorPicker;
                    break;
                case 5:
                    toggleButton = RtssCpuClocksCompactToggle;
                    checkBox = RtssCpuClocksCheckbox;
                    textBox = RtssCpuClocksTextBox;
                    colorPicker = RtssCpuClocksColorPicker;
                    break;
                case 6:
                    toggleButton = RtssAvgCpuClockVoltCompactToggle;
                    checkBox = RtssAvgCpuClockVoltCheckbox;
                    textBox = RtssAvgCpuClockVoltTextBox;
                    colorPicker = RtssAvgCpuClockVoltColorPicker;
                    break;
                case 7:
                    toggleButton = RtssApuClockVoltTempCompactToggle;
                    checkBox = RtssApuClockVoltTempCheckbox;
                    textBox = RtssApuClockVoltTempTextBox;
                    colorPicker = RtssApuClockVoltTempColorPicker;
                    break;
                case 8:
                    toggleButton = RtssFrameRateCompactToggle;
                    checkBox = RtssFrameRateCheckbox;
                    textBox = RtssFrameRateTextBox;
                    colorPicker = RtssFrameRateColorPicker;
                    break;
            }

            // Применение значения ToggleButton
            if (toggleButton != null)
            {
                toggleButton.IsChecked = RtssSettings.RtssElements[i].UseCompact;
            }

            // Применение значения CheckBox
            if (checkBox != null)
            {
                checkBox.IsChecked = RtssSettings.RtssElements[i].Enabled;
            }

            // Применение значения TextBox
            if (textBox != null)
            {
                textBox.Text = RtssSettings.RtssElements[i].Name;
            }

            // Применение значения ColorPicker
            if (colorPicker != null)
            {
                var color = RtssSettings.RtssElements[i].Color;
                var r = Convert.ToByte(color.Substring(1, 2), 16);
                var g = Convert.ToByte(color.Substring(3, 2), 16);
                var b = Convert.ToByte(color.Substring(5, 2), 16);
                colorPicker.Color = Color.FromArgb(255, r, g, b);
            }
        }
    }

    /// <summary>
    ///     Создаёт текст для отображения Rtss
    /// </summary>
    private void GenerateAdvancedCodeEditor()
    {
        // Шаг 1: Создание ColorLib
        var colorLib = new List<string>
        {
            "FFFFFF" // Добавляем белый цвет по умолчанию
        };

        AddColorIfUnique(RtssSettings.RtssElements[0].Color);
        AddColorIfUnique(RtssSettings.RtssElements[1].Color);
        AddColorIfUnique(RtssSettings.RtssElements[2].Color);
        AddColorIfUnique(RtssSettings.RtssElements[3].Color);
        AddColorIfUnique(RtssSettings.RtssElements[4].Color);
        AddColorIfUnique(RtssSettings.RtssElements[5].Color);
        AddColorIfUnique(RtssSettings.RtssElements[6].Color);
        AddColorIfUnique(RtssSettings.RtssElements[7].Color);
        AddColorIfUnique(RtssSettings.RtssElements[8].Color);

        // Шаг 2: Создание CompactLib
        var compactLib = new bool[9];
        compactLib[0] = RtssSettings.RtssElements[0].UseCompact;
        compactLib[1] = RtssSettings.RtssElements[1].UseCompact;
        compactLib[2] = RtssSettings.RtssElements[1].UseCompact && RtssSettings.RtssElements[1].Enabled
            ? RtssSettings.RtssElements[1].UseCompact
            : RtssSettings.RtssElements[2].UseCompact;
        compactLib[3] = RtssSettings.RtssElements[1].UseCompact && RtssSettings.RtssElements[1].Enabled
            ? RtssSettings.RtssElements[1].UseCompact
            : RtssSettings.RtssElements[3].UseCompact;
        compactLib[4] = RtssSettings.RtssElements[1].UseCompact && RtssSettings.RtssElements[1].Enabled
            ? RtssSettings.RtssElements[1].UseCompact
            : RtssSettings.RtssElements[4].UseCompact;
        compactLib[5] = RtssSettings.RtssElements[1].UseCompact && RtssSettings.RtssElements[1].Enabled
            ? RtssSettings.RtssElements[1].UseCompact
            : RtssSettings.RtssElements[5].UseCompact;
        compactLib[6] = RtssSettings.RtssElements[1].UseCompact && RtssSettings.RtssElements[1].Enabled
            ? RtssSettings.RtssElements[1].UseCompact
            : RtssSettings.RtssElements[6].UseCompact;
        compactLib[7] = RtssSettings.RtssElements[1].UseCompact && RtssSettings.RtssElements[1].Enabled
            ? RtssSettings.RtssElements[1].UseCompact
            : RtssSettings.RtssElements[7].UseCompact;
        compactLib[8] = RtssSettings.RtssElements[1].UseCompact && RtssSettings.RtssElements[1].Enabled
            ? RtssSettings.RtssElements[1].UseCompact
            : RtssSettings.RtssElements[8].UseCompact;

        // Шаг 3: Создание EnableLib
        var enableLib = new bool[9];
        enableLib[0] = RtssSettings.RtssElements[0].Enabled;
        enableLib[1] = RtssSettings.RtssElements[1].Enabled;
        enableLib[2] = RtssSettings.RtssElements[2].Enabled;
        enableLib[3] = RtssSettings.RtssElements[3].Enabled;
        enableLib[4] = RtssSettings.RtssElements[4].Enabled;
        enableLib[5] = RtssSettings.RtssElements[5].Enabled;
        enableLib[6] = RtssSettings.RtssElements[6].Enabled;
        enableLib[7] = RtssSettings.RtssElements[7].Enabled;
        enableLib[8] = RtssSettings.RtssElements[8].Enabled;

        // Шаг 4: Создание TextLib
        var textLib = new string[7];
        textLib[0] = RtssSettings.RtssElements[2].Name.TrimEnd(); // Saku Overclock Preset
        textLib[1] = RtssSettings.RtssElements[3].Name.TrimEnd(); // STAPM Fast Slow
        textLib[2] = RtssSettings.RtssElements[4].Name.TrimEnd(); // EDC Therm CPU Usage
        textLib[3] = RtssSettings.RtssElements[5].Name.TrimEnd(); // CPU Clocks
        textLib[4] = RtssSettings.RtssElements[6].Name.TrimEnd(); // AVG Clock Volt
        textLib[5] = RtssSettings.RtssElements[7].Name.TrimEnd(); // APU Clock Volt Temp
        textLib[6] = RtssSettings.RtssElements[8].Name.TrimEnd(); // Frame Rate

        // Шаг 5: Генерация строки AdvancedCodeEditor
        var advancedCodeEditor = new StringBuilder();

        /*public string AdvancedCodeEditor =
        "<C0=FFA0A0><C1=A0FFA0><C2=FC89AC><C3=fa2363><S1=70><S2=-50>\n" +
        "<C0>Saku Overclock <C1>" + ViewModels.ГлавнаяViewModel.GetVersion() + ": <S0>$SelectedPreset$\n" +
        "<S1><C2>STAPM, Fast, Slow: <C3><S0>$stapm_value$<S2>W<S1>$stapm_limit$W <S0>$fast_value$<S2>W<S1>$fast_limit$W <S0>$slow_value$<S2>W<S1>$slow_limit$W\n" +
        "<C2>EDC, Therm, CPU Usage: <C3><S0>$vrmedc_value$<S2>A<S1>$vrmedc_max$A <C3><S0>$cpu_temp_value$<S2>C<S1>$cpu_temp_max$C<C3><S0> $cpu_usage$<S2>%<S1>\n" +
        "<S1><C2>Clocks: $cpu_clock_cycle$<S1><C2>$currCore$:<S0><C3> $cpu_core_clock$<S2>GHz<S1>$cpu_core_voltage$V $cpu_clock_cycle_end$\n" +
        "<C2>AVG Clock, Volt: <C3><S0>$average_cpu_clock$<S2>GHz<S1>$average_cpu_voltage$V" +
        "<C2>APU Clock, Volt, Temp: <C3><S0>$gfx_clock$<S2>MHz<S1>$gfx_volt$V <S0>$gfx_temp$<S1>C\n" +
        "<C2>Framerate <C3><S0>%FRAMERATE% %FRAMETIME%";*/

        // 5.1 Первая строка с цветами и размерами
        // Пример первой строки:
        // "<C0=FFA0A0><C1=A0FFA0><C2=FC89AC><C3=fa2363><S1=70><S2=-50>\n" +
        for (var i = 0; i < colorLib.Count; i++)
        {
            advancedCodeEditor.Append($"<C{i}={colorLib[i]}>");
        }

        advancedCodeEditor.Append("<S1=70><S2=-50>\n");

        // 5.2 Вторая строка (Saku Overclock)
        // Пример второй строки:
        // "<C0>Saku Overclock <C1>" + ViewModels.ГлавнаяViewModel.GetVersion() + ": <S0>$SelectedPreset$\n" +
        if (enableLib[2])
        {
            var colorIndexMain = RtssSettings.RtssElements[0].Enabled
                ? colorLib.IndexOf(RtssSettings.RtssElements[0].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(RtssSettings.RtssElements[2].Color.Replace("#", "")).ToString();
            var colorIndexSecond = RtssSettings.RtssElements[1].Enabled
                ? colorLib.IndexOf(RtssSettings.RtssElements[1].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(RtssSettings.RtssElements[2].Color.Replace("#", "")).ToString();
            var compactMain = RtssSettings.RtssElements[0].Enabled
                ? compactLib[0] ? "<S1>" : "<S0>"
                : compactLib[2]
                    ? "<S1>"
                    : "<S0>";
            var compactSecond = compactLib[2] ? "<S2>" : "<S0>";
            advancedCodeEditor.Append(
                $"<C{colorIndexMain}>{compactMain}{textLib[0]} {ГлавнаяViewModel.GetVersion()}: <C{colorIndexSecond}>{compactSecond}<S0>$SelectedPreset$\n");
        }

        // 5.3 Третья строка (STAPM Fast Slow)
        // Пример третьей строки:
        // "<S1><C2>STAPM, Fast, Slow: <C3><S0>$stapm_value$<S2>W<S1>$stapm_limit$W <S0>$fast_value$<S2>W<S1>$fast_limit$W <S0>$slow_value$<S2>W<S1>$slow_limit$W\n" +
        if (enableLib[3])
        {
            var colorIndexMain = RtssSettings.RtssElements[0].Enabled
                ? colorLib.IndexOf(RtssSettings.RtssElements[0].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(RtssSettings.RtssElements[3].Color.Replace("#", "")).ToString();
            var colorIndexSecond = RtssSettings.RtssElements[1].Enabled
                ? colorLib.IndexOf(RtssSettings.RtssElements[1].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(RtssSettings.RtssElements[3].Color.Replace("#", "")).ToString();
            var compactMain = RtssSettings.RtssElements[0].Enabled
                ? compactLib[0] ? "<S1>" : "<S0>"
                : compactLib[3]
                    ? "<S1>"
                    : "<S0>";
            var compactSecond = compactLib[3] ? "<S2>" : "<S0>";
            var compactSign = RtssSettings.RtssElements[1].Enabled
                ? compactLib[1] ? "" : "/"
                : compactLib[3]
                    ? ""
                    : "/";
            advancedCodeEditor.Append(
                $"<C{colorIndexMain}>{compactMain}{textLib[1]}: <C{colorIndexSecond}><S0>$stapm_value${compactSecond}W{compactSign}{compactSecond.Replace("2", "1")}$stapm_limit$W <S0>$fast_value${compactSecond}W{compactSign}{compactSecond.Replace("2", "1")}$fast_limit$W <S0>$slow_value${compactSecond}W{compactSign}{compactSecond.Replace("2", "1")}$slow_limit$W\n");
        }

        // - Для EDC Therm CPU Usage
        // Пример четвёртой строки:
        // "<C2>EDC, Therm, CPU Usage: <C3><S0>$vrmedc_value$<S2>A<S1>$vrmedc_max$A <C3><S0>$cpu_temp_value$<S2>C<S1>$cpu_temp_max$C<C3><S0> $cpu_usage$<S2>%<S1>\n" +
        if (enableLib[4])
        {
            var colorIndexMain = RtssSettings.RtssElements[0].Enabled
                ? colorLib.IndexOf(RtssSettings.RtssElements[0].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(RtssSettings.RtssElements[4].Color.Replace("#", "")).ToString();
            var colorIndexSecond = RtssSettings.RtssElements[1].Enabled
                ? colorLib.IndexOf(RtssSettings.RtssElements[1].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(RtssSettings.RtssElements[4].Color.Replace("#", "")).ToString();
            var compactMain = RtssSettings.RtssElements[0].Enabled
                ? compactLib[0] ? "<S1>" : "<S0>"
                : compactLib[4]
                    ? "<S1>"
                    : "<S0>";
            var compactSecond = compactLib[4] ? "<S2>" : "<S0>";
            var compactSign = RtssSettings.RtssElements[1].Enabled
                ? compactLib[1] ? "" : "/"
                : compactLib[4]
                    ? ""
                    : "/";
            advancedCodeEditor.Append(
                $"<C{colorIndexMain}>{compactMain}{textLib[2]}: <C{colorIndexSecond}><S0>$vrmedc_value${compactSecond}A{compactSign}{compactSecond.Replace("2", "1")}$vrmedc_max$A <S0>$cpu_temp_value${compactSecond}C{compactSign}{compactSecond.Replace("2", "1")}$cpu_temp_max$C <S0>$cpu_usage${compactSecond}%\n");
        }

        // - Для CPU Clocks
        // Пример пятой строки:
        // "<S1><C2>Clocks: $cpu_clock_cycle$<S1><C2>$currCore$:<S0><C3> $cpu_core_clock$<S2>GHz<S1>$cpu_core_voltage$V $cpu_clock_cycle_end$\n" +
        if (enableLib[5])
        {
            var colorIndexMain = RtssSettings.RtssElements[0].Enabled
                ? colorLib.IndexOf(RtssSettings.RtssElements[0].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(RtssSettings.RtssElements[5].Color.Replace("#", "")).ToString();
            var colorIndexSecond = RtssSettings.RtssElements[1].Enabled
                ? colorLib.IndexOf(RtssSettings.RtssElements[1].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(RtssSettings.RtssElements[5].Color.Replace("#", "")).ToString();
            var compactMain = RtssSettings.RtssElements[0].Enabled
                ? compactLib[0] ? "<S1>" : "<S0>"
                : compactLib[5]
                    ? "<S1>"
                    : "<S0>";
            var compactSecond = compactLib[5] ? "<S2>" : "<S0>";
            var compactSign = RtssSettings.RtssElements[1].Enabled
                ? compactLib[1] ? "" : "/"
                : compactLib[5]
                    ? ""
                    : "/";
            advancedCodeEditor.Append(
                $"<C{colorIndexMain}>{compactMain}{textLib[3]}: $cpu_clock_cycle$<C{colorIndexMain}>$currCore$: <C{colorIndexSecond}>$cpu_core_clock${compactSecond}GHz{compactSign}{compactSecond.Replace("2", "1")}$cpu_core_voltage$V $cpu_clock_cycle_end$\n");
        }

        // - Для AVG Clock Volt
        // Пример шестой строки:
        // "<C2>AVG Clock, Volt: <C3><S0>$average_cpu_clock$<S2>GHz<S1>$average_cpu_voltage$V" +
        if (enableLib[6])
        {
            var colorIndexMain = RtssSettings.RtssElements[0].Enabled
                ? colorLib.IndexOf(RtssSettings.RtssElements[0].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(RtssSettings.RtssElements[6].Color.Replace("#", "")).ToString();
            var colorIndexSecond = RtssSettings.RtssElements[1].Enabled
                ? colorLib.IndexOf(RtssSettings.RtssElements[1].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(RtssSettings.RtssElements[6].Color.Replace("#", "")).ToString();
            var compactMain = RtssSettings.RtssElements[0].Enabled
                ? compactLib[0] ? "<S1>" : "<S0>"
                : compactLib[6]
                    ? "<S1>"
                    : "<S0>";
            var compactSecond = compactLib[6] ? "<S2>" : "<S0>";
            var compactSign = RtssSettings.RtssElements[1].Enabled
                ? compactLib[1] ? "" : "/"
                : compactLib[6]
                    ? ""
                    : "/";
            advancedCodeEditor.Append(
                $"<C{colorIndexMain}>{compactMain}{textLib[4]}: <C{colorIndexSecond}><S0>$average_cpu_clock${compactSecond}GHz{compactSign}{compactSecond.Replace("2", "1")}$average_cpu_voltage$V\n");
        }

        // - Для APU Clock Volt Temp
        // Пример седьмой строки:
        // "<C2>APU Clock, Volt, Temp: <C3><S0>$gfx_clock$<S2>MHz<S1>$gfx_volt$V <S0>$gfx_temp$<S1>C\n" +
        if (enableLib[7])
        {
            var colorIndexMain = RtssSettings.RtssElements[0].Enabled
                ? colorLib.IndexOf(RtssSettings.RtssElements[0].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(RtssSettings.RtssElements[7].Color.Replace("#", "")).ToString();
            var colorIndexSecond = RtssSettings.RtssElements[1].Enabled
                ? colorLib.IndexOf(RtssSettings.RtssElements[1].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(RtssSettings.RtssElements[7].Color.Replace("#", "")).ToString();
            var compactMain = RtssSettings.RtssElements[0].Enabled
                ? compactLib[0] ? "<S1>" : "<S0>"
                : compactLib[7]
                    ? "<S1>"
                    : "<S0>";
            var compactSecond = compactLib[7] ? "<S2>" : "<S0>";
            var compactSign = RtssSettings.RtssElements[1].Enabled
                ? compactLib[1] ? "" : "/"
                : compactLib[7]
                    ? ""
                    : "/";
            advancedCodeEditor.Append(
                $"<C{colorIndexMain}>{compactMain}{textLib[5]}: <C{colorIndexSecond}><S0>$gfx_clock${compactSecond}MHz{compactSign}{compactSecond.Replace("2", "1")}$gfx_volt$V <S0>$gfx_temp${compactSecond}C\n");
        }

        // - Для Frame Rate
        // Пример восьмой строки:
        // "<C2>Framerate <C3><S0>%FRAMERATE% %FRAMETIME%";*/
        if (enableLib[8])
        {
            var colorIndexMain = RtssSettings.RtssElements[0].Enabled
                ? colorLib.IndexOf(RtssSettings.RtssElements[0].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(RtssSettings.RtssElements[8].Color.Replace("#", "")).ToString();
            var colorIndexSecond = RtssSettings.RtssElements[1].Enabled
                ? colorLib.IndexOf(RtssSettings.RtssElements[1].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(RtssSettings.RtssElements[8].Color.Replace("#", "")).ToString();
            var compactMain = compactLib[8] ? "<S1>" : "<S0>";
            advancedCodeEditor.Append(
                $"<C{colorIndexMain}>{compactMain}{textLib[6]}: <C{colorIndexSecond}><S0>%FRAMERATE% %FRAMETIME%");
        }

        // Финальная строка присваивается в AdvancedCodeEditor
        RtssSettings.AdvancedCodeEditor = advancedCodeEditor.ToString();
        LoadAndFormatAdvancedCodeEditor(RtssSettings.AdvancedCodeEditor);
        RtssHandler.ChangeOsdText(RtssSettings.AdvancedCodeEditor);
        RtssSettings.SaveSettings();
        return;

        void AddColorIfUnique(string color)
        {
            if (!colorLib.Contains(color.Replace("#", "")))
            {
                colorLib.Add(color.Replace("#", ""));
            }
        }
    }

    /// <summary>
    ///     Включает или выключает Rtss, изменяет состояние видимости элементов на странице
    /// </summary>
    private void RtssSettings_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        var rtssVisibility = RtssSettingsEnable.IsOn ? Visibility.Visible : Visibility.Collapsed;
        SettingsRtssEnableName.Visibility = rtssVisibility;
        RtssGridView.Visibility = rtssVisibility;
        RtssAdvancedCodeEditor.Visibility = rtssVisibility;
        RtssAdvancedCodeEditorEditBox.Visibility =
            RtssAdvancedCodeEditor.IsOn ? rtssVisibility : Visibility.Collapsed;

        AppSettings.RtssMetricsEnabled = RtssSettingsEnable.IsOn;
        AppSettings.SaveSettings();
    }

    /// <summary>
    ///     Изменяет состояние настроек Rtss если любая из них была изменена
    /// </summary>
    private void RtssChanged_Checked(object s, object e)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (s is ToggleButton toggleButton)
        {
            if (toggleButton.Name == "RtssAllCompactToggle")
            {
                _isLoaded = false;
                RtssSakuPresetCompactToggle.IsChecked = RtssAllCompactToggle.IsChecked;
                RtssStapmFastSlowCompactToggle.IsChecked = RtssAllCompactToggle.IsChecked;
                RtssEdcThermUsageCompactToggle.IsChecked = RtssAllCompactToggle.IsChecked;
                RtssCpuClocksCompactToggle.IsChecked = RtssAllCompactToggle.IsChecked;
                RtssAvgCpuClockVoltCompactToggle.IsChecked = RtssAllCompactToggle.IsChecked;
                RtssApuClockVoltTempCompactToggle.IsChecked = RtssAllCompactToggle.IsChecked;
                RtssFrameRateCompactToggle.IsChecked = RtssAllCompactToggle.IsChecked;

                RtssSettings.RtssElements[1].UseCompact = toggleButton.IsChecked == true;
                RtssSettings.RtssElements[2].UseCompact = toggleButton.IsChecked == true;
                RtssSettings.RtssElements[3].UseCompact = toggleButton.IsChecked == true;
                RtssSettings.RtssElements[4].UseCompact = toggleButton.IsChecked == true;
                RtssSettings.RtssElements[5].UseCompact = toggleButton.IsChecked == true;
                RtssSettings.RtssElements[6].UseCompact = toggleButton.IsChecked == true;
                RtssSettings.RtssElements[7].UseCompact = toggleButton.IsChecked == true;
                RtssSettings.RtssElements[8].UseCompact = toggleButton.IsChecked == true;
                _isLoaded = true;
            }
            else
            {
                _isLoaded = false;
                RtssAllCompactToggle.IsChecked = RtssSakuPresetCompactToggle.IsChecked &
                                                   RtssStapmFastSlowCompactToggle.IsChecked &
                                                   RtssEdcThermUsageCompactToggle.IsChecked &
                                                   RtssCpuClocksCompactToggle.IsChecked &
                                                   RtssAvgCpuClockVoltCompactToggle.IsChecked &
                                                   RtssApuClockVoltTempCompactToggle.IsChecked &
                                                   RtssFrameRateCompactToggle.IsChecked;
                _isLoaded = true;
            }

            if (toggleButton.Name == "RtssMainColorCompactToggle")
            {
                RtssSettings.RtssElements[0].UseCompact = toggleButton.IsChecked == true;
            }

            if (toggleButton.Name == "RtssAllCompactToggle")
            {
                RtssSettings.RtssElements[1].UseCompact = toggleButton.IsChecked == true;
            }

            if (toggleButton.Name == "RtssSakuPresetCompactToggle")
            {
                RtssSettings.RtssElements[2].UseCompact = toggleButton.IsChecked == true;
            }

            if (toggleButton.Name == "RtssStapmFastSlowCompactToggle")
            {
                RtssSettings.RtssElements[3].UseCompact = toggleButton.IsChecked == true;
            }

            if (toggleButton.Name == "RtssEdcThermUsageCompactToggle")
            {
                RtssSettings.RtssElements[4].UseCompact = toggleButton.IsChecked == true;
            }

            if (toggleButton.Name == "RtssCpuClocksCompactToggle")
            {
                RtssSettings.RtssElements[5].UseCompact = toggleButton.IsChecked == true;
            }

            if (toggleButton.Name == "RtssAvgCpuClockVoltCompactToggle")
            {
                RtssSettings.RtssElements[6].UseCompact = toggleButton.IsChecked == true;
            }

            if (toggleButton.Name == "RtssApuClockVoltTempCompactToggle")
            {
                RtssSettings.RtssElements[7].UseCompact = toggleButton.IsChecked == true;
            }

            if (toggleButton.Name == "RtssFrameRateCompactToggle")
            {
                RtssSettings.RtssElements[8].UseCompact = toggleButton.IsChecked == true;
            }
        }

        if (s is CheckBox checkBox)
        {
            if (checkBox.Name == "RtssMainColorCheckbox")
            {
                RtssSettings.RtssElements[0].Enabled = checkBox.IsChecked == true;
            }

            if (checkBox.Name == "RtssSecondColorCheckbox")
            {
                RtssSettings.RtssElements[1].Enabled = checkBox.IsChecked == true;
            }

            if (checkBox.Name == "RtssSakuOverclockPresetCheckbox")
            {
                RtssSettings.RtssElements[2].Enabled = checkBox.IsChecked == true;
            }

            if (checkBox.Name == "RtssStapmFastSlowCheckbox")
            {
                RtssSettings.RtssElements[3].Enabled = checkBox.IsChecked == true;
            }

            if (checkBox.Name == "RtssEdcThermUsageCheckbox")
            {
                RtssSettings.RtssElements[4].Enabled = checkBox.IsChecked == true;
            }

            if (checkBox.Name == "RtssCpuClocksCheckbox")
            {
                RtssSettings.RtssElements[5].Enabled = checkBox.IsChecked == true;
            }

            if (checkBox.Name == "RtssAvgCpuClockVoltCheckbox")
            {
                RtssSettings.RtssElements[6].Enabled = checkBox.IsChecked == true;
            }

            if (checkBox.Name == "RtssApuClockVoltTempCheckbox")
            {
                RtssSettings.RtssElements[7].Enabled = checkBox.IsChecked == true;
            }

            if (checkBox.Name == "RtssFrameRateCheckbox")
            {
                RtssSettings.RtssElements[8].Enabled = checkBox.IsChecked == true;
            }
        }

        if (s is TextBox textBox)
        {
            if (textBox.Name == "RtssSakuOverclockPresetTextBox")
            {
                RtssSettings.RtssElements[2].Name = textBox.Text;
            }

            if (textBox.Name == "RtssStapmFastSlowTextBox")
            {
                RtssSettings.RtssElements[3].Name = textBox.Text;
            }

            if (textBox.Name == "RtssEdcThermUsageTextBox")
            {
                RtssSettings.RtssElements[4].Name = textBox.Text;
            }

            if (textBox.Name == "RtssCpuClocksTextBox")
            {
                RtssSettings.RtssElements[5].Name = textBox.Text;
            }

            if (textBox.Name == "RtssAvgCpuClockVoltTextBox")
            {
                RtssSettings.RtssElements[6].Name = textBox.Text;
            }

            if (textBox.Name == "RtssApuClockVoltTempTextBox")
            {
                RtssSettings.RtssElements[7].Name = textBox.Text;
            }

            if (textBox.Name == "RtssFrameRateTextBox")
            {
                RtssSettings.RtssElements[8].Name = textBox.Text;
            }
        }

        if (s is ColorPicker colorPicker)
        {
            if (colorPicker.Name == "RtssMainColorColorPicker")
            {
                RtssSettings.RtssElements[0].Color =
                    $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}";
            }

            if (colorPicker.Name == "RtssSecondColorColorPicker")
            {
                RtssSettings.RtssElements[1].Color =
                    $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}";
            }

            if (colorPicker.Name == "RtssSakuOverclockPresetColorPicker")
            {
                RtssSettings.RtssElements[2].Color =
                    $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}";
            }

            if (colorPicker.Name == "RtssStapmFastSlowColorPicker")
            {
                RtssSettings.RtssElements[3].Color =
                    $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}";
            }

            if (colorPicker.Name == "RtssEdcThermUsageColorPicker")
            {
                RtssSettings.RtssElements[4].Color =
                    $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}";
            }

            if (colorPicker.Name == "RtssCpuClocksColorPicker")
            {
                RtssSettings.RtssElements[5].Color =
                    $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}";
            }

            if (colorPicker.Name == "RtssAvgCpuClockVoltColorPicker")
            {
                RtssSettings.RtssElements[6].Color =
                    $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}";
            }

            if (colorPicker.Name == "RtssApuClockVoltTempColorPicker")
            {
                RtssSettings.RtssElements[7].Color =
                    $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}";
            }

            if (colorPicker.Name == "RtssFrameRateColorPicker")
            {
                RtssSettings.RtssElements[8].Color =
                    $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}";
            }
        }

        GenerateAdvancedCodeEditor();
        RtssSettings.SaveSettings();
    }

    /// <summary>
    ///     Включает или выключает AdvancedCodeEditor, изменяет состояние видимости элементов на странице
    /// </summary>
    private void Rtss_AdvancedCodeEditor_ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        RtssAdvancedCodeEditorEditBox.Visibility =
            RtssAdvancedCodeEditor.IsOn ? Visibility.Visible : Visibility.Collapsed;
        if (!_isLoaded)
        {
            return;
        }

        RtssAdvancedCodeEditorGrid.CornerRadius = RtssAdvancedCodeEditor.IsOn
            ? new CornerRadius(15, 15, 0, 0)
            : new CornerRadius(15);

        RtssSettings.IsAdvancedCodeEditorEnabled = RtssAdvancedCodeEditor.IsOn;
        RtssSettings.SaveSettings();
    }

    /// <summary>
    ///     Сохраняет изменения из AdvancedCodeEditor
    /// </summary>
    private void Rtss_AdvancedCodeEditor_EditBox_TextChanged(object sender, RoutedEventArgs e)
    {
        RtssAdvancedCodeEditorEditBox.Document.GetText(TextGetOptions.None, out var newString);
        RtssSettings.AdvancedCodeEditor = newString.Replace("\r", "\n").TrimEnd();
        RtssSettings.SaveSettings();
    }

    #endregion
}