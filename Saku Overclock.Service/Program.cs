using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Saku_Overclock.Core.Contracts;
using Saku_Overclock.Core.Services;
using Saku_Overclock.Shared;

namespace Saku_Overclock.Service;

public static class Program
{
    public static async Task Main(string[] args)
    {
        // Используем CreateEmptyApplicationBuilder для исключения лишней рефлексии под Native AOT
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { Args = args });

        // Важно: Имя должно ЖЕСТКО совпадать с Name="Saku_Overclock.Service" в Package.appxmanifest!
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "Saku_Overclock.Service";
        });

        // РЕГИСТРАЦИЯ DI ИЗ ЯДРА (Core)
        builder.Services.AddSingleton<ICpuService, CpuService>();
        
        // Регистрация хостед-сервиса IPC канала
        builder.Services.AddHostedService<IpcNamedPipeWorker>();

        var host = builder.Build();
        await host.RunAsync();
    }
}

public class IpcNamedPipeWorker(ICpuService cpuService) : BackgroundService
{
    private const string PipeName = "SakuOverclockIpcPipe";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
            try
            {
                // Позволяем подключение только процессам из текущей сессии/локальным
                await using var pipeServer = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipeServer.WaitForConnectionAsync(stoppingToken);

                using var reader = new StreamReader(pipeServer);
                await using var writer = new StreamWriter(pipeServer);
                writer.AutoFlush = true;

                while (!stoppingToken.IsCancellationRequested && pipeServer.IsConnected)
                {
                    var rawRequest = await reader.ReadLineAsync(stoppingToken);
                    if (string.IsNullOrEmpty(rawRequest)) break;

                    var request = JsonSerializer.Deserialize(rawRequest, IpcJsonContext.Default.IpcRequest);
                    var response = new IpcResponse { IsSuccess = true };

                    if (request == null) continue;

                    try
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
                    catch (Exception ex)
                    {
                        response.IsSuccess = false;
                        response.Message = $"Internal Service Error: {ex.Message}";
                    }

                    var rawResponse = JsonSerializer.Serialize(response, IpcJsonContext.Default.IpcResponse);
                    await writer.WriteLineAsync(rawResponse.AsMemory(), stoppingToken);
                }
            }
            catch (Exception)
            {
                await Task.Delay(100, stoppingToken);
            }
    }
}