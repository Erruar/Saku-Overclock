using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Saku_Overclock.Services;

public abstract class SimpleIpcSettingsBase<T>(IpcConnectionService ipc, string entityName, JsonTypeInfo<T> typeInfo)
    : IDisposable where T : new()
{
    protected T Cache;
    private readonly Lock _lock = new();
    private CancellationTokenSource? _debounce;

    protected SimpleIpcSettingsBase(IpcConnectionService ipc, string entityName, JsonTypeInfo<T> typeInfo, T initial)
        : this(ipc, entityName, typeInfo) => Cache = initial;

    private void Ctor() => ipc.OnEvent += OnIpcEvent;

    public async Task LoadSettingsAsync()
    {
        var json = await ipc.SendCommandAsync($"Get_{entityName}");
        if (string.IsNullOrEmpty(json)) return;
        var loaded = JsonSerializer.Deserialize(json, typeInfo);
        if (loaded != null) lock (_lock) Cache = loaded;
    }

    private void OnIpcEvent(string name, string payload)
    {
        if (name != $"{entityName}Changed") return;
        var updated = JsonSerializer.Deserialize(payload, typeInfo);
        if (updated != null) lock (_lock) Cache = updated;
        OnExternalUpdate();
    }

    protected virtual void OnExternalUpdate() { }

    protected TValue Get<TValue>(Func<T, TValue> selector)
    {
        lock (_lock) return selector(Cache);
    }

    protected void Set(Action<T> mutate)
    {
        lock (_lock) mutate(Cache);
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
            lock (_lock) snapshot = Cache;
            var json = JsonSerializer.Serialize(snapshot, typeInfo);
            await ipc.SendCommandAsync($"Set_{entityName}", json);
        }, token);
    }

    public void Dispose() => ipc.OnEvent -= OnIpcEvent;
}