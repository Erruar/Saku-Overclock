using Saku_Overclock.JsonContainers;
using Saku_Overclock.Services;
using static Saku_Overclock.Services.PresetManagerService;

namespace Saku_Overclock.Contracts.Services;
public interface IApplyerService : IDisposable
{
    Task ApplySettings(bool saveinfo);
    Task ApplyCustomPreset(Preset preset, bool saveInfo = false, bool onlyDebugFunctions = false);
    Task ApplyPremadePreset(PresetType presetType, bool presetSelected = true);
    Task AutoApplySettingsWithAppStart();
    void ScheduleApplyPreset();
    PresetId SwitchCustomPreset();
    PresetId SwitchPremadePreset();
    string GetSelectedPresetName();
}