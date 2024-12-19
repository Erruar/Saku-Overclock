using System.Runtime.InteropServices;

namespace Saku_Overclock.SMUEngine;

public static class AsusWinIOWrapper
{
    private const string DllName = "AWinIo64.dll";
    private static bool IsDllRunning;
    /*Included AsusWinIO64.dll is licenced to (c) ASUSTek COMPUTER INC*/
    /*ASUS System Control Interface is necessary for this software to work - ASUS System Analysis service must be running. It's automatically installed with MyASUS app.*/

    #region DLL Imports
    [DllImport(DllName)]
    private static extern void InitializeWinIo(); // Инит функции

    [DllImport(DllName)]
    private static extern void ShutdownWinIo(); // Выход из функции

    [DllImport(DllName)]
    public static extern int HealthyTable_FanCounts(); // Узнать количество кулеров

    [DllImport(DllName)]
    public static extern void HealthyTable_SetFanIndex(byte index); // Установить индекс текущего кулера для управления установленным кулером

    [DllImport(DllName)]
    public static extern int HealthyTable_FanRPM(); // Установть скорость кулера

    [DllImport(DllName)]
    public static extern void HealthyTable_SetFanTestMode(char mode); // Установить тестовый, диагностический режим

    [DllImport(DllName)]
    public static extern void HealthyTable_SetFanPwmDuty(short duty); // Установить минимальную скорость

    [DllImport(DllName)]
    public static extern ulong Thermal_Read_Cpu_Temperature(); // Узнать текущую температуру процессора
    #endregion
    #region DLL Voids
    public static void Init_WinIo()
    {
        if (!IsDllRunning)
        {
            InitializeWinIo();
            IsDllRunning = true;
        }
    }

    public static void Cleanup_WinIo()
    {
        if (IsDllRunning)
        {
            ShutdownWinIo();
            IsDllRunning = false;
        }
    }
    #endregion
}
