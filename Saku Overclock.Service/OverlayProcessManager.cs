using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Saku_Overclock.Service;

public sealed partial class OverlayProcessManager(ILogger<OverlayProcessManager> logger)
{
    private readonly Dictionary<uint, nint> _processHandlesBySession = new();
    private readonly Dictionary<uint, Process> _fallbackProcesses = new();

    public void StartForSession(uint sessionId)
    {
        if (_processHandlesBySession.ContainsKey(sessionId) || _fallbackProcesses.ContainsKey(sessionId))
            return;

        var pfn = GetPackageFamilyName();
        if (!string.IsNullOrEmpty(pfn))
        {
            var overlayName = $"{pfn}!Overlay";
            try
            {
                var activationManager = CreateActivationManager();
                if (activationManager != null)
                {
                    var hr = activationManager.ActivateApplication(overlayName, string.Empty, ActivateOptions.None, out uint pid);
                    if (hr >= 0)
                    {
                        _processHandlesBySession[sessionId] = Process.GetProcessById((int)pid).Handle;
                        return;
                    }
                    logger.LogWarning("ActivateApplication failed: 0x{Hr:X8}", hr);
                }

                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning("AUMID activation failed: {Error}. trying normal start.", ex.Message);
            }
        }

        StartUnpackaged(sessionId);
    }

    private void StartUnpackaged(uint sessionId)
    {
        // Primary path: LocalSystem services running as plain Win32 hold
        // SE_TCB_NAME and can use WTSQueryUserToken directly. Inside an
        // MSIX/AppContainer that privilege is stripped, so we transparently
        // fall back to duplicating explorer.exe's token (only needs
        // SE_DEBUG_NAME, which LocalSystem retains even in packaged mode).
        if (!TryGetPrimaryTokenForSession(sessionId, out var primaryToken, out var usedExplorerFallback))
        {
            logger.LogWarning("No user token available for session {SessionId}, using Process.Start fallback", sessionId);
            StartFallback(sessionId);
            return;
        }

        try
        {
            CreateEnvironmentBlock(out var envBlock, primaryToken, false);

            var startupInfo = new Startupinfow
            {
                cb = (uint)Marshal.SizeOf<Startupinfow>(),
                lpDesktop = "winsta0\\default"
            };

            const uint createUnicodeEnvironment = 0x00000400;
            const uint createNoWindow = 0x08000000;

            // TODO: pass an IPC endpoint name as argv[0] once the overlay's
            // pipe client is wired up, e.g. via lpCommandLine below.
            var overlayPath = Path.Combine(AppContext.BaseDirectory, "Saku Overclock.Overlay.exe");

            var ok = CreateProcessAsUser(
                primaryToken, overlayPath, null,
                0, 0, false,
                createUnicodeEnvironment | createNoWindow,
                envBlock, null,
                ref startupInfo, out var processInfo);

            if (envBlock != 0) DestroyEnvironmentBlock(envBlock);

            if (!ok)
            {
                logger.LogError("CreateProcessAsUser failed for session {SessionId}: {Error}",
                    sessionId, Marshal.GetLastWin32Error());
                return;
            }

            CloseHandle(processInfo.hThread);
            _processHandlesBySession[sessionId] = processInfo.hProcess;
            logger.LogInformation("Overlay started ({Mode}) for session {SessionId}, pid {Pid}",
                usedExplorerFallback ? "explorer-token" : "WTSQueryUserToken",
                sessionId, processInfo.dwProcessId);
        }
        finally
        {
            CloseHandle(primaryToken);
        }
    }

