using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.JsonContainers;
using Saku_Overclock.SMUEngine;
using ZenStates.Core;

namespace Saku_Overclock.Views;
internal partial class PowerWindow : Window, IDisposable
{
    private Cpu? CPU;
    private Visibility mode = Visibility.Visible;
    private int noteColumnOverrider = 3;
    private Powercfg? notes = new();
    private PowerMonRTSS rtssTable = new();
    private ObservableCollection<PowerMonitorItem>? PowerGridItems;
    public PowerWindow(Cpu? cpu)
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
        PowerCfgTimer.Tick += new EventHandler(PowerCfgTimer_Tick!);
        PowerCfgTimer.Stop();
        InitializeComponent();
        cpu?.RefreshPowerTable();
        notes = new Powercfg();
        FillInData(cpu?.powerTable.Table!);
        CPU = cpu; // Добавим инициализацию CPU здесь
        Closed += PowerWindow_Closed;
    }
    #region JSON containers
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
            notes = JsonConvert.DeserializeObject<Powercfg>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\PowerMon\\powercfg.json"))!;
        }
        catch
        {
            JsonRepair('n');
        }
    }
    public void RtssSave()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\", "PowerMon"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\PowerMon\\rtsscfg.json", JsonConvert.SerializeObject(notes, Formatting.Indented));
        }
        catch { }
    }
    public void RtssLoad()
    {
        try
        {
            rtssTable = JsonConvert.DeserializeObject<PowerMonRTSS>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\PowerMon\\rtsscfg.json"))!;
        }
        catch
        {
            JsonRepair('r');
        }
    }
    public void JsonRepair(char file)
    {
        if (file == 'n')
        {
            if (notes != null) //перепроверка на соответствие, восстановление конфига.
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
            else //Всё же если за 5 раз пересканирования файл пуст
            {
                notes = new Powercfg();
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
            try
            {
                for (var j = 0; j < 5; j++)
                {
                    notes = JsonConvert.DeserializeObject<Powercfg>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\PowerMon\\powercfg.json"))!;
                    if (notes != null) { break; }
                }
            }
            catch
            {
                App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                App.MainWindow.Close();
            }
        }
        if (file == 'r')
        {
            if (rtssTable != null) //перепроверка на соответствие, восстановление конфига.
            {
                try
                {
                    Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\", "PowerMon"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\PowerMon\\rtsscfg.json", JsonConvert.SerializeObject(rtssTable));
                }
                catch
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\PowerMon\\rtsscfg.json");
                    Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\", "PowerMon"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\PowerMon\\rtsscfg.json", JsonConvert.SerializeObject(rtssTable));
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }
            }
            else //Всё же если за 5 раз пересканирования файл пуст
            {
                rtssTable = new PowerMonRTSS();
                try
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\PowerMon\\rtsscfg.json");
                    Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\", "PowerMon"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\PowerMon\\rtsscfg.json", JsonConvert.SerializeObject(rtssTable));
                    App.MainWindow.Close();
                }
                catch
                {
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }
            }
            try
            {
                for (var j = 0; j < 5; j++)
                {
                    rtssTable = JsonConvert.DeserializeObject<PowerMonRTSS>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\PowerMon\\rtsscfg.json"))!;
                    if (rtssTable != null) { break; }
                }
            }
            catch
            {
                App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                App.MainWindow.Close();
            }

        }
    }
    #endregion
    #region Event Handlers
    public void Dispose() => GC.SuppressFinalize(this);

    private void PowerWindow_Closed(object sender, WindowEventArgs args)
    {
        //CPU?.powerTable.Dispose();
        CPU = null;
        UnloadObject(PowerGridView);
        Dispose();
        notes = null;
    }
    private void PowerWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        App.AppTitlebar = AppTitleBarText as UIElement;
    }
    private readonly System.Windows.Forms.Timer PowerCfgTimer = new();
    private void PowerCfgTimer_Tick(object sender, EventArgs e)
    {
        if (CPU?.RefreshPowerTable() == ZenStates.Core.SMU.Status.OK)
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
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        mode = mode == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;
        IndexName.Text = IndexName.Text == "Index" ? "OSD" : "Index";
        OffsetName.Text = OffsetName.Text == "Offset" ? "Color #" : "Offset";
        NoteName.Text = NoteName.Text == "Quick Note" ? "OSD Name (value)" : "Quick Note";
        noteColumnOverrider = noteColumnOverrider == 3 ? 4 : 3;
        NoteName.Margin = mode == Visibility.Visible ? new(10, 0, 0, 0) : new(100, 0, 0, 0);
        NotePos.Margin = new(12, 0, 0, 0);
        NotePos.Visibility = mode == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    }
    #endregion
    #region PowerMon PowerTable voids
    private class PowerMonitorItem : INotifyPropertyChanged
    {
        private Visibility _rtss = Visibility.Collapsed;
        private Visibility _normal = Visibility.Visible;
        private string? _value;
        private string? _note;
        private string? _notevalue;
        private bool _osd;
        private int _row;
        private int _column;
        private Thickness _notecolumn = new(10, 0, 0, 0);
        public string? Index
        {
            get; set;
        }
        public string? Offset
        {
            get; set;
        }
        public string? Value
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
        public string? Note
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
        public bool Osd
        {
            get => _osd;
            set
            {
                if (_osd != value)
                {
                    _osd = value;
                    OnPropertyChanged(nameof(Osd));
                }
            }
        }
        public int Row
        {
            get => _row;
            set
            {
                if (_row != value)
                {
                    _row = value;
                    OnPropertyChanged(nameof(Row));
                }
            }
        }
        public int Column
        {
            get => _column;
            set
            {
                if (_column != value)
                {
                    _column = value;
                    OnPropertyChanged(nameof(Column));
                }
            }
        }
        public Thickness NoteColumn
        {
            get => _notecolumn;
            set
            {
                if (_notecolumn != value)
                {
                    _notecolumn = value;
                    OnPropertyChanged(nameof(NoteColumn));
                }
            }
        }
        public Visibility Rtss
        {
            get => _rtss;
            set
            {
                if (_rtss != value)
                {
                    _rtss = value;
                    OnPropertyChanged(nameof(Rtss));
                }
            }
        }
        public Visibility Normal
        {
            get => _normal;
            set
            {
                if (_normal != value)
                {
                    _normal = value;
                    OnPropertyChanged(nameof(Normal));
                }
            }
        }
        public string? NoteValue
        {
            get => _notevalue;
            set
            {
                if (_notevalue != value)
                {
                    _notevalue = value;
                    OnPropertyChanged(nameof(NoteValue));
                }
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    private async void FillInData(float[] table)
    {
        await Task.Run(() =>
        {
            NoteLoad();
            RtssLoad();
            PowerGridItems = [];
            for (var i = 0; i < table.Length; i++)
            {
                if (notes?._notelist.Count <= i)
                {
                    notes._notelist.Add(" ");
                }
                var subItem = new PowerMonitorItem
                {
                    Rtss = mode == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible,
                    Normal = mode,
                    Index = $"{i:D4}",
                    Offset = $"0x{i * 4:X4}",
                    Value = $"{table[i]:F6}",
                    Note = notes?._notelist[i]
                };
               /* try
                {
                    while (rtssTable == null)
                    {
                        RtssLoad();
                    }
                    while (i >= rtssTable.Elements.Count)
                    {
                        rtssTable.Elements.Add(new PowerMonRTSSElement()); // Добавление нового элемента с параметрами по умолчанию
                    }  
                    subItem.Osd = rtssTable!.Elements[i].IsShown;
                    subItem.Column = rtssTable.Elements[i].Column;
                    subItem.Row = rtssTable.Elements[i].Row;
                    subItem.NoteValue = rtssTable?.Elements[i].Color;
                }
                catch
                {
                    //unregistered
                }*/
                PowerGridItems.Add(subItem);
            }
        });
        PowerGridView.ItemsSource = PowerGridItems;
        PowerCfgTimer.Start();
    }
    private void RefreshData(float[] table)
    {
        try
        {
            var index = 0;
            foreach (var item in PowerGridItems!)
            {
                var saveFlag = false; // Если есть изменения - сохранить
                item.Rtss = mode == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                item.Normal = mode;
                item.NoteColumn = mode == Visibility.Visible ? new(10, 0, 0, 0) : new(100, 0, 0, 0);
                //Конец безопасной зоны

                // Проверка и добавление элемента, если индекс выходит за пределы массива
            /*    while (index >= rtssTable.Elements.Count)
                {
                    rtssTable.Elements.Add(new PowerMonRTSSElement()); // Добавление нового элемента с параметрами по умолчанию
                }*/
                /*if (item.Osd != rtssTable?.Elements[index].IsShown!)
                {
                    rtssTable!.Elements[index].IsShown = item.Osd; saveFlag = true;
                }
                if (item.Column != rtssTable?.Elements[index].Column!)
                {
                    rtssTable!.Elements[index].Column = item.Column; saveFlag = true;
                }
                if (item.Row != rtssTable?.Elements[index].Row!)
                {
                    rtssTable!.Elements[index].Row = item.Row; saveFlag = true;
                }
                if (item.NoteValue != string.Empty && item.NoteValue != rtssTable?.Elements[index].Color!)
                {
                    rtssTable!.Elements[index].Color = item.NoteValue!; saveFlag = true;
                }*/

              /*  if (item.Note != string.Empty && item.Note != rtssTable?.Elements[index].Name!)
                {
                    var note = item.Note!;

                    // Условие 1: Сохранить имя до начала скобки и после неё, заменив скобку пробелом
                    //Сохранит имя до начала скобки, то есть если "Имя (Да) Крутое", сохранит только "Имя Крутое", 
                    //поставив пробел вместо скобок, если его там нет
                    if (note.Contains('('))
                    {
                        var startIndex = note.IndexOf('(');
                        var endIndex = note.IndexOf(')', startIndex);

                        if (endIndex != -1)
                        {
                            var beforeBracket = note[..startIndex].Trim();
                            var afterBracket = note[(endIndex + 1)..].Trim();

                            //rtssTable!.Elements[index].Name = $"{beforeBracket} {afterBracket}".Trim();
                            //saveFlag = true;
                        }
                    }

                    // Условие 2: Сохранить имя внутри скобки, удаляя остальные части строки
                    //Сохранит имя ТОЛЬКО внутри скобки, то есть если "Имя (Да) Крутое", сохранит только "Да", Оставив только Да.
                    //Если "Имя(Очень) Крутое (И длинное)", то оставит ТОЛЬКО "Очень И длинное" поставив пробел
                    if (note.Contains('('))
                    {
                        var startIndex = note.IndexOf('(') + 1;
                        var endIndex = note.IndexOf(')', startIndex);

                        if (endIndex != -1)
                        {
                            var insideBracket = note[startIndex..endIndex].Trim();
                            var afterSecondBracket = note[(endIndex + 1)..].Trim();

                            rtssTable!.Elements[index].Name = $"{insideBracket} {afterSecondBracket}".Trim();
                            saveFlag = true;
                        }
                    }

                }*/
                if (saveFlag && rtssTable != null && rtssTable.Elements != null)
                {
                    rtssTable.Elements[index].Offset = $"0x{index * 4:X4}";
                    RtssSave();
                }

                //Безопасная зона 
                item.Value = $"{table[index]:F6}"; // Обновление информации
                if (item.Note != notes?._notelist[index])
                {
                    notes!._notelist[index] = item.Note!;
                    NoteSave();
                }

                index++;
            }
            // Явное обновление GridView
            PowerGridView.ItemsSource = PowerGridItems;
        }
        catch
        {
            App.MainWindow.Close();
            Close();
        }
    } 
    #endregion
}