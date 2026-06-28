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

public static class App
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

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

    [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern bool QueryFullProcessImageName(
        IntPtr hProcess,
        uint dwFlags,
        IntPtr lpExeName,
        ref uint lpdwordSize);

    [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern uint WinVerifyTrust(IntPtr hwnd, ref Guid pgActionId, IntPtr pWvtData);

    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const string PipeName = "SakuOverclockIpcPipe";

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustFileInfo
    {
        public uint StructSize;
        public IntPtr FilePath; // LPCWSTR
        public IntPtr FileHandle; // HANDLE (null)
        public IntPtr Subject; // GUID*  (null)
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustData
    {
        public uint StructSize;
        public IntPtr PolicyCallbackData;
        public IntPtr SIPClientData;
        public uint UIChoice; // 2  = WTD_UI_NONE
        public uint RevocationChecks; // 0  = WTD_REVOKE_NONE
        public uint UnionChoice; // 1  = WTD_CHOICE_FILE
        public IntPtr FileInfo; // WinTrustFileInfo*
        public uint StateAction; // 1  = VERIFY, 2 = CLOSE
        public IntPtr StateData;
        public IntPtr URLReference;
        public uint ProvFlags; // 0x10 = WTD_CACHE_ONLY_URL_RETRIEVAL
        public uint UIContext;
        public IntPtr SignatureSettings;
    }

    private static readonly Guid ActionGenericVerifyV2 =
        new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    private string? _serviceThumbprint;
    private bool _serviceIsSigned;
    private bool _serviceCertChecked;

    private void EnsureServiceCertCached()
    {
        if (_serviceCertChecked) return;
        _serviceCertChecked = true;

        try
        {
            var path = Environment.ProcessPath;
            if (path is not null && VerifyAuthenticode(path))
            {
                using var cert = X509CertificateLoader.LoadCertificateFromFile(path);
                _serviceThumbprint = cert.GetCertHashString();
                _serviceIsSigned = true;
                logger.LogInformation("Service is signed. Thumbprint: {Tp}", _serviceThumbprint);
            }
            else
            {
                logger.LogInformation("Service is unsigned — debug/dev mode active.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Service cert check failed — debug/dev mode active.");
        }
    }

    private static unsafe bool VerifyAuthenticode(string filePath)
    {
        var actionId = ActionGenericVerifyV2;

        fixed (char* pathPtr = filePath)
        {
            var fileInfo = new WinTrustFileInfo
            {
                StructSize = (uint)sizeof(WinTrustFileInfo),
                FilePath = (IntPtr)pathPtr,
            };

            var trustData = new WinTrustData
            {
                StructSize = (uint)sizeof(WinTrustData),
                UIChoice = 2, // WTD_UI_NONE
                RevocationChecks = 0, // WTD_REVOKE_NONE — оффлайн, не проверяем отзыв
                UnionChoice = 1, // WTD_CHOICE_FILE
                FileInfo = (IntPtr)(&fileInfo),
                StateAction = 1, // WTD_STATE-ACTION_VERIFY
                ProvFlags = 0x10, // WTD_CACHE_ONLY_URL_RETRIEVAL
            };

            var result = WinVerifyTrust(IntPtr.Zero, ref actionId, (IntPtr)(&trustData));

            // Освобождаем StateData — иначе утечка внутри wintrust.dll
            trustData.StateAction = 2; // WTD_STATE-ACTION_CLOSE
            WinVerifyTrust(IntPtr.Zero, ref actionId, (IntPtr)(&trustData));

            // 0x00000000 = S_OK          — подпись валидна, файл не изменён
            // 0x80096010 = BAD_DIGEST    — файл изменён после подписания
            // 0x800B0100 = NO-SIGNATURE   — подписи нет
            return result == 0;
        }
    }

    private string? GetClientProcessPath(uint pid)
    {
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (handle == IntPtr.Zero) return null;

        try
        {
            const int bufferSize = 2048;
            var bufferPtr = Marshal.AllocHGlobal(bufferSize * sizeof(char));
            try
            {
                uint size = bufferSize;
                return QueryFullProcessImageName(handle, 0, bufferPtr, ref size)
                    ? Marshal.PtrToStringUni(bufferPtr, (int)size)
                    : null;
            }
            finally
            {
                Marshal.FreeHGlobal(bufferPtr);
            }
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    private bool ValidateClientSignature(NamedPipeServerStream pipe)
    {
        if (pipe.SafePipeHandle.IsInvalid) return false;

        EnsureServiceCertCached();

        if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle.DangerousGetHandle(), out var pid))
        {
            logger.LogWarning("Failed to get client PID.");
            return false;
        }

        var clientPath = GetClientProcessPath(pid);
        if (string.IsNullOrEmpty(clientPath) || !File.Exists(clientPath))
        {
            logger.LogWarning("Client executable path is invalid.");
            return false;
        }

        // 2. Базовая проверка имени — дополнительный фильтр, не основная защита
        var fileName = Path.GetFileName(clientPath);
        if (!string.Equals(fileName, "Saku Overclock.exe", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Unexpected client process name: {Name}", fileName);
            return false;
        }

        // 3. Debug/dev mode — сервис собран без подписи (из исходников / GitHub без сертификата)
        if (!_serviceIsSigned)
        {
            logger.LogInformation("Debug mode: signature check skipped. Client: {Path}", clientPath);
            return true;
        }

        // 4. Production: WinVerifyTrust — подпись есть и файл не изменён
        if (!VerifyAuthenticode(clientPath))
        {
            logger.LogWarning("Client Authenticode failed (unsigned or tampered): {Path}", clientPath);
            return false;
        }

        // 5. Thumbprint клиента должен совпадать с thumbprint сервиса
        //    (оба подписаны одним сертификатом)
        try
        {
            using var clientCert = X509CertificateLoader.LoadCertificateFromFile(clientPath);
            var clientThumbprint = clientCert.GetCertHashString();

            if (!string.Equals(clientThumbprint, _serviceThumbprint, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Thumbprint mismatch. Client: {C} | Service: {S}",
                    clientThumbprint, _serviceThumbprint);
                return false;
            }

            logger.LogInformation("Client verified successfully. Process: {Path}", clientPath);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error comparing client certificate.");
            return false;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
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

            if (!isClientVerified)
            {
                var isValid = false;
                try
                {
                    pipe.RunAsClient(() =>
                    {
                        using var identity = WindowsIdentity.GetCurrent();
                        if (!identity.IsAuthenticated) return;

                        // Отклоняем сетевые логины
                        var networkSid = new SecurityIdentifier(WellKnownSidType.NetworkSid, null);
                        if (identity.Groups?.Any(g => g.Value == networkSid.Value) == true)
                        {
                            logger.LogWarning("Network logon rejected: {Name}", identity.Name);
                            return;
                        }

                        if (identity.ImpersonationLevel is TokenImpersonationLevel.Impersonation
                            or TokenImpersonationLevel.Delegation)
                        {
                            logger.LogInformation("Client identity confirmed: {Name}", identity.Name);
                            isValid = true;
                        }
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Identity validation failed.");
                }

                if (isValid)
                    isValid = ValidateClientSignature(pipe);

                if (!isValid)
                {
                    logger.LogWarning("Access Denied: Unauthorized client process.");
                    break;
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
            case "ReturnCpuPowerLimit":
                response.Message = JsonSerializer.Serialize(cpuService.ReturnCpuPowerLimit(),
                    IpcJsonContext.Default.Single); break;
            case "ReturnUndervoltingAvailability":
                response.Message = JsonSerializer.Serialize(cpuService.ReturnUndervoltingAvailability(),
                    IpcJsonContext.Default.Boolean); break;
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
                // TODO: MAKE ABSTRACTIONS. WORK IN PROGRESS
                break;
                var coperData = JsonSerializer.Deserialize(request.Payload,
                    IpcJsonContext.Default.SetCoperSingleCorePayload);
                cpuService.SetCoperSingleCore(coperData!.CoreMask, coperData.Margin);
                break;

            case "SendSmuCommand":
                // TODO: MAKE ABSTRACTIONS. WORK IN PROGRESS
                break;
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
                // TODO: MAKE ABSTRACTIONS. WORK IN PROGRESS
                break;
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