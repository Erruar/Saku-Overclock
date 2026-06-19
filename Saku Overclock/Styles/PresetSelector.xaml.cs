using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace Saku_Overclock.Styles;

public sealed partial class PresetSelector : UserControl
{
    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(nameof(SelectedItem), typeof(PresetItem), typeof(PresetSelector), new PropertyMetadata(null));

    public PresetItem SelectedItem
    {
        get => (PresetItem)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public int SelectedIndex => GetSelectedIndex();

    private int GetSelectedIndex()
    {
        for (var index = 0; index < Items.Count; index++)
        {
            var item = Items[index];
            if (item.IsSelected)
            {
                return index;
            }
        }

        return -1;
    }

    public event EventHandler<PresetSelectorChangedEventArgs>? SelectionChanged;

    private int _visibleCount = 3;
    private readonly ObservableCollection<ObservableCollection<PresetItem>> _pages = [];

    public ObservableCollection<PresetItem> Items { get; set; } = [];

    public PresetSelector()
    {
        InitializeComponent();
        Loaded += PresetSelector_Loaded;
        SizeChanged += PresetSelector_SizeChanged;
    }

    private void PresetSelector_Loaded(object sender, RoutedEventArgs e)
    {
        foreach (var element in Helpers.VisualTreeHelper.FindVisualChildren<Button>(FlipViewContainer))
        {
            element.Shadow = new ThemeShadow();
            element.Translation = new System.Numerics.Vector3(0, 0, 20);
            element.Margin = new Thickness(0, -5, 0, 0);
        }

        foreach (var item in Items)
        {
            if (item.IsSelected) SelectedItem = item;
            
            if (!item.Description.StartsWith(item.Text + "\n"))
            {
                if (item.Description == string.Empty || item.Description == item.Text)
                {
                    item.Description = item.Text;
                }
                else 
                {
                    item.Description = item.Text + "\n" + item.Description;
                }
            }
        }
    }

    private void PresetSelector_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        CalculateVisibleItems();
        UpdateView();
    }

    private void CalculateVisibleItems()
    {
        var containerWidth = ActualWidth + 2;
        var itemWidth = 127;

        var newCount = (int)(containerWidth / itemWidth);
        if (newCount < 1) newCount = 1;

        // Вызываем тяжелый UpdateView ТОЛЬКО если реально изменилось количество колонок на странице
        if (_visibleCount != newCount)
        {
            _visibleCount = newCount;
            UpdateView();
        }
    }

    public void UpdateView()
    {
        _pages.Clear();
        for (var i = 0; i < Items.Count; i += _visibleCount)
        {
            if (Items[i].IsSelected && SelectedItem != Items[i]) SelectedItem = Items[i];
            _pages.Add(new ObservableCollection<PresetItem>(Items.Skip(i).Take(_visibleCount)));
        }

        FlipViewContainer.ItemsSource = _pages;
    }

    private void ToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { DataContext: PresetItem item } toggleButton)
        {
            if (item.IsSelected) 
            {
                toggleButton.IsChecked = true; 
            }
            else
            {
                foreach (var preset in Items)
                {
                    preset.IsSelected = false;
                }

                item.IsSelected = true;
                SelectedItem = item;
            }

            SelectionChanged?.Invoke(this, new PresetSelectorChangedEventArgs(item));
        }
    }
}

public class PresetSelectorChangedEventArgs(PresetItem addedItem) : EventArgs
{
    public PresetItem AddedItem { get; } = addedItem;
}

public class PresetItem : INotifyPropertyChanged
{
    private string _text = string.Empty;
    private string _description = string.Empty;
    private string _iconGlyph = "\uE783";
    private bool _isSelected;

    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string IconGlyph
    {
        get => _iconGlyph;
        set => SetProperty(ref _iconGlyph, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(storage, value)) return;
        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}