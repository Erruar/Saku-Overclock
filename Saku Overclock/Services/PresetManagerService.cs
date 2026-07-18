using System.Collections.Concurrent;
using System.Text.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Shared;
using Saku_Overclock.Shared.Ipc;
using Saku_Overclock.Shared.Models;

namespace Saku_Overclock.Services;

public class PresetManagerService : IPresetManagerService, IDisposable
{
    private readonly IpcConnectionService _ipc;
    private Preset[] _cache = [];
    private readonly Lock _lock = new();
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _pendingSaves = new();

    public event Action? PresetsUpdated; // Event for UI

    public PresetManagerService(IpcConnectionService ipc)
    {
        _ipc = ipc;
        _ipc.OnEvent += OnIpcEvent;
    }

    public Preset[] Presets
    {
        get { lock (_lock) return _cache; }
        set { lock (_lock) _cache = value; }
    }

    public async Task LoadSettingsAsync()
    {
        var json = await _ipc.SendCommandAsync("Get_Presets");
        if (string.IsNullOrEmpty(json)) return;
        var loaded = JsonSerializer.Deserialize(json, IpcJsonContext.Default.PresetArray);
        if (loaded != null) { lock (_lock) _cache = loaded; }
        PresetsUpdated?.Invoke();
    }

    private void OnIpcEvent(string name, string payload)
    {
        switch (name)
        {
            case "PresetsChanged":
                var full = JsonSerializer.Deserialize(payload, IpcJsonContext.Default.PresetArray);
                if (full != null) lock (_lock) _cache = full;
                PresetsUpdated?.Invoke();
                break;

            case "PresetChanged":
                var msg = JsonSerializer.Deserialize(payload, IpcJsonContext.Default.PresetUpdateMessage);
                if (msg != null)
                {
                    lock (_lock)
                    {
                        if (msg.Index >= 0 && msg.Index < _cache.Length)
                            _cache[msg.Index] = msg.Preset;
                    }
                    PresetsUpdated?.Invoke();
                }
                break;
        }
    }


    public void UpdatePreset(int index, Preset preset)
    {
        lock (_lock)
        {
            if (index < 0 || index >= _cache.Length) return;
            _cache[index] = preset;
        }
        PresetsUpdated?.Invoke();
        ScheduleSend(index, preset);
    }

    private void ScheduleSend(int index, Preset preset)
    {
        if (_pendingSaves.TryRemove(index, out var old)) old.Cancel();

        var cts = new CancellationTokenSource();
        _pendingSaves[index] = cts;
        var token = cts.Token;

        _ = Task.Run(async () =>
        {
            try { await Task.Delay(250, token); }
            catch (TaskCanceledException) { return; }

            var msg = new PresetUpdateMessage { Index = index, Preset = preset };
            var json = JsonSerializer.Serialize(msg, IpcJsonContext.Default.PresetUpdateMessage);
            await _ipc.SendCommandAsync("Set_Preset", json, token);

            _pendingSaves.TryRemove(index, out _);
        }, token);
    }

    public async Task AddPresetAsync(Preset preset)
    {
        var json = JsonSerializer.Serialize(preset, IpcJsonContext.Default.Preset);
        var res = await _ipc.SendCommandAsync("Add_Preset", json);
        ApplyFullResponse(res);
    }

    public async Task RemovePresetAsync(int index)
    {
        var json = JsonSerializer.Serialize(index, IpcJsonContext.Default.Int32);
        var res = await _ipc.SendCommandAsync("Remove_Preset", json);
        ApplyFullResponse(res);
    }

    public async Task RemovePresetsAsync(int[] indices)
    {
        var json = JsonSerializer.Serialize(indices, IpcJsonContext.Default.Int32Array);
        var res = await _ipc.SendCommandAsync("Remove_Presets", json);
        ApplyFullResponse(res);
    }

    public async Task ImportPresetsAsync(string folder, string file, bool append = false)
    {
        var req = new ImportPresetsRequest { Folder = folder, File = file, Append = append };
        var json = JsonSerializer.Serialize(req, IpcJsonContext.Default.ImportPresetsRequest);
        var res = await _ipc.SendCommandAsync("Import_Presets", json);
        if (string.IsNullOrEmpty(res)) throw new InvalidOperationException("Failed to import presets");
        ApplyFullResponse(res);
    }

    public async Task ExportPresetAsync(int index, string folder, string file)
    {
        var req = new ExportPresetRequest { Index = index, Folder = folder, File = file };
        var json = JsonSerializer.Serialize(req, IpcJsonContext.Default.ExportPresetRequest);
        var res = await _ipc.SendCommandAsync("Export_Preset", json);
        if (res is null) throw new InvalidOperationException("Failed to export preset");
    }
    
    public async Task ExportPresetsAsync(int[] indices, string folder, string file)
    {
        var req = new ExportPresetsRequest { Indices = indices, Folder = folder, File = file };
        var json = JsonSerializer.Serialize(req, IpcJsonContext.Default.ExportPresetsRequest);
        var res = await _ipc.SendCommandAsync("Export_Presets", json);
        if (res is null) throw new InvalidOperationException("Failed to export preset");
    }

    public async Task ExportAllPresetsAsync(string folder, string file)
    {
        var req = new ExportAllPresetsRequest { Folder = folder, File = file };
        var json = JsonSerializer.Serialize(req, IpcJsonContext.Default.ExportAllPresetsRequest);
        await _ipc.SendCommandAsync("Export_All_Presets", json);
    }

    public async Task<PresetId> GetNextPresetAsync()
    {
        var json = await _ipc.SendCommandAsync("Get_Next_Preset");
        var id = JsonSerializer.Deserialize(json, IpcJsonContext.Default.PresetId);
        return id;
    }

    public Task ResetPresetStateAfterApplyAsync() => _ipc.SendCommandAsync("Reset_Preset_State");

    private void ApplyFullResponse(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        var full = JsonSerializer.Deserialize(json, IpcJsonContext.Default.PresetArray);
        if (full != null) { lock (_lock) _cache = full; }
        PresetsUpdated?.Invoke();
    }

    public void Dispose() => _ipc.OnEvent -= OnIpcEvent;
}