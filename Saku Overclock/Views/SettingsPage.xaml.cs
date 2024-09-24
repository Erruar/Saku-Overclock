using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.ViewModels;
using Windows.Foundation.Metadata;
using Windows.UI.Text;
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
    private JsonContainers.NiIconsSettings niicons = new();
    private bool isLoaded = false;
    private JsonContainers.Notifications notify = new();

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
        ConfigLoad();
        try { AutostartCom.SelectedIndex = config.AutostartType; } catch { AutostartCom.SelectedIndex = 0; }
        CbApplyStart.IsOn = config.ReapplyLatestSettingsOnAppLaunch;
        CbAutoReapply.IsOn = config.ReapplyOverclock;  nudAutoReapply.Value = config.ReapplyOverclockTimer;
        CbAutoCheck.IsOn = config.CheckForUpdates;
        ReapplySafe.IsOn = config.ReapplySafeOverclock;
        ThemeLight.Visibility = config.ThemeType > 7 ? Visibility.Visible : Visibility.Collapsed;
        ThemeCustomBg.IsEnabled = config.ThemeType > 7;
        Settings_RTSS_Enable.IsOn = config.RTSSMetricsEnabled; 
        RTSS_LoadAndApply();
        UpdateTheme_ComboBox();
        NiIcon_LoadValues();
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
        Settings_RTSS_Enable_Name.Visibility = Settings_RTSS_Enable.IsOn ? Visibility.Visible : Visibility.Collapsed;
        RTTS_GridView.Visibility = Settings_RTSS_Enable.IsOn ? Visibility.Visible : Visibility.Collapsed;
        RTSS_AdvancedCodeEditor_ToggleSwitch.Visibility = Settings_RTSS_Enable.IsOn ? Visibility.Visible : Visibility.Collapsed;
        RTSS_AdvancedCodeEditor_EditBox_Scroll.Visibility = Settings_RTSS_Enable.IsOn ? Visibility.Visible : Visibility.Collapsed;
        LoadAndFormatAdvancedCodeEditor(rtssset.AdvancedCodeEditor);
        RTSS_AdvancedCodeEditor_ToggleSwitch.IsOn = rtssset.IsAdvancedCodeEditorEnabled;

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

    private Windows.UI.Color ParseColor(string hex)
    {
        if (hex.Length == 6)
        {
            return Windows.UI.Color.FromArgb(255,
                byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber),
                byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber),
                byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber));
        }
        return Windows.UI.Color.FromArgb(255, 255, 255, 255); // если цвет неизвестен
    }
    // Вспомогательный метод для преобразования HEX в Windows.UI.Color
    private void LoadAndFormatAdvancedCodeEditor(string advancedCode)
    { 
        if (string.IsNullOrEmpty(advancedCode))
        {
            return;
        }
        RTSS_AdvancedCodeEditor_EditBox.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, advancedCode.Replace("<Br>", "\n"));
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
    public void NiSave()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\niicons.json", JsonConvert.SerializeObject(niicons, Formatting.Indented));
        }
        catch { }
    }
    public void NiLoad()
    {
        try
        {
            niicons = JsonConvert.DeserializeObject<JsonContainers.NiIconsSettings>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\niicons.json"))!;
        }
        catch { niicons = new JsonContainers.NiIconsSettings(); NiSave(); }
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
        ConfigLoad();
        NiLoad(); 
        try
        {
            Settings_ni_Icons.IsOn = config.NiIconsEnabled;
            NiIconComboboxElements.Items.Clear();
            if (niicons.Elements != null)
            {
                for (var element = 0; element < niicons.Elements.Count; element++)
                {
                    try
                    {
                        NiIconComboboxElements.Items.Add(niicons.Elements[element].Name.GetLocalized());
                    }
                    catch
                    {
                        NiIconComboboxElements.Items.Add(niicons.Elements[element].Name);
                    }
                }
                NiIconComboboxElements.SelectedIndex = config.NiIconsType;
                if (NiIconComboboxElements.SelectedIndex >= 0)
                {
                    NiIcon_Stackpanel.Visibility = Visibility.Visible;
                    Settings_ni_ContextMenu.Visibility = Visibility.Visible;
                    Settings_NiIconComboboxElements.Visibility = Visibility.Visible;
                    Settings_ni_EnabledElement.Visibility = Visibility.Visible;
                }
                Settings_ni_EnabledElement.IsOn = niicons.Elements[config.NiIconsType].IsEnabled;
                if (!niicons.Elements[config.NiIconsType].IsEnabled)
                {
                    NiIcon_Stackpanel.Visibility = Visibility.Collapsed;
                    Settings_ni_ContextMenu.Visibility = Visibility.Collapsed;
                }
                NiIconCombobox.SelectedIndex = niicons.Elements[config.NiIconsType].ContextMenuType;
                NiIcons_ColorPicker_ColorPicker.Color = ParseColor(niicons.Elements[config.NiIconsType].Color);
                NiIconShapeCombobox.SelectedIndex = niicons.Elements[config.NiIconsType].IconShape;
                Settings_ni_Fontsize.Value = niicons.Elements[config.NiIconsType].FontSize;
                Settings_ni_Opacity.Value = niicons.Elements[config.NiIconsType].BgOpacity;
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
            config.NiIconsType = -1; //Нет сохранённых
            ConfigSave();
        }
    }
    private void NiIconComboboxElements_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!isLoaded) { return; }
        ConfigLoad();
        config.NiIconsType = NiIconComboboxElements.SelectedIndex;
        ConfigSave();
        NiLoad();
        if (niicons != null && niicons.Elements != null && config.NiIconsType != -1)
        {
            if (NiIconComboboxElements.SelectedIndex >= 0)
            {
                NiIcon_Stackpanel.Visibility = Visibility.Visible;
                Settings_ni_ContextMenu.Visibility = Visibility.Visible;
                Settings_NiIconComboboxElements.Visibility = Visibility.Visible;
                Settings_ni_EnabledElement.Visibility = Visibility.Visible;
            }
            Settings_ni_EnabledElement.IsOn = niicons.Elements[config.NiIconsType].IsEnabled;
            if (!niicons.Elements[config.NiIconsType].IsEnabled)
            {
                NiIcon_Stackpanel.Visibility = Visibility.Collapsed;
                Settings_ni_ContextMenu.Visibility = Visibility.Collapsed;
            }
            NiIconCombobox.SelectedIndex = niicons.Elements[config.NiIconsType].ContextMenuType;
            NiIcons_ColorPicker_ColorPicker.Color = ParseColor(niicons.Elements[config.NiIconsType].Color);
            NiIconShapeCombobox.SelectedIndex = niicons.Elements[config.NiIconsType].IconShape;
            Settings_ni_Fontsize.Value = niicons.Elements[config.NiIconsType].FontSize;
            Settings_ni_Opacity.Value = niicons.Elements[config.NiIconsType].BgOpacity;
        }
    }

    private void NiIconCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!isLoaded) { return; } 
        NiLoad(); niicons.Elements[config.NiIconsType].ContextMenuType = NiIconCombobox.SelectedIndex; NiSave();
    }

    private void Settings_ni_Icons_Toggled(object sender, RoutedEventArgs e)
    {
        if (!isLoaded) { return; } 
        ConfigLoad(); config.NiIconsEnabled = Settings_ni_Icons.IsOn; ConfigSave();
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
        if (!isLoaded) { return; }
        NiLoad(); niicons.Elements[config.NiIconsType].IsEnabled = Settings_ni_EnabledElement.IsOn; NiSave();
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
        if (!isLoaded) { return; }
        NiLoad(); niicons.Elements[config.NiIconsType].FontSize = Convert.ToInt32(Settings_ni_Fontsize.Value); NiSave();
    }

    private void Settings_ni_Opacity_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!isLoaded) { return; }
        NiLoad(); niicons.Elements[config.NiIconsType].BgOpacity = Settings_ni_Opacity.Value; NiSave();
    } 
    private async void Settings_ni_Add_Element_Click(object sender, RoutedEventArgs e)
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
        var baseNiBackground = Windows.UI.Color.FromArgb(255, 255, 255, 255); // Белый 
        var baseNiName = "New Element";
        NiLoad();
        try
        {
            if (niicons.Elements != null)
            {
                for (var element = 8; element < niicons.Elements.Count; element++)
                {
                    baseNiName = niicons.Elements[element].Name;
                    if (niicons.Elements[element].Color != "")
                    {
                        try
                        {
                            baseNiBackground = ParseColor(niicons.Elements[element].Color);
                        }
                        catch
                        {
                            baseNiBackground = Windows.UI.Color.FromArgb(255, 255, 255, 255);
                        }
                    }
                    else { baseNiBackground = Windows.UI.Color.FromArgb(255, 255, 255, 255); }
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
                                    Opacity = niicons.Elements[element].BgOpacity,
                                    VerticalAlignment = VerticalAlignment.Stretch,
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    Background =  new SolidColorBrush
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
                    eachButton.PointerEntered += (s, a) =>
                    {
                        niElementName.Margin = new Thickness(-90, 0, 0, 0);
                        buttonsPanel.Visibility = Visibility.Visible;
                    };
                    eachButton.PointerExited += (s, a) =>
                    {
                        niElementName.Margin = new Thickness(0);
                        buttonsPanel.Visibility = Visibility.Collapsed;
                    };
                    sureDelete.Click += (s, a) =>
                    {
                        try
                        {
                            niicons.Elements.RemoveAt(int.Parse(sureDelete.Name));
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
                    FontWeight = new Windows.UI.Text.FontWeight(700),
                    Text = "ThemeNewName".GetLocalized()
                }
            };
            if (niLoaderPanel.Children.Count > 0)
            {
                newNiIcon.Margin = new Thickness(0, 10, 0, 0);
            }
            niLoaderPanel.Children.Add(newNiIcon);
            //Добавить новую тему
            newNiIcon.Click += (s, a) =>
            {
                var niIconSelectedComboBox = new ComboBox
                { 
                    CornerRadius = new CornerRadius(15, 0, 0, 15),
                    Width = 300 
                };
                if (!NiIconComboboxElements.Items.Contains("Settings_ni_Values_STAPM".GetLocalized()))
                {
                    niIconSelectedComboBox.Items.Add(new ComboBoxItem()
                    {
                        Content = "Settings_ni_Values_STAPM".GetLocalized(),
                        Name = "Settings_ni_Values_STAPM"
                    });
                }
                if (!NiIconComboboxElements.Items.Contains("Settings_ni_Values_Fast".GetLocalized()))
                {
                    niIconSelectedComboBox.Items.Add(new ComboBoxItem()
                    {
                        Content = "Settings_ni_Values_Fast".GetLocalized(),
                        Name = "Settings_ni_Values_Fast"
                    });
                }
                if (!NiIconComboboxElements.Items.Contains("Settings_ni_Values_Slow".GetLocalized()))
                {
                    niIconSelectedComboBox.Items.Add(new ComboBoxItem()
                    {
                        Content = "Settings_ni_Values_Slow".GetLocalized(),
                        Name = "Settings_ni_Values_Slow"
                    });
                }
                if (!NiIconComboboxElements.Items.Contains("Settings_ni_Values_VRMEDC".GetLocalized()))
                {
                    niIconSelectedComboBox.Items.Add(new ComboBoxItem()
                    {
                        Content = "Settings_ni_Values_VRMEDC".GetLocalized(),
                        Name = "Settings_ni_Values_VRMEDC"
                    });
                }
                if (!NiIconComboboxElements.Items.Contains("Settings_ni_Values_CPUTEMP".GetLocalized()))
                {
                    niIconSelectedComboBox.Items.Add(new ComboBoxItem()
                    {
                        Content = "Settings_ni_Values_CPUTEMP".GetLocalized(),
                        Name = "Settings_ni_Values_CPUTEMP"
                    });
                }
                if (!NiIconComboboxElements.Items.Contains("Settings_ni_Values_CPUUsage".GetLocalized()))
                {
                    niIconSelectedComboBox.Items.Add(new ComboBoxItem()
                    {
                        Content = "Settings_ni_Values_CPUUsage".GetLocalized(),
                        Name = "Settings_ni_Values_CPUUsage"
                    });
                }
                if (!NiIconComboboxElements.Items.Contains("Settings_ni_Values_AVGCPUCLK".GetLocalized()))
                {
                    niIconSelectedComboBox.Items.Add(new ComboBoxItem()
                    {
                        Content = "Settings_ni_Values_AVGCPUCLK".GetLocalized(),
                        Name = "Settings_ni_Values_AVGCPUCLK"
                    });
                }
                if (!NiIconComboboxElements.Items.Contains("Settings_ni_Values_AVGCPUVOLT".GetLocalized()))
                {
                    niIconSelectedComboBox.Items.Add(new ComboBoxItem()
                    {
                        Content = "Settings_ni_Values_AVGCPUVOLT".GetLocalized(),
                        Name = "Settings_ni_Values_AVGCPUVOLT"
                    });
                }
                if (!NiIconComboboxElements.Items.Contains("Settings_ni_Values_GFXCLK".GetLocalized()))
                {
                    niIconSelectedComboBox.Items.Add(new ComboBoxItem()
                    {
                        Content = "Settings_ni_Values_GFXCLK".GetLocalized(),
                        Name = "Settings_ni_Values_GFXCLK"
                    });
                }
                if (!NiIconComboboxElements.Items.Contains("Settings_ni_Values_GFXTEMP".GetLocalized()))
                {
                    niIconSelectedComboBox.Items.Add(new ComboBoxItem()
                    {
                        Content = "Settings_ni_Values_GFXTEMP".GetLocalized(),
                        Name = "Settings_ni_Values_GFXTEMP"
                    });
                }
                if (!NiIconComboboxElements.Items.Contains("Settings_ni_Values_GFXVOLT".GetLocalized()))
                {
                    niIconSelectedComboBox.Items.Add(new ComboBoxItem()
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
                niIconAddButtonSuccess.Click += (s, a) =>
                {
                    if (niIconSelectedComboBox.SelectedIndex != -1)
                    {
                        try
                        {
                            niicons.Elements!.Add(new JsonContainers.NiIconsElements { Name = ((ComboBoxItem)niIconSelectedComboBox.SelectedItem).Name.ToString()! });
                            newNiIcon.Flyout.Hide();
                            niAddIconDialog.Hide();
                            NiSave();
                            NiIcon_LoadValues();
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
 
    private void NiIcons_ColorPicker_ColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (!isLoaded) { return; }
        NiLoad(); niicons.Elements[config.NiIconsType].Color = $"{NiIcons_ColorPicker_ColorPicker.Color.R:X2}{NiIcons_ColorPicker_ColorPicker.Color.G:X2}{NiIcons_ColorPicker_ColorPicker.Color.B:X2}"; NiSave();
    }

    private void Settings_ni_Delete_Click(object sender, RoutedEventArgs e)
    {
        if (!isLoaded) { return; }
        try
        {
            NiLoad(); niicons.Elements.RemoveAt(config.ThemeType); NiSave();
            ConfigLoad();
            config.ThemeType = -1;
            ConfigSave();
            NiIcon_LoadValues();
        }
        catch (Exception ex)
        {
            SendSMUCommand.TraceIt_TraceError(ex.ToString());
        }
    }

    private void Settings_ni_ResetDef_Click(object sender, RoutedEventArgs e)
    {
        if (!isLoaded) { return; }
        NiLoad(); 
        niicons.Elements[config.NiIconsType].IsEnabled = true;
        niicons.Elements[config.NiIconsType].ContextMenuType = 1;
        niicons.Elements[config.NiIconsType].Color = "FF6ACF";
        niicons.Elements[config.NiIconsType].IconShape = 0;
        niicons.Elements[config.NiIconsType].FontSize = 9;
        niicons.Elements[config.NiIconsType].BgOpacity = 0.5d;
        NiSave(); NiIconComboboxElements_SelectionChanged(NiIconComboboxElements, SelectionChangedEventArgs.FromAbi(0));
    }

    private void NiIconShapeCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!isLoaded) { return; }
        NiLoad(); niicons.Elements[config.NiIconsType].IconShape = NiIconShapeCombobox.SelectedIndex; NiSave();
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

                rtssset.RTSS_Elements[1].UseCompact = toggleButton.IsChecked == true;
                rtssset.RTSS_Elements[2].UseCompact = toggleButton.IsChecked == true;
                rtssset.RTSS_Elements[3].UseCompact = toggleButton.IsChecked == true;
                rtssset.RTSS_Elements[4].UseCompact = toggleButton.IsChecked == true;
                rtssset.RTSS_Elements[5].UseCompact = toggleButton.IsChecked == true;
                rtssset.RTSS_Elements[6].UseCompact = toggleButton.IsChecked == true;
                rtssset.RTSS_Elements[7].UseCompact = toggleButton.IsChecked == true;
                rtssset.RTSS_Elements[8].UseCompact = toggleButton.IsChecked == true;
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
        GenerateAdvancedCodeEditor();
        RtssSave();
    }
    private void GenerateAdvancedCodeEditor()
    {
        // Шаг 1: Создание ColorLib
        var ColorLib = new List<string>
        {
            "FFFFFF" // Добавляем белый цвет по умолчанию
        };

        void AddColorIfUnique(string color)
        {
            if (!ColorLib.Contains(color.Replace("#", "")))
            {
                ColorLib.Add(color.Replace("#",""));
            }
        }

        AddColorIfUnique(rtssset.RTSS_Elements[0].Color);
        AddColorIfUnique(rtssset.RTSS_Elements[1].Color);
        AddColorIfUnique(rtssset.RTSS_Elements[2].Color);
        AddColorIfUnique(rtssset.RTSS_Elements[3].Color);
        AddColorIfUnique(rtssset.RTSS_Elements[4].Color);
        AddColorIfUnique(rtssset.RTSS_Elements[5].Color);
        AddColorIfUnique(rtssset.RTSS_Elements[6].Color);
        AddColorIfUnique(rtssset.RTSS_Elements[7].Color);
        AddColorIfUnique(rtssset.RTSS_Elements[8].Color);

        // Шаг 2: Создание CompactLib
        var CompactLib = new bool[9];
        CompactLib[0] = rtssset.RTSS_Elements[0].UseCompact;
        CompactLib[1] = rtssset.RTSS_Elements[1].UseCompact;
        CompactLib[2] = rtssset.RTSS_Elements[1].UseCompact && rtssset.RTSS_Elements[1].Enabled ? rtssset.RTSS_Elements[1].UseCompact : rtssset.RTSS_Elements[2].UseCompact;
        CompactLib[3] = rtssset.RTSS_Elements[1].UseCompact && rtssset.RTSS_Elements[1].Enabled ? rtssset.RTSS_Elements[1].UseCompact : rtssset.RTSS_Elements[3].UseCompact;
        CompactLib[4] = rtssset.RTSS_Elements[1].UseCompact && rtssset.RTSS_Elements[1].Enabled ? rtssset.RTSS_Elements[1].UseCompact : rtssset.RTSS_Elements[4].UseCompact;
        CompactLib[5] = rtssset.RTSS_Elements[1].UseCompact && rtssset.RTSS_Elements[1].Enabled ? rtssset.RTSS_Elements[1].UseCompact : rtssset.RTSS_Elements[5].UseCompact;
        CompactLib[6] = rtssset.RTSS_Elements[1].UseCompact && rtssset.RTSS_Elements[1].Enabled ? rtssset.RTSS_Elements[1].UseCompact : rtssset.RTSS_Elements[6].UseCompact;
        CompactLib[7] = rtssset.RTSS_Elements[1].UseCompact && rtssset.RTSS_Elements[1].Enabled ? rtssset.RTSS_Elements[1].UseCompact : rtssset.RTSS_Elements[7].UseCompact;
        CompactLib[8] = rtssset.RTSS_Elements[1].UseCompact && rtssset.RTSS_Elements[1].Enabled ? rtssset.RTSS_Elements[1].UseCompact : rtssset.RTSS_Elements[8].UseCompact;

        // Шаг 3: Создание EnableLib
        var EnableLib = new bool[9];
        EnableLib[0] = rtssset.RTSS_Elements[0].Enabled;
        EnableLib[1] = rtssset.RTSS_Elements[1].Enabled;
        EnableLib[2] = rtssset.RTSS_Elements[2].Enabled;
        EnableLib[3] = rtssset.RTSS_Elements[3].Enabled;
        EnableLib[4] = rtssset.RTSS_Elements[4].Enabled;
        EnableLib[5] = rtssset.RTSS_Elements[5].Enabled;
        EnableLib[6] = rtssset.RTSS_Elements[6].Enabled;
        EnableLib[7] = rtssset.RTSS_Elements[7].Enabled;
        EnableLib[8] = rtssset.RTSS_Elements[8].Enabled;

        // Шаг 4: Создание TextLib
        var TextLib = new string[7];
        TextLib[0] = rtssset.RTSS_Elements[2].Name.TrimEnd();  // Saku Overclock Profile
        TextLib[1] = rtssset.RTSS_Elements[3].Name.TrimEnd();  // STAPM Fast Slow
        TextLib[2] = rtssset.RTSS_Elements[4].Name.TrimEnd();  // EDC Therm CPU Usage
        TextLib[3] = rtssset.RTSS_Elements[5].Name.TrimEnd();  // CPU Clocks
        TextLib[4] = rtssset.RTSS_Elements[6].Name.TrimEnd();  // AVG Clock Volt
        TextLib[5] = rtssset.RTSS_Elements[7].Name.TrimEnd();  // APU Clock Volt Temp
        TextLib[6] = rtssset.RTSS_Elements[8].Name.TrimEnd();  // Frame Rate

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
        for (var i = 0; i < ColorLib.Count; i++)
        {
            advancedCodeEditor.Append($"<C{i}={ColorLib[i]}>");
        }
        advancedCodeEditor.Append("<S1=70><S2=-50>\n");

        // 5.2 Вторая строка (Saku Overclock)
        //Пример второй строки:
        // "<C0>Saku Overclock <C1>" + ViewModels.ГлавнаяViewModel.GetVersion() + ": <S0>$SelectedProfile$\n" +
        if (EnableLib[2])
        {
            var colorIndexMain = rtssset.RTSS_Elements[0].Enabled ? ColorLib.IndexOf(rtssset.RTSS_Elements[0].Color.Replace("#", "")).ToString() : ColorLib.IndexOf(rtssset.RTSS_Elements[2].Color.Replace("#", "")).ToString();
            var colorIndexSecond = rtssset.RTSS_Elements[1].Enabled ? ColorLib.IndexOf(rtssset.RTSS_Elements[1].Color.Replace("#", "")).ToString() : ColorLib.IndexOf(rtssset.RTSS_Elements[2].Color.Replace("#", "")).ToString();
            var compactMain = rtssset.RTSS_Elements[0].Enabled ? (CompactLib[0] ? "<S1>" : "<S0>") : (CompactLib[2] ? "<S1>" : "<S0>");
            var compactSecond = rtssset.RTSS_Elements[1].Enabled ? (CompactLib[1] ? "<S2>" : "<S0>") : (CompactLib[2] ? "<S2>" : "<S0>");
            advancedCodeEditor.Append($"<C{colorIndexMain}>{compactMain}{TextLib[0]} {ГлавнаяViewModel.GetVersion()}: <C{colorIndexSecond}>{compactSecond}<S0>$SelectedProfile$\n");
        }

        // 5.3 Третья строка (STAPM Fast Slow)
        //Пример третьей строки:
        // "<S1><C2>STAPM, Fast, Slow: <C3><S0>$stapm_value$<S2>W<S1>$stapm_limit$W <S0>$fast_value$<S2>W<S1>$fast_limit$W <S0>$slow_value$<S2>W<S1>$slow_limit$W\n" +
        if (EnableLib[3])
        {
            var colorIndexMain = rtssset.RTSS_Elements[0].Enabled ? ColorLib.IndexOf(rtssset.RTSS_Elements[0].Color.Replace("#", "")).ToString() : ColorLib.IndexOf(rtssset.RTSS_Elements[3].Color.Replace("#", "")).ToString();
            var colorIndexSecond = rtssset.RTSS_Elements[1].Enabled ? ColorLib.IndexOf(rtssset.RTSS_Elements[1].Color.Replace("#", "")).ToString() : ColorLib.IndexOf(rtssset.RTSS_Elements[3].Color.Replace("#", "")).ToString();
            var compactMain = rtssset.RTSS_Elements[0].Enabled ? (CompactLib[0] ? "<S1>" : "<S0>") : (CompactLib[3] ? "<S1>" : "<S0>");
            var compactSecond = rtssset.RTSS_Elements[1].Enabled ? (CompactLib[1] ? "<S2>" : "<S0>") : (CompactLib[3] ? "<S2>" : "<S0>");
            var compactSign = rtssset.RTSS_Elements[1].Enabled ? (CompactLib[1] ? "" : "/") : (CompactLib[3] ? "" : "/");
            advancedCodeEditor.Append($"<C{colorIndexMain}>{compactMain}{TextLib[1]}: <C{colorIndexSecond}><S0>$stapm_value${compactSecond}W{compactSign}{compactSecond.Replace("2","1")}$stapm_limit$W <S0>$fast_value${compactSecond}W{compactSign}{compactSecond.Replace("2", "1")}$fast_limit$W <S0>$slow_value${compactSecond}W{compactSign}{compactSecond.Replace("2", "1")}$slow_limit$W\n");
        }
        // - Для EDC Therm CPU Usage
        //Пример четвёртой строки:
        // "<C2>EDC, Therm, CPU Usage: <C3><S0>$vrmedc_value$<S2>A<S1>$vrmedc_max$A <C3><S0>$cpu_temp_value$<S2>C<S1>$cpu_temp_max$C<C3><S0> $cpu_usage$<S2>%<S1>\n" +
        if (EnableLib[4])
        {
            var colorIndexMain = rtssset.RTSS_Elements[0].Enabled ? ColorLib.IndexOf(rtssset.RTSS_Elements[0].Color.Replace("#", "")).ToString() : ColorLib.IndexOf(rtssset.RTSS_Elements[4].Color.Replace("#", "")).ToString();
            var colorIndexSecond = rtssset.RTSS_Elements[1].Enabled ? ColorLib.IndexOf(rtssset.RTSS_Elements[1].Color.Replace("#", "")).ToString() : ColorLib.IndexOf(rtssset.RTSS_Elements[4].Color.Replace("#", "")).ToString();
            var compactMain = rtssset.RTSS_Elements[0].Enabled ? (CompactLib[0] ? "<S1>" : "<S0>") : (CompactLib[4] ? "<S1>" : "<S0>");
            var compactSecond = rtssset.RTSS_Elements[1].Enabled ? (CompactLib[1] ? "<S2>" : "<S0>") : (CompactLib[4] ? "<S2>" : "<S0>");
            var compactSign = rtssset.RTSS_Elements[1].Enabled ? (CompactLib[1] ? "" : "/") : (CompactLib[4] ? "" : "/");
            advancedCodeEditor.Append($"<C{colorIndexMain}>{compactMain}{TextLib[2]}: <C{colorIndexSecond}><S0>$vrmedc_value${compactSecond}A{compactSign}{compactSecond.Replace("2", "1")}$vrmedc_max$A <S0>$cpu_temp_value${compactSecond}C{compactSign}{compactSecond.Replace("2", "1")}$cpu_temp_max$C <S0>$cpu_usage${compactSecond}%\n");
        }
        // - Для CPU Clocks
        //Пример пятой строки:
        // "<S1><C2>Clocks: $cpu_clock_cycle$<S1><C2>$currCore$:<S0><C3> $cpu_core_clock$<S2>GHz<S1>$cpu_core_voltage$V $cpu_clock_cycle_end$\n" +
        if (EnableLib[5])
        {
            var colorIndexMain = rtssset.RTSS_Elements[0].Enabled ? ColorLib.IndexOf(rtssset.RTSS_Elements[0].Color.Replace("#", "")).ToString() : ColorLib.IndexOf(rtssset.RTSS_Elements[5].Color.Replace("#", "")).ToString();
            var colorIndexSecond = rtssset.RTSS_Elements[1].Enabled ? ColorLib.IndexOf(rtssset.RTSS_Elements[1].Color.Replace("#", "")).ToString() : ColorLib.IndexOf(rtssset.RTSS_Elements[5].Color.Replace("#", "")).ToString();
            var compactMain = rtssset.RTSS_Elements[0].Enabled ? (CompactLib[0] ? "<S1>" : "<S0>") : (CompactLib[5] ? "<S1>" : "<S0>");
            var compactSecond = rtssset.RTSS_Elements[1].Enabled ? (CompactLib[1] ? "<S2>" : "<S0>") : (CompactLib[5] ? "<S2>" : "<S0>");
            var compactSign = rtssset.RTSS_Elements[1].Enabled ? (CompactLib[1] ? "" : "/") : (CompactLib[5] ? "" : "/");
            advancedCodeEditor.Append($"<C{colorIndexMain}>{compactMain}{TextLib[3]}: $cpu_clock_cycle$<C{colorIndexMain}>$currCore$: <C{colorIndexSecond}>$cpu_core_clock${compactSecond}GHz{compactSign}{compactSecond.Replace("2", "1")}$cpu_core_voltage$V $cpu_clock_cycle_end$\n");
        }
        // - Для AVG Clock Volt
        //Пример шестой строки:
        // "<C2>AVG Clock, Volt: <C3><S0>$average_cpu_clock$<S2>GHz<S1>$average_cpu_voltage$V" +
        if (EnableLib[6])
        {
            var colorIndexMain = rtssset.RTSS_Elements[0].Enabled ? ColorLib.IndexOf(rtssset.RTSS_Elements[0].Color.Replace("#", "")).ToString() : ColorLib.IndexOf(rtssset.RTSS_Elements[6].Color.Replace("#", "")).ToString();
            var colorIndexSecond = rtssset.RTSS_Elements[1].Enabled ? ColorLib.IndexOf(rtssset.RTSS_Elements[1].Color.Replace("#", "")).ToString() : ColorLib.IndexOf(rtssset.RTSS_Elements[6].Color.Replace("#", "")).ToString();
            var compactMain = rtssset.RTSS_Elements[0].Enabled ? (CompactLib[0] ? "<S1>" : "<S0>") : (CompactLib[6] ? "<S1>" : "<S0>");
            var compactSecond = rtssset.RTSS_Elements[1].Enabled ? (CompactLib[1] ? "<S2>" : "<S0>") : (CompactLib[6] ? "<S2>" : "<S0>");
            var compactSign = rtssset.RTSS_Elements[1].Enabled ? (CompactLib[1] ? "" : "/") : (CompactLib[6] ? "" : "/");
            advancedCodeEditor.Append($"<C{colorIndexMain}>{compactMain}{TextLib[4]}: <C{colorIndexSecond}><S0>$average_cpu_clock${compactSecond}GHz{compactSign}{compactSecond.Replace("2", "1")}$average_cpu_voltage$V\n");
        }
        // - Для APU Clock Volt Temp
        //Пример седьмой строки:
        // "<C2>APU Clock, Volt, Temp: <C3><S0>$gfx_clock$<S2>MHz<S1>$gfx_volt$V <S0>$gfx_temp$<S1>C\n" +
        if (EnableLib[7])
        {
            var colorIndexMain = rtssset.RTSS_Elements[0].Enabled ? ColorLib.IndexOf(rtssset.RTSS_Elements[0].Color.Replace("#", "")).ToString() : ColorLib.IndexOf(rtssset.RTSS_Elements[7].Color.Replace("#", "")).ToString();
            var colorIndexSecond = rtssset.RTSS_Elements[1].Enabled ? ColorLib.IndexOf(rtssset.RTSS_Elements[1].Color.Replace("#", "")).ToString() : ColorLib.IndexOf(rtssset.RTSS_Elements[7].Color.Replace("#", "")).ToString();
            var compactMain = rtssset.RTSS_Elements[0].Enabled ? (CompactLib[0] ? "<S1>" : "<S0>") : (CompactLib[7] ? "<S1>" : "<S0>");
            var compactSecond = rtssset.RTSS_Elements[1].Enabled ? (CompactLib[1] ? "<S2>" : "<S0>") : (CompactLib[7] ? "<S2>" : "<S0>");
            var compactSign = rtssset.RTSS_Elements[1].Enabled ? (CompactLib[1] ? "" : "/") : (CompactLib[7] ? "" : "/");
            advancedCodeEditor.Append($"<C{colorIndexMain}>{compactMain}{TextLib[5]}: <C{colorIndexSecond}><S0>$gfx_clock${compactSecond}MHz{compactSign}{compactSecond.Replace("2", "1")}$gfx_volt$V <S0>$gfx_temp${compactSecond}C\n");
        }
        // - Для Frame Rate
        //Пример восьмой строки:
        // "<C2>Framerate <C3><S0>%FRAMERATE% %FRAMETIME%";*/
        if (EnableLib[8])
        {
            var colorIndexMain = rtssset.RTSS_Elements[0].Enabled ? ColorLib.IndexOf(rtssset.RTSS_Elements[0].Color.Replace("#", "")).ToString() : ColorLib.IndexOf(rtssset.RTSS_Elements[8].Color.Replace("#", "")).ToString();
            var colorIndexSecond = rtssset.RTSS_Elements[1].Enabled ? ColorLib.IndexOf(rtssset.RTSS_Elements[1].Color.Replace("#", "")).ToString() : ColorLib.IndexOf(rtssset.RTSS_Elements[8].Color.Replace("#", "")).ToString();
            var compactMain = rtssset.RTSS_Elements[0].Enabled ? (CompactLib[0] ? "<S1>" : "<S0>") : (CompactLib[8] ? "<S1>" : "<S0>");
            advancedCodeEditor.Append($"<C{colorIndexMain}>{compactMain}{TextLib[6]}: <C{colorIndexSecond}><S0>%FRAMERATE% %FRAMETIME%");
        }
        // Финальная строка присваивается в rtssset.AdvancedCodeEditor
        rtssset.AdvancedCodeEditor = advancedCodeEditor.ToString();
        LoadAndFormatAdvancedCodeEditor(rtssset.AdvancedCodeEditor);
        RTSSHandler.ChangeOSDText(rtssset.AdvancedCodeEditor);
        RtssSave();
    }


    private void Settings_RTSS_Enable_Toggled(object sender, RoutedEventArgs e)
    {
        if (!isLoaded) { return; }
        ConfigLoad(); config.RTSSMetricsEnabled = Settings_RTSS_Enable.IsOn; ConfigSave();
        Settings_RTSS_Enable_Name.Visibility = Settings_RTSS_Enable.IsOn ? Visibility.Visible : Visibility.Collapsed;
        RTTS_GridView.Visibility = Settings_RTSS_Enable.IsOn ? Visibility.Visible : Visibility.Collapsed;
        RTSS_AdvancedCodeEditor_ToggleSwitch.Visibility = Settings_RTSS_Enable.IsOn ? Visibility.Visible : Visibility.Collapsed;
        RTSS_AdvancedCodeEditor_EditBox_Scroll.Visibility = Settings_RTSS_Enable.IsOn ? Visibility.Visible : Visibility.Collapsed;
    }
    private void RTSS_AdvancedCodeEditor_ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        RTSS_AdvancedCodeEditor_EditBox.Visibility = RTSS_AdvancedCodeEditor_ToggleSwitch.IsOn ? Visibility.Visible : Visibility.Collapsed;
        if (!isLoaded) { return; }
        RtssLoad(); rtssset.IsAdvancedCodeEditorEnabled = RTSS_AdvancedCodeEditor_ToggleSwitch.IsOn; RtssSave();
    }

    private void RTSS_AdvancedCodeEditor_EditBox_TextChanged(object sender, RoutedEventArgs e)
    {
        string? newString;
        RTSS_AdvancedCodeEditor_EditBox.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out newString);
        rtssset.AdvancedCodeEditor = newString.Replace("\r", "\n");
        RtssSave();
    }
    #endregion

    private void Settings_Keybinds_Enable_Toggled(object sender, RoutedEventArgs e)
    {

    }
}