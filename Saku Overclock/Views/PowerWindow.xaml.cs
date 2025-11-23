using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.JsonContainers;
using Saku_Overclock.SMUEngine;

namespace Saku_Overclock.Views;

internal partial class PowerWindow : IDisposable
{
    private Powercfg? _notes;
    private ObservableCollection<PowerMonitorItem>? _powerGridItems;
    private readonly IDataProvider? _dataProvider = App.GetService<IDataProvider>();
    private bool _isInitialized;
    private float[]? _rawData; // Сырые данные
    private int _currentPage;
    private const int PageSize = 50; // Показываем только 50 элементов за раз
    private int _totalItems;
    private bool _isLoading;

    public PowerWindow()
    {
        InitializeComponent();
        InitializeWindowProperties();
        InitializeTimer();

        // Быстрая синхронная инициализация
        _powerGridItems = [];
        PowerGridView.ItemsSource = _powerGridItems;

        // Загружаем первую страницу
        LoadInitialData();
    }

    private void InitializeWindowProperties()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppTitleBarText.Text = "Saku PowerMon";
        this.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets/powermon.ico"));
        this.SetWindowSize(342, 579);

        Activated += PowerWindow_Activated;
        Closed += PowerWindow_Closed;
    }

    private void InitializeTimer()
    {
        _powerCfgTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
        _powerCfgTimer.Tick += PowerCfgTimer_Tick;
    }

    private void LoadInitialData()
    {
        try
        {
            // Быстрая загрузка заметок
            NoteLoad();

            // Получаем данные
            _rawData = _dataProvider?.GetPowerTable();
            if (_rawData == null)
            {
                return;
            }

            _totalItems = _rawData.Length;

            // Загружаем первую страницу
            LoadPage(0);

            _isInitialized = true;
            _powerCfgTimer.Start();

            // Обновляем индикатор
            UpdatePageInfo();
        }
        catch (Exception e)
        {
            throw new Exception("Unable to initialize PowerMon data: " + e.Message);
        }
    }

    private void LoadPage(int page)
    {
        if (_isLoading || _rawData == null)
        {
            return;
        }

        _isLoading = true;
        _currentPage = page;

        try
        {
            _powerGridItems?.Clear();

            var startIndex = page * PageSize;
            var endIndex = Math.Min(startIndex + PageSize, _totalItems);

            for (var i = startIndex; i < endIndex; i++)
            {
                // Убеждаемся что у нас есть заметка
                while (_notes?.Notelist.Count <= i)
                {
                    _notes?.Notelist.Add(" ");
                }

                var item = new PowerMonitorItem
                {
                    Index = $"{i:D4}",
                    Offset = $"0x{i * 4:X4}",
                    Value = $"{_rawData[i]:F6}",
                    Note = _notes?.Notelist[i] ?? " ",
                    RealIndex = i // Сохраняем реальный индекс
                };

                _powerGridItems?.Add(item);
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void UpdatePageInfo()
    {
        var totalPages = (_totalItems + PageSize - 1) / PageSize;
        PageInfo.Text = "PowerMon_Page".GetLocalized() + $"{_currentPage + 1}/{totalPages}";

        PrevPageButton.IsEnabled = _currentPage > 0;
        NextPageButton.IsEnabled = _currentPage < totalPages - 1;
    }

    #region JSON containers

    private void NoteSave()
    {
        if (_notes == null)
        {
            return;
        }

        try
        {
            var basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock",
                "PowerMon");
            Directory.CreateDirectory(basePath);

            var filePath = Path.Combine(basePath, "powercfg.json");
            File.WriteAllText(filePath, JsonConvert.SerializeObject(_notes, Formatting.Indented));
        }
        catch
        {
            // ignored
        }
    }

    private void NoteLoad()
    {
        var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock",
            "PowerMon", "powercfg.json");

        if (File.Exists(filePath))
        {
            try
            {
                _notes = JsonConvert.DeserializeObject<Powercfg>(File.ReadAllText(filePath));
            }
            catch
            {
                _notes = new Powercfg();
            }
        }
        else
        {
            _notes = new Powercfg();
        }

        _notes ??= new Powercfg();
    }

    #endregion

    #region Event Handlers

    public void Dispose()
    {
        _powerCfgTimer.Stop();
        GC.SuppressFinalize(this);
    }

    private void PowerWindow_Closed(object sender, WindowEventArgs args)
    {
        _powerCfgTimer.Stop();
        _powerGridItems?.Clear();
        _powerGridItems = null;
        _notes = null;
        _rawData = null;
        PowerGridView.ItemsSource = null;

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
        if (!_isInitialized || _isLoading)
        {
            return;
        }

        RefreshCurrentPage();
    }

    private void NumericUpDownInterval_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_isInitialized || _isLoading)
        {
            return;
        }

        try
        {
            var interval = Convert.ToInt32(NumericUpDownInterval.Value);
            _powerCfgTimer.Interval = new TimeSpan(0, 0, 0, 0, interval);
        }
        catch
        {
            NumericUpDownInterval.Value = 500;
            _powerCfgTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
        }
    }

    private void PrevPage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage > 0)
        {
            LoadPage(_currentPage - 1);
            UpdatePageInfo();
            MainScroll.ChangeView(
                horizontalOffset: null,
                verticalOffset: MainScroll.ScrollableHeight - 1,
                zoomFactor: null);
        }
    }

    private void NextPage_Click(object sender, RoutedEventArgs e)
    {
        var totalPages = (_totalItems + PageSize - 1) / PageSize;
        if (_currentPage < totalPages - 1)
        {
            LoadPage(_currentPage + 1);
            UpdatePageInfo();
            MainScroll.ChangeView(null, 1, null);
        }
    }

    private bool _maxedOut;

    private async void MainScroll_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        try
        {
            if (e.IsIntermediate) // Проверяем, что прокрутка завершена
            {
                return;
            }

            var scrollViewer = sender as ScrollViewer;
            var totalPages = (_totalItems + PageSize - 1) / PageSize;

            // Если долистали до самого низа и есть следующая страница
            if (scrollViewer == null)
            {
                return;
            }

            if (scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight &&
                _currentPage < totalPages - 1)
            {
                if (!_maxedOut && (int)(scrollViewer.VerticalOffset * 100) ==
                    (int)(scrollViewer.ScrollableHeight * 100))
                {
                    _maxedOut = true;
                    return;
                }

                _maxedOut = false;
                if (totalPages - 1 == _currentPage + 1)
                {
                    scrollViewer.ChangeView(null, 1, null);
                    await Task.Delay(190);
                    var page = totalPages;
                    page = Math.Max(1, Math.Min(page, totalPages)) - 1; // Конвертировать в правильный индекс

                    if (page != _currentPage)
                    {
                        LoadPage(page);
                        UpdatePageInfo();
                    }
                }
                else
                {
                    LoadPage(_currentPage + 1);
                    UpdatePageInfo();
                    // Перемещаем к началу новой страницы
                    scrollViewer.ChangeView(null, 1, null);
                }
            }
            // Если долистали до самого верха и есть предыдущая страница
            else if (scrollViewer.VerticalOffset <= 0 &&
                     _currentPage > 0 &&
                     totalPages - 1 !=
                     _currentPage) // Конвертировать в правильный индекс, если страница не последняя
            {
                LoadPage(_currentPage - 1);
                UpdatePageInfo();
                // Перемещаем к концу предыдущей страницы
                scrollViewer.ChangeView(null, scrollViewer.ScrollableHeight - 1, null);
            }
        }
        catch (Exception ex)
        {
            await LogHelper.LogWarn(ex);
        }
    }

    private void GoToPage_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(PageInput.Text, out var page))
        {
            var totalPages = (_totalItems + PageSize - 1) / PageSize;
            page = Math.Max(1, Math.Min(page, totalPages)) - 1; // Конвертировать в правильный индекс

            if (page != _currentPage)
            {
                LoadPage(page);
                UpdatePageInfo();
            }
        }
    }

    #endregion

    #region PowerMon PowerTable

    private sealed partial class PowerMonitorItem : INotifyPropertyChanged
    {
        private string? _value;
        private string? _note;

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

        public int RealIndex
        {
            get;
            set;
        } // Реальный индекс в массиве

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

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    private void RefreshCurrentPage()
    {
        if (_rawData == null || _isLoading)
        {
            return;
        }

        // Получаем новые данные
        var newData = _dataProvider?.GetPowerTable();
        if (newData == null)
        {
            return;
        }

        _rawData = newData;

        // Обновляем только видимые элементы
        for (var i = 0; i < _powerGridItems!.Count; i++)
        {
            var item = _powerGridItems[i];
            var realIndex = item.RealIndex;

            if (realIndex < _rawData.Length)
            {
                var newValue = $"{_rawData[realIndex]:F6}";
                if (item.Value != newValue)
                {
                    item.Value = newValue;
                }

                // Сохраняем заметки если изменились
                if (item.Note != _notes?.Notelist[realIndex]
                    && _notes != null && realIndex < _notes.Notelist.Count)
                {
                    _notes.Notelist[realIndex] = item.Note ?? " ";
                    // Отложенное сохранение
                    Task.Run(NoteSave);
                }
            }
        }
    }

    #endregion
}