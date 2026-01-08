using Saku_Overclock.Models;
using static Saku_Overclock.Services.PresetManagerService;

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
    void LoadSettings();

    /// <summary>
    ///     Сохранить пресеты
    /// </summary>
    void SaveSettings();

    /// <summary>
    ///     Добавить новый пресет
    /// </summary>
    void AddPreset(Preset preset);

    /// <summary>
    ///     Удалить пресет по индексу
    /// </summary>
    void RemovePreset(int index);

    /// <summary>
    ///     Удалить несколько пресетов по индексам
    /// </summary>
    void RemovePresets(int[]? indices);

    /// <summary>
    ///     Обновить существующий пресет
    /// </summary>
    void UpdatePreset(int index, Preset preset);

    /// <summary>
    ///     Экспортировать один пресет по индексу
    /// </summary>
    void ExportPreset(int index, string exportFolder, string exportFile);

    /// <summary>
    ///     Экспортировать несколько пресетов по индексам
    /// </summary>
    void ExportPresets(int[] indices, string exportFolder, string exportFile);

    /// <summary>
    ///     Экспортировать все пресеты
    /// </summary>
    void ExportAllPresets(string exportFolder, string exportFile);

    /// <summary>
    ///     Импортировать пресеты из файла
    /// </summary>
    /// <param name="importFolder">Путь к папке с файлом</param>
    /// <param name="importFile">Путь к файлу</param>
    /// <param name="append">Если true - добавляет к существующим, если false - заменяет</param>
    void ImportPresets(string importFolder, string importFile, bool append = false);

    void SelectPremadePreset(string nextPreset);

    /// <summary>
    ///     Выдаст информацию о следующем кастомном пресете (используется в горячих клавишах)
    /// </summary>
    /// <returns>Конфигурация следующего кастомного пресета</returns>
    PresetId GetNextCustomPreset();

    /// <summary>
    ///     Выдаст информацию о следующем готовом пресете (используется в горячих клавишах)
    /// </summary>
    /// <returns>Конфигурация следующего готового пресета</returns>
    PresetId GetNextPremadePreset();

    /// <summary>
    ///     Удалить виртуальное состояние применённого пресета после применения горячими клавишами
    /// </summary>
    void ResetPresetStateAfterApply();
}