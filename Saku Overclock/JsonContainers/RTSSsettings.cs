using Saku_Overclock.SMUEngine;

namespace Saku_Overclock.JsonContainers;
internal class RTSSsettings
{
    public List<RTSSElementsClass> RTSS_Elements =
    [
        new()
        {
            Enabled = true,
            Name = "Main Color",
            Color = "#44FFAC",
            UseCompact = false
        },
        new()
        {
            Enabled = true,
            Name = "Second Color",
            Color = "#44AFAC",
            UseCompact = true
        },
        new()
        {
            Enabled = true,
            Name = "Saku Overclock " + ViewModels.ГлавнаяViewModel.GetVersion() + ": ",
            Color = "#44AFAC",
            UseCompact = true
        },
        new()
        {
            Enabled = true,
            Name = "STAPM, Fast, Slow: (W), (W), (W)",
            Color = "#44AFAC",
            UseCompact = true
        },
        new()
        {
            Enabled = true,
            Name = "EDC, Therm, CPU Usage: (A), (C), (%)",
            Color = "#44AFAC",
            UseCompact = true
        },
        new()
        {
            Enabled = true,
            Name = "Clocks: (GHz), (V)",
            Color = "#44AFAC",
            UseCompact = true
        },
        new()
        {
            Enabled = true,
            Name = "AVG Clock, Volt: (GHz), (V)",
            Color = "#44AFAC",
            UseCompact = true
        },
        new()
        {
            Enabled = true,
            Name = "APU Clock, Volt, Temp: (GHz), (V), (C)",
            Color = "#44AFAC",
            UseCompact = true
        },
        new()
        {
            Enabled = true,
            Name = "Framerate: (FPS)",
            Color = "#44AFAC",
            UseCompact = true
        }
    ];
    public bool IsAdvancedCodeEditorEnabled = false;
    public string AdvancedCodeEditor =
        "<C0=FFA0A0><C1=A0FFA0><C2=FC89AC><C3=fa2363><S1=70><S2=-50>\n" +
        "<C0>Saku Overclock <C1>" + ViewModels.ГлавнаяViewModel.GetVersion() + ": <S0>$SelectedProfile$\n" +
        "<S1><C2>STAPM, Fast, Slow: <C3><S0>$stapm_value$<S2>W<S1>$stapm_limit$W <S0>$fast_value$<S2>W<S1>$fast_limit$W <S0>$slow_value$<S2>W<S1>$slow_limit$W\n" +
        "<C2>EDC, Therm, CPU Usage: <C3><S0>$vrmedc_value$<S2>A<S1>$vrmedc_max$A <C3><S0>$cpu_temp_value$<S2>C<S1>$cpu_temp_max$C<C3><S0> $cpu_usage$<S2>%<S1>\n" +
        "<S1><C2>Clocks: $cpu_clock_cycle$<S1><C2>$currCore$:<S0><C3> $cpu_core_clock$<S2>GHz<S1>$cpu_core_voltage$V $cpu_clock_cycle_end$\n" +
        "<C2>AVG Clock, Volt: <C3><S0>$average_cpu_clock$<S2>GHz<S1>$average_cpu_voltage$V" +
        "<C2>APU Clock, Volt, Temp: <C3><S0>$gfx_clock$<S2>MHz<S1>$gfx_volt$V <S0>$gfx_temp$<S1>C\n" +
        "<C2>Framerate <C3><S0>%FRAMERATE% %FRAMETIME%"; 
}
public class RTSSElementsClass
{
    public bool Enabled = true;
    public string Name = "Element Name";
    public string Color = "#FFFFAC";
    public bool UseCompact = false; 
}
