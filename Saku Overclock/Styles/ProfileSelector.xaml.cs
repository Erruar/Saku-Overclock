using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace Saku_Overclock.Styles;

public sealed partial class ProfileSelector : UserControl
{
    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(nameof(SelectedItem), typeof(ProfileItem), typeof(ProfileSelector), new PropertyMetadata(null));

    public ProfileItem SelectedItem
    {
        get => (ProfileItem)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public event SelectionChangedEventHandler? SelectionChanged;

    private int _visibleCount = 3;
    private readonly ObservableCollection<ObservableCollection<ProfileItem>> _pages = [];

    public ObservableCollection<ProfileItem> Items { get; set; } = [];

    public ProfileSelector()
    {
        InitializeComponent();
        Loaded += ProfileSelector_Loaded;
        SizeChanged += ProfileSelector_SizeChanged;
    }

    private void ProfileSelector_Loaded(object sender, RoutedEventArgs e)
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

    private void ProfileSelector_SizeChanged(object sender, SizeChangedEventArgs e)
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
            _pages.Add(new ObservableCollection<ProfileItem>(Items.Skip(i).Take(_visibleCount)));
        }

        FlipViewContainer.ItemsSource = _pages;
    }

    private void ToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton toggleButton && toggleButton.DataContext is ProfileItem item)
        {
            if (toggleButton.IsChecked == false) { toggleButton.IsChecked = true; }
            foreach (var profile in Items)
            {
                profile.IsSelected = false;
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

public class ProfileItem
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