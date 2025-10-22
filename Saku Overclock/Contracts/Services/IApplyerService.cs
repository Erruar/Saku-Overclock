using Saku_Overclock.JsonContainers;
using Saku_Overclock.Services;

namespace Saku_Overclock.Contracts.Services;
public interface IApplyerService
{
    Task ApplyWithoutAdjLine(bool saveinfo);
    Task Apply(string ryzenAdJline, bool saveinfo, bool reapplyOverclock, double reapplyOverclockTimer);
    Task ApplyCustomPreset(Profile profile, bool saveInfo = false);
    Task ApplyPremadePreset(PresetType presetType, OptimizationLevel optimizationLevel);
}