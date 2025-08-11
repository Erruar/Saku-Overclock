using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Win32.TaskScheduler;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.JsonContainers;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.Styles;
using Saku_Overclock.ViewModels;
using Saku_Overclock.Wrappers;
using Windows.Foundation.Metadata;
using Windows.UI;
using Windows.UI.Text;
using static System.Environment;
using Task = System.Threading.Tasks.Task;
using TextGetOptions = Microsoft.UI.Text.TextGetOptions;
using TextSetOptions = Microsoft.UI.Text.TextSetOptions;

namespace Saku_Overclock.Views;

public sealed partial class SettingsPage
{
    public SettingsViewModel ViewModel
    {
        get;
    }
    private readonly IThemeSelectorService _themeSelectorService = App.GetService<IThemeSelectorService>();
    private NiIconsSettings _niicons = new();
    private bool _isLoaded;
    private static readonly IAppNotificationService NotificationsService = App.GetService<IAppNotificationService>();
    private static readonly ISendSmuCommandService SendSmuCommand = App.GetService<ISendSmuCommandService>();
    private static readonly IAppSettingsService AppSettings = App.GetService<IAppSettingsService>();
    private static readonly IRtssSettingsService RtssSettings = App.GetService<IRtssSettingsService>();
    private static Profile[] _profile = new Profile[1]; // Всегда по умолчанию будет 1 профиль
    public ObservableCollection<string> Presets
    {
        get;
    } 
        = 
    [
    "Пресет 1",
    "Пресет 2",
    "Пресет 3",
    "Пресет 4"
    ];

    public SettingsPage()
    {
        ViewModel = App.GetService<SettingsViewModel>();
        InitializeComponent();
        InitVal();
        Loaded += LoadedApp; // Приложение загружено - разрешить изменения UI
    }

    #region JSON and Initialization

    private async void InitVal()
    {
        try
        {
            try
            {
                AutostartCom.SelectedIndex = AppSettings.AutostartType;
            }
            catch
            {
                AutostartCom.SelectedIndex = 0;
            }
            try
            {
                HideCom.SelectedIndex = AppSettings.HidingType;
            }
            catch
            {
                HideCom.SelectedIndex = 2;
            }

            CbApplyStart.IsOn = AppSettings.ReapplyLatestSettingsOnAppLaunch;
            CbAutoReapply.IsOn = AppSettings.ReapplyOverclock;
            nudAutoReapply.Value = AppSettings.ReapplyOverclockTimer;
            CbAutoCheck.IsOn = AppSettings.CheckForUpdates;
            ReapplySafe.IsOn = AppSettings.ReapplySafeOverclock;
            ThemeLight.Visibility = AppSettings.ThemeType > 7 ? Visibility.Visible : Visibility.Collapsed;
            ThemeCustomBg.IsEnabled = AppSettings.ThemeType > 7;
            Settings_RTSS_Enable.IsOn = AppSettings.RtssMetricsEnabled;
            Settings_Keybinds_Enable.IsOn = AppSettings.HotkeysEnabled;
            RTSS_LoadAndApply();
            UpdateTheme_ComboBox();
            NiIcon_LoadValues(); 
            LoadProfiles();
            await Task.Delay(390);
        }
        catch (Exception e)
        {
            await LogHelper.TraceIt_TraceError(e.ToString());
        }
    }
    private void LoadProfiles()
    {
        Presets.Clear();
        ProfileLoad();
        foreach (var profile in _profile)
        {
            Presets.Add(profile.profilename);
        }
    }
    private void UpdateTheme_ComboBox()
    {
        ThemeCombobox.Items.Clear();
        try
        {
            if (_themeSelectorService.Themes.Count != 0)
            {
                foreach (var theme in _themeSelectorService.Themes)
                {
                    try
                    {
                        if (theme.ThemeName.Contains("Theme_"))
                        {
                            ThemeCombobox.Items.Add(theme.ThemeName.GetLocalized());
                        }
                        else
                        {
                            ThemeCombobox.Items.Add(theme.ThemeName);
                        }
                    }
                    catch
                    {
                        ThemeCombobox.Items.Add(theme.ThemeName);
                    }
                }

                ThemeOpacity.Value = _themeSelectorService.Themes[AppSettings.ThemeType].ThemeOpacity;
                ThemeMaskOpacity.Value = _themeSelectorService.Themes[AppSettings.ThemeType].ThemeMaskOpacity;
                ThemeCustom.IsOn = _themeSelectorService.Themes[AppSettings.ThemeType].ThemeCustom;
                ThemeCustomBg.IsOn = _themeSelectorService.Themes[AppSettings.ThemeType].ThemeCustomBg;
                Theme_Custom();
            }

            ThemeCombobox.SelectedIndex = AppSettings.ThemeType;
            ThemeLight.Visibility =
                AppSettings.ThemeType > 7 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch
        {
            try
            {
                AppSettings.ThemeType /= 2;
            }
            catch
            {
                AppSettings.ThemeType = 0;
            } // Нельзя делить на ноль

            AppSettings.SaveSettings();
        }
    }

    private void Theme_Custom()
    {
        if (!ThemeCustom.IsOn)
        {
            ThemeOpacity.Visibility = Visibility.Collapsed;
            ThemeMaskOpacity.Visibility = Visibility.Collapsed;
            ThemeMaskOpacity.Visibility = Visibility.Collapsed;
            ThemeCustomBg.Visibility = Visibility.Collapsed;
            ThemeLight.Visibility = Visibility.Collapsed;
            ThemeBgButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            ThemeOpacity.Visibility = Visibility.Visible;
            ThemeMaskOpacity.Visibility = Visibility.Visible;
            ThemeMaskOpacity.Visibility = Visibility.Visible;
            ThemeCustomBg.IsEnabled = AppSettings.ThemeType > 7;
            ThemeCustomBg.Visibility = Visibility.Visible;
            ThemeLight.Visibility = Visibility.Visible;
            ThemeBgButton.Visibility = Visibility.Visible;
            ThemeBgButton.Visibility = ThemeCustomBg.IsOn ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void LoadedApp(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
    }

    private void RTSS_LoadAndApply()
    {
        Settings_RTSS_Enable_Name.Visibility = Settings_RTSS_Enable.IsOn ? Visibility.Visible : Visibility.Collapsed;
        RTTS_GridView.Visibility = Settings_RTSS_Enable.IsOn ? Visibility.Visible : Visibility.Collapsed;
        RTSS_AdvancedCodeEditor_ToggleSwitch.Visibility =
            Settings_RTSS_Enable.IsOn ? Visibility.Visible : Visibility.Collapsed;
        RTSS_AdvancedCodeEditor_EditBox_Scroll.Visibility =
            Settings_RTSS_Enable.IsOn ? Visibility.Visible : Visibility.Collapsed;
        LoadAndFormatAdvancedCodeEditor(RtssSettings.AdvancedCodeEditor);
        RTSS_AdvancedCodeEditor_ToggleSwitch.IsOn = RtssSettings.IsAdvancedCodeEditorEnabled;

        // Проход по элементам RTSS_Elements
        for (var i = 0; i <= 8; i++)
        {
            // Получаем имя элемента в зависимости от текущего значения i
            var toggleName = string.Empty;
            var checkBoxName = string.Empty;
            var textBoxName = string.Empty;
            var colorPickerName = string.Empty;

            switch (i)
            {
                case 0:
                    toggleName = "RTSS_MainColor_CompactToggle";
                    checkBoxName = "RTSS_MainColor_Checkbox";
                    textBoxName = string.Empty; // Здесь TextBox нет
                    colorPickerName = "RTSS_MainColor_ColorPicker";
                    break;
                case 1:
                    toggleName = "RTSS_AllCompact_Toggle";
                    checkBoxName = "RTSS_SecondColor_Checkbox";
                    textBoxName = string.Empty; // Здесь TextBox нет
                    colorPickerName = "RTSS_SecondColor_ColorPicker";
                    break;
                case 2:
                    toggleName = "RTSS_SakuProfile_CompactToggle";
                    checkBoxName = "RTSS_SakuOverclockProfile_Checkbox";
                    textBoxName = "RTSS_SakuOverclockProfile_TextBox";
                    colorPickerName = "RTSS_SakuOverclockProfile_ColorPicker";
                    break;
                case 3:
                    toggleName = "RTSS_StapmFastSlow_CompactToggle";
                    checkBoxName = "RTSS_StapmFastSlow_Checkbox";
                    textBoxName = "RTSS_StapmFastSlow_TextBox";
                    colorPickerName = "RTSS_StapmFastSlow_ColorPicker";
                    break;
                case 4:
                    toggleName = "RTSS_EDCThermUsage_CompactToggle";
                    checkBoxName = "RTSS_EDCThermUsage_Checkbox";
                    textBoxName = "RTSS_EDCThermUsage_TextBox";
                    colorPickerName = "RTSS_EDCThermUsage_ColorPicker";
                    break;
                case 5:
                    toggleName = "RTSS_CPUClocks_CompactToggle";
                    checkBoxName = "RTSS_CPUClocks_Checkbox";
                    textBoxName = "RTSS_CPUClocks_TextBox";
                    colorPickerName = "RTSS_CPUClocks_ColorPicker";
                    break;
                case 6:
                    toggleName = "RTSS_AVGCPUClockVolt_CompactToggle";
                    checkBoxName = "RTSS_AVGCPUClockVolt_Checkbox";
                    textBoxName = "RTSS_AVGCPUClockVolt_TextBox";
                    colorPickerName = "RTSS_AVGCPUClockVolt_ColorPicker";
                    break;
                case 7:
                    toggleName = "RTSS_APUClockVoltTemp_CompactToggle";
                    checkBoxName = "RTSS_APUClockVoltTemp_Checkbox";
                    textBoxName = "RTSS_APUClockVoltTemp_TextBox";
                    colorPickerName = "RTSS_APUClockVoltTemp_ColorPicker";
                    break;
                case 8:
                    toggleName = "RTSS_FrameRate_CompactToggle";
                    checkBoxName = "RTSS_FrameRate_Checkbox";
                    textBoxName = "RTSS_FrameRate_TextBox";
                    colorPickerName = "RTSS_FrameRate_ColorPicker";
                    break;
            }

            // Применение значения ToggleButton
            if (!string.IsNullOrEmpty(toggleName))
            {
                var toggleButton = (ToggleButton)FindName(toggleName);
                if (toggleButton != null)
                {
                    toggleButton.IsChecked = RtssSettings.RTSS_Elements[i].UseCompact;
                }
            }

            // Применение значения CheckBox
            if (!string.IsNullOrEmpty(checkBoxName))
            {
                var checkBox = (CheckBox)FindName(checkBoxName);
                if (checkBox != null)
                {
                    checkBox.IsChecked = RtssSettings.RTSS_Elements[i].Enabled;
                }
            }

            // Применение значения TextBox
            if (!string.IsNullOrEmpty(textBoxName))
            {
                var textBox = (TextBox)FindName(textBoxName);
                if (textBox != null)
                {
                    textBox.Text = RtssSettings.RTSS_Elements[i].Name;
                }
            }

            // Применение значения ColorPicker
            if (!string.IsNullOrEmpty(colorPickerName))
            {
                var colorPicker = (ColorPicker)FindName(colorPickerName);
                if (colorPicker != null)
                {
                    var color = RtssSettings.RTSS_Elements[i].Color;
                    var r = Convert.ToByte(color.Substring(1, 2), 16);
                    var g = Convert.ToByte(color.Substring(3, 2), 16);
                    var b = Convert.ToByte(color.Substring(5, 2), 16);
                    colorPicker.Color = Color.FromArgb(255, r, g, b);
                }
            }
        }
    }

    private static Color ParseColor(string hex)
    {
        if (hex.Length == 6)
        {
            return Color.FromArgb(255,
                byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber),
                byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber),
                byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber));
        }

        return Color.FromArgb(255, 255, 255, 255); // если цвет неизвестен
    }

