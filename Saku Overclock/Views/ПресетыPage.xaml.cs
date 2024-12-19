using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Newtonsoft.Json;
using Saku_Overclock.Helpers;
using Saku_Overclock.ViewModels;

namespace Saku_Overclock.Views;

public sealed partial class ПресетыPage
{
    private Config _config = new();
    private bool _relay;

    public ПресетыPage()
    {
        App.GetService<ПресетыViewModel>();
        InitializeComponent();
        ConfigLoad();
        InitSave();
        _config.NBFCFlagConsoleCheckSpeedRunning = false;
        _config.FlagRyzenADJConsoleTemperatureCheckRunning = false;
        ConfigSave();
    }

    #region JSON and Initialization

    private void InitSave()
    {
        ConfigLoad();
        if (_config.PremadeMaxActivated)
        {
            Max_btn.IsChecked = true;
            PrSource.ImageSource = new BitmapImage(new Uri("ms-appx:///Assets/max.png"));
            PrName.Text = "Preset_Max".GetLocalized();
            PrDesc.Text = "Preset_Max_Desc".GetLocalized();
        }

        if (_config.PremadeSpeedActivated)
        {
            Speed.IsChecked = true;
            PrSource.ImageSource = new BitmapImage(new Uri("ms-appx:///Assets/speed.png"));
            PrName.Text = "Preset_Speed".GetLocalized();
            PrDesc.Text = "Preset_Speed_Desc".GetLocalized();
        }

        if (_config.PremadeBalanceActivated)
        {
            Balance.IsChecked = true;
            PrSource.ImageSource = new BitmapImage(new Uri("ms-appx:///Assets/balance.png"));
            PrName.Text = "Preset_Balance".GetLocalized();
            PrDesc.Text = "Preset_Balance_Desc".GetLocalized();
        }

        if (_config.PremadeEcoActivated)
        {
            Eco.IsChecked = true;
            PrSource.ImageSource = new BitmapImage(new Uri("ms-appx:///Assets/eco.png"));
            PrName.Text = "Preset_Eco".GetLocalized();
            PrDesc.Text = "Preset_Eco_Desc".GetLocalized();
        }

        if (_config.PremadeMinActivated)
        {
            Min_btn.IsChecked = true;
            PrSource.ImageSource = new BitmapImage(new Uri("ms-appx:///Assets/min.png"));
            PrName.Text = "Preset_Min".GetLocalized();
            PrDesc.Text = "Preset_Min_Desc".GetLocalized();
        }
    }

