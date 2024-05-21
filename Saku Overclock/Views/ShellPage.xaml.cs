using System.Collections.ObjectModel;
using System.Windows.Threading;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation; 
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.ViewModels;
using Windows.Foundation;
using Windows.System;

namespace Saku_Overclock.Views;

// TODO: Update NavigationViewItem titles and icons in ShellPage.xaml.
public sealed partial class ShellPage : Page
{ 
    private System.Windows.Threading.DispatcherTimer? dispatcherTimer;
    private bool loaded = false;
    private bool IsNotificationPanelShow;
    private int? compareList;
    private Config config = new();
    private JsonContainers.Notifications notify = new();
    private Profile[] profile = new Profile[1];
    private readonly Microsoft.UI.Windowing.AppWindow m_AppWindow;
    private bool fixedTitleBar = false;
    public ShellViewModel ViewModel
    {
        get;
    }
    
    public ShellPage(ShellViewModel viewModel)
    {
        m_AppWindow = App.MainWindow.AppWindow;
        ViewModel = viewModel;
        InitializeComponent(); 
        ViewModel.NavigationService.Frame = NavigationFrame;
        ViewModel.NavigationViewService.Initialize(NavigationViewControl); 
        // A custom title bar is required for full window theme and Mica support.
        // https://docs.microsoft.com/windows/apps/develop/title-bar?tabs=winui3#full-customization
        App.MainWindow.ExtendsContentIntoTitleBar = true;
        App.MainWindow.SetTitleBar(AppTitleBar);
        App.MainWindow.Activated += MainWindow_Activated; 
    }
    #region JSON and Initialization
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        TitleBarHelper.UpdateTitleBar(RequestedTheme);
        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.Left, VirtualKeyModifiers.Menu));
        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.GoBack));
        loaded = true;
        MandarinAddNotification("Overclock Started!\n" + DateTime.Today, "Now you can set up your Saku Overclock", InfoBarSeverity.Success); //DEBUG. TEST MESSAGE. WILL NOT BE DISPLAYED!!!
        StartInfoUpdate();
        GetProfileInit();
    }
    private void StartInfoUpdate()
    {
        dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
        dispatcherTimer.Tick += async (sender, e) => await GetNotify();
        dispatcherTimer.Interval = TimeSpan.FromMilliseconds(1000);
        App.MainWindow.VisibilityChanged += Window_VisibilityChanged;
        App.MainWindow.Activated += Window_Activated;
        dispatcherTimer.Start();
    }
    private void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.CodeActivated || args.WindowActivationState == WindowActivationState.PointerActivated)
        {
            // Окно активировано
            dispatcherTimer?.Start();
        }
        else
        {
            // Окно не активировано
            dispatcherTimer?.Stop();
        } 
    }
    private void Window_VisibilityChanged(object sender, WindowVisibilityChangedEventArgs args)
    {
        if (args.Visible) { dispatcherTimer?.Start(); } else { dispatcherTimer?.Stop(); }
    }
    private void StopInfoUpdate()
    {
        dispatcherTimer?.Stop();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e); StartInfoUpdate();
    }
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e); StopInfoUpdate();
    }
    public Task GetNotify()
    {
        if (IsNotificationPanelShow)
        {
            return Task.CompletedTask;
        } 
        NotifyLoad(); 
        if (notify.Notifies == null) { return Task.CompletedTask; }
        DispatcherQueue?.TryEnqueue(() =>
        {
            var contains = false;
            if (compareList == notify?.Notifies.Count && NotificationContainer.Children.Count != 0) { return; } //нет новых уведомлений - пока
            ClearAllNotification(null, null);
            foreach (var notify1 in notify?.Notifies!)
            {
                MandarinAddNotification(notify1.Title, notify1.Msg, notify1.Type, notify1.isClosable, notify1.Subcontent, notify1.CloseClickHandler);
                if (notify1.Title.Contains("SaveSuccessTitle".GetLocalized()) || notify1.Title.Contains("DeleteSuccessTitle".GetLocalized()) || notify1.Title.Contains("Edit_TargetTitle".GetLocalized())) { contains = true; }
            }
            if (contains) { GetProfileInit(); } //Чтобы обновить всего раз, а не много раз, чтобы не сбить конфиг
            compareList = notify?.Notifies.Count;
        });
        return Task.CompletedTask;
    }
    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        // App.AppTitlebar = AppTitleBarText as UIElement;
        App.AppTitlebar = VersionNumberIndicator;
        AppTitleBar.Loaded += AppTitleBar_Loaded;
        AppTitleBar.SizeChanged += AppTitleBar_SizeChanged;
    }
    private void AppTitleBar_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            SetRegionsForCustomTitleBar();  //Установить регион взаимодействия
        }
        catch { }
    }

    private void AppTitleBar_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        try
        {
            SetRegionsForCustomTitleBar();  //Установить регион взаимодействия
        }
        catch { }
    }
    public void ConfigSave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json", JsonConvert.SerializeObject(config, Formatting.Indented));
        }
        catch
        {
            // ignored
        }
    }
    public void ConfigLoad()
    {
        try
        {
            config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json"))!;
        }
        catch
        {
            JsonRepair('c');
        }
    }
    public void ProfileSave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json", JsonConvert.SerializeObject(profile, Formatting.Indented));
        }
        catch
        {
            // ignored
        }
    }
    public void ProfileLoad()
    {
        try
        {

            profile = JsonConvert.DeserializeObject<Profile[]>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json"))!;
        }
        catch
        {
            JsonRepair('p');
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
                    if (notify != null) { success = true; } else { JsonRepair('p'); }
                }
                catch { JsonRepair('n'); }
            }
            else { JsonRepair('n'); }
            if (!success)
            {
                // Сделайте задержку перед следующей попыткой
                await Task.Delay(30);
                retryCount++;
            }
        }
    }
    public void JsonRepair(char file)
    {
        if (file == 'c')
        {
            try
            {
                config = new Config();
            }
            catch
            {
                App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                App.MainWindow.Close();
            }
            if (config != null)
            {
                try
                {
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json", JsonConvert.SerializeObject(config));
                }
                catch
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json");
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json", JsonConvert.SerializeObject(config));
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }
            }
            else
            {
                try
                {

                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json");
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json", JsonConvert.SerializeObject(config));
                    App.MainWindow.Close();
                }
                catch
                {
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }
            }
        }
        if (file == 'p')
        {
            try
            {
                for (var j = 0; j < 3; j++)
                {
                    profile[j] = new Profile();
                }
            }
            catch
            {
                App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                App.MainWindow.Close();
            }
            if (profile != null)
            {
                try
                {
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json", JsonConvert.SerializeObject(profile));
                }
                catch
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json");
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json", JsonConvert.SerializeObject(profile));
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }
            }
            else
            {
                try
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json");
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json", JsonConvert.SerializeObject(profile));
                    App.MainWindow.Close();
                }
                catch
                {
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }
            }
        }
        if (file == 'n')
        {
            try
            {
                notify = new JsonContainers.Notifications();
            }
            catch
            {
                App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                App.MainWindow.Close();
            }
            if (notify != null)
            {
                try
                {
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json", JsonConvert.SerializeObject(notify));
                }
                catch
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json");
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json", JsonConvert.SerializeObject(notify));
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }
            }
            else
            {
                try
                {

                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json");
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json", JsonConvert.SerializeObject(notify));
                    App.MainWindow.Close();
                }
                catch
                {
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }
            }
        }
    }
    public void GetProfileInit()
    {
        ConfigLoad();
        if (!config.OldTitleBar)
        {
            var Itemz = new ObservableCollection<ComboBoxItem>();
            Itemz.Clear();
            var userProfiles = new ComboBoxItem
            {
                Content = new TextBlock { Text = "User profiles", Foreground = (Microsoft.UI.Xaml.Media.Brush)App.Current.Resources["AccentTextFillColorTertiaryBrush"] },
                IsEnabled = false
            };
            Itemz.Add(userProfiles);
            ProfileLoad();
            foreach (var profile in profile)
            {
                var comboBoxItem = new ComboBoxItem
                {
                    Content = profile.profilename
                };
                Itemz.Add(comboBoxItem);
            }
            // Добавление второго элемента (с разделителем)
            var separator = new ComboBoxItem
            {
                IsEnabled = false,
                Content = new NavigationViewItemSeparator()
                {
                    BorderThickness = new Thickness(1)
                }
            };
            Itemz.Add(separator);
            var premadedProfiles = new ComboBoxItem
            {
                Content = new TextBlock { Text = "Premaded profiles", Foreground = (Microsoft.UI.Xaml.Media.Brush)App.Current.Resources["AccentTextFillColorTertiaryBrush"] },
                IsEnabled = false
            };
            Itemz.Add(premadedProfiles);
            Itemz.Add(new ComboBoxItem() { Content = "Minimum", Name = "PremadeSsAMin" });
            Itemz.Add(new ComboBoxItem() { Content = "Eco", Name = "PremadeSsAEco" });
            Itemz.Add(new ComboBoxItem() { Content = "Balance", Name = "PremadeSsABal" });
            Itemz.Add(new ComboBoxItem() { Content = "Speed", Name = "PremadeSsASpd" });
            Itemz.Add(new ComboBoxItem() { Content = "Maximum", Name = "PremadeSsAMax" });
            ViewModel.Items = Itemz;
            if (config.Preset == -1)
            {
                if (config.Min) { SelectTru("PremadeSsAMin"); }
                if (config.Eco) { SelectTru("PremadeSsAEco"); }
                if (config.Balance) { SelectTru("PremadeSsABal"); }
                if (config.Speed) { SelectTru("PremadeSsASpd"); }
                if (config.Max) { SelectTru("PremadeSsAMax"); }
            }
            else
            {
                ViewModel.SelectedIndex = config.Preset + 1; ProfileSetComboBox.SelectedIndex = config.Preset + 1;
            }
            if (config.autooverclock) { ProfileSetButton.IsEnabled = false; }
        }
    }
    private void SelectTru(string Names)
    {
        foreach (var box in ProfileSetComboBox.Items)
        {
            var combobox = box as ComboBoxItem;
            if (combobox?.Name.Contains(Names) == true)
            {
                ProfileSetComboBox.SelectedItem = combobox;
                return;
            }
        }
    }
    public void MandarinSparseUnit()
    {
        int indexRequired;
        var element = ProfileSetComboBox.SelectedItem as ComboBoxItem;

        ConfigLoad();
        //Required index
        if (!element!.Name.Contains("PremadeSsA"))
        {
            indexRequired = ProfileSetComboBox.SelectedIndex - 1;
            config.Preset = ProfileSetComboBox.SelectedIndex - 1;
            ConfigSave();
            /*App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                    {
                        var navigationService = App.GetService<INavigationService>();
                        if (App.MainWindow.Content as Frame == cpu.Frame) { navigationService.NavigateTo(typeof(ГлавнаяViewModel).FullName!); navigationService.NavigateTo(typeof(ПараметрыViewModel).FullName!); }
                    });*/
        }
        else
        {
            if (element!.Name.Contains("Min"))
            {
                config.Min = true;
                config.Eco = false;
                config.Balance = false;
                config.Speed = false;
                config.Max = false;
                config.adjline = " --tctl-temp=60 --stapm-limit=9000 --fast-limit=9000 --stapm-time=64 --slow-limit=6000 --slow-time=128 --vrm-current=180000 --vrmmax-current=180000 --vrmsoc-current=180000 --vrmsocmax-current=180000 --vrmgfx-current=180000 --prochot-deassertion-ramp=2";
                /*   param.InitSave();*/
                MainWindow.Applyer.Apply(false);
            }
            if (element!.Name.Contains("Eco"))
            {
                config.Min = false;
                config.Eco = true;
                config.Balance = false;
                config.Speed = false;
                config.Max = false;
                config.adjline = " --tctl-temp=68 --stapm-limit=15000  --fast-limit=18000 --stapm-time=64 --slow-limit=16000 --slow-time=128 --vrm-current=180000 --vrmmax-current=180000 --vrmsoc-current=180000 --vrmsocmax-current=180000 --vrmgfx-current=180000 --prochot-deassertion-ramp=2";
                MainWindow.Applyer.Apply(false);
            }
            if (element!.Name.Contains("Bal"))
            {
                config.Min = false;
                config.Eco = false;
                config.Balance = true;
                config.Speed = false;
                config.Max = false;
                config.adjline = " --tctl-temp=75 --stapm-limit=18000  --fast-limit=20000 --stapm-time=64 --slow-limit=19000 --slow-time=128 --vrm-current=180000 --vrmmax-current=180000 --vrmsoc-current=180000 --vrmsocmax-current=180000 --vrmgfx-current=180000 --prochot-deassertion-ramp=2";
                MainWindow.Applyer.Apply(false);
            }
            if (element!.Name.Contains("Spd"))
            {
                config.Min = false;
                config.Eco = false;
                config.Balance = false;
                config.Speed = true;
                config.Max = false;
                config.adjline = " --tctl-temp=80 --stapm-limit=20000  --fast-limit=20000 --stapm-time=64 --slow-limit=20000 --slow-time=128 --vrm-current=180000 --vrmmax-current=180000 --vrmsoc-current=180000 --vrmsocmax-current=180000 --vrmgfx-current=180000 --prochot-deassertion-ramp=2";
                MainWindow.Applyer.Apply(false);
            }
            if (element!.Name.Contains("Max"))
            {
                config.Min = false;
                config.Eco = false;
                config.Balance = false;
                config.Speed = false;
                config.Max = true;
                config.adjline = " --tctl-temp=90 --stapm-limit=45000  --fast-limit=60000 --stapm-time=64 --slow-limit=60000 --slow-time=128 --vrm-current=180000 --vrmmax-current=180000 --vrmsoc-current=180000 --vrmsocmax-current=180000 --vrmgfx-current=180000 --prochot-deassertion-ramp=2";
                MainWindow.Applyer.Apply(false);
            }
            ConfigSave();
            /* App.MainWindow.DispatcherQueue.TryEnqueue(() =>
             {
                 var navigationService = App.GetService<INavigationService>();
                 if (App.MainWindow.Content as Frame == param.Frame) { navigationService.NavigateTo(typeof(ГлавнаяViewModel).FullName!); navigationService.NavigateTo(typeof(ПресетыViewModel).FullName!); }
             });*/
            return;
        }
        ConfigLoad();
        ProfileLoad();
        var adjline = "";
        if (profile[indexRequired].cpu1)
        {
            adjline += " --tctl-temp=" + profile[indexRequired].cpu1value;
        }

        if (profile[indexRequired].cpu2)
        {
            adjline += " --stapm-limit=" + profile[indexRequired].cpu2value + "000";
        }

        if (profile[indexRequired].cpu3)
        {
            adjline += " --fast-limit=" + profile[indexRequired].cpu3value + "000";
        }

        if (profile[indexRequired].cpu4)
        {
            adjline += " --slow-limit=" + profile[indexRequired].cpu4value + "000";
        }

        if (profile[indexRequired].cpu5)
        {
            adjline += " --stapm-time=" + profile[indexRequired].cpu5value;
        }

        if (profile[indexRequired].cpu6)
        {
            adjline += " --slow-time=" + profile[indexRequired].cpu6value;
        }
        if (profile[indexRequired].cpu7)
        {
            adjline += " --cHTC-temp=" + profile[indexRequired].cpu7value;
        }

        //vrm
        if (profile[indexRequired].vrm1)
        {
            adjline += " --vrmmax-current=" + profile[indexRequired].vrm1value + "000";
        }

        if (profile[indexRequired].vrm2)
        {
            adjline += " --vrm-current=" + profile[indexRequired].vrm2value + "000";
        }

        if (profile[indexRequired].vrm3)
        {
            adjline += " --vrmsocmax-current=" + profile[indexRequired].vrm3value + "000";
        }

        if (profile[indexRequired].vrm4)
        {
            adjline += " --vrmsoc-current=" + profile[indexRequired].vrm4value + "000";
        }

        if (profile[indexRequired].vrm5)
        {
            adjline += " --psi0-current=" + profile[indexRequired].vrm5value + "000";
        }

        if (profile[indexRequired].vrm6)
        {
            adjline += " --psi0soc-current=" + profile[indexRequired].vrm6value + "000";
        }

        if (profile[indexRequired].vrm7)
        {
            adjline += " --prochot-deassertion-ramp=" + profile[indexRequired].vrm7value;
        }
        if (profile[indexRequired].vrm8)
        {
            adjline += " --oc-volt-scalar=" + profile[indexRequired].vrm8value;
        }
        if (profile[indexRequired].vrm9)
        {
            adjline += " --oc-volt-modular=" + profile[indexRequired].vrm9value;
        }
        if (profile[indexRequired].vrm10)
        {
            adjline += " --oc-volt-variable=" + profile[indexRequired].vrm10value;
        }

        //gpu
        if (profile[indexRequired].gpu1)
        {
            adjline += " --min-socclk-frequency=" + profile[indexRequired].gpu1value;
        }

        if (profile[indexRequired].gpu2)
        {
            adjline += " --max-socclk-frequency=" + profile[indexRequired].gpu2value;
        }

        if (profile[indexRequired].gpu3)
        {
            adjline += " --min-fclk-frequency=" + profile[indexRequired].gpu3value;
        }

        if (profile[indexRequired].gpu4)
        {
            adjline += " --max-fclk-frequency=" + profile[indexRequired].gpu4value;
        }

        if (profile[indexRequired].gpu5)
        {
            adjline += " --min-vcn=" + profile[indexRequired].gpu5value;
        }

        if (profile[indexRequired].gpu6)
        {
            adjline += " --max-vcn=" + profile[indexRequired].gpu6value;
        }

        if (profile[indexRequired].gpu7)
        {
            adjline += " --min-lclk=" + profile[indexRequired].gpu7value;
        }

        if (profile[indexRequired].gpu8)
        {
            adjline += " --max-lclk=" + profile[indexRequired].gpu8value;
        }

        if (profile[indexRequired].gpu9)
        {
            adjline += " --min-gfxclk=" + profile[indexRequired].gpu9value;
        }

        if (profile[indexRequired].gpu10)
        {
            adjline += " --max-gfxclk=" + profile[indexRequired].gpu10value;
        }
        if (profile[indexRequired].gpu11)
        {
            adjline += " --min-cpuclk=" + profile[indexRequired].gpu11value;
        }
        if (profile[indexRequired].gpu12)
        {
            adjline += " --max-cpuclk=" + profile[indexRequired].gpu12value;
        }
        if (profile[indexRequired].gpu13)
        {
            adjline += " --setgpu-arerture-low=" + profile[indexRequired].gpu13value;
        }
        if (profile[indexRequired].gpu14)
        {
            adjline += " --setgpu-arerture-high=" + profile[indexRequired].gpu14value;
        }
        if (profile[indexRequired].gpu15)
        {
            if (profile[indexRequired].gpu15value != 0) { adjline += " --start-gpu-link=" + (profile[indexRequired].gpu15value - 1).ToString(); }
            else { adjline += " --stop-gpu-link=0"; }
        }
        if (profile[indexRequired].gpu16)
        {
            if (profile[indexRequired].gpu16value != 0) { adjline += " --setcpu-freqto-ramstate=" + (profile[indexRequired].gpu16value - 1).ToString(); }
            else { adjline += " --stopcpu-freqto-ramstate=0"; }
        }
        //advanced
        if (profile[indexRequired].advncd1)
        {
            adjline += " --vrmgfx-current=" + profile[indexRequired].advncd1value + "000";
        }

        if (profile[indexRequired].advncd2)
        {
            adjline += " --vrmcvip-current=" + profile[indexRequired].advncd2value + "000";
        }

        if (profile[indexRequired].advncd3)
        {
            adjline += " --vrmgfxmax_current=" + profile[indexRequired].advncd3value + "000";
        }

        if (profile[indexRequired].advncd4)
        {
            adjline += " --psi3cpu_current=" + profile[indexRequired].advncd4value + "000";
        }

        if (profile[indexRequired].advncd5)
        {
            adjline += " --psi3gfx_current=" + profile[indexRequired].advncd5value + "000";
        }

        if (profile[indexRequired].advncd6)
        {
            adjline += " --apu-skin-temp=" + profile[indexRequired].advncd6value;
        }

        if (profile[indexRequired].advncd7)
        {
            adjline += " --dgpu-skin-temp=" + profile[indexRequired].advncd7value;
        }

        if (profile[indexRequired].advncd8)
        {
            adjline += " --apu-slow-limit=" + profile[indexRequired].advncd8value + "000";
        }

        if (profile[indexRequired].advncd9)
        {
            adjline += " --skin-temp-limit=" + profile[indexRequired].advncd9value + "000";
        }

        if (profile[indexRequired].advncd10)
        {
            adjline += " --gfx-clk=" + profile[indexRequired].advncd10value;
        }

        if (profile[indexRequired].advncd11)
        {
            adjline += " --oc-clk=" + profile[indexRequired].advncd11value;
        }

        if (profile[indexRequired].advncd12)
        {
            adjline += " --oc-volt=" + Math.Round((1.55 - profile[indexRequired].advncd12value / 1000) / 0.00625);
        }


        if (profile[indexRequired].advncd13)
        {
            if (profile[indexRequired].advncd13value == 1)
            {
                adjline += " --max-performance=1";
            }

            if (profile[indexRequired].advncd13value == 2)
            {
                adjline += " --power-saving=1";
            }
        }
        if (profile[indexRequired].advncd14)
        {
            if (profile[indexRequired].advncd14value == 0)
            {
                adjline += " --disable-oc=1";
            }

            if (profile[indexRequired].advncd14value == 1)
            {
                adjline += " --enable-oc=1";
            }
        }
        if (profile[indexRequired].advncd15)
        {
            adjline += " --pbo-scalar=" + profile[indexRequired].advncd15value * 100;
        }
        config.adjline = adjline + " ";
        ConfigSave();
        MainWindow.Applyer.Apply(false); //false - logging disabled 
        /*   if (profile[indexRequired].enablePstateEditor) { cpu.BtnPstateWrite_Click(); }*/
    }
    #endregion
    #region Based on Collapse Launcher TitleBar 
    private void ToggleTitleIcon(bool hide) //Based on Collapse Launcher Titlebar, check out -> https://github.com/CollapseLauncher/Collapse
    {
        if (!hide)
        {
            IconTitle.Width = double.NaN;
            IconTitle.Opacity = 1d;
            IconImg.Opacity = 1d;
            return;
        }
        IconTitle.Width = 0.0;
        IconTitle.Opacity = 0d;
        IconImg.Opacity = 0.8d;
    }
    private void TitleIcon_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (!NavigationViewControl.IsPaneOpen && !fixedTitleBar)
        {
            //показать
            var curMargin = Icon.Margin;
            curMargin.Left = -1;
            Icon.Margin = curMargin;
            ToggleTitleIcon(false);
        }
    }
    private void TitleIcon_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        //скрыть
        if (!NavigationViewControl.IsPaneOpen && !fixedTitleBar)
        {
            var curMargin = Icon.Margin;
            curMargin.Left = 3;
            Icon.Margin = curMargin;
            ToggleTitleIcon(true);
        }
    }
    private void Icon_Click(object sender, RoutedEventArgs e)
    {
        fixedTitleBar = !fixedTitleBar;
    }
    private void NavigationViewControl_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
    {
        AppTitleBar.Margin = new Thickness()
        {
            Left = sender.CompactPaneLength * (sender.DisplayMode == NavigationViewDisplayMode.Minimal ? 2 : 1),
            Top = AppTitleBar.Margin.Top,
            Right = AppTitleBar.Margin.Right,
            Bottom = AppTitleBar.Margin.Bottom
        };
    }
    private static KeyboardAccelerator BuildKeyboardAccelerator(VirtualKey key, VirtualKeyModifiers? modifiers = null)
    {
        var keyboardAccelerator = new KeyboardAccelerator() { Key = key };

        if (modifiers.HasValue)
        {
            keyboardAccelerator.Modifiers = modifiers.Value;
        }

        keyboardAccelerator.Invoked += OnKeyboardAcceleratorInvoked;

        return keyboardAccelerator;
    }
    private static void OnKeyboardAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        var navigationService = App.GetService<INavigationService>();

        var result = navigationService.GoBack();

        args.Handled = result;
    }
    private void NavigationViewControl_PaneOpened(NavigationView sender, object args)
    {
        IconColumn.Width = new GridLength(170, GridUnitType.Pixel);
        var curMargin = Icon.Margin;
        curMargin.Left = 50;
        Icon.Margin = curMargin;
        ToggleTitleIcon(false);
    }
    private void NavigationViewControl_PaneClosed(NavigationView sender, object args)
    {
        if (!NavigationViewControl.IsPaneOpen && !fixedTitleBar)
        {
            var curMargin = Icon.Margin;
            curMargin.Left = 3;
            Icon.Margin = curMargin;
            ToggleTitleIcon(true);
            IconColumn.Width = new GridLength(120, GridUnitType.Pixel);
        }
    }
    private void SetRegionsForCustomTitleBar()
    {
        var scaleAdjustment = AppTitleBar.XamlRoot.RasterizationScale; // Specify the interactive regions of the title bar.
        RightPaddingColumn.Width = new GridLength(m_AppWindow.TitleBar.RightInset / scaleAdjustment);
        LeftPaddingColumn.Width = new GridLength(m_AppWindow.TitleBar.LeftInset / scaleAdjustment);

        var transform = TitleIcon.TransformToVisual(null);
        var bounds = transform.TransformBounds(new Rect(0, 0,
                                                    TitleIcon.ActualWidth,
                                                    TitleIcon.ActualHeight));
        var SearchBoxRect = GetRect(bounds, scaleAdjustment);

        transform = ProfileSetup.TransformToVisual(null);
        bounds = transform.TransformBounds(new Rect(0, 0,
                                                    ProfileSetup.ActualWidth,
                                                    ProfileSetup.ActualHeight));
        var ProfileSetupRect = GetRect(bounds, scaleAdjustment);

        transform = RingerNotifGrid.TransformToVisual(null);
        bounds = transform.TransformBounds(new Rect(0, 0,
                                                    RingerNotifGrid.ActualWidth,
                                                    RingerNotifGrid.ActualHeight));
        var RingerNotifRect = GetRect(bounds, scaleAdjustment);

        var rectArray = new Windows.Graphics.RectInt32[] { SearchBoxRect, ProfileSetupRect, RingerNotifRect };

        var nonClientInputSrc = InputNonClientPointerSource.GetForWindowId(m_AppWindow.Id);
        nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, rectArray);
    }
    private Windows.Graphics.RectInt32 GetRect(Rect bounds, double scale)
    {
        return new Windows.Graphics.RectInt32(
            _X: (int)Math.Round(bounds.X * scale),
            _Y: (int)Math.Round(bounds.Y * scale),
            _Width: (int)Math.Round(bounds.Width * scale),
            _Height: (int)Math.Round(bounds.Height * scale)
        );
    }
    #endregion
    #region Event Handlers
    private void ProfileSetButton_Click(object sender, RoutedEventArgs e)
    {
        MandarinSparseUnit();
        ProfileSetButton.IsEnabled = false; 
    }
    private void ProfileSetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!loaded) { return; }
        ConfigLoad();
        if (ProfileSetComboBox.SelectedIndex == config.Preset + 1)
        {
            ProfileSetButton.IsEnabled = false;
        }
        else
        {
            ProfileSetButton.IsEnabled = true;
        }
    }
    private void ToggleNotificationPanelBtn_Click(object sender, RoutedEventArgs e)
    {
        IsNotificationPanelShow = ToggleNotificationPanelBtn.IsChecked ?? false;
        ShowHideNotificationPanel(true);
    }
    private void NotificationContainerBackground_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        IsNotificationPanelShow = false;
        ToggleNotificationPanelBtn.IsChecked = false;
        ShowHideNotificationPanel(true);
    }
    private void CloseThisClickHandler(InfoBar sender, object args)
    {
        var Container = new Grid() { Tag = sender.Name };
        sender.IsOpen = false;
        var list = notify?.Notifies!;
        for (var i = 0; i < list.Count; i++)
        {
            var notify1 = list[i];
            if (sender.Title == notify1.Title && sender.Message == notify1.Msg && sender.Severity == notify1.Type)
            {
                NotifyLoad();
                notify?.Notifies?.RemoveAt(i);
                NotifySave();
                return;
            }
        }
        sender.Height = 0;
        sender.Margin = new Thickness(0, 0, 0, 0);
        NoNotificationIndicator.Opacity = NotificationContainer.Children.Count > 0 ? 0f : 1f;
        if (NewNotificationCountBadge.Value > 0) { NewNotificationCountBadge.Value--; }
        NoNotificationIndicator.Opacity = NotificationContainer.Children.Count > 0 ? 0f : 1f;
        NewNotificationCountBadge.Visibility = NewNotificationCountBadge.Value > 0 ? Visibility.Visible : Visibility.Collapsed;
        NotificationPanelClearAllGrid.Visibility = NotificationContainer.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        NotificationContainer.Children.Remove(Container);
    }
    private async void ClearAllNotification(object? sender, RoutedEventArgs? args)
    {
        var button = sender is Button ? sender as Button : null;
        if (button != null)
        {
            button.IsEnabled = false;
        }
        var stackIndex = 0;
        for (; stackIndex < NotificationContainer.Children.Count;)
        {
            if (NotificationContainer.Children[stackIndex] is not Grid container
             || container.Children == null || container.Children.Count == 0
             || container.Children[0] is not InfoBar notifBar || notifBar == null
             || !notifBar.IsClosable)
            {
                ++stackIndex;
                continue;
            }

            NotificationContainer.Children.RemoveAt(stackIndex);
            notifBar.IsOpen = false;
            await Task.Delay(100);
        }
        if (NotificationContainer.Children.Count == 0)
        {
            await Task.Delay(500);
            ToggleNotificationPanelBtn.IsChecked = false;
            IsNotificationPanelShow = false;
            ShowHideNotificationPanel(false);
        }
        if (button != null) { button.IsEnabled = true; NotifyLoad(); notify.Notifies = new List<SMUEngine.Notify>(); NotifySave(); }
    }
    #endregion
    #region Based on Collapse Launcher Notification voids
    private void ShowHideNotificationPanel(bool hider)
    {
        if (!hider) { return; }
        var lastMargin = NotificationPanel.Margin;
        lastMargin.Right = IsNotificationPanelShow ? 0 : NotificationPanel.ActualWidth * -1;
        NotificationPanel.Margin = lastMargin;
        NewNotificationCountBadge.Value = 0;
        NewNotificationCountBadge.Visibility = Visibility.Collapsed;
        ShowHideNotificationLostFocusBackground(IsNotificationPanelShow);
    }
    private async void ShowHideNotificationLostFocusBackground(bool show)
    {
        if (show)
        {
            NotificationLostFocusBackground.Visibility = Visibility.Visible;
            NotificationLostFocusBackground.Opacity = 0.3;
        }
        else
        {
            NotificationLostFocusBackground.Opacity = 0;
            await Task.Delay(200);
            NotificationLostFocusBackground.Visibility = Visibility.Collapsed;
        }
    }
    public void MandarinAddNotification(string Title, string Msg, InfoBarSeverity Type, bool IsClosable = true, Grid? Subcontent = null, TypedEventHandler<InfoBar, object>? CloseClickHandler = null)
    {
        DispatcherQueue?.TryEnqueue(() =>
        {
            var OtherContentContainer = new StackPanel() { Margin = new Thickness(0, -4, 0, 8) };
            var Notification = new InfoBar
            {
                Title = Title,
                Message = Msg,
                Severity = Type,
                IsClosable = IsClosable,
                IsIconVisible = true,
                Shadow = SharedShadow,
                IsOpen = true,
                Margin = new Thickness(0, 4, 4, 0),
                Width = 600,
                CornerRadius = new CornerRadius(8),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            if (Subcontent != null) { OtherContentContainer.Children.Add(Subcontent); Notification.Content = OtherContentContainer; }
            Notification.Name = Msg + " " + DateTime.Now.ToString();
            Notification.CloseButtonClick += CloseClickHandler;
            MandarinShowNotify(Name, Notification);
            if (CloseClickHandler == null) { Notification.CloseButtonClick += CloseThisClickHandler; } 
        });
    }
    public void MandarinShowNotify(string Name, InfoBar Notification)
    {
        var Container = new Grid() { Tag = Name };
        Notification.Loaded += (a, b) =>
        {
            NoNotificationIndicator.Opacity = NotificationContainer.Children.Count > 0 ? 0f : 1f;
            NewNotificationCountBadge.Visibility = Visibility.Visible;
            NewNotificationCountBadge.Value++;
            NotificationPanelClearAllGrid.Visibility = NotificationContainer.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        };

        Notification.Closed += (s, a) =>
        {
            s.Height = 0;
            s.Margin = new Thickness(0, 0, 0, 0);
            //var msg = (int)s.Tag; 
            NoNotificationIndicator.Opacity = NotificationContainer.Children.Count > 0 ? 0f : 1f;
            if (NewNotificationCountBadge.Value > 0) { NewNotificationCountBadge.Value--; }
            NoNotificationIndicator.Opacity = NotificationContainer.Children.Count > 0 ? 0f : 1f;
            NewNotificationCountBadge.Visibility = NewNotificationCountBadge.Value > 0 ? Visibility.Visible : Visibility.Collapsed;
            NotificationPanelClearAllGrid.Visibility = NotificationContainer.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            NotificationContainer.Children.Remove(Container);
        };
        Container.Children.Add(Notification);
        NotificationContainer.Children.Add(Container);
    }
    #endregion
}
