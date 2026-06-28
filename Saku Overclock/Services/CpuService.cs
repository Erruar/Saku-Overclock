using System.IO.Pipes;
using System.Text.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Shared;

namespace Saku_Overclock.Services;

public class CpuService : ICpuService
{
    private NamedPipeClientStream? _pipeClient;
    private StreamWriter? _writer;
    private StreamReader? _reader;
    private readonly Lock _lock = new();
    private bool _isServiceUnavailable;

    public CpuService()
    {
        EnsureConnection();
    }

    private static DateTime _nextRetryTime = DateTime.MinValue;

    private bool EnsureConnection()
    {
        lock (_lock)
        {
            // 1. Если всё работает
            if (_pipeClient is { IsConnected: true } && _writer != null && _reader != null) return true;

            // 2. Если щит поднят И 10 секунд ещё не прошло — мгновенно выходим (без фризов UI и без логов)
            if (_isServiceUnavailable && DateTime.UtcNow < _nextRetryTime)
                return false;

            ResetConnection();

            try
            {
                _pipeClient = new NamedPipeClientStream(".", "SakuOverclockIpcPipe", PipeDirection.InOut,
                    PipeOptions.Asynchronous);
                _pipeClient.Connect(1000); // Тайм-аут 1 секунда
                _writer = new StreamWriter(_pipeClient) { AutoFlush = true };
                _reader = new StreamReader(_pipeClient);

                _isServiceUnavailable = false; // Служба ожила
                return true;
            }
            catch
            {
                ResetConnection();

                // 3. Логируем СТРОГО один раз — только в момент, когда связь ОТРЕЗАЛО
                if (!_isServiceUnavailable)
                {
                    LogHelper.TraceIt_TraceError("Saku Overclock Service is not running or unavailable!");
                    _isServiceUnavailable = true;
                }

                // 4. Затыкаем любые попытки переподключения на 10 секунд
                _nextRetryTime = DateTime.UtcNow.AddSeconds(10);
                return false;
            }
        }
    }

    private void ResetConnection()
    {
        try
        {
            _writer?.Dispose();
        }
        catch
        {
            LogHelper.LogError("Saku Overclock Service writer not closed properly");
        }

        try
        {
            _reader?.Dispose();
        }
        catch
        {
            LogHelper.LogError("Saku Overclock Service reader not closed properly");
        }

        try
        {
            _pipeClient?.Dispose();
        }
        catch
        {
            LogHelper.LogError("Saku Overclock Service pipe client not closed properly");
        }

        _writer = null;
        _reader = null;
        _pipeClient = null;
    }

    private string CallResponse(string command, string payload = "")
    {
        if (!EnsureConnection()) return string.Empty;

        lock (_lock)
        {
            try
            {
                var req = new IpcRequest { Command = command, Payload = payload };
                var jsonReq = JsonSerializer.Serialize(req, IpcJsonContext.Default.IpcRequest);
                _writer!.WriteLine(jsonReq);

                var jsonRes = _reader!.ReadLine();
                if (string.IsNullOrEmpty(jsonRes)) throw new IOException("Empty pipe string response.");

                var res = JsonSerializer.Deserialize(jsonRes, IpcJsonContext.Default.IpcResponse);
                return res is { IsSuccess: true } ? res.Message : string.Empty;
            }
            catch
            {
                ResetConnection();
                if (!_isServiceUnavailable)
                {
                    LogHelper.TraceIt_TraceError($"IPC communication lost during command: {command}");
                    _isServiceUnavailable = true;
                }

                return string.Empty;
            }
        }
    }

    public bool IsServiceUnavailable => _isServiceUnavailable;
    
    public enum SmuStatus : byte
    {
        Ok = 1,
        Failed = byte.MaxValue,
        UnknownCmd = 254,
        CmdRejectedPrereq = 253,
        CmdRejectedBusy = 252,
        TimeoutMutexLock = 48,
        TimeoutMailboxReady = 49,
        TimeoutMailboxMsgWrite = 50,
        PciFailed = 51
    }

    private T? GetValue<T>(string command, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        string payload = "")
    {
        var res = CallResponse(command, payload);
        return string.IsNullOrEmpty(res) ? default : JsonSerializer.Deserialize(res, typeInfo);
    }

    public bool IsAvailable => GetValue("Get_IsAvailable", IpcJsonContext.Default.Boolean);
    public bool IsRaven => GetValue("Get_IsRaven", IpcJsonContext.Default.Boolean);
    public bool IsDragonRange => GetValue("Get_IsDragonRange", IpcJsonContext.Default.Boolean);
    public uint PhysicalCores => GetValue("Get_PhysicalCores", IpcJsonContext.Default.UInt32);
    public uint Cores => GetValue("Get_Cores", IpcJsonContext.Default.UInt32);
    public string CpuName => CallResponse("Get_CpuName");
    public bool Smt => GetValue("Get_Smt", IpcJsonContext.Default.Boolean);
    public string CpuCodeName => CallResponse("Get_CpuCodeName");
    public string SmuVersion => CallResponse("Get_SmuVersion");
    public uint PowerTableVersion => GetValue("Get_PowerTableVersion", IpcJsonContext.Default.UInt32);
    public float SocMemoryClock => GetValue("Get_SocMemoryClock", IpcJsonContext.Default.Single);
    public float SocFabricClock => GetValue("Get_SocFabricClock", IpcJsonContext.Default.Single);
    public float SocVoltage => GetValue("Get_SocVoltage", IpcJsonContext.Default.Single);

