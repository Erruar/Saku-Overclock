using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
namespace Saku_Overclock.SMUEngine;

internal class KernelDriver
{
    private enum ServiceAccessRights : uint
    {
        ServiceAllAccess = 983551u
    }

    private enum ServiceControlManagerAccessRights : uint
    {
        ScManagerAllAccess = 983103u
    }

    private enum ServiceType : uint
    {
        ServiceKernelDriver = 1u,
        // ReSharper disable once UnusedMember.Local
        ServiceFileSystemDriver
    }

    private enum StartType : uint
    {
        // ReSharper disable once UnusedMember.Local
        ServiceBootStart, // ReSharper disable once UnusedMember.Local
        ServiceSystemStart, // ReSharper disable once UnusedMember.Local
        ServiceAutoStart,
        ServiceDemandStart, // ReSharper disable once UnusedMember.Local
        ServiceDisabled
    }

    private enum ErrorControl : uint
    {
        // ReSharper disable once UnusedMember.Local
        ServiceErrorIgnore,
        ServiceErrorNormal, // ReSharper disable once UnusedMember.Local
        ServiceErrorSevere, // ReSharper disable once UnusedMember.Local
        ServiceErrorCritical
    }

    private enum ServiceControl : uint
    {
        ServiceControlStop = 1u, // ReSharper disable once UnusedMember.Local
        ServiceControlPause, // ReSharper disable once UnusedMember.Local
        ServiceControlContinue, // ReSharper disable once UnusedMember.Local
        ServiceControlInterrogate, // ReSharper disable once UnusedMember.Local
        ServiceControlShutdown
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct ServiceStatus
    {
        public uint dwServiceType;

        public uint dwCurrentState;

        public uint dwControlsAccepted;

        public uint dwWin32ExitCode;

        public uint dwServiceSpecificExitCode;

        public uint dwCheckPoint;

        public uint dwWaitHint;
    }

    private enum FileAccess : uint
    {
        // ReSharper disable once UnusedMember.Local
        GenericRead = 2147483648u, // ReSharper disable once UnusedMember.Local
        GenericWrite = 1073741824u
    }

    private enum CreationDisposition : uint
    {
        // ReSharper disable once UnusedMember.Local
        CreateNew = 1u, // ReSharper disable once UnusedMember.Local
        CreateAlways,
        OpenExisting, // ReSharper disable once UnusedMember.Local
        OpenAlways, // ReSharper disable once UnusedMember.Local
        TruncateExisting
    }

    private enum FileAttributes : uint
    {
        FileAttributeNormal = 0x80u
    }

    private static class NativeMethods
    {
        // ReSharper disable once UnusedMember.Local
        private const string Kernel = "kernel32.dll";

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr OpenSCManager(string? machineName, string? databaseName, ServiceControlManagerAccessRights dwAccess);