    /// <summary>
    ///     Acquires a primary user token for the target session.
    ///     Tries WTSQueryUserToken first (works for LocalSystem Win32 services),
    ///     then falls back to duplicating the token of a process already
    ///     running inside that session (explorer.exe). The explorer path only
    ///     requires SE_DEBUG_NAME, which LocalSystem holds even inside an
    ///     MSIX/AppContainer, so it works for packaged deployments too.
    /// </summary>
    private bool TryGetPrimaryTokenForSession(uint sessionId, out nint primaryToken, out bool usedExplorerFallback)
    {
        primaryToken = 0;
        usedExplorerFallback = false;

        if (WtsQueryUserToken(sessionId, out var userToken))
        {
            try
            {
                if (DuplicateTokenEx(
                        userToken, TokenAllAccess, 0,
                        SecurityImpersonationLevel.SecurityImpersonation,
                        TokenType.TokenPrimary,
                        out primaryToken))
                {
                    return true;
                }

                logger.LogWarning("DuplicateTokenEx failed for session {SessionId}: {Error}",
                    sessionId, Marshal.GetLastWin32Error());
            }
            finally
            {
                CloseHandle(userToken);
            }
        }
        else
        {
            logger.LogWarning("WTSQueryUserToken failed for session {SessionId}: {Error}. Trying explorer.exe token.",
                sessionId, Marshal.GetLastWin32Error());
        }

        // Fallback: steal the token from explorer.exe running in the same
        // session. We pick explorer specifically because:
        //  - it always runs in the interactive user session,
        //  - its token is a non-elevated primary user token,
        //  - opening it only requires SE_DEBUG_NAME (LocalSystem has it).
        // This bypasses the SE_TCB_NAME requirement entirely, which is what
        // makes it work inside MSIX/AppContainer.
        if (TryGetPrimaryTokenFromExplorer(sessionId, out primaryToken))
        {
            usedExplorerFallback = true;
            return true;
        }

        return false;
    }

    private bool TryGetPrimaryTokenFromExplorer(uint sessionId, out nint primaryToken)
    {
        primaryToken = 0;

        Process? explorer;
        try
        {
            explorer = Process.GetProcessesByName("explorer")
                .FirstOrDefault(p => p.SessionId == (int)sessionId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to enumerate explorer processes for session {SessionId}", sessionId);
            return false;
        }

        if (explorer == null)
        {
            logger.LogWarning("No explorer.exe found in session {SessionId}", sessionId);
            return false;
        }

        var hProcess = OpenProcess(ProcessQueryInformation, false, (uint)explorer.Id);
        if (hProcess == 0)
        {
            logger.LogWarning("OpenProcess(explorer.exe) failed for session {SessionId}: {Error}",
                sessionId, Marshal.GetLastWin32Error());
            return false;
        }

        try
        {
            if (!OpenProcessToken(hProcess, TokenDuplicate | TokenQuery, out var hToken))
            {
                logger.LogWarning("OpenProcessToken(explorer.exe) failed for session {SessionId}: {Error}",
                    sessionId, Marshal.GetLastWin32Error());
                return false;
            }

            try
            {
                if (!DuplicateTokenEx(
                        hToken, TokenAllAccess, 0,
                        SecurityImpersonationLevel.SecurityImpersonation,
                        TokenType.TokenPrimary,
                        out primaryToken))
                {
                    logger.LogWarning("DuplicateTokenEx(explorer token) failed for session {SessionId}: {Error}",
                        sessionId, Marshal.GetLastWin32Error());
                    return false;
                }

                return true;
            }
            finally
            {
                CloseHandle(hToken);
            }
        }
        finally
        {
            CloseHandle(hProcess);
        }
    }

    private void StartFallback(uint sessionId)
    {
        var overlayPath = Path.Combine(AppContext.BaseDirectory, "Saku Overclock.Overlay.exe");
        if (!File.Exists(overlayPath))
        {
            logger.LogError("Overlay executable not found at {Path}", overlayPath);
            return;
        }

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = overlayPath,
                    UseShellExecute = false,
                }
            };

