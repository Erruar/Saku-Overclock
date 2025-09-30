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
    public bool OldTitleBar
    {
        get;
        set;
    } = false; // Флаг старого тайтл бара приложения

    public bool FixedTitleBar
    {
        get;
        set;
    } = false; // Флаг фиксированного тайтл бара приложения

    public int AutostartType
    {
        get;
        set;
    } =
        0; // Тип автостарта: 0 - выкл, 1 - при запуске приложения сразу в трей, 2 - автостарт с системой, 3 - автостарт и трей

    public int HidingType
    {
        get;
        set;
    } =
        2; // Тип скрытия в трей: 0 - выкл, 1 - при сворачивании приложения сразу в трей, 2 - при закрытии приложения сразу в трей

    public bool CheckForUpdates
    {
        get;
        set;
    } = true; // Флаг автообновлений

    public bool HotkeysEnabled
    {
        get;
        set;
    } = true; // Флаг включены ли горячие клавиши в приложении или нет

    public string HotkeysSwitchingCustomProfiles
    {
        get;
        set;
    } = "All"; // Список избранных переключаемых кастомных пресетов

    public string HotkeysSwitchingPremadeProfiles
    {
        get;
        set;
    } = "All"; // Список переключаемых готовых пресетов

    public bool ReapplyLatestSettingsOnAppLaunch
    {
        get;
        set;
    } = true; // При запуске приложения переприменять последние применённые параметры

    public bool ReapplySafeOverclock
    {
        get;
        set;
    } = true; // Переприменять последние безопасные параметры

    public bool ReapplyOverclock
    {
        get;
        set;
    } = true; // Переприменять последние применённые параметры

    public double ReapplyOverclockTimer
    {
        get;
        set;
    } = 3.0; // Время переприменения параметров (в секундах)

    public int ThemeType
    {
        get;
        set;
    } = 0; // Выбранная тема

    public bool NiIconsEnabled
    {
        get;
        set;
    } = false; // Включён ли Треймон

    public bool RtssMetricsEnabled
    {
        get;
        set;
    } = false; // Включены ли RTSS Метрики

    public int NiIconsType
    {
        get;
        set;
    } = -1; // Тип отображаемых иконок Треймона

    public bool StreamStabilizerEnabled
    {
        get;
        set;
    } = false; // Включен ли оптимизатор стрима

    public int StreamStabilizerType
    {
        get;
        set;
    } =
        0; // Тип оптимизатора стрима: 0 - базовая блокировка частоты, 1 - до максимальной частоты, 2 - до процента максимальной частоты

    public int StreamStabilizerMaxMHz
    {
        get;
        set;
    } = 3000; // Максимальная частота процессора при использовании оптимизатора стрима

    public int StreamStabilizerMaxPercentMHz
    {
        get;
        set;
    } = 80; // Максимальный процент частоты процессора при использовании оптимизатора стрима

    public bool ProfilespageViewModeBeginner
    {
        get;
        set;
    } = true; // Пресеты, готовые пресеты и параметры

    public int Preset
    {
        get;
        set;
    } = 0; // Выбранный пресет

    public string RyzenAdjLine
    {
        get;
        set;
    } = string.Empty; // RyzenADJline для применения параметров

    public bool PremadeMinActivated
    {
        get;
        set;
    } = false; // Готовые пресеты

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
    } = 0; // Уровень оптимизации пресета: 0 - Базовый, 1 - Стандартный, 2 - Расширенный

    public int PremadeCurveOptimizerOverrideLevel
    {
        get;
        set;
    } = -10; // Уровень андервольтинга для Расширенного режима оптимизации всех готовых пресетов

    public bool AppFirstRun
    {
        get;
        set;
    } = true; // Первый запуск приложения

    public int AppFirstRunType
    {
        get;
        set;
    } = 0; // Страница первого запуска (Поможет при закрытии приложения после первого запуска без настройки)

    // Страница управления кулером
    public bool IsNbfcModeEnabled
    {
        get;
        set;
    } = true; // Выбранная конфигурация страницы управления кулером

    public string NbfcConfigXmlName
    {
        get;
        set;
    } = string.Empty; // Имя выбранного файла NBFC конфига

    public bool NbfcAutoUpdateInformation
    {
        get;
        set;
    } = true; // Автообновление информации о скоростях кулеров

    public int NbfcServiceType
    {
        get;
        set;
    } = 0; // Флаги использования NBFC

    public double NbfcFan1UserFanSpeedRpm
    {
        get;
        set;
    } = 110.0; // Скорость первого кулера (>100 = авто)

    public double NbfcFan2UserFanSpeedRpm
    {
        get;
        set;
    } = 110.0; // Скорость второго кулера (>100 = авто)

    public bool NbfcFlagConsoleCheckSpeedRunning
    {
        get;
        set;
    } = false; // Флаг действий с консолью NBFC

    public bool FlagRyzenAdjConsoleTemperatureCheckRunning
    {
        get;
        set;
    } = false; // Флаг действий с консолью RyzenADJ

    public string NbfcAnswerSpeedFan1
    {
        get;
        set;
    } = string.Empty; // Ответ NBFC для первого кулера

    public string NbfcAnswerSpeedFan2
    {
        get;
        set;
    } = string.Empty; // Ответ NBFC для второго кулера

    public bool CoolerLastSavedAsusMode
    {
        get;
        set;
    } = false; // Сохранённый режим управления кулерами Asus

    public bool AsusModeAutoUpdateInformation
    {
        get;
        set;
    } = true; // Автообновление информации о кулерах Asus

    public int AsusCoolerServiceType
    {
        get;
        set;
    } = 0; // Тип управления кулерами Asus: выключено, только чтение, включено

    public int AsusModeSelectedMode
    {
        get;
        set;
    } = 0; // Режим управления Asus кулером

    public double AsusModeFan1UserFanSpeedRpm
    {
        get;
        set;
    } = 110.0; // Скорость первого кулера Asus (>100 = авто)

    public double AsusModeFan2UserFanSpeedRpm
    {
        get;
        set;
    } = 110.0; // Скорость второго кулера Asus (>100 = авто)

    // Загрузка настроек
    public void LoadSettings()
    {
        var settings = _fileService.Read<AppSettingsService>(_applicationDataFolder, FileName);
        foreach (var prop in typeof(AppSettingsService).GetProperties())
        {
            var value = prop.GetValue(settings);
            if (value != null)
            {
                prop.SetValue(this, value);
            }
        }
    }

    // Сохранение настроек
    public void SaveSettings() => _fileService.Save(_applicationDataFolder, FileName, this);
}