    public uint[] CoreDisableMap => GetValue("Get_CoreDisableMap", IpcJsonContext.Default.UInt32Array) ?? [];
    public float[] PowerTable => GetValue("Get_PowerTable", IpcJsonContext.Default.SingleArray) ?? [];
    public SmuAddressSet Rsmu => GetValue("Get_Rsmu", IpcJsonContext.Default.SmuAddressSet) ?? new SmuAddressSet();
    public SmuAddressSet Mp1 => GetValue("Get_Mp1", IpcJsonContext.Default.SmuAddressSet) ?? new SmuAddressSet();
    public SmuAddressSet Hsmp => GetValue("Get_Hsmp", IpcJsonContext.Default.SmuAddressSet) ?? new SmuAddressSet();

    public CommonMotherBoardInfo MotherBoardInfo =>
        GetValue("Get_MotherBoardInfo", IpcJsonContext.Default.CommonMotherBoardInfo);

    public MemoryConfig GetMemoryConfig()
    {
        return GetValue("Get_MemoryConfig", IpcJsonContext.Default.MemoryConfig);
    }

    public CpuFamily Family =>
        byte.TryParse(CallResponse("Get_Family"), out var b) ? (CpuFamily)b : CpuFamily.Unsupported;

    public bool Avx512AvailableByCodename => GetValue("Get_Avx512AvailableByCodename", IpcJsonContext.Default.Boolean);

    public uint SmuCoperCommandMp1
    {
        get => GetValue("Get_SmuCoperCommandMp1", IpcJsonContext.Default.UInt32);
        set => CallResponse("Set_SmuCoperCommandMp1", JsonSerializer.Serialize(value, IpcJsonContext.Default.UInt32));
    }

    public uint SmuCoperCommandRsmu
    {
        get => GetValue("Get_SmuCoperCommandRsmu", IpcJsonContext.Default.UInt32);
        set => CallResponse("Set_SmuCoperCommandRsmu", JsonSerializer.Serialize(value, IpcJsonContext.Default.UInt32));
    }

    public void RefreshPowerTable()
    {
        CallResponse("RefreshPowerTable");
    }

    public float? GetCpuTemperature()
    {
        return GetValue("GetCpuTemperature", IpcJsonContext.Default.NullableSingle);
    }

    public CodenameGeneration GetCodenameGeneration()
    {
        return byte.TryParse(CallResponse("GetCodenameGeneration"), out var b)
            ? (CodenameGeneration)b
            : CodenameGeneration.Unknown;
    }

    public bool? IsPlatformPc()
    {
        return GetValue("IsPlatformPc", IpcJsonContext.Default.NullableBoolean);
    }

    public bool? IsPlatformPcByCodename()
    {
        return GetValue("IsPlatformPcByCodename", IpcJsonContext.Default.NullableBoolean);
    }

    public double GetCoreMultiplier(int core)
    {
        return GetValue("GetCoreMultiplier", IpcJsonContext.Default.Double,
            JsonSerializer.Serialize(core, IpcJsonContext.Default.Int32));
    }

    public uint MakeCoreMask(uint core = 0, uint ccd = 0, uint ccx = 0)
    {
        return GetValue("MakeCoreMask", IpcJsonContext.Default.UInt32,
            JsonSerializer.Serialize(new CoreMaskPayload { Core = core, Ccd = ccd, Ccx = ccx },
                IpcJsonContext.Default.CoreMaskPayload));
    }

    public void SetCoperSingleCore(uint coreMask, int margin)
    {
        CallResponse("SetCoperSingleCore",
            JsonSerializer.Serialize(new SetCoperSingleCorePayload { CoreMask = coreMask, Margin = margin },
                IpcJsonContext.Default.SetCoperSingleCorePayload));
    }

    public SmuStatus SendSmuCommand(SmuAddressSet mailbox, uint command, ref uint[] arguments)
    {
        var p = JsonSerializer.Serialize(
            new SmuCommandPayload { Mailbox = mailbox, Command = command, Arguments = arguments },
            IpcJsonContext.Default.SmuCommandPayload);
        var res = GetValue("SendSmuCommand", IpcJsonContext.Default.SmuCommandResult, p);
        if (res == null) return SmuStatus.UnknownCmd;

        arguments = res.Arguments;
        return (SmuStatus)res.Status;
    }

    public bool ReadMsr(uint index, ref uint eax, ref uint edx)
    {
        var p = JsonSerializer.Serialize(new MsrReadPayload { Index = index }, IpcJsonContext.Default.MsrReadPayload);
        var res = GetValue("ReadMsr", IpcJsonContext.Default.MsrReadResult, p);
        if (res == null) return false;

        eax = res.Eax;
        edx = res.Edx;
        return res.Success;
    }

    public bool WriteMsr(uint msr, uint eax, uint edx)
    {
        return GetValue("WriteMsr", IpcJsonContext.Default.Boolean,
            JsonSerializer.Serialize(new MsrWritePayload { Msr = msr, Eax = eax, Edx = edx },
                IpcJsonContext.Default.MsrWritePayload));
    }

    public void GenerateDebugReport()
    {
        CallResponse("GenerateDebugReport");
    }
    
    public double ReturnCpuPowerLimit()
    {
        return GetValue("ReturnCpuPowerLimit", IpcJsonContext.Default.Single);
    }

    public bool ReturnUndervoltingAvailability()
    {
        return GetValue("ReturnUndervoltingAvailability", IpcJsonContext.Default.Boolean);
    }
}