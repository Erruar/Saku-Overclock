namespace Saku_Overclock.Models;

public class AppSettings
{
    public bool FixedTitleBar { get; set; } = false;
    public int AutostartType { get; set; } = 0;
    public bool HideToTray { get; set; } = true;
    public bool CheckForUpdates { get; set; } = true;
    public bool HotkeysEnabled { get; set; } = true;
    public bool ReapplyLatestSettingsOnAppLaunch { get; set; } = true;
    public bool ReapplyOverclock { get; set; } = true;
    public double ReapplyOverclockTimer { get; set; } = 3.0;
    public int ThemeType { get; set; } = 0;
    public bool NiIconsEnabled { get; set; } = false;
    public bool RtssMetricsEnabled { get; set; } = false;
    public int NiIconsType { get; set; } = -1;
    public bool PresetsPageViewModeBeginner { get; set; } = true;
    public int Preset { get; set; } = 0;
    public bool PremadePresetsAdded { get; set; } = false;
    public string AcPreset { get; set; } = string.Empty;
    public string BatteryPreset { get; set; } = string.Empty;
    public string RyzenAdjLine { get; set; } = string.Empty;
    public bool AppFirstRun { get; set; } = true;
}