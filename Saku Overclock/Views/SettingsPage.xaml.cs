using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using Newtonsoft.Json;
using Saku_Overclock.Helpers;
using Saku_Overclock.ViewModels;
using Task = System.Threading.Tasks.Task;
namespace Saku_Overclock.Views; 

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel
    {
        get;
    }
    private Config config = new(); 
    private bool relay = true;
    private bool isLoaded = false;
    public SettingsPage()
    {
        ViewModel = App.GetService<SettingsViewModel>();
        InitializeComponent();
        ConfigLoad(); 
        InitVal();
        config.fanex = false; //Автообновление информации выключено не зависимо от активированной страницы
        config.tempex = false;
        ConfigSave();
        Loaded += LoadedApp;
    }

    #region JSON and Initialization
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
    private void LoadedApp(object sender, RoutedEventArgs e)
    {
        isLoaded = true;
    } 
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
            config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json"))!;
            if (config == null) { config = new Config(); ConfigSave(); }
        }
        catch { }
    }
    #endregion
    #region Event Handlers
    private void CbStartBoot_Click(object sender, RoutedEventArgs e)
    {
        if (!isLoaded) { return; }
        var autoruns = new TaskService();
        if (CbStartBoot.IsChecked == true)
        {
            ConfigLoad();
            config.autostart = true;
            ConfigSave(); 
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
            ConfigLoad();
            config.autostart = false;
            ConfigSave();
            try { autoruns.RootFolder.DeleteTask("Saku Overclock"); } catch { }
        }
        
    }
    private void CbStartMini_Click(object sender, RoutedEventArgs e)
    {
        if (!isLoaded) { return; }
        ConfigLoad();
        if (CbStartMini.IsChecked == true) { config.traystart = true; ConfigSave(); }
        else { config.traystart = false; ConfigSave(); };
    }
    private void CbApplyStart_Click(object sender, RoutedEventArgs e)
    {
        if (!isLoaded) { return; }
        ConfigLoad();
        if (CbApplyStart.IsChecked == true) { config.autooverclock = true; ConfigSave(); }
        else { config.autooverclock = false; ConfigSave(); };
    }
    private void CbAutoReapply_Click(object sender, RoutedEventArgs e)
    {
        if (!isLoaded) { return; }
        ConfigLoad();
        if (CbAutoReapply.IsChecked == true) { config.reapplytime = true; config.reapplytimer = nudAutoReapply.Value; ConfigSave(); }
        else { config.reapplytime = false; config.reapplytimer = 3; ConfigSave(); };
    }
    private void CbAutoCheck_Click(object sender, RoutedEventArgs e)
    {
        if (!isLoaded) { return; }
        ConfigLoad();
        if (CbAutoCheck.IsChecked == true) { config.autoupdates = true; ConfigSave(); }
        else { config.autoupdates = false; ConfigSave(); };
    }
    private async void NudAutoReapply_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!isLoaded) { return; }
        ConfigLoad();
        await Task.Delay(20);
        config.reapplytime = true; config.reapplytimer = nudAutoReapply.Value; ConfigSave();
    }
    private async void Blue_sel_Checked(object sender, RoutedEventArgs e)
    {
        if (!isLoaded) { return; }
        ConfigLoad();
        await Task.Delay(230);
        config.bluetheme = true;
        ConfigSave();
        if (App.MainWindow.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = ElementTheme.Dark;
            TitleBarHelper.UpdateTitleBar(ElementTheme.Dark);
        }
        var micaBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop
        {
            Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt
        };
        App.MainWindow.SystemBackdrop = micaBackdrop;
        Dark_sel.IsChecked = false; Light_sel.IsChecked = false; Default_sel.IsChecked = false;
    }
    private void Default_sel_Checked(object sender, RoutedEventArgs e)
    {
        if (!isLoaded) { return; }
        if (relay == false)
        {
            ConfigLoad();
            Blue_sel.IsChecked = false;
            config.bluetheme = false;
            ConfigSave();
            var micaBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop
            {
                Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base
            };
            App.MainWindow.SystemBackdrop = micaBackdrop;
        }
    }
    private void Dark_sel_Checked(object sender, RoutedEventArgs e)
    {
        if (!isLoaded) { return; }
        if (relay == false)
        {
            ConfigLoad();
            Blue_sel.IsChecked = false;
            config.bluetheme = false;
            ConfigSave();
            var micaBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop
            {
                Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base
            };
            App.MainWindow.SystemBackdrop = micaBackdrop;
        }
    }
    private void Light_sel_Checked(object sender, RoutedEventArgs e)
    {
        if (!isLoaded) { return; }
        if (relay == false)
        {
            ConfigLoad();
            Blue_sel.IsChecked = false;
            config.bluetheme = false;
            ConfigSave();
            var micaBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop
            {
                Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base
            };
            App.MainWindow.SystemBackdrop = micaBackdrop;
        }
    }
    #endregion
}
