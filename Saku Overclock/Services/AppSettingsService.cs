using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Core.Contracts.Services;

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

    public int HidingType
    {
        get;
        set;
    } =
        2;

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

    public bool ReapplySafeOverclock
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

    public bool StreamStabilizerEnabled
    {
        get;
        set;
    } = false;

    public int StreamStabilizerType
    {
        get;
        set;
    } =
        0;

    public int StreamStabilizerMaxMHz
    {
        get;
        set;
    } = 3000;

    public int StreamStabilizerMaxPercentMHz
    {
        get;
        set;
    } = 80;

    public bool PresetspageViewModeBeginner
    {
        get;
        set;
    } = true;

    public int Preset
    {
        get;
        set;
    } = 0;

    public string RyzenAdjLine
    {
        get;
        set;
    } = string.Empty;

    public bool PremadeMinActivated
    {
        get;
        set;
    } = false;

    public bool PremadeEcoActivated
    {
        get;
        set;
    } = false;

    public bool PremadeBalanceActivated
    {
        get;
        set;
    } = false;

    public bool PremadeSpeedActivated
    {
        get;
        set;
    } = false;

    public bool PremadeMaxActivated
    {
        get;
        set;
    } = true;

    public int PremadeOptimizationLevel
    {
        get;
        set;
    } = 0;

    public int PremadeCurveOptimizerOverrideLevel
    {
        get;
        set;
    } = -10;

    public bool AppFirstRun
    {
        get;
        set;
    } = true;

    public int AppFirstRunType
    {
        get;
        set;
    } = 0;

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

    public void SaveSettings() => _fileService.Save(_applicationDataFolder, FileName, this);
}