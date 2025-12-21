using Saku_Overclock.JsonContainers;
using static Saku_Overclock.Services.PresetManagerService;

namespace Saku_Overclock.Contracts.Services;

public interface IPresetManagerService
{
    /// <summary>
    /// Массив пресетов. Прямой доступ для чтения и изменения.
    /// </summary>
    Preset[] Presets
    {
        get; set;
    }

    /// <summary>
    /// Загружает пресеты из файла
    /// </summary>
    void LoadSettings();

    /// <summary>
    /// Сохраняет пресеты в файл
    /// </summary>
    void SaveSettings();

    /// <summary>
    /// Добавляет новый пресет
    /// </summary>
    void AddPreset(Preset preset);

    /// <summary>
    /// Удаляет пресет по индексу
    /// </summary>
    void RemovePreset(int index);

    /// <summary>
    /// Удаляет несколько пресетов по индексам
    /// </summary>
    void RemovePresets(int[]? indices);

    /// <summary>
    /// Обновляет существующий пресет
    /// </summary>
    void UpdatePreset(int index, Preset preset);

    /// <summary>
    /// Экспортирует один пресет по индексу
    /// </summary>
    void ExportPreset(int index, string exportFolder, string exportFile);

    /// <summary>
    /// Экспортирует несколько пресетов по индексам
    /// </summary>
    void ExportPresets(int[] indices, string exportFolder, string exportFile);

    /// <summary>
    /// Экспортирует все пресеты
    /// </summary>
    void ExportAllPresets(string exportFolder, string exportFile);

    /// <summary>
    /// Импортирует пресеты из файла
    /// </summary>
    /// <param name="importPath">Путь к файлу</param>
    /// <param name="append">Если true - добавляет к существующим, если false - заменяет</param>
    void ImportPresets(string importFolder, string importFile, bool append = false);

    void SelectPremadePreset(string nextPreset);

    PresetId GetNextCustomPreset();

    PresetId GetNextPremadePreset();

    void ResetPresetStateAfterApply();
}