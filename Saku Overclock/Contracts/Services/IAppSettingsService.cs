namespace Saku_Overclock.Contracts.Services;

public interface IAppSettingsService
{
    void SaveSettings();
    void LoadSettings();

    public bool OldTitleBar
    {
        get;
        set;
    } // Флаг старого тайтл бара приложения

    public bool FixedTitleBar
    {
        get;
        set;
    } // Флаг фиксированного тайтл бара приложения

    public int AutostartType
    {
        get;
        set;
    } // Тип автостарта: 0 - выкл, 1 - при запуске приложения сразу в трей, 2 - автостарт с системой, 3 - автостарт и трей

    public bool CheckForUpdates
    {
        get;
        set;
    } // Флаг автообновлений

    public bool HotkeysEnabled
    {
        get;
        set;
    } // Флаг включены ли горячие клавиши в приложении или нет

    public bool ReapplyLatestSettingsOnAppLaunch
    {
        get;
        set;
    } // При запуске приложения переприменять последние применённые параметры

    public bool ReapplySafeOverclock
    {
        get;
        set;
    } // Переприменять последние безопасные параметры

    public bool ReapplyOverclock
    {
        get;
        set;
    } // Переприменять последние применённые параметры

    public double ReapplyOverclockTimer
    {
        get;
        set;
    } // Время переприменения параметров (в секундах)

    public int ThemeType
    {
        get;
        set;
    } // Выбранная тема

    public bool NiIconsEnabled
    {
        get;
        set;
    } // Включён ли Треймон

    public bool RTSSMetricsEnabled
    {
        get;
        set;
    } // Включены ли RTSS Метрики

    public int NiIconsType
    {
        get;
        set;
    }

    // Пресеты, готовые пресеты и параметры
    public int Preset
    {
        get;
        set;
    } // Выбранный пресет

    public string RyzenADJline
    {
        get;
        set;
    } // RyzenADJline для применения параметров

    public bool PremadeMinActivated
    {
        get;
        set;
    } // Готовые пресеты

    public bool PremadeEcoActivated
    {
        get;
        set;
    }

    public bool PremadeBalanceActivated
    {
        get;
        set;
    }

    public bool PremadeSpeedActivated
    {
        get;
        set;
    }

    public bool PremadeMaxActivated
    {
        get;
        set;
    }

    // Страница управления кулером
    public string NBFCConfigXMLName
    {
        get;
        set;
    } // Имя выбранного файла NBFC конфига

    public bool NBFCAutoUpdateInformation
    {
        get;
        set;
    } // Автообновление информации о скоростях кулеров

    public bool NBFCServiceStatusDisabled
    {
        get;
        set;
    } // Флаги использования NBFC

    public bool NBFCServiceStatusReadOnly
    {
        get;
        set;
    }

    public bool NBFCServiceStatusEnabled
    {
        get;
        set;
    }

    public double NBFCFan1UserFanSpeedRPM
    {
        get;
        set;
    } // Скорость первого кулера (>100 = авто)

    public double NBFCFan2UserFanSpeedRPM
    {
        get;
        set;
    } // Скорость второго кулера (>100 = авто)

    public bool NBFCFlagConsoleCheckSpeedRunning
    {
        get;
        set;
    } // Флаг действий с консолью NBFC

    public bool FlagRyzenADJConsoleTemperatureCheckRunning
    {
        get;
        set;
    } // Флаг действий с консолью RyzenADJ

    public string NBFCAnswerSpeedFan1
    {
        get;
        set;
    } // Ответ NBFC для первого кулера

    public string NBFCAnswerSpeedFan2
    {
        get;
        set;
    } // Ответ NBFC для второго кулера

    public bool CoolerLastSavedAsusMode
    {
        get;
        set;
    } // Сохранённый режим управления кулерами Asus

    public bool AsusModeAutoUpdateInformation
    {
        get;
        set;
    } // Автообновление информации о кулерах Asus

    public int AsusModeSelectedMode
    {
        get;
        set;
    } // Режим управления Asus кулером

    public double AsusModeFan1UserFanSpeedRPM
    {
        get;
        set;
    } // Скорость первого кулера Asus (>100 = авто)
}