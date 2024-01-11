using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using Saku_Overclock.ViewModels;
namespace Saku_Overclock.Views;
#pragma warning disable CS4014 // Так как этот вызов не ожидается, выполнение существующего метода продолжается до тех пор, пока вызов не будет завершен
#pragma warning disable CS8600 // Преобразование литерала, допускающего значение NULL или возможного значения NULL в тип, не допускающий значение NULL.
#pragma warning disable IDE0059 // Ненужное присваивание значения
#pragma warning disable IDE0044 // Ненужное присваивание значения
#pragma warning disable CS8601 // Возможно, назначение-ссылка, допускающее значение NULL.
public sealed partial class ИнформацияPage : Page
{
    private Config config = new();
    private DispatcherTimer timer = new();
    public ИнформацияViewModel ViewModel
    {
        get;
    }
    public ИнформацияPage()
    {
        ViewModel = App.GetService<ИнформацияViewModel>();
        InitializeComponent();
        desc();
        ConfigLoad();
        InitInf();
        config.fanex = false;
        ConfigSave();
    }
    public void InitInf()
    {
        ConfigLoad();
        if (config.reapplyinfo == true) { Absc.IsChecked = true; } else { Absc.IsChecked = false; }
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

    private void desc()
    {
        if (richTextBox1.Text == "")
        {
            DescText.Opacity = 0;
        }
        else
        {
            DescText.Opacity = 1;
        }
    }
    private void xx_Click(object sender, RoutedEventArgs e)
    {
        richTextBox1.Text = "";
        Process p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = @"ryzenadj.exe";
        p.StartInfo.Arguments = "-i";
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;
        p.Start();
        StreamReader outputWriter = p.StandardOutput;
        var line = outputWriter.ReadLine();
        while (line != null)
        {
            if (line != "")
            {
                richTextBox1.Text = richTextBox1.Text + "\n" + line;
                DescText.Opacity = 1;
            }
            line = outputWriter.ReadLine();
        }
        p.WaitForExit();
        line = null;
    }
    private async void CheckBox_Checked(object sender, RoutedEventArgs e)
    {
        await Task.Delay(20);
        if (Absc.IsChecked == true) { config.reapplyinfo = true; ConfigSave(); } else { config.reapplyinfo = false; ConfigSave(); }

        // Обновите Textblock и скрипт через заданный интервал времени
        try
        {
            timer.Interval = TimeSpan.FromMilliseconds(numberBox.Value);
        }
        catch
        {
            App.MainWindow.ShowMessageDialogAsync("Время автообновления информации некорректно и было исправлено на 100 мс", "Критическая ошибка!");
            numberBox.Value = 100;
            timer.Interval = TimeSpan.FromMilliseconds(numberBox.Value);
        }
        ConfigLoad();
        config.tempex = true;
        ConfigSave();
        timer.Tick += (sender, e) =>
        {
            if (Absc.IsChecked == true)
            {
                // Запустите ryzenadj снова
                Process();
            }

        };
        timer.Start();
    }
    private void Process()
    {
        if (config.tempex == true)
        {
            richTextBox1.Text = "";
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.FileName = @"ryzenadj.exe";
            p.StartInfo.Arguments = "-i";
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.Start();
            StreamReader outputWriter = p.StandardOutput;
            var errorReader = p.StandardError.ReadToEnd();
            var line = outputWriter.ReadLine();
            while (line != null)
            {
                if (line != "")
                {
                    richTextBox1.Text = richTextBox1.Text + "\n" + line;
                    DescText.Opacity = 1;
                }
                line = outputWriter.ReadLine();
            }
            p.WaitForExit();
            line = null;
        }
        else { timer.Stop(); }
    }
}
#pragma warning restore IDE0059 // Ненужное присваивание значения
#pragma warning restore CS8600 // Преобразование литерала, допускающего значение NULL или возможного значения NULL в тип, не допускающий значение NULL.
#pragma warning restore CS4014 // Так как этот вызов не ожидается, выполнение существующего метода продолжается до тех пор, пока вызов не будет завершен
#pragma warning restore CS8601 // Возможно, назначение-ссылка, допускающее значение NULL.