            if (process.Start())
            {
                _fallbackProcesses[sessionId] = process;
                logger.LogInformation("Overlay started (fallback mode) for session {SessionId}, pid {Pid}",
                    sessionId, process.Id);
            }
            else
            {
                logger.LogWarning("Fallback Process.Start returned false for session {SessionId}", sessionId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallback process start failed for session {SessionId}", sessionId);
        }
    }


    public void StopForSession(uint sessionId)
    {
        if (_processHandlesBySession.Remove(sessionId, out var handle))
        {
            // no graceful-shutdown IPC message yet, terminate for now.
            // swap this in future to "please exit" pipe message once that contract exists,
            // it avoids re-paying process/tray/D3D init cost on every lock cycle.
            TerminateProcess(handle, 0);
            CloseHandle(handle);
            logger.LogInformation("Overlay stopped for session {SessionId}", sessionId);
            return;
        }

        if (_fallbackProcesses.Remove(sessionId, out var fallbackProcess))
        {
            try
            {
                if (!fallbackProcess.HasExited)
                    fallbackProcess.Kill();
                fallbackProcess.Dispose();
                logger.LogInformation("Overlay (fallback) stopped for session {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to stop fallback overlay for session {SessionId}", sessionId);
            }
        }
    }

    public void StopAll()
    {
        foreach (var sessionId in _processHandlesBySession.Keys.ToArray())
            StopForSession(sessionId);

        foreach (var sessionId in _fallbackProcesses.Keys.ToArray())
            StopForSession(sessionId);
    }

