using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Saku_Overclock.Shared.Models.PresetSettings;

namespace Saku_Overclock.Styles;

public partial class PresetSetting : UserControl
{
    private bool _isLoaded;
    private bool _isUpdatingUi;
    private readonly DispatcherTimer _debounceTimer;

    public event Action<PresetOption<double>>? ValueChanged;

    #region Dependency Properties

    // Header text
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(PresetSetting), new PropertyMetadata(string.Empty));
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    
    // Checkbox visibility
    public static readonly DependencyProperty CheckBoxVisibilityProperty =
        DependencyProperty.Register(nameof(CheckBoxVisibility), typeof(Visibility), typeof(PresetSetting), new PropertyMetadata(Visibility.Visible));
    public Visibility CheckBoxVisibility { get => (Visibility)GetValue(CheckBoxVisibilityProperty); set => SetValue(CheckBoxVisibilityProperty, value); }

    // Minimum limit
    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(PresetSetting), new PropertyMetadata(0.0));
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    // Maximum limit
    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(PresetSetting), new PropertyMetadata(double.MaxValue, OnMaximumChanged));
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    // Current slider maximum (not limited)
    public static readonly DependencyProperty SliderMaximumProperty =
        DependencyProperty.Register(nameof(SliderMaximum), typeof(double), typeof(PresetSetting), new PropertyMetadata(100.0));
    public double SliderMaximum { get => (double)GetValue(SliderMaximumProperty); set => SetValue(SliderMaximumProperty, value); }

    // Preset Option to display
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(PresetOption<double>), typeof(PresetSetting), new PropertyMetadata(null, OnValueChanged));
    
    public PresetOption<double>? Value { get => (PresetOption<double>)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    #region Изменения DP

    private static void OnMaximumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PresetSetting control && e.NewValue is double max)
        {
            // Если задан жесткий максимум, слайдер изначально не должен его превышать
            if (control.SliderMaximum > max) control.SliderMaximum = max;
        }
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PresetSetting control && e.NewValue is PresetOption<double> newValue)
        {
            control.UpdateUi(newValue);
        }
    }
    #endregion

    #endregion

    public PresetSetting()
    {
        InitializeComponent();
        // Initialize delay timer
        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(350) 
        };
        _debounceTimer.Tick += DebounceTimer_Tick;

        Loaded += (_, _) =>
        {
            _isLoaded = true;
            UpdateUi(Value);
        };

        Unloaded += (_, _) => 
        {
            _debounceTimer.Stop();
            ValueChanged = null; 
        };
    }

    
    private void DebounceTimer_Tick(object? sender, object e)
    {
        _debounceTimer.Stop();
        if (Value !=  null) ValueChanged?.Invoke(Value);
    }
    
    private void UpdateUi(PresetOption<double>? value)
    {
        // If data arrives before the UI elements have loaded, ignore it
        // `Loaded` event will handle it
        if (!_isLoaded || SettingCheck == null || value == null) return;

        _isUpdatingUi = true;
        
        // Expand slider to accommodate the received value if it falls outside the default limits
        if (value.Value > SliderMaximum && value.Value <= Maximum)
        {
            SliderMaximum = FromValueToUpperFive(value.Value);
        }

        SettingCheck.IsChecked = value.IsEnabled;
        SettingSlider.Value = value.Value;
        
        _isUpdatingUi = false;
    }

    // Checkbox should react immediately (immediate: true)
    private void SettingComponent_Changed(object sender, RoutedEventArgs e) => ChangeSetting(immediate: true);
    
    private void SettingSlider_OnValueChanged(object sender, RangeBaseValueChangedEventArgs e) => ChangeSetting(immediate: false);

    private void ChangeSetting(bool immediate)
    {
        if (!_isLoaded || _isUpdatingUi) return;

        bool isEnabled = SettingCheck.IsChecked == true;
        double val = SettingSlider.Value;

        // Check if value was changed
        if (Value?.IsEnabled != isEnabled || (int)Value.Value != (int)val)
        {
            Value?.IsEnabled = isEnabled;
            Value?.Value = val;

            _debounceTimer.Stop();
            if (immediate)
            {
                // If checkbox is clicked, stop the slider timer (if it was running) and save immediately
                if (Value != null) ValueChanged?.Invoke(Value);
            }
            else
            {
                _debounceTimer.Start();
            }
        }
    }

    private void TargetNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_isLoaded || _isUpdatingUi) return;

        if (sender.Value > SliderMaximum && sender.Value <= Maximum)
        {
            SliderMaximum = FromValueToUpperFive(sender.Value);
        }
    }

    private static int FromValueToUpperFive(double value) => (int)Math.Ceiling(value / 5) * 5;
}