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
    /// <returns>Результат выполнения задачи</returns>
    Task ApplyPreset(Preset preset, bool saveInfo = false);

    /// <summary>
    ///     Применяет ранее применённые настройки, при запуске приложения
    /// </summary>
    /// <returns>Результат выполнения задачи</returns>
    Task RestoreAppliedSettings();

    /// <summary>
    ///     Применяет следующий кастомный пресет и выдаёт его информацию (используется в горячих клавишах)
    /// </summary>
    /// <returns>Конфигурация следующего кастомного пресета</returns>
    PresetId SwitchNextPreset();

    /// <summary>
    ///     Возвращает имя применённого пресета (используется в Rtss оверлее)
    /// </summary>
    /// <returns>Имя применённого пресета</returns>
    string GetSelectedPresetName();
}