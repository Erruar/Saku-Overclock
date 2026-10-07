using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;

namespace Saku_Overclock.Services;

public class PackagedOverlayStarterService : IPackagedOverlayStarterService
{
    private const string OverlayAumid = "Saku-Overclock-App_8fghyvnsg2qm8!Overlay";
    
    public void EnsureOverlayRunning()
    {
        if (!RuntimeHelper.IsMsix)
            return;

        var currentSessionId = Process.GetCurrentProcess().SessionId;

        var overlayAlreadyRunning = Process.GetProcesses()
            .Any(p => p.SessionId == currentSessionId
                      && p.ProcessName == "Saku Overclock.Overlay");

        if (overlayAlreadyRunning)
            return;

        var manager = (IApplicationActivationManager)new ApplicationActivationManagerClass();
        manager.ActivateApplication(OverlayAumid, string.Empty, 0, out _);
    }
    
    [ComImport, Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager
    {
        int ActivateApplication(
            string appUserModelId, string arguments, uint options, out uint processId);
    }

    [ComImport, Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
    private sealed class ApplicationActivationManagerClass : IApplicationActivationManager
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern int ActivateApplication(
            [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            [MarshalAs(UnmanagedType.LPWStr)] string arguments,
            uint options,
            out uint processId);
    }
}