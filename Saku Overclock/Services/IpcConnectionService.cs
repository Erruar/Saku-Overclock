using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text.Json;
using Saku_Overclock.Helpers;
using Saku_Overclock.Shared;
using Saku_Overclock.Shared.Ipc;

namespace Saku_Overclock.Services;

public sealed class IpcConnectionService : IDisposable
{
    private NamedPipeClientStream? _pipe;
    private StreamWriter? _writer;
    private StreamReader? _reader;
    private readonly Lock _lock = new();
    private bool _serviceUnavailable;
    private static DateTime _nextRetryTime = DateTime.MinValue;

    private readonly ConcurrentDictionary<string, TaskCompletionSource<IpcMessage>> _pending = new();
    private CancellationTokenSource? _readLoopCts;

    public event Action<string, string>? OnEvent; // (eventName, payload)
    public bool IsServiceUnavailable => _serviceUnavailable;

    private bool EnsureConnection()
    {
        lock (_lock)
        {
            if (_pipe is { IsConnected: true }) return true;
            if (_serviceUnavailable && DateTime.UtcNow < _nextRetryTime) return false;

            ResetConnection();
            try
            {
                _pipe = new NamedPipeClientStream(".", "SakuOverclockIpcPipe", PipeDirection.InOut, PipeOptions.Asynchronous);
                _pipe.Connect(1000);
                _writer = new StreamWriter(_pipe) { AutoFlush = true };
                _reader = new StreamReader(_pipe);
                _serviceUnavailable = false;

                _readLoopCts = new CancellationTokenSource();
                _ = ReadLoopAsync(_readLoopCts.Token);
                return true;
            }
            catch
            {
                ResetConnection();
                if (!_serviceUnavailable)
                {
                    LogHelper.TraceIt_TraceError("Saku Overclock Service is not running or unavailable!");
                    _serviceUnavailable = true;
                }
                _nextRetryTime = DateTime.UtcNow.AddSeconds(10);
                return false;
            }
        }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await _reader!.ReadLineAsync(ct);
                if (string.IsNullOrEmpty(line)) break;

                var msg = JsonSerializer.Deserialize(line, IpcJsonContext.Default.IpcMessage);
                if (msg is null) continue;

                switch (msg.Kind)
                {
                    case IpcMessageKind.Response when _pending.TryRemove(msg.Id, out var tcs):
                        tcs.TrySetResult(msg);
                        break;
                    case IpcMessageKind.Event:
                        OnEvent?.Invoke(msg.Name, msg.Payload);
                        break;
                }
            }
        }
        catch { /* Connection closed */ }
        finally { ResetConnection(); }
    }

    public async Task<string> SendCommandAsync(string name, string payload = "", CancellationToken ct = default)
    {
        if (!EnsureConnection()) return string.Empty;

        var id = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<IpcMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        var cmd = new IpcMessage { Kind = IpcMessageKind.Command, Id = id, Name = name, Payload = payload };
        var json = JsonSerializer.Serialize(cmd, IpcJsonContext.Default.IpcMessage);

        try
        {
            lock (_lock) _writer!.WriteLine(json);
        }
        catch
        {
            _pending.TryRemove(id, out _);
            ResetConnection();
            return string.Empty;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            var response = await tcs.Task.WaitAsync(cts.Token);
            return response.IsSuccess ? response.Payload : string.Empty;
        }
        catch (OperationCanceledException)
        {
            _pending.TryRemove(id, out _);
            return string.Empty;
        }
    }

    private void ResetConnection()
    {
        _readLoopCts?.Cancel();
        try { _writer?.Dispose(); } catch { /* ignore */ }
        try { _reader?.Dispose(); } catch { /* ignore */ }
        try { _pipe?.Dispose(); } catch { /* ignore */ }
        _writer = null; _reader = null; _pipe = null;
    }

    public void Dispose() => ResetConnection();
}