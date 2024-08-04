using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Saku_Overclock.SMUEngine;
public static class RyzenADJWrapper
{
    private const string DllName = "libryzenadj.dll";
    private static bool IsDllRunning = false;
    private static bool IsTableRunning = false;
    private static IntPtr endPtR = -1;
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
    public static IntPtr Init_ryzenadj()
    {
        if (!IsDllRunning)
        {
            var endPtr = init_ryzenadj();
            IsDllRunning = true;
            endPtR = endPtr;
            return endPtr;
        }
        return endPtR;
    }
    public static IntPtr Init_Table(IntPtr ryzenAccess)
    {
        if (IsDllRunning && !IsTableRunning)
        {
            IsTableRunning = true;
            return init_table(ryzenAccess);
        }
        else
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

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern IntPtr init_ryzenadj();

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern void cleanup_ryzenadj(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern RyzenFamily get_cpu_family(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int get_bios_if_ver(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int init_table(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern uint get_table_ver(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern ulong get_table_size(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern IntPtr get_table_values(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int refresh_table(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int set_stapm_limit(IntPtr ry, uint value);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int set_fast_limit(IntPtr ry, uint value);


    // Добавление оставшихся функций
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_stapm_limit(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_stapm_value(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_fast_limit(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_fast_value(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_slow_limit(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_slow_value(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_apu_slow_limit(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_apu_slow_value(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_vrm_current(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_vrm_current_value(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_vrmsoc_current(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_vrmsoc_current_value(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_vrmmax_current(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_vrmmax_current_value(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_vrmsocmax_current(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_vrmsocmax_current_value(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_tctl_temp(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_tctl_temp_value(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_apu_skin_temp_limit(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_apu_skin_temp_value(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_dgpu_skin_temp_limit(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_dgpu_skin_temp_value(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_psi0_current(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_psi0soc_current(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_stapm_time(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_slow_time(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_cclk_setpoint(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_cclk_busy_value(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_core_clk(IntPtr ry, uint value);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_core_volt(IntPtr ry, uint value);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_core_power(IntPtr ry, uint value);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_core_temp(IntPtr ry, uint value);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_l3_clk(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_l3_logic(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_l3_vddm(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_l3_temp(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_gfx_clk(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_gfx_temp(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_gfx_volt(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_mem_clk(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_fclk(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_soc_power(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_soc_volt(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern float get_socket_power(IntPtr ry);

    // Новые функции

    // Тест работы
    public static void ТестРаботы()
    {
        var ryzenAccess = Init_ryzenadj();
        if (ryzenAccess != IntPtr.Zero)
        {
            var family = get_cpu_family(ryzenAccess);
            _ = init_table(ryzenAccess);
            _ = refresh_table(ryzenAccess); 
            Console.WriteLine($"CPU Family: {family}");
            Views.ShellPage.AddNote(DllName, family.ToString() + "\n" + get_stapm_value(ryzenAccess).ToString() + " / " + get_stapm_limit(ryzenAccess).ToString(), Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational);
        }
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
}
