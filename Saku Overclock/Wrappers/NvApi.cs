using System.Runtime.InteropServices;
using System.Text;

namespace Saku_Overclock.Wrappers;

internal static class NvApi
{
    public const int MAX_GPU_UTILIZATIONS = 8;
    public const int MAX_PHYSICAL_GPUS = 64;
    public const int MAX_THERMAL_SENSORS_PER_GPU = 3;
    public const int MAX_GPU_PUBLIC_CLOCKS = 32;
    private const int ShortStringMax = 64;

    private const string DllName = "nvapi.dll";
    private const string DllName64 = "nvapi64.dll";

    public static NvApiEnumPhysicalGpUsDelegate? NvApiEnumPhysicalGpUs
    {
        get;
        private set;
    }

    public static NvApiEnumNvidiaDisplayHandleDelegate? NvApiEnumNvidiaDisplayHandle
    {
        get;
        private set;
    }

    public static NvApiGpuGetThermalSettingsDelegate? NvApiGpuGetThermalSettings
    {
        get;
        private set;
    }

    public static NvApiGpuGetAllClockFrequenciesDelegate? NvApiGpuGetAllClockFrequencies
    {
        get;
        private set;
    }

    public static NvApiGpuGetDynamicPstatesInfoExDelegate? NvApiGpuGetDynamicPstatesInfoEx
    {
        get;
        private set;
    }

    public static NvApiGpuGetMemoryInfoDelegate? NvApiGpuGetMemoryInfo
    {
        get;
        private set;
    }

    public static NvApiGetDisplayDriverVersionDelegate? NvApiGetDisplayDriverVersion
    {
        get;
        private set;
    }

    private static NvApiGpuGetFullNameDelegate? _nvApiGpuGetFullName;

    public static bool IsAvailable
    {
        get;
        private set;
    }

    public static void Initialize()
    {
        try
        {
            var nvApiInitialize = GetDelegate<NvApiInitializeDelegate>(0x0150E828);
            if (nvApiInitialize == null)
            {
                return;
            }

            if (nvApiInitialize() == NvStatus.Ok)
            {
                NvApiEnumPhysicalGpUs = GetDelegate<NvApiEnumPhysicalGpUsDelegate>(0xE5AC921F);
                NvApiEnumNvidiaDisplayHandle = GetDelegate<NvApiEnumNvidiaDisplayHandleDelegate>(0x9ABDD40D);
                NvApiGpuGetThermalSettings = GetDelegate<NvApiGpuGetThermalSettingsDelegate>(0xE3640A56);
                NvApiGpuGetAllClockFrequencies = GetDelegate<NvApiGpuGetAllClockFrequenciesDelegate>(0xDCB616C3);
                NvApiGpuGetDynamicPstatesInfoEx = GetDelegate<NvApiGpuGetDynamicPstatesInfoExDelegate>(0x60DED2ED);
                NvApiGpuGetMemoryInfo = GetDelegate<NvApiGpuGetMemoryInfoDelegate>(0x774AA982);
                NvApiGetDisplayDriverVersion = GetDelegate<NvApiGetDisplayDriverVersionDelegate>(0xF951A4D1);
                GetDelegate<NvApiGpuGetBusIdDelegate>(0x1BE0B8E5);
                GetDelegate<NvApiGpuGetPciIdentifiersDelegate>(0x2DDFB66E);
                _nvApiGpuGetFullName = GetDelegate<NvApiGpuGetFullNameDelegate>(0xCEEE8E9F);

                IsAvailable = true;
            }
        }
        catch
        {
            IsAvailable = false;
        }
    }

