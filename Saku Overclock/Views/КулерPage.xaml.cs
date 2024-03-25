using System.Diagnostics;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.ViewModels;
using Windows.Foundation.Metadata;
#pragma warning disable IDE0059 // Ненужное присваивание значения
#pragma warning disable CS8601 // Возможно, назначение-ссылка, допускающее значение NULL.
namespace Saku_Overclock.Views;
public sealed partial class КулерPage : Page
{
    private Config config = new();
    private bool isPageLoaded = false;
    public КулерViewModel ViewModel
    {
        get;
    }
    private System.Windows.Threading.DispatcherTimer tempUpdateTimer;
    private System.Windows.Threading.DispatcherTimer fanUpdateTimer;
    public КулерPage()
    {
        ViewModel = App.GetService<КулерViewModel>();
        InitializeComponent();
        ConfigLoad();
        FanInit();
        Update();
        config.tempex = true;
        ConfigSave();
        Loaded += Page_Loaded;
        fanUpdateTimer = new System.Windows.Threading.DispatcherTimer();
        fanUpdateTimer.Tick += async (sender, e) => await CheckFan();
        fanUpdateTimer.Interval = TimeSpan.FromMilliseconds(6000);
    }
    //Проверка на загрузку страницы
    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        // Логика загрузки страницы