    // Вспомогательный метод для преобразования HEX в Windows.UI.Color
    private void LoadAndFormatAdvancedCodeEditor(string advancedCode)
    {
        if (string.IsNullOrEmpty(advancedCode))
        {
            return;
        }

        RTSS_AdvancedCodeEditor_EditBox.Document.SetText(TextSetOptions.None,
            advancedCode.Replace("<Br>", "\n").TrimEnd());
    }

    private void NiSave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(GetFolderPath(SpecialFolder.Personal),
                "SakuOverclock"));
            File.WriteAllText(
                GetFolderPath(SpecialFolder.Personal) + "\\SakuOverclock\\niicons.json",
                JsonConvert.SerializeObject(_niicons, Formatting.Indented));
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex.ToString());
        }
    }

    private void NiLoad()
    {
        try
        {
            _niicons = JsonConvert.DeserializeObject<NiIconsSettings>(File.ReadAllText(
                GetFolderPath(SpecialFolder.Personal) + "\\SakuOverclock\\niicons.json"))!;
        }
        catch
        {
            _niicons = new NiIconsSettings();
            NiSave();
        }
    }

    private static void ProfileLoad()
    {
        try
        {
            _profile = JsonConvert.DeserializeObject<Profile[]>(File.ReadAllText(
                GetFolderPath(SpecialFolder.Personal) + @"\SakuOverclock\profile.json"))!;
        }
        catch (Exception ex)
        { 
            LogHelper.TraceIt_TraceError(ex.ToString());
        }
    }
    #endregion

    #region Event Handlers

    private void Discord_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://discord.com/invite/yVsKxqAaa7") { UseShellExecute = true });
    }

    private void Settings_Keybinds_Enable_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        Settings_Keybinds_Tooltip.IsOpen = true;
        AppSettings.HotkeysEnabled = Settings_Keybinds_Enable.IsOn;
        AppSettings.SaveSettings();
    }

    private void AutostartCom_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        AppSettings.AutostartType = AutostartCom.SelectedIndex;
        var autoruns = new TaskService();
        if (AutostartCom.SelectedIndex == 2 || AutostartCom.SelectedIndex == 3)
        {
            var pathToExecutableFile = Assembly.GetExecutingAssembly().Location;
            var pathToProgramDirectory = Path.GetDirectoryName(pathToExecutableFile);
            var pathToStartupLnk = Path.Combine(pathToProgramDirectory!, "Saku Overclock.exe");
            // Добавить программу в автозагрузку
            var sakuTask = autoruns.NewTask();
            sakuTask.RegistrationInfo.Description =
                "An awesome ryzen laptop overclock utility for those who want real performance! Autostart Saku Overclock application task";
            sakuTask.RegistrationInfo.Author = "Sakura Serzhik";
            sakuTask.RegistrationInfo.Version = new Version("1.0.0");
            sakuTask.Principal.RunLevel = TaskRunLevel.Highest;
            sakuTask.Triggers.Add(new LogonTrigger { Enabled = true });
            sakuTask.Actions.Add(new ExecAction(pathToStartupLnk));
            autoruns.RootFolder.RegisterTaskDefinition(@"Saku Overclock", sakuTask);
        }
        else
        {
            try
            {
                autoruns.RootFolder.DeleteTask("Saku Overclock");
            }
            catch (Exception exception)
            {
                LogHelper.TraceIt_TraceError(exception.ToString());
            }
        }

        AppSettings.SaveSettings();
    }

    private void HideCom_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }
        AppSettings.HidingType = HideCom.SelectedIndex;
        AppSettings.SaveSettings();
    }

    private void CbApplyStart_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        AppSettings.ReapplyLatestSettingsOnAppLaunch = CbApplyStart.IsOn;

        AppSettings.SaveSettings();
    }

    private void CbAutoReapply_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (CbAutoReapply.IsOn)
        {
            AutoReapplyNumberboxPanel.Visibility = Visibility.Visible;
            AppSettings.ReapplyOverclock = true;
            AppSettings.ReapplyOverclockTimer = nudAutoReapply.Value;
        }
        else
        {
            AutoReapplyNumberboxPanel.Visibility = Visibility.Collapsed;
            AppSettings.ReapplyOverclock = false;
            AppSettings.ReapplyOverclockTimer = 3;
        }

        AppSettings.SaveSettings();
    }

    private void CbAutoCheck_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        AppSettings.CheckForUpdates = CbAutoCheck.IsOn;

        AppSettings.SaveSettings();
    }

    private async void NudAutoReapply_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        try
        {
            if (!_isLoaded)
            {
                return;
            }

            await Task.Delay(20);
            AppSettings.ReapplyOverclock = true;
            AppSettings.ReapplyOverclockTimer = nudAutoReapply.Value;
            AppSettings.SaveSettings();
        }
        catch (Exception ex)
        {
            await LogHelper.LogError(ex.ToString());
        }
    }

    private async void ReapplySafe_Toggled(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_isLoaded)
            {
                return;
            }

            await Task.Delay(20);
            AppSettings.ReapplySafeOverclock = ReapplySafe.IsOn;
            SendSmuCommand.GetSetSafeReapply(ReapplySafe.IsOn);
            AppSettings.SaveSettings();
        }
        catch (Exception ex)
        {
            await LogHelper.LogError(ex.ToString());
        }
    }

    private void ThemeCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        AppSettings.ThemeType = ThemeCombobox.SelectedIndex != -1 ? ThemeCombobox.SelectedIndex : 0;
        AppSettings.SaveSettings();
        if (_themeSelectorService.Themes.Count != 0)
        {
            try
            {
                ViewModel.SwitchThemeCommand.Execute(_themeSelectorService.Themes[AppSettings.ThemeType].ThemeLight
                    ? ElementTheme.Light
                    : ElementTheme.Dark);
            }
            catch
            {
                AppSettings.ThemeType = 0;
                AppSettings.SaveSettings();
            }

            if (AppSettings.ThemeType == 0)
            {
                ViewModel.SwitchThemeCommand.Execute(ElementTheme.Default);
            }

            ThemeCustom.IsOn = _themeSelectorService.Themes[AppSettings.ThemeType].ThemeCustom;
            ThemeOpacity.Value = _themeSelectorService.Themes[AppSettings.ThemeType].ThemeOpacity;
            ThemeMaskOpacity.Value = _themeSelectorService.Themes[AppSettings.ThemeType].ThemeMaskOpacity;
            ThemeCustomBg.IsOn = _themeSelectorService.Themes[AppSettings.ThemeType].ThemeCustomBg;
            ThemeCustomBg.IsEnabled = ThemeCombobox.SelectedIndex > 7;
            ThemeLight.IsOn = _themeSelectorService.Themes[AppSettings.ThemeType].ThemeLight;
            ThemeLight.Visibility = ThemeCombobox.SelectedIndex > 7 ? Visibility.Visible : Visibility.Collapsed;
            ThemeBgButton.Visibility = ThemeCustomBg.IsOn ? Visibility.Visible : Visibility.Collapsed;
            Theme_Custom();
            NotificationsService.Notifies ??= [];
            NotificationsService.Notifies.Add(new Notify
            {
                Title = "Theme applied!",
                Msg = "DEBUG MESSAGE. YOU SHOULDN'T SEE THIS",
                Type = InfoBarSeverity.Success
            });
            NotificationsService.SaveNotificationsSettings();
        }
    }

    private void ThemeCustom_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        Theme_Custom();
        _themeSelectorService.Themes[AppSettings.ThemeType].ThemeCustom = ThemeCustom.IsOn;
        _themeSelectorService.SaveThemeInSettings();
    }

    private void ThemeOpacity_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_isLoaded)
        {
            return;
        }

        _themeSelectorService.Themes[AppSettings.ThemeType].ThemeOpacity = ThemeOpacity.Value;
        _themeSelectorService.SaveThemeInSettings();
        NotificationsService.Notifies ??= [];
        NotificationsService.Notifies.Add(new Notify
        {
            Title = "Theme applied!",
            Msg = "DEBUG MESSAGE. YOU SHOULDN'T SEE THIS",
            Type = InfoBarSeverity.Success
        });
        NotificationsService.SaveNotificationsSettings();
    }

    private void ThemeMaskOpacity_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_isLoaded)
        {
            return;
        }

        _themeSelectorService.Themes[AppSettings.ThemeType].ThemeMaskOpacity = ThemeMaskOpacity.Value;
        _themeSelectorService.SaveThemeInSettings();
        NotificationsService.Notifies ??= [];
        NotificationsService.Notifies.Add(new Notify
        {
            Title = "Theme applied!",
            Msg = "DEBUG MESSAGE. YOU SHOULDN'T SEE THIS",
            Type = InfoBarSeverity.Success
        });
        NotificationsService.SaveNotificationsSettings();
    }

    private void ThemeCustomBg_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        _themeSelectorService.Themes[AppSettings.ThemeType].ThemeCustomBg = ThemeCustomBg.IsOn;
        _themeSelectorService.SaveThemeInSettings();
        ThemeBgButton.Visibility = ThemeCustomBg.IsOn ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void ThemeBgButton_Click(object sender, RoutedEventArgs e)
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
                Translation = new System.Numerics.Vector3(0, 0, 12),
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
                Translation = new System.Numerics.Vector3(0, 0, 12),
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
                        },
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
                CloseButtonText = "Cancel".GetLocalized(),
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
            if (result == ContentDialogResult.Primary)
            {
                if (endStringPath != "")
                {
                    var backupIndex = ThemeCombobox.SelectedIndex;
                    _themeSelectorService.Themes[backupIndex].ThemeBackground = endStringPath;
                    _themeSelectorService.SaveThemeInSettings();
                    NotificationsService.Notifies ??= [];
                    NotificationsService.Notifies.Add(new Notify
                    {
                        Title = "Theme applied!",
                        Msg = "DEBUG MESSAGE. YOU SHOULDN'T SEE THIS",
                        Type = InfoBarSeverity.Success
                    });
                    NotificationsService.SaveNotificationsSettings();
                    ThemeCombobox.SelectedIndex = 0;
                    ThemeCombobox.SelectedIndex = backupIndex;
                }
            }
        }
        catch (Exception ex)
        {
            await LogHelper.TraceIt_TraceError(ex.ToString());
        }
    }

    private async void CustomTheme_Click(object sender, RoutedEventArgs e)
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

                        var name = baseThemeName; // Фикс асинхронности, чтобы не получить Com Interop Exception
                        newNameThemeSetButton.Click += (_, _) =>
                        {
                            if (textBoxThemeName.Text != "" || textBoxThemeName.Text != name)
                            {
                                _themeSelectorService.Themes[int.Parse(sureDelete.Name)].ThemeName = textBoxThemeName.Text;
                                themeNameText.Text = textBoxThemeName.Text;
                                editFlyout.Hide();
                                _themeSelectorService.SaveThemeInSettings();
                                InitVal();
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
                                InitVal();
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
                                InitVal();
                            }
                            catch (Exception ex)
                            {
                                LogHelper.LogError(ex.ToString());
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

            _ = await themerDialog.ShowAsync();
        }
        catch (Exception ex)
        {
            await LogHelper.LogError(ex.ToString());
        }
    }


    private void ThemeLight_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        _themeSelectorService.Themes[AppSettings.ThemeType].ThemeLight = ThemeLight.IsOn;
        _themeSelectorService.SaveThemeInSettings();
        NotificationsService.Notifies ??= [];
        NotificationsService.Notifies.Add(new Notify
        {
            Title = "Theme applied!",
            Msg = "DEBUG MESSAGE. YOU SHOULDN'T SEE THIS",
            Type = InfoBarSeverity.Success
        });
        NotificationsService.SaveNotificationsSettings();
    }

    private void C2t_FocusEngaged(object sender, object args)
    {
        if (sender is NumberBox numberBox)
        {
            numberBox.SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden;
        }
    }

    private void C2t_FocusDisengaged(object sender, object args)
    {
        if (sender is NumberBox numberBox)
        {
            numberBox.SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline;
        }
    }

    #endregion

    #region Ni Icons (tray icons) Related Section

    private void NiIcon_LoadValues()
    {
        NiLoad();
        try
        {
            Settings_ni_Icons.IsOn = AppSettings.NiIconsEnabled;
            NiIconComboboxElements.Items.Clear();
            if (_niicons.Elements.Count != 0)
            {
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
                if (NiIconComboboxElements.SelectedIndex >= 0)
                {
                    NiIcon_Stackpanel.Visibility = Visibility.Visible;
                    Settings_ni_ContextMenu.Visibility = Visibility.Visible;
                    Settings_NiIconComboboxElements.Visibility = Visibility.Visible;
                    Settings_ni_EnabledElement.Visibility = Visibility.Visible;
                }
                if (AppSettings.NiIconsType >= 0 && _niicons.Elements.Count >= AppSettings.NiIconsType)
                {
                    Settings_ni_EnabledElement.IsOn = _niicons.Elements[AppSettings.NiIconsType].IsEnabled;
                    if (!_niicons.Elements[AppSettings.NiIconsType].IsEnabled)
                    {
                        NiIcon_Stackpanel.Visibility = Visibility.Collapsed;
                        Settings_ni_ContextMenu.Visibility = Visibility.Collapsed;
                    }

                    NiIconCombobox.SelectedIndex =
                        _niicons.Elements[AppSettings.NiIconsType].ContextMenuType;
                    NiIcons_ColorPicker_ColorPicker.Color =
                        ParseColor(_niicons.Elements[AppSettings.NiIconsType].Color);
                    Settings_Ni_GradientToggle.IsOn = _niicons.Elements[AppSettings.NiIconsType].IsGradient;
                    NiIconShapeCombobox.SelectedIndex = _niicons.Elements[AppSettings.NiIconsType].IconShape;
                    Settings_ni_Fontsize.Value = _niicons.Elements[AppSettings.NiIconsType].FontSize;
                    Settings_ni_Opacity.Value = _niicons.Elements[AppSettings.NiIconsType].BgOpacity;
                }
            }

            if (Settings_ni_Icons.IsOn)
            {
                Settings_ni_Icons_Element.Visibility = Visibility.Visible;
                Settings_NiIconComboboxElements.Visibility = Visibility.Visible;
                Settings_ni_Add_Element.Visibility = Visibility.Visible;
                Settings_ni_EnabledElement.Visibility = Visibility.Visible;
                if (NiIconComboboxElements.SelectedIndex >= 0 && Settings_ni_EnabledElement.IsOn)
                {
                    NiIcon_Stackpanel.Visibility = Visibility.Visible;
                    Settings_ni_ContextMenu.Visibility = Visibility.Visible;
                }
                else
                {
                    NiIcon_Stackpanel.Visibility = Visibility.Collapsed;
                    Settings_ni_ContextMenu.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                Settings_ni_Icons_Element.Visibility = Visibility.Collapsed;
                NiIcon_Stackpanel.Visibility = Visibility.Collapsed;
                Settings_ni_ContextMenu.Visibility = Visibility.Collapsed;
                Settings_NiIconComboboxElements.Visibility = Visibility.Collapsed;
                Settings_ni_Add_Element.Visibility = Visibility.Collapsed;
                Settings_ni_EnabledElement.Visibility = Visibility.Collapsed;
            }
        }
        catch
        {
            AppSettings.NiIconsType = -1; // Нет сохранённых
            AppSettings.SaveSettings();
        }
    }

    private void NiIconComboboxElements_SelectionChanged(object sender, SelectionChangedEventArgs e)
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
                NiIcon_Stackpanel.Visibility = Visibility.Visible;
                Settings_ni_ContextMenu.Visibility = Visibility.Visible;
                Settings_NiIconComboboxElements.Visibility = Visibility.Visible;
                Settings_ni_EnabledElement.Visibility = Visibility.Visible;
            }

            Settings_ni_EnabledElement.IsOn = _niicons.Elements[AppSettings.NiIconsType].IsEnabled;
            if (!_niicons.Elements[AppSettings.NiIconsType].IsEnabled)
            {
                NiIcon_Stackpanel.Visibility = Visibility.Collapsed;
                Settings_ni_ContextMenu.Visibility = Visibility.Collapsed;
            }

            NiIconCombobox.SelectedIndex = _niicons.Elements[AppSettings.NiIconsType].ContextMenuType;
            NiIcons_ColorPicker_ColorPicker.Color =
                ParseColor(_niicons.Elements[AppSettings.NiIconsType].Color);
            Settings_Ni_GradientToggle.IsOn = _niicons.Elements[AppSettings.NiIconsType].IsGradient;
            NiIconShapeCombobox.SelectedIndex = _niicons.Elements[AppSettings.NiIconsType].IconShape;
            Settings_ni_Fontsize.Value = _niicons.Elements[AppSettings.NiIconsType].FontSize;
            Settings_ni_Opacity.Value = _niicons.Elements[AppSettings.NiIconsType].BgOpacity;
        }
    }

    private void NiIconCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
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

    private void Settings_ni_Icons_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        AppSettings.NiIconsEnabled = Settings_ni_Icons.IsOn;
        AppSettings.SaveSettings();
        if (Settings_ni_Icons.IsOn)
        {
            Settings_ni_Icons_Element.Visibility = Visibility.Visible;
            Settings_NiIconComboboxElements.Visibility = Visibility.Visible;
            Settings_ni_Add_Element.Visibility = Visibility.Visible;
            Settings_ni_EnabledElement.Visibility = Visibility.Visible;
            if (NiIconComboboxElements.SelectedIndex >= 0 && Settings_ni_EnabledElement.IsOn)
            {
                NiIcon_Stackpanel.Visibility = Visibility.Visible;
                Settings_ni_ContextMenu.Visibility = Visibility.Visible;
            }
            else
            {
                NiIcon_Stackpanel.Visibility = Visibility.Collapsed;
                Settings_ni_ContextMenu.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            Settings_ni_Icons_Element.Visibility = Visibility.Collapsed;
            NiIcon_Stackpanel.Visibility = Visibility.Collapsed;
            Settings_ni_ContextMenu.Visibility = Visibility.Collapsed;
            Settings_NiIconComboboxElements.Visibility = Visibility.Collapsed;
            Settings_ni_Add_Element.Visibility = Visibility.Collapsed;
            Settings_ni_EnabledElement.Visibility = Visibility.Collapsed;
        }

        App.BackgroundUpdater?.UpdateNotifyIcons();
    }

    private void Settings_ni_EnabledElement_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        NiLoad();
        _niicons.Elements[AppSettings.NiIconsType].IsEnabled = Settings_ni_EnabledElement.IsOn;
        NiSave();
        if (NiIconComboboxElements.SelectedIndex >= 0 && Settings_ni_EnabledElement.IsOn)
        {
            NiIcon_Stackpanel.Visibility = Visibility.Visible;
            Settings_ni_ContextMenu.Visibility = Visibility.Visible;
        }
        else
        {
            NiIcon_Stackpanel.Visibility = Visibility.Collapsed;
            Settings_ni_ContextMenu.Visibility = Visibility.Collapsed;
        }

        App.BackgroundUpdater?.UpdateNotifyIcons();
    }

    private void Settings_ni_Fontsize_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_isLoaded)
        {
            return;
        }

        NiLoad();
        _niicons.Elements[AppSettings.NiIconsType].FontSize =
            Convert.ToInt32(Settings_ni_Fontsize.Value);
        NiSave();

        App.BackgroundUpdater?.UpdateNotifyIcons();
    }

    private void Settings_ni_Opacity_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_isLoaded)
        {
            return;
        }

        NiLoad();
        _niicons.Elements[AppSettings.NiIconsType].BgOpacity = Settings_ni_Opacity.Value;
        NiSave();

        App.BackgroundUpdater?.UpdateNotifyIcons();
    }

    private async void Settings_ni_Add_Element_Click(object sender, RoutedEventArgs e)
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
                                NiIcon_LoadValues();
                            }
                            catch (Exception ex)
                            {
                                LogHelper.LogError(ex.ToString());
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

            _ = await niAddIconDialog.ShowAsync();
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception.ToString());
        }

        App.BackgroundUpdater?.UpdateNotifyIcons();
    }

    private void NiIcons_ColorPicker_ColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (!_isLoaded)
        {
            return;
        }

        NiLoad();
        if (Settings_Ni_GradientColorSwitcher.IsChecked == false)
        {
            _niicons.Elements[AppSettings.NiIconsType].Color =
                $"{NiIcons_ColorPicker_ColorPicker.Color.R:X2}{NiIcons_ColorPicker_ColorPicker.Color.G:X2}{NiIcons_ColorPicker_ColorPicker.Color.B:X2}";
        }
        else if (Settings_Ni_GradientColorSwitcher.IsChecked == true)
        {
            _niicons.Elements[AppSettings.NiIconsType].SecondColor =
                $"{NiIcons_ColorPicker_ColorPicker.Color.R:X2}{NiIcons_ColorPicker_ColorPicker.Color.G:X2}{NiIcons_ColorPicker_ColorPicker.Color.B:X2}";
        }

        NiSave();

        App.BackgroundUpdater?.UpdateNotifyIcons();
    }

    private void Settings_Ni_GradientToggle_Toggled(object sender, RoutedEventArgs e)
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

    private void Settings_Ni_GradientColorSwitcher_Click(object sender, RoutedEventArgs e)
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
                NiIcons_ColorPicker_ColorPicker.Color =
                    ParseColor(_niicons.Elements[AppSettings.NiIconsType].SecondColor);
            }
            else if (button.IsChecked == false)
            {
                button.Content = "Settings_ni_TrayMonGradientColorSwitch/Content".GetLocalized() + "1";
                NiIcons_ColorPicker_ColorPicker.Color =
                    ParseColor(_niicons.Elements[AppSettings.NiIconsType].Color);
            }
        }

        App.BackgroundUpdater?.UpdateNotifyIcons();
    }

    private void Settings_ni_Delete_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        try
        {
            NiLoad();
            _niicons.Elements.RemoveAt(AppSettings.ThemeType);
            NiSave();
            AppSettings.ThemeType = -1;
            AppSettings.SaveSettings();
            NiIcon_LoadValues();

            App.BackgroundUpdater?.UpdateNotifyIcons();
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex.ToString());
        }
    }

    private void Settings_ni_ResetDef_Click(object sender, RoutedEventArgs e)
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
        NiIconComboboxElements_SelectionChanged(NiIconComboboxElements, SelectionChangedEventArgs.FromAbi(0));

        App.BackgroundUpdater?.UpdateNotifyIcons();
    }

    private void NiIconShapeCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
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

    #endregion

    #region RTSS Related Section

    private void RTSSChanged_Checked(object s, object e)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (s is ToggleButton toggleButton)
        {
            if (toggleButton.Name == "RTSS_AllCompact_Toggle")
            {
                _isLoaded = false;
                RTSS_SakuProfile_CompactToggle.IsChecked = RTSS_AllCompact_Toggle.IsChecked;
                RTSS_StapmFastSlow_CompactToggle.IsChecked = RTSS_AllCompact_Toggle.IsChecked;
                RTSS_EDCThermUsage_CompactToggle.IsChecked = RTSS_AllCompact_Toggle.IsChecked;
                RTSS_CPUClocks_CompactToggle.IsChecked = RTSS_AllCompact_Toggle.IsChecked;
                RTSS_AVGCPUClockVolt_CompactToggle.IsChecked = RTSS_AllCompact_Toggle.IsChecked;
                RTSS_APUClockVoltTemp_CompactToggle.IsChecked = RTSS_AllCompact_Toggle.IsChecked;
                RTSS_FrameRate_CompactToggle.IsChecked = RTSS_AllCompact_Toggle.IsChecked;

                RtssSettings.RTSS_Elements[1].UseCompact = toggleButton.IsChecked == true;
                RtssSettings.RTSS_Elements[2].UseCompact = toggleButton.IsChecked == true;
                RtssSettings.RTSS_Elements[3].UseCompact = toggleButton.IsChecked == true;
                RtssSettings.RTSS_Elements[4].UseCompact = toggleButton.IsChecked == true;
                RtssSettings.RTSS_Elements[5].UseCompact = toggleButton.IsChecked == true;
                RtssSettings.RTSS_Elements[6].UseCompact = toggleButton.IsChecked == true;
                RtssSettings.RTSS_Elements[7].UseCompact = toggleButton.IsChecked == true;
                RtssSettings.RTSS_Elements[8].UseCompact = toggleButton.IsChecked == true;
                _isLoaded = true;
            }
            else
            {
                _isLoaded = false;
                RTSS_AllCompact_Toggle.IsChecked = RTSS_SakuProfile_CompactToggle.IsChecked &
                                                   RTSS_StapmFastSlow_CompactToggle.IsChecked &
                                                   RTSS_EDCThermUsage_CompactToggle.IsChecked &
                                                   RTSS_CPUClocks_CompactToggle.IsChecked &
                                                   RTSS_AVGCPUClockVolt_CompactToggle.IsChecked &
                                                   RTSS_APUClockVoltTemp_CompactToggle.IsChecked &
                                                   RTSS_FrameRate_CompactToggle.IsChecked;
                _isLoaded = true;
            }

            if (toggleButton.Name == "RTSS_MainColor_CompactToggle")
            {
                RtssSettings.RTSS_Elements[0].UseCompact = toggleButton.IsChecked == true;
            }

            if (toggleButton.Name == "RTSS_AllCompact_Toggle")
            {
                RtssSettings.RTSS_Elements[1].UseCompact = toggleButton.IsChecked == true;
            }

            if (toggleButton.Name == "RTSS_SakuProfile_CompactToggle")
            {
                RtssSettings.RTSS_Elements[2].UseCompact = toggleButton.IsChecked == true;
            }

            if (toggleButton.Name == "RTSS_StapmFastSlow_CompactToggle")
            {
                RtssSettings.RTSS_Elements[3].UseCompact = toggleButton.IsChecked == true;
            }

            if (toggleButton.Name == "RTSS_EDCThermUsage_CompactToggle")
            {
                RtssSettings.RTSS_Elements[4].UseCompact = toggleButton.IsChecked == true;
            }

            if (toggleButton.Name == "RTSS_CPUClocks_CompactToggle")
            {
                RtssSettings.RTSS_Elements[5].UseCompact = toggleButton.IsChecked == true;
            }

            if (toggleButton.Name == "RTSS_AVGCPUClockVolt_CompactToggle")
            {
                RtssSettings.RTSS_Elements[6].UseCompact = toggleButton.IsChecked == true;
            }

            if (toggleButton.Name == "RTSS_APUClockVoltTemp_CompactToggle")
            {
                RtssSettings.RTSS_Elements[7].UseCompact = toggleButton.IsChecked == true;
            }

            if (toggleButton.Name == "RTSS_FrameRate_CompactToggle")
            {
                RtssSettings.RTSS_Elements[8].UseCompact = toggleButton.IsChecked == true;
            }
        }

        if (s is CheckBox checkBox)
        {
            if (checkBox.Name == "RTSS_MainColor_Checkbox")
            {
                RtssSettings.RTSS_Elements[0].Enabled = checkBox.IsChecked == true;
            }

            if (checkBox.Name == "RTSS_SecondColor_Checkbox")
            {
                RtssSettings.RTSS_Elements[1].Enabled = checkBox.IsChecked == true;
            }

            if (checkBox.Name == "RTSS_SakuOverclockProfile_Checkbox")
            {
                RtssSettings.RTSS_Elements[2].Enabled = checkBox.IsChecked == true;
            }

            if (checkBox.Name == "RTSS_StapmFastSlow_Checkbox")
            {
                RtssSettings.RTSS_Elements[3].Enabled = checkBox.IsChecked == true;
            }

            if (checkBox.Name == "RTSS_EDCThermUsage_Checkbox")
            {
                RtssSettings.RTSS_Elements[4].Enabled = checkBox.IsChecked == true;
            }

            if (checkBox.Name == "RTSS_CPUClocks_Checkbox")
            {
                RtssSettings.RTSS_Elements[5].Enabled = checkBox.IsChecked == true;
            }

            if (checkBox.Name == "RTSS_AVGCPUClockVolt_Checkbox")
            {
                RtssSettings.RTSS_Elements[6].Enabled = checkBox.IsChecked == true;
            }

            if (checkBox.Name == "RTSS_APUClockVoltTemp_Checkbox")
            {
                RtssSettings.RTSS_Elements[7].Enabled = checkBox.IsChecked == true;
            }

            if (checkBox.Name == "RTSS_FrameRate_Checkbox")
            {
                RtssSettings.RTSS_Elements[8].Enabled = checkBox.IsChecked == true;
            }
        }

        if (s is TextBox textBox)
        {
            if (textBox.Name == "RTSS_SakuOverclockProfile_TextBox")
            {
                RtssSettings.RTSS_Elements[2].Name = textBox.Text;
            }

            if (textBox.Name == "RTSS_StapmFastSlow_TextBox")
            {
                RtssSettings.RTSS_Elements[3].Name = textBox.Text;
            }

            if (textBox.Name == "RTSS_EDCThermUsage_TextBox")
            {
                RtssSettings.RTSS_Elements[4].Name = textBox.Text;
            }

            if (textBox.Name == "RTSS_CPUClocks_TextBox")
            {
                RtssSettings.RTSS_Elements[5].Name = textBox.Text;
            }

            if (textBox.Name == "RTSS_AVGCPUClockVolt_TextBox")
            {
                RtssSettings.RTSS_Elements[6].Name = textBox.Text;
            }

            if (textBox.Name == "RTSS_APUClockVoltTemp_TextBox")
            {
                RtssSettings.RTSS_Elements[7].Name = textBox.Text;
            }

            if (textBox.Name == "RTSS_FrameRate_TextBox")
            {
                RtssSettings.RTSS_Elements[8].Name = textBox.Text;
            }
        }

        if (s is ColorPicker colorPicker)
        {
            if (colorPicker.Name == "RTSS_MainColor_ColorPicker")
            {
                RtssSettings.RTSS_Elements[0].Color =
                    $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}";
            }

            if (colorPicker.Name == "RTSS_SecondColor_ColorPicker")
            {
                RtssSettings.RTSS_Elements[1].Color =
                    $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}";
            }

            if (colorPicker.Name == "RTSS_SakuOverclockProfile_ColorPicker")
            {
                RtssSettings.RTSS_Elements[2].Color =
                    $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}";
            }

            if (colorPicker.Name == "RTSS_StapmFastSlow_ColorPicker")
            {
                RtssSettings.RTSS_Elements[3].Color =
                    $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}";
            }

            if (colorPicker.Name == "RTSS_EDCThermUsage_ColorPicker")
            {
                RtssSettings.RTSS_Elements[4].Color =
                    $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}";
            }

            if (colorPicker.Name == "RTSS_CPUClocks_ColorPicker")
            {
                RtssSettings.RTSS_Elements[5].Color =
                    $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}";
            }

            if (colorPicker.Name == "RTSS_AVGCPUClockVolt_ColorPicker")
            {
                RtssSettings.RTSS_Elements[6].Color =
                    $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}";
            }

            if (colorPicker.Name == "RTSS_APUClockVoltTemp_ColorPicker")
            {
                RtssSettings.RTSS_Elements[7].Color =
                    $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}";
            }

            if (colorPicker.Name == "RTSS_FrameRate_ColorPicker")
            {
                RtssSettings.RTSS_Elements[8].Color =
                    $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}";
            }
        }

        GenerateAdvancedCodeEditor();
        RtssSettings.SaveSettings();
    }

    private void GenerateAdvancedCodeEditor()
    {
        // Шаг 1: Создание ColorLib
        var colorLib = new List<string>
        {
            "FFFFFF" // Добавляем белый цвет по умолчанию
        };

        void AddColorIfUnique(string color)
        {
            if (!colorLib.Contains(color.Replace("#", "")))
            {
                colorLib.Add(color.Replace("#", ""));
            }
        }

        AddColorIfUnique(RtssSettings.RTSS_Elements[0].Color);
        AddColorIfUnique(RtssSettings.RTSS_Elements[1].Color);
        AddColorIfUnique(RtssSettings.RTSS_Elements[2].Color);
        AddColorIfUnique(RtssSettings.RTSS_Elements[3].Color);
        AddColorIfUnique(RtssSettings.RTSS_Elements[4].Color);
        AddColorIfUnique(RtssSettings.RTSS_Elements[5].Color);
        AddColorIfUnique(RtssSettings.RTSS_Elements[6].Color);
        AddColorIfUnique(RtssSettings.RTSS_Elements[7].Color);
        AddColorIfUnique(RtssSettings.RTSS_Elements[8].Color);

        // Шаг 2: Создание CompactLib
        var compactLib = new bool[9];
        compactLib[0] = RtssSettings.RTSS_Elements[0].UseCompact;
        compactLib[1] = RtssSettings.RTSS_Elements[1].UseCompact;
        compactLib[2] = RtssSettings.RTSS_Elements[1].UseCompact && RtssSettings.RTSS_Elements[1].Enabled
            ? RtssSettings.RTSS_Elements[1].UseCompact
            : RtssSettings.RTSS_Elements[2].UseCompact;
        compactLib[3] = RtssSettings.RTSS_Elements[1].UseCompact && RtssSettings.RTSS_Elements[1].Enabled
            ? RtssSettings.RTSS_Elements[1].UseCompact
            : RtssSettings.RTSS_Elements[3].UseCompact;
        compactLib[4] = RtssSettings.RTSS_Elements[1].UseCompact && RtssSettings.RTSS_Elements[1].Enabled
            ? RtssSettings.RTSS_Elements[1].UseCompact
            : RtssSettings.RTSS_Elements[4].UseCompact;
        compactLib[5] = RtssSettings.RTSS_Elements[1].UseCompact && RtssSettings.RTSS_Elements[1].Enabled
            ? RtssSettings.RTSS_Elements[1].UseCompact
            : RtssSettings.RTSS_Elements[5].UseCompact;
        compactLib[6] = RtssSettings.RTSS_Elements[1].UseCompact && RtssSettings.RTSS_Elements[1].Enabled
            ? RtssSettings.RTSS_Elements[1].UseCompact
            : RtssSettings.RTSS_Elements[6].UseCompact;
        compactLib[7] = RtssSettings.RTSS_Elements[1].UseCompact && RtssSettings.RTSS_Elements[1].Enabled
            ? RtssSettings.RTSS_Elements[1].UseCompact
            : RtssSettings.RTSS_Elements[7].UseCompact;
        compactLib[8] = RtssSettings.RTSS_Elements[1].UseCompact && RtssSettings.RTSS_Elements[1].Enabled
            ? RtssSettings.RTSS_Elements[1].UseCompact
            : RtssSettings.RTSS_Elements[8].UseCompact;

        // Шаг 3: Создание EnableLib
        var enableLib = new bool[9];
        enableLib[0] = RtssSettings.RTSS_Elements[0].Enabled;
        enableLib[1] = RtssSettings.RTSS_Elements[1].Enabled;
        enableLib[2] = RtssSettings.RTSS_Elements[2].Enabled;
        enableLib[3] = RtssSettings.RTSS_Elements[3].Enabled;
        enableLib[4] = RtssSettings.RTSS_Elements[4].Enabled;
        enableLib[5] = RtssSettings.RTSS_Elements[5].Enabled;
        enableLib[6] = RtssSettings.RTSS_Elements[6].Enabled;
        enableLib[7] = RtssSettings.RTSS_Elements[7].Enabled;
        enableLib[8] = RtssSettings.RTSS_Elements[8].Enabled;

        // Шаг 4: Создание TextLib
        var textLib = new string[7];
        textLib[0] = RtssSettings.RTSS_Elements[2].Name.TrimEnd(); // Saku Overclock Profile
        textLib[1] = RtssSettings.RTSS_Elements[3].Name.TrimEnd(); // STAPM Fast Slow
        textLib[2] = RtssSettings.RTSS_Elements[4].Name.TrimEnd(); // EDC Therm CPU Usage
        textLib[3] = RtssSettings.RTSS_Elements[5].Name.TrimEnd(); // CPU Clocks
        textLib[4] = RtssSettings.RTSS_Elements[6].Name.TrimEnd(); // AVG Clock Volt
        textLib[5] = RtssSettings.RTSS_Elements[7].Name.TrimEnd(); // APU Clock Volt Temp
        textLib[6] = RtssSettings.RTSS_Elements[8].Name.TrimEnd(); // Frame Rate

        // Шаг 5: Генерация строки AdvancedCodeEditor
        var advancedCodeEditor = new StringBuilder();

        /*public string AdvancedCodeEditor =
        "<C0=FFA0A0><C1=A0FFA0><C2=FC89AC><C3=fa2363><S1=70><S2=-50>\n" +
        "<C0>Saku Overclock <C1>" + ViewModels.ГлавнаяViewModel.GetVersion() + ": <S0>$SelectedProfile$\n" +
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
        // "<C0>Saku Overclock <C1>" + ViewModels.ГлавнаяViewModel.GetVersion() + ": <S0>$SelectedProfile$\n" +
        if (enableLib[2])
        {
            var colorIndexMain = RtssSettings.RTSS_Elements[0].Enabled
                ? colorLib.IndexOf(RtssSettings.RTSS_Elements[0].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(RtssSettings.RTSS_Elements[2].Color.Replace("#", "")).ToString();
            var colorIndexSecond = RtssSettings.RTSS_Elements[1].Enabled
                ? colorLib.IndexOf(RtssSettings.RTSS_Elements[1].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(RtssSettings.RTSS_Elements[2].Color.Replace("#", "")).ToString();
            var compactMain = RtssSettings.RTSS_Elements[0].Enabled
                ? (compactLib[0] ? "<S1>" : "<S0>")
                : (compactLib[2] ? "<S1>" : "<S0>");
            var compactSecond = compactLib[2] ? "<S2>" : "<S0>";
            advancedCodeEditor.Append(
                $"<C{colorIndexMain}>{compactMain}{textLib[0]} {ГлавнаяViewModel.GetVersion()}: <C{colorIndexSecond}>{compactSecond}<S0>$SelectedProfile$\n");
        }

        // 5.3 Третья строка (STAPM Fast Slow)
        // Пример третьей строки:
        // "<S1><C2>STAPM, Fast, Slow: <C3><S0>$stapm_value$<S2>W<S1>$stapm_limit$W <S0>$fast_value$<S2>W<S1>$fast_limit$W <S0>$slow_value$<S2>W<S1>$slow_limit$W\n" +
        if (enableLib[3])
        {
            var colorIndexMain = RtssSettings.RTSS_Elements[0].Enabled
                ? colorLib.IndexOf(RtssSettings.RTSS_Elements[0].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(RtssSettings.RTSS_Elements[3].Color.Replace("#", "")).ToString();
            var colorIndexSecond = RtssSettings.RTSS_Elements[1].Enabled
                ? colorLib.IndexOf(RtssSettings.RTSS_Elements[1].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(RtssSettings.RTSS_Elements[3].Color.Replace("#", "")).ToString();
            var compactMain = RtssSettings.RTSS_Elements[0].Enabled
                ? (compactLib[0] ? "<S1>" : "<S0>")
                : (compactLib[3] ? "<S1>" : "<S0>");
            var compactSecond = compactLib[3] ? "<S2>" : "<S0>";
            var compactSign = RtssSettings.RTSS_Elements[1].Enabled
                ? (compactLib[1] ? "" : "/")
                : (compactLib[3] ? "" : "/");
            advancedCodeEditor.Append(
                $"<C{colorIndexMain}>{compactMain}{textLib[1]}: <C{colorIndexSecond}><S0>$stapm_value${compactSecond}W{compactSign}{compactSecond.Replace("2", "1")}$stapm_limit$W <S0>$fast_value${compactSecond}W{compactSign}{compactSecond.Replace("2", "1")}$fast_limit$W <S0>$slow_value${compactSecond}W{compactSign}{compactSecond.Replace("2", "1")}$slow_limit$W\n");
        }

        // - Для EDC Therm CPU Usage
        // Пример четвёртой строки:
        // "<C2>EDC, Therm, CPU Usage: <C3><S0>$vrmedc_value$<S2>A<S1>$vrmedc_max$A <C3><S0>$cpu_temp_value$<S2>C<S1>$cpu_temp_max$C<C3><S0> $cpu_usage$<S2>%<S1>\n" +
        if (enableLib[4])
        {
            var colorIndexMain = RtssSettings.RTSS_Elements[0].Enabled
                ? colorLib.IndexOf(RtssSettings.RTSS_Elements[0].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(RtssSettings.RTSS_Elements[4].Color.Replace("#", "")).ToString();
            var colorIndexSecond = RtssSettings.RTSS_Elements[1].Enabled
                ? colorLib.IndexOf(RtssSettings.RTSS_Elements[1].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(RtssSettings.RTSS_Elements[4].Color.Replace("#", "")).ToString();
            var compactMain = RtssSettings.RTSS_Elements[0].Enabled
                ? (compactLib[0] ? "<S1>" : "<S0>")
                : (compactLib[4] ? "<S1>" : "<S0>");
            var compactSecond = compactLib[4] ? "<S2>" : "<S0>";
            var compactSign = RtssSettings.RTSS_Elements[1].Enabled
                ? (compactLib[1] ? "" : "/")
                : (compactLib[4] ? "" : "/");
            advancedCodeEditor.Append(
                $"<C{colorIndexMain}>{compactMain}{textLib[2]}: <C{colorIndexSecond}><S0>$vrmedc_value${compactSecond}A{compactSign}{compactSecond.Replace("2", "1")}$vrmedc_max$A <S0>$cpu_temp_value${compactSecond}C{compactSign}{compactSecond.Replace("2", "1")}$cpu_temp_max$C <S0>$cpu_usage${compactSecond}%\n");
        }

        // - Для CPU Clocks
        // Пример пятой строки:
        // "<S1><C2>Clocks: $cpu_clock_cycle$<S1><C2>$currCore$:<S0><C3> $cpu_core_clock$<S2>GHz<S1>$cpu_core_voltage$V $cpu_clock_cycle_end$\n" +
        if (enableLib[5])
        {
            var colorIndexMain = RtssSettings.RTSS_Elements[0].Enabled
                ? colorLib.IndexOf(RtssSettings.RTSS_Elements[0].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(RtssSettings.RTSS_Elements[5].Color.Replace("#", "")).ToString();
            var colorIndexSecond = RtssSettings.RTSS_Elements[1].Enabled
                ? colorLib.IndexOf(RtssSettings.RTSS_Elements[1].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(RtssSettings.RTSS_Elements[5].Color.Replace("#", "")).ToString();
            var compactMain = RtssSettings.RTSS_Elements[0].Enabled
                ? (compactLib[0] ? "<S1>" : "<S0>")
                : (compactLib[5] ? "<S1>" : "<S0>");
            var compactSecond = compactLib[5] ? "<S2>" : "<S0>";
            var compactSign = RtssSettings.RTSS_Elements[1].Enabled
                ? (compactLib[1] ? "" : "/")
                : (compactLib[5] ? "" : "/");
            advancedCodeEditor.Append(
                $"<C{colorIndexMain}>{compactMain}{textLib[3]}: $cpu_clock_cycle$<C{colorIndexMain}>$currCore$: <C{colorIndexSecond}>$cpu_core_clock${compactSecond}GHz{compactSign}{compactSecond.Replace("2", "1")}$cpu_core_voltage$V $cpu_clock_cycle_end$\n");
        }

        // - Для AVG Clock Volt
        // Пример шестой строки:
        // "<C2>AVG Clock, Volt: <C3><S0>$average_cpu_clock$<S2>GHz<S1>$average_cpu_voltage$V" +
        if (enableLib[6])
        {
            var colorIndexMain = RtssSettings.RTSS_Elements[0].Enabled
                ? colorLib.IndexOf(RtssSettings.RTSS_Elements[0].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(RtssSettings.RTSS_Elements[6].Color.Replace("#", "")).ToString();
            var colorIndexSecond = RtssSettings.RTSS_Elements[1].Enabled
                ? colorLib.IndexOf(RtssSettings.RTSS_Elements[1].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(RtssSettings.RTSS_Elements[6].Color.Replace("#", "")).ToString();
            var compactMain = RtssSettings.RTSS_Elements[0].Enabled
                ? (compactLib[0] ? "<S1>" : "<S0>")
                : (compactLib[6] ? "<S1>" : "<S0>");
            var compactSecond = compactLib[6] ? "<S2>" : "<S0>";
            var compactSign = RtssSettings.RTSS_Elements[1].Enabled
                ? (compactLib[1] ? "" : "/")
                : (compactLib[6] ? "" : "/");
            advancedCodeEditor.Append(
                $"<C{colorIndexMain}>{compactMain}{textLib[4]}: <C{colorIndexSecond}><S0>$average_cpu_clock${compactSecond}GHz{compactSign}{compactSecond.Replace("2", "1")}$average_cpu_voltage$V\n");
        }

        // - Для APU Clock Volt Temp
        // Пример седьмой строки:
        // "<C2>APU Clock, Volt, Temp: <C3><S0>$gfx_clock$<S2>MHz<S1>$gfx_volt$V <S0>$gfx_temp$<S1>C\n" +
        if (enableLib[7])
        {
            var colorIndexMain = RtssSettings.RTSS_Elements[0].Enabled
                ? colorLib.IndexOf(RtssSettings.RTSS_Elements[0].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(RtssSettings.RTSS_Elements[7].Color.Replace("#", "")).ToString();
            var colorIndexSecond = RtssSettings.RTSS_Elements[1].Enabled
                ? colorLib.IndexOf(RtssSettings.RTSS_Elements[1].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(RtssSettings.RTSS_Elements[7].Color.Replace("#", "")).ToString();
            var compactMain = RtssSettings.RTSS_Elements[0].Enabled
                ? (compactLib[0] ? "<S1>" : "<S0>")
                : (compactLib[7] ? "<S1>" : "<S0>");
            var compactSecond = compactLib[7] ? "<S2>" : "<S0>";
            var compactSign = RtssSettings.RTSS_Elements[1].Enabled
                ? (compactLib[1] ? "" : "/")
                : (compactLib[7] ? "" : "/");
            advancedCodeEditor.Append(
                $"<C{colorIndexMain}>{compactMain}{textLib[5]}: <C{colorIndexSecond}><S0>$gfx_clock${compactSecond}MHz{compactSign}{compactSecond.Replace("2", "1")}$gfx_volt$V <S0>$gfx_temp${compactSecond}C\n");
        }

        // - Для Frame Rate
        // Пример восьмой строки:
        // "<C2>Framerate <C3><S0>%FRAMERATE% %FRAMETIME%";*/
        if (enableLib[8])
        {
            var colorIndexMain = RtssSettings.RTSS_Elements[0].Enabled
                ? colorLib.IndexOf(RtssSettings.RTSS_Elements[0].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(RtssSettings.RTSS_Elements[8].Color.Replace("#", "")).ToString();
            var colorIndexSecond = RtssSettings.RTSS_Elements[1].Enabled
                ? colorLib.IndexOf(RtssSettings.RTSS_Elements[1].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(RtssSettings.RTSS_Elements[8].Color.Replace("#", "")).ToString();
            var compactMain = compactLib[8] ? "<S1>" : "<S0>";
            advancedCodeEditor.Append(
                $"<C{colorIndexMain}>{compactMain}{textLib[6]}: <C{colorIndexSecond}><S0>%FRAMERATE% %FRAMETIME%");
        }

        // Финальная строка присваивается в AdvancedCodeEditor
        RtssSettings.AdvancedCodeEditor = advancedCodeEditor.ToString();
        LoadAndFormatAdvancedCodeEditor(RtssSettings.AdvancedCodeEditor);
        RtssHandler.ChangeOsdText(RtssSettings.AdvancedCodeEditor);
        RtssSettings.SaveSettings();
    }


    private void Settings_RTSS_Enable_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        AppSettings.RtssMetricsEnabled = Settings_RTSS_Enable.IsOn;
        AppSettings.SaveSettings();
        Settings_RTSS_Enable_Name.Visibility = Settings_RTSS_Enable.IsOn ? Visibility.Visible : Visibility.Collapsed;
        RTTS_GridView.Visibility = Settings_RTSS_Enable.IsOn ? Visibility.Visible : Visibility.Collapsed;
        RTSS_AdvancedCodeEditor_ToggleSwitch.Visibility =
            Settings_RTSS_Enable.IsOn ? Visibility.Visible : Visibility.Collapsed;
        RTSS_AdvancedCodeEditor_EditBox_Scroll.Visibility =
            Settings_RTSS_Enable.IsOn ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RTSS_AdvancedCodeEditor_ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        RTSS_AdvancedCodeEditor_EditBox.Visibility =
            RTSS_AdvancedCodeEditor_ToggleSwitch.IsOn ? Visibility.Visible : Visibility.Collapsed;
        if (!_isLoaded)
        {
            return;
        }

        RtssSettings.IsAdvancedCodeEditorEnabled = RTSS_AdvancedCodeEditor_ToggleSwitch.IsOn;
        RtssSettings.SaveSettings();
    }

    private void RTSS_AdvancedCodeEditor_EditBox_TextChanged(object sender, RoutedEventArgs e)
    {
        RTSS_AdvancedCodeEditor_EditBox.Document.GetText(TextGetOptions.None, out var newString);
        RtssSettings.AdvancedCodeEditor = newString.Replace("\r", "\n").TrimEnd();
        RtssSettings.SaveSettings();
    }

    #endregion 
}