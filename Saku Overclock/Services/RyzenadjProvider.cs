using System.Runtime.InteropServices;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.SMUEngine;

namespace Saku_Overclock.Services;

public class RyzenadjProvider : IDataProvider
{
    private IntPtr _rypointer = IntPtr.Zero; // Указатель на внутренние структуры Ryzenadj.
    private const string DllName = "libryzenadj.dll"; // Имя библиотеки Ryzenadj
    public static bool IsPhysicallyUnavailable { get; private set; } = false; // Флаг, указывающий, что Ryzenadj физически недоступен.
    private bool _isDllRunning; // Флаг запущен ли dll
    private bool _isTableRunning; // Флаг запущена ли таблица Power Table внутри dll

    #region Provider Initialization
    /// <summary>
    /// Инициализация Ryzenadj и установка флага его доступности.
    /// Вызывается при создании экземпляра 
    /// </summary>  
    public void Initialize()
    {
        // Здесь вызов инициализации из libryzenadj.dll.
        _rypointer = ExternalRyzenadjInit();

        if (_rypointer == IntPtr.Zero)
        {
            IsPhysicallyUnavailable = true;
        }
    }

    /// <summary>
    /// Метод инициализации libryzenadj.dll.
    /// </summary>
    private IntPtr ExternalRyzenadjInit()
    {
        if (!_isDllRunning)
        {
            var currentIntPtr = init_ryzenadj();
            _isDllRunning = true;
            _rypointer = currentIntPtr;
            return currentIntPtr;
        }
        return _rypointer;
    }
    public IntPtr Init_table(IntPtr ryzenAccess)
    {
        try
        {
            if (_isDllRunning && !_isTableRunning && ryzenAccess != 0x0)
            {
                _isTableRunning = true;
                return init_table(ryzenAccess);
            }

            return 0;
        }
        catch
        {
            return 0;
        }
    }
    public void Cleanup_ryzenadj(IntPtr ry)
    {
        if (_isDllRunning)
        {
            cleanup_ryzenadj(ry);
            _isDllRunning = false;
            _isTableRunning = false;
        }
    }
    #endregion

    #region Get Provider Data
    public async Task<SensorsInformation> GetDataAsync()
    {
        if (_rypointer == IntPtr.Zero && !IsPhysicallyUnavailable)
        {
            Initialize();
        }
        _ = refresh_table(_rypointer);
        // Здесь реализация получения данных через Ryzenadj 
        await Task.Delay(30);
        var (avgCoreClk, avgCoreVolt, clkPerClock, voltPerClock, tempPerClock, powerPerClock) = CalculateCoreMetrics();
        return new SensorsInformation
        {
            CpuFamily = get_cpu_family(_rypointer).ToString(),
            CpuStapmLimit = get_stapm_limit(_rypointer),
            CpuStapmValue = get_stapm_value(_rypointer),
            CpuFastLimit = get_fast_limit(_rypointer),
            CpuFastValue = get_fast_value(_rypointer),
            CpuSlowLimit = get_slow_limit(_rypointer),
            CpuSlowValue = get_slow_value(_rypointer),
            ApuSlowLimit = get_apu_slow_limit(_rypointer),
            ApuSlowValue = get_apu_slow_value(_rypointer),
            VrmTdcValue = get_vrm_current_value(_rypointer),
            VrmTdcLimit = get_vrm_current(_rypointer),
            VrmEdcValue = get_vrmmax_current_value(_rypointer),
            VrmEdcLimit = get_vrmmax_current(_rypointer),
            VrmPsiValue = get_psi0_current(_rypointer),
            VrmPsiSocValue = get_psi0soc_current(_rypointer),
            SocTdcValue = get_vrmsoc_current_value(_rypointer),
            SocTdcLimit = get_vrmsoc_current(_rypointer),
            SocEdcValue = get_vrmsocmax_current_value(_rypointer),
            SocEdcLimit = get_vrmsocmax_current(_rypointer),
            CpuTempValue = get_tctl_temp_value(_rypointer),
            CpuTempLimit = get_tctl_temp(_rypointer),
            ApuTempValue = get_apu_skin_temp_value(_rypointer),
            ApuTempLimit = get_apu_skin_temp_limit(_rypointer),
            DgpuTempValue = get_dgpu_skin_temp_value(_rypointer),
            DgpuTempLimit = get_dgpu_skin_temp_limit(_rypointer),
            CpuStapmTimeValue = get_stapm_time(_rypointer),
            CpuSlowTimeValue = get_slow_time(_rypointer),
            CpuUsage = get_cclk_busy_value(_rypointer),
            ApuFrequency = get_gfx_clk(_rypointer),
            ApuTemperature = get_gfx_temp(_rypointer),
            ApuVoltage = get_gfx_volt(_rypointer),
            MemFrequency = get_mem_clk(_rypointer),
            FabricFrequency = get_fclk(_rypointer),
            SocPower = get_soc_power(_rypointer),
            SocVoltage = get_soc_volt(_rypointer),
            CpuFrequency = avgCoreClk,
            CpuVoltage = avgCoreVolt,
            CpuFrequencyPerCore = clkPerClock,
            CpuPowerPerCore = powerPerClock,
            CpuTemperaturePerCore = tempPerClock,
            CpuVoltagePerCore = voltPerClock
        };
    }

    private (double avgCoreClk, double avgCoreVolt, double[] clkPerClock, double[] voltPerClock, double[] tempPerClock, double[] powerPerClock) CalculateCoreMetrics()
    {
        double sumCoreClk = 0;
        double sumCoreVolt = 0;
        var validCoreCount = 0;
        var validCoreCountVoltage = 0;
        List<double> clkPerClock = [];
        List<double> voltPerClock = [];
        List<double> tempPerClock = [];
        List<double> powerPerClock = [];
        for (uint f = 0; f < 8; f++)
        {
            var clk = Math.Round(get_core_clk(_rypointer, f), 3);
            var volt = Math.Round(get_core_volt(_rypointer, f), 3);
            var pwr = Math.Round(get_core_power(_rypointer, f), 3);
            var temp = Math.Round(get_core_temp(_rypointer, f), 3);
            if (clk > 0) // Исключаем нули и -1
            {
                clkPerClock.Add(clk);
                tempPerClock.Add(temp);
                powerPerClock.Add(pwr);
                sumCoreClk += clk;
                validCoreCount++;
            }
            if (volt > 0)
            {
                voltPerClock.Add(volt);
                sumCoreVolt += volt;
                validCoreCountVoltage++;
            }
        }

        var avgCoreClk = validCoreCount > 0 ? sumCoreClk / validCoreCount : 0;
        var avgCoreVolt = validCoreCountVoltage > 0 ? sumCoreVolt / validCoreCountVoltage : 0;

        return (avgCoreClk, avgCoreVolt, clkPerClock.ToArray(), voltPerClock.ToArray(), tempPerClock.ToArray(), powerPerClock.ToArray());
    }
    #endregion

    #region RyzenADJ usings
    public enum RyzenFamily
    {
        WAIT_FOR_LOAD = -2,
        Unsupported = -1,
        RAVEN = 0,
        PICASSO,
        RENOIR,
        CEZANNE,
        DALI,
        LUCIENNE,
        VANGOGH,
        REMBRANDT,
        MENDOCINO,
        PHOENIX,
        HAWKPOINT,
        STRIXPOINT,
        END
    }

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
   
    #endregion
}