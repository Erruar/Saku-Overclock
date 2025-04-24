using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Saku_Overclock.Styles;
public sealed partial class BatteryControl : UserControl
{
    private string? _value;
    public string? Value 
    {
        get => _value;
        set => SetBattery(value);
    }
    private void SetBattery(string? value)
    {
        value ??= "100";
        _value = value;
        BatteryText.Text = value.Contains('%') ? value : value + "%";
        if (value == "N/A" || value == "NA") { value = "100"; BatteryText.Text = "N/A"; }
        var dimSize = BatteryPercentGrid.ActualWidth * Convert.ToInt32(value?.Replace("%","")) / 100;
        if (dimSize != 0)
        {
            BatteryPercentBorder.Width = dimSize;
        }
        else
        {
            BatteryPercentBorder.Width = BatteryPercentGrid.ActualWidth;
        }
    }
    public BatteryControl()
    {
        InitializeComponent();
        Loaded += BatteryControl_Loaded;
        SizeChanged += BatteryControl_SizeChanged;
    }

    private void BatteryControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        SetBattery(Value);
    }
    private void BatteryControl_Loaded(object sender, RoutedEventArgs e)
    {
        SetBattery(Value); 
    }
}
