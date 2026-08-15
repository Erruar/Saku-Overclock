using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Garbage = Saku_Overclock.Helpers.Garbage;

namespace Saku_Overclock.Views.Window.PowerMon;

internal partial class PowerWindow : IDisposable
{
    private static readonly IPowerMonSettingsService SettingsService = App.GetService<IPowerMonSettingsService>();
    private static readonly IRawSharedMemoryReaderService RawSharedMemory = App.GetService<IRawSharedMemoryReaderService>();
    private ObservableCollection<PowerMonitorItem>? _powerGridItems;
    private bool _isInitialized;
    private float[]? _rawData;
    private int _currentPage;
    private const int PageSize = 50;
    private int _totalItems;
    private bool _isLoading;

    public PowerWindow()
    {
        InitializeComponent();
        InitializeWindowProperties();
        InitializeTimer();

        // Fast synchronous initialization
        _powerGridItems = [];
        PowerGridView.ItemsSource = _powerGridItems;

        // Loading first page
        _ = LoadInitialData();
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

    private async Task LoadInitialData()
    {
        try
        {
            // Fast notes loading
            SettingsService.LoadSettings();
            
            await RawSharedMemory.StartUpdate();

            _rawData = RawSharedMemory.GetRawData();
            if (_rawData == null) return;

            _totalItems = _rawData.Length;

            // Load first page
            LoadPage(0);

            _isInitialized = true;
            _powerCfgTimer.Start();

            // Update indicator
            UpdatePageInfo();
        }
        catch (Exception e)
        {
            await LogHelper.LogError("Unable to initialize PowerMon data: " + e.Message);
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
                // Determine if note was added
                while (SettingsService?.Notelist.Count <= i)
                {
                    SettingsService.Notelist.Add(" ");
                }

                var item = new PowerMonitorItem
                {
                    Index = $"{i:D4}",
                    Offset = $"0x{i * 4:X4}",
                    Value = $"{_rawData[i]:F6}",
                    Note = SettingsService?.Notelist[i] ?? " ",
                    RealIndex = i // Save real index
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
        _rawData = null;
        PowerGridView.ItemsSource = null;
        RawSharedMemory.StopUpdate();
        
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
            if (e.IsIntermediate) // Check for scroll to be ended
            {
                return;
            }

            var scrollViewer = sender as ScrollViewer;
            var totalPages = (_totalItems + PageSize - 1) / PageSize;

            // If user scrolled to the end and next page is available
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
                    page = Math.Max(1, Math.Min(page, totalPages)) - 1; // Convert to correct index

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
                    // Move to new page start
                    scrollViewer.ChangeView(null, 1, null);
                }
            }
            // If user scrolled to the top and previous page is available
            else if (scrollViewer.VerticalOffset <= 0 &&
                     _currentPage > 0 &&
                     totalPages - 1 !=
                     _currentPage) // Convert to correct index if page is not last
            {
                LoadPage(_currentPage - 1);
                UpdatePageInfo();
                // Move to previous page end
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
            page = Math.Max(1, Math.Min(page, totalPages)) - 1; // Convert to correct index

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
        // ReSharper disable once ReplaceWithFieldKeyword
        private string? _value;
        
        // ReSharper disable once ReplaceWithFieldKeyword
        private string? _note;

        public string? Index
        {
            // ReSharper disable once UnusedAutoPropertyAccessor.Local
            get;
            set;
        }

        public string? Offset
        {
            // ReSharper disable once UnusedAutoPropertyAccessor.Local
            get;
            set;
        }

        public int RealIndex
        {
            get;
            init;
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

        var newData = RawSharedMemory.GetRawData();
        if (newData == null)
        {
            return;
        }

        _rawData = newData;

        // Update only visible elements
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

                // Save notes if changed
                if (item.Note != SettingsService.Notelist[realIndex]
                    && realIndex < SettingsService.Notelist.Count)
                {
                    SettingsService.Notelist[realIndex] = item.Note ?? " ";
                    _ = Task.Run(SettingsService.SaveSettings);
                }
            }
        }
    }

    #endregion
}