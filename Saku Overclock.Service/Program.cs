using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Saku_Overclock.Core.Contracts;
using Saku_Overclock.Core.Services;
using Saku_Overclock.Shared;

namespace Saku_Overclock.Service;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // AddWindowsService делает две вещи:
        // - запущен SCM → ведёт себя как настоящий Windows Service (без окна, обрабатывает Start/Stop)
        // - запущен интерактивно → работает как консоль (удобно для отладки)
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "SakuOverclockService";
        });

        builder.Services.AddSingleton<ICpuService, CpuService>();
        builder.Services.AddHostedService<IpcNamedPipeWorker>();

        await builder.Build().RunAsync();
    }
}

// to fix roots trimming with Native AOT
// ReSharper disable once PartialTypeWithSinglePart
public sealed partial class IpcNamedPipeWorker(
    ICpuService cpuService,
    ILogger<IpcNamedPipeWorker> logger) : BackgroundService
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNamedPipeClientProcessId(IntPtr pipeHandle, out uint clientProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    // Вызываем именно Unicode (W) версию. Вместо StringBuilder передаем чистый IntPtr на буфер.
    [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(
        IntPtr hProcess, 
        uint dwFlags, 
        IntPtr lpExeName, 
        ref uint lpdwSize);

    private const uint ProcessQueryLimitedInformation = 0x1000;

    private const string PipeName = "SakuOverclockIpcPipe";

    private bool ValidateClientSignature(NamedPipeServerStream pipe)
    {
        if (pipe.SafePipeHandle.IsInvalid) return false;

        // 1. Получаем PID клиента
        if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle.DangerousGetHandle(), out var pid))
        {
            logger.LogWarning("Failed to get client PID.");
            return false;
        }

        var processHandle = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (processHandle == IntPtr.Zero) return false;

        string? clientPath = null;
        const int bufferSize = 2048;
        var bufferPtr = Marshal.AllocHGlobal(bufferSize * sizeof(char));

        try
        {
            uint size = bufferSize;
            if (QueryFullProcessImageName(processHandle, 0, bufferPtr, ref size))
            {
                clientPath = Marshal.PtrToStringUni(bufferPtr, (int)size);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to query client process path.");
        }
        finally
        {
            Marshal.FreeHGlobal(bufferPtr);
            CloseHandle(processHandle);
        }

        if (string.IsNullOrEmpty(clientPath) || !File.Exists(clientPath))
        {
            logger.LogWarning("Client executable path is invalid.");
            return false;
        }

        // 2. Извлекаем и проверяем цифровую подпись бинарника (Полностью Managed & AOT-safe)
        try
        {
            // Берем сертификат текущего запущенного сервиса
            var currentServicePath = Environment.ProcessPath ?? throw new InvalidOperationException();
            
            using var clientCert = X509CertificateLoader.LoadCertificateFromFile(clientPath);
            using var serviceCert = X509CertificateLoader.LoadCertificateFromFile(currentServicePath);
            var serviceCertHash = serviceCert.GetCertHashString();
            var clientCertHash = clientCert.GetCertHashString();

            // Сверяем хэши сертификатов (Thumbprint)
            if (serviceCertHash == clientCertHash)
            {
                logger.LogInformation("Client signature verified successfully. Process: {Path}", clientPath);
                return true;
            }

            logger.LogWarning("Signature mismatch! Client thumbprint doesn't match service certificate.");
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            // Если у клиента (или у самого сервиса) вообще нет цифровой подписи
            logger.LogWarning("Access Denied: One of the binaries is unsigned. Client path: {Path}", clientPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while checking digital signatures.");
        }

        return false;
    }

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("IPC pipe worker started ({Pipe})", PipeName);

        var pipeSecurity = new PipeSecurity();
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
            AccessControlType.Allow));

        pipeSecurity.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = NamedPipeServerStreamAcl.Create(
                    PipeName, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous,
                    0, 0, pipeSecurity);

                await pipe.WaitForConnectionAsync(stoppingToken);

                // Теперь проверку делает сам обработчик, когда данные начнут поступать
                await HandleClientAsync(pipe, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Pipe error, restarting in 500ms");
                try
                {
                    await Task.Delay(500, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        logger.LogInformation("IPC pipe worker stopped.");
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        using var reader = new StreamReader(pipe, leaveOpen: true);
        await using var writer = new StreamWriter(pipe, leaveOpen: true);
        writer.AutoFlush = true;

        var isClientVerified = false;

        while (!ct.IsCancellationRequested && pipe.IsConnected)
        {
            string? rawRequest;
            try
            {
                rawRequest = await reader.ReadLineAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (string.IsNullOrEmpty(rawRequest)) break;

            // Проверка клиента
            if (!isClientVerified)
            {
                var isValid = false;
                try
                {
                    pipe.RunAsClient(() =>
                    {
                        using var identity = WindowsIdentity.GetCurrent();
                        // Проверяем, что это локальный пользователь
                        if (identity is { IsAuthenticated: true, ImpersonationLevel: TokenImpersonationLevel.Impersonation or TokenImpersonationLevel.Delegation })
                        {
                            logger.LogInformation("Client verified: {Name}", identity.Name);
                            isValid = true;
                        }
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Security validation failed.");
                }
                
                // Дополнительная проверка соответствия подписи файла
                if (isValid)
                {
                    isValid = ValidateClientSignature(pipe);
                }

                if (!isValid)
                {
                    logger.LogWarning("Access Denied: Unauthorized client process.");
                    break; // Выходим из цикла, закрывая соединение
                }

                isClientVerified = true;
            }

            var request = JsonSerializer.Deserialize(rawRequest, IpcJsonContext.Default.IpcRequest);
            if (request is null) continue;

            var response = new IpcResponse { IsSuccess = true };
            try
            {
                ProcessCommand(request, response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in command '{Cmd}'", request.Command);
                response.IsSuccess = false;
                response.Message = $"Internal Service Error: {ex.Message}";
            }

            var rawResponse = JsonSerializer.Serialize(response, IpcJsonContext.Default.IpcResponse);
            try
            {
                await writer.WriteLineAsync(rawResponse.AsMemory(), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void ProcessCommand(IpcRequest request, IpcResponse response)
    {
        switch (request.Command)
        {
            case "Get_IsAvailable":
                response.Message = JsonSerializer.Serialize(cpuService.IsAvailable,
                    IpcJsonContext.Default.Boolean); break;
            case "Get_IsRaven":
                response.Message = JsonSerializer.Serialize(cpuService.IsRaven,
                    IpcJsonContext.Default.Boolean); break;
            case "Get_IsDragonRange":
                response.Message = JsonSerializer.Serialize(cpuService.IsDragonRange,
                    IpcJsonContext.Default.Boolean); break;
            case "Get_PhysicalCores":
                response.Message = JsonSerializer.Serialize(cpuService.PhysicalCores,
                    IpcJsonContext.Default.UInt32); break;
            case "Get_Cores":
                response.Message =
                    JsonSerializer.Serialize(cpuService.Cores, IpcJsonContext.Default.UInt32); break;
            case "Get_CpuName": response.Message = cpuService.CpuName; break;
            case "Get_Smt":
                response.Message =
                    JsonSerializer.Serialize(cpuService.Smt, IpcJsonContext.Default.Boolean); break;
            case "Get_CpuCodeName": response.Message = cpuService.CpuCodeName; break;
            case "Get_SmuVersion": response.Message = cpuService.SmuVersion; break;
            case "Get_PowerTableVersion":
                response.Message = JsonSerializer.Serialize(cpuService.PowerTableVersion,
                    IpcJsonContext.Default.UInt32); break;
            case "Get_SocMemoryClock":
                response.Message = JsonSerializer.Serialize(cpuService.SocMemoryClock,
                    IpcJsonContext.Default.Single); break;
            case "Get_SocFabricClock":
                response.Message = JsonSerializer.Serialize(cpuService.SocFabricClock,
                    IpcJsonContext.Default.Single); break;
            case "Get_SocVoltage":
                response.Message = JsonSerializer.Serialize(cpuService.SocVoltage,
                    IpcJsonContext.Default.Single); break;
            case "Get_CoreDisableMap":
                response.Message = JsonSerializer.Serialize(cpuService.CoreDisableMap,
                    IpcJsonContext.Default.UInt32Array); break;
            case "Get_PowerTable":
                response.Message = JsonSerializer.Serialize(cpuService.PowerTable,
                    IpcJsonContext.Default.SingleArray); break;
            case "Get_Rsmu":
                response.Message = JsonSerializer.Serialize(cpuService.Rsmu,
                    IpcJsonContext.Default.SmuAddressSet); break;
            case "Get_Mp1":
                response.Message = JsonSerializer.Serialize(cpuService.Mp1,
                    IpcJsonContext.Default.SmuAddressSet); break;
            case "Get_Hsmp":
                response.Message = JsonSerializer.Serialize(cpuService.Hsmp,
                    IpcJsonContext.Default.SmuAddressSet); break;
            case "Get_MotherBoardInfo":
                response.Message = JsonSerializer.Serialize(cpuService.MotherBoardInfo,
                    IpcJsonContext.Default.CommonMotherBoardInfo); break;
            case "Get_MemoryConfig":
                response.Message = JsonSerializer.Serialize(cpuService.GetMemoryConfig(),
                    IpcJsonContext.Default.MemoryConfig); break;
            case "Get_Family": response.Message = ((byte)cpuService.Family).ToString(); break;
            case "Get_Avx512AvailableByCodename":
                response.Message = JsonSerializer.Serialize(cpuService.Avx512AvailableByCodename,
                    IpcJsonContext.Default.Boolean); break;

            case "Get_SmuCoperCommandMp1":
                response.Message = JsonSerializer.Serialize(cpuService.SmuCoperCommandMp1,
                    IpcJsonContext.Default.UInt32); break;
            case "Set_SmuCoperCommandMp1":
                cpuService.SmuCoperCommandMp1 =
                    JsonSerializer.Deserialize(request.Payload, IpcJsonContext.Default.UInt32); break;
            case "Get_SmuCoperCommandRsmu":
                response.Message = JsonSerializer.Serialize(cpuService.SmuCoperCommandRsmu,
                    IpcJsonContext.Default.UInt32); break;
            case "Set_SmuCoperCommandRsmu":
                cpuService.SmuCoperCommandRsmu =
                    JsonSerializer.Deserialize(request.Payload, IpcJsonContext.Default.UInt32); break;

            case "RefreshPowerTable": cpuService.RefreshPowerTable(); break;
            case "GetCpuTemperature":
                response.Message = JsonSerializer.Serialize(cpuService.GetCpuTemperature(),
                    IpcJsonContext.Default.NullableSingle); break;
            case "GetCodenameGeneration":
                response.Message = ((byte)cpuService.GetCodenameGeneration()).ToString(); break;
            case "IsPlatformPc":
                response.Message = JsonSerializer.Serialize(cpuService.IsPlatformPc(),
                    IpcJsonContext.Default.NullableBoolean); break;

            case "IsPlatformPcByCodename":
                response.Message = JsonSerializer.Serialize(cpuService.IsPlatformPcByCodename(),
                    IpcJsonContext.Default.NullableBoolean); break;
            case "GetCoreMultiplier":
                var core = JsonSerializer.Deserialize(request.Payload, IpcJsonContext.Default.Int32);
                response.Message = JsonSerializer.Serialize(cpuService.GetCoreMultiplier(core),
                    IpcJsonContext.Default.Double);
                break;

            case "MakeCoreMask":
                var maskData = JsonSerializer.Deserialize(request.Payload,
                    IpcJsonContext.Default.CoreMaskPayload);
                var mask = cpuService.MakeCoreMask(maskData!.Core, maskData.Ccd, maskData.Ccx);
                response.Message = JsonSerializer.Serialize(mask, IpcJsonContext.Default.UInt32);
                break;

            case "SetCoperSingleCore":
                var coperData = JsonSerializer.Deserialize(request.Payload,
                    IpcJsonContext.Default.SetCoperSingleCorePayload);
                cpuService.SetCoperSingleCore(coperData!.CoreMask, coperData.Margin);
                break;

            case "SendSmuCommand":
                var smuPayload = JsonSerializer.Deserialize(request.Payload,
                    IpcJsonContext.Default.SmuCommandPayload);
                var args = smuPayload!.Arguments;
                var status =
                    cpuService.SendSmuCommand(smuPayload.Mailbox, smuPayload.Command, ref args);
                response.Message = JsonSerializer.Serialize(
                    new SmuCommandResult { Status = status, Arguments = args },
                    IpcJsonContext.Default.SmuCommandResult);
                break;

            case "ReadMsr":
                var msrRead = JsonSerializer.Deserialize(request.Payload,
                    IpcJsonContext.Default.MsrReadPayload);
                uint eax = 0, edx = 0;
                var readSuccess = cpuService.ReadMsr(msrRead!.Index, ref eax, ref edx);
                response.Message = JsonSerializer.Serialize(
                    new MsrReadResult { Success = readSuccess, Eax = eax, Edx = edx },
                    IpcJsonContext.Default.MsrReadResult);
                break;

            case "WriteMsr":
                var msrWrite = JsonSerializer.Deserialize(request.Payload,
                    IpcJsonContext.Default.MsrWritePayload);
                response.Message = JsonSerializer.Serialize(
                    cpuService.WriteMsr(msrWrite!.Msr, msrWrite.Eax, msrWrite.Edx),
                    IpcJsonContext.Default.Boolean);
                break;

            default:
                response.IsSuccess = false;
                response.Message = "Unknown command";
                break;
        }
    }
}