        isPageLoaded = true;
    }
    //Проверка температуры

    private void StartTempUpdate()
    {
        tempUpdateTimer = new System.Windows.Threading.DispatcherTimer();
        tempUpdateTimer.Tick += async (sender, e) => await UpdateTemperatureAsync();
        tempUpdateTimer.Interval = TimeSpan.FromMilliseconds(500);
        // Подписка на событие потери фокуса
        App.MainWindow.Activated += Window_Activated;

        // Подписка на событие изменения видимости
        App.MainWindow.VisibilityChanged += Window_VisibilityChanged;
        tempUpdateTimer.Start();
    }
    private void Window_Activated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.CodeActivated || args.WindowActivationState == WindowActivationState.PointerActivated)
        {
            // Окно активировано
            tempUpdateTimer?.Start();
            if (Fanauto.IsChecked == true)
            fanUpdateTimer?.Start();
        }
        else
        {
            // Окно не активировано
            tempUpdateTimer?.Stop();
            fanUpdateTimer?.Stop();
        }

    }

    private void Window_VisibilityChanged(object sender, WindowVisibilityChangedEventArgs args)
    {
        if (args.Visible)
        {
            // Окно видимо
            tempUpdateTimer?.Start();
            if (Fanauto.IsChecked == true)
                fanUpdateTimer?.Start();
        }
        else
        {
            // Окно не видимо
            tempUpdateTimer?.Stop();
            fanUpdateTimer?.Stop();
        }
    }
    private async Task UpdateTemperatureAsync()
    {
        if (config.tempex)
        {
            var p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.FileName = @"ryzenadj.exe";
            p.StartInfo.Arguments = "-i";
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;
            try
            {
                p.Start();
            }
            catch
            { 

            }
            var outputWriter = p.StandardOutput;
            var line = await outputWriter.ReadLineAsync();
            while (line != null)
            {
                if (!string.IsNullOrWhiteSpace(line) && line.Contains("THM VALUE CORE"))
                {
                    Temp.Text = line.Replace("THM VALUE CORE", "").Replace(" ", "").Replace("|", "") + "℃";
                }

                line = await outputWriter.ReadLineAsync();
            }

            p.WaitForExit();
            line = null;
        }
    }
    // Метод, который будет вызываться при скрытии/переключении страницы
    private void StopTempUpdate()
    {
        tempUpdateTimer?.Stop();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        StartTempUpdate();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        StopTempUpdate();
    }
    //Конец проверки температуры
    //JSON форматирование
    public void ConfigSave()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json", JsonConvert.SerializeObject(config));
        }
        catch
        {

        }
    }
    public void ConfigLoad()
    {
        try
        {
            config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json"));
        }
        catch
        {
            App.MainWindow.ShowMessageDialogAsync("Пресеты 3", "Критическая ошибка!");
        }
    }

    public async void FanInit()
    {
        ConfigLoad();
        // Получить папку, в которой хранятся файлы XML
        var folderPath = @"C:\Program Files (x86)\NoteBook FanControl\Configs";

        // Получить все XML-файлы в этой папке
        var xmlFiles = Directory.GetFiles(folderPath, "*.xml");

        // Очистить ComboBox
        Selfan.Items.Clear();

        foreach (var xmlFile in xmlFiles)
        {
            // Получить имя файла без расширения
            var fileName = Path.GetFileNameWithoutExtension(xmlFile);

            // Проверить, содержится ли .xml в имени файла
            if (fileName.Contains(".xml"))
            {
                fileName = fileName.Replace(".xml", "");
            }

            // Создать новый ComboBoxItem
            var item = new ComboBoxItem
            {
                Content = fileName,
                Tag = xmlFile // Сохранить полный путь к файлу в Tag
            };

            // Добавить ComboBoxItem в ComboBox
            Selfan.Items.Add(item);

            // Проверить, соответствует ли fanValue значению в файле
            if (config.fanvalue == fileName)
            {
                // Установить выбранный элемент в ComboBox
                Selfan.SelectedItem = item;
            }
        }
        if (config.fanenabled == true) { ConfigLoad(); Fan1.Value = config.fan1; Fan2.Value = config.fan2; Enabl.IsChecked = true; Readon.IsChecked = false; Disabl.IsChecked = false; Fan1Val.Text = Fan1.Value.ToString() + " %"; Fan2Val.Text = Fan2.Value.ToString() + " %"; if (Fan1.Value > 100) { Fan1Val.Text = "Auto"; }; if (Fan2.Value > 100) { Fan2Val.Text = "Auto"; }; };
        if (config.fanread == true) { Enabl.IsChecked = false; Readon.IsChecked = true; Disabl.IsChecked = false; };
        if (config.fandisabled == true) { Enabl.IsChecked = false; Readon.IsChecked = false; Disabl.IsChecked = true; };
        if (config.autofan == true) { await Task.Delay(20); Fanauto.IsChecked = true; }
        //better fan init
        if (Enabl.IsChecked == true)
        {
            NbfcFan1();
            if (Fan1.Value > 100)
            {
                Fan1Val.Text = "Auto";
                Update();
                config.fan1 = 110.0;
                ConfigSave();
            }
            else
            {
                Fan1Pr.Value = Fan1.Value;
                Fan1Cur.Text = "Cooler_Current_Fan_Val".GetLocalized() + "   " + Fan1.Value;
                config.fanex = false;
                ConfigSave();
            }
            ConfigSave();
        }
        if (Readon.IsChecked == true)
        {
            Fan1Val.Text = "Auto";
            Update();
        }
    }
    private void Disabl_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        config.fandisabled = true; config.fanread = false; config.fanenabled = false; config.fanex = false;
        ConfigSave();
        NbfcEnable();
    }

    private void Readon_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        config.fanread = true; config.fanenabled = false; config.fandisabled = false;
        ConfigSave();
        NbfcEnable();
        Fan1Val.Text = "Auto";
        Update();
    }

    private void Enabl_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        config.fanenabled = true; config.fandisabled = false; config.fanread = false;
        ConfigSave();
        NbfcEnable();
    }

    private async void Fan1_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (Enabl.IsChecked == true)
        {
            NbfcFan1();
            await Task.Delay(200);
            config.fan1 = Fan1.Value;
            Fan1Val.Text = Fan1.Value.ToString() + " %";
            if (Fan1.Value > 100)
            {
                Fan1Val.Text = "Auto";
                Update();
                config.fan1 = 110.0;
                ConfigSave();
            }
            else
            {
                Fan1Pr.Value = Fan1.Value;
                Fan1Cur.Text = "Cooler_Current_Fan_Val".GetLocalized() + "   " + Fan1.Value;
                config.fanex = false;
                ConfigSave();
            }
            ConfigSave();
        }
        if (Readon.IsChecked == true)
        {
            Fan1Val.Text = "Auto";
            Update();
        }
    }

    private async void Fan2_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (Enabl.IsChecked == true)
        {
            NbfcFan2();
            await Task.Delay(200);
            config.fan2 = Fan2.Value;
            Fan2Val.Text = Fan2.Value.ToString() + " %";
            if (Fan2.Value > 100)
            {
                Fan2Val.Text = "Auto";
                Update();
                config.fan2 = 110.0;
                ConfigSave();
                if (Fan1Pr.Value == 10) { Fan1Pr.Value = 100; }
            }
            else
            {
                Fan2Pr.Value = Fan2.Value;
                Fan2Cur.Text = "Cooler_Current_Fan_Val".GetLocalized() + "   " + Fan2.Value;
                config.fanex = false;
                ConfigSave();
            }
            ConfigSave();
        }
        if (Readon.IsChecked == true)
        {
            Fan2Val.Text = "Auto";
            Update();
        }
    }

    public void NbfcEnable()
    {
        var p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = @"nbfc/nbfc.exe";
        ConfigLoad();
        if (config.fandisabled == true)
        {
            p.StartInfo.Arguments = " stop";
        }
        if (config.fanenabled == true)
        {
            p.StartInfo.Arguments = " start --enabled";
        }
        if (config.fanread == true)
        {
            p.StartInfo.Arguments = " start --readonly";
        }
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;

        p.Start();
        //App.MainWindow.ShowMessageDialogAsync("Вы успешно выставили свои настройки! \n" + mc.config.adjline, "Применение успешно!");
    }

    public void NbfcFan1()
    {
        var p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = @"nbfc/nbfc.exe";
        ConfigLoad();
        if (config.fanenabled == true)
        {
            if (Fan1.Value < 100) { p.StartInfo.Arguments = " set --fan 0 --speed " + Fan1.Value; }
            else { p.StartInfo.Arguments = " set --fan 0 --auto"; }
        }
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;

        p.Start();
    }
    public void NbfcFan2()
    {
        var p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = @"nbfc/nbfc.exe";
        ConfigLoad();
        if (config.fanenabled == true)
        {
            if (Fan2.Value < 100) { p.StartInfo.Arguments = " set --fan 1 --speed " + Fan2.Value; }
            else { p.StartInfo.Arguments = " set --fan 1 --auto"; }
        }
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;

        p.Start();
    }
    public void NbfcFanState()
    {
        const string quote = "\"";

        var p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = @"nbfc/nbfc.exe";
        ConfigLoad();
        p.StartInfo.Arguments = " config --apply " + quote + config.fanvalue + quote;
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;

        p.Start();
    }

    private async void Selfan_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Проверка флага, разрешено ли выполнение метода
        if (!isPageLoaded)
        {
            return; // Прерывание выполнения метода
        }
        await Task.Delay(200);
        config.fanvalue = (string)((ComboBoxItem)Selfan.SelectedItem).Content;
        ConfigSave();
        //Применить!
        NbfcFanState();

    }
    //set --fan 0 --speed 100
    //Информация о текущей скорости вращения кулеров
    public void GetInfo0(bool start)
    {
        config.fanex = true;
        ConfigSave();
        if (start) { fanUpdateTimer?.Start(); } else { fanUpdateTimer?.Stop(); }
    }
    public async Task CheckFan()
    {
        if (Readon.IsChecked == true || Enabl.IsChecked == true)
        {
            if (config.fanex == true)
            {
                config.fan1v = "";
                config.fan2v = "";
                var p = new Process();
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.FileName = @"nbfc/nbfc.exe";
                p.StartInfo.Arguments = " status --fan 0";
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                try { p.Start(); } catch (Exception ex) { await App.MainWindow.ShowMessageDialogAsync("Error " + ex.Message + " of КулерPage.xaml.cs in com.sakuoverclock.org", "Critical error!"); }
                
                var outputWriter = p.StandardOutput;
                var line = await outputWriter.ReadLineAsync();
                while (line != null)
                {

                    if (line.Contains("Current fan speed"))
                    {
                        config.fan1v = line.Replace("Current fan speed", "").Replace(" ", "").Replace(":", "").Replace("\t", "");
                        ConfigSave();
                    }

                    line = await outputWriter.ReadLineAsync();
                }
                line = null;
                p.WaitForExit();
                //fan 2
                var p1 = new Process();
                p1.StartInfo.UseShellExecute = false;
                p1.StartInfo.FileName = @"nbfc/nbfc.exe";
                p1.StartInfo.Arguments = " status --fan 1";
                p1.StartInfo.CreateNoWindow = true;
                p1.StartInfo.RedirectStandardError = true;
                p1.StartInfo.RedirectStandardInput = true;
                p1.StartInfo.RedirectStandardOutput = true;
                p1.Start();
                var outputWriter1 = p1.StandardOutput;
                var line1 = await outputWriter1.ReadLineAsync();
                while (line1 != null)
                {

                    if (line1.Contains("Current fan speed"))
                    {
                        config.fan2v = line1.Replace("Current fan speed", "").Replace(" ", "").Replace(":", "").Replace("\t", "");
                        ConfigSave();
                    }

                    line1 = await outputWriter1.ReadLineAsync();
                }
                line1 = null;
                p1.WaitForExit();
                Update();
            }
        }

    }
    private void Update()
    {
        ConfigLoad();
        if (config.fan1v == null) { return; }
        try
        {
            Fan1Pr.Value = Convert.ToInt32(double.Parse(config.fan1v, CultureInfo.InvariantCulture));
            if (Fan1Pr.Value > 100) { Fan1Pr.Value = 100; }
            Fan1Cur.Text = "Cooler_Current_Fan_Val".GetLocalized() + "   " + config.fan1v + "%";
        }
        catch
        {
            if (Fan1.Value < 100)
            {
                Fan1Pr.Value = Fan1.Value;
                Fan1Cur.Text = "Cooler_Current_Fan_Val".GetLocalized() + "   " + Fan1.Value + "%";
            }
        }
        if (config.fan2v == null) { return; }
        try
        {
            Fan2Pr.Value = Convert.ToInt32(double.Parse(config.fan2v, CultureInfo.InvariantCulture));
            if (Fan2Pr.Value > 100) { Fan2Pr.Value = 100; }
            Fan2Cur.Text = "Cooler_Current_Fan_Val".GetLocalized() + "   " + config.fan2v + "%";
        }
        catch
        {
            if (Fan2.Value < 100)
            {
                Fan2Pr.Value = Fan2.Value;
                Fan2Cur.Text = "Cooler_Current_Fan_Val".GetLocalized() + "   " + Fan2.Value + "%";
            }
        }
    }

    private async void Fanauto_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (Fanauto.IsChecked == true)
        {
            if (config.autofan == false)
            {
                var AutoDialog = new ContentDialog
                {
                    Title = "Cooler_FanAuto_Text".GetLocalized(),
                    Content = "Cooler_FanAuto_Desc".GetLocalized(),
                    CloseButtonText = "Cancel".GetLocalized(),
                    PrimaryButtonText = "Enable".GetLocalized(),
                    DefaultButton = ContentDialogButton.Close
                };

                // Use this code to associate the dialog to the appropriate AppWindow by setting
                // the dialog's XamlRoot to the same XamlRoot as an element that is already present in the AppWindow.
                if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
                {
                    AutoDialog.XamlRoot = XamlRoot;
                }
                var result = await AutoDialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    config.autofan = true; ConfigSave();
                }
                else { Fanauto.IsChecked = false; config.autofan = false; ConfigSave(); }
            }
            config.fanex = false; ConfigSave();
            GetInfo0(true);
        }
        else { config.autofan = false; ConfigSave(); GetInfo0(false); }
    }

    private async void Update_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        config.fanex = true; ConfigSave();
        await CheckFan();
        Fanauto.IsChecked = false;
    }

    private void AdvancedCooler_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(AdvancedКулерViewModel).FullName!);
    }

    private async void Suggest_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await SuggestClickAsync();
    }
    private async Task SuggestClickAsync()
    {
        SuggestTip.Subtitle = "";
        var p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = @"nbfc/nbfc.exe";
        p.StartInfo.Arguments = " config -r";
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;
        try
        {
            p.Start();
        }
        catch (Exception ex)
        {
            await App.MainWindow.ShowMessageDialogAsync("Error " + ex.Message + " of КулерPage.xaml.cs in com.sakuoverclock.org", "Critical error!");
        }
        var outputWriter = p.StandardOutput;
        var line = await outputWriter.ReadLineAsync();
        while (line != null)
        {
            if (line != "")
            {
                SuggestTip.Subtitle += line + "\n";
            }
            line = await outputWriter.ReadLineAsync();
        }
        p.WaitForExit();
        SuggestTip.IsOpen = true;
    }

}
