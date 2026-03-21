using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Core.Contracts.Services;
using Saku_Overclock.Helpers;

namespace Saku_Overclock.Services;

public class AppSettingsService : IAppSettingsService
{
    private const string FolderPath = "Saku Overclock/Settings";
    private const string FileName = "AppSettings.json";

    private readonly string _localApplicationData =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private readonly string _applicationDataFolder;

    private readonly IFileService _fileService;

    public AppSettingsService(IFileService fileService)
    {
        _applicationDataFolder = Path.Combine(_localApplicationData, FolderPath);
        _fileService = fileService;
    }

    // Настройки приложения

    public bool FixedTitleBar
    {
        get;
        set;
    } = false;

    public int AutostartType
    {
        get;
        set;
    } =
        0;

    public bool HideToTray
    {
        get;
        set;
    } = true;

    public bool CheckForUpdates
    {
        get;
        set;
    } = true;

    public bool HotkeysEnabled
    {
        get;
        set;
    } = true;

    public bool ReapplyLatestSettingsOnAppLaunch
    {
        get;
        set;
    } = true;

    public bool ReapplyOverclock
    {
        get;
        set;
    } = true;

    public double ReapplyOverclockTimer
    {
        get;
        set;
    } = 3.0;

    public int ThemeType
    {
        get;
        set;
    } = 0;

    public bool NiIconsEnabled
    {
        get;
        set;
    } = false;

    public bool RtssMetricsEnabled
    {
        get;
        set;
    } = false;

    public int NiIconsType
    {
        get;
        set;
    } = -1;

    public bool PresetsPageViewModeBeginner
    {
        get;
        set;
    } = true;

    public int Preset
    {
        get;
        set;
    } = 0;
    
    public string AcPreset
    {
        get;
        set;
    } = string.Empty;
    
    public string BatteryPreset
    {
        get;
        set;
    } = string.Empty;

    public string RyzenAdjLine
    {
        get;
        set;
    } = string.Empty;

    public bool AppFirstRun
    {
        get;
        set;
    } = true;

    // Настройки управления кулером
    public bool IsNbfcModeEnabled
    {
        get;
        set;
    } = true;

    public string NbfcConfigXmlName
    {
        get;
        set;
    } = string.Empty;

    public int NbfcServiceType
    {
        get;
        set;
    } = 0;

    public double NbfcFan1UserFanSpeedRpm
    {
        get;
        set;
    } = 110.0;

    public double NbfcFan2UserFanSpeedRpm
    {
        get;
        set;
    } = 110.0;

    public double NbfcAnswerSpeedFan1
    {
        get;
        set;
    } = -1;

    public double NbfcAnswerSpeedFan2
    {
        get;
        set;
    } = -1;

    public int AsusCoolerServiceType
    {
        get;
        set;
    } = 0;

    public double AsusModeFan1UserFanSpeedRpm
    {
        get;
        set;
    } = 110.0;

    public double AsusModeFan2UserFanSpeedRpm
    {
        get;
        set;
    } = 110.0;

    public void LoadSettings()
    {
        try
        {
            var settings = _fileService.Read<AppSettingsService>(_applicationDataFolder, FileName);

            if (settings == null)
            {
                return;
            }

            foreach (var prop in typeof(AppSettingsService).GetProperties())
            {
                if (prop is { CanRead: true, CanWrite: true })
                {
                    var value = prop.GetValue(settings);
                    if (value != null)
                    {
                        prop.SetValue(this, value);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex);
        }
    }

    public void SaveSettings() => _fileService.Save(_applicationDataFolder, FileName, this);
}