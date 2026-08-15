using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Saku_Overclock.Core.Services;
using Saku_Overclock.Shared;
using Saku_Overclock.Shared.Ipc;

namespace Saku_Overclock.Service;

// to fix roots trimming with Native AOT
// ReSharper disable once PartialTypeWithSinglePart
public sealed partial class IpcNamedPipeWorker(
    IIpcSecurityService securityService,
    IpcHub hub,
    ILogger<IpcNamedPipeWorker> logger) : BackgroundService
{
    private const string PipeName = "SakuOverclockIpcPipe";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pipeSecurity = BuildPipeSecurity();

        while (!stoppingToken.IsCancellationRequested)
        {
            var pipe = NamedPipeServerStreamAcl.Create(
                PipeName, PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous,
                0, 0, pipeSecurity);

            try
            {
                await pipe.WaitForConnectionAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }

            // каждое соединение обрабатываем в своей задаче, не блокируя цикл accept
            _ = HandleClientAsync(pipe, stoppingToken);
        }
    }

    private static PipeSecurity BuildPipeSecurity()
    {
        var pipeSecurity = new PipeSecurity();
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
            AccessControlType.Allow));
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        return pipeSecurity;
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var conn = new PipeConnection(pipe);

        if (!securityService.ValidateClientSignature(pipe))
        {
            logger.LogWarning("Access Denied: Unauthorized client process.");
            await pipe.DisposeAsync();
            return;
        }
        
        hub.AddClient(id, conn);
        try
        {
            while (!ct.IsCancellationRequested && pipe.IsConnected)
            {
                var line = await conn.Reader.ReadLineAsync(ct);
                if (string.IsNullOrEmpty(line)) break;

                var msg = JsonSerializer.Deserialize(line, IpcJsonContext.Default.IpcMessage);
                if (msg is null || msg.Kind != IpcMessageKind.Command) continue;

                var response = await hub.DispatchCommandAsync(msg, ct);
                var json = JsonSerializer.Serialize(response, IpcJsonContext.Default.IpcMessage);
                await conn.WriteLineAsync(json, ct);
            }
        }
        catch (Exception ex) { logger.LogError(ex, "Client {Id} pipe error", id); }
        finally
        {
            hub.RemoveClient(id);
            await pipe.DisposeAsync();
        }
    }
}