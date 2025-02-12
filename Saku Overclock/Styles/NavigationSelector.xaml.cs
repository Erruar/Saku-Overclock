using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Saku_Overclock.Styles;
public sealed partial class NavigationSelector : UserControl
{
    private int _visibleCount = 3;
    private readonly ObservableCollection<ObservableCollection<NavigationSelectorItem>> _pages = []; 
    public ObservableCollection<NavigationSelectorItem> Items { get; set; } = [];

    public event RoutedEventHandler? ItemClick;
    public string? SelectedString;

    public NavigationSelector()
    {
        InitializeComponent();
        Loaded += NavigationSelector_Loaded;
        SizeChanged += NavigationSelector_SizeChanged;
    }
    private void NavigationSelector_Loaded(object sender, RoutedEventArgs e)
    {
        foreach (var element in Helpers.VisualTreeHelper.FindVisualChildren<Button>(FlipViewContainer))
        {
            if (element != null)
            {
                element.Shadow = new ThemeShadow();
                element.Translation = new System.Numerics.Vector3(0, 0, 20);
            }
        }
        UpdateView();
    }

    private void NavigationSelector_SizeChanged(object sender, SizeChangedEventArgs e)
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
            _pages.Add(new ObservableCollection<NavigationSelectorItem>(Items.Skip(i).Take(_visibleCount)));
        }

        FlipViewContainer.ItemsSource = _pages;
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is NavigationSelectorItem item)
        {
            SelectedString = item.Naming;
            ItemClick?.Invoke(this, e); 
        }
    }
} 
public class NavigationSelectorItem
{
    public string Text
    {
        get; set;
    } = string.Empty;
    public string Naming
    {
        get; set; 
    } = string.Empty;
    public string IconSource
    {
        get; set;
    } = "/Assets/info.png";
}