    private string GetPackageFamilyName()
    {
        try
        {
            var dirName = Path.GetFileName(AppContext.BaseDirectory.TrimEnd('\\', '/'));
            // Saku-Overclock-App_1.1.29.0_x64__8fghyvnsg2qm8
            var parts = dirName.Split("__");
            if (parts.Length == 2)
            {
                var firstUnderscore = parts[0].IndexOf('_');
                if (firstUnderscore > 0)
                {
                    var name = parts[0][..firstUnderscore];
                    return $"{name}_{parts[1]}"; // Ret Saku-Overclock-App_8fghyvnsg2qm8
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get package family name");
        }
        return string.Empty;
    }


    #region Native methods

    private const string Wtsapi32 = "wtsapi32.dll";
    private const string Advapi32 = "advapi32.dll";
    private const string Userenv = "userenv.dll";
    private const string Kernel32 = "kernel32.dll";

    private const uint TokenAllAccess = 0x000F01FF;
    private const uint TokenDuplicate = 0x0002;
    private const uint TokenQuery = 0x0008;
    private const uint ProcessQueryInformation = 0x0400;

    internal enum SecurityImpersonationLevel
    {
        SecurityAnonymous, SecurityIdentification, SecurityImpersonation, SecurityDelegation
    }

    internal enum TokenType { TokenPrimary = 1, TokenImpersonation = 2 }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ProcessInformation
    {
        public nint hProcess;
        public nint hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct Startupinfow
    {
        public uint cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public uint dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars;
        public uint dwFillAttribute, dwFlags;
        public ushort wShowWindow, cbReserved2;
        public nint lpReserved2;
        public nint hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WtsSessionInfo
    {
        public uint SessionId;
        public nint pWinStationName;
        public int State; // WTS_CONNECTSTATE_CLASS -- 0 == WTSActive
    }

    [LibraryImport(Wtsapi32, EntryPoint = "WTSQueryUserToken", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool WtsQueryUserToken(uint sessionId, out nint token);

    [LibraryImport(Advapi32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DuplicateTokenEx(
        nint hExistingToken, uint dwDesiredAccess, nint lpTokenAttributes,
        SecurityImpersonationLevel impersonationLevel, TokenType tokenType,
        out nint phNewToken);

    [LibraryImport(Userenv, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CreateEnvironmentBlock(out nint lpEnvironment, nint hToken, [MarshalAs(UnmanagedType.Bool)] bool bInherit);

    [LibraryImport(Userenv, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyEnvironmentBlock(nint lpEnvironment);

    [DllImport(Advapi32, EntryPoint = "CreateProcessAsUserW", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern bool CreateProcessAsUser(
        nint hToken, string lpApplicationName, string? lpCommandLine,
        nint lpProcessAttributes, nint lpThreadAttributes,
        bool bInheritHandles, uint dwCreationFlags,
        nint lpEnvironment, string? lpCurrentDirectory,
        ref Startupinfow lpStartupInfo, out ProcessInformation lpProcessInformation);

    [LibraryImport(Kernel32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseHandle(nint hObject);

    [LibraryImport(Kernel32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool TerminateProcess(nint hProcess, uint uExitCode);

    [LibraryImport(Kernel32, SetLastError = true)]
    internal static partial nint OpenProcess(
        uint dwDesiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
        uint dwProcessId);

    [LibraryImport(Advapi32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool OpenProcessToken(
        nint processHandle, uint desiredAccess, out nint tokenHandle);

    [LibraryImport(Wtsapi32, EntryPoint = "WTSEnumerateSessionsW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool WtsEnumerateSessionsW(
        nint hServer, uint reserved, uint version, out nint ppSessionInfo, out uint pCount);

    [LibraryImport(Wtsapi32, EntryPoint = "WTSFreeMemory")]
    internal static partial void WTSFreeMemory(nint pMemory);

    internal static IEnumerable<uint> GetActiveConsoleSessionIds()
    {
        // 0 == WTS_CURRENT_SERVER_HANDLE
        if (!WtsEnumerateSessionsW(0, 0, 1, out var pSessionInfo, out var count))
            yield break;

        try
        {
            var sessionSize = Marshal.SizeOf<WtsSessionInfo>();
            for (var i = 0; i < count; i++)
            {
                var info = Marshal.PtrToStructure<WtsSessionInfo>(pSessionInfo + i * sessionSize);
                if (info.State == 0) // WTSActive
                    yield return info.SessionId;
            }
        }
        finally
        {
            WTSFreeMemory(pSessionInfo);
        }
    }

    [ComImport, Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
    private class ApplicationActivationManager { }

    internal enum ActivateOptions { None = 0, DesignMode = 1, NoErrorUi = 2, NoSplashScreen = 4 }

    [ComImport, Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IApplicationActivationManager
    {
        [PreserveSig]
        int ActivateApplication([In] string appUserModelId, [In] string arguments,
            [In] ActivateOptions options, [Out] out uint processId);

        [PreserveSig]
        int ActivateForFile([In] string appUserModelId, [In] IntPtr itemArray,
            [In] string verb, [Out] out uint processId);

        [PreserveSig]
        int ActivateForProtocol([In] string appUserModelId, [In] IntPtr itemArray,
            [Out] out uint processId);
    }
    
    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(
        ref Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        ref Guid riid,
        out IntPtr ppv);

    private const uint ClsctxLocalServer = 4;

    private static readonly Guid ClsidApplicationActivationManager =
        new("45BA127D-10A8-46EA-8AB7-56EA9078943C");

    private static readonly Guid IidApplicationActivationManager =
        new("2E941141-7F97-4756-BA1D-9DECDE894A3D");

    private IApplicationActivationManager? CreateActivationManager()
    {
        var clsid = ClsidApplicationActivationManager;
        var iid = IidApplicationActivationManager;

        var hr = CoCreateInstance(ref clsid, IntPtr.Zero, ClsctxLocalServer, ref iid, out var ptr);
        if (hr < 0)
        {
            logger.LogWarning("CoCreateInstance(ApplicationActivationManager) failed: 0x{Hr:X8}", hr);
            return null;
        }

        try
        {
            return (IApplicationActivationManager)Marshal.GetObjectForIUnknown(ptr);
        }
        finally
        {
            Marshal.Release(ptr);
        }
    }

    #endregion
}