    private void ConfigSave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                "SakuOverclock"));
            File.WriteAllText(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\config.json",
                JsonConvert.SerializeObject(_config));
        }
        catch
        {
            // ignored
        }
    }

    private void ConfigLoad()
    {
        try
        {
            _config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\config.json"))!;
        }
        catch
        {
            _config = new Config();
        }
    }

    #endregion

    #region Event Handlers

    private void Min_btn_Checked(object sender, RoutedEventArgs e)
    {
        _relay = true;
        Eco.IsChecked = false;
        Balance.IsChecked = false;
        Speed.IsChecked = false;
        Max_btn.IsChecked = false;
        ConfigLoad();
        _config.PremadeMinActivated = true;
        _config.PremadeEcoActivated = false;
        _config.PremadeBalanceActivated = false;
        _config.PremadeSpeedActivated = false;
        _config.PremadeMaxActivated = false;
        _config.RyzenADJline =
            " --tctl-temp=60 --stapm-limit=9000 --fast-limit=9000 --stapm-time=900 --slow-limit=6000 --slow-time=900 --vrm-current=120000 --vrmmax-current=120000 --vrmsoc-current=120000 --vrmsocmax-current=120000 --vrmgfx-current=120000 --prochot-deassertion-ramp=2 ";
        ConfigSave();
        InitSave();
        MainWindow.Applyer.Apply(_config.RyzenADJline, false, _config.ReapplyOverclock, _config.ReapplyOverclockTimer);
    }

    private void Eco_Checked(object sender, RoutedEventArgs e)
    {
        _relay = true;
        Min_btn.IsChecked = false;
        Balance.IsChecked = false;
        Speed.IsChecked = false;
        Max_btn.IsChecked = false;
        ConfigLoad();
        _config.PremadeMinActivated = false;
        _config.PremadeEcoActivated = true;
        _config.PremadeBalanceActivated = false;
        _config.PremadeSpeedActivated = false;
        _config.PremadeMaxActivated = false;
        _config.RyzenADJline =
            " --tctl-temp=68 --stapm-limit=15000  --fast-limit=18000 --stapm-time=500 --slow-limit=16000 --slow-time=500 --vrm-current=120000 --vrmmax-current=120000 --vrmsoc-current=120000 --vrmsocmax-current=120000 --vrmgfx-current=120000 --prochot-deassertion-ramp=2 ";
        ConfigSave();
        InitSave();
        MainWindow.Applyer.Apply(_config.RyzenADJline, false, _config.ReapplyOverclock, _config.ReapplyOverclockTimer);
    }

    private void Balance_Checked(object sender, RoutedEventArgs e)
    {
        _relay = true;
        Eco.IsChecked = false;
        Min_btn.IsChecked = false;
        Speed.IsChecked = false;
        Max_btn.IsChecked = false;
        ConfigLoad();
        _config.PremadeMinActivated = false;
        _config.PremadeEcoActivated = false;
        _config.PremadeBalanceActivated = true;
        _config.PremadeSpeedActivated = false;
        _config.PremadeMaxActivated = false;
        _config.RyzenADJline =
            " --tctl-temp=75 --stapm-limit=17000  --fast-limit=20000 --stapm-time=64 --slow-limit=19000 --slow-time=128 --vrm-current=120000 --vrmmax-current=120000 --vrmsoc-current=120000 --vrmsocmax-current=120000 --vrmgfx-current=120000 --prochot-deassertion-ramp=2";
        ConfigSave();
        InitSave();
        MainWindow.Applyer.Apply(_config.RyzenADJline, false, _config.ReapplyOverclock, _config.ReapplyOverclockTimer);
    }

    private void Speed_Checked(object sender, RoutedEventArgs e)
    {
        _relay = true;
        Eco.IsChecked = false;
        Balance.IsChecked = false;
        Min_btn.IsChecked = false;
        Max_btn.IsChecked = false;
        ConfigLoad();
        _config.PremadeMinActivated = false;
        _config.PremadeEcoActivated = false;
        _config.PremadeBalanceActivated = false;
        _config.PremadeSpeedActivated = true;
        _config.PremadeMaxActivated = false;
        _config.RyzenADJline =
            " --tctl-temp=80 --stapm-limit=20000  --fast-limit=20000 --stapm-time=32 --slow-limit=20000 --slow-time=64 --vrm-current=120000 --vrmmax-current=120000 --vrmsoc-current=120000 --vrmsocmax-current=120000 --vrmgfx-current=120000 --prochot-deassertion-ramp=2";
        ConfigSave();
        InitSave();
        MainWindow.Applyer.Apply(_config.RyzenADJline, false, _config.ReapplyOverclock, _config.ReapplyOverclockTimer);
    }

    private void Max_btn_Checked(object sender, RoutedEventArgs e)
    {
        _relay = true;
        Eco.IsChecked = false;
        Balance.IsChecked = false;
        Speed.IsChecked = false;
        Min_btn.IsChecked = false;
        ConfigLoad();
        _config.PremadeMinActivated = false;
        _config.PremadeEcoActivated = false;
        _config.PremadeBalanceActivated = false;
        _config.PremadeSpeedActivated = false;
        _config.PremadeMaxActivated = true;
        _config.RyzenADJline =
            " --tctl-temp=90 --stapm-limit=45000  --fast-limit=60000 --stapm-time=80 --slow-limit=60000 --slow-time=1 --vrm-current=120000 --vrmmax-current=120000 --vrmsoc-current=120000 --vrmsocmax-current=120000 --vrmgfx-current=120000 --prochot-deassertion-ramp=2";
        ConfigSave();
        InitSave();
        MainWindow.Applyer.Apply(_config.RyzenADJline, false, _config.ReapplyOverclock, _config.ReapplyOverclockTimer);
    }

    private void Min_btn_Unchecked_1(object sender, RoutedEventArgs e)
    {
        if (_relay)
        {
            return;
        }

        Min_btn.IsChecked = true;
    }

    private void Eco_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_relay)
        {
            return;
        }

        Eco.IsChecked = true;
    }

    private void Balance_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_relay)
        {
            return;
        }

        Balance.IsChecked = true;
    }

    private void Speed_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_relay)
        {
            return;
        }

        Speed.IsChecked = true;
    }

    private void Max_btn_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_relay)
        {
            return;
        }

        Max_btn.IsChecked = true;
    }

    #endregion
}