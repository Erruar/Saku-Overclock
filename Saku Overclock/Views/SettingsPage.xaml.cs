using System.Diagnostics;
using System.Globalization;
using System.Reflection;
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
using Windows.Foundation.Metadata;
using Windows.UI;
using Windows.UI.Text;
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
    private RTSSsettings _rtssset = new();
    private NiIconsSettings _niicons = new();
    private bool _isLoaded;
    private static readonly IAppNotificationService NotificationsService = App.GetService<IAppNotificationService>();
    private static readonly IAppSettingsService SettingsService = App.GetService<IAppSettingsService>();

    public SettingsPage()
    {
        ViewModel = App.GetService<SettingsViewModel>();
        InitializeComponent();
        InitVal();
        Loaded += LoadedApp; //Приложение загружено - разрешить 
    }

    #region JSON and Initialization

    private async void InitVal()
    {
        try
        {
            try
            {
                AutostartCom.SelectedIndex = SettingsService.AutostartType;
            }
            catch
            {
                AutostartCom.SelectedIndex = 0;
            }

            CbApplyStart.IsOn = SettingsService.ReapplyLatestSettingsOnAppLaunch;
            CbAutoReapply.IsOn = SettingsService.ReapplyOverclock;
            nudAutoReapply.Value = SettingsService.ReapplyOverclockTimer;
            CbAutoCheck.IsOn = SettingsService.CheckForUpdates;
            ReapplySafe.IsOn = SettingsService.ReapplySafeOverclock;
            ThemeLight.Visibility = SettingsService.ThemeType > 7 ? Visibility.Visible : Visibility.Collapsed;
            ThemeCustomBg.IsEnabled = SettingsService.ThemeType > 7;
            Settings_RTSS_Enable.IsOn = SettingsService.RTSSMetricsEnabled;
            Settings_Keybinds_Enable.IsOn = SettingsService.HotkeysEnabled;
            RTSS_LoadAndApply();
            UpdateTheme_ComboBox();
            NiIcon_LoadValues();
            await Task.Delay(390);
        }
        catch (Exception e)
        {
            SendSmuCommand.TraceIt_TraceError(e.ToString());
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
                        ThemeCombobox.Items.Add(theme.ThemeName.GetLocalized());
                    }
                    catch
                    {
                        ThemeCombobox.Items.Add(theme.ThemeName);
                    }
                }

                ThemeOpacity.Value = _themeSelectorService.Themes[SettingsService.ThemeType].ThemeOpacity;
                ThemeMaskOpacity.Value = _themeSelectorService.Themes[SettingsService.ThemeType].ThemeMaskOpacity;
                ThemeCustom.IsOn = _themeSelectorService.Themes[SettingsService.ThemeType].ThemeCustom;
                ThemeCustomBg.IsOn = _themeSelectorService.Themes[SettingsService.ThemeType].ThemeCustomBg;
                Theme_Custom();
            }

            ThemeCombobox.SelectedIndex = SettingsService.ThemeType;
            ThemeLight.Visibility =
                SettingsService.ThemeType > 7 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch
        {
            try
            {
                SettingsService.ThemeType /= 2;
            }
            catch
            {
                SettingsService.ThemeType = 0;
            } //Нельзя делить на ноль

            SettingsService.SaveSettings();
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
            ThemeCustomBg.IsEnabled = SettingsService.ThemeType > 7;
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
        // Загрузка данных из JSON файла
        RtssLoad();
        Settings_RTSS_Enable_Name.Visibility = Settings_RTSS_Enable.IsOn ? Visibility.Visible : Visibility.Collapsed;
        RTTS_GridView.Visibility = Settings_RTSS_Enable.IsOn ? Visibility.Visible : Visibility.Collapsed;
        RTSS_AdvancedCodeEditor_ToggleSwitch.Visibility =
            Settings_RTSS_Enable.IsOn ? Visibility.Visible : Visibility.Collapsed;
        RTSS_AdvancedCodeEditor_EditBox_Scroll.Visibility =
            Settings_RTSS_Enable.IsOn ? Visibility.Visible : Visibility.Collapsed;
        LoadAndFormatAdvancedCodeEditor(_rtssset.AdvancedCodeEditor);
        RTSS_AdvancedCodeEditor_ToggleSwitch.IsOn = _rtssset.IsAdvancedCodeEditorEnabled;

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
                    toggleButton.IsChecked = _rtssset.RTSS_Elements[i].UseCompact;
                }
            }

            // Применение значения CheckBox
            if (!string.IsNullOrEmpty(checkBoxName))
            {
                var checkBox = (CheckBox)FindName(checkBoxName);
                if (checkBox != null)
                {
                    checkBox.IsChecked = _rtssset.RTSS_Elements[i].Enabled;
                }
            }

            // Применение значения TextBox
            if (!string.IsNullOrEmpty(textBoxName))
            {
                var textBox = (TextBox)FindName(textBoxName);
                if (textBox != null)
                {
                    textBox.Text = _rtssset.RTSS_Elements[i].Name;
                }
            }

            // Применение значения ColorPicker
            if (!string.IsNullOrEmpty(colorPickerName))
            {
                var colorPicker = (ColorPicker)FindName(colorPickerName);
                if (colorPicker != null)
                {
                    var color = _rtssset.RTSS_Elements[i].Color;
                    var r = Convert.ToByte(color.Substring(1, 2), 16);
                    var g = Convert.ToByte(color.Substring(3, 2), 16);
                    var b = Convert.ToByte(color.Substring(5, 2), 16);
                    colorPicker.Color = Color.FromArgb(255, r, g, b);
                }
            }
        }
    }
    /*private void LoadAndFormatAdvancedCodeEditor()
    {
        // Загрузка строки из файла или иного источника
        string advancedCode = rtssset.AdvancedCodeEditor;

        // Переменная для хранения текущего цвета и размера
        Windows.UI.Color currentColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
        bool isSubscript = false;
        bool isSuperscript = false;

        // Очищаем текущее содержимое RichEditBox
        RTSS_AdvancedCodeEditor_EditBox.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, "");

        int currentIndex = 0;
        while (currentIndex < advancedCode.Length)
        {
            // Ищем следующие управляющие символы
            int nextIndex = advancedCode.IndexOfAny(new char[] { '<', '%', '$', '\\', '\n' }, currentIndex);

            if (nextIndex == -1)
            {
                // Добавляем оставшуюся строку
                AddFormattedText(advancedCode.Substring(currentIndex), currentColor, isSuperscript, isSubscript);
                break;
            }

            // Добавляем текст перед управляющим символом
            if (nextIndex > currentIndex)
            {
                AddFormattedText(advancedCode.Substring(currentIndex, nextIndex - currentIndex), currentColor, isSuperscript, isSubscript);
                currentIndex = nextIndex;
            }

            // Обработка управляющих символов
            if (advancedCode[nextIndex] == '<')
            {
                // Обрабатываем теги <C> и <S>
                if (advancedCode[nextIndex + 1] == 'C')
                {
                    // Изменение цвета
                    int closeIndex = advancedCode.IndexOf('>', nextIndex);
                    if (closeIndex != -1)
                    {
                        string colorCode = advancedCode.Substring(nextIndex + 3, closeIndex - nextIndex - 3);
                        currentColor = ParseColor(colorCode);
                        currentIndex = closeIndex + 1;
                    }
                }
                else if (advancedCode[nextIndex + 1] == 'S')
                {
                    // Обработка изменения размера текста
                    int closeIndex = advancedCode.IndexOf('>', nextIndex);
                    if (closeIndex != -1)
                    {
                        string sizeCode = advancedCode.Substring(nextIndex + 2, closeIndex - nextIndex - 2);
                        int sizeValue = int.Parse(sizeCode);

                        if (sizeValue > 0)
                        {
                            isSuperscript = true;
                            isSubscript = false;
                        }
                        else
                        {
                            isSuperscript = false;
                            isSubscript = true;
                        }
                        currentIndex = closeIndex + 1;
                    }
                }
            }
            else if (advancedCode[nextIndex] == '%')
            {
                // Обработка элементов с %...%
                int endIndex = advancedCode.IndexOf('%', nextIndex + 1);
                if (endIndex != -1)
                {
                    string element = advancedCode.Substring(nextIndex, endIndex - nextIndex + 1);
                    AddFormattedText(element, Windows.UI.Color.FromArgb(255, 255, 127, 80), isSuperscript, isSubscript);
                    currentIndex = endIndex + 1;
                }
                else
                {
                    AddFormattedText(advancedCode.Substring(nextIndex), Windows.UI.Color.FromArgb(255, 255, 127, 80), isSuperscript, isSubscript);
                    break;
                }
            }
            else if (advancedCode[nextIndex] == '$')
            {
                // Обработка элементов с $...$
                int endIndex = advancedCode.IndexOf('$', nextIndex + 1);
                if (endIndex != -1)
                {
                    string element = advancedCode.Substring(nextIndex, endIndex - nextIndex + 1);
                    AddFormattedText(element, Windows.UI.Color.FromArgb(255, 67, 182, 86), isSuperscript, isSubscript);
                    currentIndex = endIndex + 1;
                }
                else
                {
                    AddFormattedText(advancedCode.Substring(nextIndex), Windows.UI.Color.FromArgb(255, 67, 182, 86), isSuperscript, isSubscript);
                    break;
                }
            }
            else if (advancedCode[nextIndex] == '\\')
            {
                // Обработка перехода на новую строку
                if (advancedCode[nextIndex + 1] == 'n')
                {
                    RTSS_AdvancedCodeEditor_EditBox.Document.Selection.TypeText("\n");
                    currentIndex = nextIndex + 2;
                }
            }
            else if (advancedCode[nextIndex] == '\n')
            {
                // Обработка символа новой строки
                RTSS_AdvancedCodeEditor_EditBox.Document.Selection.TypeText("\n");
                currentIndex++;
            }
        }
    }*/
    /*    private void LoadAndFormatAdvancedCodeEditor()
        {
            // Загрузка строки из файла или иного источника
            string advancedCode = rtssset.AdvancedCodeEditor;

            // Переменная для хранения текущего цвета и размера
            Windows.UI.Color currentColor = Windows.UI.Color.FromArgb(255,255,255,255);
            bool isSubscript = false;
            bool isSuperscript = false;

            // Очищаем текущее содержимое RichEditBox
            RTSS_AdvancedCodeEditor_EditBox.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, "");

            int currentIndex = 0;
            while (currentIndex < advancedCode.Length)
            {
                // Ищем следующие управляющие символы
                int nextIndex = advancedCode.IndexOfAny(new char[] { '<', '%', '$', '\\', '\n' }, currentIndex);

                if (nextIndex == -1)
                {
                    // Добавляем оставшуюся строку
                    AddFormattedText(advancedCode.Substring(currentIndex), currentColor, isSuperscript, isSubscript);
                    break;
                }

                // Добавляем текст перед управляющим символом
                if (nextIndex > currentIndex)
                {
                    AddFormattedText(advancedCode.Substring(currentIndex, nextIndex - currentIndex), currentColor, isSuperscript, isSubscript);
                    currentIndex = nextIndex;
                }

                // Обработка управляющих символов
                if (advancedCode[nextIndex] == '<')
                {
                    // Обрабатываем теги <C> и <S>
                    if (advancedCode[nextIndex + 1] == 'C')
                    {
                        // Изменение цвета
                        int closeIndex = advancedCode.IndexOf('>', nextIndex);
                        if (closeIndex != -1)
                        {
                            string colorCode = advancedCode.Substring(nextIndex + 3, closeIndex - nextIndex - 3);
                            currentColor = ParseColor(colorCode);
                            currentIndex = closeIndex + 1;
                        }
                    }
                    else if (advancedCode[nextIndex + 1] == 'S')
                    {
                        // Обработка изменения размера текста
                        int closeIndex = advancedCode.IndexOf('>', nextIndex);
                        if (closeIndex != -1 && advancedCode[nextIndex + 2] == '=')
                        {
                            string sizeCode = advancedCode.Substring(nextIndex + 3, closeIndex - nextIndex - 3);
                            if (int.TryParse(sizeCode, out int sizeValue))
                            {
                                if (sizeValue > 0)
                                {
                                    isSuperscript = true;
                                    isSubscript = false;
                                }
                                else
                                {
                                    isSuperscript = false;
                                    isSubscript = true;
                                }
                            }
                            currentIndex = closeIndex + 1;
                        }
                    }
                }
                else if (advancedCode[nextIndex] == '%')
                {
                    // Обработка элементов с %...%
                    int endIndex = advancedCode.IndexOf('%', nextIndex + 1);
                    if (endIndex != -1)
                    {
                        string element = advancedCode.Substring(nextIndex, endIndex - nextIndex + 1);
                        AddFormattedText(element, Windows.UI.Color.FromArgb(255, 255, 127, 80), isSuperscript, isSubscript);
                        currentIndex = endIndex + 1;
                    }
                    else
                    {
                        AddFormattedText(advancedCode.Substring(nextIndex), Windows.UI.Color.FromArgb(255, 255, 127, 80), isSuperscript, isSubscript);
                        break;
                    }
                }
                else if (advancedCode[nextIndex] == '$')
                {
                    // Обработка элементов с $...$
                    int endIndex = advancedCode.IndexOf('$', nextIndex + 1);
                    if (endIndex != -1)
                    {
                        string element = advancedCode.Substring(nextIndex, endIndex - nextIndex + 1);
                        AddFormattedText(element, Windows.UI.Color.FromArgb(255, 67, 182, 86), isSuperscript, isSubscript);
                        currentIndex = endIndex + 1;
                    }
                    else
                    {
                        AddFormattedText(advancedCode.Substring(nextIndex), Windows.UI.Color.FromArgb(255, 67, 182, 86), isSuperscript, isSubscript);
                        break;
                    }
                }
                else if (advancedCode[nextIndex] == '\\')
                {
                    // Обработка перехода на новую строку
                    if (advancedCode[nextIndex + 1] == 'n')
                    {
                        RTSS_AdvancedCodeEditor_EditBox.Document.Selection.TypeText("\n");
                        currentIndex = nextIndex + 2;
                    }
                }
                else if (advancedCode[nextIndex] == '\n')
                {
                    // Обработка символа новой строки
                    RTSS_AdvancedCodeEditor_EditBox.Document.Selection.TypeText("\n");
                    currentIndex++;
                }
            }
        }

        private void AddFormattedText(string text, Windows.UI.Color color, bool isSuperscript, bool isSubscript)
        {
            var document = RTSS_AdvancedCodeEditor_EditBox.Document;
            var selection = document.Selection;

            selection.TypeText(text);
            selection.CharacterFormat.ForegroundColor = color;

            if (isSuperscript)
            {
                selection.CharacterFormat.Subscript = Microsoft.UI.Text.FormatEffect.Off;
                selection.CharacterFormat.Superscript = Microsoft.UI.Text.FormatEffect.On;
                selection.CharacterFormat.Size *= 0.5f;
            }
            else if (isSubscript)
            {
                selection.CharacterFormat.Superscript = Microsoft.UI.Text.FormatEffect.Off;
                selection.CharacterFormat.Subscript = Microsoft.UI.Text.FormatEffect.On;
                selection.CharacterFormat.Size *= 0.5f;
            }
            else
            {
                selection.CharacterFormat.Subscript = Microsoft.UI.Text.FormatEffect.Off;
                selection.CharacterFormat.Superscript = Microsoft.UI.Text.FormatEffect.Off;
            }
        }

         */

    private Color ParseColor(string hex)
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

    private void RtssSave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                "SakuOverclock"));
            File.WriteAllText(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\rtssparam.json",
                JsonConvert.SerializeObject(_rtssset, Formatting.Indented));
        }
        catch
        {
            //
        }
    }

    private void RtssLoad()
    {
        try
        {
            _rtssset = JsonConvert.DeserializeObject<RTSSsettings>(File.ReadAllText(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\rtssparam.json"))!;
            _rtssset.RTSS_Elements.RemoveRange(0, 9);
            //if (rtssset == null) { rtssset = new JsonContainers.RTSSsettings(); RtssSave(); }
        }
        catch
        {
            _rtssset = new RTSSsettings();
            RtssSave();
        }
    }

    private void NiSave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                "SakuOverclock"));
            File.WriteAllText(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\niicons.json",
                JsonConvert.SerializeObject(_niicons, Formatting.Indented));
        }
        catch
        {
            //
        }
    }

    private void NiLoad()
    {
        try
        {
            _niicons = JsonConvert.DeserializeObject<NiIconsSettings>(File.ReadAllText(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\niicons.json"))!;
        }
        catch
        {
            _niicons = new NiIconsSettings();
            NiSave();
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
        SettingsService.HotkeysEnabled = Settings_Keybinds_Enable.IsOn;
        SettingsService.SaveSettings();
    }

    private void AutostartCom_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        SettingsService.AutostartType = AutostartCom.SelectedIndex;
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
                SendSmuCommand.TraceIt_TraceError(exception.ToString());
            }
        }

        SettingsService.SaveSettings();
    }

    private void CbApplyStart_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        SettingsService.ReapplyLatestSettingsOnAppLaunch = CbApplyStart.IsOn;

        SettingsService.SaveSettings();
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
            SettingsService.ReapplyOverclock = true;
            SettingsService.ReapplyOverclockTimer = nudAutoReapply.Value;
        }
        else
        {
            AutoReapplyNumberboxPanel.Visibility = Visibility.Collapsed;
            SettingsService.ReapplyOverclock = false;
            SettingsService.ReapplyOverclockTimer = 3;
        }

        SettingsService.SaveSettings();
    }

    private void CbAutoCheck_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        SettingsService.CheckForUpdates = CbAutoCheck.IsOn;

        SettingsService.SaveSettings();
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
            SettingsService.ReapplyOverclock = true;
            SettingsService.ReapplyOverclockTimer = nudAutoReapply.Value;
            SettingsService.SaveSettings();
        }
        catch
        {
            //
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
            SettingsService.ReapplySafeOverclock = ReapplySafe.IsOn;
            SendSmuCommand.SafeReapply = ReapplySafe.IsOn;
            SettingsService.SaveSettings();
        }
        catch
        {
            //
        }
    }

    private void ThemeCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        SettingsService.ThemeType = ThemeCombobox.SelectedIndex != -1 ? ThemeCombobox.SelectedIndex : 0;
        SettingsService.SaveSettings();
        if (_themeSelectorService.Themes.Count != 0)
        {
            try
            {
                ViewModel.SwitchThemeCommand.Execute(_themeSelectorService.Themes[SettingsService.ThemeType].ThemeLight
                    ? ElementTheme.Light
                    : ElementTheme.Dark);
            }
            catch
            {
                SettingsService.ThemeType = 0;
                SettingsService.SaveSettings();
            }

            if (SettingsService.ThemeType == 0)
            {
                ViewModel.SwitchThemeCommand.Execute(ElementTheme.Default);
            }

            ThemeCustom.IsOn = _themeSelectorService.Themes[SettingsService.ThemeType].ThemeCustom;
            ThemeOpacity.Value = _themeSelectorService.Themes[SettingsService.ThemeType].ThemeOpacity;
            ThemeMaskOpacity.Value = _themeSelectorService.Themes[SettingsService.ThemeType].ThemeMaskOpacity;
            ThemeCustomBg.IsOn = _themeSelectorService.Themes[SettingsService.ThemeType].ThemeCustomBg;
            ThemeCustomBg.IsEnabled = ThemeCombobox.SelectedIndex > 7;
            ThemeLight.IsOn = _themeSelectorService.Themes[SettingsService.ThemeType].ThemeLight;
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
        _themeSelectorService.Themes[SettingsService.ThemeType].ThemeCustom = ThemeCustom.IsOn;
        _themeSelectorService.SaveThemeInSettings();
    }

    private void ThemeOpacity_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_isLoaded)
        {
            return;
        }

        _themeSelectorService.Themes[SettingsService.ThemeType].ThemeOpacity = ThemeOpacity.Value;
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

        _themeSelectorService.Themes[SettingsService.ThemeType].ThemeMaskOpacity = ThemeMaskOpacity.Value;
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

        _themeSelectorService.Themes[SettingsService.ThemeType].ThemeCustomBg = ThemeCustomBg.IsOn;
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
                //IsEnabled = false,
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
                                    Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 0, 0)),
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
            //Открыть диалог с изменением 
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
                        fromLink
                    }
                },
                CloseButtonText = "Cancel".GetLocalized(),
                PrimaryButtonText = "ThemeSelect".GetLocalized(),
                DefaultButton = ContentDialogButton.Close
            };
            fromFile.Click += (_, _) =>
            {
                fromFilePickedFile.Text = "";
                var ofn = new OpenFileDialog.OpenFileName
                {
                    lpstrFile = new string(new char[256])
                };
                ofn.nMaxFile = ofn.lpstrFile.Length;
                ofn.lpstrFilter = "Images (*.BMP;*.JPG;*.GIF;*.png;*.jpg)";
                ofn.lpstrTitle = "Select an image file";
                ofn.Flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000200 | 0x00020000; // OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_READONLY | OFN_NOCHANGEDIR

                // Get the current window's HWND
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                ofn.hwndOwner = hwnd;

                if (OpenFileDialog.GetOpenFileName(ofn))
                {                    fromFilePickedFile.Text = ofn.lpstrFile;
                    // Do something with the file path
                }
                else
                {
                    // User cancelled
                }
                /* // Создаём FileOpenPicker 
                 var filePicker = new FileOpenPicker
                 {
                     SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                     ViewMode = PickerViewMode.List 
                 };
                 //var hwnd = WindowNative.GetWindowHandle(App.MainWindow); // App.MainWindow — Главный Window
                 //var hwnd = ActivationInvokeHandler.FindMainWindowHWND(null, "Saku Overclock");
                 //InitializeWithWindow.Initialize(filePicker, App.Hwnd);
                 //filePicker.SetOwnerWindow(App.MainWindow);
                 // Устанавливаем фильтры для поддерживаемых форматов
                 Windows.Storage.Pickers.WindowsStoragePickersExtensions.SetOwnerWindow(filePicker, App.MainWindow);
                 filePicker.FileTypeFilter.Add(".gif");
                 filePicker.FileTypeFilter.Add(".png");
                 filePicker.FileTypeFilter.Add(".jpg");
                 filePicker.FileTypeFilter.Add(".jpeg");
                 filePicker.FileTypeFilter.Add(".bmp");
                 filePicker.ViewMode = PickerViewMode.Thumbnail;

                 // Открываем диалог выбора файла 
                 var file = await filePicker.PickSingleFileAsync();
                 if (file != null)
                 {
                     // Проверяем, является ли файл поддерживаемым изображением
                     fromFilePickedFile.Text = "ThemePickedFile".GetLocalized() + file.Path;
                     endStringPath = file.Path;
                 }
                 else
                 {
                     fromFilePickedFile.Text = "ThemeOpCancel".GetLocalized();
                 }

                 // Переключение видимости элементов
                 if (fromFilePickedFile.Visibility == Visibility.Collapsed)
                 {
                     fromFileWhy.Visibility = Visibility.Collapsed;
                     fromFilePickedFile.Visibility = Visibility.Visible;
                 }
                 else
                 {
                     fromFileWhy.Visibility = Visibility.Visible;
                     fromFilePickedFile.Visibility = Visibility.Collapsed;
                 }*/

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
            SendSmuCommand.TraceIt_TraceError(ex.ToString());
        }
    }

    private async void CustomTheme_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            //Отрыть редактор тем  
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
                                SettingsService.ThemeType = 0;
                                SettingsService.SaveSettings();
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
                //Добавить новую тему
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
                            catch
                            {
                                //
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
        catch
        {
            //
        }
    }


    private void ThemeLight_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        _themeSelectorService.Themes[SettingsService.ThemeType].ThemeLight = ThemeLight.IsOn;
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
            Settings_ni_Icons.IsOn = SettingsService.NiIconsEnabled;
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

                NiIconComboboxElements.SelectedIndex = SettingsService.NiIconsType;
                if (NiIconComboboxElements.SelectedIndex >= 0)
                {
                    NiIcon_Stackpanel.Visibility = Visibility.Visible;
                    Settings_ni_ContextMenu.Visibility = Visibility.Visible;
                    Settings_NiIconComboboxElements.Visibility = Visibility.Visible;
                    Settings_ni_EnabledElement.Visibility = Visibility.Visible;
                }
                if (SettingsService.NiIconsType >= 0 && _niicons.Elements.Count >= SettingsService.NiIconsType)
                {
                    Settings_ni_EnabledElement.IsOn = _niicons.Elements[SettingsService.NiIconsType].IsEnabled;
                    if (!_niicons.Elements[SettingsService.NiIconsType].IsEnabled)
                    {
                        NiIcon_Stackpanel.Visibility = Visibility.Collapsed;
                        Settings_ni_ContextMenu.Visibility = Visibility.Collapsed;
                    }

                    NiIconCombobox.SelectedIndex =
                        _niicons.Elements[SettingsService.NiIconsType].ContextMenuType;
                    NiIcons_ColorPicker_ColorPicker.Color =
                        ParseColor(_niicons.Elements[SettingsService.NiIconsType].Color);
                    Settings_Ni_GradientToggle.IsOn = _niicons.Elements[SettingsService.NiIconsType].IsGradient;
                    NiIconShapeCombobox.SelectedIndex = _niicons.Elements[SettingsService.NiIconsType].IconShape;
                    Settings_ni_Fontsize.Value = _niicons.Elements[SettingsService.NiIconsType].FontSize;
                    Settings_ni_Opacity.Value = _niicons.Elements[SettingsService.NiIconsType].BgOpacity;
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
            SettingsService.NiIconsType = -1; //Нет сохранённых
            SettingsService.SaveSettings();
        }
    }

    private void NiIconComboboxElements_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        SettingsService.NiIconsType = NiIconComboboxElements.SelectedIndex;
        SettingsService.SaveSettings();
        NiLoad();
        if (_niicons.Elements.Count != 0 && SettingsService.NiIconsType != -1)
        {
            if (NiIconComboboxElements.SelectedIndex >= 0)
            {
                NiIcon_Stackpanel.Visibility = Visibility.Visible;
                Settings_ni_ContextMenu.Visibility = Visibility.Visible;
                Settings_NiIconComboboxElements.Visibility = Visibility.Visible;
                Settings_ni_EnabledElement.Visibility = Visibility.Visible;
            }

            Settings_ni_EnabledElement.IsOn = _niicons.Elements[SettingsService.NiIconsType].IsEnabled;
            if (!_niicons.Elements[SettingsService.NiIconsType].IsEnabled)
            {
                NiIcon_Stackpanel.Visibility = Visibility.Collapsed;
                Settings_ni_ContextMenu.Visibility = Visibility.Collapsed;
            }

            NiIconCombobox.SelectedIndex = _niicons.Elements[SettingsService.NiIconsType].ContextMenuType;
            NiIcons_ColorPicker_ColorPicker.Color =
                ParseColor(_niicons.Elements[SettingsService.NiIconsType].Color);
            Settings_Ni_GradientToggle.IsOn = _niicons.Elements[SettingsService.NiIconsType].IsGradient;
            NiIconShapeCombobox.SelectedIndex = _niicons.Elements[SettingsService.NiIconsType].IconShape;
            Settings_ni_Fontsize.Value = _niicons.Elements[SettingsService.NiIconsType].FontSize;
            Settings_ni_Opacity.Value = _niicons.Elements[SettingsService.NiIconsType].BgOpacity;
        }
    }

    private void NiIconCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        NiLoad();
        _niicons.Elements[SettingsService.NiIconsType].ContextMenuType = NiIconCombobox.SelectedIndex;
        NiSave();
    }

    private void Settings_ni_Icons_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        SettingsService.NiIconsEnabled = Settings_ni_Icons.IsOn;
        SettingsService.SaveSettings();
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

    private void Settings_ni_EnabledElement_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        NiLoad();
        _niicons.Elements[SettingsService.NiIconsType].IsEnabled = Settings_ni_EnabledElement.IsOn;
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
    }

    private void Settings_ni_Fontsize_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_isLoaded)
        {
            return;
        }

        NiLoad();
        _niicons.Elements[SettingsService.NiIconsType].FontSize =
            Convert.ToInt32(Settings_ni_Fontsize.Value);
        NiSave();
    }

    private void Settings_ni_Opacity_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_isLoaded)
        {
            return;
        }

        NiLoad();
        _niicons.Elements[SettingsService.NiIconsType].BgOpacity = Settings_ni_Opacity.Value;
        NiSave();
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
                //Добавить новую тему
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
                            catch
                            {
                                //
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
            SendSmuCommand.TraceIt_TraceError(exception.ToString());
        }
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
            _niicons.Elements[SettingsService.NiIconsType].Color =
                $"{NiIcons_ColorPicker_ColorPicker.Color.R:X2}{NiIcons_ColorPicker_ColorPicker.Color.G:X2}{NiIcons_ColorPicker_ColorPicker.Color.B:X2}";
        }
        else if (Settings_Ni_GradientColorSwitcher.IsChecked == true)
        {
            _niicons.Elements[SettingsService.NiIconsType].SecondColor =
                $"{NiIcons_ColorPicker_ColorPicker.Color.R:X2}{NiIcons_ColorPicker_ColorPicker.Color.G:X2}{NiIcons_ColorPicker_ColorPicker.Color.B:X2}";
        }

        NiSave();
    }

    private void Settings_Ni_GradientToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        NiLoad();
        _niicons.Elements[SettingsService.NiIconsType].IsGradient = true;
        NiSave();
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
                    ParseColor(_niicons.Elements[SettingsService.NiIconsType].SecondColor);
            }
            else if (button.IsChecked == false)
            {
                button.Content = "Settings_ni_TrayMonGradientColorSwitch/Content".GetLocalized() + "1";
                NiIcons_ColorPicker_ColorPicker.Color =
                    ParseColor(_niicons.Elements[SettingsService.NiIconsType].Color);
            }
        }
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
            _niicons.Elements.RemoveAt(SettingsService.ThemeType);
            NiSave();
            SettingsService.ThemeType = -1;
            SettingsService.SaveSettings();
            NiIcon_LoadValues();
        }
        catch (Exception ex)
        {
            SendSmuCommand.TraceIt_TraceError(ex.ToString());
        }
    }

    private void Settings_ni_ResetDef_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        NiLoad();
        _niicons.Elements[SettingsService.NiIconsType].IsEnabled = true;
        _niicons.Elements[SettingsService.NiIconsType].ContextMenuType = 1;
        _niicons.Elements[SettingsService.NiIconsType].Color = "FF6ACF";
        _niicons.Elements[SettingsService.NiIconsType].IconShape = 0;
        _niicons.Elements[SettingsService.NiIconsType].FontSize = 9;
        _niicons.Elements[SettingsService.NiIconsType].BgOpacity = 0.5d;
        NiSave();
        NiIconComboboxElements_SelectionChanged(NiIconComboboxElements, SelectionChangedEventArgs.FromAbi(0));
    }

    private void NiIconShapeCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        NiLoad();
        _niicons.Elements[SettingsService.NiIconsType].IconShape = NiIconShapeCombobox.SelectedIndex;
        NiSave();
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

                _rtssset.RTSS_Elements[1].UseCompact = toggleButton.IsChecked == true;
                _rtssset.RTSS_Elements[2].UseCompact = toggleButton.IsChecked == true;
                _rtssset.RTSS_Elements[3].UseCompact = toggleButton.IsChecked == true;
                _rtssset.RTSS_Elements[4].UseCompact = toggleButton.IsChecked == true;
                _rtssset.RTSS_Elements[5].UseCompact = toggleButton.IsChecked == true;
                _rtssset.RTSS_Elements[6].UseCompact = toggleButton.IsChecked == true;
                _rtssset.RTSS_Elements[7].UseCompact = toggleButton.IsChecked == true;
                _rtssset.RTSS_Elements[8].UseCompact = toggleButton.IsChecked == true;
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
                _rtssset.RTSS_Elements[0].UseCompact = toggleButton.IsChecked == true;
            }

            if (toggleButton.Name == "RTSS_AllCompact_Toggle")
            {
                _rtssset.RTSS_Elements[1].UseCompact = toggleButton.IsChecked == true;
            }

            if (toggleButton.Name == "RTSS_SakuProfile_CompactToggle")
            {
                _rtssset.RTSS_Elements[2].UseCompact = toggleButton.IsChecked == true;
            }

            if (toggleButton.Name == "RTSS_StapmFastSlow_CompactToggle")
            {
                _rtssset.RTSS_Elements[3].UseCompact = toggleButton.IsChecked == true;
            }

            if (toggleButton.Name == "RTSS_EDCThermUsage_CompactToggle")
            {
                _rtssset.RTSS_Elements[4].UseCompact = toggleButton.IsChecked == true;
            }

            if (toggleButton.Name == "RTSS_CPUClocks_CompactToggle")
            {
                _rtssset.RTSS_Elements[5].UseCompact = toggleButton.IsChecked == true;
            }

            if (toggleButton.Name == "RTSS_AVGCPUClockVolt_CompactToggle")
            {
                _rtssset.RTSS_Elements[6].UseCompact = toggleButton.IsChecked == true;
            }

            if (toggleButton.Name == "RTSS_APUClockVoltTemp_CompactToggle")
            {
                _rtssset.RTSS_Elements[7].UseCompact = toggleButton.IsChecked == true;
            }

            if (toggleButton.Name == "RTSS_FrameRate_CompactToggle")
            {
                _rtssset.RTSS_Elements[8].UseCompact = toggleButton.IsChecked == true;
            }
        }

        if (s is CheckBox checkBox)
        {
            if (checkBox.Name == "RTSS_MainColor_Checkbox")
            {
                _rtssset.RTSS_Elements[0].Enabled = checkBox.IsChecked == true;
            }

            if (checkBox.Name == "RTSS_SecondColor_Checkbox")
            {
                _rtssset.RTSS_Elements[1].Enabled = checkBox.IsChecked == true;
            }

            if (checkBox.Name == "RTSS_SakuOverclockProfile_Checkbox")
            {
                _rtssset.RTSS_Elements[2].Enabled = checkBox.IsChecked == true;
            }

            if (checkBox.Name == "RTSS_StapmFastSlow_Checkbox")
            {
                _rtssset.RTSS_Elements[3].Enabled = checkBox.IsChecked == true;
            }

            if (checkBox.Name == "RTSS_EDCThermUsage_Checkbox")
            {
                _rtssset.RTSS_Elements[4].Enabled = checkBox.IsChecked == true;
            }

            if (checkBox.Name == "RTSS_CPUClocks_Checkbox")
            {
                _rtssset.RTSS_Elements[5].Enabled = checkBox.IsChecked == true;
            }

            if (checkBox.Name == "RTSS_AVGCPUClockVolt_Checkbox")
            {
                _rtssset.RTSS_Elements[6].Enabled = checkBox.IsChecked == true;
            }

            if (checkBox.Name == "RTSS_APUClockVoltTemp_Checkbox")
            {
                _rtssset.RTSS_Elements[7].Enabled = checkBox.IsChecked == true;
            }

            if (checkBox.Name == "RTSS_FrameRate_Checkbox")
            {
                _rtssset.RTSS_Elements[8].Enabled = checkBox.IsChecked == true;
            }
        }

        if (s is TextBox textBox)
        {
            if (textBox.Name == "RTSS_SakuOverclockProfile_TextBox")
            {
                _rtssset.RTSS_Elements[2].Name = textBox.Text;
            }

            if (textBox.Name == "RTSS_StapmFastSlow_TextBox")
            {
                _rtssset.RTSS_Elements[3].Name = textBox.Text;
            }

            if (textBox.Name == "RTSS_EDCThermUsage_TextBox")
            {
                _rtssset.RTSS_Elements[4].Name = textBox.Text;
            }

            if (textBox.Name == "RTSS_CPUClocks_TextBox")
            {
                _rtssset.RTSS_Elements[5].Name = textBox.Text;
            }

            if (textBox.Name == "RTSS_AVGCPUClockVolt_TextBox")
            {
                _rtssset.RTSS_Elements[6].Name = textBox.Text;
            }

            if (textBox.Name == "RTSS_APUClockVoltTemp_TextBox")
            {
                _rtssset.RTSS_Elements[7].Name = textBox.Text;
            }

            if (textBox.Name == "RTSS_FrameRate_TextBox")
            {
                _rtssset.RTSS_Elements[8].Name = textBox.Text;
            }
        }

        if (s is ColorPicker colorPicker)
        {
            if (colorPicker.Name == "RTSS_MainColor_ColorPicker")
            {
                _rtssset.RTSS_Elements[0].Color =
                    $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}";
            }

            if (colorPicker.Name == "RTSS_SecondColor_ColorPicker")
            {
                _rtssset.RTSS_Elements[1].Color =
                    $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}";
            }

            if (colorPicker.Name == "RTSS_SakuOverclockProfile_ColorPicker")
            {
                _rtssset.RTSS_Elements[2].Color =
                    $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}";
            }

            if (colorPicker.Name == "RTSS_StapmFastSlow_ColorPicker")
            {
                _rtssset.RTSS_Elements[3].Color =
                    $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}";
            }

            if (colorPicker.Name == "RTSS_EDCThermUsage_ColorPicker")
            {
                _rtssset.RTSS_Elements[4].Color =
                    $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}";
            }

            if (colorPicker.Name == "RTSS_CPUClocks_ColorPicker")
            {
                _rtssset.RTSS_Elements[5].Color =
                    $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}";
            }

            if (colorPicker.Name == "RTSS_AVGCPUClockVolt_ColorPicker")
            {
                _rtssset.RTSS_Elements[6].Color =
                    $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}";
            }

            if (colorPicker.Name == "RTSS_APUClockVoltTemp_ColorPicker")
            {
                _rtssset.RTSS_Elements[7].Color =
                    $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}";
            }

            if (colorPicker.Name == "RTSS_FrameRate_ColorPicker")
            {
                _rtssset.RTSS_Elements[8].Color =
                    $"#{colorPicker.Color.R:X2}{colorPicker.Color.G:X2}{colorPicker.Color.B:X2}";
            }
        }

        GenerateAdvancedCodeEditor();
        RtssSave();
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

        AddColorIfUnique(_rtssset.RTSS_Elements[0].Color);
        AddColorIfUnique(_rtssset.RTSS_Elements[1].Color);
        AddColorIfUnique(_rtssset.RTSS_Elements[2].Color);
        AddColorIfUnique(_rtssset.RTSS_Elements[3].Color);
        AddColorIfUnique(_rtssset.RTSS_Elements[4].Color);
        AddColorIfUnique(_rtssset.RTSS_Elements[5].Color);
        AddColorIfUnique(_rtssset.RTSS_Elements[6].Color);
        AddColorIfUnique(_rtssset.RTSS_Elements[7].Color);
        AddColorIfUnique(_rtssset.RTSS_Elements[8].Color);

        // Шаг 2: Создание CompactLib
        var compactLib = new bool[9];
        compactLib[0] = _rtssset.RTSS_Elements[0].UseCompact;
        compactLib[1] = _rtssset.RTSS_Elements[1].UseCompact;
        compactLib[2] = _rtssset.RTSS_Elements[1].UseCompact && _rtssset.RTSS_Elements[1].Enabled
            ? _rtssset.RTSS_Elements[1].UseCompact
            : _rtssset.RTSS_Elements[2].UseCompact;
        compactLib[3] = _rtssset.RTSS_Elements[1].UseCompact && _rtssset.RTSS_Elements[1].Enabled
            ? _rtssset.RTSS_Elements[1].UseCompact
            : _rtssset.RTSS_Elements[3].UseCompact;
        compactLib[4] = _rtssset.RTSS_Elements[1].UseCompact && _rtssset.RTSS_Elements[1].Enabled
            ? _rtssset.RTSS_Elements[1].UseCompact
            : _rtssset.RTSS_Elements[4].UseCompact;
        compactLib[5] = _rtssset.RTSS_Elements[1].UseCompact && _rtssset.RTSS_Elements[1].Enabled
            ? _rtssset.RTSS_Elements[1].UseCompact
            : _rtssset.RTSS_Elements[5].UseCompact;
        compactLib[6] = _rtssset.RTSS_Elements[1].UseCompact && _rtssset.RTSS_Elements[1].Enabled
            ? _rtssset.RTSS_Elements[1].UseCompact
            : _rtssset.RTSS_Elements[6].UseCompact;
        compactLib[7] = _rtssset.RTSS_Elements[1].UseCompact && _rtssset.RTSS_Elements[1].Enabled
            ? _rtssset.RTSS_Elements[1].UseCompact
            : _rtssset.RTSS_Elements[7].UseCompact;
        compactLib[8] = _rtssset.RTSS_Elements[1].UseCompact && _rtssset.RTSS_Elements[1].Enabled
            ? _rtssset.RTSS_Elements[1].UseCompact
            : _rtssset.RTSS_Elements[8].UseCompact;

        // Шаг 3: Создание EnableLib
        var enableLib = new bool[9];
        enableLib[0] = _rtssset.RTSS_Elements[0].Enabled;
        enableLib[1] = _rtssset.RTSS_Elements[1].Enabled;
        enableLib[2] = _rtssset.RTSS_Elements[2].Enabled;
        enableLib[3] = _rtssset.RTSS_Elements[3].Enabled;
        enableLib[4] = _rtssset.RTSS_Elements[4].Enabled;
        enableLib[5] = _rtssset.RTSS_Elements[5].Enabled;
        enableLib[6] = _rtssset.RTSS_Elements[6].Enabled;
        enableLib[7] = _rtssset.RTSS_Elements[7].Enabled;
        enableLib[8] = _rtssset.RTSS_Elements[8].Enabled;

        // Шаг 4: Создание TextLib
        var textLib = new string[7];
        textLib[0] = _rtssset.RTSS_Elements[2].Name.TrimEnd(); // Saku Overclock Profile
        textLib[1] = _rtssset.RTSS_Elements[3].Name.TrimEnd(); // STAPM Fast Slow
        textLib[2] = _rtssset.RTSS_Elements[4].Name.TrimEnd(); // EDC Therm CPU Usage
        textLib[3] = _rtssset.RTSS_Elements[5].Name.TrimEnd(); // CPU Clocks
        textLib[4] = _rtssset.RTSS_Elements[6].Name.TrimEnd(); // AVG Clock Volt
        textLib[5] = _rtssset.RTSS_Elements[7].Name.TrimEnd(); // APU Clock Volt Temp
        textLib[6] = _rtssset.RTSS_Elements[8].Name.TrimEnd(); // Frame Rate

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
        //Пример первой строки:
        // "<C0=FFA0A0><C1=A0FFA0><C2=FC89AC><C3=fa2363><S1=70><S2=-50>\n" +
        for (var i = 0; i < colorLib.Count; i++)
        {
            advancedCodeEditor.Append($"<C{i}={colorLib[i]}>");
        }

        advancedCodeEditor.Append("<S1=70><S2=-50>\n");

        // 5.2 Вторая строка (Saku Overclock)
        //Пример второй строки:
        // "<C0>Saku Overclock <C1>" + ViewModels.ГлавнаяViewModel.GetVersion() + ": <S0>$SelectedProfile$\n" +
        if (enableLib[2])
        {
            var colorIndexMain = _rtssset.RTSS_Elements[0].Enabled
                ? colorLib.IndexOf(_rtssset.RTSS_Elements[0].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(_rtssset.RTSS_Elements[2].Color.Replace("#", "")).ToString();
            var colorIndexSecond = _rtssset.RTSS_Elements[1].Enabled
                ? colorLib.IndexOf(_rtssset.RTSS_Elements[1].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(_rtssset.RTSS_Elements[2].Color.Replace("#", "")).ToString();
            var compactMain = _rtssset.RTSS_Elements[0].Enabled
                ? (compactLib[0] ? "<S1>" : "<S0>")
                : (compactLib[2] ? "<S1>" : "<S0>");
            var compactSecond = _rtssset.RTSS_Elements[1].Enabled
                ? (compactLib[1] ? "<S2>" : "<S0>")
                : (compactLib[2] ? "<S2>" : "<S0>");
            advancedCodeEditor.Append(
                $"<C{colorIndexMain}>{compactMain}{textLib[0]} {ГлавнаяViewModel.GetVersion()}: <C{colorIndexSecond}>{compactSecond}<S0>$SelectedProfile$\n");
        }

        // 5.3 Третья строка (STAPM Fast Slow)
        //Пример третьей строки:
        // "<S1><C2>STAPM, Fast, Slow: <C3><S0>$stapm_value$<S2>W<S1>$stapm_limit$W <S0>$fast_value$<S2>W<S1>$fast_limit$W <S0>$slow_value$<S2>W<S1>$slow_limit$W\n" +
        if (enableLib[3])
        {
            var colorIndexMain = _rtssset.RTSS_Elements[0].Enabled
                ? colorLib.IndexOf(_rtssset.RTSS_Elements[0].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(_rtssset.RTSS_Elements[3].Color.Replace("#", "")).ToString();
            var colorIndexSecond = _rtssset.RTSS_Elements[1].Enabled
                ? colorLib.IndexOf(_rtssset.RTSS_Elements[1].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(_rtssset.RTSS_Elements[3].Color.Replace("#", "")).ToString();
            var compactMain = _rtssset.RTSS_Elements[0].Enabled
                ? (compactLib[0] ? "<S1>" : "<S0>")
                : (compactLib[3] ? "<S1>" : "<S0>");
            var compactSecond = _rtssset.RTSS_Elements[1].Enabled
                ? (compactLib[1] ? "<S2>" : "<S0>")
                : (compactLib[3] ? "<S2>" : "<S0>");
            var compactSign = _rtssset.RTSS_Elements[1].Enabled
                ? (compactLib[1] ? "" : "/")
                : (compactLib[3] ? "" : "/");
            advancedCodeEditor.Append(
                $"<C{colorIndexMain}>{compactMain}{textLib[1]}: <C{colorIndexSecond}><S0>$stapm_value${compactSecond}W{compactSign}{compactSecond.Replace("2", "1")}$stapm_limit$W <S0>$fast_value${compactSecond}W{compactSign}{compactSecond.Replace("2", "1")}$fast_limit$W <S0>$slow_value${compactSecond}W{compactSign}{compactSecond.Replace("2", "1")}$slow_limit$W\n");
        }

        // - Для EDC Therm CPU Usage
        //Пример четвёртой строки:
        // "<C2>EDC, Therm, CPU Usage: <C3><S0>$vrmedc_value$<S2>A<S1>$vrmedc_max$A <C3><S0>$cpu_temp_value$<S2>C<S1>$cpu_temp_max$C<C3><S0> $cpu_usage$<S2>%<S1>\n" +
        if (enableLib[4])
        {
            var colorIndexMain = _rtssset.RTSS_Elements[0].Enabled
                ? colorLib.IndexOf(_rtssset.RTSS_Elements[0].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(_rtssset.RTSS_Elements[4].Color.Replace("#", "")).ToString();
            var colorIndexSecond = _rtssset.RTSS_Elements[1].Enabled
                ? colorLib.IndexOf(_rtssset.RTSS_Elements[1].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(_rtssset.RTSS_Elements[4].Color.Replace("#", "")).ToString();
            var compactMain = _rtssset.RTSS_Elements[0].Enabled
                ? (compactLib[0] ? "<S1>" : "<S0>")
                : (compactLib[4] ? "<S1>" : "<S0>");
            var compactSecond = _rtssset.RTSS_Elements[1].Enabled
                ? (compactLib[1] ? "<S2>" : "<S0>")
                : (compactLib[4] ? "<S2>" : "<S0>");
            var compactSign = _rtssset.RTSS_Elements[1].Enabled
                ? (compactLib[1] ? "" : "/")
                : (compactLib[4] ? "" : "/");
            advancedCodeEditor.Append(
                $"<C{colorIndexMain}>{compactMain}{textLib[2]}: <C{colorIndexSecond}><S0>$vrmedc_value${compactSecond}A{compactSign}{compactSecond.Replace("2", "1")}$vrmedc_max$A <S0>$cpu_temp_value${compactSecond}C{compactSign}{compactSecond.Replace("2", "1")}$cpu_temp_max$C <S0>$cpu_usage${compactSecond}%\n");
        }

        // - Для CPU Clocks
        //Пример пятой строки:
        // "<S1><C2>Clocks: $cpu_clock_cycle$<S1><C2>$currCore$:<S0><C3> $cpu_core_clock$<S2>GHz<S1>$cpu_core_voltage$V $cpu_clock_cycle_end$\n" +
        if (enableLib[5])
        {
            var colorIndexMain = _rtssset.RTSS_Elements[0].Enabled
                ? colorLib.IndexOf(_rtssset.RTSS_Elements[0].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(_rtssset.RTSS_Elements[5].Color.Replace("#", "")).ToString();
            var colorIndexSecond = _rtssset.RTSS_Elements[1].Enabled
                ? colorLib.IndexOf(_rtssset.RTSS_Elements[1].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(_rtssset.RTSS_Elements[5].Color.Replace("#", "")).ToString();
            var compactMain = _rtssset.RTSS_Elements[0].Enabled
                ? (compactLib[0] ? "<S1>" : "<S0>")
                : (compactLib[5] ? "<S1>" : "<S0>");
            var compactSecond = _rtssset.RTSS_Elements[1].Enabled
                ? (compactLib[1] ? "<S2>" : "<S0>")
                : (compactLib[5] ? "<S2>" : "<S0>");
            var compactSign = _rtssset.RTSS_Elements[1].Enabled
                ? (compactLib[1] ? "" : "/")
                : (compactLib[5] ? "" : "/");
            advancedCodeEditor.Append(
                $"<C{colorIndexMain}>{compactMain}{textLib[3]}: $cpu_clock_cycle$<C{colorIndexMain}>$currCore$: <C{colorIndexSecond}>$cpu_core_clock${compactSecond}GHz{compactSign}{compactSecond.Replace("2", "1")}$cpu_core_voltage$V $cpu_clock_cycle_end$\n");
        }

        // - Для AVG Clock Volt
        //Пример шестой строки:
        // "<C2>AVG Clock, Volt: <C3><S0>$average_cpu_clock$<S2>GHz<S1>$average_cpu_voltage$V" +
        if (enableLib[6])
        {
            var colorIndexMain = _rtssset.RTSS_Elements[0].Enabled
                ? colorLib.IndexOf(_rtssset.RTSS_Elements[0].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(_rtssset.RTSS_Elements[6].Color.Replace("#", "")).ToString();
            var colorIndexSecond = _rtssset.RTSS_Elements[1].Enabled
                ? colorLib.IndexOf(_rtssset.RTSS_Elements[1].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(_rtssset.RTSS_Elements[6].Color.Replace("#", "")).ToString();
            var compactMain = _rtssset.RTSS_Elements[0].Enabled
                ? (compactLib[0] ? "<S1>" : "<S0>")
                : (compactLib[6] ? "<S1>" : "<S0>");
            var compactSecond = _rtssset.RTSS_Elements[1].Enabled
                ? (compactLib[1] ? "<S2>" : "<S0>")
                : (compactLib[6] ? "<S2>" : "<S0>");
            var compactSign = _rtssset.RTSS_Elements[1].Enabled
                ? (compactLib[1] ? "" : "/")
                : (compactLib[6] ? "" : "/");
            advancedCodeEditor.Append(
                $"<C{colorIndexMain}>{compactMain}{textLib[4]}: <C{colorIndexSecond}><S0>$average_cpu_clock${compactSecond}GHz{compactSign}{compactSecond.Replace("2", "1")}$average_cpu_voltage$V\n");
        }

        // - Для APU Clock Volt Temp
        //Пример седьмой строки:
        // "<C2>APU Clock, Volt, Temp: <C3><S0>$gfx_clock$<S2>MHz<S1>$gfx_volt$V <S0>$gfx_temp$<S1>C\n" +
        if (enableLib[7])
        {
            var colorIndexMain = _rtssset.RTSS_Elements[0].Enabled
                ? colorLib.IndexOf(_rtssset.RTSS_Elements[0].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(_rtssset.RTSS_Elements[7].Color.Replace("#", "")).ToString();
            var colorIndexSecond = _rtssset.RTSS_Elements[1].Enabled
                ? colorLib.IndexOf(_rtssset.RTSS_Elements[1].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(_rtssset.RTSS_Elements[7].Color.Replace("#", "")).ToString();
            var compactMain = _rtssset.RTSS_Elements[0].Enabled
                ? (compactLib[0] ? "<S1>" : "<S0>")
                : (compactLib[7] ? "<S1>" : "<S0>");
            var compactSecond = _rtssset.RTSS_Elements[1].Enabled
                ? (compactLib[1] ? "<S2>" : "<S0>")
                : (compactLib[7] ? "<S2>" : "<S0>");
            var compactSign = _rtssset.RTSS_Elements[1].Enabled
                ? (compactLib[1] ? "" : "/")
                : (compactLib[7] ? "" : "/");
            advancedCodeEditor.Append(
                $"<C{colorIndexMain}>{compactMain}{textLib[5]}: <C{colorIndexSecond}><S0>$gfx_clock${compactSecond}MHz{compactSign}{compactSecond.Replace("2", "1")}$gfx_volt$V <S0>$gfx_temp${compactSecond}C\n");
        }

        // - Для Frame Rate
        //Пример восьмой строки:
        // "<C2>Framerate <C3><S0>%FRAMERATE% %FRAMETIME%";*/
        if (enableLib[8])
        {
            var colorIndexMain = _rtssset.RTSS_Elements[0].Enabled
                ? colorLib.IndexOf(_rtssset.RTSS_Elements[0].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(_rtssset.RTSS_Elements[8].Color.Replace("#", "")).ToString();
            var colorIndexSecond = _rtssset.RTSS_Elements[1].Enabled
                ? colorLib.IndexOf(_rtssset.RTSS_Elements[1].Color.Replace("#", "")).ToString()
                : colorLib.IndexOf(_rtssset.RTSS_Elements[8].Color.Replace("#", "")).ToString();
            var compactMain = _rtssset.RTSS_Elements[0].Enabled
                ? (compactLib[0] ? "<S1>" : "<S0>")
                : (compactLib[8] ? "<S1>" : "<S0>");
            advancedCodeEditor.Append(
                $"<C{colorIndexMain}>{compactMain}{textLib[6]}: <C{colorIndexSecond}><S0>%FRAMERATE% %FRAMETIME%");
        }

        // Финальная строка присваивается в rtssset.AdvancedCodeEditor
        _rtssset.AdvancedCodeEditor = advancedCodeEditor.ToString();
        LoadAndFormatAdvancedCodeEditor(_rtssset.AdvancedCodeEditor);
        RtssHandler.ChangeOsdText(_rtssset.AdvancedCodeEditor);
        RtssSave();
    }


    private void Settings_RTSS_Enable_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        SettingsService.RTSSMetricsEnabled = Settings_RTSS_Enable.IsOn;
        SettingsService.SaveSettings();
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

        RtssLoad();
        _rtssset.IsAdvancedCodeEditorEnabled = RTSS_AdvancedCodeEditor_ToggleSwitch.IsOn;
        RtssSave();
    }

    private void RTSS_AdvancedCodeEditor_EditBox_TextChanged(object sender, RoutedEventArgs e)
    {
        RTSS_AdvancedCodeEditor_EditBox.Document.GetText(TextGetOptions.None, out var newString);
        _rtssset.AdvancedCodeEditor = newString.Replace("\r", "\n").TrimEnd();
        RtssSave();
    }

    #endregion
}