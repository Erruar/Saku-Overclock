using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Collections.ObjectModel;

namespace Saku_Overclock.Styles;

public sealed partial class OptionsVisualizer : UserControl
{
    private int _visibleCount = 3;
    private readonly ObservableCollection<ObservableCollection<OptionsItem>> _pages = [];

    public ObservableCollection<OptionsItem> Items { get; set; } = [];
    public OptionsVisualizer()
    {
        InitializeComponent();
        Loaded += OptionsVisualizer_Loaded;
        SizeChanged += OptionsVisualizer_SizeChanged;
    }

    private void OptionsVisualizer_Loaded(object sender, RoutedEventArgs e)
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
        UpdateView();
    }

    private void OptionsVisualizer_SizeChanged(object sender, SizeChangedEventArgs e)
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

    private void UpdateView()
    {
        _pages.Clear();
        for (var i = 0; i < Items.Count; i += _visibleCount)
        {
            _pages.Add(new ObservableCollection<OptionsItem>(Items.Skip(i).Take(_visibleCount)));
        }

        FlipViewContainer.ItemsSource = _pages;
    } 
}
public class OptionsItem
{
    public string Value
    {
        get; set;
    } = string.Empty;
    public string Sign
    {
        get; set;
    } = string.Empty;
    public string Description
    {
        get; set;
    } = string.Empty;
    public Visibility Visibility
    {
        get; set;
    } = Visibility.Visible;
}
