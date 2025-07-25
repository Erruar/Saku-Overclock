﻿using Saku_Overclock.SMUEngine;

namespace Saku_Overclock.Contracts.Services;

public interface IBackgroundDataUpdater
{
    Task StartAsync(CancellationToken cancellationToken);
    void Stop();
    void UpdateNotifyIcons();
    event EventHandler<SensorsInformation> DataUpdated;
}
