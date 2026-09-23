using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Saku_Overclock.Service;

/// <summary>
///     Owns the lifetime of the per-session "Saku Overclock.Overlay" companion
///     process. One instance is launched per interactive session, running as
///     that session's logged-on user token, never elevated.
/// </summary>
public sealed partial class OverlayProcessManager(ILogger<OverlayProcessManager> logger)
{
    private readonly Dictionary<uint, nint> _processHandlesBySession = new();

    public void StartForSession(uint sessionId)
    {
        if (_processHandlesBySession.ContainsKey(sessionId))
            return; // already running for this session

        if (!WtsQueryUserToken(sessionId, out var userToken))
        {
            logger.LogWarning("WTSQueryUserToken failed for session {SessionId}: {Error}",
                sessionId, Marshal.GetLastWin32Error());
            return;
        }

        try
        {
            if (!DuplicateTokenEx(
                    userToken, TokenAllAccess, 0,
                    SecurityImpersonationLevel.SecurityImpersonation,
                    TokenType.TokenPrimary,
                    out var primaryToken))
            {
                logger.LogWarning("DuplicateTokenEx failed for session {SessionId}: {Error}",
                    sessionId, Marshal.GetLastWin32Error());
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

                if (envBlock != 0)
                    DestroyEnvironmentBlock(envBlock);

                if (!ok)
                {
                    logger.LogError("CreateProcessAsUser failed for session {SessionId}: {Error}",
                        sessionId, Marshal.GetLastWin32Error());
                    return;
                }

                CloseHandle(processInfo.hThread);
                _processHandlesBySession[sessionId] = processInfo.hProcess;
                logger.LogInformation("Overlay started for session {SessionId}, pid {Pid}",
                    sessionId, processInfo.dwProcessId);
            }
            finally
            {
                CloseHandle(primaryToken);
            }
        }
        finally
        {
            CloseHandle(userToken);
        }
    }

    public void StopForSession(uint sessionId)
    {
        if (!_processHandlesBySession.Remove(sessionId, out var handle))
            return;

        // no graceful-shutdown IPC message yet, terminate for now.
        // swap this in future to "please exit" pipe message once that contract exists,
        // it avoids re-paying process/tray/D3D init cost on every lock cycle.
        TerminateProcess(handle, 0);
        CloseHandle(handle);
        logger.LogInformation("Overlay stopped for session {SessionId}", sessionId);
    }
    
    #region Native methods
    
    private const string Wtsapi32 = "wtsapi32.dll";
    private const string Advapi32 = "advapi32.dll";
    private const string Userenv = "userenv.dll";
    private const string Kernel32 = "kernel32.dll";

    private const uint TokenAllAccess = 0x000F01FF;

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

    [LibraryImport(Wtsapi32, SetLastError = true)]
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

    [LibraryImport(Wtsapi32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool WtsEnumerateSessionsW(
        nint hServer, uint reserved, uint version, out nint ppSessionInfo, out uint pCount);

    [LibraryImport(Wtsapi32)]
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
    
    #endregion
}