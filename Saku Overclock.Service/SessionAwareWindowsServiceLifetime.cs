using System.ServiceProcess;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Saku_Overclock.Service;

public sealed class SessionAwareWindowsServiceLifetime : WindowsServiceLifetime
{
    private readonly OverlayProcessManager _overlayProcessManager;

    public SessionAwareWindowsServiceLifetime(
        IHostEnvironment environment,
        IHostApplicationLifetime applicationLifetime,
        ILoggerFactory loggerFactory,
        IOptions<HostOptions> optionsAccessor,
        IOptions<WindowsServiceLifetimeOptions> windowsServiceOptionsAccessor,
        OverlayProcessManager overlayProcessManager)
        : base(environment, applicationLifetime, loggerFactory, optionsAccessor, windowsServiceOptionsAccessor)
    {
        _overlayProcessManager = overlayProcessManager;
        CanHandleSessionChangeEvent = true;
    }

    protected override void OnSessionChange(SessionChangeDescription changeDescription)
    {
        var sessionId = (uint)changeDescription.SessionId;

        switch (changeDescription.Reason)
        {
            case SessionChangeReason.SessionLogon:
            case SessionChangeReason.SessionUnlock:
                _overlayProcessManager.StartForSession(sessionId);
                break;

            case SessionChangeReason.SessionLogoff:
            case SessionChangeReason.SessionLock:
                // TODO: swap to an IPC "hide" message once that contract
                // exists avoids re-paying tray/D3D init on every
                // lock/unlock cycle, which will happen constantly in practice.
                _overlayProcessManager.StopForSession(sessionId);
                break;
        }

        base.OnSessionChange(changeDescription);
    }
}