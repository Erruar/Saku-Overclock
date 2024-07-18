using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Newtonsoft.Json;
using Saku_Overclock.Helpers;
using Saku_Overclock.ViewModels;

namespace Saku_Overclock.Views;

public sealed partial class ПресетыPage : Page
{
    public ПресетыViewModel ViewModel
    {
        get;
    }
    private Config config = new(); 
    private bool relay = false;
    public ПресетыPage()
    {
        ViewModel = App.GetService<ПресетыViewModel>();
        InitializeComponent();
        ConfigLoad(); 
        InitSave();
        config.NBFCFlagConsoleCheckSpeedRunning = false;
        config.FlagRyzenADJConsoleTemperatureCheckRunning = false;
        ConfigSave();
    }
    #region JSON and Initialization
    public void InitSave()
    {
        ConfigLoad();
        if (config.PremadeMaxActivated == true) { Max_btn.IsChecked = true; PrSource.ImageSource = new BitmapImage(new System.Uri("ms-appx:///Assets/max.png")); PrName.Text = "Preset_Max".GetLocalized(); PrDesc.Text = "Preset_Max_Desc".GetLocalized(); }
        if (config.PremadeSpeedActivated == true) { Speed.IsChecked = true; PrSource.ImageSource = new BitmapImage(new System.Uri("ms-appx:///Assets/speed.png")); PrName.Text = "Preset_Speed".GetLocalized(); PrDesc.Text = "Preset_Speed_Desc".GetLocalized(); }
        if (config.PremadeBalanceActivated == true) { Balance.IsChecked = true; PrSource.ImageSource = new BitmapImage(new System.Uri("ms-appx:///Assets/balance.png")); PrName.Text = "Preset_Balance".GetLocalized(); PrDesc.Text = "Preset_Balance_Desc".GetLocalized(); }
        if (config.PremadeEcoActivated == true) { Eco.IsChecked = true; PrSource.ImageSource = new BitmapImage(new System.Uri("ms-appx:///Assets/eco.png")); PrName.Text = "Preset_Eco".GetLocalized(); PrDesc.Text = "Preset_Eco_Desc".GetLocalized(); }
        if (config.PremadeMinActivated == true) { Min_btn.IsChecked = true; PrSource.ImageSource = new BitmapImage(new System.Uri("ms-appx:///Assets/min.png")); PrName.Text = "Preset_Min".GetLocalized(); PrDesc.Text = "Preset_Min_Desc".GetLocalized(); }
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
    private void Min_btn_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        relay = true;
        Eco.IsChecked = false; Balance.IsChecked = false; Speed.IsChecked = false; Max_btn.IsChecked = false;
        ConfigLoad();
        config.PremadeMinActivated = true;
        config.PremadeEcoActivated = false;
        config.PremadeBalanceActivated = false;
        config.PremadeSpeedActivated = false;
        config.PremadeMaxActivated = false;
        config.RyzenADJline = " --tctl-temp=60 --stapm-limit=9000 --fast-limit=9000 --stapm-time=900 --slow-limit=6000 --slow-time=900 --vrm-current=120000 --vrmmax-current=120000 --vrmsoc-current=120000 --vrmsocmax-current=120000 --vrmgfx-current=120000 --prochot-deassertion-ramp=2 ";
        ConfigSave();
        InitSave();
        MainWindow.Applyer.Apply(false);
    } 
    private void Eco_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        relay = true; 
        Min_btn.IsChecked = false; Balance.IsChecked = false; Speed.IsChecked = false; Max_btn.IsChecked = false;
        ConfigLoad();
        config.PremadeMinActivated = false;
        config.PremadeEcoActivated = true;
        config.PremadeBalanceActivated = false;
        config.PremadeSpeedActivated = false;
        config.PremadeMaxActivated = false;
        config.RyzenADJline = " --tctl-temp=68 --stapm-limit=15000  --fast-limit=18000 --stapm-time=500 --slow-limit=16000 --slow-time=500 --vrm-current=120000 --vrmmax-current=120000 --vrmsoc-current=120000 --vrmsocmax-current=120000 --vrmgfx-current=120000 --prochot-deassertion-ramp=2 ";
        ConfigSave();
        InitSave();
        MainWindow.Applyer.Apply(false);
    }
    private void Balance_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        relay = true;
        Eco.IsChecked = false; Min_btn.IsChecked = false; Speed.IsChecked = false; Max_btn.IsChecked = false;
        ConfigLoad();
        config.PremadeMinActivated = false;
        config.PremadeEcoActivated = false;
        config.PremadeBalanceActivated = true;
        config.PremadeSpeedActivated = false;
        config.PremadeMaxActivated = false;
        config.RyzenADJline = " --tctl-temp=75 --stapm-limit=17000  --fast-limit=20000 --stapm-time=64 --slow-limit=19000 --slow-time=128 --vrm-current=120000 --vrmmax-current=120000 --vrmsoc-current=120000 --vrmsocmax-current=120000 --vrmgfx-current=120000 --prochot-deassertion-ramp=2";
        ConfigSave();
        InitSave();
        MainWindow.Applyer.Apply(false);
    }
    private void Speed_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        relay = true;
        Eco.IsChecked = false; Balance.IsChecked = false; Min_btn.IsChecked = false; Max_btn.IsChecked = false;
        ConfigLoad();
        config.PremadeMinActivated = false;
        config.PremadeEcoActivated = false;
        config.PremadeBalanceActivated = false;
        config.PremadeSpeedActivated = true;
        config.PremadeMaxActivated = false;
        config.RyzenADJline = " --tctl-temp=80 --stapm-limit=20000  --fast-limit=20000 --stapm-time=32 --slow-limit=20000 --slow-time=64 --vrm-current=120000 --vrmmax-current=120000 --vrmsoc-current=120000 --vrmsocmax-current=120000 --vrmgfx-current=120000 --prochot-deassertion-ramp=2";
        ConfigSave();
        InitSave();
        MainWindow.Applyer.Apply(false);
    }
    private void Max_btn_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        relay = true;
        Eco.IsChecked = false; Balance.IsChecked = false; Speed.IsChecked = false; Min_btn.IsChecked = false;
        ConfigLoad();
        config.PremadeMinActivated = false;
        config.PremadeEcoActivated = false;
        config.PremadeBalanceActivated = false;
        config.PremadeSpeedActivated = false;
        config.PremadeMaxActivated = true;
        config.RyzenADJline = " --tctl-temp=90 --stapm-limit=45000  --fast-limit=60000 --stapm-time=80 --slow-limit=60000 --slow-time=1 --vrm-current=120000 --vrmmax-current=120000 --vrmsoc-current=120000 --vrmsocmax-current=120000 --vrmgfx-current=120000 --prochot-deassertion-ramp=2";
        ConfigSave();
        InitSave();
        MainWindow.Applyer.Apply(false);
    }
    private void Min_btn_Unchecked_1(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (relay) { return; } 
        Min_btn.IsChecked = true;
    }
    private void Eco_Unchecked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (relay) { return; }
        Eco.IsChecked = true;
    }
    private void Balance_Unchecked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (relay) { return; }
        Balance.IsChecked = true;
    }
    private void Speed_Unchecked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (relay) { return; }
        Speed.IsChecked = true;
    }
    private void Max_btn_Unchecked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (relay) { return; }
        Max_btn.IsChecked = true;
    }
    #endregion
}