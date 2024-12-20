﻿namespace Saku_Overclock;
#pragma warning disable CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.
#pragma warning disable CS0649 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.
internal class Config
{
    //Настройки приложения
    public bool OldTitleBar = false; // Флаг старого тайтл бара приложения
    public bool FixedTitleBar = false; // Флаг фиксированного тайтл бара приложения
    public int AutostartType = 0; // Тип автостарта: 0 - выкл, 1 - при запуске приложения сразу в трей, 2 - автостарт с системой, 3 - автостарт и трей
    public bool CheckForUpdates = true; // Флаг автообновлений
    public bool HotkeysEnabled = true; // Флаг включены ли горячии клавиши в приложении или нет
    public bool ReapplyLatestSettingsOnAppLaunch = true; // При запуске приложения переприменять последние применённые параметры в прошлый раз после закрытия приложения
    public bool ReapplySafeOverclock = true; // Переприменять последние Безопасные применённые параметры каждые
    public bool ReapplyOverclock = true; // Переприменять последние применённые параметры каждые
    public double ReapplyOverclockTimer = 3.0; // Переприменять последние применённые параметры каждые (время в секундах)
    public int ThemeType = 0; // Выбранная тема
    public bool NiIconsEnabled = false; // Включён Треймон в ядре приложения
    public bool RTSSMetricsEnabled = false; // Включёны RTSS Метрики в ядре приложения
    public int NiIconsType = -1;
    // Пресеты, готовые пресеты и параметры
    public int Preset = 0; // Выбранный пользователем пресет
    public string RyzenADJline = ""; // Собственно RyzenADJline которая применяет все параметры, соединяя всё активированное в пресете
    public bool PremadeMinActivated = false; // Готовые пресеты
    public bool PremadeEcoActivated = false; // Готовые пресеты
    public bool PremadeBalanceActivated = false; // Готовые пресеты
    public bool PremadeSpeedActivated = false; // Готовые пресеты
    public bool PremadeMaxActivated = true; // Готовые пресеты
    // Страница управления кулером
    public string NBFCConfigXMLName; // Имя выбранного файла конфига nbfc
    public bool NBFCAutoUpdateInformation = true; // Автоматически обновлять информацию о скоростях кулеров
    public bool NBFCServiceStatusDisabled = true; // Флаги использования nbfc на странице управления кулером
    public bool NBFCServiceStatusReadOnly = false; // Флаги использования nbfc на странице управления кулером
    public bool NBFCServiceStatusEnabled = false; // Флаги использования nbfc на странице управления кулером
    public double NBFCFan1UserFanSpeedRPM = 110.0; // Выставленная пользователем скорость вращения первым кулером, больше 100 - авто 
    public double NBFCFan2UserFanSpeedRPM = 110.0; // Выставленная пользователем скорость вращения вторым кулером, больше 100 - авто 
    public bool NBFCFlagConsoleCheckSpeedRunning = false; // Флаг для страницы управления кулером, сейчас происходит действие с консолью при помощи nbfc
    public bool FlagRyzenADJConsoleTemperatureCheckRunning = false; // Флаг для страницы управления кулером,  сейчас происходит действие с консолью при помощи RyzenADJ получаем температуру
    public string NBFCAnswerSpeedFan1; // Ответ nbfc консоли для вывода информации от первого кулера
    public string NBFCAnswerSpeedFan2; // Ответ nbfc консоли для вывода информации от второго кулера
    public bool CoolerLastSavedAsusMode = false; // Если был выбран режим управления кулерами на ноутбуках Asus - сохранить его и открывать всегда по умолчанию
    public bool AsusModeAutoUpdateInformation = true; // Автоматически обновлять информацию о скоростях кулеров
    public int AsusModeSelectedMode = 0; // Режим управления скоростью кулера: 0 - ручной, 1 - турбо, 2 - баланс, 3 - тихий
    public double AsusModeFan1UserFanSpeedRPM = 110.0; // Выставленная пользователем скорость вращения первым кулером, больше 100 - авто 
}
#pragma warning restore CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.
