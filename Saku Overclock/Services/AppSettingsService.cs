using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Models;

namespace Saku_Overclock.Services;

public class AppSettingsService(IFileService fileService) : IAppSettingsService
{
    private const string FolderPath = "Saku Overclock/Settings";
    private const string FileName = "AppSettings.json";

    private readonly string _applicationDataFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FolderPath);

    private AppSettings _settings = new();

    public bool FixedTitleBar
    {
        get => _settings.FixedTitleBar;
        set => _settings.FixedTitleBar = value;
    }

    public int AutostartType
    {
        get => _settings.AutostartType;
        set => _settings.AutostartType = value;
    }

    public bool HideToTray
    {
        get => _settings.HideToTray;
        set => _settings.HideToTray = value;
    }

    public bool CheckForUpdates
    {
        get => _settings.CheckForUpdates;
        set => _settings.CheckForUpdates = value;
    }

    public bool HotkeysEnabled
    {
        get => _settings.HotkeysEnabled;
        set => _settings.HotkeysEnabled = value;
    }

    public bool ReapplyLatestSettingsOnAppLaunch
    {
        get => _settings.ReapplyLatestSettingsOnAppLaunch;
        set => _settings.ReapplyLatestSettingsOnAppLaunch = value;
    }

    public bool ReapplyOverclock
    {
        get => _settings.ReapplyOverclock;
        set => _settings.ReapplyOverclock = value;
    }

    public double ReapplyOverclockTimer
    {
        get => _settings.ReapplyOverclockTimer;
        set => _settings.ReapplyOverclockTimer = value;
    }

    public int ThemeType
    {
        get => _settings.ThemeType;
        set => _settings.ThemeType = value;
    }

    public bool NiIconsEnabled
    {
        get => _settings.NiIconsEnabled;
        set => _settings.NiIconsEnabled = value;
    }

    public bool RtssMetricsEnabled
    {
        get => _settings.RtssMetricsEnabled;
        set => _settings.RtssMetricsEnabled = value;
    }

    public int NiIconsType
    {
        get => _settings.NiIconsType;
        set => _settings.NiIconsType = value;
    }

    public bool PresetsPageViewModeBeginner
    {
        get => _settings.PresetsPageViewModeBeginner;
        set => _settings.PresetsPageViewModeBeginner = value;
    }

    public int Preset
    {
        get => _settings.Preset;
        set => _settings.Preset = value;
    }

    public bool PremadePresetsAdded
    {
        get => _settings.PremadePresetsAdded;
        set => _settings.PremadePresetsAdded = value;
    }

    public string AcPreset
    {
        get => _settings.AcPreset;
        set => _settings.AcPreset = value;
    }

    public string BatteryPreset
    {
        get => _settings.BatteryPreset;
        set => _settings.BatteryPreset = value;
    }

    public string RyzenAdjLine
    {
        get => _settings.RyzenAdjLine;
        set => _settings.RyzenAdjLine = value;
    }

    public bool AppFirstRun
    {
        get => _settings.AppFirstRun;
        set => _settings.AppFirstRun = value;
    }

    public void LoadSettings()
    {
        try
        {
            var loaded = fileService.Read<AppSettings>(_applicationDataFolder, FileName);

            if (loaded != null)
            {
                _settings = loaded;
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex);
        }
    }

    public void SaveSettings() => fileService.Save(_applicationDataFolder, FileName, _settings);
}