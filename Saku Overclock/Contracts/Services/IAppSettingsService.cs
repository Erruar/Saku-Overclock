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
    ///     1 - автостарт с системой,
    ///     2 - автостарт и трей
    /// </summary>
    int AutostartType
    {
        get;
        set;
    }

    /// <summary>
    ///     При закрытии приложения сразу в трей
    /// </summary>
    bool HideToTray
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
    ///     Текущий режим отображения настроек на странице управления пресетами - Новичок, если false - Про
    /// </summary>
    bool PresetsPageViewModeBeginner
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
    ///     Флаг создания готовых пресетов, при их отсутствии
    /// </summary>
    public bool PremadePresetsAdded
    {
        get;
        set;
    }
    
    /// <summary>
    ///     Выбранный пресет для применения от сети
    /// </summary>
    public string AcPreset
    {
        get;
        set;
    }
    
    /// <summary>
    ///     Выбранный пресет для применения от батареи
    /// </summary>
    public string BatteryPreset
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

    /// <summary>
    ///     Флаг активации первого запуска приложения
    /// </summary>
    bool AppFirstRun
    {
        get;
        set;
    }

    // Настройки управления кулером
}