    [DllImport(DllName, EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr NvAPI32_QueryInterface(uint interfaceId);

    [DllImport(DllName64, EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr NvAPI64_QueryInterface(uint interfaceId);

    private static T? GetDelegate<T>(uint id) where T : class
    {
        var ptr = Environment.Is64BitProcess ? NvAPI64_QueryInterface(id) : NvAPI32_QueryInterface(id);
        return ptr != IntPtr.Zero ? Marshal.GetDelegateForFunctionPointer(ptr, typeof(T)) as T : null;
    }

    public static NvStatus NvAPI_GPU_GetFullName(NvPhysicalGpuHandle gpuHandle, out string name)
    {
        StringBuilder builder = new(ShortStringMax);
        var status = _nvApiGpuGetFullName?.Invoke(gpuHandle, builder) ?? NvStatus.FunctionNotFound;
        name = builder.ToString();
        return status;
    }

    internal static int MAKE_NVAPI_VERSION<T>(int ver) => Marshal.SizeOf<T>() | (ver << 16);

    // Delegates
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate NvStatus NvApiInitializeDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate NvStatus NvApiGpuGetFullNameDelegate(NvPhysicalGpuHandle gpuHandle, StringBuilder name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate NvStatus NvApiEnumPhysicalGpUsDelegate([Out] NvPhysicalGpuHandle[] gpuHandles, out int gpuCount);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate NvStatus NvApiEnumNvidiaDisplayHandleDelegate(int thisEnum, ref NvDisplayHandle displayHandle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate NvStatus NvApiGpuGetThermalSettingsDelegate(NvPhysicalGpuHandle gpuHandle, int sensorIndex,
        ref NvThermalSettings nvThermalSettings);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate NvStatus NvApiGpuGetAllClockFrequenciesDelegate(NvPhysicalGpuHandle gpuHandle,
        ref NvGpuClockFrequencies clockFrequencies);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate NvStatus NvApiGpuGetDynamicPstatesInfoExDelegate(NvPhysicalGpuHandle gpuHandle,
        ref NvDynamicPStatesInfo nvPStates);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate NvStatus
        NvApiGpuGetMemoryInfoDelegate(NvDisplayHandle displayHandle, ref NvMemoryInfo nvMemoryInfo);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate NvStatus NvApiGetDisplayDriverVersionDelegate(NvDisplayHandle displayHandle,
        [In] [Out] ref NvDisplayDriverVersion nvDisplayDriverVersion);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate NvStatus NvApiGpuGetBusIdDelegate(NvPhysicalGpuHandle gpuHandle, out uint busId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate NvStatus NvApiGpuGetPciIdentifiersDelegate(NvPhysicalGpuHandle gpuHandle, out uint deviceId,
        out uint subSystemId, out uint revisionId, out uint extDeviceId);

    // Enums
    public enum NvStatus
    {
        Ok = 0,
        FunctionNotFound = -136
    }

    public enum NvThermalTarget
    {
        All = 15
    }

    public enum NvThermalController;

    public enum NvGpuPublicClockId
    {
        Graphics = 0,
        Memory = 4
    }

    // Structs
    [StructLayout(LayoutKind.Sequential)]
    public struct NvPhysicalGpuHandle
    {
        private readonly IntPtr ptr;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NvDisplayHandle
    {
        private readonly IntPtr ptr;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct NvThermalSettings
    {
        public uint Version;
        public uint Count;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_THERMAL_SENSORS_PER_GPU)]
        public NvSensor[] Sensor;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct NvSensor
    {
        public NvThermalController Controller;
        public uint DefaultMinTemp;
        public uint DefaultMaxTemp;
        public uint CurrentTemp;
        public NvThermalTarget Target;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct NvGpuClockFrequencies
    {
        public uint Version;
        private readonly uint _reserved;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_GPU_PUBLIC_CLOCKS)]
        public NvGpuClockFrequenciesDomain[] Clocks;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct NvGpuClockFrequenciesDomain
    {
        private readonly uint _isPresent;
        public uint Frequency;

        public bool IsPresent => (_isPresent & 1) != 0;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct NvDynamicPStatesInfo
    {
        public uint Version;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_GPU_UTILIZATIONS)]
        public NvDynamicPState[] Utilizations;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct NvDynamicPState
    {
        public bool IsPresent;
        public int Percentage;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct NvMemoryInfo
    {
        public uint Version;
        public uint DedicatedVideoMemory;
        public uint AvailableDedicatedVideoMemory;
        public uint SystemVideoMemory;
        public uint SharedSystemMemory;
        public uint CurrentAvailableDedicatedVideoMemory;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct NvDisplayDriverVersion
    {
        public uint Version;
        public uint DriverVersion;
        public uint BldChangeListNum;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = ShortStringMax)]
        public string BuildBranch;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = ShortStringMax)]
        public string Adapter;
    }
}