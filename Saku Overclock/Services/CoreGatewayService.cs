using System.Text.Json;
using Saku_Overclock.Shared;
using Saku_Overclock.Shared.Ipc;
using Saku_Overclock.Shared.Models;

namespace Saku_Overclock.Services;

public partial class CoreGatewayService : IDisposable
{
    private readonly IpcConnectionService _ipc;
    private HardwareInfoSnapshot? _hwSnapshot;
    private readonly SemaphoreSlim _hwLoadLock = new(1, 1);

    public CoreGatewayService(IpcConnectionService ipc)
    {
        _ipc = ipc;
        _ipc.OnEvent += OnIpcEvent;
    }

    public bool IsServiceUnavailable => _ipc.IsServiceUnavailable;

    public async Task WarmupAsync()
    {
        var json = await _ipc.SendCommandAsync("Get_HardwareInfo");
        _hwSnapshot = string.IsNullOrEmpty(json)
            ? new HardwareInfoSnapshot()
            : JsonSerializer.Deserialize(json, IpcJsonContext.Default.HardwareInfoSnapshot) ?? new HardwareInfoSnapshot();
    }

    public async Task RefreshHardwareInfoAsync()
    {
        await _hwLoadLock.WaitAsync();
        try { await WarmupAsync(); }
        finally { _hwLoadLock.Release(); }
    }

    private void OnIpcEvent(string name, string payload)
    {
        switch (name)
        {
            case "PresetApplied": OnPresetApplied(payload); break;
        }
    }

    public void Dispose() => _ipc.OnEvent -= OnIpcEvent;
}

public partial class CoreGatewayService : ICpuGateService
{
    public bool IsAvailable => _hwSnapshot?.IsAvailable ?? false;
    public uint PhysicalCores => _hwSnapshot?.PhysicalCores ?? 0;
    public uint[] CoreDisableMap => _hwSnapshot?.CoreDisableMap ?? [];
    public uint Cores => _hwSnapshot?.Cores ?? 0;
    public string CpuName => _hwSnapshot?.CpuName ?? string.Empty;
    public bool Smt => _hwSnapshot?.Smt ?? false;
    public CommonMotherBoardInfo MotherBoardInfo => _hwSnapshot?.MotherBoardInfo ?? new CommonMotherBoardInfo();
    public bool Avx512AvailableByCodename => _hwSnapshot?.Avx512AvailableByCodename ?? false;
    public string CpuCodeName => _hwSnapshot?.CpuCodeName ?? string.Empty;
    public string SmuVersion => _hwSnapshot?.SmuVersion ?? string.Empty;
    public uint PowerTableVersion => _hwSnapshot?.PowerTableVersion ?? 0;

    public CodenameGeneration GetCodenameGeneration() => _hwSnapshot?.CodenameGeneration ?? default;
    public MemoryConfig GetMemoryConfig() => _hwSnapshot?.MemoryConfig ?? new MemoryConfig();

    public async Task<string> GenerateDebugReportAsync() =>
        await _ipc.SendCommandAsync("Generate_Debug_Report");
}

public partial class CoreGatewayService : IPstateGateService
{
    public bool IsSupported => _hwSnapshot?.PstateSupported ?? false;

    public async Task<IReadOnlyList<PstateOperationResult>> ReadAllPstatesAsync()
    {
        var json = await _ipc.SendCommandAsync("Get_Pstates");
        if (string.IsNullOrEmpty(json)) return [];
        return JsonSerializer.Deserialize(json, IpcJsonContext.Default.ListPstateOperationResult) ?? [];
    }
}

public partial class CoreGatewayService : IOcFinderGateService
{
    public async Task<PresetRecommendations> GetPerformanceRecommendationDataAsync()
    {
        var json = await _ipc.SendCommandAsync("Get_Preset_Recommendations");
        return string.IsNullOrEmpty(json)
            ? new PresetRecommendations()
            : JsonSerializer.Deserialize(json, IpcJsonContext.Default.PresetRecommendations) ?? new();
    }

    public async Task<bool> IsUndervoltingAvailableAsync()
    {
        var json = await _ipc.SendCommandAsync("Get_Is_Undervolting_Available");
        return !string.IsNullOrEmpty(json) && JsonSerializer.Deserialize(json, IpcJsonContext.Default.Boolean);
    }

    public async Task<int> GetCpuPowerAsync()
    {
        var json = await _ipc.SendCommandAsync("Get_Cpu_Power");
        return string.IsNullOrEmpty(json) ? 0 : JsonSerializer.Deserialize(json, IpcJsonContext.Default.Int32);
    }
}

public partial class CoreGatewayService : IApplyerGateService
{
    public event Action<List<ApplyResult>>? OnSettingsApplied;

    public async Task ApplyPreset(Preset preset, bool saveInfo = false)
    {
        var req = new ApplyPresetRequest { Preset = preset, SaveInfo = saveInfo };
        var json = JsonSerializer.Serialize(req, IpcJsonContext.Default.ApplyPresetRequest);
        await _ipc.SendCommandAsync("Apply_Preset", json);
    }
    
    public async Task<PresetId?> SwitchNextPreset()
    {
        var json = await _ipc.SendCommandAsync("Apply_SwitchNextPreset");
        return JsonSerializer.Deserialize(json, IpcJsonContext.Default.PresetId);
    }

    private void OnPresetApplied(string payload)
    {
        var results = JsonSerializer.Deserialize(payload, IpcJsonContext.Default.ListApplyResult);
        if (results != null) OnSettingsApplied?.Invoke(results);
    }
}

public partial class CoreGatewayService
{
    public async Task<bool> IsBatteryUnavailableAsync()
    {
        var json = await _ipc.SendCommandAsync("Get_Is_Battery_Unavailable");
        return !string.IsNullOrEmpty(json) && JsonSerializer.Deserialize(json, IpcJsonContext.Default.Boolean);
    }
}