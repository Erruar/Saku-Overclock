using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Saku_Overclock.Styles;

public partial class BandCrowdToggle : UserControl
{
    private bool _isLoaded;

    public event Action<BandCrowdStates>? OnClick;

    public static readonly DependencyProperty IsManualProperty =
        DependencyProperty.Register(
            nameof(IsManual), 
            typeof(Visibility), 
            typeof(BandCrowdToggle), 
            new PropertyMetadata(Visibility.Collapsed));

    public Visibility IsManual
    {
        get => (Visibility)GetValue(IsManualProperty);
        set => SetValue(IsManualProperty, value);
    }

    public static readonly DependencyProperty StateProperty =
        DependencyProperty.Register(
            nameof(State),
            typeof(BandCrowdStates),
            typeof(BandCrowdToggle),
            new PropertyMetadata(BandCrowdStates.Off, OnStateDependencyChanged));

    public BandCrowdStates State
    {
        get => (BandCrowdStates)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public BandCrowdToggle()
    {
        InitializeComponent();
        Loaded += (_, _) => 
        {
            _isLoaded = true;
            UpdateVisualStates(State);
        };
    }

    private void BandCrowd_OnClick(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) { return; }

        var clickedState = (sender as ToggleButton)?.Name switch
        {
            "BandCrowdEnabled" => BandCrowdStates.Auto,
            "BandCrowdManual" => BandCrowdStates.Manual,
            _ => BandCrowdStates.Off
        };

        State = clickedState;

        OnClick?.Invoke(clickedState);
    }

    public enum BandCrowdStates
    {
        Off,
        Auto,
        Manual
    }

    public void SelectOnly(BandCrowdStates mode)
    {
        State = mode;
    }

    private static void OnStateDependencyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BandCrowdToggle control)
        {
            var newState = (BandCrowdStates)e.NewValue;

            control.IsManual = newState == BandCrowdStates.Manual ? Visibility.Visible : Visibility.Collapsed;

            control.UpdateVisualStates(newState);
        }
    }

    private void UpdateVisualStates(BandCrowdStates mode)
    {
        if (!_isLoaded) { return; }

        if (BandCrowdDisabled != null) BandCrowdDisabled.IsChecked = mode == BandCrowdStates.Off;
        if (BandCrowdEnabled != null) BandCrowdEnabled.IsChecked = mode == BandCrowdStates.Auto;
        if (BandCrowdManual != null) BandCrowdManual.IsChecked = mode == BandCrowdStates.Manual;
    }
}