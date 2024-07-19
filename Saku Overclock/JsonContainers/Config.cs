namespace Saku_Overclock;
#pragma warning disable CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.
#pragma warning disable CS0649 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.
internal class Config
{
    //Настройки приложения
    public bool OldTitleBar = false; //Флаг старого тайтл бара приложения
    public bool FixedTitleBar = false; //Флаг фиксированного тайтл бара приложения
    public int AutostartType = 0; //Тип автостарта: 0 - выкл, 1 - при запуске приложения сразу в трей, 2 - автостарт с системой, 3 - автостарт и трей
    public bool CheckForUpdates = true; //Флаг автообновлений. Надеюсь скоро пригодится
    public bool ReapplyLatestSettingsOnAppLaunch = true; //При запуске приложения переприменять последние применённые параметры в прошлый раз после закрытия приложения
    public bool ReapplySafeOverclock = true; //Переприменять последние Безопасные применённые параметры каждые
    public bool ReapplyOverclock = true; //Переприменять последние применённые параметры каждые
    public double ReapplyOverclockTimer = 3.0; //Переприменять последние применённые параметры каждые (время в секундах)
    public int ThemeType = 0; //Выбранная тема
    //Пресеты, готовые пресеты и параметры
    public int Preset = 0; //Выбранный пользователем пресет
    public string RyzenADJline = ""; //Собственно RyzenADJline которая применяет все параметры, соединяя всё активированное в пресете
    public string ApplyInfo = ""; //Информация об ошибках послежних применённых параметрах
    public bool RangeApplied = false; //Применён ли диапазон для команды SMU
    public bool PremadeMinActivated = false; //Готовые пресеты
    public bool PremadeEcoActivated = false; //Готовые пресеты
    public bool PremadeBalanceActivated = false; //Готовые пресеты
    public bool PremadeSpeedActivated = false; //Готовые пресеты
    public bool PremadeMaxActivated = true; //Готовые пресеты
    public bool FlagRyzenADJConsoleRunning = false; //Старый флаг если выполняется какое-то действие с консолью, например RyzenADJ
    //Страница управления кулером
    public string NBFCConfigXMLName; //Имя выбранного файла конфига nbfc
    public bool NBFCAutoUpdateInformation = true; //Автоматически обновлять информаци о скоростях кулеров
    public bool NBFCServiceStatusDisabled = true; //Флаги использования nbfc на странице управления кулером
    public bool NBFCServiceStatusReadOnly = false; //Флаги использования nbfc на странице управления кулером
    public bool NBFCServiceStatusEnabled = false; //Флаги использования nbfc на странице управления кулером
    public double NBFCFan1UserFanSpeedRPM = 110.0; //Выставленная пользователем скорость вращения первым кулером, больше 100 - авто 
    public double NBFCFan2UserFanSpeedRPM = 110.0; //Выставленная пользователем скорость вращения вторым кулером, больше 100 - авто 
    public bool NBFCFlagConsoleCheckSpeedRunning = false; //Флаг для страницы управления кулером, сейчас происходит действие с консолью при помощи nbfc
    public bool FlagRyzenADJConsoleTemperatureCheckRunning = false; //Флаг для страницы управления кулером,  сейчас происходит действие с консолью при помощи RyzenADJ получаем температуру
    public string NBFCAnswerSpeedFan1; //Ответ nbfc консоли для вывода информации от первого кулера
    public string NBFCAnswerSpeedFan2; //Ответ nbfc консоли для вывода информации от второго кулера
}
#pragma warning restore CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.
