namespace Saku_Overclock.Contracts.Services;

public interface IAppSettingsService
{
    /// <summary>
    ///     Сохранение настроек
    /// </summary>
    void SaveSettings();

    /// <summary>
    ///     Загрузка настроек
    /// </summary>
    void LoadSettings();

    /// <summary>
    ///     Флаг фиксированного тайтл бара приложения
    /// </summary>
    bool FixedTitleBar
    {
        get;
        set;
    }

    /// <summary>
    ///     Тип автостарта:
    ///     0 - выкл,
    ///     1 - при запуске приложения сразу в трей,
    ///     2 - автостарт с системой,
    ///     3 - автостарт и трей
    /// </summary>
    int AutostartType
    {
        get;
        set;
    }

    /// <summary>
    ///     Тип скрытия в трей:
    ///     0 - выкл,
    ///     1 - при сворачивании приложения сразу в трей,
    ///     2 - при закрытии приложения сразу в трей
    /// </summary>
    int HidingType
    {
        get;
        set;
    }

    /// <summary>
    ///     Флаг автообновлений
    /// </summary>
    bool CheckForUpdates
    {
        get;
        set;
    }

    /// <summary>
    ///     Флаг включены ли горячие клавиши в приложении или нет
    /// </summary>
    bool HotkeysEnabled
    {
        get;
        set;
    }

    /// <summary>
    ///     При запуске приложения переприменять последние применённые параметры
    /// </summary>
    bool ReapplyLatestSettingsOnAppLaunch
    {
        get;
        set;
    }

    /// <summary>
    ///     Переприменять последние безопасные параметры
    /// </summary>
    bool ReapplySafeOverclock
    {
        get;
        set;
    }

    /// <summary>
    ///     Переприменять последние применённые параметры
    /// </summary>
    bool ReapplyOverclock
    {
        get;
        set;
    }

    /// <summary>
    ///     Время переприменения параметров (в секундах)
    /// </summary>
    double ReapplyOverclockTimer
    {
        get;
        set;
    }

    /// <summary>
    ///     Выбранная тема
    /// </summary>
    int ThemeType
    {
        get;
        set;
    }

    /// <summary>
    ///     Включён ли Треймон
    /// </summary>
    bool NiIconsEnabled
    {
        get;
        set;
    }

    /// <summary>
    ///     Включены ли RTSS Метрики
    /// </summary>
    bool RtssMetricsEnabled
    {
        get;
        set;
    }

    /// <summary>
    ///     Тип отображаемых иконок Треймона
    /// </summary>
    int NiIconsType
    {
        get;
        set;
    }

    /// <summary>
    ///     Включен ли оптимизатор стрима
    /// </summary>
    bool StreamStabilizerEnabled
    {
        get;
        set;
    }

    /// <summary>
    ///     Тип оптимизатора стрима:
    ///     0 - базовая блокировка частоты,
    ///     1 - до максимальной частоты,
    ///     2 - до процента максимальной частоты
    /// </summary>
    int StreamStabilizerType
    {
        get;
        set;
    }

    /// <summary>
    ///     Максимальная частота процессора при использовании оптимизатора стрима
    /// </summary>
    int StreamStabilizerMaxMHz
    {
        get;
        set;
    }

    /// <summary>
    ///     Максимальный процент частоты процессора при использовании оптимизатора стрима
    /// </summary>
    int StreamStabilizerMaxPercentMHz
    {
        get;
        set;
    }

    /// <summary>
    ///     Текущий режим отображения настроек на странице управления пресетами - Новичок, если false - Про
    /// </summary>
    bool PresetspageViewModeBeginner
    {
        get;
        set;
    }

    // Пресеты, готовые пресеты и параметры

    /// <summary>
    ///     Выбранный пресет
    /// </summary>
    int Preset
    {
        get;
        set;
    }

    /// <summary>
    ///     RyzenAdjLine для применения параметров
    /// </summary>
    string RyzenAdjLine
    {
        get;
        set;
    }

    // Готовые пресеты

    /// <summary>
    ///     Флаг активированного готового пресета "Минимум"
    /// </summary>
    bool PremadeMinActivated
    {
        get;
        set;
    }

    /// <summary>
    ///     Флаг активированного готового пресета "Эко"
    /// </summary>
    bool PremadeEcoActivated
    {
        get;
        set;
    }

    /// <summary>
    ///     Флаг активированного готового пресета "Баланс"
    /// </summary>
    bool PremadeBalanceActivated
    {
        get;
        set;
    }

    /// <summary>
    ///     Флаг активированного готового пресета "Скорость"
    /// </summary>
    bool PremadeSpeedActivated
    {
        get;
        set;
    }

    /// <summary>
    ///     Флаг активированного готового пресета "Максимум"
    /// </summary>
    bool PremadeMaxActivated
    {
        get;
        set;
    }

    /// <summary>
    ///     Уровень оптимизации пресета:
    ///     0 - Базовый,
    ///     1 - Стандартный,
    ///     2 - Расширенный
    /// </summary>
    int PremadeOptimizationLevel
    {
        get;
        set;
    }

    /// <summary>
    ///     Уровень андервольтинга для Расширенного режима оптимизации всех готовых пресетов
    /// </summary>
    int PremadeCurveOptimizerOverrideLevel
    {
        get;
        set;
    }

    /// <summary>
    ///     Флаг активации первого запуска приложения
    /// </summary>
    bool AppFirstRun
    {
        get;
        set;
    }

    /// <summary>
    ///     Страница первого запуска (Поможет при закрытии приложения после первого запуска без настройки)
    /// </summary>
    int AppFirstRunType
    {
        get;
        set;
    }

    // Настройки управления кулером

    /// <summary>
    ///     Выбранная конфигурация страницы управления кулером
    /// </summary>
    bool IsNbfcModeEnabled
    {
        get;
        set;
    }

    /// <summary>
    ///     Имя выбранного файла NBFC конфига
    /// </summary>
    string NbfcConfigXmlName
    {
        get;
        set;
    }

    /// <summary>
    ///     Флаги использования NBFC
    /// </summary>
    int NbfcServiceType
    {
        get;
        set;
    }

    /// <summary>
    ///     Скорость первого кулера (>100 = авто)
    /// </summary>
    double NbfcFan1UserFanSpeedRpm
    {
        get;
        set;
    }

    /// <summary>
    ///     Скорость второго кулера (>100 = авто)
    /// </summary>
    double NbfcFan2UserFanSpeedRpm
    {
        get;
        set;
    }

    /// <summary>
    ///     Ответ NBFC для первого кулера
    /// </summary>
    double NbfcAnswerSpeedFan1
    {
        get;
        set;
    }

    /// <summary>
    ///     Ответ NBFC для второго кулера
    /// </summary>
    double NbfcAnswerSpeedFan2
    {
        get;
        set;
    }

    /// <summary>
    ///     Тип управления кулерами Asus:
    ///     0 - выключено,
    ///     1 - только чтение,
    ///     2 - включено
    /// </summary>
    int AsusCoolerServiceType
    {
        get;
        set;
    }

    /// <summary>
    ///     Скорость первого кулера Asus (>100 = авто)
    /// </summary>
    double AsusModeFan1UserFanSpeedRpm
    {
        get;
        set;
    }

    /// <summary>
    ///     Скорость первого кулера Asus (>100 = авто)
    /// </summary>
    double AsusModeFan2UserFanSpeedRpm
    {
        get;
        set;
    }
}