namespace Saku_Overclock.Contracts.Services;

public interface IAppSettingsService
{
    void SaveSettings();
    void LoadSettings();

    bool OldTitleBar
    {
        get;
        set;
    } // Флаг старого тайтл бара приложения

    bool FixedTitleBar
    {
        get;
        set;
    } // Флаг фиксированного тайтл бара приложения

    int AutostartType
    {
        get;
        set;
    } // Тип автостарта: 0 - выкл, 1 - при запуске приложения сразу в трей, 2 - автостарт с системой, 3 - автостарт и трей

    int HidingType
    {
        get;
        set;
    } // Тип скрытия в трей: 0 - выкл, 1 - при сворачивании приложения сразу в трей, 2 - при закрытии приложения сразу в трей

    bool CheckForUpdates
    {
        get;
        set;
    } // Флаг автообновлений

    bool HotkeysEnabled
    {
        get;
        set;
    } // Флаг включены ли горячие клавиши в приложении или нет

    bool ReapplyLatestSettingsOnAppLaunch
    {
        get;
        set;
    } // При запуске приложения переприменять последние применённые параметры

    bool ReapplySafeOverclock
    {
        get;
        set;
    } // Переприменять последние безопасные параметры

    bool ReapplyOverclock
    {
        get;
        set;
    } // Переприменять последние применённые параметры

    double ReapplyOverclockTimer
    {
        get;
        set;
    } // Время переприменения параметров (в секундах)

    int ThemeType
    {
        get;
        set;
    } // Выбранная тема

    bool NiIconsEnabled
    {
        get;
        set;
    } // Включён ли Треймон

    bool RTSSMetricsEnabled
    {
        get;
        set;
    } // Включены ли RTSS Метрики

    int NiIconsType
    {
        get;
        set;
    } // Тип отображаемых иконок Треймона

    bool StreamOptimizerEnabled
    {
        get;
        set;
    } // Включен ли оптимизатор стрима

    bool CurveOptimizerOverallEnabled
    {
        get;
        set;
    } // Включен ли глобальный андервольтинг

    int CurveOptimizerOverallLevel
    {
        get;
        set;
    } // Уровень глобального андервольтинга (0 - лёгкий, 1 - средний, 2 - сильный)

    bool ProfilespageViewModeBeginner
    {
        get;
        set;
    } // Текущий режим отображения настроек на странице управления профилями - Новичок, если false - Про

    // Пресеты, готовые пресеты и параметры
    int Preset
    {
        get;
        set;
    } // Выбранный пресет

    string RyzenADJline
    {
        get;
        set;
    } // RyzenADJline для применения параметров

    bool PremadeMinActivated
    {
        get;
        set;
    } // Готовые пресеты

    bool PremadeEcoActivated
    {
        get;
        set;
    }

    bool PremadeBalanceActivated
    {
        get;
        set;
    }

    bool PremadeSpeedActivated
    {
        get;
        set;
    }

    bool PremadeMaxActivated
    {
        get;
        set;
    }

    // Страница управления кулером
    bool IsNBFCModeEnabled
    {
        get; set;
    } // Выбранная конфигурация страницы управления кулером
    string NBFCConfigXMLName
    {
        get;
        set;
    } // Имя выбранного файла NBFC конфига

    bool NBFCAutoUpdateInformation
    {
        get;
        set;
    } // Автообновление информации о скоростях кулеров

    int NBFCServiceType
    {
        get;
        set;
    } // Флаги использования NBFC 

    double NBFCFan1UserFanSpeedRPM
    {
        get;
        set;
    } // Скорость первого кулера (>100 = авто)

    double NBFCFan2UserFanSpeedRPM
    {
        get;
        set;
    } // Скорость второго кулера (>100 = авто)

    bool NBFCFlagConsoleCheckSpeedRunning
    {
        get;
        set;
    } // Флаг действий с консолью NBFC

    bool FlagRyzenADJConsoleTemperatureCheckRunning
    {
        get;
        set;
    } // Флаг действий с консолью RyzenADJ

    string NBFCAnswerSpeedFan1
    {
        get;
        set;
    } // Ответ NBFC для первого кулера

    string NBFCAnswerSpeedFan2
    {
        get;
        set;
    } // Ответ NBFC для второго кулера

    bool CoolerLastSavedAsusMode
    {
        get;
        set;
    } // Сохранённый режим управления кулерами Asus

    bool AsusModeAutoUpdateInformation
    {
        get;
        set;
    } // Автообновление информации о кулерах Asus

    int AsusModeSelectedMode
    {
        get;
        set;
    } // Режим управления Asus кулером

    double AsusModeFan1UserFanSpeedRPM
    {
        get;
        set;
    } // Скорость первого кулера Asus (>100 = авто)
}