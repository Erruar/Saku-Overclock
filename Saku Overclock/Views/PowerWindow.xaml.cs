using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.SMUEngine;

namespace Saku_Overclock.Views;

internal partial class PowerWindow : IDisposable
{
    private int _noteColumnOverrider = 3;
    private Powercfg? _notes;
    private ObservableCollection<PowerMonitorItem>? _powerGridItems;
    private readonly IDataProvider? _dataProvider = App.GetService<IDataProvider>();
    private bool _isInitialized = false;

    public PowerWindow()
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

        FillInData(_dataProvider.GetPowerTable()!);

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
        var filePath = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\PowerMon\powercfg.json";
        if (File.Exists(filePath))
        {
            try
            {
                _notes = JsonConvert.DeserializeObject<Powercfg>(File.ReadAllText(filePath))!;
            }
            catch
            {
                JsonRepair();
            }
        }
        else
        {
            JsonRepair();
        }
    }

    private void JsonRepair()
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
            }
            catch
            {
                App.GetService<IAppNotificationService>()
                    .Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
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
        }

    }

    #endregion

    #region Event Handlers
    public void Dispose() => GC.SuppressFinalize(this);

    private void PowerWindow_Closed(object sender, WindowEventArgs args)
    {
        //CPU?.powerTable.Dispose();
        _powerGridItems = null;
        _notes = null;
        PowerGridView.ItemsSource = null;
        PowerGridView.Items.Clear();
        _ = Garbage.Garbage_Collect();
        Dispose();
    }

    private void PowerWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        App.AppTitlebar = AppTitleBarText;
    }

    private readonly DispatcherTimer _powerCfgTimer = new();

    private void PowerCfgTimer_Tick(object? sender, object e)
    {
        RefreshData(_dataProvider!.GetPowerTable()!);
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
        IndexName.Text = IndexName.Text == "Index" ? "OSD" : "Index";
        OffsetName.Text = OffsetName.Text == "Offset" ? "Color #" : "Offset";
        NoteName.Text = NoteName.Text == "Quick Note" ? "OSD Name (value)" : "Quick Note";
        _noteColumnOverrider = _noteColumnOverrider == 3 ? 4 : 3;
    }

    #endregion

    #region PowerMon PowerTable voids

    private sealed partial class PowerMonitorItem : INotifyPropertyChanged
    {
        private string? _value;
        private readonly string? _note;

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
            PowerGridView.ItemsSource = _powerGridItems;
            await Task.Run(() =>
            {
                NoteLoad();
                _powerGridItems = [];
                for (var i = 0; i < table.Length; i++)
                {
                    if (_notes?._notelist.Count <= i)
                    {
                        _notes._notelist.Add(" ");
                    }

                    var subItem = new PowerMonitorItem
                    {
                        Index = $"{i:D4}",
                        Offset = $"0x{i * 4:X4}",
                        Value = $"{table[i]:F6}",
                        Note = _notes?._notelist[i]
                    };
                    _powerGridItems.Add(subItem);
                }
            }); 
            _powerCfgTimer.Start();
        }
        catch (Exception e)
        {
            // ReSharper disable once AsyncVoidMethod
            throw new Exception("Unable to fill in data to PowerMon PowerTable " + e); // TODO handle exception
        }
        _isInitialized = true;
    }

    private void RefreshData(float[] table)
    {
        if (!_isInitialized) { return; }
        try
        {
            var index = 0;
            foreach (var item in _powerGridItems!)
            {
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
            // Ignored
        }
    }

    #endregion
}