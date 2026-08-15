using System.IO.MemoryMappedFiles;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Shared;

namespace Saku_Overclock.Services;

public class RawSharedMemoryReaderService(IpcConnectionService ipcConnectionService,
    ICpuGateService cpu) : IRawSharedMemoryReaderService, IDisposable
{
    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _accessor;
    private int _elementCount;
    private float[]? _buffer;

    public void Initialize()
    {
        Dispose();
        _elementCount = (int)cpu.PowerTableSize;

        _mmf = MemoryMappedFile.OpenExisting(@"Global\SakuOverclock_RawSensors", MemoryMappedFileRights.Read);
        _accessor = _mmf.CreateViewAccessor(0, 2 * sizeof(int) + _elementCount * sizeof(float), MemoryMappedFileAccess.Read);
    }

    public async Task StartUpdate()
    {
        try
        {
            await ipcConnectionService.SendCommandAsync("RawData_StartUpdate");
            Initialize();
        }
        catch (Exception ex)
        {
            await LogHelper.LogError(ex);
        }
    }

    public async Task StopUpdate()
    {
        try
        {
            await ipcConnectionService.SendCommandAsync("RawData_StopUpdate");
        }
        catch (Exception ex)
        {
            await LogHelper.LogError(ex);
        }
    }

    public float[]? GetRawData()
    {
        if (_accessor == null) return null;

        int startIteration;
        _buffer ??= new float[_elementCount];
        do
        {
            startIteration = _accessor.ReadInt32(0);
            if (startIteration % 2 != 0) return null; 

            _accessor.ReadArray(8, _buffer, 0, _elementCount);

        } while (startIteration != _accessor.ReadInt32(4)); 

        return _buffer;
    }

    public void Dispose()
    {
        _accessor?.Dispose();
        _mmf?.Dispose();
        _accessor = null;
        _mmf = null;
    }
}