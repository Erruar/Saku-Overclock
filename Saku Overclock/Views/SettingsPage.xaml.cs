using System.Diagnostics;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.ViewModels;
using Windows.Foundation.Metadata;
using Task = System.Threading.Tasks.Task;
namespace Saku_Overclock.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel
    {
        get;
    }
    private Config config = new();
    private Themer themer = new();
    private JsonContainers.RTSSsettings rtssset = new();
    private bool isLoaded = false;
    private JsonContainers.Notifications notify = new();

    public SettingsPage()
    {
        ViewModel = App.GetService<SettingsViewModel>();
        InitializeComponent();
        ConfigLoad(); 
        InitVal();
        config.NBFCFlagConsoleCheckSpeedRunning = false; //Автообновление информации выключено не зависимо от активированной страницы
        config.FlagRyzenADJConsoleTemperatureCheckRunning = false;
        ConfigSave();
        Loaded += LoadedApp; //Приложение загружено - разрешить 
    }

    #region JSON and Initialization
    private async void InitVal()
    {
        ConfigLoad();
        try { AutostartCom.SelectedIndex = config.AutostartType; } catch { AutostartCom.SelectedIndex = 0; }
        CbApplyStart.IsOn = config.ReapplyLatestSettingsOnAppLaunch;
        CbAutoReapply.IsOn = config.ReapplyOverclock;  nudAutoReapply.Value = config.ReapplyOverclockTimer;
        CbAutoCheck.IsOn = config.CheckForUpdates;
        ReapplySafe.IsOn = config.ReapplySafeOverclock;
        ThemeLight.Visibility = config.ThemeType > 7 ? Visibility.Visible : Visibility.Collapsed;
        ThemeCustomBg.IsEnabled = config.ThemeType > 7; 
        RTSS_LoadAndApply();
        UpdateTheme_ComboBox();
        await Task.Delay(390);
    }
    private void UpdateTheme_ComboBox()
    {
        ThemeLoad();
        ThemeCombobox.Items.Clear();
        try
        {
            if (themer.Themes != null)
            {
                for (var element = 0; element < themer.Themes.Count; element++)
                {
                    try
                    {
                        ThemeCombobox.Items.Add(themer.Themes[element].ThemeName.GetLocalized());
                    }
                    catch
                    {
                        ThemeCombobox.Items.Add(themer.Themes[element].ThemeName);
                    }
                }
                ThemeOpacity.Value = themer.Themes[config.ThemeType].ThemeOpacity;
                ThemeMaskOpacity.Value = themer.Themes[config.ThemeType].ThemeMaskOpacity;
                ThemeCustom.IsOn = themer.Themes[config.ThemeType].ThemeCustom;
                ThemeCustomBg.IsOn = themer.Themes[config.ThemeType].ThemeCustomBg;
                Theme_Custom();
            }
            ThemeCombobox.SelectedIndex = config.ThemeType;
            ThemeLight.Visibility = config.ThemeType > 7 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch
        {
            try { config.ThemeType /= 2; } catch { config.ThemeType = 0; } //Нельзя делить на ноль
            ConfigSave();
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
            ThemeCustomBg.IsEnabled = config.ThemeType > 7;
            ThemeCustomBg.Visibility = Visibility.Visible;
            ThemeLight.Visibility = Visibility.Visible;
            ThemeBgButton.Visibility = Visibility.Visible;
            ThemeBgButton.Visibility = ThemeCustomBg.IsOn ? Visibility.Visible : Visibility.Collapsed;
        }
    }
    private void LoadedApp(object sender, RoutedEventArgs e)
    {
        isLoaded = true;
    }
    private void RTSS_LoadAndApply()
    {
        // Загрузка данных из JSON файла
        RtssLoad();

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
                    toggleButton.IsChecked = rtssset.RTSS_Elements[i].UseCompact;
                }
            }

            // Применение значения CheckBox
            if (!string.IsNullOrEmpty(checkBoxName))
            {
                var checkBox = (CheckBox)FindName(checkBoxName);
                if (checkBox != null)
                {
                    checkBox.IsChecked = rtssset.RTSS_Elements[i].Enabled;
                }
            }

            // Применение значения TextBox
            if (!string.IsNullOrEmpty(textBoxName))
            {
                var textBox = (TextBox)FindName(textBoxName);
                if (textBox != null)
                {
                    textBox.Text = rtssset.RTSS_Elements[i].Name;
                }
            }

            // Применение значения ColorPicker
            if (!string.IsNullOrEmpty(colorPickerName))
            {
                var colorPicker = (ColorPicker)FindName(colorPickerName);
                if (colorPicker != null)
                {
                    var color = rtssset.RTSS_Elements[i].Color;
                    var r = Convert.ToByte(color.Substring(1, 2), 16);
                    var g = Convert.ToByte(color.Substring(3, 2), 16);
                    var b = Convert.ToByte(color.Substring(5, 2), 16);
                    colorPicker.Color = Windows.UI.Color.FromArgb(255, r, g, b);
                }
            }
        } 
    }

    public void ConfigSave()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json", JsonConvert.SerializeObject(config, Formatting.Indented));
        }
        catch { }
    }
    public void ConfigLoad()
    {
        try
        {
            config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json"))!;
            if (config == null) { config = new Config(); ConfigSave(); }
        }
        catch { }
    }
    public void RtssSave()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\rtssparam.json", JsonConvert.SerializeObject(rtssset, Formatting.Indented));
        }
        catch { }
    }
    public void RtssLoad()
    {
        try
        {
            rtssset = JsonConvert.DeserializeObject<JsonContainers.RTSSsettings>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\rtssparam.json"))!;
            rtssset.RTSS_Elements.RemoveRange(0, 9);
            //if (rtssset == null) { rtssset = new JsonContainers.RTSSsettings(); RtssSave(); }
        }
        catch { rtssset = new JsonContainers.RTSSsettings(); RtssSave(); }
    }
    public void ThemeSave()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\theme.json", "");
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\theme.json", JsonConvert.SerializeObject(themer, Formatting.Indented));
        }
        catch { }
    }
    public void ThemeLoad()
    {
        try
        {
            themer = JsonConvert.DeserializeObject<Themer>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\theme.json"))!;
            if (themer.Themes.Count > 8)
            {
                themer.Themes.RemoveRange(0, 8);
            }
            if (themer == null) { Fix_Themer(); }
        }
        catch
        {
            Fix_Themer();
        }
    }
    private void Fix_Themer()
    {
        try
        {
            themer = new Themer();
        }
        catch
        {
            App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
        }
        if (themer != null)
        {
            try
            {
                Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\theme.json", JsonConvert.SerializeObject(themer));
            }
            catch
            {
                File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\theme.json");
                Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\theme.json", JsonConvert.SerializeObject(themer));
                App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
            }
        }
        else
        {
            try
            {

                File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\theme.json");
                Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\theme.json", JsonConvert.SerializeObject(themer));
            }
            catch
            {
            }
        }
    }
    public void NotifySave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json", JsonConvert.SerializeObject(notify, Formatting.Indented));
        }
        catch
        {
            // ignored
        }
    }
    public async void NotifyLoad()
    {
        var success = false;
        var retryCount = 1;
        while (!success && retryCount < 3)
        {
            if (File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json"))
            {
                try
                {
                    notify = JsonConvert.DeserializeObject<JsonContainers.Notifications>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json"))!;
                    if (notify != null) { success = true; } else { notify = new JsonContainers.Notifications(); NotifySave(); }
                }
                catch { notify = new JsonContainers.Notifications(); NotifySave(); }
            }
            else { notify = new JsonContainers.Notifications(); NotifySave(); }
            if (!success)
            {
                // Сделайте задержку перед следующей попыткой
                await Task.Delay(30);
                retryCount++;
            }
        }
    }
    #endregion
    #region Event Handlers
    private void Discord_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://discord.com/invite/yVsKxqAaa7") { UseShellExecute = true });
    }
    private void AutostartCom_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!isLoaded) { return; }
        ConfigLoad();
        config.AutostartType = AutostartCom.SelectedIndex;
        var autoruns = new TaskService();
        if (AutostartCom.SelectedIndex == 2 || AutostartCom.SelectedIndex == 3)
        {
            var pathToExecutableFile = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var pathToProgramDirectory = Path.GetDirectoryName(pathToExecutableFile);
            var pathToStartupLnk = Path.Combine(pathToProgramDirectory!, "Saku Overclock.exe");
            // Добавить программу в автозагрузку
            var SakuTask = autoruns.NewTask();
            SakuTask.RegistrationInfo.Description = "An awesome ryzen laptop overclock utility for those who want real performance! Autostart Saku Overclock application task";
            SakuTask.RegistrationInfo.Author = "Sakura Serzhik";
            SakuTask.RegistrationInfo.Version = new Version("1.0.0");
            SakuTask.Principal.RunLevel = TaskRunLevel.Highest;
            SakuTask.Triggers.Add(new LogonTrigger { Enabled = true });
            SakuTask.Actions.Add(new ExecAction(pathToStartupLnk));
            autoruns.RootFolder.RegisterTaskDefinition(@"Saku Overclock", SakuTask);
        }
        else
        {
            try { autoruns.RootFolder.DeleteTask("Saku Overclock"); } catch { }
        }
        ConfigSave();
    }
    private void CbApplyStart_Click(object sender, RoutedEventArgs e)
    {
        if (!isLoaded) { return; }
        ConfigLoad();
        if (CbApplyStart.IsOn == true) { config.ReapplyLatestSettingsOnAppLaunch = true; ConfigSave(); }
        else { config.ReapplyLatestSettingsOnAppLaunch = false; ConfigSave(); };
    }
    private void CbAutoReapply_Click(object sender, RoutedEventArgs e)
    {
        if (!isLoaded) { return; }
        ConfigLoad();
        if (CbAutoReapply.IsOn == true) { AutoReapplyNumberboxPanel.Visibility = Visibility.Visible; config.ReapplyOverclock = true; config.ReapplyOverclockTimer = nudAutoReapply.Value; ConfigSave(); }
        else { AutoReapplyNumberboxPanel.Visibility = Visibility.Collapsed; config.ReapplyOverclock = false; config.ReapplyOverclockTimer = 3; ConfigSave(); };
    }
    private void CbAutoCheck_Click(object sender, RoutedEventArgs e)
    {
        if (!isLoaded) { return; }
        ConfigLoad();
        if (CbAutoCheck.IsOn == true) { config.CheckForUpdates = true; ConfigSave(); }
        else { config.CheckForUpdates = false; ConfigSave(); };
    }
    private async void NudAutoReapply_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!isLoaded) { return; }
        ConfigLoad();
        await Task.Delay(20);
        config.ReapplyOverclock = true; config.ReapplyOverclockTimer = nudAutoReapply.Value; ConfigSave();
    }
    private async void ReapplySafe_Toggled(object sender, RoutedEventArgs e)
    {
        if (!isLoaded) { return; }
        ConfigLoad();
        await Task.Delay(20);
        config.ReapplySafeOverclock = ReapplySafe.IsOn; SendSMUCommand.SafeReapply = ReapplySafe.IsOn; ConfigSave();
    }
    private void ThemeCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!isLoaded) { return; }
        ThemeLoad(); //Если нет конфига с темами - создать!
        ConfigLoad(); config.ThemeType = ThemeCombobox.SelectedIndex; ConfigSave();
        if (themer != null && themer.Themes != null)
        {
            try
            {
                if (themer.Themes[config.ThemeType].ThemeLight)
                {
                    ViewModel.SwitchThemeCommand.Execute(ElementTheme.Light);
                }
                else
                {
                    ViewModel.SwitchThemeCommand.Execute(ElementTheme.Dark);
                }
            }
            catch
            {
                ConfigLoad(); config.ThemeType = 0; ConfigSave();
            } 
            if (config.ThemeType == 0)
            {
                ViewModel.SwitchThemeCommand.Execute(ElementTheme.Default);
            }
            ThemeCustom.IsOn = themer.Themes[config.ThemeType].ThemeCustom;
            ThemeOpacity.Value = themer.Themes[config.ThemeType].ThemeOpacity;
            ThemeMaskOpacity.Value = themer.Themes[config.ThemeType].ThemeMaskOpacity;
            ThemeCustomBg.IsOn = themer.Themes[config.ThemeType].ThemeCustomBg;
            ThemeCustomBg.IsEnabled = ThemeCombobox.SelectedIndex > 7;
            ThemeLight.IsOn = themer.Themes[config.ThemeType].ThemeLight;
            ThemeLight.Visibility = ThemeCombobox.SelectedIndex > 7 ? Visibility.Visible : Visibility.Collapsed;
            ThemeBgButton.Visibility = ThemeCustomBg.IsOn ? Visibility.Visible : Visibility.Collapsed;
            Theme_Custom();
            NotifyLoad(); notify.Notifies ??= [];
            notify.Notifies.Add(new Notify { Title = "Theme applied!", Msg = "DEBUG MESSAGE. YOU SHOULDN'T SEE THIS", Type = InfoBarSeverity.Success });
            NotifySave();
        }
    }
    private void ThemeCustom_Toggled(object sender, RoutedEventArgs e)
    {
        if (!isLoaded) { return; }
        Theme_Custom();
        ThemeLoad();
        themer.Themes[config.ThemeType].ThemeCustom = ThemeCustom.IsOn;
        ThemeSave();
    }
    private void ThemeOpacity_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!isLoaded) { return; }
        ThemeLoad();
        themer.Themes[config.ThemeType].ThemeOpacity = ThemeOpacity.Value;
        ThemeSave();
        NotifyLoad(); notify.Notifies ??= [];
        notify.Notifies.Add(new Notify { Title = "Theme applied!", Msg = "DEBUG MESSAGE. YOU SHOULDN'T SEE THIS", Type = InfoBarSeverity.Success });
        NotifySave();
    }
    private void ThemeMaskOpacity_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!isLoaded) { return; }
        ThemeLoad();
        themer.Themes[config.ThemeType].ThemeMaskOpacity = ThemeMaskOpacity.Value;
        ThemeSave();
        NotifyLoad(); notify.Notifies ??= [];
        notify.Notifies.Add(new Notify { Title = "Theme applied!", Msg = "DEBUG MESSAGE. YOU SHOULDN'T SEE THIS", Type = InfoBarSeverity.Success });
        NotifySave();
    }
    private void ThemeCustomBg_Toggled(object sender, RoutedEventArgs e)
    {
        if (!isLoaded) { return; }
        ThemeLoad();
        themer.Themes[config.ThemeType].ThemeCustomBg = ThemeCustomBg.IsOn;
        ThemeSave();
        ThemeBgButton.Visibility = ThemeCustomBg.IsOn ? Visibility.Visible : Visibility.Collapsed;
    }
    private async void ThemeBgButton_Click(object sender, RoutedEventArgs e)
    {
        var endStringPath = "";
        var fromFileWhy = new TextBlock
        {
            MaxWidth = 300,
            Text = "ThemeBgFromFileWhy".GetLocalized(),
            TextWrapping = TextWrapping.WrapWholeWords,
            FontWeight = new Windows.UI.Text.FontWeight(300)
        };
        var fromFilePickedFile = new TextBlock
        {
            MaxWidth = 300,
            Visibility = Visibility.Collapsed,
            Text = "ThemeUnknownNewFile".GetLocalized(),
            TextWrapping = TextWrapping.WrapWholeWords,
            FontWeight = new Windows.UI.Text.FontWeight(300)
        };
        var fromFile = new Button
        {
            Height = 90,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Children =
                {
                    new Image
                    {
                        Margin = new Thickness(0,0,0,0),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Center,
                        Source = new BitmapImage(new Uri( "ms-appx:///Assets/ThemeBg/folder.png"))
                    },
                    new StackPanel
                    {
                        MinWidth = 300,
                        Orientation = Orientation.Vertical,
                        Margin = new Thickness(108,0,0,0),
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Top,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "ThemeBgFromFile".GetLocalized(),
                                FontWeight = new Windows.UI.Text.FontWeight(600)
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
        var fromLinkWhy = new TextBlock
        {
            MaxWidth = 300,
            HorizontalAlignment = HorizontalAlignment.Left,
            Text = "ThemeBgFromURLWhy".GetLocalized(),
            TextWrapping = TextWrapping.WrapWholeWords,
            FontWeight = new Windows.UI.Text.FontWeight(300)
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
            Content = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Children =
                {
                    new Image
                    {
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Center,
                        Source = new BitmapImage(new Uri( "ms-appx:///Assets/ThemeBg/link.png"))
                    },
                    new StackPanel
                    {
                        MinWidth = 300,
                        Orientation = Orientation.Vertical,
                        Margin = new Thickness(108,0,0,0),
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Top,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "ThemeBgFromURL".GetLocalized(),
                                FontWeight = new Windows.UI.Text.FontWeight(600)
                            },
                            fromLinkWhy,
                            fromLinkTextBox
                        }
                    },

                }
            }
        };
        //Открыть диалог с изменением 
        var BgDialog = new ContentDialog
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
                        fromLink
                    }
            },
            CloseButtonText = "Cancel".GetLocalized(),
            PrimaryButtonText = "ThemeSelect".GetLocalized(),
            DefaultButton = ContentDialogButton.Close
        };
        fromFile.Click += (s, a) =>
        {
            fromFilePickedFile.Text = "";
            OpenFileDialog fileDialog = new();
            var result = fileDialog.ShowDialog();
            if (result == true)
            {
                if (fileDialog.FileName.Contains(".gif") || fileDialog.FileName.Contains(".GIF") || fileDialog.FileName.Contains(".png") || fileDialog.FileName.Contains(".PNG")
                || fileDialog.FileName.Contains(".jpg") || fileDialog.FileName.Contains(".JPG") || fileDialog.FileName.Contains(".JPEG") || fileDialog.FileName.Contains(".JPEG")
                || fileDialog.FileName.Contains(".bmp") || fileDialog.FileName.Contains(".BMP")
                )
                {
                    fromFilePickedFile.Text = "ThemePickedFile".GetLocalized() + fileDialog.FileName;
                    endStringPath = fileDialog.FileName;
                }
                else
                {
                    fromFilePickedFile.Text = "ThemeTypeFile".GetLocalized();
                }
            }
            else
            {
                fromFilePickedFile.Text = "ThemeOpCancel".GetLocalized();
            }
            if (fromFilePickedFile.Visibility == Visibility.Collapsed)
            {
                fromFileWhy.Visibility = Visibility.Collapsed;
                fromFilePickedFile.Visibility = Visibility.Visible;
            }
            else
            {
                fromFileWhy.Visibility = Visibility.Visible;
                fromFilePickedFile.Visibility = Visibility.Collapsed;
            }
        };
        fromLink.Click += (s, a) =>
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
        fromLinkTextBox.TextChanged += (s, a) =>
        {
            endStringPath = fromLinkTextBox.Text;
        };
        // Use this code to associate the dialog to the appropriate AppWindow by setting
        // the dialog's XamlRoot to the same XamlRoot as an element that is already present in the AppWindow.
        if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
        {
            BgDialog.XamlRoot = XamlRoot;
        }
        var result = await BgDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            if (endStringPath != "")
            {
                var backupIndex = ThemeCombobox.SelectedIndex;
                ThemeLoad(); 
                themer.Themes[backupIndex].ThemeBackground = endStringPath;
                ThemeSave();
                NotifyLoad(); notify.Notifies ??= [];
                notify.Notifies.Add(new Notify { Title = "Theme applied!", Msg = "DEBUG MESSAGE. YOU SHOULDN'T SEE THIS", Type = InfoBarSeverity.Success });
                NotifySave();
                ThemeCombobox.SelectedIndex = 0;
                ThemeCombobox.SelectedIndex = backupIndex;
            }
        }
    }
    private async void CustomTheme_Click(object sender, RoutedEventArgs e)
    {
        //Отрыть редактор тем  
        var themeLoaderPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var ThemerDialog = new ContentDialog
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
        var baseThemeUri = new Uri("ms-appx:///Assets/Themes/ZqjqlOs.png");
        var baseThemeName = "Theme:Default"; 
        ThemeLoad();
        try
        {
            if (themer.Themes != null)
            {
                for (var element = 8; element < themer.Themes.Count; element++)
                {
                    baseThemeName = themer.Themes[element].ThemeName;
                    if (themer.Themes[element].ThemeBackground != "")
                    {
                        try
                        {
                            baseThemeUri = new Uri(themer.Themes[element].ThemeBackground);
                        }
                        catch
                        {
                            baseThemeUri = null;
                        }
                    }
                    else { baseThemeUri = null; }
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
                        CornerRadius = new CornerRadius(15,0,0,15),
                        Width = 300,
                        PlaceholderText = "ThemeNewName".GetLocalized(),
                        Text = baseThemeName
                    };
                    var newNameThemeSetButton = new Button
                    {
                        CornerRadius = new CornerRadius(0,15,15,0),
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
                        FontWeight = new Windows.UI.Text.FontWeight(800)
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
                                    Opacity = themer.Themes[element].ThemeOpacity,
                                    VerticalAlignment = VerticalAlignment.Stretch,
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    Background =  new ImageBrush
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
                                    Background = (Brush)Application.Current.Resources["BackgroundImageMaskAcrylicBrush"],
                                    Opacity = themer.Themes[element].ThemeMaskOpacity
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
                    newNameThemeSetButton.Click += (s, a) =>
                    {
                        if (textBoxThemeName.Text != "" || textBoxThemeName.Text != baseThemeName) 
                        {
                            themer.Themes[int.Parse(sureDelete.Name)].ThemeName = textBoxThemeName.Text;
                            themeNameText.Text = textBoxThemeName.Text;
                            editFlyout.Hide();
                            ThemeSave();
                            InitVal();
                        }
                    };
                    eachButton.PointerEntered += (s, a) =>
                    {
                        themeNameText.Margin = new Thickness(-90, 0, 0, 0);
                        buttonsPanel.Visibility = Visibility.Visible;
                    };
                    eachButton.PointerExited += (s, a) =>
                    {
                        themeNameText.Margin = new Thickness(0);
                        buttonsPanel.Visibility = Visibility.Collapsed;
                    };
                    sureDelete.Click += (s, a) =>
                    {
                        try
                        {
                            themer.Themes.RemoveAt(int.Parse(sureDelete.Name));
                            ThemeSave(); ConfigLoad(); config.ThemeType = 0; ConfigSave();
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
                    FontWeight = new Windows.UI.Text.FontWeight(700),
                    Text = "ThemeNewName".GetLocalized()
                }
            };
            if (themeLoaderPanel.Children.Count > 0)
            {
                newTheme.Margin = new Thickness(0, 10, 0, 0);
            }
            themeLoaderPanel.Children.Add(newTheme);
            //Добавить новую тему
            newTheme.Click += (s, a) =>
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
                newNameThemeSetButton.Click += (s, a) =>
                {
                    if (textBoxThemeName.Text != "")
                    {
                        try 
                        {
                            themer!.Themes!.Add(new Styles.ThemeClass { ThemeName = textBoxThemeName.Text });
                            newTheme.Flyout.Hide();
                            ThemerDialog.Hide(); 
                            ThemeSave();
                            InitVal();
                        } 
                        catch
                        {

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
            ThemerDialog.XamlRoot = XamlRoot;
        }
        _ = await ThemerDialog.ShowAsync(); 
    }


    private void ThemeLight_Toggled(object sender, RoutedEventArgs e)
    {
        if (!isLoaded) { return; }
        ThemeLoad();
        themer.Themes[config.ThemeType].ThemeLight = ThemeLight.IsOn;
        ThemeSave();
        //if (ThemeLight.IsOn) { ViewModel.SwitchThemeCommand.Execute(ElementTheme.Light); } else { ViewModel.SwitchThemeCommand.Execute(ElementTheme.Dark); }
        NotifyLoad(); notify.Notifies ??= [];
        notify.Notifies.Add(new Notify { Title = "Theme applied!", Msg = "DEBUG MESSAGE. YOU SHOULDN'T SEE THIS", Type = InfoBarSeverity.Success });
        NotifySave();
    }
    #endregion

    #region Ni Icons (tray icons) Related Section

    private void NiIconCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {

    }

    private void Settings_ni_Icons_Toggled(object sender, RoutedEventArgs e)
    {

    } 

    private void Settings_ni_Fontsize_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {

    }

    private void Settings_ni_Opacity_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {

    }

    private void NiIconComboboxElements_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {

    }

    private void Settings_ni_Add_Element_Click(object sender, RoutedEventArgs e)
    {

    }

    private void Settings_ni_Color_Picker_Click(object sender, RoutedEventArgs e)
    {

    }

    private void Settings_ni_Delete_Click(object sender, RoutedEventArgs e)
    {

    }

    private void Settings_ni_ResetDef_Click(object sender, RoutedEventArgs e)
    {

    }

    private void NiIconShapeCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {

    }
    #endregion
    #region RTSS Related Section

    private void RTSSChanged_Checked(object s, object e)
    {
        if (!isLoaded) { return; }
        if (s is ToggleButton toggleButton)
        { 
            if (toggleButton.Name == "RTSS_AllCompact_Toggle")
            {
                isLoaded = false;
                RTSS_SakuProfile_CompactToggle.IsChecked = RTSS_AllCompact_Toggle.IsChecked;
                RTSS_StapmFastSlow_CompactToggle.IsChecked = RTSS_AllCompact_Toggle.IsChecked;
                RTSS_EDCThermUsage_CompactToggle.IsChecked = RTSS_AllCompact_Toggle.IsChecked;
                RTSS_CPUClocks_CompactToggle.IsChecked = RTSS_AllCompact_Toggle.IsChecked;
                RTSS_AVGCPUClockVolt_CompactToggle.IsChecked = RTSS_AllCompact_Toggle.IsChecked;
                RTSS_APUClockVoltTemp_CompactToggle.IsChecked = RTSS_AllCompact_Toggle.IsChecked;
                RTSS_FrameRate_CompactToggle.IsChecked = RTSS_AllCompact_Toggle.IsChecked;
                isLoaded = true;
            }
            else
            {
                isLoaded = false;
                RTSS_AllCompact_Toggle.IsChecked = RTSS_SakuProfile_CompactToggle.IsChecked &
                    RTSS_StapmFastSlow_CompactToggle.IsChecked &
                    RTSS_EDCThermUsage_CompactToggle.IsChecked &
                    RTSS_CPUClocks_CompactToggle.IsChecked &
                    RTSS_AVGCPUClockVolt_CompactToggle.IsChecked &
                    RTSS_APUClockVoltTemp_CompactToggle.IsChecked &
                    RTSS_FrameRate_CompactToggle.IsChecked;
                isLoaded = true;
            }
            if (toggleButton.Name == "RTSS_MainColor_CompactToggle") { rtssset.RTSS_Elements[0].UseCompact = toggleButton.IsChecked == true; }
            if (toggleButton.Name == "RTSS_AllCompact_Toggle") { rtssset.RTSS_Elements[1].UseCompact = toggleButton.IsChecked == true; }
            if (toggleButton.Name == "RTSS_SakuProfile_CompactToggle") { rtssset.RTSS_Elements[2].UseCompact = toggleButton.IsChecked == true; }
            if (toggleButton.Name == "RTSS_StapmFastSlow_CompactToggle") { rtssset.RTSS_Elements[3].UseCompact = toggleButton.IsChecked == true; }
            if (toggleButton.Name == "RTSS_EDCThermUsage_CompactToggle") { rtssset.RTSS_Elements[4].UseCompact = toggleButton.IsChecked == true; }
            if (toggleButton.Name == "RTSS_CPUClocks_CompactToggle") { rtssset.RTSS_Elements[5].UseCompact = toggleButton.IsChecked == true; }
            if (toggleButton.Name == "RTSS_AVGCPUClockVolt_CompactToggle") { rtssset.RTSS_Elements[6].UseCompact = toggleButton.IsChecked == true; }
            if (toggleButton.Name == "RTSS_APUClockVoltTemp_CompactToggle") { rtssset.RTSS_Elements[7].UseCompact = toggleButton.IsChecked == true; }
            if (toggleButton.Name == "RTSS_FrameRate_CompactToggle") { rtssset.RTSS_Elements[8].UseCompact = toggleButton.IsChecked == true; }
        } 
        if (s is CheckBox checkBox)
        {
            if (checkBox.Name == "RTSS_MainColor_Checkbox") { rtssset.RTSS_Elements[0].Enabled = checkBox.IsChecked == true; }
            if (checkBox.Name == "RTSS_SecondColor_Checkbox") { rtssset.RTSS_Elements[1].Enabled = checkBox.IsChecked == true; }
            if (checkBox.Name == "RTSS_SakuOverclockProfile_Checkbox") { rtssset.RTSS_Elements[2].Enabled = checkBox.IsChecked == true; }
            if (checkBox.Name == "RTSS_StapmFastSlow_Checkbox") { rtssset.RTSS_Elements[3].Enabled = checkBox.IsChecked == true; }
            if (checkBox.Name == "RTSS_EDCThermUsage_Checkbox") { rtssset.RTSS_Elements[4].Enabled = checkBox.IsChecked == true; }
            if (checkBox.Name == "RTSS_CPUClocks_Checkbox") { rtssset.RTSS_Elements[5].Enabled = checkBox.IsChecked == true; }
            if (checkBox.Name == "RTSS_AVGCPUClockVolt_Checkbox") { rtssset.RTSS_Elements[6].Enabled = checkBox.IsChecked == true; }
            if (checkBox.Name == "RTSS_APUClockVoltTemp_Checkbox") { rtssset.RTSS_Elements[7].Enabled = checkBox.IsChecked == true; }
            if (checkBox.Name == "RTSS_FrameRate_Checkbox") { rtssset.RTSS_Elements[8].Enabled = checkBox.IsChecked == true; }
        }
        if (s is TextBox textBox)
        {
            if (textBox.Name == "RTSS_SakuOverclockProfile_TextBox") { rtssset.RTSS_Elements[2].Name = textBox.Text; }
            if (textBox.Name == "RTSS_StapmFastSlow_TextBox") { rtssset.RTSS_Elements[3].Name = textBox.Text; }
            if (textBox.Name == "RTSS_EDCThermUsage_TextBox") { rtssset.RTSS_Elements[4].Name = textBox.Text; }
            if (textBox.Name == "RTSS_CPUClocks_TextBox") { rtssset.RTSS_Elements[5].Name = textBox.Text; }
            if (textBox.Name == "RTSS_AVGCPUClockVolt_TextBox") { rtssset.RTSS_Elements[6].Name = textBox.Text; }
            if (textBox.Name == "RTSS_APUClockVoltTemp_TextBox") { rtssset.RTSS_Elements[7].Name = textBox.Text; }
            if (textBox.Name == "RTSS_FrameRate_TextBox") { rtssset.RTSS_Elements[8].Name = textBox.Text; }
        }
        if (s is ColorPicker colorPicker)
        {
            if (colorPicker.Name == "RTSS_MainColor_ColorPicker") { rtssset.RTSS_Elements[0].Color = $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}"; }
            if (colorPicker.Name == "RTSS_SecondColor_ColorPicker") { rtssset.RTSS_Elements[1].Color = $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}"; }
            if (colorPicker.Name == "RTSS_SakuOverclockProfile_ColorPicker") { rtssset.RTSS_Elements[2].Color = $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}"; }
            if (colorPicker.Name == "RTSS_StapmFastSlow_ColorPicker") { rtssset.RTSS_Elements[3].Color = $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}"; }
            if (colorPicker.Name == "RTSS_EDCThermUsage_ColorPicker") { rtssset.RTSS_Elements[4].Color = $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}"; }
            if (colorPicker.Name == "RTSS_CPUClocks_ColorPicker") { rtssset.RTSS_Elements[5].Color = $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}"; }
            if (colorPicker.Name == "RTSS_AVGCPUClockVolt_ColorPicker") { rtssset.RTSS_Elements[6].Color = $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}"; }
            if (colorPicker.Name == "RTSS_APUClockVoltTemp_ColorPicker") { rtssset.RTSS_Elements[7].Color = $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}"; }
            if (colorPicker.Name == "RTSS_FrameRate_ColorPicker") { rtssset.RTSS_Elements[8].Color = $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}"; }
        }
        RtssSave();
    }

    private void Settings_RTSS_Enable_Toggled(object sender, RoutedEventArgs e)
    {

    }
    private void RTSS_AdvancedCodeEditor_ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {

    }

    private void RTSS_AdvancedCodeEditor_EditBox_TextChanged(object sender, RoutedEventArgs e)
    {

    }
    #endregion
}