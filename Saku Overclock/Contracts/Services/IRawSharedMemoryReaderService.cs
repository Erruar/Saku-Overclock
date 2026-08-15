namespace Saku_Overclock.Contracts.Services;

public interface IRawSharedMemoryReaderService
{
    /// <summary>
    ///     Service registration 
    /// </summary>
    void Initialize();

    /// <summary>
    ///     Start receiving raw data
    /// </summary>
    Task StartUpdate();
    
    /// <summary>
    ///     Stop receiving raw data
    /// </summary>
    Task StopUpdate();
    
    /// <summary>
    ///     Read PM Table
    /// </summary>
    /// <returns>Raw sensors table</returns>
    float[]? GetRawData();
}