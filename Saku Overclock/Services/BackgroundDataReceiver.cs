using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Shared.Models;

namespace Saku_Overclock.Services;

public class BackgroundDataReceiver(ILogger<BackgroundDataReceiver> logger) : IBackgroundDataReceiver, IDisposable
{
    private CancellationTokenSource? _cts;
    private readonly SensorsInformation _sensorsInformation = new();
    public event EventHandler<SensorsInformation>? DataUpdated;
    
    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _accessor;
    private bool _isMemoryInitialized;
    private bool _isStaticDataInitialized; // Parse static strings only once

    public void StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _sensorsInformation.CpuTemperaturePerCore = new double[32];
        _sensorsInformation.CpuFrequencyPerCore = new double[32];
        _sensorsInformation.CpuVoltagePerCore = new double[32];
        _sensorsInformation.CpuPowerPerCore = new double[32];

        Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    if (!_isMemoryInitialized) InitializeMemory();

                    if (_isMemoryInitialized && TryReadSensors(out var sharedData))
                    {
                        MapDataToModel(ref sharedData);
                        DataUpdated?.Invoke(this, _sensorsInformation);
                    }
                }
                catch (FileNotFoundException)
                {
                    _isMemoryInitialized = false;
                    _isStaticDataInitialized = false;
                }
                catch (Exception ex)
                {
                    logger.LogError("Error reading shared memory: {ex}", ex);
                    _isMemoryInitialized = false;
                    _isStaticDataInitialized = false;
                }

                try
                {
                    await Task.Delay(300, _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, _cts.Token);
    }

    private void InitializeMemory()
    {
        if (_accessor != null) return;
        
        _mmf = MemoryMappedFile.OpenExisting(@"Global\SakuOverclock_Sensors", MemoryMappedFileRights.Read);
        int size = Unsafe.SizeOf<SensorsInformationShared>();
        _accessor = _mmf.CreateViewAccessor(0, size, MemoryMappedFileAccess.Read);
        _isMemoryInitialized = true;
    }

    private bool TryReadSensors(out SensorsInformationShared localCopy)
    {
        localCopy = default;
        if (_accessor == null) return false;

        int startIteration;
        do
        {
            startIteration = _accessor.ReadInt32(0);
            if (startIteration % 2 != 0) return false; 
            
            _accessor.Read(0, out localCopy);

        } while (startIteration != localCopy.IterationEnd);

        return true;
    }

    private void MapDataToModel(ref SensorsInformationShared data)
    {
        // Dyn data
        _sensorsInformation.CpuStapmLimit = data.CpuStapmLimit;
        _sensorsInformation.CpuStapmValue = data.CpuStapmValue;
        _sensorsInformation.CpuFastLimit = data.CpuFastLimit;
        _sensorsInformation.CpuFastValue = data.CpuFastValue;
        _sensorsInformation.CpuSlowLimit = data.CpuSlowLimit;
        _sensorsInformation.CpuSlowValue = data.CpuSlowValue;
        _sensorsInformation.ApuSlowLimit = data.ApuSlowLimit;
        _sensorsInformation.ApuSlowValue = data.ApuSlowValue;
        
        _sensorsInformation.VrmTdcValue = data.VrmTdcValue;
        _sensorsInformation.VrmTdcLimit = data.VrmTdcLimit;
        _sensorsInformation.VrmEdcValue = data.VrmEdcValue;
        _sensorsInformation.VrmEdcLimit = data.VrmEdcLimit;
        _sensorsInformation.VrmPsiValue = data.VrmPsiValue;
        _sensorsInformation.VrmPsiSocValue = data.VrmPsiSocValue;
        
        _sensorsInformation.SocTdcValue = data.SocTdcValue;
        _sensorsInformation.SocTdcLimit = data.SocTdcLimit;
        _sensorsInformation.SocEdcValue = data.SocEdcValue;
        _sensorsInformation.SocEdcLimit = data.SocEdcLimit;
        
        _sensorsInformation.CpuTempValue = data.CpuTempValue;
        _sensorsInformation.CpuTempLimit = data.CpuTempLimit;
        _sensorsInformation.ApuTempValue = data.ApuTempValue;
        _sensorsInformation.ApuTempLimit = data.ApuTempLimit;
        _sensorsInformation.DgpuTempValue = data.DgpuTempValue;
        _sensorsInformation.DgpuTempLimit = data.DgpuTempLimit;
        
        _sensorsInformation.CpuStapmTimeValue = data.CpuStapmTimeValue;
        _sensorsInformation.CpuSlowTimeValue = data.CpuSlowTimeValue;
        _sensorsInformation.CpuUsage = data.CpuUsage;

        _sensorsInformation.ApuFrequency = data.ApuFrequency;
        _sensorsInformation.ApuVoltage = data.ApuVoltage;
        _sensorsInformation.MemFrequency = data.MemFrequency;
        _sensorsInformation.FabricFrequency = data.FabricFrequency;
        _sensorsInformation.SocPower = data.SocPower;
        _sensorsInformation.SocVoltage = data.SocVoltage;
        _sensorsInformation.CpuFrequency = data.CpuFrequency;
        _sensorsInformation.CpuVoltage = data.CpuVoltage;

        _sensorsInformation.BatteryUnavailable = data.BatteryUnavailable;
        _sensorsInformation.BatteryPercent = data.BatteryPercent;
        _sensorsInformation.BatteryState = data.BatteryState;
        _sensorsInformation.BatteryChargeRate = data.BatteryChargeRate;
        _sensorsInformation.BatteryLifeTime = data.BatteryLifeTime;

        _sensorsInformation.RamTotal = data.RamTotal;
        _sensorsInformation.RamBusy = data.RamBusy;
        _sensorsInformation.RamUsagePercent = data.RamUsagePercent;
        
        _sensorsInformation.RamUsage = $"{data.RamUsagePercent}%\n{data.RamBusy:F1}/{data.RamTotal:F1}GB";

        _sensorsInformation.IsNvidiaGpuAvailable = data.IsNvidiaGpuAvailable;
        _sensorsInformation.NvidiaVramFrequency = data.NvidiaVramFrequency;
        _sensorsInformation.NvidiaGpuUsage = data.NvidiaGpuUsage;
        _sensorsInformation.NvidiaGpuFrequency = data.NvidiaGpuFrequency;
        _sensorsInformation.NvidiaGpuTemperature = data.NvidiaGpuTemperature;

        // Arrays
        for (int i = 0; i < 32; i++)
        {
            _sensorsInformation.CpuFrequencyPerCore![i] = data.CpuFrequencyPerCore[i];
            _sensorsInformation.CpuVoltagePerCore![i] = data.CpuVoltagePerCore[i];
            _sensorsInformation.CpuPowerPerCore![i] = data.CpuPowerPerCore[i];
            _sensorsInformation.CpuTemperaturePerCore![i] = data.CpuTemperaturePerCore[i];
        }

        // Static data
        if (!_isStaticDataInitialized)
        {
            _sensorsInformation.CpuFamily = ParseSafeString16(data.CpuCodeName);
            _sensorsInformation.BatteryName = ParseSafeString32(data.BatteryName);
            _sensorsInformation.BatteryHealth = ParseSafeString16(data.BatteryHealth);
            _sensorsInformation.BatteryCycles = ParseSafeString32(data.BatteryCycles);
            _sensorsInformation.BatteryCapacity = ParseSafeString32(data.BatteryCapacity);
            _sensorsInformation.NvidiaDriverVersion = ParseSafeString16(data.NvidiaDriverVersion);
            _sensorsInformation.NvidiaVramSize = ParseSafeString16(data.NvidiaVramSize);
            _sensorsInformation.NvidiaVramType = ParseSafeString16(data.NvidiaVramType);
            _sensorsInformation.NvidiaVramWidth = ParseSafeString16(data.NvidiaVramWidth);

            _isStaticDataInitialized = true;
        }
    }

    private static string ParseSafeString16(ReadOnlySpan<char> span)
    {
        int nullIndex = span.IndexOf('\0');
        return new string(nullIndex == -1 ? span : span.Slice(0, nullIndex));
    }

    private static string ParseSafeString32(ReadOnlySpan<char> span)
    {
        int nullIndex = span.IndexOf('\0');
        return new string(nullIndex == -1 ? span : span.Slice(0, nullIndex));
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _accessor?.Dispose();
        _mmf?.Dispose();
    }
}