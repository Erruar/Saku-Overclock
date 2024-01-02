using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using Saku_Overclock.ViewModels;

namespace Saku_Overclock.Views;

public sealed partial class ПараметрыPage : Page
{
    public ПараметрыViewModel ViewModel
    {
        get;
    }

    private Config config = new Config();

    private Devices devices = new Devices();

    private Profile profile = new Profile();

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
    }

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

    public async void SlidersInit()
    {
        DeviceLoad();
        InitializeComponent();
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
        //TODO СОздать итератор для своевременной загрузки того, что ниже
        
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


    //Параметры процессора
    //Максимальная температура CPU (C)
    private void c1_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (c1.IsChecked == true)
        {
            devices.c1 = true;
            devices.c1v = c1v.Value;
            DeviceSave();

        }
        else
        {
            devices.c1 = false;
            devices.c1v = 90;
            DeviceSave();
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
        }
        else
        {
            devices.c2 = false;
            devices.c2v = 20;
            DeviceSave();
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
        }
        else
        {
            devices.c3 = false;
            devices.c3v = 25;
            DeviceSave();
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
        }
        else
        {
            devices.c4 = false;
            devices.c4v = 25;
            DeviceSave();
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
        }
        else
        {
            devices.c5 = false;
            devices.c5v = 128;
            DeviceSave();
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
        }
        else
        {
            devices.c6 = false;
            devices.c6v = 64;
            DeviceSave();
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
        }
        else
        {
            devices.v1 = false;
            devices.v1v = 64;
            DeviceSave();
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
        }
        else
        {
            devices.v2 = false;
            devices.v2v = 55;
            DeviceSave();
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
        }
        else
        {
            devices.v3 = false;
            devices.v3v = 13;
            DeviceSave();
        }
    }
    //Лимит по току SOC A
    private void v4_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (V1.IsChecked == true)
        {
            devices.v4 = true;
            devices.v4v = V4V.Value;
            DeviceSave();
        }
        else
        {
            devices.v4 = false;
            devices.v4v = 10;
            DeviceSave();
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
        }
        else
        {
            devices.v5 = false;
            devices.v5v = 13;
            DeviceSave();
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
        }
        else
        {
            devices.v6 = false;
            devices.v6v = 5;
            DeviceSave();
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
        }
        else
        {
            devices.v7 = false;
            devices.v7v = 2;
            DeviceSave();
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
        }
        else
        {
            devices.g1 = false;
            devices.g1v = 800;
            DeviceSave();
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
        }
        else
        {
            devices.g2 = false;
            devices.g2v = 1200;
            DeviceSave();
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
        }
        else
        {
            devices.g3 = false;
            devices.g3v = 800;
            DeviceSave();
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
        }
        else
        {
            devices.g4 = false;
            devices.g4v = 1200;
            DeviceSave();
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
        }
        else
        {
            devices.g5 = false;
            devices.g5v = 400;
            DeviceSave();
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
        }
        else
        {
            devices.g6 = false;
            devices.g6v = 1200;
            DeviceSave();
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
        }
        else
        {
            devices.g7 = false;
            devices.g7v = 400;
            DeviceSave();
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
        }
        else
        {
            devices.g8 = false;
            devices.g8v = 1200;
            DeviceSave();
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
        }
        else
        {
            devices.g9 = false;
            devices.g9v = 400;
            DeviceSave();
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
        }
        else
        {
            devices.g10 = false;
            devices.g10v = 1200;
            DeviceSave();
        }
    }

    //Параметры процессора, при изменении слайдеров
    //Максимальная температура CPU (C)

    private void c1_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (c1.IsChecked == true)
        {
            devices.c1 = true;
            devices.c1v = c1v.Value;
            DeviceSave();
        }
        else { SlidersInit(); }
    }
    //Лимит CPU (W)

    private void c2_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (c2.IsChecked == true)
        {
            devices.c2 = true;
            devices.c2v = c2v.Value;
            DeviceSave();
        }
    }
    //Реальный CPU (W)

    private void c3_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (c3.IsChecked == true)
        {
            devices.c3 = true;
            devices.c3v = c3v.Value;
            DeviceSave();
        }
    }
    //Средний CPU(W)

    private void c4_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (c4.IsChecked == true)
        {
            devices.c4 = true;
            devices.c4v = c4v.Value;
            DeviceSave();
        }
    }
    //Тик быстрого разгона (S)

    private void c5_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (c5.IsChecked == true)
        {
            devices.c5 = true;
            devices.c5v = c5v.Value;
            DeviceSave();
        }
    }
    //Тик медленного разгона (S)

    private void c6_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (c6.IsChecked == true)
        {
            devices.c6 = true;
            devices.c6v = c6v.Value;
            DeviceSave();
        }
    }

    //Параметры VRM
    private void v1v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (V1.IsChecked == true)
        {
            devices.v1 = true;
            devices.v1v = V1V.Value;
            DeviceSave();
        }
        load = true;
    }

    private void v2v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (V2.IsChecked == true)
        {
            devices.v2 = true;
            devices.v2v = V2V.Value;
            DeviceSave();
        }
    }

    private void v3v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (V3.IsChecked == true)
        {
            devices.v3 = true;
            devices.v3v = V3V.Value;
            DeviceSave();
        }
    }

    private void v4v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (V4.IsChecked == true)
        {
            devices.v4 = true;
            devices.v4v = V4V.Value;
            DeviceSave();
        }
    }

    private void v5v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (V5.IsChecked == true)
        {
            devices.v5 = true;
            devices.v5v = V5V.Value;
            DeviceSave();
        }
    }

    private void v6v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (V6.IsChecked == true)
        {
            devices.v6 = true;
            devices.v6v = V6V.Value;
            DeviceSave();
        }
    }

    private void v7v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (V7.IsChecked == true)
        {
            devices.v7 = true;
            devices.v7v = V7V.Value;
            DeviceSave();
        }
    }
    //Параметры GPU
    private void g1v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (g1.IsChecked == true)
        {
            devices.g1 = true;
            devices.g1v = g1v.Value;
            DeviceSave();
        }
    }

    private void g2v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (g2.IsChecked == true)
        {
            devices.g2 = true;
            devices.g2v = g2v.Value;
            DeviceSave();
        }
    }

    private void g3v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (g3.IsChecked == true)
        {
            devices.g3 = true;
            devices.g3v = g3v.Value;
            DeviceSave();
        }
    }

    private void g4v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (g4.IsChecked == true)
        {
            devices.g4 = true;
            devices.g4v = g4v.Value;
            DeviceSave();
        }
    }

    private void g5v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (g5.IsChecked == true)
        {
            devices.g5 = true;
            devices.g5v = g5v.Value;
            DeviceSave();
        }
    }

    private void g6v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (g6.IsChecked == true)
        {
            devices.g6 = true;
            devices.g6v = g6v.Value;
            DeviceSave();
        }
    }

    private void g7v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (g7.IsChecked == true)
        {
            devices.g7 = true;
            devices.g7v = g7v.Value;
            DeviceSave();
        }
    }

    private void g8v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (g8.IsChecked == true)
        {
            devices.g8 = true;
            devices.g8v = g8v.Value;
            DeviceSave();
        }
    }

    private void g9v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (g9.IsChecked == true)
        {
            devices.g9 = true;
            devices.g9v = g9v.Value;
            DeviceSave();
        }
    }

    private void g10v_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (g10.IsChecked == true)
        {
            devices.g10 = true;
            devices.g10v = g10v.Value;
            DeviceSave();
        }
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

    private void Expander_Expanding(Expander sender, ExpanderExpandingEventArgs args)
    {

    }
}
