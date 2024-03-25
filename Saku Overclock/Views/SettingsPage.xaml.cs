using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using Saku_Overclock.Helpers;
using Saku_Overclock.ViewModels;
namespace Saku_Overclock.Views;
#pragma warning disable CS8604 // Возможно, аргумент-ссылка, допускающий значение NULL.
#pragma warning disable CS8601 // Возможно, назначение-ссылка, допускающее значение NULL.
public sealed partial class SettingsPage : Microsoft.UI.Xaml.Controls.Page
{
    public SettingsViewModel ViewModel
    {
        get;
    }
    private Config config = new();
    private Devices devices = new();
    private Profile profile = new();
    private bool relay = true; 

    public SettingsPage()
    {
        ViewModel = App.GetService<SettingsViewModel>();
        InitializeComponent();
        ConfigLoad();
        DeviceLoad(); 
        InitVal();
        config.fanex = false;
        config.tempex = false;
        ConfigSave();
    }
    private async void InitVal()
    {
        if (config.bluetheme == true) { Blue_sel.IsChecked = true; Dark_sel.IsChecked = false; Light_sel.IsChecked = false; Default_sel.IsChecked = false; }
        if (config.autostart == true) { CbStartBoot.IsChecked = true; }
        if (config.traystart == true) { CbStartMini.IsChecked = true; }
        if (config.autooverclock == true) { CbApplyStart.IsChecked = true; }
        if (config.reapplytime == true) { CbAutoReapply.IsChecked = true; nudAutoReapply.Value = config.reapplytimer; }
        if (config.autoupdates == true) { CbAutoCheck.IsChecked = true; }
        relay = false;
        await Task.Delay(390);
        if (Blue_sel.IsChecked == false && Dark_sel.IsChecked == false && Light_sel.IsChecked == false && Default_sel.IsChecked == false)
        {
            Blue_sel.IsChecked = true;
        }
    }
    private void cbStartBoot_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (CbStartBoot.IsChecked == true)
        {
            config.autostart = true;
            ConfigSave();
            // Получите путь к исполняемому файлу программы
            var pathToExecutableFile = System.Reflection.Assembly.GetExecutingAssembly().Location;

            // Получите путь к папке с программой
            var pathToProgramDirectory = Path.GetDirectoryName(pathToExecutableFile);
            // Получите путь к файлу startup.lnk
            var pathToStartupLnk = Path.Combine(pathToProgramDirectory, "startup.lnk");


            // Получите путь к папке автозагрузки
            var pathToStartupFolder = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu) + "\\Programs\\Startup\\startup.lnk";

            // Копируйте файл startup.lnk в папку автозагрузки
            File.Copy(pathToStartupLnk, pathToStartupFolder, true);
        }
        else
        {
            config.autostart = false;
            ConfigSave();
            File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu) + "\\Programs\\Startup", "startup.lnk"));
        }
    }
    private void cbStartMini_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (CbStartMini.IsChecked == true) { config.traystart = true; ConfigSave(); }
        else { config.traystart = false; ConfigSave(); };
    }
    private void cbApplyStart_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (CbApplyStart.IsChecked == true) { config.autooverclock = true; ConfigSave(); }
        else { config.autooverclock = false; ConfigSave(); };
    }
    private void cbAutoReapply_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (CbAutoReapply.IsChecked == true) { config.reapplytime = true; config.reapplytimer = nudAutoReapply.Value; ConfigSave(); }
        else { config.reapplytime = false; config.reapplytimer = 3; ConfigSave(); };
    }
    private void cbAutoCheck_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (CbAutoCheck.IsChecked == true) { config.autoupdates = true; ConfigSave(); }
        else { config.autoupdates = false; ConfigSave(); };
    }
    //JSON форматирование
    public void ConfigSave()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json", JsonConvert.SerializeObject(config));
        }
        catch { }
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
    public void DeviceSave()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\devices.json", JsonConvert.SerializeObject(devices));
        }
        catch { }
    }
    public void DeviceLoad()
    {
        try
        {
            devices = JsonConvert.DeserializeObject<Devices>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\devices.json"));
        }
        catch
        {
            App.MainWindow.ShowMessageDialogAsync("Пресеты 2", "Критическая ошибка!");
        }
    } 
    private async void nudAutoReapply_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        await Task.Delay(20);
        config.reapplytime = true; config.reapplytimer = nudAutoReapply.Value; ConfigSave();
    }

    private async void Blue_sel_Checked(object sender, RoutedEventArgs e)
    {
        await Task.Delay(230);
        config.bluetheme = true;
        ConfigSave();
        if (App.MainWindow.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = ElementTheme.Dark;
            TitleBarHelper.UpdateTitleBar(ElementTheme.Dark);
        }
        var micaBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
        micaBackdrop.Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt;
        App.MainWindow.SystemBackdrop = micaBackdrop;
        Dark_sel.IsChecked = false; Light_sel.IsChecked = false; Default_sel.IsChecked = false;
    }

    private void Default_sel_Checked(object sender, RoutedEventArgs e)
    {
        if (relay == false)
        {
            Blue_sel.IsChecked = false;
            config.bluetheme = false;
            ConfigSave();
            var micaBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
            micaBackdrop.Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base;
            App.MainWindow.SystemBackdrop = micaBackdrop;
        }
    }

    private void Dark_sel_Checked(object sender, RoutedEventArgs e)
    {
        if (relay == false)
        {
            Blue_sel.IsChecked = false;
            config.bluetheme = false;
            ConfigSave();
            var micaBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
            micaBackdrop.Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base;
            App.MainWindow.SystemBackdrop = micaBackdrop;
        }
    }

    private void Light_sel_Checked(object sender, RoutedEventArgs e)
    {
        if (relay == false)
        {
            Blue_sel.IsChecked = false;
            config.bluetheme = false;
            ConfigSave();
            var micaBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
            micaBackdrop.Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base;
            App.MainWindow.SystemBackdrop = micaBackdrop;
        }
    }
#pragma warning restore CS8604 // Возможно, аргумент-ссылка, допускающий значение NULL.
#pragma warning restore CS8601 // Возможно, назначение-ссылка, допускающее значение NULL.
}
