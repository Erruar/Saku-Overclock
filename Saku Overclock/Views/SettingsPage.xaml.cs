using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using Microsoft.Windows.AppNotifications;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Notifications;
using Saku_Overclock.Services;
using Saku_Overclock.ViewModels;
namespace Saku_Overclock.Views;

// TODO: Set the URL for your privacy policy by updating SettingsPage_PrivacyTermsLink.NavigateUri in Resources.resw.
public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel
    {
        get;
    }


    private Config config = new Config();

    private Devices devices = new Devices();

    private Profile profile = new Profile();

    public SettingsPage()
    {
        ViewModel = App.GetService<SettingsViewModel>();
        InitializeComponent();
        ConfigLoad();
        DeviceLoad();
        ProfileLoad();
        InitVal();
    }
    private void InitVal()
    {
        if (config.autostart == true) { cbStartBoot.IsChecked = true; }
        if (config.traystart == true) { cbStartMini.IsChecked = true; }
        if (config.autooverclock == true) { cbApplyStart.IsChecked = true; }
        if (config.reapplytime == true) { cbAutoReapply.IsChecked = true; nudAutoReapply.Value = config.reapplytimer; }
        if (config.autoupdates == true) { cbAutoCheck.IsChecked = true; }
        
    }

    private void cbStartBoot_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (cbStartBoot.IsChecked == true)
        {
            config.autostart = true;
            ConfigSave();
            // Получите путь к исполняемому файлу программы
            string pathToExecutableFile = System.Reflection.Assembly.GetExecutingAssembly().Location;

            // Получите путь к папке с программой
            string pathToProgramDirectory = Path.GetDirectoryName(pathToExecutableFile);
            // Получите путь к файлу startup.lnk
            string pathToStartupLnk = Path.Combine(pathToProgramDirectory, "startup.lnk");

            // Получите путь к папке автозагрузки
            string pathToStartupFolder = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu) + "\\Programs\\Startup\\startup.lnk";

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
        if (cbStartMini.IsChecked == true) { config.traystart = true; ConfigSave(); }
        else { config.traystart = false; ConfigSave(); };
    }

    private void cbApplyStart_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (cbApplyStart.IsChecked == true) { config.autooverclock = true; ConfigSave(); }
        else { config.autooverclock = false; ConfigSave(); };
    }

    private void cbAutoReapply_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (cbAutoReapply.IsChecked == true) { config.reapplytime = true; config.reapplytimer = nudAutoReapply.Value; ConfigSave(); }
        else { config.reapplytime = false; config.reapplytimer = 3; ConfigSave(); };
    }

    private void cbAutoCheck_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (cbAutoCheck.IsChecked == true) { config.autoupdates = true; ConfigSave(); }
        else { config.autoupdates = false; ConfigSave(); };
    }

    internal void ShowAsync() => throw new NotImplementedException();

    //JSON форматирование
    public void ConfigSave()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json", JsonConvert.SerializeObject(config));
        }
        catch (Exception ex)
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

    public void DeviceSave()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\devices.json", JsonConvert.SerializeObject(devices));
        }
        catch (Exception ex)
        {

        }
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

    public void ProfileSave()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json", JsonConvert.SerializeObject(profile));
        }
        catch (Exception ex)
        {

        }
    }

    public void ProfileLoad()
    {
        try
        {
            profile = JsonConvert.DeserializeObject<Profile>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json"));
        }
        catch
        {
            App.MainWindow.ShowMessageDialogAsync("Пресеты 1", "Критическая ошибка!");
        }
    }

    private async void nudAutoReapply_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        await Task.Delay(20);
        config.reapplytime = true; config.reapplytimer = nudAutoReapply.Value; ConfigSave();
    }
}
