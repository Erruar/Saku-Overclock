using Saku_Overclock.Models;
using Saku_Overclock.Services;
using static Saku_Overclock.Services.PresetManagerService;

namespace Saku_Overclock.Contracts.Services;

public interface IApplyerService : IDisposable
{
    /// <summary>
    ///     Применить кастомный пресет
    /// </summary>
    /// <param name="preset">Пресет для применения</param>
    /// <param name="saveInfo">Сохранять информацию о применении (о наличии ошибок)</param>
    /// <param name="onlyDebugFunctions">Применять только дебаг-функции</param>
    /// <returns>Результат выполнения задачи</returns>
    Task ApplyCustomPreset(Preset preset, bool saveInfo = false, bool onlyDebugFunctions = false);

    /// <summary>
    ///     Применить готовый пресет
    /// </summary>
    /// <param name="presetType">Тип пресета (мин, баланс, макс..)</param>
    /// <param name="presetSelected">Выбрать этот пресет как последний применённый</param>
    /// <returns>Результат выполнения задачи</returns>
    Task ApplyPremadePreset(PresetType presetType, bool presetSelected = true);

    /// <summary>
    ///     Применяет ранее применённые настройки, при запуске приложения
    /// </summary>
    /// <returns>Результат выполнения задачи</returns>
    Task AutoApplySettingsWithAppStart();

    /// <summary>
    ///     Применяет следующий кастомный пресет и выдаёт его информацию (используется в горячих клавишах)
    /// </summary>
    /// <returns>Конфигурация следующего кастомного пресета</returns>
    PresetId SwitchCustomPreset();

    /// <summary>
    ///     Применяет следующий готовый пресет и выдаёт его информацию (используется в горячих клавишах)
    /// </summary>
    /// <returns>Конфигурация следующего готового пресета</returns>
    PresetId SwitchPremadePreset();

    /// <summary>
    ///     Возвращает имя применённого пресета (используется в Rtss оверлее)
    /// </summary>
    /// <returns>Имя применённого пресета</returns>
    string GetSelectedPresetName();
}