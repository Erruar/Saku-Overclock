using System.Management;
using System.Runtime.InteropServices;
using ZenStates.Core;

namespace Saku_Overclock.SMUEngine;
public static class RyzenADJWrapper
{
    private const string DllName = "libryzenadj.dll";
    private static bool IsDllRunning;
    private static bool IsTableRunning;
    private static IntPtr runningLibRyzenAdjIntPtr = -1;
    private static List<double> currentCPUClocks = [];
    private static float currentCPULoad;
    private static int globalCoreCounter = -1;
    private static int globalCPUDetectionMethod;
    public enum RyzenFamily
    {
        WAIT_FOR_LOAD = -2,
        FAM_UNKNOWN = -1,
        FAM_RAVEN = 0,
        FAM_PICASSO,
        FAM_RENOIR,
        FAM_CEZANNE,
        FAM_DALI,
        FAM_LUCIENNE,
        FAM_VANGOGH,
        FAM_REMBRANDT,
        FAM_MENDOCINO,
        FAM_PHOENIX,
        FAM_HAWKPOINT,
        FAM_STRIXPOINT,
        FAM_END
    }
    #region DLL Init
    public static IntPtr Init_ryzenadj()
    {
        if (!IsDllRunning)
        {
            var currentIntPtr = init_ryzenadj();
            IsDllRunning = true;
            runningLibRyzenAdjIntPtr = currentIntPtr;
            return currentIntPtr;
        }
        return runningLibRyzenAdjIntPtr;
    }
    public static IntPtr Init_Table(IntPtr ryzenAccess)
    {
        try
        {
            if (IsDllRunning && !IsTableRunning && ryzenAccess != 0x0)
            {
                IsTableRunning = true;
                return init_table(ryzenAccess);
            }

            return 0;
        }
        catch
        {
            return 0;
        }
    }
    public static void Cleanup_ryzenadj(IntPtr ry)
    {
        if (IsDllRunning)
        {
            cleanup_ryzenadj(ry);
            IsDllRunning = false;
            IsTableRunning = false;
        }
    }
    #endregion
    #region DLL Imports
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern IntPtr init_ryzenadj();

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern void cleanup_ryzenadj(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern RyzenFamily get_cpu_family(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int get_bios_if_ver(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int init_table(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern uint get_table_ver(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern ulong get_table_size(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern IntPtr get_table_values(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int refresh_table(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int set_stapm_limit(IntPtr ry, uint value);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int set_fast_limit(IntPtr ry, uint value);


    // Добавление оставшихся функций
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_stapm_limit(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_stapm_value(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_fast_limit(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_fast_value(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_slow_limit(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_slow_value(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_apu_slow_limit(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_apu_slow_value(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_vrm_current(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_vrm_current_value(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_vrmsoc_current(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_vrmsoc_current_value(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_vrmmax_current(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_vrmmax_current_value(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_vrmsocmax_current(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_vrmsocmax_current_value(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_tctl_temp(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_tctl_temp_value(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_apu_skin_temp_limit(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_apu_skin_temp_value(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_dgpu_skin_temp_limit(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_dgpu_skin_temp_value(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_psi0_current(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_psi0soc_current(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_stapm_time(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_slow_time(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_cclk_setpoint(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_cclk_busy_value(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_core_clk(IntPtr ry, uint value);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_core_volt(IntPtr ry, uint value);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_core_power(IntPtr ry, uint value);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_core_temp(IntPtr ry, uint value);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_l3_clk(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_l3_logic(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_l3_vddm(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_l3_temp(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_gfx_clk(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_gfx_temp(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_gfx_volt(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_mem_clk(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_fclk(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_soc_power(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_soc_volt(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_socket_power(IntPtr ry);
    #endregion
    #region DLL voids
    public static RyzenFamily Get_cpu_family(IntPtr ry)
    {
        if (ry == 0x0) { return RyzenFamily.FAM_UNKNOWN; }
        return get_cpu_family(ry);
    }
    public static int Get_bios_if_ver(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_bios_if_ver(ry);
    }
    public static uint Get_table_ver(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_table_ver(ry);
    }
    public static ulong Get_table_size(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_table_size(ry);
    }
    public static IntPtr Get_table_values(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_table_values(ry);
    }
    public static int Refresh_table(IntPtr ry)
    {
        if (ry == 0x0) { return -2; }
        return refresh_table(ry);
    }
    public static int Set_stapm_limit(IntPtr ry, uint value)
    {
        if (ry == 0x0) { return 0; }
        return set_stapm_limit(ry, value);
    }
    public static int Set_fast_limit(IntPtr ry, uint value)
    {
        if (ry == 0x0) { return 0; }
        return set_fast_limit(ry, value);
    }
    // Добавление оставшихся функций

    public static float Get_stapm_limit(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_stapm_limit(ry);
    }
    public static float Get_stapm_value(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_stapm_value(ry);
    }
    public static float Get_fast_limit(IntPtr ry)
    {
        return CpuFrequencyManager.GetFastLimit(ry);
    }
    public static float Get_fast_value(IntPtr ry)
    {
        return CpuFrequencyManager.GetFastValue(ry);
    }
    public static float Get_slow_limit(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_slow_limit(ry);
    }
    public static float Get_slow_value(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_slow_value(ry);
    }
    public static float Get_apu_slow_limit(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_apu_slow_limit(ry);
    }
    public static float Get_apu_slow_value(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_apu_slow_value(ry);
    }
    public static float Get_vrm_current(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_vrm_current(ry);
    }
    public static float Get_vrm_current_value(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_vrm_current_value(ry);
    }
    public static float Get_vrmsoc_current(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_vrmsoc_current(ry);
    }
    public static float Get_vrmsoc_current_value(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_vrmsoc_current_value(ry);
    }
    public static float Get_vrmmax_current(IntPtr ry)
    {
        return CpuFrequencyManager.GetVRMEDCLimit(ry);
    }
    public static float Get_vrmmax_current_value(IntPtr ry)
    {
        return CpuFrequencyManager.GetVRMEDCValue(ry);
    }
    public static float Get_vrmsocmax_current(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_vrmsocmax_current(ry);
    }
    public static float Get_vrmsocmax_current_value(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_vrmsocmax_current_value(ry);
    }
    public static float Get_tctl_temp(IntPtr ry)
    {
        return CpuFrequencyManager.GetTCTLLimit(ry);
    }
    public static float Get_tctl_temp_value(IntPtr ry)
    {
        return CpuFrequencyManager.GetTCTLValue(ry);
    }
    public static float Get_apu_skin_temp_limit(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_apu_skin_temp_limit(ry);
    }
    public static float Get_apu_skin_temp_value(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_apu_skin_temp_value(ry);
    }
    public static float Get_dgpu_skin_temp_limit(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_dgpu_skin_temp_limit(ry);
    }
    public static float Get_dgpu_skin_temp_value(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_dgpu_skin_temp_value(ry);
    }
    public static float Get_psi0_current(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_psi0_current(ry);
    }
    public static float Get_psi0soc_current(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_psi0soc_current(ry);
    }
    public static float Get_stapm_time(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_stapm_time(ry);
    }
    public static float Get_slow_time(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_slow_time(ry);
    }
    public static float Get_cclk_setpoint(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_cclk_setpoint(ry);
    }
    public static float Get_cclk_busy_value(IntPtr ry)
    {
        return CpuFrequencyManager.GetCoreLoad(ry);
    }
    public static float Get_core_clk(IntPtr ry, uint value)
    {
        return CpuFrequencyManager.GetCoreClock(ry, value); 
    }
    public static float Get_core_volt(IntPtr ry, uint value)
    {
        if (ry == 0x0) { return 0; }
        return get_core_volt(ry, value);
    }
    public static float Get_core_power(IntPtr ry, uint value)
    {
        if (ry == 0x0) { return 0; }
        return get_core_power(ry, value);
    }
    public static float Get_core_temp(IntPtr ry, uint value)
    {
        if (ry == 0x0) { return 0; }
        return get_core_temp(ry, value);
    }
    public static float Get_l3_clk(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_l3_clk(ry);
    }
    public static float Get_l3_logic(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_l3_logic(ry);
    }
    public static float Get_l3_vddm(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_l3_vddm(ry);
    }
    public static float Get_l3_temp(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_l3_temp(ry);
    }
    public static float Get_gfx_clk(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_gfx_clk(ry);
    }
    public static float Get_gfx_temp(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_gfx_temp(ry);
    }
    public static float Get_gfx_volt(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_gfx_volt(ry);
    }
    public static float Get_mem_clk(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_mem_clk(ry);
    }
    public static float Get_fclk(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_fclk(ry);
    }
    public static float Get_soc_power(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_soc_power(ry);
    }
    public static float Get_soc_volt(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_soc_volt(ry);
    }
    public static float Get_socket_power(IntPtr ry)
    {
        if (ry == 0x0) { return 0; }
        return get_socket_power(ry);
    }
    public static string GetCPUCodename()
    {
        var ryzenAccess = Init_ryzenadj(); var endname = string.Empty;
        if (ryzenAccess != IntPtr.Zero)
        {
            var family = get_cpu_family(ryzenAccess);
            endname = family.ToString();
        }
        return endname;
    }
    #endregion

    public class CpuFrequencyManager
    {
        private static readonly Dictionary<int, int> _coreIndexMap = [];
        private static int _cpuLoadIndex;
        private static float[]? _cachedTable;
        // Флаг для проверки готовности таблицы
        private static bool _isInitialized;

        public static void InitializeCoreIndexMapAsync(int coreCounter)
        {
            globalCPUDetectionMethod = 1;
            if (_isInitialized) { return; }
            _coreIndexMap.Clear();
            // Асинхронная загрузка WMI
            if (currentCPUClocks.Count == 0) { currentCPUClocks = GetSystemInfo.GetCurrentClockSpeedsMHz(coreCounter); }
            if (currentCPULoad == 0) { currentCPULoad = (float)GetSystemInfo.GetCurrentUtilisation(); }
            if (coreCounter == 0) 
            {
                if (globalCoreCounter == -1 || globalCoreCounter == 0)
                {
                    var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Processor");
                    foreach (var queryObj in searcher.Get().Cast<ManagementObject>())
                    {
                        coreCounter = Convert.ToInt32(queryObj["NumberOfCores"]);
                    }
                    globalCoreCounter = coreCounter;
                }
                coreCounter = globalCoreCounter;
            }
            for (var core = 0; core < coreCounter; core++)
            {
                var index = FindIndexInPowerTable(currentCPUClocks[core]);
                if (index >= 0)
                {
                    _coreIndexMap[core] = index;
                }
            }
            _cpuLoadIndex = FindIndexInPowerTable(currentCPULoad);
            
            _isInitialized = true;
        }
        public static async Task AsyncWMIGetCoreFreq(int coreCounter)
        {
            globalCPUDetectionMethod = 2; 
            await Task.Run(() => 
            {
                currentCPUClocks = GetSystemInfo.GetCurrentClockSpeedsMHz(coreCounter);
                currentCPULoad = (float)GetSystemInfo.GetCurrentUtilisation();
            });
        }

        public static void RefreshPowerTable(Cpu cpu)
        {
            try
            {
                cpu ??= CpuSingleton.GetInstance(); 
                cpu?.RefreshPowerTable();
                _cachedTable = cpu?.powerTable.Table;
            }
            catch { }
        }

        private static int FindIndexInPowerTable(double clockSpeedMHz)
        {
            if (_cachedTable == null)
            {
                return -1;
            }
            for (var i = 0; i < _cachedTable.Length; i++)
            {
                if (Math.Abs(_cachedTable[i] - clockSpeedMHz) < 0.100 && _cachedTable[i] > 0.38) // Допустимая погрешность
                {
                    return i;
                }
            }
            return -1;
        }
        public static float GetCoreLoad(IntPtr ry)
        {
            if (ry != IntPtr.Zero)
            {
                return get_cclk_busy_value(ry);
            }
            if (!_isInitialized)
            {
                return currentCPULoad;
            }
            if (_cpuLoadIndex == 0)
            {
                return 0;
            }
            if (globalCPUDetectionMethod == 1)
            {
                return (_cachedTable != null && _cachedTable.Length >= _cpuLoadIndex && _cpuLoadIndex >= 0) ? _cachedTable[_cpuLoadIndex] : 0;
            }

            return currentCPULoad;
        }
        public static float GetFastLimit(IntPtr ry)
        {
            if (ry != IntPtr.Zero)
            {
                return get_fast_limit(ry);
            }
            return _cachedTable != null ? _cachedTable[2] : 0;
        }
        public static float GetFastValue(IntPtr ry)
        {
            if (ry != IntPtr.Zero)
            {
                return get_fast_value(ry);
            }
            return _cachedTable != null ? _cachedTable[3] : 0;
        }
        public static float GetVRMEDCLimit(IntPtr ry)
        {
            if (ry != IntPtr.Zero)
            {
                return get_vrmmax_current(ry);
            }
            return _cachedTable != null ? _cachedTable[8] : 0;
        }
        public static float GetVRMEDCValue(IntPtr ry)
        {
            if (ry != IntPtr.Zero)
            {
                return get_vrmmax_current_value(ry);
            }
            return _cachedTable != null ? _cachedTable[9] : 0;
        }
        public static float GetTCTLLimit(IntPtr ry)
        {
            if (ry != IntPtr.Zero)
            {
                return get_tctl_temp(ry);
            }
            return _cachedTable != null ? _cachedTable[10] : 0;
        }
        public static float GetTCTLValue(IntPtr ry)
        {
            if (ry != IntPtr.Zero)
            {
                return get_tctl_temp_value(ry);
            }
            return _cachedTable != null ? _cachedTable[11] : 0;
        }

        public static float GetCoreClock(IntPtr ry, uint core)
        {
            if (ry != IntPtr.Zero)
            {
                return get_core_clk(ry, core);
            }

            if (!_isInitialized)
            {
                return (float)currentCPUClocks[(int)core];
            }
            if (!_coreIndexMap.TryGetValue((int)core, out var value))
            {
                return -1;
            }

            if (_cachedTable == null || value >= _cachedTable.Length)
            {
                SendSmuCommand.TraceIt_TraceError("Cached table is invalid or out of range.");
                return -1;
            } 
            if (_cachedTable[value] >= 7 && globalCPUDetectionMethod == 1) 
            {
                foreach(var el in _cachedTable)
                {
                    if (el < 7)
                    {
                        return el;
                    }
                }
            }
            if (globalCPUDetectionMethod == 1)
            {
                return _cachedTable[value];
            }

            if (currentCPUClocks[(int)core] == 0)
            {
                foreach (var el in currentCPUClocks)
                {
                    if (el > 0)
                    {
                        return (float)el;
                    }
                }
            }
            return (float)currentCPUClocks[(int)core];
        }
    }


}