        [DllImport("advapi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseServiceHandle(IntPtr hScObject);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateService(IntPtr hScManager, string lpServiceName, string lpDisplayName, ServiceAccessRights dwDesiredAccess, ServiceType dwServiceType, StartType dwStartType, ErrorControl dwErrorControl, string? lpBinaryPathName, string? lpLoadOrderGroup, string? lpdwTagId, string? lpDependencies, string? lpServiceStartName, string? lpPassword);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr OpenService(IntPtr hScManager, string lpServiceName, ServiceAccessRights dwDesiredAccess);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteService(IntPtr hService);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool StartService(IntPtr hService, uint dwNumServiceArgs, string[]? lpServiceArgVectors);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ControlService(IntPtr hService, ServiceControl dwControl, ref ServiceStatus lpServiceStatus);

        [DllImport("kernel32.dll")]
        [Obsolete("Obsolete")]
        public static extern bool DeviceIoControl(SafeFileHandle? device, IoControlCode ioControlCode, [In][MarshalAs(UnmanagedType.AsAny)] object? inBuffer, uint inBufferSize, [Out][MarshalAs(UnmanagedType.AsAny)] object? outBuffer, uint nOutBufferSize, out uint bytesReturned, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateFile(string lpFileName, FileAccess dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, CreationDisposition dwCreationDisposition, FileAttributes dwFlagsAndAttributes, IntPtr hTemplateFile);
    }

    private readonly string id;

    private SafeFileHandle? device;
    // ReSharper disable once UnusedMember.Local
    private const int ErrorServiceExists = -2147023823; // ReSharper disable once UnusedMember.Local

    private const int ErrorServiceAlreadyRunning = -2147023840;

    public static bool IsOpen => true;

    public KernelDriver(string id)
    {
        this.id = id;
    }

    public bool Install(string? path, out string? errorMessage)
    {
        var intPtr = NativeMethods.OpenSCManager(null, null, ServiceControlManagerAccessRights.ScManagerAllAccess);
        if (intPtr == IntPtr.Zero)
        {
            errorMessage = "OpenSCManager returned zero.";
            return false;
        }
        var intPtr2 = NativeMethods.CreateService(intPtr, id, id, ServiceAccessRights.ServiceAllAccess, ServiceType.ServiceKernelDriver, StartType.ServiceDemandStart, ErrorControl.ServiceErrorNormal, path, null, null, null, null, null);
        if (intPtr2 == IntPtr.Zero)
        {
            if (Marshal.GetHRForLastWin32Error() == -2147023823)
            {
                errorMessage = "Service already exists";
                return false;
            }
            errorMessage = "CreateService returned the error: " + Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error())?.Message;
            NativeMethods.CloseServiceHandle(intPtr);
            return false;
        }
        if (!NativeMethods.StartService(intPtr2, 0u, null) && Marshal.GetHRForLastWin32Error() != -2147023840)
        {
            errorMessage = "StartService returned the error: " + Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error())?.Message;
            NativeMethods.CloseServiceHandle(intPtr2);
            NativeMethods.CloseServiceHandle(intPtr);
            return false;
        }
        NativeMethods.CloseServiceHandle(intPtr2);
        NativeMethods.CloseServiceHandle(intPtr);
        try
        {
            var fileName = @"\\.\" + id;
            var fileInfo = new FileInfo(fileName);
            var accessControl = fileInfo.GetAccessControl();
            accessControl.SetSecurityDescriptorSddlForm("O:BAG:SYD:(A;;FA;;;SY)(A;;FA;;;BA)");
            fileInfo.SetAccessControl(accessControl);
        }
        catch
        {
            // ignored
        }

        errorMessage = null;
        return true;
    }

    public bool Open()
    {
        device = new SafeFileHandle(NativeMethods.CreateFile(@"\\.\" + id, (FileAccess)3221225472u, 0u, IntPtr.Zero, CreationDisposition.OpenExisting, FileAttributes.FileAttributeNormal, IntPtr.Zero), ownsHandle: true);
        if (!device.IsInvalid)
        {
            return device != null;
        }

        device.Close();
        device.Dispose();
        device = null;
        return device != null;
    }

    [Obsolete("Obsolete")]
    public bool DeviceIoControl(IoControlCode ioControlCode, object? inBuffer)
    {
        return device != null && NativeMethods.DeviceIoControl(device, ioControlCode, inBuffer, (inBuffer != null) ? ((uint)Marshal.SizeOf(inBuffer)) : 0u, null, 0u, out _, IntPtr.Zero);
    }

    [Obsolete("Obsolete")]
    public bool DeviceIoControl<T>(IoControlCode ioControlCode, object? inBuffer, ref T? outBuffer)
    {
        if (device == null)
        {
            return false;
        }
        object? obj = outBuffer;
        var result = obj != null && NativeMethods.DeviceIoControl(device, ioControlCode, inBuffer, (inBuffer != null) ? ((uint)Marshal.SizeOf(inBuffer)) : 0u, obj, (uint)Marshal.SizeOf(obj), out _, IntPtr.Zero);
        outBuffer = (T)obj!;
        return result;
    }

    public void Close()
    {
        if (device == null)
        {
            return;
        }

        device.Close();
        device.Dispose();
        device = null;
    }

    public bool Delete()
    {
        var intPtr = NativeMethods.OpenSCManager(null, null, ServiceControlManagerAccessRights.ScManagerAllAccess);
        if (intPtr == IntPtr.Zero)
        {
            return false;
        }
        var intPtr2 = NativeMethods.OpenService(intPtr, id, ServiceAccessRights.ServiceAllAccess);
        if (intPtr2 == IntPtr.Zero)
        {
            return true;
        }
        var lpServiceStatus = default(ServiceStatus);
        NativeMethods.ControlService(intPtr2, ServiceControl.ServiceControlStop, ref lpServiceStatus);
        NativeMethods.DeleteService(intPtr2);
        NativeMethods.CloseServiceHandle(intPtr2);
        NativeMethods.CloseServiceHandle(intPtr);
        return true;
    }
}