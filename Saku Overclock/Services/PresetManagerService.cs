using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Models;
using Saku_Overclock.Views;

namespace Saku_Overclock.Services;

public class PresetManagerService(IFileService fileService, IAppSettingsService appSettings) : IPresetManagerService
{
    private const string FolderPath = "Saku Overclock/Presets";
    private const string FileName = "UserPresets.json";

    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private readonly string _applicationDataFolder = Path.Combine(LocalAppData, FolderPath);
    
    // Состояние для отслеживания позиции при быстром переключении
    private int _virtualCustomPresetIndex = -1; // Виртуальная позиция в кастомных пресетах
    private bool _isVirtualStateActive; // Флаг активности виртуального состояния


    public Preset[] Presets
    {
        get;
        set;
    } = [];

    public void LoadSettings()
    {
        try
        {
            Presets = fileService.Read<Preset[]>(_applicationDataFolder, FileName) ?? [];
        }
        catch
        {
            Presets = [];
            SaveSettings();
        }
    }

    public void SaveSettings()
    {
        fileService.Save(_applicationDataFolder, FileName, Presets);
    }

    public void AddPreset(Preset preset)
    {
        var newPresets = new Preset[Presets.Length + 1];
        Array.Copy(Presets, newPresets, Presets.Length);
        newPresets[Presets.Length] = preset;
        Presets = newPresets;
    }

    public void RemovePreset(int index)
    {
        if (index < 0 || index >= Presets.Length)
        {
            return;
        }

        var newPresets = new Preset[Presets.Length - 1];
        Array.Copy(Presets, 0, newPresets, 0, index);
        Array.Copy(Presets, index + 1, newPresets, index, Presets.Length - index - 1);
        Presets = newPresets;
    }

    public void RemovePresets(int[]? indices)
    {
        if (indices == null || indices.Length == 0)
        {
            return;
        }

        var sortedIndices = indices.OrderByDescending(i => i).ToArray();
        var tempPresets = Presets;

        foreach (var index in sortedIndices)
        {
            if (index >= 0 && index < tempPresets.Length)
            {
                var newPresets = new Preset[tempPresets.Length - 1];
                Array.Copy(tempPresets, 0, newPresets, 0, index);
                Array.Copy(tempPresets, index + 1, newPresets, index, tempPresets.Length - index - 1);
                tempPresets = newPresets;
            }
        }

        Presets = tempPresets;
    }

    public void UpdatePreset(int index, Preset preset)
    {
        if (index < 0 || index >= Presets.Length)
        {
            return;
        }

        Presets[index] = preset;
    }

    public void ExportPreset(int index, string exportFolder, string exportFile)
    {
        if (index < 0 || index >= Presets.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Invalid preset index");
        }

        try
        {
            var preset = Presets[index];
            fileService.Save(exportFolder, exportFile, preset);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to export preset: {ex.Message}", ex);
        }
    }

    public void ExportPresets(int[] indices, string exportFolder, string exportFile)
    {
        if (indices == null || indices.Length == 0)
        {
            throw new ArgumentException("No indices provided", nameof(indices));
        }

        try
        {
            var preset = indices
                .Where(i => i >= 0 && i < Presets.Length)
                .Select(i => Presets[i])
                .ToArray();

            if (preset.Length == 0)
            {
                throw new ArgumentException("No valid indices found");
            }

            fileService.Save(exportFolder, exportFile, preset);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to export presets: {ex.Message}", ex);
        }
    }

    public void ExportAllPresets(string exportFolder, string exportFile)
    {
        try
        {
            fileService.Save(exportFolder, exportFile, Presets);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to export all presets: {ex.Message}", ex);
        }
    }

    public void ImportPresets(string importFolder, string importFile, bool append = false)
    {
        try
        {
            var imported = fileService.Read<Preset[]>(importFolder, importFile);
            if (imported == null || imported.Length == 0)
            {
                throw new InvalidOperationException("No valid presets found in file");
            }

            if (append)
            {
                var combined = new Preset[Presets.Length + imported.Length];
                Array.Copy(Presets, combined, Presets.Length);
                Array.Copy(imported, 0, combined, Presets.Length, imported.Length);
                Presets = combined;
            }
            else
            {
                Presets = imported;
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to import presets: {ex.Message}", ex);
        }
    }

    public struct PresetId
    {
        public string PresetName;
        public string? PresetDesc;
        public string PresetIcon;
        public int PresetIndex;
    }

    /// <summary>
    ///     Метод для получения следующего кастомного пресета, без применения настроек
    /// </summary>
    public PresetId GetNextPreset()
    {
        try
        {
            if (Presets.Length == 0)
            {
                LogHelper.TraceIt_TraceError("No custom presets available");

                return new PresetId
                {
                    PresetName = "Balance",
                    PresetDesc = string.Empty,
                    PresetIcon = "\uE783",
                    PresetIndex = -1
                };
            }

            int nextPresetIndex;

            // Определяем текущую позицию
            if (_isVirtualStateActive && _virtualCustomPresetIndex >= 0)
            {
                nextPresetIndex = (_virtualCustomPresetIndex + 1) % Presets.Length;
            }
            else
            {
                if (appSettings.Preset == -1)
                {
                    // Сейчас активен готовый пресет - начинаем с первого кастомного
                    nextPresetIndex = 0;
                    _virtualCustomPresetIndex = -1; // Чтобы следующий был 0
                }
                else
                {
                    // Уже выбран кастомный пресет
                    nextPresetIndex = (appSettings.Preset + 1) % Presets.Length;
                    _virtualCustomPresetIndex = appSettings.Preset;
                }

                _isVirtualStateActive = true;
            }

            // Обновляем виртуальную позицию
            _virtualCustomPresetIndex = nextPresetIndex;

            // Проверяем корректность индекса и данных пресета
            if (nextPresetIndex >= 0 && nextPresetIndex < Presets.Length &&
                !string.IsNullOrEmpty(Presets[nextPresetIndex].PresetName))
            {
                var preset = Presets[nextPresetIndex];
                var presetName = preset.PresetName; 
                var presetDesc = preset.PresetDesc;
                if (presetName.Contains("Preset_")){ presetName = ГлавнаяPage.TryLocalize(presetName); }
                if (presetDesc.Contains("Preset_")){ presetDesc = ГлавнаяPage.TryLocalize(presetDesc); }
                return new PresetId
                {
                    PresetName = presetName,
                    PresetDesc = presetDesc,
                    PresetIcon = preset.PresetIcon,
                    PresetIndex = nextPresetIndex
                };
            }

            LogHelper.TraceIt_TraceError($"Invalid preset index: {nextPresetIndex}");
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError($"Error getting next custom preset: {ex.Message}");
        }

        return new PresetId
        {
            PresetName = "Balance",
            PresetDesc = string.Empty,
            PresetIcon = "\uE783",
            PresetIndex = -1
        };
    }

    public void ResetPresetStateAfterApply()
    {
        _isVirtualStateActive = false;
        _virtualCustomPresetIndex = -1;
    }
}