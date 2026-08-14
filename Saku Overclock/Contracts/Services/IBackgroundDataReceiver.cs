using Saku_Overclock.Shared.Models;

namespace Saku_Overclock.Contracts.Services;

public interface IBackgroundDataReceiver
{
    /// <summary>
    ///     Start reading Shared Memory
    /// </summary>
    /// <param name="cancellationToken">cts</param>
    void StartAsync(CancellationToken cancellationToken);
    
    /// <summary>
    ///     Data Received
    /// </summary>
    event EventHandler<SensorsInformation>? DataUpdated;
}