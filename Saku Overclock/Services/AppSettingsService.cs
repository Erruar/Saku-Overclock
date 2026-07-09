using System.Text.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Shared;
using Saku_Overclock.Shared.Models;

namespace Saku_Overclock.Services;

public partial class AppSettingsService : IAppSettingsService, IDisposable
{
    private readonly IpcConnectionService _ipc;
    private AppSettings _cache = new();
    private readonly Lock _lock = new();
    private CancellationTokenSource? _saveDebounce;

    public AppSettingsService(IpcConnectionService ipc)
    {
        _ipc = ipc;
        _ipc.OnEvent += OnIpcEvent;
    }

    public async Task LoadSettingsAsync()
    {
        var json = await _ipc.SendCommandAsync("Get_AppSettings");
        if (string.IsNullOrEmpty(json)) return;

        var loaded = JsonSerializer.Deserialize(json, IpcJsonContext.Default.AppSettings);
        if (loaded != null) lock (_lock) _cache = loaded;
    }

    private void OnIpcEvent(string name, string payload)
    {
        if (name != "AppSettingsChanged") return;
        var updated = JsonSerializer.Deserialize(payload, IpcJsonContext.Default.AppSettings);
        if (updated != null) lock (_lock) _cache = updated;
        // тут же можно дёрнуть событие для UI (PropertyChanged/WeakReferenceMessenger), если правки пришли не от нас
    }

    // паттерн для каждого свойства: читаем из кэша сразу, пишем в кэш сразу (отзывчивый UI) + debounce-сохранение
    public bool FixedTitleBar
    {
        get { lock (_lock) return _cache.FixedTitleBar; } 
        set { lock (_lock) _cache.FixedTitleBar = value; ScheduleSave(); }
    }

    public int AutostartType
    {
        get { lock (_lock) return _cache.AutostartType; } 
        set { lock (_lock) _cache.AutostartType = value; ScheduleSave(); }
    }

    public bool HideToTray
    {
        get { lock (_lock) return _cache.HideToTray; }
        set { lock (_lock) _cache.HideToTray = value; ScheduleSave(); }
    }

    public bool CheckForUpdates
    {
        get { lock (_lock) return _cache.CheckForUpdates; }
        set { lock (_lock) _cache.CheckForUpdates = value; ScheduleSave(); }
    }

    public bool HotkeysEnabled
    {
        get { lock (_lock) return _cache.HotkeysEnabled; }
        set { lock (_lock) _cache.HotkeysEnabled = value; ScheduleSave(); }
    }

    public bool ReapplyLatestSettingsOnAppLaunch
    {
        get { lock (_lock) return _cache.ReapplyLatestSettingsOnAppLaunch; }
        set { lock (_lock) _cache.ReapplyLatestSettingsOnAppLaunch = value; ScheduleSave(); }
    }

    public bool ReapplyOverclock
    {
        get { lock (_lock) return _cache.ReapplyOverclock; }
        set { lock (_lock) _cache.ReapplyOverclock = value; ScheduleSave(); }
    }

    public double ReapplyOverclockTimer
    {
        get { lock (_lock) return _cache.ReapplyOverclockTimer; }
        set { lock (_lock) _cache.ReapplyOverclockTimer = value; ScheduleSave(); }
    }

    public int ThemeType
    {
        get { lock (_lock) return _cache.ThemeType; }
        set { lock (_lock) _cache.ThemeType = value; ScheduleSave(); }
    }

    public bool NiIconsEnabled
    {
        get { lock (_lock) return _cache.NiIconsEnabled; }
        set { lock (_lock) _cache.NiIconsEnabled = value; ScheduleSave(); }
    }

    public bool RtssMetricsEnabled
    {
        get { lock (_lock) return _cache.RtssMetricsEnabled; }
        set { lock (_lock) _cache.RtssMetricsEnabled = value; ScheduleSave(); }
    }

    public int NiIconsType
    {
        get { lock (_lock) return _cache.NiIconsType; }
        set { lock (_lock) _cache.NiIconsType = value; ScheduleSave(); }
    }

    public bool PresetsPageViewModeBeginner
    {
        get { lock (_lock) return _cache.PresetsPageViewModeBeginner; }
        set { lock (_lock) _cache.PresetsPageViewModeBeginner = value; ScheduleSave(); }
    }

    public int Preset
    {
        get { lock (_lock) return _cache.Preset; }
        set { lock (_lock) _cache.Preset = value; ScheduleSave(); }
    }

    public bool PremadePresetsAdded
    {
        get { lock (_lock) return _cache.PremadePresetsAdded; }
        set { lock (_lock) _cache.PremadePresetsAdded = value; ScheduleSave(); }
    }

    public string AcPreset
    {
        get { lock (_lock) return _cache.AcPreset; }
        set { lock (_lock) _cache.AcPreset = value; ScheduleSave(); }
    }

    public string BatteryPreset
    {
        get { lock (_lock) return _cache.BatteryPreset; }
        set { lock (_lock) _cache.BatteryPreset = value; ScheduleSave(); }
    }

    public string RyzenAdjLine
    {
        get { lock (_lock) return _cache.RyzenAdjLine; }
        set { lock (_lock) _cache.RyzenAdjLine = value; ScheduleSave(); }
    }

    public bool AppFirstRun
    {
        get { lock (_lock) return _cache.AppFirstRun; }
        set { lock (_lock) _cache.AppFirstRun = value; ScheduleSave(); }
    }
    // ... остальные свойства аналогично

    private void ScheduleSave()
    {
        _saveDebounce?.Cancel();
        _saveDebounce = new CancellationTokenSource();
        var token = _saveDebounce.Token;

        _ = Task.Run(async () =>
        {
            try { await Task.Delay(300, token); }
            catch (TaskCanceledException) { return; }

            AppSettings snapshot;
            lock (_lock) snapshot = _cache;

            var json = JsonSerializer.Serialize(snapshot, IpcJsonContext.Default.AppSettings);
            await _ipc.SendCommandAsync("Set_AppSettings", json, token);
        }, token);
    }

    public void Dispose() => _ipc.OnEvent -= OnIpcEvent;
}