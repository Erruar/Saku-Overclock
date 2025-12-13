using Saku_Overclock.SmuEngine;

namespace Saku_Overclock.Contracts.Services;

public interface IBackgroundDataUpdater
{
    Task StartAsync(CancellationToken cancellationToken);
    void Stop();
    void UpdateNotifyIcons();
    event EventHandler<SensorsInformation> DataUpdated;
    bool IsBatteryUnavailable();
}