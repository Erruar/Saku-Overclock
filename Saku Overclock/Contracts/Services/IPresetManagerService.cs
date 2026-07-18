using Saku_Overclock.Shared.Models;
using PresetId = Saku_Overclock.Shared.Models.PresetId;

namespace Saku_Overclock.Contracts.Services;

public interface IPresetManagerService
{
    /// <summary>
    ///     Preset collection
    /// </summary>
    Preset[] Presets
    {
        get;
        set;
    }

    /// <summary>
    ///     Load user presets
    /// </summary>
    Task LoadSettingsAsync();

    /// <summary>
    ///     Add new preset
    /// </summary>
    Task AddPresetAsync(Preset preset);

    /// <summary>
    ///     Remove preset by index
    /// </summary>
    Task RemovePresetAsync(int index);

    /// <summary>
    ///     Remove presets by index
    /// </summary>
    Task RemovePresetsAsync(int[] indices);

    /// <summary>
    ///     Update preset data
    /// </summary>
    void UpdatePreset(int index, Preset preset);
    
    /// <summary>
    ///     Update preset data
    /// </summary>
    void UpdatePreset(int index);
    
    /// <summary>
    ///     Update preset data
    /// </summary>
    void UpdatePreset();

    /// <summary>
    ///     Export preset by index
    /// </summary>
    Task ExportPresetAsync(int index, string exportFolder, string exportFile);

    /// <summary>
    ///     Export presets by indexes
    /// </summary>
    Task ExportPresetsAsync(int[] indices, string exportFolder, string exportFile);

    /// <summary>
    ///     Export all presets
    /// </summary>
    Task ExportAllPresetsAsync(string exportFolder, string exportFile);

    /// <summary>
    ///     Import presets from file
    /// </summary>
    /// <param name="importFolder">File folder path</param>
    /// <param name="importFile">File path</param>
    /// <param name="append">If true - append, else - replace all presets from file</param>
    Task ImportPresetsAsync(string importFolder, string importFile, bool append = false);

    /// <summary>
    ///     Get next preset (used in hotkeys)
    /// </summary>
    /// <returns>Next preset config</returns>
    Task<PresetId> GetNextPresetAsync();
    
    /// <summary>
    ///     Remove virtual state after hotkeys switching
    /// </summary>
    Task ResetPresetStateAfterApplyAsync();
}