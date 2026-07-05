using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace Saku_Overclock.Service;

public class IpcSecurityService(ILogger<IpcSecurityService> logger) : IIpcSecurityService
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNamedPipeClientProcessId(IntPtr pipeHandle, out uint clientProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern bool QueryFullProcessImageName(
        IntPtr hProcess,
        uint dwFlags,
        IntPtr lpExeName,
        ref uint lpdwordSize);

    [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern uint WinVerifyTrust(IntPtr hwnd, ref Guid pgActionId, IntPtr pWvtData);

    private const uint ProcessQueryLimitedInformation = 0x1000;

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustFileInfo
    {
        public uint StructSize;
        public IntPtr FilePath; // LPCWSTR
        public IntPtr FileHandle; // HANDLE (null)
        public IntPtr Subject; // GUID*  (null)
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustData
    {
        public uint StructSize;
        public IntPtr PolicyCallbackData;
        public IntPtr SIPClientData;
        public uint UIChoice; // 2  = WTD_UI_NONE
        public uint RevocationChecks; // 0  = WTD_REVOKE_NONE
        public uint UnionChoice; // 1  = WTD_CHOICE_FILE
        public IntPtr FileInfo; // WinTrustFileInfo*
        public uint StateAction; // 1  = VERIFY, 2 = CLOSE
        public IntPtr StateData;
        public IntPtr URLReference;
        public uint ProvFlags; // 0x10 = WTD_CACHE_ONLY_URL_RETRIEVAL
        public uint UIContext;
        public IntPtr SignatureSettings;
    }

    private static readonly Guid ActionGenericVerifyV2 =
        new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    private string? _serviceThumbprint;
    private bool _serviceIsSigned;
    private bool _serviceCertChecked;

    private void EnsureServiceCertCached()
    {
        if (_serviceCertChecked) return;
        _serviceCertChecked = true;

        try
        {
            var path = Environment.ProcessPath;
            if (path is not null && VerifyAuthenticode(path))
            {
                using var cert = X509CertificateLoader.LoadCertificateFromFile(path);
                _serviceThumbprint = cert.GetCertHashString();
                _serviceIsSigned = true;
                logger.LogInformation("Service is signed. Thumbprint: {Tp}", _serviceThumbprint);
            }
            else
            {
                logger.LogInformation("Service is unsigned — debug/dev mode active.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Service cert check failed — debug/dev mode active.");
        }
    }

    private static unsafe bool VerifyAuthenticode(string filePath)
    {
        var actionId = ActionGenericVerifyV2;

        fixed (char* pathPtr = filePath)
        {
            var fileInfo = new WinTrustFileInfo
            {
                StructSize = (uint)sizeof(WinTrustFileInfo),
                FilePath = (IntPtr)pathPtr,
            };

            var trustData = new WinTrustData
            {
                StructSize = (uint)sizeof(WinTrustData),
                UIChoice = 2, // WTD_UI_NONE
                RevocationChecks = 0, // WTD_REVOKE_NONE — оффлайн, не проверяем отзыв
                UnionChoice = 1, // WTD_CHOICE_FILE
                FileInfo = (IntPtr)(&fileInfo),
                StateAction = 1, // WTD_STATE-ACTION_VERIFY
                ProvFlags = 0x10, // WTD_CACHE_ONLY_URL_RETRIEVAL
            };

            var result = WinVerifyTrust(IntPtr.Zero, ref actionId, (IntPtr)(&trustData));

            // Освобождаем StateData — иначе утечка внутри wintrust.dll
            trustData.StateAction = 2; // WTD_STATE-ACTION_CLOSE
            WinVerifyTrust(IntPtr.Zero, ref actionId, (IntPtr)(&trustData));

            // 0x00000000 = S_OK          — подпись валидна, файл не изменён
            // 0x80096010 = BAD_DIGEST    — файл изменён после подписания
            // 0x800B0100 = NO-SIGNATURE   — подписи нет
            return result == 0;
        }
    }

    private string? GetClientProcessPath(uint pid)
    {
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (handle == IntPtr.Zero) return null;

        try
        {
            const int bufferSize = 2048;
            var bufferPtr = Marshal.AllocHGlobal(bufferSize * sizeof(char));
            try
            {
                uint size = bufferSize;
                return QueryFullProcessImageName(handle, 0, bufferPtr, ref size)
                    ? Marshal.PtrToStringUni(bufferPtr, (int)size)
                    : null;
            }
            finally
            {
                Marshal.FreeHGlobal(bufferPtr);
            }
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    public bool ValidateClientSignature(NamedPipeServerStream pipe)
    {
        if (pipe.SafePipeHandle.IsInvalid) return false;

        EnsureServiceCertCached();

        if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle.DangerousGetHandle(), out var pid))
        {
            logger.LogWarning("Failed to get client PID.");
            return false;
        }

        var clientPath = GetClientProcessPath(pid);
        if (string.IsNullOrEmpty(clientPath) || !File.Exists(clientPath))
        {
            logger.LogWarning("Client executable path is invalid.");
            return false;
        }

        // 2. Базовая проверка имени — дополнительный фильтр, не основная защита
        var fileName = Path.GetFileName(clientPath);
        if (!string.Equals(fileName, "Saku Overclock.exe", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Unexpected client process name: {Name}", fileName);
            return false;
        }

        // 3. Debug/dev mode — сервис собран без подписи (из исходников / GitHub без сертификата)
        if (!_serviceIsSigned)
        {
            logger.LogInformation("Debug mode: signature check skipped. Client: {Path}", clientPath);
            return true;
        }

        // 4. Production: WinVerifyTrust — подпись есть и файл не изменён
        if (!VerifyAuthenticode(clientPath))
        {
            logger.LogWarning("Client Authenticode failed (unsigned or tampered): {Path}", clientPath);
            return false;
        }

        // 5. Thumbprint клиента должен совпадать с thumbprint сервиса
        //    (оба подписаны одним сертификатом)
        try
        {
            using var clientCert = X509CertificateLoader.LoadCertificateFromFile(clientPath);
            var clientThumbprint = clientCert.GetCertHashString();

            if (!string.Equals(clientThumbprint, _serviceThumbprint, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Thumbprint mismatch. Client: {C} | Service: {S}",
                    clientThumbprint, _serviceThumbprint);
                return false;
            }

            logger.LogInformation("Client verified successfully. Process: {Path}", clientPath);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error comparing client certificate.");
            return false;
        }
    }
}