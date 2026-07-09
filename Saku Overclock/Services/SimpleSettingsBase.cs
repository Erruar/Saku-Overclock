using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Saku_Overclock.Services;

public abstract class SimpleIpcSettingsBase<T>
    : IDisposable where T : new()
{
    private T _cache;
    private readonly Lock _lock = new();
    private CancellationTokenSource? _debounce;

    private readonly IpcConnectionService _ipc;
    private readonly string _entityName;
    private readonly JsonTypeInfo<T> _typeInfo;

    protected SimpleIpcSettingsBase(IpcConnectionService ipc, string entityName, JsonTypeInfo<T> typeInfo, T? initial = default)
    {
        _ipc = ipc;
        _entityName = entityName;
        _typeInfo = typeInfo;
        _cache = initial ?? new T();

        _ipc.OnEvent += OnIpcEvent;
    }

    protected async Task LoadSettingsAsync()
    {
        var json = await _ipc.SendCommandAsync($"Get_{_entityName}");
        if (string.IsNullOrEmpty(json)) return;
        var loaded = JsonSerializer.Deserialize(json, _typeInfo);
        if (loaded != null) lock (_lock) _cache = loaded;
    }

    private void OnIpcEvent(string name, string payload)
    {
        if (name != $"{_entityName}Changed") return;
        var updated = JsonSerializer.Deserialize(payload, _typeInfo);
        if (updated != null) lock (_lock) _cache = updated;
        OnExternalUpdate();
    }

    protected virtual void OnExternalUpdate() { }

    protected TValue Get<TValue>(Func<T, TValue> selector)
    {
        lock (_lock) return selector(_cache);
    }

    protected void Set(Action<T> mutate)
    {
        lock (_lock) mutate(_cache);
        ScheduleSend();
    }

    private void ScheduleSend()
    {
        _debounce?.Cancel();
        _debounce = new CancellationTokenSource();
        var token = _debounce.Token;

        _ = Task.Run(async () =>
        {
            try { await Task.Delay(300, token); }
            catch (TaskCanceledException) { return; }

            T snapshot;
            lock (_lock) snapshot = _cache;
            var json = JsonSerializer.Serialize(snapshot, _typeInfo);
            await _ipc.SendCommandAsync($"Set_{_entityName}", json, token);
        }, token);
    }

    public void Dispose() => _ipc.OnEvent -= OnIpcEvent;
}