using System.Collections.ObjectModel;
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

    public event SelectionChangedEventHandler? SelectionChanged;

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
            if (element != null)
            {
                element.Shadow = new ThemeShadow();
                element.Translation = new System.Numerics.Vector3(0, 0, 20);
                element.Margin = new Thickness(0, -5, 0, 0);
            }
        }
        foreach (var item in Items)
        {
            if (item.Description == string.Empty)
            {
                item.Description = item.Text;
            }
            else 
            {
                item.Description = item.Text + "\n" + item.Description;
            }
        }
        UpdateView();
    }

    private void PresetSelector_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        CalculateVisibleItems();
        UpdateView();
    }

    private void CalculateVisibleItems()
    {
        var containerWidth = ActualWidth;
        var itemWidth = ActualHeight + 5;

        _visibleCount = (int)(containerWidth / itemWidth);
        if (_visibleCount < 1)
        {
            _visibleCount = 1;
        }
    }

    public void UpdateView()
    {
        _pages.Clear();
        for (var i = 0; i < Items.Count; i += _visibleCount)
        {
            _pages.Add(new ObservableCollection<PresetItem>(Items.Skip(i).Take(_visibleCount)));
        }

        FlipViewContainer.ItemsSource = _pages;
    }

    private void ToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton toggleButton && toggleButton.DataContext is PresetItem item)
        {
            if (toggleButton.IsChecked == false) { toggleButton.IsChecked = true; }
            foreach (var preset in Items)
            {
                preset.IsSelected = false;
            }

            item.IsSelected = true;
            SelectedItem = item;

            SelectionChanged?.Invoke(this, new SelectionChangedEventArgs([], [item]));
            foreach (var element in Helpers.VisualTreeHelper.FindVisualChildren<ToggleButton>(FlipViewContainer))
            {
                if (element != toggleButton)
                {
                    if (element != null)
                    {
                        element.IsChecked = false;
                    }
                }
            }
        }
    }
}

public class PresetItem
{
    public string Text
    {
        get; set;
    } = string.Empty;
    public string Description
    {
        get; set;
    } = string.Empty;
    public string IconGlyph
    {
        get; set;
    } = "\uE783";
    public bool IsSelected
    {
        get; set;
    }
}