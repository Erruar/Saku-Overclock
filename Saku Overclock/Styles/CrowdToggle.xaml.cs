using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using static Saku_Overclock.Styles.BandCrowdToggle;

namespace Saku_Overclock.Styles;

public partial class CrowdToggle : UserControl
{
    private bool _isLoaded;

    public event Action<BandCrowdStates>? OnClick;

    // Текст главного выбора
    public static readonly DependencyProperty PrimaryTextProperty =
        DependencyProperty.Register(
            nameof(PrimaryText), 
            typeof(string), 
            typeof(CrowdToggle), 
            new PropertyMetadata(string.Empty));
    public string PrimaryText { get => (string)GetValue(PrimaryTextProperty); set => SetValue(PrimaryTextProperty, value); }
    
    public static readonly DependencyProperty IsManualProperty =
        DependencyProperty.Register(
            nameof(IsManual), 
            typeof(Visibility), 
            typeof(CrowdToggle), 
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
            typeof(CrowdToggle),
            new PropertyMetadata(BandCrowdStates.Off, OnStateDependencyChanged));

    public BandCrowdStates State
    {
        get => (BandCrowdStates)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public CrowdToggle()
    {
        InitializeComponent();
        Loaded += (_, _) => 
        {
            _isLoaded = true;
            UpdateVisualStates(State);
        };
    }

    private void Crowd_OnClick(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) { return; }

        var clickedState = (sender as ToggleButton)?.Name switch
        {
            "CrowdManual" => BandCrowdStates.Manual,
            _ => BandCrowdStates.Off
        };

        State = clickedState;

        OnClick?.Invoke(clickedState);
    }

    public void SelectOnly(BandCrowdStates mode)
    {
        State = mode;
    }

    private static void OnStateDependencyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CrowdToggle control)
        {
            var newState = (BandCrowdStates)e.NewValue;

            control.IsManual = newState == BandCrowdStates.Manual ? Visibility.Visible : Visibility.Collapsed;

            control.UpdateVisualStates(newState);
        }
    }

    private void UpdateVisualStates(BandCrowdStates mode)
    {
        if (!_isLoaded) { return; }

        if (CrowdDisabled != null) CrowdDisabled.IsChecked = mode == BandCrowdStates.Off;
        if (CrowdManual != null) CrowdManual.IsChecked = mode == BandCrowdStates.Manual;
    }
}