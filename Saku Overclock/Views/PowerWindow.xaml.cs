using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Newtonsoft.Json;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
namespace Saku_Overclock.Views;
#pragma warning disable CS8622 // Допустимость значений NULL для ссылочных типов в типе параметра не соответствует целевому объекту делегирования (возможно, из-за атрибутов допустимости значений NULL).
#pragma warning disable CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.
#pragma warning disable CS8612 // Допустимость значения NULL для ссылочных типов в типе не совпадает с явно реализованным членом.
#pragma warning disable CS8601 // Возможно, назначение-ссылка, допускающее значение NULL.
internal partial class PowerWindow : Window
{
    private readonly SMUEngine.Cpu CPU;
    private Powercfg notes = new();
    private ObservableCollection<PowerMonitorItem> PowerGridItems;
    public PowerWindow(SMUEngine.Cpu cpu)
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        Activated += PowerWindow_Activated;
        AppTitleBarText.Text = "Saku PowerMon";
        this.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets/powermon.ico"));
        this.SetWindowSize(342, 579);
        NoteLoad();
        PowerCfgTimer.Interval = 2000;
        PowerCfgTimer.Tick += new EventHandler(PowerCfgTimer_Tick);
        InitializeComponent();
        cpu.RefreshPowerTable();
        notes = new Powercfg();
        FillInData(cpu.PowerTable.Table);
        CPU = cpu; // Добавим инициализацию CPU здесь
        PowerCfgTimer.Start();
    }
    private void PowerWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        App.AppTitlebar = AppTitleBarText as UIElement;
    }
    private readonly System.Windows.Forms.Timer PowerCfgTimer = new();
    private class PowerMonitorItem : INotifyPropertyChanged
    {
        private string _value;
        private string _note;
        public string Index
        {
            get; set;
        }
        public string Offset
        {
            get; set;
        }
        public string Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged(nameof(Value));
                }
            }
        }
        public string Note
        {
            get => _note;
            set
            {
                if (_note != value)
                {
                    _note = value;
                    OnPropertyChanged(nameof(Note));
                }
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        } 
    }
    public void NoteSave()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\", "PowerMon"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\PowerMon\\powercfg.json", JsonConvert.SerializeObject(notes, Formatting.Indented));
        }
        catch { }
    }
    public void NoteLoad()
    {
        try
        {
            notes = JsonConvert.DeserializeObject<Powercfg>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\PowerMon\\powercfg.json"));
        }
        catch
        {
            JsonRepair('p');
        }
    }
    public void JsonRepair(char file)
    {  
        if (file == 'p')
        {
            try
            {
                for (var j = 0; j < 5; j++)
                {
                    notes = new Powercfg();
                }
            }
            catch
            {
                App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                App.MainWindow.Close();
            }
            if (notes != null)
            {
                try
                {
                    Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\", "PowerMon"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\PowerMon\\powercfg.json", JsonConvert.SerializeObject(notes));
                }
                catch
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\PowerMon\\powercfg.json");
                    Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\", "PowerMon"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\PowerMon\\powercfg.json", JsonConvert.SerializeObject(notes));
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }
            }
            else
            {
                try
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\PowerMon\\powercfg.json");
                    Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\", "PowerMon"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\PowerMon\\powercfg.json", JsonConvert.SerializeObject(notes));
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
    private void FillInData(float[] table)
    { 
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Run(() =>
            {
                NoteLoad();
                PowerGridItems = new ObservableCollection<PowerMonitorItem>();
                for (var i = 0; i < table.Length; i++)
                {
                    try { if (notes._notelist.Count <= i) { notes._notelist.Add(" "); } } 
                    catch {  }
                    PowerGridItems.Add(new PowerMonitorItem
                    {
                        Index = $"{i:D4}",
                        Offset = $"0x{i * 4:X4}",
                        Value = $"{table[i]:F6}",
                        Note = notes._notelist[i]
                    });
                }
            });
            PowerGridView.ItemsSource = PowerGridItems;
        });
    }
    private void RefreshData(float[] table)
    {
        try
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                var index = 0;
                foreach (var item in PowerGridItems)
                {
                    item.Value = $"{table[index]:F6}";
                    if (item.Note != notes._notelist[index])
                    {
                        notes._notelist[index] = item.Note;
                        NoteSave();
                    }
                    index++;
                }
                // Явное обновление GridView
                PowerGridView.ItemsSource = PowerGridItems;
            });
        }
        catch
        {
            App.MainWindow.Close();
            Close();
        } 
    }

    private void PowerCfgTimer_Tick(object sender, EventArgs e)
    {
        if (CPU.RefreshPowerTable() == SMUEngine.SMU.Status.OK)
        {
            RefreshData(CPU.PowerTable.Table);
        }
    }
    private void Button_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            PowerCfgTimer.Interval = Convert.ToInt32(numericUpDownInterval.Value);
        }
        catch
        {
            numericUpDownInterval.Value = 2000;
            PowerCfgTimer.Interval = Convert.ToInt32(numericUpDownInterval.Value);
        }
    }
}
#pragma warning restore CS8622 // Допустимость значений NULL для ссылочных типов в типе параметра не соответствует целевому объекту делегирования (возможно, из-за атрибутов допустимости значений NULL).
#pragma warning restore CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.
#pragma warning restore CS8612 // Допустимость значения NULL для ссылочных типов в типе не совпадает с явно реализованным членом.