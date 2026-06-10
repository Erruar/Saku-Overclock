using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Saku_Overclock.Models;

namespace Saku_Overclock.Styles;

public partial class PresetSetting : UserControl
{
    private bool _isLoaded;
    private bool _isUpdatingUi;
    private readonly DispatcherTimer _debounceTimer;

    public event Action<PresetOption<double>>? ValueChanged;

    #region Dependency Properties

    // Текст заголовка
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(PresetSetting), new PropertyMetadata(string.Empty));
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    // Минумум
    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(PresetSetting), new PropertyMetadata(0.0));
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    // Жесткий максимум (по умолчанию не ограничен)
    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(PresetSetting), new PropertyMetadata(double.MaxValue, OnMaximumChanged));
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    // Текущий максимум слайдера (может расширяться динамически)
    public static readonly DependencyProperty SliderMaximumProperty =
        DependencyProperty.Register(nameof(SliderMaximum), typeof(double), typeof(PresetSetting), new PropertyMetadata(100.0));
    public double SliderMaximum { get => (double)GetValue(SliderMaximumProperty); set => SetValue(SliderMaximumProperty, value); }

    // Основное значение-объект
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
        // Инициализируем таймер задержки
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
        // Если данные пришли до загрузки UI элементов — игнорируем, Loaded сам всё вызовет
        if (!_isLoaded || SettingCheck == null || value == null) return;

        _isUpdatingUi = true;
        
        // Расширяем слайдер под пришедшее значение, если оно выходит за дефолтные рамки
        if (value.Value > SliderMaximum && value.Value <= Maximum)
        {
            SliderMaximum = FromValueToUpperFive(value.Value);
        }

        SettingCheck.IsChecked = value.IsEnabled;
        SettingSlider.Value = value.Value;
        
        _isUpdatingUi = false;
    }

    // Чекбокс должен срабатывать мгновенно (immediate: true)
    private void SettingComponent_Changed(object sender, RoutedEventArgs e) => ChangeSetting(immediate: true);
    
    private void SettingSlider_OnValueChanged(object sender, RangeBaseValueChangedEventArgs e) => ChangeSetting(immediate: false);

    private void ChangeSetting(bool immediate)
    {
        if (!_isLoaded || _isUpdatingUi) return;

        bool isEnabled = SettingCheck.IsChecked == true;
        double val = SettingSlider.Value;

        // Проверяем, изменилось ли что-то на самом деле
        if (Value?.IsEnabled != isEnabled || (int)Value.Value != (int)val)
        {
            Value?.IsEnabled = isEnabled;
            Value?.Value = val;

            _debounceTimer.Stop();
            if (immediate)
            {
                // Если кликнули чекбокс — гасим таймер слайдера (если он тикал) и сохраняем прямо сейчас
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