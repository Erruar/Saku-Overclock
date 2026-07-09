using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Shared;

namespace Saku_Overclock.Services;

public class PowerMonSettingsService(IpcConnectionService ipc)
    : SimpleIpcSettingsBase<List<string>>(ipc, "PowerMon", IpcJsonContext.Default.ListString, [])
        , IPowerMonSettingsService
{
    public List<string> Notelist
    {
        get => Get(s => s.ToList());
        set => Set(cache => { cache.Clear(); cache.AddRange(value); });
    }

    public void LoadSettings() => _ = LoadSettingsAsync();
}