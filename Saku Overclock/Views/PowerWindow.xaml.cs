using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Saku_Overclock.Helpers;
using WinUIEx;
namespace Saku_Overclock.Views;
#pragma warning disable CS8622 // ƒопустимость значений NULL дл€ ссылочных типов в типе параметра не соответствует целевому объекту делегировани€ (возможно, из-за атрибутов допустимости значений NULL).
#pragma warning disable CS8618 // ѕоле, не допускающее значени€ NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. ¬озможно, стоит объ€вить поле как допускающее значени€ NULL.
#pragma warning disable CS8612 // ƒопустимость значени€ NULL дл€ ссылочных типов в типе не совпадает с €вно реализованным членом.
internal partial class PowerWindow : Window
{
    private readonly Services.Cpu CPU;
    private ObservableCollection<PowerMonitorItem> PowerGridItems;
    public PowerWindow(Services.Cpu cpu)
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        Activated += PowerWindow_Activated;
        AppTitleBarText.Text = "Saku PowerMon";
        this.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets/powermon.ico"));
        this.SetWindowSize(342, 579);
        PowerCfgTimer.Interval = 2000;
        PowerCfgTimer.Tick += new EventHandler(PowerCfgTimer_Tick);
        InitializeComponent();
        cpu.RefreshPowerTable();
        FillInData(cpu.powerTable.Table);
        CPU = cpu; // ƒобавим инициализацию CPU здесь
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
    private async void FillInData(float[] table)
    {
        await Task.Run(() =>
        {
            PowerGridItems = new ObservableCollection<PowerMonitorItem>();
            for (var i = 0; i < table.Length; i++)
            {
                PowerGridItems.Add(new PowerMonitorItem
                {
                    Index = $"{i:D4}",
                    Offset = $"0x{i * 4:X4}",
                    Value = $"{table[i]:F6}",
                    Note = string.Empty
                });
            }
        });
        PowerGridView.ItemsSource = PowerGridItems;
    }
    private void RefreshData(float[] table)
    {
        var index = 0;
        foreach (var item in PowerGridItems)
        {
            item.Value = $"{table[index]:F6}";
            index++;
        }
        // явное обновление GridView
        PowerGridView.ItemsSource = PowerGridItems;
    }

    private void PowerCfgTimer_Tick(object sender, EventArgs e)
    {
        if (CPU.RefreshPowerTable() == Services.SMU.Status.OK)
        {
            RefreshData(CPU.powerTable.Table);
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
#pragma warning restore CS8622 // ƒопустимость значений NULL дл€ ссылочных типов в типе параметра не соответствует целевому объекту делегировани€ (возможно, из-за атрибутов допустимости значений NULL).
#pragma warning restore CS8618 // ѕоле, не допускающее значени€ NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. ¬озможно, стоит объ€вить поле как допускающее значени€ NULL.
#pragma warning restore CS8612 // ƒопустимость значени€ NULL дл€ ссылочных типов в типе не совпадает с €вно реализованным членом.