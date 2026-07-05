using Saku_Overclock.Models;
using Saku_Overclock.Shared.Models;
using static Saku_Overclock.Services.PresetManagerService;
using PresetId = Saku_Overclock.Shared.Models.PresetId;

namespace Saku_Overclock.Contracts.Services;

public interface IPresetManagerService
{
    /// <summary>
    ///     Коллекция пресетов
    /// </summary>
    Preset[] Presets
    {
        get;
        set;
    }

    /// <summary>
    ///     Загрузить пресеты
    /// </summary>
    Task LoadSettingsAsync();

    /// <summary>
    ///     Добавить новый пресет
    /// </summary>
    Task AddPresetAsync(Preset preset);

    /// <summary>
    ///     Удалить пресет по индексу
    /// </summary>
    Task RemovePresetAsync(int index);

    /// <summary>
    ///     Удалить несколько пресетов по индексам
    /// </summary>
    Task RemovePresetsAsync(int[] indices);

    /// <summary>
    ///     Обновить существующий пресет
    /// </summary>
    void UpdatePreset(int index, Preset preset);

    /// <summary>
    ///     Экспортировать один пресет по индексу
    /// </summary>
    Task ExportPresetAsync(int index, string exportFolder, string exportFile);

    /// <summary>
    ///     Экспортировать несколько пресетов по индексам
    /// </summary>
    Task ExportPresetsAsync(int[] indices, string exportFolder, string exportFile);

    /// <summary>
    ///     Экспортировать все пресеты
    /// </summary>
    Task ExportAllPresetsAsync(string exportFolder, string exportFile);

    /// <summary>
    ///     Импортировать пресеты из файла
    /// </summary>
    /// <param name="importFolder">Путь к папке с файлом</param>
    /// <param name="importFile">Путь к файлу</param>
    /// <param name="append">Если true - добавляет к существующим, если false - заменяет</param>
    Task ImportPresetsAsync(string importFolder, string importFile, bool append = false);

    /// <summary>
    ///     Выдаст информацию о следующем кастомном пресете (используется в горячих клавишах)
    /// </summary>
    /// <returns>Конфигурация следующего кастомного пресета</returns>
    Task<PresetId> GetNextPresetAsync();
    
    /// <summary>
    ///     Удалить виртуальное состояние применённого пресета после применения горячими клавишами
    /// </summary>
    Task ResetPresetStateAfterApplyAsync();
}