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

internal partial class PowerWindow : IDisposable
{
    private Cpu? _cpu;
    private Visibility _mode = Visibility.Visible;
    private int _noteColumnOverrider = 3;
    private Powercfg? _notes;
    private PowerMonRtss _rtssTable = new();
    private ObservableCollection<PowerMonitorItem>? _powerGridItems;

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
        _powerCfgTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
        _powerCfgTimer.Tick += PowerCfgTimer_Tick;
        _powerCfgTimer.Stop();
        InitializeComponent();
        cpu?.RefreshPowerTable();
        _notes = new Powercfg();
        FillInData(cpu?.powerTable.Table!);
        _cpu = cpu; // Добавим инициализацию CPU здесь
        Closed += PowerWindow_Closed;
    }

    #region JSON containers

    private void NoteSave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                "SakuOverclock"));
            Directory.CreateDirectory(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\", "PowerMon"));
            File.WriteAllText(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                @"\SakuOverclock\PowerMon\powercfg.json", JsonConvert.SerializeObject(_notes, Formatting.Indented));
        }
        catch
        {
            // ignored
        }
    }

    private void NoteLoad()
    {
        try
        {
            _notes = JsonConvert.DeserializeObject<Powercfg>(File.ReadAllText(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                @"\SakuOverclock\PowerMon\powercfg.json"))!;
        }
        catch
        {
            JsonRepair('n');
        }
    }

    private void RtssSave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                "SakuOverclock"));
            Directory.CreateDirectory(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\", "PowerMon"));
            File.WriteAllText(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\PowerMon\rtsscfg.json",
                JsonConvert.SerializeObject(_notes, Formatting.Indented));
        }
        catch
        {
            // ignored
        }
    }

    private void RtssLoad()
    {
        try
        {
            _rtssTable = JsonConvert.DeserializeObject<PowerMonRtss>(File.ReadAllText(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                @"\SakuOverclock\PowerMon\rtsscfg.json"))!;
        }
        catch
        {
            JsonRepair('r');
        }
    }

    private void JsonRepair(char file)
    {
        switch (file)
        {
            case 'n':
            {
                if (_notes != null) //перепроверка на соответствие, восстановление конфига.
                {
                    try
                    {
                        Directory.CreateDirectory(Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                        Directory.CreateDirectory(Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\",
                            "PowerMon"));
                        File.WriteAllText(
                            Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                            @"\SakuOverclock\PowerMon\powercfg.json", JsonConvert.SerializeObject(_notes));
                    }
                    catch
                    {
                        File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                                    @"\SakuOverclock\PowerMon\powercfg.json");
                        Directory.CreateDirectory(Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                        Directory.CreateDirectory(Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\",
                            "PowerMon"));
                        File.WriteAllText(
                            Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                            @"\SakuOverclock\PowerMon\powercfg.json", JsonConvert.SerializeObject(_notes));
                        App.GetService<IAppNotificationService>()
                            .Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                        App.MainWindow.Close();
                    }
                }
                else //Всё же если за 5 раз пересканирования файл пуст
                {
                    _notes = new Powercfg();
                    try
                    {
                        File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                                    @"\SakuOverclock\PowerMon\powercfg.json");
                        Directory.CreateDirectory(Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                        Directory.CreateDirectory(Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\",
                            "PowerMon"));
                        File.WriteAllText(
                            Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                            @"\SakuOverclock\PowerMon\powercfg.json", JsonConvert.SerializeObject(_notes));
                        App.MainWindow.Close();
                    }
                    catch
                    {
                        App.GetService<IAppNotificationService>()
                            .Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                        App.MainWindow.Close();
                    }
                }

                try
                {
                    for (var j = 0; j < 5; j++)
                    {
                        _notes = JsonConvert.DeserializeObject<Powercfg>(File.ReadAllText(
                            Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                            @"\SakuOverclock\PowerMon\powercfg.json"))!;
                        if (_notes != null)
                        {
                            break;
                        }
                    }
                }
                catch
                {
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(),
                        AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }

                break;
            }
            case 'r':
            {
                try
                {
                    Directory.CreateDirectory(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    Directory.CreateDirectory(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\",
                        "PowerMon"));
                    File.WriteAllText(
                        Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                        @"\SakuOverclock\PowerMon\rtsscfg.json", JsonConvert.SerializeObject(_rtssTable));
                }
                catch
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                                @"\SakuOverclock\PowerMon\rtsscfg.json");
                    Directory.CreateDirectory(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    Directory.CreateDirectory(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\",
                        "PowerMon"));
                    File.WriteAllText(
                        Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                        @"\SakuOverclock\PowerMon\rtsscfg.json", JsonConvert.SerializeObject(_rtssTable));
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(),
                        AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }

                try
                {
                    _rtssTable = JsonConvert.DeserializeObject<PowerMonRtss>(File.ReadAllText(
                        Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                        @"\SakuOverclock\PowerMon\rtsscfg.json"))!;
                }
                catch
                {
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(),
                        AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }

                break;
            }
        }
    }

    #endregion

    #region Event Handlers

    public void Dispose() => GC.SuppressFinalize(this);

    private void PowerWindow_Closed(object sender, WindowEventArgs args)
    {
        //CPU?.powerTable.Dispose();
        _ = Garbage.Garbage_Collect();
        _cpu = null;
        Dispose();
        _notes = null;
    }

    private void PowerWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        App.AppTitlebar = AppTitleBarText;
    }

    private readonly DispatcherTimer _powerCfgTimer = new();

    private void PowerCfgTimer_Tick(object? sender, object e)
    {
        if (_cpu?.RefreshPowerTable() == SMU.Status.OK)
        {
            RefreshData(_cpu.powerTable.Table);
        }
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _powerCfgTimer.Interval = new TimeSpan(0, 0, 0, 0, Convert.ToInt32(NumericUpDownInterval.Value));
        }
        catch
        {
            NumericUpDownInterval.Value = 500;
            _powerCfgTimer.Interval = new TimeSpan(0, 0, 0, 0, Convert.ToInt32(NumericUpDownInterval.Value));
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        _mode = _mode == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;
        IndexName.Text = IndexName.Text == "Index" ? "OSD" : "Index";
        OffsetName.Text = OffsetName.Text == "Offset" ? "Color #" : "Offset";
        NoteName.Text = NoteName.Text == "Quick Note" ? "OSD Name (value)" : "Quick Note";
        _noteColumnOverrider = _noteColumnOverrider == 3 ? 4 : 3;
        NoteName.Margin = _mode == Visibility.Visible ? new Thickness(10, 0, 0, 0) : new Thickness(100, 0, 0, 0);
        NotePos.Margin = new Thickness(12, 0, 0, 0);
        NotePos.Visibility = _mode == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    }

    #endregion

    #region PowerMon PowerTable voids

    private sealed partial class PowerMonitorItem : INotifyPropertyChanged
    {
        private Visibility _rtss = Visibility.Collapsed;
        private Visibility _normal = Visibility.Visible;
        private string? _value;
        private readonly string? _note;
        private string? _notevalue;
        private bool _osd;
        private int _row;
        private int _column;
        private Thickness _notecolumn = new(10, 0, 0, 0);

        public string? Index
        {
            get;
            set;
        }

        public string? Offset
        {
            get;
            set;
        }

        public string? Value
        {
            get => _value;
            set
            {
                if (_value == value)
                {
                    return;
                }

                _value = value;
                OnPropertyChanged(nameof(Value));
            }
        }

        public string? Note
        {
            get => _note;
            init
            {
                if (_note == value)
                {
                    return;
                }

                _note = value;
                OnPropertyChanged(nameof(Note));
            }
        }

        public bool Osd
        {
            get => _osd;
            set
            {
                if (_osd == value)
                {
                    return;
                }

                _osd = value;
                OnPropertyChanged(nameof(Osd));
            }
        }

        public int Row
        {
            get => _row;
            set
            {
                if (_row == value)
                {
                    return;
                }

                _row = value;
                OnPropertyChanged(nameof(Row));
            }
        }

        public int Column
        {
            get => _column;
            set
            {
                if (_column == value)
                {
                    return;
                }

                _column = value;
                OnPropertyChanged(nameof(Column));
            }
        }

        public Thickness NoteColumn
        {
            get => _notecolumn;
            set
            {
                if (_notecolumn == value)
                {
                    return;
                }

                _notecolumn = value;
                OnPropertyChanged(nameof(NoteColumn));
            }
        }

        public Visibility Rtss
        {
            get => _rtss;
            set
            {
                if (_rtss == value)
                {
                    return;
                }

                _rtss = value;
                OnPropertyChanged(nameof(Rtss));
            }
        }

        public Visibility Normal
        {
            get => _normal;
            set
            {
                if (_normal == value)
                {
                    return;
                }

                _normal = value;
                OnPropertyChanged(nameof(Normal));
            }
        }

        public string? NoteValue
        {
            get => _notevalue;
            set
            {
                if (_notevalue == value)
                {
                    return;
                }

                _notevalue = value;
                OnPropertyChanged(nameof(NoteValue));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    private async void FillInData(float[] table)
    {
        try
        {
            await Task.Run(() =>
            {
                NoteLoad();
                RtssLoad();
                _powerGridItems = [];
                for (var i = 0; i < table.Length; i++)
                {
                    if (_notes?._notelist.Count <= i)
                    {
                        _notes._notelist.Add(" ");
                    }

                    var subItem = new PowerMonitorItem
                    {
                        Rtss = _mode == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible,
                        Normal = _mode,
                        Index = $"{i:D4}",
                        Offset = $"0x{i * 4:X4}",
                        Value = $"{table[i]:F6}",
                        Note = _notes?._notelist[i]
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
                    _powerGridItems.Add(subItem);
                }
            });
            PowerGridView.ItemsSource = _powerGridItems;
            _powerCfgTimer.Start();
        }
        catch (Exception e)
        {
            // ReSharper disable once AsyncVoidMethod
            throw new Exception("Unable to fill in data to PowerMon PowerTable " + e); // TODO handle exception
        }
    }

    private void RefreshData(float[] table)
    {
        try
        {
            var index = 0;
            foreach (var item in _powerGridItems!)
            {
                const bool saveFlag = false; // Если есть изменения - сохранить
                item.Rtss = _mode == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                item.Normal = _mode;
                item.NoteColumn = _mode == Visibility.Visible
                    ? new Thickness(10, 0, 0, 0)
                    : new Thickness(100, 0, 0, 0);
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
                if (saveFlag && _rtssTable is { Elements: not null })
                {
                    _rtssTable.Elements[index].Offset = $"0x{index * 4:X4}";
                    RtssSave();
                }

                //Безопасная зона 
                item.Value = $"{table[index]:F6}"; // Обновление информации
                if (item.Note != _notes?._notelist[index])
                {
                    _notes!._notelist[index] = item.Note!;
                    NoteSave();
                }

                index++;
            }

            // Явное обновление GridView
            PowerGridView.ItemsSource = _powerGridItems;
        }
        catch
        {
            App.MainWindow.Close();
            Close();
        }
    }

    #endregion
}