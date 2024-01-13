using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using Saku_Overclock.ViewModels;
using Windows.Foundation.Metadata;
using Windows.UI.Popups;
namespace Saku_Overclock.Views;
public sealed partial class ПараметрыPage : Page
{
    public ПараметрыViewModel ViewModel
    {
        get;
    }
    private Config config = new();
    private Devices devices = new();
    private Profile profile = new();
    public string adjline;
    private bool load = false;
    public ПараметрыPage()
    {
        ViewModel = App.GetService<ПараметрыViewModel>();
        InitializeComponent();
        DeviceLoad();
        ConfigLoad();
        ProfileLoad();
        SlidersInit();
        config.fanex = false;
        config.tempex = false;
        ConfigSave();
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

    public void ProfileSave()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json", JsonConvert.SerializeObject(profile));
        }
        catch { }
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
    public async void SlidersInit()
    {
        DeviceLoad();
        ProfileLoad();
        InitializeComponent();
        switch (profile.Preset)
        {
            case 0:
                ProfileCOM.SelectedIndex = 0;
                break;
            case 1:
                ProfileCOM.SelectedIndex = 1;
                break;
            case 2:
                ProfileCOM.SelectedIndex = 2;
                break;
            case 3:
                ProfileCOM.SelectedIndex = 3;
                break;
            case 4:
                ProfileCOM.SelectedIndex = 4;
                break;
            case 5:
                ProfileCOM.SelectedIndex = 5;
                break;
            case 6:
                ProfileCOM.SelectedIndex = 6;
                break;
            case 7:
                ProfileCOM.SelectedIndex = 7;
                break;
            case 8:
                ProfileCOM.SelectedIndex = 8;
                break;
            case 9:
                ProfileCOM.SelectedIndex = 9;
                break;
        }
            
        if (profile.Unsaved == true && ProfileCOM.SelectedIndex == 0)
        {
            ProfileCOM.SelectedIndex = 0;
            //Если выбран несохранённый профиль
            if (devices.c1 == true)
            {
                c1.IsChecked = true;
            }
            if (devices.c2 == true)
            {
                c2v.Value = devices.c2v;
                c2.IsChecked = true;
            }
            if (devices.c3 == true)
            {
                c3v.Value = devices.c3v;
                c3.IsChecked = true;

            }
            if (devices.c4 == true)
            {
                c4v.Value = devices.c4v;
                c4.IsChecked = true;

            }
            if (devices.c5 == true)
            {
                c5v.Value = devices.c5v;
                c5.IsChecked = true;

            }
            if (devices.c6 == true)
            {
                c6v.Value = devices.c6v;
                c6.IsChecked = true;

            }
            DeviceLoad();
            if (load == true)
            {
                if (devices.v1 == true)
                {
                    V1V.Value = devices.v1v;
                    V1.IsChecked = true;
                }

                if (devices.v2 == true)
                {
                    V2V.Value = devices.v2v;
                    V2.IsChecked = true;
                }
                if (devices.v3 == true)
                {
                    V3V.Value = devices.v3v;
                    V3.IsChecked = true;
                }
                if (devices.v4 == true)
                {
                    V4V.Value = devices.v4v;
                    V4.IsChecked = true;
                }
                if (devices.v5 == true)
                {
                    V5V.Value = devices.v5v;
                    V5.IsChecked = true;
                }
                if (devices.v6 == true)
                {
                    V6V.Value = devices.v6v;
                    V6.IsChecked = true;
                }
                if (devices.v7 == true)
                {
                    V7V.Value = devices.v7v;
                    V7.IsChecked = true;
                }

                if (devices.g1 == true)
                {
                    g1v.Value = devices.g1v;
                    g1.IsChecked = true;
                }
                if (devices.g2 == true)
                {
                    g2v.Value = devices.g2v;
                    g2.IsChecked = true;
                }
                if (devices.g3 == true)
                {
                    g3v.Value = devices.g3v;
                    g3.IsChecked = true;
                }
                if (devices.g4 == true)
                {
                    g4v.Value = devices.g4v;
                    g4.IsChecked = true;
                }
                if (devices.g5 == true)
                {
                    g5v.Value = devices.g5v;
                    g5.IsChecked = true;
                }
                if (devices.g6 == true)
                {
                    g6v.Value = devices.g6v;
                    g6.IsChecked = true;
                }
                if (devices.g7 == true)
                {
                    g7v.Value = devices.g7v;
                    g7.IsChecked = true;
                }
                if (devices.g8 == true)
                {
                    g8v.Value = devices.g8v;
                    g8.IsChecked = true;
                }
                if (devices.g9 == true)
                {
                    g9v.Value = devices.g9v;
                    g9.IsChecked = true;
                }
                if (devices.g10 == true)
                {
                    g10v.Value = devices.g10v;
                    g10.IsChecked = true;
                }
            }
            else
            {
                await Task.Delay(200);
                if (devices.v1 == true)
                {
                    V1V.Value = devices.v1v;
                    V1.IsChecked = true;
                }

                if (devices.v2 == true)
                {
                    V2V.Value = devices.v2v;
                    V2.IsChecked = true;
                }
                if (devices.v3 == true)
                {
                    V3V.Value = devices.v3v;
                    V3.IsChecked = true;
                }
                if (devices.v4 == true)
                {
                    V4V.Value = devices.v4v;
                    V4.IsChecked = true;
                }
                if (devices.v5 == true)
                {
                    V5V.Value = devices.v5v;
                    V5.IsChecked = true;
                }
                if (devices.v6 == true)
                {
                    V6V.Value = devices.v6v;
                    V6.IsChecked = true;
                }
                if (devices.v7 == true)
                {
                    V7V.Value = devices.v7v;
                    V7.IsChecked = true;
                }

                if (devices.g1 == true)
                {
                    g1v.Value = devices.g1v;
                    g1.IsChecked = true;
                }
                if (devices.g2 == true)
                {
                    g2v.Value = devices.g2v;
                    g2.IsChecked = true;
                }
                if (devices.g3 == true)
                {
                    g3v.Value = devices.g3v;
                    g3.IsChecked = true;
                }
                if (devices.g4 == true)
                {
                    g4v.Value = devices.g4v;
                    g4.IsChecked = true;
                }
                if (devices.g5 == true)
                {
                    g5v.Value = devices.g5v;
                    g5.IsChecked = true;
                }
                if (devices.g6 == true)
                {
                    g6v.Value = devices.g6v;
                    g6.IsChecked = true;
                }
                if (devices.g7 == true)
                {
                    g7v.Value = devices.g7v;
                    g7.IsChecked = true;
                }
                if (devices.g8 == true)
                {
                    g8v.Value = devices.g8v;
                    g8.IsChecked = true;
                }
                if (devices.g9 == true)
                {
                    g9v.Value = devices.g9v;
                    g9.IsChecked = true;
                }
                if (devices.g10 == true)
                {
                    g10v.Value = devices.g10v;
                    g10.IsChecked = true;
                }
            }
        }
        if (profile.pr1 == true)
        {
            ProfileCOM_1.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            ProfileCOM_1.Content = profile.pr1name;
            if (ProfileCOM.SelectedIndex == 1)
            {
                //Если выбран несохранённый профиль
                if (profile.c1pr1 == true) { c1.IsChecked = true; } else { c1.IsChecked = false; }
                if (profile.c2pr1 == true) { c2v.Value = profile.c2pr1v; c2.IsChecked = true; } else { c2.IsChecked = false; }
                if (profile.c3pr1 == true) { c3v.Value = profile.c3pr1v; c3.IsChecked = true; } else { c3.IsChecked = false; }
                if (profile.c4pr1 == true) { c4v.Value = profile.c4pr1v; c4.IsChecked = true; } else { c4.IsChecked = false; }
                if (profile.c5pr1 == true) { c5v.Value = profile.c5pr1v; c5.IsChecked = true; } else { c5.IsChecked = false; }
                if (profile.c6pr1 == true) { c6v.Value = profile.c6pr1v; c6.IsChecked = true; } else { c6.IsChecked = false; }
                DeviceLoad();
                if (load == true)
                {
                    if (profile.v1pr1 == true) { V1V.Value = profile.v1pr1v; V1.IsChecked = true; } else { V1.IsChecked = false; }
                    if (profile.v2pr1 == true) { V2V.Value = profile.v2pr1v; V2.IsChecked = true; } else { V2.IsChecked = false; }
                    if (profile.v3pr1 == true) { V3V.Value = profile.v3pr1v; V3.IsChecked = true; } else { V3.IsChecked = false; }
                    if (profile.v4pr1 == true) { V4V.Value = profile.v4pr1v; V4.IsChecked = true; } else { V4.IsChecked = false; }
                    if (profile.v5pr1 == true)
                    {
                        V5V.Value = profile.v5pr1v;
                        V5.IsChecked = true;
                    }
                    else { V5.IsChecked = false; }
                    if (profile.v6pr1 == true)
                    {
                        V6V.Value = profile.v6pr1v;
                        V6.IsChecked = true;
                    }
                    else { V6.IsChecked = false; }
                    if (profile.v7pr1 == true)
                    {
                        V7V.Value = profile.v7pr1v;
                        V7.IsChecked = true;
                    }
                    else { V7.IsChecked = false; }
                    if (profile.g1pr1 == true)
                    {
                        g1v.Value = profile.g1pr1v;
                        g1.IsChecked = true;
                    }
                    else { g1.IsChecked = false; }
                    if (profile.g2pr1 == true)
                    {
                        g2v.Value = profile.g2pr1v;
                        g2.IsChecked = true;
                    }
                    else { g2.IsChecked = false; }
                    if (profile.g3pr1 == true)
                    {
                        g3v.Value = profile.g3pr1v;
                        g3.IsChecked = true;
                    }
                    else { g3.IsChecked = false; }
                    if (profile.g4pr1 == true)
                    {
                        g4v.Value = profile.g4pr1v;
                        g4.IsChecked = true;
                    }
                    else { g4.IsChecked = false; }
                    if (profile.g5pr1 == true)
                    {
                        g5v.Value = profile.g5pr1v;
                        g5.IsChecked = true;
                    }
                    else { g5.IsChecked = false; }
                    if (profile.g6pr1 == true)
                    {
                        g6v.Value = profile.g6pr1v;
                        g6.IsChecked = true;
                    }
                    else { g6.IsChecked = false; }
                    if (profile.g7pr1 == true)
                    {
                        g7v.Value = profile.g7pr1v;
                        g7.IsChecked = true;
                    }
                    else { g7.IsChecked = false; }
                    if (profile.g8pr1 == true)
                    {
                        g8v.Value = profile.g8pr1v;
                        g8.IsChecked = true;
                    }
                    else { g8.IsChecked = false; }
                    if (profile.g9pr1 == true)
                    {
                        g9v.Value = profile.g9pr1v;
                        g9.IsChecked = true;
                    }
                    else { g9.IsChecked = false; }
                    if (profile.g10pr1 == true)
                    {
                        g10v.Value = profile.g10pr1v;
                        g10.IsChecked = true;
                    }
                    else { g10.IsChecked = false; }
                }
                else
                {
                    await Task.Delay(200);
                    if (profile.v1pr1 == true)
                    {
                        V1V.Value = profile.v1pr1v;
                        V1.IsChecked = true;
                    }
                    else { V1.IsChecked = false; }

                    if (profile.v2pr1 == true)
                    {
                        V2V.Value = profile.v2pr1v;
                        V2.IsChecked = true;
                    }
                    else { V2.IsChecked = false; }
                    if (profile.v3pr1 == true)
                    {
                        V3V.Value = profile.v3pr1v;
                        V3.IsChecked = true;
                    }
                    else { V3.IsChecked = false; }
                    if (profile.v4pr1 == true)
                    {
                        V4V.Value = profile.v4pr1v;
                        V4.IsChecked = true;
                    }
                    else { V4.IsChecked = false; }
                    if (profile.v5pr1 == true)
                    {
                        V5V.Value = profile.v5pr1v;
                        V5.IsChecked = true;
                    }
                    else { V5.IsChecked = false; }
                    if (profile.v6pr1 == true)
                    {
                        V6V.Value = profile.v6pr1v;
                        V6.IsChecked = true;
                    }
                    else { V6.IsChecked = false; }
                    if (profile.v7pr1 == true)
                    {
                        V7V.Value = profile.v7pr1v;
                        V7.IsChecked = true;
                    }
                    else { V7.IsChecked = false; }
                    if (profile.g1pr1 == true)
                    {
                        g1v.Value = profile.g1pr1v;
                        g1.IsChecked = true;
                    }
                    else { g1.IsChecked = false; }
                    if (profile.g2pr1 == true)
                    {
                        g2v.Value = profile.g2pr1v;
                        g2.IsChecked = true;
                    }
                    else { g2.IsChecked = false; }
                    if (profile.g3pr1 == true)
                    {
                        g3v.Value = profile.g3pr1v;
                        g3.IsChecked = true;
                    }
                    else { g3.IsChecked = false; }
                    if (profile.g4pr1 == true)
                    {
                        g4v.Value = profile.g4pr1v;
                        g4.IsChecked = true;
                    }
                    else { g4.IsChecked = false; }
                    if (profile.g5pr1 == true)
                    {
                        g5v.Value = profile.g5pr1v;
                        g5.IsChecked = true;
                    }
                    else { g5.IsChecked = false; }
                    if (profile.g6pr1 == true)
                    {
                        g6v.Value = profile.g6pr1v;
                        g6.IsChecked = true;
                    }
                    else { g6.IsChecked = false; }
                    if (profile.g7pr1 == true)
                    {
                        g7v.Value = profile.g7pr1v;
                        g7.IsChecked = true;
                    }
                    else { g7.IsChecked = false; }
                    if (profile.g8pr1 == true)
                    {
                        g8v.Value = profile.g8pr1v;
                        g8.IsChecked = true;
                    }
                    else { g8.IsChecked = false; }
                    if (profile.g9pr1 == true)
                    {
                        g9v.Value = profile.g9pr1v;
                        g9.IsChecked = true;
                    }
                    else { g9.IsChecked = false; }
                    if (profile.g10pr1 == true)
                    {
                        g10v.Value = profile.g10pr1v;
                        g10.IsChecked = true;
                    }
                    else { g10.IsChecked = false; }
                }
            }
        }
        if (profile.pr2 == true)
        {
            ProfileCOM_2.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            ProfileCOM_2.Content = profile.pr2name;
            //Если выбран несохранённый профиль
            if (ProfileCOM.SelectedIndex == 2)
            {
                if (profile.c1pr2 == true)
                {
                    c1.IsChecked = true;
                }
                else { c1.IsChecked = false; }
                if (profile.c2pr2 == true)
                {
                    c2v.Value = profile.c2pr2v;
                    c2.IsChecked = true;
                }
                else { c2.IsChecked = false; }
                if (profile.c3pr2 == true)
                {
                    c3v.Value = profile.c3pr2v;
                    c3.IsChecked = true;

                }
                else { c3.IsChecked = false; }
                if (profile.c4pr2 == true)
                {
                    c4v.Value = profile.c4pr2v;
                    c4.IsChecked = true;

                }
                else { c4.IsChecked = false; }
                if (profile.c5pr2 == true)
                {
                    c5v.Value = profile.c5pr2v;
                    c5.IsChecked = true;

                }
                else { c5.IsChecked = false; }
                if (profile.c6pr2 == true)
                {
                    c6v.Value = profile.c6pr2v;
                    c6.IsChecked = true;

                }
                else { c6.IsChecked = false; }
                DeviceLoad();
                if (load == true)
                {
                    if (profile.v1pr2 == true)
                    {
                        V1V.Value = profile.v1pr2v;
                        V1.IsChecked = true;
                    }
                    else { V1.IsChecked = false; }

                    if (profile.v2pr2 == true)
                    {
                        V2V.Value = profile.v2pr2v;
                        V2.IsChecked = true;
                    }
                    else { V2.IsChecked = false; }
                    if (profile.v3pr2 == true)
                    {
                        V3V.Value = profile.v3pr2v;
                        V3.IsChecked = true;
                    }
                    else { V3.IsChecked = false; }
                    if (profile.v4pr2 == true)
                    {
                        V4V.Value = profile.v4pr2v;
                        V4.IsChecked = true;
                    }
                    else { V4.IsChecked = false; }
                    if (profile.v5pr2 == true)
                    {
                        V5V.Value = profile.v5pr2v;
                        V5.IsChecked = true;
                    }
                    else { V5.IsChecked = false; }
                    if (profile.v6pr2 == true)
                    {
                        V6V.Value = profile.v6pr2v;
                        V6.IsChecked = true;
                    }
                    else { V6.IsChecked = false; }
                    if (profile.v7pr2 == true)
                    {
                        V7V.Value = profile.v7pr2v;
                        V7.IsChecked = true;
                    }
                    else { V7.IsChecked = false; }

                    if (profile.g1pr2 == true)
                    {
                        g1v.Value = profile.g1pr2v;
                        g1.IsChecked = true;
                    }
                    else { g1.IsChecked = false; }
                    if (profile.g2pr2 == true)
                    {
                        g2v.Value = profile.g2pr2v;
                        g2.IsChecked = true;
                    }
                    else { g2.IsChecked = false; }
                    if (profile.g3pr2 == true)
                    {
                        g3v.Value = profile.g3pr2v;
                        g3.IsChecked = true;
                    }
                    else { g3.IsChecked = false; }
                    if (profile.g4pr2 == true)
                    {
                        g4v.Value = profile.g4pr2v;
                        g4.IsChecked = true;
                    }
                    else { g4.IsChecked = false; }
                    if (profile.g5pr2 == true)
                    {
                        g5v.Value = profile.g5pr2v;
                        g5.IsChecked = true;
                    }
                    else { g5.IsChecked = false; }
                    if (profile.g6pr2 == true)
                    {
                        g6v.Value = profile.g6pr2v;
                        g6.IsChecked = true;
                    }
                    else { g6.IsChecked = false; }
                    if (profile.g7pr2 == true)
                    {
                        g7v.Value = profile.g7pr2v;
                        g7.IsChecked = true;
                    }
                    else { g7.IsChecked = false; }
                    if (profile.g8pr2 == true)
                    {
                        g8v.Value = profile.g8pr2v;
                        g8.IsChecked = true;
                    }
                    else { g8.IsChecked = false; }
                    if (profile.g9pr2 == true)
                    {
                        g9v.Value = profile.g9pr2v;
                        g9.IsChecked = true;
                    }
                    else { g9.IsChecked = false; }
                    if (profile.g10pr2 == true)
                    {
                        g10v.Value = profile.g10pr2v;
                        g10.IsChecked = true;
                    }
                    else { g10.IsChecked = false; }
                }
                else
                {
                    await Task.Delay(200);
                    if (profile.v1pr2 == true)
                    {
                        V1V.Value = profile.v1pr2v;
                        V1.IsChecked = true;
                    }
                    else { V1.IsChecked = false; }

                    if (profile.v2pr2 == true)
                    {
                        V2V.Value = profile.v2pr2v;
                        V2.IsChecked = true;
                    }
                    else { V2.IsChecked = false; }
                    if (profile.v3pr2 == true)
                    {
                        V3V.Value = profile.v3pr2v;
                        V3.IsChecked = true;
                    }
                    else { V3.IsChecked = false; }
                    if (profile.v4pr2 == true)
                    {
                        V4V.Value = profile.v4pr2v;
                        V4.IsChecked = true;
                    }
                    else { V4.IsChecked = false; }
                    if (profile.v5pr2 == true)
                    {
                        V5V.Value = profile.v5pr2v;
                        V5.IsChecked = true;
                    }
                    else { V5.IsChecked = false; }
                    if (profile.v6pr2 == true)
                    {
                        V6V.Value = profile.v6pr2v;
                        V6.IsChecked = true;
                    }
                    else { V6.IsChecked = false; }
                    if (profile.v7pr2 == true)
                    {
                        V7V.Value = profile.v7pr2v;
                        V7.IsChecked = true;
                    }
                    else { V7.IsChecked = false; }

                    if (profile.g1pr2 == true)
                    {
                        g1v.Value = profile.g1pr2v;
                        g1.IsChecked = true;
                    }
                    else { g1.IsChecked = false; }
                    if (profile.g2pr2 == true)
                    {
                        g2v.Value = profile.g2pr2v;
                        g2.IsChecked = true;
                    }
                    else { g2.IsChecked = false; }
                    if (profile.g3pr2 == true)
                    {
                        g3v.Value = profile.g3pr2v;
                        g3.IsChecked = true;
                    }
                    else { g3.IsChecked = false; }
                    if (profile.g4pr2 == true)
                    {
                        g4v.Value = profile.g4pr2v;
                        g4.IsChecked = true;
                    }
                    else { g4.IsChecked = false; }
                    if (profile.g5pr2 == true)
                    {
                        g5v.Value = profile.g5pr2v;
                        g5.IsChecked = true;
                    }
                    else { g5.IsChecked = false; }
                    if (profile.g6pr2 == true)
                    {
                        g6v.Value = profile.g6pr2v;
                        g6.IsChecked = true;
                    }
                    else { g6.IsChecked = false; }
                    if (profile.g7pr2 == true)
                    {
                        g7v.Value = profile.g7pr2v;
                        g7.IsChecked = true;
                    }
                    else { g7.IsChecked = false; }
                    if (profile.g8pr2 == true)
                    {
                        g8v.Value = profile.g8pr2v;
                        g8.IsChecked = true;
                    }
                    else { g8.IsChecked = false; }
                    if (profile.g9pr2 == true)
                    {
                        g9v.Value = profile.g9pr2v;
                        g9.IsChecked = true;
                    }
                    else { g9.IsChecked = false; }
                    if (profile.g10pr2 == true)
                    {
                        g10v.Value = profile.g10pr2v;
                        g10.IsChecked = true;
                    }
                    else { g10.IsChecked = false; }
                }
            }
        }
        if (profile.pr3 == true)
        {
            ProfileCOM_3.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            ProfileCOM_3.Content = profile.pr3name;
            if (ProfileCOM.SelectedIndex == 3)
            {

                if (profile.c1pr3 == true)
                {
                    c1.IsChecked = true;
                }
                else { c1.IsChecked = false; }
                if (profile.c2pr3 == true)
                {
                    c2v.Value = profile.c2pr3v;
                    c2.IsChecked = true;
                }
                else { c2.IsChecked = false; }
                if (profile.c3pr3 == true)
                {
                    c3v.Value = profile.c3pr3v;
                    c3.IsChecked = true;

                }
                else { c3.IsChecked = false; }
                if (profile.c4pr3 == true)
                {
                    c4v.Value = profile.c4pr3v;
                    c4.IsChecked = true;

                }
                else { c4.IsChecked = false; }
                if (profile.c5pr3 == true)
                {
                    c5v.Value = profile.c5pr3v;
                    c5.IsChecked = true;

                }
                else { c5.IsChecked = false; }
                if (profile.c6pr3 == true)
                {
                    c6v.Value = profile.c6pr3v;
                    c6.IsChecked = true;

                }
                else { c6.IsChecked = false; }
                DeviceLoad();
                if (load == true)
                {
                    if (profile.v1pr3 == true)
                    {
                        V1V.Value = profile.v1pr3v;
                        V1.IsChecked = true;
                    }
                    else { V1.IsChecked = false; }

                    if (profile.v2pr3 == true)
                    {
                        V2V.Value = profile.v2pr3v;
                        V2.IsChecked = true;
                    }
                    else { V2.IsChecked = false; }
                    if (profile.v3pr3 == true)
                    {
                        V3V.Value = profile.v3pr3v;
                        V3.IsChecked = true;
                    }
                    else { V3.IsChecked = false; }
                    if (profile.v4pr3 == true)
                    {
                        V4V.Value = profile.v4pr3v;
                        V4.IsChecked = true;
                    }
                    else { V4.IsChecked = false; }
                    if (profile.v5pr3 == true)
                    {
                        V5V.Value = profile.v5pr3v;
                        V5.IsChecked = true;
                    }
                    else { V5.IsChecked = false; }
                    if (profile.v6pr3 == true)
                    {
                        V6V.Value = profile.v6pr3v;
                        V6.IsChecked = true;
                    }
                    else { V6.IsChecked = false; }
                    if (profile.v7pr3 == true)
                    {
                        V7V.Value = profile.v7pr3v;
                        V7.IsChecked = true;
                    }
                    else { V7.IsChecked = false; }

                    if (profile.g1pr3 == true)
                    {
                        g1v.Value = profile.g1pr3v;
                        g1.IsChecked = true;
                    }
                    else { g1.IsChecked = false; }
                    if (profile.g2pr3 == true)
                    {
                        g2v.Value = profile.g2pr3v;
                        g2.IsChecked = true;
                    }
                    else { g2.IsChecked = false; }
                    if (profile.g3pr3 == true)
                    {
                        g3v.Value = profile.g3pr3v;
                        g3.IsChecked = true;
                    }
                    else { g3.IsChecked = false; }
                    if (profile.g4pr3 == true)
                    {
                        g4v.Value = profile.g4pr3v;
                        g4.IsChecked = true;
                    }
                    else { g4.IsChecked = false; }
                    if (profile.g5pr3 == true)
                    {
                        g5v.Value = profile.g5pr3v;
                        g5.IsChecked = true;
                    }
                    else { g5.IsChecked = false; }
                    if (profile.g6pr3 == true)
                    {
                        g6v.Value = profile.g6pr3v;
                        g6.IsChecked = true;
                    }
                    else { g6.IsChecked = false; }
                    if (profile.g7pr3 == true)
                    {
                        g7v.Value = profile.g7pr3v;
                        g7.IsChecked = true;
                    }
                    else { g7.IsChecked = false; }
                    if (profile.g8pr3 == true)
                    {
                        g8v.Value = profile.g8pr3v;
                        g8.IsChecked = true;
                    }
                    else { g8.IsChecked = false; }
                    if (profile.g9pr3 == true)
                    {
                        g9v.Value = profile.g9pr3v;
                        g9.IsChecked = true;
                    }
                    else { g9.IsChecked = false; }
                    if (profile.g10pr3 == true)
                    {
                        g10v.Value = profile.g10pr3v;
                        g10.IsChecked = true;
                    }
                    else { g10.IsChecked = false; }
                }
                else
                {
                    await Task.Delay(200);
                    if (profile.v1pr3 == true)
                    {
                        V1V.Value = profile.v1pr3v;
                        V1.IsChecked = true;
                    }
                    else { V1.IsChecked = false; }

                    if (profile.v2pr3 == true)
                    {
                        V2V.Value = profile.v2pr3v;
                        V2.IsChecked = true;
                    }
                    else { V2.IsChecked = false; }
                    if (profile.v3pr3 == true)
                    {
                        V3V.Value = profile.v3pr3v;
                        V3.IsChecked = true;
                    }
                    else { V3.IsChecked = false; }
                    if (profile.v4pr3 == true)
                    {
                        V4V.Value = profile.v4pr3v;
                        V4.IsChecked = true;
                    }
                    else { V4.IsChecked = false; }
                    if (profile.v5pr3 == true)
                    {
                        V5V.Value = profile.v5pr3v;
                        V5.IsChecked = true;
                    }
                    else { V5.IsChecked = false; }
                    if (profile.v6pr3 == true)
                    {
                        V6V.Value = profile.v6pr3v;
                        V6.IsChecked = true;
                    }
                    else { V6.IsChecked = false; }
                    if (profile.v7pr3 == true)
                    {
                        V7V.Value = profile.v7pr3v;
                        V7.IsChecked = true;
                    }
                    else { V7.IsChecked = false; }

                    if (profile.g1pr3 == true)
                    {
                        g1v.Value = profile.g1pr3v;
                        g1.IsChecked = true;
                    }
                    else { g1.IsChecked = false; }
                    if (profile.g2pr3 == true)
                    {
                        g2v.Value = profile.g2pr3v;
                        g2.IsChecked = true;
                    }
                    else { g2.IsChecked = false; }
                    if (profile.g3pr3 == true)
                    {
                        g3v.Value = profile.g3pr3v;
                        g3.IsChecked = true;
                    }
                    else { g3.IsChecked = false; }
                    if (profile.g4pr3 == true)
                    {
                        g4v.Value = profile.g4pr3v;
                        g4.IsChecked = true;
                    }
                    else { g4.IsChecked = false; }
                    if (profile.g5pr3 == true)
                    {
                        g5v.Value = profile.g5pr3v;
                        g5.IsChecked = true;
                    }
                    else { g5.IsChecked = false; }
                    if (profile.g6pr3 == true)
                    {
                        g6v.Value = profile.g6pr3v;
                        g6.IsChecked = true;
                    }
                    else { g6.IsChecked = false; }
                    if (profile.g7pr3 == true)
                    {
                        g7v.Value = profile.g7pr3v;
                        g7.IsChecked = true;
                    }
                    else { g7.IsChecked = false; }
                    if (profile.g8pr3 == true)
                    {
                        g8v.Value = profile.g8pr3v;
                        g8.IsChecked = true;
                    }
                    else { g8.IsChecked = false; }
                    if (profile.g9pr3 == true)
                    {
                        g9v.Value = profile.g9pr3v;
                        g9.IsChecked = true;
                    }
                    else { g9.IsChecked = false; }
                    if (profile.g10pr3 == true)
                    {
                        g10v.Value = profile.g10pr3v;
                        g10.IsChecked = true;
                    }
                    else { g10.IsChecked = false; }
                }
            }
        }
        if (profile.pr4 == true)
        {
            ProfileCOM_4.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            ProfileCOM_4.Content = profile.pr4name;
            if (ProfileCOM.SelectedIndex == 4)
            {

                if (profile.c1pr4 == true)
                {
                    c1.IsChecked = true;
                }
                else { c1.IsChecked = false; }
                if (profile.c2pr4 == true)
                {
                    c2v.Value = profile.c2pr4v;
                    c2.IsChecked = true;
                }
                else { c2.IsChecked = false; }
                if (profile.c3pr4 == true)
                {
                    c3v.Value = profile.c3pr4v;
                    c3.IsChecked = true;

                }
                else { c3.IsChecked = false; }
                if (profile.c4pr4 == true)
                {
                    c4v.Value = profile.c4pr4v;
                    c4.IsChecked = true;

                }
                else { c4.IsChecked = false; }
                if (profile.c5pr4 == true)
                {
                    c5v.Value = profile.c5pr4v;
                    c5.IsChecked = true;

                }
                else { c5.IsChecked = false; }
                if (profile.c6pr4 == true)
                {
                    c6v.Value = profile.c6pr4v;
                    c6.IsChecked = true;

                }
                else { c6.IsChecked = false; }
                DeviceLoad();
                if (load == true)
                {
                    if (profile.v1pr4 == true)
                    {
                        V1V.Value = profile.v1pr4v;
                        V1.IsChecked = true;
                    }
                    else { V1.IsChecked = false; }

                    if (profile.v2pr4 == true)
                    {
                        V2V.Value = profile.v2pr4v;
                        V2.IsChecked = true;
                    }
                    else { V2.IsChecked = false; }
                    if (profile.v3pr4 == true)
                    {
                        V3V.Value = profile.v3pr4v;
                        V3.IsChecked = true;
                    }
                    else { V3.IsChecked = false; }
                    if (profile.v4pr4 == true)
                    {
                        V4V.Value = profile.v4pr4v;
                        V4.IsChecked = true;
                    }
                    else { V4.IsChecked = false; }
                    if (profile.v5pr4 == true)
                    {
                        V5V.Value = profile.v5pr4v;
                        V5.IsChecked = true;
                    }
                    else { V5.IsChecked = false; }
                    if (profile.v6pr4 == true)
                    {
                        V6V.Value = profile.v6pr4v;
                        V6.IsChecked = true;
                    }
                    else { V6.IsChecked = false; }
                    if (profile.v7pr4 == true)
                    {
                        V7V.Value = profile.v7pr4v;
                        V7.IsChecked = true;
                    }
                    else { V7.IsChecked = false; }

                    if (profile.g1pr4 == true)
                    {
                        g1v.Value = profile.g1pr4v;
                        g1.IsChecked = true;
                    }
                    else { g1.IsChecked = false; }
                    if (profile.g2pr4 == true)
                    {
                        g2v.Value = profile.g2pr4v;
                        g2.IsChecked = true;
                    }
                    else { g2.IsChecked = false; }
                    if (profile.g3pr4 == true)
                    {
                        g3v.Value = profile.g3pr4v;
                        g3.IsChecked = true;
                    }
                    else { g3.IsChecked = false; }
                    if (profile.g4pr4 == true)
                    {
                        g4v.Value = profile.g4pr4v;
                        g4.IsChecked = true;
                    }
                    else { g4.IsChecked = false; }
                    if (profile.g5pr4 == true)
                    {
                        g5v.Value = profile.g5pr4v;
                        g5.IsChecked = true;
                    }
                    else { g5.IsChecked = false; }
                    if (profile.g6pr4 == true)
                    {
                        g6v.Value = profile.g6pr4v;
                        g6.IsChecked = true;
                    }
                    else { g6.IsChecked = false; }
                    if (profile.g7pr4 == true)
                    {
                        g7v.Value = profile.g7pr4v;
                        g7.IsChecked = true;
                    }
                    else { g7.IsChecked = false; }
                    if (profile.g8pr4 == true)
                    {
                        g8v.Value = profile.g8pr4v;
                        g8.IsChecked = true;
                    }
                    else { g8.IsChecked = false; }
                    if (profile.g9pr4 == true)
                    {
                        g9v.Value = profile.g9pr4v;
                        g9.IsChecked = true;
                    }
                    else { g9.IsChecked = false; }
                    if (profile.g10pr4 == true)
                    {
                        g10v.Value = profile.g10pr4v;
                        g10.IsChecked = true;
                    }
                    else { g10.IsChecked = false; }
                }
                else
                {
                    await Task.Delay(200);
                    if (profile.v1pr4 == true)
                    {
                        V1V.Value = profile.v1pr4v;
                        V1.IsChecked = true;
                    }
                    else { V1.IsChecked = false; }

                    if (profile.v2pr4 == true)
                    {
                        V2V.Value = profile.v2pr4v;
                        V2.IsChecked = true;
                    }
                    else { V2.IsChecked = false; }
                    if (profile.v3pr4 == true)
                    {
                        V3V.Value = profile.v3pr4v;
                        V3.IsChecked = true;
                    }
                    else { V3.IsChecked = false; }
                    if (profile.v4pr4 == true)
                    {
                        V4V.Value = profile.v4pr4v;
                        V4.IsChecked = true;
                    }
                    else { V4.IsChecked = false; }
                    if (profile.v5pr4 == true)
                    {
                        V5V.Value = profile.v5pr4v;
                        V5.IsChecked = true;
                    }
                    else { V5.IsChecked = false; }
                    if (profile.v6pr4 == true)
                    {
                        V6V.Value = profile.v6pr4v;
                        V6.IsChecked = true;
                    }
                    else { V6.IsChecked = false; }
                    if (profile.v7pr4 == true)
                    {
                        V7V.Value = profile.v7pr4v;
                        V7.IsChecked = true;
                    }
                    else { V7.IsChecked = false; }

                    if (profile.g1pr4 == true)
                    {
                        g1v.Value = profile.g1pr4v;
                        g1.IsChecked = true;
                    }
                    else { g1.IsChecked = false; }
                    if (profile.g2pr4 == true)
                    {
                        g2v.Value = profile.g2pr4v;
                        g2.IsChecked = true;
                    }
                    else { g2.IsChecked = false; }
                    if (profile.g3pr4 == true)
                    {
                        g3v.Value = profile.g3pr4v;
                        g3.IsChecked = true;
                    }
                    else { g3.IsChecked = false; }
                    if (profile.g4pr4 == true)
                    {
                        g4v.Value = profile.g4pr4v;
                        g4.IsChecked = true;
                    }
                    else { g4.IsChecked = false; }
                    if (profile.g5pr4 == true)
                    {
                        g5v.Value = profile.g5pr4v;
                        g5.IsChecked = true;
                    }
                    else { g5.IsChecked = false; }
                    if (profile.g6pr4 == true)
                    {
                        g6v.Value = profile.g6pr4v;
                        g6.IsChecked = true;
                    }
                    else { g6.IsChecked = false; }
                    if (profile.g7pr4 == true)
                    {
                        g7v.Value = profile.g7pr4v;
                        g7.IsChecked = true;
                    }
                    else { g7.IsChecked = false; }
                    if (profile.g8pr4 == true)
                    {
                        g8v.Value = profile.g8pr4v;
                        g8.IsChecked = true;
                    }
                    else { g8.IsChecked = false; }
                    if (profile.g9pr4 == true)
                    {
                        g9v.Value = profile.g9pr4v;
                        g9.IsChecked = true;
                    }
                    else { g9.IsChecked = false; }
                    if (profile.g10pr4 == true)
                    {
                        g10v.Value = profile.g10pr4v;
                        g10.IsChecked = true;
                    }
                    else { g10.IsChecked = false; }
                }
            }
        }
        if (profile.pr5 == true)
        {
            ProfileCOM_5.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            ProfileCOM_5.Content = profile.pr5name;
            if (ProfileCOM.SelectedIndex == 5)
            {

                if (profile.c1pr5 == true)
                {
                    c1.IsChecked = true;
                }
                else { c1.IsChecked = false; }
                if (profile.c2pr5 == true)
                {
                    c2v.Value = profile.c2pr5v;
                    c2.IsChecked = true;
                }
                else { c2.IsChecked = false; }
                if (profile.c3pr5 == true)
                {
                    c3v.Value = profile.c3pr5v;
                    c3.IsChecked = true;

                }
                else { c3.IsChecked = false; }
                if (profile.c4pr5 == true)
                {
                    c4v.Value = profile.c4pr5v;
                    c4.IsChecked = true;

                }
                else { c4.IsChecked = false; }
                if (profile.c5pr5 == true)
                {
                    c5v.Value = profile.c5pr5v;
                    c5.IsChecked = true;

                }
                else { c5.IsChecked = false; }
                if (profile.c6pr5 == true)
                {
                    c6v.Value = profile.c6pr5v;
                    c6.IsChecked = true;

                }
                else { c6.IsChecked = false; }
                DeviceLoad();
                if (load == true)
                {
                    if (profile.v1pr5 == true)
                    {
                        V1V.Value = profile.v1pr5v;
                        V1.IsChecked = true;
                    }
                    else { V1.IsChecked = false; }

                    if (profile.v2pr5 == true)
                    {
                        V2V.Value = profile.v2pr5v;
                        V2.IsChecked = true;
                    }
                    else { V2.IsChecked = false; }
                    if (profile.v3pr5 == true)
                    {
                        V3V.Value = profile.v3pr5v;
                        V3.IsChecked = true;
                    }
                    else { V3.IsChecked = false; }
                    if (profile.v4pr5 == true)
                    {
                        V4V.Value = profile.v4pr5v;
                        V4.IsChecked = true;
                    }
                    else { V4.IsChecked = false; }
                    if (profile.v5pr5 == true)
                    {
                        V5V.Value = profile.v5pr5v;
                        V5.IsChecked = true;
                    }
                    else { V5.IsChecked = false; }
                    if (profile.v6pr5 == true)
                    {
                        V6V.Value = profile.v6pr5v;
                        V6.IsChecked = true;
                    }
                    else { V6.IsChecked = false; }
                    if (profile.v7pr5 == true)
                    {
                        V7V.Value = profile.v7pr5v;
                        V7.IsChecked = true;
                    }
                    else { V7.IsChecked = false; }

                    if (profile.g1pr5 == true)
                    {
                        g1v.Value = profile.g1pr5v;
                        g1.IsChecked = true;
                    }
                    else { g1.IsChecked = false; }
                    if (profile.g2pr5 == true)
                    {
                        g2v.Value = profile.g2pr5v;
                        g2.IsChecked = true;
                    }
                    else { g2.IsChecked = false; }
                    if (profile.g3pr5 == true)
                    {
                        g3v.Value = profile.g3pr5v;
                        g3.IsChecked = true;
                    }
                    else { g3.IsChecked = false; }
                    if (profile.g4pr5 == true)
                    {
                        g4v.Value = profile.g4pr5v;
                        g4.IsChecked = true;
                    }
                    else { g4.IsChecked = false; }
                    if (profile.g5pr5 == true)
                    {
                        g5v.Value = profile.g5pr5v;
                        g5.IsChecked = true;
                    }
                    else { g5.IsChecked = false; }
                    if (profile.g6pr5 == true)
                    {
                        g6v.Value = profile.g6pr5v;
                        g6.IsChecked = true;
                    }
                    else { g6.IsChecked = false; }
                    if (profile.g7pr5 == true)
                    {
                        g7v.Value = profile.g7pr5v;
                        g7.IsChecked = true;
                    }
                    else { g7.IsChecked = false; }
                    if (profile.g8pr5 == true)
                    {
                        g8v.Value = profile.g8pr5v;
                        g8.IsChecked = true;
                    }
                    else { g8.IsChecked = false; }
                    if (profile.g9pr5 == true)
                    {
                        g9v.Value = profile.g9pr5v;
                        g9.IsChecked = true;
                    }
                    else { g9.IsChecked = false; }
                    if (profile.g10pr5 == true)
                    {
                        g10v.Value = profile.g10pr5v;
                        g10.IsChecked = true;
                    }
                    else { g10.IsChecked = false; }
                }
                else
                {
                    await Task.Delay(200);
                    if (profile.v1pr5 == true)
                    {
                        V1V.Value = profile.v1pr5v;
                        V1.IsChecked = true;
                    }
                    else { V1.IsChecked = false; }

                    if (profile.v2pr5 == true)
                    {
                        V2V.Value = profile.v2pr5v;
                        V2.IsChecked = true;
                    }
                    else { V2.IsChecked = false; }
                    if (profile.v3pr5 == true)
                    {
                        V3V.Value = profile.v3pr5v;
                        V3.IsChecked = true;
                    }
                    else { V3.IsChecked = false; }
                    if (profile.v4pr5 == true)
                    {
                        V4V.Value = profile.v4pr5v;
                        V4.IsChecked = true;
                    }
                    else { V4.IsChecked = false; }
                    if (profile.v5pr5 == true)
                    {
                        V5V.Value = profile.v5pr5v;
                        V5.IsChecked = true;
                    }
                    else { V5.IsChecked = false; }
                    if (profile.v6pr5 == true)
                    {
                        V6V.Value = profile.v6pr5v;
                        V6.IsChecked = true;
                    }
                    else { V6.IsChecked = false; }
                    if (profile.v7pr5 == true)
                    {
                        V7V.Value = profile.v7pr5v;
                        V7.IsChecked = true;
                    }
                    else { V7.IsChecked = false; }

                    if (profile.g1pr5 == true)
                    {
                        g1v.Value = profile.g1pr5v;
                        g1.IsChecked = true;
                    }
                    else { g1.IsChecked = false; }
                    if (profile.g2pr5 == true)
                    {
                        g2v.Value = profile.g2pr5v;
                        g2.IsChecked = true;
                    }
                    else { g2.IsChecked = false; }
                    if (profile.g3pr5 == true)
                    {
                        g3v.Value = profile.g3pr5v;
                        g3.IsChecked = true;
                    }
                    else { g3.IsChecked = false; }
                    if (profile.g4pr5 == true)
                    {
                        g4v.Value = profile.g4pr5v;
                        g4.IsChecked = true;
                    }
                    else { g4.IsChecked = false; }
                    if (profile.g5pr5 == true)
                    {
                        g5v.Value = profile.g5pr5v;
                        g5.IsChecked = true;
                    }
                    else { g5.IsChecked = false; }
                    if (profile.g6pr5 == true)
                    {
                        g6v.Value = profile.g6pr5v;
                        g6.IsChecked = true;
                    }
                    else { g6.IsChecked = false; }
                    if (profile.g7pr5 == true)
                    {
                        g7v.Value = profile.g7pr5v;
                        g7.IsChecked = true;
                    }
                    else { g7.IsChecked = false; }
                    if (profile.g8pr5 == true)
                    {
                        g8v.Value = profile.g8pr5v;
                        g8.IsChecked = true;
                    }
                    else { g8.IsChecked = false; }
                    if (profile.g9pr5 == true)
                    {
                        g9v.Value = profile.g9pr5v;
                        g9.IsChecked = true;
                    }
                    else { g9.IsChecked = false; }
                    if (profile.g10pr5 == true)
                    {
                        g10v.Value = profile.g10pr5v;
                        g10.IsChecked = true;
                    }
                    else { g10.IsChecked = false; }
                }
            }
        }
        if (profile.pr6 == true)
        {
            ProfileCOM_6.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            ProfileCOM_6.Content = profile.pr6name;
            if (ProfileCOM.SelectedIndex == 6)
            {

                if (profile.c1pr6 == true)
                {
                    c1.IsChecked = true;
                }
                else { c1.IsChecked = false; }
                if (profile.c2pr6 == true)
                {
                    c2v.Value = profile.c2pr6v;
                    c2.IsChecked = true;
                }
                else { c2.IsChecked = false; }
                if (profile.c3pr6 == true)
                {
                    c3v.Value = profile.c3pr6v;
                    c3.IsChecked = true;

                }
                else { c3.IsChecked = false; }
                if (profile.c4pr6 == true)
                {
                    c4v.Value = profile.c4pr6v;
                    c4.IsChecked = true;

                }
                else { c4.IsChecked = false; }
                if (profile.c5pr6 == true)
                {
                    c5v.Value = profile.c5pr6v;
                    c5.IsChecked = true;

                }
                else { c5.IsChecked = false; }
                if (profile.c6pr6 == true)
                {
                    c6v.Value = profile.c6pr6v;
                    c6.IsChecked = true;

                }
                else { c6.IsChecked = false; }
                DeviceLoad();
                if (load == true)
                {
                    if (profile.v1pr6 == true)
                    {
                        V1V.Value = profile.v1pr6v;
                        V1.IsChecked = true;
                    }
                    else { V1.IsChecked = false; }

                    if (profile.v2pr6 == true)
                    {
                        V2V.Value = profile.v2pr6v;
                        V2.IsChecked = true;
                    }
                    else { V2.IsChecked = false; }
                    if (profile.v3pr6 == true)
                    {
                        V3V.Value = profile.v3pr6v;
                        V3.IsChecked = true;
                    }
                    else { V3.IsChecked = false; }
                    if (profile.v4pr6 == true)
                    {
                        V4V.Value = profile.v4pr6v;
                        V4.IsChecked = true;
                    }
                    else { V4.IsChecked = false; }
                    if (profile.v5pr6 == true)
                    {
                        V5V.Value = profile.v5pr6v;
                        V5.IsChecked = true;
                    }
                    else { V5.IsChecked = false; }
                    if (profile.v6pr6 == true)
                    {
                        V6V.Value = profile.v6pr6v;
                        V6.IsChecked = true;
                    }
                    else { V6.IsChecked = false; }
                    if (profile.v7pr6 == true)
                    {
                        V7V.Value = profile.v7pr6v;
                        V7.IsChecked = true;
                    }
                    else { V7.IsChecked = false; }

                    if (profile.g1pr6 == true)
                    {
                        g1v.Value = profile.g1pr6v;
                        g1.IsChecked = true;
                    }
                    else { g1.IsChecked = false; }
                    if (profile.g2pr6 == true)
                    {
                        g2v.Value = profile.g2pr6v;
                        g2.IsChecked = true;
                    }
                    else { g2.IsChecked = false; }
                    if (profile.g3pr6 == true)
                    {
                        g3v.Value = profile.g3pr6v;
                        g3.IsChecked = true;
                    }
                    else { g3.IsChecked = false; }
                    if (profile.g4pr6 == true)
                    {
                        g4v.Value = profile.g4pr6v;
                        g4.IsChecked = true;
                    }
                    else { g4.IsChecked = false; }
                    if (profile.g5pr6 == true)
                    {
                        g5v.Value = profile.g5pr6v;
                        g5.IsChecked = true;
                    }
                    else { g5.IsChecked = false; }
                    if (profile.g6pr6 == true)
                    {
                        g6v.Value = profile.g6pr6v;
                        g6.IsChecked = true;
                    }
                    else { g6.IsChecked = false; }
                    if (profile.g7pr6 == true)
                    {
                        g7v.Value = profile.g7pr6v;
                        g7.IsChecked = true;
                    }
                    else { g7.IsChecked = false; }
                    if (profile.g8pr6 == true)
                    {
                        g8v.Value = profile.g8pr6v;
                        g8.IsChecked = true;
                    }
                    else { g8.IsChecked = false; }
                    if (profile.g9pr6 == true)
                    {
                        g9v.Value = profile.g9pr6v;
                        g9.IsChecked = true;
                    }
                    else { g9.IsChecked = false; }
                    if (profile.g10pr6 == true)
                    {
                        g10v.Value = profile.g10pr6v;
                        g10.IsChecked = true;
                    }
                    else { g10.IsChecked = false; }
                }
                else
                {
                    await Task.Delay(200);
                    if (profile.v1pr6 == true)
                    {
                        V1V.Value = profile.v1pr6v;
                        V1.IsChecked = true;
                    }
                    else { V1.IsChecked = false; }

                    if (profile.v2pr6 == true)
                    {
                        V2V.Value = profile.v2pr6v;
                        V2.IsChecked = true;
                    }
                    else { V2.IsChecked = false; }
                    if (profile.v3pr6 == true)
                    {
                        V3V.Value = profile.v3pr6v;
                        V3.IsChecked = true;
                    }
                    else { V3.IsChecked = false; }
                    if (profile.v4pr6 == true)
                    {
                        V4V.Value = profile.v4pr6v;
                        V4.IsChecked = true;
                    }
                    else { V4.IsChecked = false; }
                    if (profile.v5pr6 == true)
                    {
                        V5V.Value = profile.v5pr6v;
                        V5.IsChecked = true;
                    }
                    else { V5.IsChecked = false; }
                    if (profile.v6pr6 == true)
                    {
                        V6V.Value = profile.v6pr6v;
                        V6.IsChecked = true;
                    }
                    else { V6.IsChecked = false; }
                    if (profile.v7pr6 == true)
                    {
                        V7V.Value = profile.v7pr6v;
                        V7.IsChecked = true;
                    }
                    else { V7.IsChecked = false; }

                    if (profile.g1pr6 == true)
                    {
                        g1v.Value = profile.g1pr6v;
                        g1.IsChecked = true;
                    }
                    else { g1.IsChecked = false; }
                    if (profile.g2pr6 == true)
                    {
                        g2v.Value = profile.g2pr6v;
                        g2.IsChecked = true;
                    }
                    else { g2.IsChecked = false; }
                    if (profile.g3pr6 == true)
                    {
                        g3v.Value = profile.g3pr6v;
                        g3.IsChecked = true;
                    }
                    else { g3.IsChecked = false; }
                    if (profile.g4pr6 == true)
                    {
                        g4v.Value = profile.g4pr6v;
                        g4.IsChecked = true;
                    }
                    else { g4.IsChecked = false; }
                    if (profile.g5pr6 == true)
                    {
                        g5v.Value = profile.g5pr6v;
                        g5.IsChecked = true;
                    }
                    else { g5.IsChecked = false; }
                    if (profile.g6pr6 == true)
                    {
                        g6v.Value = profile.g6pr6v;
                        g6.IsChecked = true;
                    }
                    else { g6.IsChecked = false; }
                    if (profile.g7pr6 == true)
                    {
                        g7v.Value = profile.g7pr6v;
                        g7.IsChecked = true;
                    }
                    else { g7.IsChecked = false; }
                    if (profile.g8pr6 == true)
                    {
                        g8v.Value = profile.g8pr6v;
                        g8.IsChecked = true;
                    }
                    else { g8.IsChecked = false; }
                    if (profile.g9pr6 == true)
                    {
                        g9v.Value = profile.g9pr6v;
                        g9.IsChecked = true;
                    }
                    else { g9.IsChecked = false; }
                    if (profile.g10pr6 == true)
                    {
                        g10v.Value = profile.g10pr6v;
                        g10.IsChecked = true;
                    }
                    else { g10.IsChecked = false; }
                }
            }
        }
        if (profile.pr7 == true)
        {
            ProfileCOM_7.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            ProfileCOM_7.Content = profile.pr7name;
            if (ProfileCOM.SelectedIndex == 7)
            {

                if (profile.c1pr7 == true)
                {
                    c1.IsChecked = true;
                }
                else { c1.IsChecked = false; }
                if (profile.c2pr7 == true)
                {
                    c2v.Value = profile.c2pr7v;
                    c2.IsChecked = true;
                }
                else { c2.IsChecked = false; }
                if (profile.c3pr7 == true)
                {
                    c3v.Value = profile.c3pr7v;
                    c3.IsChecked = true;

                }
                else { c3.IsChecked = false; }
                if (profile.c4pr7 == true)
                {
                    c4v.Value = profile.c4pr7v;
                    c4.IsChecked = true;

                }
                else { c4.IsChecked = false; }
                if (profile.c5pr7 == true)
                {
                    c5v.Value = profile.c5pr7v;
                    c5.IsChecked = true;

                }
                else { c5.IsChecked = false; }
                if (profile.c6pr7 == true)
                {
                    c6v.Value = profile.c6pr7v;
                    c6.IsChecked = true;

                }
                else { c6.IsChecked = false; }
                DeviceLoad();
                if (load == true)
                {
                    if (profile.v1pr7 == true)
                    {
                        V1V.Value = profile.v1pr7v;
                        V1.IsChecked = true;
                    }
                    else { V1.IsChecked = false; }

                    if (profile.v2pr7 == true)
                    {
                        V2V.Value = profile.v2pr7v;
                        V2.IsChecked = true;
                    }
                    else { V2.IsChecked = false; }
                    if (profile.v3pr7 == true)
                    {
                        V3V.Value = profile.v3pr7v;
                        V3.IsChecked = true;
                    }
                    else { V3.IsChecked = false; }
                    if (profile.v4pr7 == true)
                    {
                        V4V.Value = profile.v4pr7v;
                        V4.IsChecked = true;
                    }
                    else { V4.IsChecked = false; }
                    if (profile.v5pr7 == true)
                    {
                        V5V.Value = profile.v5pr7v;
                        V5.IsChecked = true;
                    }
                    else { V5.IsChecked = false; }
                    if (profile.v6pr7 == true)
                    {
                        V6V.Value = profile.v6pr7v;
                        V6.IsChecked = true;
                    }
                    else { V6.IsChecked = false; }
                    if (profile.v7pr7 == true)
                    {
                        V7V.Value = profile.v7pr7v;
                        V7.IsChecked = true;
                    }
                    else { V7.IsChecked = false; }

                    if (profile.g1pr7 == true)
                    {
                        g1v.Value = profile.g1pr7v;
                        g1.IsChecked = true;
                    }
                    else { g1.IsChecked = false; }
                    if (profile.g2pr7 == true)
                    {
                        g2v.Value = profile.g2pr7v;
                        g2.IsChecked = true;
                    }
                    else { g2.IsChecked = false; }
                    if (profile.g3pr7 == true)
                    {
                        g3v.Value = profile.g3pr7v;
                        g3.IsChecked = true;
                    }
                    else { g3.IsChecked = false; }
                    if (profile.g4pr7 == true)
                    {
                        g4v.Value = profile.g4pr7v;
                        g4.IsChecked = true;
                    }
                    else { g4.IsChecked = false; }
                    if (profile.g5pr7 == true)
                    {
                        g5v.Value = profile.g5pr7v;
                        g5.IsChecked = true;
                    }
                    else { g5.IsChecked = false; }
                    if (profile.g6pr7 == true)
                    {
                        g6v.Value = profile.g6pr7v;
                        g6.IsChecked = true;
                    }
                    else { g6.IsChecked = false; }
                    if (profile.g7pr7 == true)
                    {
                        g7v.Value = profile.g7pr7v;
                        g7.IsChecked = true;
                    }
                    else { g7.IsChecked = false; }
                    if (profile.g8pr7 == true)
                    {
                        g8v.Value = profile.g8pr7v;
                        g8.IsChecked = true;
                    }
                    else { g8.IsChecked = false; }
                    if (profile.g9pr7 == true)
                    {
                        g9v.Value = profile.g9pr7v;
                        g9.IsChecked = true;
                    }
                    else { g9.IsChecked = false; }
                    if (profile.g10pr7 == true)
                    {
                        g10v.Value = profile.g10pr7v;
                        g10.IsChecked = true;
                    }
                    else { g10.IsChecked = false; }
                }
                else
                {
                    await Task.Delay(200);
                    if (profile.v1pr7 == true)
                    {
                        V1V.Value = profile.v1pr7v;
                        V1.IsChecked = true;
                    }
                    else { V1.IsChecked = false; }

                    if (profile.v2pr7 == true)
                    {
                        V2V.Value = profile.v2pr7v;
                        V2.IsChecked = true;
                    }
                    else { V2.IsChecked = false; }
                    if (profile.v3pr7 == true)
                    {
                        V3V.Value = profile.v3pr7v;
                        V3.IsChecked = true;
                    }
                    else { V3.IsChecked = false; }
                    if (profile.v4pr7 == true)
                    {
                        V4V.Value = profile.v4pr7v;
                        V4.IsChecked = true;
                    }
                    else { V4.IsChecked = false; }
                    if (profile.v5pr7 == true)
                    {
                        V5V.Value = profile.v5pr7v;
                        V5.IsChecked = true;
                    }
                    else { V5.IsChecked = false; }
                    if (profile.v6pr7 == true)
                    {
                        V6V.Value = profile.v6pr7v;
                        V6.IsChecked = true;
                    }
                    else { V6.IsChecked = false; }
                    if (profile.v7pr7 == true)
                    {
                        V7V.Value = profile.v7pr7v;
                        V7.IsChecked = true;
                    }
                    else { V7.IsChecked = false; }

                    if (profile.g1pr7 == true)
                    {
                        g1v.Value = profile.g1pr7v;
                        g1.IsChecked = true;
                    }
                    else { g1.IsChecked = false; }
                    if (profile.g2pr7 == true)
                    {
                        g2v.Value = profile.g2pr7v;
                        g2.IsChecked = true;
                    }
                    else { g2.IsChecked = false; }
                    if (profile.g3pr7 == true)
                    {
                        g3v.Value = profile.g3pr7v;
                        g3.IsChecked = true;
                    }
                    else { g3.IsChecked = false; }
                    if (profile.g4pr7 == true)
                    {
                        g4v.Value = profile.g4pr7v;
                        g4.IsChecked = true;
                    }
                    else { g4.IsChecked = false; }
                    if (profile.g5pr7 == true)
                    {
                        g5v.Value = profile.g5pr7v;
                        g5.IsChecked = true;
                    }
                    else { g5.IsChecked = false; }
                    if (profile.g6pr7 == true)
                    {
                        g6v.Value = profile.g6pr7v;
                        g6.IsChecked = true;
                    }
                    else { g6.IsChecked = false; }
                    if (profile.g7pr7 == true)
                    {
                        g7v.Value = profile.g7pr7v;
                        g7.IsChecked = true;
                    }
                    else { g7.IsChecked = false; }
                    if (profile.g8pr7 == true)
                    {
                        g8v.Value = profile.g8pr7v;
                        g8.IsChecked = true;
                    }
                    else { g8.IsChecked = false; }
                    if (profile.g9pr7 == true)
                    {
                        g9v.Value = profile.g9pr7v;
                        g9.IsChecked = true;
                    }
                    else { g9.IsChecked = false; }
                    if (profile.g10pr7 == true)
                    {
                        g10v.Value = profile.g10pr7v;
                        g10.IsChecked = true;
                    }
                    else { g10.IsChecked = false; }
                }
            }
        }
        if (profile.pr8 == true)
        {
            ProfileCOM_8.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            ProfileCOM_8.Content = profile.pr8name;
            if (ProfileCOM.SelectedIndex == 8)
            {

                if (profile.c1pr8 == true)
                {
                    c1.IsChecked = true;
                }
                else { c1.IsChecked = false; }
                if (profile.c2pr8 == true)
                {
                    c2v.Value = profile.c2pr8v;
                    c2.IsChecked = true;
                }
                else { c2.IsChecked = false; }
                if (profile.c3pr8 == true)
                {
                    c3v.Value = profile.c3pr8v;
                    c3.IsChecked = true;

                }
                else { c3.IsChecked = false; }
                if (profile.c4pr8 == true)
                {
                    c4v.Value = profile.c4pr8v;
                    c4.IsChecked = true;

                }
                else { c4.IsChecked = false; }
                if (profile.c5pr8 == true)
                {
                    c5v.Value = profile.c5pr8v;
                    c5.IsChecked = true;

                }
                else { c5.IsChecked = false; }
                if (profile.c6pr8 == true)
                {
                    c6v.Value = profile.c6pr8v;
                    c6.IsChecked = true;

                }
                else { c6.IsChecked = false; }
                DeviceLoad();
                if (load == true)
                {
                    if (profile.v1pr8 == true)
                    {
                        V1V.Value = profile.v1pr8v;
                        V1.IsChecked = true;
                    }
                    else { V1.IsChecked = false; }

                    if (profile.v2pr8 == true)
                    {
                        V2V.Value = profile.v2pr8v;
                        V2.IsChecked = true;
                    }
                    else { V2.IsChecked = false; }
                    if (profile.v3pr8 == true)
                    {
                        V3V.Value = profile.v3pr8v;
                        V3.IsChecked = true;
                    }
                    else { V3.IsChecked = false; }
                    if (profile.v4pr8 == true)
                    {
                        V4V.Value = profile.v4pr8v;
                        V4.IsChecked = true;
                    }
                    else { V4.IsChecked = false; }
                    if (profile.v5pr8 == true)
                    {
                        V5V.Value = profile.v5pr8v;
                        V5.IsChecked = true;
                    }
                    else { V5.IsChecked = false; }
                    if (profile.v6pr8 == true)
                    {
                        V6V.Value = profile.v6pr8v;
                        V6.IsChecked = true;
                    }
                    else { V6.IsChecked = false; }
                    if (profile.v7pr8 == true)
                    {
                        V7V.Value = profile.v7pr8v;
                        V7.IsChecked = true;
                    }
                    else { V7.IsChecked = false; }

                    if (profile.g1pr8 == true)
                    {
                        g1v.Value = profile.g1pr8v;
                        g1.IsChecked = true;
                    }
                    else { g1.IsChecked = false; }
                    if (profile.g2pr8 == true)
                    {
                        g2v.Value = profile.g2pr8v;
                        g2.IsChecked = true;
                    }
                    else { g2.IsChecked = false; }
                    if (profile.g3pr8 == true)
                    {
                        g3v.Value = profile.g3pr8v;
                        g3.IsChecked = true;
                    }
                    else { g3.IsChecked = false; }
                    if (profile.g4pr8 == true)
                    {
                        g4v.Value = profile.g4pr8v;
                        g4.IsChecked = true;
                    }
                    else { g4.IsChecked = false; }
                    if (profile.g5pr8 == true)
                    {
                        g5v.Value = profile.g5pr8v;
                        g5.IsChecked = true;
                    }
                    else { g5.IsChecked = false; }
                    if (profile.g6pr8 == true)
                    {
                        g6v.Value = profile.g6pr8v;
                        g6.IsChecked = true;
                    }
                    else { g6.IsChecked = false; }
                    if (profile.g7pr8 == true)
                    {
                        g7v.Value = profile.g7pr8v;
                        g7.IsChecked = true;
                    }
                    else { g7.IsChecked = false; }
                    if (profile.g8pr8 == true)
                    {
                        g8v.Value = profile.g8pr8v;
                        g8.IsChecked = true;
                    }
                    else { g8.IsChecked = false; }
                    if (profile.g9pr8 == true)
                    {
                        g9v.Value = profile.g9pr8v;
                        g9.IsChecked = true;
                    }
                    else { g9.IsChecked = false; }
                    if (profile.g10pr8 == true)
                    {
                        g10v.Value = profile.g10pr8v;
                        g10.IsChecked = true;
                    }
                    else { g10.IsChecked = false; }
                }
                else
                {
                    await Task.Delay(200);
                    if (profile.v1pr8 == true)
                    {
                        V1V.Value = profile.v1pr8v;
                        V1.IsChecked = true;
                    }
                    else { V1.IsChecked = false; }

                    if (profile.v2pr8 == true)
                    {
                        V2V.Value = profile.v2pr8v;
                        V2.IsChecked = true;
                    }
                    else { V2.IsChecked = false; }
                    if (profile.v3pr8 == true)
                    {
                        V3V.Value = profile.v3pr8v;
                        V3.IsChecked = true;
                    }
                    else { V3.IsChecked = false; }
                    if (profile.v4pr8 == true)
                    {
                        V4V.Value = profile.v4pr8v;
                        V4.IsChecked = true;
                    }
                    else { V4.IsChecked = false; }
                    if (profile.v5pr8 == true)
                    {
                        V5V.Value = profile.v5pr8v;
                        V5.IsChecked = true;
                    }
                    else { V5.IsChecked = false; }
                    if (profile.v6pr8 == true)
                    {
                        V6V.Value = profile.v6pr8v;
                        V6.IsChecked = true;
                    }
                    else { V6.IsChecked = false; }
                    if (profile.v7pr8 == true)
                    {
                        V7V.Value = profile.v7pr8v;
                        V7.IsChecked = true;
                    }
                    else { V7.IsChecked = false; }

                    if (profile.g1pr8 == true)
                    {
                        g1v.Value = profile.g1pr8v;
                        g1.IsChecked = true;
                    }
                    else { g1.IsChecked = false; }
                    if (profile.g2pr8 == true)
                    {
                        g2v.Value = profile.g2pr8v;
                        g2.IsChecked = true;
                    }
                    else { g2.IsChecked = false; }
                    if (profile.g3pr8 == true)
                    {
                        g3v.Value = profile.g3pr8v;
                        g3.IsChecked = true;
                    }
                    else { g3.IsChecked = false; }
                    if (profile.g4pr8 == true)
                    {
                        g4v.Value = profile.g4pr8v;
                        g4.IsChecked = true;
                    }
                    else { g4.IsChecked = false; }
                    if (profile.g5pr8 == true)
                    {
                        g5v.Value = profile.g5pr8v;
                        g5.IsChecked = true;
                    }
                    else { g5.IsChecked = false; }
                    if (profile.g6pr8 == true)
                    {
                        g6v.Value = profile.g6pr8v;
                        g6.IsChecked = true;
                    }
                    else { g6.IsChecked = false; }
                    if (profile.g7pr8 == true)
                    {
                        g7v.Value = profile.g7pr8v;
                        g7.IsChecked = true;
                    }
                    else { g7.IsChecked = false; }
                    if (profile.g8pr8 == true)
                    {
                        g8v.Value = profile.g8pr8v;
                        g8.IsChecked = true;
                    }
                    else { g8.IsChecked = false; }
                    if (profile.g9pr8 == true)
                    {
                        g9v.Value = profile.g9pr8v;
                        g9.IsChecked = true;
                    }
                    else { g9.IsChecked = false; }
                    if (profile.g10pr8 == true)
                    {
                        g10v.Value = profile.g10pr8v;
                        g10.IsChecked = true;
                    }
                    else { g10.IsChecked = false; }
                }
            }
        }
        if (profile.pr9 == true)
        {
            ProfileCOM_9.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            ProfileCOM_9.Content = profile.pr9name;
            if (ProfileCOM.SelectedIndex == 9)
            {

                if (profile.c1pr9 == true)
                {
                    c1.IsChecked = true;
                }
                else { c1.IsChecked = false; }
                if (profile.c2pr9 == true)
                {
                    c2v.Value = profile.c2pr9v;
                    c2.IsChecked = true;
                }
                else { c2.IsChecked = false; }
                if (profile.c3pr9 == true)
                {
                    c3v.Value = profile.c3pr9v;
                    c3.IsChecked = true;

                }
                else { c3.IsChecked = false; }
                if (profile.c4pr9 == true)
                {
                    c4v.Value = profile.c4pr9v;
                    c4.IsChecked = true;

                }
                else { c4.IsChecked = false; }
                if (profile.c5pr9 == true)
                {
                    c5v.Value = profile.c5pr9v;
                    c5.IsChecked = true;

                }
                else { c5.IsChecked = false; }
                if (profile.c6pr9 == true)
                {
                    c6v.Value = profile.c6pr9v;
                    c6.IsChecked = true;

                }
                else { c6.IsChecked = false; }
                DeviceLoad();
                if (load == true)
                {
                    if (profile.v1pr9 == true)
                    {
                        V1V.Value = profile.v1pr9v;
                        V1.IsChecked = true;
                    }
                    else { V1.IsChecked = false; }

                    if (profile.v2pr9 == true)
                    {
                        V2V.Value = profile.v2pr9v;
                        V2.IsChecked = true;
                    }
                    else { V2.IsChecked = false; }
                    if (profile.v3pr9 == true)
                    {
                        V3V.Value = profile.v3pr9v;
                        V3.IsChecked = true;
                    }
                    else { V3.IsChecked = false; }
                    if (profile.v4pr9 == true)
                    {
                        V4V.Value = profile.v4pr9v;
                        V4.IsChecked = true;
                    }
                    else { V4.IsChecked = false; }
                    if (profile.v5pr9 == true)
                    {
                        V5V.Value = profile.v5pr9v;
                        V5.IsChecked = true;
                    }
                    else { V5.IsChecked = false; }
                    if (profile.v6pr9 == true)
                    {
                        V6V.Value = profile.v6pr9v;
                        V6.IsChecked = true;
                    }
                    else { V6.IsChecked = false; }
                    if (profile.v7pr9 == true)
                    {
                        V7V.Value = profile.v7pr9v;
                        V7.IsChecked = true;
                    }
                    else { V7.IsChecked = false; }

                    if (profile.g1pr9 == true)
                    {
                        g1v.Value = profile.g1pr9v;
                        g1.IsChecked = true;
                    }
                    else { g1.IsChecked = false; }
                    if (profile.g2pr9 == true)
                    {
                        g2v.Value = profile.g2pr9v;
                        g2.IsChecked = true;
                    }
                    else { g2.IsChecked = false; }
                    if (profile.g3pr9 == true)
                    {
                        g3v.Value = profile.g3pr9v;
                        g3.IsChecked = true;
                    }
                    else { g3.IsChecked = false; }
                    if (profile.g4pr9 == true)
                    {
                        g4v.Value = profile.g4pr9v;
                        g4.IsChecked = true;
                    }
                    else { g4.IsChecked = false; }
                    if (profile.g5pr9 == true)
                    {
                        g5v.Value = profile.g5pr9v;
                        g5.IsChecked = true;
                    }
                    else { g5.IsChecked = false; }
                    if (profile.g6pr9 == true)
                    {
                        g6v.Value = profile.g6pr9v;
                        g6.IsChecked = true;
                    }
                    else { g6.IsChecked = false; }
                    if (profile.g7pr9 == true)
                    {
                        g7v.Value = profile.g7pr9v;
                        g7.IsChecked = true;
                    }
                    else { g7.IsChecked = false; }
                    if (profile.g8pr9 == true)
                    {
                        g8v.Value = profile.g8pr9v;
                        g8.IsChecked = true;
                    }
                    else { g8.IsChecked = false; }
                    if (profile.g9pr9 == true)
                    {
                        g9v.Value = profile.g9pr9v;
                        g9.IsChecked = true;
                    }
                    else { g9.IsChecked = false; }
                    if (profile.g10pr9 == true)
                    {
                        g10v.Value = profile.g10pr9v;
                        g10.IsChecked = true;
                    }
                    else { g10.IsChecked = false; }
                }
                else
                {
                    await Task.Delay(200);
                    if (profile.v1pr9 == true)
                    {
                        V1V.Value = profile.v1pr9v;
                        V1.IsChecked = true;
                    }
                    else { V1.IsChecked = false; }

                    if (profile.v2pr9 == true)
                    {
                        V2V.Value = profile.v2pr9v;
                        V2.IsChecked = true;
                    }
                    else { V2.IsChecked = false; }
                    if (profile.v3pr9 == true)
                    {
                        V3V.Value = profile.v3pr9v;
                        V3.IsChecked = true;
                    }
                    else { V3.IsChecked = false; }
                    if (profile.v4pr9 == true)
                    {
                        V4V.Value = profile.v4pr9v;
                        V4.IsChecked = true;
                    }
                    else { V4.IsChecked = false; }
                    if (profile.v5pr9 == true)
                    {
                        V5V.Value = profile.v5pr9v;
                        V5.IsChecked = true;
                    }
                    else { V5.IsChecked = false; }
                    if (profile.v6pr9 == true)
                    {
                        V6V.Value = profile.v6pr9v;
                        V6.IsChecked = true;
                    }
                    else { V6.IsChecked = false; }
                    if (profile.v7pr9 == true)
                    {
                        V7V.Value = profile.v7pr9v;
                        V7.IsChecked = true;
                    }
                    else { V7.IsChecked = false; }

                    if (profile.g1pr9 == true)
                    {
                        g1v.Value = profile.g1pr9v;
                        g1.IsChecked = true;
                    }
                    else { g1.IsChecked = false; }
                    if (profile.g2pr9 == true)
                    {
                        g2v.Value = profile.g2pr9v;
                        g2.IsChecked = true;
                    }
                    else { g2.IsChecked = false; }
                    if (profile.g3pr9 == true)
                    {
                        g3v.Value = profile.g3pr9v;
                        g3.IsChecked = true;
                    }
                    else { g3.IsChecked = false; }
                    if (profile.g4pr9 == true)
                    {
                        g4v.Value = profile.g4pr9v;
                        g4.IsChecked = true;
                    }
                    else { g4.IsChecked = false; }
                    if (profile.g5pr9 == true)
                    {
                        g5v.Value = profile.g5pr9v;
                        g5.IsChecked = true;
                    }
                    else { g5.IsChecked = false; }
                    if (profile.g6pr9 == true)
                    {
                        g6v.Value = profile.g6pr9v;
                        g6.IsChecked = true;
                    }
                    else { g6.IsChecked = false; }
                    if (profile.g7pr9 == true)
                    {
                        g7v.Value = profile.g7pr9v;
                        g7.IsChecked = true;
                    }
                    else { g7.IsChecked = false; }
                    if (profile.g8pr9 == true)
                    {
                        g8v.Value = profile.g8pr9v;
                        g8.IsChecked = true;
                    }
                    else { g8.IsChecked = false; }
                    if (profile.g9pr9 == true)
                    {
                        g9v.Value = profile.g9pr9v;
                        g9.IsChecked = true;
                    }
                    else { g9.IsChecked = false; }
                    if (profile.g10pr9 == true)
                    {
                        g10v.Value = profile.g10pr9v;
                        g10.IsChecked = true;
                    }
                    else { g10.IsChecked = false; }
                }
            }
        }
    }
    private async void ProfileCOM_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await Task.Delay(100);
        switch (ProfileCOM.SelectedIndex)
        {
            case 0:
                profile.Unsaved = true; profile.Preset = 0;
                ProfileSave();
                break;
            case 1:
                profile.Unsaved = false; profile.Preset = 1;
                ProfileSave();
                break;
            case 2:
                profile.Unsaved = false; profile.Preset = 2;
                ProfileSave();
                break;
            case 3:
                profile.Unsaved = false; profile.Preset = 3;
                ProfileSave();
                break;
            case 4:
                profile.Unsaved = false; profile.Preset = 4;
                ProfileSave();
                break;
            case 5:
                profile.Unsaved = false; profile.Preset = 5;
                ProfileSave();
                break;
            case 6:
                profile.Unsaved = false; profile.Preset = 6;
                ProfileSave();
                break;
            case 7:
                profile.Unsaved = false; profile.Preset = 7;
                ProfileSave();
                break;
            case 8:
                profile.Unsaved = false; profile.Preset = 8;
                ProfileSave();
                break;
            case 9:
                profile.Unsaved = false; profile.Preset = 9;
                ProfileSave();
                break;
        }
        SlidersInit();
    }
    //Параметры процессора
    //Максимальная температура CPU (C)
    private void c1_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (c1.IsChecked == true)
        {
            devices.c1 = true;
            devices.c1v = c1v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.c1pr1 = true;
                    profile.c1pr1v = c1v.Value;
                    break;
                case 2:
                    profile.c1pr2 = true;
                    profile.c1pr2v = c1v.Value;
                    break;
                case 3:
                    profile.c1pr3 = true;
                    profile.c1pr3v = c1v.Value;
                    break;
                case 4:
                    profile.c1pr4 = true;
                    profile.c1pr4v = c1v.Value;
                    break;
                case 5:
                    profile.c1pr5 = true;
                    profile.c1pr5v = c1v.Value;
                    break;
                case 6:
                    profile.c1pr6 = true;
                    profile.c1pr6v = c1v.Value;
                    break;
                case 7:
                    profile.c1pr7 = true;
                    profile.c1pr7v = c1v.Value;
                    break;
                case 8:
                    profile.c1pr8 = true;
                    profile.c1pr8v = c1v.Value;
                    break;
                case 9:
                    profile.c1pr9 = true;
                    profile.c1pr9v = c1v.Value;
                    break;
            }
            ProfileSave();
        }
        else
        {
            devices.c1 = false;
            devices.c1v = 90;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.c1pr1 = false;
                    break;
                case 2:
                    profile.c1pr2 = false;
                    break;
                case 3:
                    profile.c1pr3 = false;
                    break;
                case 4:
                    profile.c1pr4 = false;
                    break;
                case 5:
                    profile.c1pr5 = false;
                    break;
                case 6:
                    profile.c1pr6 = false;
                    break;
                case 7:
                    profile.c1pr7 = false;
                    break;
                case 8:
                    profile.c1pr8 = false;
                    break;
                case 9:
                    profile.c1pr9 = false;
                    break;
            }
            ProfileSave();
        }
    }
    //Лимит CPU (W)
    private void c2_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (c2.IsChecked == true)
        {
            devices.c2 = true;
            devices.c2v = c2v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.c2pr1 = true;
                    profile.c2pr1v = c2v.Value;
                    break;
                case 2:
                    profile.c2pr2 = true;
                    profile.c2pr2v = c2v.Value;
                    break;
                case 3:
                    profile.c2pr3 = true;
                    profile.c2pr3v = c2v.Value;
                    break;
                case 4:
                    profile.c2pr4 = true;
                    profile.c2pr4v = c2v.Value;
                    break;
                case 5:
                    profile.c2pr5 = true;
                    profile.c2pr5v = c2v.Value;
                    break;
                case 6:
                    profile.c2pr6 = true;
                    profile.c2pr6v = c2v.Value;
                    break;
                case 7:
                    profile.c2pr7 = true;
                    profile.c2pr7v = c2v.Value;
                    break;
                case 8:
                    profile.c2pr8 = true;
                    profile.c2pr8v = c2v.Value;
                    break;
                case 9:
                    profile.c2pr9 = true;
                    profile.c2pr9v = c2v.Value;
                    break;
            }
            ProfileSave();
        }
        else
        {
            devices.c2 = false;
            devices.c2v = 20;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.c2pr1 = false;
                    break;
                case 2:
                    profile.c2pr2 = false;
                    break;
                case 3:
                    profile.c2pr3 = false;
                    break;
                case 4:
                    profile.c2pr4 = false;
                    break;
                case 5:
                    profile.c2pr5 = false;
                    break;
                case 6:
                    profile.c2pr6 = false;
                    break;
                case 7:
                    profile.c2pr7 = false;
                    break;
                case 8:
                    profile.c2pr8 = false;
                    break;
                case 9:
                    profile.c2pr9 = false;
                    break;
            }
            ProfileSave();
        }
    }
    //Реальный CPU (W)
    private void c3_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (c3.IsChecked == true)
        {
            devices.c3 = true;
            devices.c3v = c3v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.c3pr1 = true;
                    profile.c3pr1v = c3v.Value;
                    break;
                case 2:
                    profile.c3pr2 = true;
                    profile.c3pr2v = c3v.Value;
                    break;
                case 3:
                    profile.c3pr3 = true;
                    profile.c3pr3v = c3v.Value;
                    break;
                case 4:
                    profile.c3pr4 = true;
                    profile.c3pr4v = c3v.Value;
                    break;
                case 5:
                    profile.c3pr5 = true;
                    profile.c3pr5v = c3v.Value;
                    break;
                case 6:
                    profile.c3pr6 = true;
                    profile.c3pr6v = c3v.Value;
                    break;
                case 7:
                    profile.c3pr7 = true;
                    profile.c3pr7v = c3v.Value;
                    break;
                case 8:
                    profile.c3pr8 = true;
                    profile.c3pr8v = c3v.Value;
                    break;
                case 9:
                    profile.c3pr9 = true;
                    profile.c3pr9v = c3v.Value;
                    break;
            }
            ProfileSave();
        }
        else
        {
            devices.c3 = false;
            devices.c3v = 25;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.c3pr1 = false;
                    break;
                case 2:
                    profile.c3pr2 = false;
                    break;
                case 3:
                    profile.c3pr3 = false;
                    break;
                case 4:
                    profile.c3pr4 = false;
                    break;
                case 5:
                    profile.c3pr5 = false;
                    break;
                case 6:
                    profile.c3pr6 = false;
                    break;
                case 7:
                    profile.c3pr7 = false;
                    break;
                case 8:
                    profile.c3pr8 = false;
                    break;
                case 9:
                    profile.c3pr9 = false;
                    break;
            }
            ProfileSave();
        }
    }
    //Средний CPU (W)
    private void c4_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (c4.IsChecked == true)
        {
            devices.c4 = true;
            devices.c4v = c4v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.c4pr1 = true;
                    profile.c4pr1v = c4v.Value;
                    break;
                case 2:
                    profile.c4pr2 = true;
                    profile.c4pr2v = c4v.Value;
                    break;
                case 3:
                    profile.c4pr3 = true;
                    profile.c4pr3v = c4v.Value;
                    break;
                case 4:
                    profile.c4pr4 = true;
                    profile.c4pr4v = c4v.Value;
                    break;
                case 5:
                    profile.c4pr5 = true;
                    profile.c4pr5v = c4v.Value;
                    break;
                case 6:
                    profile.c4pr6 = true;
                    profile.c4pr6v = c4v.Value;
                    break;
                case 7:
                    profile.c4pr7 = true;
                    profile.c4pr7v = c4v.Value;
                    break;
                case 8:
                    profile.c4pr8 = true;
                    profile.c4pr8v = c4v.Value;
                    break;
                case 9:
                    profile.c4pr9 = true;
                    profile.c4pr9v = c4v.Value;
                    break;
            }
            ProfileSave();
        }
        else
        {
            devices.c4 = false;
            devices.c4v = 25;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.c4pr1 = false;
                    break;
                case 2:
                    profile.c4pr2 = false;
                    break;
                case 3:
                    profile.c4pr3 = false;
                    break;
                case 4:
                    profile.c4pr4 = false;
                    break;
                case 5:
                    profile.c4pr5 = false;
                    break;
                case 6:
                    profile.c4pr6 = false;
                    break;
                case 7:
                    profile.c4pr7 = false;
                    break;
                case 8:
                    profile.c4pr8 = false;
                    break;
                case 9:
                    profile.c4pr9 = false;
                    break;
            }
            ProfileSave();
        }
    }
    //Тик быстрого разгона (S)
    private void c5_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (c5.IsChecked == true)
        {
            devices.c5 = true;
            devices.c5v = c5v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.c5pr1 = true;
                    profile.c5pr1v = c5v.Value;
                    break;
                case 2:
                    profile.c5pr2 = true;
                    profile.c5pr2v = c5v.Value;
                    break;
                case 3:
                    profile.c5pr3 = true;
                    profile.c5pr3v = c5v.Value;
                    break;
                case 4:
                    profile.c5pr4 = true;
                    profile.c5pr4v = c5v.Value;
                    break;
                case 5:
                    profile.c5pr5 = true;
                    profile.c5pr5v = c5v.Value;
                    break;
                case 6:
                    profile.c5pr6 = true;
                    profile.c5pr6v = c5v.Value;
                    break;
                case 7:
                    profile.c5pr7 = true;
                    profile.c5pr7v = c5v.Value;
                    break;
                case 8:
                    profile.c5pr8 = true;
                    profile.c5pr8v = c5v.Value;
                    break;
                case 9:
                    profile.c5pr9 = true;
                    profile.c5pr9v = c5v.Value;
                    break;
            }
            ProfileSave();
        }
        else
        {
            devices.c5 = false;
            devices.c5v = 128;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.c5pr1 = false;
                    break;
                case 2:
                    profile.c5pr2 = false;
                    break;
                case 3:
                    profile.c5pr3 = false;
                    break;
                case 4:
                    profile.c5pr4 = false;
                    break;
                case 5:
                    profile.c5pr5 = false;
                    break;
                case 6:
                    profile.c5pr6 = false;
                    break;
                case 7:
                    profile.c5pr7 = false;
                    break;
                case 8:
                    profile.c5pr8 = false;
                    break;
                case 9:
                    profile.c5pr9 = false;
                    break;
            }
            ProfileSave();
        }
    }
    //Тик медленного разгона (S)
    private void c6_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (c6.IsChecked == true)
        {
            devices.c6 = true;
            devices.c6v = c6v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.c6pr1 = true;
                    profile.c6pr1v = c6v.Value;
                    break;
                case 2:
                    profile.c6pr2 = true;
                    profile.c6pr2v = c6v.Value;
                    break;
                case 3:
                    profile.c6pr3 = true;
                    profile.c6pr3v = c6v.Value;
                    break;
                case 4:
                    profile.c6pr4 = true;
                    profile.c6pr4v = c6v.Value;
                    break;
                case 5:
                    profile.c6pr5 = true;
                    profile.c6pr5v = c6v.Value;
                    break;
                case 6:
                    profile.c6pr6 = true;
                    profile.c6pr6v = c6v.Value;
                    break;
                case 7:
                    profile.c6pr7 = true;
                    profile.c6pr7v = c6v.Value;
                    break;
                case 8:
                    profile.c6pr8 = true;
                    profile.c6pr8v = c6v.Value;
                    break;
                case 9:
                    profile.c6pr9 = true;
                    profile.c6pr9v = c6v.Value;
                    break;
            }
            ProfileSave();
        }
        else
        {
            devices.c6 = false;
            devices.c6v = 64;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.c6pr1 = false;
                    break;
                case 2:
                    profile.c6pr2 = false;
                    break;
                case 3:
                    profile.c6pr3 = false;
                    break;
                case 4:
                    profile.c6pr4 = false;
                    break;
                case 5:
                    profile.c6pr5 = false;
                    break;
                case 6:
                    profile.c6pr6 = false;
                    break;
                case 7:
                    profile.c6pr7 = false;
                    break;
                case 8:
                    profile.c6pr8 = false;
                    break;
                case 9:
                    profile.c6pr9 = false;
                    break;
            }
            ProfileSave();
        }
    }
    //Параметры VRM
    //Максимальный ток VRM A
    private void v1_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (V1.IsChecked == true)
        {
            devices.v1 = true;
            devices.v1v = V1V.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.v1pr1 = true;
                    profile.v1pr1v = V1V.Value;
                    break;
                case 2:
                    profile.v1pr2 = true;
                    profile.v1pr2v = V1V.Value;
                    break;
                case 3:
                    profile.v1pr3 = true;
                    profile.v1pr3v = V1V.Value;
                    break;
                case 4:
                    profile.v1pr4 = true;
                    profile.v1pr4v = V1V.Value;
                    break;
                case 5:
                    profile.v1pr5 = true;
                    profile.v1pr5v = V1V.Value;
                    break;
                case 6:
                    profile.v1pr6 = true;
                    profile.v1pr6v = V1V.Value;
                    break;
                case 7:
                    profile.v1pr7 = true;
                    profile.v1pr7v = V1V.Value;
                    break;
                case 8:
                    profile.v1pr8 = true;
                    profile.v1pr8v = V1V.Value;
                    break;
                case 9:
                    profile.v1pr9 = true;
                    profile.v1pr9v = V1V.Value;
                    break;
            }
            ProfileSave();
        }
        else
        {
            devices.v1 = false;
            devices.v1v = 64;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.v1pr1 = false;
                    break;
                case 2:
                    profile.v1pr2 = false;
                    break;
                case 3:
                    profile.v1pr3 = false;
                    break;
                case 4:
                    profile.v1pr4 = false;
                    break;
                case 5:
                    profile.v1pr5 = false;
                    break;
                case 6:
                    profile.v1pr6 = false;
                    break;
                case 7:
                    profile.v1pr7 = false;
                    break;
                case 8:
                    profile.v1pr8 = false;
                    break;
                case 9:
                    profile.v1pr9 = false;
                    break;
            }
            ProfileSave();
        }
    }
    //Лимит по току VRM A
    private void v2_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (V2.IsChecked == true)
        {
            devices.v2 = true;
            devices.v2v = V2V.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.v2pr1 = true;
                    profile.v2pr1v = V2V.Value;
                    break;
                case 2:
                    profile.v2pr2 = true;
                    profile.v2pr2v = V2V.Value;
                    break;
                case 3:
                    profile.v2pr3 = true;
                    profile.v2pr3v = V2V.Value;
                    break;
                case 4:
                    profile.v2pr4 = true;
                    profile.v2pr4v = V2V.Value;
                    break;
                case 5:
                    profile.v2pr5 = true;
                    profile.v2pr5v = V2V.Value;
                    break;
                case 6:
                    profile.v2pr6 = true;
                    profile.v2pr6v = V2V.Value;
                    break;
                case 7:
                    profile.v2pr7 = true;
                    profile.v2pr7v = V2V.Value;
                    break;
                case 8:
                    profile.v2pr8 = true;
                    profile.v2pr8v = V2V.Value;
                    break;
                case 9:
                    profile.v2pr9 = true;
                    profile.v2pr9v = V2V.Value;
                    break;
            }
            ProfileSave();
        }
        else
        {
            devices.v2 = false;
            devices.v2v = 55;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.v2pr1 = false;
                    break;
                case 2:
                    profile.v2pr2 = false;
                    break;
                case 3:
                    profile.v2pr3 = false;
                    break;
                case 4:
                    profile.v2pr4 = false;
                    break;
                case 5:
                    profile.v2pr5 = false;
                    break;
                case 6:
                    profile.v2pr6 = false;
                    break;
                case 7:
                    profile.v2pr7 = false;
                    break;
                case 8:
                    profile.v2pr8 = false;
                    break;
                case 9:
                    profile.v2pr9 = false;
                    break;
            }
            ProfileSave();
        }
    }
    //Максимальный ток SOC A
    private void v3_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (V3.IsChecked == true)
        {
            devices.v3 = true;
            devices.v3v = V3V.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.v3pr1 = true;
                    profile.v3pr1v = V3V.Value;
                    break;
                case 2:
                    profile.v3pr2 = true;
                    profile.v3pr2v = V3V.Value;
                    break;
                case 3:
                    profile.v3pr3 = true;
                    profile.v3pr3v = V3V.Value;
                    break;
                case 4:
                    profile.v3pr4 = true;
                    profile.v3pr4v = V3V.Value;
                    break;
                case 5:
                    profile.v3pr5 = true;
                    profile.v3pr5v = V3V.Value;
                    break;
                case 6:
                    profile.v3pr6 = true;
                    profile.v3pr6v = V3V.Value;
                    break;
                case 7:
                    profile.v3pr7 = true;
                    profile.v3pr7v = V3V.Value;
                    break;
                case 8:
                    profile.v3pr8 = true;
                    profile.v3pr8v = V3V.Value;
                    break;
                case 9:
                    profile.v3pr9 = true;
                    profile.v3pr9v = V3V.Value;
                    break;
            }
            ProfileSave();
        }
        else
        {
            devices.v3 = false;
            devices.v3v = 13;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.v3pr1 = false;
                    break;
                case 2:
                    profile.v3pr2 = false;
                    break;
                case 3:
                    profile.v3pr3 = false;
                    break;
                case 4:
                    profile.v3pr4 = false;
                    break;
                case 5:
                    profile.v3pr5 = false;
                    break;
                case 6:
                    profile.v3pr6 = false;
                    break;
                case 7:
                    profile.v3pr7 = false;
                    break;
                case 8:
                    profile.v3pr8 = false;
                    break;
                case 9:
                    profile.v3pr9 = false;
                    break;
            }
            ProfileSave();
        }
    }
    //Лимит по току SOC A
    private void v4_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (V4.IsChecked == true)
        {
            devices.v4 = true;
            devices.v4v = V4V.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.v4pr1 = true;
                    profile.v4pr1v = V4V.Value;
                    break;
                case 2:
                    profile.v4pr2 = true;
                    profile.v4pr2v = V4V.Value;
                    break;
                case 3:
                    profile.v4pr3 = true;
                    profile.v4pr3v = V4V.Value;
                    break;
                case 4:
                    profile.v4pr4 = true;
                    profile.v4pr4v = V4V.Value;
                    break;
                case 5:
                    profile.v4pr5 = true;
                    profile.v4pr5v = V4V.Value;
                    break;
                case 6:
                    profile.v4pr6 = true;
                    profile.v4pr6v = V4V.Value;
                    break;
                case 7:
                    profile.v4pr7 = true;
                    profile.v4pr7v = V4V.Value;
                    break;
                case 8:
                    profile.v4pr8 = true;
                    profile.v4pr8v = V4V.Value;
                    break;
                case 9:
                    profile.v4pr9 = true;
                    profile.v4pr9v = V4V.Value;
                    break;
            }
            ProfileSave();
        }
        else
        {
            devices.v4 = false;
            devices.v4v = 10;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.v4pr1 = false;
                    break;
                case 2:
                    profile.v4pr2 = false;
                    break;
                case 3:
                    profile.v4pr3 = false;
                    break;
                case 4:
                    profile.v4pr4 = false;
                    break;
                case 5:
                    profile.v4pr5 = false;
                    break;
                case 6:
                    profile.v4pr6 = false;
                    break;
                case 7:
                    profile.v4pr7 = false;
                    break;
                case 8:
                    profile.v4pr8 = false;
                    break;
                case 9:
                    profile.v4pr9 = false;
                    break;
            }
            ProfileSave();
        }
    }
    //Максимальный ток PCI VDD A
    private void v5_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (V5.IsChecked == true)
        {
            devices.v5 = true;
            devices.v5v = V5V.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.v5pr1 = true;
                    profile.v5pr1v = V5V.Value;
                    break;
                case 2:
                    profile.v5pr2 = true;
                    profile.v5pr2v = V5V.Value;
                    break;
                case 3:
                    profile.v5pr3 = true;
                    profile.v5pr3v = V5V.Value;
                    break;
                case 4:
                    profile.v5pr4 = true;
                    profile.v5pr4v = V5V.Value;
                    break;
                case 5:
                    profile.v5pr5 = true;
                    profile.v5pr5v = V5V.Value;
                    break;
                case 6:
                    profile.v5pr6 = true;
                    profile.v5pr6v = V5V.Value;
                    break;
                case 7:
                    profile.v5pr7 = true;
                    profile.v5pr7v = V5V.Value;
                    break;
                case 8:
                    profile.v5pr8 = true;
                    profile.v5pr8v = V5V.Value;
                    break;
                case 9:
                    profile.v5pr9 = true;
                    profile.v5pr9v = V5V.Value;
                    break;
            }
            ProfileSave();
        }
        else
        {
            devices.v5 = false;
            devices.v5v = 13;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.v5pr1 = false;
                    break;
                case 2:
                    profile.v5pr2 = false;
                    break;
                case 3:
                    profile.v5pr3 = false;
                    break;
                case 4:
                    profile.v5pr4 = false;
                    break;
                case 5:
                    profile.v5pr5 = false;
                    break;
                case 6:
                    profile.v5pr6 = false;
                    break;
                case 7:
                    profile.v5pr7 = false;
                    break;
                case 8:
                    profile.v5pr8 = false;
                    break;
                case 9:
                    profile.v5pr9 = false;
                    break;
            }
            ProfileSave();
        }
    }
    //Максимальный ток PCI SOC A
    private void v6_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (V6.IsChecked == true)
        {
            devices.v6 = true;
            devices.v6v = V6V.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.v6pr1 = true;
                    profile.v6pr1v = V6V.Value;
                    break;
                case 2:
                    profile.v6pr2 = true;
                    profile.v6pr2v = V6V.Value;
                    break;
                case 3:
                    profile.v6pr3 = true;
                    profile.v6pr3v = V6V.Value;
                    break;
                case 4:
                    profile.v6pr4 = true;
                    profile.v6pr4v = V6V.Value;
                    break;
                case 5:
                    profile.v6pr5 = true;
                    profile.v6pr5v = V6V.Value;
                    break;
                case 6:
                    profile.v6pr6 = true;
                    profile.v6pr6v = V6V.Value;
                    break;
                case 7:
                    profile.v6pr7 = true;
                    profile.v6pr7v = V6V.Value;
                    break;
                case 8:
                    profile.v6pr8 = true;
                    profile.v6pr8v = V6V.Value;
                    break;
                case 9:
                    profile.v6pr9 = true;
                    profile.v6pr9v = V6V.Value;
                    break;
            }
            ProfileSave();
        }
        else
        {
            devices.v6 = false;
            devices.v6v = 5;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.v6pr1 = false;
                    break;
                case 2:
                    profile.v6pr2 = false;
                    break;
                case 3:
                    profile.v6pr3 = false;
                    break;
                case 4:
                    profile.v6pr4 = false;
                    break;
                case 5:
                    profile.v6pr5 = false;
                    break;
                case 6:
                    profile.v6pr6 = false;
                    break;
                case 7:
                    profile.v6pr7 = false;
                    break;
                case 8:
                    profile.v6pr8 = false;
                    break;
                case 9:
                    profile.v6pr9 = false;
                    break;
            }
            ProfileSave();
        }
    }
    //Отключить троттлинг на время
    private void v7_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (V7.IsChecked == true)
        {
            devices.v7 = true;
            devices.v7v = V7V.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.v7pr1 = true;
                    profile.v7pr1v = V7V.Value;
                    break;
                case 2:
                    profile.v7pr2 = true;
                    profile.v7pr2v = V7V.Value;
                    break;
                case 3:
                    profile.v7pr3 = true;
                    profile.v7pr3v = V7V.Value;
                    break;
                case 4:
                    profile.v7pr4 = true;
                    profile.v7pr4v = V7V.Value;
                    break;
                case 5:
                    profile.v7pr5 = true;
                    profile.v7pr5v = V7V.Value;
                    break;
                case 6:
                    profile.v7pr6 = true;
                    profile.v7pr6v = V7V.Value;
                    break;
                case 7:
                    profile.v7pr7 = true;
                    profile.v7pr7v = V7V.Value;
                    break;
                case 8:
                    profile.v7pr8 = true;
                    profile.v7pr8v = V7V.Value;
                    break;
                case 9:
                    profile.v7pr9 = true;
                    profile.v7pr9v = V7V.Value;
                    break;
            }
            ProfileSave();
        }
        else
        {
            devices.v7 = false;
            devices.v7v = 2;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.v7pr1 = false;
                    break;
                case 2:
                    profile.v7pr2 = false;
                    break;
                case 3:
                    profile.v7pr3 = false;
                    break;
                case 4:
                    profile.v7pr4 = false;
                    break;
                case 5:
                    profile.v7pr5 = false;
                    break;
                case 6:
                    profile.v7pr6 = false;
                    break;
                case 7:
                    profile.v7pr7 = false;
                    break;
                case 8:
                    profile.v7pr8 = false;
                    break;
                case 9:
                    profile.v7pr9 = false;
                    break;
            }
            ProfileSave();
        }
    }

    //Параметры графики
    //Минимальная частота SOC 
    private void g1_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (g1.IsChecked == true)
        {
            devices.g1 = true;
            devices.g1v = g1v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g1pr1 = true;
                    profile.g1pr1v = g1v.Value;
                    break;
                case 2:
                    profile.g1pr2 = true;
                    profile.g1pr2v = g1v.Value;
                    break;
                case 3:
                    profile.g1pr3 = true;
                    profile.g1pr3v = g1v.Value;
                    break;
                case 4:
                    profile.g1pr4 = true;
                    profile.g1pr4v = g1v.Value;
                    break;
                case 5:
                    profile.g1pr5 = true;
                    profile.g1pr5v = g1v.Value;
                    break;
                case 6:
                    profile.g1pr6 = true;
                    profile.g1pr6v = g1v.Value;
                    break;
                case 7:
                    profile.g1pr7 = true;
                    profile.g1pr7v = g1v.Value;
                    break;
                case 8:
                    profile.g1pr8 = true;
                    profile.g1pr8v = g1v.Value;
                    break;
                case 9:
                    profile.g1pr9 = true;
                    profile.g1pr9v = g1v.Value;
                    break;
            }
            ProfileSave();
        }
        else
        {
            devices.g1 = false;
            devices.g1v = 800;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g1pr1 = false;
                    break;
                case 2:
                    profile.g1pr2 = false;
                    break;
                case 3:
                    profile.g1pr3 = false;
                    break;
                case 4:
                    profile.g1pr4 = false;
                    break;
                case 5:
                    profile.g1pr5 = false;
                    break;
                case 6:
                    profile.g1pr6 = false;
                    break;
                case 7:
                    profile.g1pr7 = false;
                    break;
                case 8:
                    profile.g1pr8 = false;
                    break;
                case 9:
                    profile.g1pr9 = false;
                    break;
            }
            ProfileSave();
        }
    }
    //Максимальная частота SOC
    private void g2_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (g2.IsChecked == true)
        {
            devices.g2 = true;
            devices.g2v = g2v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g2pr1 = true;
                    profile.g2pr1v = g2v.Value;
                    break;
                case 2:
                    profile.g2pr2 = true;
                    profile.g2pr2v = g2v.Value;
                    break;
                case 3:
                    profile.g2pr3 = true;
                    profile.g2pr3v = g2v.Value;
                    break;
                case 4:
                    profile.g2pr4 = true;
                    profile.g2pr4v = g2v.Value;
                    break;
                case 5:
                    profile.g2pr5 = true;
                    profile.g2pr5v = g2v.Value;
                    break;
                case 6:
                    profile.g2pr6 = true;
                    profile.g2pr6v = g2v.Value;
                    break;
                case 7:
                    profile.g2pr7 = true;
                    profile.g2pr7v = g2v.Value;
                    break;
                case 8:
                    profile.g2pr8 = true;
                    profile.g2pr8v = g2v.Value;
                    break;
                case 9:
                    profile.g2pr9 = true;
                    profile.g2pr9v = g2v.Value;
                    break;
            }
            ProfileSave();
        }
        else
        {
            devices.g2 = false;
            devices.g2v = 1200;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g2pr1 = false;
                    break;
                case 2:
                    profile.g2pr2 = false;
                    break;
                case 3:
                    profile.g2pr3 = false;
                    break;
                case 4:
                    profile.g2pr4 = false;
                    break;
                case 5:
                    profile.g2pr5 = false;
                    break;
                case 6:
                    profile.g2pr6 = false;
                    break;
                case 7:
                    profile.g2pr7 = false;
                    break;
                case 8:
                    profile.g2pr8 = false;
                    break;
                case 9:
                    profile.g2pr9 = false;
                    break;
            }
            ProfileSave();
        }
    }
    //Минимальная частота Infinity Fabric
    private void g3_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (g3.IsChecked == true)
        {
            devices.g3 = true;
            devices.g3v = g3v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g3pr1 = true;
                    profile.g3pr1v = g3v.Value;
                    break;
                case 2:
                    profile.g3pr2 = true;
                    profile.g3pr2v = g3v.Value;
                    break;
                case 3:
                    profile.g3pr3 = true;
                    profile.g3pr3v = g3v.Value;
                    break;
                case 4:
                    profile.g3pr4 = true;
                    profile.g3pr4v = g3v.Value;
                    break;
                case 5:
                    profile.g3pr5 = true;
                    profile.g3pr5v = g3v.Value;
                    break;
                case 6:
                    profile.g3pr6 = true;
                    profile.g3pr6v = g3v.Value;
                    break;
                case 7:
                    profile.g3pr7 = true;
                    profile.g3pr7v = g3v.Value;
                    break;
                case 8:
                    profile.g3pr8 = true;
                    profile.g3pr8v = g3v.Value;
                    break;
                case 9:
                    profile.g3pr9 = true;
                    profile.g3pr9v = g3v.Value;
                    break;
            }
            ProfileSave();
        }
        else
        {
            devices.g3 = false;
            devices.g3v = 800;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g3pr1 = false;
                    break;
                case 2:
                    profile.g3pr2 = false;
                    break;
                case 3:
                    profile.g3pr3 = false;
                    break;
                case 4:
                    profile.g3pr4 = false;
                    break;
                case 5:
                    profile.g3pr5 = false;
                    break;
                case 6:
                    profile.g3pr6 = false;
                    break;
                case 7:
                    profile.g3pr7 = false;
                    break;
                case 8:
                    profile.g3pr8 = false;
                    break;
                case 9:
                    profile.g3pr9 = false;
                    break;
            }
            ProfileSave();
        }
    }
    //Максимальная частота Infinity Fabric
    private void g4_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (g4.IsChecked == true)
        {
            devices.g4 = true;
            devices.g4v = g4v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g4pr1 = true;
                    profile.g4pr1v = g4v.Value;
                    break;
                case 2:
                    profile.g4pr2 = true;
                    profile.g4pr2v = g4v.Value;
                    break;
                case 3:
                    profile.g4pr3 = true;
                    profile.g4pr3v = g4v.Value;
                    break;
                case 4:
                    profile.g4pr4 = true;
                    profile.g4pr4v = g4v.Value;
                    break;
                case 5:
                    profile.g4pr5 = true;
                    profile.g4pr5v = g4v.Value;
                    break;
                case 6:
                    profile.g4pr6 = true;
                    profile.g4pr6v = g4v.Value;
                    break;
                case 7:
                    profile.g4pr7 = true;
                    profile.g4pr7v = g4v.Value;
                    break;
                case 8:
                    profile.g4pr8 = true;
                    profile.g4pr8v = g4v.Value;
                    break;
                case 9:
                    profile.g4pr9 = true;
                    profile.g4pr9v = g4v.Value;
                    break;
            }
            ProfileSave();
        }
        else
        {
            devices.g4 = false;
            devices.g4v = 1200;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g4pr1 = false;
                    break;
                case 2:
                    profile.g4pr2 = false;
                    break;
                case 3:
                    profile.g4pr3 = false;
                    break;
                case 4:
                    profile.g4pr4 = false;
                    break;
                case 5:
                    profile.g4pr5 = false;
                    break;
                case 6:
                    profile.g4pr6 = false;
                    break;
                case 7:
                    profile.g4pr7 = false;
                    break;
                case 8:
                    profile.g4pr8 = false;
                    break;
                case 9:
                    profile.g4pr9 = false;
                    break;
            }
            ProfileSave();
        }
    }
    //Минимальная частота кодека VCE
    private void g5_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (g5.IsChecked == true)
        {
            devices.g5 = true;
            devices.g5v = g5v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g5pr1 = true;
                    profile.g5pr1v = g5v.Value;
                    break;
                case 2:
                    profile.g5pr2 = true;
                    profile.g5pr2v = g5v.Value;
                    break;
                case 3:
                    profile.g5pr3 = true;
                    profile.g5pr3v = g5v.Value;
                    break;
                case 4:
                    profile.g5pr4 = true;
                    profile.g5pr4v = g5v.Value;
                    break;
                case 5:
                    profile.g5pr5 = true;
                    profile.g5pr5v = g5v.Value;
                    break;
                case 6:
                    profile.g5pr6 = true;
                    profile.g5pr6v = g5v.Value;
                    break;
                case 7:
                    profile.g5pr7 = true;
                    profile.g5pr7v = g5v.Value;
                    break;
                case 8:
                    profile.g5pr8 = true;
                    profile.g5pr8v = g5v.Value;
                    break;
                case 9:
                    profile.g5pr9 = true;
                    profile.g5pr9v = g5v.Value;
                    break;
            }
            ProfileSave();
        }
        else
        {
            devices.g5 = false;
            devices.g5v = 400;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g5pr1 = false;
                    break;
                case 2:
                    profile.g5pr2 = false;
                    break;
                case 3:
                    profile.g5pr3 = false;
                    break;
                case 4:
                    profile.g5pr4 = false;
                    break;
                case 5:
                    profile.g5pr5 = false;
                    break;
                case 6:
                    profile.g5pr6 = false;
                    break;
                case 7:
                    profile.g5pr7 = false;
                    break;
                case 8:
                    profile.g5pr8 = false;
                    break;
                case 9:
                    profile.g5pr9 = false;
                    break;
            }
            ProfileSave();
        }
    }
    //Максимальная частота кодека VCE
    private void g6_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (g6.IsChecked == true)
        {
            devices.g6 = true;
            devices.g6v = g6v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g6pr1 = true;
                    profile.g6pr1v = g6v.Value;
                    break;
                case 2:
                    profile.g6pr2 = true;
                    profile.g6pr2v = g6v.Value;
                    break;
                case 3:
                    profile.g6pr3 = true;
                    profile.g6pr3v = g6v.Value;
                    break;
                case 4:
                    profile.g6pr4 = true;
                    profile.g6pr4v = g6v.Value;
                    break;
                case 5:
                    profile.g6pr5 = true;
                    profile.g6pr5v = g6v.Value;
                    break;
                case 6:
                    profile.g6pr6 = true;
                    profile.g6pr6v = g6v.Value;
                    break;
                case 7:
                    profile.g6pr7 = true;
                    profile.g6pr7v = g6v.Value;
                    break;
                case 8:
                    profile.g6pr8 = true;
                    profile.g6pr8v = g6v.Value;
                    break;
                case 9:
                    profile.g6pr9 = true;
                    profile.g6pr9v = g6v.Value;
                    break;
            }
            ProfileSave();
        }
        else
        {
            devices.g6 = false;
            devices.g6v = 1200;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g6pr1 = false;
                    break;
                case 2:
                    profile.g6pr2 = false;
                    break;
                case 3:
                    profile.g6pr3 = false;
                    break;
                case 4:
                    profile.g6pr4 = false;
                    break;
                case 5:
                    profile.g6pr5 = false;
                    break;
                case 6:
                    profile.g6pr6 = false;
                    break;
                case 7:
                    profile.g6pr7 = false;
                    break;
                case 8:
                    profile.g6pr8 = false;
                    break;
                case 9:
                    profile.g6pr9 = false;
                    break;
            }
            ProfileSave();
        }
    }
    //Минимальная частота частота Data Latch
    private void g7_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (g7.IsChecked == true)
        {
            devices.g7 = true;
            devices.g7v = g7v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g7pr1 = true;
                    profile.g7pr1v = g7v.Value;
                    break;
                case 2:
                    profile.g7pr2 = true;
                    profile.g7pr2v = g7v.Value;
                    break;
                case 3:
                    profile.g7pr3 = true;
                    profile.g7pr3v = g7v.Value;
                    break;
                case 4:
                    profile.g7pr4 = true;
                    profile.g7pr4v = g7v.Value;
                    break;
                case 5:
                    profile.g7pr5 = true;
                    profile.g7pr5v = g7v.Value;
                    break;
                case 6:
                    profile.g7pr6 = true;
                    profile.g7pr6v = g7v.Value;
                    break;
                case 7:
                    profile.g7pr7 = true;
                    profile.g7pr7v = g7v.Value;
                    break;
                case 8:
                    profile.g7pr8 = true;
                    profile.g7pr8v = g7v.Value;
                    break;
                case 9:
                    profile.g7pr9 = true;
                    profile.g7pr9v = g7v.Value;
                    break;
            }
            ProfileSave();
        }
        else
        {
            devices.g7 = false;
            devices.g7v = 400;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g7pr1 = false;
                    break;
                case 2:
                    profile.g7pr2 = false;
                    break;
                case 3:
                    profile.g7pr3 = false;
                    break;
                case 4:
                    profile.g7pr4 = false;
                    break;
                case 5:
                    profile.g7pr5 = false;
                    break;
                case 6:
                    profile.g7pr6 = false;
                    break;
                case 7:
                    profile.g7pr7 = false;
                    break;
                case 8:
                    profile.g7pr8 = false;
                    break;
                case 9:
                    profile.g7pr9 = false;
                    break;
            }
            ProfileSave();
        }
    }
    //Максимальная частота Data Latch
    private void g8_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (g8.IsChecked == true)
        {
            devices.g8 = true;
            devices.g8v = g8v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g8pr1 = true;
                    profile.g8pr1v = g8v.Value;
                    break;
                case 2:
                    profile.g8pr2 = true;
                    profile.g8pr2v = g8v.Value;
                    break;
                case 3:
                    profile.g8pr3 = true;
                    profile.g8pr3v = g8v.Value;
                    break;
                case 4:
                    profile.g8pr4 = true;
                    profile.g8pr4v = g8v.Value;
                    break;
                case 5:
                    profile.g8pr5 = true;
                    profile.g8pr5v = g8v.Value;
                    break;
                case 6:
                    profile.g8pr6 = true;
                    profile.g8pr6v = g8v.Value;
                    break;
                case 7:
                    profile.g8pr7 = true;
                    profile.g8pr7v = g8v.Value;
                    break;
                case 8:
                    profile.g8pr8 = true;
                    profile.g8pr8v = g8v.Value;
                    break;
                case 9:
                    profile.g8pr9 = true;
                    profile.g8pr9v = g8v.Value;
                    break;
            }
            ProfileSave();
        }
        else
        {
            devices.g8 = false;
            devices.g8v = 1200;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g8pr1 = false;
                    break;
                case 2:
                    profile.g8pr2 = false;
                    break;
                case 3:
                    profile.g8pr3 = false;
                    break;
                case 4:
                    profile.g8pr4 = false;
                    break;
                case 5:
                    profile.g8pr5 = false;
                    break;
                case 6:
                    profile.g8pr6 = false;
                    break;
                case 7:
                    profile.g8pr7 = false;
                    break;
                case 8:
                    profile.g8pr8 = false;
                    break;
                case 9:
                    profile.g8pr9 = false;
                    break;
            }
            ProfileSave();
        }
    }
    //Минимальная частота iGpu
    private void g9_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (g9.IsChecked == true)
        {
            devices.g9 = true;
            devices.g9v = g9v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g9pr1 = true;
                    profile.g9pr1v = g9v.Value;
                    break;
                case 2:
                    profile.g9pr2 = true;
                    profile.g9pr2v = g9v.Value;
                    break;
                case 3:
                    profile.g9pr3 = true;
                    profile.g9pr3v = g9v.Value;
                    break;
                case 4:
                    profile.g9pr4 = true;
                    profile.g9pr4v = g9v.Value;
                    break;
                case 5:
                    profile.g9pr5 = true;
                    profile.g9pr5v = g9v.Value;
                    break;
                case 6:
                    profile.g9pr6 = true;
                    profile.g9pr6v = g9v.Value;
                    break;
                case 7:
                    profile.g9pr7 = true;
                    profile.g9pr7v = g9v.Value;
                    break;
                case 8:
                    profile.g9pr8 = true;
                    profile.g9pr8v = g9v.Value;
                    break;
                case 9:
                    profile.g9pr9 = true;
                    profile.g9pr9v = g9v.Value;
                    break;
            }
            ProfileSave();
        }
        else
        {
            devices.g9 = false;
            devices.g9v = 400;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g9pr1 = false;
                    break;
                case 2:
                    profile.g9pr2 = false;
                    break;
                case 3:
                    profile.g9pr3 = false;
                    break;
                case 4:
                    profile.g9pr4 = false;
                    break;
                case 5:
                    profile.g9pr5 = false;
                    break;
                case 6:
                    profile.g9pr6 = false;
                    break;
                case 7:
                    profile.g9pr7 = false;
                    break;
                case 8:
                    profile.g9pr8 = false;
                    break;
                case 9:
                    profile.g9pr9 = false;
                    break;
            }
            ProfileSave();
        }
    }
    //Максимальная частота iGpu
    private void g10_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (g10.IsChecked == true)
        {
            devices.g10 = true;
            devices.g10v = g10v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g10pr1 = true;
                    profile.g10pr1v = g10v.Value;
                    break;
                case 2:
                    profile.g10pr2 = true;
                    profile.g10pr2v = g10v.Value;
                    break;
                case 3:
                    profile.g10pr3 = true;
                    profile.g10pr3v = g10v.Value;
                    break;
                case 4:
                    profile.g10pr4 = true;
                    profile.g10pr4v = g10v.Value;
                    break;
                case 5:
                    profile.g10pr5 = true;
                    profile.g10pr5v = g10v.Value;
                    break;
                case 6:
                    profile.g10pr6 = true;
                    profile.g10pr6v = g10v.Value;
                    break;
                case 7:
                    profile.g10pr7 = true;
                    profile.g10pr7v = g10v.Value;
                    break;
                case 8:
                    profile.g10pr8 = true;
                    profile.g10pr8v = g10v.Value;
                    break;
                case 9:
                    profile.g10pr9 = true;
                    profile.g10pr9v = g10v.Value;
                    break;
            }
            ProfileSave();
        }
        else
        {
            devices.g10 = false;
            devices.g10v = 1200;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g10pr1 = false;
                    break;
                case 2:
                    profile.g10pr2 = false;
                    break;
                case 3:
                    profile.g10pr3 = false;
                    break;
                case 4:
                    profile.g10pr4 = false;
                    break;
                case 5:
                    profile.g10pr5 = false;
                    break;
                case 6:
                    profile.g10pr6 = false;
                    break;
                case 7:
                    profile.g10pr7 = false;
                    break;
                case 8:
                    profile.g10pr8 = false;
                    break;
                case 9:
                    profile.g10pr9 = false;
                    break;
            }
            ProfileSave();
        }
    }
    //Параметры процессора, при изменении слайдеров
    //Максимальная температура CPU (C)
    private async void c1_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (c1.IsChecked == true)
        {
            devices.c1 = true;
            devices.c1v = c1v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.c1pr1 = true;
                    profile.c1pr1v = c1v.Value;
                    break;
                case 2:
                    profile.c1pr2 = true;
                    profile.c1pr2v = c1v.Value;
                    break;
                case 3:
                    profile.c1pr3 = true;
                    profile.c1pr3v = c1v.Value;
                    break;
                case 4:
                    profile.c1pr4 = true;
                    profile.c1pr4v = c1v.Value;
                    break;
                case 5:
                    profile.c1pr5 = true;
                    profile.c1pr5v = c1v.Value;
                    break;
                case 6:
                    profile.c1pr6 = true;
                    profile.c1pr6v = c1v.Value;
                    break;
                case 7:
                    profile.c1pr7 = true;
                    profile.c1pr7v = c1v.Value;
                    break;
                case 8:
                    profile.c1pr8 = true;
                    profile.c1pr8v = c1v.Value;
                    break;
                case 9:
                    profile.c1pr9 = true;
                    profile.c1pr9v = c1v.Value;
                    break;
            }
            ProfileSave();
        }
        else { SlidersInit(); }
        await Task.Delay(20);
        c1t.Content = c1v.Value.ToString();
    }
    //Лимит CPU (W)
    private async void c2_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (c2.IsChecked == true)
        {
            devices.c2 = true;
            devices.c2v = c2v.Value;
            DeviceSave(); 
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.c2pr1 = true;
                    profile.c2pr1v = c2v.Value;
                    break;
                case 2:
                    profile.c2pr2 = true;
                    profile.c2pr2v = c2v.Value;
                    break;
                case 3:
                    profile.c2pr3 = true;
                    profile.c2pr3v = c2v.Value;
                    break;
                case 4:
                    profile.c2pr4 = true;
                    profile.c2pr4v = c2v.Value;
                    break;
                case 5:
                    profile.c2pr5 = true;
                    profile.c2pr5v = c2v.Value;
                    break;
                case 6:
                    profile.c2pr6 = true;
                    profile.c2pr6v = c2v.Value;
                    break;
                case 7:
                    profile.c2pr7 = true;
                    profile.c2pr7v = c2v.Value;
                    break;
                case 8:
                    profile.c2pr8 = true;
                    profile.c2pr8v = c2v.Value;
                    break;
                case 9:
                    profile.c2pr9 = true;
                    profile.c2pr9v = c2v.Value;
                    break;
            }
            ProfileSave();
        }
        await Task.Delay(20);
        c2t.Content = c2v.Value.ToString();
    }
    //Реальный CPU (W)
    private async void c3_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (c3.IsChecked == true)
        {
            devices.c3 = true;
            devices.c3v = c3v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.c3pr1 = true;
                    profile.c3pr1v = c3v.Value;
                    break;
                case 2:
                    profile.c3pr2 = true;
                    profile.c3pr2v = c3v.Value;
                    break;
                case 3:
                    profile.c3pr3 = true;
                    profile.c3pr3v = c3v.Value;
                    break;
                case 4:
                    profile.c3pr4 = true;
                    profile.c3pr4v = c3v.Value;
                    break;
                case 5:
                    profile.c3pr5 = true;
                    profile.c3pr5v = c3v.Value;
                    break;
                case 6:
                    profile.c3pr6 = true;
                    profile.c3pr6v = c3v.Value;
                    break;
                case 7:
                    profile.c3pr7 = true;
                    profile.c3pr7v = c3v.Value;
                    break;
                case 8:
                    profile.c3pr8 = true;
                    profile.c3pr8v = c3v.Value;
                    break;
                case 9:
                    profile.c3pr9 = true;
                    profile.c3pr9v = c3v.Value;
                    break;
            }
            ProfileSave();
        }
        await Task.Delay(20);
        c3t.Content = c3v.Value.ToString();
    }
    //Средний CPU(W)
    private async void c4_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (c4.IsChecked == true)
        {
            devices.c4 = true;
            devices.c4v = c4v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.c4pr1 = true;
                    profile.c4pr1v = c4v.Value;
                    break;
                case 2:
                    profile.c4pr2 = true;
                    profile.c4pr2v = c4v.Value;
                    break;
                case 3:
                    profile.c4pr3 = true;
                    profile.c4pr3v = c4v.Value;
                    break;
                case 4:
                    profile.c4pr4 = true;
                    profile.c4pr4v = c4v.Value;
                    break;
                case 5:
                    profile.c4pr5 = true;
                    profile.c4pr5v = c4v.Value;
                    break;
                case 6:
                    profile.c4pr6 = true;
                    profile.c4pr6v = c4v.Value;
                    break;
                case 7:
                    profile.c4pr7 = true;
                    profile.c4pr7v = c4v.Value;
                    break;
                case 8:
                    profile.c4pr8 = true;
                    profile.c4pr8v = c4v.Value;
                    break;
                case 9:
                    profile.c4pr9 = true;
                    profile.c4pr9v = c4v.Value;
                    break;
            }
            ProfileSave();
        }
        await Task.Delay(20);
        c4t.Content = c4v.Value.ToString();
    }
    //Тик быстрого разгона (S)
    private async void c5_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (c5.IsChecked == true)
        {
            devices.c5 = true;
            devices.c5v = c5v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.c5pr1 = true;
                    profile.c5pr1v = c5v.Value;
                    break;
                case 2:
                    profile.c5pr2 = true;
                    profile.c5pr2v = c5v.Value;
                    break;
                case 3:
                    profile.c5pr3 = true;
                    profile.c5pr3v = c5v.Value;
                    break;
                case 4:
                    profile.c5pr4 = true;
                    profile.c5pr4v = c5v.Value;
                    break;
                case 5:
                    profile.c5pr5 = true;
                    profile.c5pr5v = c5v.Value;
                    break;
                case 6:
                    profile.c5pr6 = true;
                    profile.c5pr6v = c5v.Value;
                    break;
                case 7:
                    profile.c5pr7 = true;
                    profile.c5pr7v = c5v.Value;
                    break;
                case 8:
                    profile.c5pr8 = true;
                    profile.c5pr8v = c5v.Value;
                    break;
                case 9:
                    profile.c5pr9 = true;
                    profile.c5pr9v = c5v.Value;
                    break;
            }
            ProfileSave();
        }
        await Task.Delay(20);
        c5t.Content = c5v.Value.ToString();
    }
    //Тик медленного разгона (S)
    private async void c6_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (c6.IsChecked == true)
        {
            devices.c6 = true;
            devices.c6v = c6v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.c6pr1 = true;
                    profile.c6pr1v = c6v.Value;
                    break;
                case 2:
                    profile.c6pr2 = true;
                    profile.c6pr2v = c6v.Value;
                    break;
                case 3:
                    profile.c6pr3 = true;
                    profile.c6pr3v = c6v.Value;
                    break;
                case 4:
                    profile.c6pr4 = true;
                    profile.c6pr4v = c6v.Value;
                    break;
                case 5:
                    profile.c6pr5 = true;
                    profile.c6pr5v = c6v.Value;
                    break;
                case 6:
                    profile.c6pr6 = true;
                    profile.c6pr6v = c6v.Value;
                    break;
                case 7:
                    profile.c6pr7 = true;
                    profile.c6pr7v = c6v.Value;
                    break;
                case 8:
                    profile.c6pr8 = true;
                    profile.c6pr8v = c6v.Value;
                    break;
                case 9:
                    profile.c6pr9 = true;
                    profile.c6pr9v = c6v.Value;
                    break;
            }
            ProfileSave();
        }
        await Task.Delay(20);
        c6t.Content = c6v.Value.ToString();
    }
    //Параметры VRM
    private async void v1v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (V1.IsChecked == true)
        {
            devices.v1 = true;
            devices.v1v = V1V.Value;
            DeviceSave();
        }
        load = true;
        switch (ProfileCOM.SelectedIndex)
        {
            case 1:
                profile.v1pr1 = true;
                profile.v1pr1v = V1V.Value;
                break;
            case 2:
                profile.v1pr2 = true;
                profile.v1pr2v = V1V.Value;
                break;
            case 3:
                profile.v1pr3 = true;
                profile.v1pr3v = V1V.Value;
                break;
            case 4:
                profile.v1pr4 = true;
                profile.v1pr4v = V1V.Value;
                break;
            case 5:
                profile.v1pr5 = true;
                profile.v1pr5v = V1V.Value;
                break;
            case 6:
                profile.v1pr6 = true;
                profile.v1pr6v = V1V.Value;
                break;
            case 7:
                profile.v1pr7 = true;
                profile.v1pr7v = V1V.Value;
                break;
            case 8:
                profile.v1pr8 = true;
                profile.v1pr8v = V1V.Value;
                break;
            case 9:
                profile.v1pr9 = true;
                profile.v1pr9v = V1V.Value;
                break;
        }
        ProfileSave();
        await Task.Delay(20);
        v1t.Content = V1V.Value.ToString();
    }
    private async void v2v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (V2.IsChecked == true)
        {
            devices.v2 = true;
            devices.v2v = V2V.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.v2pr1 = true;
                    profile.v2pr1v = V2V.Value;
                    break;
                case 2:
                    profile.v2pr2 = true;
                    profile.v2pr2v = V2V.Value;
                    break;
                case 3:
                    profile.v2pr3 = true;
                    profile.v2pr3v = V2V.Value;
                    break;
                case 4:
                    profile.v2pr4 = true;
                    profile.v2pr4v = V2V.Value;
                    break;
                case 5:
                    profile.v2pr5 = true;
                    profile.v2pr5v = V2V.Value;
                    break;
                case 6:
                    profile.v2pr6 = true;
                    profile.v2pr6v = V2V.Value;
                    break;
                case 7:
                    profile.v2pr7 = true;
                    profile.v2pr7v = V2V.Value;
                    break;
                case 8:
                    profile.v2pr8 = true;
                    profile.v2pr8v = V2V.Value;
                    break;
                case 9:
                    profile.v2pr9 = true;
                    profile.v2pr9v = V2V.Value;
                    break;
            }
            ProfileSave();
        }
        await Task.Delay(20);
        v2t.Content = V2V.Value.ToString();
    }
    private async void v3v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (V3.IsChecked == true)
        {
            devices.v3 = true;
            devices.v3v = V3V.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.v3pr1 = true;
                    profile.v3pr1v = V3V.Value;
                    break;
                case 2:
                    profile.v3pr2 = true;
                    profile.v3pr2v = V3V.Value;
                    break;
                case 3:
                    profile.v3pr3 = true;
                    profile.v3pr3v = V3V.Value;
                    break;
                case 4:
                    profile.v3pr4 = true;
                    profile.v3pr4v = V3V.Value;
                    break;
                case 5:
                    profile.v3pr5 = true;
                    profile.v3pr5v = V3V.Value;
                    break;
                case 6:
                    profile.v3pr6 = true;
                    profile.v3pr6v = V3V.Value;
                    break;
                case 7:
                    profile.v3pr7 = true;
                    profile.v3pr7v = V3V.Value;
                    break;
                case 8:
                    profile.v3pr8 = true;
                    profile.v3pr8v = V3V.Value;
                    break;
                case 9:
                    profile.v3pr9 = true;
                    profile.v3pr9v = V3V.Value;
                    break;
            }
            ProfileSave();
        }
        await Task.Delay(20);
        v3t.Content = V3V.Value.ToString();
    }
    private async void v4v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (V4.IsChecked == true)
        {
            devices.v4 = true;
            devices.v4v = V4V.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.v4pr1 = true;
                    profile.v4pr1v = V4V.Value;
                    break;
                case 2:
                    profile.v4pr2 = true;
                    profile.v4pr2v = V4V.Value;
                    break;
                case 3:
                    profile.v4pr3 = true;
                    profile.v4pr3v = V4V.Value;
                    break;
                case 4:
                    profile.v4pr4 = true;
                    profile.v4pr4v = V4V.Value;
                    break;
                case 5:
                    profile.v4pr5 = true;
                    profile.v4pr5v = V4V.Value;
                    break;
                case 6:
                    profile.v4pr6 = true;
                    profile.v4pr6v = V4V.Value;
                    break;
                case 7:
                    profile.v4pr7 = true;
                    profile.v4pr7v = V4V.Value;
                    break;
                case 8:
                    profile.v4pr8 = true;
                    profile.v4pr8v = V4V.Value;
                    break;
                case 9:
                    profile.v4pr9 = true;
                    profile.v4pr9v = V4V.Value;
                    break;
            }
            ProfileSave();
        }
        await Task.Delay(20);
        v4t.Content = V4V.Value.ToString();
    }
    private async void v5v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (V5.IsChecked == true)
        {
            devices.v5 = true;
            devices.v5v = V5V.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.v5pr1 = true;
                    profile.v5pr1v = V5V.Value;
                    break;
                case 2:
                    profile.v5pr2 = true;
                    profile.v5pr2v = V5V.Value;
                    break;
                case 3:
                    profile.v5pr3 = true;
                    profile.v5pr3v = V5V.Value;
                    break;
                case 4:
                    profile.v5pr4 = true;
                    profile.v5pr4v = V5V.Value;
                    break;
                case 5:
                    profile.v5pr5 = true;
                    profile.v5pr5v = V5V.Value;
                    break;
                case 6:
                    profile.v5pr6 = true;
                    profile.v5pr6v = V5V.Value;
                    break;
                case 7:
                    profile.v5pr7 = true;
                    profile.v5pr7v = V5V.Value;
                    break;
                case 8:
                    profile.v5pr8 = true;
                    profile.v5pr8v = V5V.Value;
                    break;
                case 9:
                    profile.v5pr9 = true;
                    profile.v5pr9v = V5V.Value;
                    break;
            }
            ProfileSave();
        }
        await Task.Delay(20);
        v5t.Content = V5V.Value.ToString();
    }
    private async void v6v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (V6.IsChecked == true)
        {
            devices.v6 = true;
            devices.v6v = V6V.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.v6pr1 = true;
                    profile.v6pr1v = V6V.Value;
                    break;
                case 2:
                    profile.v6pr2 = true;
                    profile.v6pr2v = V6V.Value;
                    break;
                case 3:
                    profile.v6pr3 = true;
                    profile.v6pr3v = V6V.Value;
                    break;
                case 4:
                    profile.v6pr4 = true;
                    profile.v6pr4v = V6V.Value;
                    break;
                case 5:
                    profile.v6pr5 = true;
                    profile.v6pr5v = V6V.Value;
                    break;
                case 6:
                    profile.v6pr6 = true;
                    profile.v6pr6v = V6V.Value;
                    break;
                case 7:
                    profile.v6pr7 = true;
                    profile.v6pr7v = V6V.Value;
                    break;
                case 8:
                    profile.v6pr8 = true;
                    profile.v6pr8v = V6V.Value;
                    break;
                case 9:
                    profile.v6pr9 = true;
                    profile.v6pr9v = V6V.Value;
                    break;
            }
            ProfileSave();
        }
        await Task.Delay(20);
        v6t.Content = V6V.Value.ToString();
    }
    private async void v7v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (V7.IsChecked == true)
        {
            devices.v7 = true;
            devices.v7v = V7V.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.v7pr1 = true;
                    profile.v7pr1v = V7V.Value;
                    break;
                case 2:
                    profile.v7pr2 = true;
                    profile.v7pr2v = V7V.Value;
                    break;
                case 3:
                    profile.v7pr3 = true;
                    profile.v7pr3v = V7V.Value;
                    break;
                case 4:
                    profile.v7pr4 = true;
                    profile.v7pr4v = V7V.Value;
                    break;
                case 5:
                    profile.v7pr5 = true;
                    profile.v7pr5v = V7V.Value;
                    break;
                case 6:
                    profile.v7pr6 = true;
                    profile.v7pr6v = V7V.Value;
                    break;
                case 7:
                    profile.v7pr7 = true;
                    profile.v7pr7v = V7V.Value;
                    break;
                case 8:
                    profile.v7pr8 = true;
                    profile.v7pr8v = V7V.Value;
                    break;
                case 9:
                    profile.v7pr9 = true;
                    profile.v7pr9v = V7V.Value;
                    break;
            }
            ProfileSave();
        }
        await Task.Delay(20);
        v7t.Content = V7V.Value.ToString();
    }
    //Параметры GPU
    private async void g1v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (g1.IsChecked == true)
        {
            devices.g1 = true;
            devices.g1v = g1v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g1pr1 = true;
                    profile.g1pr1v = g1v.Value;
                    break;
                case 2:
                    profile.g1pr2 = true;
                    profile.g1pr2v = g1v.Value;
                    break;
                case 3:
                    profile.g1pr3 = true;
                    profile.g1pr3v = g1v.Value;
                    break;
                case 4:
                    profile.g1pr4 = true;
                    profile.g1pr4v = g1v.Value;
                    break;
                case 5:
                    profile.g1pr5 = true;
                    profile.g1pr5v = g1v.Value;
                    break;
                case 6:
                    profile.g1pr6 = true;
                    profile.g1pr6v = g1v.Value;
                    break;
                case 7:
                    profile.g1pr7 = true;
                    profile.g1pr7v = g1v.Value;
                    break;
                case 8:
                    profile.g1pr8 = true;
                    profile.g1pr8v = g1v.Value;
                    break;
                case 9:
                    profile.g1pr9 = true;
                    profile.g1pr9v = g1v.Value;
                    break;
            }
            ProfileSave();
        }
        await Task.Delay(20);
        g1t.Content = g1v.Value.ToString();
    }
    private async void g2v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (g2.IsChecked == true)
        {
            devices.g2 = true;
            devices.g2v = g2v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g2pr1 = true;
                    profile.g2pr1v = g2v.Value;
                    break;
                case 2:
                    profile.g2pr2 = true;
                    profile.g2pr2v = g2v.Value;
                    break;
                case 3:
                    profile.g2pr3 = true;
                    profile.g2pr3v = g2v.Value;
                    break;
                case 4:
                    profile.g2pr4 = true;
                    profile.g2pr4v = g2v.Value;
                    break;
                case 5:
                    profile.g2pr5 = true;
                    profile.g2pr5v = g2v.Value;
                    break;
                case 6:
                    profile.g2pr6 = true;
                    profile.g2pr6v = g2v.Value;
                    break;
                case 7:
                    profile.g2pr7 = true;
                    profile.g2pr7v = g2v.Value;
                    break;
                case 8:
                    profile.g2pr8 = true;
                    profile.g2pr8v = g2v.Value;
                    break;
                case 9:
                    profile.g2pr9 = true;
                    profile.g2pr9v = g2v.Value;
                    break;
            }
            ProfileSave();
        }
        await Task.Delay(20);
        g2t.Content = g2v.Value.ToString();
    }
    private async void g3v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (g3.IsChecked == true)
        {
            devices.g3 = true;
            devices.g3v = g3v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g3pr1 = true;
                    profile.g3pr1v = g3v.Value;
                    break;
                case 2:
                    profile.g3pr2 = true;
                    profile.g3pr2v = g3v.Value;
                    break;
                case 3:
                    profile.g3pr3 = true;
                    profile.g3pr3v = g3v.Value;
                    break;
                case 4:
                    profile.g3pr4 = true;
                    profile.g3pr4v = g3v.Value;
                    break;
                case 5:
                    profile.g3pr5 = true;
                    profile.g3pr5v = g3v.Value;
                    break;
                case 6:
                    profile.g3pr6 = true;
                    profile.g3pr6v = g3v.Value;
                    break;
                case 7:
                    profile.g3pr7 = true;
                    profile.g3pr7v = g3v.Value;
                    break;
                case 8:
                    profile.g3pr8 = true;
                    profile.g3pr8v = g3v.Value;
                    break;
                case 9:
                    profile.g3pr9 = true;
                    profile.g3pr9v = g3v.Value;
                    break;
            }
            ProfileSave();
        }
        await Task.Delay(20);
        g3t.Content = g3v.Value.ToString();
    }
    private async void g4v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (g4.IsChecked == true)
        {
            devices.g4 = true;
            devices.g4v = g4v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g4pr1 = true;
                    profile.g4pr1v = g4v.Value;
                    break;
                case 2:
                    profile.g4pr2 = true;
                    profile.g4pr2v = g4v.Value;
                    break;
                case 3:
                    profile.g4pr3 = true;
                    profile.g4pr3v = g4v.Value;
                    break;
                case 4:
                    profile.g4pr4 = true;
                    profile.g4pr4v = g4v.Value;
                    break;
                case 5:
                    profile.g4pr5 = true;
                    profile.g4pr5v = g4v.Value;
                    break;
                case 6:
                    profile.g4pr6 = true;
                    profile.g4pr6v = g4v.Value;
                    break;
                case 7:
                    profile.g4pr7 = true;
                    profile.g4pr7v = g4v.Value;
                    break;
                case 8:
                    profile.g4pr8 = true;
                    profile.g4pr8v = g4v.Value;
                    break;
                case 9:
                    profile.g4pr9 = true;
                    profile.g4pr9v = g4v.Value;
                    break;
            }
            ProfileSave();
        }
        await Task.Delay(20);
        g4t.Content = g4v.Value.ToString();
    }
    private async void g5v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (g5.IsChecked == true)
        {
            devices.g5 = true;
            devices.g5v = g5v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g5pr1 = true;
                    profile.g5pr1v = g5v.Value;
                    break;
                case 2:
                    profile.g5pr2 = true;
                    profile.g5pr2v = g5v.Value;
                    break;
                case 3:
                    profile.g5pr3 = true;
                    profile.g5pr3v = g5v.Value;
                    break;
                case 4:
                    profile.g5pr4 = true;
                    profile.g5pr4v = g5v.Value;
                    break;
                case 5:
                    profile.g5pr5 = true;
                    profile.g5pr5v = g5v.Value;
                    break;
                case 6:
                    profile.g5pr6 = true;
                    profile.g5pr6v = g5v.Value;
                    break;
                case 7:
                    profile.g5pr7 = true;
                    profile.g5pr7v = g5v.Value;
                    break;
                case 8:
                    profile.g5pr8 = true;
                    profile.g5pr8v = g5v.Value;
                    break;
                case 9:
                    profile.g5pr9 = true;
                    profile.g5pr9v = g5v.Value;
                    break;
            }
            ProfileSave();
        }
        await Task.Delay(20);
        g5t.Content = g5v.Value.ToString();
    }
    private async void g6v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (g6.IsChecked == true)
        {
            devices.g6 = true;
            devices.g6v = g6v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g6pr1 = true;
                    profile.g6pr1v = g6v.Value;
                    break;
                case 2:
                    profile.g6pr2 = true;
                    profile.g6pr2v = g6v.Value;
                    break;
                case 3:
                    profile.g6pr3 = true;
                    profile.g6pr3v = g6v.Value;
                    break;
                case 4:
                    profile.g6pr4 = true;
                    profile.g6pr4v = g6v.Value;
                    break;
                case 5:
                    profile.g6pr5 = true;
                    profile.g6pr5v = g6v.Value;
                    break;
                case 6:
                    profile.g6pr6 = true;
                    profile.g6pr6v = g6v.Value;
                    break;
                case 7:
                    profile.g6pr7 = true;
                    profile.g6pr7v = g6v.Value;
                    break;
                case 8:
                    profile.g6pr8 = true;
                    profile.g6pr8v = g6v.Value;
                    break;
                case 9:
                    profile.g6pr9 = true;
                    profile.g6pr9v = g6v.Value;
                    break;
            }
            ProfileSave();
        }
        await Task.Delay(20);
        g6t.Content = g6v.Value.ToString();
    }
    private async void g7v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (g7.IsChecked == true)
        {
            devices.g7 = true;
            devices.g7v = g7v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g7pr1 = true;
                    profile.g7pr1v = g7v.Value;
                    break;
                case 2:
                    profile.g7pr2 = true;
                    profile.g7pr2v = g7v.Value;
                    break;
                case 3:
                    profile.g7pr3 = true;
                    profile.g7pr3v = g7v.Value;
                    break;
                case 4:
                    profile.g7pr4 = true;
                    profile.g7pr4v = g7v.Value;
                    break;
                case 5:
                    profile.g7pr5 = true;
                    profile.g7pr5v = g7v.Value;
                    break;
                case 6:
                    profile.g7pr6 = true;
                    profile.g7pr6v = g7v.Value;
                    break;
                case 7:
                    profile.g7pr7 = true;
                    profile.g7pr7v = g7v.Value;
                    break;
                case 8:
                    profile.g7pr8 = true;
                    profile.g7pr8v = g7v.Value;
                    break;
                case 9:
                    profile.g7pr9 = true;
                    profile.g7pr9v = g7v.Value;
                    break;
            }
            ProfileSave();
        }
        await Task.Delay(20);
        g7t.Content = g7v.Value.ToString();
    }
    private async void g8v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (g8.IsChecked == true)
        {
            devices.g8 = true;
            devices.g8v = g8v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g8pr1 = true;
                    profile.g8pr1v = g8v.Value;
                    break;
                case 2:
                    profile.g8pr2 = true;
                    profile.g8pr2v = g8v.Value;
                    break;
                case 3:
                    profile.g8pr3 = true;
                    profile.g8pr3v = g8v.Value;
                    break;
                case 4:
                    profile.g8pr4 = true;
                    profile.g8pr4v = g8v.Value;
                    break;
                case 5:
                    profile.g8pr5 = true;
                    profile.g8pr5v = g8v.Value;
                    break;
                case 6:
                    profile.g8pr6 = true;
                    profile.g8pr6v = g8v.Value;
                    break;
                case 7:
                    profile.g8pr7 = true;
                    profile.g8pr7v = g8v.Value;
                    break;
                case 8:
                    profile.g8pr8 = true;
                    profile.g8pr8v = g8v.Value;
                    break;
                case 9:
                    profile.g8pr9 = true;
                    profile.g8pr9v = g8v.Value;
                    break;
            }
            ProfileSave();
        }
        await Task.Delay(20);
        g8t.Content = g8v.Value.ToString();
    }
    private async void g9v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (g9.IsChecked == true)
        {
            devices.g9 = true;
            devices.g9v = g9v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g9pr1 = true;
                    profile.g9pr1v = g9v.Value;
                    break;
                case 2:
                    profile.g9pr2 = true;
                    profile.g9pr2v = g9v.Value;
                    break;
                case 3:
                    profile.g9pr3 = true;
                    profile.g9pr3v = g9v.Value;
                    break;
                case 4:
                    profile.g9pr4 = true;
                    profile.g9pr4v = g9v.Value;
                    break;
                case 5:
                    profile.g9pr5 = true;
                    profile.g9pr5v = g9v.Value;
                    break;
                case 6:
                    profile.g9pr6 = true;
                    profile.g9pr6v = g9v.Value;
                    break;
                case 7:
                    profile.g9pr7 = true;
                    profile.g9pr7v = g9v.Value;
                    break;
                case 8:
                    profile.g9pr8 = true;
                    profile.g9pr8v = g9v.Value;
                    break;
                case 9:
                    profile.g9pr9 = true;
                    profile.g9pr9v = g9v.Value;
                    break;
            }
            ProfileSave();
        }
        await Task.Delay(20);
        g9t.Content = g9v.Value.ToString();
    }
    private async void g10v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (g10.IsChecked == true)
        {
            devices.g10 = true;
            devices.g10v = g10v.Value;
            DeviceSave();
            switch (ProfileCOM.SelectedIndex)
            {
                case 1:
                    profile.g10pr1 = true;
                    profile.g10pr1v = g10v.Value;
                    break;
                case 2:
                    profile.g10pr2 = true;
                    profile.g10pr2v = g10v.Value;
                    break;
                case 3:
                    profile.g10pr3 = true;
                    profile.g10pr3v = g10v.Value;
                    break;
                case 4:
                    profile.g10pr4 = true;
                    profile.g10pr4v = g10v.Value;
                    break;
                case 5:
                    profile.g10pr5 = true;
                    profile.g10pr5v = g10v.Value;
                    break;
                case 6:
                    profile.g10pr6 = true;
                    profile.g10pr6v = g10v.Value;
                    break;
                case 7:
                    profile.g10pr7 = true;
                    profile.g10pr7v = g10v.Value;
                    break;
                case 8:
                    profile.g10pr8 = true;
                    profile.g10pr8v = g10v.Value;
                    break;
                case 9:
                    profile.g10pr9 = true;
                    profile.g10pr9v = g10v.Value;
                    break;
            }
            ProfileSave();
        }
        await Task.Delay(20);
        g10t.Content = g10v.Value.ToString();
    }
    //Кнопка применить, итоговый выход, Ryzen ADJ
    private void Apply_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (c1.IsChecked == true)
        {
            adjline += " --tctl-temp=" + c1v.Value;
        }
        if (c2.IsChecked == true)
        {
            adjline += " --stapm-limit=" + c2v.Value + "000";
        }
        if (c3.IsChecked == true)
        {
            adjline += " --fast-limit=" + c3v.Value + "000";
        }
        if (c4.IsChecked == true)
        {
            adjline += " --slow-limit=" + c4v.Value + "000";
        }
        if (c5.IsChecked == true)
        {
            adjline += " --stapm-time=" + c5v.Value;
        }
        if (c6.IsChecked == true)
        {
            adjline += " --slow-time=" + c6v.Value;
        }

        //vrm
        if (V1.IsChecked == true)
        {
            adjline += " --vrmmax-current=" + V1V.Value + "000";
        }
        if (V2.IsChecked == true)
        {
            adjline += " --vrm-current=" + V2V.Value + "000";
        }
        if (V3.IsChecked == true)
        {
            adjline += " --vrmsocmax-current=" + V3V.Value + "000";
        }
        if (V4.IsChecked == true)
        {
            adjline += " --vrmsoc-current=" + V4V.Value + "000";
        }
        if (V5.IsChecked == true)
        {
            adjline += " --psi0-current=" + V5V.Value + "000";
        }
        if (V6.IsChecked == true)
        {
            adjline += " --psi0soc-current=" + V6V.Value + "000";
        }
        if (V7.IsChecked == true)
        {
            adjline += " --prochot-deassertion-ramp=" + V7V.Value;
        }

        //gpu
        if (g1.IsChecked == true)
        {
            adjline += " --min-socclk-frequency=" + g1v.Value;
        }
        if (g2.IsChecked == true)
        {
            adjline += " --max-socclk-frequency=" + g2v.Value;
        }
        if (g3.IsChecked == true)
        {
            adjline += " --min-fclk-frequency=" + g3v.Value;
        }
        if (g4.IsChecked == true)
        {
            adjline += " --max-fclk-frequency=" + g4v.Value;
        }
        if (g5.IsChecked == true)
        {
            adjline += " --min-vcn=" + g5v.Value;
        }
        if (g6.IsChecked == true)
        {
            adjline += " --max-vcn=" + g6v.Value;
        }
        if (g7.IsChecked == true)
        {
            adjline += " --min-lclk=" + g7v.Value;
        }
        if (g8.IsChecked == true)
        {
            adjline += " --max-lclk=" + g8v.Value;
        }
        if (g9.IsChecked == true)
        {
            adjline += " --max-gfxclk=" + g9v.Value;
        }
        if (g10.IsChecked == true)
        {
            adjline += " --min-socclk-frequency=" + g10v.Value;
        }
        config.adjline = adjline;
        adjline = "";
        ConfigSave();
        MainWindow.Applyer.Apply();

        App.MainWindow.ShowMessageDialogAsync("You have successfully set your settings! \n" + config.adjline, "Setted successfully!");


    }


    private void Save_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (SaveName.Text != "")
        {
            if (profile.pr1 == false)
            {
                profile.pr1 = true;
                ProfileCOM_1.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                ProfileCOM_1.Content = SaveName.Text;
                ProfileCOM.SelectedIndex = 1;
                profile.pr1name = SaveName.Text;
                return;
            }
            else
            {
                if (profile.pr2 == false)
                {
                    profile.pr2 = true;
                    ProfileCOM_2.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                    ProfileCOM_2.Content = SaveName.Text;
                    ProfileCOM.SelectedIndex = 2;
                    profile.pr2name = SaveName.Text;
                    return;
                }
                else
                {
                    if (profile.pr3 == false)
                    {
                        profile.pr3 = true;
                        ProfileCOM_3.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                        ProfileCOM_3.Content = SaveName.Text;
                        ProfileCOM.SelectedIndex = 3;
                        profile.pr3name = SaveName.Text;
                        return;
                    }
                    else
                    {
                        if (profile.pr4 == false)
                        {
                            profile.pr4 = true;
                            ProfileCOM_4.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                            ProfileCOM_4.Content = SaveName.Text;
                            ProfileCOM.SelectedIndex = 4;
                            profile.pr4name = SaveName.Text;
                            return;
                        }
                        else
                        {
                            if (profile.pr5 == false)
                            {
                                profile.pr5 = true;
                                ProfileCOM_5.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                                ProfileCOM_5.Content = SaveName.Text;
                                ProfileCOM.SelectedIndex = 5;
                                profile.pr5name = SaveName.Text;
                                return;
                            }
                            else
                            {
                                if (profile.pr6 == false)
                                {
                                    profile.pr6 = true;
                                    ProfileCOM_6.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                                    ProfileCOM_6.Content = SaveName.Text;
                                    ProfileCOM.SelectedIndex = 6;
                                    profile.pr6name = SaveName.Text;
                                    return;
                                }
                                else
                                {
                                    if (profile.pr7 == false)
                                    {
                                        profile.pr7 = true;
                                        ProfileCOM_7.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                                        ProfileCOM_7.Content = SaveName.Text;
                                        ProfileCOM.SelectedIndex = 7;
                                        profile.pr7name = SaveName.Text;
                                        return;
                                    }
                                    else
                                    {
                                        if (profile.pr8 == false)
                                        {
                                            profile.pr8 = true;
                                            ProfileCOM_8.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                                            ProfileCOM_8.Content = SaveName.Text;
                                            ProfileCOM.SelectedIndex = 8;
                                            profile.pr8name = SaveName.Text;
                                            return;
                                        }
                                        else
                                        {
                                            if (profile.pr9 == false)
                                            {
                                                profile.pr9 = true;
                                                ProfileCOM_9.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                                                ProfileCOM_9.Content = SaveName.Text;
                                                ProfileCOM.SelectedIndex = 9;
                                                profile.pr9name = SaveName.Text;
                                                return;
                                            }
                                            else
                                            {
                                                App.MainWindow.ShowMessageDialogAsync("You can't add more than 9 profiles at one time! \n", "Profiles error!"); 
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        else
        {
            App.MainWindow.ShowMessageDialogAsync("You can't add profile without name! \n", "Corrupted Name!");
        }
        ProfileSave();
    }
    private void Edit_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (SaveName.Text != "")
        {
            switch (ProfileCOM.SelectedIndex)
            {
                case 0:
                    App.MainWindow.ShowMessageDialogAsync("You can't rename unsaved preset! \n", "Corrupted Name!");
                    break;
                case 1:
                    ProfileCOM_1.Content = SaveName.Text;
                    profile.pr1name = SaveName.Text;
                    ProfileCOM.SelectedIndex = 0;
                    ProfileCOM.SelectedIndex = 1;
                    break;
                case 2:
                    ProfileCOM_2.Content = SaveName.Text;
                    profile.pr2name = SaveName.Text;
                    ProfileCOM.SelectedIndex = 0;
                    ProfileCOM.SelectedIndex = 2;
                    break;
                case 3:
                    ProfileCOM_3.Content = SaveName.Text;
                    profile.pr3name = SaveName.Text;
                    ProfileCOM.SelectedIndex = 0;
                    ProfileCOM.SelectedIndex = 3;
                    break;
                case 4:
                    ProfileCOM_4.Content = SaveName.Text;
                    profile.pr4name = SaveName.Text;
                    ProfileCOM.SelectedIndex = 0;
                    ProfileCOM.SelectedIndex = 4;
                    break;
                case 5:
                    ProfileCOM_5.Content = SaveName.Text;
                    profile.pr5name = SaveName.Text;
                    ProfileCOM.SelectedIndex = 0;
                    ProfileCOM.SelectedIndex = 5;
                    break;
                case 6:
                    ProfileCOM_6.Content = SaveName.Text;
                    profile.pr6name = SaveName.Text;
                    ProfileCOM.SelectedIndex = 0;
                    ProfileCOM.SelectedIndex = 6;
                    break;
                case 7:
                    ProfileCOM_7.Content = SaveName.Text;
                    profile.pr7name = SaveName.Text;
                    ProfileCOM.SelectedIndex = 0;
                    ProfileCOM.SelectedIndex = 7;
                    break;
                case 8:
                    ProfileCOM_8.Content = SaveName.Text;
                    profile.pr8name = SaveName.Text;
                    ProfileCOM.SelectedIndex = 0;
                    ProfileCOM.SelectedIndex = 8;
                    break;
                case 9:
                    ProfileCOM_9.Content = SaveName.Text;
                    profile.pr9name = SaveName.Text;
                    ProfileCOM.SelectedIndex = 0;
                    ProfileCOM.SelectedIndex = 9;
                    break;
            }
            ProfileSave();
        }
        else
        {
            App.MainWindow.ShowMessageDialogAsync("You can't edit profile without name! \n", "Corrupted Name!");
        }
    }
    private async void Delete_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ContentDialog DelDialog = new ContentDialog
        {
            Title = "Delete preset",
            Content = "Did you really want to delete this preset?",
            CloseButtonText = "Cancel",
            PrimaryButtonText = "Delete",
            DefaultButton = ContentDialogButton.Close
        };

        // Use this code to associate the dialog to the appropriate AppWindow by setting
        // the dialog's XamlRoot to the same XamlRoot as an element that is already present in the AppWindow.
        if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
        {
            DelDialog.XamlRoot = XamlRoot;
        }

        ContentDialogResult result = await DelDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            switch (ProfileCOM.SelectedIndex)
            {
                case 0:
                    await App.MainWindow.ShowMessageDialogAsync("You can't delete unsaved preset!", "Can't Delete!");
                    break;
                case 1:
                    profile.pr1 = false;
                    ProfileCOM.SelectedIndex = 0;
                    ProfileCOM_1.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                    break;
                case 2:
                    profile.pr2 = false;
                    ProfileCOM.SelectedIndex = 0;
                    ProfileCOM_2.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                    break;
                case 3:
                    profile.pr3 = false;
                    ProfileCOM.SelectedIndex = 0;
                    ProfileCOM_3.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                    break;
                case 4:
                    profile.pr4 = false;
                    ProfileCOM.SelectedIndex = 0;
                    ProfileCOM_4.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                    break;
                case 5:
                    profile.pr5 = false;
                    ProfileCOM.SelectedIndex = 0;
                    ProfileCOM_5.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                    break;
                case 6:
                    profile.pr6 = false;
                    ProfileCOM.SelectedIndex = 0;
                    ProfileCOM_6.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                    break;
                case 7:
                    profile.pr7 = false;
                    ProfileCOM.SelectedIndex = 0;
                    ProfileCOM_7.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                    break;
                case 8:
                    profile.pr8 = false;
                    ProfileCOM.SelectedIndex = 0;
                    ProfileCOM_8.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                    break;
                case 9:
                    profile.pr9 = false;
                    ProfileCOM.SelectedIndex = 0;
                    ProfileCOM_9.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                    break;
            }
            ProfileSave();
        }
        else
        {
            
        }
        
    }



#pragma warning restore CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.
#pragma warning restore CS8601 // Возможно, назначение-ссылка, допускающее значение NULL.
}
